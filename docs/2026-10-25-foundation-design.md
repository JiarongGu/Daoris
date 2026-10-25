# The foundation: the participant, the course and the move, the event (FOUNDATION1, proposed, round 2, 2026-10-25)

**Standing:** proposed with D212, round 2 under D191, written from main at `c8eccf30e`. Round 1 was the synthesis of the three
sealed proposals, `docs/2026-10-25-foundation-proposal-claude.md` (Claude's), `docs/2026-10-25-foundation-proposal-codex.md` (Codex's)
and `docs/2026-10-25-foundation-proposal-opus.md` (the independent Opus reader's), each written from the brief
`docs/2026-10-25-foundation-brief.md` and the survey `docs/2026-10-25-foundation-survey.md` alone, each difference settled in §1. Both
makers read round 1 (`docs/2026-10-25-foundation-reads.md`): the independent Opus reader as a foundation, 20 findings, 3 blocking,
accept after named changes; Codex, 18 findings, 16 blocking, another round. Round 2 answers every finding by number in §12, each
taken or disputed with the code line the steward checked, the stricter taken where the two overlap, and carries the owner's direction
of 2026-10-25 on roles (the brief §8) in §2.7. It changes a foundation in every sense D191's amendment of 2026-10-20 names, and some of
its rows grant or refuse authority and delete records, so by D191's note of 2026-10-24 it is not accepted on one maker's read: both
makers read round 2 before anything is accepted. Nothing here is built, spends an account, signs in, publishes or deletes.

Read for it: the brief (its §8 amendment included), the survey and the three proposals whole; both reads of round 1; D137, D155, D163,
D167, D173, D182, D188, D191, D193, D197, D203, D205, D206, D208, D209 and D210 at the points this cites; D119 and D120 for the plugins,
whose points' frames are D101's; the in-flight designs EVENTHOOK1 (`docs/2026-10-24-event-hook-design.md`), COWORKVIEW1
(`docs/2026-10-24-cowork-view-design.md`), SWITCHCTX1 (`docs/2026-10-24-switch-context-design.md`) and AGENTERRORS1 (D210 and its
notes); the precedent of this shape, `docs/2026-10-23-actors-design.md` with D205; `.claude/knowledge/claims-need-checks.md`,
`compatible-builds.md`, `session-words.md`, `session-record-fields.md`, `pushed-slices.md`, `person-waits.md`, `twins.md` and
`translation-parity.md`; the glossary. Round 1's claims about the code were checked on main at `92ac2bd7e`; `c8eccf30e`'s `src/` is
that commit's unchanged, so its lines hold, and round 2's checks of the reads' findings were made there. LIMITSWITCH1's build was read
on its session branch, at `3fdc6bbeb` in round 1 and at `adacd5fdc` in round 2, not yet on main when this was written. §0.2 lists
where the brief, the survey or a proposal was wrong, and §12 where round 1 was. `Dnn` is `docs/decisions/Dnn.md`; paths use the
survey's aliases (`Driver/`, `Modules/`, `Host/`, `Core/`, `Mcp/`, `Http/`, `Web/`), with `Chat/` for `Driver/Conversations/Chat/`,
`Loop/` for `Driver/Loop/` and `App/` for the desktop's executable as the survey shortens them. *C* is Claude's proposal, *X* Codex's,
*O* the Opus reader's; *O-n* and *X-n.m* are the reads' findings.

## 0. What this rests on

### 0.1 The direction

The owner's direction of 2026-10-25, as the brief §1 carries it: Daoris grew through many foundational designs, each with a
mechanism of its own, and may need a refactor to a better structure that names the Fabric, the actor roles and the event system
as the foundation the rest stands on, designed under D191 and never built as a big-bang rewrite. The survey reads the cost: 51 of
the 273 fix-log entries of 2026-10-15 to 2026-10-25 came from two mechanisms answering one question differently (survey §0, a
judgement this design inherits and has not recounted). The quest's case, a finished record's words waiting days for its own
account while others ran work, is decided (LIMITSWITCH1, D137's note of 2026-10-25) and built for driven records of an open or
taken quest on a session branch; its remainder, LIMITSWITCH2, is a conversation's go-on.

The owner's second direction of the same day, taken from round 2 (the brief §8): actors and roles are not hard-coded; each role can
be configured and set up into the company, D193's picture of a development company run by agents. The cut: **capabilities in code,
roles in configuration** (§2.7).

What the three proposals share, and this design keeps without argument: three concepts over the stores Daoris already has, each a
type with one owner and a census, never a new authoritative store for what already has one (D173's first guard). **Who acts** is
a participant read. **How work moves** is one pure decision both loops call, with one durable record per move in flight. **What is
said** is one closed vocabulary of events, every channel that tells *that something happened* a projection of it, while pushed
slices stay state, command answers stay answers and the event file, the lineage and the channel stay stores. Nothing new reaches
the page as vocabulary but the words a person needs to see and change a role (D193's rejection; the brief §8), and the first row
builds the account rule.

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
| LIMITSWITCH2 as filed on LIMITSWITCH1's branch: a conversation goes on *through `BriefedMove`* on another ready account | `BriefedMove` opens on its record's own account as a pick (`Chat/BriefedMove.cs:133`), refused exactly while it cools; the ended limit move's body walks (`LimitMove.cs:237`) and writes the one durable box a move has (survey §2.5). The filed row's other half, *a pick and Ask Daoris's room keep the own account*, stands, and round 2 keeps it (O-8) | C and O on the door; the filed row on the rest |
| Brief §4: *Fabric, actor (角色), role … already have homes* | *Role* has no glossary entry, and Settings words the appointed roles 角色 (`Web/locales/zh/settings.daoris.json:108`), the glossary's word for *actor* (`glossary.json:475`; `zh/settings.ai.json:28`). Round 1 read that as two concepts sharing one Chinese word; the owner's direction on roles makes the actor and the appointed role one configured record, so one word is right (§6) | O on the fact; the direction on the remedy |
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
session going on as a conversation through the shell's runner (`Loop/Driver.cs:2374-2384`, wired at `DriverLoop.cs:926`).

## 1. The three proposals, question by question

The question numbers are the brief's §5. The second table says what each choice rejects; the sections after give the design. A
cell marked *(round 2)* changed in this round, by a read's finding or by the owner's direction on roles.

| Q | C | X | O | Chosen |
|---|---|---|---|---|
| 1 **The participant** | A read over the stores; typed ids; its kind the stored `actor`, derived for records from before | A read contract with subject variants, relations, visibility and provenance; bindings issued by the service in new local-only tables | A type twinned in the service and the driver; its role read from the marks (D203 point 1), `actor` only provenance checked against it | **A read, never a store (all). Its work, a capability, read from the marks by one rule in the service, twinned in the driver (O), named `work` because the wire's `kind` already says driven or chat (round 2, O-5, X-2.1); its role the configured role it started under, `actor`'s value and D205's provenance, held to its work by a census (O, X; round 2); X's visibility field; ids Daoris already issues (C, O); no binding table yet** (§2.1, §2.2) |
| 2 **Rule and admission** | One `KindRule` {mode, agents, minutes, atOnce} over today's keys; three admission tables, twinned | One resolved policy with typed requirements; a staged admission with closed refusal codes; unknown named subjects read only | One `KindRule` per kind; four layers (start, call, act, contain), a table each keyed by role, failing closed; the brainstorm's `off` honoured | **O's four layers, a table each, keyed by capability (the owner's direction, round 2); X's policy shape (common fields and typed requirements per work) and closed refusal codes; failing closed for a named subject (all), a brainstorm voice placed by its scope first (round 2, X-2.2); the brainstorm's mode given no new behaviour (C, X); containment as D201 has it, a piece failing closed (round 2, X-1.1)** (§2.3) |
| 3 **The choice** | One `StartPlan`; one walk; one re-ask | One resolver whose answer is a settled selection with its reservation, uncounted until committed | One `ActorWalk`; one re-ask; every start through it | **One start plan whose answer is settled and carried to the open (X), its reservation taken at the selection under the roster's gate and only probes uncounted (round 2, O-18); one walk, one pass-code set and one re-ask (all); a start finds its role by its work and place, and a continuation takes its record's role and asked choice (all; round 2)** (§2.4) |
| 4 **Roles and actors** | A role held by a participant; a holder plays an actor once SUPERVISOR1c runs; one act door late | The same; appointment authority stays in `StandingConfirmation`; no door unified | Two axes of one participant; one act door, both makers | **One configured record, the role (the owner's direction, round 2): D205's actors are roles always on that take work; the supervisor and the steward are roles filled by appointment that hold acts; a holder fills an appointed role, its authority the appointment's, proven per act; C's and O's one act door and proof, a late row both makers read** (§2.5, §2.7) |
| 5 **Native subagents** | A participant bound to its part by lineage; first reader COWORKVIEW1 row a; C2a placing calls | The same; unreadable never *no children* | The same; COWORKVIEW1's `start`/`end` lines as its generation | **All three; X's three outcomes of a read** (§2.6) |
| 6 **One decision** | `Course.Decide` → Resume, Move, Wait, Hold, Never | `MovementPlanner.Decide` with preconditions and intended effects | `LineNext.Decide` → Stay (resume, retry, hold) or Move with when, where, how and which words | **One pure `Course.Decide`: C's five answers and a sixth, `Start`, for a new line with no `from` (round 2, X-2.2's start-from, X-3.2); O's three axes on a move, X's preconditions, the words' state among them (round 2, O-13); the answers-alone hold and the boundary among the facts (round 2, X-3.1); a standing move makes its old part's own go-on `Never` (round 2, O-2); the retry stays inside the run (all); the carry a move's *how* (O)** (§3.1) |
| 7 **The account** | Own for a resume; go on now where ready; wait for the earliest reset of the accounts it may take; a pick waits | The same, with ineligible accounts adding no reset, and an account pick told from an agent pick by recorded provenance | The same; D206 point 5 amended to *a pick of an agent*, by the owner | **The rule once, read by every path (all); go on now where the own account cools, for a driven record, a conversation and a closed quest's session, within LIMITSWITCH1's decision, Ask Daoris and the other holds left as today until each is decided (round 2, O-1); the earliest reset over exactly the accounts the line may take, ineligible ones adding none, and a hold naming the repair where none has a reset (C, X; round 2, X-3.3); a recorded account pick waits, an agent pick walks its agent's accounts, a record with no provenance is an agent pick (X); a hand-over press's named account a recorded account pick (round 2, O-9); the briefed move takes the walk (O); one settled selection (X)** (§3.2) |
| 8 **The move record** | A home file per move, the driver library's; recovered by the lease holder | Service-owned local tables, claimed by entry and generation; a destination allocated idempotently; a record even for a same-conversation resume | A home file per move, the driver library's; one come-up stage | **A home file, the driver library's (C, O), fenced by a claim with the executor's generation (round 2, X-3.5); recovered by the executor of its kind, a conversation's by the shell's chat runner (X), before the come-up's sweep (round 2, O-2); the destination opened atomically under the move's key in the service, an older host's lost open held and never searched for (round 2, X-3.5); a resume writes none** (§3.3) |
| 9 **Words across a move** | Words follow the line's head, judged at the say door; delivered after the open; none held in memory | A word's whole identity at every keep; mandatory acknowledged attempts; receipts after a word leaves `said` | Kept for a winding-up record; one receiver; delivery by id and key; one carry predicate | **All composed, in rows: X's identity first (row b); C's head at the say door, one service head across kinds (row d; round 2, O-6, X-3.4); O's keep for a winding-up record, after the course reads move records (row o; round 2, O-2); X's attempts, dispositions and receipts before the move record (row p; round 2, O-11, X-3.7, X-5.1); a moved word keeping its producer key beside its transfer id (round 2, X-3.6); one carry predicate (all)** (§3.4) |
| 10 **The errors** | D210's kinds the course's input; a chat's limit by code | Typed outcomes; the survey's retry row stale | The same | **All three** (§3.5) |
| 11 **The event** | Record events minted by trigger into a journal; machine events by the lease's loop | The transition's owner mints at commit, through a transactional outbox; three orders kept apart | A driver-side hub of judged deltas, minted by the judge that diffs the records; memory only | **Record events by trigger at the commit (C's mechanism is X's outbox, and the one an older connector also mints through), raw transitions guarded by what changed and coded in .NET at projection (round 2, O-4, X-4.1), with a retention contract (round 2, X-4.2); machine events by each working process's hub at the judgement that owns the fact (O), the lease's loop alone telling plugins and outside readers; every process reading the journal from its come-up (round 2, O-3); X's three orders** (§4.1) |
| 12 **The vocabulary** | `EventCodes` beside `NoteCodes`; an end's reason and cause | One registry with payloads, privacy and rendering; subject-specific codes; *answered* split | `ChangeGrammar` from EVENTHOOK1's grammar; one end class | **X's registry and subject-specific codes, extending EVENTHOOK1's grammar (O); an end's note code and one class, judged in .NET (C, O; round 2); EVENTHOOK1's published meanings kept by its adapter (X)** (§4.2) |
| 13 **The readers** | A table; a Confirm ungated as a wait | A table; a teammate's mirror gets nothing new | A table; a new `DAORIS.CHANGE` frame | **The three tables are one; no new frame (rejects O's)** (§4.4) |
| 14 **Durability** | The journal durable and bounded; news told once by a durable said-set | A bounded outbox with an earliest cursor; a notice baseline is no event baseline | A change is a wake in memory; the first read a baseline | **C's journal and said-set, with X's floor and no balloon replayed; told at most once, the said-set claimed before the balloon, a cursor per teller (round 2, X-4.3, O-12)** (§4.5) |
| 15 **Records** | One owner each; `ledger_events` new | One owner per fact; new tables for bindings, moves, receipts | One owner each; one `<s>.move.json` | **§5.2: one owner per record; three new machine-local stores (the journal, the move file, the receipts), three machine-local fields, none on the wire, and the role catalogue in `driver.json` (round 2)** |
| 16 **The names** | *role* 职务 in prose; *move* 接续; *Fabric* 协作织网 | *role* 任命职责; *move* 转接; *Fabric* 协作底座 | *role* 职务 on the page, retiring its 角色 | **The role 角色, one configured record of which D205's actor and D208's appointed role are both cases, so round 1's 职务 is withdrawn (round 2); no Chinese for Fabric, participant, capability, move or event, which never reach the page; *work* for a participant's capability, D203's helper, partner and reviewer among them (round 2)** (§6) |
| 17 **The order** | 1a the account rule; 1b words at the say door; 1c the kind | 1 a word's identity; 2 the planner beside; 3 LIMITSWITCH | F1 the account rule; F2 and F3 the concepts named | **The account rule first, split in three (round 2, O-10), a word's identity second (X), the participant's work third (C, O); attempts and receipts before the move record (round 2, X-5.1); the company's rows after the start plan (round 2)** (§10) |
| 18 **The owner's** | Nothing | Nothing new | D206 point 5's amendment | **Two open questions, each its own decision, neither built until answered: Ask Daoris going on elsewhere outside a limit, and a conversation carried on where its own account is signed out, refused or gone (round 2, O-1)** (§8) |

### 1.1 What each choice rejects

| Q | Rejected | Why |
|---|---|---|
| 1 | C's stored kind (`actor` is the kind) | Two truths where they differ, and they differ by design: a record from before D205 carries the marks and no `actor` (`Sessions.cs:195`), and once SUPERVISOR1c lets a holder play an actor, its actor is not its kind. D203 point 1, accepted, already reads it. The defect class of CAPCOUNT1, ASKRAIL1 and SESSBADGE1 was many readers each reading the marks their own way, which one function removes without a stored kind |
| 1 | The participant's field named `kind` on the wire (round 1's) | The sessions slice already pushes `kind` as driven or chat (`SessionsStateSlice.cs:41`), typed and read by the page (`Web/data/api.ts:445`; `identity.ts:65-66`; `Web/work/say.ts:90`), so a second meaning breaks every unmoved reader (O-5, X-2.1) |
| 1 | X's binding tables in the service now | No subject needs an issued identity before C2a and C2b: a record's id, a brainstorm's scope, a holder's id and a child's lineage already place every caller. A table before its reader is a second store waiting for a use (D173's first guard); the row that needs one (a connected child, C2a's) designs it |
| 2 | C's and O's flat rule shape | X's point holds: an opinion's `required`, `verify` and `recheck`, a partner's measured row and the brainstorm's fan-out are rules, and a `{mode, agents}` shape would drop them; common fields plus a typed requirement per work keep them |
| 2 | One admission judge across processes | The start, the call, the act and the containment answer four questions in processes that share no code (O; `twins.md`); one classification feeding four tables is the unity that holds |
| 2 | Tables keyed by a fixed list of kinds (round 1's) | The owner's direction: a configured role must be admitted by what it does, so every table is keyed by the closed capability it takes, which a configured role composes and never extends (§2.7) |
| 2 | O's brainstorm `off` honoured inside the extraction | A rule no start reads today would start refusing the person's own request in a refactor; it is BRAINSTORM1d's door to give `mode` a reader (`BrainstormRules.cs:63-64`), said by the census as a rule with no reader yet |
| 3 | A caller walking again after the plan | The live limit move walks, then opens on `profile: null`, which walks again (`LimitMove.cs:86`, `:142`), and the ended move does the same (`:237`, `:256`), so the account judged and the one opened can differ and the first walk counts a start never made (survey §2.4; O-8) |
| 3 | X's *evaluation uncounted, the launcher consumes the reservation* as worded | The roster spreads a look's starts across accounts by counting each chosen start at its selection (`HarnessRoster.cs:894-897`); counting only at the launcher would put a look's starts on one account. The reservation is taken at the settled selection, under the roster's gate, and only a probe is uncounted (`counts: false`, `:911`) (O-18) |
| 4 | Roles hard-coded: eight actor ids and two appointed roles in code (round 1's) | The owner's direction of 2026-10-25: a company's positions are set up, not compiled; the code keeps what each capability may do (§2.7) |
| 4 | A role widening what its work may call, or holding an act that is the owner's | Admission is the capability's, in code; a role narrows it; an owner's act is never a role's (D208 points 1 and 6; D209 point 3's *everything else is never a role's act*) |
| 6 | One loop for driven lines and conversations; the decision inside each executor | A rewrite of both loops at once; the disagreement is in the decision, and a pure judge both call removes it without moving an executor (all three) |
| 6 | C's *the person's hand-over press ends the part now* for every kind | It would drop a conversation's closing turn, which its press asks today (`ChatHandOver.cs:145-150`); the two timings are a difference of door, stated once |
| 6 | Start-from as a `Move` keyed by its source (round 1's) | It opens an independent entry with no `from` (`ChatRunner.cs:339`; `SessionLedger.cs:698`), so a move record keyed by the source would make a second press reuse one destination or supersede the source (X-3.2) |
| 7 | Round 1's row a reaching Ask Daoris outside a limit and a conversation whose own account is signed out, refused or gone | Each reverses a *never by itself* rule LIMITSWITCH1's text does not reach: D137 §2.2 and D205's *one exception* for Ask Daoris (`HandOver.cs:52-57`; `CurveMove.cs:70-71`), and *nothing carries a chat's words on by itself* (`ChatGoOn.cs:566-569`). Each needs its own decision (§8; O-1) |
| 7 | C's *a pick waits for its own account* as written | The record cannot tell an account pick from an agent pick (§0.2), so waiting for every pick pins agent-only picks D205 §3.5(d) lets walk, and moving every pick moves an account the person named |
| 7 | O's amendment of D206 point 5 to *a pick of an agent*, asked of the owner | An account the person named is a constraint D205 §3.5(d) keeps; once it is recorded the rule reads it, and the owner's LIMITSWITCH1 decision already chose another ready account over a wait for every other case, so nothing new is asked |
| 7 | A wait with a reset manufactured where no account the line may take has one | Today `Unready` keeps a non-cooling refusal in exactly that case (`HarnessRoster.cs:1732`); a promised automatic resumption there would never come (X-3.3) |
| 8 | X's service-owned move tables | One executor per kind writes and takes up a move (a conversation's the shell's chat runner, a driven line's the look), the boxes it generalizes are files recovered there today, and a table family with its routes is a second store family for one record; the transactional needs X names, a destination never opened twice and a move never claimed twice, are met by the service's atomic open under the move's key and a fenced claim on the file (§3.3). X keeps the move's execution in the driver too (its proposal §*Durable movement*); the disagreement is where the move's persistence lives, not who executes it (X-6.1) |
| 8 | C's recovery by the lease holder | A conversation's shell runs without the lease (§0.2) |
| 8 | A move record for a same-conversation resume (X) | No word changes record in a resume; the dispatcher's keep, attempt and take already own it |
| 8 | Round 1's *an older host's open found by its `from` and its moment* | Successors are not unique by `from` (`Sessions.cs:998`), so the search can find another part; against an older host a lost open is held for the person (X-3.5) |
| 9 | O's receiver judged in the driver from the move record | The service's say door cannot read a driver's file, and a judgement made outside its exclusive step leaves the race LIMITWORD1 and HANDWORDS1-i came from; the head judged inside that step closes it |
| 9 | A moved word kept under the move's key alone (round 1's) | The service answers a producer's retry only by the addressed record's `SaidWord.Key` (`SessionLedger.cs:956`), so a retry at the new head would keep a second word; the producer key stays the word's, the transfer id beside it (X-3.6) |
| 11 | O's minting by diffing records | A diff of snapshots misses a park answered between two judgements (AUTONOMY1e) and a park made around a restart (`Attention.cs:224`); X's rule, a watcher never manufactures history from its latest snapshot |
| 11 | Minting in the driver's `ServiceClient` | It misses a connector's write and a second process's; a trigger mints for every writer, an older connector included, as the revisions do |
| 11 | Codes, classes and kinds judged in SQL (round 1's) | A trigger cannot read the look's conclusions (`Attention.cs:250-252`), and a third copy of the work rule in SQL would be a twin with no test; the trigger writes the raw transition and .NET codes it (O-4) |
| 13 | O's `DAORIS.CHANGE` frame | The page's state is the slices' (D182 point 2) and its news the notices slice's; a frame of raw events would be a second telling of both |
| 14 | Changes in memory only (O) | A restart loses a park made just before it, today's defect (`SessionLog.cs:38-41`) |
| 14 | C's and O's one writer of the notices in the lease's loop | The same shell tells its conversations' news without the lease, and machine events never cross processes but by the relay; one owner taking every teller's facts keeps both (X) |
| 14 | News told exactly once (round 1's) | Daoris cannot commit its file and the system's balloon together; the said-set is claimed before the balloon, so a crash between loses one announcement, never repeats it (X-4.3) |
| 16 | 协作织网, 协作底座, 接续, 转接, 任命职责 | Fabric, participant, move and event never reach the page (D193's rejection), so they take no Chinese until one does; a move keeps each kind's own words (转交, 继续, 额度用尽); 任命职责 is a phrase where a status has five units |
| 16 | Round 1's 职务 for the appointed role | It named a second concept the owner's direction folds into the role: the appointed role and the actor are one configured record, so the one word 角色 is right on both screens (round 2) |

### 1.2 What each proposal got that the others missed

**Claude's.** The account rule as the first row because it removes the most defects for the least change (taken, §10). Words following
the line's head judged inside the say door's one exclusive step, so no part opens between the judgement and the keep (taken, §3.4).
Record events minted by trigger, as the revisions already are, so an older connector mints them too (taken, §4.1). One durable said-set
in place of every first-read baseline (taken, §4.5). The four meanings of *ended* as one event and four declared filters (taken, §4.2).

**Codex's.** The survey's stale retry row and the say door's fail-open attempt (§0.2). A word's whole identity preserved at the shell's
boundary as a small first repair (row b). An account pick told from an agent pick by recorded provenance, and no historical pin invented
(§3.2). A settled selection carried to the open (§2.4). The destination of a move allocated under its key, never searched by time (§3.3).
Three orders kept apart (§4.1). The guarantee across a non-idempotent harness write stated honestly: no automatic second attempt after an
uncertain one, the evidence kept, the person's *Say it again* (§3.4). Conversations running without the lease.

**The Opus reader's.** The survey's row 1 read whole, with the conversation's `CannotGoOn` and the ended move's guard (§0.2). The carry-on's
agent defect confirmed (§0.2). The four layers of admission (§2.3). The kind read from the marks, never stored (§2.2). The carry as a
move's *how*, not a trigger, and the workflow press as a word (§3.1). The briefed move taking the walk (§3.2). The end's class half-built in
`SessionEnded`'s flags (§4.2).

## 2. Who acts: the participant and the company

### 2.1 The read

`Participant` is a read, never a store. One function, `Participants.Of`, builds it in the driver library from the stores that hold each
kind of subject (survey §1.5), and its twin in the service, `Participants.WorkOf`, answers the connector and the HTTP host from the ledger
and the brainstorm box alone; the two share no code and read one fixture table (`twins.md`).

| Field | Says | Read from |
|---|---|---|
| `Id` | the record's id, unchanged on every wire, so an older driver's spawn names the same caller; for a subject with no record, the identity Daoris already issues it: a brainstorm's scope (`brainstorm-<id>-<turn>`, `Core/Sessions/BrainstormBox.cs:15-26`), a holder's id and generation, a child's part and native id from its lineage | the record; the box; the appointment; `ChildBindings.Fold` |
| `Work` | the capability it was opened for, one closed code (§2.2, §2.7), or a placement with none: another machine's, a holder, a child | the marks, by one rule; the brainstorm's scope |
| `Role` | the configured role whose staffing its start walked (§2.7), D205's provenance, with the choice it took | `actor`, `choice`, `asked` (`Sessions.cs:195-204`) |
| `Serves` | the part it works for: a helper's, a partner's, a reviewer's once D203 point 6 lands; a child's part | `serves`, `piece`, `opinion`; the lineage |
| `Entry` | its line of work (D167) | `entry` |
| `Binding` | D173's `RuntimeBinding`: adapter, account by profile name, door, harness version, the kept conversation, the process generation | the record's columns; `<s>.harness.json`; COWORKVIEW1 row a's `start` and `end` lines |
| `Visibility` | opaque, observed, connected or recoverable (D173 ADR-B; X) | what its harness said, unknown where it said nothing |
| `Authority` | none, or the acts of the appointed role it holds while the appointment stands (D208, D209); never the owner's | the appointment and its role |

D173's `Participant` and `RuntimeBinding` (ADR-B) are this type and its `Binding`: ADR-A's *no second Session state* holds, and ADR-B's
identity exists in code. `WorkUnit` keeps its kinds and reads participants as its executors. The page reads the work as data: the
sessions slice's allow-list gains **`work`**, an additive field beside today's `kind` (driven or chat, `SessionsStateSlice.cs:41`), judged
in .NET (D182 point 6), answered on loopback only, never on the sync wire, so a teammate's mirror reads as a teammate's session as today.
A page reading a host that sends no `work` falls back to today's predicates (`identity.ts:70-79`), so an older host draws as it does.

### 2.2 The work, read from the marks

One rule reads it, the first that holds: a mirror is another machine's, with no work here; `opinion` is `review`; `serves` with `piece`
is `partner`; `serves` alone is `help`; `ask` is `intake`; `Repository == "daoris:help"` is `ask-daoris`; `Kind = chat` is `chat`;
otherwise `build`. With no record: a brainstorm's scope is `brainstorm`, generator or synthesizer as its scope says
(`Core/Sessions/BrainstormBox.cs:13`); an appointment's holder is a holder, with no work and its role's acts; a lineage child is a child,
its parent's part and read-only. A record the service cannot read for a session Daoris names is *unplaced*, and fails closed (§2.3). A
meeting's turn is no participant until D177's turns start an agent (`DriverMeetings.cs:320-322`).

| Work, or placement | Its role in the default company (§2.7) | Its record | Distinct because |
|---|---|---|---|
| `build`, `chat`, `ask-daoris`, `intake`, `help`, `partner`, `review` | Build, Chat, Ask Daoris, Intake, Helper, Partner, Second opinion | the session record | they share one record, ledger, dispatcher and connector identity; what differs (admission, tools, time) is a table keyed by the work |
| `design` | none until a role takes it; Build's until then | the session record, read as `build` from its marks | a driven quest's session whose quest is marked design or decision work (MODELROLE1l); admitted, called and contained exactly as `build` in every table, so a reader that cannot tell the two apart answers the same |
| `brainstorm` | Brainstorm | its pass file | a pass is one turn with no line; a record for it would be a chat-shaped record every older build lists as the person's conversation (`compatible-builds.md`) |
| holder | Supervisor, Steward | the appointment | its authority and no connector (`HolderTerminal.cs:44`) |
| another machine's | none here | a mirror record | another machine's: nothing here starts or admits it, and its marks and actor do not travel (`SessionFeed.cs:22-26`) |
| child | its parent's | the lineage, never a record | its identity issued from lineage, never the environment, and read-only (C2a) |

A co-worker is a relation, `Serves`, never a work of its own (D203 point 1). The work and the role agree by a census on every record
carrying both: the role named by `actor` takes the work the marks say, by the catalogue as it stood at the start (§2.7); where they would
differ the marks decide the work, since the desks write them at the open and a record from before D205 has no actor, and the role says
only its staffing. *The driver's own session*, which the cap counts, becomes a property of the work (`build`, `design`, `intake`, `help`,
`partner`, `review`), the set `HoldsSlot` tells today (`Planner.cs:880-885`), judged equal on every case before it moves.

### 2.3 The policy and the admission

**One policy shape, read from today's keys.** A work's resolved policy carries what every work has (enabled and its consent mode, `on`,
`ask` or `off`; the agents it permits; its concurrency; its minutes) and a typed requirement of its own (an opinion's `required`, `verify`
and `recheck` and D155 point 4's maker passes; a partner's measured row, D199 point 4; the brainstorm's fan-out; the hand-over's consent).
It is read from the keys `driver.json` has now (`helpers`, `workspaceHelpers`, `handOver`, `opinions`, `workspaceOpinions`, `brainstorms`,
`workspaceBrainstorms`, Intake's and Ask Daoris's actors), so no key moves and an older build reads its keys as before (D205 point 6;
`twins.md` on `driver.json`'s writers). The rules still say who may and the role which staffing (D205 point 6): a policy is the work's, and
a configured role never carries one. Each work's default is written once in the reader's table: helpers `on` (D163's HELPERDEFAULT1 note),
hand-over `ask`, opinions none; a partner's is *not a rule yet* until COAGENT1d gives it a key, never `on`; the brainstorm's `mode` is
listed as a rule whose reader is unbuilt.

**Four layers, a table each, keyed by capability**, sharing one classification and answering closed refusal codes:

| Layer | The question | Where | Keyed by | Replaces | A caller it cannot place |
|---|---|---|---|---|---|
| Start | may this work start, for this asker, now | the driver library | the work | `HelperJudge.Judge`, `PartnerJudge.Refusal`, the intake's and Ask Daoris's gates, the counters (survey §1.3 rows 6, 10, 20) | refused, saying the rule |
| Call | may this caller call this tool | the connector | the work read from the record, a brainstorm's scope or the lineage | `RoleFilter` and the five per-tool predicates (rows 7, 15, 16), every registered tool classed from the registration, so a new tool is refused until classed (C2a's point 3) | for a session Daoris names: a brainstorm's scope placed first, through its box, as a generator or the synthesizer (`BrainstormBox.cs:13`, `:29`), each served its own result tool (`brainstorm_give`, `brainstorm_synthesize`; the box's own check, `:71-74`; `BrainstormPermissions.cs:22-28`); otherwise reads served, every mutation refused (C2a's point 1); a connector naming no session, a person's own agent, keeps today's scope |
| Act | may this holder make this act | the shell's pipe | the act, against the appointed role's authority (§2.7) | the two role doors (row 22, §2.5) | refused (D208's proof) |
| Contain | how this process is contained | the launcher | the work | unchanged (`AgentStarts.StartAsync`, D201): a piece fails closed; an ordinary start contained where the launcher can and started as today where it cannot (`Driver/Platform/AgentStarts.cs:13`; `Driver/Agents/Consoles/AgentJobs.cs:119`) | as today |

The harness's own rules handed at the spawn (`OpinionPermissions`, `IntakeRoom`, `HelpRoom`, `BrainstormPermissions`) are rendered from
the call layer's table and stay a second guard where the adapter takes settings; a test compares the two cell for cell. Each work's row
carries the mutations its work is for: a reviewer's `opinion_give` (`OpinionPermissions.cs:29`), a voice's result tool, a helper's
answer. *May this session ask a helper?*, asked in five places today, is asked of the start layer by the page and the terminal through the
driver (D182 point 6), and the service's desk keeps one check as its store's own guard, reading the same row.

**What the person sees change**, each a decision already taken: a partner is drawn as a partner (`identity.ts:79` draws any `serves` as a
helper); Ask Daoris is refused `helper_ask` (D203 point 6); a reviewer's and a brainstorm voice's calls beyond the tools their handed
rules allow are refused at the server on every door, as the handed rules refuse them only where the adapter takes settings
(`Driver.cs:3087`). Nothing else, and nothing for a person who sets nothing.

### 2.4 The choice: one start plan

Every start resolves its role, choice, agent and account through `StartPlan.Resolve(work, trigger, place, record?, pick?, fence?)`: the
company finds the role that takes the work in the place (§2.7); D205's `ActorChoices.Effective` gives that role's order; one walk choice
by choice for the entry (today four, survey §1.3 row 3), D197's account walk inside each choice, one set of pass codes (today two), one
re-ask of a refused model (today five). Its answer is a **settled selection**: role, choice, agent, asked model and effort, account,
harness identity, the passes made and its reservation. The reservation is taken at the selection under the roster's gate, as a chosen
start is counted today so a look's starts spread across accounts (`HarnessRoster.cs:894-897`); a probe that judges without starting asks
`counts: false` (`:911`); the launcher consumes the reservation once and a failure releases it once (X; O-18). No caller walks again after
it. A continuation takes its record's role and asked choice (D205 point 1), never the machine's `adapter`; a record whose role the person
has since removed continues on the role that takes its work now, said in its opening note as a later choice is (D205 point 5).

| Start | Today | After |
|---|---|---|
| A driven go-on and carry-on | `handedQuest?.Agent ?? config.Adapter` with the record's asked model (`Driver.cs:1917`, `:2019-2023`; §0.2) | the record's role and asked choice, or the handed agent's Build entry |
| The look's report of a start | names `handed ?? config.Adapter` before the walk (`Driver.cs:1107`) | the plan's answer |
| A terminal's start-from | the first agent only (`SessionsVerbs.cs:32-33`) | Chat's order, as the route's |
| A reviewer's same-agent press; the other `config.Adapter` fallbacks | `record.Adapter ?? Config.Adapter` (`OpinionGateLook.cs:393`; `ConversationRoutes.cs:51`, `:173`; `SessionGroups.cs:882`; `BriefedMove.cs:113`; `ChatGoOn.cs:526`) | the record's role through the plan; *as before* stays `Effective`'s own answer |
| A helper's and a partner's choice | through `ReviewerChoice` by an opinion's rule, so a helper is told *No second opinion* (`HelperChoice.cs:66-70`) | under their own policy |
| A brainstorm voice | no workspace (`DriverBrainstorms.cs:682-683`), so no pinned version | the request's workspace; a voice that cannot run dropped, never replaced (D205's round-2 note) |
| The live and the ended limit moves | a walk, then a second at the open (`LimitMove.cs:86`, `:142`; `:237`, `:256`) | the settled selection opens |
| A design or decision quest | Build's order (MODELROLE1l) | the role that takes `design` in its place, Build's while none does (§2.7) |
| A role holder; a meeting | outside, by D208's design; none | the census's two starts outside the plan, the holder's until its appointed role has staffing (SUPERVISOR1c, §2.7) |

### 2.5 Roles and actors

D205's **actor** and D208's **appointed role** are one configured record, the **role** (§2.7, the owner's direction): a position in the
company with a name, the work it takes, its staffing, its authority, its scope and how it is filled. An actor is a role always on, taking
work, with no authority; the supervisor and the steward are roles filled by appointment, holding acts and taking no work. A role is
**held by** a participant only where it is appointed; a role always on is **played** by every participant its starts open. Today the
holder is a participant placed as a holder, started by the window outside the pool; once SUPERVISOR1c lets a holder run on staffing
(D208 point 2), its appointed role's own staffing is what it runs on, through the start plan, and its authority is unchanged. Being
started on a role's staffing grants nothing: the authority is the appointment's, proven per act by the HMAC over the hello's nonce
(`SupervisorProof.cs:313-328`), the key never crossing the pipe. Its acts are its role's exact forms from the code's act table
(`StewardActRows.cs:152-162`; `RoleActs.cs:47-63`), recorded `by` the role and never as the owner's words (D208 point 1, D209 points 1 to
3); attribution narrows and never widens (D156 points 1 to 4). In the event vocabulary `role` is a cause of its own beside `person`,
`daoris` and `agent` (§4.2), so no reader takes a role's act for the person's press.

The two doors (survey §1.3 row 22: `SUPERVISOR_ANSWER`/`DEFER` with `SupervisorProof.Canonical` against `ROLE_ACT` with `RoleActForm.Bytes`)
become one act door with one canonical form and one whitespace rule, one reading of *a driven repository*, refusal sentences per role
(`InstancePipe.cs:1365-1381` says *the supervisor* for both), and one check that the act is in the appointed role's authority. The
supervisor's decisions in files and the steward's acts in memory stay as D208 and D209 chose. It changes an authority's door, so it is
built only after both makers read it (row t).

### 2.6 Native subagents

A harness's child is a participant placed as a child, bound to its part by C1a's lineage (`ChildBindings.Fold`), never a record. What is
read of it is what the fold holds (harness, native id, parent, depth, the signals that named it, its state) within COWORKVIEW1 row a's
generation; nothing of its conversation. What no harness said stays unknown (D173's canary 9), and a read has three outcomes: no file, read,
and unreadable with its kind, never *nobody* for the last (X; COWORKVIEW1 row a's rule 9). Its first production reader is COWORKVIEW1 row a.
At the connector a child is placed per call by its lineage and refused every mutation, an unreadable lineage read as a child (C2a's points 1
and 5); the call layer's child column is that class table. Until C2a is staged, a child calls with its parent's id and role
(`Core/Sessions/CallLineage.cs:11-14`; `RoleFilter.cs:28-30`), the one known gap of the participant's identity, named by the census.

### 2.7 The company: capabilities in code, roles in configuration

The owner's direction of 2026-10-25 (the brief §8): actors and roles are not hard-coded; each role can be configured and set up into the
company. **The code keeps a closed vocabulary of capabilities; a role is a configured record composed from them.**

**The capabilities**, one closed vocabulary in the driver library, `Capabilities`, each with its admission row in each layer of §2.3:

| Kind | Codes | What the code fixes for each |
|---|---|---|
| Work: the kinds of work a participant can take | `build`, `design`, `review`, `intake`, `help`, `partner`, `brainstorm`, `ask-daoris`, `chat` (the direction's *help* is a helper's, D163; Ask Daoris's front desk keeps its code's name) | the desk or door that opens it and the mark it writes (§2.2); the tools it may call and the mutations it is for (§2.3's call table); its containment and handed rules; whether it may be off (Intake, Ask Daoris, Design); its fallback (`design` to whatever takes `build`); its policy's typed requirement |
| Acts: what an appointed role may make for the owner | the supervisor's two (`answer`, `defer`, D208 point 4) and the steward's act table (`StewardActRows.cs:152-162`, D209 point 3) | each act's route, exact form, proof and record; and the acts that are never a role's, which no configuration names (D208 point 6; D209 point 3's list) |
| Doors | the call layer's tool classes; the pipe's route classes | which work or act may use each; a role may narrow a work's doors, never widen them |
| Containment | the launcher's kinds (`Agent`, `Piece`) and each work's handed rules | which each work runs under; a role cannot loosen it |

**A role** is a record in `driver.json`'s new `roles` list, judged by one judge, `Company.Judge`, for every door, as `ActorChoices.Judge` is:

| Field | Says | Bound |
|---|---|---|
| `id` | the role's wire id | the eight actor ids and `supervisor`, `steward` for the default company; an added role a new id |
| `name` | its name in English and in Chinese | authored content, kept as written (the translation contract) |
| `takes` | the work it takes | codes of the work vocabulary; at most one role takes one work in one scope |
| `choices` | its staffing: D205's one to four ordered `{ agent, model?, effort? }` | D205's judge; for the default company's eight, `actors[id]` stays the one home (D205's key, written through to the old keys) |
| `authority` | the acts and doors it may use | acts from the act vocabulary only for a role filled by appointment; doors only narrowing its work's |
| `scope` | where it works: the machine, a workspace, a repository | the most specific scope's role takes a work there; an appointment names a scope inside its role's |
| `filled` | `always`, started whenever its work arrives and its policy admits; or `appointed`, held by the holder the owner appoints (D208 point 1) | an appointed role's authority is live only while its appointment stands, revoked at once |

**The default company** is seeded: where `roles` names nothing, the catalogue reads a dated seed table in code that reproduces today
exactly, as `ActorChoices.Effective` reads *as before* (D205 point 7). The seed holds Build, Intake, Second opinion, Helper, Partner,
Brainstorm, Ask Daoris and Chat (each `always`, machine-wide, taking the work of its name, its staffing `actors[id]`), and the Supervisor
(`appointed`, a repository or a workspace, `answer` and `defer`) and the Steward (`appointed`, the machine, the steward's acts). No role
takes `design`, so design work falls back to Build. An upgrade writes nothing; a role's record is written at the person's first change
to it, and the seed's ids, `ActorChoices.Ids` and `ActorRun`'s list become one list both read (survey §1.3 row 9; O-17).

**Who changes it.** The person adds, changes or removes a role in Settings and with `daoris driver role`; Ask Daoris proposes with
`role_propose`, carrying the revision it read, judged by the same judge, and the person's press applies it in both modes, as D205 point 9's
`actor_propose` (a `Person` route waits for the window's card whoever calls). **A role gaining authority** (an act, or a door its work does
not have) needs the person's own press in the window, as an appointment does (D208 point 1). **An owner's act stays the owner's**: the
judge refuses a role naming one, and the act door refuses it whatever a role says (D209 point 3). **Appointing** stays the owner's press in
the window alone; no verb, Ask Daoris door, card or grant appoints (D208 point 1). Removing the role that takes a work leaves that work
with no role in its place: its starts hold, said in D205 point 4's sentence, until the person gives it one, and a running session is never
stopped by the change.

**What reads it.** The start plan finds a start's role by its work and place (§2.4); the admission tables read the work, never the role
(§2.3), so a configured role is admitted exactly as the default role taking the same work; the act door reads the appointed role's
authority (row t); the page's Settings read the catalogue, the AI screen's actor rows its roles that take work and This Daoris's *Roles*
its roles filled by appointment, as today in the default company; `actor` on a record names the role, its provenance.

**An older build** reads `actors` and the old keys as before and keeps `roles` on its own write, since every writer of `driver.json` keeps
the keys it does not own (`DriverConfigKeepsTests`); it starts design work on Build and never starts an added role. A record whose `actor`
names an added role, read by an older build, is the company row's first compatibility case (*unverified* how an older build's
continuation reads an actor id it does not know).

**MODELROLE1l** becomes a configured role, *Designer*, taking `design`, its staffing `opus` · high as D205's note of 2026-10-25 and
`docs/model-roles.md` have it, offered on D205 point 7's suggestion card and applied by the person's press. The code it needs is the
capability: `design` in the vocabulary, admitted as `build`, falling back to it, and taken by a quest its publisher or the intake marks as
design or decision work, as MODELROLE1i's `weight: exact` is taken (row x). No Design actor is compiled.

## 3. How work moves: the course and the move

### 3.1 One decision

`Course.Decide(CourseFacts facts, Trigger trigger) → Course`, a pure function in `Driver/Conversations/Domain/`, beside `Continuation.Judge`
(`Continuation.cs:207-239`), whose reasons it reads. The look calls it for a driven line and the chat runner for a conversation; each
executes the answer with the code it has today. Nothing merges the two loops.

**Inputs.** The line's head: its newest part's state, end code, `limited`, role and asked choice, adapter, profile, tree, kept conversation
and whether its door resumes, teammate, intake, stood down, and its waiting words with their ids and authors. The answers-alone hold:
its count across the line's preceding parts, read from this machine's event files, its bound and its release grounds
(`Driver/Conversations/GoOn/AnswersAloneHold.cs:31-32`, `:97-123`, which the chat applies before a resume, `ChatGoOn.cs:505`, and the
driven line, `Driver.cs:934-937`, the helper's words, `ChatHelperWords.cs:96`, and the supervisor's door, `Modules/Bridge/SupervisorDoor.cs:305`,
read too) (X-3.1); the executor reads the count into the facts and writes the hold's marks (`AnswersAloneHold.cs:44-45`), so the judgement
stays pure. The boundary the line stands at: a turn running, between
turns, the process exited, a press's reservation, winding up. The failure's kind, by D210's reader. The account facts, read once (§3.2).
The person's presses: a pick, *Go on in a new session*, a briefed press, a hand-over with its agent and account, a stop, a pause. The
rules: the hand-over's, the role's choices, the curve's decision (`SwitchDecision.Decide`). A move record standing for the line (§3.3).

**Triggers.** Words kept on a parked or ended record; a solicited answer; a limit's end; a limit's wake; a transient end past its bound; a
turn's end with nothing taking (the curve); a hand-over, pressed or asked; the briefed press; a conversation proven lost; the planner's
carry-ons (cut off, stop released, retried, limited, brought forward); a workflow's send-back to an ended conversation; *Go on in a new
session*; *Start a conversation with these words*; the come-up.

**Answers.**

| `Course` | Says | Executed by |
|---|---|---|
| `Resume` | the same part, its own conversation, its own account, the words that go and Daoris's word by its key (`LimitReset`, `TransientRetry`) | the go-on (`DriverContinuations.ContinueAsync`; `ChatGoOn`) |
| `Move` | a new part of the same line, on three axes: **when** (now, or at the turn's end), **where** (a role and choice, an account named or the walk's settled selection), **how** (`brief`, or `carry` once SWITCHCTX1 lands), with its `why` and `by` from one closed enum (`from_why`, `from_by`, `Driver.cs:4622-4633`) and the words it carries | the start (`Driver.RunAsync`; `ChatRunner.StartAsync`), with a move record |
| `Start` | a new line with no `from`, the person's words as its first, keyed by the press's own intent key, so a second press opens a second line and none supersedes the source (X-3.2) | `ChatRunner.StartAsync` without `from` (`ChatRunner.cs:339`), as today; no move record |
| `Wait` | until a moment, its reason code and the accounts it waits on | the hold (`Driver.cs:2041-2050`; `LimitWaits`) |
| `Hold` | for the person, its reason code and the doors out, a repair named where one is the way out (§3.2 step 4) | the hold's note |
| `Never` | a teammate's record, an intake, a stand-down, a record a later part supersedes, and the old part of a move that stands before `delivered`, whose words are the move's (O-2) | the say door's refusal (`SessionSay.cs:40-46`); the come-up's sweep passing the record |

Every answer carries its **preconditions** (X): the record's revision, the words' state (the ids waiting on `said`, since a word kept with
no note moves no revision, `Sessions.cs:1565-1574`; O-13), the quest's status, the account's cooling entry and the move record's phase it
read. The executor checks them before it acts and asks again where one moved, as the driven go-on reads the words again after the ledger's
move today (`DriverContinuations.cs:346-355`), so no decision runs on facts that changed under it. Each automatic hand-over reaches `Move`
only through the gate its door has today, else `Hold` for the person, each door a census case in rows h and i (O-14): an agent's ask once the
hand-over rule admits it (`DriverHandOvers.cs:208-214`); the curve's move once its scope's switch does (`CurveMove.cs:17-19`, `:131-133`), since
Daoris's own offer is not the hand-over rule's (`HandOverRules.cs:9-10`). The transient retry stays inside the run (D210 point 3), and the
course decides only what follows its bound. Two of the survey's fourteen paths are not decisions: the carry (M) is a move's *how*, and a
workflow's press (L) is a word the dispatcher owns (O).

| Path (survey §2.2) | The course's answer, as today's behaviour |
|---|---|
| A, B go-on, driven and conversation | `Resume`, else `Move`, `Wait` or `Hold` by §3.2; B's `CannotGoOn` where its own account cools and another is ready becomes the same `Move` (row a-i); B's other `CannotGoOn` cases stay `Hold` (§8) |
| C go on in a new session; start-from | go-on-new `Move` (`resume`), the person's words only; start-from `Start`, a fresh line with a preface, unchanged |
| D, E, F the limit move, its wake, the driven limit resume | `Resume` on the own account at its reset, else `Move` (`limit`) where an account it may take is ready, else `Wait`; F's step 0, a hand-over made from it, first |
| G the curve | `Move` (`curve`) on the decision's account, once its scope's switch admits it |
| H, I hand-over of a conversation and of a quest's session | `Move` (`press` or `agent`), the brief only; an agent's ask once the rule admits it; its **when** by who asked and the door, as today: an agent's ask at the turn's end; the person's press on a conversation at its turn's end with a closing turn asked, and on a quest's session now, as a stop (D163 point 1's *or the person stops it*; D188 point 1). Survey §2.4 row 12 is a difference of door, stated once in the course's table, not a disagreement to settle |
| J the briefed move | `Move` (`resume`), by the walk (§3.2) |
| K the driven carry-on | `Move` (`carry-on`, `retry`, `limit`, …), the record's role and choice (§2.4) |
| L a chat's workflow send-back | a word between turns; an ended chat's go-on as B |
| M SWITCHCTX1's carry | `Move` with `how: carry` |
| N past the transient bound | `Hold` until the person's word or *Try again* (`Planner.cs:1525-1532`) |

A census lists every site that opens a part `from` another or resumes with words (the planner's carry-ons, the go-ons, the limit move and
wake, the curve, the hand-overs, the briefed move and its not-offered path, go-on-new, start-from, the send-back) and fails on one that
neither calls the course nor is named *not yet moved*, as the brief §7.2 asks.

### 3.2 The account

The rule, stated once in `LineAccount.Decide` and read by every path through the course's account facts:

1. **A resume of the agent's own conversation asks for the record's own account** (D131, D137 point 8): ready or near, it resumes there.
2. **The own account cools and an account the line may take is ready: the line goes on now** in a new part there, briefed today and
   carried once SWITCHCTX1 lands (LIMITSWITCH1; D206 point 3 ii), for every line the owner's decision of 2026-10-25 reaches, *words to an
   ended record whose own account is cooling*:
   - **a driven record of an open or taken quest**, as LIMITSWITCH1 built it (`switches`, its `HarnessRoster.ResumeAsync`);
   - **a repository conversation**, through the ended limit move's body, its guard (`LimitMove.cs:213`) widened from a record a limit ended
     to a cooling own account, why `account`;
   - **a closed quest's session**, as a conversation in its tree, through the path a lost conversation takes (`Driver.cs:2374-2381`): the
     look decides, the shell's chat runner it is wired to (`DriverLoop.cs:926`) executes and writes the move record (§3.3), and a headless
     host with no runner waits for the person, as today. This amends D137 point 4 and its design §2.2 (*a closed quest … nothing carries on
     by itself*, `docs/2026-10-03-session-messages-design.md` §2.2) for this cooling case alone, as the owner's decision reaches it: the case
     the owner reported, a finished record's words held four days for its own account (D137's LIMITSWITCH1 note), was a closed quest's.
   The person's *Go on in a new session* still outranks it. **Ask Daoris** waits for its own account, as D137 §2.2 and D205's *one exception*
   have it (`HandOver.cs:52-57`; `CurveMove.cs:70-71`): at a limit it moves among its agent's accounts, and to another agent only with none of
   its agent's left (`LimitMove.cs:72`, `:85-88`; D205 §3.5(e)), unchanged, and going on elsewhere outside a limit is §8's first question.
   **A pick** keeps its own account until row a-ii records which picks named an account, as LIMITSWITCH2 was filed.
3. **The own account cannot carry for another reason** (limited, refused, signed out, gone, the asked model not offered, and for a driven
   record alone outside the set or kept for conversations, `ResumeAccount.cs:31-36`; `ResumeAccount.Judge`): a limited record goes where
   D206 point 3 sends it; a driven record walks at once, as D137 point 7 has it today; a conversation's refused, signed-out or gone account
   stays as today, `CannotGoOn` to the person since *nothing carries a chat's words on by itself* (`ChatGoOn.cs:566-569`, `:581`), and going on
   by itself there is §8's second question; a model not offered starts its role afresh (`ChatGoOn.cs:518-523`), unchanged.
4. **The accounts a line may take**, by its constraint: the asked choice's other accounts of its agent, then the role's later choices where a
   limit ended it or every account of its agent cools (D205 point 5, D206 point 6; the driven half is LIMITRESUME1e's); **an account the
   person picked and the record keeps** only that account (D205 §3.5(d), D206 point 5), the person's named account on a hand-over press
   included, recorded as `START_CHAT`'s profile is (O-9); **an agent pick** (`choice: pick` with no account kept), **a handed agent** and a role
   *as before* only that agent's accounts (D206 point 6). A named account on the other doors keeps today's reading: a conversation's
   hand-over, the agent's ask and the curve's included, opens its named account as a pick (`ChatHandOver.cs:156-157`; `CurveMove.cs:114-115`;
   *a pick is the one account, never moved*, `HarnessRoster.cs:887`), and a handed quest's named account comes first, then its agent's walk
   (`HarnessRoster.cs:923-934`); only the person's press is recorded as an account pick. A record from before the account pick is kept is an
   agent pick once row a-ii lands: Daoris invents no historical pin (X).
5. **None ready: it waits** until the earliest reset of exactly the accounts step 4 allows, one computation (`EarliestReset`), where an account
   signed out, refused, not offered or outside the set adds no reset (X); said in D206 §3.7's words on its record and its rail, woken by the
   look (D206 point 4). **Where none of them has a reset**, every one signed out, refused or not offered, it is `Hold`, naming the repair (a
   sign-in, an account added, the model offered), as `HarnessRoster.Unready` keeps a non-cooling refusal today (`HarnessRoster.cs:1736-1740`),
   never a manufactured reset or a promised resumption (X-3.3).
6. **A new part takes one settled selection** (§2.4), never the own account where its conversation could resume there (D206's rejection of a
   briefed carry-on on the same account), and the briefed move, a new part, takes the walk with its own account first where ready
   (`BriefedMove.cs:133` picks the cooling account today, the door that fails exactly when it is needed, survey §2.4 row 13).

It replaces (survey §2.4): row 1's flag at its three places and LIMITSWITCH1's `switches` parameter; row 2's seven answers, which become three
(own for a resume, the settled selection for a new part, a named account first); row 5's pick, by step 4; row 9's maker reading, asked by the
roster for every own account a limit cooled, the wake's included; row 11's two earliest resets; row 13's door.

### 3.3 The move record

Every `Move` writes one durable **move record**, before the old part ends where one runs and at `old-ended` for a move from an ended record,
which has no part to end (`LimitMove.cs:191-196`; O-20), and the record owns the move's delivery and recovery until the new part's first
turn answers. It is SWITCHCTX1's carry record (its §2.3) made the record of every move, and what LimitBox already is for one.

- **Where.** `<home>/sessions/<old record>.move.json`, keyed by the record left, as the limit box and the hand-over box are, in the session's file
  inventory (`SessionHomeFiles`) so a delete and a clear take it. If SWITCHCTX1a lands first, its `<s>.carry.json` becomes this file; there are
  never two.
- **What.** Its executor (`conversation` or `driven`) and its **claim**: the claiming process's generation; its phase, each with its moment:
  `decided` → `quiescing` (waiting for its natural boundary, the person's cut against an agent's ask kept apart) → `old-ended` → `opening`
  (its key written) → `opened` (the new record's id) → [`copied` → `verified`, the carry's] → `delivering` → `delivered`, or `fell-back`
  (its code), or `held` (its reason); `why`, `by`, `how`; the settled selection's account by profile name; the brief's recipe (the why and the
  event bound it is composed up to, `ContextBriefs.cs:59-95`); the delivery: each word's id and producer key, the move's transfer id, a
  limit's refused turns' bound, the channel messages held. Never a word's text, a path or a key.
- **Who writes and recovers it, fenced.** The executor of its kind, alone: a conversation's move by the shell's chat runner, a driven line's
  by the look of the loop that holds the home's lease, a closed quest's session going on as a conversation by the chat runner the look hands
  it to. Each phase is written by an atomic replace guarded by the revision the writer read and the claim's generation, so a writer holding
  an older claim changes nothing and says so (X-3.5); a process's in-memory exclusion (`GoingOn`, `Chat/ChatContext.cs:64`) is no claim.
  At its come-up the executor of a kind takes up every record of its kind before anything else that could act on the same records: a
  conversation's where `LimitMove.TakeUpBoxesAsync` runs today (`ChatGoOn.cs:195`), before the come-up's go-ons (`:213-257`), the hand-over
  boxes' take-up (`:260`) and the sweep (`:136-168`), none of which reads a box today, and before the lease; a driven line's at the slot where
  uncertain words are settled (`DriverWatch.cs:739`). Taking one up writes a newer claim. The shell's guard for words said meanwhile
  (`TakeWaits`, `SessionWords.cs:544-545`), which reads only the hand-over box, reads the move record. Each phase is
  safe to run again, and an unknown effect outside Daoris (a spawn or a prompt whose answer was lost) is `held` and never repeated by itself (X).
- **The destination, never opened twice.** The ledger's opens take an optional `opens` key, kept on the record as a field of this machine's
  and never on the wire (`session-record-fields.md`), unique in the store; a repeat open with the same key answers the record it opened, in the
  open's own statement (X). Against an older host, which answers a new record each time, an open whose answer was lost is `held` for the
  person with the candidates named, never searched for by `from` and moment, since successors are not unique by `from` (`Sessions.cs:998`;
  X-3.5), so the guarantee *never twice* is a newer host's alone.
- **Today's boxes become its adapters, one recovery authority at each phase.** `<s>.limit-move.json` and the conversation's hand-over box are
  written beside it while a build in the update's rollback window could read them (D139), and retired in the records-last row. The old limit
  recovery reconstructs and queues delivery (`LimitMove.cs:352`) and the hand-over's consumes and clears its box (`HandOver.cs:292`), so the two
  are never both executable: for each phase a table in row f says what the box holds so that an older build's recovery from it is safe (the
  words it may deliver, the receipts it must see) or that the box is not written in that phase, and at its come-up a newer build reads the
  evidence an older one may have left (a box cleared, a record opened `from` the old one, a word taken) and brings the move record to it before
  it acts (X-5.2). While a limit box stands, its words are delivered under the box's own key (`limit-move/<session>`, `LimitMove.cs:352-379`), so an
  older build's recovery and a newer one's dedupe against each other, and an older build re-taking a hand-over box is already refused once the
  successor exists (`HandOver.cs:143`, `:177-179`). Three records are not moves and stay what they are (O): the hand-over's **ask** (`HandOverAskBox.cs:28`), the agent's request
  through the connector, the service's, the move beginning at its approval; `data/handed/<quest>.json`, the agent a quest is handed to until it
  leaves the machine (`Driver.cs:1914-1917`), a constraint the course reads; and `<s>.new-session.json`, the person's press. A same-conversation
  resume writes none: no word changes record.

Today one path of five keeps a move durable, the ended limit move's box (survey §2.5); the record lands first on the live limit move, the
curve and the driven carry-on, which keep none.

### 3.4 Words across a move

The dispatcher's guarantee (`.claude/knowledge/session-words.md`) holds by construction once six rules hold, each in one place:

1. **A word keeps its whole identity everywhere it is held**: its producer's key, level, files and display choice, through the shell's held words,
   their file and the record path, and the headless fallback (§0.2; row b). A display choice never becomes `SaidWord.Choice`.
2. **Words follow the line's head, judged inside the say door's exclusive step.** `SessionLedger.SayAsync` already reads, judges and writes as one
   step (`SessionLedger.cs:931-973`); it gains the head inside it, by **one service function**, `Lines.HeadOf`, which follows the `from` chain
   across kinds, a driven part's successor conversation included (a closed quest's lost session already goes on so today, `Driver.cs:2374-2381`),
   and for a driven part with no successor its quest's last run here; the partner desk's walk (`PartnerDesk.ContinuedByAsync`,
   `Core/Sessions/PartnerDesk.cs:114-120`) and the helper desk's (`HelperDesk.cs:140-145`) read the same function (O-6; X-3.4). The
   service has no such refusal today (`SessionSayRefusal`, `SessionLedger.cs:89-175`): a new value, `Continued`, carries the head, read by
   `ServiceClient` and the headless door (`SessionSay.cs:207-209`), each say caller's handling named in row d's census from the survey §2.5's
   list, an older driver reading it as the refusal it already reads for an unknown value. No part can open between the judgement and the keep.
   The driver's `WordsNever.ContinuedIn`, which follows chats alone (`SessionSay.cs:54-58`), stays the older host's fallback (row d).
3. **A move delivers after the open.** The new record opens in one write, so the head moves at once. The move reads the old part's waiting words
   after that write, never before, and moves each by one service step that keeps it on the new part under **its producer key**, with the move's
   transfer id beside it, takes it off the old, and writes its receipt, in one transaction, so a producer's retry at the new head finds its word
   and a retried delivery finds its transfer (X-3.6). Moved words keep their order ahead of any word kept on the new part after its open, since
   the new part's first turn is the move's delivery (O-19). LIMITWORD1's second read becomes the rule of every move: the ended and live limit
   moves, the driven carry-on, and the briefed move and its not-offered path, which read their words before their open today (`BriefedMove.cs:99`,
   `:158`, `:179`, `:250`; O-7). Row d's census takes every site that hands words to a part it opens, `from` or not: start-from reads its words
   before its open too (`ChatRunner.StartFrom.cs:101-131`), and a refused open moves its words (`ChatRunner.cs:1961-1982`) (row d; the move
   record's delivery list, row f).
4. **Every word is kept on `said` before anything holds it, a winding-up or moving record included.** The say door keeps a word for an active
   record whose dispatcher closed as waiting for its next part, asked only by the shell and the driver as `running` is (refused today,
   `SessionLedger.cs:945-952`); the shell's holds become keeps; `held-words.json` is read once at a come-up for an older shell's words and never
   written again; a chat dispatcher's `Hold()` during a move (`ChatTurns.cs:468-476`, under which a word is neither kept nor taken, `:192`) keeps
   and queues, as a limit's wait and a press already do (`:499-510`, `:570-599`). Such a word is the move's while the move stands (§3.1's
   `Never`), and it is kept in a shape an older shell's come-up cannot read as an ended chat's waiting word, since an older sweep would resume the
   old part in the tree its successor holds (`ChatGoOn.cs:191-262`; `SessionWords.cs:455-458`; O-2), proven by an older shell's own sweep over it;
   where no such shape exists the row waits for the rollback window, as row v does. It lands after rows h and i (row o).
5. **A word is attempted once, its disposition kept, and an attempt acknowledged before the wire.** The mark that fails open today (§0.2) becomes
   mandatory, and the attempt's answer names each word it marked, so a word it does not name is not attempted and does not go: today the service
   answers an `unmarked` list (`SessionLedger.cs:1234-1246`) that the client ignores, answering success (`Driver/Service/ServiceClient.cs:569`)
   (X-3.7). Each attempted word keeps one disposition: **taken** (the turn took it: never sent again); **refused at a limit** (the door refused the
   turn as a limit before the agent took it: the definite refusal carries it once to the part that goes on, as a limit's refused turns' words are
   carried today, `Chat/ChatTurns.cs:1225`; `LimitWaits.cs:223`); **uncertain** (attempted, no conclusive answer: never sent again by itself, said with
   *Say it again*); a transient try followed by a limit's refusal is the limit's. The service keeps a receipt of each producer key after its word
   leaves `said`, machine-local, for seven days and at most 10,000 a home, trimmed by its own statement, so a retry after the take is answered as
   kept within that horizon; the come-up sends only words proven never attempted (X; row p). Across a harness write that carries no receipt, that is
   the guarantee there is: no automatic second attempt, the evidence kept, the person's press.
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
| Minted | **by trigger, in the statement that writes the record**, into `event_journal` in the service's database, so every writer mints it (the host, the driver through it, each connector, and a connector built before the table), as the revisions are minted (`Sessions.cs:652-681`). A trigger journals the **raw transition** only (the record, the column, its old and new value, and for `said` each word's id added or removed), guarded by what changed (`WHEN OLD.state IS NOT NEW.state`, and per column), since `AFTER UPDATE OF` fires on a statement's list and `SetStateAsync` names `state`, `limited` and `said` on every move (`Sessions.cs:899-902`); an attempt mark, which rewrites `said` at its length (`:1626-1632`), journals nothing; a mirror's upsert (`:931-936`) journals nothing, since another machine's records are not this machine's facts; a trigger is written never to raise, so an older writer's statement cannot fail through it (O-4; X-4.1). The code, the end's class, the work and a park's `for` are judged in .NET at projection, never in SQL (D182 point 6) | **by the hub of the process that runs the work**, at the judgement that owns the fact (O): the lease's loop for driven work, the shell for its conversations, the executor for a move's phases. The lease's loop alone tells plugins and outside readers (EVENTHOOK1 §4.1) |
| Id and order | `position`, an integer in commit order under the store's write lock, never reused after a trim, its epoch the store's identity | `run` and `seq` (EVENTHOOK1 §4.3) |
| Wake | the journal's insert moves a family of its own in the change stream, `journal`, so a reader is woken by every append, a word taken included, which today moves neither the session's revision nor the words family (`Sessions.cs:1605`, `:677`; X-4.1), and by nothing the guards leave out | the hub's teller |
| Scope | the record's workspace, filtered as data (NEEDSSCOPE1) | this home |
| Kept | durable, machine-local, never on the sync wire; at most 50,000 rows, the insert's own statement deleting the oldest past it, so an older build that never trims still bounds it; the **floor**, the lowest position kept, answered with every read (X-4.2) | memory, one run, bounded per reader with `missed` |

**Each process reads the journal from its come-up** (O-3): the shell for its conversations' record events whether or not it holds the lease,
as `ServiceConfirmations.RunAsync` runs before it (`DriverLoop.cs:787`), and the lease's loop for driven work and the outside readers. Today
a conversation's end reaches the shell in process from the routes' `onEnded` (`ConversationRoutes.cs:55`; `DriverLoop.cs:1233`) whoever holds
the lease, and a shell waiting on the lease builds no watch and hears no change stream (`DriverLoop.cs:841`, `:895`), so a projection moved onto
the journal without this would lose or delay a conversation's events while a headless loop drives.

**Three orders, never one** (X): the journal's `position` orders what a store committed; `run`/`seq` orders what a hub told; a slice's
`epoch`/`seq` orders a projection's snapshots and deltas (D182 point 3). A timestamp orders nothing across processes. The change stream's families
stay the wake (`ChangeStream.cs:76-80`): the journal is what a woken reader reads, so it learns *what* moved, in order, rather than *that* a family
moved. Records stay the truth: the journal is never replayed into state, so this is no event-sourced database (D182's rejection; D182 point 7's
*no new bus*: the journal is a table and the hub a teller on the bus there is). **What a trim loses** is said: a reader whose cursor is below the
floor reads its records whole, and its history between the cursor and the floor is incomplete, since a record's state and notes are overwritten
(`Sessions.cs:899`) and a park answered between cannot be rebuilt from them; the reader says *incomplete since* its cursor, never a complete
history (X-4.2).

### 4.2 The vocabulary

One registry, `EventCodes`, beside `NoteCodes`, extending EVENTHOOK1's `WorkEventGrammar` (O): each code with its closed facts, its privacy class
(this machine, or outside) and its rendering family (X), each judged in .NET from the journal's raw transitions and the hubs' facts. A code without
words in both catalogues fails its census, so *ConfirmingRestored*'s English toast (survey §3.3) cannot recur. Codes are subject-specific where the
facts differ (X): a part ends, a line of work goes on, work lands.

| Kind of change | Code | Facts |
|---|---|---|
| a part opened | `part.opened` | work, entry, `from`, `why`, `by` |
| a park, and its answer | `part.parked`, `part.answered` | `for` (the person-waits kinds, `person-waits.md`); `by` |
| an end | `part.ended` | state; `reason`, the note's `ended.*` code; `class`, one closed code from the record's state, its `limited` flag and its note's code (`completed`, `failed`, `limited`, `transient-bound`, `access`, `stopped-person`, `stopped-role`, `stopped-daoris`, `refused-choice`, `awaits-helper`, `stood-down`, `unknown`), judged in .NET; where the look concluded a driven end, its `SessionEnded` flags (`Attention.cs:250-252`) refine the class in the look's process, never in the trigger |
| a limit | `part.limited`, `account.waits` | the reset as a moment; the account never off this machine |
| a move | `move.decided`, `move.settled`, `move.held` | the move's old and new parts, `how`, `why`, reason |
| a word | `words.kept`, `words.taken`, `delivery.uncertain` | the word's id, `by`, level |
| *answered*, four facts apart | `turn.answered`, `channel.replied`, `question.answered`, `part.answered` | each its own correlation (survey §3.3) |
| a landing | `work.landed`; `work.integrated` once FABRIC-END proves the line holds it | its code |
| a wait on the person | `person.waiting`, `person.answered` | its kind, a confirmation's id; only for a wait no record holds, a park being `part.parked` alone |
| a quest's operation | `quest.<op>` | the quest log's operation names, mapped at projection from the channel's own line codes, which keep their names: they are read from the quest per read, never stored (`Core/Asks/Channel.cs:106-116`, `:1121-1133`), and reach the page, the connector's `channel_read` and older builds (O-20) |
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
| Notifications | the news projection: `data/notices.json` its one durable record, owned by `Notices`, to which every process that tells news (the lease's loop for driven work, the shell for its conversations) submits facts under the file's keeping (`Driver/Platform/Notices.cs:12-17`, its `KeepLock`), each teller with **its own journal cursor**, and each piece of news keyed by its subject's durable id (a park by its record and its park's moment, a Confirm by its confirmation's id), never by a position (O-12) |
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
| A teammate's mirror | the records that travel, their travelling fields (`session-record-fields.md`) | the journal, move records, the work, the role's fields, `said`, receipts, the lineage, notices, the log, the role catalogue |

Never leaves the machine: the journal, every machine event but a relayed frame, move records, receipts, notices, the event file, the lineage, the
log, `said`, a participant's binding, every account's name, every path, the role catalogue.

### 4.5 Durability and loss

| What | Kept | A restart | How a reader knows it missed something |
|---|---|---|---|
| Records, the journal | durable | nothing lost that the journal still holds | a cursor below the journal's floor, or another store identity: the reader reads its records whole and says its history is incomplete since its cursor (X) |
| News | its said-set durable in `data/notices.json`, kept apart from the visible notices so a prune or a dismissal never forgets what was told, bounded by its own horizon, one journal cursor per teller beside it | resumes from its cursors | the floor |
| Machine events | memory, one run | a new `run`, `run.started` | a new `run`, a `seq` gap, `missed` (EVENTHOOK1 §4.4) |
| Slices | memory, rebuilt from the truth | a new epoch | the epoch and the manifest (D182 point 3) |
| Move records | durable | taken up by their executor, fenced | — |
| Held answers, `UNDER_WAY`, the confirmations' lists, the hook queue, held calls | memory | rebuilt or empty; a held call ends with its process, which holds it in memory alone (`Modules/Loop/HeldCallAnnouncements.cs:22`) | the read asked again; pipe-waiting keeps its own record |

**One rule for a first read** (survey §3.4 row 6): the baseline gives way to the said-set. A piece of news is told **at most once**: its key is
claimed in the said-set, durably, before the balloon or the toast is shown, so a crash between the claim and the showing loses that one
announcement and never repeats it, while the wait it told of stays where the person reads waits (*What needs you*, judged from records); one
claimed before is never told again, so reopening the app replays no balloon (X; X-4.3). A park made just before a restart, never claimed, is told
once after it (today dropped by the first look's baseline, `Attention.cs:224`; `SessionLog.cs:38-41`). A held call is told once while its owning
process lives; a restart of that process ends the hold and tells nothing for it (X-1.2). Only a home with no said-set yet, the first run of the
build that adds it, takes what stands as said. A claim the file refuses (a refused write is dropped, `Notices.cs:17-19`) shows nothing, which
keeps it at most once. The cursor's catch-up is bounded by the notices' own week and count (a week, at most 200, `Notices.cs:32-35`). The journal's
retention is not EVENTHOOK1's delivery: its relay keeps no replay and says its losses (`docs/2026-10-24-event-hook-design.md` §4.4), and moving
its session kinds onto the journal lets a restart of the lease's loop re-read what the journal still holds, never replay what a plugin missed
(X-4.4).

## 5. The designs in flight, and the records

### 5.1 What each in-flight design becomes, and which of its rows wait

| Design | Becomes | Its rows |
|---|---|---|
| **LIMITSWITCH1** (D137's note of 2026-10-25), built for driven records of an open or taken quest on its session branch | the account rule's driven half, as built | lands as built; row a-i waits for it on main and reads its `Switched` through `LineAccount` |
| **LIMITSWITCH2**, its remainder | row a-i, re-cut: through the ended limit move's body, not `BriefedMove` (§0.2); a pick and Ask Daoris's room keep the own account, as filed; a closed quest's session joins it by the steward's reading of the owner's decision (§3.2 step 2) | moves into row a-i once D212 is accepted; until then it stays filed, not built |
| **MODELROLE1l** (D205's note of 2026-10-25), filed | a configured role, *Designer*, taking `design` (§2.7) | its code is the `design` capability and the quest's mark, row x; its role a seeded suggestion applied by the person's press, row y-i |
| **EVENTHOOK1** (proposed, round 4, Codex's read owed) | the first outside projection and the first instance of the vocabulary: built first as designed, then moved onto the journal (row n): its session kinds read the retained journal, its `WorkEventGrammar` becomes `EventCodes`' outside projection, its kinds keeping their names there, its `work.landed` and `quest.parked` machine events under its own `run` and `seq`, its relay's delivery contract unchanged | none waits on the foundation; 1d's quest kinds, a later round, are designed from the journal's `quest.<op>` rather than the look's snapshot; row n waits for 1b and row k |
| **COWORKVIEW1** (proposed, round 4, Codex's read owed) | row a: the lineage's first production reader, the participant's child half, built as designed; the routing half (rows e, b, c, d, f): re-cut to read `Participants.Of` (row c) for who is at work, the line's head (row d) for the current executor in place of its own selector over `LastRun` (its §3.1), the move records (row f) for what waits on what, and the event codes (rows k to m) in place of reading waits from end codes | row a waits for nothing here; its routing half's round is written after rows c, d and f land |
| **SWITCHCTX1's carry** (proposed, round 5, Codex's read owed); its line view 1c-i accepted and built | re-cut in storage, kept in protocol: its carry record is the move record's `copied` and `verified` phases and `how: carry`, so a carried move and every other kind have one owner of delivery; its compatibility table, replay check, model judgement and fallbacks stay as designed | 1c-i stands; 1a, 1b, 1c-ii, 1d, 1e and 1f wait for row f's record as well as their own owed read; if 1a is accepted and built first, its file is the one row f extends, never a second |
| **AGENTERRORS1** (D210; 1a, 1a2, 1b built) | the course's failure input | 1b2 and 1c to 1g built as designed, in their own order; row g adds only what they do not: a conversation's and a brainstorm's limit by code, and a conversation's limited flag |
| **MODELROLE1** (D205; rows a to e built) | its `Effective` and walk become the start plan's; its eight actors the default company's always-on roles | rows j and w |
| **LIMITRESUME1** (D206), open rows 1e and 1f | read the account rule's constraints | built as designed; 1e's walk across choices is step 4's driven half |

### 5.2 Records and owners

Every record of the survey's §4 keeps its owner unless this table says otherwise; *older* says what a build from before reads.

| Record | Owner after | Travels | An older build |
|---|---|---|---|
| `sessions` with `said`, the marks, `from`, `entry`, `limited`, the actor's fields | the service, unchanged; the work read, never stored; `actor` naming the role | as today; the work, `actor` and `from` never | reads it as today |
| `opens` and `accountPicked` on the record (new, rows f and a-ii) | the service, written at the open, each through `session-record-fields.md`'s chain (the record, its column, the create, the reader, the ledger's opens, the host's request, route and loopback answer, the driver's write and read, the stand-ins, the web type) | no field on the wire | never reads them; an `accountPicked` it cannot see makes it treat the pick as today's `pick` |
| `event_journal` (new, row k) | the service, by trigger | never | never reads it; its own writes mint rows through the triggers, which bound it |
| Producer receipts (new, row p) | the service | never | never reads them |
| `<s>.move.json` (new, row f) | the driver library, written by the executor of its kind under its claim | no | never reads it; the boxes written beside it keep its moves recoverable for an older build until row v, one recovery authority at each phase |
| `<s>.limit-move.json`, the conversation's hand-over box | the driver library, written from the move record as adapters, then retired (row v) | no | reads them until retired |
| `data/handed/<quest>.json`, `<s>.new-session.json`, the hand-over's ask | unchanged: a constraint, a press and a request, read by the course | no | unchanged |
| `held-words.json` | read once at a come-up for an older shell's words, never written again (row o); retired in row v | no | an older shell writes it; a newer one reads it once |
| `data/notices.json` | one owner, `Notices`, every teller submitting facts to it with its own cursor (row m) | no | unchanged shape; an older headless host still writes it, read either way |
| `driver.json`'s `roles` (new, row w) | the driver library, one judge for every door (§2.7); the default company a seed in code until the person's first change | no | keeps the key on its own write, reads `actors` and the old keys as before |
| `store_identity` | the schema's initialisation, not the change stream's observer (X; row k) | no | unchanged |
| A reviewer's `serves` (D203 point 6, row v) | the service, at the desk's open | no field on the wire | an older page reads a reviewer with `serves` as a helper, so it lands only where every reader reads `opinion` first (D203 point 6's own rule) |
| Everything else in survey §4 | unchanged | unchanged | unchanged |

## 6. The names

None of these is a new word on the page except a role's, which the person sees and sets. They name code, records and prose.

| Concept | en | zh | On the page | Code | Retires |
|---|---|---|---|---|---|
| Fabric | *Fabric*, the three concepts together, in prose (D173) | none | never | none | — |
| participant | *participant* in prose | none until it reaches the page | never | `Participant`, `Participants.Of`, `RuntimeBinding` | — |
| capability | *capability* in prose; a participant's is its *work* | none: each is shown by its role's name | never as a word; a role's work shown by the default roles' names (开发, 第二意见, 助手, 搭档, …) and 设计 for `design` | `Capabilities`, `ParticipantWork`, `Participants.WorkOf` | the connector's `SessionRole` (`Mcp/RoleFilter.cs:13-19`), which is neither a role nor a work (X), when its readers move |
| role | *role* (the owner's direction): a configured position in the company; D205's *actor* and D208's appointed role are both roles | 角色 | Settings → This Daoris → *Roles* and Settings → AI features → *Actors*, both reading the catalogue; the two labels stay as they are until row y-i designs the one place a role is set, under the UI and translation contracts | `Company`, `Role`, `ActorChoices` (a role's staffing) | — |
| actor | *actor* (D205): a role as its staffing reads | 角色 | unchanged | `ActorChoices` | the word in prose, where *role* says it |
| line of work | *session* on the page; *line of work* in prose wherever *line* could mean the branch | 会话 | unchanged | `Entry` (D167's key), never `Line`, since `LinesStateSlice` is the branch's | — |
| line (the branch) | *line* | 主线 | unchanged | unchanged | — |
| part | *part* | 段 | unchanged | unchanged | — |
| move | *move* in prose | none | never: each kind keeps its words (转交, 继续, 额度用尽) | `Course`, `Move`, `Moves`, `LineAccount` | the boxes' types, as adapters |
| event | *event* in prose (the brief's, EVENTHOOK1's) | none | never: the page shows notices | `DaorisEvent`, `EventCodes`, `event_journal` | `WorkEventGrammar`, as `EventCodes`' outside projection |

*Role* enters the glossary with its reasons (D116): 角色, a position in the company the owner sets up, of which an actor is one always on and the
supervisor and the steward ones filled by appointment (任命), so the one word on both screens names one concept. The glossary's *actor* line *A
co-worker's role names its actor* reads *A co-worker's work names the role it runs as*. Code names that change: `ResumeChoice.Waits` read through
`LineAccount` (row a-i); `HoldsSlot` over the work (row c); `SessionRole` (row r); `LimitBox` read as a move (row f), its file moved in row v;
`AttentionEvent` a projection of `DaorisEvent` (row m). Only in prose: Fabric, participant, capability, line of work, move, event.

## 7. The disagreements this settles, each a dated note

By the brief §7.3 each unification states which answer stands, as a dated note on the decision it amends, in the row that makes it.

| # | Question | Today | The answer that stands | Note on, in row |
|---|---|---|---|---|
| 1 | the own account cools: wait or move (survey §2.4 row 1) | a driven open or taken quest moves (LIMITSWITCH1); a conversation, Ask Daoris and a closed quest's session wait | go on now where an account the line may take is ready, for a driven record, a repository conversation and a closed quest's session; Ask Daoris waits for its own (§8's first question) | D137 point 7, a conformance note to its 2026-10-25 note; row a-i |
| 1b | a closed quest's session whose own account cools (D137 point 4) | its words wait with *Start a conversation* (`Driver.cs:2367-2373`) | goes on as a conversation in its tree on a ready account, through the chat runner, a headless host waiting | D137 point 4 and its design §2.2, for that cooling case alone, as the owner's decision reaches it; row a-i |
| 2 | a pick (§2.4 row 5) | `choice: pick` for both an account pick and an agent pick; the live move moves both among the agent's accounts | a recorded account pick waits for its own account, a hand-over press's named account included; an agent pick walks its agent's accounts; a record with no provenance is an agent pick | D205 §3.5(d) and D206 point 5, a clarifying note; row a-ii |
| 3 | the earliest reset (§2.4 row 11) | two computations, each one agent's | one, over exactly the accounts the line may take, ineligible ones adding none; none with a reset, a hold naming the repair | D206 point 2; row a-i |
| 4 | the briefed move's account (§2.4 row 13) | its own account as a pick | the walk, its own account first where ready | D189 points 2 and 3; row a-iii |
| 5 | a carry-on's agent (§1.3 row 12) | the machine's first agent with the record's asked model | the record's role and asked choice | D205 point 1, a conformance note; row j |
| 6 | the kind: stored or read (§1.3 row 8) | both, by 27 lines and more | the work read from the marks by one rule; `actor` D205's provenance, naming the role | D203 point 1 and D205 point 10, a note that each stands; D203 point 10's terminal field named `work`; row c |
| 7 | words during a move (§2.4 row 3) | box, hold or none | kept on `said`, judged at the head inside the say door, delivered after the open | D188 point 6; D167 point 1; rows d and o |
| 8 | an attempt mark that fails (§2.3) | the word still goes | the word waits, said; each attempted word one disposition; receipts after the take | D188 point 7; D137; row p |
| 9 | Ask Daoris asks a helper (§1.3 row 6) | allowed by the helper's copies, refused by the partner's | refused | D203 point 6, conformance; row q |
| 10 | a caller the connector cannot place (§1.3 row 16) | passes | for a session Daoris names: a brainstorm's scope placed first, else reads served and every mutation refused | D173's FABRIC-C2a note; D199 for the role filter; row r |
| 11 | a reviewer's and a voice's calls at the server (§1.3 rows 7, 15) | pass | refused beyond what their handed rules allow | D155, D176; row r |
| 12 | a park across a restart (§3.4 rows 2, 6) | a park dropped by the baseline | told at most once, by the durable said-set | D196 and the watch's SURF5b note; row m |
| 13 | the usage slice and a driven end (survey §3.3) | reached only at its period | woken by `part.ended` | D182's PUSH1e note; row l |
| 14 | the chain's ranks 3 and 4 (`Chain.cs:170-172`) | declared, bound nowhere | removed unless a row reads them | D173's FABRIC-CHAIN1 note; row s |
| 15 | the notices' writers (§3.4 row 10) | the slice, the look and the headless host, each editing the file | one owner, `Notices`, to which each process that tells news submits its facts under the file's keeping, with its own cursor | D196; row m |
| 16 | who staffs a kind of work (the owner's direction) | eight actors and two appointed roles in code | configured roles over a closed vocabulary of capabilities, the default company seeded | D205 points 1, 8 and 9, D208 point 1 and D209 points 1 and 3, a note on each that the role is configured and its bounds stand; rows w to y |

## 8. What is the owner's

Nothing built from round 2 needs D191 point 3's answer. It changes no direction: the owner's FOUNDATION1 direction asks for exactly this
structure, and the owner's direction on roles is taken as given (§2.7). It spends nothing beyond development's own runs and what the owner
already decided: row a-i moves a driven record's, a repository conversation's and a closed quest's session's words to another ready account
where the own account cools, which LIMITSWITCH1's decision covers in its own words (*words to an ended record whose own account is cooling no
longer wait … while another account of the line's choices is ready*), the closed quest's case being the one the owner reported; and an account
the person named stays pinned (§3.2), so no account is spent that the owner did not choose. It needs no sign-in, publishes, pushes and rewrites
nothing, and loses none of the owner's data: the journal is a new history the records do not hold, bounded by its own trim, whose loss is said
(§4.1); an old box or `held-words.json` is retired only once no build reads it and every word in it is on `said`, which row v proves. Enabling a
plugin that tells outside programs, appointing a role and changing a role stay the owner's existing doors; no row turns one on.

**Two open questions for the owner**, cut from row a by round 2 (O-1), each a decision of its own; until each is answered the code keeps today's
answer and no row builds it:

1. **Ask Daoris whose own account cools**, outside a limit: today its words wait for that account (D137 §2.2); it goes on elsewhere only at a
   limit, among its agent's accounts and to another agent only with none of its agent's left (D205 §3.5(e), *the one exception*,
   `HandOver.cs:52-57`; `LimitMove.cs:72`, `:85-88`). Should its words go on now in a new part of its room on another ready account, as a
   repository conversation's now do? Yes spends another of the owner's accounts on Ask Daoris's conversation and opens a new part in the room's
   history; no keeps today's wait, said in D206 §3.7's words. Read D205 §3.5(e) and this design's §3.2 step 2.
2. **A conversation whose own account is signed out, refused or gone** (*kept* and *outside the set* are a driven record's holds alone,
   `ResumeAccount.cs:31-36`): today it is `CannotGoOn`, the person offered *Start a conversation with these words*, since nothing carries a
   chat's words on by itself (`ChatGoOn.cs:566-569`, `:581`; D137 point 4). Should it go on by itself in a briefed part on another ready
   account, as a driven record does (D137 point 7)? Yes spends another account without the person's press and moves a conversation the person
   may mean to repair; no keeps today's door. Read D137 points 4 and 7 and this design's §3.2 step 3.

Near the line, and named so the steward can check them: rows q, r, t, v and y-ii grant or refuse authority (who may start, call and act) or delete
records, so each is built only after both makers have read it (D191's note of 2026-10-24); and the whole of D212 waits for both reads of round 2.

## 9. Rejected

| Rejected | Why |
|---|---|
| A `participants` table, or a record per brainstorm voice or child | two authoritative stores for one concept (D173's first guard); the C0 audit rejected a record per child (`D173.md:91`) |
| A stored kind, or the kind read from `actor` | §1.1, Q1 |
| Hard-coded actors and roles; a role that widens its work's doors or holds an owner's act | §1.1, Q4; the owner's direction |
| One admission judge across the connector, the driver, the pipe and the launcher | §1.1, Q2 |
| One loop for driven lines and conversations | §1.1, Q6 |
| The service owning the move record's persistence | §1.1, Q8: one executor per kind already recovers its boxes from files, and the two transactional needs are met by the service's atomic open and a fenced claim; where neither can be had, the move is held |
| A second carry record beside the move record | two owners of one delivery, the gap LIMITWORD1 came from |
| The dispatcher owning a move | a dispatcher is per record and a move spans two |
| Minting record events in the driver's client, by diffing snapshots, or coding them in SQL | §1.1, Q11 |
| An event-sourced database, or replaying the journal into state | D182's rejection: records stay the truth |
| One bus for state and news, or events replacing pushed slices | state is pushed with its data (D182 point 2); news is told once; two jobs |
| Retiring `STATE_CHANGED` now | plugins and every pull not yet pushed read it (D182 point 2) |
| Words or names in an event a plugin or outside agent reads | EVENTHOOK1 §4.3: a name may be shaped like a path and a word is a prompt someone else wrote |
| A new key per work in `driver.json` for its policy | an older build reads today's keys; one reader over them changes nothing it reads |
| A message or a channel post doing a move's or a quest's job | D167's rejection of replacing quests by messages; D188 point 4 |
| Showing *participant*, *capability*, *move* or *event* on the page | D193's rejection |
| A model deciding a course or classing an event | D24; D157's declared facts |
| A timer per wait, or a second loop for conversations | D206's rejection; the look is the wake |
| A record-format cutover before every reader reads through the concept | the brief §7.4; an older build ignores a new flag, so new meaning goes where it cannot misread (`compatible-builds.md`) |

## 10. The rows, in build order

Each row is its own branch, lands with every gate of the brief §6 green and its area's suites run whole, leaves a person who sets nothing seeing
Daoris work as today except where its row names a decided change, and adds its own proof. A row that introduces a concept reads it beside today's
mechanisms and moves no reader; a row that moves readers moves one path or channel, the old surface its adapter, the census failing on a path that
neither moved nor is named. **Rows marked ⚖ grant or refuse authority, or delete, and are built only after both makers' reads** (D191's note of
2026-10-24). The rows are filed in `TASKS.md` when D212 is accepted; until then FOUNDATION1 points here. *Row a-i* in this design is
FOUNDATION1a-i, and so on. **The order** (round 2): a-i, b, c, a-ii, a-iii, d, p, e, f, g, h, i, o, j, w, x, y-i, k, l-i to l-v, m-i to m-v, n, q,
r, s, t, y-ii, u, v. Row a-i lands only after LIMITSWITCH1 merges onto a main that has moved under its branch (43 files differ, AGENTERRORS1b's
`ChatRetry` among them, O's §4).

### The first three

**FOUNDATION1a-i — one account rule, the cooling go-on inside it** (driver, web; after LIMITSWITCH1 lands on main).
- *What.* `LineAccount.Decide(own hold, trigger, constraint, line kind, the walk's answer over the accounts it may take) → Resume | Elsewhere(selection,
  why) | Wait(earliest, accounts) | Hold(reason, repair)`, pure, in `Accounts/Rotation/` beside `ResumeAccount`: §3.2's rule. Read by `ResumeChoice`
  (`Waits` and LIMITSWITCH1's `Switched`) and the roster's resume, whose inline twin (`HarnessRoster.cs:1008`) and `switches` parameter become one
  call; by the chat's go-on (`ChatGoOn.cs:571-587`), the wake (`LimitWaits.cs:186`, `:199`) and the closed quest's hold (`Driver.cs:2367-2384`).
  Executors unchanged but for two: a repository conversation's cooling *elsewhere* goes on through the ended move's body, its guard
  (`LimitMove.cs:213`) widened to a cooling own account, why `account`, its box written, the brief and the waiting words handed once; a closed quest's
  cooling session through `GoesOnLost`'s path as a conversation in its tree. Ask Daoris's go-on (`ChatGoOn.cs:672-688`) reads the rule and keeps its
  wait; a `choice: pick` keeps its own account; a conversation's other `CannotGoOn` cases are unchanged (§8). The earliest reset one computation;
  none with a reset, a hold naming the repair.
- *Census.* Every caller of `ResumeAsync`, `CoolingOf`, `CoolingsOf` and `ResumeChoice.Waits` reads `LineAccount` or is named *not yet moved*.
- *Proof.* `LineAccountTests`: own (ready, near, cooling, signed out, refused, missing, outside, kept, not offered) × trigger × constraint (none, a
  pick) × others (ready on its agent, ready on another choice, none, none with a reset) × kind (driven open or taken, driven closed, conversation, Ask
  Daoris), every row LIMITSWITCH1 and LIMITSWITCH2 do not change compared with today's `ResumeChoice` and `LimitWaits` answers in the same commit.
  LIMITSWITCH2's proofs, each seen failing first: a stand-in ended conversation whose own account cools with another ready goes on there in one
  briefed part, the words once; with none ready it waits and names the earliest reset; with every account signed out it holds naming the sign-in; a
  pick waits for its own; Ask Daoris waits for its own; a closed quest's cooling session goes on as a conversation in its tree, and on a headless
  host waits; a conversation whose own account is signed out is `CannotGoOn` as today. `ChatGoOnTests`, `ChatGoesOnProcessTests`,
  `LimitWaitsProcessTests`, `ContinuationTests`, `AccountRotationTickTests`, `LimitResumeTickTests`, `ResumeAccountTests` and
  `SessionMessagesTickTests` whole; the held row in both languages. Installed: the next real cooling own account with a conversation's words goes on
  within one look (`session.moved … why=account`).
- *Notes.* §7 rows 1, 1b and 3.

**FOUNDATION1b — a word keeps its identity wherever it is held** (modules, driver; any time, a day).
- *What.* The shell's held word carries its producer's key, level, files and display choice (`SessionWords.cs:179`, `:464`), kept in
  `held-words.json` additively, so an older shell reads text, files and door as before (`:915-935`); the record path passes the producer's key
  (`:701`, `:782`), and so does the headless fallback (`SessionSay.cs:197`). The display choice never becomes `SaidWord.Choice`.
- *Census.* Every `SayAsync` outside tests passes a key or is named as a legacy case.
- *Proof.* A producer's retry keeps one word; a restart keeps key, level, files and display choice; an ordinary display choice never reads as the
  supervisor's (`SessionLedger.cs:902-907`); a Process case restarts the shell while a record winds up and delivers once; `SessionWordsTerminalTests`,
  `SessionWordsWaitingTests`, `SessionLedgerTests` and `SessionStoreTests` whole.

**FOUNDATION1c — the participant's work, read once** (service, driver, web).
- *What.* `ParticipantWork` and `Participants.WorkOf` in the service, its twin in the driver, one fixture table both suites read (§2.2); the service
  answers `work` for every record of this machine on loopback, never on the wire; the sessions slice's allow-list gains `work` beside `kind`; `HoldsSlot`
  reads the work's table; `identity.ts` reads the pushed `work` where a host sends it and today's predicates where it does not, so a partner is drawn
  as a partner. The `Participant` type with its census, read by nothing else yet.
- *Census.* A: on every fixture row `WorkOf` agrees with `HoldsSlot`, `identity.ts`'s predicates (the page's test reads the same fixture),
  `ConnectorRoles`' reading of `serves` and `piece` and `WordsNever.IsHelp`; each disagreement listed as a decision (the partner drawn as a helper is
  one). B: every line outside `WorkOf` that reads `Serves`, `Opinion`, `Ask`, `Piece`, `HelpMode` or `HelpRoom.Repository` to tell a work is named
  *not yet moved* with the row that moves it; a new one fails. C: every record carrying an `actor` maps it to a role taking its work.
- *Proof.* `SessionStoreTests` (a record from before answered its work; a mirror another machine's), `SessionsStateSliceTests` (`work` pushed,
  allow-listed, loopback only, `kind` unchanged), `PlannerTests`' CAPCOUNT1 and SESSBADGE1 cases, ASKRAIL1's rail case, the page's identity tests with a
  partner record and with a host sending no `work`; `McpToolsTests`, `ActorChoicesTests` and `SessionLedgerTests` whole.

### The rest, in order

| Row | What | Proof |
|---|---|---|
| **1a-ii** account-pick provenance | a start the person names an account for (`START_CHAT`'s `profile`, the terminal's account, the person's hand-over press with an account; never an agent's ask or the curve's account, which keep today's reading) keeps `accountPicked` on the record, machine-local, through `session-record-fields.md`'s chain (service: the record, its column, the create, the reader, the ledger's opens; host: the request, the route, the loopback answer; driver: its write and read; the stand-ins; the web type); the rule reads it: a recorded account pick waits, an agent pick and a record from before walk their agent's accounts; §7 row 2 | `SessionStoreTests`' mirrored and elder cases (the field null), `ContinuesHostTests`, the feed's absence (`QuestSyncTests`), the route and loopback tests, `ChatHandOverTests` with a named account, `LineAccountTests`' constraint axis whole |
| **1a-iii** the briefed move's walk and one selection | the briefed move and its not-offered path through the walk, own account first where ready; the live and ended limit moves open on the selection they judged, one count (`LimitMove.cs:86`, `:142`, `:237`, `:256`); §7 row 4 | `BriefedMoveTests`, `LimitWaitsProcessTests`; the judged account is the opened one, counted once, on both moves |
| **1d** words follow the line's head at the say door | §3.4 rules 2 and 3: `Lines.HeadOf` inside `SayAsync`'s exclusive step, across kinds, read by `PartnerDesk` too; the new `Continued` refusal with the head, each say caller's handling named; every site that opens `from` and hands words reads them after the open, from the census (the ended and live limit moves, the driven carry-on, the briefed move and its not-offered path); moved words by one service step keeping the producer key beside the transfer id; moved words ahead of later ones | first the survey's *unverified* gap made a test: a word said to a live limit move between the old part's end and the new open lands once on the new part; a late word to a closed quest's driven part reaches the conversation that went on from it; a producer's retry at the new head keeps one word; LIMITWORD1's, HANDWORDS1-i's and SAIDBACK1e's cases green; the say door's twin tables cell for cell; `SessionLedgerTests`, `DrivenInboxTests`, `ChatGoOnTests`, `HandOverTests`, `CurveMoveTests`, `BriefedMoveTests` whole; the say door's latency on the install before and after |
| **1p** attempts acknowledged, dispositions and receipts | §3.4 rule 5; §7 row 8; before row f, so its proof stands | the attempt's commit failing sends nothing and says so; an answer naming fewer words than asked sends only those; a commit whose answer is lost; a crash before and after the wire; a limit's refusal carrying its words once, a transient try then a limit's refusal the limit's; a producer's retry after the take answered as kept within the horizon; no automatic resend of an uncertain attempt, its words in en and zh; the install's delivery latency and service writes before and after |
| **1e** the course, beside | `Course.Decide` over paths A to N reproducing today's answers, read by nothing; its facts with the answers-alone hold and the boundary; `Start` for start-from; the consent before an agent's ask and the curve; a standing move's old part `Never`; one closed `from_why`/`from_by` enum; each place two paths differ on the same facts listed as a decision for its row | the paths' suites' cases ported as its table; the trigger-site census (§3.1); `AnswersAloneHoldTests`' cases as course rows |
| **1f** the move record | `<s>.move.json` with its phases, its executor and claim, and its come-up stage per kind, before the sweep; the ledger's `opens` key, unique, atomic; first on the live limit move, the curve and the driven carry-on, which keep none, then the hand-over and the ended limit move with their boxes as adapters, the per-phase table of one recovery authority | a shell killed at each phase comes up and delivers each word on `said` when the move read them exactly once, a Process case per phase (a word said during a chat's `Hold()` named *row o's* in the census); a lost open's answer found by its key, never opened twice; an older host's lost open held, never searched; two claims, the older changing nothing; a downgrade and a re-upgrade at each phase acting once; `HandOverProcessTests`, `LimitWaitsProcessTests`, `CurveMoveTests` |
| **1g** failures read once on every path | what AGENTERRORS1's remaining rows do not already reach: a conversation's and a brainstorm's limit by the reader's code, a conversation's limited record written as the ledger's flag at its end | a chat's limit by code alone moves it; AGENTERRORS1's C4-6 case on a conversation; `AcpTransientTests`, `ChatTransientProcessTests`, `TransientRetryTickTests`, `LimitResumeTickTests` whole |
| **1h** conversations onto the course, one path per branch: the curve; the live and ended limit moves; the wake; the go-on; the briefed move and go-on-new; the hand-over | each path calls `Course.Decide`, its old decision the adapter, the hand-over rule's consent kept on the curve's and an agent's door | the path's suite whole (`CurveMoveTests`, `LimitWaitsProcessTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `HandOverTests`, `HandOverLookTests`); a consent case per door; the census's *not yet moved* list shorter by one |
| **1i** driven lines onto the course, one path per branch: the go-on and the limit resume; the quest's hand-over; the carry-on; past the transient bound; the send-back | as 1h | `ContinuationTests`, `SessionMessagesTickTests`, `LimitResumeTickTests`, `PlannerTests`, `TransientRetryTickTests`; a consent case per door; the family rehearsal |
| **1o** words never held outside `said` | §3.4 rule 4, after 1h and 1i: the say door's keep for a winding-up or moving record, in a shape an older shell's sweep cannot read as waiting; the shell's holds become keeps; a chat's `Hold()` during a move keeps and queues; `held-words.json` read once, never written | HANDWORDS1-i's case with the shell restarted mid-hand-over; an older shell's own sweep over a word kept for a moving record resuming nothing; `DrivenInboxTests`, `SessionMessagesTickTests`, `ChatGoOnTests`; a restart between keep and delivery; the say-to-wire latency on the install |
| **1j** one start plan and one walk | §2.4, one start per branch; four walks into one, one pass-code set, one re-ask; the reservation at the selection, probes uncounted; §7 row 5 | `ActorChoicesTests`, `HelperLookTests`, `OneTurnReaskTests`, the intake's and the brainstorm's suites; a record on Build's second choice resumed by the person's words; exactly one reservation per start; a look's starts spread as today |
| **1w** the company, read beside | §2.7's capability vocabulary and role record; `Company.Read` over `driver.json`'s `roles` and the seed; `Company.RoleFor(work, place)`; `Company.Judge`; the seed's ids, `ActorChoices.Ids` and `ActorRun`'s list one list; read by nothing | the seed reproduces today's every start case; the judge refuses an owner's act, a widened door, two roles taking one work in one scope; `DriverConfigKeepsTests` (an older build keeps `roles`); `ActorChoicesTests` whole |
| **1x** starts take their role | the start plan finds each start's role by its work and place; `actor` written as the role's id; the `design` capability, admitted as `build` and falling back to it, taken by a quest its publisher or the intake marks as design or decision work (MODELROLE1l's code) | a quest marked design starts on Build's order while no role takes `design`, and on the role that takes it once one does, each said in *Details*; an unmarked quest on Build's; the default company changes no start (`ActorChoicesTests`, `PlannerTests`, `HelperLookTests` whole); an older build's continuation of a record whose actor is an added role |
| **1y-i** the person sets the company up | adding, changing and removing a role without authority (name, work, staffing, scope) in Settings, with `daoris driver role` and through `role_propose` with the person's press; the one place a role is set, its words in both catalogues under the UI and translation contracts; the *Designer* suggestion on D205 point 7's card | the judge's cases at every door; a removed role's work holding its starts, said; a running session untouched; `i18n:check`, `names:check -- --strict`; the screen in both themes, widths and languages; an installed proof of the owner adding a role |
| **1k** the event journal, read by nothing | `event_journal` and its guarded raw triggers on `sessions`, `quest_log`, `asks` and the channel; the `journal` family; the floor and the trim; `store_identity` written by the schema's initialisation | each ledger move once, in order, as its raw transition; a note-only move, a same-state write and a mirror pass journalling nothing; a word taken waking the `journal` family; an older build's own statements, run in the test, mint rows and never fail (`compatible-builds.md`); the bound holds; positions never reused; landing limits measured on the install before and after: the `sessions` write's p95 latency within 10 % and the journal under 5 MB |
| **1l-i** to **1l-v** one end, one reader per branch | `part.ended` with its reason and class judged in .NET, §4.2's filter table; i the usage slice wakes on it; ii `SESSION_ENDED`; iii `session/ended`; iv the log's `session.ended`; v *Ended*; every process reading the journal from its come-up | `AttentionTests`, `NoteCodesTests`, `PluginKitTests`, `StatePublisherTests` whole; a driven end wakes the usage slice; LIMITRESUME1a's case; the class table both ways against `NoteCodes`' `ended.*`; a Process case with a shell beside a headless lease holder, a conversation's end told once and at once |
| **1m-i** to **1m-v** news from the journal, one reader per branch | i one owner of the notices, a cursor per teller; ii parks from the journal; iii ends; iv the said-set in place of each first-read baseline, one baseline per branch; v the notify rule in one table | a park made just before a restart told once after it; a crash between claim and balloon telling nothing twice; a held call told once while its process lives; nothing told twice; `AttentionStateSliceTests`, `AttentionTests`, `LookSaidTests`; the look's time on the install before and after |
| **1n** EVENTHOOK1 onto the journal | after EVENTHOOK1's rows land as designed, its session kinds read the retained journal (§5.1); its relay's delivery contract unchanged | its grammar census and relay proofs whole; a restart of the lease's loop re-reads a session event the journal holds; a missed frame still said as missed |
| **1q** ⚖ one policy shape and start admission | §2.3's policy over today's keys and the start layer's table, keyed by the work; §7 row 9 | `HelperLookTests`, `HelperJudgeTests`, `PartnerJudgeTests`; the five doors of survey §1.3 row 6 asked the same cases; `DriverConfigKeepsTests` (no key moved) |
| **1r** ⚖ the connector by work, failing closed | the call layer's table over every tool × every work, derived from the registration; a brainstorm's scope placed first, generator and synthesizer apart; `SessionRole` becomes the work; §7 rows 10 and 11 | `McpToolsTests`, `McpRoleFilterTests` over every work × every tool; a voice's result tools served; a stand-in named caller with no record served reads and refused mutations; C2a's measurement of the ACP doors first (FABRIC-ACP1) |
| **1s** push only | with D182's remaining rows: `DRIVER_TICK`'s data into slices; `STATE_CHANGED` for unmoved scopes and plugins alone; held answers by declared inputs and versions; §7 row 14 | `pushedState.test.tsx`, `bridge/state/*.test.ts*`, `StateSlicesTests`; bridge reads per minute on the install with two windows, before and after (D182 point 8) |
| **1t** ⚖ one role door | §2.5: one act form and proof, the act checked against the appointed role's authority, the holder a participant placed as a holder | `PipeCensusTests`, `StandingConfirmationTests` whole; a role's act never read as the person's; an act outside the role's authority refused; an owner's act refused whatever the role says |
| **1y-ii** ⚖ roles with authority | a role filled by appointment configured with acts from the vocabulary, after row t; an act added only by the person's own press in the window; appointing unchanged (D208 point 1) | the judge refusing an owner's act and an authority added by a proposal or the terminal alone; `StandingConfirmationTests`, `PipeCensusTests` whole; a configured appointed role's act recorded as its own |
| **1u** the role's name | the glossary's *role* entry (角色) with its reasons and *actor*'s line (§6) | `i18n:check`, `names:check -- --strict` |
| **1v** ⚖ records last | the old boxes, `held-words.json`'s reader and the driver's `ContinuedIn` fallback retired once no build in the update's rollback window reads them; a reviewer's `serves` (D203 point 6) once every reader reads `opinion` first; the move file and the carry's one file | an older build's statements over each new shape; `SessionHomeFiles`' inventory and its kept names; `QuestSyncTests` (nothing new on the wire); `SessionStoreTests` (the field null for a record from before and for a mirror); every word of a retired file on `said` before it goes |

1a-i is first because it removes the most defects for the least change: one pure function and the places that read it remove survey §2.4 rows 1,
11 and 13's account half, the class of LIMITSWITCH1, LIMITRESUME1b, LIMITRESUME1c and LIMITGOON1 (survey §2.6), and build a decision the owner already
made; its provenance and its briefed move are rows of their own (O-10). 1b is a day at the common boundary where three losses sit. 1c proves the
participant on the defects it came from (CAPCOUNT1, ASKRAIL1, SESSBADGE1) before anything is admitted by it. 1p comes before 1f so the move record's
proof can stand on acknowledged attempts and receipts; 1o after 1h and 1i so the course's `Never` holds the old part before a word is kept there.

## 11. What stays unmeasured, and what this design's checks do not cover

- **The triggers' cost** on `sessions` at the install's write rate, and the journal's size at its bound: row k measures write latency and CPU against
  its landing limits. That every end writes a reason code in `note_parts` is read from the state's statement and the codes' list, not proven for
  every writer; one that writes none is told `reason` absent and class `unknown`.
- **The head's cost at the say door**: a chain of `from` reads per word, with no index on `from_session` known (*unverified*); row d measures it.
- **Whether the course reproduces every live combination**: it is proven on the cases the paths' suites hold and on the census, not on every state a
  line reaches on the install.
- **The live limit move's word gap** (survey §2.4 row 3, *unverified* that it loses one): row d's first test.
- **News after a long downtime**: whether a cursor resuming after days tells too much at once; row m bounds it by the notices' week and measures it.
- **The receipts' and the move record's cost**, and how often an uncertain delivery happens: rows p and f measure them; the receipts' horizon (seven
  days, 10,000) is a first bound, not a measurement.
- **Whether lineage reaches Daoris over either harness's ACP door** (FABRIC-ACP1, C2b's), on which row r's child column waits.
- **Whether two processes append one session's event file**, and whether a shell not holding the lease reconciles its slices only at the manifest
  (survey §3.2, §3.4 row 7, *unverified*): the hub's rule assumes neither.
- **How often each move happens on the install**, which sizes row f's recovery cases: read from the machine log's `session.moved` lines first.
- **A brainstorm voice resolved at the connector through its box** in time for its first call (*unverified*).
- **An older build's continuation of a record whose `actor` names an added role** (*unverified*): row x's first case.
- **The defects' classes** (survey §6): a judgement, which this order inherits.
- **The checks.** A document only: `doc-links`, `doc-system`, `doc-shapes`, `doc-duplicates`, `doc-budgets` and `orient-index --check` check its
  place, its links and the index, never its claims. The code was read, not run, on main at `92ac2bd7e` and `c8eccf30e` (one `src/`), and LIMITSWITCH1
  on its branch at `3fdc6bbeb` and `adacd5fdc`; EVENTHOOK1, COWORKVIEW1's routing half and SWITCHCTX1's carry are proposed, not built, and what this
  says of them is read from their documents at that commit.

## 12. Round 2: the reads of round 1 answered

Each finding by number, as `docs/2026-10-25-foundation-reads.md` holds it, checked by the steward against the code at `c8eccf30e`; *taken* says where the
body changed, *disputed* says why not. Where the two reads overlap the stricter is taken, and the pair is named.

### 12.1 The Opus reader's foundation read (20 findings, 3 blocking)

| # | | Answer |
|---|---|---|
| 1 | blocking | **Taken, as the steward called it.** The code holds: Ask Daoris's *one exception* (`HandOver.cs:52-57`; `CurveMove.cs:70-71`), a closed quest's session waiting unless proven lost (`Driver.cs:2367-2384`), *nothing carries a chat's words on by itself* (`ChatGoOn.cs:566-569`, `:581`), and LIMITSWITCH1's own `switches` keeping both waits (`adacd5fdc`'s `HarnessRoster.ResumeAsync`; `Driver.cs:1989`). A closed quest's session whose own account cools stays in row a-i, within the owner's decision of 2026-10-25 (its reported case), recorded as amending D137 point 4 and its design §2.2 for that cooling case alone (§3.2 step 2, §7 row 1b), its executor the chat runner the look hands it to (`DriverLoop.cs:926`), a headless host waiting. Ask Daoris outside a limit and a conversation's refused, signed-out or gone account are cut from row a, each §8's open question; *kept* is a driven record's hold alone (`ResumeAccount.cs:31-36`), so round 1's list for a conversation was wrong. §3.2's misreading of D205 §3.5(e) is corrected: at a limit Ask Daoris already moves among its agent's accounts (`LimitMove.cs:72`, `:85-88`) |
| 2 | blocking | **Taken.** A standing move record before `delivered` makes the old part's own go-on `Never`, its words the move's (§3.1). Today the come-up takes the limit boxes up first (`ChatGoOn.cs:195`), resumes ended chats with waiting words (`:213-257`, guarded only by `ContinuedIn` at `:228`) and takes the hand-over boxes up last (`:260`), and the sweep (`:136-168`) reads no box: move records are taken up first, before all three (§3.3), and `TakeWaits` (`SessionWords.cs:544-545`) reads the move record. A word kept for a moving record is kept in a shape an older shell's sweep cannot read as waiting (`SessionWords.cs:455-458`); row o waits for rows h and i, its proof HANDWORDS1-i's case with the shell restarted mid-hand-over and an older shell's sweep |
| 3 | blocking | **Taken.** Each process reads the journal from its come-up, as `ServiceConfirmations.RunAsync` runs (`DriverLoop.cs:787`), the shell for its conversations whether or not it holds the lease (`:841`, `:895`, the change stream opened only inside the watch, `DriverWatch.cs:488-491`; today's in-process `onEnded`, `ConversationRoutes.cs:55`, `DriverLoop.cs:1233`) (§4.1); row l carries the Process case with a shell beside a headless lease holder. The curve's, the limit wake's and the limit move's `onEnded` hooks are wired inside the watch today (`DriverLoop.cs:920-929`), so they run for the lease holder alone; row l's case covers them |
| 4 | — | **Taken, with X-4.1.** Triggers guarded by what changed (`SetStateAsync`'s statement, `Sessions.cs:899-902`; the mirror's upsert, `:931-936`), a mirror journalling nothing, triggers never raising, the raw transition only, codes, class, work and `for` judged in .NET (§4.1, §4.2); row k's proof journals nothing for a note-only move, a same-state write and a mirror pass |
| 5 | — | **Taken, with X-2.1.** The participant's field is `work` on the wire, additive beside `kind` (`SessionsStateSlice.cs:41`; `api.ts:445`; `identity.ts:65-66`), with an older host's fallback (§2.1); D203 point 10's terminal field named `work`, added to D212's amend list |
| 6 | — | **Taken, with X-3.4.** The service's new `Continued` refusal named with its readers (`SessionSayRefusal`, `SessionLedger.cs:89-184`, has none today; `SessionSay.cs:207-209`) and every say caller's handling in row d's census; one service head, `Lines.HeadOf`, for the say door, `PartnerDesk.ContinuedByAsync` (`PartnerDesk.cs:114-120`) and the helper desk's walk the reader did not name (`HelperDesk.cs:140-145`), across kinds (§3.4 rule 2) |
| 7 | — | **Taken, and wider.** Row d's set is the census of every site that hands words to a part it opens: the briefed move and its not-offered path (`BriefedMove.cs:99`, `:158`, `:179`, `:250`), and start-from, which opens no `from` and reads its words before its open (`ChatRunner.StartFrom.cs:101-131`), and a refused open's move of its words (`ChatRunner.cs:1961-1982`) (§3.4 rule 3) |
| 8 | — | **Taken.** The ended move's selection is carried to its open too (`LimitMove.cs:236-237`, `:256`), in row a-iii (§2.4) |
| 9 | — | **Taken in part.** The person's named account on a hand-over press is a recorded account pick, as `START_CHAT`'s profile is (§3.2 step 4; §7 row 2; row a-ii). The split for the other doors is not taken: today a conversation's hand-over opens its named account as a pick on every door, the person's, the agent's ask and the curve's alike (`ChatHandOver.cs:156-157`; `CurveMove.cs:114-115`; `HarnessRoster.cs:887`), so *first, then the walk* for the agent's and the curve's would change today's behaviour unasked; round 1's step 3, which sent a conversation's hand-over's named account to the walk, is withdrawn with it, and a handed quest's named account, first and then its agent's walk, is today's (`HarnessRoster.cs:923-934`) |
| 10 | — | **Taken, as named.** Row a split: a-i `LineAccount` and the cooling go-on; a-ii account-pick provenance with its field chain (the service's lanes) and tests; a-iii the briefed move's walk and the one selection (§10) |
| 11 | — | **Taken, with X-5.1.** Row p moves before row f, and row f's proof is scoped to the words on `said` when the move reads them, a word said during a chat's `Hold()` row o's (`SessionLedger.cs:955-962`; `SaidDoor.cs:50-62`) |
| 12 | — | **Taken, with X-4.3.** A journal cursor per teller; news keyed by its subject's durable id; at most once (§4.3, §4.5). `Notices`' `KeepLock` and its keeping of keys it did not write hold (`Notices.cs:12-17`), so one owner holds as a class |
| 13 | — | **Taken, with X-3.1.** The words' state is a precondition (a word kept with no note moves no revision, `Sessions.cs:1565-1574`), the executor reading again as the driven go-on does (`DriverContinuations.cs:346-355`) (§3.1) |
| 14 | — | **Taken, each door at its own gate.** An agent's hand-over ask reaches `Move` only once the hand-over rule admits it (`DriverHandOvers.cs:208-214`), and the curve's move once its scope's switch does (`CurveMove.cs:17-19`, `:131-133`), since Daoris's own offer is not the hand-over rule's (`HandOverRules.cs:9-10`), else `Hold`, a census case per door in rows h and i (§3.1); with each gate stated and unchanged the rows change no authority, so they stay unmarked |
| 15 | — | **Taken, with X-2.2.** *The tools its handed rules refuse*, not every mutation: `opinion_give` (`OpinionPermissions.cs:29`) and the voices' result tools (`BrainstormPermissions.cs:22-28`) are the work's own (§2.3) |
| 16 | — | **Taken.** Rows l and m split into one reader per branch, l-i to l-v and m-i to m-v (§10) |
| 17 | — | **Taken.** One list of ids: the company's seed, which `ActorChoices.Ids` (`ActorChoices.cs:37`) and `ActorRun`'s list (`ActorRun.cs:176-177`) both read (row w). A word's author: one reader, `Words.AuthorOf`, over `By`, `Helper` and `From`, with a partner's new answers written as a partner's (`PartnerDesk.cs:93-96`), in row c's census B |
| 18 | — | **Taken, as a clarification.** Round 1 put the reservation in the settled selection and then said evaluating it counts nothing, which read two ways; the count is taken at the choice under the roster's gate, as today (`HarnessRoster.cs:894-897`, `:1271-1273`, `:1530`), only a probe uncounted (`counts: false`, `:1061`) (§2.4, §1.1 Q3) |
| 19 | — | **Taken.** Moved words keep their order ahead of words kept on the new part after its open, with a test in row d (§3.4 rule 3) |
| 20 | — | **Taken, the third for another reason.** Row h's reference to §7 row 15, the notices' writers, is gone (row h names its own cases); a move from an ended record starts at `old-ended` (`LimitMove.cs:191-196`; §3.3); the channel's quest lines are mapped at projection, but not because they travel to teammates: they are read from the quest per read and never stored (`Channel.cs:106-116`, `:1121-1133`), the team wire carrying messages alone, and their codes reach the page, the connector's `channel_read` and older builds, which is why they keep their names (§4.2) |

### 12.2 Codex's read (18 findings, 16 blocking)

| # | | Answer |
|---|---|---|
| 1.1 | blocking | **Taken.** Containment is D201's: a piece fails closed and an ordinary start runs as today where the launcher cannot contain it (`AgentStarts.cs:13`; `AgentJobs.cs:119`); round 1's *nothing starts uncontained* is withdrawn (§2.3) |
| 1.2 | — | **Taken.** A held call lives in its process's memory (`HeldCallAnnouncements.cs:22-23`; `Driver/Sessions/Events/HeldPermissions.cs:77-78`, *a restart ends that process, and the request with it*); it is told once while that process lives, and round 1's §7 row 12 and row m's proof of a held call across a restart are replaced (§4.5). Round 1 had misread the survey's §3.4 row 6, where *news for Holding* means it has no first-read baseline |
| 2.1 | blocking | **Taken, with O-5.** `work`, an additive field, with an older host's fallback (`SessionsStateSlice.cs:41`; `Http/ApiContracts.cs:400`; `say.ts:90`; `Web/design/ui.tsx:369`) (§2.1) |
| 2.2 | blocking | **Taken, with O-15.** A voice is placed through its box before the unknown-caller fallback, generator and synthesizer apart (`BrainstormScope`, `BrainstormBox.cs:13`, `:29`), each served its result tool as the box's own check already requires (`:71-74`; `BrainstormPermissions.cs:28`), with closed refusal codes (§2.3, row r). The child the read also names, calling with its parent's id (`RoleFilter.cs:28-29`), is the gap §2.6 names until C2a |
| 3.1 | blocking | **Taken, with O-13.** The answers-alone hold's count, bound and release grounds and the line's boundary are course facts (`AnswersAloneHold.cs:31-32`, `:97-123`; `ChatGoOn.cs:505`); the count is read from this machine's event files by the executor, which also writes the hold's marks (`:44-45`), so the course stays pure, and the other readers (`Driver.cs:934-937`, `ChatHelperWords.cs:96`, `SupervisorDoor.cs:305`) are census rows (§3.1) |
| 3.2 | blocking | **Taken.** Start-from is `Start`, a new line with no `from` under the press's own intent key (`ChatRunner.cs:339-341`; `SessionLedger.cs:698`), no move record; its only exclusion today is process-local (`:332`) (§3.1) |
| 3.3 | blocking | **Taken.** `Hold(reason, repair)` where no account the line may take has a reset, as `Unready` keeps a non-cooling refusal (`HarnessRoster.cs:1736-1740`, and *none ready* naming the signed-out accounts with no time at `:1741-1745`) (§3.2 step 5); `LineAccount`'s answers gain it |
| 3.4 | blocking | **Taken, with O-6.** One head across the driven-to-conversation boundary (`ContinuedIn` follows chats alone, `SessionSay.cs:56`, `:69`; `LastOwnForQuestAsync` passes a successor chat with no quest, `Sessions.cs:1045`, a chat opening with none, `SessionLedger.cs:696`), proven by a late word to the original driven part reaching the final conversation (§3.4 rule 2, row d). The gap stands today, not only after row a: a closed quest's lost session already goes on as a chat (`Driver.cs:2374-2381`) |
| 3.5 | blocking | **Taken.** A fenced claim with the executor's generation, each phase an atomic replace guarded by the revision read (`GoingOn` is a process's dictionary, `ChatContext.cs:64`); the destination opened atomically under a unique `opens` key; against an older host a lost open is held, never searched (`Sessions.cs:998`, newest first, nothing unique), *never twice* a newer host's guarantee alone (§3.3) |
| 3.6 | blocking | **Taken.** A moved word keeps its producer key with the transfer id beside it, moved, taken off and receipted in one service transaction (`SessionLedger.cs:956`; `SaidWord`'s one `Key`, `Sessions.cs:392-395`), which also closes the window the read names where both parts hold the word; the receipts' horizon seven days and 10,000 a home (§3.4 rules 3 and 5) |
| 3.7 | blocking | **Taken.** Dispositions apart: taken, refused at a limit (carried once, `ChatTurns.cs:1225`; `LimitWaits.cs:223`), uncertain, and a transient try then a limit's the limit's; the attempt's answer names each word it marked (`SessionLedger.cs:1234-1246`), which the client reads instead of answering success over an `unmarked` list (`ServiceClient.cs:569`) (§3.4 rule 5) |
| 4.1 | blocking | **Taken, with O-4.** Guards by OLD and NEW, per-word differences for `said`, an attempt mark journalling nothing (`Sessions.cs:1626-1632`), and a `journal` family waking on every append, a word taken included (`Sessions.cs:898-902`, `:1605`, `:677-678`) (§4.1) |
| 4.2 | blocking | **Taken; round 1 had the order and the floor's reading, not the rest.** Retention as a contract: 50,000 rows, the insert's statement trimming, positions never reused, the floor answered with every read, history below it said incomplete since the reader's cursor (`Sessions.cs:899` overwrites state and notes); §8's claim that the records hold what the journal trims is withdrawn; row k's landing limits stated (§4.1, row k) |
| 4.3 | blocking | **Taken, with O-12.** At most once: the said-set claimed durably before the balloon, the crash window losing one announcement, a refused claim showing nothing (`Notices.cs:17-19`), the said-set kept apart from the visible notices, which are pruned and dismissed (`:174`, `:187`, `:207`), the catch-up bounded by the week and the 200 (`:32-35`) (§4.5) |
| 4.4 | blocking | **Taken.** Row n's claim limited to the retained journal; EVENTHOOK1's relay keeps no replay and says its losses (`docs/2026-10-24-event-hook-design.md` §4.4); a shell's conversation events reach the lease-owned plugin stream only as record events in the journal, a shell's machine events never (§4.5, row n) |
| 5.1 | blocking | **Taken, with O-11.** Row p before row f; row f's guarantee scoped to words on `said` when the move reads them, a word said during a chat's `Hold()` (`ChatTurns.cs:192`) row o's, after rows h and i (§10) |
| 5.2 | blocking | **Taken.** One recovery authority at each phase: row f's per-phase table of what an old box holds or that it is not written, and a newer build's come-up reconciling the move record to an older build's evidence before it acts (`LimitMove.cs:352-379`; `HandOver.cs:292-295`) (§3.3); a box's words delivered under the box's own key (`limit-move/<session>`) while it stands, so the two builds dedupe against each other, and a hand-over box already refused to an older build once the successor exists (`HandOver.cs:143`, `:177-179`). The two-maker gates of rows q, r, t, v (and y-ii) and SWITCHCTX1's own owed read before its carry rows stand |
| 6.1 | — | **Taken in part.** The rejection is reworded: Codex's proposal keeps execution in the driver (its line 215), and the disagreement is where persistence lives (§1.1 Q8, §9); round 1's other reasons (one executor per kind, today's boxes in files, a second store family) did not rest on the misstatement and stand. Files are kept, with the fencing Codex names supplied by 3.5's claim and the service's atomic open; where neither can be had the move is held. Service-owned move tables stay rejected as a second store family for one record |
