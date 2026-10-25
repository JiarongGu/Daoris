# The foundation: who acts, how work moves, what is said — the brief

> FOUNDATION1, phase 1 of a foundational co-design under D191 (2026-10-25). This brief is the one document every proposal is
> written from. It holds the direction, the process, the constraints a proposal keeps, the questions every proposal answers,
> what must not regress, and how the work lands. The facts of today are in `docs/2026-10-25-foundation-survey.md`, by `file:line`,
> and are not repeated here: a proposal cites the survey's section and the code's line, never this brief, for a fact. The
> proposals will be one section per maker in `docs/2026-10-25-foundation-proposals.md`, each written from this brief and the
> survey alone and sealed before its author reads another. A synthesis follows in a later quest. `Dnn` is
> `docs/decisions/Dnn.md`. Paths follow the survey's aliases.

## 1. The direction

The owner's direction, 2026-10-25, as the quest carries it: Daoris has grown through many foundational designs, each with a
mechanism of its own, and may need a refactor to a better structure, one that names the **Fabric**, the **actor roles** and the
**event system** as the foundation the rest stands on. It is designed under D191 as a foundation, never built as a big-bang
rewrite.

The steward's reading, which the survey tests against the code: three areas each grew several parallel mechanisms, and defects
keep coming from the places where they disagree.

- **Who acts.** Every kind of participant (a driven session, a chat, Ask Daoris, an intake, a helper, a partner, a reviewer, a
  brainstorm voice, a role holder, a native subagent) has its own rule, admission, choice of agent and record (survey §1).
- **How work moves.** Where a line's next words or work go is decided by fourteen paths in two loops, each choosing an agent, an
  account, a conversation and a context its own way (survey §2).
- **What is said.** A change is told by some fifteen channels, each with its own kinds, ids, ordering and readers (survey §3).

Of the 273 fix-log entries of 2026-10-15 to 2026-10-25, 51 came from such a disagreement (survey §0).

The example the quest names: a finished record's words waited days for its own account's weekly reset while other
accounts ran work, because the go-on's account rule (`Accounts/Rotation/ResumeAccount.cs:78`, D137 point 7) never learned what
D206 taught the limit move (survey §2.4, row 1). The owner's decision on that one case is LIMITSWITCH1; this design asks why the
two rules were two.

**The part of the company it makes work** (D193): every part at once, since it is how the company's staff are seated (part 4),
how its work orders move between them (part 5), and how a department works and is seen working (part 7). A company whose rules
for a seat, a hand-off and a memo are written once is one whose next rule is written once.

## 2. The process

D191 governs it, with its amendments of 2026-10-20 and 2026-10-23 and its note of 2026-10-24. This design changes a foundation
in every sense the 2026-10-20 amendment names (the data model of records, a protocol between processes, concurrency, and decided
rules other designs rest on), so:

1. **Three sealed proposals**, each from this brief and the survey alone: Claude's first, then Codex's and an independent Opus
   reader's (in Fable's place, D191's amendment of 2026-10-23). No author reads another's before its own is sealed.
2. **One synthesis**, written by Claude Opus, as a proposed decision and a design document, comparing the proposals question by
   question (§5) and saying what each choice rejects.
3. **Both makers read it**: Codex, and the independent Opus reader as a foundation read. Each finding names the file and line it
   rests on, and the steward checks each against the code before it is taken.
4. **Rounds until both makers' blocking findings are taken.** By D191's note of 2026-10-24 this design is not accepted on one
   maker's read, and where any part of it grants authority or deletes, not at all without both.
5. **Rows**, each its own branch, each behind today's tests (§7).

**What the owner is still asked** (D191 point 3): a change of the product's direction; anything that spends the owner's accounts
or money beyond the development's own runs; a sign-in; anything that publishes, pushes or rewrites history; anything that loses
the owner's data. A proposal that needs one of these says so, in a list of its own.

## 3. The inputs

These designs are in flight. Each is an input to FOUNDATION1, not a competitor of it: a proposal says, for each, whether it is
built as designed inside the foundation, built first and then moved onto it, or re-cut by it.

| Design | Standing | What it brings to the foundation |
|---|---|---|
| EVENTHOOK1, `docs/2026-10-24-event-hook-design.md` | proposed, round 4; Codex's read owed; nothing built | the first event told outside Daoris: one point `work/event`, generated ids and closed codes under one grammar, a sequence per plugin, `events.missed`; its §1 is a survey of the hook set and the attention watch |
| COWORKVIEW1, `docs/2026-10-24-cowork-view-design.md` | proposed, round 4; row a settled alone; the routing half a later round's | one read of who is at work on a session (parts, co-workers, native children, quests, the channel); its §1 surveys the records each kind keeps |
| SWITCHCTX1's carry, `docs/2026-10-24-switch-context-design.md` | proposed, round 5; Codex's read owed; its line view (1c-i) accepted alone | one durable carry record per move that owns its delivery and recovery; its §1 surveys every move a line makes |
| AGENTERRORS1, D210 | accepted on the amended text; rows 1a, 1a2 built | one reader of every door's failure into four kinds, one retry in the same conversation |
| MODELROLE1, D205 | accepted; rows a to e built | one choice of agent, model and effort per actor, walked choice by choice |

Two more are its ground: D173's Collaboration Fabric (ADR-A to F, and its C0 audit,
`docs/2026-10-16-fabric-contract-audit.md`), and D182's pushed state (`.claude/knowledge/pushed-slices.md`).

## 4. The constraints a proposal keeps

Each is decided and stands, unless a proposal argues to amend it. A proposal that amends one says which and why, and the
amendment is appended to the decision as a dated note, never rewritten into it.

**The doctrine** (`AGENTS.md`).
- **Orchestration is central; ownership is local.** A session changes its own repository and asks another through a quest
  (D45). Nothing in the foundation lets a participant write into another repository, or makes a message do a quest's job (D167's
  rejection of *replacing quests by messages*).
- **Local operation does not require a remote**, and doctrine works without the service. The CLI's doctrine commands stay
  offline with no runtime dependencies; its management verbs reach .NET over D171's pipe or run the same .NET under the home's
  lease (D182 point 6, D194).
- **Machine-local stays on the desktop**: streams, accounts, browser state and filesystem actions. A shared host exchanges
  permitted records, serves no UI and answers no machine path (D47 §4). A field that must not cross a sync has no field on the
  wire (`.claude/knowledge/session-record-fields.md`).
- **Model-dependent capabilities say the tier that ran** (D24), and nothing spends differently until the person presses (D205
  points 7 and 10).
- **No machine path, private repository name or credential** in a tracked file, a record that travels, an event that leaves the
  machine, or a commit message.

**The person's door and the roles.**
- Authority is the person key's; attribution is the body's, which may narrow and never widen (D156 points 1 to 4). A terminal
  asks the window to confirm.
- A role the owner appointed acts in its exact forms, each act recorded as the role's and overruled by the owner, never as the
  owner's words; no role decides what is the owner's (D208 point 1, D209 points 1 to 3). The key never crosses the pipe.
- Every door reaches what a setting does: the window, the terminal and Ask Daoris (D50, D110, D183).

**The state hub** (D182). .NET owns each slice of the machine's state once; judged state is pushed with its data, allow-listed,
versioned by `{ epoch, seq }`, never a credential or a machine path outside a desktop-only projection; every window keeps its own
cache, and more windows never mean more judges; a write that bypasses the host still reaches the windows through the service's
change stream; the domain's judgement lives in .NET once, never in TypeScript; success is measured on the installed app. Command
answers are not broadcast.

**The Fabric** (D173). One authoritative store per concept, never two at once; today's pieces move onto the foundation one
concept at a time, each old surface becoming an adapter with a mapping and a migration; a child's identity is issued, never
inherited from its parent's environment; what a harness does not report stays unknown, never assumed; correctness work stays
first and the foundation's runs beside it.

**Lines, words and moves.**
- A session is one line of work in one repository, its records its parts; the entry key travels and is never rewritten (D167).
- Every word that may make a turn happen is kept on the record's `said` through the session's one dispatcher before anything
  holds it, and a word once on the wire is never sent again (D137, D188, D210; `.claude/knowledge/session-words.md`).
- No message stops a turn; agents never talk each other into turns (D188 points 1 and 4; D155).
- Daoris resumes a conversation only by the id it kept (D137 point 8). A move hands Daoris's brief, composed without a model,
  never the agent's thoughts or another model's summary (D189 points 4 and 5); SWITCHCTX1's carry would amend this and is
  proposed.
- A limit is never a strike, and the line resumes by itself (D206); a transient error is tried again in the same conversation,
  never a walk (D210).
- A second opinion is independent by maker (D155 point 4). A partner is admitted by a measured row (D199). Every agent Daoris
  starts is contained through the launcher (D201).

**Records and compatibility.**
- A record an older build still opens keeps working for it: new meaning goes where older builds cannot see it
  (`.claude/knowledge/compatible-builds.md`). A twin changes with its twin (`.claude/knowledge/twins.md`).
- Project facts stay in versioned records: decisions amended, never rewritten; open work in `TASKS.md`, finished work moved to
  the archive.

**Words on the page.** Chrome comes in English and Chinese within its kind's budget; authored content stays as written; a new term
enters through the glossary (`src/Daoris.Web/src/locales/glossary.json`) with its reasons (D116,
`.claude/knowledge/translation-parity.md`). *Fabric*, *actor* (角色), *role*, *line*, *part* (段) and *channel* (频道) already
have homes; a proposal that names a new concept gives both words and says which existing term it retires.

**Simple.** A person who sets nothing sees Daoris work as today. The foundation is a structure for the code and the records, not a
new vocabulary on the page (D193's rejection).

## 5. The questions every proposal answers

A proposal answers each, citing the survey's section and the code's line for every fact it rests on, and says where it amends a
decision.

**Who acts** (survey §1).
1. **The participant.** What one record says who acts: its fields, its owner (service, driver or desktop), its identity at the
   connector, and how it relates to D173's `Participant` and `RuntimeBinding`. Which of today's kinds become roles of it, and
   which stay distinct, and why.
2. **The rule and the admission.** Whether the per-kind rules (`helpers`, `partners`, `handOver`, `opinions`, the brainstorm
   policy, `intakeAdapter`, `helperAdapter`) become one shape, and whether admission (the rule's ask, a measured row, the
   connector's per-kind allow-lists, the lineage filter, a role's proof, the launcher) becomes one judge. What the person sees
   change: nothing, or what.
3. **The choice.** Whether every start resolves its agent, model and account through one path (D205's choices and D197's walk),
   naming each start that does not today.
4. **Roles.** How an appointed role (D208, D209) and an actor (D205) relate: a role held by a session, a role played by an actor,
   or both; and how a role holder is a participant without holding the owner's authority.
5. **Native subagents.** Whether a harness's child is a participant, bound to its part (D173's C1a), and what is read of it.

**How work moves** (survey §2).
6. **One decision or many.** Whether one judge decides where a line's next words or work go (go-on, limit move, curve move,
   hand-over, briefed move, carry-on, retry, a chat's workflow press, the carry), for driven lines and conversations alike; its
   inputs, its outputs, and where it runs (the look, the chat runner, both).
7. **The account.** One rule for *the record's own account cools: wait or move*, and for *which account a resumed conversation
   takes*, stated once and read by every path.
8. **The context.** One move protocol (resume the conversation, carry it, or brief a new part), with one durable record of a move
   that owns its delivery and recovery for every move, not only the limit's box.
9. **The words across a move.** How the dispatcher's guarantee holds through every move and every come-up, by construction rather
   than by each path's care.
10. **The errors.** How D210's four kinds feed the one decision, so a limit, an access refusal, a transient and a fatal end each
    reach every path the same way.

**What is said** (survey §3).
11. **The event.** What one event is: who mints it, its id, its kind's code, its order and its scope; and which of today's channels
    become projections of it (pushed slices, held answers, `STATE_CHANGED` and `DRIVER_TICK`, hook points, chain events, the
    attention watch, notices, the machine log, session event and lineage files, the ask's channel, EVENTHOOK1's relay).
12. **The vocabulary.** One closed code per kind of change (a park, an end, a limit, a move, a landing, a wait on the person), and
    where a sentence is composed from it in each language.
13. **The readers.** The page, the person's notifications, plugins, an outside agent's session, the terminal, a teammate's mirror:
    what each may read, and what never leaves the machine.
14. **Durability and loss.** What is kept, what is memory only, what a restart loses and says, and how a reader knows it missed
    something.

**Across the three.**
15. **Records and owners.** For every record the survey lists (§4), its one owner after the change, which travels on the sync
    wire, and which an older build still reads.
16. **The names.** Fabric, actor, role, participant, line, part, move and event, in English and Chinese against the glossary;
    which code names change, and which only in prose.
17. **The order.** The steps, each a row (§7), the first one small enough to land in a day and prove the shape, and how each in-flight
    design (§3) folds in.
18. **What is the owner's.** Every point that needs D191 point 3's answer.
19. **Proof.** Per step: the unit and census tests, the Process cases, the rehearsals, and what is measured on the install.

## 6. What must not regress

Every step keeps today's suites green, as `daoris.gates.json` declares them, and adds its own:

| Gate | Command |
|---|---|
| cli | `npm run verify` |
| tool-tests | `npm run tooltests` |
| service | `dotnet test src/Daoris.Service` |
| driver | `dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --filter Category!=Process` |
| driver-process | `… Driver.Tests --settings src/Daoris.Desktop/process.runsettings` |
| driver-process-git | `… Driver.Tests --settings src/Daoris.Desktop/git.process.runsettings` |
| modules, modules-process | the modules' fast and Process halves |
| web-types, web-rules, web-unit, web, web-measured | the page's types, rules, unit, end-to-end and measured suites |
| deployment | `npm run rehearse:deploy` |
| family rehearsal | `npm run rehearse:family`, run by the merge tool beside the git half |

The suites that hold each area's contracts today, which a step touching that area runs whole, not by sample:

- **Who acts.** `McpToolsTests` (the connector's tools and their denials), `HelperLookTests`, `ActorChoicesTests`,
  `PipeCensusTests` and `StandingConfirmationTests` (the person's door and the roles), `ChildLineageTests` and
  `LineageHookProcessTests` (native children), `SessionLedgerTests` and `SessionStoreTests` (the records), `HelpCoverageTests`
  (Ask Daoris's doors).
- **How work moves.** `ContinuationTests`, `SessionMessagesTickTests`, `ChatGoOnTests`, `ChatGoesOnProcessTests`, `HandOverTests`,
  `HandOverLookTests`, `CurveMoveTests`, `LimitWaitsProcessTests`, `LimitResumeTickTests`, `AccountRotationTickTests`,
  `TransientRetryTickTests`, `AcpTransientTests`, `DrivenInboxTests`, `PlannerTests`.
- **What is said.** `StatePublisherTests`, `StateSlicesTests`, `SessionsStateSliceTests`, `AttentionStateSliceTests`,
  `AttentionTests`, `LookSaidTests`, `NoteCodesTests`, `PluginKitTests`, `QuestSyncTests`; the page's `pushedState.test.tsx` and
  `bridge/state/*.test.ts*`.

And the behaviours the install relies on, each with a case before the step that could break it: a word is never sent twice nor
lost across a move or a restart; a limit never strikes; a transient error never walks; a person's press is never taken as a
role's or an agent's; a record a teammate's build reads still reads; nothing the page drew before reads blank after.

## 7. How the work lands

**Never a big-bang rewrite.** The foundation is reached in steps, each its own row and branch, each landing with every gate of §6
green, each leaving Daoris working as it did for a person who sets nothing. The shape the D173 guards already set:

1. **Name the concept, then its one store.** A step introduces one concept of the foundation (a participant, a move, an event) as a
   type with its census test, read beside today's mechanisms and judged equal to them on every case they cover, before anything
   reads it.
2. **Move one reader at a time onto it.** Each step moves one path or channel onto the concept, the old surface becoming its
   adapter, the census failing on a path that neither moved nor is named as not yet moved. Never two authoritative stores for one
   concept at once.
3. **A disagreement found is a decision, not a refactor.** Where the census shows two paths deciding one question differently (the
   survey's tables), the step that unifies them states which answer stands, with its reason, as a dated note on the decision it
   amends; it never changes behaviour silently inside a move.
4. **Records last, and compatibly.** A record's shape changes only after every reader reads it through the concept, in a shape
   older builds cannot misread.
5. **Measured on the install** where the step moves a hot path (the look, the pushes, the dispatcher), before and after.

A proposal names its first three rows concretely, with their proofs, and the rest in order.

## 8. Amendment, 2026-10-25: capabilities in code, roles in configuration

The owner directed on 2026-10-25, after round 1 of the synthesis, that Daoris's actors and roles not be hard-coded: each role can be
configured and set up into the company, as D193 pictures its staff (part 4). The proposals were sealed before this direction, so the
synthesis carries it from its round 2 (`docs/2026-10-25-foundation-design.md` §2.7), read by both makers like the rest. The cut the
steward set, which every round from 2 keeps:

- **Capabilities in code.** The code keeps one closed vocabulary of capabilities: the kinds of work a participant can take (build,
  design, review, intake, help, chat, and the other kinds Daoris starts today), the acts an appointed role may make (the supervisor's
  and the steward's), the doors and tools each kind of work may use, and the containment each runs under. A capability's admission,
  its doors and its containment are code, keyed by the capability and never by a fixed list of kinds.
- **Roles in configuration.** A role is a configured record composed from those capabilities: its name in English and Chinese; the
  work it takes; its staffing, D205's ordered choices of agent, model and effort; its authority, the acts and doors it may use; its
  scope, the machine, a workspace or a repository; and how it is filled, always on or appointed (D208, D209).
- **The default company.** D205's eight actors and the two appointed roles are seeded into configuration as the default company, so a
  person who sets nothing sees Daoris work as today, and an upgrade changes nothing (D205 point 7).
- **Who changes it.** The person adds, changes or removes a role, at its doors, or Ask Daoris proposes the change and the person's press
  applies it (D205 point 9's shape). A role gaining authority needs the person's press, and an act that is the owner's (D191 point 3;
  D209's *never what is the owner's*) stays the owner's whatever a role says.
- **What it re-cuts.** Q1's participant carries its role and its work; Q2's admission tables are keyed by capability; Q4's actor and
  appointed role are one configured record; Q16's names follow; MODELROLE1l's Design actor (D205's note of 2026-10-25) becomes a
  configured Designer role, never new code beyond the capability it takes.

The constraints of §4 stand unchanged: the person's door, D208's and D209's bounds, the state hub, the Fabric's guards, and *Simple*:
the page gains only the words a person needs to see and change a role, entering through the glossary.
