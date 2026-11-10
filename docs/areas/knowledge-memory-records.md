# Knowledge, memory and records — how it works now

## What it does

The knowledge service reads the checkouts registered on this machine and keeps one derived index of what they wrote: rules,
knowledge, skills, decisions and their notes, fixes, finished tasks, the router, a declared documents folder and the generated
index of where things are. It answers searches by words, and by meaning where the deployment names a model. Every hit names its
lines and the commit it was read at. Sessions reach it through the MCP connector, which serves each session only the tools its
kind of work may call. The memory coordinator keeps one memory per repository, owned by that repository's sessions and changed
only through a log of operations. A workspace may be wired to a remote: quests sync as operations, records as feeds, and nothing
leaves the machine unless the repository's manifest says it may. This repository keeps its own records in declared files with
one job each, checked by `verify`.

## Its parts

`Core/` is `src/Daoris.Service/Daoris.Service.Core/`; `Mcp/` is `src/Daoris.Service/Daoris.Service.Mcp/`.

| Unit | File | Its job |
|---|---|---|
| `KnowledgeService` | `Core/Knowledge/KnowledgeService.cs` | The one face every door calls; it keeps the index's reading state (refresh, the registered roots, the registry reload) and one-line doors to the types below |
| `ReadingBehind` | `Core/Knowledge/ReadingBehind.cs` | The registry's wait on the index: only while it holds nothing; otherwise the reading runs behind it, one at a time |
| `KnowledgeAnswers`, `KnowledgeStandings`, `KnowledgeCitations` | `Core/Knowledge/` | Search with its tier and the records merged in; each hit's standing (`RootProbes` asks a root's uncommitted status once, `DecisionMarks` reads a decision's notes); cite and recheck |
| `KnowledgeFeeds`, `KnowledgeRegistrar`, `KnowledgeCodeMaps` | `Core/Knowledge/` | The feed doors (a declaration, knowledge, a code map) behind one gate; register, retire and re-stamp; a code map read or held |
| Feed records | `Core/Knowledge/Records/` | `FeedProvenance`, `FeedOutcome`, `FeedRefusal`, `RepositorySummary` |
| `WorkRecordStore` | `Core/Sessions/WorkRecordStore.cs`, records in `Core/Sessions/Records/` | A finished tree's record, kept in `knowledge.db` and searched through `work_records_fts`; `NewerWorkRecordsException` when a newer build stamped them |
| `RepositoryScanner` | `Core/Registry/RepositoryScanner.cs` (`Scan` 94-203) | Reads one checkout into entries, in a fixed order; one file is one place |
| `RepositoryLayout`, `RepositoryDocuments`, `DaorisLock` | `Core/Registry/` | Where a checkout keeps its doctrine, which roles `daoris.json` declares, and which files are canonical |
| Ingest | `Core/Knowledge/Ingest/` | Splits files: `DecisionNotes`, `IndexSections`, `IndexRows`, `MarkdownSections`, `MarkdownFence`, `EntryPieces` |
| `KnowledgeIndex`, `DisclosurePolicy` | `Core/Knowledge/` | Writes a source into the store, withholding at ingest what may not leave |
| `SqliteKnowledgeStore` | `Core/Knowledge/Store/SqliteKnowledgeStore.cs` | `knowledge.db` with FTS5; `InMemoryKnowledgeStore` is its test twin |
| Search | `Core/Knowledge/Search/` | `SqliteKnowledgeSearch` (BM25), `LexicalKnowledgeSearch` (term overlap over any store), `SemanticKnowledgeSearch`, `HybridKnowledgeSearch` (rank fusion), `ConvergenceDetector` |
| Freshness | `Core/Knowledge/Store/` | `CheckoutFreshness` (watch), `RootSignature` (per root), `UncommittedRecords` (git status), `NewerIndexException` |
| `EntryStanding`, `Citation` | `Core/Knowledge/` | The `standing:` line of a hit; a record cited by id, lines, commit and digest |
| `Registry`, `RegistrationStore`, `RegistryImport` | `Core/Registry/` | Registrations pushed by `connect` and persisted; a folder scan only proposes |
| `AmbientWorkspace`, `WorkspaceScope` | `Core/Registry/AmbientWorkspace.cs`, `Mcp/WorkspaceScope.cs` | Which workspace a call answers from |
| Feed | `Core/Knowledge/Feed/` | `FeedOrder` and `FeedDigest` (both in `FeedOrder.cs`), `CodeMap`, `CodeMapWire`: what a remote is fed and how it is ordered |
| Connector host | `Mcp/Program.cs`, `Mcp/KnowledgeServer.cs` | The stdio MCP server; `ToolTypes` is the one list of tool classes |
| HTTP knowledge reads | `src/Daoris.Contracts/Knowledge/KnowledgeContracts.cs`, the HTTP host's `Routes/KnowledgeRoutes.cs` | Status, protocol, repositories, search (its tier in `x-daoris-tier`), entry, entries and convergence, each declared once as its request record; the driver sends the records, and the page sends them by name with its generated types (`data/api.ts`) |
| Knowledge tools | `Mcp/KnowledgeSearchTools.cs` | `knowledge_search`, `knowledge_get`, `knowledge_repositories`, `knowledge_convergence`, `registry`, `knowledge_refresh` |
| Memory tools | `Mcp/MemoryTools.cs` | `memory_search`, `memory_get`, `memory_write`, `memory_end`, `memory_propose`; a search across a workspace's repositories gives each a fair share of its ten places, taking turns in repository order with each engine's own order within, since the engines give no score to merge by |
| `ToolClasses` | `Mcp/ToolClasses.cs` | Each tool's class: read, restricted or publish |
| `CallTable`, `SessionWorks` | `Mcp/CallTable.cs` (`Refusal` 121-144), `Mcp/RoleFilter.cs` | What each kind of work is served; filters `tools/list` and every call |
| Call guards | `Mcp/CallLineageFilter.cs`, `ArgumentFilter.cs`, `WaitingFilter.cs` | Lineage proof, missing arguments, the waiting line |
| Call keys | `Core/Sessions/CallLineage.cs`, `Core/Sessions/CallKeys/` | A call's `_meta` lineage read by one ordered list of agents' keys, one file per agent (Claude Code's first, so a call carrying both is read as its); the list is held to the driver census's `service.callKeys` twin |
| `MemoryOpStore` | `Core/Knowledge/Memory/MemoryOpStore.cs` | `memory_ops` in `knowledge.db`: the log of record of every memory change; its read sets are `MemoryOpReads`, its changes since a cursor `MemoryOpChanges`, the waits no session holds `MemoryOpWaits`, its rows' types `Memory/Records/` |
| `LyntaiMemory`, `MemoryEngine` | `Core/Knowledge/Memory/` | One engine per repository over `memory.db`, and its read-only view for others |
| `MemoryApplier`, `MemoryCoordinator` | `Core/Knowledge/Memory/` | Applies released rows, recovers at start, paces the sweep |
| `MemorySweep`, `MemoryPresenter`, `MemoryView` | `Core/Knowledge/Memory/` | Rechecks citations, shows an entry as current only when checked, reads another repository's memory |
| `MemoryProposals`, `MemoryDesk` | `Core/Knowledge/Memory/` | A proposal as a quest to the owner; the person's approve, decline, Keep and Drop |
| Remote | `Core/Remote/Remote.cs`, `RemoteConfig.cs`, `QuestSync.cs`, `SessionSync.cs`, `ChannelSync.cs`, `CodeMapSync.cs` | The remote's client, its configuration, and one pass per kind, run by `POST /api/sync` and ticked by the driver's `RemoteSync.cs` |
| Record tooling | `tools/orient-index.mjs`, `index-fresh.mjs`, `index-hook.mjs`, `knowledge-server.mjs`, `doc-duplicates.mjs`, `doc-current.mjs`, `doc-system.mjs`, `merge-rows.mjs` | Writes and checks the index, builds and serves the workspace's connector, checks and merges the records |

## Its rules

**Knowledge**

1. Local is the default and is fully useful alone: no URL means local, silently, and opens no socket. A shared host is the same
   executable in shared mode. (from D21, D47)
2. The index is derived from files. The files are the floor and must work with no service; the service only adds search across
   checkouts and by meaning. (from D129, D151)
3. The scanner reads, in order: rules and the doctrine region, knowledge, skills, rooms, the declared router, decisions, fixes and
   archive (declared path first, else known names, first match wins), the deployment's index, the declared index, the deployment's
   documents folder, and the README until the repository adopts. (from D122, D124, D151)
4. One file is one place: a file already read is never read again under another kind. A link is never followed. What git ignores
   inside a walked folder is never opened; a declared folder git ignores whole is still read. (from D117, D151)
5. The lock decides provenance. Canonical entries are the same everywhere and never feed; local entries are what differs.
   (from D5, D21)
6. Kinds are rule, knowledge, skill, decision, fix, task outcome, index and record. A decision is one entry and each dated note
   another. An index entry is a heading's prose, a table row or a list item. A record is a finished tree's, on the local host only:
   no feed carries one, and a feed naming the kind is refused whole. A search naming repositories narrows to them before it
   ranks, records included. (from D134, D151, D195)
7. Every entry keeps its lines. A hit names `path:first-last`; `knowledge_get` reads an entry whole or a range. A search that names
   no kinds answers at most two index entries and three records. (from D151)
8. Ranking is BM25 over FTS5. With an embedder, meaning is fused with words by reciprocal rank. A long entry is embedded in pieces
   the deployment's window bounds. With no model, every answer says it matched on words only. (from D24, D123, D151)
9. Disclosure applies at ingest. In shared mode a repository's local entries leave only when its manifest joins and shares
   knowledge; silence keeps them local. (from D21, D47)
10. A server over one checkout watches its refs and knowledge folders and reads again when marked stale or five minutes old (one
    minute where nothing can be watched). A host over registered roots judges each root's signature at most once a minute and
    reads only roots that moved, replacing a moved root's repository with nothing where it now reads none. A file or folder the scan
    cannot read (held by an editor, denied) is left out of its repository's reading, never the reading's end; a manifest or lock
    so held reads as none, a folder import still proposes its repository, and a code map so held answers a problem said. The registry counts what the index holds and waits for a reading only while it holds nothing;
    otherwise the reading runs behind it, so a rescan or a moved root holds no door that resolves a workspace. The repositories'
    summary, which answers the reading itself, waits for it. (from D151, D220)
11. A refresh reads each repository's generation before the registry, so a write made during the refresh refuses that
    repository's write. (from D220)
12. Every hit and `get` carries one `standing:` line: the reading's commit, `+ uncommitted` (over the record folders the scan reads, the
    declared documents it reads and each declared room's `AGENTS.md`, which a root's signature covers too; waited for three seconds at most,
    else `not yet read`), a fed reading's commit, and for a decision the notes after it and its `Amended by`/`Superseded by`
    marks. No model judges which record is current. (from D220)
13. An index a newer build wrote is refused, never rebuilt; the store's quests, sessions and keys stay usable. (from D36)
14. A call answers from the caller's own workspace. Naming another, or `all`, is a person's look. A session bound to its
    workspace never looks across. (from D48, D107)
15. The registry decides what is indexed. A registration is pushed by `daoris connect` and persisted; a folder scan proposes. A
    root is machine-local: answered to loopback callers only and stripped from anything that syncs. A registration's `kind` is
    `git`, `folder` or `system`: an import and the page's add say it from the root, the driver's follow reads a folder's again and registers
    one that became a git checkout as `git`, `connect`'s silence keeps the row's, and a row that never said is a repository's,
    held where its root stops being one. A folder's registration also keeps the person's own `excludes`, globs its manifest passes by
    beside the defaults: silence preserves them, an empty list clears them. A system's has no root and keeps its adapter as `system`,
    `<plugin>/<system>`, preserved when unstated; the page's *A system…* and `daoris repository add --system <plugin>/<system> [<name>]`
    add one, refused where no plugin installed and on declares that system (`SystemAdd`). (from D34, D48, D70, D254)

**The connector**

16. Each session spawns its own connector over stdio; all of them share one `knowledge.db` under the Daoris home. The store writes
    ahead into a `-wal` log beside the file, each commit synced in full (`StoreJournal`, set at every open), so a copy of the
    store is the file and its log together. Serving never
    builds. A workspace's own server is built by `node tools/knowledge-server.mjs build` into `local/`, and a running session
    moves onto a new build at its next request. (from D63, D151, D236)
17. Every registered tool has a class: read, restricted or publish. A tool with no class is refused to every named session.
    (from D173)
18. The call table serves by work. A session in a repository gets the 18 core tools (knowledge, memory, quests, its line, the
    registry, a helper's ask, the channel, handbacks) plus its kind's own. Ask Daoris gets every tool but the brainstorm turns,
    memory changes and `helper_ask`, which an intake is refused too: neither works in a repository of its own. A partner, a reviewer and a brainstorm voice get their handed rows. A session that cannot be placed gets
    the reads. `system_plan` is served only to a quest's session whose department is registered as a system (`SessionWork.InSystem`,
    read from the registration), and refused to every other work, so no repository's session lists it. (from D212, D230, D270)
19. `tools/list` names only what the table serves, and a refused call answers a sentence and a closed code before the tool
    runs. `McpRoleFilterTests` holds the table to the tests' `fixtures/call-table.json`. (from D212, D230)
20. A connector that names no session (a person's own agent) is not filtered by the table. (from D212)
21. A tool other than a read runs only for a caller whose lineage proves a root; a child may publish a plain quest and nothing
    else. A missing argument is answered from the tool's schema; argument values are never logged. (from D173)
22. An answer ends with one line of what waits on the ask's channel when a message waits that no line named yet, except `channel_read`'s own. (from D188)

**Memory**

23. Each registered repository has one memory, which it owns. The engine is per repository over `memory.db`, opened with
    reinforcement off, so a read changes nothing. (from D220)
24. Only a session of a registered repository writes or ends that repository's memory. Ask Daoris, an intake, a helper, a partner
    and a reviewer are refused; a reader asks the owner with `memory_propose`, which travels as a quest and joins one still
    untaken. (from D220)
25. Every change is a `memory_ops` row: `staged` by its session, released at the session's commit point (a landing's record, or
    a main-tree session's completion), then `ready` or `awaiting`, `applying`, and `applied`, `stale` or `held`; a proposal never applies and ends `answered`, `declined`, `stale` or `dropped`. Nothing staged
    applies by itself; a stopped session's rows wait for the person's Keep or Drop, and deleting a session drops its staged rows,
    while its awaiting end or replacement, with no session left, is a wait of its own. (from D220)
26. An end or a replacement waits for an approver other than its proposer. Until a second opinion may approve, that is the
    person. (from D220)
27. Only the service applies. A row is marked `applying` in one transaction and settled in another; a keyed receipt settles it
    after a crash. An outcome no receipt names is held for the person, never replayed. (from D220)
28. An entry cites records by id, lines, commit and digest. The sweep rechecks them: holding writes a confirmation, failing an
    awaiting end. A repository's sweep asks the engine for more due entries, doubling up to 1,000, until it has written 50, so entries
    that write nothing (inconclusive, amended, an end pending) do not hold back those behind them. A repository whose memory cannot
    be swept loses no other repository's rechecks: they are written, and the pass then ends failed, told by the coordinator. An
    entry is shown as current only when it was read again and every citation held. The sweep's end names no
    session, so the waits list it as a wait of its own (`GET /api/memory/waits`, the `end` of a wait with no session: its operation,
    repository, entry, revision and reason), *What needs you* shows it as a `memory` row by that operation with no door, and the
    person approves or declines it in the row through the decision by operation, which names no session. An awaiting end whose session
    was deleted is listed the same way. (from D220)
29. Another repository's memory is read only through its view, the caller's workspace checked again at each read. (from D220)

**The remote**

30. A workspace's remote is machine configuration: the home's `remotes.json`, keyed by workspace, the environment overriding.
    The repository's manifest says whether it may share (join, share knowledge); the machine says where. (from D47, D48)
    The service reads this file, and the session boxes' files the driver writes, through `AtomicFile.ReadText`, which shares
    deletion with the writer's move, so a read inside that move is never refused; a file held otherwise names no remote for that
    ask, since a take asks for its remote after it has committed.
31. Quests: every verb commits locally and succeeds locally. Sync is fetch, rebase and push of operations. The first push wins
    and the loser is kept as a conflict for a person. A take claims by push and awaits the remote's answer (its client gives up after 15 seconds); a take the remote does not
    answer stands unconfirmed and the next sync decides. (from D68, D69)
    A quest id is twelve characters only: no lookup reads the six-character width. A closed (Done or Declined) quest is removed
    by one remover, `Core/Quests/QuestClosedRemoval.cs`: tombstoned with a `deleted` operation where it may have travelled, simply gone where it never left;
    it removes no open or taken quest, and the person's delete of an open one (`QuestDeleteOperation`) writes the same tombstone. The home migration's `closed-quests` step
    clears the closed six-character quests the same way; an open one stays until it closes. A quest's id derives from its
    sender, receiver, title, lanes, workflow request key and parent. The same plain words published while that quest is open or
    taken are a retry: the answer is that quest, said, and nothing is written. Published after it closed (done or declined), or
    after this machine forgot it, they are a new ask under the next id (`~again<n>`), and the closed quest stays as it was. A
    request key or a parent is a quest's identity, so it answers its own quest whatever became of it.
32. Records sync, processes never. Session records, channel messages and code maps travel keyed by origin and id, a feed taken
    whole or refused whole. Only local knowledge feeds, replacing that repository's entries. No wire carries a root, a
    transcript or private content. A feed is admitted, ordered and written inside one gate, so one queued behind a declaration
    that stopped sharing is refused; a retirement takes the same gate, so a feed admitted before it leaves no provenance behind
    it. (from D47)
33. Two trust shapes: local trusts the loopback and binds nothing else; shared gates every route with minted per-person,
    per-machine, expiring keys, stored as a hash. (from D47)

**This repository's records**

34. `daoris.json`'s `documents` declares each record once: brief, router, index, areas, decisions, backlog, archive, fixes,
    changelog, glossary, gates, and the `present` homes. The doctrine region renders them as *Where things are*. (from D122, D151)
35. Each decision is its own file, `docs/decisions/D<n>.md`, its heading first and its notes appended at its end. The dispatching
    parent reserves the number. `docs/DECISIONS.md` is a fixed page with no rows. (from D106, D134)
36. Changelog, archive, fix log and twins merge by union; decisions by the `notes` driver; the router by the `rows` driver; the
    backlog as plain text. `npm ci` installs both drivers. (from D106, D134)
37. `doc-duplicates` (in `verify`) refuses a decision number, heading, index row or changelog line written twice, and a decision
    file with a heading not its own or a glued note label. (from D106, D134)
38. The index of where things are is written in each checkout and never committed: the checkout hooks and the knowledge server
    write it, and `orient-index --check` fails a stale file or a refused note mark. (from D151, D220)
39. A present fact has one home and is rewritten in place; a superseded way is removed with it. History is appended and read by
    lookup. Tests pin rules, never counts, except where the count is a budget. (from D220, D228)
40. The backlog holds open rows of a line or two. A finished row leaves for one archive line. A regression gets a fix-log
    entry, and a fix's commit body names the root cause. (from D127, D220)
41. Versions and release headings are stamped by the release workflow, never by hand. The glossary is the authority for names.
    Model choices are recorded by role, with evidence, in `docs/model-roles.md`. (from D116, D174)

## Where a change goes

- **A new record shape or role a checkout is read for:** the scanner (`RepositoryScanner`), with the split in `Knowledge/Ingest/`
  and the declared path in `RepositoryDocuments` or `RepositoryLayout`.
- **Ranking, kinds asked, standing words:** `Knowledge/Search/`, `KnowledgeQuery`, `EntryStanding`.
- **Storage or schema:** `Knowledge/Store/`; the store's schema version moves with it (`NewerIndexException`).
- **When a reading is taken again:** `CheckoutFreshness`, `RootSignature`, `UncommittedRecords`.
- **A new knowledge read over HTTP:** its records in `src/Daoris.Contracts/Knowledge/KnowledgeContracts.cs` and its handler in
  the HTTP host's `Routes/KnowledgeRoutes.cs`.
- **A new connector tool:** its own `*Tools.cs` in `Mcp/`, one line in `KnowledgeServer.ToolTypes`, a class in `ToolClasses`,
  its cells in `CallTable` and `fixtures/call-table.json`.
- **Memory behaviour:** `Knowledge/Memory/`; a new state or kind goes in `Memory/Records/MemoryOpKind.cs` and `MemoryOpState.cs`, never as a branch elsewhere.
- **A new kind that syncs:** a `<Kind>Sync.cs` in `Remote/`, a door on `IRemote`, and its route in the HTTP host's `PersonDoors`
  as a shared door.
- **Record checks and merges:** `tools/` (`orient-index`, `doc-duplicates`, `doc-current`, `merge-rows`, `index-hook`).

**Tests that pin it.** `src/Daoris.Service/Daoris.Service.Tests/`: `Knowledge/` (`KnowledgeStoreContractTests`, `SearchTests`,
`HybridSearchTests`, `RefreshTests`, `CheckoutFreshnessTests`, `IndexEntriesTests`, `DecisionNotesTests`, `KnowledgeStandingTests`,
`CitationRecheckTests`, `DisclosureTests`, `NewerIndexTests`, `OneCheckoutTests` and the `Memory*Tests`); `Mcp/`
(`McpRoleFilterTests`, `CoreSetTests`, `ToolClassesTests`, `McpToolRegistrationTests`, `CallLineageFilterTests`,
`ArgumentFilterTests`, `McpMemoryToolsTests`, `McpMemoryProposalTests`); `Registry/` (`RepositoryScannerTests`,
`RegistrationStoreTests`, `RepositoryLayoutTests`, `RepositoryDocumentsTests`); `Remote/` (`QuestSyncTests`, `SessionSyncTests`,
`ChannelSyncTests`, `CodeMapSyncTests`, `HttpRemoteTests`, `RemoteConfigTests`). Tools: `orient-index.test.mjs`,
`doc-current.test.mjs`, `index-hook.test.mjs`, `merge-rows.test.mjs`, and the CLI's `src/Daoris.Cli/test/knowledge-server.test.ts`.
The web: `src/Daoris.Web/src/knowledge/*.test.tsx`, `work/head/SessionMemory.test.tsx`.

**Gates that bite** (`daoris.gates.json`): `service` (`dotnet test src/Daoris.Service`, its `Process` category apart);
`service-process` (the route budgets over a benchmark home of three thousand sessions, run alone, on the `service` gate's build,
in the full set and the CI wherever the service's sources or tests change); `cli` (`npm run verify`:
`doc-duplicates`, `doc-system`, `doc-links`, the tools' tests); `tool-tests`; `code-map`; `universal` (`orient-index`, the docs
gate that fails `src/Daoris.Service/README.md` more than seven days behind its code, and the sensitive scan); `web-unit` for the
Knowledge view and session memory; `deployment` for the shared host.

## Contradictions

1. D151 points 1-2 (the index is committed files, never text-merged) vs `.gitignore:13-14` and `tools/orient-index.mjs:18-26`
   (ignored, written per checkout). The code reads right.
2. D134 point 3 (union per file, a merge driver rejected) and D151's rejected merge driver vs `.gitattributes:39` (`merge=rows`)
   and `:45` (`merge=notes`), installed by `npm ci`. The code reads right.
3. The `notes` driver (`tools/index-hook.mjs`) and the merge tool both set a glued decision note apart with `setApart`
   (`tools/doc-duplicates.mjs`), but the driver also sets apart an undated `**Note, ` line, which `NOTE_LABEL` and its other
   readers (the service's `DecisionNotes`, the digest) do not take for a label, and the driver's own `UnionJoins` is a third
   copy of the rule. D228 allows one.
4. D134 point 2 (no index of the decisions, hand-kept or generated) vs the generated digest `docs/index/decisions.md` (its own
   ORIENT1b note). The code reads right.
5. D106 (`docs/README.md` and `docs/DECISIONS.md` merge by union) vs `.gitattributes:30-39` (the router by rows, the page not
   union). The code reads right.
6. D47 (a quest has one home; its verbs write through or fail) vs D68 (every verb commits locally; fetch, rebase, push) and
   `Core/Remote/Remote.cs:10-20`. D68 reads right.
7. D21 (the shared store a git repository first; keys read-only; access mirrors the source host) vs D47 (git as store priced and
   declined) and `Core/Platform/ApiKeys.cs` (keyed writes at a SQLite host). D47 reads right.
8. D21 and D47 (people sign in to a shared host with OIDC) vs the service, which has no OIDC code. The code is today's truth.
9. D30 (the web's primary view is convergence) vs `src/Daoris.Web/src/knowledge/modes.ts:8,18-21` (Search first and default).
   The code reads right.
10. D22 (depend on released siblings, never a working tree) vs `src/Daoris.Service/Directory.Packages.props:19-22` (a local
    Lyntai build until its next release). The code is today's truth.
11. D21 (`local/` a hard exclusion in shared mode) vs `Core/Knowledge/DisclosurePolicy.cs:17-25` (no path rule): `local/` stays
    out only because the walk skips what git ignores, and a declared folder git ignores whole is read. The code holds in
    practice.
12. `tools/knowledge-server.mjs:22` (a reading re-read once a minute old) vs `Core/Knowledge/Store/CheckoutFreshness.cs:42` and
    `Core/Composition/ServiceFactory.cs:61` (five minutes while watched, one when not). The code reads right.

## Open

- **KNOWCENTRAL2i** — the installed measurement of the memory coordinator, looking also for conflicting entries about one fact
  (PROFILEFIX1's re-open condition).
- **KNOWCENTRAL1** (next round) — retirement through decay, which upkeep a model takes alone, a memory shared across repositories.
- **MC8** — a second opinion as an end's approver; until then the person approves every end.
- **KNOWFRESH1** — a-iv: a restatement within one repository found; b: a Lyntai release carrying MC7, MC11 and MC12 (today a
  local build); c: the machine's memory server's half; `doc-current` is report-only and not in `verify`.
- **D129 §6, KNOW2a** (parked) — recall by meaning pushed into a driven session's prompt.
- **OIDC for people on a shared host** (D21, D47) — no row.
- **DOCSHOME1** — managed documents and skills with more than one source, given two portable homes.
