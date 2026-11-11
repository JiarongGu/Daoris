# Roles and supervision — how it works now

## What it does

A role is who does a kind of work for the person. A working role takes one or more works (build, chat, review…) at a
place, and its staffing says which agents, models and efforts run it, in order. The person sets roles through one door:
the window, the terminal, or an Ask Daoris proposal they apply. A role may carry a policy: weights, shares, budgets and
the moves a line makes to another choice. The supervisor watches a line's counts. Its rules are the person's, read with no
model: *when this count reaches n here, do this*. The supervisor and the steward are catalogue roles of the appointed shape,
staffed through the same door; one held by the terminal acts once the window starts its terminal. Ask Daoris is the front desk: it reads the machine, takes the person to a place,
starts an ask, or proposes a change the person applies. In full access it acts itself.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Modules/`
(`src/Daoris.Desktop/Daoris.Desktop.Modules/`), `Service/` (`src/Daoris.Service/`) or `Web/` (`src/Daoris.Web/src/`).

| Unit | File | Its job |
|---|---|---|
| `RoleCatalog` | `Roles/RoleCatalog.cs` | The roles: the seed per id, the store's rows, the judge, the role that takes a work at a place, the appointed role of a kind at a place |
| `RoleHold`, `AppointedRoles`, `RoleHolders` | `Roles/RoleHold.cs` | The appointed shape: kind, acts, holder, the supervisor's window; the kinds' act vocabularies; its judge |
| `AppointedStaffing`, `AgentHold` | `Roles/AppointedStaffing.cs`, `Roles/AgentHold.cs` | An agent-held supervisor's order and policy at the door; its generation by role and revision |
| The agent's turn | `Loop/Look/SuperviseLook.cs`, `Loop/Supervise/SupervisePass.cs`, `Loop/Supervise/SuperviseTurn.cs`, `Roles/SupervisorInstruction.cs`, `Roles/SupervisorFacts.cs`, `Roles/SupervisorBlock.cs`, `Roles/SupervisorHolders.cs`, `Roles/SupervisorRuns.cs`, `Roles/ISupervisorDesk.cs`, `Modules/Bridge/AgentSupervisorDesk.cs`, `Participants/Supervise/SuperviseDoors.cs`, `Review/Domain/SupervisePermissions.cs`, `Service/Daoris.Service.Core/Sessions/SuperviseDesk.cs` | The look's pass, one attempt and its turn, the prompt and the facts it shares with the print, the answer's block, the agent's holder on a decision, the running turns, the decision door's half the look reaches, the record's open on either side, the turn's rules |
| `RoleRecord`, `RoleWork`, `RoleScope` | `Roles/RoleRecords.cs` | A role's record; the ten works; machine, workspace or repository scope |
| The seed | `Defaults/roles.json` | The ten seeded roles, machine-wide; Design's order is `ActorChoices.DesignSeed`, Exact build's `ActorChoices.ExactSeed` |
| `RoleDoor` | `Roles/RoleDoor.cs` | The person's one door: judges a press, then writes role, staffing, policy and rules in one transaction |
| `RoleVerb` | `Roles/RoleVerbs.cs` | `daoris role` and `role set/remove/default/policy/rules/supervisor/suggested` |
| `RoleStaffing` | `Roles/RoleStaffing.cs` | `role_staffing` in the desktop store: import, the one writer |
| `StaffingProjection`, `StaffingStep` | `Roles/StaffingProjection.cs`, `Transitional/Migration/StaffingStep.cs` | The store's rows as a record's fields; the migration's staffing step |
| `ActorChoices`, `ActorKey` | `Roles/ActorChoices.cs`, `Settings/ActorKey.cs` | An order of 1 to 4 choices, its effective entries, its judge, who may be off |
| `StartPlan`, `ReaskMove` | `Roles/StartPlan.cs`, `Roles/OneTurnReask.cs` | The one walk over a role's order, work to role, pass codes; the one re-ask |
| `StaffPolicy` | `Roles/StaffPolicy.cs` | The policy record: rule, entries, presets, digest, judge. Its `Moves` and `Most` are the course's read of a line's rules, set by `SupervisorRuleBook.Course`, never stored |
| `StaffJudge` | `Roles/StaffJudge.cs` | `Arrange` (weights, shares, budgets, works) and `OnCondition` |
| `StaffPolicies`, `StaffLedger`, `StaffHolds`, `StaffCapabilities` | `Roles/StaffLedger.cs`, `Roles/StaffHolds.cs`, `Roles/StaffPolicyDoor.cs` | `role_policy` and starts ledger; stale reads; this process's reservations and not-answering marks, one type whose every write moves the count the limit wait's stamp carries; the ledger's next change by the clock; the fields a build enforces |
| `RoleOrders`, `ActorSuggestion`, `StaffWeek` | `Roles/RoleOrders.cs`, `Roles/ActorSuggestion.cs`, `Roles/StaffWeek.cs` | The other readers' order; suggested choices; a role's last seven days |
| `RoleUse`, `RoleUseMoves` | `Roles/RoleUse.cs`, `Roles/RoleUseMoves.cs` | A role's week from the machine log: starts per choice, those that fell and why (`actor.chose`), its moves by condition (`actor.moved`) |
| `ApprovedChoice` | `Course/HandOver/ApprovedChoice.cs` | The choice a hand-over is approved on: an agent's own ask takes the arranged order's step for its agent; a handed quest's file keeps its model and effort, and its start takes them while the role still lists them |
| `SupervisorRules` | `Roles/SupervisorRules.cs` | The rule supervisor's pure half: counts, actions, bounds, places, judge, effective set, act |
| `SupervisorRuleStore`, `SupervisorRuleBook`, `RuledPolicy` | `Roles/SupervisorRule*.cs`, `Roles/RuledPolicy.cs` | `supervisor_rules`; the fresh seed; what each executor reads; a preset's rules |
| `SupervisorRuleActs`, `SupervisorRuleHold`, `RuleWiderPlaces`, `RuleWiderPlaceStore` | `Roles/SupervisorRuleActs.cs`, `Roles/SupervisorRuleHold.cs`, `Roles/RuleWiderPlaces.cs`, `Roles/RuleWiderPlaceStore.cs` | A rule's act said, kept and read back; `rule-holds`; a repository's workspace as its wider place, kept in the store from the look's registry. A Planned look pass (`RuleActsLook`, `PlannedRuleActs`) records a park, a transient hold, a rule's move and the moves bound once per session; the supervisor's print lists them (`LineFacts.Acts`) |
| `QuestionSupervisorDecisions`, `SupervisorWhy`, `SupervisorBounds` | `Roles/QuestionSupervisorDecisions.cs`, `Roles/SupervisorDecision.cs` | The agent supervisor's decisions in memory, its lifecycle, refusals and bounds; the class keeps one-line doors |
| `SupervisorCourses`, `ReferredPoint`, `ReferredPoints`, `SupervisorCourseView`, `SupervisePointPrint` | `Roles/SupervisorCourses.cs`, `Roles/ReferredPoint.cs`, `WorkOrders/ReferredPoints.cs` (it reads Course's go-on marks and the planner's considerations), `Roles/SupervisorCourseView.cs`, `Roles/SupervisePointPrint.cs` | A `refer` point's course decision kept, held, released or overruled; the open point (answers alone's mark, or a quest's parked or asking verdict), the line's earlier points (`Passed`) and its release door; the decision read back from memory and its files (`SupervisorReads.CoursesOf`); the holder's print of a point. A quest's go-on is released by `CourseReleaseLook` (`Loop/Look/`) |
| `SupervisorDecisionDoor`, `ISupervisorKind`, `QuestionKind`, `CourseKind`, `SupervisedParks`, `SupervisorSubject`, `SupervisorDecideForm`, `SupervisorLeaveForm` | `Modules/Bridge/SupervisorDecisionDoor.cs`, `Modules/Bridge/SupervisorKinds/`, `Modules/Bridge/Routes/SupervisorDecisionRoutes.cs`, `Roles/SupervisorSubject.cs`, `Roles/SupervisorDecisionForms.cs` | The supervisor's one decision door: `SUPERVISOR_DECIDE`, `SUPERVISOR_LEAVE` and `SUPERVISOR_WAITING`; each kind one row with its waiting subjects, its keeping and so its release; the question kind's park read; the typed subject per kind; the two exact forms the proof is over |
| `SupervisorAllows`, `SupervisorAllowView`, `StewardAllows`, `StewardLandRoutes`, `SupervisorAllowCard` | `Roles/SupervisorAllows.cs`, `Roles/SupervisorAllowView.cs`, `Modules/Platform/StewardAllows.cs`, `Modules/Bridge/Routes/StewardLandRoutes.cs`, `Web/work/head/SupervisorAllowCard.tsx` | The allow decisions kept, moved and read back as `SESSION_SUPERVISOR`'s `allows`; the steward's request waiting for the allow supervisor and its allowed `land` in its window with the person's *Overrule* |
| `SupervisorBook`, `SupervisorDecisionFiles`, `SupervisorKeeping`, `SupervisorReleasing`, `SupervisorSweeps`, `SupervisorTickets`, `SupervisorReads`, `SupervisorAttempts` | `Roles/Supervisor*.cs` | The decisions' shared memory (live decisions, session locks, closed questions) and its one move in the book; the files, keeping, releasing, sweeps, tickets and reads each reach it and keep no state; an agent supervisor's attempts beside the decisions |
| `SessionsSuperviseCommand`, `SupervisorQuarantine`, `HolderKey` | `Roles/SessionsSuperviseCommand.cs`, `Roles/SupervisorQuarantine.cs`, `Roles/HolderKey.cs`, `Roles/SupervisorProof.cs` | The holder's verb; unattributed words taken back; the key and the role terminal's verbs; the proof |
| `KeyedProof`, `DelegateKey`, `SessionDelegates`, `DelegateProof`, `PipeDelegate` | `Platform/KeyedProof.cs`, `Platform/DelegateKey.cs`, `Agents/SessionDelegates.cs`, `Instance/DelegateProof.cs`, `Modules/Platform/PipeDelegate.cs` | The key and the HMAC proof the holder and a session share (`KeyedProof`, which `HolderKey` and `SupervisorProof` call); the key every session's start is handed (`DAORIS_DELEGATE_KEY`) and its verb takes; the hashes kept by session; the proof over a press's route, payload and key; the pipe's tier for a proved press |
| `StandingConfirmation` | `Modules/Platform/StandingConfirmation.cs` | The roles' terminals this run: holds, generations, holders; the act judge over the catalogue. The doors; the memory is `StandingState`'s under its one lock, which `RoleTerminals` (starts, catalogue changes, a holder's end), `RoleJudge` (the role method, the decisions' admission), `RoleActsLedger` (acts, operations, the owner's marks) and `StewardRecord` (the steward's acts this run) reach |
| Steward | `Roles/StewardCommand.cs`, `Roles/StewardAsked.cs`, `Modules/Bridge/StewardActRows.cs`, `Modules/Bridge/StewardRouteAct.cs`, `Modules/Bridge/StewardHandOverAct.cs`, `Modules/Bridge/StewardPauseAct.cs`, `Modules/Bridge/StewardUpdateAct.cs`, `Modules/Bridge/StewardTargets.cs`, `Modules/Bridge/Routes/QuestEndRoutes.cs`, `Modules/Platform/RoleActs.cs`, `Modules/Platform/StewardAsk.cs` | `steward <act>` over `ROLE_ACT`, and its words when an act asks the owner; the eighteen acts and the set-up act, the route acts among them; pending operations; an act outside the granted ones sent on as its person's press |
| `StartAdmission`, `CallTable` | `Participants/Domain/StartAdmission.cs`, `Service/Daoris.Service.Mcp/CallTable.cs` | Who may ask for a co-worker; which tools each work is served |
| `HelpProposals`, `HelpProposalKinds` | `Help/HelpProposals.cs`, `Help/HelpProposalKinds.cs`, `Help/Proposals/` | Ask Daoris's 16 proposal kinds, each judged by its route's code and applied by the person |
| `HelpModes`, `HelpSwap`, `HelpHousekeeping`, `HelpSettings`, `HelpReceipt` | `Help/HelpMode.cs` and its neighbours, `Help/HelpWired.cs` | Access mode, its change at a turn, configured access, what a start handed |
| `SupervisorResolution`, `SupervisorGrants`, `SupervisorKinds`, `SupervisorDeciders`, `SupervisorConversion` | `Roles/Supervisor{Resolution,Grants,Kinds,Deciders,Conversion}.cs` | Who decides a kind at a place; the one grant check; the kinds and their acts; `ROLES`' `deciders`; the door's `not-converted` until the migration record (`Store/MigrationRecord.cs`) lists `supervisor-kinds` |
| The page | `Web/agents/roles/`, `Web/agents/setup/` | One role editor and policy form, appointed roles, structure and call table; a place's Roles, its supervisor, *Who decides here* (`WhoDecidesHere`, `whoDecides.ts`) |

## Its rules

**The roles**

1. A role is a record of the role catalogue in the desktop store, in one of two shapes. A working role: a name (authored
   text), the works it takes, a scope, and a staffing. An appointed role: a name, its kind (supervisor or steward), the acts it
   holds of that kind's vocabulary, a scope, its holder, and the supervisor's window. A role carries no doors of its own. (from D212, D222, D233)
2. The works are build, design, exact, chat, ask-daoris, intake, help, partner, review and brainstorm. A record's work is read
   from its marks by one rule, never stored. Design and exact fall to the role that takes build when none takes them. (from D212)
3. Ten roles are seeded: build, design, exact, intake, opinion, helper, partner, brainstorm, ask-daoris and chat, machine-wide.
   An id reads as its seed until the store holds a record of it, so an upgrade writes nothing. Design takes design work
   (a quest its publisher or the intake marked so) on Claude Code · `opus` · high, the reference's judgement row, the quest
   naming no model; a home from before it reads that seed for it, and a workflow step may name it. Removed, design work
   falls to Build's. Exact build takes exact work (a quest its publisher marked `weight: exact`, the pack naming the change)
   on Claude Code · `sonnet` at the account's default effort, the reference's build-exact row, the quest naming no model; a
   longer window (`sonnet[1m]`) is the person's to choose from their account's offers. It is read as Design is: a home from
   before it reads its seed, a workflow step may name it, removed its work falls to Build's, and a start says whose order it ran
   on. A quest marked neither keeps Build's. A seeded role is edited, restored and removed by the one door; no suggestion adds
   it. (from D205, D212)
4. The most specific scope's role takes a work: repository, then workspace, then machine. At most one role takes a work
   in one scope (`two-roles-one-work`). (from D212)
5. Only Intake and Ask Daoris may be off. A work that may not be off keeps a role on the machine (`may-not-be-off`). A
   removed role's work holds its starts, said, until the person gives it a role. (from D205, D212)
6. A record that does not read holds the starts that read it, said with its error. It is never read as the seed. (from D212)
7. `supervisor` (held by the person's rules) and `steward` (held by no one) are seeded machine-wide; a seeded id keeps its
   shape and kind (`mixed-shape`). The steward works on the machine alone, is never held by rules, and takes no agent and no
   choices (`appointed-not-configurable`). The supervisor's holder is rules, the terminal or an agent, its window 1 to 60
   minutes. An agent-held supervisor's staffing is a working role's (`AppointedStaffing`): one to four choices, judged by the one
   order judge (`ActorKey.JudgeOrder`), kept as its own `role_staffing` row through the door's one write, and its policy a working
   role's but for works, its preset writing no rules; one with no choice, and choices on a supervisor held by rules or the
   terminal, are refused `choices`, and moving the holder off the agent deletes the row in the same transaction. A supervisor's `decides` names the kinds of decision it answers (`SupervisorKinds`: `question`,
   `course`, `limit`, `allow`, `files`, `landing`); none makes it its scope's generic supervisor, which answers what no kind
   takes there. The `allow` kind decides a permission request held in the asking mode at its session's repository (`allow.once`,
   `allow.reject`, ended through the hold's own end) and a widening at its rule's reach (`allow.standing`, `allow.reject`,
   settled recorded by the role), and a working session's asked press `{ press }` at its session's repository (`allow.once`, offered before
   the person's card and run as the session's own; a leave or ten minutes falls to the card; Ask Daoris's press is never offered), each taking effect at its keeping (`AllowKind`) and kept as an allow decision (below). The `files` kind decides an act a person's `ask`
   in the rule file held at a start (`files.allow-run`, `files.allow-copy` at the repository, `files.allow-share` at the narrowest
   place covering both repositories), by writing the held act's own text, never the `ask`'s pattern, into the person's own layer
   of each repository it reaches in one write of the file (all layers or none), never the machine's, with the role and the decision (`FilesKind`, `FileAllows`); an ask naming
   the person (`run(…)@person`) is listed for no supervisor. Its `authority` grants acts as `<kind>.<act>`, only of the kinds it decides, each off on a new record; the seed
   grants `question.answer`, `course.go-on` and `limit.move`. A scope holds one generic supervisor and one per kind
   (`two-roles-one-work`); the machine's generic supervisor may not be removed, scoped away or given kinds (`may-not-be-off`).
   The role door writes no supervisor record before the home's `supervisor-kinds` migration step is recorded (`not-converted`);
   that step first keeps this build as the home's newest build and reads it back, then grants a stored `answer` as the seed's
   three and drops `defer` in `roles` and every `role_revisions` row, and gives each decision file its `kind`. (from D233)
8. A start keeps its role and the role's revision on its binding, and a census holds the two to the work. Changing or
   removing a role touches no running session; a continuation reads the role that takes its work now. (from D212)
9. One door changes roles: `RoleDoor`, reached by `SET_ROLE`, `SET_ROLES` and `daoris role`, Ask Daoris's press of it
   shown on the Confirm card by `RoleChangeView`. Each answers the judge's closed codes alike. A press carries the revision it read, which the catalogue's write compares with the revision its own transaction
   reads (`RoleCatalog.WriteIn`), so two presses read at one revision cannot both write; the terminal's verb sends the revision it planned against, and `role suggested --use` is one batch. (from D212, D228)
10. A batch is judged whole before anything is written. A role, its staffing, its policy and a place's rules land in one
    store transaction or not at all. (from D226, D229)

**Staffing**

11. A staffing is an order of one to four choices, each `{ agent, model?, effort? }` in the tool's own words. An absent
    model or effort is *account default*: nothing is passed. One judge serves every door (`ActorChoices.Judge`). (from D205)
12. The staffing lives in `role_staffing` alone, and every write is the store's. A fresh home is seeded with each role's
    seed. (from D205, D212, D232)
13. The release's migration imports an older home's `driver.json` staffing once, writes Build, Intake, Ask Daoris, Chat and
    Brainstorm as they ran, at account default, Intake or Ask Daoris with no key written off, keeps an unreadable entry,
    leaves Second opinion, Partner and Helper to their caller's rule, and takes the four keys out. (from D232)
14. A role of the five with no stored order has no agent: nothing reads the old single-agent keys. (from D232)
15. A change of `driver.json`'s staffing by hand is read nowhere. (from D232)
16. A start walks its role's order choice by choice, each choice's accounts before the next. A choice that cannot run is
    passed with a code, a provider not opened to the start's place `not-opened` before its accounts are asked. An account
    cooling for one model family passes that family's choices and serves the others. Only the choice taken counts under the
    cap. (from D205, D197, D212)
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
    order, two starts in one look reserved apart, a reservation given back when its walk refuses or throws, or its start ends before the record opens (held, refused, thrown); a weight is 0 to 10. (from D226)
24. A choice may take only some of the role's works, hold a share of the window (1 to 99%), and keep a daily budget of
    starts or reported tokens in the machine's local day. A budget acts at a start only. (from D226)
25. Presets fill the form and never save: *First that can run*, *Spread evenly* and *Spread by weight*; the two spreads
    move a limit by weight, at most 3 moves: they write those as rules naming the role, beside the policy. (from D226, D229)
26. One pure judge, `StaffJudge.Arrange`, orders the role's own entries inside the one walk. The account walk inside a
    choice is unchanged; no account is named or assigned. (from D226)
27. A build offers only what it enforces. The fields are the rule, weight, share, both budgets and works. A stored
    `moves` or `most` is passed over and the next write holds neither. (from D226, D229)
28. A signed-out sign-in, a refused model and a reached budget are passes of the walk, never fields. (from D226)
29. A move to another choice is a new part handed the brief at a part's end, never mid-turn. An uncertain send is never
    replayed. It is said as *move to another choice*, never *switch*. (from D226)
30. A policy holds no moves and no `most`, and `role policy` takes no `--on` or `--most`. The lines read their moves and
    bound from the effective rules alone. (from D226, D229, D232)

**The rule supervisor**

31. A rule is *when this count reaches n here, do this*. The counts are answers alone in a row, failed sessions since
    *Try again*, failures in a row on one choice, transient tries, a quiet end, a limited end, and moves in 24 hours. (from D229)
32. The actions are hold, ask, wait (5 to 600 minutes), next, weighted, refer and off, each count allowing its own set.
    `refer` (ask the agent supervisor) is offered on five counts, each holding its line between sessions behind a door a
    supervisor's act can go through: answers alone (its mark), failed sessions since *Try again* (it parks as `hold` does; the
    quest's *Try again*), and failures in a row on one choice, a limited end and the moves bound (each asks as `ask` does; the
    quest's one approval). Limits and moves are a conversation's counts too, whose hold is released by its person's word, which no
    supervisor speaks, so a refer on them names roles taking driven work alone (build and design, `RoleWork.Driven`; the judge's
    `SupervisorRuleWorld.Driven`), and one for every role or naming another is refused `do`; a refer on limits takes 5 to 600
    minutes as `wait` does, the most a supervisor's wait holds the line. On tries and the quiet end it is refused `do`: a transient
    hold is no supervisor's, and a quiet end has none. `ROLES` names each count's `drivenRefer` and the `driven` roles, and the
    fold offers *Ask the supervisor* on limits and moves only to a rule naming those roles. (from D229, D233)
33. A place is the machine, `workspace:<id>` or `repository:<id>`; a line has no place of its own, so the judge refuses
    `line:<key>` (`place`) and the chain has no line step. A failure is one thing for the quest's count and its run on
    one choice: a failed end or a cut-off, never a limit, a refused account, a choice not offered, a transient end, an
    update's close or the quiet end (`SessionReads.IsFailure`). For each count the most specific place
    holding a rule decides, with no layering. Rules naming the role beat those naming none; `off` ends inheritance. A
    repository's workspace is its wider place at the judge in every process as at the lines: the look keeps the registry's
    in the store (`repository_workspaces`, the store's step 21; a registry that lists no repository leaves it as it was, one whose repositories name no workspace empties it) and the judge reads it there; `ROLES` answers each place's wider places,
    and the fold draws what a place's rules replace and inherit along that chain, for each role's lines. (from D229)
34. A write is refused where any place's effective set has no tries rule, a move with no moves rule, or failures on one
    choice acting at or past failures, and where two rules on one count at the place would apply to one line (`ladder`:
    both for every role, or both naming one role); a rule naming a role beside the every-role rule stands. The terminal's
    `--set` takes one step. A place's set is written whole at the revision read. No write asks for a conversion first. (from D229, D232)
35. One seed serves a fresh home's first start and the release's migration: answers alone at 2, failures at 3 (the
    migration: an older home's `strikes`), tries at 5 and moves 3 a day before the next asks, for every role; the
    migration takes `strikes` out and passes a stored policy's moves over. Where the machine's place holds nothing, or the
    store does not read, the seeded rules decide, and a write is judged against them too (`SupervisorRuleBook.WithSeed`), so a repository's
    rules are not refused for a machine bound every line already reads. (from D229, D232)
36. Every executor reads the effective rules, one rule per count: the answers-alone hold, the failures that park a quest,
    the tries of a transient episode and a line's moves. A count no rule watches acts on nothing. A line's role and place
    for its rules are resolved once (`RuledLine`): the role its start was bound to while the catalogue holds it, else the
    role taking its work at its place; the place its record's repository and workspace, a first start's the quest's
    repository and the registry's workspace. Every reader takes it: the hold, the facts, a conversation's rules, the
    planner, the course, a run's tries and the acts. (from D229, D232)
37. A rule decides at the executor that acts on its count: answers alone in the admission's `at`, failures at the
    planner's start, tries at a run's start and past its episode, conditions where the staffing course reads a move. (from D229, D212)
38. A rule acts between turns only. It never acts on an unknown count, never ends a running or quiet session, and never
    answers a question. Each hold is released by its own door. Its act is said on the line (`supervisor.rule`: the
    count, its value, the rule's number, place and act), kept as a final decision file of holder kind `rule` with no
    question, which the questions' reader and the sweep pass by, and logged; the answers-alone hold records it. The value
    is the count its executor read: the admission's, the failed sessions past *Try again*'s mark, the episode's tries
    (`ended.transient`'s count, also the line's `tries` count), the line's moves, the failures in a row on a choice; one
    hold is one act, and a new answer while a line waits joins its mark. Every watched count names its rule's place;
    a line whose rules did not read says so. (from D229)
39. The failed sessions that park a quest are the failures rule's alone: no door sets `strikes` and no configuration reads it.
    The planner's park carries its rule's number (`Consideration.ParksAt`), which *Try again*, Ask Daoris's `retry` and the
    terminal say; a set-up parks at its line's rule; `STATE`'s `strikes` answers the retried quest's rule, else the machine's;
    `driver list` points to `role supervisor`. The cap stays a machine setting outside the rules. (from D229, D232)
40. Rules are read with `driver role supervisor` and written with `driver role rules`, `SET_ROLE`/`SET_ROLES` naming
    `supervisor` with `rules`. A rules press carrying a role's field is `mixed-shape`. The terminal names a
    rule's roles after an `@` on each `--set`, `--off` or `--clear` (`SupervisorRulesFlags`), so the Confirm card's command is
    one that clears the place and writes the rules the judge accepted, and the card carries the place and its rules
    before and after as fields (`PressRules`) that the page words from its catalogue in both languages (`RulesChange`). The page writes
    them in a fold under each place's Roles (`SupervisorRulesFold`, over `ROLES`' `supervisor` sets and counts), whole
    at the revision read, each rule saying what it replaces and keeping its roles whole (a rule set for several roles is
    drawn with them and written back as read); a role's editor says the rules naming it and writes none,
    and its policy record carries the rule and choices alone (the web's `StaffPolicy` wire type names no `moves` or
    `most`). A held answer carries its rule (`AnswerRuleWords`), a parked quest the failures rule's number
    (`parksAt`), and a hold on an ask or the moves bound words the rule; the head, the foot and *What needs you* say
    one in one sentence, and *Change the rule…* opens the place whose rule it is (`ruleDoor`, handed down by the frame as
    `onChangeRule`; a place that is not the machine, a workspace or a repository opens nothing). `SESSION_SUPERVISOR` answers, beside the decisions, the line's `counts` (each with the rule it acts
    by) and the `acts` the rules made: *Details* draws each count and its Supervisor line, and the head's chip
    (`supervisionChip`) names a count a rule holds the line at or one short of it. The room says each place's rules
    (`HelpRoomRules`); the window says *supervisor rule* and *count*. (from D229)

**The supervisor and the steward**

41. They are staffed through the role door like every role (`SET_ROLE`, `driver role set --decides --acts --holder --window`,
    Ask Daoris's press of it). A supervisor of kinds or a generic one names `decides` (`[]` or `every` is the generic one),
    `authority`, `holder` (`rules`, `terminal`, or `agent` with its choices and policy as a working role's) and `window`. A press is
    refused `not-converted` until the home's `supervisor-kinds` step is recorded, the role door's judge refuses the rest (an
    unknown kind or act, an act of a kind not decided, an agent holder with no choices, two of a kind in one scope, choices its
    holder walks none of, work), and the Confirm card's view (`RoleChangeView`) says the supervisor's kinds, holder, acts and window
    before and after in the catalogue's words (`SupervisorChange`), applied by the person's press through the one door. The act judge reads the catalogue at each act, answer and admission. One resolver
    (`SupervisorResolution.Find`, `RoleCatalog.Supervising`) names who decides a decision of a kind at a place, kind first: the
    repository's, the workspace's, then the machine's supervisor deciding the kind, else the repository's, the workspace's, then
    the machine's generic one, the repository placed in the workspace `repository_workspaces` keeps. An unreadable appointed
    record covering the place, or a workspace map that does not read, makes the decision the person's. The admission
    (`Admit(kind, place, act)`) answers that supervisor's hold where its holder is the started terminal, or an agent's generation
    where an agent holds it, and `SupervisorGrants.Covers` grants the act, else `not-staffed`, `not-started` or `not-granted`; a
    leave asks no grant. The judge
    refuses a holder's decide its grants do not cover `not-granted` (a supervisor of a kind deciding another kind among them), the
    keeping refuses one whose hold no longer answers the kind at the subject's place `role-changed` (a generic supervisor where a
    supervisor of the kind is effective), and the keeping, the releases and the tickets ask the admission with the decision's kind and
    act, so a grant taken away by any process stops the next step. `ROLES` answers the same
    resolution as `deciders` (`SupervisorDeciders`): for the machine, each place an appointed role's scope or `repository_workspaces`
    names, and a repository no place names, a line per kind naming the supervisor, its place, its holder and the kind's granted acts,
    or the resolver's sentence. A place's Roles words them as *Who decides here* (`WhoDecidesHere`): the supervisor and where it is
    set, *your rules, then you* where the person's rules hold it, *you (… may not …)* where it is granted none of the kind's acts, or
    *you* with the sentence; *Add a supervisor for…* offers the kinds no supervisor placed there decides and opens the role editor
    with that kind, the place and nothing ticked. The editor shows the machine's generic supervisor's *Decides* fixed, said. An
    appointed press names its `kind`. The steward is the catalogue's one steward. Ask Daoris may explain the supervisor, never
    overrule it. (from D233)
42. A role held by the terminal acts once the window starts its terminal (`START_ROLE_TERMINAL`, the window's alone); unstarted
    is `not-started`, another holder `not-staffed`, a hold the role no longer reads as `role-changed`; a catalogue that cannot be read ends no hold, which stands until it reads, an act's step and a terminal's start saying `role-changed` meanwhile and withdrawing no grant. Unstaffed is off. A
    supervisor decides for a place exactly where its holder is a started terminal or an agent: the holder is the person's
    per-place setting, and no build flag gates it. Held by rules alone, a place behaves as before: a `refer` point waits for
    the person. An agent-held supervisor's subjects are its own look's (`SuperviseLook`, after `course-release`), never listed
    to a terminal: each attempt (`SupervisePass`) walks the bound supervisor's order through the start plan less the choices its
    attempts spent, writes the attempt, and runs one read-only turn (`SuperviseTurn`, a helper's shape) in a copy of the
    subject's work with its work in progress laid over it, or an empty folder of its own where the subject has no tree, removed
    however it ends, bounded by ten minutes, taking no place under the cap. Its record opens through `POST /api/supervise`,
    marked `decides` with the attempt (the `supervise` work) and `serves` with the subject's session; its prompt
    (`SupervisorInstruction`) hands the same facts as the holder's print, one composer (`SupervisorFacts`) serving both, the acts
    its grants cover and the bounds; an agent takes the question kind's subjects and the points of the course and limit kinds
    (`SupervisorKinds.IsPoint`), and the landing, allow and files kinds' subjects, whose facts each kind's row composes (`ISupervisorKind.FactsAsync` over
    `SupervisorKindFacts`, read through `AgentSupervisorDesk`) with the line's course and commits, so an agent supervisor answers
    every kind it is granted and the editor refuses none of them to an agent holder. Its answer is the one fenced
    block of its last message (`SupervisorBlock`), which names a `choice` exactly where the act chooses a label, as the door's form
    does, and is kept through its
    kind's row by the decision door (`AgentSupervisorDesk`), its grant and hold read again first, the decision keeping the
    agent's holder (role, revision, choice, tier that ran, posture, session, tokens). A limited or transient end is a pass and the
    next look takes the next choice; a failed end, a timeout, a block that does not read or names another subject ends the
    subject's attempts; a block the keeping refuses is dropped (`supervisor.dropped`) and said on the subject's line, ending its attempts, but one
    dropped `role-changed` spends no choice and decides again under the role as it now stands (outcome `role-changed`); a
    leave is final. The prompt states the reasons' bounds. With no choice ready, or every choice of its order spent (limited, transient),
    nothing starts, the person's rules stand as they acted, and the subject waits for the person, said once on its line
    (`supervisor.unavailable`) and taken once a choice is ready. The person's word on a subject ends its running turn and drops
    its result, and a cancel that meets a turn already ended is no error; what was said once is forgotten when its subject stops waiting. (from D233, D229)
43. A hold lives in memory with a generation every act carries, never the key or its hash; a restart starts nothing. A role's
    press through the door ends the holds it no longer staffs and sweeps their decisions. An agent's hold needs no press and lives
    nowhere: its generation is its role's id and revision (`AgentHold`, `agent:<role>@<revision>`), read from the catalogue at
    every admission, so any write of the role from any process moves it and a decision kept under the old one is `role-changed`
    at its next step; it is never in the terminals' holds, so a pipe act naming one is refused as an unknown generation is.
    `driver role` refuses every write from a process an agents' job holds (`OwnAgentJob`, the namespace read as the pipe reads a
    role act's caller), and acts from the person's terminal. (from D208, D209, D233)
44. The holder is a dedicated session in a tab the window starts, outside every repository, on a configuration Daoris
    writes, running only read tools and its roles' verbs. Every other verb refuses `role-terminal`. (from D208, D209)
45. The holder key is drawn at the appointment, kept only as a hash in memory, and stripped from every other start. The
    verb sends an HMAC proof over the hello's nonce, only to the install's own executable. (from D208, D209)
46. A supervisor decides or leaves a decision through one door: `SUPERVISOR_DECIDE { kind, subject, generation, proof, do, choice?,
    reasons }` and `SUPERVISOR_LEAVE { kind, subject, generation, proof, bound, reasons }`, both `Role`, each proved over its own
    route's bytes with the subject whole in them. The subject is the kind's typed reference, which does not change while it waits: a
    question's `{ session, question }`, a referred point's `{ session, point }`, a landing's `{ session, landing }` (`landing` names
    the run, the node and the attempt it waits on, the session the run's work); the door takes the question, course, limit and
    landing kinds, each a row (a landing step is listed only where it offers an act the supervisor's grants cover; `QuestionKind`, over `ReferredPointKind` the `CourseKind` and `LimitKind`, a point's kind its count's,
    `SupervisorRules.KindOf`, and `LandingKind`) that lists its waiting subjects and keeps a decision against the subject as it stands. A
    decision's file keeps its `kind` and `subject`, and its holder is the hold's as the catalogue's record names it. On a question it
    answers a parked session's recorded question with one offered choice and one to five sourced reasons (`--do answer --choice`,
    under `question.answer`), a decide naming a question the session no longer waits on refused `question-changed`; any subject it may
    leave to the owner with a bound (`--leave`, under no grant, but only by the hold that answers its place: another place's holder is refused, as a decide is). The holder's verb is `sessions supervise <session> --kind <kind>
    --subject <id> --generation <g> --do <act> [--choice <label>]`, or `--leave <bound>` in place of `--do`; `SUPERVISOR_WAITING` lists
    every kind's subjects, `{ kind, subject, repository, generation, acts }`, with the kind's acts the grants cover there, and a subject
    whose supervisor covers none, or whose hold is an agent's, is not listed. An `allow` is listed in three shapes (a held request
    `{ session, request, held }`, a widening `{ proposal }`, a steward's act `{ ask }`) and the verb's `<id>` is the session for the
    first and the subject's own id for the others; it sends the subject whole as the listing names it. A `files` subject
    `{ repository, start, what, text }` is named by its repository, the verb's `--subject` its text. The verb prints an allow or a
    held act as an agent's prompt is handed it: Daoris's own words, then the facts its kind's row composed (the listing's `facts`)
    fenced under the print's own nonce, then the line's course where the facts name a session. (from D208, D233)
47. It never answers a go-ahead, a sign-in, an access widening, spending, a deletion, a question the repository says to
    ask the person, a preference, an unrecorded or contradicting one, or one that directs it. (from D208)
48. It is also refused on a question the agent marked the owner's, past the answers-alone bound, and where a person's
    rule holds the line (`rule-holds`), at the keeping and again at the release. The rule is read first and its act
    stands. Where the answers-alone rule says `refer`, its count's point holds the line as `hold` does, behind *Go on with this
    answer* and *Not now*, and the act is kept as `refer`. The supervisor's list (`SUPERVISOR_WAITING`) names the point where a
    started terminal holds the supervisor for the line's repository and no decision answers it, a question decision still moving
    on the line answering first; the holder decides it once through the one door (`--kind course --subject <point> --do go-on`, or
    `--leave <bound>`, kept as `hold` with its bound). A decide naming an earlier point of the line, one the rules recorded and the
    line has passed, is refused `answered`, and one it never reached `not-referred`. A leave is the person's at once. `go-on` is held for the role's window,
    *Overrule* taking it back, then the supervisor's look releases it between turns through the count's own door, the
    admission and the rules read again first (`role-changed`, `rule-holds`, `answered` move it stale): the mark taken away and
    `answers.supervised` kept, which resets the count as the person's press does. No course act stops, ends or moves a
    session. With no started terminal for the place the point waits for the person and says so (`supervisor.unreferred`). The
    decision is kept beside the questions' with no `question`. A restart empties memory: the start's sweep reads every kind's decision
    files, closes a held or released one stale (`restarted`) and an admitted one interrupted, and remembers each subject final to the
    person (overruled, stale, superseded, interrupted, left) as closed by its kind and subject, so a point overruled before a restart,
    or one whose overrule line was never written, is listed for no supervisor again and its decide is refused `overruled`; a course,
    landing or question decision does the same in the run it goes final to the person in (a left one refuses `answered`). An agent
    supervisor's attempt (`SupervisorAttempts`) is written beside the subject's decisions before its turn starts and its outcome
    (decided, left, failed, limited, role-changed) beside it at its end, one per choice of the role's order and one at a time per subject; the
    sweep closes an attempt with no outcome failed and remembers every subject whose attempts ended as closed, so no choice is
    spent on it again.
    A quest's point is its rule's act on the quest's last session while the planner's verdicts hold it there, parked (`failures`)
    or asking (`failures-on-choice`); its release is the look after the plan (`CourseReleaseLook`), which reads the same verdicts and
    makes the quest's own *Try again* (`QuestRetry.Decide`: a park's mark, or a policy's one approval, the same writes the person's press
    makes), never while its session runs, and the plan, the rules and the admission read again first. The count's judge reads the
    same hold: a supervisor's answer is refused `rule-holds` where a refer parks. A quest's *Try again* the supervisor made
    is said on the line it acted on (`supervisor.retried`), so the person sees who released it. Failures on one choice, a limited
    end (`limits`, at 1) and the moves bound are the limit kind's points (`SupervisorRules.LimitCounts`), answers alone and failures
    the course kind's: the holder decides one `--kind limit --do move` (under `limit.move`, the seed's grant) or, on a limited end
    alone, `--do wait` (under `limit.wait`; elsewhere `no-wait`), held for the window with *Overrule*, and a course decide on it is
    `not-referred`. The look after the plan releases a move as the quest's one approval in `role_moves` (`QuestRetry.Decide`, the yes
    *Go on now with…* writes), the limit's own carry and walk then moving the next part as for the person's yes, and a wait as a
    `waited` row on the cut part (`StaffMoves.Wait`), which the start reads as a `wait` rule to the refer's minutes after the cut-off
    (`StaffCourse.SupervisorWaits`), the ask the person's again past them; each is said on its line (`supervisor.moved`,
    `supervisor.waited`). The keeping and the release of a course or limit decision read the person's own holds first
    (`OwnerHolds`: the line's stop, the quest's pause or stop, a steward's own stop of this run passed for its hand-over alone), a
    decide refused and a held one gone stale `owner-held`; the steward's hand-over reads the same unit. The decision reads back, in
    memory and from its files after a restart, as `SESSION_SUPERVISOR`'s `courses`: the point's count and value, the rule and its
    place, the act, the holder, when it was kept and goes on, and where it stands (held, went on, overruled, stale with why, left).
    *Details* says the newest in its *Supervisor's decision* row, and the head's card says it with *Overrule* (the person's
    `SUPERVISOR_OVERRULE`) only while it is held; both name the decision's kind (`SESSION_SUPERVISOR`'s rows carry `kind`). `sessions supervise <id>` prints a point waiting or already decided: its count,
    value, rule and place, the course as it stands, the line's course and its commits, and for a waiting point how to decide it.
    (`SupervisorCourses`, `ReferredPoints`, `OwnerHolds`, `SupervisorCourseView`, `SupervisePointPrint`, `Modules/Bridge/SupervisorKinds/`,
    `Web/work/head/SupervisorCourseCard.tsx`; from D229, D233)
49. A decision is held whole in memory for the supervisor role's window (1 to 60 minutes, 5 by default), then released in
    one conditional step that asks the admission again. Its word is `From: supervisor`, never the person's; an
    unattributable one is quarantined. A landing is the same window with *Overrule*: the supervisor's `accept` of a `review` step,
    or `approve` or `reject` of a `human` approval (`LandingSteps`, the kit saying which kinds offer which acts and which hold a run
    for the person alone), is kept against the step as it waits on that attempt, refused `answered` once it has moved,
    `go-ahead-open` on a run a `go-ahead` holds, and `owner-held` on a line the person stopped or whose quest they paused
    (`LandingJudge`, which reads no plan: a `land` names no plugin, and an approval is judged by its step alone). The look after the courses
    (`LandingReleaseLook`) reads the step, the admission and `OwnerHolds` again and keeps the node's own word on the run's
    record through `RunNodeWords.Word`, carrying its author (`NodeWordBy`: role and decision) and attempt; the decision is live,
    `released`, until the node pass applies the word as the supervisor's (`went-on`) or drops it `stale` where the attempt has
    moved or the decision is not live in this run. A person's word on the node replaces a supervisor's pending one and the
    decision moves `superseded`. A restart empties memory, so the first pass drops a word it left, and the start's sweep records a
    decision whose word the record shows applied as made and closes the rest stale. The head's card says *The supervisor
    accepted this; it lands at 14:10.* with *Overrule* while it is held (`SupervisorLandingCard`, `SESSION_SUPERVISOR`'s `landings`).
    (from D208, D233)
50. The owner's own words supersede a supervisor's decision; *Overrule* takes it back until the move to working admits
    it. (from D208)
51. The steward, scoped to this Daoris, makes the acts its role's `authority` lists of seventeen with no card: drive, undrive,
    hold, resume, stop, finish, try again, a repository's workflow, a quest's pause, unpause, done and delete, a
    session's tree land and clean, a staged update's now and cancel, and a session's hand-over, each one exact form over
    `ROLE_ACT`, logged `role.act` and said on its line; `setup` only where the person lists it. A route act (`StewardRouteAct`:
    workflow, done, delete, land, clean; `StewardHandOverAct`) runs its person's route inside the role's door, a refusal of the
    route refusing the act with its code and its words. The hand-over is `SESSION_HAND_OVER { id, agent[, account] }` over a
    quest's own session (`steward hand-over <id> --agent <a> [--account <x>]`): its session's line says the steward asked
    (`handover.asked-steward`) while the move it keeps is the route's own, and it is refused `owner-held` where its quest is held
    by what only the owner releases (a stop not the steward's own of this run, an outage past its tries, the strikes, a pause, a
    role policy's bound), since a hand-over releases that hold. Outside the grants its route's judge refuses the press before any
    card, and the steward's verb says the judge's words; done and
    delete are `QUEST_DONE` and `QUEST_DELETE`, the service's doors sent as the person's records; land is `LAND_SESSION_TREE`
    and clean the unforced `DISCARD_SESSION_TREE`, each over the session alone and judged by the session's own tree
    alone (`StewardTargets.OwnTreeAsync`): neither ever pushes to a remote, a push being its own `push` step. The pause (`StewardPauseAct`) never writes the
    person's stop: the sessions it stops carry `stopped.paused-quest-steward` (a steward's stop to every reader of the
    person's), its log says `by: steward`, and its unpause, only of a pause the steward made this run and the owner has not
    touched, releases them as the steward's own role release. The update acts (`StewardUpdateAct`) say the install's own
    word (`update.requested`, door `steward`); the pipe has no person's route for an update, so outside the granted acts
    they are refused `not-granted`, never asked. The person grants and takes back each act in the role editor, read at every act, and again at the act's step under its gates
    (`RoleActsLedger.RunAct`): an act taken back while it queued is refused `not-granted`. A proved
    holder's act outside them that asks (`StewardAsk`, `role.asked`) is first an `allow` subject `{ ask }` at its target's
    place, read as `ActAsync` reads it (`StewardPlaces`): one pending entry on the press's reservation (`StewardAllows`,
    `PendingAllow`), settled once by the first of the allow supervisor's `allow.steward` (the act admitted once as the steward's
    own, `owner-set` read first, logged `by: steward` naming the decision; an allowed `land` held for the supervisor's window
    and still the entry through it), its leave, ten minutes from the raise, the person's overrule of a held `land`, each of
    which falls to the card, or the steward's withdrawal, which answers `withdrawn` with no card. A target that does not
    resolve, no supervisor there, or a supervisor whose terminal's holder is the steward's own goes to the card at once. The card
    is the act's row's person's route, in *Notifications*, and confirmed it is the person's own press; a process that does not
    prove the key is refused, nothing asked. Everything else is never a role's act. The held `land` is the steward's line in
    *Notifications* saying when it lands (`STATE`'s `stewardLands`, `STEWARD_LANDS`), with the person's *Overrule*
    (`STEWARD_LAND_OVERRULE { ask, session }`, the window's alone, marking the session the land names as the owner's; refused as
    `done: false` where the window ended, the steward withdrew it or the session is not the land's), which hands it to the card
    (`StewardAllows.Overrule`); an act a supervisor allowed says so on its line (`stewardActs`' `allowed`). Each allow a supervisor
    takes, of a held request, a widening or a steward's act, is kept as an allow decision (`SupervisorAllows`): a file beside the
    session's other decisions (the proposing session's for a widening, the land's session for a land, else the machine's), `went-on`
    at its keeping, and for an allowed `land` `held` until it lands (`went-on`), is overruled, or the steward withdraws it
    (`stale`, `withdrawn`); the start's sweep closes a held one `stale`. A leave of any of the three, or of a `files` ask, is kept
    the same way as a decision `left` at once (`do: leave`, with its bound and reasons; a `files` one, which names no session, in the
    machine's folder), and the subject stays the person's. It reads back as `SESSION_SUPERVISOR`'s `allows`, and the
    line's card says *The supervisor allowed this once* (or refused it, or left it to you) for a held request, with no *Overrule*
    (`SupervisorAllowCard`). (from D201, D209, D233)
52. What the owner does to a repository, a quest or a session makes it the owner's for the run (`owner-set`). Every
    release the steward writes is kept in `roleReleases`, `by: steward`. A working session's own press (`PipeDoor.Worker`)
    makes nothing the owner's: it is logged `session.pressed`, `by: session <id>`. (from D209, D253)

**What a working role may do**

53. A session is served the tools of its work alone, by the call table, which the page shows read-only; a supervisor's turn
    (`supervise`) is served the reads alone. (from D212, D233)
54. Only a build, design or chat session may ask for a co-worker. A helper, a reviewer, an intake, Ask Daoris and a
    supervisor's turn may not. (from D212, D203, D233)

**Ask Daoris**

55. Ask Daoris answers from the records, takes the person to a place (`go`), starts work as an ask the intake turns into
    quests, or proposes a change. A quest's or session's own acts stay in its own conversation: accepting a departure and
    handing a landed branch to a landing plugin are the person's own presses, never proposed; a run's Accept has a terminal
    verb (`daoris workflow accept <run> <node>`), still the person's press. (from D161, D193, D233, D253)
56. The modes differ only in who says yes to a press of Daoris's verb, which both run. Every session's start is handed a delegate
    key of its own, kept only as a hash in memory by session, stripped from every other start and forgotten at its process's end,
    its conversation's end and an access swap; the verb proves a person's press with it over the hello's nonce. In restricted mode,
    the default, it reads and presses, and a proved press waits on a card naming Ask Daoris and runs on the person's yes; the card
    names the place the press named where that lies outside the conversation's focus (`HelpCardPlace`, judged as rule 58's
    `HelpPressPlace`), and the press is kept in rule 58's record as asked, with its card's state (`HelpAskedPress`). The press
    is the one way to ask the person: a route with a view (`PressViews`, one unit per route) judges the press at the ask with the
    route's own rules, answers one it would refuse to the terminal in those words with no card, and gives the card what the press
    means and the command it is, above every value it carries; the views are a repository's move (`repository wire`), a session's
    archive, unarchive and delete, a quest's and an ask's delete (`ask --delete` is a press of its own, `ASK_DELETE`) and the
    pause and resume of an ask's or a quest's work, an account's model and effort (`agent settings`), and how an agent's accounts are
    used, ordered and ended early (`agent account use|order|ready`, one view of `ACCOUNT_USE`), a repository's drive and hold
    switches (`place drive|undrive|hold|resume`), a line, a session language, a standing answer and reading and
    writing across (`place line|language|standing|across`), a repository's or workspace's review, second-opinion, helpers and
    commit rules (`place review|opinion|helpers|commits`, judged against the rule the machine holds now), a retry of a parked or
    held quest (`quest retry`), a git identity's use (`git use`, every form pressed) and the machine's notify, cap, cool-off, quiet and keep rules and a terminal's tier
    (`settings notify|cap|cooloff|quiet|keep|terminal`), an agent's update and pin (`agent update|pin`) and Daoris's browser's settings and
    favorites (`browser use|links|extensions|favorite add|remove`, the person's presses `BROWSER_SETTING` and `BROWSER_FAVORITE`, which
    the terminal sends where Daoris runs and writes alone where it does not), a role's change (`role set|remove|default|policy|rules`,
    one view of `SET_ROLE`, judged by the role door's judges against the catalogue, staffing and rules held now; a supervisor's record
    and a place's rules ride it before and after), and a plugin's add from a folder, install of an offer, update, switch, remove and
    approval or withdrawal of what its server offers Ask Daoris (`plugin add|update|enable|disable|remove`, `plugins approve|withdraw`,
    the views of `PLUGIN_ADD`, `PLUGIN_INSTALL`, `PLUGIN_UPDATE` and `PLUGIN_ACTION`, judged as the Plugins place judges them; what the
    plugin runs as its manifest writes it, or every tool an approval names, rides the card). A press of Ask Daoris's that
    no line waits on is said in its conversation once it settles, as Daoris's word (`SettledSaid`); an update or a pin says its end there,
    a failure before it starts and an end before the settle registers it included, and an ask or a quest a pressed verb's answer names is
    kept with the conversation (`HelpPublished`) and named in its line (`PressedEnds`). A working session's tier is its repository's access setup, read at the press from the place its key was handed for
    (`SessionDelegates.PlaceOf`, `DelegateTiers`): unrestricted runs its proved press at once as its own (rule 52); asking and the
    judged default put it on a card naming the session. Each proposal still proposed is a kind under `Help/Proposals/`, judged
    by its route's own code against the machine now and applied only by the person's *Apply* through that route; a kind goes
    whole, tool to card, with the change that gives its route a view (the setting, agent, browser, role and plugin kinds are gone; the
    ask, sync, clear, workflow and setup kinds remain, with `go`). `sessions archive --ended` stays a terminal's own, an archive being
    this machine's mark and undone by *Unarchive*. (from D89, D170, D253, D261)
57. Restricted mode adds the access the person configures: housekeeping, chosen settings (notify, language, cap, cool-off
    and quiet), checkouts and families of acts, each request
    judged at the call by the door's own judges, in any workspace's repositories; the checkouts it is handed are every
    readable one on the machine, its own workspace's first. A granted family (`acts`, `ActFamilies`: `pause` is a quest's or
    an ask's pause and resume, a repository's hold and a session's stop) runs Ask Daoris's proved press of it with no card as the
    person's own, read at each press, so a change asks no conversation to start again. Its tool calls outside its allow-list
    are held for the person (agents-and-harnesses 23). No settings grant reaches a role. (from D183, D205, D253)
58. Full access runs its harness's unrestricted mode, and a press it proves runs at once with no card as the person's own, said
    once on its conversation's console, logged `help.pressed` and kept in this run's memory (`HelpPressRecord`: route, conversation,
    when, outcome, and the workspace or repository it named, or for a press naming only a quest the repository that quest asks,
    with whether that was beyond its conversation's focus at the press, `HelpPressPlace`; a restricted press asked on a card is
    kept there too, marked asked, its outcome its card's state: waiting, allowed, refused, changed or unanswered; the newest 100, a
    restart empties it) and told to the windows, which list it in *Notifications* beside the steward's line, the asked apart
    under *Asked you*, each row opening its conversation and a pause that ran offering *Resume*. A terminal's press under the
    terminal's tier `act` (desktop-app 15) is kept and listed the same way, marked a terminal's, under its own line and with no
    conversation to open. A proof that does not hold, a nonce used already or a session
    holding no key is refused `delegate-unproved`. Only the person chooses it (Settings, the panel's chip, `help mode`), never
    Ask Daoris. A change of mode or access takes the next turn and is said. (from D183, D253, D271)
59. What each start handed is recorded at the start and shown on the panel, never worked out again. (from D183)
60. A proposal kind, a route's view, a room's door row or a terminal verb exists only where one is wanted; no census asks one
    of every action. The room's doors table says where each change is and whether Ask Daoris proposes it, a proposing row naming a
    door its kind takes; *Done at a terminal* names the acts no proposal takes, by their commands. (from D110, D170, D233, D261)
61. A plugin's servers and skills reach Ask Daoris only where its manifest declares them for it, approved by the person
    in restricted mode. A plugin changes Daoris only through the person's presses, each on its card. (from D170, D183, D261)
62. The room says each role's staffing, policy and last seven days, so *why did this run on Opus?* is answered from
    the records. (from D226)

## Where a change goes

- **A new work or kind of start:** `RoleWork`, `StartPlan.RoleOf`, the seed in `Defaults/roles.json`, and the participant's
  work rule (`Roles/Domain/ParticipantWork.cs`, with its service and page twins). Tools for it go in the service's call table and its fixture.
- **A role's field or judge:** `RoleCatalog` and `RoleRecords`, reached only through `RoleDoor`. No second writer.
- **Staffing:** `RoleStaffing.Set` is the one writer. A reader of an order that is not the start's goes through
  `RoleOrders` and is named in `OrderReadersCensusTests`.
- **A policy field:** `StaffPolicy`, `StaffJudge`, then `StaffCapabilities.Fields` in the row that enforces it.
- **A supervisor count or action:** `SupervisorRules` (windows, allowed actions, bounds), the count in
  `Course/Domain/LineCounts.cs`, and the executor's read through `SupervisorRuleBook`. No course step.
- **The agent supervisor:** `QuestionSupervisorDecisions`, `SupervisorDecision`, `Modules/Bridge/SupervisorDoor.cs`.
- **A steward act:** a row in `Modules/Bridge/StewardActRows.cs`, naming its person's route; one that is a person's route
  writing no one's words is a `StewardRouteAct`, one that stops sessions has its own note code (`StewardPauseAct`), and one with no
  person's route on the pipe is refused outside the grant (`StewardUpdateAct`). Its name in `StewardActNames`, the verb's grammar, the web's `ACTS` and
  `STEWARD_ACTS`, and its words in both catalogues.
- **An Ask Daoris door:** a file of its own in `Help/Proposals/`, registered by one line in `HelpProposalKinds.All`, with
  the service's writer `Help<Kind>ProposalWriter.cs` (`Daoris.Service.Core/Help/`), the page's card, and its row in the room's doors table. Only where one is wanted.
- **The page:** `Web/agents/roles/` and `Web/agents/setup/`.
- **A changed stored shape:** a step in `Transitional/Migration/`, one entry of `HomeMigration.Steps`; the old read is deleted in the same change.

**Tests that pin it.** Driver: `Roles/*Tests` (catalogue, door, verbs, staffing, walk, judge, ledger, rules, decisions,
holder key, steward), `Install/HomeMigrationTests`, `Course/LineCourseTests`, `Course/StaffCourseLookTests`,
`Loop/StaffPolicy*TickTests`, `Help/HelpProposalKindsTests`, `Help/Proposals/*Tests`, and
`Knowledge/HelpRoom*Tests` with the room goldens. Modules: `Bridge/DriverModuleRoleTests`, `DriverModuleRoleTerminalTests`,
`Platform/RoleTerminalTests`, `RoleActTests`, `Holder*Tests`, `SupervisorScopeTests`. Service:
`Help/HelpRoleProposalTests`, `Help/HelpProposalBoxKindsTests`, `Mcp/McpRoleFilterTests`, `Sessions/SupervisorWordTests`.
Web: `agents/roles/*.test.ts(x)` and `agents/setup/*.test.tsx`.

**Gates that bite.** `driver`, `modules`, `censuses`, `service` (a proposal's twin, the call table), `web-unit` and
`web`, `cli` (`npm run verify`: the transitional markers, the size ratchet), and `orient-index`, `test-map`, `code-map`.

## Contradictions

1. D229's 2026-11-04 note (`daoris role rules`, `daoris role supervisor`) vs `Roles/RoleVerbs.cs` (`driver role rules`):
   Ask Daoris's role cards print the note's form as their terminal twin (`Roles/SupervisorRulesFlags.cs`, `Terminal`
   and the role card's command), while the verb's own grammar and help say `driver role`. The verb's grammar reads right.
2. D226 (seven conditions, each with its actions) vs `Roles/StaffPolicy.cs` (`StaffConditions`) and `Roles/SupervisorRules.cs`
   (moves for four counts; signed-out, model-refused and budget are the walk's). Code reads right.
3. D226 (moves and `most` live in the policy) vs D229 and `Roles/StaffPolicy.cs` (the policy holds neither; they are the supervisor's rules).
   D229 reads right.
4. D205 points 2 and 7 (choices in `driver.json`'s `actors`) vs `Roles/RoleStaffing.cs` (`role_staffing` alone). Code reads right.
5. D205 points 8-9 (Settings → Actors, `driver actor`, `ACTORS`/`SET_ACTOR`, `actor_propose`) vs `driver role`,
   `ROLES`/`SET_ROLE` (`Platform/Views/RoleChangeView.cs`). Code reads right.
6. D89 (Ask Daoris's agent named under Settings → *Daoris's own AI* as a third job) vs the `ask-daoris` role's staffing
   (`Defaults/roles.json`, `Settings/ActorKey.cs:20`). Code reads right.
7. D170 (never a shell) vs D183 full access (`Help/HelpMode.cs:17`). D183 reads right; D170 holds in restricted mode.
8. D209 point 1 and its COMPANYVIEW1e note, and D205's COMPANYVIEW1b note (appointed and staffed under Settings → This
    Daoris, then Agents → Roles) vs `Web/agents/modes.ts` (`AGENTS_MODES` is `agents` and `setup`: the Roles mode is retired; its sections sit under
    Agents → Setup). Code reads right.
9. D110 point 4 (a rule is never proposed by an agent) vs Ask Daoris pressing `daoris role rules`, the supervisor's rules
   (`Platform/Views/RoleChangeView.cs`). D110 means permission rules; it should say so.
10. D208 points 4 and 8 (a workspace's supervisor later; held five minutes) vs SUPERVISOR1e as built (workspace
    appointments, `Web/agents/setup/PlaceSupervisorRules.tsx`; a window of 1 to 60, `Roles/RoleHold.cs`, `RoleHold.InWindow`). Code reads right.

## Open

- **CONTEXTDRIVE1j (rest):** the installed proof of a parked quest gone on.
- **CONTEXTDRIVE1l, m:** the installed proof. The hooks' coverage rows owed at CONTEXTDRIVE1g are not written: the every-door
  census that would hold them was retired with the door-only-where-wanted rule (D233 point 1), so no test names a hook.
- **STAFFPOLICY1h/i:** selection by measurement; the installed proof.
- **MODELROLE1l/e2/i:** built; the installed proof of a design quest on Design, and of an exact one on Exact build, is in the installed batch, the partner's real-process
  case goes with COAGENT1. D233 choice 5 removes Ask Daoris's Accept and hand-over proposals, not the hand-over doors a person or
  an agent presses, so the hand-over's model stands; the four fenced roles are edited in the one role editor.
- **SUPERVISOR1a-i (rest):** the author sweep test, the page's mark with *Overrule* on a question's answer (a referred point's
  decision has its card), the answers-alone go-on's process case (a parked quest's go-on has one, `SupervisorReferTickTests`).
- **SUPERVISOR1a-iii:** the installed proof of the activation: a place's supervisor held by a started terminal on the install's
  `app/Daoris.Desktop.exe`, a referred point decided and gone on, and the look in the window.
- **QUESTIONUI1b, SUPERVISOR1d, SUPERVISOR1f:** the pick on the card; the bench; words.
- **STEWARDROLE1d:** the installed look of rule 51's acts.
- **STEWARDROLE1e (answers) to 1h:** the steward's yes or no to an agent's hand-over ask (a pending operation consumed from memory, the
  move written `HandOverBy.Steward`), the go-on, the set-up, and free words. The go-on releases a hold D210 point 3 leaves to the person
  alone, so it waits on D210 carrying or answering the steward design's §4.3a.
- **ROLEPERM1, PERSONDOOR1c-i:** permission answers through appointed actors; the role's boundary at every door.
- **SETUPUX1y:** the places tree lit by default, retiring the Roles sections under Setup.
