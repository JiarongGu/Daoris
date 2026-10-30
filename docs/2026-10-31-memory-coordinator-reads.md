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

