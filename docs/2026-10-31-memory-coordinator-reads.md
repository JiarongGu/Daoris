# KNOWCENTRAL1 round 2: both makers' reads of the memory coordinator's synthesis (2026-10-31)

The synthesis is `2026-10-31-memory-coordinator-design.md`. Codex asked for another round (six blocking findings); the Opus maker accepted after named changes (four blocking). Both reads follow as written.

## Codex's read

**1. Verdict: another round.** The ownership model, quest transport, and call-table placement are sound. The mutation and currency guarantees need another design pass.

**2. Findings**

- **BLOCKING — Recovery (§3, 2c).** An opaque revision detects movement; it cannot identify which operation committed after a lost response. `MoveStore.MarkEffect` holds uncertain effects rather than assuming success. Require operation-keyed receipts/idempotency, or an explicit uncertain hold, with concurrent-writer and lost-response proofs.

- **BLOCKING — Atomic replacement (§4, 2d).** “Successor and retirement apply as one change” is not established by the cited MC1–MC6 contract. Separate calls can expose contradictory facts or lose the predecessor without publishing its successor. Require and prove an atomic replacement seam before enabling supersession.

- **BLOCKING — Reads’ internal effects (§2, 2b/2g).** `ToolClasses.Read` means changing nothing, and `CallLineageFilter` serves reads to children. Only foreign MC7 reads explicitly prohibit use stamps, activation, and decay effects. Require observational behavior for every memory tool classed Read, including owner-local child reads; prove counters unchanged.

- **BLOCKING — Recheck evidence (§5, 2a/2f).** `FindAsync` and `StandingsAsync` do not provide one coherent snapshot. Moreover, `ReadingOfAsync` converts a failed dirty probe’s null result into false. Matching cached bodies could therefore confirm unsupported currency. Require coherent content/standing validation; unknown provenance and failed probes remain inconclusive. Test refresh races and probe failures.

- **BLOCKING — Historical attribution (§1, 2d).** Joining bindings when displaying an operation cannot establish what ran earlier: `BindingStore` updates and deletes bindings, and `binding_roles` contains no model tier. Capture operation-time role revision and actual run evidence, preserving unknowns explicitly; prove attribution survives continuation and binding deletion.

- **BLOCKING — Work without landing (§1/§4, 2d).** Checkout chats have repository authority but no Daoris landing. Their additions have no application path under this text. Define an explicit commit/approval lifecycle for such work, including cancellation and recovery, and test it.

**3. Rejections of my proposal**

- **Private task memory:** adequate in principle; staging needs the nonlanding lifecycle above.
- **Audited reassignment:** adequate; end, write, and quest preserve local ownership.
- **Explicit writer assignment:** adequate; repository-local Build/Design/Chat authority is a coherent alternative.
- **`WorkPolicy` ceilings:** adequate; `CallTable` already owns connector authorization.
- **Claim that CallTable/C2a were unbuilt:** correctly rejected; my checkout claim was stale.
- **Workspace change log:** adequate for the explicitly local first implementation; cross-machine delivery remains deferred.
- **Transactional outbox:** inadequate rejection; write-ahead recording alone does not establish recovery equivalence.
- **Separate stream identity:** adequate locally; journal store identity suffices alongside scope invalidation.
- **Appointed staff before MC8:** adequate; measured authority should precede autonomous retirement.
- **Separate citation owner:** adequate; the qualified id carries repository ownership.
- **`sourceView`:** adequate while one authoritative reading and explicit fed provenance identify the source.
- **`standingRevision`:** inadequate rationale; a validation token records a reading, not another authoritative home for marks. A coherent snapshot alternative could suffice.
- **Identities/contracts first:** adequate; passive citation functionality can establish its types through executable behavior.

**4. Order and proofs**

The broad sequence is buildable, **but the rows are not buildable as written**. Specify recovery and atomic replacement contracts before 2c’s crash proofs and 2d’s integration. Extend 2a/2b with coherent-validation and observational-read proofs; extend 2d with durable attribution and nonlanding work. Keep 2f conditional on those guarantees. The 2h measurement must distinguish two repositories on one machine from cross-machine behavior, whose transport is deferred.
## The Opus maker's read

**1. Verdict:** accept after named changes. Four of the changes are blocking. Each is a local fix to the design, so another round is not needed.

**2. Findings**

1. **BLOCKING: there is no workspace check to copy.** `circle.Outside` (`KnowledgeSearchTools.cs:157-158`) refuses only a session that ASKSCOPE1c binds, and only Ask Daoris is bound (`AmbientWorkspace.Bound`). Any other session can name another workspace or `all`. If 2b is built "as knowledge_get does", Build, Chat, Help and unnamed callers can read and propose across workspaces. Change: write a new check against the record's `Workspace` (the ambient one for unnamed callers) and apply it whether or not the session is bound. Where no workspace resolves, refuse; never span every workspace.
2. **BLOCKING: the person's card.** `PersonConfirmations` keeps cards in memory only, each lasts two minutes, at most five wait at once, and each is tied to one terminal's request. Ends held there would expire, be lost at restart, and a sixth landing would be refused. Change: keep the wait as a durable `memory_ops` state shown on the landing, the way `SessionGoAheads` keeps a go-ahead on the ask store.
3. **BLOCKING: states against crash settlement.** §3 names no staged state and no state for an end awaiting its approver, yet 2d's proof shows `staged`. Settling such a row by its unmoved revision after a crash could drop a staged write or apply an end nobody approved. Change: name `staged`, `awaiting` and `applying`, and settle only `applying` by revision. 2c's kill proof should keep a staged row and an awaiting row unchanged.
4. **BLOCKING: the tier is joined only when shown.** `binding_roles` is updated in place per session (`KeepRole`) and deleted with the session's record (`BindingStore.Forget`); another machine's mirrored records have none. Entries outlive sessions, so the card and the landing view would lose the tier or show the wrong one. Change: the driver writes role, revision and tier onto the row at landing, and the row carries them the way `HarnessVersion` travels.
5. **Recheck and `+ uncommitted`.** The flag covers a whole repository's record folders (`ReadingOfAsync`). Any session's edit therefore makes every citation into that repository inconclusive. Change: the same digest with no mark should Hold at any reading, and an uncommitted or pending reading should leave only a mark inconclusive. An id no longer indexed should ask for a new citation after a bounded number of tries, as a new digest does, because ids carry headings that D220 rewrites.
6. **2b also changes the driver.** The fixture's `_why` ties the reviewer and partner columns to `OpinionPermissions.Offered` and `PartnerPermissions.Offered`. `McpToolRegistrationTests` holds `BrainstormPermissions.ConnectorTools` to the registration. `Permissions.Defaults` decides which calls run without a prompt. Change: name all four in 2b.
7. **The read record was dropped, with no reason given.** 2f's "told once" needs a record of who read which reference at which revision, but `memory_ops` records operations only. Change: add a bounded, local read set to §3 and build it in 2c.
8. **Sessions without a tree, and the tree path.** A chat, or any session in the main tree (`Session.Tree` null), has no landing, and §1 never says when its additions apply. §1 also stores `tree`, a machine-local path, in a row meant to travel. Change: say when such a session's additions apply, and key staging by session rather than by path.

**3. Rejections of my proposal:** the journal as another machine's feed, a mark as approver, a fed reading as inconclusive, checking references only at get and expansion, and another maker's reviewer approving before MC8 were rejected for adequate reasons. Attribution read at the connector was rejected adequately as to what the connector can reach, but the replacement is worse (finding 4).

**4. Order and proofs:** buildable once these changes are made. 2a can start now (notes are separate anchored entries, so an amendment leaves the opening entry's digest unchanged). 2b's five tools ship before any engine exists, so until 2d they answer that memory is not on. 2c's bootstrap proof needs one read transaction tested against a writer on a second connection. 2d to 2h are gated correctly, on Lyntai's release, its answer to §6, and the install.


## Round 3: the reads of the revision

Codex asked another round (recovery, the coherent recheck and attribution not fully resolved, and one new blocking finding); the Opus maker accepted after three blocking changes. Both reads follow.

### Codex's read

1. **Verdict: another round.** Three original blockers remain incompletely resolved: recovery, coherent recheck, and historical attribution. The revision substantially improves the design, but its resolution table overstates those guarantees.

2. **The six blocking findings**

- **Recovery — not resolved.** The states are right: preserve `staged` and `awaiting`; settle only `applying`. An end’s coordinator-generated row id in its persisted reason can identify that end; unexplained movement going to `held` is safe. However, content deduplication is **not idempotent application**: Lyntai’s `src/Lyntai.Storage.Sqlite/SqliteMemoryGraphStore.cs:83` advances the engine before every upsert; duplicate writes replace supplied metadata (`:117`) and increment revision (`:130`). Repeated relations also strengthen edges (`src/Lyntai.Core/Memory/Engines/GraphMemoryEngine.cs:592`). A confirmation timestamp at or beyond the requested instant proves neither which operation ran nor which revision it confirmed (`src/Lyntai.Core/Storage/MemoryGraphSql.cs:173`). Require operation-keyed receipts for these effects, or hold ambiguous outcomes without replay. Prove concurrent same-content writes, repeated relations, and unrelated later confirmations.

- **Atomic replacement — resolved by deferral.** The cited implementation really stores the successor before approval and ends predecessors separately: Lyntai’s `src/Lyntai.Core/Memory/MemoryChangeGate.cs:65` and `:110`. Refusing `supersedes` until Request A is implemented removes the unsupported guarantee from the initial implementation.

- **Read effects — resolved at the design-contract level.** `ReinforceOn = None` reaches the early return before touches, co-activation and review writes (`src/Lyntai.Core/Memory/Engines/GraphMemoryEngine.cs:1942`). Applying it to every coordinator engine covers owner-local children as well as foreign readers. The counter-equality proofs in 2d are appropriate. Coordinator read bookkeeping is explicitly distinguished from changing the owner’s memory.

- **Recheck evidence — not fully resolved.** The revision correctly replaces separate reads and makes failed probes inconclusive; the existing null-to-clean conversion is indeed at `src/Daoris.Service/Daoris.Service.Core/Knowledge/KnowledgeService.cs:408`. But holding `_refreshLock` and `_feedGate` (`:203`, `:206`) does not exclude every writer: `RetireAsync` replaces repository entries without either gate (`:538`, `:550`). Require all relevant mutation paths to participate in the snapshot protocol, with a fixed lock order and bounded acquisition. Add retirement and workspace-restamping races to 2a, alongside refresh.

- **Historical attribution — not resolved.** Capturing role revision at the call fixes the mutable-binding problem (`src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/BindingStore.cs:134`, `:160`). But “the newest model `ran` reports” is not operation-time evidence. `Roles/Domain/ActorRun.cs:147` deduplicates model names, `:148` preserves first-seen order, `:179` imports prior reports, and `:187` receives turn events. A model revisited later need not become the last item; a current call can precede its turn’s report. Bind attribution to the current run/turn, preserving `unknown` until corresponding evidence exists.

- **Work without landing — resolved at the lifecycle-contract level.** The design now specifies completion, interruption, continuation, Keep and Drop, keyed by session rather than path. The named completion seam exists (`src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Chat/ChatRunner.cs:2939`). Implementation must reconcile durable completed records after restart so a crash between completion and releasing staged rows cannot strand them.

3. **New problems**

- **BLOCKING — coherent reads after an interrupted feed.** Gates prevent overlapping calls; they cannot repair an already split persisted reading. `KnowledgeService.cs:848` replaces entries before provenance is recorded at `:855`. A crash between them can leave new bodies with old provenance. Require atomic persistence of entries and provenance, or a durable incomplete-feed marker that makes recheck inconclusive until repaired. Add a restart proof at that boundary.

- **Nonblocking — the `Amends` fallback needs an explicit limitation.** It intentionally leaves both entries live. State that this records a partial correction or disputed claim; it does not satisfy D220’s replacement guarantee for an outright replacement. Keep supersession unavailable for that case.

4. **Request A to Lyntai**

It is the right ask: guarded, approved, atomic replacement with a recoverable repeat result. Add an explicit operation key and require its receipt to commit atomically with the replacement.

Holding supersession off is adequate for supersession safety. It does not establish safe replay of ordinary writes, relations or confirmations; those need their own receipt contract or conservative holds.

5. **The rows**

The broad order is sensible, but **not buildable as written**.

- **2a can start now**, incorporating complete writer coordination and interrupted-feed handling.
- **2b follows**, with the stand-in proving observational reads and workspace enforcement.
- **2c must wait for the corrected recovery contract**; its stand-in must reproduce actual duplicate-write and relation behavior.
- **2d** then integrates the released engine, corrected attribution and restart reconciliation.
- **2e–2f** follow those foundations.
- **2g** waits on B; **2h** waits on A.
- **2i** correctly measures two repositories on one machine; it proves no cross-machine behavior.

### The Opus maker's read

**1. Verdict:** accept after named changes. Three are blocking, each a sentence or two in §1, §3 or §5; no further round is needed.

**2. My findings.** 1 (workspace check), 2 (the person's card), 6 (four sources), 7 (read set; say that a reference trimmed from the 256 is never told it moved) and 8 (no tree) are resolved. 3 is resolved for `applying` against Lyntai's SQL: an end is one statement guarded by `ended_reason IS NULL` and the revision (`MemoryGraphSql.cs:154-166`), a stamp never moves back (`:171-177`), an upsert is keyed by content (`SqliteMemoryGraphStore.cs:94-134`); one hole remains (new problem 3). 4 is resolved for session, role and revision at the call, and unsound for the model (new problem 2). 5 is resolved in substance; the coherence it rests on does not hold (new problem 1).

**3. New problems**
1. **BLOCKING: the writer gates belong to one process.** Each session's connector is its own process over the shared `knowledge.db` (`Daoris.Service.Mcp/Program.cs:18-20`, `:104`); `_refreshLock` and `_feedGate` (`KnowledgeService.cs:203-206`) are in-process, and the reading lives in that process's `_readings` (`:185`, `:1189`). A connector that scanned before a merge and writes after the recheck's refresh leaves rows without their `Superseded by` mark, and a superseded record Holds. Change: keep each repository's reading in the store, written in the transaction with its entries; refuse a refresh older than the reading held; read entry, marks and reading in one read transaction. 2a's proof gains a second `KnowledgeService` over the same file writing between entry and standing.
2. **BLOCKING: the model at the call reads nothing.** `ran` is written when a turn ends (`ActorRun.cs:185-198`, emitted at the result frame, `StructuredOutput.cs:354-361`), asynchronously (`ActorRun.cs:200-207`); a one-turn driven run's calls all find none, and `Models` is deduplicated first-seen (`:142-150`). Change: session, role, revision and harness at the call; the models at the commit point, as the set `ran` reports then, `unknown` only where it reports none. 2d's proof gains a one-turn session's write naming its model.
3. **BLOCKING: the commit point's move is outside crash settlement.** A kill after `completed` or the landing is recorded but before the rows leave `staged` strands them. Change: move the rows in the transaction that records the commit point (the record's state or `work_record_sessions`, `WorkRecords.cs:72-73`), or sweep at the service's start; only the service writes the engine and settles rows, connectors write `memory_ops` and read. 2c's proof gains a kill between the commit point and the move.
4. A Holds confirmation and a repeated addition bump the revision (`MemoryGraphSql.cs:173`, `SqliteMemoryGraphStore.cs:130`), so an approved end awaiting at R goes stale after any sweep: while an end awaits, record the Holds on the row and do not confirm the entry.
5. Ended under another row's receipt: settle this row `stale`, not `held`.
6. Lyntai never calls `Recheck` (`GraphMemoryEngine.cs:463-475`, at most 2,000 characters, `MemoryRecords.cs:195`): say the coordinator's sweep calls `RecheckAsync`.
7. A hand-over continuation in the same tree sees its predecessor's staged rows, by `From`; the reason is shown to readers (`MemoryTools.cs:74`), so the row id is a trailing token the page strips.

**4. Request A** is right, with one clause too many: drop "nothing stored before the approval" (Daoris holds the successor on its own `awaiting` row and calls Lyntai only after approval; the clause would reverse `MemoryChangeGate.cs:65-68` for nothing). Add: the end's reason, or a caller's key, kept on each predecessor, so receipts settle a replacement. Holding supersession off meanwhile is adequate; prefer `Disputes` to `Amends`, which Lyntai defines as leaving the rest standing (`MemoryRelations.cs:9`).

**5. Rows:** buildable once the three changes are written. 2a can start now with its coherence in the store; 2b beside it over a stand-in engine; 2c after 2b's stand-in; 2d and 2e on Lyntai's release; 2f after 2a, 2c and 2d; 2g on §6 B, 2h on §6 A, 2i on the install.

## Round 4: the reads of the third revision

### Codex's read

1. **Verdict: accept after named changes.** Correct durable model attribution and protect 2a against already-open older writers. No further design round is necessary for these bounded corrections.

2. **Round-3 blockers**

- **Recovery: resolved as a contract.** §3 now holds ambiguous additions, relations and confirmations without replay until C supplies receipts. Ends recover by their operation token; another operation’s end means `stale`. This addresses the recorded Lyntai effects: `src/Lyntai.Storage.Sqlite/SqliteMemoryGraphStore.cs:83,130`, `src/Lyntai.Core/Memory/Engines/GraphMemoryEngine.cs:592`, and `src/Lyntai.Core/Storage/MemoryGraphSql.cs:173`. Lyntai source is absent from this checkout, so these remain the recorded round-3 source checks, not fresh verification.

- **Atomic replacement: resolved by deferral.** Refusing `supersedes` until A removes the insert-then-end guarantee. `Disputes` correctly leaves both claims current without claiming D220 replacement.

- **Read effects: resolved at the design level.** Every coordinator engine must use `ReinforceOn = None`; 2d explicitly checks owner and child reads. The supporting Lyntai citation remains qualified as above.

- **Coherent recheck: resolved for participating writers.** The proposed stored generation, conditional replacement and transactional entry/marks/reading snapshot replace process-local gates. I checked every production replacement: `Knowledge/KnowledgeIndex.cs:47,67` and `Knowledge/KnowledgeService.cs:550,653,753,848`, beneath `src/Daoris.Service/Daoris.Service.Core/`. This covers refresh, ghosts, retirement, restamping, withdrawal and feeds. One compatibility exception remains below.

- **Historical attribution: not fully resolved.** Per-turn reports fix first-seen deduplication, but §1 incorrectly says their models are persisted in the event log. `src/Daoris.Desktop/Daoris.Desktop.Driver/Events/Domain/SessionEvents.cs:396` marks `Models` **`[JsonIgnore]`**; `Context/Events/SessionEventLog.cs:64` serializes using that contract. **Change:** specify a machine-local durable per-turn report containing session, sequence, instant and reported model names, handed into the commit transaction. Preserve `unknown` for missing evidence. Add restart-before-landing and A→B→A proofs; keep models off shared records.

- **Work without landing: resolved.** Moving operations inside completion eliminates the crash gap: `Sessions/SessionLedger.cs:1929,1974` runs through `Sessions/Sessions.cs:847`’s transaction. `Sessions/WorkRecords.cs:369,408` likewise provides the transaction containing the record and session links. Posts without landings remain staged.

- **Interrupted feed: resolved as specified.** Entries and provenance must commit together. The existing split is exactly `Knowledge/KnowledgeService.cs:848,855`; `Composition/ServiceFactory.cs:312` supplies their shared connection. The fault-and-restart proof in 2a targets the correct boundary.

3. **New blocking issue: older open writers bypass the reading protocol.** `Knowledge/Store/SqliteKnowledgeStore.cs:102` checks schema at open; `:154` subsequently consults cached `Refusal`, then `:167` replaces entries. An already-open older connector can therefore change bodies while leaving a valid-looking new reading untouched. **Change:** put the new entry/reading pair in versioned entry tables older writers cannot reach, and add a proof using an old connection opened before upgrade. A schema bump alone is insufficient.

4. **Requests A and C: yes, right as worded.** Both require operation-key receipts committed with effects and repeat-without-effect semantics. A correctly leaves approval with Daoris.

5. **Rows:** **2a needs the older-writer correction before starting as written. 2b may start now**, independently over its specified stand-in.

Read only; nothing changed, and no `local/` directory was opened.
