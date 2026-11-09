# A repository's Daoris configuration read once and indexed (REPOCONFIG1)

**Round 6, proposed for both makers' reads before a decision is written** (2026-11-09). The owner finds reading a repository's
Daoris configuration slow and wants it read once and better indexed. Round 1 (§0 to §5) measured every reader (§1, §2, §3) and
proposed one store every reader passes (§4) and the rows that build it (§5). Both makers read it ([the reads](2026-11-09-repo-config-reads.md))
and found seventeen blocking sequences and thirteen readers it missed; round 2 (§6 to §17) answers them by class, and supersedes
round 1 where it differs. Both read round 2 and found seven more and a leak in each census; round 3 (§18 to §28) closes them by
construction, and supersedes round 2 where it differs. Both read round 3 and found seven more, and a planted member each census
passed; round 4 (§29 to §40) stops following names and classifies every site where bytes or facts come from a file or from git, and
supersedes round 3 where it differs. One maker read round 4 (the other's read stopped at its spend cap) and found five more and the site
census out of proportion; round 5 (§41 to §55) makes the compiler the census, drops round 4's site census, and supersedes round 4 where
it differs. One maker read round 5 and found four more, each a row that could not land as written; round 6 (§56 to §63) closes them by
construction and rewrites §55's rows a2, a3, c1, c2 and d1 in place. The current rows are §55. Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`.
It builds on GITLIB1 ([its design](2026-11-09-git-library-design.md)): the stamp it designed (its §10.3 and §14.5), which round 2
builds first, and its one object read (`GitObjectReads`).

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

*Superseded: the current rows are §55.*

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

*Superseded: the current rows are §55.* *Supersede §5's.* REPOCONFIG1a lands first; b and c follow a and are apart; d follows b and c; e follows b. In every row each moved
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

*Superseded: the current rows are §55.*

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

## Round 4

*Supersedes round 3 where it differs: §23's source half and its run-time halves by §30 to §32, and its parses census's run-time walk
by §33; §19's third point and §21's mismatch, for an ask by blob id, by §35.4; §21's `driver.json` kind by §35.5; §24's `LimitWaits` row
by §35.2; §25's N2 mark by §36; §28 by §40.*

*Round 5 drops §29 to §34's site census (§51) and supersedes §40 by §55.*

Both makers read round 3 ([the reads](2026-11-09-repo-config-reads.md), *Codex, round 3* and *Opus (independent), round 3*). Here
Codex's four blocking gaps are **Codex 1** to **Codex 4**, Opus's three are **X1** to **X3**, and the members each read planted where a
census of round 3 stayed green are *the planted members*. Three rounds closed the censuses' leaks one shape at a time (names through
helpers, then listings, then stats, then metadata), and each round's reads found a shape the names did not reach. Round 4 stops
following names. Written from the code at `b62716d462`.

## 29. In short

- **The census's unit is the site, not the name** (Codex 3, Codex 4, the planted members). Every call the compiler resolves to a file,
  a folder, a parse, a process, git or the git library is a site, and each is the store's, *not a declaration* by a check the census
  computes from that site's own arguments, or exempt by a row. A site with none is red, so a member not yet written fails until it is
  classified, and a reviewer sees how (§30). Some 3,300 sites stand today (§34).
- **Git sites are classified by their argument shape**, one table read by the census and by `GitReadCache`'s admission. An answer whose
  start reads the asking tree's working files (its attributes, `.mailmap`, the index) is never kept, which closes Codex 1 and the blame
  leak by one rule (§31).
- **The run-time half checks the two tables the source half trusts**, by the site each start came from, its ids seeded with every
  accepted and frozen declaration id (§32).
- **An ask by blob id is answered only by bytes that hash to it** (X1, §35.4); `driver.json` has one kind, its other readers' views and
  one writer (X2, §35.5); `LimitWaits` is stamped by the staffing it decides with (Codex 2, §35.2); three second ways move (X3, §35.6).
- **The site census lands in row a**, every unmoved reader a row naming the row that moves it, and each later row deletes its rows (§40).

## 30. The site census

**A site.** `DeclarationSitesCensusTests` builds the driver's and the modules' compilations with Roslyn and walks every operation the
compiler bound: an invocation, an object creation, a property or field reference, and a method group made a delegate
(`paths.Where(File.Exists)`). A site is such an operation whose target is a *sink member*, one of the APIs below or a member of the
sources that forwards a parameter to one. Bound by symbol, never by text, so `using static`, an alias, an extension, an overload, a
generic form and a method group are each the member they bind to. `dynamic`, reflection (`MethodInfo.Invoke`, `Activator`) and a
delegate from outside the sources are sites the census cannot bind, and red.

**The sink members**, each with the role of each parameter (the path or folder; the data written; the input parsed; a program's
arguments):

- System.IO: every member of `File`, `Directory`, `FileInfo`, `DirectoryInfo`, `FileSystemInfo` and `DriveInfo`, properties included
  (`Exists`, `Length`, `LastWriteTimeUtc`, `Attributes`, `LinkTarget`); `File.OpenHandle`; `RandomAccess`; the constructors of
  `FileStream`, `StreamReader` and `StreamWriter` that take a path; `FileSystemWatcher`; `System.IO.Enumeration`; `ZipFile`;
- the parses: `JsonDocument.Parse*`, `JsonNode.Parse`, `JsonSerializer.Deserialize*`, `JsonElement.ParseValue`, a `Utf8JsonReader` made;
- the desktop store: `Microsoft.Data.Sqlite`'s commands, the statement and its values;
- processes: `Process.Start` and `ProcessStartInfo`, which `ProcessStartsCensusTests` holds to the one door, `Tools.Hand`;
- the git library: every member of `LibGit2Sharp`;
- and any member outside the sources handed a value whose path shape (below) is known, so a library that opens a path it is handed
  (`XDocument.Load(path)`) is a site with no list naming it.

The project's own doors are not listed. `AtomicFile`, `HomeWrites`, `JsonFile`, `WorkingTree.GitAsync` and the other git doors,
`GitObjectReads` and `FileGit` are found as forwarders, so each of their calls is a site.

**Summaries, applied at each call.** For each member of the sources the census makes one summary: which parameters reach a sink and in
which role (with the constant segments or arguments the member adds), how its return's path shape is made from its parameters, and
whether its return carries a store answer, of a parameter or of its own asking. A constructor's parameter kept in a read-only field it
alone assigns is the type's, each `new` a call. Summaries are made bottom-up and iterated over recursion until they stop changing; then
every call is judged by its own arguments under the callee's summary. What is judged is always a site.

**The classes.** Each site, its forwarded roles filled with its call's arguments, is one of three.

1. **The store**: it stands in `Workplace/Declarations/`.
2. **Not a declaration**, by one of four checks, each computed from the site:
   - *Its path's shape.* The path is made (through `Path.Combine`, `Path.Join`, concatenation, interpolation, a local assigned once, a
     summary) of a root and a constant tail. A root is a symbol in one table, `PathRoots`, whose every row says whether it is a
     declaration: each `HomeEntry` field by itself (`DriverConfig`, `Files`, `FilesFound`, `FilesAsks` and `Workflows` are; every other
     entry is not); git's directory (`GitBranches.GitDirectory`, `RefFiles`); the process's and the system's folders; the desktop store's
     file. A value from data (a record's field, a listing's element, a request, a process's answer) is no root. The tail is compared
     with `DeclarationNames` case-insensitively, as a Windows volume matches a name. A stat, open or read is not a declaration where its
     root is not one, or where its tail's last segment names none (`.git/HEAD` under any root). An enumeration is not one where its root
     is not, or where its tail names no folder that holds one (a tree's top, the home's `data`, its `workflows`, a tree's `docs`) and its
     pattern cannot match one. `Directory.Exists` and `Directory.CreateDirectory` answer nothing of a file, so they pass on any path but
     a declaration folder; a `DirectoryInfo`'s times and attributes do not, since at a file's path they answer the file's.
   - *Its git shape* (§31).
   - *Its guard.* The path is `NotDeclarationPath.Value`, a type made only by `DeclarationPaths.Outside(path)`, which refuses a path
     whose resolved name is a declaration's (`DeclarationPathException`, naming the store's ask), and by `DeclarationPaths.Drop`, which
     passes a listing less its declarations. A guard stands where data enters (a route's request, a record's path, a listing) and its
     type carries it to every site the value reaches, so one guard serves many sites; a guarded value with a constant tail passes where
     the tail names no declaration.
   - *Its source.* A parse whose input is a constant, a network answer (`HttpContent`, a socket's frame), or the result of a classified
     read in the same member, whose class it takes.
3. **Exempt**, by a row of `DeclarationSitesCensusTests.Exempt`: the member, the sink and its role, how many such sites the member
   holds, and why. A member that gains a site is red until its row's count moves, and a row naming a site gone is red
   (`SourceCensus.Problems`: a member missed, a member gone). An unmoved reader is a row whose why names the row that moves it.

**No answer reaches a site outside the store.** An *answer* is a value a store member returns, carried as the summaries say. An answer in
any role of a site outside the store is red: a path made from a view's `documents.router`, data written to a file or a row, a parse's
input, a git argument. So a declaration's fact copied into a home file or a store row and read back at the next start is red where it is
written, and a store's text parsed again is red where it is parsed. Round 3's keepers census (§23) stays for fields.

**Why the fixed point's context no longer matters.** Round 3 asked whether a name could reach a sink, so a value's property was given to
every parameter and field it passed through: `HomeEntry`'s one constructor takes `driver.json`'s literal, every entry's `New` then bore a
name, and every home file was red. Here no value is followed for a name. A site's root is the symbol in its own argument:
`DaorisHome.Entry(home, HomeEntry.Accounts)` is the `Accounts` row and `HomeEntry.DriverConfig` another, whatever constructor made them.
A summary is a property of a member, applied at each call with that call's arguments, so a helper one caller hands a declaration and
another an account's file is judged twice. A field keeps the union of what is assigned to it: exact for a static entry, and red for a
field two kinds of path share, which is split.

## 31. Git sites, by their shape

A git site is a call of a git door with its arguments filled. The census reads the list as the compiler holds it: a collection or an
array of constants, constant-folded, with spreads of constant arrays (`GitFormats`' `Log`), a list built in the member by `Add` and
`AddRange` folded in order (a loop's adds one repeated hole), and a hole for each value it cannot fold (an id, a path, a name). A helper
that builds the list (`GitFormats.BlameArguments`) is a summary like any other. A list it cannot fold is red. The shape is the subcommand
after the global options (`-c`, `--no-optional-locks`, `--literal-pathspecs`), its options, and its pathspec after `--`.

`GitShapes`, one table in the driver, says for each shape what its answer can carry and what git reads for it beyond the objects and the
stamp's configuration:

| Answer | Shapes | Reads besides |
|---|---|---|
| none of a file | `rev-parse --verify`, `merge-base`, `rev-list`, `for-each-ref`, `symbolic-ref`, `config`, `worktree list`; `log` whose format names only `%H %P %an %ae %aI %cn %ce %cI %s %B` and asks no diff, path or `--follow` | nothing |
| paths | `status`, `ls-files`, `ls-tree`, `diff --name-only`, `--name-status` or `--raw`, `log --name-status`, a write whose answer names paths (a merge's conflicts) | the index and the working tree (a status, `ls-files`, a diff against the tree); the `diff` attribute where renames are scored by content (`-M`, `--follow`, or `diff.renames`, on by default) |
| content | `diff -p` or `--numstat`, `show`, `grep`, `blame`, `cat-file` | the asking tree's attributes and their textconv (a patch, `--numstat`, `grep -I`'s binary rule); blame's `.mailmap`, `mailmap.file` and `blame.ignoreRevsFile`'s file |

A write's effect on the tree is read back by its stat (§13.2), so a write is judged by what its answer carries.

**Classed by shape.** A shape whose answer names no file is *not a declaration*. A paths or content shape is not one where its pathspec
is literal paths none naming a declaration (constants, or guarded values, checked as a path's tail), or where it ends with
`DeclarationNames.Excluded`, the spread of `:(exclude,literal)<name>` for every declaration's name; a start under
`--literal-pathspecs` cannot exclude and is judged by its literal paths. `ls-tree` takes no `:(exclude)` (git refuses the magic there),
so a whole listing is the store's or hands its answer to `DeclarationPaths.Drop` in its own statement (the start's answer the call's
argument, or a local assigned once and read only there), whose entries are `ListedEntry`s. An object read by id (`cat-file`,
`GitObjectReads`) is not a declaration only where its id is a `ListedEntry.Object`; any other id is the store's or a row's. A whole-tree
shape, a glob (`*.json` matches `daoris.json`), a hole in the pathspec, and an id of no listed provenance are the store's or a row's. The
rows are the person's views (the Changes page, a commit's page, a file's history and blame, a file at a commit), which show a declaration
as any file, and a tree's state (a status that judges a tree clean, a merge's conflicts), which must see a changed declaration as any
changed file.

**What `GitReadCache` keeps, by shape** (Codex 1). An answer is kept only where every start its read made had a shape that reads
nothing besides. `GitReads`' seam records each start's arguments and `AnsweredAsync` asks `GitShapes` before it keeps, so admission is
the table's at run time and the census's at compile time, one table. Kept, by commit id under §19's fence: a graph page, a commit's own
fields, a compare's commit list. Asked at every ask: a commit's changes and a compare's (`TreeDiffs.RangeAsync`'s `-p --numstat -M` read
the asking tree's `diff` attribute and its textconv), a file's history (`--follow` scores renames), and blame. So `GitCommitDetail` and
`GitCompare` are kept without their `Changes` and joined with a fresh diff at each ask. `ViewGitCountTests` moves: the graph kept by id
after its first open; a commit's page and a compare one start after the first, their diff; a history and a blame one start every open.
The review graph already places a textconv read in its tree's own program (`Π`, `Workplace/LandedGraph.cs:73`), so the two agree. The
stamp no longer needs `.mailmap` or any attribute file: no kept answer rests on them.

**Git as the oracle.** `GitShapesTests` runs each shape in two trees of one repository that differ in every working file git reads (a
`.gitattributes` marking every path `-diff` with a textconv, `info/attributes`, `core.attributesFile`'s file, a `.mailmap`, the
ignore-revs file, an entry staged otherwise). It is red where a shape the table says reads nothing besides answers differently in the two,
and where a shape it says reads the tree answers alike in every fixture, so the fixture exercises it. It runs against each git Daoris
starts, as §20's oracle does, so a git that mapped `%an` through `.mailmap` would turn it red and move the shape's row.

## 32. What the run-time half keeps

The source half is the guarantee: every site is classified. The run-time half checks the two tables it trusts, `PathRoots` and
`GitShapes`, on the flows the count tests and the bench run, and judges each start by the site it came from.

- **Starts, by their site.** Under the count tests each start keeps its stack (a test seam; the product keeps nothing), and the run-time
  half finds in it the site the census classified: the call where the start's last forwarded role was filled, so a start through
  `FileGit.Ask` is judged at the caller that named its arguments, not at the door. Over `LandGitCountTests`, `TidyGitCountTests`,
  `StartGitCountTests`, `ViewGitCountTests` and the bench's flows (`GitStartTally`), a start from a site the census calls *not a
  declaration* is red where its arguments or batch questions name a declaration's path or object id. The ids are the declaration entries
  at every commit the run's starts named, listed after the run through the store's own listing, and **seeded** with every accepted id
  `driver.json` holds (`integrationGates` and the landed route's) and every frozen id the integration store's epochs hold, so a blob-only
  read with no commit named is seen (Codex 4). A start from the store or a row is not judged: a row is a claim a reviewer read.
- **Opens, held.** Round 3's held declaration files stay as the same check for opens. Metadata is no longer their job: a stat, a time
  or an attribute query passes a held handle (Codex 3), and is judged by its site.
- **The shape table**, by §31's oracle; and **the guard**, which refuses at run time.

What goes: round 3's run-time halves as the census's ground (a start judged by its arguments alone, whoever built it), and the parses
census's run-time walk (§33).

## 33. Each planted member, red

| Planted | Its site | Why no class takes it |
|---|---|---|
| `changed.Any(p => p == DeclarationNames.Gates)` over `diff --name-only` (`FoundReads.cs:78`'s shape) | `FileGit.ChangedAsync`'s `diff --name-only -z --no-renames <from> <to> --` | a paths shape over the whole tree, no exclusion, not the store: red whatever its caller compares |
| `JsonDocument.Parse(answer.Text)` outside the store | the parse | its input is an answer, red by §30's rule; nor is it a constant, a network answer or a classified read's result |
| `git grep … <commit> -- '*.json'` (`FileGit.LinesAsync`, its pathspecs `file-signatures.json`'s) | the grep | a content shape whose pathspec is a glob matching `daoris.json`, filled from data: red until it ends with `DeclarationNames.Excluded` |
| a whole `ls-tree -r` matched by name (`LayoutGit.cs:35`'s) | `ls-tree -r -z <commit>` | a whole-tree paths shape `ls-tree` cannot exclude: red until its answer passes `Drop` and the declarations' entries come from the store |
| `File.GetLastWriteTimeUtc` or `FileInfo.Length` on a path built from data (Codex 3) | the stat | its root is data and its tail none: red until the data is guarded where it enters |
| `File.Exists` on a request's path | the stat, its path a route's parameter the framework hands, so data | red until the route guards it, or its reader is a row (the person's read of a file they name) |
| an answer serialised into a home file or a store row and read back | the write (`AtomicFile.WriteText(path, JsonSerializer.Serialize(view))`, a statement's values) | its data carries an answer: red where it is written; the read back, a home entry that is no declaration, is rightly green |
| an `IReadOnlyList<string>` backed by a `List<string>` on a branch no input reaches (`FileAnalysis.WithMembers`' `[.. ]` into `FileRule.Paths`) | the member declarations of each type a kind or a view reaches | **declared types**: every such member is an immutable concrete type (`ImmutableArray<>`, `FrozenDictionary<,>`, `FrozenSet<>`, a string, a primitive, an enum, a compiled `Regex`, a cloned `JsonElement`, or a sealed record of these); an interface or abstract collection, `object`, an array or a delegate is red, since it admits a mutable value on any branch. `[.. ]` into an `ImmutableArray<string>` makes one, so no branch can put a list there, and the run-time walk goes |
| `CultureInfo.CurrentCulture` through `Order()` or `ToLower()` | each call a view's computation or a kind's parse reaches (§23's walk) | red where its target has an overload taking a comparer, a `StringComparison`, a culture or a format provider that the call leaves to the ambient default, over a string, a char or an `IFormattable`; found from the overloads the compiler holds, not a list; and on any reference to `CultureInfo.Current*`, `TimeZoneInfo.Local`, the clock or the environment |
| `blame --porcelain` reading `.mailmap` and the ignore-revs file (`GitReads.Formats.cs:73`) | the blame start | a content shape that reads the tree: never kept (§31); its path a request's, the Git page's row |
| a blob-only read, `IntegrationAcceptance.TextAsync(root, acceptedBlob)` (Codex 4) | `GitObjectReads.TextAsync(root, blob, …)`, its id forwarded from each caller | an object by id of no listed provenance: the store's ask by id or red; at run time the seeded ids see it (§32) |

Each is also red on each exemption left out in turn and on each row's count left unmoved, as round 3's.

## 34. What it costs

Counted on main at `b62716d462` by the text of the calls; the compiler finds a few more (a `FileInfo`'s properties, method groups):

| Sites | Driver | Modules |
|---|---|---|
| filesystem: `File`, `Directory`, a `FileInfo` or `DirectoryInfo` made, a stream on a path, `AtomicFile` | 2,005 | 168 |
| git: the git doors, `GitObjectReads`, the library | 663 | 0 |
| JSON parses | 359 | 50 |
| processes | 53 | 2 |
| the desktop store's statements (one root, `HomeEntry.DesktopDb`; the answer rule over their values) | 261 | 0 |

So some 3,300 sites besides the store's statements, in 656 of the driver's 2,563 files and 60 of the modules' 301. Fifty filesystem sites
drawn at random and read by hand: about three in five are classed by their shape with nothing written (a home entry, git's directory, a
constant tail, a parameter forwarded); about one in three is a path made from data (a record's tree, a listing's element, a harness's
transcript), which takes a guard where the data enters, one guard for every site the value reaches; a few in a hundred are rows. Of the
git sites some two in three are shapes that name no file; of the 200 or so that carry paths or content, about half take the exclusion or
move to the store, and half are rows (the person's views, a tree's state). The estimate is some 150 to 300 sites in 60 to 100 members as
exemptions, beside one row per unmoved reader until its row moves it; the census's first run in row a prints the true table, and its rows
are that run's. The census is one semantic pass over the two projects, which the keepers, parses and views censuses read too. Upkeep: a
new site of a known shape costs nothing, a new place where data enters costs a guard, and a new kind of read costs a row a reviewer reads.

## 35. The blocking findings, each closed

### 35.1 Patches kept without their attributes (Codex 1)

§31: `GitReadCache` keeps only shapes that read nothing besides the objects and the stamp, so a patch-bearing answer is asked at every
ask and the key needs no attribute. Rejected: keying and fencing the attribute inputs (§39). Proof (row a): tree A reads commit C's page;
tree B of the same repository marks a changed file `-diff`; B's page says it is binary, as git says in B, its fields from the kept page
and its changes fresh; an attribute file edited between two asks in one tree, the second ask answering the edit.

### 35.2 `LimitWaits`' skip and `role_staffing` (Codex 2, B1's remainder)

`LookAsync`'s skip (`Course/Chat/LimitWaits.cs:90`) compares `Stamp()`, whose `driver.json` part is the file's length and write time
(`:395`), while the decisions after it (`OneAsync`, `LimitMove.ElsewhereAsync`, `RoleOrders.Arranged`) take the staffing
`DriverConfig.Load` lays from `role_staffing`. The skip stays and its stamp takes the staffing it decides with: `DriverConfig.Effective`,
which the load sets as it lays the staffing over the kept parse. It is the file's content key and `role_staffing`'s revision in
`store_revisions`, read beside the staffing in one query; the content key alone where the store holds no staffing yet and the file's is
read; and *unversioned* where the store failed and the file's staffing answered, which equals no stamp, so that look never skips. The
look stamps `config.Effective` of the config it is handed, beside its parts that are no declaration (the cooling store, the harness
settings, the account folders), which the census classes by their home entries. Every writer of `role_staffing` moves its revision: the
store's write of its family, or `write.Moved(RoleStaffing.Family)` inside another family's (`StaffingProjection.Rows` in the import).
Proof (row c): `LimitWaitsTests`: a wait recorded and looked at; `role_staffing` changed through `RoleStaffing` with `driver.json` and
every account file untouched; the next look asks the service (`Asked` moves) and moves the conversation as the new staffing says; the
staffing store throwing once, and the look after it not skipping. `RoleStaffingTests`: each writer (a set, an added role's rows, the
import, the projection) moves the revision.

### 35.3 Metadata and blob-only reads (Codex 3, Codex 4)

§30 makes every member of the file system's types a site, properties included, judged by its path's shape or its guard, so no proof
rests on a held handle; §31 makes every object read by id a site, not a declaration only where its id came from a dropped listing, and
§32 seeds the run-time ids with the accepted and frozen ids. Proof (row a): the census red on `File.Exists(pathFromRequest)` and on
`new FileInfo(dataPath).Length`; a planted start tagged with a site the census calls *not a declaration*, reading the accepted id with no
commit named, red at run time.

### 35.4 Replaced bytes answering an accepted blob (X1)

`IntegrationAcceptance.AcceptedTextAsync` compares the tip's entry id with the accepted id (`Workplace/IntegrationAcceptance.cs:97`) and
then reads the text by that id (`:98`, through `TextAsync` at `:84`), which git answers with a replacement's bytes. **An ask by blob id
is answered only by bytes that hash to that id; any other answer is *unread: replaced*, kept for none** (round 2's rule, restored for
asks by id). The store's `ByIdAsync(repository, BlobId, kind)` takes the kept bytes where no replacement can be in force, else git's,
and hashes them before it answers. An ask by path keeps §21's rule: git's bytes answer, kept by their content key and carrying
no id, so no caller that compares ids can take them as the id the tree names. `IntegrationAcceptance` (`AcceptedTextAsync`, `TextAsync`),
`IntegrationPromoteReads.FenceAsync`'s frozen blob, `GeneratedWriters`' accepted blobs and `LandedGates`' text ask by id. So in X1's
sequence the state still reads `Accepted` (the tree names X), the text is *unread: replaced*, integration runs nothing as accepted, and the
person reviewing X reads that git answers other bytes for it here, never Y's text under X's name. Proof: rows a and e (§40), and row b's
sequence on `IntegrationAcceptance`. Round 3's row e asserted git's bytes for a replaced saved blob, the defect.

### 35.5 `driver.json` read and written outside `DriverConfig` (X2)

`GitAccessKeys.Load` (`Workplace/GitAccessKeys.cs:95`) and `GitIdentityUse.Read` (`Workplace/GitIdentityUse.cs:86`) each parse the file;
`DriverConfigFile.Change` (`Workplace/GitHostKeys.cs:79-97`), from `GitAccessActs.Apply` (`:151`) and `GitIdentityActs` (`:407`),
writes it under `KeepLock`. **The kind keeps the immutable JSON beside the record**: `driver.json`'s kind holds the cloned `JsonElement`
and `DriverConfigReader.Parse`'s record, both of the bytes alone. The access levels (`GitAccessKeys.Of`) and the identity pointers
(`GitIdentityUse.Of`) are views over the element, keyed by the content key. **One writer**: the store's, under the file's lock, reads the
file's bytes as they stand (a write never starts from a kept parse), hands a `JsonObject` parsed from them to the change, writes
atomically and drops the path's entry before it returns. `DriverConfigDisk.Save` and the two key edits are its callers, the save's choice
of an edit or a whole write its own; `DriverConfigFile` goes. Exempt beside `WorkflowsStep`: `DriverKeysStep`, `GitPushStep` and
`RulesStep` (`Transitional/Migration/`), the release migration's own reads and writes of the file, deleted with it. Proof (row c): an
access edit and an identity edit through the writer, the next levels and pointers in the same flow the new ones; `GitAccessKeys`',
`GitIdentityUse`'s and the acts' tests unchanged.

### 35.6 Three second ways (X3)

- **`FoundReads.cs:78`**: whether the gates changed between two commits is the store's: the gates entry at each commit from its hashed
  listing, compared by id (absent at one and present at the other is a change; unread at either is one, as a commit git no longer holds
  is today). The files the shipped signatures reach stay `FileGit.ChangedAsync`'s, its start ending with `DeclarationNames.Excluded`.
- **`Service/Domain/LayoutFacts.cs:230-231`**: `LayoutReader.Read` takes the manifest's entry (mode, type) and its view from the store at
  the line's commit, in place of `LayoutGit`'s listing and its own `Json(Text(Manifest))`; `LayoutGit`'s whole listing passes `Drop`, its
  instruction files read by their `ListedEntry` ids. The manifest view gains LayoutFacts' part, added to §13.1's manifest views: whether
  it reads as an object, `harness`, `rooms`, whether `domain` declares, `documents.index` and `documents.router`. `daoris.lock` is no
  declaration of this store (the CLI's lock, which the service reads in its own process) and stays `LayoutGit`'s.
- **`FileGit.LinesAsync`**: its arguments end with `DeclarationNames.Excluded` after the signatures' pathspecs, so the search never reads
  a declaration; the declarations' lines that hold a name are found in the store's bytes by the same fixed-string rule, by line, so the
  analysis's hits stay as they were. One answer can differ: a tree marking a declaration `-diff` hid it from `grep -I`; a declaration is
  text to Daoris, so its lines are searched.

## 36. The points that did not block

- **`CanBeInForce` and `TipOf` share one `packed-refs` parse**, made once a `FileStamp` version: it keeps the `refs/heads/` lines `TipOf`
  reads and whether any line names `refs/replace/`, so the gate's warm ask is the loose folder's stat and a lookup, within row a's 2 ms.
  `GitLibraryAdmission.Replaced`'s own read of the file (`Workplace/Git/GitLibraryAdmission.cs:54`) goes.
- **`documents.gates` goes** (D228). `Review/OpinionPackets.cs:145` honours the manifest's `documents.gates` path where every other reader
  reads the fixed name. The gates are declared at `daoris.gates.json`; `documents.gates` is the router's document role, which the CLI
  writes into the generated table of where things are, and no second place the gates can be declared. `OpinionPackets.RulesIn` reads the
  fixed name through the store (row c), and the manifest view says a `documents.gates` naming another path as the manifest's problem.
- **§19's batch is a form of `GitObjectFormsCensusTests`**: one `cat-file --batch` start asked several questions (a commit, its tree and
  the folders on the way), its answers read by `GitObjectAnswer` and its trees by one tree parser in the store; a second tree parser red.
- **N2's mark lives in the home.** The volume's clock mark is written in the home's `run/` folder (`HomeEntry.ClockMark`, `run/clock.mark`),
  never in a tree or its git directory. It gives the clock of the home's volume, for the home's layers and every tree on that volume. A
  tree on a volume that is neither a local fixed one nor the home's has no mark: no stat there is trusted, each ask hashes its bytes
  (1.4 ms for this repository's four), and §19's fences there admit nothing, so the tracked list and `GitReadCache` ask afresh for a
  repository on a share.

## 37. What the classification cannot see

- **A child process's own reads.** Git reads what its shape's row says, which §31's oracle holds against each git Daoris starts. A
  harness reads its tree as an agent does, which is the agent's reading, not Daoris's configuration. A toolchain question
  (`FoundReads.AskAsync`, through the shell) runs a command the found layer names, and its answer is digested, never parsed. Bounded: 55
  process sites, each a row naming its program, and whatever a child answers reaches Daoris only through a site classified by its source.
- **A library's reads of a path it finds itself**: LibGit2Sharp (its search paths pointed at an empty folder, its admission refusing
  where configuration matters, each call a site), the runtime's own (assemblies, its configuration), WebView2's profile. None names a
  repository's declaration.
- **A row's reason.** The census checks a row's member and count, not its why; a reviewer reads the why, and the run-time half sees a row
  only on the flows the count tests run.
- **A path a volume makes equal by other means**: an 8.3 short name or a stream name (`daoris.json:x`). The tail check is
  case-insensitive and the guard compares the resolved path and a link's final target; a short or stream name is not resolved, and no
  code of Daoris spells one.
- **What crosses another process and comes back**: an answer handed to the service or to a child and read back is the service's record or
  the child's answer, which no reader takes as a declaration (§13.4).

## 38. The reads answered (round 4)

| Finding | Answer | Section | Row |
|---|---|---|---|
| **Codex 1** Patches kept without their attributes | `GitReadCache` keeps by shape; a patch, a history and a blame asked at every ask | §31, §35.1 | a |
| **Codex 2**, **B1**'s remainder: `LimitWaits` blind to `role_staffing` | The skip stamped by `DriverConfig.Effective`: the content key and the staffing's revision; every staffing writer moves it | §35.2 | c |
| **Codex 3** Metadata reads | Every member of the file system's types a site, judged by its path's shape or guard; held handles no longer the proof | §30, §32 | a |
| **Codex 4** Blob-only reads | Every object read by id a site, not a declaration only from a dropped listing; the run-time ids seeded with the accepted and frozen ids | §31, §32 | a |
| **X1** Replaced bytes answer an accepted blob | An ask by id answered only by bytes hashing to it, else *unread: replaced*; rows a and e corrected | §35.4 | a, b, e |
| **X2** `driver.json` outside `DriverConfig` | One kind with its JSON; levels and pointers as views; one writer; three migration steps exempt | §35.5 | c |
| **X3** Three second ways | Entry ids for `FoundReads`; the store's entry and manifest view for `LayoutFacts`; the declarations excluded from the search | §35.6 | b |
| **The planted members** | Each red at its site | §33 | a |
| **The fixed point's context** (`HomeEntry`'s constructor) | The site as the unit: roots by symbol, summaries applied per call | §30 | a |
| **`CanBeInForce`'s `packed-refs`** | One parse with `TipOf` | §36 | a |
| **`documents.gates`** | Goes; the fixed name through the store | §36 | c |
| **§19's batch** | A form of `GitObjectFormsCensusTests` | §36 | a |
| **N2's mark** | In the home's `run/`; a share's stats never trusted | §36 | a |
| **The rows** (both) | The site census in a; c's gates readers in b; row e's cold start corrected | §40 | all |

## 39. Rejected in round 4

- **Names as the census's ground** (rounds 2 and 3): each round's reads planted a shape the names did not reach.
- **A row for every site**: 3,300 rows no reviewer reads. Rules class most sites, and a row is for what no rule proves.
- **Patches kept under a key and a fence of their attributes** (Codex 1's other change): every folder's `.gitattributes` on each changed
  path, the index's copy where a working one is absent, `info/attributes`, `core.attributesFile`, the system file and `attr.tree`; the
  index is an input wherever no working file stands, so the fence would refuse at every stage of a working session.
- **`--attr-source`, `--no-textconv` or a neutral attribute file for the Git page's diffs**: each changes the person's answer.
- **Removing `LimitWaits`' skip** (Codex 2's other change): a service request every look, where the version costs one query.
- **The parses' run-time walk** (round 3's): a branch no input reaches passes it, and declared types fail at compile time.
- **A mismatch as present for an ask by id** (round 3's): X1.
- **The mark in a tree's git directory** (round 3's N2): a write into a repository that is not Daoris's.
- **Held handles as the file census's ground** (round 3's): metadata passes them.

## 40. The rows

*Superseded: the current rows are §55.* *Supersede §28's.* REPOCONFIG1a lands first; b and c follow a and are apart; d follows both; e follows b. c's gates readers (`HeavyGates`,
`TreeLocalInputs`, `ContextBriefs`, `HeavyTold`) move in b, once its parity table proves their views. The site census lands in a, green
with a row for every unmoved site naming the row that moves it; each later row deletes its rows as its sites move under the store, and
adds none but an exemption with its reason. In every row each moved reader's existing tests pass unchanged before a count is lowered, and
the bench is run before and after it: the bench measures, the tests assert.

- **a. The stamp, the gate, the kernel, the site census, and the censuses that hold each move.** GITLIB1's stamp (§7, §20) as §28 says,
  with `GitReadCache` keeping by shape (§31), `GitShapes` and its oracle; `GitReplacements.CanBeInForce` sharing `TipOf`'s `packed-refs`
  parse (§36). The kernel as §28 says, with the ask by id (§35.4) and the clock mark in the home (§36). `DeclarationSitesCensusTests` with
  `PathRoots`, `DeclarationNames.Excluded`, `DeclarationPaths` and its two types, the answer rule and its first run's rows; the run-time
  half by site with its seeds (§32); the keepers census for fields (§23); the parses census by declared types and the views and kinds
  census with the ambient rule (§33). Proof: §28's proofs of a, and: a blob kept, a replacement of it added, asked by id *unread:
  replaced* and by path git's bytes with no id; Codex 1's two trees, each commit's page as git answers in that tree, an attribute edit
  read at the next ask; `GitShapesTests` against each git; the census red on each of §33's members, on each exemption left out and on
  each row's count unmoved; the run-time half red on §35.3's planted start; `GitObjectFormsCensusTests` naming the batch. The bench's
  store rows as §28 says.
- **b. The gates file's one parse, the commit readers, and the gates' tree and checkout readers.** §28's b; then `HeavyGates`,
  `TreeLocalInputs`, `ContextBriefs` and `HeavyTold` over the views the parity table proved; X3's three (§35.6); the asks by id of §35.4.
  Proof: §28's proofs of b, and: X1's sequence, the acceptance's text *unread: replaced*, nothing integrated as accepted and the page
  saying so; the gates' change between two commits found by entry ids; `LayoutReaderTests` unchanged over the store's manifest view;
  the analysis's hits equal before and after the exclusion on every fixture; each moved site's row deleted.
- **c. The checkout and home readers, and the writers.** §28's c without the gates readers; `driver.json`'s kind with its JSON, its
  views and its one writer, the three migration steps exempt (§35.5); `DriverConfig`'s members as frozen types, its builders freezing
  where the compiler finds them; `LimitWaits` stamped by `DriverConfig.Effective` (§35.2); `OpinionPackets` reading the fixed name (§36).
  Proof: §28's proofs of c, and §35.2's and §35.5's; each moved site's row deleted.
- **d. The views, the analysis's kept parts, and the last rows.** §28's d without the readers' censuses, which are a's: §11's views with
  their key tests and N5's key, §12's parts as §19 and §22 restate them, `FilesLooks` naming its line's ref in full. Proof: §28's proofs
  of d but the censuses; the site census holding no row that names a row to move it.
- **e. Saved.** §28's e, its cold start corrected: a blob saved, a replacement of it made while the desktop is down, the cold start's ask
  by id answering *unread: replaced* with nothing loaded under the id, and its ask by path git's bytes with no id; the rest as §28 says.

Nothing was measured anew for round 4 but the sites, counted from the code, and nothing in the product changed.

## Round 5

*Supersedes round 4 where it differs: §29 to §34's site census by §42 to §46, §51 saying what goes and what stays; §32's run-time half by
§46; §35.2's `Effective` by §48; §35.5's kind by §49; §40 by §55.*

One maker read round 4 ([the reads](2026-11-09-repo-config-reads.md), *Opus (independent), round 4*); the other's read stopped at its
spend cap before it answered. The read judged §29 to §34's census out of proportion to what it guards and proposed a smaller construction
with the same guarantee for every reader that names a declaration. Round 5 adopts it. Here the read's blocking findings are **B1** to
**B5** as it numbers them, its two other holes **H1** (`Driver.Host` and `AskConsole`) and **H2** (a start's site lost after an `await`),
and its points that did not block **P1** (the clock mark's twins), **P2** (`Drop`) and **P3** (the search's parity). The code asks for
three refinements, each said where it falls: the names are banned by symbol rather than hidden in an assembly (§43); `driver.json`'s
place is handed out as a handle, never a string (§44); and a whole answer of names passes through the door as git gives it (§45).
Written from the code at `96264878ad`.

## 41. In short

- **A miss costs speed, not correctness** (§42). A reader that passes the store reads fresh bytes; only what is kept can be stale, and the
  keepers, parses and views censuses hold what is kept. So the readers' census must find every reader that *names* a declaration, which
  the compiler can, and gives up a reader that reaches one by data, which costs a read and D228's one way.
- **The compiler is the census** (§43). `Microsoft.CodeAnalysis.BannedApiAnalyzers` makes `DeclarationNames`, the five declaration
  `HomeEntry` rows and the layers' path helpers an error outside the store's folder; a source test is red on a declaration's name written
  out, folded as the compiler folds constants. Each ban lands in the row that moves its symbol's last use.
- **The store resolves its five home entries** (§44): `DaorisHome.Entry`'s probe for them is the store's read, and the `configPath`
  strings threaded through some 100 files become a `DriverConfigFile` handle whose place no caller holds.
- **One door for path-bearing git reads** (§45): `GitTreeReads` builds every git read that carries a pathspec, a `<rev>:<path>` or a
  batch question by path, or lists the tree; a declaration's fact asked through it is the store's.
- **Kept**: round 3's run-time census by path and object id, keyed by the door's scope rather than the stack (§46); the keepers census,
  with version types that are no answer and one holder table (§47); the parses census, each kind entering the store frozen (§50); the
  views and kinds census with §33's ambient rule.
- **`Effective`** carries driver.json's content key and the revisions of `role_staffing`, `roles`, `role_policy` and `role_starts` in one
  query, and a look is due again at the ledger's next day (§48). **driver.json's kind is its syntax**, with five views each holding its
  own problem (§49).
- **The rows** (§55): a1, a2 and a3 in turn; then b beside c1 and c2; d1 and d2 after both; e after b.

## 42. What a miss costs

A declaration read outside the store reads the file or the blob as it stands, so its answer is current. What it costs is the read the
store would have saved, and a second way of reading one file (D228). It makes none of the store's answers stale: a checkout's or the
home's file is trusted by its stat under §9's rule, so a write anything makes, a bypassing writer's included, is read at the next ask;
and a commit's facts are proven by their own bytes (§19), whoever else reads them. What can be stale is what is *kept*: a parse, a view or
bytes held past the version they were read at, or a kind whose parse reads more than its bytes. The keepers (§47), the parses (§50) and
the views and kinds censuses hold those, over everything the store answers; they are the correctness half, and they stay.

Two bounds on the claim. A *writer* that passes the store can lose an update where it does not take the file's lock
(`StandingChoices.Write` loads outside it today, §49); every writer names its file, so the ban moves each to the store's one writer
(§35.5). And a reader that reaches a declaration by data and keeps what it read is a file cache like any other, keyed by its own path,
outside the keepers census, whose answers are the store's (§53).

So the readers' census need not be complete to be correct. It must find every reader that names a declaration, which the compiler does by
symbol and by constant; a reader that reaches one only by data (a listing's element, a request's path, a name assembled at run time) is
given up to the run-time half (§46), which sees it on the flows the count tests and the bench run.

## 43. The compiler as the census

**Banned by symbol, not hidden by an assembly.** Two ways make a name unreachable outside the store, weighed against the code:

| | The store in its own assembly | A banned-symbol analyzer |
|---|---|---|
| The store's dependencies | `DaorisHome`, `AtomicFile`, the git doors (`WorkingTree`, `GitObjectReads`, the scheduler), `DesktopStore` and the parsers' types are all the driver's, which references `Daoris.Contracts` alone: a store below the driver needs them moved below it first, a split of the driver's 2,563 files; a store above it reaches its readers only through a port, and `DriverConfig.Load`'s 159 static calls through a locator | the store stays in the driver, `Workplace/Declarations/`, beside what it reads |
| The five home rows | leave `Platform/DaorisHome.cs`, whose rows `home.test.ts` reads as text, in order, against the CLI's and the service's twins | stay in the table; the ban is on the symbol |
| Whom it holds | other assemblies alone: the modules and the host, never the driver's own files | every project it is added to, the driver's own files among them |
| What it cannot hold | reflection, and a value from data | the same, and a suppression, which a test holds |

So the ban. `Microsoft.CodeAnalysis.BannedApiAnalyzers` is added to the driver, the modules, the headless host and the application, its
version in `Directory.Packages.props`, one `BannedSymbols.txt` in the driver's folder linked into the other three, and RS0030 an error
(`WarningsAsErrors`). A use of a listed symbol is an error in every file but those under `Workplace/Declarations/`, whose own
`.editorconfig` sets RS0030 to none, and `Platform/DaorisHome.cs`, which declares the rows and lists them in `HomeEntry.Table`.
`DeclarationBoundaryTests`, a source census as `GitObjectFormsCensusTests` is, is red on any other way RS0030 is quieted (a
`#pragma warning disable`, a `SuppressMessage`, a `NoWarn`, an `.editorconfig` or `.globalconfig` naming it elsewhere) and on one of the
four projects without the analyzer.

**What is banned, each in the row that moves its last use**, so every row builds green:

| Symbols | Banned in |
|---|---|
| `DeclarationNames.Gates` | b, which moves every reader of the gates file |
| `DeclarationNames.Manifest`, `.Lanes` and `.Rules` | the later of b and c2 |
| `HomeEntry.DriverConfig`, `DriverConfig.ResolvePath`, `DeclarationNames.DriverConfig` | c1 |
| `HomeEntry.Files`, `FilesFound`, `FilesAsks` and `Workflows`; `PersonFiles.PathOf`, `FoundFiles.PathOf`, `FileAsks`' path, `WorkflowStore.PathOf` and `WorkflowStore.Folder`; their names in `DeclarationNames` | c2 |
| `GitObjectName.At`'s form with a path | d1, with the door (§45) |

`DeclarationNames` (a2) holds every name. The constants each reader declares today (`HeavyGates.FileName`, `LineRegistration.ManifestFile`
and `LanesFile`, `RepositoryFilesReader.FileName`, `GeneratedWriters.DeclarationFile`, `PersonFiles.FileName`, the workflows' folder
name in `WorkflowStore`) go in a2, their uses pointed at it, so each declaration has one name to ban.

**The literal check.** `DeclarationLiteralsCensusTests` reads the four projects' compilations, as `StructureAnalysis` makes the driver's,
and is red outside the store's folder on a string whose path tail is a declaration's name: every constant the compiler folds
(`IOperation.ConstantValue`: a literal, a `const`, a concatenation or an interpolation of constants), every literal fragment of an
interpolation or a concatenation that is not constant, and the constant arguments a `Path.Combine` or `Path.Join` call ends with, joined.
A tail is a declaration's where what follows its last `/`, `\` or `:` equals one of `daoris.json`, `daoris.files.json`,
`daoris.gates.json`, `daoris.lanes.json`, `driver.json`, `files.json`, `files-found.json` or `files-asks.json`, without regard to case as
a Windows volume compares names; or where the joined constant arguments end with the workflows' folder under the home (`data/workflows`).
A message that names a file in words has no such tail and is words. Its rows, each a file of the same name that is no repository's
declaration: `EngineProfile.RecordPath` (a browser profile's own `daoris.json`), `ServiceHostLocator` and
`KnowledgeConnector.WorkspaceMarker` (existence walks for the build's workspace), and `DaorisHome.cs`'s table. Planted, each red:
`File.ReadAllText(Path.Combine(root, "daoris.gates.json"))`; `"daoris." + "gates.json"`; `Path.Combine(home, "data", "driver.json")`;
`$"{commit}:daoris.gates.json"`; `Path.Combine(home, "data", "workflows")`; and a row left out. Today 24 lines in 18 files of the
desktop's sources hold a string naming a declaration; a2 points each that names a file at `DeclarationNames`, the four rows' aside.

**What it cannot see**, given up by §42: a name assembled from values that are not constant; a path from data, as
`Modules/Loop/LoopGrounds.cs:218` hashes each uncommitted path of a tree; and the home table's walkers, which take the rows as values
(`SystemWiring`, `SpaceFloor` through `HomeEntry.Named`, `SpaceFree`, `ToolHomeLeftovers`, `WiringPlaces`, `HomePlacing`): they size,
list and move places, reading no content, and where they resolve a declaration's place they reach the store's probe (§44).

**H1.** The headless host carries the analyzer and the literal check, so it names no declaration; 25 of its files hold today one of
driver.json's helpers, which move in c1 (§44). `AskConsole.cs:190-191` reads each file the person names to send with an ask: a path from
data and the person's attachment, as `WorkflowTerminal` reads a file the person names, never Daoris's configuration; it needs no row.

## 44. The home's five entries, resolved by the store (B2)

`DaorisHome.Entry` reads what it locates: `Choose` (`Platform/DaorisHome.cs:283-289`) calls `Holds` (`:299-316`), which enumerates a
folder entry (`:302`) and opens and parses a JSON entry (`:307-309`) to judge whether the new place holds it. **For the five declaration
entries the probe is the store's**: `Holds` asks `RepositoryDeclarations.Holds(entry, place)` where the entry is one of the store's (the
table's file may name them), and the store answers from its own ask of that place: present with a clean syntax for a JSON entry, a
listing that is not empty for the workflows' folder, kept by its stat. So the one resolution stays `DaorisHome`'s, the probe's read is the
store's first read of the file rather than a second, and a table walker that resolves a declaration's place reads it through the store
too. `Holds` stays as it is for every other entry.

**The callers**, counted at `96264878ad` in the driver, the modules, the host and the application: 33 references to the five rows in 25
files outside `DaorisHome.cs` (`DriverConfig` 19, `Workflows` 12, `Files`, `FilesFound` and `FilesAsks` one each); `DriverConfig.ResolvePath`
82 in 50 files; `DriverConfig.HomeOf` 47 in 27; `DriverConfig.Load` 159 calls; `WorkflowStore.PathOf` and `Folder` 15 in 9. The read's
some 60 counts the rows' callers with the helpers they reach. Each stops compiling outside the store when c1 or c2 bans its symbol, so
each moves in that row, by what it does:

| What the caller does | Today | Moves to |
|---|---|---|
| loads a home's choices | `DriverConfig.Load(DriverConfig.ResolvePath(home))`, 23 calls | `DriverConfig.Of(home)`, the store's ask |
| hands driver.json's place to a command, a wiring or a watch | `var configPath = DriverConfig.ResolvePath()`, the `ConfigPath` properties, `config ?? DriverConfig.ResolvePath(home)` parameters | `DriverConfigFile`, a handle the store makes from a home or from the environment, carrying `Home` and, inside the store alone, its place; `DriverConfig.Load(file)` |
| finds the home from the file | `DriverConfig.HomeOf(path)` | `file.Home` |
| stamps the file | `HomeWrites.Stamp(DriverConfig.ResolvePath(home))` (`Course/Chat/LimitWaits.cs:395`), `HomePart.File(configPath)` (`Events/HomeWatch.cs:159`) | the store's version of it (§47) |
| writes it | `StandingChoices.Write(path, …)` (14 callers, `Roles/RoleStaffing.cs:331` among them), `DriverConfigDisk.Save`, `DriverConfigFile.Change` | the store's one writer (§35.5, §49) |
| reads a part of it | `GitAccessKeys.Load` (`Workplace/GitAccessKeys.cs:94`), `GitIdentityUse.Read` (`Workplace/GitIdentityUse.cs:84`), the staffing import (`RoleStaffing.cs:236-242`), the wiring page's facts (`Loop/WiringFiles.cs:118-145`) | the kind's views (§49) |
| reads the workflows, the person's layer, the found layer or the asks | `HomeEntry.Workflows` (12), `WorkflowStore.PathOf`, the layers' own readers | the store's asks, `WorkflowFiles`, `PersonFiles`, `FoundFiles` and `FileAsks` moved under its folder (c2) |

A sentence that must say where the file is takes the handle's words, which name its place for the person; a caller that hands those words
to a file API reads by data (§53). Row a lands green: no row bans a symbol before the row that moves its last use, so a1 to a3 change no
caller of these.

## 45. One door for path-bearing git reads (B1)

`GitTreeReads` (`Workplace/Declarations/`) builds and runs every git read that carries a pathspec, a `<rev>:<path>` argument or a batch
question by path, or that lists the tree: `ls-tree`; `diff`, `log` and `show` asked `--name-only`, `--name-status` or `--raw`; `grep`;
`rev-list` and `log` with a pathspec; and `rev-parse --verify <rev>:<path>`. Its members take typed arguments (a revision, a path, a list
of paths), never an argument list, and each runs inside the store's scope (§46). It answers four ways:

1. **A declaration's own fact, from the store**: its entry at a commit (a `<rev>:<path>`, an `ls-tree` of named paths, a batch question
   by path) from the store's hashed listing; its bytes from the store's ask by path; whether it changed between two commits, by the two
   entries' ids (§35.6); its lines holding a name, from its bytes (below). Every other path of the same ask as git answers it.
2. **A search** (`grep`): git's start with every declaration excluded (`DeclarationNames.Excluded`, the spread of
   `:(exclude,literal)<name>`, the door's own), and the declarations' lines from the store's bytes, so git reads no declaration for it.
3. **A whole answer of names and ids** (a whole `ls-tree -r`, a name diff between commits or against the tree): git's answer whole,
   each declaration in it as the name and id the tree gives. A name and an id are a fact of the tree, of the same object the store's
   listing proves; answering them from the store costs a start for each commit not yet listed and changes no answer, and leaving them
   out changes the answer of every caller that must see a changed declaration as any changed file (P2: the grounds over uncommitted
   paths, `LoopGrounds.cs:214-218`; a merge's conflicts; the Changes page). So nothing is dropped, and `DeclarationPaths.Drop` goes.
4. **History with a pathspec** (`rev-list`, `log`): git's for every path. The store holds no history, and whether a declaration changed
   is the first way's question, never a history's.

**B1's two sequences.** Comparing `rev-parse --verify A:daoris.gates.json` with B's needs the gates' name: written out it is red in the
literal check from a3, through `DeclarationNames.Gates` it does not compile outside the store from b, and a hand-built `<rev>:<path>` is
red in the door's census from d1; asked through the door it is the store's entry at A, compared by id.
`rev-list -1 A..B -- daoris.gates.json` needs the name the same way, and its shape, `rev-list` with a pathspec, is the door's alone from
d1. Handed a path from data, the door asks git for a history, which reads fresh and keeps no version (§42).

**The search's parity** (P3). The declarations' lines are found as today's pipeline finds them (`Workplace/Files/FileGit.cs:52-61`):
`git grep -I -F` matches each line its bytes split at LF; a file holding a NUL in its first 8,000 bytes is binary and gives none; and the
answer is split again by `ReplaceLineEndings` (`:60`), which also breaks a line at a lone CR, NEL, a line or paragraph separator or a
form feed, keeping only the first piece, the one that carries the file's name. So the store matches the names in each LF line by ordinal
and answers that line cut at the first other line ending `ReplaceLineEndings` knows. One answer can differ, as §35.6 said: a declaration
marked `-diff` in the asking tree was hidden from `grep -I`, and is searched now.

**Its census.** `GitTreeReadsCensusTests`, as `GitObjectFormsCensusTests` holds the object reads to one form: outside the door's file,
red on a constant `ls-tree`, `grep`, `--name-only`, `--name-status` or `--raw` in a git argument list (the expression handed to a git door,
or a list its member builds and hands to one); on such a list holding `rev-list` or `log` beside `--`; and on an interpolation or a
concatenation that puts `:` between two values in one, but push's refspecs (`Workplace/GitPushActs.cs:39`,
`Workplace/GitHelperPush.cs:97`), its two rows. A table of subcommands that starts nothing (`GitBounds`, `GitLaunch`, `RefSnapshot`) is
no argument list. `GitObjectName.At`'s form with a path is banned by symbol outside the door. Shown red on B1's two sequences planted
outside the door and on a row left out. Counted by their words today: `ls-tree` on 21 lines in 20 files, a name diff on 45, `grep` on 2, a
`<rev>:<path>` on 15 (11 through `GitObjectName.At`, 4 by hand: `GeneratedWriters.cs:55`, `BacklogMerge.cs:264`, `RepositorySetup.cs:56`,
`EvidenceReader.cs:211`), `rev-list` and `log` with a pathspec on at least 6; d1 moves them, and its first run lists the sites exactly.

## 46. The run-time half, by the door's scope (H2)

Round 4 found a start's site from its stack, and after an `await` inside a forwarder the caller's frame is gone: `GitObjectReads.ReadAsync`
awaits `NamedAsync` (`Workplace/Git/GitObjectReads.cs:28`, again under `GitLazyFetch.Fetching()` at `:32`), which awaits
`rev-parse --verify` (`:66`) and then starts `cat-file` (`:68`) from a continuation holding no frame of the caller that named the path. **The
run-time half is keyed by the door, not the stack.** Every member of the store and of `GitTreeReads` does its work inside
`DeclarationScope`, an `AsyncLocal` it sets on entry, as `GitLazyFetch.Fetching()` scopes its fetching ask (`GitLazyFetch.cs:26`). An
`AsyncLocal` flows across every `await`, so the start at `:68` carries the scope of the ask begun at `:28`. The process gate's tally
(`GitStartTally`) records the scope with each start.

Round 3's census stands on that record (`DeclarationGitCensusTests`), over `LandGitCountTests`, `TidyGitCountTests`,
`StartGitCountTests`, `ViewGitCountTests` and the bench's flows: a start outside the scope is red where its arguments or batch questions
name a declaration's path (a pathspec's element, the path of a `<rev>:<path>`, an `ls-tree` path) or a declaration's object id. The ids
are the declaration entries at every commit the run's starts named, listed after the run through the store's listing, and seeded with
every accepted id driver.json holds and every frozen id the integration store's epochs hold, so a blob-only read is seen (round 4's §32).
A start inside the scope is judged too: one naming a declaration other than in the door's exclusion is red, since the door asks the
store for it. Round 3's held files stand beside it (`DeclarationFileCensusTests`): an open of a declaration's file outside the store on
the bench's flows is red; a stat passes a held handle, and costs a read, no more (§42).

A declaration is judged at run time from the row that bans its name (§43), so both land in a3 green and narrow with each row. Planted in
a3 against a test's set of names, each red: a `cat-file` started after an `await` in a forwarder outside the scope, naming a declaration's
id; a blob-only read of an accepted id with no commit named; an open of a held declaration through a helper that swallows its error.

## 47. Keepers, and versions that are no answer (B3)

**An answer is a parse, a view or bytes** of the store: a kind's value, a view's value, a `DeclarationBytes`, and what a view composes from
them at an ask (`DriverConfig` with its staffing laid over it). **A version is no answer**: `ContentKey`, `BlobId`, `DeclarationVersion` (a
content key, or *absent*, or *unread*), `StoreRevisions` (each family's name and revision), and `Effective`, made of these (§48). The
parses census holds each version type to its declared members (ids, numbers, strings of ids), so no version carries an answer.

The keepers census is round 3's (§23) over answers alone: red on an answer written into a static field, an instance field outside its
constructor, an element of a collection a field holds, a `??=` target or a `Lazy<>`, whatever the field's type; and in a type that asks
the store, on every field the fixed point does not prove answer-free. Beside it, **a holder table**, as `GitKeepersCensusTests` lists git's
holders: a field that keeps an answer for one flow, said with the flow that ends it; red on a holder left out and on a listed one gone. The
members the read named:

| Member | What it keeps | Its place |
|---|---|---|
| `LimitWaits._stamp` (`Course/Chat/LimitWaits.cs:92`, compared at `:90`) | the look's stamp | a `LimitStamp`: `Effective` (§48) beside the parts that are no declaration (the cooling store, the harness settings, the account folders, `:387-409`); a version |
| the pipe's grounds (`Modules/Loop/LoopGrounds.cs:231`) | the checkout manifest's hash until Confirm | the store's `DeclarationVersion` of the checkout's manifest, compared at Confirm with a fresh ask; a version |
| `HomeWatch`'s part stamps (`Events/HomeWatch.cs:27`, `:53`; the config topic at `:159`; the workflows in the gates topic) | each file's stamp, by length and write time | for a declaration, the store's version as text; every other part's stamp as now |
| `WatchRun._config` (`Loop/Watch/WatchRun.cs:42`, set at `:331`) | the look's choices, so a torn file keeps the pace it had | the one holder: one look's `DriverConfig`, swapped whole at each tick and read by that look's wait and link 8's pass (`:233`); the config topic's wake starts the next tick |

So a content key or a store revision held across looks is no keeper, and the one answer held past a call is a listed holder with its
flow.

## 48. `Effective`, every family the move reads (B4)

The limit move reads more than `role_staffing`. `ElsewhereAsync` reads the role catalogue (`RoleCatalog.ReadHome`,
`Course/Chat/LimitMove.cs:585`: the `roles` table, written as the family `roles`, `Roles/RoleCatalog.cs:121`, `:392`) and arranges the
order (`RoleOrders.Arranged`, `LimitMove.cs:589`), which asks the ledger's preview (`Roles/RoleOrders.cs:65`). The preview reads the
role's policy joined with its staffing (`role_policy` and `role_staffing`, `Roles/StaffLedger.cs:36-39`) and its facts: the starts in its
window and today's counts (`role_starts`, `:391-395`), against the day's start by the clock (`:396`). Every write of the desktop store
moves its family's revision in its own transaction (`Store/DesktopStore.cs:231-233`).

**`Effective` is driver.json's content key and the revisions of `role_staffing`, `roles`, `role_policy` and `role_starts`, read in one
query** (`SELECT family, revision FROM store_revisions WHERE family IN (…)`, `Revisions()`'s shape, `DesktopStore.cs:303-305`), and
*unversioned* where the store throws (§35.2). The families are a list beside the move (`LimitMove.Reads`), held by `LimitMoveReadsTests`:
the move run over a store whose queries are recorded, red where a query reads a table of a family the list leaves out. Not every family:
any write (news, acts, deliveries) would move a stamp over all of them, and the look would never skip.

**The clock, found beside it.** Today's counts turn at the day's start, which no write marks, so a wait held by a spent daily budget
skips until `_due`. The look is due again no later than the ledger's next day start where the role's policy counts a day.

Proof (c1), each with driver.json and `role_staffing` untouched and the look moving the conversation as the move then says: a wait with
no allowed ready account, then a policy edit allowing one; a role added to the catalogue that takes the work; a start counted against a
budget; the day turning on a seam clock; the staffing store throwing once, and the look after it not skipping. `LimitMoveReadsTests` red
on a planted query of a family the list leaves out.

## 49. driver.json: its syntax the kind, five views (B5)

The read's sequence needs one correction from the code. `"cap": "4"` does not fail the load today: `Int` reads only a JSON number and
answers none for a string (`Settings/DriverConfigReader.cs:372-375`), so the cap is the default (`:27`). The load fails on a number that
is not whole (`"cap": 4.5`, the throw at `:374`) and on a root that is not an object (`:20`); on the first, `GitAccessKeys.Load`
(`GitAccessKeys.cs:90-102`) and `GitIdentityUse.Read` (`GitIdentityUse.cs:80-92`) still answer their levels and pointers, and on the
second, none. The readers also differ in syntax: the record reads a key written twice as its last value in its first place
(`JsonKeeping.Once`, `DriverConfigReader.cs:16`), while the levels and the pointers parse with `JsonNode.Parse`.

**The kind is the syntax** (§21's pattern): the text as the file reads (UTF-8, a byte-order mark taken), and its JSON as one immutable
element, or its syntax problem with its line and byte. **Five views**, each validating its own part over the kind and answering its own
value or its own problem, as its reader does now:

| View | Today's reader | Its problem |
|---|---|---|
| the record | `DriverConfigReader.Parse` | `DriverConfigUnreadableException`'s words; the load fails |
| the access levels | `GitAccessKeys.Of` | the levels fall to none |
| the identity pointers | `GitIdentityUse.Of` | the pointers fall to none |
| the staffing import's part | `RoleStaffing.Parse` over the bytes read at `RoleStaffing.cs:242` | `StaffingImported.Unread`, with its words |
| the wiring page's facts | `WiringFiles.Written` and `Parses` (`Loop/WiringFiles.cs:118-145`) | `unparsed`, a code, beside the stamp's write time |

The import reads the file under the staffing's lock and writes the store; its read is the store's writer's read of the bytes as they stand
(§35.5), so it never starts from a kept parse.

**Parity is a table**, as §21's: each of today's five readers run over every driver.json the tests write and the mixed-validity fixtures,
its outcome recorded, and each view equal to it cell by cell before that reader's own parse is deleted. The fixtures: `"cap": "4"` beside
an identity pointer (the record's default cap, the pointer resolved); `"cap": 4.5` beside one (the load failing, the pointer resolving); a
malformed pointer beside a valid record; a key written twice; a root that is an array; text that is not JSON; a byte-order mark. A cell
that differs is a change of answer. The one expected is a key written twice: one syntax gives every view the record's rule, and c1 names
that change with its test, or keeps each view's own rule where the table shows a reader depends on it.

**One writer.** `StandingChoices.Write` loads the file (`Settings/StandingChoices.cs:91`) outside the lock `Save` takes (`:92`), so two
writes at once can lose one; under the store's writer the read is inside the lock (§35.5).

## 50. The parses census, landable

Round 4's census by declared types (§33) holds every type a kind or a view reaches to an immutable concrete type. **A kind enters the
store frozen**, in the row that adds it, so the census is green at every row and needs no table of rows:

| Types | Frozen in |
|---|---|
| the rule file's kind, the kernel's first: `FileRule.Paths`, `RepositoryFilesRead.Rules` and `Problems` as `ImmutableArray<>` | a2, with the 9 constructions of `FileRule` in the driver and the 28 in the tests |
| the gates file's syntax and its views; the manifest's and the lanes file's kinds and their commit views | b |
| driver.json's syntax and its five views: `DriverConfig`'s read-only interface members as frozen collections, its builders and its `with` edits making them | c1 |
| the person's and the found layers' kinds; a workflow's kind | c2 |
| a tree's effective rules and §11's other views | d2 |

driver.json's kind itself is one cloned `JsonElement`, an immutable leaf; its views freeze where they enter, in c1.

## 51. Dropped from round 4

§29 to §34's site census goes: the summaries and the three classes (§30), `PathRoots`, `GitShapes` as the census's table (§31), the guard
types `NotDeclarationPath` and `DeclarationPaths` with `Drop` (§30), the run-time half by the stack (§32), and the exemption rows and their
counts (§30, §34). What stays of them: §31's rule for what `GitReadCache` keeps (an answer whose starts read the objects alone; changes,
history and blame asked at every ask, §35.1), held by `GitReadCacheTests`' two trees rather than a table; §33's declared types and its
ambient rule, in the parses and the views and kinds censuses; §32's seeds, in §46.

## 52. The reads answered (round 5)

| Finding | Answer | Section | Row |
|---|---|---|---|
| **B1** Paths inside revisions unseen | The names banned and the literal check, so neither sequence can name the gates outside the store; path-bearing shapes and `<rev>:<path>` built only by the door, which answers a declaration's fact from the store | §43, §45 | a3, b, d1 |
| **B2** `DaorisHome.Entry` reads what it locates | The store's probe for its five entries; every caller moved by what it does, driver.json's place a handle; each ban in the row that moves its last use, so a lands green | §44 | c1, c2 |
| **B3** The keepers census flags the design's own moves | Answers are parses, views and bytes; content keys and revisions are versions; `WatchRun._config` the one holder, with its flow | §47 | a2, a3, c1, c2 |
| **B4** `Effective` misses what the move reads | driver.json's content key and four families' revisions in one query; the day's start; `LimitMoveReadsTests`; a policy edit among the tests | §48 | c1 |
| **B5** driver.json's one kind changes other answers | The syntax the kind, five views each with its own problem; the parity table; `"cap": "4"` corrected to `"cap": 4.5` | §49 | c1 |
| **H1** `Driver.Host`, `AskConsole.cs:190-191` | The host carries the analyzer; the attachment is the person's file, data | §43 | a3, c1 |
| **H2** A start's site lost after an `await` | Keyed by the door's `AsyncLocal` scope, recorded at the process gate | §46 | a3 |
| **P1** The clock mark's twins | Its row in the driver's, the CLI's and the service's tables at once | §55 | a1 |
| **P2** `Drop` | Goes; a whole answer passes through whole | §45 | d1 |
| **P3** The search's parity | The store cuts each LF line as `ReplaceLineEndings` does | §45 | a2, b |
| **The census's proportion** | The compiler is the census; §29 to §34 dropped | §42, §43, §51 | a3 |
| **The rows** | a in three, c in two, d in two; each ban, kind and census in its row | §55 | all |

## 53. What stays open

- **A reader that reaches a declaration by data** passes the compiler. It reads fresh bytes and costs a read; the run-time half sees it on
  the flows the count tests and the bench run. Proving a value from data is no declaration's path took §29 to §34's machinery, out of
  proportion to a read's cost.
- **A file cache keyed by a data path** that happens to hold a declaration is outside the keepers census, whose answers are the store's;
  its staleness is that cache's own, as for any file, and `GitKeepersCensusTests` holds a git answer kept.
- **Between a3 and d1** a name not yet banned can gain a reader outside the store; the run-time half judges it from its row. A ban before
  its row's move would break the build.
- **A key written twice in driver.json**: the parity table decides in c1 whether the access and identity views keep their own rule.
- **`DriverConfigFile`'s words** name a place for the person; handed to a file API they are data.
- **Both makers' reads**: round 4 and round 5 have one maker's read each, the other's owed.

## 54. Rejected in round 5

- **The store in an assembly of its own** (§43): its dependencies are the driver's, so it needs a split of the driver below it or a port
  and a locator above it, and its home rows leave a table three twins read as text.
- **Every ban in a3, the unmoved readers allowed by a list**: a list of rows each later row deletes; a ban in the row that frees it needs
  none.
- **A whole answer's declarations from the store** (§45, the third way): a start for each commit not yet listed, for no change of answer;
  and dropping them changes the answers of every caller that must see them.
- **`Effective` over every family's revision** (§48): any write would move it, and the look would never skip.
- **`WatchRun._config` removed**: a torn file would leave the wait with no pace; it is the holder table's one row.
- **The site census** (§29 to §34): §42.

## 55. The rows

*Supersede §40's.* a1, a2 and a3 land in turn; then b beside c1, and c2 after c1; d1 after b and c2, d2 after d1; e after b. In every row
each moved reader's existing tests pass unchanged before a count is lowered, the bench is run before and after it, and the row's bans
(§43) and kinds (§50) are added with the build green.

- **a1. The stamp and the gate** (driver; cli and service for one row). GITLIB1's stamp: `FileStamp` under §9's rule with the volume's
  clock from the mark in the home (§36), `HomeEntry.ClockMark` added to the driver's, the CLI's (`src/platform/home.ts`) and the
  service's (`Daoris.Service.Core/Platform/DaorisHome.cs`) tables in one place (P1); `GitStamp` with every candidate git reads, present
  or absent (§7, §20), `GitAskerContext` folded into it; `GitReadCache` keyed by it, admitting under the fence of stats before and after
  (§19), keeping only answers whose starts read the objects alone (a graph page, a commit's own fields, a compare's commit list) and
  asking a commit's changes, a compare's, a file's history and blame at every ask (§35.1); `GitReplacements.CanBeInForce` lifted from the
  library's admission, its `packed-refs` parse made once a `FileStamp` version and shared with a2's `TipOf` (§36). Proof:
  `GitAskerContextTests`, `GitReadCacheTests` and the flow memos' tests unchanged; `GitStampTests` with git as the oracle against each
  git Daoris starts, §20's five planted files and a same-length rewrite of each stamp file inside the clock's grain; a replacement added,
  a graph read and the replacement removed, the answer refused by its fence; two trees of one repository, one marking a changed file
  `-diff`, each commit's page as git answers in that tree, and an attribute file edited between two asks read at the second;
  `ViewGitCountTests` moved (the graph kept by id after its first open, a commit's page and a compare one start after the first, a
  history and a blame one start each open); a share's clock lagging by a seam, its racy stat hashed again; `home.test.ts` with the mark's
  row in all three tables; `GitKeepersCensusTests` naming the stamp.
- **a2. The kernel** (driver). `ContentKey`, `BlobId` and the version types (§47), compared by `Same` alone, *unread* the same as no
  version (§60); the hashed listing (§19); `TipOf`; the checkout and home index; the three answers (§21); the ask by blob id answered only
  by bytes that hash to it (§35.4); `IDeclarationKind` and `IDeclarationView` with the contents handle; a bound per kind (§25);
  `DeclarationScope`, recording whether the store or the door asks (§46, §62); `GitTreeReads` and the store's answers to it (§45), no
  caller moved; `DeclarationNames` with every name a `static readonly` member, today's constants and their const aliases gone and every
  reader's literal name pointed at it (§43, §59); the rule file's kind, frozen: `FileRule`'s nine lists and `RepositoryFilesRead`'s two as
  immutable arrays, their counts, constructions and `with` edits repaired (§59). Proof: `Same` false for *unread* against *unread*,
  against *absent* and against a key, and *unread*'s `Text` throwing; a unit test of each key and each invalidation (a stat moved; a racy stat hashed again
  and never promoted; a stat moved during the read; a link; a loose, a packed, a symbolic and a `daoris/` ref moved; a replacement keeping
  no listing; two contexts; two trees of one content; a blob not hashing to its id; each cause of *unread* kept for none; a parse with a
  problem made again; a size past a reader's limit); §19's sequences (a blob kept, a replacement of it added, the next ask git's bytes; a
  commit, its tree and a folder each replaced in turn, nothing admitted; `refs/replace/` beside `core.useReplaceRefs=false`, passed
  through; a replacement added after the gate and removed after the listing, nothing admitted; a tracked list read across an add and a
  remove of a replacement, refused by its fence); §35.4's (a blob kept and a replacement of it added, asked by id *unread: replaced*, by
  path git's bytes with no id); the door's search over fixtures holding a lone CR, a NUL and a name after a CR, equal to `FileGit`'s; the
  weights against the allocations; the rule file's readers' tests unchanged with its kind frozen, their 28 constructions changing only
  the type they hand; a `FileRule` made with a default array refused; the build green with no const alias left; the bench's store rows, a
  warm checkout ask at its stat and a warm commit or tip ask at no start and under 2 ms.
- **a3. The boundary and the censuses** (driver, modules; every desktop project's build). The analyzer on every desktop project (§61) with the store's folder and
  `DaorisHome.cs` allowed by the desktop tree's one `.editorconfig`, its list and the allowance table empty, and
  `DeclarationBoundaryTests` with its own walk of every reference to a banned symbol and the suppressions it names (§57, §61);
  `DeclarationLiteralsCensusTests` over every desktop project with its four rows, folding through `string` and `Path` and judging
  `HomeEntry.Named` (§43, §61); the keepers census and its holder table (§47); the parses census by declared types (§50); the views and
  kinds census with the ambient rule (§33); `DeclarationGitCensusTests` and `DeclarationFileCensusTests` by the scope, seeded, each judging
  the names banned, a door's start by path alone, the grounds a data-reader row (§46, §62). Proof: the analyzer red on a planted use of a
  banned symbol in a test's compilation and silent on it under the store's folder; the boundary test red on a planted `#pragma` with
  RS0030 and one with no id, `SuppressMessage`, `NoWarn`, `WarningsNotAsErrors`, a second `.editorconfig`,
  `dotnet_analyzer_diagnostic.category-ApiDesign.severity` and `RunAnalyzers=false`; its walk, over a test's compilation, red on a banned
  use no row names, on a second banned symbol in an allowed file, on a row whose use is deleted, on a row whose file is gone and on a
  section that is no row's; the literal check red on §43's five planted names, on §61's three folded ones, on `HomeEntry.Named("driver")`
  and on a row left out; the keepers census red on round 3's planted keepers (an `IReadOnlyList<Regex>` made from a view by a helper and held in
  a record a field's dictionary keeps; an `object` field set from a view; a `??=` of a hash of a store answer; a static dictionary filled
  with an answer handed to it) and green on a field holding a `ContentKey` or an `Effective`; the parses census red on an
  `IReadOnlyList<string>` member and on an array behind one; the views and kinds census red on a view whose helper in another file calls
  `PersonFiles.Read`, on a kind whose parse calls `RoleStaffingPort.Fill`, on a view asking its contents for a key its record does not
  hold, and on `ToLower()` and `Order()` left to the ambient culture; the run-time censuses red on §46's three planted reads and on a
  door's start naming a declaration by path, and green on the store's own read by a declaration's id; the held-file census red with the
  grounds' row left out.
- **b. The gates file's one parse, the commit readers and every reader of the gates** (driver). `GatesDeclaration` as §21's syntax with
  each view's own validation, and the parity table over every gates blob in this repository's history, the tests' fixtures and §21's
  mixed-validity fixtures, each parser deleted once its column is equal; §10's tip and commit rows, with the manifest's, the lanes file's
  and the rule file's kinds and their commit views; §24's commit members (`UnionJoins`, `FileAnalysis`' commit asks, `LayoutGit`'s
  declarations, `DirectGateRuns`, `IntegrationJudging`); the gates' tree and checkout readers (`HeavyGates`, `TreeLocalInputs`,
  `ContextBriefs`, `HeavyTold`, and `OpinionPackets.RulesIn` reading the fixed name, `documents.gates` going, §36); X3's three (§35.6),
  `FileGit.LinesAsync` through the door's search; the asks by id (§35.4); `TreeReadMemo.Kept.Declared`, `BacklogMerge`'s `_declared` and
  `TogetherChecks`' memo removed; `DeclarationNames.Gates` banned, and with c2 landed `.Manifest`, `.Lanes` and `.Rules`. Proof: each moved reader's tests unchanged; the mixed-validity
  fixtures, each view answering its parser's cell; a malformed gates file said, then fixed in place and read; the line moving during a
  replay; a link of identical bytes refused; the offline partial clone's follow said, nothing kept, read once its promisor answers; a
  missing commit said unread, not absent; an oversize manifest said, never parsed as a prefix; `TogetherAccepts` with a tag shadowing its
  line; X1's sequence on `IntegrationAcceptance` (the text *unread: replaced*, nothing integrated as accepted, the page saying so); the
  gates' change between two commits found by entry ids; `LayoutReaderTests` unchanged over the store's manifest view; the analysis's hits
  equal before and after the exclusion on every fixture; the run-time censuses green judging the gates. Then `LandGitCountTests`,
  `TidyGitCountTests` and `StartGitCountTests` lowered, and the bench's line and commit rows at one listing a new commit and one read a new
  content.
- **c1. driver.json: its handle, its kind, its views and its one writer** (driver, modules, host, GitDoor, Launcher). §44's table for
  driver.json across the four projects: `DriverConfigFile` in place of every `configPath`, `DriverConfig.Of(home)` and
  `DriverConfig.Load(file)` the store's asks (`RunRebindsStep` among them), `HomeOf` as `file.Home`, the store's probe for its entry; §49's
  kind and five views, frozen (§50), with `SourceFile` and `Fill` laid at each ask (§21); GitDoor's two readers moved, the store's home
  ask, the kind and its access and identity views linked into GitDoor and the Launcher (§61); the one writer, with
  `StandingChoices.Write`, `DriverConfigDisk.Save` and the two key edits its callers, `DriverConfigFile.Change` gone (§35.5);
  `LimitWaits` stamped by `LimitStamp` over `LimitMove.Reads`' seven families and `StaffHolds`' count, due by `StaffLedger.NextChange`
  (§47, §48, §58); `HomeWatch`'s config part and `TreeReadMemo.Home` by the store's version; `WatchRun._config` and `LimitWaits._due` the
  holder table's rows (§58); `HomeEntry.DriverConfig`, `DriverConfig.ResolvePath`, `FindPath`, `DefaultPath` and `FileName`,
  `DriverConfigFile.Words` and `DeclarationNames.DriverConfig` banned (§61), with the allowance table's rows for the four migration steps,
  `DownloadClearing` and `DriveConsole` (§57). Proof: each moved reader's tests unchanged, the host's commands' and GitDoor's among them;
  GitDoor's and the Launcher's builds green with the analyzer on; round 2's three sequences for the load (`role_staffing` changed with
  driver.json untouched, the next load answering the new actors; the staffing store throwing once, that load answering the file's staffing
  and the next the store's; two homes of equal bytes sharing one parse, each load and save carrying its own `SourceFile`); §48's; §58's (a
  limits rule's `wait` deleted, a move ageing out of its 24 hours, a mark cleared by `Answered`, a mark's end passing, a start's binding
  moved, each with driver.json and §48's four families untouched; `LimitMoveReadsTests` red on a planted query of each of the three
  families left out; each writer of `StaffHolds` moving its count); §49's parity table and fixtures, `"cap": 4.5` beside an identity
  pointer failing the load while the pointer resolves; an access edit and an identity edit through the writer, read at once in the same
  flow; two standing choices written at once, neither lost; an in-place write of the same length inside the clock's grain, asked again at
  3 s, reading the new content; a write during the read kept for none; the boundary test's walk green with c1's rows and red with each left
  out; the keepers census green with its two holders. The bench's driver.json rows at a stat each.
- **c2. The other checkout and home readers** (driver, modules). §10's checkout and home rows but the gates' and driver.json's:
  `RegistryOperations`; `RegistryModule`'s declaration editor as the manifest's writer; `LoopGrounds` by version, *unread* refused at the
  press and at Confirm (§47, §60); `RepositoryIndexes`, `StandingBlocks` and `CodeMapFile`; `PersonFiles` with `PersonFilesForm.Rewrite`,
  `FoundFiles` with `FoundReads`' writes, and `FileAsks`; the workflows (`WorkflowFiles`, `WorkflowStore`), `StampedWorkflows` removed and
  `WorkflowHomeTopic` and the gates topic's workflows by version; §11's listings per ask; the store's probe for the four other entries;
  `HomeEntry.Files`, `FilesFound`, `FilesAsks` and `Workflows`, the layers' path helpers, `WorkflowStore.Folder`, `FolderOf` and `PathOf`
  and their names banned (§61), with the allowance table's rows for `WorkflowsStep`'s second symbol and `PushStepsStep`; and with b
  landed `DeclarationNames.Manifest`, `.Lanes` and `.Rules`, with `RemoteSync`'s check a row of the table, git's judgement of the file
  (§24, §57). Proof: each moved reader's tests unchanged; a child process writing the person's layer, read at the next ask; a workflow
  written by another process listed and its topic published; an index file in one of two trees with one manifest found in that tree alone;
  `core.autocrlf=true`, the manifest still unmodified; the pipe's grounds refusing a manifest edited between the press and Confirm, refusing
  one unreadable at both (held open with no sharing by the test), and applying over one absent at both; the boundary test's walk green with
  c2's rows and red with each left out. The bench's checkout and home rows at a stat each.
- **d1. The door** (driver, modules). Every path-bearing git read moved into `GitTreeReads` (§45), a revision that is not a commit id
  resolved in the asker's folder before the store is asked (§62); `GitObjectName` a name alone, refusing one holding `:`, and the path
  form the door's own `GitObjectAt`, its constructor and `At` banned outside the door (§62); `GitTreeReadsCensusTests` with push's two
  rows. Proof: each moved caller's tests unchanged; the census red on B1's two sequences planted outside the door and on a row left out;
  planted outside the door in a test's compilation, `new GitObjectName(rev, path)` and `with { Path = … }` not compiling and a
  `GitObjectAt` made there red under the ban; a batch question by path written on a start's input outside `GitObjectReads.cs` red in
  `GitObjectFormsCensusTests`; a name holding `:` refused at its making; two linked trees of one repository at different commits, each
  asking the gates at `HEAD` through the door and answered its own commit's; a whole `ls-tree -r` and a name diff answering their
  declarations as git lists them; the run-time censuses green over every count test with every name banned; the count tests within the
  budgets b, c1 and c2 left.
- **d2. The views and the analysis's kept parts** (driver). §11's views with their key tests and N5's key, each frozen (§50); §12's parts
  as §19 and §22 restate them (the tracked list by commit under its fence, ignore-case by the stamp, the search fresh); `FilesLooks`
  naming its line's ref in full. Proof: the analysis's, the files stage's and the Files page's tests unchanged; two repositories sharing
  three ids and differing in personal rules, two views; an ignored output added at an unchanged `HEAD`, and tree B's new top, each judged
  `Output`; two trees of one commit, one marking a writer script `-diff`, each judged as git judges it; a `core.ignorecase` change moving
  the stamp; the bench's files-stage and Files page rows warm at three starts, two where the tree has no ignored top, from 11 and 10.
- **e. Saved** (driver). §14.2's tables without the stamp's digest (§19), the bound at admission and the trim. Proof, the misses first: a
  replacement made while the desktop is down, the cold start's ask passed through to git; a blob saved and a replacement of it made while
  the desktop is down, the cold start's ask by id *unread: replaced* with nothing loaded under the id, and its ask by path git's bytes with
  no id; a replacement added, a commit read and the replacement removed, then a restart, no row; the promisor down, nothing saved, and
  saved once it answers; a blob row whose bytes do not hash to its id refused at its load; a fill past the bound within it after every
  insert, each kind counted; the trim. Then the hits: the bench's follow over 20 repositories run twice with the driver made again
  between, 0 git starts the second time where no line moved (the top from its files, the tips from the refs' files, listings and blobs from
  the store), the toolchain digest's processes and any stamp ask counted apart.

Nothing was measured anew for round 5 but the counts of §44 and §45, taken from the code, and nothing in the product changed.

## Round 6

*Supersedes round 5 where it differs: §43's two allowed places by §57's table and one `.editorconfig`; §43's literal check, boundary
test and projects by §61; §44's banned members by §61; §46's rule inside the scope by §62; §47's version types by §60 and its holder
table by §58; §48 by §58; §50's a2 row by §59; §45's object names by §62. §55's rows a2, a3, c1, c2 and d1 are rewritten in place.*

One maker read round 5 ([the reads](2026-11-09-repo-config-reads.md), *Opus (independent), round 5*); the other's read did not run. The
read found a1 and a2 able to land green and four blocking findings, **N1** to **N4** as it numbers them, each a row that could not land
as written; five gaps in the boundary's completeness; and five points that did not block. Each is answered here by construction, its
proof added to its row. The code adds three the read did not name: GitDoor compiles two of the driver's readers of driver.json, and
GitDoor and the Launcher compile the home table whose probe asks the store (§61); two more migration steps name the workflows' places and
one more reads driver.json (§57); and `FileRule` holds eight lists beside `Paths`, which the parses census reaches as it reaches `Paths`
(§59). Written from the code at `da114b5864`.

## 56. In short

- **An allowance table** (§57). A file outside the store that keeps a banned name is a row: the file, its banned symbols, and why. Each
  row is one section of the desktop tree's one `.editorconfig`; `DeclarationBoundaryTests` walks every reference to a banned symbol
  itself, red on one no row names and on a row whose use or file is gone. Its rows: five migration steps, `RemoteSync`'s check and two
  sentences.
- **Everything the look reads** (§58). `LimitMove.Reads` gains `supervisor_rules`, `role_moves` and `bindings`; `_due` is no later than
  the ledger's next change by the clock; the ledger's marks and reservations are one type whose every write moves a count the stamp
  carries. `_due` is a listed holder.
- **Names never folded** (§59). `DeclarationNames`' members are `static readonly`; the const aliases go in a2; `FileRule`'s nine lists
  become immutable arrays in a2, their counts and constructions repaired.
- ***Unread* is no version** (§60). Versions compare by `Same` alone, false where either side is *unread*, and *unread* has no text; the
  grounds unread at the press or at Confirm refuse, as they do today.
- **The boundary, closed** (§61). A constant naming a declaration entry handed to `HomeEntry.Named` is red; every member answering a
  declaration's name or place is banned; folding passes through `string` and `Path`; each configuration that switches the analyzer off is
  red; every desktop project is checked, and GitDoor's readers of driver.json move.
- **The door and the run-time half** (§62). Inside the scope a door's start is judged by path; the half's four gaps are said once; the
  grounds are a data-reader row; a revision is resolved in the asker's folder first; an object name's path form is the door's type alone.

## 57. Allowed uses of a banned name (N1)

The read is right: RS0030 is off only in the store's folder and in `DaorisHome.cs`, and the boundary test is red on any other quieting,
so c1's ban of `HomeEntry.DriverConfig` stops four migration steps compiling and the manifest's ban stops `RemoteSync`. **A file outside
the store that keeps a banned name is a row of the allowance table**: the file, the banned symbols it may name, and why. The desktop tree
has one `.editorconfig` (`src/Daoris.Desktop/.editorconfig`), whose sections set RS0030 to none for the store's folder, for
`DaorisHome.cs` and for each row's file alone; a section applies by the file's path, so a file another project links is allowed there
too. A row lands in the row that bans its first symbol, and a symbol banned later joins its row then, so every row builds green.

| File | Banned symbols it names | Lands in | Why |
|---|---|---|---|
| `Transitional/Migration/DriverKeysStep.cs` | `HomeEntry.DriverConfig` | c1 | takes out the keys an older build wrote (`:16`) |
| `Transitional/Migration/GitPushStep.cs` | `HomeEntry.DriverConfig` | c1 | sets the machine's Write where `no-push` was switched off, writing the file where none is (`:22-30`) |
| `Transitional/Migration/RulesStep.cs` | `HomeEntry.DriverConfig` | c1 | reads `strikes` to seed the supervisor's rules, then takes it out (`:21-34`) |
| `Transitional/Migration/WorkflowsStep.cs` | `HomeEntry.DriverConfig`; `WorkflowStore.FolderOf` | c1; c2 | keeps a copy of the file and of each saved workflow and says what it changed (`:16-28`); it reads and writes driver.json through the store, the place only for the copy and the record |
| `Transitional/Migration/PushStepsStep.cs` | `WorkflowStore.PathOf` | c2 | keeps a copy of each workflow it converts and says it (`:25`, `:30`); it writes through the workflow store |
| `Service/RemoteSync.cs` | `DeclarationNames.Manifest` | the later of b and c2 | asks git whether the checkout's manifest is as `HEAD` has it (`WorkingTree.UnmodifiedAsync`, `status --porcelain -- daoris.json`, `:309`): git's judgement of the file, reading no content of the store's (§9, §24); its alias at `:283` goes in a2 (§59) |
| `Workplace/DownloadClearing.cs` | `DriverConfig.FileName` | c1 | the reason a downloaded folder is kept names the file that names it, for the person, as it names `tools.json` (`:374-375`) |
| `DriveConsole.cs` (the headless host) | `DriverConfigFile.Words` | c1 | tells the person where to name the repositories it drives (`:163`) |

The five migration rows are the release migration's own reads and writes of the home. Each runs before anything reads the home
(`HomeMigration`'s contract), through `HomeMigrationRun`, which keeps a copy in the salvage folder first, and each is deleted with the
migration; a write it makes is read at the next ask by its stat (§9). `RunRebindsStep` (`:27`) only reads driver.json: from c1 it asks
`DriverConfig.Of(run.Home)` and needs no row.

**The walk is the guarantee.** `DeclarationBoundaryTests` does not trust the analyzer's silence. Over every desktop project's
compilation (§61) it finds each reference to a symbol `BannedSymbols.txt` lists, resolved by its documentation id (a member's use, a
construction, a conversion through a listed operator), outside the store's folder and `DaorisHome.cs`, and pairs it with a row by file
and symbol. It is red on a reference no row names, a second banned symbol in an allowed file among them, since a section quiets its file
whole; on a row's symbol its file no longer references; on a row whose file is gone; and on a section that is no row's, or a row with no
section. It reads the syntax trees, not the analyzer's diagnostics, so no quieting hides a reference from it: the build's error is the
fast signal, the walk the guarantee.

## 58. Everything the look reads (N2)

From the code. A look reaches the move (`Course/Chat/LimitWaits.cs:271`, `LimitMove.cs:149`), whose `LimitRuled.Holds` asks
`ConversationRules.Judge`: the line's role through `RuledLine` (`Course/RuledLine.cs:49`, the start's binding in the `bindings` family,
then the catalogue), its effective rules (`StaffCourse.CourseOf`, `Course/Chat/StaffCourse.cs:22`, `supervisor_rules`) and its moves in
the last 24 hours (`StaffMoves.Of`, `Roles/StaffMoves.cs:61`, `role_moves`). The ledger's preview reads its reservations and its
not-answering marks, held in memory, each with an end (`Roles/StaffLedger.cs:405-414`; `ReservationLife` ten minutes, `Mark` fifteen).
Every input is of one of three sorts, and each sort has one way to make the look run:

| Sort | Inputs | What makes the look run |
|---|---|---|
| a family of the desktop store | `role_staffing`, `roles`, `role_policy`, `role_starts` (§48); `supervisor_rules`, `role_moves`, `bindings` | the family's revision, which every write moves (`Store/DesktopStore.cs:231-233`): `LimitMove.Reads` lists the seven and the stamp's one query reads them |
| a fact the clock alone ends | today's counts and tokens; a move, counted for 24 hours; a mark; a reservation | `_due`, no later than the earliest such end |
| the ledger's memory | marks and reservations made, cleared and released | a count the ledger moves at each such write, which the stamp carries |

**`_due` no later than the ledger's next change.** `StaffLedger.NextChange(now)` answers the earliest of: the next day's start where a
role's policy counts a day (§48); the oldest `role_moves` row inside the window, plus 24 hours (one query over every line, never later
than any line's own); the earliest end of a mark; and the earliest reservation's moment plus `ReservationLife`. The look sets `_due`
(`:120`) to the earlier of its waits' next and that. The tokens of today's turns need no stamp: within a day they only grow, which only
holds a choice tighter, and the day's start that frees them is among the ends.

**Mark, unmark and release set `_dirty`, by a count.** The ledger's `_marks` and `_reserved` become one type, `StaffHolds`, whose members
are the only writers of either collection: a mark (`MarkNotAnswering`), its clearing (`Answered`), a reservation taken, and its release
(`Release`, `ReleaseBound` and a binding's end). Each moves the type's `Revision` under the ledger's lock, and `LimitStamp` carries it,
so the look after any of them does not skip, as a set `_dirty` makes it. The ledger is the home's and the stage is a runner's, so a count
the stage reads stands in for a call from the ledger into the stage. An expiry moves nothing; `_due` covers it.

**`_due` is a holder.** It is a moment the look computes from decisions that took the look's `DriverConfig`, an answer, and it is kept
across looks, so the keepers census sees it. It joins `WatchRun._config` in the holder table: it keeps the earliest moment a held wait
comes due, for one flow, from the look that set it to the next look that runs. Every look that runs replaces it whole (`:120`), and a
look runs whenever `LimitStamp` moves, so it is never read against choices other than those it came from. `_stamp` holds a
`LimitStamp`, a version, and is no holder.

Proof (c1), each with driver.json and §48's four families untouched: a wait held by a limits rule's `wait`, the rule deleted through
`SupervisorRuleStore`, the next look moving the conversation; a line at its moves' bound, its oldest move passing 24 hours on a seam
clock, the look due by then and moving it; an agent marked not answering and `Answered` clearing the mark, the next look not skipping; a
mark's fifteen minutes passing on the seam clock, the look due by then; a start's binding moved to another role, the next look not
skipping. `LimitMoveReadsTests` red on a planted query of each of the three families left out of the list; `StaffHoldsTests`, each
writer moving the count.

## 59. Names never folded, lists frozen (N3)

N3 is right: were `DeclarationNames`' members `const`, as today's names are, every reference outside the store would fold to a
declaration's name and be red in the literal check from a3, until b, c1 or c2 moved it. **`DeclarationNames`' members are
`static readonly`.** A reference to one has no constant value, so the literal check never folds it, and the ban holds it from its row
on; until then it compiles, and the run-time half judges it (§46).

**No const alias stands**, since a `const` made from a `static readonly` does not compile. a2 deletes each: `LandedGates.File`
(`Workflows/LandedGates.cs:24`, made from `GeneratedWriters.DeclarationFile`), its use at `Setup/PlaceCommand.cs:264` reading
`DeclarationNames.Gates`; and each constant a2 points at `DeclarationNames` (§43's seven, `FoundFiles.FileName`, and the private names in
`RemoteSync`, `RepositoryIndexes`, `OpinionPackets`, `LayoutFacts` and `BacklogMerge`), its uses reading the member. None is used today
where only a constant compiles (a `case` label, an attribute, a parameter's default); a2's build is the proof.
`KnowledgeConnector.WorkspaceMarker` stays a constant, a row of the literal check (§43).

**`FileRule`'s lists, frozen with their counts.** The parses census holds every type a kind reaches (§50), and the rule file's kind
reaches every member of `FileRule`, not `Paths` alone: `Setup`, `Key`, `Toolchain`, `Env`, `Secret`, `Caches`, `Refuses` and `Evidence`
are lists too (`Workplace/Files/FileRule.cs:26-58`). a2 makes all nine `ImmutableArray<string>`, the eight optional ones nullable so
that null still leaves a field out and an empty `Key` still keys by the commit's tree, and `RepositoryFilesRead.Rules` and `Problems`
immutable arrays too. An immutable array counts by `Length` and is not made from a `List<>`, so a2 repairs each `.Count` and
`{ Count: … }` on them (`FileRuleFile.cs:142`, `FilesLooks.cs:44` and `PersonFilesForm.cs:52` among them), each of `FileRule`'s 9
constructions in the driver and 28 in the tests, and each `with` that sets a list (`RuleFileFirstForm.cs:70-95`, `FileAnalysis.cs:213`);
and it refuses a default array at construction, so no rule holds one that throws when enumerated. The tests change only in the type they
hand; every assertion stands.

## 60. *Unread* is no version (N4)

N4 is right: §47 gave `DeclarationVersion` an *unread* case and said only of `Effective`'s *unversioned* that it equals nothing (§35.2).
**An *unread* version is the same as no version, another *unread* included**, by construction three ways:

- versions are compared by `Same(a, b)` alone, false where either side is *unread* or *unversioned*; two equal content keys, or *absent*
  twice, are the same;
- `DeclarationVersion` is no record and declares no `==`, so no generated equality makes two *unread* equal; and an *unread* is kept for
  none (§35.4), so no two asks share one;
- its `Text`, the form a fingerprint, a stamp or a key holds, throws `DeclarationUnreadException`, an `IOException`, for *unread*, so no
  string made from versions holds one.

An equality answering false for *unread* against itself was weighed and rejected: a value unequal to itself breaks every dictionary and
set it enters. `Effective` and `LimitStamp` compare by `Same` through their parts. `HomeWatch`, which only publishes, stamps an *unread*
declaration part by its stat, as it stamps every other part (§47), so it publishes when the file moves and not at every poll.

**The pipe's grounds.** The `declaration` ground (`Modules/Loop/LoopGrounds.cs:230-231`) becomes, in c2, the store's version of the
checkout's manifest as text. *Unread* at the press, its `Text` throws, as `Hash` throws today where the file does not open (`:270`), and
the pipe refuses as it does today; *unread* at Confirm, the same; read at both, the keys compare; absent at both, `none` both times, as
today. Proof (a2): `Same` false for *unread* against *unread*, against *absent* and against a key; *unread*'s `Text` throwing. Proof
(c2): a manifest unreadable at both the press and Confirm, nothing applied; one edited between them, nothing applied; one absent at both,
applied.

## 61. The boundary, closed

**Entries by name.** `File.ReadAllText(DaorisHome.Entry(home, HomeEntry.Named("driver")!))` names no banned symbol and no declaration's
tail. The literal check is red outside `DaorisHome.cs` on a constant naming one of the five declaration entries (`driver`, `files`,
`files-found`, `files-asks`, `workflows`, by ordinal as `Named` compares, `Platform/DaorisHome.cs:488`) that is handed to
`HomeEntry.Named` or compared with a `HomeEntry`'s `Name`. A name from data passes, as the table's walkers' names do (§43).

**Every member answering a declaration's name or place is banned**, in the row that bans its entry. c1 adds to `ResolvePath`:
`DriverConfig.FindPath` in both forms, `DefaultPath` and `FileName` (`Settings/DriverConfig.cs:501-516`), and the handle's words,
`DriverConfigFile.Words` (§44). c2 adds `WorkflowStore.FolderOf` (`Workflows/WorkflowStore.cs:26`) beside `PathOf` and `Folder`. Today's
callers of c1's: `RemoteSync.cs:296` and `Loop/SystemWiring.cs:410` take a handle the store makes from the environment;
`Agents/AgentsCommand.cs:229` writes the file's name in its sentence, which the literal check reads as words; `DownloadClearing` and
`DriveConsole` are rows (§57). c1's and c2's builds list every other use exactly, and each moves by §44's table or takes a row.

**Folding through `string` and `Path`.** The check knows a value two ways. It is known whole where it is a constant, or a call of a
member of `string` or `Path` whose receiver and arguments are all known whole, evaluated by calling that member
(`string.Concat("daoris.gates", ".json")`, `Path.ChangeExtension("daoris.gates", "json")`, `"DAORIS.JSON".ToLowerInvariant()`). It is
known after its last separator where it is a `Path.Combine` or `Path.Join` whose last arguments are known, joined, or a
`Path.ChangeExtension` of such a value with a known extension, applied to the known part. A value known either way is judged by §43's
rule on its tail. Planted, each red: `Path.ChangeExtension(Path.Combine(root, "daoris.gates"), "json")`;
`string.Concat("daoris.gates", ".json")`; `Path.Combine(home, string.Join("/", "data", "driver.json"))`.

**The configurations that switch the analyzer off.** Beside round 5's list (§43), `DeclarationBoundaryTests` is red on a
`#pragma warning disable` naming no id, which quiets every warning after it; on `dotnet_analyzer_diagnostic.category-ApiDesign.severity`,
RS0030's category, and on `dotnet_analyzer_diagnostic.severity`, in any `.editorconfig` or `.globalconfig`; on `RunAnalyzers` or
`RunAnalyzersDuringBuild` set false in a desktop project or a props or targets file it imports; and on RS0030 in `WarningsNotAsErrors`.
The walk (§57) sees a reference through each of these; each is red for what it does to the build's error.

**Every desktop project.** The analyzer, the walk and the literal check cover all eight: the driver, the modules, the headless host, the
application, the Launcher, GitDoor, PieceStart and OwnerStandIn. The four small ones compile files of the driver by link and name no
declaration in their own. Two reach one through their links:

- **GitDoor** compiles `Workplace/GitAccessKeys.cs` and `Workplace/GitIdentityUse.cs`, whose `Load` (`:94`) and `Read` (`:84`) resolve
  `HomeEntry.DriverConfig` and parse the file in the door's own process, which git starts at each act (`Workplace/GitDoors.cs:149`,
  `:168`; `Workplace/GitSessionAccess.cs:63`).
- **GitDoor and the Launcher** compile `Platform/DaorisHome.cs`, whose probe asks the store for its declaration entries from c1 (§44).

c1 moves both. The levels and the pointers are the kind's views in every process (§49); GitDoor links the store's home ask, driver.json's
kind and its two views from `Workplace/Declarations/`, and the Launcher the home ask alone, as each links `AtomicFile.cs` and
`DaorisHome.cs` today. The door then asks the store as the driver does, and what it keeps ends with its process, one git act. Each linked
file uses the base library alone, as the door requires (`GitDoors.cs:19`); a dependency beyond it fails the door's build, which is the
proof. An allowance row for the door was weighed and rejected: it would keep a second reader of driver.json.

## 62. The door and the run-time half

- **Inside the scope, by path.** `DeclarationScope` records whose ask it is, the innermost member's: the store's own reads (a blob by
  id, a listing, and its pass-through to git by path while `CanBeInForce` holds) or the door's. A start under the door's scope is red
  where a pathspec's element, a `<rev>:<path>`'s path or an `ls-tree` path names a declaration other than in the door's exclusion; never
  by id, since the store's own reads name declarations' ids. A start under the store's scope is the store reading.
- **The half's known gaps, said once.** The scope flows with its asker's execution, so a start that does not run in its asker's flow
  loses or borrows it: `GitScheduler` joins an identical queued start and runs it in its first asker's flow
  (`Platform/GitScheduler.cs:192-195`, `:277`); the flow memos answer without a start (`Workplace/GitRuns.cs:136`); a `Lazy` or a timer
  made inside the scope keeps it for whoever runs it later (`Workplace/Git/GitLazyFetch.cs:76`'s pattern), and one made outside lacks it;
  an iterator loses it at each yield. Each hides a reader by data alone: a reader that names a declaration is the compiler's (§43), and
  what any reader keeps is the keepers census's (§47). So a gap costs the half its sight of a data reader, whose miss costs a read (§42).
- **The grounds, a data-reader row.** `DeclarationFileCensusTests` has a row for each reader that must open any file by data, a
  declaration among them: the pipe's grounds, which hash each path a tree holds uncommitted (`Modules/Loop/LoopGrounds.cs:218`) and each
  file under a folder the person names (`Files`, `:253-264`), comparing bytes at the press and at Confirm and never reading a
  declaration's meaning. Red on the row left out and on a row gone.
- **A revision resolved first.** A door member handed a revision that is not a full commit id (`HEAD`, a branch, a linked tree's own
  `HEAD`) resolves it in the asker's folder, from the refs' files by `TipOf` or else by `rev-parse --verify <rev>^{commit}` there,
  before it asks the store, whose listing is keyed by commit id. A full id is asked as it is.
- **An object name's path form, the door's alone.** Round 5 banned `GitObjectName.At`, and `new GitObjectName(rev, path)`,
  `with { Path = … }` and the conversion from a string (`Workplace/Git/GitObjectName.cs:8-10`) passed. In d1 `GitObjectName` keeps a
  name alone (an id, or a name git resolves) and refuses one holding `:`, as it refuses a line break, so no string becomes a
  `<rev>:<path>` question; no caller writes one today. The path form is the door's own type, `GitObjectAt`, declared beside the door
  with its framing and the fallback for a path holding a line break (`Workplace/Git/GitObjectReads.cs:28`, `:66`), its constructor and
  `At` banned outside the door. `new GitObjectName(rev, path)` and `with { Path = … }` then do not compile. A batch question by path
  written on another start's input needs a `--batch` start, which `GitObjectFormsCensusTests` holds to `GitObjectReads.cs` alone.

## 63. The reads answered (round 6)

| Finding | Answer | Section | Row |
|---|---|---|---|
| **N1** Exempt users of a banned name cannot compile | The allowance table, a `.editorconfig` section per row, and the boundary test's own walk, red on a row left out or gone; five migration rows, `RemoteSync`'s check and two sentences | §57 | a3, c1, c2 |
| **N2** B4's remainder | `LimitMove.Reads` with the three families; `_due` no later than the ledger's next change; marks and reservations one type whose writes move a count the stamp carries | §58 | c1 |
| **N3** Names that fold | `static readonly` members, never folded; the const aliases gone in a2; `FileRule`'s nine lists frozen with their counts and constructions | §59 | a2, a3 |
| **N4** A failure counted as a version | `Same` false for *unread*; no value equality; no text; the grounds refusing at the press and at Confirm | §60 | a2, c2 |
| `LimitWaits._due` | A holder, its flow one look to the next that runs | §58 | c1 |
| `HomeEntry.Named` | A constant naming a declaration entry, handed to it or compared with `Name`, red | §61 | a3 |
| Members answering a name or a place | `FindPath`, `DefaultPath`, `FileName`, the handle's words and `WorkflowStore.FolderOf` banned in their rows | §61 | c1, c2 |
| Folding | Through every `string` and `Path` member over known values, and `ChangeExtension` over a known tail | §61 | a3 |
| Three more suppressions | An id-less `#pragma`, the ApiDesign category and the bulk severity, `RunAnalyzers=false`, `WarningsNotAsErrors` red; the walk sees through all | §57, §61 | a3 |
| The small projects | All eight checked; GitDoor's two readers of driver.json moved; GitDoor and the Launcher linking the store's home ask | §61 | a3, c1 |
| By path inside the scope | A door's start judged by path; the store's reads by id and its pass-through its own | §62 | a3 |
| The run-time half's gaps | A joined start, flow memos, a `Lazy` or a timer, iterators: data readers alone | §62 | a3 |
| The held-file census's data readers | The grounds' row | §62 | a3 |
| A revision that is not a commit id | Resolved in the asker's folder first | §62 | d1 |
| A batch question by path; `new GitObjectName(rev, path)` | The path form the door's type; a name refusing `:`; `--batch` held to `GitObjectReads.cs` | §62 | d1 |

Rounds 4 to 6 have one maker's read each; the other's is owed. Nothing was measured for round 6: the lines cited are the code's at
`da114b5864`, and nothing in the product changed.
