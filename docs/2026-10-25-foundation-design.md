# The foundation: the participant, the course and the move, the event, and where state is kept (FOUNDATION1, proposed, round 3, 2026-10-25)

**Standing:** proposed with D212, round 3 under D191, written from main at `2a08bde4a`, whose `src/` is round 2's `29a066182` unchanged.
Round 1 was the synthesis of the three sealed proposals, `docs/2026-10-25-foundation-proposal-claude.md` (Claude's),
`docs/2026-10-25-foundation-proposal-codex.md` (Codex's) and `docs/2026-10-25-foundation-proposal-opus.md` (the independent Opus
reader's), each written from the brief `docs/2026-10-25-foundation-brief.md` and the survey `docs/2026-10-25-foundation-survey.md` alone,
each difference settled in §1. Both makers read round 1 (`docs/2026-10-25-foundation-reads.md`): the independent Opus reader as a
foundation, 20 findings, 3 blocking, accept after named changes; Codex, 18 findings, 16 blocking, another round; round 2 answered every
finding in §12 and carried the owner's direction on roles (the brief §8) in §2.7. Both makers read round 2 at `29a066182`: the Opus reader,
14 findings, 6 blocking, accept after named changes; Codex, 14 findings, 10 blocking, another round, naming the role's authorization
lifecycle, the move's fencing and rollback protocol and the journal's capture schema as choices still to make. Round 3 answers every finding
of round 2 by number in §15, each taken or disputed with the code line the steward checked, the stricter taken where the two overlap; makes
Codex's three choices (§2.7, §3.3 with §13.4, §4.1); carries the owner's direction that configuration and state move from JSON files to
SQLite, with the steward's line between the two (the brief §9), as a fourth area, **where state is kept** (§13); and carries the owner's
direction that the driver be analyzed into its subdomains (the brief §10) as the driver's module map (§14). §13 and §14 follow §12 so that
every section number the reads cite stays where it was. It changes a foundation in every sense D191's amendment of 2026-10-20 names, and
some of its rows grant or refuse authority and delete records, so by D191's note of 2026-10-24 it is not accepted on one maker's read: both
makers read round 3 before anything is accepted. Nothing here is built, spends an account, signs in, publishes or deletes.

Read for it: the brief (its §8, §9 and §10 amendments included), the survey and the three proposals whole; the reads of rounds 1 and 2; D139 and its design (`docs/2026-10-03-update-when-idle-design.md`) for the update's rollback; D137, D155, D163,
D167, D173, D182, D188, D191, D193, D197, D203, D205, D206, D208, D209 and D210 at the points this cites; D119 and D120 for the plugins,
whose points' frames are D101's; the in-flight designs EVENTHOOK1 (`docs/2026-10-24-event-hook-design.md`), COWORKVIEW1
(`docs/2026-10-24-cowork-view-design.md`), SWITCHCTX1 (`docs/2026-10-24-switch-context-design.md`) and AGENTERRORS1 (D210 and its
notes); the precedent of this shape, `docs/2026-10-23-actors-design.md` with D205; `.claude/knowledge/claims-need-checks.md`,
`compatible-builds.md`, `session-words.md`, `session-record-fields.md`, `pushed-slices.md`, `person-waits.md`, `twins.md` and
`translation-parity.md`; the glossary. Round 1's claims about the code were checked on main at `92ac2bd7e`; `c8eccf30e`'s `src/` is
that commit's unchanged, so its lines hold, and round 2's checks of the reads' findings were made there. LIMITSWITCH1's build was read
on its session branch, at `3fdc6bbeb` in round 1 and at `adacd5fdc` in round 2, and merged into main at `a85004bd0` as round 2 was
written; its merge moves a few lines this cites by up to ten (`ResumeChoice.Waits` now `ResumeAccount.cs:87`, the roster's twin
`HarnessRoster.cs:1014`, the closed quest's hold `Driver.cs:2368`), and the citations stay `c8eccf30e`'s. §0.2 lists
where the brief, the survey or a proposal was wrong, and §12 where round 1 was. `Dnn` is `docs/decisions/Dnn.md`; paths use the
survey's aliases (`Driver/`, `Modules/`, `Host/`, `Core/`, `Mcp/`, `Http/`, `Web/`), with `Chat/` for `Driver/Conversations/Chat/`,
`Loop/` for `Driver/Loop/` and `App/` for the desktop's executable as the survey shortens them. *C* is Claude's proposal, *X* Codex's,
*O* the Opus reader's; *O-n* and *X-n.m* are the findings of round 1's reads, *O2-n* and *X2-n.m* those of round 2's. Round 3's checks were
made at `2a08bde4a`, and its lines are that commit's.

## 0. What this rests on

### 0.1 The direction

The owner's direction of 2026-10-25, as the brief §1 carries it: Daoris grew through many foundational designs, each with a
mechanism of its own, and may need a refactor to a better structure that names the Fabric, the actor roles and the event system
as the foundation the rest stands on, designed under D191 and never built as a big-bang rewrite. The survey reads the cost: 51 of
the 273 fix-log entries of 2026-10-15 to 2026-10-25 came from two mechanisms answering one question differently (survey §0, a
judgement this design inherits and has not recounted). The quest's case, a finished record's words waiting days for its own
account while others ran work, is decided (LIMITSWITCH1, D137's note of 2026-10-25) and built for driven records of an open or
taken quest, merged at `a85004bd0`; its remainder, LIMITSWITCH2, a conversation's and a closed quest's session's go-on, is building now (§5.1).
The case the owner reported was a closed quest's session, which LIMITSWITCH1 did not reach (§3.2 step 2).

The owner's second direction of the same day, taken from round 2 (the brief §8): actors and roles are not hard-coded; each role can
be configured and set up into the company, D193's picture of a development company run by agents. The cut: **capabilities in code,
roles in configuration** (§2.7), the configured set the **role catalogue** and its subdomain **Roles** (the owner's naming, round 3).

Two more directions of the same day, taken from round 3. **Configuration and state move from JSON files to SQLite** (the brief §9), with the
steward's line between the two by the owner's leave: one desktop store, owned by the driver library, holding the Fabric's records, the role
catalogue, every setting, the machine-local state more than one process writes and every record this design adds (§13). **The driver is
analyzed into its subdomains** (the brief §10), its map's names and grouping the steward's by the owner's leave (§14).

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
| Brief §4: *Fabric, actor (角色), role … already have homes* | *Role* has no glossary entry, and Settings words the appointed roles 角色 (`Web/locales/zh/settings.daoris.json:108`), the glossary's word for *actor* (`glossary.json:475`; `zh/settings.ai.json:28`). Round 1 read that as two concepts sharing one Chinese word; round 2 took the owner's direction on roles to make one word right, 角色; round 3, the steward's choice, gives the role 职务 and leaves 角色 the actor's (§6) | O on the fact; the steward on the remedy |
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
| 1 **The participant** | A read over the stores; typed ids; its kind the stored `actor`, derived for records from before | A read contract with subject variants, relations, visibility and provenance; bindings issued by the service in new local-only tables | A type twinned in the service and the driver; its role read from the marks (D203 point 1), `actor` only provenance checked against it | **A read, never a store (all). Its work, a capability, read from the marks by one rule in the service, twinned in the driver (O), named `work` because the wire's `kind` already says driven or chat (round 2, O-5, X-2.1); its role the configured role it started under, `actor`'s value and D205's provenance, held to its work by a census (O, X; round 2); X's visibility field; ids Daoris already issues (C, O); its binding a row of the desktop store, today's `<s>.harness.json` moved there and the role it started under beside it (round 3)** (§2.1, §2.2, §13.3) |
| 2 **Rule and admission** | One `KindRule` {mode, agents, minutes, atOnce} over today's keys; three admission tables, twinned | One resolved policy with typed requirements; a staged admission with closed refusal codes; unknown named subjects read only | One `KindRule` per kind; four layers (start, call, act, contain), a table each keyed by role, failing closed; the brainstorm's `off` honoured | **O's four layers, a table each, keyed by capability (the owner's direction, round 2); X's policy shape (common fields and typed requirements per work) and closed refusal codes; failing closed for a named subject (all), a brainstorm voice placed by its scope first (round 2, X-2.2); the brainstorm's mode given no new behaviour (C, X); containment as D201 has it, a piece failing closed (round 2, X-1.1)** (§2.3) |
| 3 **The choice** | One `StartPlan`; one walk; one re-ask | One resolver whose answer is a settled selection with its reservation, uncounted until committed | One `ActorWalk`; one re-ask; every start through it | **One start plan whose answer is settled and carried to the open (X), its reservation taken at the selection under the roster's gate and only probes uncounted (round 2, O-18); one walk, one pass-code set and one re-ask (all); a start finds its role by its work and place, and a continuation takes its record's role and asked choice (all; round 2)** (§2.4) |
| 4 **Roles and actors** | A role held by a participant; a holder plays an actor once SUPERVISOR1c runs; one act door late | The same; appointment authority stays in `StandingConfirmation`; no door unified | Two axes of one participant; one act door, both makers | **One configured record, the role (the owner's direction, round 2): D205's actors are roles always on that take work; the supervisor and the steward are roles filled by appointment that hold acts; in two exclusive shapes, working and appointed (round 3, O2-2); an act admitted only within the code's table, the appointment's grant and the role's configuration, the grant raised only at the next *Appoint* (round 3, O2-1, X2-2.2); the seed read per id (round 3, O2-6, X2-2.3); `actor` keeping the seeded id (round 3, O2-3); a holder fills an appointed role, its authority the appointment's, proven per act; C's and O's one act door and proof, a late row both makers read** (§2.5, §2.7) |
| 5 **Native subagents** | A participant bound to its part by lineage; first reader COWORKVIEW1 row a; C2a placing calls | The same; unreadable never *no children* | The same; COWORKVIEW1's `start`/`end` lines as its generation | **All three; X's three outcomes of a read** (§2.6) |
| 6 **One decision** | `Course.Decide` → Resume, Move, Wait, Hold, Never | `MovementPlanner.Decide` with preconditions and intended effects | `LineNext.Decide` → Stay (resume, retry, hold) or Move with when, where, how and which words | **One pure `Course.Decide`: C's five answers and a sixth, `Start`, for a new line with no `from` (round 2, X-2.2's start-from, X-3.2); O's three axes on a move, X's preconditions, the words' state among them (round 2, O-13); the answers-alone hold and the boundary among the facts (round 2, X-3.1); a standing move makes its old part's own go-on `Never` (round 2, O-2); the retry stays inside the run (all); the carry a move's *how* (O)** (§3.1) |
| 7 **The account** | Own for a resume; go on now where ready; wait for the earliest reset of the accounts it may take; a pick waits | The same, with ineligible accounts adding no reset, and an account pick told from an agent pick by recorded provenance | The same; D206 point 5 amended to *a pick of an agent*, by the owner | **The rule once, read by every path (all); go on now where the own account cools, for a driven record, a conversation and a closed quest's session, within LIMITSWITCH1's decision, Ask Daoris and the other holds left as today until each is decided (round 2, O-1); the earliest reset over exactly the accounts the line may take and the own account where it may resume, ineligible ones adding none, and a hold naming the repair where none has a reset (C, X; round 2, X-3.3; round 3, O2-4); the closed quest's session once the briefed move walks (round 3, O2-5); a recorded account pick waits, an agent pick walks its agent's accounts, a record with no provenance is an agent pick (X); a hand-over press's named account a recorded account pick (round 2, O-9); the briefed move takes the walk (O); one settled selection (X)** (§3.2) |
| 8 **The move record** | A home file per move, the driver library's; recovered by the lease holder | Service-owned local tables, claimed by entry and generation; a destination allocated idempotently; a record even for a same-conversation resume | A home file per move, the driver library's; one come-up stage | **A row of the desktop store, the driver library's (round 3, the brief §9), claimed by the holder of its executor's role and fenced at every effect by a mark written before it, a begun effect never repeated (round 3, X2-3.1); recovered by the executor of its kind, a conversation's by the shell's chat runner (X), before the come-up's sweep (round 2, O-2); the destination opened atomically under the move's key in the service, the old part's waiting words moved in the same step (round 3); an older host's lost open held and never searched for (round 2, X-3.5); no old box written beside it, an older build recovering from the ledger alone (round 3, X2-1.1); a resume writes none** (§3.3, §13.4) |
| 9 **Words across a move** | Words follow the line's head, judged at the say door; delivered after the open; none held in memory | A word's whole identity at every keep; mandatory acknowledged attempts; receipts after a word leaves `said` | Kept for a winding-up record; one receiver; delivery by id and key; one carry predicate | **All composed, in rows: X's identity first (row b); C's head at the say door, one service head across kinds (row d; round 2, O-6, X-3.4); O's keep for a winding-up record, after the course reads move records (row o; round 2, O-2); X's attempts, dispositions and receipts before the move record (row p; round 2, O-11, X-3.7, X-5.1), each disposition per attempt (round 3, X2-3.2), the receipts in the desktop store by line and key (round 3, X2-3.3); a moved word keeping its producer key beside its transfer id (round 2, X-3.6), moved by the open itself (round 3); one carry predicate (all)** (§3.4) |
| 10 **The errors** | D210's kinds the course's input; a chat's limit by code | Typed outcomes; the survey's retry row stale | The same | **All three** (§3.5) |
| 11 **The event** | Record events minted by trigger into a journal; machine events by the lease's loop | The transition's owner mints at commit, through a transactional outbox; three orders kept apart | A driver-side hub of judged deltas, minted by the judge that diffs the records; memory only | **Record events by trigger at the commit (C's mechanism is X's outbox, and the one an older connector also mints through), raw transitions guarded by what changed and coded in .NET at projection (round 2, O-4, X-4.1), each captured with an allow-listed snapshot of the facts its coding needs, bounded per row (round 3, X2-4.1, X2-4.2), with a retention contract (round 2, X-4.2); machine events by each working process's hub at the judgement that owns the fact (O), the lease's loop alone telling plugins and outside readers; every process reading the journal from its come-up (round 2, O-3); X's three orders** (§4.1) |
| 12 **The vocabulary** | `EventCodes` beside `NoteCodes`; an end's reason and cause | One registry with payloads, privacy and rendering; subject-specific codes; *answered* split | `ChangeGrammar` from EVENTHOOK1's grammar; one end class | **X's registry and subject-specific codes, extending EVENTHOOK1's grammar (O); an end's note code and one class, judged in .NET (C, O; round 2); EVENTHOOK1's published meanings kept by its adapter (X)** (§4.2) |
| 13 **The readers** | A table; a Confirm ungated as a wait | A table; a teammate's mirror gets nothing new | A table; a new `DAORIS.CHANGE` frame | **The three tables are one; no new frame (rejects O's)** (§4.4) |
| 14 **Durability** | The journal durable and bounded; news told once by a durable said-set | A bounded outbox with an earliest cursor; a notice baseline is no event baseline | A change is a wake in memory; the first read a baseline | **C's journal and said-set, with X's floor and no balloon replayed; told at most once, the said-set claimed before the balloon, a cursor per teller (round 2, X-4.3, O-12); news keyed by its occurrence, the said-set's horizon one with the news a teller may tell, both kept in the desktop store (round 3, X2-4.3)** (§4.5) |
| 15 **Records** | One owner each; `ledger_events` new | One owner per fact; new tables for bindings, moves, receipts | One owner each; one `<s>.move.json` | **§5.2 and §13: one owner per record; the journal in the service's store; the moves, the words in transfer, the receipts, the bindings, the cursors, the said-set, the role catalogue and every setting in one desktop store (round 3, the brief §9); one machine-local field on the service's record (`opens`), none on the wire** |
| 16 **The names** | *role* 职务 in prose; *move* 接续; *Fabric* 协作织网 | *role* 任命职责; *move* 转接; *Fabric* 协作底座 | *role* 职务 on the page, retiring its 角色 | **The role 职务 on the page, the steward's choice of round 3, restoring round 1's word and withdrawing round 2's 角色 for both, *actor* keeping 角色 where D205's screens say it; the subdomain *Roles* and the configured set the *role catalogue*, *the company* D193's narrative alone (round 3); no Chinese for Fabric, participant, capability, move or event, which never reach the page; *work* for a participant's capability, D203's helper, partner and reviewer among them (round 2)** (§6) |
| 17 **The order** | 1a the account rule; 1b words at the say door; 1c the kind | 1 a word's identity; 2 the planner beside; 3 LIMITSWITCH | F1 the account rule; F2 and F3 the concepts named | **The account rule first, split in three (round 2, O-10), a word's identity second (X), the participant's work third (C, O); attempts and receipts before the move record (round 2, X-5.1); the role catalogue's rows after the start plan (round 2); the store and each of its families a row, the code moved subdomain by subdomain (round 3)** (§10, §13.6, §14) |
| 18 **The owner's** | Nothing | Nothing new | D206 point 5's amendment | **Two open questions, each its own decision, neither built until answered: Ask Daoris going on elsewhere outside a limit, and a conversation carried on where its own account is signed out, refused or gone (round 2, O-1); and one reading the owner may overrule, a closed quest's cooling session within LIMITSWITCH1's decision (round 3, O2-7)** (§8) |
| S **Where state is kept** (the brief §9; round 3) | — | Service-owned local tables for moves, bindings and receipts | — | **The steward's line: one desktop SQLite store, machine-local and never synced, owned by the driver library, holding the Fabric's records, the role catalogue, every setting, the machine-local state more than one process writes and every record this design adds; the service keeps its own store; JSON kept for others' formats, tracked records, manifests and files read before the store opens, streams, and `driver.json` as export and import alone** (§13) |
| M **The driver's subdomains** (the brief §10; round 3) | — | — | — | **Twenty-one subdomains in one order, bottom to top, each naming only those below it: Platform, Store, Settings, Service, Install, Instance, Events, Knowledge, Agents, Workplace, Roles, Participants, Context, Course, Work orders, Review, Plugins, Workflows, Setup, Help and Loop; folders, namespaces and a census in `StructureTests` rather than assemblies; the code moved one subdomain per row, bottom up, no behaviour changed; the steward's decision by the owner's leave, *Roles* the owner's name** (§14) |

### 1.1 What each choice rejects

| Q | Rejected | Why |
|---|---|---|
| 1 | C's stored kind (`actor` is the kind) | Two truths where they differ, and they differ by design: a record from before D205 carries the marks and no `actor` (`Sessions.cs:195`), and once SUPERVISOR1c lets a holder play an actor, its actor is not its kind. D203 point 1, accepted, already reads it. The defect class of CAPCOUNT1, ASKRAIL1 and SESSBADGE1 was many readers each reading the marks their own way, which one function removes without a stored kind |
| 1 | The participant's field named `kind` on the wire (round 1's) | The sessions slice already pushes `kind` as driven or chat (`SessionsStateSlice.cs:41`), typed and read by the page (`Web/data/api.ts:445`; `identity.ts:65-66`; `Web/work/say.ts:90`), so a second meaning breaks every unmoved reader (O-5, X-2.1) |
| 1 | X's binding tables in the service now | No subject needs an issued identity before C2a and C2b: a record's id, a brainstorm's scope, a holder's id and a child's lineage already place every caller. A table before its reader is a second store waiting for a use (D173's first guard); the row that needs one (a connected child, C2a's) designs it. The binding a record already has, its kept conversation in `<s>.harness.json`, moves into the desktop store as its one home (round 3, §13.3): a store moved, never a second one |
| 2 | C's and O's flat rule shape | X's point holds: an opinion's `required`, `verify` and `recheck`, a partner's measured row and the brainstorm's fan-out are rules, and a `{mode, agents}` shape would drop them; common fields plus a typed requirement per work keep them |
| 2 | One admission judge across processes | The start, the call, the act and the containment answer four questions in processes that share no code (O; `twins.md`); one classification feeding four tables is the unity that holds |
| 2 | Tables keyed by a fixed list of kinds (round 1's) | The owner's direction: a configured role must be admitted by what it does, so every table is keyed by the closed capability it takes, which a configured role composes and never extends (§2.7) |
| 2 | O's brainstorm `off` honoured inside the extraction | A rule no start reads today would start refusing the person's own request in a refactor; it is BRAINSTORM1d's door to give `mode` a reader (`BrainstormRules.cs:63-64`), said by the census as a rule with no reader yet |
| 3 | A caller walking again after the plan | The live limit move walks, then opens on `profile: null`, which walks again (`LimitMove.cs:86`, `:142`), and the ended move does the same (`:237`, `:256`), so the account judged and the one opened can differ and the first walk counts a start never made (survey §2.4; O-8) |
| 3 | X's *evaluation uncounted, the launcher consumes the reservation* as worded | The roster spreads a look's starts across accounts by counting each chosen start at its selection (`HarnessRoster.cs:894-897`); counting only at the launcher would put a look's starts on one account. The reservation is taken at the settled selection, under the roster's gate, and only a probe is uncounted (`counts: false`, `:911`) (O-18) |
| 4 | Roles hard-coded: eight actor ids and two appointed roles in code (round 1's) | The owner's direction of 2026-10-25: the positions of D193's company are set up, not compiled; the code keeps what each capability may do (§2.7) |
| 4 | One flat role record holding work and authority alike (round 2's) | It would seat D208 point 2's dedicated holder in work orders and bypass each act's offered flag and scope kind (O2-2; X2-2.2); two exclusive shapes, the judge refusing a mixture |
| 4 | An added role's id in `actor` (round 2's) | Every continuation reads `actor` into a run that throws on an id it does not know, after the harness started (`ActorRun.cs:176-177`; `Driver.cs:3651`, `:3666`), in every build of the rollback window too (O2-3); the binding keeps the added role, `actor` the seeded id |
| 4 | A role's authority read live from configuration (round 2's) | An edit by any writer would widen a holder already appointed without the owner's press (`StandingConfirmation.cs:1273-1279`; O2-1, X2-2.2); the act is admitted within the appointment's grant, which only the next *Appoint* raises |
| 4 | A role widening what its work may call, or holding an act that is the owner's | Admission is the capability's, in code; an owner's act is never a role's (D208 points 1 and 6; D209 point 3's *everything else is never a role's act*) |
| 4 | A role narrowing its work's doors (round 2's) | No table read it: admission reads the work, so a narrowing bound only through the handed rules, where the adapter takes settings (O2-8; X2-2.1); a role carries no doors until a row gives the call layer the role, with its reason |
| 6 | One loop for driven lines and conversations; the decision inside each executor | A rewrite of both loops at once; the disagreement is in the decision, and a pure judge both call removes it without moving an executor (all three) |
| 6 | C's *the person's hand-over press ends the part now* for every kind | It would drop a conversation's closing turn, which its press asks today (`ChatHandOver.cs:145-150`); the two timings are a difference of door, stated once |
| 6 | Start-from as a `Move` keyed by its source (round 1's) | It opens an independent entry with no `from` (`ChatRunner.cs:339`; `SessionLedger.cs:698`), so a move record keyed by the source would make a second press reuse one destination or supersede the source (X-3.2) |
| 7 | Round 1's row a reaching Ask Daoris outside a limit and a conversation whose own account is signed out, refused or gone | Each reverses a *never by itself* rule LIMITSWITCH1's text does not reach: D137 §2.2 and D205's *one exception* for Ask Daoris (`HandOver.cs:52-57`; `CurveMove.cs:70-71`), and *nothing carries a chat's words on by itself* (`ChatGoOn.cs:566-569`). Each needs its own decision (§8; O-1) |
| 7 | C's *a pick waits for its own account* as written | The record cannot tell an account pick from an agent pick (§0.2), so waiting for every pick pins agent-only picks D205 §3.5(d) lets walk, and moving every pick moves an account the person named |
| 7 | O's amendment of D206 point 5 to *a pick of an agent*, asked of the owner | An account the person named is a constraint D205 §3.5(d) keeps; once it is recorded the rule reads it, and the owner's LIMITSWITCH1 decision already chose another ready account over a wait for every other case, so nothing new is asked |
| 7 | A wait with a reset manufactured where no account the line may take has one | Today `Unready` keeps a non-cooling refusal in exactly that case (`HarnessRoster.cs:1732`); a promised automatic resumption there would never come (X-3.3) |
| 8 | X's service-owned move tables | One executor per kind writes and takes up a move (a conversation's the shell's chat runner, a driven line's the look), both in processes that open the desktop store, and service tables would give a desktop record a second owner reached only by routes. X's transactional needs are met where the move lives (round 3): the move's claim, its words in transfer and their receipts in one desktop transaction, and the destination opened once by the service's atomic open under the move's key, which moves the old part's words in the same step (§3.3, §13.4). X keeps the move's execution in the driver too (its proposal §*Durable movement*); the disagreement was where the move's persistence lives (X-6.1) |
| 8 | A home file per move with a compare-and-replace claim (round 2's) | A read, a compare and a rename can interleave across processes (O2-12), and a guarded write fences no spawn or prompt made between two writes (X2-3.1); a row of the store, written under its write lock with a mark before every effect, fences both |
| 8 | Old boxes written beside the move record for the rollback window (round 2's) | Two recovery authorities: the old limit recovery delivers from its box and the hand-over's take clears its box whatever happened (`LimitMove.cs:352-379`; `HandOver.cs:294`), and a producer key dedupes only while its word waits (`SessionLedger.cs:956`), so the two builds do not dedupe against each other (X2-1.1; O2-14); no box is written, and an older build recovers from the ledger alone (§13.5) |
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
| 16 | Round 2's 角色 for every role | The steward's choice of round 3: a role is a position held, whichever of its two shapes, which 职务 names; 角色 stays D205's word for an actor where its screens show staffing (`zh/settings.ai.json:28`), so the page says the configured position and the staffing screen apart |
| 16 | *Company* for the subdomain or the configured set (round 2's) | Too broad a word for a module, and D193's narrative; the subdomain is *Roles* and the set the *role catalogue* (the owner's naming, round 3) |
| 16 | *Roster*, *Org* and *Team* for it | *Roster* is the accounts' (`HarnessRoster`); *Org* collides with the fixtures' *Org A* and *Org B* and with GitHub's organisations; *Team* is D203's word for a session's co-workers |

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

## 2. Who acts: the participant and the role catalogue

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

| Work, or placement | Its seeded role (§2.7) | Its record | Distinct because |
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

The closed refusal codes are named once and answered alike at every door that judges the same case (X2-2.4): the call layer's `unplaced` (a
named session with no record), `unread` (its record could not be read), `unknown-tool` (a tool not yet classed) and `forbidden-work` (a tool its
work may not call); the start layer's are its rule's, and the role catalogue's judge's are §2.7's. The harness's own rules handed at the spawn
(`OpinionPermissions`, `IntakeRoom`, `HelpRoom`, `BrainstormPermissions`) are rendered from the call layer's table and stay a second guard where
the adapter takes settings; a test compares the two cell for cell. Each work's row
carries the mutations its work is for: a reviewer's `opinion_give` (`OpinionPermissions.cs:29`), a voice's result tool, a helper's
answer. *May this session ask a helper?*, asked in five places today, is asked of the start layer by the page and the terminal through the
driver (D182 point 6), and the service's desk keeps one check as its store's own guard, reading the same row.

**What the person sees change**, each a decision already taken: a partner is drawn as a partner (`identity.ts:79` draws any `serves` as a
helper); Ask Daoris is refused `helper_ask` (D203 point 6); a reviewer's and a brainstorm voice's calls beyond the tools their handed
rules allow are refused at the server on every door, as the handed rules refuse them only where the adapter takes settings
(`Driver.cs:3087`). Nothing else, and nothing for a person who sets nothing.

### 2.4 The choice: one start plan

Every start resolves its role, choice, agent and account through `StartPlan.Resolve(work, trigger, place, record?, pick?, fence?)`: the
role catalogue finds the working role that takes the work in the place (§2.7); D205's `ActorChoices.Effective` gives that role's order; one walk choice
by choice for the entry (today four, survey §1.3 row 3), D197's account walk inside each choice, one set of pass codes (today two), one
re-ask of a refused model (today five). Its answer is a **settled selection**: role, choice, agent, asked model and effort, account,
harness identity, the passes made and its reservation. The reservation is taken at the selection under the roster's gate, as a chosen
start is counted today so a look's starts spread across accounts (`HarnessRoster.cs:894-897`); a probe that judges without starting asks
`counts: false` (`:911`); the launcher consumes the reservation once and a failure releases it once (X; O-18). No caller walks again after
it. A continuation takes its record's role, read from its binding before `actor` (§2.7), and its asked choice (D205 point 1), never the
machine's `adapter`; a record whose role the person has since removed continues on the role that takes its work now, said in its opening
note as a later choice is (D205 point 5).

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
| A role holder; a meeting | outside, by D208's design; none | the census's two starts outside the plan, the holder's until SUPERVISOR1c gives a holder staffing (§2.5) |

### 2.5 Roles and actors

D205's **actor** and D208's **appointed role** are one configured concept, the **role** (§2.7, the owner's direction), in two exclusive
shapes: a working role with a name, the work it takes, its staffing and its scope, which is what an actor is; and an appointed role with a
name, its authority and its scope, which the supervisor and the steward are. A role is **held by** a participant only where it is appointed;
a working role is **played** by every participant its starts open. Today the holder is a participant placed as a holder, started by the
window outside the pool; once SUPERVISOR1c lets a holder run on an actor's staffing (D208 point 2), the holder's staffing is SUPERVISOR1c's to
place, on the appointment's holder and never as the appointed role's `choices`, which its shape refuses, and its authority is unchanged. Being
started on a role's staffing grants nothing: the authority is the appointment's, proven per act by the HMAC over the hello's nonce
(`SupervisorProof.cs:313-328`), the key never crossing the pipe. Its acts are its role's exact forms from the code's act table
(`StewardActRows.cs:152-162`; `RoleActs.cs:47-63`), recorded `by` the role and never as the owner's words (D208 point 1, D209 points 1 to
3); attribution narrows and never widens (D156 points 1 to 4). In the event vocabulary `role` is a cause of its own beside `person`,
`daoris` and `agent` (§4.2), so no reader takes a role's act for the person's press.

The two doors (survey §1.3 row 22: `SUPERVISOR_ANSWER`/`DEFER` with `SupervisorProof.Canonical` against `ROLE_ACT` with `RoleActForm.Bytes`)
become one act door with one canonical form and one whitespace rule, one reading of *a driven repository*, refusal sentences per role
(`InstancePipe.cs:1365-1381` says *the supervisor* for both), and one check that the act is within §2.7's three (the code's act table,
the appointment's grant and the role's configuration as it stands), the appointment's `acts` kept as today's check is (O2-1). The
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

### 2.7 The role catalogue: capabilities in code, roles in configuration

The owner's direction of 2026-10-25 (the brief §8): actors and roles are not hard-coded; each role can be configured and set up into the
company D193 pictures. **The code keeps a closed vocabulary of capabilities; a role is a configured record composed from them, and the
configured set is the role catalogue**, kept in the desktop store (§13) and owned by the Roles subdomain (§14). *The company* is D193's
narrative alone, never a name in the code (the owner's naming, round 3).

**The capabilities**, one closed vocabulary in the driver library, `Capabilities`, each with its admission row in each layer of §2.3:

| Kind | Codes | What the code fixes for each |
|---|---|---|
| Work: the kinds of work a participant can take | `build`, `design`, `review`, `intake`, `help`, `partner`, `brainstorm`, `ask-daoris`, `chat` (the direction's *help* is a helper's, D163; Ask Daoris's front desk keeps its code's name) | the desk or door that opens it and the mark it writes (§2.2); the tools it may call and the mutations it is for (§2.3's call table); its containment and handed rules; whether it may be off (Intake, Ask Daoris, Design); its fallback (`design` to whatever takes `build`); its policy's typed requirement |
| Acts: what an appointed role may make for the owner | the supervisor's two (`answer`, `defer`, D208 point 4) and the steward's act table (`StewardActRows.cs:152-162`, D209 point 3) | each act's route, exact form, proof and record; its **admissible scope kind** (the supervisor's for a repository or a workspace, the steward's for the machine, `Modules/Platform/StandingConfirmation.cs:831-835`) and its **offered flag** (`:330`, `:336`); and the acts that are never a role's, which no configuration names (D208 point 6; D209 point 3's list) |
| Doors | the call layer's tool classes; the pipe's route classes | which work or act may use each. A role carries no doors of its own: round 2's narrowing had no reader, since every table reads the work (O2-8; X2-2.1) |
| Containment | the launcher's kinds (`Agent`, `Piece`) and each work's handed rules | which each work runs under; no field of a role names it |

**A role** is a record of the catalogue, judged by one judge, `RoleCatalog.Judge`, for every door, as `ActorChoices.Judge` is. It has **two
exclusive shapes**, and the judge refuses a record that mixes them (O2-2; X2-2.2): a **working role** takes work and holds no authority; an
**appointed role** holds acts and takes no work, so D208 point 2's dedicated holder never takes work orders.

| Field | Shape | Says | Bound |
|---|---|---|---|
| `id` | both | the role's wire id | the eight actor ids and `supervisor`, `steward` for the seeded roles; an added role a new id, never one of theirs |
| `name` | both | its name in English and in Chinese | authored content, kept as written (the translation contract) |
| `filled` | both | `always` for a working role, started whenever its work arrives and its policy admits; `appointed` for an appointed role, held by the holder the owner appoints (D208 point 1) | decides the shape |
| `takes` | working | the work it takes | codes of the work vocabulary; at most one role takes one work in one scope |
| `choices` | working | its staffing: D205's one to four ordered `{ agent, model?, effort? }` | D205's judge keyed by the work rather than the literal id, so a role taking `brainstorm` holds one voice per agent (`ActorChoices.cs:51`, `:65-72`; X2-2.4); walked only over the agents the work's policy permits in that place, the rules still fencing (D205 point 6) |
| `authority` | appointed | the acts it may hold | acts of the vocabulary only; offered only where every act it holds is offered in this build, and scoped only where every act admits the scope's kind (O2-2) |
| `scope` | both | where it works: the machine, a workspace, a repository | the most specific scope's role takes a work there; an appointment names a scope inside its role's |
| `revision` | both | the record's revision, every change a new one, never rewritten in place | read by a start, which keeps the revision it took (§13.3's bindings), so census C compares a record with the catalogue as it stood at its start (X2-2.1) |

**The seeded roles, per id** (O2-6; X2-2.3). The seed is a dated table in code that reproduces today exactly, as `ActorChoices.Effective`
reads *as before* (D205 point 7): Build, Intake, Second opinion, Helper, Partner, Brainstorm, Ask Daoris and Chat (each `always`,
machine-wide, taking the work of its name, its staffing D205's actor's), the Supervisor (`appointed`, a repository or a workspace, `answer` and
`defer`) and the Steward (`appointed`, the machine, the steward's acts). No role takes `design`, so design work falls back to Build. The
catalogue reads **each id** in one of four states: **absent**, the seed's; **set**, its record; **removed**, a record saying so; and
**unreadable**, a record that does not read, kept as its error and holding the starts that read it, as `ActorUnreadableException` holds an
actor's today, never read as the seed. So the person's first change writes that id's record alone and every other seeded role stays the
seed's; an empty or absent catalogue is the seed whole; *Back to the default* deletes a seeded id's record. The judge refuses removing the
role of a work that may not be off (`ActorChoices.MayBeOff`, `ActorChoices.cs:43`, `:55`, lets only Intake and Ask Daoris be off, and Design
falls back to Build), and refuses scoping such a seeded role away from the machine, so every such work has a role in every place; a role
added for a workspace or a repository takes the work there and the machine's role stays. An upgrade writes nothing.

**The one list of ids** is the eight always-on roles' (`ActorChoices.Ids`, `ActorChoices.cs:37`, and `ActorRun`'s list, `ActorRun.cs:176-177`,
read from one list; O-17). The supervisor and the steward stay off it, so *Actors* (`ActorChoices.cs:320`) and `daoris driver actor`
(`DriverChoicesCommand.cs:2059-2061`) list what they list today (O2-3).

**`actor` keeps the seeded id** (O2-3). A start writes in `actor` the seeded always-on role of its work (`build` for a start on an added role
taking `design` or `build`), so every build's continuation reads an id `ActorRun` knows: a continuation passes the record's `actor` into its
run (`Loop/Driver.cs:3666`; `DriverContinuations.cs:432`; `Chat/ChatGoOn.cs:645`, `:722`; `ChatRunner.cs:1398`; `LimitMove.cs:433`), and
`ActorRun` throws on any other id (`ActorRun.cs:176-177`) after the harness has started (`Driver.cs:3651`, `:3673`). The role the start took,
with its revision, is kept on the participant's binding in the desktop store (§13.3), machine-local and in a table no older build reads
(`compatible-builds.md`), and the run reads the binding's role before `actor`.

**An appointed role's authority, as a lifecycle** (O2-1; X2-2.2). An act is admitted only where it is in all three: the code's act table;
**the appointment's grant**, the acts the owner ticked at *Appoint* and its scope, kept with the appointment and never changed while it
stands (`Appointment`'s `Acts`, `StandingConfirmation.cs:187-189`, checked at `:1276` and at the act's run, `:1304`); and the role's
configuration as it stands at the act, its authority and scope, which only narrow. So an edit to a role narrows a live appointment at once and
widens none: an act added to a role, a scope widened or a restriction lifted takes effect only at the next *Appoint*, the owner's press in the
window (D208 point 1). A role's authority is edited only through a `Window`-class route, as `SET_APPOINTMENT` is, never through
`role_propose`, a card or the terminal; an import that would widen it holds that change for the same press (§13.5). The act's target is
checked against the appointment's scope and the role's as they stand. Removing an appointed role revokes its live appointments at once by
D208's revoke and its sweep, cancelling every act admitted and not yet run. An appointment found at a start stays dormant until *Appoint
again* (D209 points 1, 2 and 4).

**Who changes the catalogue.** The person adds, changes or removes a working role in Settings and with `daoris driver role`; Ask Daoris
proposes with `role_propose`, carrying the revision it read, judged by the same judge, and the person's press applies it in both modes.
`role_propose` extends MODELROLE1h's `actor_propose`, owed and unbuilt (`Driver/Help/Room/HelpRoomOwed.cs:94-101`), as row y-i's shape follows
MODELROLE1g's suggestion card (`ActorSuggestion`, not in `src/`); both are row y-i's predecessors, and D205 point 9's *no `settings_*` grant
covers actors* covers roles (O2-11). **An owner's act stays the owner's**: the judge refuses a role naming one, and the act door refuses it
whatever a role says (D209 point 3). **Appointing** stays the owner's press in the window alone; no verb, Ask Daoris door, card or grant
appoints (D208 point 1). Removing a working role whose work may be off leaves that work with no role in its place: its starts hold, said in
D205 point 4's sentence, until the person gives it one, and a running session is never stopped by the change; so row y-i refuses starts and
is ⚖. Every writer of today's actors and the catalogue is in the judge's census: `SET_ACTOR` (`Modules/Bridge/Routes/DriverRoutes.cs:319`),
`daoris driver actor` and `daoris driver role`, Ask Daoris's proposals (`Driver/Help/Proposals/HelpSettingProposals.cs:222`), the import
(§13.5) and the new role doors, each answering the judge's closed refusal codes: `unknown-work`, `mixed-shape`, `owner-act`, `not-offered`,
`scope-kind`, `may-not-be-off`, `two-roles-one-work`, `widening` (X2-2.4).

**What reads it.** The start plan finds a start's role by its work and place (§2.4); the admission tables read the work, never the role
(§2.3), so a configured role is admitted exactly as the seeded role taking the same work; the act door reads the intersection above (row t);
the page's Settings read the catalogue, the AI screen's actor rows its working roles and This Daoris's *Roles* its appointed roles, as today
for the seeded ones.

**An older build** (§13.5) reads no catalogue: a build from before the store reads the backup `driver.json`, and a store-aware build from
before the catalogue's table never opens it. It starts design work on Build and never starts an added role, and its continuation of a record
started on an added role reads `actor`, the seeded id, and runs as that role's seeded work (row x's first case, now answered by
construction).

**MODELROLE1l** becomes a configured working role, *Designer*, taking `design`, its staffing `opus` · high as D205's note of 2026-10-25 and
`docs/model-roles.md` have it, offered on D205 point 7's suggestion card and applied by the person's press. The code it needs is the
capability: `design` in the vocabulary, admitted as `build`, falling back to it, and taken by a quest its publisher or the intake marks as
design or decision work (row x). The mark is a new field of the quest, `weight`, shared with MODELROLE1i's `exact` and built by whichever row
comes first (O2-9): it travels with the quest as a fact about the work, carrying no path or name, and a build that reads no `weight` starts
the quest on Build's order (§5.2). The work read from the marks stays `build` for such a session (§2.2), and census C admits a role taking
`design` on a record whose marks read `build`. No Design actor is compiled.

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
     to a cooling own account, why `account`, among its agent's accounts; where none of them is ready it waits as today, since the ended
     move has no step to the role's later choices (`LimitMove.cs:235-241`, where the live move has `ElsewhereAsync`, `:92`, `:444-469`), which
     row a-iii adds with its case (O2-13);
   - **a closed quest's session**, as a conversation in its tree, through the path a lost conversation takes (`GoesOnLost`, `DriverLoop.cs:926-927`,
     to `ChatRunner.GoOnLostAsync` and `BriefedMove.MoveAsync`, `ChatRunner.cs:244-246`), once that move takes the walk in row a-iii: today it
     opens on the record's own account as a pick (`BriefedMove.cs:133`), refused exactly while that account cools, so the case cannot land
     before the walk does (O2-5). The look decides, the shell's chat runner executes and keeps the move (§3.3), and a headless host with no
     runner waits for the person, as today. This amends D137 point 4 and its design §2.2 (*a closed quest … nothing carries on by itself*,
     `docs/2026-10-03-session-messages-design.md` §2.2) for this cooling case alone, **as the steward reads the owner's decision**: D137's
     LIMITSWITCH1 note reaches *words to an ended record whose own account is cooling*, and the steward reads *an ended record* as including a
     closed quest's session. The evidence is the steward's (O2-7): the session the owner reported on 2026-10-25 was a finished build session
     whose quest it had itself closed as done (a build session closes its quest with `quest_respond`), so its quest was no longer open when
     its words were held for its own account's weekly reset, and LIMITSWITCH1, which reaches an open or taken quest's record alone, did not
     reach it; its FIX-LOG entry, which claimed the case, is corrected by a dated line. The owner may overrule the reading (§8).
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
   included, recorded on the start's binding in the desktop store (§13.3; O-9); **an agent pick** (`choice: pick` with no account kept), **a handed agent** and a role
   *as before* only that agent's accounts (D206 point 6). A named account on the other doors keeps today's reading: a conversation's
   hand-over, the agent's ask and the curve's included, opens its named account as a pick (`ChatHandOver.cs:156-157`; `CurveMove.cs:114-115`;
   *a pick is the one account, never moved*, `HarnessRoster.cs:887`), and a handed quest's named account comes first, then its agent's walk
   (`HarnessRoster.cs:923-934`); only the person's press is recorded as an account pick. A record from before the account pick is kept is an
   agent pick once row a-ii lands: Daoris invents no historical pin (X).
5. **None ready: it waits** until the earliest reset of exactly the accounts step 4 allows **and of the record's own account wherever step 1
   may resume there** (O2-4), one computation (`EarliestReset`), where an account signed out, refused, not offered or outside the set adds no
   reset (X): LIMITSWITCH1 as built keeps the own account's reset as the wait where the walk finds none sooner (`HarnessRoster.cs:1026-1034`),
   and resumes there by itself, as D206 has it. Said in D206 §3.7's words on its record and its rail, woken by the look (D206 point 4).
   **Where none of them has a reset**, the own account and every other signed out, refused or not offered, it is `Hold`, naming the repair (a
   sign-in, an account added, the model offered), as `HarnessRoster.Unready` keeps a non-cooling refusal today (`HarnessRoster.cs:1736-1740`),
   never a manufactured reset or a promised resumption (X-3.3).
6. **A new part takes one settled selection** (§2.4). **An automatic new part** (a limit's move, a carry-on, the cooling go-on) never takes the
   own account where its conversation could resume there (D206's rejection of a briefed carry-on on the same account). **The person's own press
   for a fresh part** (*Go on with Daoris's brief*, *Go on in a new session*) is the one exception, since the person chose a new part over the
   resume: it takes the walk with the own account first where ready (X2-3.4), where today the briefed move picks the cooling account
   (`BriefedMove.cs:133`, the door that fails exactly when it is needed, survey §2.4 row 13). A part whose own conversation is proven lost has no
   resume to keep, so its walk takes the own account where ready, as the ended move's lost path does today (`LimitMove.cs:238-239`).

It replaces (survey §2.4): row 1's flag at its three places and LIMITSWITCH1's `switches` parameter; row 2's seven answers, which become three
(own for a resume, the settled selection for a new part, a named account first); row 5's pick, by step 4; row 9's maker reading, asked by the
roster for every own account a limit cooled, the wake's included; row 11's two earliest resets; row 13's door.

### 3.3 The move record

Every `Move` keeps one durable **move record**, written before the old part ends where one runs and at `old-ended` for a move from an ended
record, which has no part to end (`LimitMove.cs:191-196`; O-20), and the record owns the move's delivery and recovery until the new part's
first turn answers. It is SWITCHCTX1's carry record (its §2.3) made the record of every move, and what LimitBox already is for one.

- **Where** (round 3). A row of the desktop store's `moves` table, keyed by the record left, its words in transfer in `move_words` (§13.3), owned
  by the Course subdomain (§14); deleted with the session's record by a delete or a clear, as the session's files are today
  (`SessionHomeFiles`). If SWITCHCTX1a lands first, its `<s>.carry.json` is a family row f-i imports; there are never two records of one move.
- **What.** Its executor (`conversation` or `driven`) and its **claim**: the run that holds it and its generation (§13.4); its phase, each with its
  moment: `decided` → `quiescing` (waiting for its natural boundary, the person's cut against an agent's ask kept apart) → `old-ended` →
  `opening` (its `opens` key written) → `opened` (the new record's id and the words its open moved) → [`copied` → `verified`, the carry's] →
  `delivering` → `delivered`, or `fell-back` (its code), or `held` (its reason); its **effect mark**, the external effect begun (a spawn, a prompt,
  a copy), its moment and, once known, its outcome; `why`, `by`, `how`; the settled selection's account by profile name; the brief's recipe (the
  why and the event bound it is composed up to, `ContextBriefs.cs:59-95`); the delivery: each word's id, producer key, transfer id and
  disposition, a limit's refused turns' bound, the channel messages held. Never a word's text, a path or a key.
- **Who writes and recovers it, claimed and fenced** (round 3, X2-3.1; O2-12). The executor of its kind, alone: a conversation's move by the
  shell's chat runner, a driven line's by the look of the loop that holds the home's lease, a closed quest's session going on as a conversation by
  the chat runner the look hands it to. Every write is one store transaction under the write lock that checks the claim first, so a read, a
  check and a write never interleave across processes; a move is claimed by its executor role's holder and taken over only by that role's current
  holder from a run that no longer holds it, so a live executor, however long it pauses, is never taken over; and **every external effect is
  fenced by a mark** written, under a claim check, before the effect, so after a takeover the old run begins none, and an effect it had begun and
  never recorded is the new holder's `held`, made at most once and never repeated by itself (§13.4 gives the protocol and who holds each role). A
  process's in-memory exclusion (`GoingOn`, `Chat/ChatContext.cs:64`) is no claim.
  At its come-up the executor of a kind takes up every record of its kind before anything else that could act on the same records: a
  conversation's where `LimitMove.TakeUpBoxesAsync` runs today (`ChatGoOn.cs:195`), before the come-up's go-ons (`:213-257`), the hand-over
  boxes' take-up (`:260`) and the sweep (`:136-168`), none of which reads a box today, and before the lease; a driven line's at the slot where
  uncertain words are settled (`DriverWatch.cs:739`); and before either, the imports of any box an older build may have written (§13.5). Taking one
  up writes a newer generation. The shell's guard for words said meanwhile (`TakeWaits`, `SessionWords.cs:544-545`), which reads only the hand-over
  box, reads the move record. Each phase is safe to run again, and an unknown effect outside Daoris (a spawn or a prompt whose answer was lost) is
  `held` and never repeated by itself (X).
- **The destination, never opened twice.** The ledger's opens take an optional `opens` key, kept on the record as a field of this machine's
  and never on the wire (`session-record-fields.md`), unique in the service's store; a repeat open with the same key answers the record it opened
  and the words it moved, in the open's own step (X; §3.4 rule 3). Against an older host, which answers a new record each time and moves no
  words, an open whose answer was lost is `held` for the person with the candidates named, never searched for by `from` and moment, since
  successors are not unique by `from` (`Sessions.cs:998`; X-3.5), so the guarantee *never twice* is a newer host's alone.
- **No old box is written beside it** (round 3; X2-1.1, O2-14). Round 2 wrote `<s>.limit-move.json` and the conversation's hand-over box beside
  the move record through the rollback window and claimed the two builds dedupe against each other by the limit box's key. They do not: a
  producer's key answers only while its word waits on `said` (`SessionLedger.cs:956`), the limit box's first delivery is a live turn rather than a
  keyed word (`ChatHandOver.SayHandedOver`, `ChatHandOver.cs:238-250`, called at `LimitMove.cs:277`; only the take-up's re-delivery is keyed, its
  guard the new part's `User` event, `:366-378`), and the hand-over's take clears its box however it came out, leaving a part it opened unbriefed
  (`HandOver.cs:271`, `:294`). So a move the store carries writes no box, and an older build recovers it from the ledger alone, as it recovers today
  a move that keeps no box; §13.5's table says what it does at each phase and what the newer build reads of its evidence when it comes back,
  before it acts. A box an older build writes while it runs is imported once at the newer build's come-up and set aside. Three records are not moves and stay what they are (O): the hand-over's **ask** (`HandOverAskBox.cs:28`), the agent's request
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
3. **The open moves the words** (round 3). The new record opens in one service step that also moves the old part's waiting words: inside
   the say door's exclusive step (`SessionLedger.cs:933`), the ledger opens the new record under the move's `opens` key (§3.3) and keeps on it
   the words the course named by id (`Carries`, rule 6, judged in .NET) and every word of the person's kept on the old part since the course
   read it, which the service tells apart by its author fields (`SaidWord`'s `From`, `By` and `Helper`, `Core/Sessions/Sessions.cs:392-424`),
   each under **its producer key** with a **transfer id** (the move's key and the word's own id, so a word with no key has one too, X2-3.3), and
   takes them off the old. So the head and the words move at once, and no word falls between a read and an open. A word said to the old part
   after the open is refused there with the head named (rule 2), and its caller passes it on. A retried open with the same key answers the
   record it opened and the words it moved, and moves nothing again, so a lost answer neither opens twice nor moves twice (X-3.6); the move
   record keeps the moved ids it is answered (§3.3). Moved words keep their order ahead of any word kept on the new part after its open, since
   the new part's first turn is the move's delivery (O-19). LIMITWORD1's second read, and the reads before the open that the ended and live limit
   moves, the driven carry-on, and the briefed move and its not-offered path make today (`BriefedMove.cs:99`, `:158`, `:179`, `:250`; O-7), are
   retired by it. Row d's census takes every site that hands words to a part it opens, `from` or not: start-from's `Start` names the record it
   takes the words from, with no `from` link (`ChatRunner.StartFrom.cs:101-131`), and a refused open moves its words (`ChatRunner.cs:1961-1982`).
   Row d needs no receipt for this (X2-5.1): the open's key and the transfer ids on the new record are its idempotency, inside one service step.
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
   (X-3.7). **Each attempt keeps one disposition, per attempt** (X2-3.2): **taken** (the turn took it: never sent again); **refused at a limit**,
   only where the door's refusal is conclusive that the agent never accepted the prompt the word was attempted in (the turn refused as a limit:
   the definite refusal carries it once to the part that goes on, as a limit's refused turns' words are carried today, `Chat/ChatTurns.cs:1225`;
   `LimitWaits.cs:223`); **uncertain** otherwise (attempted, no conclusive answer: never sent again by itself, said with *Say it again*). A try the
   provider failed as transient is no conclusive refusal, since the agent may have read its prompt, which is why `LimitHeld.Of` clears every word
   asked before a transient event (`Driver/Conversations/Domain/LimitHeld.cs:35-41`): a word attempted in such a try stays uncertain, and a limit
   that then ends the episode carries only the words no try attempted, never reclassifying an earlier attempt. Round 2's *a transient try
   followed by a limit's refusal is the limit's* is withdrawn. **The receipts** are rows of the desktop store (§13.3): each disposition is written
   by the dispatcher of the process that runs the part, in one transaction with the move's phase where a move delivers it, before that process
   asks the service to take the word off `said`; a word's producer receipt stays after the take for seven days and at most 10,000 a home, trimmed
   by its own statement. Every keyed producer reaches the say door through the desktop's service client (the HTTP host's say route is the door's
   one caller, `Http/Program.cs:1527`), which reads the receipt before it says, so a producer's retry after the take is answered as kept within
   that horizon, as the service answers it by key while the word waits (`SessionLedger.cs:956`). **A receipt's identity** (X2-3.3) is the line (its
   entry, D167) and the producer's key, so equal keys on unrelated lines are two words and a retry through several successors finds its word on
   the line; a word with no key is its own, as today (`Sessions.cs:373-375`), moved and receipted by its word id. The come-up sends only words
   proven never attempted (X; row p). Across a harness write that carries no receipt, that is the guarantee there is: no automatic second
   attempt, the evidence kept, the person's press.
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
| Minted | **by trigger, in the statement that writes the record**, into `event_journal` in the service's database, so every writer mints it (the host, the driver through it, each connector, and a connector built before the table), as the revisions are minted (`Sessions.cs:652-681`). A trigger journals the **raw transition** only (the record, the column, its old and new value, and for `said` each word's id added or removed), guarded by what changed (`WHEN OLD.state IS NOT NEW.state`, and per column), since `AFTER UPDATE OF` fires on a statement's list and `SetStateAsync` names `state`, `limited` and `said` on every move (`Sessions.cs:899-902`); an attempt mark, which rewrites `said` at its length (`:1626-1632`), journals nothing; a mirror's upsert (`:931-936`) journals nothing, since another machine's records are not this machine's facts; a trigger holds no logic that can raise, so an older writer's statement cannot fail through it (O-4; X-4.1). Each row carries **an allow-listed snapshot** of the facts its coding needs, taken from OLD and NEW at capture, so no later lookup is needed (round 3, X2-4.1; below). The code, the end's class, the work and a park's `for` are judged in .NET at projection from that snapshot alone, never in SQL (D182 point 6) | **by the hub of the process that runs the work**, at the judgement that owns the fact (O): the lease's loop for driven work, the shell for its conversations, the executor for a move's phases. The lease's loop alone tells plugins and outside readers (EVENTHOOK1 §4.1) |
| Id and order | `position`, an integer in commit order under the store's write lock, never reused after a trim, its epoch the store's identity | `run` and `seq` (EVENTHOOK1 §4.3) |
| Wake | the journal's insert moves a family of its own in the change stream, `journal`, so a reader is woken by every append, a word taken included, which today moves neither the session's revision nor the words family (`Sessions.cs:1605`, `:677`; X-4.1), and by nothing the guards leave out | the hub's teller |
| Scope | the record's workspace, filtered as data (NEEDSSCOPE1) | this home |
| Kept | durable, machine-local, never on the sync wire, in the service's own store (the brief §9); at most 50,000 rows of at most 1 KiB each, the insert's own statement deleting the oldest past the count, so an older build that never trims still bounds it; the **floor**, the lowest position kept, answered with every read (X-4.2; round 3's bound per row, X2-4.2) | memory, one run, bounded per reader with `missed` |

**The capture schema** (round 3; X2-4.1, X2-4.2). One table, `event_journal`, one row per transition, every column but `position`, `at`,
`record` and `change` nullable, so no writer's statement can leave out a value a constraint requires:

| Column | Holds | Bound |
|---|---|---|
| `position`, `at` | the commit order and the moment | an integer never reused; the store's clock |
| `record`, `family`, `change` | the record's id; its family (`sessions`, `quest_log`, `asks`, `channel`); one closed code of what changed (`opened`, `state`, `limited`, `word-added`, `word-removed`, `words-overflow`, `quest-op`, `ask`, `message`) | ids at most 64 characters (`substr`), codes 32 |
| `state_old`, `state_new`, `limited_old`, `limited_new` | the state and the limit flag on either side | codes |
| `note_codes` | the codes of the note's parts on NEW, at most eight, never their values or the note's text | 8 × 64 |
| `marks` | the work's facts on NEW: `kind`, and whether `ask`, `opinion`, `serves`, `piece`, `help_mode` and `daoris:help` hold, and the mirror's origin | codes and flags |
| `entry`, `from_session`, `from_why`, `from_by`, `workspace`, `quest` | the line, the part it came from and why, its scope and its quest | ids and codes |
| `word`, `word_author`, `word_level`, `word_keyed` | for `word-*`: the word's id; its author class from `From`, `By` and `Helper` (`person`, `daoris`, `agent`, `supervisor`, `helper`, `opinion`); its level; whether it carries a producer key | ids and codes, never the word's text or key |
| `op`, `ask`, `message`, `line_code` | a quest's operation, an ask's or a message's id, the channel's line code | ids and codes |

A row is at most 1 KiB, so the table's payload is at most 50 MiB at its 50,000 rows, plus its one index; row k's measured landing limit (under
5 MB at the install's rate) is the typical size, never the bound. Pages a trim frees are reused, so the file stays at its high-water mark without a
`VACUUM`; the bound leaves out the write-ahead log, which SQLite checkpoints, measured by row k. A statement that adds more than 64 words journals
the first 64 and one `words-overflow` row with the count, so an overflow is said, never silent; no path adds more than one today. **Never raising,
qualified** (X2-4.2): the triggers hold no `RAISE` and no constraint their inserts can violate, so no writer's statement fails through their logic;
a storage failure (a full disk, an I/O error) fails the whole statement, the record's write with it, since a trigger's insert is part of its
statement: capture is atomic with the write, never skipped. **The projection's inputs are immutable** (X2-4.1): .NET codes a transition from its
row's snapshot alone, since a record's state and notes are overwritten (`Sessions.cs:886`) and a word leaves `said` (`:1605`); row k proves an
end, a reopen and an end, and a park, its answer and a park again, all made before any reader runs, each coded from its own rows.

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
| Notifications | the news projection: the notices its one durable record, owned by `Notices`, today `data/notices.json` under the file's keeping (`Driver/Platform/Notices.cs:12-17`, its `KeepLock`) and from its row the desktop store's `notices` (§13.6), to which every process that tells news (the lease's loop for driven work, the shell for its conversations) submits facts in one transaction, each teller with **its own journal cursor**, and each piece of news keyed by its occurrence (§4.5; O-12, X2-4.3) |
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
| News | its said-set durable in the desktop store (`news_said`, §13.3), kept apart from the visible notices so a prune or a dismissal never forgets what was told, bounded by its horizon, one journal cursor per teller beside it (`journal_cursors`) | resumes from its cursors | the floor |
| Machine events | memory, one run | a new `run`, `run.started` | a new `run`, a `seq` gap, `missed` (EVENTHOOK1 §4.4) |
| Slices | memory, rebuilt from the truth | a new epoch | the epoch and the manifest (D182 point 3) |
| Move records | durable, in the desktop store | taken up by their executor, claimed and fenced (§3.3) | — |
| Held answers, `UNDER_WAY`, the confirmations' lists, the hook queue, held calls | memory | rebuilt or empty; a held call ends with its process, which holds it in memory alone (`Modules/Loop/HeldCallAnnouncements.cs:22`) | the read asked again; pipe-waiting keeps its own record |

**One rule for a first read** (survey §3.4 row 6): the baseline gives way to the said-set. A piece of news is told **at most once**: its key is
claimed in the said-set, durably, before the balloon or the toast is shown, so a crash between the claim and the showing loses that one
announcement and never repeats it, while the wait it told of stays where the person reads waits (*What needs you*, judged from records); one
claimed before is never told again, so reopening the app replays no balloon (X; X-4.3). A park made just before a restart, never claimed, is told
once after it (today dropped by the first look's baseline, `Attention.cs:224`; `SessionLog.cs:38-41`). A held call is told once while its owning
process lives; a restart of that process ends the hold and tells nothing for it (X-1.2). Only a home with no said-set yet, the first run of the
build that adds it, takes what stands as said, and so does a teller telling for the first time on a home, its cursor starting at the journal's
newest position. A claim the store refuses shows nothing, which keeps it at most once.

**A key is an occurrence, and forgetting one never tells it again** (round 3, X2-4.3). A piece of news is keyed by its occurrence, never by its
subject alone: a record event by the record's id and the journal position of the transition that made it, unique and never reused (§4.1); a
machine event by its own durable id (a Confirm's confirmation id; a held call's request within its process's run); and news a reconciliation
below the floor finds, which has no positions, by the record's id and the ledger's own moment for the state it reads (`updated`), so a later
answer or end of the same resumed record is a new occurrence, as `Notices.cs:146` already renews an answered notice. The said-set and the news a
teller may tell share **one horizon**: a teller tells only occurrences whose moment is later than both the notices' week ago (`Notices.cs:32-35`)
and the moment of the oldest key the said-set still holds, and the said-set forgets a key only once its moment is outside the week or its own
count (10,000, oldest first, trimmed by its own statement) pushes it out, which raises the horizon with it. So no forgotten key's occurrence is
ever eligible again; the 200 the notices keep bound what is shown, never what was said. Row m-iv proves more than 200 eligible pieces of news at
once, a key expiring with the horizon, a new teller, and a reconciliation below the floor, none told twice. The journal's
retention is not EVENTHOOK1's delivery: its relay keeps no replay and says its losses (`docs/2026-10-24-event-hook-design.md` §4.4), and moving
its session kinds onto the journal lets a restart of the lease's loop re-read what the journal still holds, never replay what a plugin missed
(X-4.4).

## 5. The designs in flight, and the records

### 5.1 What each in-flight design becomes, and which of its rows wait

| Design | Becomes | Its rows |
|---|---|---|
| **LIMITSWITCH1** (D137's note of 2026-10-25), built for driven records of an open or taken quest, merged at `a85004bd0` | the account rule's driven half, as built | stands as built; row a-i reads its `Switched` through `LineAccount` |
| **LIMITSWITCH2**, its remainder, building now as its own quest in the shape of rows a-i and a-iii (round 3): a conversation and a closed quest's session whose own account cools go on elsewhere | the account rule's conversation half and the closed quest's case: a conversation through the ended limit move's body; a closed quest's session through the briefed move once it walks (§3.2 step 2); a pick and Ask Daoris's room keeping the own account, as filed | rows a-i and a-iii absorb what it lands: each part it lands is taken as built, its proof kept as the row's. Row a-i's `LineAccount` then reads LIMITSWITCH1's `switches` and LIMITSWITCH2's switch as one call and changes no behaviour, its proof the same answers on every case both built; row a-iii keeps only what LIMITSWITCH2 leaves (the one selection on both limit moves, the ended move's `ElsewhereAsync`, the walk of the person's briefed press). LIMITSWITCH2's note on D137, point 4's amendment for the cooling case, is §7 rows 1 and 1b's, which become conformance notes |
| **MODELROLE1l** (D205's note of 2026-10-25), filed | a configured working role, *Designer*, taking `design` (§2.7) | its code is the `design` capability and the quest's `weight`, row x; its role a seeded suggestion applied by the person's press, row y-i |
| **EVENTHOOK1** (proposed, round 4, Codex's read owed) | the first outside projection and the first instance of the vocabulary: built first as designed, then moved onto the journal (row n): its session kinds read the retained journal, its `WorkEventGrammar` becomes `EventCodes`' outside projection, its kinds keeping their names there, its `work.landed` and `quest.parked` machine events under its own `run` and `seq`, its relay's delivery contract unchanged | none waits on the foundation; 1d's quest kinds, a later round, are designed from the journal's `quest.<op>` rather than the look's snapshot; row n waits for 1b and row k |
| **COWORKVIEW1** (proposed, round 4, Codex's read owed) | row a: the lineage's first production reader, the participant's child half, built as designed; the routing half (rows e, b, c, d, f): re-cut to read `Participants.Of` (row c) for who is at work, the line's head (row d) for the current executor in place of its own selector over `LastRun` (its §3.1), the move records (row f) for what waits on what, and the event codes (rows k to m) in place of reading waits from end codes | row a waits for nothing here; its routing half's round is written after rows c, d and f land |
| **SWITCHCTX1's carry** (proposed, round 5, Codex's read owed); its line view 1c-i accepted and built | re-cut in storage, kept in protocol: its carry record is the move record's `copied` and `verified` phases and `how: carry`, so a carried move and every other kind have one owner of delivery; its compatibility table, replay check, model judgement and fallbacks stay as designed | 1c-i stands; 1a, 1b, 1c-ii, 1d, 1e and 1f wait for row f's record as well as their own owed read; if 1a is accepted and built first, its `<s>.carry.json` is a family row f-i imports into the store's `moves`, never a second record |
| **AGENTERRORS1** (D210; 1a, 1a2, 1b built) | the course's failure input | 1b2 and 1c to 1g built as designed, in their own order; row g adds only what they do not: a conversation's and a brainstorm's limit by code, and a conversation's limited flag |
| **MODELROLE1** (D205; rows a to e built) | its `Effective` and walk become the start plan's; its eight actors the catalogue's seeded working roles | rows j and w |
| **LIMITRESUME1** (D206), open rows 1e and 1f | read the account rule's constraints | built as designed; 1e's walk across choices is step 4's driven half |

### 5.2 Records and owners

Every record of the survey's §4 keeps its owner unless this table or §13.6 says otherwise; *older* says what a build from before reads. Where a
record moves from a JSON file into the desktop store, §13.6 names its row and §13.5 what an older build does with it.

| Record | Owner after | Travels | An older build |
|---|---|---|---|
| `sessions` with `said`, the marks, `from`, `entry`, `limited`, the actor's fields | the service, unchanged; the work read, never stored; `actor` naming the seeded role of its work (§2.7) | as today; the work, `actor` and `from` never | reads it as today |
| `opens` on the record (new, row f-i) | the service, written at the open through `session-record-fields.md`'s chain (the record, its column, the create, the reader, the ledger's opens, the host's request, route and loopback answer, the driver's write and read, the stand-ins, the web type); it stays on the service's record, the brief §9's one exception, since its uniqueness is the open's own statement | no field on the wire | never reads it |
| The participants' bindings (new home of `<s>.harness.json`, row z-iii): the kept conversation, the account picked and whether the person picked it (row a-ii), the role taken and its revision (row x) | the desktop store, the Participants subdomain (§13.3, §14) | never | a build from before the store reads the file it left as the backup (§13.5) |
| `event_journal` (new, row k) | the service, by trigger, in the service's store | never | never reads it; its own writes mint rows through the triggers, which bound it |
| Moves, words in transfer, receipts (new, rows f-i and p) | the desktop store | never | never reads them; it recovers a move from the ledger alone (§13.5) |
| `<s>.limit-move.json`, the conversation's hand-over box | written no more once a move of their kind is the store's (rows f-v, f-vi); a box an older build writes is imported once and set aside (§13.5) | no | reads and writes its own as today |
| `data/handed/<quest>.json`, `<s>.new-session.json`, the hand-over's ask | a constraint, a press and a request, read by the course; the first two move into the store (§13.6), the ask stays the service's | no | §13.5 |
| `held-words.json` | read once at a come-up for an older shell's words, never written again (row o); retired in row v | no | an older shell writes it; a newer one reads it once |
| The notices, their said-set and the tellers' cursors | one owner, `Notices`, in the desktop store (row z-v), every teller submitting facts with its own cursor (row m) | no | §13.5: the file left as the backup |
| The role catalogue (new, row w) and every setting of `driver.json` (row z-ii) | the desktop store, the Roles subdomain and the settings' one parser (§2.7, §13.3); the seed in code, read per id | no | reads the backup `driver.json` and never starts an added role (§13.5) |
| A quest's `weight` (new, the first of row x and MODELROLE1i) | the service's quest record, written by its publisher or the intake | with the quest, a fact of the work carrying no path or name | reads no `weight`, so it starts the quest on Build's order |
| `store_identity` | the schema's initialisation, not the change stream's observer (X; row k) | no | unchanged |
| A reviewer's `serves` (D203 point 6, row v) | the service, at the desk's open | no field on the wire | an older page reads a reviewer with `serves` as a helper, so it lands only where every reader reads `opinion` first (D203 point 6's own rule) |
| Everything else in survey §4 | unchanged, or moved into the store by its row of §13.6 | unchanged | §13.5 |

## 6. The names

None of these is a new word on the page except a role's, which the person sees and sets. They name code, records and prose.

| Concept | en | zh | On the page | Code | Retires |
|---|---|---|---|---|---|
| Fabric | *Fabric*, the three concepts together, in prose (D173) | none | never | none | — |
| participant | *participant* in prose | none until it reaches the page | never | `Participant`, `Participants.Of`, `RuntimeBinding` | — |
| capability | *capability* in prose; a participant's is its *work* | none: each is shown by its role's name | never as a word; a role's work shown by the default roles' names (开发, 第二意见, 助手, 搭档, …) and 设计 for `design` | `Capabilities`, `ParticipantWork`, `Participants.WorkOf` | the connector's `SessionRole` (`Mcp/RoleFilter.cs:13-19`), which is neither a role nor a work (X), when its readers move |
| role | *role* (the owner's direction): a configured position, working or appointed; D205's *actor* and D208's appointed role are both roles | 职务 (round 3, the steward's choice) | Settings → This Daoris → *Roles* and Settings → AI features → *Actors*, both reading the catalogue; the two labels stay as they are until row y-i designs the one place a role is set, under the UI and translation contracts | `Role`, `RoleCatalog` (`RoleCatalog.Judge`, `.Read`, `.RoleFor`), in the subdomain `Daoris.Driver.Roles` (§14) | the page's 角色 for an appointed role (`zh/settings.daoris.json:108`) |
| role catalogue | *role catalogue* in prose: the configured set of roles, the seed's per id where none is set | none until it reaches the page | the screens above | `RoleCatalog` | *the company* as a code or record name, which stays D193's narrative (the owner's naming) |
| actor | *actor* (D205): a working role as its staffing reads | 角色, D205's | unchanged | `ActorChoices` | the word in prose, where *role* says it |
| the desktop store | *the desktop store* in prose | none | never: the person sees *Export* and *Import* of the configuration (row z-ii), worded in both catalogues | `DesktopStore` (§13), in the subdomain `Daoris.Driver.Store` | — |
| subdomain | *subdomain* in prose (the brief §10) | none | never | one namespace each under `Daoris.Driver` (§14) | — |
| line of work | *session* on the page; *line of work* in prose wherever *line* could mean the branch | 会话 | unchanged | `Entry` (D167's key), never `Line`, since `LinesStateSlice` is the branch's | — |
| line (the branch) | *line* | 主线 | unchanged | unchanged | — |
| part | *part* | 段 | unchanged | unchanged | — |
| move | *move* in prose | none | never: each kind keeps its words (转交, 继续, 额度用尽) | `Course`, `Move`, `Moves`, `LineAccount` | the boxes' types, as adapters |
| event | *event* in prose (the brief's, EVENTHOOK1's) | none | never: the page shows notices | `DaorisEvent`, `EventCodes`, `event_journal` | `WorkEventGrammar`, as `EventCodes`' outside projection |

*Role* enters the glossary with its reasons (D116): 职务, a position the owner sets up, held or played, of which an actor is a working one and the
supervisor and the steward appointed ones (任命); 角色 stays *actor*'s, so the staffing screen and the position read apart; and 职务 is used nowhere
else in the catalogues (O's §7). The steward chose it in round 3, withdrawing round 2's 角色 for both. The glossary's *actor* line *A co-worker's
role names its actor* reads *A co-worker's work names the role it runs as*. Code names that change: `ResumeChoice.Waits` read through
`LineAccount` (row a-i); `HoldsSlot` over the work (row c); `SessionRole` (row r); `LimitBox` read as a move (row f), its file moved in row v;
`AttentionEvent` a projection of `DaorisEvent` (row m). Only in prose: Fabric, participant, capability, line of work, move, event.

## 7. The disagreements this settles, each a dated note

By the brief §7.3 each unification states which answer stands, as a dated note on the decision it amends, in the row that makes it.

| # | Question | Today | The answer that stands | Note on, in row |
|---|---|---|---|---|
| 1 | the own account cools: wait or move (survey §2.4 row 1) | a driven open or taken quest moves (LIMITSWITCH1); a conversation, Ask Daoris and a closed quest's session wait | go on now where an account the line may take is ready, for a driven record, a repository conversation and a closed quest's session; Ask Daoris waits for its own (§8's first question) | D137 point 7, a conformance note to its 2026-10-25 note; row a-i |
| 1b | a closed quest's session whose own account cools (D137 point 4) | its words wait with *Start a conversation* (`Driver.cs:2367-2373`) | goes on as a conversation in its tree on a ready account, through the chat runner once the briefed move walks, a headless host waiting | D137 point 4 and its design §2.2, for that cooling case alone, as the steward reads the owner's decision (§3.2 step 2, §8); row a-iii, or LIMITSWITCH2's note where it lands first |
| 2 | a pick (§2.4 row 5) | `choice: pick` for both an account pick and an agent pick; the live move moves both among the agent's accounts | a recorded account pick waits for its own account, a hand-over press's named account included; an agent pick walks its agent's accounts; a record with no provenance is an agent pick | D205 §3.5(d) and D206 point 5, a clarifying note; row a-ii |
| 3 | the earliest reset (§2.4 row 11) | two computations, each one agent's | one, over exactly the accounts the line may take and the own account where it may resume, ineligible ones adding none; none with a reset, a hold naming the repair | D206 point 2; row a-i |
| 4 | the briefed move's account (§2.4 row 13) | its own account as a pick | the walk, its own account first where ready, the person's fresh-part press the one exception to an automatic part's same-account refusal | D189 points 2 and 3; D206's same-account rejection, a clarifying note; row a-iii |
| 5 | a carry-on's agent (§1.3 row 12) | the machine's first agent with the record's asked model | the record's role and asked choice | D205 point 1, a conformance note; row j |
| 6 | the kind: stored or read (§1.3 row 8) | both, by 27 lines and more | the work read from the marks by one rule; `actor` D205's provenance, naming the role | D203 point 1 and D205 point 10, a note that each stands; D203 point 10's terminal field named `work`; row c |
| 7 | words during a move (§2.4 row 3) | box, hold or none | kept on `said`, judged at the head inside the say door, delivered after the open | D188 point 6; D167 point 1; rows d and o |
| 8 | an attempt mark that fails (§2.3) | the word still goes | the word waits, said; each attempt one disposition, an uncertain one never reclassified; receipts after the take, by line and key | D188 point 7; D137; D210's AGENTERRORS1b note (a transient try's words stay uncertain); row p |
| 9 | Ask Daoris asks a helper (§1.3 row 6) | allowed by the helper's copies, refused by the partner's | refused | D203 point 6, conformance; row q |
| 10 | a caller the connector cannot place (§1.3 row 16) | passes | for a session Daoris names: a brainstorm's scope placed first, else reads served and every mutation refused | D173's FABRIC-C2a note; D199 for the role filter; row r |
| 11 | a reviewer's and a voice's calls at the server (§1.3 rows 7, 15) | pass | refused beyond what their handed rules allow | D155, D176; row r |
| 12 | a park across a restart (§3.4 rows 2, 6) | a park dropped by the baseline | told at most once, by the durable said-set | D196 and the watch's SURF5b note; row m |
| 13 | the usage slice and a driven end (survey §3.3) | reached only at its period | woken by `part.ended` | D182's PUSH1e note; row l |
| 14 | the chain's ranks 3 and 4 (`Chain.cs:170-172`) | declared, bound nowhere | removed unless a row reads them | D173's FABRIC-CHAIN1 note; row s |
| 15 | the notices' writers (§3.4 row 10) | the slice, the look and the headless host, each editing the file | one owner, `Notices`, to which each process that tells news submits its facts in one transaction of the store, with its own cursor, keyed by occurrence | D196; rows z-v and m |
| 16 | who staffs a kind of work (the owner's direction) | eight actors and two appointed roles in code | configured roles over a closed vocabulary of capabilities, the seeded roles read per id | D205 points 1, 7, 8 and 9, D208 point 1 and D209 points 1 and 3, a note on each that the role is configured and its bounds stand; rows w to y |
| 17 | an appointed role's authority (round 3) | the appointment's ticked acts, held for its life (`StandingConfirmation.cs:1276`) | the act within the code's table, the appointment's grant and the role's configuration as it stands; a widening only at the next *Appoint* | D208 point 1 and D209 points 1, 2 and 4, a conformance note; rows t and y-ii |
| 18 | a role's scope (round 3) | D205 rejects per-workspace choices | a working role scoped to a workspace or a repository takes its work there, the machine's role staying | D205's rejection of per-workspace choices, amended (O2-10); row w |
| 19 | where the configuration is read (the brief §9) | `driver.json`, hand-edited and read live | the desktop store; `driver.json` its export and import, imported once and kept as the backup | D205 point 6 (the rules' keys kept as their shapes in the store, a conformance note); D50 and D110 (every door reaches the import and export); row z-ii |
| 20 | an older build over a newer home (round 3) | each file read by whichever build opens it | a build from before the store reads the backups it left; one from before a table never opens it; a store stamped past a build's reading refuses its writes, saying to update | D139 (the imports after the build confirms), a note; row z-i |
| 21 | the driver's shape (the brief §10) | fifteen folders cut by technique, one flat namespace | one acyclic map of subdomains, each a folder and a namespace, held by `StructureTests` | D173, a note naming the map as the Fabric's code; rows mod-0 onward |

## 8. What is the owner's

Nothing built from round 3 needs D191 point 3's answer. It changes no direction: the owner's FOUNDATION1 direction asks for exactly this
structure, and the owner's directions on roles (§2.7), on SQLite (§13) and on the driver's subdomains (§14) are taken as given, with the line
between JSON and SQLite and the map's names and grouping the steward's by the owner's leave. It spends nothing beyond development's own runs
and what the owner already decided: rows a-i and a-iii move a driven record's, a repository conversation's and a closed quest's session's words
to another ready account where the own account cools, which LIMITSWITCH1's decision covers in its own words (*words to an ended record whose
own account is cooling no longer wait … while another account of the line's choices is ready*); and an account the person named stays pinned
(§3.2), so no account is spent that the owner did not choose. **One reading the owner may overrule** (O2-7): that *an ended record* includes a
closed quest's session. The steward reads it so on the evidence of the case the owner reported (§3.2 step 2), records it as amending D137 point
4 for the cooling case alone, and builds it in row a-iii, or LIMITSWITCH2 where it lands first; the owner's word otherwise puts the case back to
today's wait. It needs no sign-in, publishes, pushes and rewrites nothing, and loses none of the owner's data: the journal is a new history the
records do not hold, bounded by its own trim, whose loss is said (§4.1); a JSON file the store imports is kept as its backup, or set aside under
its import's receipt, never deleted (§13.5); an old box or `held-words.json` is retired only once no build reads it and every word in it is on
`said`, which row v proves. Enabling a plugin that tells outside programs, appointing a role and changing a role's authority stay the owner's
existing doors; no row turns one on.

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

Near the line, and named so the steward can check them: rows q, r, t, v, y-i and y-ii grant or refuse authority (who may start, call and act) or
delete records, so each is built only after both makers have read it (D191's note of 2026-10-24); row z-ii's import holds a change of authority
for the person's press; and the whole of D212 waits for both reads of round 3.

## 9. Rejected

| Rejected | Why |
|---|---|
| A `participants` table, or a record per brainstorm voice or child | two authoritative stores for one concept (D173's first guard); the C0 audit rejected a record per child (`D173.md:91`) |
| A stored kind, or the kind read from `actor` | §1.1, Q1 |
| Hard-coded actors and roles; a role that holds an owner's act, carries doors of its own, mixes the two shapes, writes an added id into `actor`, or reads its authority live | §1.1, Q4; the owner's direction |
| One admission judge across the connector, the driver, the pipe and the launcher | §1.1, Q2 |
| One loop for driven lines and conversations | §1.1, Q6 |
| The service owning the move record's persistence | §1.1, Q8: one executor per kind recovers its moves, and the transactional needs are met in the desktop store and the service's atomic open (§13.4); where neither can be had, the move is held |
| Old boxes written beside the move record; a home file per move | §1.1, Q8: two recovery authorities; a compare-and-replace fences no effect (round 3) |
| The store's alternatives (one store for the service and the desktop, a store per family, `driver.json` written through, blobs, `said` moved, the CLI on SQLite, credentials in the store) | §13.7 |
| Separate assemblies per subdomain now | §14.4: the folders form one cycle today, so assemblies would need every cut at once |
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
FOUNDATION1a-i, and so on. **The order** (round 3): a-i, b, c, mod-0, z-i, z-iii, a-ii, a-iii, p, d, e, f-i to f-vi, g, h, i, o, j, z-ii, z-iv, w,
x, y-i, k, z-v, l-i to l-v, m-i to m-v, n, q, r, s, t, y-ii, u, v. The store's other families (z-vi to z-xiv, §13.6) land after z-i in any order,
each before the first row that reads its family through the store; the driver's moves (mod-i to mod-xviii, §14.6) go bottom up after mod-0,
each when no open branch holds the files it moves, their cuts after each. Row p now comes before row d as well as f (X2-5.1), though row d needs
no receipt (§3.4 rule 3), so that every row that moves words lands on acknowledged attempts. The Opus reader's precondition for row a-i, that
LIMITSWITCH1 merge onto a main that had moved under its branch (O's §4), is met: it merged at `a85004bd0`; and LIMITSWITCH2, building now, lands
its parts of rows a-i and a-iii ahead of them (§5.1).

### The first three

**FOUNDATION1a-i — one account rule, the cooling go-on inside it** (driver, web; on LIMITSWITCH1, merged; cut back in round 3 to the cooling
rule for driven records and conversations, its earliest reset and its hold, O2-4, O2-5).
- *What.* `LineAccount.Decide(own hold, trigger, constraint, line kind, the walk's answer over the accounts it may take) → Resume | Elsewhere(selection,
  why) | Wait(earliest, accounts) | Hold(reason, repair)`, pure, in the Course subdomain's folder where row mod-0 has landed and beside
  `ResumeAccount` where it has not: §3.2's rule. Read by `ResumeChoice` (`Waits` and LIMITSWITCH1's `Switched`) and the roster's resume, whose inline
  twin (`HarnessRoster.cs:1014`) and `switches` parameter become one call; by the chat's go-on (`ChatGoOn.cs:571-587`) and the wake
  (`LimitWaits.cs:186`, `:199`). Executors unchanged but for one: a repository conversation's cooling *elsewhere* goes on through the ended move's
  body, its guard (`LimitMove.cs:213`) widened to a cooling own account, why `account`, among its agent's accounts, waiting as today where none of
  them is ready (O2-13), the brief and the waiting words handed once. The closed quest's hold (`Driver.cs:2367-2384`) reads the rule and keeps its
  wait until row a-iii. Ask Daoris's go-on (`ChatGoOn.cs:672-688`) reads the rule and keeps its wait; a `choice: pick` keeps its own account; a
  conversation's other `CannotGoOn` cases are unchanged (§8). The earliest reset one computation, the own account's included where it may resume
  (O2-4); none with a reset, a hold naming the repair. Where LIMITSWITCH2 has landed the conversation's switch, this row reads it through
  `LineAccount` and changes no behaviour (§5.1).
- *Census.* Every caller of `ResumeAsync`, `CoolingOf`, `CoolingsOf` and `ResumeChoice.Waits` reads `LineAccount` or is named *not yet moved*.
- *Proof.* `LineAccountTests`: own (ready, near, cooling, signed out, refused, missing, outside, kept, not offered) × trigger × constraint (none, a
  pick) × others (ready on its agent, ready on another choice, none, none with a reset) × kind (driven open or taken, driven closed, conversation, Ask
  Daoris), every row LIMITSWITCH1 and LIMITSWITCH2 do not change compared with today's `ResumeChoice` and `LimitWaits` answers in the same commit.
  The conversation's cases, each seen failing first unless LIMITSWITCH2 built it: a stand-in ended conversation whose own account cools with another
  ready goes on there in one briefed part, the words once; with none ready it waits and names the earliest reset; **its own account cooling and every
  other signed out, it waits for its own reset; a driven record whose own account and every other are signed out holds, naming the sign-in** (O2-4);
  a pick waits for its own; Ask Daoris waits for its own; a closed quest's cooling session waits, as today, until row a-iii; a conversation whose own
  account is signed out is `CannotGoOn` as today. `ChatGoOnTests`, `ChatGoesOnProcessTests`, `LimitWaitsProcessTests`, `ContinuationTests`,
  `AccountRotationTickTests`, `LimitResumeTickTests`, `ResumeAccountTests` and `SessionMessagesTickTests` whole; the held row in both languages.
  Installed: the next real cooling own account with a conversation's words goes on within one look (`session.moved … why=account`).
- *Notes.* §7 rows 1 and 3.

**FOUNDATION1b — a word keeps its identity wherever it is held** (modules, driver; any time, a day).
- *What.* The shell's held word carries its producer's key, level, files and display choice (`SessionWords.cs:179`, `:464`), kept in
  `held-words.json` additively, so an older shell reads text, files and door as before (`:915-935`); the record path passes the producer's key
  (`:701`, `:782`), and so does the headless fallback (`SessionSay.cs:197`). The display choice never becomes `SaidWord.Choice`.
- *Census.* Every `SayAsync` outside tests passes a key or is named as a legacy case.
- *Proof.* A producer's retry keeps one word; a restart keeps key, level, files and display choice; an ordinary display choice never reads as the
  supervisor's (`SessionLedger.cs:902-907`); a Process case restarts the shell while a record winds up and delivers once; `SessionWordsTerminalTests`,
  `SessionWordsWaitingTests`, `SessionLedgerTests` and `SessionStoreTests` whole.

**FOUNDATION1c — the participant's work, read once** (service, driver, modules, web: the sessions slice is the modules', `Modules/Loop/SessionsStateSlice.cs`; O2-14).
- *What.* `ParticipantWork` and `Participants.WorkOf` in the service, its twin in the driver, one fixture table both suites read (§2.2); the service
  answers `work` for every record of this machine on loopback, never on the wire; the sessions slice's allow-list gains `work` beside `kind`; `HoldsSlot`
  reads the work's table; `identity.ts` reads the pushed `work` where a host sends it and today's predicates where it does not, so a partner is drawn
  as a partner. The `Participant` type with its census, read by nothing else yet.
- *Census.* A: on every fixture row `WorkOf` agrees with `HoldsSlot`, `identity.ts`'s predicates (the page's test reads the same fixture),
  `ConnectorRoles`' reading of `serves` and `piece` and `WordsNever.IsHelp`; each disagreement listed as a decision (the partner drawn as a helper is
  one). B: every line outside `WorkOf` that reads `Serves`, `Opinion`, `Ask`, `Piece`, `HelpMode` or `HelpRoom.Repository` to tell a work is named
  *not yet moved* with the row that moves it; a new one fails. C: every record carrying an `actor` maps it to a role taking its work, by the role's
  revision the start kept, a role taking `design` admitted on a record whose marks read `build` (O2-9).
- *Proof.* `SessionStoreTests` (a record from before answered its work; a mirror another machine's), `SessionsStateSliceTests` (`work` pushed,
  allow-listed, loopback only, `kind` unchanged), `PlannerTests`' CAPCOUNT1 and SESSBADGE1 cases, ASKRAIL1's rail case, the page's identity tests with a
  partner record and with a host sending no `work`; `McpToolsTests`, `ActorChoicesTests` and `SessionLedgerTests` whole.

### The rest, in order

| Row | What | Proof |
|---|---|---|
| **1mod-0** the structure gate (§14.5) | `StructureTests` and `fixtures/structure.json` hold the twenty-one subdomains in their order, the subdomain, direction, purity, Loop and global-using rules, today's edges against the order named as *not yet cut*; new code written in place from here on | the gate passes over the tree with today's exceptions listed and fails on a planted upward type, a planted `Domain/` file naming `ServiceClient`, a planted rule in Loop and a planted global using; `StructureTests`' fixtures for each |
| **1z-i** the desktop store, read by nothing (§13.2 to §13.4, §13.6) | `data/desktop.db`, `DesktopStore` in Store, its version, revisions, imports and wake file; the home's table and its two twins; the GitDoor's read-only open | §13.6's row z-i |
| **1z-iii** the bindings (§13.6) | `<s>.harness.json` into `bindings`, every resume reading its id there | §13.6's row z-iii |
| **1a-ii** account-pick provenance | a start the person names an account for (`START_CHAT`'s `profile`, the terminal's account, the person's hand-over press with an account; never an agent's ask or the curve's account, which keep today's reading) keeps on its binding in the desktop store that the person picked its account (round 3: the store's `bindings`, never a field on the service's record, so no chain through the host and no field on any wire); the rule reads it: a recorded account pick waits, an agent pick and a record from before walk their agent's accounts; §7 row 2 | a binding of a record from before and of a mirror holds no pick; `ChatHandOverTests` with a named account; `LineAccountTests`' constraint axis whole; an older build continuing a picked record reads it as today's `pick` |
| **1a-iii** the briefed move's walk, one selection, the closed quest's case | the briefed move and its not-offered path through the walk, own account first where ready, the person's fresh-part press the one exception to an automatic part's same-account refusal (§3.2 step 6); the live and ended limit moves open on the selection they judged, one count (`LimitMove.cs:86`, `:142`, `:237`, `:256`); the ended move gains `ElsewhereAsync`, so a conversation whose agent's accounts all cool goes on with its role's later choice where the person allowed one (O2-13); then **the closed quest's cooling session goes on as a conversation in its tree** through `GoesOnLost`'s path (O2-5), a headless host waiting; §7 rows 1b and 4. Each part LIMITSWITCH2 lands is taken as built (§5.1) | `BriefedMoveTests`, `LimitWaitsProcessTests`; the judged account is the opened one, counted once, on both moves; an ended conversation with every account of its agent cooling going on with its next choice; a closed quest's cooling session going on in its tree on a ready account, and on a headless host waiting; the person's briefed press opening on the own account where ready |
| **1p** attempts acknowledged, dispositions and receipts | §3.4 rule 5; §7 row 8; before rows d and f, so their proofs stand on it; the receipts in the desktop store, by line and producer key | the attempt's commit failing sends nothing and says so; an answer naming fewer words than asked sends only those; a commit whose answer is lost; a crash before and after the wire; a limit's refusal carrying its words once; **a transient try's words staying uncertain when a limit then ends the episode, only the words no try attempted carried** (X2-3.2); a producer's retry after the take answered as kept within the horizon; **equal keys on unrelated lines two words, a retry through two successors one word, a keyless word moved and receipted by its id** (X2-3.3); no automatic resend of an uncertain attempt, its words in en and zh; the install's delivery latency and store writes before and after |
| **1d** words follow the line's head, and the open moves them | §3.4 rules 2 and 3: `Lines.HeadOf` inside `SayAsync`'s exclusive step, across kinds, read by `PartnerDesk` too; the new `Continued` refusal with the head, each say caller's handling named; the ledger's open moving the old part's waiting words under their producer keys with transfer ids, in its own step, idempotent under its key; every site that opens and hands words, from the census (the ended and live limit moves, the driven carry-on, the briefed move and its not-offered path, start-from's `Start`, a refused open); moved words ahead of later ones | first the survey's *unverified* gap made a test: a word said to a live limit move between the old part's end and the new open lands once on the new part; a word kept between the course's read and the open moved by the open; a late word to a closed quest's driven part reaches the conversation that went on from it; a producer's retry at the new head keeps one word; a retried open moving nothing again; LIMITWORD1's, HANDWORDS1-i's and SAIDBACK1e's cases green; the say door's twin tables cell for cell; `SessionLedgerTests`, `DrivenInboxTests`, `ChatGoOnTests`, `HandOverTests`, `CurveMoveTests`, `BriefedMoveTests` whole; the say door's latency on the install before and after |
| **1e** the course, beside | `Course.Decide` over paths A to N reproducing today's answers, read by nothing; its facts with the answers-alone hold and the boundary; `Start` for start-from; the consent before an agent's ask and the curve; a standing move's old part `Never`; one closed `from_why`/`from_by` enum; each place two paths differ on the same facts listed as a decision for its row | the paths' suites' cases ported as its table; the trigger-site census (§3.1); `AnswersAloneHoldTests`' cases as course rows |
| **1f-i** the move record, read by nothing | `moves` and `move_words` in the store, the claim, the effect marks, the come-up stage per kind before the sweep and after the imports; the ledger's `opens` key, unique, atomic; a box from SWITCHCTX1a, if built, imported (split by round 3, X2-5.2) | the store's transactions over a claim, a takeover by the role's current holder and none of a live holder; **a paused executor resuming after a takeover begins no effect, and an effect it began is held, never repeated** (X2-3.1); a lost open's answer found by its key, never opened twice; an older host's lost open held, never searched |
| **1f-ii** to **1f-iv** each move without a record keeps one, one per branch: ii the live limit move; iii the curve; iv the driven carry-on | the move written and taken up through the store; nothing else of the path changes | for each, a shell or loop killed at each phase comes up and ends with **each word on `said` when the open moved it either delivered once, held at a limit's conclusive refusal and carried once, or uncertain and said**, never sent twice (X2-5.1); a word said during a chat's `Hold()` named *row o's* in the census; §13.5's downgrade table run at each phase with an older build's own come-up, then the newer build's return acting once; `LimitWaitsProcessTests`, `CurveMoveTests`, `ContinuationTests` |
| **1f-v**, **1f-vi** the boxed moves, one per branch: v the hand-over; vi the ended limit move | the move through the store, its box imported once and no longer written (§3.3) | as ii to iv, and an older build's own box written during a downgrade imported once at the return and set aside; `HandOverProcessTests`, `LimitWaitsProcessTests` |
| **1g** failures read once on every path | what AGENTERRORS1's remaining rows do not already reach: a conversation's and a brainstorm's limit by the reader's code, a conversation's limited record written as the ledger's flag at its end | a chat's limit by code alone moves it; AGENTERRORS1's C4-6 case on a conversation; `AcpTransientTests`, `ChatTransientProcessTests`, `TransientRetryTickTests`, `LimitResumeTickTests` whole |
| **1h** conversations onto the course, one path per branch: the curve; the live and ended limit moves; the wake; the go-on; the briefed move and go-on-new; the hand-over | each path calls `Course.Decide`, its old decision the adapter, the hand-over rule's consent kept on the curve's and an agent's door | the path's suite whole (`CurveMoveTests`, `LimitWaitsProcessTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `HandOverTests`, `HandOverLookTests`); a consent case per door; the census's *not yet moved* list shorter by one |
| **1i** driven lines onto the course, one path per branch: the go-on and the limit resume; the quest's hand-over; the carry-on; past the transient bound; the send-back | as 1h | `ContinuationTests`, `SessionMessagesTickTests`, `LimitResumeTickTests`, `PlannerTests`, `TransientRetryTickTests`; a consent case per door; the family rehearsal |
| **1o** words never held outside `said` | §3.4 rule 4, after 1h and 1i: the say door's keep for a winding-up or moving record, in a shape an older shell's sweep cannot read as waiting; the shell's holds become keeps; a chat's `Hold()` during a move keeps and queues; `held-words.json` read once, never written | HANDWORDS1-i's case with the shell restarted mid-hand-over; an older shell's own sweep over a word kept for a moving record resuming nothing; `DrivenInboxTests`, `SessionMessagesTickTests`, `ChatGoOnTests`; a restart between keep and delivery; the say-to-wire latency on the install |
| **1j** one start plan and one walk | §2.4, one start per branch; four walks into one, one pass-code set, one re-ask; the reservation at the selection, probes uncounted; §7 row 5 | `ActorChoicesTests`, `HelperLookTests`, `OneTurnReaskTests`, the intake's and the brainstorm's suites; a record on Build's second choice resumed by the person's words; exactly one reservation per start; a look's starts spread as today |
| **1z-ii** the configuration (§13.5, §13.6) | `data/driver.json` into `settings` and `role_staffing`, every writer and reader through Settings; the export and the import at every door | §13.6's row z-ii |
| **1z-iv** the appointments (§13.6) | `data/standing-confirmation.json` into `appointments`, each with its grant | §13.6's row z-iv |
| **1w** the role catalogue, read beside | §2.7's capability vocabulary (each act with its scope kind and offered flag) and the two exclusive shapes; `RoleCatalog.Read` over the store's `roles` and the seed per id; `RoleCatalog.RoleFor(work, place)`; `RoleCatalog.Judge` with its closed refusal codes, keyed by the work, its census every writer (`SET_ACTOR`, `daoris driver actor` and `role`, Ask Daoris's proposals, the import); the eight always-on ids one list; read by nothing | the seed reproduces today's every start case; **a catalogue with one added role reproduces the eight seeded roles' starts** (O2-6); the judge refuses an owner's act, a mixed shape, an appointed role taking work, an act not offered or out of its scope kind, removing or scoping away a role of a work that may not be off, two roles taking one work in one scope; first edit, partial edit and last deletion keep absent, set, removed and unreadable apart (X2-2.3); `ActorChoicesTests` whole |
| **1x** starts take their role | the start plan finds each start's role by its work and place; `actor` written as the seeded role of its work, the role taken and its revision on the binding; the `design` capability, admitted as `build` and falling back to it, taken by a quest whose `weight` its publisher or the intake marks `design` (MODELROLE1l's code; the field shared with MODELROLE1i) | **first, an older build's continuation of a record started on an added role runs as its seeded work** (O2-3; X2-5.2); a quest marked design starts on Build's order while no role takes `design`, and on the role that takes it once one does, each said in *Details*; an unmarked quest on Build's; a scoped role naming an agent its place's rule does not permit walks only the permitted (the census's case); the seeded catalogue changes no start (`ActorChoicesTests`, `PlannerTests`, `HelperLookTests` whole) |
| **1y-i** ⚖ the person sets the catalogue up | adding, changing and removing a working role (name, work, staffing, scope) in Settings, with `daoris driver role` and through `role_propose` with the person's press; the one place a role is set, its words in both catalogues under the UI and translation contracts; the *Designer* suggestion on D205 point 7's card; after MODELROLE1g's card and MODELROLE1h's `actor_propose`, which `role_propose` extends (O2-11); ⚖ since removing a role holds its work's starts | the judge's cases at every door; a removed role's work holding its starts, said; a running session untouched; no `settings_*` grant reaching a role; `i18n:check`, `names:check -- --strict`; the screen in both themes, widths and languages; an installed proof of the owner adding a role |
| **1k** the event journal, read by nothing | `event_journal` with §4.1's capture schema, its guarded raw triggers on `sessions`, `quest_log`, `asks` and the channel, each row's allow-listed snapshot; the `journal` family; the floor and the trim; `store_identity` written by the schema's initialisation | each ledger move once, in order, as its raw transition; a note-only move, a same-state write and a mirror pass journalling nothing; a word taken waking the `journal` family; **an end, a reopen and an end, and a park, its answer and a park again, made before any reader runs, each coded from its own row** (X2-4.1); a statement adding more than 64 words journalling an overflow row; an older build's own statements, run in the test, mint rows and never fail (`compatible-builds.md`); the bound per row and per table holds; positions never reused; landing limits measured on the install before and after: the `sessions` write's p95 latency within 10 % and the journal's typical size under 5 MB |
| **1z-v** the notices (§13.6) | `data/notices.json` into `notices`, with the said-set's and cursors' tables | §13.6's row z-v |
| **1l-i** to **1l-v** one end, one reader per branch | `part.ended` with its reason and class judged in .NET, §4.2's filter table; i the usage slice wakes on it; ii `SESSION_ENDED`; iii `session/ended`; iv the log's `session.ended`; v *Ended*; every process reading the journal from its come-up | `AttentionTests`, `NoteCodesTests`, `PluginKitTests`, `StatePublisherTests` whole; a driven end wakes the usage slice; LIMITRESUME1a's case; the class table both ways against `NoteCodes`' `ended.*`; a Process case with a shell beside a headless lease holder, a conversation's end told once and at once |
| **1m-i** to **1m-v** news from the journal, one reader per branch | i one owner of the notices, a cursor per teller; ii parks from the journal; iii ends; iv the said-set in place of each first-read baseline, one baseline per branch; v the notify rule in one table | a park made just before a restart told once after it; a crash between claim and balloon telling nothing twice; a held call told once while its process lives; nothing told twice; m-iv's horizon cases: more than 200 eligible at once, a key expiring with the horizon, a new teller, a reconciliation below the floor, none told twice (X2-4.3); `AttentionStateSliceTests`, `AttentionTests`, `LookSaidTests`; the look's time on the install before and after |
| **1n** EVENTHOOK1 onto the journal | after EVENTHOOK1's rows land as designed, its session kinds read the retained journal (§5.1); its relay's delivery contract unchanged | its grammar census and relay proofs whole; a restart of the lease's loop re-reads a session event the journal holds; a missed frame still said as missed |
| **1q** ⚖ one policy shape and start admission | §2.3's policy over today's keys and the start layer's table, keyed by the work; §7 row 9 | `HelperLookTests`, `HelperJudgeTests`, `PartnerJudgeTests`; the five doors of survey §1.3 row 6 asked the same cases; `DriverConfigKeepsTests` (no key moved) |
| **1r** ⚖ the connector by work, failing closed | the call layer's table over every tool × every work, derived from the registration; a brainstorm's scope placed first, generator and synthesizer apart; `SessionRole` becomes the work; §7 rows 10 and 11 | `McpToolsTests`, `McpRoleFilterTests` over every work × every tool; a voice's result tools served; a stand-in named caller with no record served reads and refused mutations; C2a's measurement of the ACP doors first (FABRIC-ACP1) |
| **1s** push only | with D182's remaining rows: `DRIVER_TICK`'s data into slices; `STATE_CHANGED` for unmoved scopes and plugins alone; held answers by declared inputs and versions; §7 row 14 | `pushedState.test.tsx`, `bridge/state/*.test.ts*`, `StateSlicesTests`; bridge reads per minute on the install with two windows, before and after (D182 point 8) |
| **1t** ⚖ one role door | §2.5: one act form and proof, the act admitted within §2.7's three (the code's act table, the appointment's grant, the role's configuration as it stands), the appointment's `acts` kept (O2-1), the holder a participant placed as a holder | `PipeCensusTests`, `StandingConfirmationTests` whole; a role's act never read as the person's; an act outside the appointment's grant refused whatever the role's configuration says; an owner's act refused whatever the role says |
| **1y-ii** ⚖ appointed roles configured | an appointed role configured with acts from the vocabulary, after row t, through a `Window`-class route alone; an increase (an act added, a scope widened, a restriction lifted) effective only at the next *Appoint*; appointing unchanged (D208 point 1) | **a live appointment not widened by a later edit to its role, nor by a scope's widening or a restriction's removal, until the next *Appoint*** (O2-1; X2-2.2); a narrowing effective at once on a live appointment; removing an appointed role revoking its appointments and cancelling its admitted acts; an authority added by a proposal, a card or the terminal refused; `StandingConfirmationTests`, `PipeCensusTests` whole; a configured appointed role's act recorded as its own |
| **1u** the role's name | the glossary's *role* entry (职务, the steward's choice of round 3) with its reasons and *actor*'s line (§6); the page's 角色 for an appointed role retired (`zh/settings.daoris.json:108`) | `i18n:check`, `names:check -- --strict` |
| **1v** ⚖ records last | `held-words.json`'s reader and the driver's `ContinuedIn` fallback retired once no build in the update's rollback window reads them; the set-aside backups (§13.5) kept until then; a reviewer's `serves` (D203 point 6) once every reader reads `opinion` first | an older build's statements over each new shape; `SessionHomeFiles`' inventory and its kept names; `QuestSyncTests` (nothing new on the wire); `SessionStoreTests` (the field null for a record from before and for a mirror); every word of a retired file on `said` before it goes |
| **1z-vi** to **1z-xiv** the store's other families | one family per row, §13.6's table: the accounts, the agents' wiring, the course's marks and presses, the workplace's records, the work records, the rules, Ask Daoris's links, the service's boxes, the CLI's own files | each its §13.6 proof |
| **1mod-i** to **1mod-xviii** the driver's moves (§14.6) | one subdomain per row, bottom up, its files moved to its folder and namespace, no behaviour changed; the cuts of each one's upward edges after it, one edge family per row | the gates of the brief §6 whole; `StructureTests`' census shorter by the moved subdomain's files and edges |

1a-i is first because it removes the most defects for the least change: one pure function and the places that read it remove survey §2.4 rows 1,
11 and 13's account half, the class of LIMITSWITCH1, LIMITRESUME1b, LIMITRESUME1c and LIMITGOON1 (survey §2.6), and build a decision the owner already
made; its provenance and its briefed move are rows of their own (O-10), and round 3 moves the closed quest's case into a-iii, after the walk
it needs (O2-5). 1b is a day at the common boundary where three losses sit. 1c proves the participant on the defects it came from (CAPCOUNT1,
ASKRAIL1, SESSBADGE1) before anything is admitted by it. mod-0 comes next so every new file of the rows after it is written in its subdomain, and
z-i and z-iii before a-ii, whose provenance is a binding in the store. 1p comes before 1d and 1f so their proofs stand on acknowledged attempts and
receipts; 1o after 1h and 1i so the course's `Never` holds the old part before a word is kept there; z-ii and z-iv before w, whose catalogue and
grants are the store's.

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
- **An older build's continuation of a record started on an added role**: answered by round 3 (§2.7: `actor` keeps the seeded id, O2-3), proven by
  row x's first case.
- **The store's cost** (round 3): a write's latency with `synchronous = FULL` and write-ahead logging, the busy waits between the shell, a headless
  loop and the verbs, the wake file's latency against today's file watches, and the time of each family's import on the install's home: row z-i
  and each family's row measure them. How the window kit's single-instance guard names itself is *unverified* (§13.4); the claim rests on the
  lease's way of telling a live holder, which is read.
- **The map's census**: the edge counts of §14.1 are a measurement by type names with comments and strings removed, a lower bound of the
  dependencies (a type named only in a string or by reflection is missed); row mod-0's census is the exact list.
- **The defects' classes** (survey §6): a judgement, which this order inherits.
- **The checks.** A document only: `doc-links`, `doc-system`, `doc-shapes`, `doc-duplicates`, `doc-budgets` and `orient-index --check` check its
  place, its links and the index, never its claims. The code was read, not run, on main at `92ac2bd7e` and `c8eccf30e` (one `src/`), at `2a08bde4a`
  for round 3 (whose `src/` is round 2's `29a066182`), and LIMITSWITCH1 on its branch at `3fdc6bbeb` and `adacd5fdc`, merged at `a85004bd0`;
  LIMITSWITCH2 is building and was not read; EVENTHOOK1, COWORKVIEW1's routing half and SWITCHCTX1's carry are proposed, not built, and what this
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

## 13. Where state is kept: the desktop store

The owner's direction of 2026-10-25, after round 2 (the brief §9): as configuration and state grow complex they move from JSON files to
SQLite. The line between the two is the steward's, by the owner's leave (the brief §9): **SQLite holds what Daoris owns and more than one
reader or writer reads and sets; JSON stays only where something outside Daoris owns the format, where the file is a repository's reviewed
record, where it is read before the store opens, or where it is an export or a stream.** This section is the fourth area of the design, and
it is where round 3 makes the move's fencing and rollback choices Codex asked for (X2-1.1, X2-3.1). The inventory below was read at
`2a08bde4a`, every file's writers and readers by `file:line`.

### 13.1 Today

- **The desktop opens no SQLite.** No project under `src/Daoris.Desktop` references `Microsoft.Data.Sqlite`; the service does (10.0.12,
  `src/Daoris.Service/Directory.Packages.props:30`), and its one database, `data/knowledge.db` (`Core/Platform/DaorisHome.cs:338`), is already a
  file several processes open: the HTTP host and every session's connector (`Http/Program.cs:90-91`; `Mcp/Program.cs:78-79`), its index stamped
  by `user_version` and its quests by their own `quest_store_version` (`Core/Quests/Quests.cs:716-720`, `:941-965`; `compatible-builds.md`).
- **About sixty JSON files and families** sit in the home's `data/`, `profiles/` and `run/`, written by the driver library in the shell and in
  `daoris-driver`, by the modules in the shell, by the service and its connectors, by the GitDoor (the git launcher and remote helper, D198,
  which compiles fourteen of the driver library's files, `Daoris.Desktop.GitDoor.csproj:20-37`), by the launcher, and by the Node CLI. They are guarded three ways: a
  `KeepLock` sidecar (`<file>.lock`, `Driver/Platform/KeepLock.cs:17-38`, which every `JsonKeeping.Save` takes, `JsonKeeping.cs:72`), a
  `RecordLock` held handle (`run/locks/records/<name>.lock`, `RecordLock.cs:38-67`), or an in-process lock alone, so that `data/windows.json`,
  `data/reads.json`, `data/usage.json` and `data/sessions/held-words.json` can race between the shell and a `daoris-driver` (`AccountWindows.cs:98`;
  `AccountReads.cs:104`; `Accounts/Usage.cs:90`, `:169`; `Modules/Loop/SessionWords.cs:916` beside `SessionHomeFiles.cs:333-368`).
- **`data/driver.json` has many writers and no version stamp**: `DriverConfig.Save` under its `KeepLock` (`DriverConfig.cs:1009-1028`), reached
  from fourteen sites of `DriverChoicesCommand`, the shell's `DriverContext.Change` (`Modules/Bridge/DriverContext.cs:149-151`), the steward's
  acts (`StewardActRows.cs:418`, `:432`) and a dozen more, and a raw writer for the git pointers (`GitHostKeys.cs:75-98`); read on every tick
  (`DriverWatch.cs:1118`), by the headless host (`Host/Program.cs:864-865`) and by the GitDoor (`GitIdentityUse.cs:80-86`). An older build reads
  it by keeping keys it does not know (`DriverConfig.cs:918-928`, `:1057-1063`, held by `DriverConfigKeepsTests`).
- **Cross-process wakes are file watches**: `HomeWatch` watches `driver.json`, `cooling.json`, `harnesses.json`, `held-words.json`,
  `lines-moved.json`, the opinion gates, the notices and the proposals (`Driver/Platform/HomeWatch.cs:192-232`).
- **The home's lease is a file naming its holder** (`run/home.lease`: kind, pid, the process's start time, the home's id;
  `Driver/Platform/HomeLease.cs:307-320`), taken by a create-only move so one of two racing takers lands (`:337-343`), stale only once its pid is
  dead or belongs to a process started at another time (`:66-68`, `:165-173`), released only by the holder whose text it is (`:63`). The shell
  waits on it (`Modules/Loop/DriverLoop.cs:841`, `:1270-1310`), the headless loop takes or shares it (`Host/Program.cs:868-885`), and a verb
  beside a live instance holds nothing (`HomeLease.ForVerb`, `:219-225`).
- **The Node CLI** spawns `daoris-driver` for its management verbs (`src/Daoris.Cli/src/driverprogram.ts:27-30`, `:90-101`) and writes no
  `driver.json` since PUSH1f2b (`jsonkeep.ts:8`, `:68`); it reads `run/running.json` and `data/home.json` and writes `data/browser/settings.json`,
  `data/browser/favorites.json` and `profiles/remotes.json` itself (`service.ts:115`; `home.ts:393`; `browser.ts:80-216`; `remotemap.ts:143-185`).

### 13.2 The store and its owner

- **One file**, `data/desktop.db`, a row of D172's table of the home's entries, which has three twins (`Core/Platform/DaorisHome.cs:317-411`,
  `Driver/Platform/DaorisHome.cs`, the CLI's `home.ts:98`; `twins.md`), changed together in row z-i.
- **Its owner is the driver library**: one class, `DesktopStore`, in the Store subdomain (§14), is the only code that opens the file, and every
  family's table is read and written by the subdomain that owns it (§13.3) through it. It is machine-local and never synced: no table travels,
  no shared host opens it (no shared host runs the driver), and no route answers its rows but through the judges that own them.
- **The service keeps its own store** for sessions, quests, asks, the channel and the event journal, and never opens the desktop's; what the
  service needs of the desktop's state reaches it as today, over its HTTP API from the driver. **One field this design adds stays on the
  service's record**, `opens` (§3.3), since a destination's uniqueness must be the open's own statement in the service's exclusive step.
- **Who opens it**: the shell (the modules and the driver library in one process); `daoris-driver`, as the headless loop and as each terminal
  verb, the Node CLI's management verbs reaching it by spawning `daoris-driver` as they do today; and the GitDoor, read-only, for the families it
  reads today (the git pointers, the permissions, the tools, the git identities), through the same class compiled into it as `GitIdentityUse`
  is. The service, its connectors, the launcher and the Node CLI never open it. The CLI's doctrine commands read no store at all.
- **Kept out of it, by the line**: the formats other tools own (an agent's `settings.json`, `.mcp.json` and `.claude.json`, the rooms'
  `.claude/settings.json`, the window kit's `window-state.json`, plugin and tool manifests); the tracked repository records (`daoris.json`,
  `daoris.lock`, `daoris.gates.json`, the lanes file); the install's manifests (`update/build.json`, `swap.json`); the files read before the store
  opens or by processes that never open it (`run/home.lease` and its older places, `run/running.json`, `data/home.json`, `data/update.json`, which
  the launcher reads, `run/home-move.json`, every lock under `run/locks/`); the streams (`.jsonl`: a session's events and lineage, the usage
  history's readings and turns, the machine log, the pipe's journal); **the credentials** in `profiles/` (`profiles/keys.json`, the keys in
  `profiles/remotes.json`), which stay in their own home so no export of the configuration can carry one; the files the Node CLI writes itself
  (`data/browser/*`, `profiles/remotes.json`) until its browser verbs reach them through `daoris-driver`, row z-xiv; and **`driver.json`
  itself**, which becomes the configuration's export and import format alone (§13.5).

### 13.3 The schema's shape

Each family's tables belong to one subdomain (§14), never to two. Every column a writer always writes is required; a column is added nullable
only for rows an older store-aware build may keep writing; a column is never repurposed; new meaning goes into a new table
(`compatible-builds.md`). Rows never hold a word's text, an account's key or a credential.

| Family | Tables | Subdomain | Replaces |
|---|---|---|---|
| The store's own | `store_version` (the schema's version and the oldest build that may write it); `store_revisions` (one revision per family, moved in the writing transaction); `imports` (each file imported: family, its path in the home, size and SHA-256, moment, build, rows, class) | Store | — |
| Settings | `settings` (one row per key of today's `driver.json`, its value the key's JSON shape, read by today's one parser, `DriverConfig.Parse`, key by key, and every key this build does not know kept as it is, as `JsonKeeping` keeps it today) | Settings | `data/driver.json` |
| The role catalogue | `roles`, `role_revisions` (every revision of every role, never rewritten), `role_staffing` (each working role's ordered choices: the seeded eight's from `driver.json`'s `actors` key at the import, an added role's from its record) | Roles | `driver.json`'s `actors`; round 2's `roles` key |
| Appointments | `appointments` (each with its grant: the acts ticked and its scope, its generation, its window), the standing confirmation's own records | Roles | `data/standing-confirmation.json` |
| Bindings | `bindings` (one per session record of this machine: the kept conversation's id and door; the account by profile name and whether the person picked it, row a-ii; the role taken and its revision, row x; the process generation) | Participants | `data/sessions/<s>.harness.json` |
| Accounts | `account_cooling`, `account_windows`, `account_reads`, `account_names`, `usage`, `usage_epochs` | Agents | `data/cooling.json`, `windows.json`, `reads.json`, `accounts.json`, `usage.json`, `usage-history/epochs.json` |
| Agents' wiring | `agent_offers`, `harness_wiring`, `tools` | Agents (`tools` Install's) | `data/agent-offers.json`, `harnesses.json`, `tools.json` |
| The course | `moves` (§3.3: the executor kind, the claim's run and generation, the phase and each moment, the effect mark, the selection, the recipe, why, by, how), `move_words` (each word in transfer: its id, producer key, transfer id, order and disposition), `word_receipts` (by line and producer key, §3.4 rule 5), `go_on_marks`, `reopen_marks`, `go_on_new` (the person's press), `handed_quests` | Course | `<s>.cannot.json`, `.reopened.json`, `.new-session.json`, `data/handed/<quest>.json`; the boxes `<s>.limit-move.json` and `.hand-over.json`, imported and no longer written |
| News | `notices`, `news_said` (the said-set, §4.5), `journal_cursors` (one per teller) | Events | `data/notices.json` |
| The workplace | `keeping_runs`, `landed_branches`, `session_branches`, `abandoned`, `auto_landings`, `lines_moved`, `registry_followed` | Workplace | `data/keeping.json`, `landings.json`, `session-branches.json`, `abandoned.json`, `sessions/auto-landings.json`, `lines-moved.json`, `registry-followed.json` |
| Work records | `workflow_plans`, `workflow_runs`, `workflow_units`, `workflow_resources`, `workflow_closures`, `chat_looks`; `setup_plans`; `opinion_gates`, `opinion_dues` | Workflows; Setup; Review | `data/workflows/*`; `data/setup/<workspace>.json`; `data/opinions/gates/*`, `opinions/due.json` |
| Rules | `permissions`, `configured_access`; `git_identities`; `plugin_state`, `plugin_approvals` | Participants (admission's rules); Workplace (git); Plugins | `data/permissions.json`, `configured-access.json`; `git-identities.json`; `plugins.json`, `plugin-approvals.json` |
| Ask Daoris's links; the archive | `help_links` (its `.help.json`, `.wired.json`, `.published.json`); `session_archive` | Help; Workplace (history) | `data/sessions/<s>.help.json`, `.wired.json`, `.published.json`; `sessions/archived.json` |

Each family is migrated by its own row (§13.6). **Migrations** are numbered steps in code, forward only, each in one exclusive transaction:
the first process to open a store older than its build migrates it while the others wait on the write lock; a crash mid-step leaves the store
as it was, since SQLite's DDL is transactional. `store_version` is never lowered. A build opening a store whose oldest-writer stamp is past its
own reads the tables it knows and refuses every write, saying the store was written by a newer Daoris and that it should update, as the quest
store refuses a take's bind (QUESTSTOREVER1); a build older than a table never opens it.

### 13.4 Transactions and processes

- **One connection per process** behind a gate, as the service's `ConnectionGate` keeps its own (SQLITETX1); write-ahead logging, so a reader
  never waits on the writer; `synchronous = FULL`, since a claim or a receipt lost to a power cut could repeat an effect, at a write rate low
  enough for it, measured in row z-i; a busy wait of five seconds, then a refusal said in the process's own words.
- **Every write is one transaction under the write lock, taken at its start** (`BEGIN IMMEDIATE`), so a read, a check and a write never
  interleave across processes, which round 2's file claim could not promise (O2-12). No transaction spans an await on another process or an
  external effect: an effect is a mark's transaction, the effect, and an outcome's transaction.
- **A move, its words and their receipts in one transaction; its open claimed once** (the brief §9; X2-3.1). The move's protocol:
  1. *Claim.* The executor writes the move in one transaction: its phase, its claim (the run of the process that holds the move's executor
     role, below, and a generation) and the `opens` key it will open under.
  2. *Every external effect is fenced by a mark.* Before a spawn, a prompt or a copy, a transaction checks the claim (the row's run and
     generation are the writer's, and the writer still holds its role) and writes the effect's mark; then the effect; then a transaction writes
     its outcome. A mark whose transaction fails stops the executor before the effect.
  3. *The open is the service's, idempotent.* The open under the `opens` key moves the old part's words in the service's one step (§3.4 rule 3);
     the next transaction writes `opened`, the new record's id, every word the open moved (`move_words`) and the phase, together.
  4. *Delivery.* The prompt's mark (step 2) and the service's acknowledged attempt (§3.4 rule 5); then one transaction writes each word's
     disposition, its receipt and the phase `delivered`; and only then is each word taken off `said`, a take a crash leaves undone finished at the
     come-up by its receipt.
- **The claim's holder is the holder of the executor's role.** A conversation's move is the shell's, and the shell's role is the instance it
  is: one shell per home (the window kit's single-instance guard, `App/Program.cs:368-372`, *unverified* how it names itself; and the host's
  start claim, a named mutex, `HostSupervisor.cs:480-505`). A driven line's move is the lease holder's (`run/home.lease`). Each process writes
  its run (a random id drawn at its start, with its pid and start time, the lease's own way of telling a live holder, `HomeLease.cs:165-173`) as
  the role's holder when it takes the role. **A move is taken over only by its role's current holder, of a move whose run is not that holder's
  run**; so a live executor, however long it pauses, is never taken over while it holds its role. Where a role passes while its old holder
  lives (a lease given up or shared, `Host/Program.cs:868-885`), the old holder's next mark fails its claim check, so it begins no effect after
  the takeover; an effect it had begun, marked and never recorded, is the new holder's `held`, named and never repeated by itself (X). A paused
  executor resuming after a takeover therefore finds its next mark refused, ends what it started where it can (a spawned harness before its
  first prompt), and says so; row f-i proves it with a paused executor, not only two writes.
- **Wakes.** A write moves its family's revision in `store_revisions` in the same transaction, and after its commit touches the store's wake
  file, `data/desktop.db.wake`, which holds nothing; `HomeWatch` watches that one file where it watches `driver.json`, `cooling.json`,
  `harnesses.json`, `held-words.json`, `lines-moved.json`, the gates and the notices today, reads the revisions, and wakes exactly the families
  that moved. The look's period is the fallback where a watch drops an event, as it is today.
- **The GitDoor** opens the file read-only and reads its families' tables at each git act, never waiting on a writer under write-ahead
  logging; it writes nothing there, as it writes none of those files today.

### 13.5 Older builds, the rollback window and the export

- **An import's timing keeps D139's rollback whole.** The launcher rolls a new build back only before it writes `confirmed`, which it does once
  composed (D139; `docs/2026-10-03-update-when-idle-design.md` §3 step 5, §5); a build that confirmed is never rolled back by itself. So a family
  an older build only *reads* (a configuration or a cache) is imported at the first start of the build that moves it and **left in place as the
  backup**; a family an older build would *act on* (a box it recovers, a press it performs, a mark that gates a go-on, a record a landing or a
  removal follows) is imported only after that build has confirmed, then **set aside** under its import's receipt (`<file>.imported`, never
  deleted), and its readers take it up only after its import. A rollback before the confirmation therefore meets every file as it was.
- **After the move, no file is written through.** A file left in place is the configuration or state as it stood at its import; an older build
  run later on this home (the person installing an older build, or a stage rolled back) reads it and writes it as it always did. When the newer
  build comes back, it compares each left file with its import's SHA-256: a configuration changed since is **offered as an import**, through the
  same judges and, for a change of authority, the person's own press, never applied by itself; a state family changed since is merged by its
  family's rule (a cool-off by the later `until`, a notice by its id, a landing record by its branch). A set-aside family whose file an older
  build wrote anew (a box, a press) is imported once and set aside again. Nothing is lost on a rollback: the store keeps what the newer build
  wrote, the files what the older one did.
- **Store-aware builds.** A build that knows the store reads and writes the tables it knows and never opens a newer one; one opening a store
  stamped past its writing refuses to write and says so (§13.3).
- **The export and import of the configuration** (the brief §9). `daoris driver config export` and Settings → *Export* write the settings, the
  role catalogue's records and the appointments' grants as readable JSON in `driver.json`'s shape (its keys as today, `actors` for the seeded
  roles' staffing, `roles` for the rest), to review, back up or edit by hand; `daoris driver config import` and Settings → *Import* read one back
  through every judge a door uses (D50, D110: the window, the terminal and Ask Daoris's proposal reach the same judges), each key judged as its
  own door judges it, and a change of authority (a role's acts, an appointment's grant, `access`, `drivable`, `trees`, `standing`) held for the
  person's own press in the window. The first start of the build that moves `driver.json` imports today's file once, as it stands, without a
  press: it is the configuration the person already set, granting what it already granted. `DAORIS_DRIVER_CONFIG` names a file imported into a
  store of its own, as the tests seed a configuration today (`DriverConfig.cs:846`).
- **A move in flight across a downgrade** (X2-1.1; O2-14). A move the store carries writes no old box, so an older build recovers it from the
  ledger alone, as it recovers today a move that keeps no box (the live limit move, the curve, a driven carry-on), and the newer build, back,
  reads the evidence it left before acting:

| Phase at the downgrade | The ledger shows | An older build | The newer build, back |
|---|---|---|---|
| `decided`, `quiescing`, `old-ended` | the old part as it was, its words on `said` | settles the old part as after any crash: an ended record's words go on by its own rules, resumed or held for its account; nothing opened | a later run on the old part, or a record `from` it under no key of the move's, closes the move as superseded; else it goes on from its phase |
| `opening` | nothing new (the open not made), or the new record under the move's key with the words already moved (the answer lost) | without the open, as above; with it, the new record is the line's head and its words wait there: its come-up resumes it where its conversation exists, else takes it as lost and briefs it (today's path), skipping the old part, which went on (`ChatGoOn.cs:228`) | asks the open again under its key, is answered the record and the words, and reconciles as below |
| `opened`, `copied`, `verified` | the new record with the moved words, no prompt yet | as the open's second case: each moved word goes once, from the new record | the moved words taken by an older build's turn make the move `delivered` by that evidence; else it delivers |
| `delivering` | the new record, its words marked attempted or not | settles attempted words as uncertain, never sent again (`GoOn/UncertainWords.cs:25-94`), and goes on with the rest | the begun effect is `held` for the person unless a turn's events and the words' takes show it delivered |
| `delivered`, `held`, `fell-back` | an ordinary line, or one held for the person with its reason | as any such line | closes or keeps the hold |

No phase leaves a box an older build could execute, so no move is made twice across builds; the price, said here, is that an older build
briefs a part the newer one opened by its own lost-conversation path rather than the move's recipe. Against an older *host*, which takes no
`opens` and moves no words, a move is made as today's are, a lost open held for the person (round 2's X-3.5).

### 13.6 The migration rows, one family each

Each row moves every writer and every reader of its family through its subdomain's seam onto the store, imports the family once (§13.5), and
proves an older build's own reads and writes over what it leaves (`compatible-builds.md`). The gates of the brief §6 stay green in each.

| Row | Files | Into | Class (§13.5) | Its proof beyond the gates |
|---|---|---|---|---|
| **z-i** the store, read by nothing | — | `data/desktop.db`, `DesktopStore`, `store_version`, `store_revisions`, `imports`, the wake file, the GitDoor's read-only open | — | the three home tables' twins equal; the shell, a headless loop, a verb and the GitDoor opening one store at once; a store stamped ahead refused for writes and said; a migration interrupted at each step leaving the store as it was; write latency with `FULL` on the install |
| **z-ii** the configuration | `data/driver.json` | `settings`, `role_staffing`; the export and import | left in place | every key round-tripped through the import and the export (`DriverConfigKeepsTests`' fixture whole, every writer of the explorer's census, `DriverConfig.cs:1066-1180`, and the raw git pointers); an import widening authority held for the press; an older build reading the backup; a changed backup offered at the return |
| **z-iii** the bindings | `data/sessions/<s>.harness.json` | `bindings` | left in place (a record's conversation id never changes) | every resume reads its id from the binding; a record deleted takes its binding |
| **z-iv** the appointments | `data/standing-confirmation.json` | `appointments`, the standing confirmation's records | left in place (an appointment found at a start stays dormant, D209 points 1, 2 and 4) | `StandingConfirmationTests`, `PipeCensusTests` whole |
| **z-v** the notices | `data/notices.json` (its unreadable copies stay files, as evidence) | `notices` | left in place | `NoticesStateSlice`'s and `LookSaid`'s suites; two tellers in two processes |
| **z-vi** the accounts | `data/cooling.json`, `windows.json`, `reads.json`, `accounts.json`, `usage.json`, `usage-history/epochs.json` | the accounts' tables | left in place | the shell and a `daoris-driver` writing the windows and usage at once, lost by neither (today's race); `AccountRotationTickTests`, `ResumeAccountTests` |
| **z-vii** the agents' wiring | `data/agent-offers.json`, `harnesses.json`, `tools.json` | `agent_offers`, `harness_wiring`, `tools` | left in place | the GitDoor reading `tools` |
| **z-viii** the course's marks and presses | `<s>.cannot.json`, `<s>.reopened.json`, `<s>.new-session.json`, `data/handed/<quest>.json` | `go_on_marks`, `reopen_marks`, `go_on_new`, `handed_quests` | set aside | a press taken once across a restart and a downgrade |
| **z-ix** the workplace's records | `data/keeping.json`, `landings.json`, `session-branches.json`, `abandoned.json`, `sessions/auto-landings.json`, `lines-moved.json`, `registry-followed.json` | the workplace's tables | set aside (an older build lands, removes or follows from them) | KEEPSIZE1's removal proofs whole; a due landing made once |
| **z-x** the work records | `data/workflows/*`, `data/setup/<workspace>.json`, `data/opinions/gates/*`, `data/opinions/due.json` | the work records' tables | set aside | the workflow and opinion suites whole |
| **z-xi** the rules | `data/permissions.json`, `configured-access.json`, `git-identities.json`, `plugins.json`, `plugin-approvals.json` | the rules' tables | left in place | the GitDoor reading `permissions` and `git_identities`; an import widening a rule held for the press |
| **z-xii** Ask Daoris's links | `<s>.help.json`, `.wired.json`, `.published.json`, `data/sessions/archived.json` | `help_links`, `session_archive` | left in place | `HelpCoverageTests` whole |
| **z-xiii** the service's boxes, into the service's store | the service's halves of `<s>.question.json`, `.hand-over-ask.json`, `data/proposals/<id>.json`, `data/help/proposals/<id>.json`, the helpers', partners' and brainstorms' boxes, `run/requests/*`; their driver halves (`.hand-over-state.json`, a helper's `state`, `capture` and `back`, a partner's `state`, `facts` and `delivery`, a brainstorm's `state`, `stop` and turns) into the desktop store | the service's and the desktop's tables, one row per box family, designed with the service's owners | set aside | each box family's own suite, and its connector built before the row still answered |
| **z-xiv** the CLI's own files | `data/browser/settings.json`, `data/browser/favorites.json` | `browser_settings`, `browser_favorites`, once the CLI's browser verbs spawn `daoris-driver` as its other management verbs do | left in place | the CLI's browser verbs' tests over the driver |

**What stays a file, and why** (§13.2): the backups the imports leave in place or set aside (§13.5), kept until row v; `data/sessions/held-words.json`,
which row o retires by keeping its words on `said` rather than moving them; `data/pipe-waiting/<id>.json`, written and read by the shell
alone (`PipeWaiting.cs:55`, `:107`), so outside the line until a second process reads it; `<s>.supervisor.json` with its `.decisions/`, a role's
decisions kept with the session's history, which row t's one act record takes up rather than a row of its own; the credentials, the formats
other tools own, the manifests, the pre-store files and the streams. The inventory also found three session files the session's file
inventory misses, so its delete and clear do not take them (`<s>.limit-move.json`, `.hand-over-ask.json`, `.hand-over-state.json`, absent from
`SessionHomeFiles.Inventory`, `SessionHomeFiles.cs:109-141`; *unverified* that nothing else does): filed as SESSFILES1 in `TASKS.md`, since it
is a defect today whatever the store does.

### 13.7 Rejected

| Rejected | Why |
|---|---|
| The desktop's tables in the service's database, or one store for both | The service's database is the service's (D46, D47 §4), opened by every connector; a second owner of one file would make its schema two components' and a shared host's store carry desktop state |
| The service reading the desktop store (for receipts or bindings) | The service stays blind to the desktop's state; every keyed producer reaches the say door through the desktop's client, which reads the receipts (§3.4 rule 5) |
| A store per family | The one transaction across a move, its words and their receipts, and the one wake, would be lost |
| `driver.json` written through beside the store, or read live | Two homes of one setting; and an older build's later write would be silently overruled or silently win |
| Every file kept as a JSON blob row | Leaves each file's parser and loses typed reads for the Fabric's records; only the settings keep `driver.json`'s key shapes, so its one parser and the export stay one |
| `said` in the desktop store | The words are the service's record (D137 point 6), read by the say door inside its exclusive step |
| The Node CLI opening SQLite (`node:sqlite`) | Its doctrine commands keep no runtime dependency and the packaged CLI supports Node from 22; its management verbs reach the store through `daoris-driver` |
| Credentials in the store | Their home is `profiles/`, and an export must never carry one |
| A version stamp on `driver.json` to warn an older build | A stamp binds only the builds that read it (`compatible-builds.md`); no build before it would |
| Importing every family at the first start | A family an older build acts on would meet a rollback before the confirmation already moved; such a family waits for the confirmation (§13.5) |

## 14. The driver's subdomains: its module map

The owner's direction of 2026-10-25, after round 2 (the brief §10): the full update also analyzes the driver into its subdomains, among them
the context engine, the memory and knowledge system, the Fabric and the actors. The owner confirmed **Roles** as one subdomain's name and left
the map's other names and grouping to the steward; **the map below is the steward's decision by the owner's leave**, each choice over the
steward's draft (Company, Fabric, Course, Context engine, Knowledge, Events, Agents and accounts, Work, Workplace, Store, Platform, Loop) given
its reason in a line. Read at `2a08bde4a`: `Driver/` is 784 files and 231,407 lines, every file in one namespace, `Daoris.Driver` (the project's
`RootNamespace`), so namespaces follow nothing.

### 14.1 What the code says today

- **Fifteen folders cut by technique**: Trees 49,071 lines (its `Nodes/` 11,039 of them the workflow runtime, its `SessionTrees/` 17,581),
  Platform 26,019 in one flat folder of 86 files, Sessions 24,727, Conversations 20,409, Agents 19,062, Accounts 15,188, Loop 14,558, Help
  12,935, Plugins 10,854, Workflows 8,893, Service 7,442, Review 6,701, Quests 6,552, Setup 6,034 (install and update among it), Brainstorms 2,961.
- **One cycle.** Measured by which folder's type names each folder's files use (comments and strings removed), 59 of the 105 pairs of folders
  depend on each other both ways, and all fourteen folders but Loop form one strongly connected cycle: Sessions and Conversations (9 files one
  way, 35 the other), Accounts and Agents (5 and 12), Trees and Platform (96 and 24), Trees and Sessions (19 and 26).
- **Platform is no leaf**: it names types of fourteen folders, `DriverConfig` of nine and `DriverChoicesCommand` of ten, `DaorisHome` of six,
  `NoteCodes` eight of Conversations'; 47 of its files name nothing outside it (`AtomicFile`, `Moments`, `MachineLog`, `Chain`, …).
- **Loop holds rules**: `LimitResume.NotLifted` (`Loop/LimitResume.cs:74-83`), `SupervisorQuarantine.Unhandable` (`:46`), `OpinionLook`'s
  `Concluded`, `MadeSince` and `HoldsPush` (`:19`, `:127`, `:134`), `DriverWatch.Due` (`:883`), `Driver.OnAnotherAccount` (`Loop/Driver.cs:4581`),
  `DriverHelpers.LeadWaits` and `Wait` (`:428`, `:547`), `DriverContinuations.HandedToResume` (`:966`), `ReopenTelling.BringsToSource` (`:357`).
- **`Domain/` does I/O through the driver's own types**, which the pure rule's tokens cannot see: `Quests/Domain/AskWords.cs:87` reads through
  `ServiceClient`, `Sessions/Domain/DrivenAccess.cs:68`, `:184` through `SessionProcesses` and `ServiceClient`, `Sessions/Domain/ActorRun.cs:169-178`
  holds a `MachineLog`, `Review/Domain/ReviewerChoice.cs:222`, `:369` walks `HarnessRoster`, `Plugins/Domain/PluginCatalog.cs:137` reads `DaorisHome`;
  nine `Domain/` files hold an `async Task`.
- **What the gates hold**: `StructureTests` (`Driver.Tests/StructureTests.cs:4-9`, its map `fixtures/structure.json`, its rules
  `StructureCheck.cs`) keeps every source in its module's folder, only `Domain` as a layer folder, a `Domain` file free of I/O tokens
  (`StructureCheck.cs:71-85`, `:140-149`), each listed test under its module's test folder (`:183-213`), and one direction alone: nothing outside
  Loop names a type declared in Loop (`:216-217`). The map must name exactly the fifteen modules (`StructureTests.cs:185-202`). Source-scan
  tests find files by name (`SourceFiles.Driver("DriverConfig.cs")`, `Platform/ConfigSeamTests.cs:109`), so a file's move breaks none of them.
- **The draft against the code.** It had no home for six parts the code holds: the settings (`DriverConfig` named in 165 files of every folder,
  22 of them `Domain/`), the terminal's and pipe's doors (53 `*Command.cs` files in thirteen folders, 18,125 lines, and the instance and lease
  group, 2,235), the local service's client (`ServiceClient`, named in 102 files of thirteen folders) with remote sync and registration
  following, Ask Daoris and its proposals (about 8,600), the plugins (10,854) and install and update with the managed tools (about 6,400). Its
  *Fabric* names what the code's `FABRIC-*` tags (about 1,700, 79 of them in Trees) and the company concept call the workflows, their graphs and
  their pipelines (`docs/2026-10-21-company-concept.md:28`), and holds about 2,000 scattered lines; its *Work* is about 55,000 lines; its Store
  is seven Platform files and 27 per-domain `*Files.cs` and `*Record(s).cs` stores, 118 files calling the file primitives directly.

### 14.2 The map

Twenty-one subdomains, each a folder and a namespace, `Daoris.Driver.<Name>`, listed **bottom to top: a subdomain names only its own types and
those of the subdomains listed before it**, so the graph is acyclic by its order. *Takes* names today's folders and
files (`Platform/` file by file, the rest by folder, a file named where its folder splits); *Owns* its store tables (§13.3); *Seam* the types the
rest may name; *Tests* its test folder, which mirrors it (`Driver.Tests/<Name>/`).

| # | Subdomain, over the draft | Takes | Owns | Seam | Tests |
|---|---|---|---|---|---|
| 1 | **Platform** (the draft's) | `Platform/`'s `DaorisHome`, `HomeIdentity`, `HomeLease`, `ChildProcess`, `CommandPresence`, `ProcessStarts`, `AgentStarts` (the launcher's containment, D201), `DriverHttp`, `Downloads`, `DriverException`, `Moments`, `TextBounds`, `SafeNames`; `Agents/Consoles/`'s process job and pseudo-console | — (the home's table of entries, D172, stays its, naming paths, never another subdomain's type) | the home, the lease, a process start, the clock | `ProcessStartsCensusTests`, `NoConsoleWindowTests` |
| 2 | **Store** (the draft's) | `AtomicFile`, `Json`, `JsonFile`, `JsonKeeping`, `KeepLock`, `RecordList`, `RecordLock`; `DesktopStore` and its migrations (§13) | `store_version`, `store_revisions`, `imports` | `DesktopStore`, a family's transaction, its revision and wake | the store's suite (row z-i) |
| 3 | **Settings** (new: the draft had none, and 165 files read it) | `DriverConfig`, `DriverConfigUnreadableException`, `StandingChoices`; the settings' writers' seam; the export and import | `settings` | a key's shape read through one parser; a subdomain reads its own keys, its rule types its own, so Settings names none of theirs (§14.3) | `ConfigSeamTests`, `DriverConfigKeepsTests` |
| 4 | **Service** (new: the client of the local service is named by 102 files) | `Service/` (`ServiceClient`, the host's locator and doors, `LedgerChanges`, remote sync, registration following), `Platform/RunningAddress`; `Trees/SessionGroups` (the service's session records as groups) | — | `ServiceClient`, the change stream's families | the service client's and sync's suites, `QuestSyncTests` |
| 5 | **Install** (new: install, update and the managed tools, from Setup and Agents) | `Setup/`'s `InstallSwap`, `StagedBuild`, release channels and downloads, `UpdateCommand`; `Agents/ToolInstall/`, `Agents/Tools.cs` | `tools` | a tool's path, the staged build | the install's and tools' suites |
| 6 | **Instance** (new: the terminal's way into a running instance) | `InstanceClient`, `InstanceDoor`, `InstanceRoutes`, `PipeName`, `SentPresses`, `VerbLeases`, `ShellWord` | — | the pipe's client and a verb's lease; each verb (`*Command.cs`) stays in its own subdomain and reaches the instance through this | the pipe's census in the modules (`PipeCensusTests`) and the driver's client tests |
| 7 | **Events** (the draft's) | `Chain`, `ChangeWatch`, `FolderWatcherSource`, `HomeWatch`, `HomeWrites`, `MachineFacts`, `MachineLog`, `MachineLogReader`, `Notices`, `NoticesRead`, `LookSaid`, `NoteCodes` (the vocabulary, the eight Conversations codes it names moved into it as codes); `Sessions/Events/SessionLog`, `RunningCalls`, `Sessions/Watch/`; `Quests/Domain/Attention` (the attention watch, its policy); the journal's readers and the hubs (§4) | `notices`, `news_said`, `journal_cursors` | `EventCodes`, `NoteCodes`, a hub's teller, the notices' submit, the machine log | `AttentionTests`, `LookSaidTests`, `NoteCodesTests`, the notices' suites |
| 8 | **Knowledge** (the draft's *knowledge*, apart from the context engine: it answers across repositories from the index and the records, where the context engine composes one line's) | `Service/KnowledgeConnector` (the connector handed to sessions); `Agents/RepositoryKnowledge/` (the code map, indexes, standing answers); `Help/`'s `HelpFacts`, `Room/` and the room's sections, `HelpCheckouts`, `HelpCircle`; `Sessions/Trace/`, `Sessions/Domain/Trace.*`, `TracedRecords` | — | the facts a room or an agent is handed, the trace's records | `HelpCoverageTests`' facts half, the trace's suites |
| 9 | **Agents** (the draft's *agents and accounts*, one subdomain, since an account is an agent's and the two folders depend on each other today) | `Agents/` but what Install, Knowledge and Context take; `Accounts/` but the account rule (to Course) and the instruction composer (to Context); `Sessions/SessionProcesses`, `HarnessEnding`; `Loop/TransientRetries` | the accounts' tables, `agent_offers`, `harness_wiring` | an adapter, a door's start and failure (`AgentErrors.Read`), the roster's walk and resume, cooling, usage | `AcpTransientTests`, `AccountRotationTickTests`, the adapters' suites |
| 10 | **Workplace** (the draft's: trees, git, landing, keeping, named for the place work is done and lands) | `Trees/` but `Nodes/`, `Inventory/` and `SessionGroups`; `Platform/`'s git files (`GitAccessRules` to `LineGuesses`, 22) and keeping files (`KeepChoices` to `BrowserCaches`, 13); `Sessions/History/`; `Loop/FreshTree` | the workplace's tables, `git_identities`, `session_archive` | a session's tree, a landing, a git read, a removal's eligibility, the landing's hold | `SessionTreesTests`, the landing's and keep-size suites, the git half |
| 11 | **Roles** (the owner's name; the draft's *Company*) | `Platform/ActorChoices`, `BuildWalk`; `Sessions/Questions/`'s roles, appointments, supervisor and steward and holder proof; `Sessions/Domain/ActorRun`; `Loop/SupervisorQuarantine`, `OneTurnReask`; the capabilities, the role catalogue and the start plan (§2.4, §2.7) | `roles`, `role_revisions`, `role_staffing`, `appointments` | `Capabilities`, `RoleCatalog`, `StartPlan.Resolve`, an act's admission | `ActorChoicesTests`, `StandingConfirmationTests`' driver half, `PipeCensusTests` |
| 12 | **Participants** (the draft's *Fabric*, renamed: the code's `FABRIC-*` and the company concept's *fabric* are the workflows and pipelines; what this holds is who takes part) | the participant read, its work rule and its census (§2.1, §2.2); `Sessions/ChildLineage`, `RunningSessions`, `Domain/ChildBindings`, the lineage hook; `Conversations/Helpers/`, `Partners/` and their `Domain/` rules, `Chat/ChatRunBinds`; `Sessions/AccessCommand`, `Domain/AccessRules`, `DrivenAccess`; `Loop/DriverHelpers`, `DriverPartners`; admission's start layer (§2.3) | `bindings`, `permissions`, `configured_access` | `Participants.Of`, the work, a binding, the start layer's judgement | `ChildLineageTests`, `HelperLookTests`, the admission's suites |
| 13 | **Context** (the draft's *context engine*) | `Sessions/Events/ContextBriefs`, `SessionEventLog`, `SessionLastWords`, `Domain/SessionOutput`; `Conversations/Domain/ContextBrief`, `TranscriptFile`; `Accounts/InstructionAccount`, `InstructionAccounting` (the instruction composer, misfiled under Accounts); `Agents/Adapters/SessionParts`; the carry's copy and read-back (SWITCHCTX1) and compaction when a row designs it | — (a session's event file stays a stream, §13.2) | a line's brief, a conversation's record, the instruction handed at a start | the briefs' and event log's suites |
| 14 | **Course** (the draft's) | `Course.Decide`, `LineAccount`, the move record (§3); `Accounts/Rotation/ResumeAccount`, `Accounts/Domain/SwitchDecision`; `Conversations/` but the chat runner's core (to Loop), the helpers and partners (to Participants) and the context's files: `DrivenInbox`, `SaidDoor`, `GoOn/`, `HandOver/`, `Chat/`'s `LimitMove`, `CurveMove`, `LimitBox`, `LimitWaits`, `BriefedMove`, `ChatGoOn`, `ChatHandOver`, `Domain/Continuation`, `LimitHeld`, `WordsHeld`, `MessageLevel`; `Sessions/Requests/`; `Loop/DriverContinuations`, `DriverHandOvers`, `LimitResume` | `moves`, `move_words`, `word_receipts`, `go_on_marks`, `reopen_marks`, `go_on_new`, `handed_quests` | `Course.Decide`, `LineAccount.Decide`, a move's protocol, the dispatchers | the brief §6's *how work moves* suites |
| 15 | **Work orders** (from the draft's *Work*, split: quests, asks and the person's orders, apart from review, workflows and set-up, each with records of its own) | `Quests/` but `Domain/Attention`; `Sessions/Work/`, `Questions/QuestionFiles`; `Trees/Inventory/` (the work inventory); `Brainstorms/` (a request the person orders, its voices started as any work, too small for a subdomain); `Loop/DriverIntakes`, `DriverBrainstorms`, `DriverMeetings`, `ChannelReach`, `ReopenTelling` | — | the planner, an intake, a quest's or an ask's verbs | `PlannerTests`, the intake's and brainstorm's suites |
| 16 | **Review** (from *Work*: a second opinion and verification) | `Review/`; `Sessions/Heavy/` (heavy gates and checks); `Loop/`'s five `Opinion*` files | `opinion_gates`, `opinion_dues` | an opinion's gate and its reviewer's choice; a check's verdict | the opinion's and heavy gates' suites |
| 17 | **Plugins** (new: the draft placed only their wire) | `Plugins/` | `plugin_state`, `plugin_approvals` | the hook points, the catalogue, the work wire, `work/event`'s outside projection (§4) | `PluginKitTests`, the hooks' suites |
| 18 | **Workflows** (from *Work*, with the node runtime that sat in Trees) | `Workflows/`; `Trees/Nodes/` | the workflows' tables, `chat_looks` | a run's evaluation, a node's contract | the workflow and node suites, the family rehearsal |
| 19 | **Setup** (from *Work*: a workspace's set-up) | `Setup/` but install and update | `setup_plans` | a set-up's plan and press | the set-up suites |
| 20 | **Help** (new: Ask Daoris, the company concept's front desk, apart from the knowledge it reads) | `Help/` but what Knowledge takes: `HelpConversations`, `HelpMode`, `ConfiguredAccess`'s judge, `HelpSwap`, `HelpWired`, `HelpProposals` and its kinds, `HelpSettings`, `HelpHousekeeping`, `Proposals/` | `help_links` | the room's start, a proposal's judge | `HelpCoverageTests` |
| 21 | **Loop** (the draft's, now both loops) | the look (`Loop/Driver`, `DriverCommand`, `LoopContext`, `LookStages`, `PassAsks`) and the chat runner's core (`Chat/ChatRunner`, `ChatTurns`, `ChatPending`, `StartRequests`, `ChatContext`, `ChatHelp`), which execute the course's answers (§3.1); the composition root; the wiring view (`SystemWiring`, `WiringCommand`, `WiringRedact`, `WiringSizes`, `WiringVariables`), a read of what it composed | — | none: nothing names Loop, as today | the brief §6's loop suites, the rehearsals |

Not in the map: the modules (the shell's own library, held by its own structure gate, `Modules.Tests/StructureTests.cs:66-78`), the host
(`Daoris.Driver.Host`), the GitDoor, the launcher and the piece start, each its own project; the service, whose side of this design (the head,
`opens`, the journal's triggers, the work's twin) lands in its own code. A verb file (`*Command.cs`) belongs to the subdomain whose acts it
drives, never to a subdomain of verbs.

### 14.3 The rules the map keeps

- **Downward only.** A file names only its own subdomain's types and those ranked below it. Where a lower subdomain calls a higher one today
  (the landing calling a plugin's `work/land`, `NoteCodes` naming Conversations' types, `DaorisHome` naming six folders', the settings' parser
  naming nine folders' rule types), the lower declares a port and the higher implements it, wired by Loop; or the type moves down to its one
  owner. Settings in particular reads keys by their shape and hands each subdomain its own keys, whose rule types that subdomain owns, so
  `DriverConfig` names none of them; until its 165 readers move, `DriverConfig` stays the composed record, read by each through a census that
  shrinks one subdomain at a time.
- **Loop holds no rules.** Each judgement it holds today moves to its subdomain (§14.1's list, each with its row); Loop composes and executes.
- **`Domain/` stays pure, and is seen to be.** Beside today's tokens, a `Domain/` file names no type of the I/O seams (`ServiceClient`,
  `SessionProcesses`, `HarnessRoster`, `DaorisHome`, `MachineLog`, `WorkingTree`, `DesktopStore`) and holds no `async`; today's six crossings and
  nine `async` files are the census's named exceptions, each cut by its subdomain's row.
- **No global usings** of a subdomain's namespace, so a file's `using` lines are its dependencies and the census reads them.

### 14.4 Folders and a census, or assemblies

**Folders, namespaces and a census, chosen; assemblies rejected for now.** Assemblies would hold the graph at compile time, but they cannot exist
while the folders form one cycle: every one of the 59 two-way pairs would have to be cut before the first split builds, which is the big-bang
rewrite the brief §7 forbids. They would also turn the tests' one `InternalsVisibleTo` into twenty-one, re-cut the GitDoor, which compiles fourteen
of the driver's files directly (`Daoris.Desktop.GitDoor.csproj:20-37`), and multiply the projects the merge tool's parallel gates build beside each other, whose
shared outputs already had to be kept apart (KEEPSIZE1b3's FIX-LOG entry). A census in `StructureTests` holds the same graph from the first row,
names every edge against it as *not yet cut* with the row that cuts it, and fails on a new one; once a subdomain's edges all point down it can
become an assembly in a row of its own, measured, Store and Platform the first candidates.

### 14.5 The structure gate, a row of its own (row mod-0)

`StructureTests` reads the map from `fixtures/structure.json`, which names the twenty-one subdomains in their order, each one's folder, its
namespace and its allowed `Domain/`, and the census of today's exceptions; `StructureCheck` gains:

1. **The subdomain rule**: a source sits in its subdomain's folder; once moved, its namespace is `Daoris.Driver.<Name>`; a file added outside
   its subdomain fails, so new code is written in place from this row on.
2. **The direction rule**: a file names (by its `using` lines once namespaced, by type names before, as `StructureCheck` reads Loop's today) only
   its own subdomain's types and lower ones; an edge against the order is allowed only where the census names it, with its row.
3. **The purity rule** widened as §14.3 says, its exceptions named.
4. **Loop's rule** kept (nothing names Loop) and widened: Loop declares no judgement the census does not name as moving.
5. **No global using** of a subdomain's namespace.

Its proof: the gate run over the tree at this row's commit passes with today's exceptions listed and fails on a planted file that names an
upward type, a planted `Domain/` file naming `ServiceClient`, and a planted rule in Loop; `StructureTests`' own fixtures for each.

### 14.6 The order of the moves

Each move is a row that **changes no behaviour**: it moves one subdomain's files into its folder and namespace, adds the `using` lines its readers
need, updates the map, and keeps every gate of the brief §6 green; it lands when no open branch holds the files it moves, since a move under
another branch's edit is its conflict. The moves go **bottom up**, so each subdomain moves onto ground already in place: mod-i Platform and
Store; mod-ii Settings; mod-iii Service, Install and Instance; mod-iv Events; mod-v Knowledge; mod-vi Agents; mod-vii Workplace; mod-viii Roles;
mod-ix Participants; mod-x Context; mod-xi Course; mod-xii Work orders; mod-xiii Review; mod-xiv Plugins; mod-xv Workflows; mod-xvi Setup;
mod-xvii Help; mod-xviii Loop, the remainder. **The cuts** follow each move, one edge family per row (a port declared, or a type moved to its
owner), each changing no behaviour and shrinking the census by its edges; Loop's rules leave it in the rows that move their subdomains, each a
move with its tests, never a rewrite. Foundation rows that add code (the account rule, the course, the store, the catalogue) write it in its
subdomain's folder from row mod-0 on, so no new code needs moving.

### 14.7 Where the three areas, the store and the roles land

| Part of this design | Lands in |
|---|---|
| **Who acts** (§2): the participant read, the work, bindings, lineage, admission | **Participants**, with the service's twin of the work in the service |
| The capabilities, the role catalogue, the start plan, appointments and the act's admission (§2.4, §2.5, §2.7) | **Roles** |
| **How work moves** (§3): `Course.Decide`, `LineAccount`, the move record, the words' rules | **Course**, executed by **Loop**'s two loops; a move's brief and carry in **Context**; the say door's head and the open's words in the service |
| **What is said** (§4): the vocabulary, the hubs, the journal's readers, the notices, the machine log | **Events**; the outside projection (`work/event`) in **Plugins**; the journal itself in the service's store |
| **Where state is kept** (§13): `DesktopStore`, its migrations and imports | **Store**, each family's tables in the subdomain the §13.3 table names |
| The settings and their export and import | **Settings** |

## 15. Round 3: the reads of round 2 answered

Each finding by number, as `docs/2026-10-25-foundation-reads.md` holds it, checked by the steward against the code at `2a08bde4a`, whose `src/` is
`29a066182`'s, where both reads were made; *taken* says where the body changed, *disputed* says why not. Where the two reads overlap the stricter is
taken, and the pair is named. Codex's verdict names three choices still to make; round 3 makes them: **the role's authorization lifecycle**
(§2.7: the act within the code's table, the appointment's grant and the role's configuration, the grant raised only at the next *Appoint*), **the
move's fencing and rollback protocol** (§3.3, §13.4, §13.5: a store row claimed by its executor role's holder, every external effect fenced by a
mark written before it, no old box beside it and a phase table for an older build), and **the journal's capture schema** (§4.1: an allow-listed
snapshot per row, bounded per row and per table).

### 15.1 The Opus reader's foundation read (14 findings, 6 blocking)

Its §0 holds round 2's answers but three: round 1's O-1 and O-10, whose closed quest's case needed a-iii and its evidence (findings 5 and 7), and
O-17, reopened by the catalogue (finding 3). All three are answered below.

| # | | Answer |
|---|---|---|
| 1 | blocking | **Taken, with X2-2.2's wider form.** An act is admitted only within the code's act table, the appointment's grant (its ticked acts and scope, kept for its life: `StandingConfirmation.cs:187-189`, checked at `:1276` and at the act's run, `:1304`) and the role's configuration as it stands; an increase takes effect only at the next *Appoint*, through a `Window`-class route alone, never `role_propose`, a card or the terminal (§2.7); row t keeps the appointment's `acts`, row y-ii's proof gains a live appointment no later edit widens |
| 2 | blocking | **Taken.** Two exclusive shapes, the judge refusing a mixture (§2.7); each act carries its admissible scope kind and offered flag in code (`StandingConfirmation.cs:330`, `:336`, `:828`, `:831-835`), an added appointed role offered and scoped only where every act it holds is; row w's proof gains the refusals |
| 3 | blocking | **Taken, its field placed in the store.** `actor` keeps the seeded id of the start's work; the role taken and its revision go on the participant's binding in the desktop store (§2.7, §13.3), which no older build reads and which needs no chain through the host, where the reader named a new record field; the run reads the binding before `actor` (`Driver.cs:3666`; `ActorRun.cs:176-177`, throwing after the start at `Driver.cs:3651`); the one list is the eight always-on ids, so *Actors* and `daoris driver actor` are unchanged (`ActorChoices.cs:320`; `DriverChoicesCommand.cs:2059-2061`); row x's first case is an older build's continuation of a Designer record |
| 4 | blocking | **Taken.** Step 5's wait includes the own account's reset wherever step 1 may resume there (`HarnessRoster.cs:1026-1034`), and row a-i's proof splits the case: own cooling with every other signed out waits for its own reset; a driven record with its own and every other signed out holds, naming the sign-in (§3.2, §10) |
| 5 | blocking | **Taken.** The closed quest's case moves to row a-iii, after the briefed move takes the walk: `GoesOnLost` reaches `BriefedMove.MoveAsync` (`ChatRunner.cs:244-246`), which opens on the cooling own account as a pick (`BriefedMove.cs:133`) (§3.2 step 2, §7 row 1b, §10) |
| 6 | blocking | **Taken, with X2-2.3.** The seed is read per id, each id absent, set, removed or unreadable; the judge refuses removing or scoping away a role of a work that may not be off (`ActorChoices.cs:43`, `:55`); row w's proof gains a catalogue with one added role reproducing the eight seeded starts (§2.7) |
| 7 | — | **Taken in part, the claim kept with its evidence.** The reader is right that nothing it could read supported *the case the owner reported was a closed quest's*. The steward supplies the evidence: the session the owner reported on 2026-10-25 was a finished build session whose quest it had itself closed as done (a build session closes its quest with `quest_respond`), so its quest was no longer open when its words were held for its own account's weekly reset. D137's note covers *an ended record*, which the steward reads as including a closed quest's session; that is said as the steward's reading (§3.2 step 2), recorded as amending D137 point 4 for the cooling case alone (§7 row 1b; D212), listed in §8 as a reading the owner may overrule, and built in row a-iii. LIMITSWITCH1's FIX-LOG entry, which claimed the reported case, is corrected by a dated line: the case is LIMITSWITCH2's |
| 8 | — | **Taken.** A role carries no doors of its own, since every table reads the work (§2.7's capabilities; §1.1 Q4) |
| 9 | — | **Taken.** Census C admits a role taking `design` on a record whose marks read `build`; the quest's mark is a new field, `weight`, shared with MODELROLE1i's `exact` and built by whichever comes first, listed in §5.2 with its travel and an older build's reading (§2.7, row x) |
| 10 | — | **Taken.** A working role's scope reverses D205's rejection of per-workspace choices; the dated note is §7 row 18's and D212's amend list's |
| 11 | — | **Taken.** Row y-i is ⚖; MODELROLE1g's card and MODELROLE1h's `actor_propose` (owed, `HelpRoomOwed.cs:94-101`; neither in `TASKS.md`) are its predecessors, `role_propose` extending the latter; D205 point 9's *no `settings_*` grant covers actors* extends to roles (§2.7, §10) |
| 12 | — | **Taken, and superseded.** The move record is a row of the desktop store, every write one transaction under the write lock taken at its start, so no read-compare-write interleaves across processes (§13.4) |
| 13 | — | **Taken.** Row a-i's conversation path waits as today where none of its agent's accounts is ready, the ended move having no `ElsewhereAsync` (`LimitMove.cs:235-241`; the live move's at `:92`, `:444-469`); row a-iii adds it with its case (§3.2, §10) |
| 14 | — | **Taken.** Row c's lanes name the modules (`Modules/Loop/SessionsStateSlice.cs`); §3.3's box-key sentence is withdrawn with the box itself, the mechanism it half-named (a live first delivery, `ChatHandOver.cs:238-250`; the keyed re-delivery and its `User` check, `LimitMove.cs:366-378`) now said whole |

Beyond its numbered findings, its §2 and §3 are taken too: staffing has one home, `role_staffing`, for the seeded and the added roles (§13.3);
row x's census lists a scoped role naming an agent its place's rule does not permit; the closed quest's case is both in §8 and in row a-iii.

### 15.2 Codex's read (14 findings, 10 blocking)

Its §0 finds five of round 1's findings still open: 3.5 (the claim, answered by 3.1 here), 3.7 (dispositions, 3.2), 4.2 (retention, 4.2), 4.3
(announcements, 4.3) and 5.2 (rollback, 1.1); the rest it finds answered.

| # | | Answer |
|---|---|---|
| 1.1 | blocking | **Taken.** Round 2's claim that the two builds dedupe by the limit box's key is withdrawn: the key answers only while its word waits (`SessionLedger.cs:956`), and the hand-over's take clears its box however it came out (`HandOver.cs:271`, `:294`). A move the store carries writes no box at all, and §13.5's table gives, phase by phase, what the ledger shows, what an older build does and what the newer one reads of its evidence on its return, uncertain delivery and the phases with no box included (§3.3, §13.5) |
| 2.1 | blocking | **Taken, by removing what had no path.** A role carries no doors (with O2-8), so the connector needs no role and no evidence of one; the catalogue's revision a start took is kept on its binding, so census C compares a record with the catalogue as it stood (§2.7, §13.3) |
| 2.2 | blocking | **Taken, the stricter with O2-1.** The appointment's grant is the owner-approved ceiling, immutable for its life (`StandingConfirmation.cs:188`, `:1276`, `:1304`); the role's configuration intersects it at each act; every effective increase, a scope's widening and a restriction's removal included, waits for a fresh *Appoint*; a narrowing applies at once; removing a role revokes its appointments and cancels their admitted acts (§2.7) |
| 2.3 | blocking | **Taken, with O2-6.** Each id absent, set, removed or unreadable; an empty or absent catalogue the seed; a malformed record unreadable and holding its starts; deleting a seeded id's record its return to the seed, an added id's its removal; row w proves first edit, partial edit and last deletion (§2.7) |
| 2.4 | — | **Taken.** The judge's census names `SET_ACTOR` (`Modules/Bridge/Routes/DriverRoutes.cs:319`), the terminal's verbs, Ask Daoris's proposals and the import; staffing is judged by the work, not the literal id (`ActorChoices.cs:51`, `:65-72`); the refusal codes are named for the catalogue (§2.7) and the call layer (§2.3), each answered alike at every door |
| 3.1 | blocking | **Taken.** A file's guarded write (`Driver/Platform/AtomicFile.cs:154-158` replaces unconditionally) fences no spawn or prompt; the move becomes a store row whose claim is its executor role's holder's, taken over only by that role's current holder from a run that no longer holds it, every external effect fenced by a mark under a claim check, a begun effect never repeated; row f-i tests a paused executor resuming after a takeover (§3.3, §13.4) |
| 3.2 | blocking | **Taken.** Dispositions are per attempt: only a conclusive limit refusal carries; a try the provider failed as transient leaves its words uncertain (`LimitHeld.cs:35-41`), and a later limit carries only the words no try attempted; round 2's sentence is withdrawn, and row p's proof says so (§3.4 rule 5) |
| 3.3 | — | **Taken.** A receipt is keyed by the line and the producer's key; a keyless word moves and is receipted by its id (`Sessions.cs:373-375`); row p tests equal keys on unrelated lines and a retry through two successors (§3.4 rule 5) |
| 3.4 | — | **Taken.** The same-account refusal covers automatic parts; the person's fresh-part press is its one exception, taking the walk with its own account first where ready; a part whose own conversation is lost has no resume to keep (§3.2 step 6) |
| 4.1 | blocking | **Taken.** Each journal row carries an allow-listed snapshot of OLD and NEW (state, limit, the note's codes, the work's marks, the line's links, a word's id, author class and level), classified in .NET from the row alone; row k proves end, reopen, end and park, answer, park before any reader runs (§4.1) |
| 4.2 | blocking | **Taken.** A row is at most 1 KiB, the table at most 50 MiB of payload at its 50,000 rows, the write-ahead log outside the bound and said; more than 64 words in one statement journal an overflow row; *never raising* means no logic that raises, a storage failure failing the record's write with it, atomically (§4.1) |
| 4.3 | blocking | **Taken.** News is keyed by its occurrence (a journal position, a confirmation's id, or a record's own moment for a reconciliation); the said-set and the tellable news share one horizon, so a forgotten key is never eligible again; row m-iv tests more than 200 at once, the horizon's expiry, a new teller and a reconciliation below the floor (§4.5) |
| 5.1 | blocking | **Taken.** Row d needs no receipt, since the open moves the words under its key and transfer ids in one service step (§3.4 rule 3), and row p moves before row d anyway; row f's proofs name three outcomes, delivered once, refused at a limit and carried once, uncertain and said (§10) |
| 5.2 | — | **Taken.** Row f splits into f-i (the record, read by nothing) and one move kind per branch, ii to vi, each with its own compatibility proof; row x's older-build continuation is its first case; the two-maker gates and SWITCHCTX1's own owed read stand (§10) |
