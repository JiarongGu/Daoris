# Roles and supervision — how it works now

## What it does

A role is who does a kind of work for the person. A working role takes one or more works (build, chat, review…) at a
place, and its staffing says which agents, models and efforts run it, in order. The person sets roles through one door:
the window, the terminal, or an Ask Daoris proposal they apply. A role may carry a policy: weights, shares, budgets and
the moves a line makes to another choice. The supervisor watches a line's counts. Its rules are the person's, read with no
model: *when this count reaches n here, do this*. An appointed supervisor or steward (an agent the owner appoints in the
window) is built but dark in this build. Ask Daoris is the front desk: it reads the machine, takes the person to a place,
starts an ask, or proposes a change the person applies. In full access it acts itself.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Modules/`
(`src/Daoris.Desktop/Daoris.Desktop.Modules/`), `Service/` (`src/Daoris.Service/`) or `Web/` (`src/Daoris.Web/src/`).

| Unit | File | Its job |
|---|---|---|
| `RoleCatalog` | `Roles/RoleCatalog.cs` | The working roles: the seed per id, the store's rows, the judge, the role that takes a work at a place |
| `RoleRecord`, `RoleWork`, `RoleScope` | `Roles/RoleRecords.cs` | A role's record; the nine works; machine, workspace or repository scope |
| The seed | `Defaults/roles.json` | The eight seeded roles, machine-wide |
| `RoleDoor` | `Roles/RoleDoor.cs` | The person's one door: judges a press, then writes role, staffing, policy and rules in one transaction |
| `RoleVerb` | `Roles/RoleVerbs.cs` | `daoris driver role` and `role set/remove/default/policy/rules/supervisor/suggested` |
| `RoleStaffing` | `Roles/RoleStaffing.cs` | `role_staffing` in the desktop store: import, the one writer, the copy onto `driver.json`, the found check |
| `StaffingProjection`, `StaffingPart` | `Roles/StaffingProjection.cs`, `Conversion/StaffingPart.cs` | Changes found in the file's copy; the conversion's staffing part |
| `ActorChoices`, `ActorKey` | `Roles/ActorChoices.cs`, `Settings/ActorKey.cs` | An order of 1 to 4 choices, its effective entries, its judge, who may be off |
| `StartPlan`, `ReaskMove` | `Roles/StartPlan.cs`, `Roles/OneTurnReask.cs` | The one walk over a role's order, work to role, pass codes; the one re-ask |
| `StaffPolicy` | `Roles/StaffPolicy.cs` | The policy record: rules, entries, conditions, actions, presets, digest, judge |
| `StaffJudge` | `Roles/StaffJudge.cs` | `Arrange` (weights, shares, budgets, works) and `OnCondition` |
| `StaffPolicies`, `StaffLedger`, `StaffCapabilities` | `Roles/StaffLedger.cs`, `Roles/StaffPolicyDoor.cs` | `role_policy` and starts ledger; stale reads; the fields a build enforces |
| `RoleOrders`, `ActorSuggestion`, `StaffWeek` | `Roles/RoleOrders.cs`, `Roles/ActorSuggestion.cs`, `Roles/StaffWeek.cs` | The other readers' order; suggested choices; a role's last seven days |
| `SupervisorRules` | `Roles/SupervisorRules.cs` | The rule supervisor's pure half: counts, actions, bounds, places, judge, effective set, act |
| `SupervisorRuleStore`, `SupervisorRuleBook`, `RuledPolicy` | `Roles/SupervisorRule*.cs`, `Roles/RuledPolicy.cs` | `supervisor_rules`; reached mark; the fresh seed; what each executor reads; old doors refused |
| `QuestionSupervisorDecisions`, `SupervisorWhy`, `SupervisorBounds` | `Roles/QuestionSupervisorDecisions.cs`, `Roles/SupervisorDecision.cs` | The agent supervisor's decisions in memory, its lifecycle, refusals and bounds |
| `SessionsSuperviseCommand`, `SupervisorQuarantine`, `HolderKey` | `Roles/SessionsSuperviseCommand.cs`, `Roles/SupervisorQuarantine.cs`, `Roles/SupervisorProof.cs` | The holder's verb; unattributed words taken back; the key, its proof and the role terminal's verbs |
| `StandingConfirmation`, `RoleHolders` | `Modules/Platform/StandingConfirmation.cs` | Appointments, generations, holders, the two offered flags |
| Steward | `Roles/StewardCommand.cs`, `Modules/Bridge/StewardActRows.cs`, `Modules/Platform/RoleActs.cs` | `steward <act>` over `ROLE_ACT`; the eight acts; pending operations |
| `StartAdmission`, `CallTable` | `Participants/Domain/StartAdmission.cs`, `Service/Daoris.Service.Mcp/CallTable.cs` | Who may ask for a co-worker; which tools each work is served |
| `HelpProposals`, `HelpProposalKinds` | `Help/HelpProposals.cs`, `Help/HelpProposalKinds.cs`, `Help/Proposals/` | Ask Daoris's 16 proposal kinds, each judged by its route's code and applied by the person |
| `HelpModes`, `HelpSwap`, `HelpHousekeeping`, `HelpSettings`, `HelpReceipt` | `Help/HelpMode.cs` and its neighbours, `Help/HelpWired.cs` | Access mode, its change at a turn, configured access, what a start handed |
| The page | `Web/agents/roles/`, `Web/agents/setup/` | One role editor and policy form, appointed roles, structure and call table; a place's Roles, its supervisor, the conversion |

## Its rules

**The roles**

1. A role is a record of the role catalogue in the desktop store. This build configures working roles only: a name
   (authored text), the works it takes, a scope, and a staffing. A role carries no doors of its own. (from D212, D222)
2. The works are build, design, chat, ask-daoris, intake, help, partner, review and brainstorm. A record's work is read
   from its marks by one rule, never stored. Design falls to the role that takes build when none takes design. (from D212)
3. Eight roles are seeded: build, intake, opinion, helper, partner, brainstorm, ask-daoris and chat, machine-wide. An id
   reads as its seed until the store holds a record of it, so an upgrade writes nothing. (from D205, D212)
4. The most specific scope's role takes a work: repository, then workspace, then machine. At most one role takes a work
   in one scope (`two-roles-one-work`). (from D212)
5. Only Intake and Ask Daoris may be off. A work that may not be off keeps a role on the machine (`may-not-be-off`). A
   removed role's work holds its starts, said, until the person gives it a role. (from D205, D212)
6. A record that does not read holds the starts that read it, said with its error. It is never read as the seed. (from D212)
7. `supervisor` and `steward` are reserved ids. No added role takes them, and a record with `filled: appointed` is
   refused `appointed-not-configurable`. (from D208, D212)
8. A start keeps its role and the role's revision on its binding, and a census holds the two to the work. Changing or
   removing a role touches no running session; a continuation reads the role that takes its work now. (from D212)
9. One door changes roles: `RoleDoor`, reached by `SET_ROLE`, `SET_ROLES`, `daoris driver role` and `role_propose`
   applied by the person's press. Each answers the judge's closed codes alike. A press carries the revision it read. (from D212, D228)
10. A batch is judged whole before anything is written. A role, its staffing, its policy and a place's rules land in one
    store transaction or not at all. (from D226, D229)

**Staffing**

11. A staffing is an order of one to four choices, each `{ agent, model?, effort? }` in the tool's own words. An absent
    model or effort is *account default*: nothing is passed. One judge serves every door (`ActorChoices.Judge`). (from D205)
12. The staffing lives in `role_staffing`. `driver.json`'s `actors`, `adapter`, `intakeAdapter` and `helperAdapter` are
    imported once; a fresh home is seeded with each role's seed. (from D205, D212)
13. Until the home is converted, every staffing write is copied onto those four keys for an older build. A change found
    there waits for *Take it* or *Dismiss* (`driver actor found`). (from D205, D228)
14. A role of the five with no stored order runs *as before*, from its old single-agent key at account default. (from D205)
15. The staffing part of the conversion door writes Build, Intake, Ask Daoris, Chat and Brainstorm as they run, at account
    default, Intake or Ask Daoris with no key written off. It keeps an unreadable entry, leaves Second opinion, Partner and
    Helper to their caller's rule, removes the four keys and keeps them for Undo, and ends the copy. (from D228)
16. A start walks its role's order choice by choice, each choice's accounts before the next. A choice that cannot run is
    passed with a code. Only the choice taken counts under the cap. (from D205, D197)
17. With no choice ready, a driven start holds with one sentence naming the soonest way out. A conversation's press is
    refused in that sentence. (from D205)
18. The rules say who may; the choices say with which model. A fence (reviewers, helpers, partners, brainstorm agents)
    walks the role's entries for each listed agent, else `rule-default`. A pick and a handed agent walk as given. (from D205)
19. A running session never moves to another choice. A model refused at the open asks the walk again once. A refusal
    after a prompt was sent is never a fallback. A resume applies the record's choice again. (from D205)
20. The tier that ran is said. The record keeps `actor`, `choice`, `asked`, `ran` and `chosen`; the opening note, the
    head and the machine log (`actor.chose`, `actor.passed`, `actor.moved`) say it. (from D205, D226, D143)
21. Suggested choices come from one dated table, held to `docs/model-roles.md` by a census. They apply only by the
    person's press: the card, or `role suggested --use`. (from D205, D174)

**A role's policy**

22. A policy is a row of `role_policy`, bound by a digest to the choices it was set for. Daoris's own writes of the choices
    re-key it. Any other writer leaves it *stale*, and the role walks its order and says so. No row means `order`. (from D226)
23. Two rules exist. `order` is the default. `weight` is a deterministic pick over the role's last 20 new starts, ties by
    order, two starts in one look reserved apart; a weight is 0 to 10. (from D226)
24. A choice may take only some of the role's works, hold a share of the window (1 to 99%), and keep a daily budget of
    starts or reported tokens in the machine's local day. A budget acts at a start only. (from D226)
25. Presets fill the form and never save: *First that can run*, *Spread evenly* and *Spread by weight*; the two spreads
    move a limit by weight, at most 3 moves. (from D226)
26. One pure judge, `StaffJudge.Arrange`, orders the role's own entries inside the one walk. The account walk inside a
    choice is unchanged; no account is named or assigned. (from D226)
27. A build offers only what it enforces. The fields are the rule, weight, share, both budgets, works, the moves on a limit,
    a transient episode, a fatal error and a quiet stop, and `most`. Any other field is refused and nothing saved. (from D226)
28. A signed-out sign-in, a refused model and a reached budget are passes of the walk, never fields. (from D226)
29. A move to another choice is a new part handed the brief at a part's end, never mid-turn. An uncertain send is never
    replayed. It is said as *move to another choice*, never *switch*. (from D226)
30. On a home whose rules are not reached, a policy's moves and `most` bound its lines: 3 moves in 24 hours once any move
    is set. On a reached home they are refused, naming `role rules`; a preset writes them as rules naming the role. (from D226, D229)

**The rule supervisor**

31. A rule is *when this count reaches n here, do this*. The counts are answers alone in a row, failed sessions since
    *Try again*, failures in a row on one choice, transient tries, a quiet end, a limited end, and moves in 24 hours. (from D229)
32. The actions are hold, ask, wait (5 to 600 minutes), next, weighted, refer and off, each count allowing its own set.
    `refer` (ask the agent supervisor) is not offered yet. (from D229)
33. A place is the machine, `workspace:<id>`, `repository:<id>` or `line:<key>`. For each count the most specific place
    holding a rule decides, with no layering. Rules naming the role beat those naming none; `off` ends inheritance. (from D229)
34. A write is refused where any place's effective set has no tries rule, a move with no moves rule, or failures on one
    choice acting at or past failures. A place's set is written whole at the revision read. (from D229)
35. A home is reached when the machine's place holds a revision. A fresh home is seeded at its first start: answers alone
    at 2, failures at 3 (the default `strikes`), tries at 5. A home with a `driver.json` is never seeded. (from D229)
36. On a home not reached every rule write is refused `convert-first`, and today's numbers run: `strikes`, Daoris's own
    answers-alone bound and the policies' moves. (from D229)
37. A rule decides at the executor that acts on its count: answers alone in the admission's `at`, failures at the
    planner's start, tries at a run's start and past its episode, conditions where the staffing course reads a move. (from D229, D212)
38. A rule acts between turns only. It never acts on an unknown count, never ends a running or quiet session, and never
    answers a question. Each hold is released by its own door. (from D229)
39. On a reached home `SET_STRIKES` and `settings strikes` are refused, naming `role rules`. The cap stays a machine
    setting outside the rules. (from D229)
40. Rules are read with `driver role supervisor` and written with `driver role rules`, `SET_ROLE`/`SET_ROLES` naming
    `supervisor`, or `role_propose`. Any other field of the supervisor is `appointed-not-configurable`. (from D229)

**Appointed roles**

41. The owner appoints a role in the window with their own press (`SET_APPOINTMENT`), per scope, off by default. No verb,
    Ask Daoris door, card or grant appoints one. Ask Daoris may explain the supervisor, never appoint or overrule it. (from D208)
42. Each role has its own offered flag, and both are off in this build. Every appointment, answer, defer and steward act
    is refused `not-offered`. Only the `terminal` holder is offered once lit; `actor` is not. (from D208, D209)
43. An appointment is kept in the standing confirmation's record with a generation every act carries, never the key or
    its hash. It lasts until Daoris restarts, and one found at a start is dormant until *Appoint again*. (from D208, D209)
44. The holder is a dedicated session in a tab the window starts, outside every repository, on a configuration Daoris
    writes, running only read tools and its roles' verbs. Every other verb refuses `role-terminal`. (from D208, D209)
45. The holder key is drawn at the appointment, kept only as a hash in memory, and stripped from every other start. The
    verb sends an HMAC proof over the hello's nonce, only to the install's own executable. (from D208, D209)
46. The supervisor answers a parked session's recorded question with one offered choice and one to five sourced reasons
    (`sessions supervise --choice`), or leaves it to the owner with a bound (`--leave`). (from D208)
47. It never answers a go-ahead, a sign-in, an access widening, spending, a deletion, a question the repository says to
    ask the person, a preference, an unrecorded or contradicting one, or one that directs it. (from D208)
48. It is also refused on a question the agent marked the owner's, past the answers-alone bound, and where a person's
    rule holds the line (`rule-holds`). The rule is read first and its act stands. (from D208, D229)
49. A decision is held whole in memory for the overrule window (1 to 60 minutes, 5 by default), then released in one
    conditional step. Its word is `From: supervisor`, never the person's; an unattributable one is quarantined. (from D208)
50. The owner's own words supersede a supervisor's decision; *Overrule* takes it back until the move to working admits
    it. (from D208)
51. The steward, scoped to this Daoris, makes eight acts with no card: drive, undrive, trees, hold, resume, stop, finish
    and try again, each one exact form over `ROLE_ACT`. Everything else is never a role's act. (from D209)
52. What the owner does to a repository, a quest or a session makes it the owner's for the run (`owner-set`). Every
    release the steward writes is kept in `roleReleases`, `by: steward`. (from D209)

**What a working role may do**

53. A session is served the tools of its work alone, by the call table, which the page shows read-only. (from D212)
54. Only a build, design or chat session may ask for a co-worker. A helper, a reviewer, an intake and Ask Daoris may
    not. (from D212, D203)

**Ask Daoris**

55. Ask Daoris answers from the records, takes the person to a place (`go`), starts work as an ask the intake turns into
    quests, or proposes a change. A quest's or session's own acts stay in its own conversation: accepting a departure and
    handing a landed branch to a landing plugin are the person's own presses, never proposed. (from D161, D193, D233)
56. In restricted mode, the default, it reads and proposes. Each proposal is a kind under `Help/Proposals/`, judged by
    its route's own code against the machine now, and applied only by the person's *Apply* through that route. (from D89, D170)
57. Restricted mode adds the access the person configures: housekeeping, chosen settings and checkouts, each request
    judged at the call by the door's own judges. No settings grant reaches a role. (from D183, D205)
58. Full access runs its harness's unrestricted mode. Only the person chooses it (Settings, the panel's chip, `help
    mode`), never Ask Daoris. A change of mode or access takes the next turn and is said. (from D183)
59. What each start handed is recorded at the start and shown on the panel, never worked out again. (from D183)
60. Every driver verb and Settings control is a proposal's door, a door owed, or exempt with a reason, held by
    `HelpCoverageTests`. A door owed is said as owed. (from D110, D170)
61. A plugin's servers and skills reach Ask Daoris only where its manifest declares them for it, approved by the person
    in restricted mode. A plugin changes Daoris only through the proposal doors. (from D170, D183)
62. The room says each role's staffing, policy and last seven days, so *why did this run on Opus?* is answered from
    the records. (from D226)

## Where a change goes

- **A new work or kind of start:** `RoleWork`, `StartPlan.RoleOf`, the seed in `Defaults/roles.json`, and the participant's
  work rule (`Participants/Domain/ParticipantWork.cs`). Tools for it go in the service's call table and its fixture.
- **A role's field or judge:** `RoleCatalog` and `RoleRecords`, reached only through `RoleDoor`. No second writer.
- **Staffing:** `RoleStaffing.Set` is the one writer. A reader of an order that is not the start's goes through
  `RoleOrders` and is named in `OrderReadersCensusTests`.
- **A policy field:** `StaffPolicy`, `StaffJudge`, then `StaffCapabilities.Fields` in the row that enforces it.
- **A supervisor count or action:** `SupervisorRules` (windows, allowed actions, bounds), the count in
  `Course/Domain/LineCounts.cs`, and the executor's read through `SupervisorRuleBook`. No course step.
- **The agent supervisor:** `QuestionSupervisorDecisions`, `SupervisorDecision`, `Modules/Bridge/SupervisorDoor.cs`.
- **A steward act:** a row in `Modules/Bridge/StewardActRows.cs`, behind its own look.
- **An Ask Daoris door:** a file of its own in `Help/Proposals/`, registered by one line in `HelpProposalKinds.All`, with
  the service's writer `HelpProposalBox.<Kind>.cs`, the page's card, and its coverage row.
- **The page:** `Web/agents/roles/` and `Web/agents/setup/`.
- **An old way kept:** an entry in `Transitional/Register.cs`.

**Tests that pin it.** Driver: `Roles/*Tests` (catalogue, door, verbs, staffing, walk, judge, ledger, rules, decisions,
holder key, steward), `Conversion/StaffingPartTests`, `Course/LineCourseTests`, `Course/StaffCourseLookTests`,
`Loop/StaffPolicy*TickTests`, `Help/HelpCoverageTests`, `Help/HelpProposalKindsTests`, `Help/Proposals/*Tests`, and
`Knowledge/HelpRoom*Tests` with the room goldens. Modules: `Bridge/DriverModuleRoleTests`, `DriverModuleAppointmentTests`,
`Platform/StandingConfirmationTests`, `RoleActTests`, `Holder*Tests`, `SupervisorScopeTests`. Service:
`Help/HelpRoleProposalTests`, `Help/HelpProposalBoxKindsTests`, `Mcp/McpRoleFilterTests`, `Sessions/SupervisorWordTests`.
Web: `agents/roles/*.test.ts(x)` and `agents/setup/*.test.tsx`.

**Gates that bite.** `driver`, `modules`, `censuses`, `service` (a proposal's twin, the call table), `web-unit` and
`web`, `cli` (`npm run verify`: the transitional markers, the size ratchet), and `orient-index`, `test-map`, `code-map`.

## Contradictions

1. D212 point 16 (supervisor and steward are seeded catalogue roles; an appointed shape) vs `Roles/RoleCatalog.cs:35,49,281`:
   the catalogue holds working roles alone and reserves both ids. Code reads right.
2. D229 point 5 (a rule is a holder of the supervisor role) vs `Modules/Platform/StandingConfirmation.cs:172-176` (holders
   are `terminal` and `actor`; rules are a store set through the role door). Same meaning; the code's split reads right.
3. D229's 2026-11-04 note (`daoris role rules`, `daoris role supervisor`) vs `Roles/RoleVerbs.cs:130` (`driver role rules`);
   `Roles/RuledPolicy.cs:25,35` print the note's form. The verb's grammar reads right.
4. D226 (seven conditions, each with its actions) vs `Roles/StaffPolicyDoor.cs:12-34` (moves for four; signed-out,
   model-refused and budget are the walk's). Code reads right.
5. D226 (moves and `most` live in the policy) vs D229 and `Roles/RuledPolicy.cs:14-26` (on a reached home they are the
   supervisor's rules). D229 reads right; D226's hold only on a home not reached.
6. D205 points 2 and 7 (choices in `driver.json`'s `actors`) vs `Roles/RoleStaffing.cs:92-107` (`role_staffing`; the file
   is a copy until conversion). Code reads right.
7. D205 points 8-9 (Settings → Actors, `driver actor`, `ACTORS`/`SET_ACTOR`, `actor_propose`) vs `driver role`,
   `ROLES`/`SET_ROLE` and `role_propose` (`Help/Proposals/HelpRoleProposals.cs:20`); `driver actor` survives only as
   `actor found` (`Roles/ActorVerbs.cs:39`). Code reads right.
8. D89 (Ask Daoris's agent named under Settings → *Daoris's own AI* as a third job) vs the `ask-daoris` role's staffing
   (`Defaults/roles.json`, `Settings/ActorKey.cs:20`). Code reads right.
9. D170 (never a shell) vs D183 full access (`Help/HelpMode.cs:17`). D183 reads right; D170 holds in restricted mode.
10. D209 point 1 and its COMPANYVIEW1e note, and D205's COMPANYVIEW1b note (appointed and staffed under Settings → This
    Daoris, then Agents → Roles) vs `Web/agents/modes.ts:3-8,29-33` (the Roles mode is retired; its sections sit under
    Agents → Setup). Code reads right.
11. D110 point 4 (a rule is never proposed by an agent) vs `role_propose` carrying the supervisor's rules
    (`Help/Proposals/HelpRoleProposals.cs:145`). D110 means permission rules; it should say so.
12. D208 points 4 and 8 (a workspace's supervisor later; held five minutes) vs SUPERVISOR1e as built (workspace
    appointments, `Web/agents/setup/PlaceSupervisor.tsx`; a window of 1 to 60, `StandingConfirmation.cs:183-184`). Code reads right.

## Open

- **CONTEXTDRIVE1e (rest):** `rule-holds` at the release, the `supervisor.rule` note, a rule's decision file,
  `SupervisorRuleActs`, and a workspace read as a repository's wider place.
- **CONTEXTDRIVE1f:** the conversion door's `rules` part, so an existing home is reached (`Conversion/` holds only the
  staffing and workflow parts).
- **CONTEXTDRIVE1g, h, k:** the rules' editors on the page (no page code reads them yet); a rule's hold on the head and
  *What needs you*; the room's and glossary's words.
- **CONTEXTDRIVE1n, j:** a conversation's course reads the rules; `refer`, the agent supervisor's *course* act.
- **CONTEXTDRIVE1l, m:** the kept reads (`answers-alone-bound`, `strikes-key`, `policy-moves`) and old doors deleted; the
  installed proof.
- **STAFFPOLICY1d/f/h/i:** an agent's own ask and a hand-over take no policy; the use line's moves by condition
  (`Roles/RoleUse.cs`); selection by measurement; the installed proof.
- **ONEWAY1af:** delete `actor-key-before` and `staffing-copy` once the staffing part's census ends.
- **SUPERVISOR1a-i (rest):** the author sweep test, the page's decision mark with *Overrule*, the go-on's process cases.
- **SUPERVISOR1a-iii:** the supervisor's activation (its flag on, on a candidate build).
- **QUESTIONUI1b, SUPERVISOR1d, SUPERVISOR1c, SUPERVISOR1f:** the pick on the card; the bench; the `actor` holder; words.
- **STEWARDROLE1d to 1h:** the steward's activation, hand-over, go-on, set-up, and free words.
- **ROLEPERM1, PERSONDOOR1c-i:** permission answers through appointed actors; the role's boundary at every door.
- **SETUPUX1y:** the places tree lit by default, retiring the Roles sections under Setup.
