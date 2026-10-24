# The foundation: the participant, the course and the move, the event (FOUNDATION1, proposed, round 1, 2026-10-25)

**Standing:** proposed with D212, round 1 under D191: the synthesis of the three sealed proposals,
`docs/2026-10-25-foundation-proposal-claude.md` (Claude's), `docs/2026-10-25-foundation-proposal-codex.md` (Codex's) and
`docs/2026-10-25-foundation-proposal-opus.md` (the independent Opus reader's), each written from the brief
`docs/2026-10-25-foundation-brief.md` and the survey `docs/2026-10-25-foundation-survey.md` alone. Each difference is settled in
§1 by choosing one answer, or composing two, and saying what the others lose. It changes a foundation in every sense D191's
amendment of 2026-10-20 names, and some of its rows grant or refuse authority and delete records, so by D191's note of 2026-10-24
it is not accepted on one maker's read: Codex reads it, the independent Opus reader reads it as a foundation, and the steward
checks every finding against the code before it is taken. Nothing here is built, spends an account, signs in, publishes or
deletes.

Read for it: the brief, the survey and the three proposals whole; D137, D155, D163, D167, D173, D182, D188, D191, D197, D203,
D205, D206, D208, D209 and D210 at the points this cites; D119 and D120 for the plugins, whose points' frames are D101's; the in-flight designs
EVENTHOOK1 (`docs/2026-10-24-event-hook-design.md`), COWORKVIEW1 (`docs/2026-10-24-cowork-view-design.md`), SWITCHCTX1
(`docs/2026-10-24-switch-context-design.md`) and AGENTERRORS1 (D210 and its notes); the precedent of this shape,
`docs/2026-10-23-actors-design.md` with D205; `.claude/knowledge/claims-need-checks.md`, `compatible-builds.md`,
`session-words.md`, `session-record-fields.md`, `pushed-slices.md`, `person-waits.md`, `twins.md` and `translation-parity.md`; the
glossary. Every claim about the code was checked on main at `92ac2bd7e`, and LIMITSWITCH1's build on its session branch at
`3fdc6bbeb`, read and not merged; §0.2 lists where the brief, the survey or a proposal was wrong. `Dnn` is
`docs/decisions/Dnn.md`; paths use the survey's aliases (`Driver/`, `Modules/`, `Host/`, `Core/`, `Mcp/`, `Web/`), with `Chat/`
for `Driver/Conversations/Chat/` and `Loop/` for `Driver/Loop/` as the survey shortens them. *C* is Claude's proposal, *X* Codex's,
*O* the Opus reader's.

## 0. What this rests on

### 0.1 The direction

The owner's direction of 2026-10-25, as the brief §1 carries it: Daoris grew through many foundational designs, each with a
mechanism of its own, and may need a refactor to a better structure that names the Fabric, the actor roles and the event system
as the foundation the rest stands on, designed under D191 and never built as a big-bang rewrite. The survey reads the cost: 51 of
the 273 fix-log entries of 2026-10-15 to 2026-10-25 came from two mechanisms answering one question differently (survey §0, a
judgement this design inherits and has not recounted). The quest's case, a finished record's words waiting days for its own
account while others ran work, is decided (LIMITSWITCH1, D137's note of 2026-10-25) and built for driven records on a session
branch that is landing; its remainder, LIMITSWITCH2, is a conversation's go-on and a closed quest's session.

What the three proposals share, and this design keeps without argument: three concepts over the stores Daoris already has, each a
type with one owner and a census, never a new authoritative store for what already has one (D173's first guard). **Who acts** is
a participant read. **How work moves** is one pure decision both loops call, with one durable record per move in flight. **What is
said** is one closed vocabulary of events, every channel that tells *that something happened* a projection of it, while pushed
slices stay state, command answers stay answers and the event file, the lineage and the channel stay stores. Nothing new reaches
the page as vocabulary (D193's rejection), and the first row builds the account rule.

### 0.2 Corrections to the brief, the survey and the proposals, checked

| Claim | At main (`92ac2bd7e`) | Who is right |
|---|---|---|
| Survey §2.2 N, §2.4 row 6: the transient retry reaches driven quest sessions on the protocol door only | **Stale.** A conversation's and Ask Daoris's session take `ChatRetry` once open (`Driver/Conversations/Chat/ChatRunner.cs:1666`), AGENTERRORS1b, built (D210's note of 2026-10-25) | X |
| Survey §2.4 row 1: *one question, one flag, four sites* | The rule is computed twice, as `ResumeChoice.Waits` (`Driver/Accounts/Rotation/ResumeAccount.cs:78`) and inline in the roster (`Driver/Accounts/Harnesses/HarnessRoster.cs:1008`), and the wake has a third of its own (`Chat/LimitWaits.cs:186`). A conversation's and Ask Daoris's resume ask `walks: false` (`Chat/ChatGoOn.cs:571`, `:672`), so a resume that no longer waits answers `Elsewhere`, which becomes `CannotGoOn` to the person (`:581`, `:682`); and the ended move a conversation would take refuses a record no limit ended (`Chat/LimitMove.cs:213`). LIMITSWITCH1's build adds a parameter, `switches`, read only for a driven record of an open or taken quest (its `HarnessRoster.ResumeAsync`), so after it lands the rule is a flag and a parameter in three places | O |
| Survey §1.3 row 12 (*unverified*): a carry-on's model can come from another agent's entry | **Holds, and wider.** `agent = handedQuest?.Agent ?? config.Adapter` (`Loop/Driver.cs:1917`); `GoOns.ResumesOwn` resumes words only where the record ran on that agent (`Loop/DriverContinuations.cs:40-45`); `config.Adapter` is Build's first agent by the write-through (`Driver/Platform/ActorChoices.cs:173`). A record Build's walk ran on a later choice's agent is never resumed by the person's words, and its carry-on runs the first agent with that record's asked model (`Driver.cs:2019-2023`), against D205 point 1 | O |
| Survey §1.3 row 8: the kind read from the marks by *at least 27 lines* | A floor. A rough grep at main finds 138 comparison-shaped C# lines outside tests and 162 page lines naming a mark or its predicates; both over-count writes and declarations, so row c's census counts them exactly | O, in kind |
| Survey §2.4 row 11: two computations of the earliest reset | Neither is D206 point 2's: both are over one agent's accounts (`HarnessRoster.cs:1728`; `LimitWaits.cs:199`), where point 2 is any account of any of the line's choices | O |
| Survey §3.4 row 3: invalidations refetch what lies beneath a pushed entry *under the same keys* | The page already leaves every live pushed entry out of a broad invalidation (`notPushed`, `Web/bridge/state/slices.ts:309`; `Web/bridge/state/stateHub.ts:77-80`; `Web/app/ShellSignals.tsx:91-122`); only queries beneath them are asked again | X |
| Survey §2.3: one dispatcher keeps the words' guarantee | The attempt mark fails open by design: *Unmarked, the word still goes* (`Driver/Conversations/SaidDoor.cs:50-62`, `:84-90`), and a producer's key is answered once only while its word still waits on `said` (`Core/Sessions/SessionLedger.cs:956`). Across a crash after the wire, a word can go twice | X |
| Survey §2.4's last paragraph: held words drop their `Choice` and `Key` | Also their level and key at construction (`Modules/Loop/SessionWords.cs:179`, `:464`); the file drops the display choice too (`:915-935`); the record path keeps a word with no key (`:701`, `:782`), and so does the headless fallback (`Driver/Conversations/GoOn/SessionSay.cs:197`). The held word's choice is a display choice, not `SaidWord.Choice`, which the service keeps for a supervisor's verified release alone (`SessionLedger.cs:902-907`) | X |
| Survey §1.3 row 8, §1.4 (D203): the kind *stored as `Actor`* against D203's *read, never stored* | `Actor` is D205's provenance, *the kind of work this start serves*, machine-local and absent on older and mirrored records (`Core/Sessions/Sessions.cs:195-204`); the marks are relationships (`:135-165`). They are two facts, and D203 point 1 (accepted 2026-10-23) stands beside D205 | X; C is wrong that D203 point 1 is proposed |
| C §1.3: Ask Daoris refused `helper_ask` *as D203 point 10 decides* | It is D203 point 6 (*Ask Daoris refused at both helper admission judges*); point 10 is the terminal's | O |
| C §2.3: *the loop that holds the home's lease reads every move record at its come-up* | A shell comes up and runs its conversations before and without the lease: `ComeUpAsync` (`Modules/Loop/DriverLoop.cs:777`), whose chat take-up runs at `:1224`, comes before `HoldHomeAsync` (`:841`), and *the host, the page, the conversations* carry on while a headless loop holds it (`:838-840`). A conversation's move is the shell's chat runner's to recover | X |
| O §4.2: the record keeps no account pick, so D206 point 5's *a pick waits for its own account* is the code's choice against the text | The record keeps no account pick (`ResumeAccount.cs:15-17`), and `choice: pick` stands for both a pick of an agent and an account and the chip's agent-only pick (the actors design §3.5(d); `START_CHAT`'s `profile` is optional, `Modules/Bridge/Routes/ConversationRoutes.cs:46-58`), so no reader can tell which was picked. The live limit move moves a picked conversation among its agent's accounts (`LimitMove.cs:85-104`, its one pick rule at `:447` refusing another agent) | O on the code; X that provenance is missing |
| LIMITSWITCH2 as filed on LIMITSWITCH1's branch: a conversation goes on *through `BriefedMove`* on another ready account | `BriefedMove` opens on its record's own account as a pick (`Chat/BriefedMove.cs:133`), refused exactly while it cools; the ended limit move's body walks (`LimitMove.cs:237`) and writes the one durable box a move has (survey §2.5) | C and O |
| Brief §4: *Fabric, actor (角色), role … already have homes* | *Role* has no glossary entry, and Settings words the appointed roles 角色 (`Web/locales/zh/settings.daoris.json:108`), the glossary's word for *actor* (`glossary.json:475`; `zh/settings.ai.json:28`), so two concepts share one Chinese word on one screen. *Fabric* has no entry either. And the glossary's *actor* says *a co-worker's role names its actor*, so *role* also names D203's helper, partner and reviewer | O, with the third use found here |
| Brief §5 Q16: *line* | The glossary's *line* is the branch work lands on, 主线 (`glossary.json:530`); D167's line of work is the *session*, 会话 (`:482`) | X and O |
| Survey §1.3 row 17: the lineage *read by nothing* | Holds; and `ChildLineage.Read` folds an unreadable file as no children (`Driver/Sessions/ChildLineage.cs:134-137`), which a reader must not take as *nobody* (COWORKVIEW1 row a's rule 9 holds it) | X |

The rest the design rests on holds as the survey and the proposals cite it, with line drift of a few lines: the store's triggers
in the statement that writes a record, so a connector built before them still moves them (`Core/Sessions/Sessions.cs:652-681`);
the say door's one exclusive step (`SessionLedger.cs:931-973`) and its refusal of a word for an active record not asked for its
run (`:945-952`); the change stream as families, never records (`Core/Platform/ChangeStream.cs:76-80`), whose observer writes
`store_identity` (`:489-490`); the role filter's *no record, nothing refused* (`Mcp/RoleFilter.cs:107-115`); the cap's
`HoldsSlot` (`Driver/Quests/Domain/Planner.cs:872-885`); the page's mark predicates (`Web/work/identity.ts:70-79`); `actor`
already in the sessions slice's allow-list (`Modules/Loop/SessionsStateSlice.cs:46`); the watch's end flags
(`Driver/Quests/Domain/Attention.cs:252`); `SESSION_ENDED`'s one writer for a conversation's end (`DriverLoop.cs:1228-1234`) and
the usage slice waking on it alone (`Modules/Loop/UsageStateSlice.cs:47`); the chain's ranks 3 and 4 bound nowhere
(`Driver/Platform/Chain.cs:170-172`); the chat's limit read by its sentence alone (`ChatRunner.cs:2697`); the session feed's
record with no account, `said`, actor or conversation id (`Core/Sessions/SessionFeed.cs:22-26`); and a closed quest's lost
session going on as a conversation through the shell's runner (`Loop/Driver.cs:2367-2384`).

## 1. The three proposals, question by question

The question numbers are the brief's §5. The second table says what each choice rejects; the sections after give the design.

| Q | C | X | O | Chosen |
|---|---|---|---|---|
| 1 **The participant** | A read over the stores; typed ids; its kind the stored `actor`, derived for records from before | A read contract with subject variants, relations, visibility and provenance; bindings issued by the service in new local-only tables | A type twinned in the service and the driver; its role read from the marks (D203 point 1), `actor` only provenance checked against it | **A read, never a store (all). Its kind from the marks by one rule in the service, twinned in the driver (O); `actor` D205's provenance, held equal by a census (O, X); X's visibility field; ids Daoris already issues (C, O); no binding table yet** (§2.1, §2.2) |
| 2 **Rule and admission** | One `KindRule` {mode, agents, minutes, atOnce} over today's keys; three admission tables, twinned | One resolved policy with typed requirements; a staged admission with closed refusal codes; unknown named subjects read only | One `KindRule` per kind; four layers (start, call, act, contain), a table each keyed by role, failing closed; the brainstorm's `off` honoured | **O's four layers, a table each, keyed by the participant's kind; X's policy shape (common fields and typed requirements per kind) and closed refusal codes; failing closed for a named subject (all); the brainstorm's mode given no new behaviour (C, X)** (§2.3) |
| 3 **The choice** | One `StartPlan`; one walk; one re-ask | One resolver whose answer is a settled selection with its reservation, uncounted until committed | One `ActorWalk`; one re-ask; every start through it | **One start plan whose answer is settled and carried to the open (X), one walk, one pass-code set and one re-ask (all); a continuation takes its record's agent and asked choice (all; §0.2 row 3)** (§2.4) |
| 4 **Roles and actors** | A role held by a participant; a holder plays an actor once SUPERVISOR1c runs; one act door late | The same; appointment authority stays in `StandingConfirmation`; no door unified | Two axes of one participant; one act door, both makers | **All three's relation; C's and O's one door and proof, a late row both makers read** (§2.5) |
| 5 **Native subagents** | A participant bound to its part by lineage; first reader COWORKVIEW1 row a; C2a placing calls | The same; unreadable never *no children* | The same; COWORKVIEW1's `start`/`end` lines as its generation | **All three; X's three outcomes of a read** (§2.6) |
| 6 **One decision** | `Course.Decide` → Resume, Move, Wait, Hold, Never | `MovementPlanner.Decide` with preconditions and intended effects | `LineNext.Decide` → Stay (resume, retry, hold) or Move with when, where, how and which words | **One pure `Course.Decide`: C's five answers, O's three axes on a move, X's preconditions; the retry stays inside the run (all); the carry a move's *how* (O)** (§3.1) |
| 7 **The account** | Own for a resume; go on now where ready; wait for the earliest reset of the accounts it may take; a pick waits | The same, with ineligible accounts adding no reset, and an account pick told from an agent pick by recorded provenance | The same; D206 point 5 amended to *a pick of an agent*, by the owner | **The rule once, read by every path (all); the earliest reset over exactly the accounts the line may take, ineligible ones adding none (C, X); a recorded account pick waits, an agent pick walks its agent's accounts, a record with no provenance is an agent pick (X); the briefed move takes the walk (O); one settled selection (X)** (§3.2) |
| 8 **The move record** | A home file per move, the driver library's; recovered by the lease holder | Service-owned local tables, claimed by entry and generation; a destination allocated idempotently; a record even for a same-conversation resume | A home file per move, the driver library's; one come-up stage | **A home file, the driver library's (C, O); recovered by the executor of its kind, a conversation's by the shell's chat runner (X's correction); the destination opened under the move's key (X); a resume writes none** (§3.3) |
| 9 **Words across a move** | Words follow the line's head, judged at the say door; delivered after the open; none held in memory | A word's whole identity at every keep; mandatory acknowledged attempts; receipts after a word leaves `said` | Kept for a winding-up record; one receiver; delivery by id and key; one carry predicate | **All composed, in rows: X's identity first (row b); C's head at the say door (row d); O's keep for a winding-up record (row o); X's attempts and receipts (row p); one carry predicate (all)** (§3.4) |
| 10 **The errors** | D210's kinds the course's input; a chat's limit by code | Typed outcomes; the survey's retry row stale | The same | **All three** (§3.5) |
| 11 **The event** | Record events minted by trigger into a journal; machine events by the lease's loop | The transition's owner mints at commit, through a transactional outbox; three orders kept apart | A driver-side hub of judged deltas, minted by the judge that diffs the records; memory only | **Record events by trigger at the commit (C's mechanism is X's outbox, and the one an older connector also mints through); machine events by each working process's hub at the judgement that owns the fact (O), the lease's loop alone telling plugins and outside readers; X's three orders** (§4.1) |
| 12 **The vocabulary** | `EventCodes` beside `NoteCodes`; an end's reason and cause | One registry with payloads, privacy and rendering; subject-specific codes; *answered* split | `ChangeGrammar` from EVENTHOOK1's grammar; one end class | **X's registry and subject-specific codes, extending EVENTHOOK1's grammar (O); an end's note code and one class (C, O); EVENTHOOK1's published meanings kept by its adapter (X)** (§4.2) |
| 13 **The readers** | A table; a Confirm ungated as a wait | A table; a teammate's mirror gets nothing new | A table; a new `DAORIS.CHANGE` frame | **The three tables are one; no new frame (rejects O's)** (§4.4) |
| 14 **Durability** | The journal durable and bounded; news told once by a durable said-set | A bounded outbox with an earliest cursor; a notice baseline is no event baseline | A change is a wake in memory; the first read a baseline | **C's journal and said-set, with X's floor and no balloon replayed** (§4.5) |
| 15 **Records** | One owner each; `ledger_events` new | One owner per fact; new tables for bindings, moves, receipts | One owner each; one `<s>.move.json` | **§5.2: one owner per record; three new machine-local stores (the journal, the move file, the receipts) and two machine-local fields, none on the wire** |
| 16 **The names** | *role* 职务 in prose; *move* 接续; *Fabric* 协作织网 | *role* 任命职责; *move* 转接; *Fabric* 协作底座 | *role* 职务 on the page, retiring its 角色; no Chinese for words never shown | **O's: 职务 on the page and in the glossary; no Chinese for Fabric, participant, move or event, which never reach the page; *kind* for D203's helper, partner and reviewer** (§6) |
| 17 **The order** | 1a the account rule; 1b words at the say door; 1c the kind | 1 a word's identity; 2 the planner beside; 3 LIMITSWITCH | F1 the account rule; F2 and F3 the concepts named | **The account rule first (C, O, and the quest), a word's identity second (X), the kind third (C, O)** (§10) |
| 18 **The owner's** | Nothing | Nothing new | D206 point 5's amendment | **Nothing** (§8) |

### 1.1 What each choice rejects

| Q | Rejected | Why |
|---|---|---|
| 1 | C's stored kind (`actor` is the kind) | Two truths where they differ, and they differ by design: a record from before D205 carries the marks and no `actor` (`Sessions.cs:195`), and once SUPERVISOR1c lets a holder play an actor, its actor is not its kind. D203 point 1, accepted, already reads it. The defect class of CAPCOUNT1, ASKRAIL1 and SESSBADGE1 was many readers each reading the marks their own way, which one function removes without a stored kind |
| 1 | X's binding tables in the service now | No subject needs an issued identity before C2a and C2b: a record's id, a brainstorm's scope, a holder's id and a child's lineage already place every caller. A table before its reader is a second store waiting for a use (D173's first guard); the row that needs one (a connected child, C2a's) designs it |
| 2 | C's and O's flat rule shape | X's point holds: an opinion's `required`, `verify` and `recheck`, a partner's measured row and the brainstorm's fan-out are rules, and a `{mode, agents}` shape would drop them; common fields plus a typed requirement per kind keep them |
| 2 | One admission judge across processes | The start, the call, the act and the containment answer four questions in processes that share no code (O; `twins.md`); one classification feeding four tables is the unity that holds |
| 2 | O's brainstorm `off` honoured inside the extraction | A rule no start reads today would start refusing the person's own request in a refactor; it is BRAINSTORM1d's door to give `mode` a reader (`BrainstormRules.cs:63-64`), said by the census as a rule with no reader yet |
| 3 | A caller walking again after the plan | The live limit move walks, then opens on `profile: null`, which walks again (`LimitMove.cs:86`, `:142`), so the account judged and the one opened can differ and the first walk counts a start never made (survey §2.4) |
| 6 | One loop for driven lines and conversations; the decision inside each executor | A rewrite of both loops at once; the disagreement is in the decision, and a pure judge both call removes it without moving an executor (all three) |
| 6 | C's *the person's hand-over press ends the part now* for every kind | It would drop a conversation's closing turn, which its press asks today (`ChatHandOver.cs:145-150`); the two timings are a difference of door, stated once |
| 7 | C's *a pick waits for its own account* as written | The record cannot tell an account pick from an agent pick (§0.2), so waiting for every pick pins agent-only picks D205 §3.5(d) lets walk, and moving every pick moves an account the person named |
| 7 | O's amendment of D206 point 5 to *a pick of an agent*, asked of the owner | An account the person named is a constraint D205 §3.5(d) keeps; once it is recorded the rule reads it, and the owner's LIMITSWITCH1 decision already chose another ready account over a wait for every other case, so nothing new is asked (§8) |
| 8 | X's service-owned move tables | One executor per kind writes and takes up a move (a conversation's the shell's chat runner, a driven line's the look), the boxes it generalizes are files recovered there today, and a table family with its routes is a second store family for one record; the one transactional need X names, a destination never opened twice, is met by a key on the open |
| 8 | C's recovery by the lease holder | A conversation's shell runs without the lease (§0.2) |
| 8 | A move record for a same-conversation resume (X) | No word changes record in a resume; the dispatcher's keep, attempt and take already own it |
| 9 | O's receiver judged in the driver from the move record | The service's say door cannot read a driver's file, and a judgement made outside its exclusive step leaves the race LIMITWORD1 and HANDWORDS1-i came from; the head judged inside that step closes it |
| 11 | O's minting by diffing records | A diff of snapshots misses a park answered between two judgements (AUTONOMY1e) and a park made around a restart (`Attention.cs:224`); X's rule, a watcher never manufactures history from its latest snapshot |
| 11 | Minting in the driver's `ServiceClient` | It misses a connector's write and a second process's; a trigger mints for every writer, an older connector included, as the revisions do |
| 13 | O's `DAORIS.CHANGE` frame | The page's state is the slices' (D182 point 2) and its news the notices slice's; a frame of raw events would be a second telling of both |
| 14 | Changes in memory only (O) | A restart loses a park made just before it, today's defect (`SessionLog.cs:38-41`) |
| 14 | C's and O's one writer of the notices in the lease's loop | The same shell tells its conversations' news without the lease, and machine events never cross processes but by the relay; one owner taking every teller's facts keeps both (X) |
| 16 | 协作织网, 协作底座, 接续, 转接, 任命职责 | Fabric, participant, move and event never reach the page (D193's rejection), so they take no Chinese until one does; a move keeps each kind's own words (转交, 继续, 额度用尽); 任命职责 is a phrase where a status has five units, and 职务 is the plain word for the office an appointment gives |

### 1.2 What each proposal got that the others missed

**Claude's.** The account rule as the first row because it removes the most defects for the least change (taken, §10). Words following
the line's head judged inside the say door's one exclusive step, so no part opens between the judgement and the keep (taken, §3.4).
Record events minted by trigger, as the revisions already are, so an older connector mints them too (taken, §4.1). One durable said-set
in place of every first-read baseline (taken, §4.5). The four meanings of *ended* as one event and four declared filters (taken, §4.2).

**Codex's.** The survey's stale retry row and the say door's fail-open attempt (§0.2). A word's whole identity preserved at the shell's
boundary as a small first repair (row b). An account pick told from an agent pick by recorded provenance, and no historical pin invented
(§3.2). A settled selection carried to the open, evaluation uncounted (§2.4). The destination of a move allocated under its key, never
searched by time (§3.3). Three orders kept apart (§4.1). The guarantee across a non-idempotent harness write stated honestly: no automatic
second attempt after an uncertain one, the evidence kept, the person's *Say it again* (§3.4). Conversations running without the lease.

**The Opus reader's.** The survey's row 1 read whole, with the conversation's `CannotGoOn` and the ended move's guard (§0.2). The carry-on's
agent defect confirmed (§0.2). The four layers of admission (§2.3). The kind read from the marks, never stored (§2.2). The carry as a
move's *how*, not a trigger, and the workflow press as a word (§3.1). The briefed move taking the walk (§3.2). 职务 and the collision it
repairs (§6). The end's class half-built in `SessionEnded`'s flags (§4.2).

## 2. Who acts: the participant

### 2.1 The read

`Participant` is a read, never a store. One function, `Participants.Of`, builds it in the driver library from the stores that hold each
kind (survey §1.5), and its twin in the service, `Participants.KindOf`, answers the connector and the HTTP host from the ledger alone; the
two share no code and read one fixture table (`twins.md`).

| Field | Says | Read from |
|---|---|---|
| `Id` | the record's id, unchanged on every wire, so an older driver's spawn names the same caller; for a subject with no record, the identity Daoris already issues it: a brainstorm's scope (`brainstorm-<id>-<turn>`, `Core/Sessions/BrainstormBox.cs:15-26`), a holder's id and generation, a child's part and native id from its lineage | the record; the box; the appointment; `ChildBindings.Fold` |
| `Kind` | one closed code (§2.2) | the marks, by one rule |
| `Serves` | the part it works for: a helper's, a partner's, a reviewer's once D203 point 6 lands; a child's part | `serves`, `piece`, `opinion`; the lineage |
| `Entry` | its line of work (D167) | `entry` |
| `Actor` | D205's provenance: the actor whose choices its start walked, and the choice it took | `actor`, `choice`, `asked` (`Sessions.cs:195-204`) |
| `Binding` | D173's `RuntimeBinding`: adapter, account by profile name, door, harness version, the kept conversation, the process generation | the record's columns; `<s>.harness.json`; COWORKVIEW1 row a's `start` and `end` lines |
| `Visibility` | opaque, observed, connected or recoverable (D173 ADR-B; X) | what its harness said, unknown where it said nothing |
| `Authority` | none, or the appointments it holds (D208, D209); never the owner's | the appointment |

D173's `Participant` and `RuntimeBinding` (ADR-B) are this type and its `Binding`: ADR-A's *no second Session state* holds, and ADR-B's
identity exists in code. `WorkUnit` keeps its kinds and reads participants as its executors. The page reads the kind as data: the
sessions slice's allow-list gains `kind`, judged in .NET (D182 point 6), answered on loopback only, never on the sync wire, so a
teammate's mirror reads as a teammate's session as today.

### 2.2 The kinds

One rule reads them, the first that holds: a mirror is `teammate`; `opinion` is `reviewer`; `serves` with `piece` is `partner`; `serves`
alone is `helper`; `ask` is `intake`; `Repository == "daoris:help"` is `ask-daoris`; `Kind = chat` is `chat`; otherwise `build`. With no
record: a brainstorm's scope is `voice`; an appointment's holder is `holder`; a lineage child is `child`. A meeting's turn is no participant
until D177's turns start an agent (`DriverMeetings.cs:320-322`).

| Kind | Actor (D205) | Its record | Distinct because |
|---|---|---|---|
| `build`, `chat`, `ask-daoris`, `intake`, `helper`, `partner`, `reviewer` | build, chat, ask-daoris, intake, helper, partner, opinion | the session record | they share one record, ledger, dispatcher and connector identity; what differs (admission, tools, time) is a table keyed by the kind |
| `voice` | brainstorm | its pass file | a pass is one turn with no line; a record for it would be a chat-shaped record every older build lists as the person's conversation (`compatible-builds.md`) |
| `holder` | none until SUPERVISOR1c | the appointment | its authority and no connector (`HolderTerminal.cs:44`) |
| `teammate` | none here | a mirror record | another machine's: nothing here starts or admits it, and its marks and actor do not travel (`SessionFeed.cs:22-26`) |
| `child` | its parent's | the lineage, never a record | its identity issued from lineage, never the environment, and read-only (C2a) |

A co-worker is a relation, `Serves`, never a kind (D203 point 1). The kind and the actor agree by a table a census holds on every record
carrying both; where they would differ the marks decide, since the desks write them at the open and a record from before D205 has no
actor. *The driver's own session*, which the cap counts, becomes a property of the kind (`build`, `intake`, `helper`, `partner`,
`reviewer`), the set `HoldsSlot` tells today (`Planner.cs:880-885`), judged equal on every case before it moves.

### 2.3 The policy and the admission

**One policy shape, read from today's keys.** A kind's resolved policy carries what every kind has (enabled and its consent mode, `on`,
`ask` or `off`; the agents it permits; its concurrency; its minutes) and a typed requirement of its own (an opinion's `required`, `verify`
and `recheck` and D155 point 4's maker passes; a partner's measured row, D199 point 4; the brainstorm's fan-out; the hand-over's consent).
It is read from the keys `driver.json` has now (`helpers`, `workspaceHelpers`, `handOver`, `opinions`, `workspaceOpinions`, `brainstorms`,
`workspaceBrainstorms`, Intake's and Ask Daoris's actors), so no key moves and an older build reads its keys as before (D205 point 6;
`twins.md` on `driver.json`'s writers). Each kind's default is written once in the reader's table: helpers `on` (D163's HELPERDEFAULT1
note), hand-over `ask`, opinions none; a partner's is *not a rule yet* until COAGENT1d gives it a key, never `on`; the brainstorm's `mode`
is listed as a rule whose reader is unbuilt.

**Four layers, a table each, keyed by the participant's kind**, sharing one classification and answering closed refusal codes:

| Layer | The question | Where | Replaces | A caller it cannot place |
|---|---|---|---|---|
| Start | may this kind start, for this asker, now | the driver library | `HelperJudge.Judge`, `PartnerJudge.Refusal`, the intake's and Ask Daoris's gates, the counters (survey §1.3 rows 6, 10, 20) | refused, saying the rule |
| Call | may this caller call this tool | the connector | `RoleFilter` and the five per-tool predicates (rows 7, 15, 16), every registered tool classed from the registration, so a new tool is refused until classed (C2a's point 3) | for a session Daoris names: reads served, every mutation refused (C2a's point 1); a connector naming no session, a person's own agent, keeps today's scope |
| Act | may this holder make this act | the shell's pipe | the two role doors (row 22, §2.5) | refused (D208's proof) |
| Contain | is this process contained | the launcher | unchanged (`AgentStarts.StartAsync`, D201) | nothing starts uncontained |

The harness's own rules handed at the spawn (`OpinionPermissions`, `IntakeRoom`, `HelpRoom`, `BrainstormPermissions`) are rendered from
the call layer's table and stay a second guard where the adapter takes settings; a test compares the two cell for cell. *May this session
ask a helper?*, asked in five places today, is asked of the start layer by the page and the terminal through the driver (D182 point 6), and
the service's desk keeps one check as its store's own guard, reading the same row.

**What the person sees change**, each a decision already taken: a partner is drawn as a partner (`identity.ts:79` draws any `serves` as a
helper); Ask Daoris is refused `helper_ask` (D203 point 6); a reviewer's and a brainstorm voice's mutations are refused at the server on
every door, as the handed rules refuse them only where the adapter takes settings (`Driver.cs:3087`). Nothing else, and nothing for a
person who sets nothing.

### 2.4 The choice: one start plan

Every start resolves its agent, choice and account through `StartPlan.Resolve(kind, trigger, record?, pick?, fence?)`: D205's
`ActorChoices.Effective` for the order, one walk choice by choice for the entry (today four, survey §1.3 row 3), D197's account walk inside
each choice, one set of pass codes (today two), one re-ask of a refused model (today five). Its answer is a **settled selection**: actor,
choice, agent, asked model and effort, account, harness identity, the passes made and its reservation. Evaluating it counts nothing; the
launcher consumes the reservation once, and a failure releases it once (X). No caller walks again after it. A continuation takes its
record's agent and asked choice (D205 point 1), never the machine's `adapter`.

| Start | Today | After |
|---|---|---|
| A driven go-on and carry-on | `handedQuest?.Agent ?? config.Adapter` with the record's asked model (`Driver.cs:1917`, `:2019-2023`; §0.2) | the record's agent and asked choice, or the handed agent's Build entry |
| The look's report of a start | names `handed ?? config.Adapter` before the walk (`Driver.cs:1107`) | the plan's answer |
| A terminal's start-from | the first agent only (`SessionsVerbs.cs:32-33`) | Chat's order, as the route's |
| A reviewer's same-agent press; the other `config.Adapter` fallbacks | `record.Adapter ?? Config.Adapter` (`OpinionGateLook.cs:393`; `ConversationRoutes.cs:51`, `:173`; `SessionGroups.cs:882`; `BriefedMove.cs:113`; `ChatGoOn.cs:526`) | the record's actor through the plan; *as before* stays `Effective`'s own answer |
| A helper's and a partner's choice | through `ReviewerChoice` by an opinion's rule, so a helper is told *No second opinion* (`HelperChoice.cs:66-70`) | under their own policy |
| A brainstorm voice | no workspace (`DriverBrainstorms.cs:682-683`), so no pinned version | the request's workspace; a voice that cannot run dropped, never replaced (D205's round-2 note) |
| The live limit move | a walk, then a second at the open (`LimitMove.cs:86`, `:142`) | the settled selection opens |
| A role holder; a meeting | outside, by D208's design; none | named in the census as the two starts outside the plan |

### 2.5 Roles and actors

An **actor** says what kind of work a participant does and on which agent, model and account (D205). A **role** says what a participant
may decide for the owner (D208, D209). A role is **held by** a participant and is never a kind or an actor's property. Today the holder is a
participant of kind `holder`, started by the window outside the pool, playing no actor; once SUPERVISOR1c lets a holder run as an actor
(D208 point 2), it is started through the start plan with its role's own actor, and its authority is unchanged. Being started by an actor
grants nothing: the authority is the appointment's, proven per act by the HMAC over the hello's nonce (`SupervisorProof.cs:313-328`), the key
never crossing the pipe. Its acts are its role's exact forms (`StewardActRows.cs:152-162`; `RoleActs.cs:47-63`), recorded `by` the role and
never as the owner's words (D208 point 1, D209 points 1 to 3); attribution narrows and never widens (D156 points 1 to 4). In the event
vocabulary `role` is a cause of its own beside `person`, `daoris` and `agent` (§4.2), so no reader takes a role's act for the person's press.

The two doors (survey §1.3 row 22: `SUPERVISOR_ANSWER`/`DEFER` with `SupervisorProof.Canonical` against `ROLE_ACT` with `RoleActForm.Bytes`)
become one act door with one canonical form and one whitespace rule, one reading of *a driven repository*, and refusal sentences per role
(`InstancePipe.cs:1365-1381` says *the supervisor* for both). The supervisor's decisions in files and the steward's acts in memory stay as
D208 and D209 chose. It changes an authority's door, so it is built only after both makers read it (row t).

### 2.6 Native subagents

A harness's child is a participant of kind `child`, bound to its part by C1a's lineage (`ChildBindings.Fold`), never a record. What is read
of it is what the fold holds (harness, native id, parent, depth, the signals that named it, its state) within COWORKVIEW1 row a's
generation; nothing of its conversation. What no harness said stays unknown (D173's canary 9), and a read has three outcomes: no file, read,
and unreadable with its kind, never *nobody* for the last (X; COWORKVIEW1 row a's rule 9). Its first production reader is COWORKVIEW1 row a.
At the connector a child is placed per call by its lineage and refused every mutation, an unreadable lineage read as a child (C2a's points 1
and 5); the call layer's child column is that class table. Until C2a is staged, a child calls with its parent's id and role
(`Core/Sessions/CallLineage.cs:11-14`; `RoleFilter.cs:28-30`), the one known gap of the participant's identity, named by the census.

## 3. How work moves: the course and the move

### 3.1 One decision

`Course.Decide(CourseFacts facts, Trigger trigger) → Course`, a pure function in `Driver/Conversations/Domain/`, beside `Continuation.Judge`
(`Continuation.cs:207-239`), whose reasons it reads. The look calls it for a driven line and the chat runner for a conversation; each
executes the answer with the code it has today. Nothing merges the two loops.

**Inputs.** The line's head: its newest part's state, end code, `limited`, actor and asked choice, adapter, profile, tree, kept conversation
and whether its door resumes, teammate, intake, stood down, and its waiting words with their authors. The failure's kind, by D210's reader.
The account facts, read once (§3.2). The person's presses: a pick, *Go on in a new session*, a briefed press, a hand-over with its agent and
account, a stop, a pause. The rules: the hand-over's, the actor's choices, the curve's decision (`SwitchDecision.Decide`). A move record
standing for the line (§3.3).

**Triggers.** Words kept on a parked or ended record; a solicited answer; a limit's end; a limit's wake; a transient end past its bound; a
turn's end with nothing taking (the curve); a hand-over, pressed or asked; the briefed press; a conversation proven lost; the planner's
carry-ons (cut off, stop released, retried, limited, brought forward); a workflow's send-back to an ended conversation; *Go on in a new
session*; *Start a conversation with these words*; the come-up.

**Answers.**

| `Course` | Says | Executed by |
|---|---|---|
| `Resume` | the same part, its own conversation, its own account, the words that go and Daoris's word by its key (`LimitReset`, `TransientRetry`) | the go-on (`DriverContinuations.ContinueAsync`; `ChatGoOn`) |
| `Move` | a new part, on three axes: **when** (now, or at the turn's end), **where** (an agent and choice, an account named or the walk's settled selection), **how** (`brief`, or `carry` once SWITCHCTX1 lands), with its `why` and `by` from one closed enum (`from_why`, `from_by`, `Driver.cs:4622-4633`) and the words it carries | the start (`Driver.RunAsync`; `ChatRunner.StartAsync`), with a move record |
| `Wait` | until a moment, its reason code and the accounts it waits on | the hold (`Driver.cs:2041-2050`; `LimitWaits`) |
| `Hold` | for the person, its reason code and the doors out | the hold's note |
| `Never` | a teammate's record, an intake, a stand-down, a record a later part supersedes | the say door's refusal (`SessionSay.cs:40-46`) |

Every answer carries its **preconditions** (X): the record's revision, the quest's status, the account's cooling entry and the move record's
phase it read. The executor checks them before it acts and asks again where one moved, so no decision runs on facts that changed under it.
The transient retry stays inside the run (D210 point 3), and the course decides only what follows its bound. Two of the survey's fourteen
paths are not decisions: the carry (M) is a move's *how*, and a workflow's press (L) is a word the dispatcher owns (O).

| Path (survey §2.2) | The course's answer, as today's behaviour |
|---|---|
| A, B go-on, driven and conversation | `Resume`, else `Move` or `Wait` by §3.2; B's `CannotGoOn` where its own account cannot carry becomes the same `Move` (row a) |
| C go on in a new session; start-from | `Move` (`resume`), the person's words only; start-from a fresh part with a preface, unchanged |
| D, E, F the limit move, its wake, the driven limit resume | `Resume` on the own account at its reset, else `Move` (`limit`) where an account it may take is ready, else `Wait`; F's step 0, a hand-over made from it, first |
| G the curve | `Move` (`curve`) on the decision's account |
| H, I hand-over of a conversation and of a quest's session | `Move` (`press` or `agent`), the brief only; its **when** by who asked and the door, as today: an agent's ask at the turn's end; the person's press on a conversation at its turn's end with a closing turn asked, and on a quest's session now, as a stop (D163 point 1's *or the person stops it*; D188 point 1). Survey §2.4 row 12 is a difference of door, stated once in the course's table, not a disagreement to settle |
| J the briefed move | `Move` (`resume`), by the walk (§3.2) |
| K the driven carry-on | `Move` (`carry-on`, `retry`, `limit`, …), the record's agent and choice (§2.4) |
| L a chat's workflow send-back | a word between turns; an ended chat's go-on as B |
| M SWITCHCTX1's carry | `Move` with `how: carry` |
| N past the transient bound | `Hold` until the person's word or *Try again* (`Planner.cs:1525-1532`) |

A census lists every site that opens a part `from` another or resumes with words (the planner's carry-ons, the go-ons, the limit move and
wake, the curve, the hand-overs, the briefed move, go-on-new, start-from, the send-back) and fails on one that neither calls the course nor
is named *not yet moved*, as the brief §7.2 asks.

### 3.2 The account

The rule, stated once in `LineAccount.Decide` and read by every path through the course's account facts:

1. **A resume of the agent's own conversation asks for the record's own account** (D131, D137 point 8): ready or near, it resumes there.
2. **The own account cannot carry the words** (cooling, limited, refused, signed out, gone, outside the set, kept for conversations, the asked
   model not offered; `ResumeAccount.Judge`) **and an account the line may take is ready: the line goes on now** in a new part there, briefed
   today and carried once SWITCHCTX1 lands, whatever ended the record (LIMITSWITCH1; D206 point 3 ii). The person's *Go on in a new session*
   still outranks it.
3. **The accounts a line may take**, by its constraint: the asked choice's other accounts of its agent, then the actor's later choices where a
   limit ended it or every account of its agent cools (D205 point 5, D206 point 6; the driven half is LIMITRESUME1e's); **an account the person
   picked and the record keeps** only that account (D205 §3.5(d), D206 point 5); **an agent pick** (`choice: pick` with no account kept), **a
   handed agent** and an actor *as before* only that agent's accounts (D206 point 6); a hand-over's or a handed quest's **named account** first,
   then that agent's walk. A record from before the account pick is kept is an agent pick: Daoris invents no historical pin (X).
4. **None ready: it waits** until the earliest reset of exactly the accounts step 3 allows, one computation (`EarliestReset`), where an account
   signed out, refused, not offered or outside the set adds no reset (X); said in D206 §3.7's words on its record and its rail, woken by the
   look (D206 point 4).
5. **A new part takes one settled selection** (§2.4), never the own account where its conversation could resume there (D206's rejection of a
   briefed carry-on on the same account), and the briefed move, a new part, takes the walk with its own account first where ready
   (`BriefedMove.cs:133` picks the cooling account today, the door that fails exactly when it is needed, survey §2.4 row 13).

It replaces (survey §2.4): row 1's flag at its three places and LIMITSWITCH1's `switches` parameter; row 2's seven answers, which become three
(own for a resume, the settled selection for a new part, a named account first); row 5's pick, by step 3; row 9's maker reading, asked by the
roster for every own account a limit cooled, the wake's included; row 11's two earliest resets; row 13's door. **Ask Daoris** follows the rule,
its new part opened in its workspace's room with its link in the room's history, as D205 §3.5(e) already opens it. **A closed quest's
session** goes on as a conversation in its tree through the shell's runner (`Driver.cs:2374-2381`), the path its lost conversation takes today;
a headless host with no runner waits for the person, as today.

### 3.3 The move record

Every `Move` writes one durable **move record** before the old part ends, and the record owns the move's delivery and recovery until the new
part's first turn answers. It is SWITCHCTX1's carry record (its §2.3) made the record of every move, and what LimitBox already is for one.

- **Where.** `<home>/sessions/<old record>.move.json`, keyed by the record left, as the limit box and the hand-over box are, in the session's file
  inventory (`SessionHomeFiles`) so a delete and a clear take it. If SWITCHCTX1a lands first, its `<s>.carry.json` becomes this file; there are
  never two.
- **What.** Its executor (`conversation` or `driven`); its phase, each with its moment: `decided` → `quiescing` (waiting for its natural
  boundary, the person's cut against an agent's ask kept apart) → `old-ended` → `opening` (its key written) → `opened` (the new record's id) →
  [`copied` → `verified`, the carry's] → `delivering` → `delivered`, or `fell-back` (its code), or `held` (its reason); `why`, `by`, `how`; the
  settled selection's account by profile name; the brief's recipe (the why and the event bound it is composed up to, `ContextBriefs.cs:59-95`);
  the delivery: each word's id and its key `move/<old>/<word>`, a limit's refused turns' bound, the channel messages held. Never a word's text,
  a path or a key.
- **Who writes and recovers it.** The executor of its kind, alone: a conversation's move by the shell's chat runner, taken up at its come-up,
  where `LimitMove.TakeUpBoxesAsync` runs today (`ChatGoOn.cs:195`, before the lease); a driven line's by the look of the loop that holds the
  home's lease, at the slot where uncertain words are settled (`DriverWatch.cs:739`). Each phase is safe to run again, and an unknown effect
  outside Daoris (a spawn or a prompt whose answer was lost) is `held` and never repeated by itself (X).
- **The destination, never opened twice.** The ledger's opens take an optional `opens` key, kept on the record as a field of this machine's
  and never on the wire (`session-record-fields.md`); a repeat open with the same key answers the record it opened (X). An older host answers a
  new record each time, so there the executor finds an open whose answer was lost by its `from` and its moment, SWITCHCTX1 §2.3's step 4, and
  says so in the log.
- **Today's boxes become its adapters.** `<s>.limit-move.json` and the conversation's hand-over box are written beside it, from it, while a
  build in the update's rollback window could read them (D139), and retired in the records-last row. Three records are not moves and stay
  what they are (O): the hand-over's **ask** (`HandOverAskBox.cs:28`), the agent's request through the connector, the service's, the move
  beginning at its approval; `data/handed/<quest>.json`, the agent a quest is handed to until it leaves the machine (`Driver.cs:1914-1917`), a
  constraint the course reads; and `<s>.new-session.json`, the person's press. A same-conversation resume writes none: no word changes record.

Today one path of five keeps a move durable, the ended limit move's box (survey §2.5); the record lands first on the live limit move, the
curve and the driven carry-on, which keep none.

### 3.4 Words across a move

The dispatcher's guarantee (`.claude/knowledge/session-words.md`) holds by construction once six rules hold, each in one place:

1. **A word keeps its whole identity everywhere it is held**: its producer's key, level, files and display choice, through the shell's held words,
   their file and the record path, and the headless fallback (§0.2; row b). A display choice never becomes `SaidWord.Choice`.
2. **Words follow the line's head, judged inside the say door's exclusive step.** `SessionLedger.SayAsync` already reads, judges and writes as one
   step (`SessionLedger.cs:931-973`); it gains the head inside it, by today's two rules (a conversation by its `from` chain; a quest's session by its
   quest's last run here), and answers today's `continued` refusal with the head named, an optional last member an older driver reads past. No
   part can open between the judgement and the keep. The driver's `WordsNever.ContinuedIn` stays the older host's fallback (row d).
3. **A move delivers after the open.** The new record opens in one write, so the head moves at once. The move reads the old part's waiting words
   after that write, never before, and moves each by id, kept on the new part under `move/<old>/<word>` and taken off the old once the new kept
   it. LIMITWORD1's second read becomes the rule of every move (row d for the three that deliver words today; the move record's delivery list,
   row f).
4. **Every word is kept on `said` before anything holds it, a winding-up or moving record included.** The say door keeps a word for an active
   record whose dispatcher closed as waiting for its next part, asked only by the shell and the driver as `running` is (refused today,
   `SessionLedger.cs:945-952`); the shell's holds become keeps; `held-words.json` is read once at a come-up for an older shell's words and never
   written again; a chat dispatcher's `Hold()` during a move (`ChatTurns.cs:468-476`, under which a word is neither kept nor taken, `:192`)
   keeps and queues, as a limit's wait and a press already do (`:499-510`, `:570-599`) (row o).
5. **A word once attempted is never sent again, and an attempt is acknowledged before the wire.** The mark that fails open today (§0.2) becomes
   mandatory: a word whose mark the service did not acknowledge waits, said, and is tried at the next natural point; the service keeps a
   receipt of each producer key after its word leaves `said`, machine-local and bounded, so a retry after the take is answered as kept; the
   come-up sends only words proven never attempted, and an attempted word with no receipt is COWORK1c's uncertain word, said with *Say it again*
   (X; row p). Across a harness write that carries no receipt, that is the guarantee there is: no automatic second attempt, the evidence kept,
   the person's press.
6. **Which words a new part carries is one predicate**, `Carries`, where five sites write it today (survey §2.4 row 14): the person's waiting
   words and a limit's refused turns' words, each once; Daoris's never carried, composed again under their keys; a helper's answer and another
   agent's findings never to a new part (D188 point 4).

### 3.5 The errors

D210's four kinds are the course's failure input, read by the one reader for every door and every kind of run.

| Kind | The course's answer | What changes |
|---|---|---|
| `limit` | §3.2: resume own at its reset, go on now elsewhere, or wait; never a strike (D206 point 1) | a conversation's and a brainstorm's limit read by the reader's code as well as its sentence (today the sentence alone, `ChatRunner.cs:2697`; `DriverBrainstorms.cs:778`); a conversation's limited record written as the ledger's flag at its end, which travels already, instead of a scan of its events (`LimitHeld.cs:17-56`) |
| `access` | `Hold` for the person, naming the account (D210 point 5) | — |
| `transient` | the retry inside the run, driven and conversation (AGENTERRORS1a, 1b); past its bound `Hold`, released by the person's word or *Try again* | AGENTERRORS1's remaining rows reach the other doors and kinds as designed |
| `fatal` | the end with its reason; the planner's strikes as today | — |

A limit or access refusal at a resume's open runs the same readers as every other end (AGENTERRORS1's C4-6), since the open's failure
reaches the course like any other. One try count per kind is stated in the course's table: five transient tries, one native resume that exited
before naming its conversation, three strikes.

## 4. What is said: the event

### 4.1 One envelope, two minters, three orders

A `DaorisEvent` is `{ position | (run, seq), at, code, subject, scope, facts }`: an immutable description of a committed change, its facts
generated ids, closed codes, counts and times, never a name, a word, a path, an account or a key.

| | Record events | Machine events |
|---|---|---|
| What | a service record moved: a part opened (with its `from`, `why`, `by`), moved to a state, limited; a word kept or taken; a quest operation; an ask or a channel message kept | a fact no record holds: an account's wait, a Confirm waiting, a tool call held, a part gone quiet, a landing tried, a move's phase, the look's news, a child seen |
| Minted | **by trigger, in the statement that writes the record**, into `event_journal` in the service's database, so every writer mints it (the host, the driver through it, each connector, and a connector built before the table), as the revisions are minted (`Sessions.cs:652-681`). Each trigger fires on a narrow column (`AFTER UPDATE OF state`, a `said` that grows or shrinks, an insert), so the look's own bookkeeping mints nothing (the change stream's canary 5) | **by the hub of the process that runs the work**, at the judgement that owns the fact (O): the lease's loop for driven work, the shell for its conversations, the executor for a move's phases. The lease's loop alone tells plugins and outside readers (EVENTHOOK1 §4.1) |
| Id and order | `position`, an integer in commit order under the store's write lock, its epoch the store's identity | `run` and `seq` (EVENTHOOK1 §4.3) |
| Scope | the record's workspace, filtered as data (NEEDSSCOPE1) | this home |
| Kept | durable, machine-local, never on the sync wire; bounded by its own trigger, so an older build that never trims still bounds it | memory, one run, bounded per reader with `missed` |

**Three orders, never one** (X): the journal's `position` orders what a store committed; `run`/`seq` orders what a hub told; a slice's
`epoch`/`seq` orders a projection's snapshots and deltas (D182 point 3). A timestamp orders nothing across processes. The change stream's families
stay the wake (`ChangeStream.cs:76-80`): the journal is what a woken reader reads, so it learns *what* moved, in order, rather than *that* a family
moved. Records stay the truth: the journal is never replayed into state, so this is no event-sourced database (D182's rejection; D182 point 7's
*no new bus*: the journal is a table and the hub a teller on the bus there is).

### 4.2 The vocabulary

One registry, `EventCodes`, beside `NoteCodes`, extending EVENTHOOK1's `WorkEventGrammar` (O): each code with its closed facts, its privacy class
(this machine, or outside) and its rendering family (X). A code without words in both catalogues fails its census, so *ConfirmingRestored*'s English
toast (survey §3.3) cannot recur. Codes are subject-specific where the facts differ (X): a part ends, a line of work goes on, work lands.

| Kind of change | Code | Facts |
|---|---|---|
| a part opened | `part.opened` | kind, entry, `from`, `why`, `by` |
| a park, and its answer | `part.parked`, `part.answered` | `for` (the person-waits kinds, `person-waits.md`); `by` |
| an end | `part.ended` | state; `reason`, the note's `ended.*` code; `class`, one closed code from the record's state, its `limited` flag and its note's code (`completed`, `failed`, `limited`, `transient-bound`, `access`, `stopped-person`, `stopped-role`, `stopped-daoris`, `refused-choice`, `awaits-helper`, `stood-down`, `unknown`), the driver's own `SessionEnded` flags its first source (`Attention.cs:252`) |
| a limit | `part.limited`, `account.waits` | the reset as a moment; the account never off this machine |
| a move | `move.decided`, `move.settled`, `move.held` | the move's old and new parts, `how`, `why`, reason |
| a word | `words.kept`, `words.taken`, `delivery.uncertain` | the word's id, `by`, level |
| *answered*, four facts apart | `turn.answered`, `channel.replied`, `question.answered`, `part.answered` | each its own correlation (survey §3.3) |
| a landing | `work.landed`; `work.integrated` once FABRIC-END proves the line holds it | its code |
| a wait on the person | `person.waiting`, `person.answered` | its kind, a confirmation's id |
| a quest's operation | `quest.<op>` | the quest log's operation names, which the channel's quest lines take too (survey §3.4 row 8) |
| a held call; quiet | `call.held`; `part.quiet` | the part |
| a child | `child.seen`, `child.ended` | part, child |
| the hub | `run.started`, `events.missed` | the counts of what waits; missed |

The four meanings of *ended* (survey §3.4 row 1) become one event and four filters in one table, each tested: `SESSION_ENDED` keeps its contract,
a conversation's end (`DriverLoop.cs:1228-1234`); `session/ended` the ends the look drained, as today; the log's `session.ended` every end; *Ended*
the classes the watch says today (`Attention.cs:246-252`). The usage slice wakes on `part.ended` rather than on `SESSION_ENDED`, so a driven end
reaches it at once. `cause`, derived from the class by one table (`person`, `role`, `daoris`, `agent`, `limit`, `transient`, `choice`, `unknown`),
is what a notification reads. `unknown` is a record from before codes, or a writer that wrote none: what nothing said stays unknown (D173's guard).
EVENTHOOK1's published kinds, `session.parked` and `session.ended`, keep their narrower meaning through its adapter, never silently redefined (X).

**Where a sentence is composed.** On the page from the catalogue, in both languages, as notes are (D116; `translation-parity.md`); on the terminal
and in the log in Daoris's English from the same table; to a plugin and an outside agent never a sentence, the code and ids alone (EVENTHOOK1 §4.3).
No reader parses a sentence back into a code.

### 4.3 The channels

| Channel (survey §3.1) | After |
|---|---|
| Pushed slices | **state**, D182's, woken by events, carrying no news |
| `STATE_CHANGED`, `DRIVER_TICK` | compatibility wakes for unmoved readers and plugins (D182 point 2), retired slice by slice; `DRIVER_TICK`'s data (considered, untrusted, waits) moves into the driver and attention slices, so no key is both pushed and invalidated |
| Held answers | demand-driven answers (D182 point 2), each declaring its inputs and forgotten when one moves, a stale completion refused by its version (X; COWORKVIEW1 row a's source fence) |
| `UNDER_WAY`, the confirmations' lists, command answers | correlated operation state, each its own numbered telling; command answers private to their requester |
| `SESSION_ENDED`, `SESSION_ATTENTION`, `NOTICE`, `MESSAGE` | projections of events through one filter table; the streams (`SESSION_OUTPUT`, `SESSION_STREAMS`, `TERMINAL_*`) stay streams |
| The attention watch | an attention policy over events; it no longer establishes that a park happened by comparing snapshots (X) |
| Notifications | the news projection: `data/notices.json` its one durable record, owned by `Notices`, to which every process that tells news (the lease's loop for driven work, the shell for its conversations) submits facts under the file's keeping, rather than the slice, the look and the headless host each editing it (X) |
| The machine log | the machine's diagnostics, its lifecycle lines carrying the events' codes; its twin writers stay twins |
| The event file, the lineage, the ask's channel | stores: the conversation's record (D189 point 1), the child's binding (C1a), D167's and D188's messages, which no event replaces |
| Hook points | `session/ended` and `state/changed` projections; `work/event` the outside projection; `quest/consider`, `work/land`, `work/step`, `work/perform`, `work/watch` and `work/state` calls a plugin answers, which stay |
| The chain | internal wakes over the same vocabulary; ranks 3 and 4, declared and bound nowhere, are not bound because they exist (X): removed by row s's note unless a row by then reads them |
| The quest log | the quests' part of the journal: its insert mints `quest.<op>` |
| Presses told, `ReopenTelling`, `HeavyTold`, `AskMessages` | presses told stay the terminal's channel into the loop; the others become events where they tell news and stay where they tell an agent |
| EVENTHOOK1's relay | an outside projection of `work/event`, as designed |

### 4.4 The readers

| Reader | May read | Never |
|---|---|---|
| This machine's page | every slice, allow-listed, a machine path only in a desktop-only projection; the notices; command answers | a credential, a role key, a raw event's payload |
| The person's notifications | the news projection, from one table; the notify switch gates news alike, and a Confirm waiting is a wait on the person, not news, and stays ungated (D196) | a press the person made, told back to them (`Attention.cs:124-127`) |
| A plugin | `work/event` frames; `session/ended`'s frame as today (D101's fields), with `reason` and `class` added; `state/changed`; `quest/consider` | `said`, a key |
| An outside agent's session | `work/event` frames through the relay: ids, codes, counts, times | a name, a word, a path, an account; an event is no authority to act |
| The terminal | the same .NET judges over the pipe (D182 point 6), English from the codes | another home's records |
| A teammate's mirror | the records that travel, their travelling fields (`session-record-fields.md`) | the journal, move records, the kind, the actor's fields, `said`, receipts, the lineage, notices, the log |

Never leaves the machine: the journal, every machine event but a relayed frame, move records, receipts, notices, the event file, the lineage, the
log, `said`, a participant's binding, every account's name, every path.

### 4.5 Durability and loss

| What | Kept | A restart | How a reader knows it missed something |
|---|---|---|---|
| Records, the journal | durable | nothing lost | a cursor below the journal's floor, or another store identity: the reader reads its records whole and says it did (X) |
| News | its said-set durable in `data/notices.json`, its journal cursor beside it | resumes from its cursor | the floor |
| Machine events | memory, one run | a new `run`, `run.started` | a new `run`, a `seq` gap, `missed` (EVENTHOOK1 §4.4) |
| Slices | memory, rebuilt from the truth | a new epoch | the epoch and the manifest (D182 point 3) |
| Move records | durable | taken up by their executor | — |
| Held answers, `UNDER_WAY`, the confirmations' lists, the hook queue | memory | rebuilt or empty | the read asked again; pipe-waiting keeps its own record |

**One rule for a first read** (survey §3.4 row 6): the baseline gives way to the said-set. A piece of news is told once where the notices record
does not hold it as said, at the first read or later, so a park made just before a restart is told once after it (today dropped by the first
look's baseline, `Attention.cs:224`; `SessionLog.cs:38-41`), and one told before is never told again: reopening the app replays no balloon
(X). Only a home with no said-set yet, the first run of the build that adds it, takes what stands as said. The cursor's catch-up is bounded by the
notices' own week (`Notices.cs:32-35`).

## 5. The designs in flight, and the records

### 5.1 What each in-flight design becomes, and which of its rows wait

| Design | Becomes | Its rows |
|---|---|---|
| **LIMITSWITCH1** (D137's note of 2026-10-25), built on its session branch, landing | the account rule's driven half, as built | lands as built; row a waits for it on main and reads its `Switched` through `LineAccount` |
| **LIMITSWITCH2**, its remainder | row a, re-cut: through the ended limit move's body, not `BriefedMove` (§0.2); an agent pick walks its agent's accounts and only a recorded account pick waits (§3.2); Ask Daoris follows the rule in its room; a closed quest's session goes on as a conversation | moves into row a once D212 is accepted; until then it stays filed, not built |
| **EVENTHOOK1** (proposed, round 4, Codex's read owed) | the first outside projection and the first instance of the vocabulary: built first as designed, then moved onto the journal (row n): its session kinds read the journal and survive a restart, its `WorkEventGrammar` becomes `EventCodes`' outside projection, its kinds keeping their names there, its `work.landed` and `quest.parked` machine events under its own `run` and `seq` | none waits on the foundation; 1d's quest kinds, a later round, are designed from the journal's `quest.<op>` rather than the look's snapshot; row n waits for 1b and row k |
| **COWORKVIEW1** (proposed, round 4, Codex's read owed) | row a: the lineage's first production reader, the participant's `child` half, built as designed; the routing half (rows e, b, c, d, f): re-cut to read `Participants.Of` (row c) for who is at work, the line's head (row d) for the current executor in place of its own selector over `LastRun` (its §3.1), the move records (row f) for what waits on what, and the event codes (rows k to m) in place of reading waits from end codes | row a waits for nothing here; its routing half's round is written after rows c, d and f land |
| **SWITCHCTX1's carry** (proposed, round 5, Codex's read owed); its line view 1c-i accepted and built | re-cut in storage, kept in protocol: its carry record is the move record's `copied` and `verified` phases and `how: carry`, so a carried move and every other kind have one owner of delivery; its compatibility table, replay check, model judgement and fallbacks stay as designed | 1c-i stands; 1a, 1b, 1c-ii, 1d, 1e and 1f wait for row f's record as well as their own owed read; if 1a is accepted and built first, its file is the one row f extends, never a second |
| **AGENTERRORS1** (D210; 1a, 1a2, 1b built) | the course's failure input | 1b2 and 1c to 1g built as designed, in their own order; row g adds only what they do not: a conversation's and a brainstorm's limit by code, and a conversation's limited flag |
| **MODELROLE1** (D205; rows a to e built) | its `Effective` and walk become the start plan's | row j |
| **LIMITRESUME1** (D206), open rows 1e and 1f | read the account rule's constraints | built as designed; 1e's walk across choices is step 3's driven half |

### 5.2 Records and owners

Every record of the survey's §4 keeps its owner unless this table says otherwise; *older* says what a build from before reads.

| Record | Owner after | Travels | An older build |
|---|---|---|---|
| `sessions` with `said`, the marks, `from`, `entry`, `limited`, the actor's fields | the service, unchanged; the kind read, never stored | as today; the kind, `actor` and `from` never | reads it as today |
| `opens` and `accountPicked` on the record (new, rows f and a) | the service, written at the open | no field on the wire | never reads them; an `accountPicked` it cannot see makes it treat the pick as today's `pick` |
| `event_journal` (new, row k) | the service, by trigger | never | never reads it; its own writes mint rows through the triggers, which bound it |
| Producer receipts (new, row p) | the service | never | never reads them |
| `<s>.move.json` (new, row f) | the driver library, written by the executor of its kind | no | never reads it; the boxes written beside it keep its moves recoverable for an older build until row v |
| `<s>.limit-move.json`, the conversation's hand-over box | the driver library, written from the move record as adapters, then retired (row v) | no | reads them until retired |
| `data/handed/<quest>.json`, `<s>.new-session.json`, the hand-over's ask | unchanged: a constraint, a press and a request, read by the course | no | unchanged |
| `held-words.json` | read once at a come-up for an older shell's words, never written again (row o); retired in row v | no | an older shell writes it; a newer one reads it once |
| `data/notices.json` | one owner, `Notices`, every teller submitting facts to it (row m) | no | unchanged shape; an older headless host still writes it, read either way |
| `store_identity` | the schema's initialisation, not the change stream's observer (X; row k) | no | unchanged |
| A reviewer's `serves` (D203 point 6, row v) | the service, at the desk's open | no field on the wire | an older page reads a reviewer with `serves` as a helper, so it lands only where every reader reads `opinion` first (D203 point 6's own rule) |
| Everything else in survey §4 | unchanged | unchanged | unchanged |

## 6. The names

None of these is a new word on the page except 职务, which repairs a collision. They name code, records and prose.

| Concept | en | zh | On the page | Code | Retires |
|---|---|---|---|---|---|
| Fabric | *Fabric*, the three concepts together, in prose (D173) | none | never | none | — |
| participant | *participant* in prose | none until it reaches the page | never | `Participant`, `Participants.Of`, `RuntimeBinding` | — |
| a participant's kind | *kind*; D203's *role* of a helper, partner or reviewer is a kind | the page keeps each kind's own word (助手, 搭档, 阅读者) | as today | `ParticipantKind`, `Participants.KindOf` | the connector's `SessionRole` (`Mcp/RoleFilter.cs:13-19`), which is neither an actor nor an appointed role (X), when its readers move |
| actor | *actor* (D205) | 角色 | unchanged | `ActorChoices` | — |
| role (appointed) | *role* (D208, D209) | **职务** | Settings → Daoris → *Roles*, its label | `Authority` | 角色 on that label (`zh/settings.daoris.json:108`) |
| line of work | *session* on the page; *line of work* in prose wherever *line* could mean the branch | 会话 | unchanged | `Entry` (D167's key), never `Line`, since `LinesStateSlice` is the branch's | — |
| line (the branch) | *line* | 主线 | unchanged | unchanged | — |
| part | *part* | 段 | unchanged | unchanged | — |
| move | *move* in prose | none | never: each kind keeps its words (转交, 继续, 额度用尽) | `Course`, `Move`, `Moves`, `LineAccount` | the boxes' types, as adapters |
| event | *event* in prose (the brief's, EVENTHOOK1's) | none | never: the page shows notices | `DaorisEvent`, `EventCodes`, `event_journal` | `WorkEventGrammar`, as `EventCodes`' outside projection |

职务 enters the glossary with its reasons (D116): 职务 is the office an appointment (任命) gives, which a role is, apart from 角色, the kind of work an
actor is; 角色 joins its avoid list; it fits a label's budget (two units, as 角色). The glossary's *actor* line *A co-worker's role names its actor*
reads *A co-worker's kind names its actor*. Code names that change: `ResumeChoice.Waits` read through `LineAccount` (row a); `HoldsSlot` over the
kind (row c); `SessionRole` (row r); `LimitBox` read as a move (row f), its file moved in row v; `AttentionEvent` a projection of `DaorisEvent`
(row m). Only in prose: Fabric, participant, line of work, move, event.

## 7. The disagreements this settles, each a dated note

By the brief §7.3 each unification states which answer stands, as a dated note on the decision it amends, in the row that makes it.

| # | Question | Today | The answer that stands | Note on, in row |
|---|---|---|---|---|
| 1 | the own account cools: wait or move (survey §2.4 row 1) | a driven open or taken quest moves (LIMITSWITCH1); a conversation, Ask Daoris and a closed quest's session wait | go on now where an account the line may take is ready, for every line | D137 point 7, a conformance note to its 2026-10-25 note; row a |
| 2 | a pick (§2.4 row 5) | `choice: pick` for both an account pick and an agent pick; the live move moves both among the agent's accounts | a recorded account pick waits for its own account; an agent pick walks its agent's accounts; a record with no provenance is an agent pick | D205 §3.5(d) and D206 point 5, a clarifying note; row a |
| 3 | the earliest reset (§2.4 row 11) | two computations, each one agent's | one, over exactly the accounts the line may take, ineligible ones adding none | D206 point 2; row a |
| 4 | the briefed move's account (§2.4 row 13) | its own account as a pick | the walk, its own account first where ready | D189 points 2 and 3; row a |
| 5 | a carry-on's agent (§1.3 row 12) | the machine's first agent with the record's asked model | the record's agent and asked choice | D205 point 1, a conformance note; row j |
| 6 | the kind: stored or read (§1.3 row 8) | both, by 27 lines and more | read from the marks by one rule; `actor` D205's provenance | D203 point 1 and D205 point 10, a note that each stands; row c |
| 7 | words during a move (§2.4 row 3) | box, hold or none | kept on `said`, judged at the head inside the say door, delivered after the open | D188 point 6; D167 point 1; rows d and o |
| 8 | an attempt mark that fails (§2.3) | the word still goes | the word waits, said; receipts after the take | D188 point 7; D137; row p |
| 9 | Ask Daoris asks a helper (§1.3 row 6) | allowed by the helper's copies, refused by the partner's | refused | D203 point 6, conformance; row q |
| 10 | a caller the connector cannot place (§1.3 row 16) | passes | reads served, every mutation refused, for a session Daoris names | D173's FABRIC-C2a note; D199 for the role filter; row r |
| 11 | a reviewer's and a voice's mutations at the server (§1.3 rows 7, 15) | pass | refused by the call table | D155, D176; row r |
| 12 | a park or a held call across a restart (§3.4 rows 2, 6) | a park dropped by the baseline; a held call told again | told once, by the durable said-set | D196 and the watch's SURF5b note; row m |
| 13 | the usage slice and a driven end (survey §3.3) | reached only at its period | woken by `part.ended` | D182's PUSH1e note; row l |
| 14 | the chain's ranks 3 and 4 (`Chain.cs:170-172`) | declared, bound nowhere | removed unless a row reads them | D173's FABRIC-CHAIN1 note; row s |
| 15 | the notices' writers (§3.4 row 10) | the slice, the look and the headless host, each editing the file | one owner, `Notices`, to which each process that tells news submits its facts under the file's keeping | D196; row m |

## 8. What is the owner's

Nothing here needs D191 point 3's answer. It changes no direction: the owner's FOUNDATION1 direction asks for exactly this structure. It spends
nothing beyond development's own runs and what the owner already decided: row a moves a conversation's, Ask Daoris's and a closed quest's words
to another ready account, which LIMITSWITCH1's decision covers in its own words (*words to an ended record whose own account is cooling no longer
wait … while another account of the line's choices is ready*), and an account the person named stays pinned (§3.2), so no account is spent that
the owner did not choose. It needs no sign-in, publishes, pushes and rewrites nothing, and loses no data: the journal trims only rows the records
still hold, and an old box or `held-words.json` is retired only once no build reads it and every word in it is on `said`, which row v proves.
Enabling a plugin that tells outside programs, appointing a role and changing an actor stay the owner's existing doors; no row turns one on.

Near the line, and named so the steward can check them: rows q, r and t grant or refuse authority (who may start, call and act), and row v
deletes records, so each is built only after both makers have read it (D191's note of 2026-10-24); and the whole of D212 waits for both reads.

## 9. Rejected

| Rejected | Why |
|---|---|
| A `participants` table, or a record per brainstorm voice or child | two authoritative stores for one concept (D173's first guard); the C0 audit rejected a record per child (`D173.md:91`) |
| A stored kind, or the kind read from `actor` | §1.1, Q1 |
| One admission judge across the connector, the driver, the pipe and the launcher | §1.1, Q2 |
| One loop for driven lines and conversations | §1.1, Q6 |
| The service owning the move record | §1.1, Q8; a move ends a process, spawns a harness and reads an account's home, all the desktop's (D47 §4) |
| A second carry record beside the move record | two owners of one delivery, the gap LIMITWORD1 came from |
| The dispatcher owning a move | a dispatcher is per record and a move spans two |
| Minting record events in the driver's client, or by diffing snapshots | §1.1, Q11 |
| An event-sourced database, or replaying the journal into state | D182's rejection: records stay the truth |
| One bus for state and news, or events replacing pushed slices | state is pushed with its data (D182 point 2); news is told once; two jobs |
| Retiring `STATE_CHANGED` now | plugins and every pull not yet pushed read it (D182 point 2) |
| Words or names in an event a plugin or outside agent reads | EVENTHOOK1 §4.3: a name may be shaped like a path and a word is a prompt someone else wrote |
| A new key per kind in `driver.json` | an older build reads today's keys; one reader over them changes nothing it reads |
| A message or a channel post doing a move's or a quest's job | D167's rejection of replacing quests by messages; D188 point 4 |
| Showing *participant*, *move* or *event* on the page | D193's rejection |
| A model deciding a course or classing an event | D24; D157's declared facts |
| A timer per wait, or a second loop for conversations | D206's rejection; the look is the wake |
| A record-format cutover before every reader reads through the concept | the brief §7.4; an older build ignores a new flag, so new meaning goes where it cannot misread (`compatible-builds.md`) |

## 10. The rows, in build order

Each row is its own branch, lands with every gate of the brief §6 green and its area's suites run whole, leaves a person who sets nothing seeing
Daoris work as today except where its row names a decided change, and adds its own proof. A row that introduces a concept reads it beside today's
mechanisms and moves no reader; a row that moves readers moves one path or channel, the old surface its adapter, the census failing on a path that
neither moved nor is named. **Rows marked ⚖ grant or refuse authority, or delete, and are built only after both makers' reads** (D191's note of
2026-10-24). The rows are filed in `TASKS.md` when D212 is accepted; until then FOUNDATION1 points here. *Row a* in this design is
FOUNDATION1a, and so on.

### The first three

**FOUNDATION1a — one account rule, LIMITSWITCH2 inside it** (driver, web; after LIMITSWITCH1 lands on main).
- *What.* `LineAccount.Decide(own hold, trigger, constraint, line kind, the walk's answer over the accounts it may take) → Resume | Elsewhere(selection,
  why) | Wait(earliest, account)`, pure, in `Accounts/Rotation/` beside `ResumeAccount`: §3.2's rule. Read by `ResumeChoice` (`Waits` and
  LIMITSWITCH1's `Switched`) and the roster's resume, whose inline twin (`HarnessRoster.cs:1008`) and `switches` parameter become one call; by the
  chat's and Ask Daoris's go-on (`ChatGoOn.cs:571-587`, `:672-688`), the wake (`LimitWaits.cs:186`, `:199`) and the closed quest's hold
  (`Driver.cs:2367-2384`). Executors unchanged but for three: a conversation's and Ask Daoris's *elsewhere* goes on through the ended move's body,
  its guard (`LimitMove.cs:213`) widened to an own account that cannot carry, why `account`, its box written, the brief and the waiting words handed
  once, Ask Daoris in its workspace's room; a closed quest's session through `GoesOnLost`'s path as a conversation in its tree; the briefed move
  through the walk, own account first where ready. The live limit move opens on the selection it judged, one count (`LimitMove.cs:86`, `:142`). A
  start that names an account (`START_CHAT`'s `profile`, the terminal's account) keeps `accountPicked` on the record, machine-local, by
  `session-record-fields.md`'s chain, and the rule reads it.
- *Census.* Every caller of `ResumeAsync`, `CoolingOf`, `CoolingsOf` and `ResumeChoice.Waits` reads `LineAccount` or is named *not yet moved*.
- *Proof.* `LineAccountTests`: own (ready, near, cooling, signed out, refused, missing, outside, kept, not offered) × trigger × constraint (none, agent
  pick, recorded account pick, handed agent, named account) × others (ready on its agent, ready on another choice, none) × kind (driven open or taken,
  driven closed, conversation, Ask Daoris), every row LIMITSWITCH1 and LIMITSWITCH2 do not change compared with today's `ResumeChoice` and
  `LimitWaits` answers in the same commit. LIMITSWITCH2's proofs, each seen failing first: a stand-in ended conversation whose own account cools with
  another ready goes on there in one briefed part, the words once; with none ready it waits and names the earliest reset; a recorded account pick
  waits for its own; an agent pick goes on among its agent's accounts; Ask Daoris goes on in its room; a closed quest's session goes on as a
  conversation in its tree; the live limit move's judged account is its opened one, counted once. `ChatGoOnTests`, `ChatGoesOnProcessTests`,
  `LimitWaitsProcessTests`, `ContinuationTests`, `AccountRotationTickTests`, `LimitResumeTickTests`, `ResumeAccountTests` and
  `SessionMessagesTickTests` whole; the held row in both languages. Installed: the next real cooling own account with a conversation's words goes on
  within one look (`session.moved … why=account`).
- *Notes.* §7 rows 1 to 4.

**FOUNDATION1b — a word keeps its identity wherever it is held** (modules, driver; any time, a day).
- *What.* The shell's held word carries its producer's key, level, files and display choice (`SessionWords.cs:179`, `:464`), kept in
  `held-words.json` additively, so an older shell reads text, files and door as before (`:915-935`); the record path passes the producer's key
  (`:701`, `:782`), and so does the headless fallback (`SessionSay.cs:197`). The display choice never becomes `SaidWord.Choice`.
- *Census.* Every `SayAsync` outside tests passes a key or is named as a legacy case.
- *Proof.* A producer's retry keeps one word; a restart keeps key, level, files and display choice; an ordinary display choice never reads as the
  supervisor's (`SessionLedger.cs:902-907`); a Process case restarts the shell while a record winds up and delivers once; `SessionWordsTerminalTests`,
  `SessionWordsWaitingTests`, `SessionLedgerTests` and `SessionStoreTests` whole.

**FOUNDATION1c — the participant's kind, read once** (service, driver, web).
- *What.* `ParticipantKind` and `Participants.KindOf` in the service, its twin in the driver, one fixture table both suites read (§2.2); the service
  answers `kind` for every record of this machine on loopback, never on the wire; the sessions slice's allow-list gains it; `HoldsSlot` reads the
  kind's table; `identity.ts` reads the pushed kind, so a partner is drawn as a partner. The `Participant` type with its census, read by nothing else
  yet.
- *Census.* A: on every fixture row `KindOf` agrees with `HoldsSlot`, `identity.ts`'s predicates (the page's test reads the same fixture),
  `ConnectorRoles`' reading of `serves` and `piece` and `WordsNever.IsHelp`; each disagreement listed as a decision (the partner drawn as a helper is
  one). B: every line outside `KindOf` that reads `Serves`, `Opinion`, `Ask`, `Piece`, `HelpMode` or `HelpRoom.Repository` to tell a kind is named
  *not yet moved* with the row that moves it; a new one fails. C: every record carrying an `actor` maps its kind to it.
- *Proof.* `SessionStoreTests` (a record from before answered its kind; a mirror `teammate`), `SessionsStateSliceTests` (`kind` pushed, allow-listed,
  loopback only), `PlannerTests`' CAPCOUNT1 and SESSBADGE1 cases, ASKRAIL1's rail case, the page's identity tests with a partner record;
  `McpToolsTests`, `ActorChoicesTests` and `SessionLedgerTests` whole.

### The rest, in order

| Row | What | Proof |
|---|---|---|
| **1d** words follow the line's head at the say door | §3.4 rules 2 and 3: the head judged inside `SayAsync`'s exclusive step, `continued` answered with the head named (an optional member); the ended and live limit moves and the driven carry-on read their words after the new record's open (`LimitMove.cs:157-166`, `:249`, `:266-270`; `Driver.cs:2386-2402`) | first the survey's *unverified* gap made a test: a word said to a live limit move between the old part's end and the new open lands once on the new part; LIMITWORD1's, HANDWORDS1-i's and SAIDBACK1e's cases green; the say door's twin tables cell for cell; `SessionLedgerTests`, `DrivenInboxTests`, `ChatGoOnTests`, `HandOverTests`, `CurveMoveTests` whole; the say door's latency on the install before and after |
| **1e** the course, beside | `Course.Decide` over paths A to N reproducing today's answers, read by nothing; one closed `from_why`/`from_by` enum; each place two paths differ on the same facts listed as a decision for its row | the paths' suites' cases ported as its table; the trigger-site census (§3.1) |
| **1f** the move record | `<s>.move.json` with its phases, its executor and its come-up stage per kind; the ledger's `opens` key; first on the live limit move, the curve and the driven carry-on, which keep none, then the hand-over and the ended limit move with their boxes as adapters | a shell killed at each phase comes up and delivers each word once, a Process case per phase; a lost open's answer found by its key, never opened twice; an older host's open found by `from` and moment; `HandOverProcessTests`, `LimitWaitsProcessTests`, `CurveMoveTests` |
| **1g** failures read once on every path | what AGENTERRORS1's remaining rows do not already reach: a conversation's and a brainstorm's limit by the reader's code, a conversation's limited record written as the ledger's flag at its end | a chat's limit by code alone moves it; AGENTERRORS1's C4-6 case on a conversation; `AcpTransientTests`, `ChatTransientProcessTests`, `TransientRetryTickTests`, `LimitResumeTickTests` whole |
| **1h** conversations onto the course, one path per branch: the curve; the live and ended limit moves; the wake; the go-on; the briefed move and go-on-new; the hand-over | each path calls `Course.Decide`, its old decision the adapter; §7 row 15 | the path's suite whole (`CurveMoveTests`, `LimitWaitsProcessTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `HandOverTests`, `HandOverLookTests`); the census's *not yet moved* list shorter by one |
| **1i** driven lines onto the course, one path per branch: the go-on and the limit resume; the quest's hand-over; the carry-on; past the transient bound; the send-back | as 1h | `ContinuationTests`, `SessionMessagesTickTests`, `LimitResumeTickTests`, `PlannerTests`, `TransientRetryTickTests`; the family rehearsal |
| **1j** one start plan and one walk | §2.4, one start per branch; four walks into one, one pass-code set, one re-ask; §7 row 5 | `ActorChoicesTests`, `HelperLookTests`, `OneTurnReaskTests`, the intake's and the brainstorm's suites; a record on Build's second choice resumed by the person's words; exactly one reservation per start |
| **1k** the event journal, read by nothing | `event_journal` and its triggers on `sessions`, `quest_log`, `asks` and the channel; `store_identity` written by the schema's initialisation | each ledger move once, in order, with its code; an older build's own statements, run in the test, mint rows (`compatible-builds.md`); the look's bookkeeping mints none; the bound holds; the install's write latency and CPU before and after |
| **1l** one end | `part.ended` with its reason and class, §4.2's filter table; `SESSION_ENDED`, `session/ended`, the log's `session.ended` and *Ended* each read their filter; the usage slice wakes on it | `AttentionTests`, `NoteCodesTests`, `PluginKitTests`, `StatePublisherTests` whole; a driven end wakes the usage slice; LIMITRESUME1a's case; the class table both ways against `NoteCodes`' `ended.*` |
| **1m** news from the journal | the attention policy reads parks and ends from the journal by a durable cursor; one owner of the notices; the said-set in place of every first-read baseline; the notify rule in one table | a park made just before a restart told once after it; a held call standing across one told once; nothing told twice; `AttentionStateSliceTests`, `AttentionTests`, `LookSaidTests`; the look's time on the install before and after |
| **1n** EVENTHOOK1 onto the journal | after EVENTHOOK1's rows land as designed, its session kinds read the journal (§5.1) | its grammar census and relay proofs whole; a restart loses no session event |
| **1o** words never held outside `said` | §3.4 rule 4: the say door's keep for a winding-up record; the shell's holds become keeps; a chat's `Hold()` during a move keeps and queues; `held-words.json` read once, never written | `DrivenInboxTests`, `SessionMessagesTickTests`, `ChatGoOnTests`; an older build's statements over a word kept for a winding-up record; a restart between keep and delivery; the say-to-wire latency on the install |
| **1p** attempts acknowledged, and receipts | §3.4 rule 5; §7 row 8 | the attempt's commit failing sends nothing and says so; a commit whose answer is lost; a crash before and after the wire; a producer's retry after the take answered as kept; no automatic resend of an uncertain attempt, its words in en and zh; the install's delivery latency and service writes before and after |
| **1q** ⚖ one policy shape and start admission | §2.3's policy over today's keys and the start layer's table; §7 row 9 | `HelperLookTests`, `HelperJudgeTests`, `PartnerJudgeTests`; the five doors of survey §1.3 row 6 asked the same cases; `DriverConfigKeepsTests` (no key moved) |
| **1r** ⚖ the connector by kind, failing closed | the call layer's table over every tool × every kind, derived from the registration; `SessionRole` becomes the kind; §7 rows 10 and 11 | `McpToolsTests`, `McpRoleFilterTests` over every kind × every tool; a stand-in named caller with no record served reads and refused mutations; C2a's measurement of the ACP doors first (FABRIC-ACP1) |
| **1s** push only | with D182's remaining rows: `DRIVER_TICK`'s data into slices; `STATE_CHANGED` for unmoved scopes and plugins alone; held answers by declared inputs and versions; §7 row 14 | `pushedState.test.tsx`, `bridge/state/*.test.ts*`, `StateSlicesTests`; bridge reads per minute on the install with two windows, before and after (D182 point 8) |
| **1t** ⚖ one role door | §2.5: one act form and proof, the holder a participant of kind `holder` | `PipeCensusTests`, `StandingConfirmationTests` whole; a role's act never read as the person's |
| **1u** 职务 | the Roles label, the glossary's entry and *actor*'s line (§6) | `i18n:check`, `names:check -- --strict`; the label within its budget in both themes and widths |
| **1v** ⚖ records last | the old boxes, `held-words.json`'s reader and the driver's `ContinuedIn` fallback retired once no build in the update's rollback window reads them; a reviewer's `serves` (D203 point 6) once every reader reads `opinion` first; the move file and the carry's one file | an older build's statements over each new shape; `SessionHomeFiles`' inventory and its kept names; `QuestSyncTests` (nothing new on the wire); `SessionStoreTests` (the field null for a record from before and for a mirror); every word of a retired file on `said` before it goes |

1a is first because it removes the most defects for the least change: one pure function and the places that read it remove survey §2.4 rows 1, 5,
11 and 13, the class of LIMITSWITCH1, LIMITRESUME1b, LIMITRESUME1c and LIMITGOON1 (survey §2.6), and build a decision the owner already made. 1b is
a day at the common boundary where three losses sit. 1c proves the participant on the defects it came from (CAPCOUNT1, ASKRAIL1, SESSBADGE1) before
anything is admitted by it. 1u depends on nothing and may land at any time.

## 11. What stays unmeasured, and what this design's checks do not cover

- **The triggers' cost** on `sessions` at the install's write rate, and the journal's size at its bound: row k measures write latency and CPU.
  That every end writes a reason code a trigger can read in `note_parts` is read from the state's statement and the codes' list, not proven for
  every writer; one that writes none is told `reason` absent and class `unknown`.
- **The head's cost at the say door**: a chain of `from` reads per word, with no index on `from_session` known (*unverified*); row d measures it.
- **Whether the course reproduces every live combination**: it is proven on the cases the paths' suites hold and on the census, not on every state a
  line reaches on the install.
- **The live limit move's word gap** (survey §2.4 row 3, *unverified* that it loses one): row d's first test.
- **News after a long downtime**: whether a cursor resuming after days tells too much at once; row m bounds it by the notices' week and measures it.
- **The receipts' and the move record's cost**, and how often an uncertain delivery happens: rows p and f measure them.
- **Whether lineage reaches Daoris over either harness's ACP door** (FABRIC-ACP1, C2b's), on which row r's child column waits.
- **Whether two processes append one session's event file**, and whether a shell not holding the lease reconciles its slices only at the manifest
  (survey §3.2, §3.4 row 7, *unverified*): the hub's rule assumes neither.
- **How often each move happens on the install**, which sizes row f's recovery cases: read from the machine log's `session.moved` lines first.
- **A brainstorm voice resolved at the connector through its box** in time for its first call (*unverified*).
- **The defects' classes** (survey §6): a judgement, which this order inherits.
- **The checks.** A document only: `doc-links`, `doc-system`, `doc-shapes`, `doc-duplicates`, `doc-budgets` and `orient-index --check` check its
  place, its links and the index, never its claims. The code was read, not run, on main at `92ac2bd7e`, and LIMITSWITCH1 on its branch at
  `3fdc6bbeb`; EVENTHOOK1, COWORKVIEW1's routing half and SWITCHCTX1's carry are proposed, not built, and what this says of them is read from their
  documents at that commit.
