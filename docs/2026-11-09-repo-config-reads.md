# The repository configuration design's reads

Both makers' reads of [the repository configuration design](2026-11-09-repo-config-design.md) (REPOCONFIG1), each read-only and
independent, kept as history; the design's next round answers them.

## Codex, the design

Codex's read of the design at `d1afd86621`, run by the steward from outside the install, read-only; verbatim.

One store is the right direction, but **the three lookups are not sufficient as specified**. These gaps block correctness:

- **§4.3, source versions:** `IntegrationAcceptance.AcceptedTextAsync` takes an explicit commit and frozen acceptance; `LineRules` takes `onto`. Freeze A, move the line to B, then read A: the proposed tip contract selects B. Specify contracts per method, retaining named commits and blob reads.

- **§4.1, entry metadata:** `LineDeclarationReader` rejects symlinks using `LineEntry.IsFile`. Commit identical bytes first as a regular file, then as a symlink: blob-only mappings cannot distinguish them. Retain mode and object type alongside content identity.

- **§4.1, replacements:** replacing a declaration’s blob changes the bytes git returns without changing its tree entry’s stored id. A replacement-enabled context can populate content later reused by a replacement-disabled context. Resolve source objects under context/history stamps; deduplicate only by hashes of actual returned bytes.

- **§4.2, restart:** `declaration_at` persists context but no history stamp. Save A’s lookup, stop, replace A’s commit, restart: unchanged `GitAskerContext` can load the old mapping. Persist and validate the complete history stamp before reuse.

- **§4.1, refs:** `CountedRefs.Counted` excludes `daoris/*`, while line settings accept those branches. Cache such a line, then let a session commit there: its version stays unchanged. Stamp the requested ref and its resolution dependencies, including excluded branches.

- **§4.1, stat aging:** read A while its stat is young; an editor writes same-length B within the timestamp grain, retaining file identity; ask again after two seconds. A’s cached stat now looks trustworthy. Never promote a racy observation merely because time passed; require a fresh hash before reuse.

- **§4.1, layered views:** `FileRules.Of` selects `person.For(repository)`; `FoundFiles.Read` also selects a repository. Two repositories can share all three file ids and floor facts yet have different personal rules. Include repository/scope selectors and signature/configuration dependencies in view keys.

- **Row d, analysis:** `FileAnalysis.Scripts` searches committed scripts using the current ignored tops. Add an ignored output at unchanged HEAD: a commit-only cached search can miss its writer. Cache the script corpus, filter per current tree, and retain fresh walks, floor facts and ignore-case configuration.

- **§4.1, discovery:** add a workflow or index file without changing any previously known file: existing content/stat keys cannot invalidate a cached list. Re-enumerate membership and router existence, or stamp those dependencies explicitly.

- **§4.1, failures:** `RepositoryFilesReader.AtAsync` currently maps a failed blob read through `Read(null)` to empty rules. Memoizing that result hides a later successful fetch. Distinguish verified absence, unread/truncated content and parse problems; never cache fallback emptiness as successful configuration.

**Rows a–e:** the broad dependency order is reasonable, but the proofs are insufficient. Make GITLIB1’s complete stamps an explicit prerequisite. Establish behavioral parity before lowering budgets; cover these sequences, writer races, malformed-file recovery and restart invalidation. Consolidate all gates parsers, including `DeclaredGates`, `VerifyRunner` and `RunnerOf`; mutation-test variable-path reads as well as literals. Row d’s two-start claim must account for script searches and ignore-case reads. Row e needs eviction tests counting mapping/absence rows and retained views, not just blob payloads; daily pruning alone supplies no hard admission bound. The existing bench reports costs without asserting these behaviors.

## Opus (independent), the design

The independent Opus 5.5 reader's read of the design at `d1afd86621`, read-only; as it answered, set down by the steward. One store with these three lookups can serve every reader; as written it has seven blocking gaps, and the rows prove start counts, not that each reader still reads the version it read before.

**Blocking.**

1. **The history stamp does not exist yet.** In code `GitAskerContext` hashes only `config.worktree` (and the folder where an `includeIf` exists); `GitReadCache` is keyed by the common directory alone; nothing reads `refs/replace/`, grafts, `shallow`, the git in use or the shared config. Sequence: commit C is read; while the desktop is down `git replace C C'` is run, or `core.useReplaceRefs=false` set in the shared config; at the cold start `declaration_at` answers with C's old blob; a replaced blob also leaves wrong bytes stored under its id, shared with every repository. Change: row a builds the stamp as one type, `GitReadCache` takes it too, `declaration_at` carries it and a row whose stamp differs is not read; bytes read from git are kept only if they hash to their id.
2. **A failure is cached as absent.** `GitObjectReads.TextAsync` returns the same null for missing, not-a-blob, too large and git failed, and `<commit>:<path> missing` also covers a missing commit or tree (lazy fetch is off). Sequence: an offline partial clone or a `stalled` repository at a watch's start; the follow's manifest read gives null; `none` is persisted; the row says "not set up on its line" after the network returns, across restarts. Change: keep "none" only from `ls-tree` exiting 0 with no entry (as `IntegrationAcceptance.BlobAtAsync` reads it); keep nothing from a failed, missing or oversize read, or an IO error on a checkout file.
3. **The racy rule as worded does not close the race**: it is judged by the stat's age at each ask, so a stat recorded young is trusted once it ages. Sequence: a hash at t; an in-place write of the same length inside the clock tick leaves the length, write time and file id unchanged; at t+3 s the stat is old and trusted, giving the old content. Change: record when the bytes were hashed and trust a stat only when the hash came at least 2 s after its write time (git's rule); read the stat before and after the bytes and keep the entry only if they agree.
4. **§4.3 puts `LineRules` and `IntegrationAcceptance` at the line's tip, but they are asked at a named commit** (callers `BranchReplays` `onto`, `IntegrationResolves` `lineBase`, `TogetherWatch` `facts.LineTip`, `TreeLandPlans` `sync.Onto`, `IntegrationPromotes` `candidate.Base` and `frozen`, `IntegrationPromoteReads`). Sequence: the line moves during a replay, so the rules come from the new tip while the replay is onto the old one. Change: both at a named commit; the store resolves a tip only for the ref the caller names.
5. **Some views rest on folder listings, not contents** (`RepositoryIndexes.Find` lists seven folders and checks the router exists; `WorkflowFiles.List` lists `workflows/`; `CodeMapFile.Find` checks existence). Keyed by contents, two trees with the same `daoris.json` but different index files share one list, and a workflow written by a separate `daoris` process is missed. Change: listings and existence checks stay per ask, or are part of the view's key.
6. **A checkout's id is not git's id.** With `core.autocrlf=true` (Git for Windows' default) or any filter, the raw bytes' hash differs from the stored blob; if `RemoteSync`'s unmodified check (row c) becomes "checkout id equals commit id", a clean CRLF checkout always reads modified and the registration never names its commit. Change: the checkout id keys the raw bytes only and is never compared with a commit's id; `RemoteSync`'s check stays git's.
7. **Row d's commit part is wider than named.** `FileGit.LinesAsync` greps at the commit for names taken from the tree's ignored tops; `IgnoresCaseAsync` reads configuration; `FileWalk` reads the tree's folders. Cached by commit alone, tree B's new top gets tree A's grep and is judged `Unknown` instead of `Output`. Change: key the grep by commit, names and pathspecs, ignore-case by the stamp, and name the walks as the tree's.

**Not blocking.** A shared parse must be immutable and thread-safe (`WorkflowFileFound.File` is a lazily filled `JsonNode`). Readers set different size limits on one blob (512 KB, 256 KB, whole): keep the size and apply each reader's limit. §4.4 misses two writers: `PersonFilesForm.Rewrite` (a write inside `PersonFiles.Read`) and `FileAsks`. Parses and views carry no weight in the 16 MB bound, and the 64 MB on disk is enforced only at the daily eviction. §1 lists the registry rows (79 call sites) but §4 never says whether they go through the store. The census will flag `EngineProfile`'s browser-profile `daoris.json`, `ServiceHostLocator` and `KnowledgeConnector.WorkspaceMarker`: it needs an exemption list. `IntegrationOwed` reads at the tip, not a named commit as §4.3 says. Row a's "a warm ask no dearer than its stat" cannot hold for tip or commit asks (they pay the context and the refs' version, about 2 ms). After a restart the follow still runs `FoundReads`' toolchain digest for every repository (`Asked` lives in memory): processes, not git starts.

**Readers missed by §1:** `TogetherChecks` (the row tables at the head tip, its own `declared ??=`, a fourth cache), `LineHolds`, `TogetherAccepts`, `TogetherWatch`, `TreeLandPlans`, `IntegrationResolves`, `BranchReplays`, `IntegrationPromoteReads`, `PlacePlan`'s parse, `HeavyRun`, `HeavyTold`'s existence check of the gates file, `CodeMapFile.Find`, `StandingBlocks`.

**Rows a to e.** The order is right (kernel, commit readers, checkout and home readers, views, persistence); the analysis's gates appear in both b and d; row a must build the stamp (finding 1); b's proof needs each moved reader's existing tests run unchanged and a line-moves-during-replay case (4); c's needs a write from another process and the in-place same-length race (3); d cannot reach two starts without finding 7's keys; e's shows only hits and needs misses: a replacement made while the desktop is down, a failed read not saved, a row with a different stamp refused.
