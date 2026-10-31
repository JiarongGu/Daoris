# KNOWCENTRAL1, round 2: the coordinator's half — the Opus maker's sealed proposal

Written from the brief alone, before reading the other maker's; code read at `484842df1`, where FOUNDATION1r is merged.

**Read.** `AGENTS.md`; `docs/decisions/D220.md`, `D212.md` (FOUNDATION1k, l/m, q, r; rows n and v), `D173.md` (FABRIC-C2a's
settled note, FABRIC-ACP1, the Codex reading), `D155.md`; `TASKS.md` (KNOWCENTRAL1, KNOWFRESH1, FABRIC-C2a);
`docs/2026-10-30-memory-currency-brief.md`, `-proposal-codex.md`, `docs/2026-10-30-knowledge-freshness-design.md`. Under
`src/Daoris.Service/`: `Daoris.Service.Core/Knowledge/` (`KnowledgeEntry.cs`, `EntryStanding.cs`, `DisclosurePolicy.cs`,
`KnowledgeService.cs:340-444`, `Ingest/DecisionNotes.cs`), `Daoris.Service.Core/Sessions/EventJournal.cs`,
`Daoris.Service.Core/Platform/JournalRows.cs`; `Daoris.Service.Mcp/` (`CallTable.cs`, `ToolClasses.cs`, `CallLineageFilter.cs`,
`RoleFilter.cs`, `KnowledgeSearchTools.cs:144-191`, `WorkspaceScope.cs`, `QuestTools.cs`, `GoAheadTools.cs`). And
`src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/WorkPolicy.cs`.

## 1. Where memory lives and who owns a fact

**Choice: one memory per repository.** One Lyntai engine per repository, its task key the repository's registry name, held by
the local service of the machine that drives it. The repository owns every entry, and only sessions whose record names it write
or end one. Each operation records, from the connector's placement (`SessionWorks.ReadAsync`, `RoleFilter.cs:44-61`) and never
from arguments: the session, its work, its role and revision (FOUNDATION1x's `binding_roles`), its quest, and the agent and model
tier that ran. Another machine's sessions propose through quests (§4), which already cross a remote.

**Why.** The repository is already the unit of ownership: a session changes its own and asks another through a quest. Interference
advances per engine, so a quiet repository's facts do not fade because a busy sibling works. A workspace-wide fact still has an
owning repository.

**Rejected.** *Per workspace*: no session owns a circle, so every member would write every fact and the busiest would bury the
rest. *Per role's task*: Build and Design in one repository would hold one fact twice (D220), and a fact outlives a role's
revision (FOUNDATION1w), so the role is attribution, not a key.

## 2. Grants on every operation

**Choice: five tools in the existing tables, one ownership rule, no new policy.** `ToolClasses.Named` classes `memory_search` and
`memory_get` as `Read`, and `memory_write` and `memory_end` as `Restricted`; `memory_propose` is already restricted by its suffix
(`ToolClasses.Of`). So `CallLineageFilter` serves a child the reads and refuses the rest, unchanged, and `quest_publish` stays the
one exception. `CallTable` gains the cells:

| Column | read | propose | write, end |
|---|---|---|---|
| Build, Design, Chat | yes | yes | yes, its own repository |
| Intake, Ask Daoris | yes | yes | refused: they change no repository |
| Help | yes | refused, as `Serving` refuses quests | refused |
| Review | yes (`ReviewerServed`) | refused | refused; it approves through its opinion (§4) |
| Partner | yes (`PartnerServed`) | refused | refused |
| Brainstorm turns | refused, the turn tool alone | refused | refused |
| Unread, unplaced, a child | yes | refused | refused |

**The ownership rule, on every reference, an expansion included.** A read's reference must name a repository inside the caller's
workspace scope: the check `knowledge_get` makes of an id (`KnowledgeSearchTools.cs:157-158`, ASKSCOPE1c), with `DisclosurePolicy`
where a feed holds it. A write's or end's target is the caller's record's repository; the tools take no repository argument, so
nothing a model writes widens them. An unnamed connector (a person's own agent, which `CallTable.Refusal` passes) reads and
proposes; the memory box refuses its write and end as a store's guard, since no record names the tier that would write.

**Why.** FOUNDATION1r made the table where a tool meets a work, failing closed; `WorkPolicies.Of` stays the one policy shape,
read for approval (§4). Ownership is the record's fact, not a setting.

**Rejected.** A `memory` work or a grants file (a second shape); a scope in arguments (D173 point 1: the harness places, never
the model); a child's propose beside `quest_publish` (unproven callers filling owners' queues).

## 3. The change log and cursors

**Choice: the event journal.** Every operation goes through the coordinator's record, `memory_ops` in the service's store (id,
repository, kind, reference, the revision read, attribution, approver, state `proposed → confirmed → applied | refused | stale |
dropped`), written before the engine is called and settled after it, as FOUNDATION1f's move record is. Its trigger journals
`memory` rows in `EventJournal.cs`'s shape: raw moves, ids and codes only, never text, trimmed by its own statement. A cursor is
`(store identity, position)`, as `JournalRows.ReadAsync` answers: after a restart a reader reads past it, and a changed identity
or a position below the floor means re-validating all it holds by revision.

Subscribers: the driver, telling a running session once that an entry it read was ended or superseded, as FOUNDATION1m tells
news (the coordinator keeps, bounded, who read which reference at which revision); EVENTHOOK1's plugins; the page, through the
change stream; MC7's reader caches (§6).

**Why.** The journal already is a change log: captured atomically with the write, positions never reused, bounded, its floor
telling a reader it missed rows. A trigger on Daoris's own table keeps Lyntai's storage private.

**Rejected.** A log of its own (a second home for what changed); triggers on Lyntai's tables; `DueAsync` as currency (it is the
idle sweep's repair, row 6).

## 4. A reader's proposal

**Choice: a quest.** `memory_propose` on another repository's entry publishes a quest to its owner, or joins the open one from the
same repository to the same owner, as `go_ahead_ask` joins an act already asked. It carries the reference, the opaque revision
read (MC1's guard), the relation proposed (`Supersedes`, `Amends`, `Disputes`, an end) and its evidence as citations (§5). The
owner's session decides with its own `memory_write` or `memory_end`, or declines with a reason that reaches the proposer. In its
own repository a session writes directly.

**When a change applies, and the approver (MC6).** A session's operations in a tree are staged with its work and applied at its
landing; an abandoned tree drops them with its claim. An addition applies with the landing. An end or a supersession applies
only when an approver other than the proposing session and its lineage confirms: the repository's second opinion (D155, another
maker, where `WorkPolicies.Of(…, Review, …)` names one) accepting the work whose memory changes it read; a deterministic check
(§5's `Superseded`); else the person, on a card held as `Daoris.Service.Http/PersonConfirmations.cs` holds others (always, for a
chat with no landing). Until then the predecessor's hits say `end proposed by <session>, awaiting <approver>`. MC1's revision guard makes a
confirmation of an entry moved since review `stale`.

**The person sees** the quest with the entry, the relation and each citation's standing; at a landing, its memory changes beside
its commits, with proposer, approver and tier; the cards waiting. In English and Chinese.

**Why.** Requests across repositories are quests: pulled, declinable with a reason, synced through a joined remote, seen. Joining
bounds the sessions proposals start.

**Rejected.** A word: it reaches a session, not an owner, and ends with it. A new kind of request: a second lifecycle beside the
quest's. A model of the proposer's maker as default approver: D109's 3.4 to 1 binds it until MC8 measures.

## 5. Records cited

**Choice: cite an index entry, never copy it.** A citation is `{id, lines?, commit, digest}`: `KnowledgeEntry.Id`
(repository-relative, so no machine path travels), the span a hit names, the commit `EntryStanding` carries, and a digest of the
entry's body. The session names id and lines from a hit; the coordinator fills the rest from the index (D217: host-issued identity).
Lyntai keeps it opaque under a scheme whose `Recheck` calls a new `KnowledgeService.RecheckAsync` over `StandingsAsync`:

- **Holds**: the same digest at any commit, and no mark: `ConfirmAsync`, which is not a use.
- **Superseded**: a committed `Superseded by D<n>` on the cited entry: **Failed**, the mark its approver.
- **Amended**: no end; hits say `cited record amended by D<n>` (ending amended entries whole cut recall from 66% to 14%).
- **Changed**: another digest: inconclusive, and the owner's next session is asked to cite again.
- **Inconclusive**: a mark read only `+ uncommitted`, `not yet read`, an id no longer indexed: retried, validity untouched.

**Why.** KNOWFRESH1a already says each hit's standing from the records' own marks, so no model judges currency.

**Rejected.** Records ingested as authoritative entries (a second home); the commit alone as revision (an unrelated edit moves
it); a moved id read as Failed.

## 6. MC7's contract

What Daoris asks of Lyntai's read-only view over another task key:

1. **No write path in its type**: no method that writes, ends, relates or confirms; not a flag.
2. **A read moves nothing of the owner's**: no use stamp, co-activation or decay counter, so a reader's interest never refreshes
   the owner's stale fact.
3. **Each result carries its reference, opaque revision and validity**, with a cheap `RevisionOf(reference)`.
4. **Expansion asks the host per reference**, through a callback, before following it.
5. **References, never copies**: the reader keeps references and revisions; its memory cites a foreign entry as it cites a record,
   its scheme's `Recheck` asking the view, never by a cross-task link.

**Rejected.** Federation or merged ranking in the library (the coordinator labels results by repository); replicas.

## 7. The order

Smallest first; each proof seen failing first.

1. **Recheck over the index** (service; needs nothing of Lyntai's). The citation and `RecheckAsync`, read by nothing. *Proof*:
   fixtures: `Superseded by` Failed, `Amended by` Amended, an edited body Changed, an unchanged body at a later commit Holds; an
   uncommitted mark, a pending reading and a removed id Inconclusive; a fed reading the same.
2. **The tools placed** (connector; a stand-in engine). *Proof*: `McpRoleFilterTests`' census over every column of
   `fixtures/call-table.json`; a child served reads and refused the rest in C2a's words; a reference outside the scope refused at
   `memory_get` and at an expansion; an argument naming another repository changes nothing; an unnamed connector refused write
   and end.
3. **The operation record and its journal** (service store). *Proof*: each state move journalled once, in order, with no text; a
   process killed at each phase settles by revision; a moved revision ends `stale`; a cursor below the floor is told to
   re-validate.
4. **Lyntai's engine per repository, staged by tree, applied at landing** (after the owner lands Lyntai's MC1 to MC6). *Proof*: a
   tree's write unseen by other sessions until its landing and shown `staged` to its own; an abandoned tree drops it; a proposer
   confirming its own end refused; another maker's reviewer applies it; with no approver it waits on the person's card, the
   predecessor's hits saying so.
5. **A reader's proposal as a quest.** *Proof*: two proposals from one repository open one quest; the owner's decline reaches the
   proposer with its reason; the proposer has no door that ends the owner's entry; the quest page in both languages.
6. **Currency at presentation.** *Proof*: an entry ended between a read and a hand-over's brief is shown ended; a running session
   that read it is told once; the idle sweep's pass confirms and never counts as a use.
7. **MC7's views** (after Lyntai builds §6). *Proof*: the owner's engine unchanged by a foreign read; a foreign expansion outside
   the scope refused; a foreign citation rechecks.

**Waits on the install:** the journal's p95 and size with memory rows (row 3, beside FOUNDATION1k's); proposals a week and the
sessions they start (row 5); FABRIC-C2a shipped before Codex sessions write (row 2); any end a model approves, until MC8's bar;
MC3's candidate coverage, Lyntai's.
