# The memory coordinator: Daoris's half of a shared, model-kept memory (KNOWCENTRAL1, round 3)

**Standing:** accepted 2026-10-31 under D191: both makers' reads of the third revision accepted after named changes, each taken here
by the steward (`…-reads.md`, round 4 and the settlement). The steward's synthesis from the brief and the sealed proposals of the Opus
maker (O) and Codex (X), revised in place after rounds 2 and 3; checked at Daoris `a0dbbc423`, Lyntai `5cfdc76a`. Rows 2a and 2b are
buildable now.
**Governing:** D220; Lyntai's D241 as amended; D205; D212; D173; D191.

## 0. Where the two proposals agree

One memory per repository; role and session as attribution, identity from the connector's placement; a child only reads; a
correction is a quest; a record cited, never ingested, an amendment never ending an entry whole (that cut its recall from 66% to 14%);
`DueAsync` proves no currency; no model approves an end before MC8's verdict.

## 1. Where memory lives and who owns a fact

**Choice (O).** One Lyntai engine per repository, keyed by its registry name, held by the local service of the machine that drives it,
its only writer. The repository owns every entry. Intake and Ask Daoris name no repository (`Service/Domain/SessionRecord.cs:12`), so
they write none.

**Who, at the call**, from the caller's record, never from arguments: session, work, repository, workspace, quest, adapter, harness
version (`Sessions/Sessions.cs:101-117`); the role and revision the driver names on the connector's environment at each start
(`IntakeScope.cs:28`), so a continuation writes its own; the call's instant. Nothing is joined later from `binding_roles`, which
`KeepRole` overwrites and `Forget` deletes (`BindingStore.cs:134-146`, `:160-172`).

**Which model, at the commit point.** `ran` cannot say at the call: it is kept as a turn ends (`ActorRun.cs:185-198`;
`Agents/StructuredOutput.cs:355-363`), asynchronously (`ActorRun.cs:200-207`), even after a chat's record ended
(`Loop/Chat/ChatRunner.cs:2839`, `:2939`), and first-seen (`ActorRun.cs:142-150`). Nor can the event log: its Turn line carries no
models (`Events/Domain/SessionEvents.cs:393-397`, `[JsonIgnore]`, D179 point 2). The usage history's turn lines are the durable
per-turn record (`Accounts/Usage/UsageHistory.cs:286-299`, `:613-650`: session, seq, started, ended, model or split). At the commit
point the driver hands each moved session's turn lines; a row's models are those of the turn whose `started`..`ended` holds its
instant. Where `started` is missing, an asked `User` event between the instant and the turn leaves `unknown` (a chat stopped
mid-turn and continued, `SessionLedger.cs:1947`, takes no later turn's model); a pruned or dropped line reads `unknown`. A row
that travels says `not shared` for the model (D205).

**An operation is released in the transaction that records its session's commit point.** Rows are staged by session id, never by
tree, a machine-local path (`Sessions.cs:88-98`):
- a session with a tree: the post of its finished tree's record naming a landing (`WorkRecordStore.PostAsync`,
  `Sessions/WorkRecords.cs:366`, `:44-48`; its sessions in `work_record_sessions`, `:72-73`);
- a chat or any session in the main tree (`Session.Tree` null, `Sessions.cs:116`): the ledger's advance to `completed`, one
  transaction (`SessionLedger.cs:1929`, `:1974`; from `ChatRunner.cs:3865`);
- otherwise (stopped, failed, interrupted, declined, a post naming no landing) rows stay `staged`, carried by a continuation, the
  record's page offering the person *Keep* or *Drop*. Nothing staged applies by itself.

Only its own session sees a staged row, and a hand-over continuation its predecessor's, by `From` (`Sessions.cs:175`).

**Why.** A session changes its own repository; a commit point is where its work is accepted; a model is evidence of a turn.
**Rejected.** A workspace's or role's memory. X's private task memory (staging holds it) and audited reassignment (two changes and a
request). The model at the call, or `ran`'s last name.

## 2. Grants on every operation

**Choice (O, with X's coverage).** `ToolClasses.Named` classes `memory_search` and `memory_get` (expansion and history included) as
`Read`, `memory_write` and `memory_end` as `Restricted`; `memory_propose` is restricted by its suffix (`ToolClasses.cs:77`), so a child
is served the reads alone (`CallLineageFilter.cs:51`). Until the engine exists (2d) each tool says memory is not on. `CallTable` gains:

| Work | Read | Propose | Write, end |
|---|---|---|---|
| Build, Design, Chat | yes | yes | its own repository |
| Intake, Ask Daoris | yes | yes | refused |
| Help | yes | refused, as `Serving` refuses quests | refused |
| Review, Partner | in `ReviewerServed`, `PartnerServed` | refused | refused |
| A brainstorm turn | refused | refused | refused |
| Unread, unplaced, a child | yes | refused | refused |
| Unnamed (a person's own agent) | yes | yes | refused by the memory box |

**A Read is observational**, root or child. A Lyntai recall reinforces, co-activates and logs what it returns, so every engine the
coordinator opens sets `ReinforceOn = None` (`GraphMemoryOptions.cs:285`), whose gate skips touches, links and review rows
(`GraphMemoryEngine.cs:1942`); recall and expansion write nothing else (`:1022`, `:1281`) and age nothing (`:1908`).

**The workspace check is new.** `WorkspaceScope.Outside` refuses only a bound session (`WorkspaceScope.cs:62-66`; `:31-41`). Every door
that takes or yields a reference checks its repository against the registry's members of the caller record's `Workspace`
(`Sessions.cs:112`) at each call, bound or not, an unnamed caller's being the ambient one (`AmbientWorkspace.cs:51`); none resolved is
refused. No memory tool takes a workspace or repository argument.

**Four permission sources move with the table** (O): `OpinionPermissions.Offered` and `PartnerPermissions.Offered` gain the two reads
(`CallTable.cs:42-57`); `BrainstormPermissions.ConnectorTools` gains all five, denied, held by `McpToolRegistrationTests` (`:53-57`);
`PermissionRules.Defaults` allows the five unprompted (`Agents/Permissions.cs:111`): a write stages, an end waits, a proposal is a quest.

**Rejected.** X's ceilings on `WorkPolicy`, which the connector never reads; X's owner assignment; a child's proposal; a scope from
arguments; `knowledge_get`'s check, which binds only Ask Daoris.

## 3. The change log, recovery and cursors

**Choice (O's record, X's limits).** The log of record is `memory_ops`, in `knowledge.db` beside the sessions and the finished trees'
records: id, session, repository, kind (`write`, `relate`, `end`, `confirm`, `propose`), target, revision read, attribution, approver,
and a state: `staged → ready → applying → applied` (an end `staged → awaiting → ready → …`), or `dropped`, `declined`, `stale`, `held`.
A connector writes `staged` rows and reads; a commit point's transaction moves rows, whichever process records it; only the service
marks `applying`, calls the engine, settles and sweeps.

- **`awaiting`** holds an end for its approver (§4), durable, decided through the row as a go-ahead is kept (`SessionGoAheads.cs:7-11`),
  never an in-memory `PersonConfirmations` card (`PersonConfirmations.cs:124-127`, `:180`).
- **`ready`** was never tried; the service applies it, at its start too.
- **`applying`** is written in its own transaction before the engine call, the outcome after; a guard mismatch is `stale`.
- **Recovery holds what no receipt names** (X). At start each `applying` row is read observationally:
  - an end's reason carries its row id as a trailing token the page strips (readers see the reason, `MemoryTools.cs:74`), and an end is
    one statement guarded by `ended_reason IS NULL` and the revision (`MemoryGraphSql.cs:154-166`): ended under this token, `applied`;
    under another's, otherwise, gone, or unended at another revision, `stale`; unended at the revision read, ended again;
  - an addition or relation is **`held`**, never replayed by itself: nothing names which operation ran (§6 C). The person settles
    it from the entry's current state (present, `encoding_at`, revision, relations; Lyntai keeps no per-entry change log,
    `MemoryRecords.cs:169`), as `MoveStore.MarkEffect` holds an effect (`Course/MoveStore.cs:24-25`, `:472-488`); §6 C's receipt
    would settle it (2j);
  - a confirmation is guarded by the revision read (`MemoryGraphSql.cs:171-177`), so it settles `stale` and the next sweep rechecks.
- **While an end awaits**, a Holds on its target is recorded on its row and the entry not confirmed: a confirmation bumps the revision
  the approver read (`MemoryGraphSql.cs:173`).
- **The sweep.** Lyntai keeps a re-check text (2,000 characters at most) and runs nothing (`GraphMemoryEngine.cs:463-475`,
  `MemoryRecords.cs:195`). The service reads `DueAsync` (`GraphMemoryEngine.cs:524`), calls `RecheckAsync` (§5) per citation, and
  writes a Holds as a `ready` confirmation, a Failed as an `awaiting` end.
- **Journal.** A trigger journals `memory` rows into `event_journal` (ids and codes), waking readers and the applier, and never travels
  (D212 point 11); rows travel later, as a permitted record. **Cursor:** `(store, position)` (`Platform/JournalRows.cs:15`); a new store or a floor
  past it re-validates. An unsettled row counts as a change; a bootstrap reads rows and newest position in one transaction; a change of
  scope invalidates (X).
- **The read set** (O): who read which reference at which revision; local, bounded (256 per session, 65,536 in all), so a running
  session is told once that an entry it read moved. A reference trimmed from it is never told.

Subscribers: the driver (FOUNDATION1m), EVENTHOOK1's plugins, the page, MC7's caches.

**Rejected.** X's outbox: Lyntai's store opens its own transaction per call (`SqliteMemoryGraphStore.cs:80-81`). Content
deduplication as idempotence: it repeats effects. Settling by revision or stamp alone. A second stream identity. Triggers on Lyntai's
tables.

## 4. A reader's proposal

**Choice (both).** A quest. `memory_propose` on another repository's entry publishes one to its owner, or joins the open one from the
same repository (`SessionGoAheads.cs:10-11`). Its body is a versioned packet (X): target, revision read, relation (`Disputes`,
`Amends`, an end), citations, proposer. The owner's session decides through its own operations or declines with a reason. Closing the
quest applies nothing; its handback names the outcome.

**The approver (MC6).**
- An addition, relation or confirmation is released at its commit point; a relation ends nothing (D241).
- An end needs an approver neither its proposer nor its lineage: until MC8's verdict, the person, on the row where the landing or record
  lists its ends with evidence, proposer, adapter and model, keeping the revision shown. After MC8, another maker's second opinion where
  `WorkPolicies.Of(…, Review, …)` names one (D155); the person keeps a chat's ends and any the reviewer declines.
- A `Superseded by` mark is evidence, never the approver (X); meanwhile hits say `end proposed by <session>, awaiting <approver>`.
- **Supersession waits on Lyntai.** Its gate stores the successor, then ends each predecessor in another call
  (`MemoryChangeGate.cs:65-68`, `:110-111`): between them both are current. So `memory_write` refuses `supersedes`, naming §6 A, until
  2h. Meanwhile an owner replacing a fact writes the new one with `Disputes` naming the old and proposes its end: two decisions, both
  current until approval, a disputed claim and never D220's replacement. `Amends` is only a correction leaving the rest standing
  (`MemoryRelations.cs:9-10`).

The person sees each citation's standing on the quest, and a landing's memory changes beside its commits.

**Rejected.** A word, or a new kind of request. A mark as approver. Appointed staff before MC8. A same-maker approver (Lyntai's
D109). Hiding the successor until the end lands.

## 5. Records cited

**Choice (O's shape, X's reading).** A citation is `{id, lines?, commit, fed, digest}`: the repository-qualified `KnowledgeEntry.Id`,
with no machine path; the span a hit names; `commit` and `fed` from the stored reading; the body's digest. The session names id and
lines; the coordinator fills the rest. Citing an already superseded entry is refused, naming its successor.

**The reading is a stored fact** (O, X). Connectors are processes over one `knowledge.db` (`Daoris.Service.Mcp/Program.cs:18-20`,
`:104`); a reading lives in one process (`KnowledgeService.cs:185`, `:1189-1195`); the writer gates are in-process (`:203-206`),
retirement takes neither (`:538-551`), and a feed records provenance after its entries (`:848`, `:855`). So the index keeps each
repository's reading, `index_readings`: generation, commit, root signature (`RootSignature.cs:11`), fed or read; written in the
transaction replacing the entries (`Store/SqliteKnowledgeStore.cs:152-156`), a feed's provenance with it (one connection,
`ServiceFactory.cs:312`), only where the generation is still the one its writer read before reading the source (as the signature is,
`KnowledgeService.cs:1116`), else not at all. Every replacing path passes one: refresh and ghosts (`KnowledgeIndex.cs:47`, `:67`),
feed (`KnowledgeService.cs:848`), retirement (`:550`), restamping (`:653`), a withdrawn registration (`:753`). Retirement, withdrawal
and restamping read no source, so each takes the generation inside its own transaction and always writes: a retirement that loses a
race to a refresh never leaves the retired repository served. A crash leaves both old or both new; entries without a reading read
inconclusive. An older connector already open checks the schema only at its open (`Store/SqliteKnowledgeStore.cs:102`, `:154-167`),
so the entries and readings move to versioned tables it cannot reach, and the index rebuilds once (`SqliteKnowledgeStore.cs:36`).

**The recheck reads one reading.** A new `RecheckAsync` compares the root's signature with the stored one, reading that root again
where it moved (`KnowledgeService.cs:1110`); then, in one transaction (`Platform/ConnectionGate.cs:129-135`), reads the entry, its
file's marks (`:381`) and the reading; then the cited file's own state against that commit, and the signature again. It takes no
in-process gate (one path already holds `_feedGate`, then `_refreshLock`: `:686`, `:745`, `:644`). `StandingsAsync` reads the stored
reading too.

- **Holds**: the same digest, no mark, the cited file unmodified at a commit read here, or a fed reading; another file's edit changes
  nothing (O).
- **Failed**: a `Superseded by D<n>` under the same condition, sent to the approver.
- **Amended**: no end; hits say `cited record amended by D<n>`.
- **Inconclusive**, retried, validity untouched: the cited file modified or its state unknown (a failed probe, no seam); the signature
  moved during the check; no reading, commit or feed; another digest, or an id gone, asking the owner's next session to cite again (the
  id after three tries); a scope refusal.

A fed reading answers about its fed commit. A hit whose probe failed says `+ uncommitted unknown`.

**Rejected.** Ingesting records. The in-process gates as the snapshot. X's `standingRevision` (the stored generation is that token),
`owner` and `sourceView` (the id and `fed` carry them). The commit alone. A moved id as Failed.

## 6. Requests to Lyntai, by quest

Published at acceptance; Lyntai decides, and nothing is written into Lyntai.

**A. A supersession that applies as one change** (2h). Needed: afterwards either the successor is current and every named predecessor
is ended by it, or nothing changed; each predecessor guarded by the revision its approver read; the call carries the caller's
operation key, whose receipt commits in the replacement's transaction and is kept on each predecessor, so a repeat with that key
answers that it applied, naming the successor, and changes nothing. Why: Daoris presents one repository's memory to others as current,
and insert-then-end shows both entries current between the calls, repaired only by a retry from a session that may have ended. Daoris
holds the successor on its own `awaiting` row and calls only after approval: the gate's store-first path (`MemoryChangeGate.cs:65-68`)
may stay for agents, while the requested call stores nothing before it applies. It asks D241 to reopen its rejection of a transaction over insert and end, or to meet
the need another way.

**B. MC7's view over another task key:** (1) no write path in its type; (2) a read moving nothing of the owner's (use stamp,
confirmation, co-activation, decay); (3) each result's source task, reference, opaque revision, relations and retirement standing,
and a cheap `RevisionOf(reference)`; (4) expansion asking the host per reference first; (5) references, never copies; (6) an engine
without the view saying so.

**C. An operation key on every write** (2j). Needed: a remember, relation, confirmation and end each accept a caller's key; its receipt
(reference, revision after, an outcome telling created from refreshed, since a remember can land on another writer's content,
`SqliteMemoryGraphStore.cs:104`) commits in the effect's transaction and is kept for a stated period; a repeat with the key answers
the receipt and changes nothing; a key reused with other content is refused; the receipt reads without effect. Why: after a lost response Daoris must know whether its operation ran, and the store cannot
say: a same-content write advances the engine and bumps the revision (`SqliteMemoryGraphStore.cs:83-89`, `:130`), a repeated relation
adds weight (`GraphMemoryEngine.cs:592-593`), a stamp names no confirmation (`MemoryGraphSql.cs:171-177`). Until then Daoris holds
each such outcome for the person.

**At presentation (X).** Cached material shown as current is first rechecked and stamped `checked at <revision>`; an invalidation
before delivery discards it; where no check answers, it shows as historical.

**Rejected.** Federation or merged ranking in the library; replicas; the library knowing repositories, quests or policy. Asking that
nothing be stored before approval (O): Daoris's row holds it.

## 7. The order

Smallest first, each proof seen failing first (O). **Can start now:** 2a (an amendment is a separate anchored entry, so the cited
digest holds) and 2b beside it. **Then:** 2c after 2b's stand-in; 2d and 2e on a Lyntai release carrying MC1 to MC6; 2f after 2a, 2c
and 2d; 2g on §6 B, 2h on §6 A, 2j on §6 C; 2i on the install. Cross-machine behavior waits on its transport (X). **Rejected:** X's
first row of identities and contracts.

## 8. The rows

Each row, then its proof:
- **KNOWCENTRAL2a: stored reading, citation, recheck** (service): `index_readings`, the generation check on every replacing path, the
  feed's provenance in its transaction, `RecheckAsync` and `StandingsAsync` over it. `Superseded by` Failed, inconclusive with the file
  modified, its probe failed or no seam; `Amended by` Amended; an edited body inconclusive, re-cited; the same body at a later commit,
  or another record file uncommitted, Holds; a removed id re-cited on the third try; a fed reading naming its commit; a second
  `KnowledgeService` over the file writing, retiring or restamping between entry and standing never mixing readings; a refresh begun
  before another's write refused; a restamp racing a refresh never writing old bodies back; a fault between a feed's entries and
  provenance, then a restart, leaving the old pair; no reading, or the root moved mid-check, inconclusive; a superseded entry refused
  at citation; `+ uncommitted unknown`; a retirement racing a refresh never leaving the retired repository served; a connector opened
  before the upgrade unable to write the new tables.
- **KNOWCENTRAL2b: tools, grants, workspace check** (connector, driver), over a stand-in repeating the write effects §6 C names and a
  confirmation's revision bump: the census over `fixtures/call-table.json`, its `_why` naming the four sources; `ConnectorTools` held
  to the registration; the driver's lists; `Defaults` naming the five; a child served reads alone; another workspace's reference
  refused at each door for Build, Chat, Help, bound and unnamed callers, and with no ambient workspace; a repository moved out refused
  at its next call; Intake, Ask Daoris and an unnamed caller refused write and end; no write from a Read tool.
- **KNOWCENTRAL2c: record, commit points, recovery, journal, read set** (service store, over 2b's stand-in): each move journalled once,
  in order, no text; `staged` and `awaiting` unchanged by a kill; a fault inside the advance's or `PostAsync`'s transaction leaving
  neither record nor move, a kill after it leaving `ready` rows the next start applies; a post naming no landing leaving rows `staged`;
  models from the usage history's turn lines (one turn, its model; turns A, B, A, a row of the third naming A; a chat stopped
  mid-turn and continued, `unknown`; a restart before the commit point keeping them; a pruned line, `unknown`); after a kill in
  `applying`, an end settled by its token, `stale` under another's, ended again at the revision read; a concurrent same-content write
  and a repeated relation each `held`, no second effect; an unrelated later confirmation `stale`, rechecked by the next sweep; a Holds during an awaiting end kept on the row;
  two ends of one entry, one `applied`, one `stale`; cursors re-validating below the floor or across stores; a bootstrap missing no row
  a second connection commits; a trimmed reference never told.
- **KNOWCENTRAL2d: engine, the driver's commit points, approver, sweep** (on a Lyntai release with MC1 to MC6): decay state, links,
  review rows and revision equal across search, get and expansion, root and child; a connector writing only `staged` rows; a tree's
  addition seen by its session and hand-over continuation alone until the post; a chat's released at `completed`, kept after a stop
  until the person decides; no tree path on a row; an end `awaiting` across a restart, decided through the row; a proposer or its child
  approving its own end refused; attribution surviving a continuation and `BindingStore.Forget`; a one-turn driven session's write
  naming its model; a run killed mid-turn `unknown`; the sweep rechecking `DueAsync`'s entries; no token on the page; `supersedes`
  refused, naming §6 A.
- **KNOWCENTRAL2e: a proposal as a quest** (after 2d): two proposals from one repository one quest; a decline reaching the proposer; a
  moved target stale; closing applying nothing; no proposer's door ending the entry; `Disputes` and a proposed end shown as two
  decisions; both languages.
- **KNOWCENTRAL2f: currency at presentation** (after 2a, 2c, 2d): an entry ended between a read and a hand-over's brief shown ended; an
  invalidation before delivery discarding the answer; a reader told once; an inconclusive recheck never stamped.
- **KNOWCENTRAL2g: MC7's views** (after §6 B): the owner's counters equal across a foreign read; a foreign expansion outside the scope
  refused before lookup; a foreign citation rechecked through the view; an engine without it saying so.
- **KNOWCENTRAL2h: supersession** (after §6 A): a replacement killed between its parts leaving all or nothing; no read seeing both
  current; a repeat by key `applied`, naming the successor, the key on each predecessor; a mismatch writing nothing.
- **KNOWCENTRAL2i: the installed measurement** (two repositories, one machine), measuring the journal's p95 and size beside
  FOUNDATION1k's; proposals a week; recheck latency and inconclusive rate; false-current answers across a restart and an interrupted
  delivery; `held` rows; models named and `unknown`; whether use should reinforce.
- **KNOWCENTRAL2j: recovery by key** (after §6 C): a lost response repeated by its key applied once; a held row settled by its
  receipt; a key with no receipt applied again.

## How each finding was resolved

| Finding | Resolution, where |
|---|---|
| 2 X recovery, O 3 states | Only `applying` settled (§3, 2c) |
| 2 X atomic replacement | `supersedes` refused until §6 A (§4, 2h) |
| 2 X read effects, recheck; O 5 flag | `ReinforceOn = None`; the cited file, a failed probe inconclusive (§2, §5) |
| 2 X attribution, O 4 tier | Role and revision at the call (§1) |
| 2 X no landing, O 8 tree | Commit points by session; Keep, Drop (§1) |
| 2 O 1, 2, 6, 7 | Workspace check; `awaiting`; four sources; read set (§2, §3) |
| 2 X outbox, `standingRevision` | Outbox rejected; the generation is the token (§3, §5) |
| 3 O 1, X gates; X interrupted feed | Reading stored with entries, generation-checked, read in one transaction (§5, 2a) |
| 3 O 2, X the model | Models from each row's turn at the commit point (§1, 2c) |
| 3 O 3, X lifecycle | Moved in the commit point's transaction; only the service calls the engine (§1, §3) |
| 3 X recovery, O 5 | Ends by token, else `stale`; the rest held; §6 C (§3, 2c) |
| 3 O 4, 6, 7, trim | Holds on the row; sweep calls `RecheckAsync`; `From`; stripped token; trimmed never told (§1, §3) |
| 3 X `Amends`, O, X request A | `Disputes`, not D220's replacement; A gains a key, loses the approval clause (§4, §6) |
| 2, 3 rows | 2a, 2b now; 2j added (§7, §8) |
