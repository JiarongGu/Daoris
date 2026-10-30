# The memory coordinator: Daoris's half of a shared, model-kept memory (KNOWCENTRAL1, round 2)

**Standing:** a synthesis, 2026-10-31, nothing built. The steward's, under D191, from the brief
(`2026-10-31-memory-coordinator-brief.md`) and the sealed proposals of the Opus maker (O, `…-proposal-opus.md`) and Codex
(X, `…-proposal-codex.md`), claims checked at `484842df1`. It grants authority and ends entries, so it waits for both makers' reads
(D191's note of 2026-10-24).
**Governing:** D220; Lyntai's D241 as amended, read through the brief; D212; D173; D191.

## 0. Where the two proposals agree

Both choose **one memory per repository**, the role and session as attribution, never the key, the workspace bounding disclosure, and
identity from the connector's placement, never an argument. Every operation passes **two checks**: the call table's tool against the
work, then the reference against the caller's scope. A child only reads. A correction is **a quest** to the owner; helpers, partners and
reviewers propose nothing. A record is **cited, never ingested**, and no model judges currency; an amended record never ends an entry
whole (that cut an amendment's recall from 66% to 14%, `2026-10-30-memory-currency-brief.md`). **MC7 moves nothing of the owner's**,
Daoris checking grants before lookup and expansion. `DueAsync` proves no currency. No model approves an end before MC8's verdict.

### 0.1 Claims checked

| Claim | At `484842df1` | Holds |
|---|---|---|
| X: `RoleFilter.cs`, not `CallTable`; FABRIC-C2a open | `Daoris.Service.Mcp/CallTable.cs` and `CallLineageFilter.cs` both enforce; C2a archived 2026-10-31 | no |
| O: the event journal is the change log | D212 point 11: it "never travels"; trigger-minted in the writing statement (`Sessions/EventJournal.cs`) | locally only |
| O: role, revision and model tier read from the placement | `binding_roles` is the desktop store's (`Participants/BindingStore.cs`); the service's `Session` names the adapter, "never a model" (`Sessions/Sessions.cs:63`) | no: joined where shown |
| X: an `owner` field beside the entry id | `KnowledgeEntry.Id` already names the repository | no |
| X: grants as ceilings on `WorkPolicy` | the driver's record (`Participants/Domain/WorkPolicy.cs`); the connector judges by `CallTable` (`RoleFilter.cs:89`) and never reads it | a second place |

## 1. Where memory lives and who owns a fact

**Choice (O).** One Lyntai engine per repository, keyed by its registry name as an entry id qualifies it, held by the local service of
the machine that drives it. The repository owns every entry. An operation records the connector's record: session, work, repository,
tree, quest and adapter. Role, revision and model tier are joined from the desktop's binding where the operation is shown, so the tier
that ran is stated. Intake (`ask #a`) and Ask Daoris (`daoris:help`) name no repository (`SessionRecord.cs:12`), so they have none to
write. A tree's operations are **staged with its work and applied at its landing**. An abandoned tree drops them.

**Why.** A session changes its own repository and asks another by quest. Interference advances per engine, so a quiet repository's facts
do not fade beside a busy one. Staging rides the landing that carries the work.

**Rejected.** A workspace memory, which no session owns, and a role's task, which holds one fact twice (both). X's private task memory, a
second key for what staging holds. X's audited reassignment: a change of owner is an end here and a write there, two changes and a
request.

## 2. Grants on every operation

**Choice (O, with X's coverage).** `ToolClasses.Named` classes `memory_search` and `memory_get` as reads, and `memory_write` and
`memory_end` as restricted. `memory_propose` is restricted by its suffix (`ToolClasses.cs:77`), so C2a refuses it to a child unchanged.
`CallTable` gains these cells:

| Work | Read | Propose | Write, end |
|---|---|---|---|
| Build, Design, Chat | yes | yes | its own repository |
| Intake, Ask Daoris | yes | yes | refused |
| Help | yes | refused, beside `Serving`'s quests | refused |
| Review, Partner | added to `ReviewerServed`, `PartnerServed` | refused | refused |
| A brainstorm turn | refused | refused | refused |
| Unread, unplaced, a child | yes | refused | refused |
| Unnamed (a person's own agent) | yes | yes | refused by the memory box |

Every door that takes or yields a reference then checks it against the caller's workspace on each call, as `knowledge_get` does
(`KnowledgeSearchTools.cs:157-158`): search, get, expansion, history, citation resolution and subscription (X). A write or end targets the
record's repository; no tool takes a repository argument.

**Why.** FOUNDATION1r made `CallTable` the one place a tool meets a work, failing closed. Ownership is the record's fact.

**Rejected.** X's ceilings on `WorkPolicy`, which the connector never reads: a second place for one judgement. X's owner assignment. A
child's proposal (O). A scope from arguments (both).

## 3. The change log and cursors

**Choice (O's record, X's limits).** The change log of record is `memory_ops`, in the service's store. Each row holds the id, repository,
kind, reference, revision read, attribution, approver and a state: `proposed → confirmed → applied | refused | stale | dropped`.

- **Write-ahead.** Written before the engine is called and settled after, as FOUNDATION1f's move record is; after a crash, MC1's
  revision settles it.
- **Journal.** A trigger journals `memory` rows into `event_journal` (ids and codes, never text), waking local readers.
- **Cursor.** `(store, position)` (`Platform/JournalRows.cs:16`); a new store, or a floor past it, re-validates all held.
- **X's reader rules.** An unsettled row counts as a change, so nothing moves without a record ahead of it. A bootstrap reads rows and
  newest position in one gated read. A change of scope invalidates.

The journal never travels (D212 point 11). The `memory_ops` rows travel later, as a permitted record through the workspace's exchange,
once a second machine reads memory. Subscribers: the driver, telling a running session once that an entry it read ended (FOUNDATION1m);
EVENTHOOK1's plugins; the page; MC7's caches.

**Rejected.** X's outbox in Lyntai's adapter: it puts Lyntai's storage in Daoris's transaction for a guarantee the write-ahead row
gives. X's second stream identity. Triggers on Lyntai's tables. `DueAsync` as currency (both).

## 4. A reader's proposal

**Choice (both).** A quest. `memory_propose` on another repository's entry publishes one to its owner, or joins the open one from the same
repository, as `go_ahead_ask` joins an asked act (`GoAheadTools.cs:16`; O). Its body is a versioned packet (X): target reference,
revision read, relation (`Supersedes`, `Amends`, `Disputes`, an end), citations, proposer. The owner writes, ends or declines with a
reason. Closing the quest applies nothing; its handback names the outcome (X). A moved target is stale.

**The approver (MC6).**
- An addition applies at its landing.
- An end or a supersession needs an approver who is neither the proposer nor its lineage (O); a supersession's successor and
  retirement apply as one change (X).
- Until MC8's verdict, the approver is the person: one card per landing listing its ends, held as `PersonConfirmations.cs` holds cards.
- After it, another maker's second opinion where `WorkPolicies.Of(…, Review, …)` names one (D155); the person keeps a chat's ends and
  any the reviewer declines.
- A `Superseded by` mark is evidence on the card, never the approver (X). Meanwhile the predecessor's hits say
  `end proposed by <session>, awaiting <approver>`.

The person sees the quest with each citation's standing, and at a landing its memory changes beside its commits, with proposer,
approver, adapter and tier, in English and Chinese.

**Why.** Cross-repository requests are quests: pulled, declinable, synced, seen. MC8 measures whether a model may end a fact.

**Rejected.** A word, and a new kind of request (both). O's mark as approver: a mark retires a record, not every claim citing it, and
D241 leaves that to the writer or confirmer. X's appointed staff before MC8, a model ending facts unmeasured. A same-maker approver
(Lyntai's D109).

## 5. Records cited

**Choice (O's shape, X's reading).** A citation is `{id, lines?, commit, fed, digest}`:
- `id` is `KnowledgeEntry.Id`: repository-qualified, with no machine path;
- `lines` is the span a hit names;
- `commit` and `fed` come from `EntryStanding`;
- `digest` is a digest of the entry's body.

The session names id and lines; the coordinator fills the rest. Lyntai's `Recheck` calls a new `KnowledgeService.RecheckAsync` over
`FindAsync` and `StandingsAsync` (`KnowledgeService.cs:352`):

- **Holds**: same digest, no mark. A `ConfirmAsync`, never a use.
- **Failed**: a committed `Superseded by D<n>`, sent to the approver (§4).
- **Amended**: no end; hits say `cited record amended by D<n>`.
- **Inconclusive**, retried, validity untouched: another digest (the owner's next session asked to cite again), `+ uncommitted` or
  `not yet read`, an id no longer indexed, a scope refusal.

A fed reading answers about its fed commit and says so.

**Why.** KNOWFRESH1a already reads standing from the records' own marks.

**Rejected.** Ingesting records (both). X's `standingRevision`: standing is read live; stored, it is a second home of the mark. X's
`owner` and `sourceView`, which the id and `fed` carry. The commit alone, which unrelated edits move (both). A moved id as Failed.

## 6. MC7's contract: a request to Lyntai, by quest

Daoris asks by a quest published at acceptance; Lyntai decides, and nothing is written into Lyntai. The view over another task key needs:

1. **No write path in its type**: no method that writes, ends, relates or confirms; not a flag.
2. **A read that moves nothing of the owner's**: no use stamp, confirmation, co-activation or decay counter, so a reader's interest
   never keeps a stale fact alive.
3. **Each result's source task, reference, opaque revision, relations and retirement standing**, with a cheap `RevisionOf(reference)`.
4. **Expansion that asks the host per reference**, by callback, before following it; a historical expansion says so (X).
5. **References, never copies**: a foreign entry is cited as a record is, its `Recheck` asking the view.
6. **An engine without the view says so** (X).

**At presentation (X, with O's proofs).** Cached material shown as current is first rechecked and stamped `checked at <revision>`; an
invalidation arriving before delivery discards it; where no check answers, it shows as historical.

**Rejected.** Federation or merged ranking in the library. Replicas. The library knowing repositories, quests or policy (both).

## 7. The order

**Choice (O).** Smallest first, each proof seen failing first: the citation over the index, which needs nothing of Lyntai's; then grants,
the record, and the engine once a release carries MC1 to MC6. The MC7 request goes out at acceptance, not as a row, and the installed
measurement closes the order (both X).

**Rejected.** X's first row of identities and contracts: types nothing proves until a door uses them.

## 8. Every difference

| Difference | Chosen | Rejected |
|---|---|---|
| Unfinished observations | staged by tree, applied at landing (O) | a private task (X) |
| A fact changing owner | an end and a write, by quest | an audited reassignment (X) |
| Where grants live | `ToolClasses` and `CallTable` (O) | ceilings on `WorkPolicy` (X) |
| Who writes for the owner | its Build, Design and Chat sessions (O) | an owner assignment (X) |
| Doors that check a reference | every one, each call (X) | get and expansion (O) |
| FABRIC-C2a | built and enforcing (O) | still open (X) |
| The change log | `memory_ops`, journalled locally (O) | a workspace log of its own (X) |
| Capture with the change | written ahead, settled by revision (O) | an outbox in Lyntai's adapter (X) |
| Cursor and bootstrap | `(store, position)` (O), X's cut and invalidation | a second stream identity (X) |
| Another machine's reader | the record travels later, never the journal (X) | the journal as feed (O) |
| The proposal's body | O's join, X's packet; closing applies nothing | none |
| The approver before MC8 | the person, a card per landing (O) | appointed staff (X) |
| A `Superseded by` mark | evidence (X) | the approver (O) |
| Citation fields | O's, with `fed` | `owner`, `sourceView`, `standingRevision` (X) |
| A fed reading | answers about its commit (X) | inconclusive (O) |
| Attribution | the record's fields; tier joined | read at the connector (O) |
| MC7 and presentation | O's five points and proofs; X's two points, stamp and discard | none |
| The first row | the citation (O) | identities and contracts (X) |

## 9. The rows, smallest first

- **KNOWCENTRAL2a: a citation and its recheck over the index** (service; nothing of Lyntai's). **Proof:** fixtures: `Superseded by`
  Failed; `Amended by` Amended, never ended; an edited body inconclusive, a new citation asked; the same body at a later commit Holds;
  `+ uncommitted`, `not yet read`, a removed id and a foreign id inconclusive; a fed reading naming its commit.
- **KNOWCENTRAL2b: the tools in the call table, over a stand-in engine** (connector). **Proof:** `McpRoleFilterTests`' census over every
  column of `fixtures/call-table.json`; a child served reads, refused the rest in C2a's words; a foreign reference refused at each door;
  an argument naming another repository changing nothing; Intake, Ask Daoris and an unnamed caller refused write and end; a repository
  moved out of the workspace refused at the next call.
- **KNOWCENTRAL2c: the operation record and its journal** (service store). **Proof:** each move journalled once, in order, no text; a
  kill at each phase settled by revision, a moved revision `stale`; an unsettled row invalidating; a cursor below the floor or across a
  new identity re-validating; a bootstrap's cut missing no concurrent row.
- **KNOWCENTRAL2d: the engine per repository, staged by tree, and the approver** (after the owner lands Lyntai's MC1 to MC6 and a
  release carries them). **Proof:** a tree's write unseen by others until landing, `staged` to its own; an abandoned tree's dropped; a
  proposer or its child confirming its own end refused; a supersession applied as one change; a Failed recheck waiting on the card,
  which names adapter and tier.
- **KNOWCENTRAL2e: a reader's proposal as a quest.** **Proof:** two proposals from one repository one quest; a decline reaching the
  proposer with its reason; a moved target stale; closing applying nothing, the handback naming the outcome; no proposer's door ending
  the entry; the page in both languages.
- **KNOWCENTRAL2f: currency at presentation.** **Proof:** an entry ended between a read and a hand-over's brief shown ended; an
  invalidation before delivery discarding the answer; a session that read it told once; no unchecked answer stamped; a `DueAsync` pass
  never a use.
- **KNOWCENTRAL2g: MC7's views** (after Lyntai answers §6). **Proof:** the owner's counters equal before and after a foreign read; a
  foreign expansion outside the scope refused before lookup; a foreign citation rechecked through the view; an engine without it saying so.
- **KNOWCENTRAL2h: the installed measurement** (two real repositories). **Measures:** the journal's p95 and size with memory rows, beside
  FOUNDATION1k's; proposals a week and the sessions they start; recheck latency; false-current answers across a restart and an
  interrupted delivery; the tier that ran.

**Waits on measurement.** The second opinion approves only once MC8's verdict meets its bar; MC3's candidate coverage is Lyntai's.
