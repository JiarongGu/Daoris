# The foundation: who acts, how work moves, what is said — the independent Opus reader's proposal

> FOUNDATION1, phase 1 under D191 (its amendment of 2026-10-23 seats an independent Opus reader where a third maker is wanted). One
> of three sealed proposals, written from `docs/2026-10-25-foundation-brief.md` and `docs/2026-10-25-foundation-survey.md` alone, before
> any other proposal for this design was read. The code was read at `f280761cf`, whose `src/` is the survey's `0e195ee61` unchanged, so
> every line the survey cites holds and every line cited here is that commit's. Read, not run: nothing here is built, spends, signs in,
> publishes or deletes. Paths follow the survey's aliases (`Driver/`, `Modules/`, `Host/`, `Core/`, `Mcp/`, `Web/`); `Dnn` is
> `docs/decisions/Dnn.md`; rows are named F1 to F15 here, for the synthesis to name as it files them.

*Sealed 2026-10-25, before any other proposal for this design was read.*

## 1. The shape in one paragraph

Three concepts, each a pure judgement in .NET over records that already exist, each with one owner, and every one of today's parallel
mechanisms becoming either a reader of a judgement or an executor of a step a judgement returns. **Who acts** is a `Participant`: a
projection, never a store, built by one function from the session record the service already owns (its role read from the marks the
desks set at the open, as D203 point 1 has it, the stored `actor` only its check), or, for a kind with no record, from the identity
Daoris issued it (a brainstorm's scope, a holder's appointment, a native child's lineage); one closed role keys every table that admits
it, at each of the four layers admission really has. **How work moves** is one decision, `LineNext`, a pure function from a trigger and
the line's facts to a step (stay and resume, retry, hold, or move, with where, how and which words), called by both loops, and one
durable `Move` record per move in flight that owns its delivery and its recovery whatever triggered it; the account rule LIMITSWITCH1
decided is the decision's first rule, written once, and the foundation's first row. **What is said** is a `Change`: a judged delta,
minted once by the judge that owns its fact in the process running that work, carrying generated ids and one closed code from one
grammar, ordered by its hub's run and sequence, a wake and never the truth. Pushed slices stay the state, command answers stay answers,
the session's event file, the lineage and the ask's channel stay stores; the channels that tell *that something happened* become
projections of Changes, each a filter declared in one table. Nothing new reaches the page as vocabulary (D193's rejection), no record
changes shape until every reader reads it through its concept (brief §7 point 4), and no row is a rewrite.

## 2. Where I part from the survey and the brief

### 2.1 The survey, checked against the code

I read again every line this proposal rests on: `ResumeAccount.cs:25-100`, `HarnessRoster.cs:895-1064`, `:1140-1155`, `:1520-1535`,
`LimitWaits.cs:145-200`, `LimitMove.cs:25-150`, `:191-245`, `ChatGoOn.cs:510-685`, `Driver.cs:1905-2060`, `:3780-3800`,
`DriverContinuations.cs:40-54`, `Planner.cs:845-895`, `Mcp/RoleFilter.cs:20-145`, `Core/Sessions/Sessions.cs:40-250`,
`SessionLedger.cs:860-990`, `SessionWords.cs:695-786`, `:898-945`, `DriverLoop.cs:1222-1240`, `Chain.cs:160-175`, `ChatRunner.cs:2665-2678`,
`ActorChoices.cs:30-180`, `ActorRun.cs:170-180`, `HookPoints.cs:108-128`, `Attention.cs:218-260`, `Web/work/identity.ts:55-95`, the
glossary and the zh catalogues. The survey holds where I lean on it; these are where it is wrong or short.

1. **§2.4 row 1 is not one flag at four sites, and the flag alone is not LIMITSWITCH1.** The rule is computed twice, as the property
   `ResumeChoice.Waits` (`ResumeAccount.cs:78`) and inline in `HarnessRoster.ResumeAsync` (`HarnessRoster.cs:1008`), and the conversations'
   wake has a third rule of its own over the cooling file (`LimitWaits.cs:186`). More important: a conversation's and Ask Daoris's resume ask
   `walks: false` (`ChatGoOn.cs:571`, `:672`), so a resume that no longer waits answers `Elsewhere`, which `ChatGoOn.cs:581` and `:682` turn
   into `CannotGoOn`, to the person; and the move a conversation would need, `LimitMove.MoveEndedAsync`, refuses a record no limit ended
   (`LimitMove.cs:212-213`). So LIMITSWITCH1 for conversations needs the ended move to take a cooling record a limit did not end. F1 carries
   both halves.
2. **§1.3 row 12, marked unverified, holds by reading and is wider.** The driven go-on resumes words only where the record's agent is
   `handedQuest?.Agent ?? config.Adapter` (`Driver.cs:1917`, `:1980`; `DriverContinuations.cs:43-44`), and `config.Adapter` is Build's first
   choice's agent by the write-through (`ActorChoices.cs:171-177`). A record Build's walk ran on a later choice's agent is never resumed by
   the person's words: its carry-on starts `config.Adapter` with that record's asked model (`Driver.cs:2016-2023`). D205 point 1 says a
   continuation keeps its record's actor; this keeps the machine's first agent. Read, not run; F9's first proof is a case that runs it.
3. **§1.3 row 8's "at least 27 lines" is a floor.** A rough grep at this commit (comparisons on `.Serves`, `.Opinion`, `.Piece`,
   `HelpRoom.Repository`, `IsHelp(`) finds 203 C# lines outside tests, and 46 page lines call `isHelp`, `isHelper`, `isOpinion` or
   `isIntake` or read a mark. The grep over-counts writes and declarations, so it bounds the reads from above.
4. **§2.4 row 11: neither computation is D206 point 2's.** The driven wait is over the walk's accounts of one agent
   (`HarnessRoster.cs:1728`), the conversation's the first cooling entry of its one agent (`LimitWaits.cs:199`); D206 point 2 is the earliest
   moment any account of any of the line's choices is ready.
5. **An end class already half exists.** The attention watch leaves an end out by the driver's own `SessionEnded` flags `ByPerson`, `Limit`,
   `NotOffered` and a park that says it (`Attention.cs:252`); a conversation's end carries none of them. F2's sibling concept, the end class,
   starts from that record.
6. **The brief's names (§4, *Words on the page*) do not all have homes.** *Role* has no glossary entry, and the window words an appointed
   role 角色 (`locales/zh/settings.daoris.json:108`), the word the glossary gives *actor* (`locales/zh/settings.ai.json:28`): Settings shows
   two concepts under one Chinese word. The glossary's *line* is the branch work lands on (主线), not D167's line of work, which the code calls
   a line (`LimitResume.Line`) and the page calls a session (会话). *Co-worker* (同事, D203 point 9) is not in the glossary yet.
7. **A word held by the shell is held outside the contract.** The service refuses a word to an active record not asked for its run
   (`SessionLedger.cs:945-952`), so the shell keeps a winding-up or moving record's words in memory and in `held-words.json`, which drops each
   word's `Choice` and `Key` (`SessionWords.cs:901-942`), and its record path keeps a word with no producer key (`SessionWords.cs:701`,
   `:782`). The survey names each; together they are one finding: the one store a word may wait in has a gap exactly where moves happen.

### 2.2 The questions I would ask instead

- **Q1** asks what one record says who acts. A new record is a second Session state (D173's guard, ADR-A's §8). The right question is what
  one function over the record that exists says, and what identity Daoris issues the kinds that have none. §3.1 answers both.
- **Q2** asks whether admission becomes one judge. It should not: the rule's ask, a measured row, a tool allow-list, the lineage filter, a
  role's proof and the launcher answer four questions (may it start, may it call, may it act, is it contained) in processes that share no
  code (the driver, the connector, the shell's pipe, the launcher; `.claude/knowledge/twins.md`). The right question is whether each layer
  is one table keyed by one role and fails the same way on a caller it cannot place. §3.3.
- **Q6** asks where one judge runs. Location is not the seam; separation is. The right question is whether the decision is one pure function
  apart from its executors, and whether every executor's crash is recovered from one durable record. §4.1 and §4.3.
- **Q11** lists pushed slices and held answers among the channels that might become projections of the event. A slice is what is now, a
  held answer a command's answer; neither tells that something happened, and making either a projection of events is event sourcing over
  D182's judged state. The right question separates what tells (one Change), what is state (slices), what answers (D182 point 2) and what
  stores (the event file, the lineage, the channel). §5.2.
- **Q14** asks about the event's durability. The event stays a wake (EVENTHOOK1 §4.4's *an event is a wake*). The right question is which
  readers need what was told to survive a restart (the person's notices, an outside subscriber's watermark) and how every reader learns it
  missed something. §5.5.

## 3. Who acts

Answers Q1 (§3.1), Q2 (§3.3), Q3 (§3.2), Q4 (§3.4) and Q5 (§3.5).

### 3.1 The participant (Q1)

**`Participant` is a type, not a table.** It is read by `Participants.Of`, in the service (`Core/Sessions/Participants.cs`) and in the
driver (`Driver/Sessions/Domain/Participants.cs`), twins with one fixture table both suites read (`twins.md`), since the two share no code.

| Field | What it holds | Where it comes from |
|---|---|---|
| `Id` | the session record's id; for a kind with none, the id Daoris issued it | the record; a brainstorm's scope (`Core/Sessions/BrainstormBox.cs:15-26`); a holder's id; a child's part and native id (`ChildBindings.Fold`) |
| `Role` | one closed code (below) | the record's marks, read by one rule; never stored |
| `Line` | the entry key (D167 point 2) | `Session.Entry` (`Sessions.cs:237`) |
| `Serves` | the part it works for | `Serves` (`Sessions.cs:158`); a reviewer's once D203 point 6 lands (F15) |
| `Actor` | the D205 actor whose choices it walked | `Actor` (`Sessions.cs:196`), provenance; none for a holder, a child or a meeting |
| `Binding` | D173's `RuntimeBinding`: adapter, account by profile name, the kept conversation id, the process generation, the door | the record's `Adapter` and `Profile`; `<s>.harness.json`; COWORKVIEW1's `start` and `end` lineage lines |
| `Authority` | none, or an appointment (role, scope, generation) | `data/standing-confirmation.json`'s appointments (D208, D209); never the owner's |

**The roles.** One rule reads them in this order, the first that holds: a mirror (`Origin`) is `teammate`; `Opinion` is `reviewer`;
`Serves` with `Piece` is `partner`; `Serves` alone is `helper`; `Ask` is `intake`; `Repository == "daoris:help"` is `ask-daoris`; `Kind = Chat`
is `chat`; otherwise `build`. Without a record: a brainstorm scope is `voice`; an appointment's holder is `holder`; a lineage child is
`child`. A meeting's turn is no participant until D177's turns start an agent (survey §1.1: `MeetingTurns` has no production value).

| Role | Actor (D205) | Starts through | Identity at the connector | Its record |
|---|---|---|---|---|
| `build` | build | `HoldAsync("build")` | `DAORIS_SESSION_ID` | the session record |
| `chat` | chat | `RunAsync("chat")` | the same | the same |
| `ask-daoris` | ask-daoris | `RunAsync("ask-daoris")` | the same and its workspace | the same |
| `intake` | intake | `HoldAsync("intake")` | the same and `DAORIS_ASK_ID` | the same |
| `helper`, `partner`, `reviewer` | helper, partner, opinion | `HoldAsync(…)` | the same | the same, opened by the desks |
| `voice` | brainstorm | `HoldAsync("brainstorm")` | the brainstorm's scope | its pass record |
| `holder` | none until SUPERVISOR1c | the window's terminal | none: an empty connector configuration (`HolderTerminal.cs:44`) | the appointment |
| `child` | its parent's | the harness | its parent's id, told apart by `_meta` lineage (D173's C0 note) | the lineage file, never a record |

**Which kinds become roles of one participant, and which stay distinct.** The seven with a session record are roles of one participant: they
already share one record, one ledger, one dispatcher and one connector identity, and what differs (admission, tools, time) is a table keyed by
the role. The voice is a role with no record: a pass is one turn with no line, and a record for it would be a chat-shaped record that every
older build lists as the person's conversation (`.claude/knowledge/compatible-builds.md`: a mark binds only the builds that read it). The
holder and the child stay distinct in their identity and their authority, not as kinds outside the model: a holder has an authority and no
connector; a child has an identity issued from lineage, never the environment (D173's guard), and only reads (C2a).

**Its owner.** The service owns the record and reads the role for its own readers (the connector's filter, the HTTP answer); the driver's twin
reads it for the look, the cap and the slices. The page reads it as data: the sessions slice's allow-list gains `role`, judged in .NET
(D182 point 6; `pushed-slices.md`), and the service answers it beside the marks on loopback only, so a teammate's mirror is answered no
role and reads as a teammate's session, as today. The stored `actor` stays D205's provenance (a continuation walks its record's choices); a
census holds that every record carrying both maps role to actor by the table, and where they disagree the marks win, since the desks write
them at the open and an older or mirrored record has no `actor` (`Sessions.cs:195`).

**D173's `Participant` and `RuntimeBinding`** (ADR-B) are this type and its `Binding`, a projection over the stores C0 named, so ADR-A's *no
second Session state* holds and ADR-B's identity exists in code. **The driver's own session**, which the cap counts (CAPCOUNT1), becomes a
property of the role: `role ∈ {build, intake, helper, partner, reviewer}`, the set `ActiveSessions.HoldsSlot` tells today from marks
(`Planner.cs:872-885`), judged equal on its every case before it moves (F2, F4).

### 3.2 The choice (Q3)

**One walk.** `Driver.WalkBuildPassesAsync` (`Driver.cs:710-779`), `ChatRunner.WalkAsync` (`ChatRunner.cs:906-955`), `DriverIntakes.WalkAsync`
(`DriverIntakes.cs:121-180`) and `ReviewerChoice.WalkAsync` (`ReviewerChoice.cs:250-290`) become one `ActorWalk.WalkAsync(actor, fence,
kind, workspace, passed)`. The reviewer's maker order and its hold of an agent's later entries (`ReviewerChoice.cs:196-201`, `:270-279`) are the
fence, a parameter; the two sets of pass codes (`ReviewerChoice.cs:89-111`) become one closed set; the workspace is always passed, so a
brainstorm's start reads its workspace's pinned version (`DriverBrainstorms.cs:682-683`; `HarnessRoster.cs:899`). The five re-asks of a model
the open refuses (§1.3 row 5) become one policy taking the start's kind. Helpers and partners choose through the walk under their own rule
type, not the opinion's (`HelperChoice.cs:66-70`).

**Every start through it, and the starts that do not go through it today:** a carry-on or retry (`Driver.cs:1917`, `:2020`; §2.1 item 2), the
look's report (`Driver.cs:1107`), a terminal's start-from (`SessionsVerbs.cs:33`, first agent only, where the route walks Chat's order,
`ConversationRoutes.cs:171-174`), the same-agent opinion press (`OpinionGateLook.cs:393`), the briefed move (`BriefedMove.cs:113`), a chat's
go-on (`ChatGoOn.cs:526`), the groups' report (`Trees/SessionGroups.cs:882`) and a new chat's fallback (`ConversationRoutes.cs:51`). Each reads
`config.Adapter` beside the actor (§1.3 row 11). After F9 a first start walks its actor and a continuation takes its record's agent and choice
(D205 point 1), never the machine's first agent. Two stay outside, each by a decision: the role holder's pinned harness on its own sign-in
(D208, until SUPERVISOR1c's actor holder) and the meeting, which starts nothing.

### 3.3 The rule and the admission, by layer (Q2)

| Layer | The question | Where it runs | One table, keyed by role | On a caller it cannot place |
|---|---|---|---|---|
| Start | may this kind start, for this asker, now | the driver | `KindRule` per role and `StartAdmission.Judge` | refuse, saying the rule |
| Call | may this caller call this tool | the connector | `ConnectorRoles` per role, every registered tool classed | **closed**: reads only (C2a's rule, `D173.md:140-145`) |
| Act | may this holder make this act | the shell's pipe | the role's act table (`StewardActRows`, the supervisor's) | refused (D208's proof) |
| Contain | is this process contained | the launcher | `AgentStarts.StartAsync` (`AgentStarts.cs:30`) | nothing starts uncontained (D201) |

**Start.** The five rule shapes with three defaults (§1.3 row 1) become one parsed shape, `KindRule { mode, agents, minutes?, scopes,
default }`, read from today's keys unchanged (`helpers`/`workspaceHelpers`, `handOver`, `opinions`/`workspaceOpinions`,
`brainstorms`/`workspaceBrainstorms`; Intake's and Ask Daoris's off from their actor). The stored keys do not change: D205 point 6 keeps
the rules where they are, and `driver.json` has twin writers (`twins.md`). The kind's own parameters (an opinion's `required`, `verify`,
`recheck`; minutes) stay on the kind. One `StartAdmission.Judge(role, asker, rule, counts)` holds each kind's concurrency in one table (§1.3
row 20) and a partner's measured row as that kind's condition (D199 point 4). Every door asks it: the page's route, the terminal's verb, the
desk and the driver, where today five copies decide *may this session ask a helper* (§1.3 row 6). The brainstorm's `mode`, read by no start
today, is read: `off` refuses a brainstorm, as the setting says. `partners` stays absent until COAGENT1d, the judge answering that a partner
is the person's ask alone.

**Call.** `ConnectorRoles` (`Mcp/RoleFilter.cs:31-94`) gains an allow-list per role on the server, where today only a partner and anything with
`serves` have one, and the client's lists (`OpinionPermissions`, `IntakeRoom`, `HelpRoom`, `BrainstormPermissions`, §1.3 row 15) are
projected from the same table, kept as a second guard on a harness that takes settings. The role filter reads the participant, so a reviewer
and a voice no longer pass it (`RoleFilter.cs:110-113`), and a caller it cannot place fails closed: a named session with no record reads
only and is told to try again (`ConnectorRoles.Unread`'s sentence, `RoleFilter.cs:90-93`), a brainstorm's scope is a voice, and a connector
naming no session, a person's own agent, keeps today's behaviour (C2a's carve-out).

**What the person sees change:** nothing where nothing is set, and these, each a decision already taken: Ask Daoris is refused `helper_ask`
(D203 point 6), a Codex brainstorm voice is refused at the server what a Claude one is refused by its handed rules, a partner is drawn as a
partner rather than as a helper (`identity.ts:79`), and a brainstorm whose rule says `off` does not start.

### 3.4 Roles and actors (Q4)

**Two axes of one participant, never one.** An actor (D205) is staffing: which agent, model and effort a kind of work runs on. A role (D208,
D209) is authority: a scope of acts the owner granted by an appointment, each recorded as the role's. A role is *held* by a session, the
holder, and once SUPERVISOR1c lands it is *played* by an actor (`supervisor` and `steward` join `ActorChoices.Ids`, `ActorChoices.cs:37`, and
`ActorRun`'s list, `ActorRun.cs:176-177`, a census holding the two lists equal, §1.3 row 9). Being started by an actor grants nothing: the
authority is the appointment's, proven per act by the HMAC over the hello's nonce (`SupervisorProof.cs:313-328`), and the key never crosses
the pipe (D208). So the holder is a participant whose `Authority` names the appointment and whose connector names nothing, and whose acts
are recorded `by` the role (attribution narrows, D156 points 1 to 4). It never holds the owner's authority: the owner's press on a repository
or quest takes it back for the run (D209), and the person key stays the window's.

**The two doors become one.** `SUPERVISOR_ANSWER`/`DEFER` and `ROLE_ACT` (§1.3 row 22) become one act door with one canonical form
(`RoleActForm.Bytes`, `RoleActForm.cs:48-63`) and one whitespace rule, the supervisor's answer and deferral two acts of its table, one reading
of *a driven repository*, and refusal sentences per role (`InstancePipe.cs:1365-1381` says *the supervisor* for both). Its decisions in files
and the steward's acts in memory stay as D208 and D209 chose; the door and the proof unify. It changes an authority's door, so it is built
only after both makers read it (D191's note of 2026-10-24); F14.

### 3.5 Native subagents (Q5)

**A child is a participant, bound to its part, never a record** (D173's C1a note). Its identity is issued from what the harness wrote in
`_meta` and the hook, never its inherited `DAORIS_SESSION_ID` (the guard). What is read of it is `ChildBindings.Fold`'s projection
(`ChildBindings.cs:128-266`): harness, native id, parent, depth, the signals that named it, and its state (active, finished, ended, lost),
within the process generation COWORKVIEW1's `start` and `end` lines bound. Nothing of its conversation is read. What no harness said stays
unknown (D173's canary 9). Its calls are read-only by its lineage, failing closed (C2a), which the call layer of §3.3 enforces once C2a's
measurement of the ACP doors is in (D173's C2a note, point 2).

### 3.6 The in-flight designs in this area

- **MODELROLE1** (D205, rows a to e built): built as designed; F9 completes it, its four walks one.
- **COWORKVIEW1 row a**: built as designed, independent of every row here. It is the lineage's first production reader (§1.3 row 17), so the
  child participant gets its read from it, and its `start`/`end` lines become the binding's generation. **Its routing half is re-cut**: the
  *current executor* is the line's receiver (§4.4), and *what waits on what* reads the `Move` records and the roles.
- **COWORKUX1** (D203): its point 1 is F2; its point 6 (a reviewer's `serves`, server allow-lists, Ask Daoris refused) is F5 and F15.

## 4. How work moves

Answers Q6 (§4.1), Q7 (§4.2), Q8 (§4.3), Q9 (§4.4) and Q10 (§4.5).

### 4.1 One decision (Q6)

`LineNext.Decide(trigger, line, accounts, rules) → Step`, a pure function in `Driver/Conversations/Domain/`, as `Continuation.Judge` is
(`Continuation.cs:207-239`).

- **Triggers:** words kept on a parked or ended record; a turn's end (the curve's occasion); a limit's end; a door's failure, by D210's kind; a
  hand-over (the person, Daoris's offer as the person's press, the agent's ask, the curve); the briefed press; a conversation proven lost; the
  planner's carry-ons (cut off, stop released, retried, limited, brought forward); a workflow's send-back to an ended conversation; *Go on in
  a new session*; *Start a conversation with these words*.
- **Inputs:** the line's records (the newest part, its state, `limited`, `ended.transient`, `from`, `choice`, its kept conversation and door,
  via `Continuation.Judge`); the account facts read once (§4.2); the rules (the actor's choices, `handOver`, a pick); the `Move` standing, if
  any.
- **The step:** `Stay`, which is *resume* (the same record and conversation), *retry* (the same conversation, try *n*, D210) or *hold* (for
  the person, the cap, a repository's hold, or a reset, with its moment and its door out); or `Move`, which is a kind, a why and a by (the
  `from_why`/`from_by` codes, `Sessions.cs:177-181`, closed in one enum with `Driver.cs:4622-4633`'s), *where* (an agent; an account named or
  the walk's; a choice), *how* (brief, or carry once SWITCHCTX1 is built), *when* (now, at the turn's end) and *which words* (§4.4).
- **Its rules, in order:** the nevers, one function (`WordsNever`, `Continuation.Judge`'s never, `HandOver.Never` and `GoOnNew`'s, §2.3,
  written four times today); the error's kind (§4.5); the account rule (§4.2); resume or brief (D189 point 2, D206 point 3); the hand-over's
  timing; the walk (§3.2).
- **Where it runs:** where its trigger is heard, by one function. The look calls it from the start (`Driver.RunAsync`) and from the plan
  (`Planner.WordsOn`, `LimitedFrom`, `CarriedOnFrom`); the chat runner from `ChatGoOn`, `LimitWaits`, `LimitMove.Due`, `CurveMove.Due`,
  `ChatHandOver`, `BriefedMove` and `ChatTurns`' send-back. A census lists every site that opens a part `from` another or resumes with
  words, and fails on one that does not call it.

**Today's paths, collapsed** (survey §2.2):

| Path | Trigger | Step | Executor, which stays where it is |
|---|---|---|---|
| A. go-on, driven | words | resume, or move (account, §4.2) | `Driver.RunAsync` and `DriverContinuations` |
| B. go-on, conversation | words, the sweep, the come-up | resume, or move (account), or hold | `ChatGoOn` |
| C. go-on-new, start-from | the person's press | move (press), by the walk | the look; `ChatRunner.StartAsync` |
| D. limit move, live | a limit's end | move (limit) now, or hold for the reset | `LimitMove.MoveAsync` |
| E. limit wait and wake | a reset | resume own, or move (limit) | `LimitWaits` |
| F. limit resume, driven | a limit's end; *Try again* | resume own, or move (limit), or hold | `Driver.RunAsync` |
| G. curve | a turn's end | move (curve) | `CurveMove` |
| H. hand-over, conversation | a hand-over | move (hand-over) at the turn's end | `ChatHandOver`, `HandOver` |
| I. hand-over, quest's session | a hand-over | move (hand-over): the person's now, the agent's at the turn's end | `HandOverQuest`, the look |
| J. briefed move | the briefed press; lost | move (resume), by the walk | `BriefedMove` |
| K. carry-on | the planner's | move (carry-on), the record's agent | the look |
| L. workflow press | *Accept*; a send-back | stay (an ordinary word between turns); ended: as B | `ChatPresses`, `ChatTurns` |
| M. SWITCHCTX1's carry | a move of one agent between accounts | the *how* of a move, not a trigger | the start, per its §2.3 |
| N. transient retry | a transient failure | retry, else hold past the bound | `AcpSession.RunAsync`, `TransientRetries` |

Two of the survey's fourteen are not decisions at all: the carry (M) is how a move hands its context, orthogonal to why it moved; the
workflow's press (L) is a word, which the dispatcher already owns. The decision's output has three axes, *when*, *where* and *how*, and
every path picks a point on each; today each path spells its own.

### 4.2 The account (Q7)

**The rule, stated once.** A part that *resumes* its own conversation runs on the record's own account, since the conversation lives in that
account's home (D131 point 2, D137 point 7). Every *new* part takes the walk over its line's actor choices, the same agent's accounts first
(D197, D205 point 4), a named account first where a hand-over named one (`HarnessRoster.cs:923-934`), never another agent on a pick
(`LimitMove.cs:447`). Where the own account cannot carry words that would resume:

| Own account | Another account of the line's choices ready | No account ready |
|---|---|---|
| ready, or near | resume own | resume own |
| cooling, words or a limit's end (LIMITSWITCH1, D206 point 3) | **move now, briefed** | **wait** until the earliest reset over every account of every choice (D206 point 2), said in D206 §3.7's words |
| cooling, the person pressed *Go on in a new session* | move now | hold, saying it |
| gone, outside, kept, refused, signed out, the model not offered | move now (D137 point 7) | hold, saying it |

`LineAccount.Decide` holds this table; `ResumeChoice.Waits` (`ResumeAccount.cs:78`) and `HarnessRoster.cs:1008` read it; one
`EarliestReset(line)` replaces the driven `Unready` (`HarnessRoster.cs:1728`) and the conversation's first cooling entry (`LimitWaits.cs:199`)
for a line's wait. The seven answers to *which account* (§2.4 row 2) become two: own for a resume, the walk for a new part. The briefed
move, a new part, takes the walk with its own account first where ready (today a pick of the cooling account, `BriefedMove.cs:133`, which
§2.4 row 13 names as the door that fails exactly when it is needed). The limit move's probe walk (`LimitMove.cs:86`, `:237`) asks with
`counts: false`, so the account it judges is not counted as a start the next walk spreads around (`HarnessRoster.cs:1530`; survey §2.4).

**Two disagreements the rule's rows decide, each a dated note:** *a pick* holds its agent; whether it holds its account is today's code's
(the limit move moves a picked conversation among its agent's accounts, `LimitMove.cs:85-104`) against D206 point 5's text (*a pick waits
for its own account*), and the record keeps no account pick to read (`ResumeAccount.cs:15-17`). I recommend the code's behaviour, amending
D206 point 5 to *a pick of an agent*, for LIMITSWITCH1's reason: a weekly limit would hold a picked conversation for days. It is in §10's
list. And *a driven line at a limit walks other choices* (§2.4 row 10) is D206's row 1e, open: until it lands, the driven *others* are its
agent's accounts.

### 4.3 The move record (Q8)

**One durable record per move in flight**, read and written through `Moves`, generalising the ended limit move's box (`Chat/LimitBox.cs:22-88`)
and the hand-over's take, and holding what SWITCHCTX1's carry record holds (its §2.3) for every kind of move.

| Field | Holds |
|---|---|
| key | the record the move leaves, as the limit box, the hand-over box and SWITCHCTX1's carry record are keyed |
| `kind`, `why`, `by` | limit, curve, hand-over, briefed, carry-on, account, press; the closed `from_why`/`from_by` codes |
| `phase` | `judged` → `old-ended` → `opened` (the new record's id) → [`copied` → `verified`, the carry's] → `delivering` → `delivered`, or `fell-back` (its code) or `ended` (why) |
| `where` | the agent, the account by profile name, the choice |
| `how` | brief or carry; the brief's recipe (the events' bound it is composed up to, the agent's own note) |
| `delivery` | the ids of the words it carries and their keys (`move/<old>/<word>`), a limit's refused turns' bound, the channel messages held |

Never a word's text, a path or a key. Each step writes its phase before the next, and each is safe to run again (LimitBox's and SWITCHCTX1's
rule). **Its owner** is the driver library, written by the process running the line's move: the chat runner in the shell for a conversation,
the look for a driven line. **Its recovery** is one come-up stage, `Moves.TakeUpAsync`, generalising `LimitMove.TakeUpBoxesAsync`, beside the
look's `limits` stage, for every kind. **Its adapters, until F15 moves the file:** the limit box for the limit kinds, the hand-over box and
`HandOverAskFiles`' state for the hand-over and the curve (asked as one, `CurveMove.cs:114-117`), the ledger's `from` link for a driven carry-on
(the planner derives it from the records every look, so it is durable already), `data/handed/<quest>.json` for a quest handed to another
agent, which is the quest's agent and not a move in flight, and `<s>.new-session.json` for the person's request. Today a move in flight is
durable for the ended limit move alone (survey §2.5); F6 gives the live limit move and the curve theirs.

### 4.4 The words across a move (Q9)

The dispatcher's guarantee (`.claude/knowledge/session-words.md`) holds by construction once five rules hold:

1. **A word is kept on a record's `said` before anything holds it, a moving or winding-up record included.** The say door gains an `after`
   keep: a word for an active record whose dispatcher closed is kept with `reopens` (`SessionLedger.cs:945-952` refuses it today), asked only by
   the shell and the driver, as `running` is. The shell's holds (`SessionWords.cs:455-467`, `:714-720`, `:752-812`) become keeps, and
   `held-words.json` is read once at a come-up for an older shell's words and never written again. The record path passes the producer's
   key (`SessionWords.cs:701`, `:782`).
2. **One function says which record a word goes to:** `Line.Receiver(entry)`, the line's newest part, or, where a `Move` stands before
   `delivered`, the record it leaves, whose delivery will carry the word. It replaces the say doors' and the shell's readings of
   `WordsNever.ContinuedIn` (`SessionSay.cs:54`), the source of LIMITWORD1 and HANDWORDS1-i.
3. **A move's delivery is by id and key.** The new part's first prompt is composed from the `Move`'s delivery; each word is said on the new
   record under `move/<old>/<word>`, so the say door answers a retried delivery with the word it kept (`SessionLedger.cs:955-962`), and it is
   taken off the old record only once the new answered, as the ended limit move already does (`LimitMove.cs:199-201`).
4. **A word once attempted is never sent again.** A word the old part's door marked attempting is settled by `UncertainWords` at the come-up
   (`GoOn/UncertainWords.cs:25-94`) and by D210's dispositions, never carried as held; the delivery carries held words only.
5. **Only the person's words go to a new part**, one function, `Delivery.Carries(word)`, where five sites write it today (§2.4 row 14).

### 4.5 The errors (Q10)

Every door's failure is read by `AgentErrors.Read` (`Agents/Errors/AgentErrors.cs:133-205`) into one of D210's four kinds, and the kind is a
trigger of `LineNext`: a **limit** takes the account rule's row (D206), never a strike; **access** holds for the person, never a move; a
**transient** is a retry in the same conversation where the door has its retry (the driven protocol door today, `Driver.cs:3789-3794`; D210's
later rows extend it), and past the bound a hold (`Planner.cs:1525-1532`); a **fatal** end ends with its reason. Every path reads the same
kind: the chat runner's limit reads the reader's code as the driven path does (`ChatRunner.Limited` takes the failure's sentence alone,
`ChatRunner.cs:2670-2675`, where the driven path reads the code too, `Driver.cs:3237`; §2.4 row 8), and the brainstorm's end
(`DriverBrainstorms.cs:778`) and the one-turn kinds' go through the same reader. *Which record is limited* is one reading: the ledger's flag,
for conversations too, where a scan of the events stands in today (`LimitHeld.cs:17-56`).

### 4.6 The in-flight designs in this area

- **LIMITSWITCH1** (D137's note of 2026-10-25): built as F1, the account rule's first form, so the decision is written once from its first day.
- **SWITCHCTX1's carry**: **re-cut in storage, kept in protocol.** Its carry record is the `Move` record's carry section (its phases, its
  read-back, its fallbacks, its `CarryAsk` as the move's *how*, its `handed` field as designed), so a carried move and every other kind have
  one owner of delivery. If it is accepted and built before F6, its `<s>.carry.json` is the file F6 and F15 extend to the other kinds, never
  a second store beside it.
- **AGENTERRORS1** (D210, rows 1a and 1a2 built): its later rows are built as designed; F8 makes its kinds the decision's input, and its rows
  for conversations land as the retry's executor in the chat runner.
- **D206's open rows** (1e, 1f): decided inside F8's rows as the account rule's *others* and the pipe door's resume.

## 5. What is said

Answers Q11 (§5.1, §5.2), Q12 (§5.3), Q13 (§5.4) and Q14 (§5.5).

### 5.1 The change (Q11)

A `Change` is `{ run, seq, at, kind, subject, facts }`:

- **Who mints it:** the judge that owns its fact, in the process running that work, once. The hub, `Driver/Platform/Changes.cs`, lives in the
  driver library, since the headless host references the driver alone (`Host/*.csproj`) and the shell's slices live in the modules: one hub
  per process that runs work, the lease's loop telling plugins and outside readers alone, as EVENTHOOK1 §4.1 already rules.
- **Its id:** the hub's `run` (twelve hex digits from the system's random source, minted at the hub's start) and its `seq` (from 1), as
  EVENTHOOK1 §4.3's frame has them.
- **Its kind:** one closed code from one grammar, `ChangeGrammar` (§5.3), extending EVENTHOOK1's `WorkEventGrammar`.
- **Its order:** per hub, `seq` under one short lock (EVENTHOOK1 §4.4); `seq` orders what was told, never what happened.
- **Its scope:** this home; its subject's ids name what the reader then reads through its own doors, and a narrower reader is filtered in .NET
  before the frame is built, never by the reader (EVENTHOOK1 §4.3).
- **Its facts:** generated ids, closed codes, counts and times; never a name, a word, a path, an account or a key.

**Minted at the judgement that first sees the fact, never at a writer.** The connector writes records directly (D182 point 5), so a writer-side
mint misses a session that parks itself; a look-side mint is a look late and loses a park around a restart (`SessionLog.cs:38-41`). The judge that
diffs records as the ledger's family moves sees every write, whoever made it, and it settles the two comments that disagree on who parks
(`SessionLog.cs:34-37` against `Attention.cs:119-122`): neither; the record moved, and its judge saw it.

### 5.2 What becomes a projection, and what stays (Q11)

| Today's channel (survey §3.1) | It tells, holds or answers | In the foundation |
|---|---|---|
| pushed slices (D182) | what is now | **state, unchanged**: woken by Changes as by their `WakesOn` and `Hears` today |
| held answers (NOBLOCK1) | a command's last answer | **an answer, unchanged** (D182 point 2); each retires as its read becomes a slice |
| `UNDER_WAY` | the presses under way | state, unchanged |
| `STATE_CHANGED`, `DRIVER_TICK` | that a family moved | **projections**: a table from Change kinds to scopes, for plugins and unmoved pulls; `DRIVER_TICK` retires once its three keys are pushed (F13) |
| `DAORIS.*` bus events (`SESSION_ATTENTION`, `NOTICE`, `MESSAGE`, `SESSION_ENDED`, `SESSION_QUEUED`, …) | that something happened, to a window | **projections**: one `DAORIS.CHANGE` frame; each old event a declared filter of it until its page readers move |
| the attention watch | parks, ends, waits | **the first minting judge**, re-cut to diff records (F11) |
| notifications (D196) | what came to the person | **a projection**: the notices record written from Changes by one attention policy (F12) |
| hook points | to plugins | **projections**: `work/event` the grammar-held frame; `session/ended` its frame kept (protocol 1); `state/changed` from the scope table; `quest/consider` stays a decision point, not a Change |
| Fabric chain events | the look's internal wakes | **internal**, unchanged; ranks 3 and 4, declared and bound nowhere (`Chain.cs:170-172`), are bound or removed in F13 |
| the machine log | a diagnostic line | **its own store**; its lifecycle lines (`session.ended`, `.parked`, `.moved`) written from the Change by the hub, one writer per line |
| the session's event file | the conversation | **a store** (D189's portable context), unchanged; a move's notes written by the `Move`'s steps |
| the lineage file | a child's signals | **a store** (C1a), its reader COWORKVIEW1 row a, which mints `child.seen` and `child.ended` |
| the ask's channel | messages between participants | **a store** (D167, D188), unchanged; its quest lines read the quest log's operation codes (§3.4 row 8) |
| EVENTHOOK1's relay | to an outside session | **a projection**: `work/event` frames through the relay, as designed |
| presses told, `run/requests/*.told` | a terminal's press, to the shell | an adapter that mints `press.told` |

**Across processes, only durable projections travel.** A Change never crosses a process except through EVENTHOOK1's relay. A shell that does
not hold the lease hears a headless loop's ends through the notices record and the home watch (`DriverLoop.cs:822`), as today, and its own
conversations' Changes from its own hub; so the headless end that today reaches a window only as a focused window's `NOTICE` (§3.4 row 11)
reaches every window the same way, once F12 writes notices from one policy.

### 5.3 The vocabulary (Q12)

One closed code per kind of change, `<subject>.<what>`, each with its closed facts and its one minting judge:

| Kind | Facts | Minted by |
|---|---|---|
| `session.started` | session, entry, role, actor, choice | the records' judge |
| `session.parked` | session, quest?, ask?, why (question, go-ahead, helper's answer, …) | the records' judge |
| `session.answered` | session, by (person, supervisor, helper, partner) | the records' judge |
| `session.ended` | session, quest?, `class`, state, adapter? | the records' judge; `SessionEnded` for the driver's own |
| `session.reopened` | session, by | the records' judge |
| `line.moved` | from, to, why, by, how | the records' judge (a record `from` another appears) |
| `line.waits` | session, for (reset, person, helper, cap, hold), until? | the look |
| `account.cooled` | agent, until, why | the roster's `Limited` (`HarnessRoster.cs:272-304`); machine-local, never outside |
| `quest.published`, `.taken`, `.closed` | quest, operation | the quest log's operations (EVENTHOOK1's 1d) |
| `work.landed` | session, quest?, code | `AutoLander.Tried` (EVENTHOOK1's 1b) |
| `press.waiting`, `press.answered` | confirmation | the pipe's confirmations (EVENTHOOK1's `person.answered`) |
| `call.held`, `session.quiet` | session | the calls judge, the watch |
| `child.seen`, `child.ended` | part, child | the lineage's reader |
| `run.started` | the counts of what waits | the hub, first |
| `events.missed` | missed | the hub, in a failed event's place (EVENTHOOK1 §4.3) |

**The end's class** is one closed code, `EndClass.Of(record)`, read from the record's state, its `limited` flag and its note's code, and from
`SessionEnded`'s flags for the driver's own (`Attention.cs:252`): `completed`, `failed`, `limited`, `transient-bound`, `access`, `stopped-person`,
`stopped-daoris`, `stopped-role`, `refused-choice`, `awaits-helper`, `stood-down`. Today's four names for four sets of a session's end (§3.4
row 1) become four declared filters over it in one table, each tested: `SESSION_ENDED` (conversations, every class), `session/ended`
(the look's drained runs, every class), the log's `session.ended` (every class), *Ended* (not `stopped-person`, `limited`, `refused-choice`,
nor an end its quest's park says). A filter whose set the census shows to differ from what its readers expect is a decision of F2's sibling
row, never a silent move. *Answered*'s four facts (§3.3) are four kinds or four `by`s, never one word.

**Where the sentence is composed:** from the code, per reader and language. The page composes chrome from it in both catalogues
(`translation-parity.md`), the bell's and the toasts' words included (`Web/notifications/noticeWords.ts`); .NET composes the English for the
terminal and the event file's notes as `Noted` with its code (`NoteCodes`), which the page renders in either language, as notes already are.
A code without words in both catalogues fails the census, so *ConfirmingRestored*'s English toast (§3.3) cannot recur.

### 5.4 The readers (Q13)

| Reader | May read | Never |
|---|---|---|
| this machine's page | pushed slices (allow-listed; a machine path only in a desktop-only projection), `DAORIS.CHANGE` frames, command answers | a credential |
| the person's notifications | the notices record, written from Changes by one attention policy; the notify switch gates every kind alike except a wait on the person's own press, a declared exception (§3.4 row 5) | a word an agent wrote beyond the note's |
| plugins | `work/event` (the grammar: ids and codes); `session/ended`'s frame as today, its names included, protocol 1; `state/changed`; `quest/consider` | `said`, a key |
| an outside agent's session (EVENTHOOK1) | `work/event` frames through the relay: ids, codes, counts and times | a name, a word, a path, an account |
| the terminal | the same .NET judges over the pipe (D182 point 6), in English from the codes | — |
| a teammate's mirror | the sync wire's records: sessions without machine-local fields, quests, the quest log, asks, the channel | every Change, `said`, the marks and the role, the actor's fields, `Move` records, the lineage, the cooling, the appointments |

What never leaves the machine: every Change but a relayed frame, every machine path, every profile name, `said`, conversation ids, the
lineage, the cooling and the appointments.

### 5.5 Durability and loss (Q14)

| What | Kept | A restart |
|---|---|---|
| a Change | memory, bounded per reader (256, oldest dropped, `missed` counted; EVENTHOOK1 §4.4) | lost; the new `run` and `run.started` say so |
| the notices | `data/notices.json`, one writer after F12 | kept |
| a `Move` | its record | taken up by `Moves.TakeUpAsync` |
| a word | `said` on the service, whatever the record's state (§4.4 rule 1) | kept; one attempted is settled as uncertain |
| a slice | memory, rebuilt | rebuilt; `epoch` new, the manifest repairs (D182 point 3) |
| a held answer, the presses under way, the confirmations' lists | memory | rebuilt or asked again |
| the attention watch's last look | replaced by the records' judge's last read | a baseline, as today; a park around a restart is in *What needs you*, judged from records (`NeedsAPerson.cs:266-273`), and counted in `run.started` |
| a transient episode | memory, one run (D210) | its go-on on `said`, past the bound a hold on the record |

**How a reader knows it missed something:** a `seq` past the next, a `missed` above 0, an `events.missed` frame, or a new `run`
(EVENTHOOK1 §4.4), and for state a slice's base or the manifest (D182 point 3). One rule of first reads replaces today's two (§3.4 row 6):
a judge's first read is a baseline, said by `run.started`'s counts, never as news; *Holding*'s first read becomes a baseline too
(`HeldCallAnnouncements.cs:22-23`). A record written by four and read by none (the lineage) is a store waiting for its reader, not a
channel; COWORKVIEW1 row a is that reader.

### 5.6 The in-flight design in this area

**EVENTHOOK1: built first, as designed, then moved onto the hub.** Its rows land without waiting for any row here. F11 moves `HookFrames.Event`'s
source from `AttentionWatch.Observe` and `AutoLander.Tried` to the hub, its `WorkEventGrammar` becomes `ChangeGrammar`'s outside projection
(the kinds an outside reader may hear), its 1d's quest kinds are minted from the quest log's operations as its round 4 asked, and its
`person.answered` is `press.answered`. Nothing of its relay, its pipe or its frame changes.

## 6. Records and their owners (Q15)

The survey's §4, after the rows; *older* says what a build from before reads.

| Record | Owner after | Travels | Older build |
|---|---|---|---|
| `sessions` with `said`, the marks, `from`, `entry`, `limited`, the actor's fields | the service, unchanged; the role derived, never stored | the record without its machine-local fields, as today | unchanged |
| a word kept `after` (F7) | the service | no | reads it as a word waiting on the record, going on at its end, as an ended record's words do; held by a test of the older build's own statements (`compatible-builds.md`) |
| `handed` (SWITCHCTX1), a reviewer's `serves` (D203 point 6) | the service | no field on the wire | absent, read as none |
| `quests`, `quest_log`, `asks`, the channel | the service, unchanged | yes | unchanged |
| `opinions` | the service, unchanged | no | unchanged |
| a helper's box | the ask and the answer the service's; its `back` retired in F15 for the answer's keyed say on the lead (`helper/<ask>`), whose key is the durable *handed* | no | reads `back` while it is written; F15 writes both until no reader reads `back` |
| a hand-over's ask and box | the ask the service's (the connector writes it); the box the trigger, the driver's; its state the `Move`'s | no | unchanged until F15 |
| `<s>.limit-move.json` | the `Move`'s, as its limit kind's file until F15 | no | read at its come-up while written |
| `<s>.move.json` (F15) | the driver library | no | invisible to an older build, which takes a move in flight up as a shell's end today (the come-up's uncertain words), said in F15's note |
| `held-words.json` | retired (F7): read once at a come-up, never written | no | an older shell writes it; a newer reads it once |
| `<s>.new-session.json`, `data/handed/<quest>.json` | unchanged, triggers | no | unchanged |
| `cooling.json` | the driver library, unchanged; one earliest-reset reader | no | unchanged |
| `data/notices.json` | one writer, the lease loop's notices projection (F12), where three write today | no | an older headless host still writes; the slice reads either |
| the machine log | two writers by process, by design (the driver's and the service's), with the twin parser's test table | no | unchanged |
| the event file, the lineage file | unchanged; the lineage gains COWORKVIEW1's `start`/`end` | no | unchanged |
| appointments, holder rooms, the steward's effects, `pipe-waiting` | the modules, unchanged | no | unchanged |
| memory only | slices, held answers, presses under way, confirmations, Changes, hook queues, a transient episode | — | — |

## 7. The names (Q16)

| Concept | English | Chinese | On the page | Retires |
|---|---|---|---|---|
| the Fabric | *Fabric* in prose (D173) | none | never (D193) | — |
| participant | `Participant` in code, *participant* in prose | 参与者 in prose | never | — |
| role (appointed) | *role* | **职务** (an office one is appointed to; its verb stays 任命) | Settings → Daoris → *Roles* | the page's 角色 for roles (`zh/settings.daoris.json:108`), which collides with *actor* |
| actor | *actor* | 角色 (D205) | unchanged | — |
| line of work | *session* on the page; `Line` in code; *line of work* in prose wherever *line* could mean the branch | 会话 on the page | unchanged | — |
| line (the branch) | *line* | 主线 (the glossary's) | unchanged | — |
| part | *part* | 段 | unchanged | — |
| move | `Move` in code, *move* in prose | none | never: each kind keeps its words (转交, 继续, 额度用尽) | — |
| event | `Change` in code, *change* in prose | none | never | — |
| co-worker | *co-worker* (D203) | 同事 | when COWORKUX1's chrome lands | the glossary gains it then |

**Code names that change:** `ResumeChoice.Waits` read through `LineAccount` (F1); `ActiveSessions.HoldsSlot` over the role (F4); `SessionRole`
in `Mcp/RoleFilter.cs` becomes the participant's role (F5); `LimitBox` read as a `Move` (F3), its file moved in F15; `AttentionEvent` a
projection of `Change` (F11). **Only in prose:** Fabric, participant, line of work, move, change. 职务 needs its glossary entry with its
reasons (D116) and its status budget checked; 岗位 is in *actor*'s avoid list, and 职务 is not used elsewhere in the catalogues.

## 8. The rows, in build order (Q17, Q19)

Each row is its own branch, lands with every gate of `daoris.gates.json` green and its area's suites run whole (brief §6), and leaves Daoris
working as before for a person who sets nothing, except where its *Changes* line names a decided change. **The first is F1, because it
removes the one live defect the owner has already decided (LIMITSWITCH1), at four call sites and one function, and it proves the
shape every later row follows on the case that opened this design: one judgement, many executors.** The rows that only name a concept
(F2, F3) change nothing, so they cannot remove a defect; the word rows (F7) prevent the largest class (about nine of the twenty-six in §2.6) but
touch the dispatcher's hot path and need F3 first.

### F1. The line's account, once (LIMITSWITCH1 built as it). A day.

- **Code.** `Driver/Accounts/Rotation/LineAccount.cs`: §4.2's table, pure, over the own account's hold and cooling, the trigger (words, a
  limit's end, the person's new session, a model not offered), a pick's agent, the first ready account of the line's choices, and one
  `EarliestReset`. `ResumeChoice.Waits` (`ResumeAccount.cs:78`) and `HarnessRoster.cs:1008-1016` read it; the inline twin goes. A driven
  record whose own account cools with words waiting moves as a limited one does (`ResumeChoice.Limited`, `Driver.cs:1985-1991`), to its
  agent's next account (D206's row 1e for other choices). A conversation and Ask Daoris (`ChatGoOn.cs:573-579`, `:674-680`) take the ended
  move on `Move`: `LimitMove.MoveEndedAsync` takes a cooling record no limit ended where the step says so, why `account` (its guard,
  `LimitMove.cs:212-213`), handed the brief and the waiting words; the wake (`LimitWaits.cs:186`, `:199`) takes the same step. The briefed
  move takes the walk, its own account first where ready (`BriefedMove.cs:133`). The probe walks ask `counts: false` (`LimitMove.cs:86`, `:237`).
- **Changes, each decided:** LIMITSWITCH1 (D137's note of 2026-10-25); the earliest reset over the line's choices for both kinds (D206 point
  2); the briefed press's account, a dated note on D189 (the briefed move is a new part, its point 2).
- **Proof.** `LineAccountTests`: the table over own (ready, near, cooling, signed out, refused, missing, outside, kept, not offered) × trigger ×
  pick × others (ready on its agent, ready on another choice, none) × kind (driven, conversation, Ask Daoris), every row LIMITSWITCH1 does not
  change compared, in the same commit, with today's `ResumeChoice` and `LimitWaits` answers. Process: `LimitWaitsProcessTests` and
  `ChatGoesOnProcessTests` gain an ended chat whose own account cools with another ready (a briefed part now, each word once) and with none
  ready (it waits at the earliest reset, said); `ContinuationTests`, `AccountRotationTickTests`, `LimitResumeTickTests` gain a completed driven
  record with words and its account cooling (a carry-on now on another account). Census: `ResumeChoice.Waits` has no reader but through
  `LineAccount`. Install: the next weekly cool-off, a finished record's words go on within one look (`session.moved … why=account`), and
  where none is ready the page says D206 §3.7's wait.
- **Removes:** the quest's example; §2.4 rows 1, 11 and 13, and the briefed move's answer in row 2 (the rest of row 2 is F8's); the shape
  behind LIMITRESUME1b, 1c and LIMITGOON1, each a path that had not learned another's account rule (§2.6).

### F2. Who acts, named. No behaviour.

- **Code.** `ParticipantRole` and `Participants.RoleOf` in the service and the driver (§3.1), and `EndClass.Of` beside it (§5.3), each pure;
  `fixtures/participant-roles.json` and `fixtures/end-classes.json` read by both suites. Nothing reads them yet.
- **Proof.** Census A: on every fixture row, `RoleOf` agrees with `ActiveSessions.HoldsSlot` (`Planner.cs:880-885`), with `identity.ts`'s
  predicates (the page's test reads the same fixture), with `ConnectorRoles.Refusal`'s reading of `serves` and `piece`, and with
  `WordsNever.IsHelp`; every disagreement found is listed as a decision for F4 (the partner read as a helper, `identity.ts:79`, is one).
  Census B: a source census, the pipe census's shape, lists every line outside `RoleOf` that reads `Serves`, `Opinion`, `Ask`, `Piece`,
  `HelpMode` or `HelpRoom.Repository` to tell a kind, each named *not yet moved* with the row that moves it; a new such line fails.
  Census C: every record with an `actor` maps its role to it. `EndClass` reproduces the four end sets of §3.4 row 1 exactly, each as its
  filter. Gates: service, driver, web-unit.

### F3. A move, named. No behaviour.

- **Code.** `Move` and `Moves.Read` (§4.3) in `Driver/Conversations/Domain/`, reading today's records as adapters: `LimitBox`, `HandOverBox`
  and `HandOverAskFiles`' state, the ledger's `from` link, `HandedQuests`, `GoOnNew`'s file. The `from_why` and `from_by` codes closed in one
  enum.
- **Proof.** A census with one row per path of §2.2, A to N: its trigger, its step's kind, its durable record or *none, not yet moved (F6)*; a
  new caller of `StartAsync(from:` or `ContinuesFrom`, or a new `from_why` code, that the table does not name fails. Judged equal: at each
  step of `HandOverTests`, `LimitWaitsProcessTests` and `CurveMoveTests`, `Moves.Read` names what the box names.

### F4 to F15, in order

| Row | What | Changes | Proof | Folds in |
|---|---|---|---|---|
| **F4** | the cap, the page and the predicates read the role: `HoldsSlot`, `identity.ts` from the pushed `role`, both `IsHelp`s | a partner drawn as a partner | `CapCountTests`, `SessionsStateSliceTests` (`role` pushed, allow-listed, answered on loopback only), web-unit; census B shrinks | D203 point 1 |
| **F5** | the connector keyed by role, failing closed (§3.3) | Ask Daoris refused `helper_ask`; a Codex voice refused at the server | `McpToolsTests`, `McpRoleFilterTests` extended to every role × every tool; a stand-in caller with no record refused all but reads; the C2a measurement of the ACP doors first | C2a; D203 point 6; **both makers** |
| **F6** | every move keeps its record: the live limit move and the curve write theirs; one come-up stage | none | `LimitWaitsProcessTests`, `CurveMoveTests`, `HandOverTests`; a shell killed between each phase, every word reaching exactly one part (the live gap §2.4 row 3 calls unverified) | SWITCHCTX1's carry builds on it |
| **F7** | words never held outside `said` (§4.4) | none for the person | `DrivenInboxTests`, `SessionMessagesTickTests`, `ChatGoOnTests`; an older build's statements over an `after` word; a restart between keep and delivery; install: say-to-wire latency before and after | session-words's contract |
| **F8** | one decision, `LineNext`, absorbing F1 (§4.1) | each disagreement decided by note: §2.4 rows 4, 5, 10, 12 (the person's hand-over stops a quest's session as a press does, D188 point 1; the agent's waits), 13 (D189 point 3 for conversations); the chat's limit by code | every *how work moves* suite of brief §6 whole; `TransientRetryTickTests`, `AcpTransientTests` | AGENTERRORS1's later rows; D206's 1e and 1f |
| **F9** | one walk, one re-ask, every start through it (§3.2) | a continuation runs on its record's agent | `ActorChoicesTests`, `HelperLookTests`, `PlannerTests`; a record on Build's second choice resumed by words (§2.1 item 2) | MODELROLE1 complete |
| **F10** | start admission, one table (§3.3) | the brainstorm's `off` honoured | `HelperLookTests`, the five doors of §1.3 row 6 asked the same cases | — |
| **F11** | the hub and its first minting judge: parks and ends from records, beside the attention watch | none | `AttentionTests`' cases judged equal; `LookSaidTests`, `NoteCodesTests`; a census over `ChangeGrammar` | EVENTHOOK1 moved onto it |
| **F12** | the person's channels as projections: notices one writer, one attention policy | the notify switch's exception declared; a headless end reaches every window | `AttentionStateSliceTests`, `StatePublisherTests`, `pushedState.test.tsx`; install: the bell's count against *What needs you* | D196 |
| **F13** | invalidations as projections; `DRIVER_TICK` retired once its keys are pushed; the chain's ranks 3 and 4 bound or removed | none | `StateSlicesTests`, `bridge/state/*.test.ts*`, `PluginKitTests`; install: pushes and git starts per idle look (IDLECPU1's measure) | D182's PUSH rows |
| **F14** | the role holders' one act door and proof (§3.4) | none for the owner | `PipeCensusTests`, `StandingConfirmationTests` | D208, D209; **both makers** |
| **F15** | records last: the one `<s>.move.json` with the carry's section; a reviewer's `serves`; the helper's answer a keyed say; `held-words.json`'s reader removed once no build writes it | none | `SessionStoreTests` (the field null for a record from before and for a mirror), `QuestSyncTests` (nothing new on the wire); an older build over each new shape | SWITCHCTX1's file; D203 point 6 |

**Independent of every row:** EVENTHOOK1's rows, COWORKVIEW1's row a and AGENTERRORS1's rows land as their designs say, each when its own
reads are in (D191's note of 2026-10-24), and F11, F2 and F8 fold them in as §5.6, §3.6 and §4.6 say.

## 9. What is the owner's (Q18)

1. **F1 spends another account's allowance** where a record's own account cools: decided by the owner in LIMITSWITCH1 (D137's note of
   2026-10-25). Nothing more is asked.
2. **D206 point 5 amended to *a pick of an agent*** (§4.2, F8): a conversation the person picked moves among its agent's accounts at a limit, as
   the code does today, instead of waiting for its own account as D206's text says. It spends another of the owner's accounts on the owner's
   own conversation, so it is the owner's answer under D191 point 3.
3. Nothing changes the product's direction, signs in, publishes, pushes or rewrites history. F7 retires a file holding the person's words only
   after every word in it is kept on `said`, so no data is lost; its proof shows it.
4. F5 and F14 change who may call and act: not the owner's under D191 point 3, but, granting and refusing authority, built only after both
   makers read them (D191's note of 2026-10-24).

## 10. Rejected

- **A participant table, or a session record per brainstorm voice.** A second Session state (D173's guard), and a chat-shaped record every
  older build lists as the person's conversation.
- **Storing the role.** A stored role drifts from the marks it summarises: two truths, the class of CAPCOUNT1 and ASKRAIL1. D203 point 1
  already chose to read it.
- **One admission judge across the connector, the driver and the pipe.** The layers answer different questions in processes that share no code
  (§3.3).
- **Moving conversations into the look.** A conversation's turns are interactive and live with the shell's window; the look also runs headless.
  One decision and one move record give the unity without moving the executor.
- **Event sourcing: a durable event log as the truth.** Every concept would gain a second store beside its records, against D173's guard, and
  D182 already pushes judged state with a version.
- **A typed outbox in the service.** The change stream is families by design, never records (D169 point 3; `ChangeStream.cs:76-80`), so the
  look's own writes cannot wake it for ever (canary 5); minting at the judge sees the connector's writes without it.
- **One bus event replacing every channel at once.** A big bang; each old event stays a declared filter until its readers move (F11 to F13).
- **Retiring `STATE_CHANGED` now.** Plugins and every pull not yet pushed read it (D182 point 2).
- **A second carry box beside the move record.** Two owners of one move's delivery, the gap LIMITWORD1 came from.
- **Making the dispatcher own a move.** A dispatcher is per record; a move spans two.
- **One `rules` key in `driver.json`.** The stored keys have twin writers and older readers; only the parsed shape becomes one.
- **New words on the page** for participant, move or change (D193's rejection).
- **One writer for the machine log.** Its writers are per process by design; their lines are twins held by tables.

## 11. What stays unmeasured

- Whether the live limit move can lose a word in its gap (§2.4 row 3, *unverified* in the survey): F6's kill-between-phases case measures it.
- Whether lineage reaches Daoris over either harness's ACP door (C2b's), on which F5's fail-closed call layer waits.
- Whether two processes append one session's event file (§3.4 row 7, *unverified*), and whether a shell not holding the lease reconciles
  its slices only at the manifest's period (§3.2, *unverified*); F11's hub rule assumes neither.
- How often F1's move happens and what its brief costs the agent against waiting for the reset; SWITCHCTX1f measures the carry that removes the
  cost.
- Whether an older driver's dispatcher would hand an `after` word to a running part on the same home (F7's older-statement test reads it; a
  downgrade on one home is the only exposure).
- The defects' classes (survey §6): a judgement, which this proposal's ordering inherits.
- The cost of `LineNext` at a turn's end: a pure function over facts already read, expected negligible, measured with F8 on the install's
  turn-end timing.
