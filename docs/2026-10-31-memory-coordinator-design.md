# The memory coordinator: Daoris's half of a shared, model-kept memory (KNOWCENTRAL1, round 2)

**Standing:** a synthesis, 2026-10-31, revised after both makers' reads (`…-reads.md`); nothing built. The steward's, under D191,
from the brief and the sealed proposals of the Opus maker (O) and Codex (X); claims checked at `cd205046b`, Lyntai's at `5cfdc76a`. It
grants authority and ends entries, so it is accepted only on both makers' reads (D191's note of 2026-10-24).
**Governing:** D220; Lyntai's D241 as amended; D205; D212; D173; D191.

## 0. Where the two proposals agree

One memory per repository; role and session as attribution; identity from the connector's placement. A child only reads; a
correction is a quest. A record is cited, never ingested; an amended record never ends an entry whole (that cut an amendment's recall
from 66% to 14%). `DueAsync` proves no currency. No model approves an end before MC8's verdict.

## 1. Where memory lives and who owns a fact

**Choice (O).** One Lyntai engine per repository, keyed by its registry name, held by the local service of the machine that drives it,
its only writer. The repository owns every entry. Intake and Ask Daoris name no repository (`Service/Domain/SessionRecord.cs:12`), so
they write none.

**Attribution is written at the call**, from the caller's record, never from arguments: session, work, repository, workspace, quest,
adapter and harness version (`Sessions/Sessions.cs:101-117`); the role and its revision, which the driver names on the connector's
environment at each start as it names the session (`IntakeScope.cs:28`), so a continuation writes its own; and the newest model the
record's `ran` reports (D205, `ActorRan.cs:3-5`). What is not read is written `unknown`; nothing is joined later from `binding_roles`,
which `KeepRole` overwrites and `Forget` deletes (`BindingStore.cs:134-146`, `:160-172`). D205 keeps `ran` off the wire, so a row that
travels says `not shared` for the model until the exchange's permitted record admits it.

**An operation applies at its session's commit point.** Rows are staged by session id, never by tree, a machine-local path
(`Sessions.cs:88-98`):
- a session with a tree commits at its landing; a tree discarded unlanded drops its rows;
- a session with no landing, a chat or any session in the main tree (`Session.Tree` null, `Sessions.cs:116`), commits when its record
  ends `completed` (`Loop/Chat/ChatRunner.cs:2936`);
- one ended otherwise (stopped, failed, interrupted, declined) keeps its rows `staged`: a continuation of the record carries them, and
  the record's page offers the person *Keep* or *Drop*. Nothing staged applies by itself.

Only its own session sees a staged row.

**Why.** A session changes its own repository and asks another by quest; interference advances per engine; a commit point is where
the work an observation came from is accepted.

**Rejected.** A workspace memory, which no session owns; a role's task, holding one fact twice. X's private task memory: staging by
session holds the same. X's audited reassignment: an end and a write, two changes and a request. Attribution joined where shown.

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

**A Read is observational**, the owner's and a child's alike. `Read` changes nothing (`ToolClasses.cs:6-7`), but a Lyntai recall or
expansion reinforces, co-activates and logs what it returns. So every engine the coordinator opens sets `ReinforceOn = None` (Lyntai's
`GraphMemoryOptions.cs:285`), whose gate skips touches, links and review rows (`GraphMemoryEngine.cs:1942`); recall and expansion write
nothing else (`:1022`, `:1281`) and age nothing (`:1908`). A confirmation is the coordinator's recorded write; whether use should
reinforce is measured (2i). The read set (§3) is the coordinator's bookkeeping, like the lineage line
every call writes (`CallLineageFilter.cs:35`).

**The workspace check is new.** `WorkspaceScope.Outside` refuses only a bound session (`WorkspaceScope.cs:62-66`), and an unbound one
may name another workspace or `all` (`:31-41`). So every door that takes or yields a reference (search, get, expansion, history,
citation resolution, subscription) checks its repository against the registry's members of the caller record's `Workspace`
(`Sessions.cs:112`) at each call, bound or not; an unnamed caller's is the ambient one (`AmbientWorkspace.cs:51`). Where none resolves,
the call is refused, never spanning every workspace. No memory tool takes a workspace or repository argument.

**Four permission sources move with the table** (O): `OpinionPermissions.Offered` and `PartnerPermissions.Offered` gain the two reads,
as `ReviewerServed` and `PartnerServed` do (`CallTable.cs:42-57`); `BrainstormPermissions.ConnectorTools` gains all five, denied to a
pass, held to the registration by `McpToolRegistrationTests` (`:53-57`); `PermissionRules.Defaults` allows the five unprompted
(`Agents/Permissions.cs:111`), since a write stages, an end waits on its approver and a proposal is a quest.

**Why.** FOUNDATION1r made `CallTable` the one place a tool meets a work, failing closed.

**Rejected.** X's ceilings on `WorkPolicy`, which the connector never reads. X's owner assignment. A child's proposal. A scope from
arguments. `knowledge_get`'s check, which binds only Ask Daoris.

## 3. The change log, recovery and cursors

**Choice (O's record, X's limits).** The change log of record is `memory_ops`, in the service's store: id, session, repository, kind
(`write`, `relate`, `end`, `confirm`, `propose`), target, revision read, attribution, approver, and a state:
`staged → awaiting → applying → applied`, or `dropped`, `declined`, `stale`, `held`.

- **`staged`** waits on its session's commit point. An addition or relation then goes to `applying`, an end to **`awaiting`** its
  approver (§4): a durable state shown on the landing or the record and decided through the row, as a go-ahead is kept on the ask store
  (`SessionGoAheads.cs:7-11`); never a `PersonConfirmations` card, held in memory for two minutes, five at once
  (`PersonConfirmations.cs:124-127`, `:180`).
- **`applying` is the effect mark**, written in its own transaction before the engine is called; the outcome is written after with the
  state. A guard mismatch ends `stale`.
- **Recovery names the operation.** After a crash only `applying` rows are settled, each read observationally for its receipt:
  - an addition or relation is applied again: a write is keyed by its content (Lyntai's `IMemoryGraphStore.cs:428`), so a repeat
    returns the same entry, and a repeated relation changes nothing (`MemoryRelations.cs:28-29`);
  - each end's reason carries its row's id, so a target ended under it is `applied`; a confirmation's stamp at or past its row's
    instant settles it;
  - a target still at the revision read is applied again under the same guard;
  - anything else, moved by what no receipt names, is **`held`**: never applied again by itself, settled by the person from the
    entry's history, as `MoveStore.MarkEffect` holds an effect with no outcome (`Course/MoveStore.cs:24-25`, `:472-488`).
- **Journal.** A trigger journals `memory` rows into `event_journal` (ids and codes, never text), waking local readers; it never
  travels (D212 point 11). **Cursor:** `(store, position)` (`Platform/JournalRows.cs:15`); a new store, or a floor past it,
  re-validates all held. **X's reader rules:** an unsettled row counts as a change; a bootstrap reads rows and newest position in one
  read transaction; a change of scope invalidates.
- **The read set** (O): who read which reference at which revision; local, never journalled, bounded (the newest 256 per session,
  65,536 in all), so a running session is told once that an entry it read moved.

The rows travel later, as a permitted record, once a second machine reads memory. Subscribers: the driver (FOUNDATION1m), EVENTHOOK1's
plugins, the page, MC7's caches.

**Rejected.** X's transactional outbox: Lyntai's stores open their own connection and transaction per call
(`SqliteMemoryGraphStore.cs:80-81`), so its write cannot join Daoris's; receipts establish recovery, and an outcome none names is held,
never assumed. Settling by revision alone: it sees that something moved, not what. A second stream identity. Triggers on Lyntai's
tables.

## 4. A reader's proposal

**Choice (both).** A quest. `memory_propose` on another repository's entry publishes one to its owner, or joins the open one from the
same repository, as a go-ahead joins an act already asked (`SessionGoAheads.cs:10-11`). Its body is a versioned packet (X): target,
revision read, relation (`Amends`, `Disputes`, an end; a supersession once §6 A is met), citations, proposer. The owner's session
decides through its own operations or declines with a reason. Closing the quest applies nothing; its handback names the outcome.

**The approver (MC6).**
- An addition, a relation and a confirmation apply at their commit point; a relation ends nothing (D241).
- An end needs an approver who is neither its proposer nor its lineage. Until MC8's verdict that is the person, deciding on the row
  where the landing or the record lists its ends with evidence, proposer, adapter and model; the row keeps the revision shown.
- After MC8, another maker's second opinion where `WorkPolicies.Of(…, Review, …)` names one (D155); the person keeps a chat's ends and
  any the reviewer declines.
- A `Superseded by` mark is evidence, never the approver (X). Meanwhile the predecessor's hits say `end proposed by <session>,
  awaiting <approver>`.
- **Supersession waits on Lyntai.** Lyntai's gate stores the successor live, then ends each predecessor in another call
  (`MemoryChangeGate.cs:65-68`, `:110-111`), and D241 rejected a transaction over the two: between them both are current. So
  `memory_write` refuses `supersedes`, naming §6 A, until 2h. Meanwhile an owner writes the new fact with `Amends` naming the old and
  proposes the old one's end: two decisions, each shown as such.

The person sees each citation's standing on the quest, and a landing's memory changes beside its commits.

**Rejected.** A word, and a new kind of request. A mark as approver: it retires a record, not every claim citing it. Appointed staff
before MC8. A same-maker approver (Lyntai's D109). Hiding the successor until the end lands: a second home for currency.

## 5. Records cited

**Choice (O's shape, X's reading).** A citation is `{id, lines?, commit, fed, digest}`: `id` is `KnowledgeEntry.Id`,
repository-qualified, with no machine path; `lines` the span a hit names; `commit` and `fed` from `EntryStanding`; `digest` of the
entry's body. The session names id and lines; the coordinator fills the rest. Citing an entry already superseded is refused, naming its
successor.

**The recheck reads one coherent reading.** `FindAsync` and `StandingsAsync` (`KnowledgeService.cs:320`, `:352`) taken apart can
straddle a refresh; the uncommitted flag covers a whole repository's record folders (`:163-165`), and a failed probe reads clean
(`:1130-1137`, `:408`). So a new `RecheckAsync`, which Lyntai's `Recheck` calls, brings the repository's reading current as a search
does (`RootSignature.cs:11`), then, holding the index's writer gates for a bounded wait (`:203-206`), reads the entry, its file's marks,
the reading's commit or feed, and the cited file's own state against that commit (one status per repository and pass).

- **Holds**: the same digest, no mark, the cited file unmodified at a commit read here, or a fed reading. A `ConfirmAsync`, never a
  use. Another file's edit changes nothing (O).
- **Failed**: a `Superseded by D<n>` under the same condition, sent to the approver.
- **Amended**: no end; hits say `cited record amended by D<n>`.
- **Inconclusive**, retried, validity untouched: the cited file modified or its state unknown (a failed probe, no seam, a gate not
  taken in time); no commit and no feed; another digest, or an id gone, which asks the owner's next session to cite again (the id after
  three tries); a scope refusal.

A fed reading answers about its fed commit. A hit whose probe failed says `+ uncommitted unknown`.

**Rejected.** Ingesting records. X's `standingRevision`: the coherent reading is the alternative X named, and the one mark a token would
add, a supersession at citation, is refused there. X's `owner` and `sourceView`, which the id and `fed` carry. The commit alone. A
moved id as Failed.

## 6. Requests to Lyntai, by quest

Published at acceptance; Lyntai decides, and nothing is written into Lyntai.

**A. A supersession that applies as one change** (2h waits on it). Needed: afterwards either the successor is current and every named
predecessor is ended by it, or nothing changed; each predecessor guarded by the revision its approver read; nothing stored before the
approval; a repeat after a lost response answering that it applied, naming the successor. Why: Daoris presents one repository's memory
to other repositories as current, and the insert-then-end order shows both entries current between the calls, repaired only by a
retry from a session that may have ended. It asks D241 to reopen its rejection of a transaction over insert and end, or to meet the
need another way.

**B. MC7's view over another task key:** (1) no write path in its type, not a flag; (2) a read that moves nothing of the owner's (use
stamp, confirmation, co-activation, decay), whatever the engine's options; (3) each result's source task, reference, opaque revision,
relations and retirement standing, with a cheap `RevisionOf(reference)`; (4) expansion asking the host per reference before following
it, a historical one saying so; (5) references, never copies, a foreign entry cited as a record is; (6) an engine without the view
saying so.

**At presentation (X).** Cached material shown as current is first rechecked and stamped `checked at <revision>`; an invalidation
before delivery discards it; where no check answers, it shows as historical.

**Rejected.** Federation or merged ranking in the library. Replicas. The library knowing repositories, quests or policy.

## 7. The order

Smallest first, each proof seen failing first (O): the citation, which can start now (an amendment is a separate anchored entry, so
the cited digest holds); the tools; the record and its recovery; the engine once a Lyntai release carries MC1 to MC6; presentation
only after recheck, recovery and observational reads hold. One machine closes the order; cross-machine behavior waits on its transport
(X). **Rejected:** X's first row of identities and contracts, which nothing proves until a door uses them.

## 8. The rows

- **KNOWCENTRAL2a: the citation and its recheck** (service; nothing of Lyntai's). **Proof:** a committed `Superseded by`
  Failed; the same mark with the cited file modified, its probe failed or no seam, inconclusive; `Amended by` Amended; an edited body
  inconclusive, a new citation asked; the same body at a later commit, another record file uncommitted, Holds; a removed id re-cited on
  the third try; no commit and no feed inconclusive; a fed reading naming its commit; a refresh released by a test gate between entry
  and standing never mixing two readings; the gates held past their bound inconclusive; a superseded entry refused at citation; a
  failed probe's hit saying `+ uncommitted unknown`.
- **KNOWCENTRAL2b: the tools, grants and workspace check, over a stand-in engine** (connector and driver). **Proof:**
  `McpRoleFilterTests`' census over `fixtures/call-table.json`, its `_why` naming the four sources; `McpToolRegistrationTests` holding
  `ConnectorTools`; the driver's lists matching `ReviewerServed` and `PartnerServed`; `Defaults` naming the five; a child served reads
  alone, in C2a's words; another workspace's reference refused at each door for Build, Chat, Help, a bound and an unnamed caller; an
  unnamed caller with no ambient workspace refused; a repository moved out of the workspace refused at the next call; Intake, Ask Daoris
  and an unnamed caller refused write and end; the stand-in counting no write from a Read tool, root or child.
- **KNOWCENTRAL2c: the record, recovery, journal and read set** (service store, over 2b's stand-in). **Proof:** each state move
  journalled once, in order, no text; `staged` and `awaiting` unchanged by a kill; a kill before the engine call, and after its commit
  but before the outcome, settled by each kind's receipt; an entry moved by a writer no receipt names `held`; two ends of one entry, one
  `applied`, one `stale`; a cursor below the floor or across a new store re-validating; a bootstrap missing no row a writer on a second
  connection commits during it; the read set trimmed at its bounds.
- **KNOWCENTRAL2d: the engine, staging, approver and attribution** (after a Lyntai release carries MC1 to MC6). **Proof:** decay state,
  links, review rows and revision equal across a search, a get and an expansion, by root and child; a tree's addition seen `staged` by
  its own session alone until landing, dropped at discard; a chat's applied at `completed`, kept after a stop or failure until the
  person decides, carried by a continuation; no row holding a tree path; an end `awaiting` across a restart, decided by the person
  through the row; a proposer or its child approving its own end refused; attribution surviving a continuation (its new role revision
  on later rows) and `BindingStore.Forget`, an unreported model `unknown`; `supersedes` refused, naming §6 A.
- **KNOWCENTRAL2e: a reader's proposal as a quest.** **Proof:** two proposals from one repository one quest; a decline reaching the
  proposer; a moved target stale; closing applying nothing; no proposer's door ending the entry; both languages.
- **KNOWCENTRAL2f: currency at presentation** (after 2a, 2c, 2d). **Proof:** an entry ended between a read and a hand-over's brief
  shown ended; an invalidation before delivery discarding the answer; a reader told once; an inconclusive recheck never stamped.
- **KNOWCENTRAL2g: MC7's views** (after §6 B). **Proof:** the owner's counters equal across a foreign read; a foreign expansion outside
  the scope refused before lookup; a foreign citation rechecked through the view; an engine without it saying so.
- **KNOWCENTRAL2h: supersession** (after §6 A). **Proof:** a replacement killed between its parts leaving all or nothing; no read seeing
  both current; a repeat after a lost response `applied`, naming the successor; a mismatch writing nothing.
- **KNOWCENTRAL2i: the installed measurement** (two repositories on one machine). **Measures:** the journal's p95 and size beside
  FOUNDATION1k's; proposals a week; recheck latency and inconclusive rate; false-current answers across a restart and an interrupted
  delivery; `held` rows; the model that ran; whether use should reinforce.

**Waits on measurement.** The second opinion approves only once MC8's verdict meets its bar; MC3's candidate coverage is Lyntai's.

## Round 2: how each finding was resolved

| Finding | Answered in |
|---|---|
| Recovery (X); states against crash settlement (O 3) | §3; 2c |
| Atomic replacement (X) | §4, §6 A; 2h |
| Reads' internal effects (X) | §2; 2b, 2d |
| Recheck evidence (X); the repository-wide flag (O 5) | §5, narrowed to the cited file; 2a |
| Historical attribution (X); the tier (O 4) | §1, at the call (X's operation time); 2d |
| Work without a landing (X); no tree, the tree path (O 8) | §1; 2d |
| No workspace check to copy (O 1) | §2; 2b |
| The person's card (O 2) | §3 `awaiting`; 2d |
| Four permission sources (O 6) | §2; 2b |
| The read record (O 7) | §3; 2c, 2f |
| Outbox rejection (X) | §3, rejected with a new reason |
| `standingRevision` rejection (X) | §5: rejected; X's coherent reading taken |
| Rows, 2f, 2h (X); 2a now, bootstrap (O) | §7, §8 |
