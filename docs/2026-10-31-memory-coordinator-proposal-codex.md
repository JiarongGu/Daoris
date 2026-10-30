This proposal serves D193’s knowledge and coordination functions: keep one accountable home for a fact, let other repositories rely on it, and make corrections travel to its owner.

I read `docs/decisions/{D45,D48,D191,D193,D220}.md`, the relevant D212 foundation sections and D173 lineage contract; `TASKS.md`; `docs/2026-10-30-memory-currency-{brief,proposal-codex}.md`; and `docs/2026-10-30-knowledge-freshness-design.md`. Discovery used `.claude/INDEX.md`, `doc-loader`, and the applicable knowledge contracts. Code references below identify the implementation read. I read neither `local/` nor the other maker’s proposal.

**1. Memory belongs to a repository; a workspace bounds disclosure.**

Choose one durable memory namespace per owning repository within its workspace’s deployment. Map it to a Lyntai task; retain separate private task memories for unfinished observations. A role or session authors a fact but does not become its permanent owner.

Each fact carries an owner repository identity, engine/task/reference, opaque claim revision, author session and work, and provenance. Daoris records which owner-local writer or confirmer accepted each change. Ownership survives that session ending; reassignment is explicit and audited. Simultaneous authorized writers compete through revision guards.

This follows D45’s local ownership and D48’s workspace boundary. Reject one workspace-wide writable task: sharing would erase ownership. Reject permanent memory per role or session: continuation and staffing changes would multiply present facts.

Tracked records remain canonical. Memory cites them rather than becoming another authoritative copy. A replacement names its predecessor; admitting the successor and retiring the predecessor form one published change. Apply retirement through amended D241’s decay seam, preserving its reason and recoverable history.

**2. Extend the existing work policy; intersect it with resource ownership.**

Add memory permissions to the existing `WorkPolicy`, resolved through `WorkPolicies.Of`; do not introduce an independently configured memory policy or put permissions on role staffing. The four operation ceilings are read, propose, write and end. Scope restrictions narrow those ceilings.

`CallTable` classifies each tool by operation and work. The resource judge then checks the authenticated caller, current policy, workspace, owner and target reference. Neither check substitutes for the other. Search candidates, expansion, history, citation resolution and subscriptions all receive the resource check; possessing a reference grants nothing.

Choose these defaults:

| Caller | Memory authority |
|---|---|
| Owner-local build/design/chat root | Read and propose; write/end only within an explicit owner assignment |
| Helper, partner, reviewer | Authorized reads; suggestions travel in their existing result |
| Native child | Read only, narrowed by its parent’s scope and verified lineage |
| Ask Daoris | Authorized reads and proposals to owners; no general write/end authority |
| Unplaced or unverifiable caller | No memory mutation |

An attributed `Disputes` or `Amends` relation changes owned memory and therefore requires write authority. A reader’s proposed relation remains a proposal.

I read `Participants/Domain/WorkPolicy.cs` under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and `Sessions/{ParticipantWork,CallLineage}.cs` under `src/Daoris.Service/Daoris.Service.Core/`. This checkout still has `Daoris.Service.Mcp/RoleFilter.cs`, rather than `CallTable`; its FABRIC-C2a rows remain open. Thus the brief’s call-table and lineage guarantees are prerequisites for exposing mutations, not guarantees this checkout already enforces.

Reject a scope supplied in model arguments as identity. Locally resolved grants may travel as authenticated, narrowed capabilities; shared hosts validate them on every operation, including revocation. They receive no machine paths or desktop policy files.

**3. Give memory its own change log; reuse the journal for local wakes.**

Choose a workspace-scoped memory change log. The existing `Sessions/EventJournal.cs` captures machine-local transitions, trims its history, and does not travel. Exporting it would violate FOUNDATION1’s privacy contract.

Each accepted memory change records an operation ID, owner, affected references and revisions, operation kind, attribution, confirmation and evidence references. The log orders commits within its store; timestamps establish no cross-host order. Claim text stays behind separately authorized reads.

Require mutation and durable notification capture to commit together through a transactional outbox in the memory adapter. Replacement batches also commit together. This is a required integration seam, not a capability established by D241’s summary. If the chosen adapter cannot provide it, request the neutral seam from Lyntai before enabling shared mutations. Reject a successful mutation followed by a best-effort log append.

Readers subscribe by authorized owner namespace or reference. Each keeps its own cursor, including stream identity and scope generation. Cache invalidation and cursor advancement commit together; replay is harmless. Bootstrap returns a snapshot and watermark from one consistent cut, followed by changes after that watermark.

A replaced store, expired cursor or changed authorization invalidates the affected cache and requires a new authorized snapshot. History across a trimmed gap is explicitly incomplete.

`Platform/JournalRows.cs` and the driver’s `Events/JournalFeed.cs` provide the existing store/floor/cursor exemplar. Reuse those semantics, not the raw journal’s disclosure surface. Local journal events may wake the memory-log reader.

**4. A correction request is an ordinary quest, carrying a precise proposal.**

Choose the quest as the durable request to another repository. Initially carry a versioned proposal packet in its body: request ID, target reference and revision, proposed operation, replacement or relation, evidence citations, and proposer attribution. Validate the packet when applying it. No new quest operation is necessary merely to transport it.

The owner chooses how to answer. Its appointed writer or confirmer explicitly answers MC6 against the exact proposal and claim revision. Missing approver, timeout or disagreement leaves the proposal unresolved. A newer revision requires fresh consideration. Closing a quest does not itself approve or apply memory changes; its handback names the resulting change or refusal.

This matches `Daoris.Service.Mcp/QuestTools.cs` and `Daoris.Service.Core/Quests/QuestExchange.cs`. A word may alert an existing owner session, but is not the request or its authority. Helpers and reviewers return suggestions through their lead rather than acquiring quest-publishing powers.

The person sees the existing quest and handback, with the affected fact, evidence, confirmer and outcome. Routine confirmation belongs to authorized staff; D191’s owner-only matters still go to the person. Reject both a new request inbox and blanket person approval for every correction.

**5. Cite an indexed entry and the exact reading, not a line number alone.**

Use a citation shaped as:

`{ owner, entryId, sourceView, revision, commit?, contentDigest, standingRevision }`

`entryId` uses today’s `KnowledgeEntry.Id`; `sourceView` identifies the authoritative reading without exposing its checkout path. Lines remain navigation hints. The opaque revision covers indexed content and the standing information relevant to that citation. Commit alone cannot distinguish uncommitted edits.

I read `Knowledge/{KnowledgeEntry,EntryStanding,KnowledgeService}.cs`, `Knowledge/Ingest/{EntryAnchors,DecisionNotes}.cs`, `Knowledge/Store/CheckoutFreshness.cs`, and `Daoris.Service.Tests/Knowledge/KnowledgeStandingTests.cs`.

`Recheck` resolves the citation through Daoris’s index, verifies the source reading, and returns structured evidence: unchanged, changed, superseded, amended, missing, or unreadable. `no mark` remains exactly that. Pending or unknown provenance cannot become a clean confirmation. A fed source supports a statement about the revision fed, not an unsupported claim about its repository’s latest state.

An amendment returns its context without retiring unaffected claims. A supersession mark identifies historical standing; the writer or confirmer still decides the cited claim’s retirement through D241. Network failure, missing permission and ambiguous identity are inconclusive.

Reject automatic semantic approval from matching commits, later dates, similarity, or absence of marks.

**6. MC7 must be an observational view with qualified references.**

Ask Lyntai for a read-only view of another task that supports bounded recall and reference expansion, returning source task, reference, opaque revision, relations and retirement standing. Historical expansion must say it is historical. References remain qualified by their source; no foreign claim is inserted into the reader’s task.

“Read-only” must cover internal effects: reading the view must not update source use, confirmation, activation or associations. Otherwise a reader can influence an owner’s decay despite lacking write permission. Unsupported engines report the missing capability explicitly.

Daoris selects the allowed views and enforces grants before lookup and expansion. Lyntai owns engine consistency and observational behavior; it need not know repositories, quests or policy.

Before presenting cached material as current, Daoris obtains an authorized validation of the claim revision and its dependencies. The answer is stamped “checked at” the returned revision and watermark. An invalidation received before delivery discards that result. If validation is unavailable, retain an explicitly historical/unverified reading or withhold it from the current answer.

Subscriptions reduce work; they do not prove currency. `DueAsync` repairs missed upkeep; it is not the presentation barrier. Reject copying another task’s facts or silently refreshing its decay through ordinary reads.

**7. Build in this order, with these proofs.**

1. **KNOWCENTRAL1a — identities and contracts.** Define owner, citation, revision, proposal and observational-view shapes; send MC7’s contract to Lyntai. Prove reference isolation, ownership surviving session continuation, and explicit unsupported capabilities.

2. **KNOWCENTRAL1b — grants at every door.** Extend `WorkPolicies.Of` and the foundation call table after FABRIC-C2a’s root proof. Prove every operation, forged references, foreign expansion, unknown tools, revoked grants, and root/child distinctions. Connector registration and harness restrictions move together.

3. **KNOWCENTRAL1c — citations and `Recheck`.** Add the index validation door. Prove committed and dirty readings, pending status, fed provenance, moved anchors, amendments and whole supersession; failures remain inconclusive.

4. **KNOWCENTRAL1d — confirmed mutation and durable capture.** Integrate MC1–MC6 with the transactional adapter and outbox. Prove stale confirmations refuse, absent approvers approve nothing, replacement publishes once, and crashes cannot expose a changed fact without its change record.

5. **KNOWCENTRAL1e — proposals through quests.** Add packet validation and linked outcomes. Prove duplicate delivery, changed target revisions, declined proposals and owner unavailability; existing quest views show the result in both chrome languages.

6. **KNOWCENTRAL1f — subscriptions, MC7 and presentation validation.** Prove snapshot/tail races, restart, trimming, store replacement, authorization changes, disconnection and observational reads. Cached stale material never receives a successful current-validation stamp.

7. **KNOWCENTRAL1g — installed measurement.** Exercise two real repositories across restart, concurrent replacement and interrupted delivery. Measure validation latency, convergence, false-current answers, lost current claims and cost. Report the actual retrieval/model tier. Autonomous semantic retirement remains unpromoted until MC8’s isolated arms have adjudicated labels and an accepted error bar; candidate-coverage claims also await MC3’s embedding measurement and MC10’s window repair.