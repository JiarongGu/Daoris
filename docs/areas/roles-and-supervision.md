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
| `RoleRecord`, `RoleWork`, `RoleScope` | `Roles/RoleRecords.cs` | A role's record; the nine works; machine, workspace or repository scope |
| The seed | `Defaults/roles.json` | The nine seeded roles, machine-wide; Design's order is `ActorChoices.DesignSeed` |
| `RoleDoor` | `Roles/RoleDoor.cs` | The person's one door: judges a press, then writes role, staffing, policy and rules in one transaction |
| `RoleVerb` | `Roles/RoleVerbs.cs` | `daoris role` and `role set/remove/default/policy/rules/supervisor/suggested` |
| `RoleStaffing` | `Roles/RoleStaffing.cs` | `role_staffing` in the desktop store: import, the one writer |
| `StaffingProjection`, `StaffingStep` | `Roles/StaffingProjection.cs`, `Transitional/Migration/StaffingStep.cs` | The store's rows as a record's fields; the migration's staffing step |
| `ActorChoices`, `ActorKey` | `Roles/ActorChoices.cs`, `Settings/ActorKey.cs` | An order of 1 to 4 choices, its effective entries, its judge, who may be off |
| `StartPlan`, `ReaskMove` | `Roles/StartPlan.cs`, `Roles/OneTurnReask.cs` | The one walk over a role's order, work to role, pass codes; the one re-ask |
| `StaffPolicy` | `Roles/StaffPolicy.cs` | The policy record: rule, entries, presets, digest, judge. Its `Moves` and `Most` are the course's read of a line's rules, set by `SupervisorRuleBook.Course`, never stored |
| `StaffJudge` | `Roles/StaffJudge.cs` | `Arrange` (weights, shares, budgets, works) and `OnCondition` |
| `StaffPolicies`, `StaffLedger`, `StaffCapabilities` | `Roles/StaffLedger.cs`, `Roles/StaffPolicyDoor.cs` | `role_policy` and starts ledger; stale reads; the fields a build enforces |
| `RoleOrders`, `ActorSuggestion`, `StaffWeek` | `Roles/RoleOrders.cs`, `Roles/ActorSuggestion.cs`, `Roles/StaffWeek.cs` | The other readers' order; suggested choices; a role's last seven days |
| `RoleUse`, `RoleUseMoves` | `Roles/RoleUse.cs`, `Roles/RoleUseMoves.cs` | A role's week from the machine log: starts per choice, those that fell and why (`actor.chose`), its moves by condition (`actor.moved`) |
| `ApprovedChoice` | `Course/HandOver/ApprovedChoice.cs` | The choice a hand-over is approved on: an agent's own ask takes the arranged order's step for its agent; a handed quest's file keeps its model and effort, and its start takes them while the role still lists them |
| `SupervisorRules` | `Roles/SupervisorRules.cs` | The rule supervisor's pure half: counts, actions, bounds, places, judge, effective set, act |
| `SupervisorRuleStore`, `SupervisorRuleBook`, `RuledPolicy` | `Roles/SupervisorRule*.cs`, `Roles/RuledPolicy.cs` | `supervisor_rules`; the fresh seed; what each executor reads; a preset's rules |
| `SupervisorRuleActs`, `SupervisorRuleHold`, `RuleWiderPlaces`, `RuleWiderPlaceStore` | `Roles/SupervisorRuleActs.cs`, `Roles/SupervisorRuleHold.cs`, `Roles/RuleWiderPlaces.cs`, `Roles/RuleWiderPlaceStore.cs` | A rule's act said, kept and read back; `rule-holds`; a repository's workspace as its wider place, kept in the store from the look's registry. A Planned look pass (`RuleActsLook`, `PlannedRuleActs`) records a park, a transient hold, a rule's move and the moves bound once per session; the supervisor's print lists them (`LineFacts.Acts`) |
| `QuestionSupervisorDecisions`, `SupervisorWhy`, `SupervisorBounds` | `Roles/QuestionSupervisorDecisions.cs`, `Roles/SupervisorDecision.cs` | The agent supervisor's decisions in memory, its lifecycle, refusals and bounds; the class keeps one-line doors |
| `SupervisorCourses`, `ReferredPoint`, `ReferredPoints`, `SupervisorCourseForm`, `SupervisorCourseView`, `SupervisePointPrint` | `Roles/SupervisorCourses.cs`, `Roles/ReferredPoint.cs`, `WorkOrders/ReferredPoints.cs` (it reads Course's go-on marks and the planner's considerations), `Roles/SupervisorCourseForm.cs`, `Roles/SupervisorCourseView.cs`, `Roles/SupervisePointPrint.cs`, `Modules/Bridge/SupervisorCourseDoor.cs`, `Modules/Bridge/Routes/SupervisorCourseRoutes.cs` | A `refer` point's course decision kept, held, released or overruled; the open point (answers alone's mark, or a quest's parked or asking verdict) and its release door; the act's form; its route and its listing; the decision read back from memory and its files (`SupervisorReads.CoursesOf`); the holder's print of a point. A quest's go-on is released by `CourseReleaseLook` (`Loop/Look/`) |
| `SupervisorBook`, `SupervisorDecisionFiles`, `SupervisorKeeping`, `SupervisorReleasing`, `SupervisorSweeps`, `SupervisorTickets`, `SupervisorReads` | `Roles/Supervisor*.cs` | The decisions' shared memory (live decisions, session locks, closed questions) and its one move in the book; the files, keeping, releasing, sweeps, tickets and reads each reach it and keep no state |
| `SessionsSuperviseCommand`, `SupervisorQuarantine`, `HolderKey` | `Roles/SessionsSuperviseCommand.cs`, `Roles/SupervisorQuarantine.cs`, `Roles/SupervisorProof.cs` | The holder's verb; unattributed words taken back; the key, its proof and the role terminal's verbs |
| `StandingConfirmation` | `Modules/Platform/StandingConfirmation.cs` | The roles' terminals this run: holds, generations, holders; the act judge over the catalogue. The doors; the memory is `StandingState`'s under its one lock, which `RoleTerminals` (starts, catalogue changes, a holder's end), `RoleJudge` (the role method, the decisions' admission), `RoleActsLedger` (acts, operations, the owner's marks) and `StewardRecord` (the steward's acts this run) reach |
| Steward | `Roles/StewardCommand.cs`, `Roles/StewardAsked.cs`, `Modules/Bridge/StewardActRows.cs`, `Modules/Bridge/StewardRouteAct.cs`, `Modules/Bridge/StewardPauseAct.cs`, `Modules/Bridge/StewardUpdateAct.cs`, `Modules/Bridge/StewardTargets.cs`, `Modules/Bridge/Routes/QuestEndRoutes.cs`, `Modules/Platform/RoleActs.cs`, `Modules/Platform/StewardAsk.cs` | `steward <act>` over `ROLE_ACT`, and its words when an act asks the owner; the seventeen acts, the route acts among them; pending operations; an act outside the granted ones sent on as its person's press |
| `StartAdmission`, `CallTable` | `Participants/Domain/StartAdmission.cs`, `Service/Daoris.Service.Mcp/CallTable.cs` | Who may ask for a co-worker; which tools each work is served |
| `HelpProposals`, `HelpProposalKinds` | `Help/HelpProposals.cs`, `Help/HelpProposalKinds.cs`, `Help/Proposals/` | Ask Daoris's 16 proposal kinds, each judged by its route's code and applied by the person |
| `HelpModes`, `HelpSwap`, `HelpHousekeeping`, `HelpSettings`, `HelpReceipt` | `Help/HelpMode.cs` and its neighbours, `Help/HelpWired.cs` | Access mode, its change at a turn, configured access, what a start handed |
| The page | `Web/agents/roles/`, `Web/agents/setup/` | One role editor and policy form, appointed roles, structure and call table; a place's Roles, its supervisor |

## Its rules

**The roles**

1. A role is a record of the role catalogue in the desktop store, in one of two shapes. A working role: a name (authored
   text), the works it takes, a scope, and a staffing. An appointed role: a name, its kind (supervisor or steward), the acts it
   holds of that kind's vocabulary, a scope, its holder, and the supervisor's window. A role carries no doors of its own. (from D212, D222, D233)
2. The works are build, design, chat, ask-daoris, intake, help, partner, review and brainstorm. A record's work is read
   from its marks by one rule, never stored. Design falls to the role that takes build when none takes design. (from D212)
3. Nine roles are seeded: build, design, intake, opinion, helper, partner, brainstorm, ask-daoris and chat, machine-wide.
   An id reads as its seed until the store holds a record of it, so an upgrade writes nothing. Design takes design work
   (a quest its publisher or the intake marked so) on Claude Code · `opus` · high, the reference's judgement row, the quest
   naming no model; a home from before it reads that seed for it, and a workflow step may name it. Removed, design work
   falls to Build's. A seeded role is edited, restored and removed by the one door; no suggestion adds it. (from D205, D212)
4. The most specific scope's role takes a work: repository, then workspace, then machine. At most one role takes a work
   in one scope (`two-roles-one-work`). (from D212)
5. Only Intake and Ask Daoris may be off. A work that may not be off keeps a role on the machine (`may-not-be-off`). A
   removed role's work holds its starts, said, until the person gives it a role. (from D205, D212)
6. A record that does not read holds the starts that read it, said with its error. It is never read as the seed. (from D212)
7. `supervisor` (held by the person's rules) and `steward` (held by no one) are seeded machine-wide; a seeded id keeps its
   shape and kind (`mixed-shape`). An agent holder is refused `appointed-not-configurable`, as are choices on an appointed role.
   The steward works on the machine alone and is never held by rules; the supervisor's holder is rules or the terminal, its
   window 1 to 60 minutes; two of one kind never share a scope; the machine's supervisor may not be removed. (from D233)
8. A start keeps its role and the role's revision on its binding, and a census holds the two to the work. Changing or
   removing a role touches no running session; a continuation reads the role that takes its work now. (from D212)
9. One door changes roles: `RoleDoor`, reached by `SET_ROLE`, `SET_ROLES`, `daoris role` and `role_propose`
   applied by the person's press. Each answers the judge's closed codes alike. A press carries the revision it read. (from D212, D228)
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
    order, two starts in one look reserved apart; a weight is 0 to 10. (from D226)
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
    `refer` (ask the agent supervisor) is offered on three counts: answers alone, and the two only a driven quest has, failed
    sessions since *Try again* (it parks as `hold` does) and failures in a row on one choice (it asks as `ask` does). Each is a
    count whose hold sits between sessions and whose own door the supervisor's course act can go through: answers alone's mark,
    and for the quest's two its *Try again*. On any other count the judge refuses it `do`, and no flag offers it: tries, limits and
    moves are a conversation's counts too, and a conversation's hold is released by its person's word, which no course act may
    speak; a quiet end has no hold to release, and its session is never ended. (from D229)
33. A place is the machine, `workspace:<id>` or `repository:<id>`; a line has no place of its own, so the judge refuses
    `line:<key>` (`place`) and the chain has no line step. A failure is one thing for the quest's count and its run on
    one choice: a failed end or a cut-off, never a limit, a refused account, a choice not offered, a transient end, an
    update's close or the quiet end (`SessionReads.IsFailure`). For each count the most specific place
    holding a rule decides, with no layering. Rules naming the role beat those naming none; `off` ends inheritance. A
    repository's workspace is its wider place at the judge in every process as at the lines: the look keeps the registry's
    in the store (`repository_workspaces`, step 21) and the judge reads it there; `ROLES` answers each place's wider places,
    and the fold draws what a place's rules replace and inherit along that chain, for each role's lines. (from D229)
34. A write is refused where any place's effective set has no tries rule, a move with no moves rule, or failures on one
    choice acting at or past failures, and where two rules on one count at the place would apply to one line (`ladder`:
    both for every role, or both naming one role); a rule naming a role beside the every-role rule stands. The terminal's
    `--set` takes one step. A place's set is written whole at the revision read. No write asks for a conversion first. (from D229, D232)
35. One seed serves a fresh home's first start and the release's migration: answers alone at 2, failures at 3 (the
    migration: an older home's `strikes`), tries at 5 and moves 3 a day before the next asks, for every role; the
    migration takes `strikes` out and passes a stored policy's moves over. Where the machine's place holds nothing, or the
    store does not read, the seeded rules decide. (from D229, D232)
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
    `supervisor` with `rules`, or `role_propose`. A rules press carrying a role's field is `mixed-shape`. The terminal names a
    rule's roles after an `@` on each `--set`, `--off` or `--clear` (`SupervisorRulesFlags`), so Ask Daoris's card twin is
    one command that clears the place and writes the rules the judge accepted, and the card carries the place and its rules
    before and after as fields (`HelpRulesView`) that the page words from its catalogue in both languages (`RulesChange`). The page writes
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

41. They are staffed through the role door like every role (`SET_ROLE`, `driver role set --acts --holder --window`,
    `role_propose`). The act judge reads the catalogue at each act, answer and admission: the most specific scope's role of the
    kind that covers the place, held by the terminal. Ask Daoris may explain the supervisor, never overrule it. (from D233)
42. A role held by the terminal acts once the window starts its terminal (`START_ROLE_TERMINAL`, the window's alone); unstarted
    is `not-started`, another holder `not-staffed`, a hold the role no longer reads as `role-changed`. Unstaffed is off. The
    agent supervisor is on for a place exactly where its supervisor's holder is `terminal` and that terminal is started: the
    holder is the person's per-place setting, and no build flag gates it. Held by rules alone, a place behaves as before:
    a `refer` point waits for the person. (from D233, D229)
43. A hold lives in memory with a generation every act carries, never the key or its hash; a restart starts nothing. A role's
    press through the door ends the holds it no longer staffs and sweeps their decisions. (from D208, D209, D233)
44. The holder is a dedicated session in a tab the window starts, outside every repository, on a configuration Daoris
    writes, running only read tools and its roles' verbs. Every other verb refuses `role-terminal`. (from D208, D209)
45. The holder key is drawn at the appointment, kept only as a hash in memory, and stripped from every other start. The
    verb sends an HMAC proof over the hello's nonce, only to the install's own executable. (from D208, D209)
46. The supervisor answers a parked session's recorded question with one offered choice and one to five sourced reasons
    (`sessions supervise --choice`), or leaves it to the owner with a bound (`--leave`). (from D208)
47. It never answers a go-ahead, a sign-in, an access widening, spending, a deletion, a question the repository says to
    ask the person, a preference, an unrecorded or contradicting one, or one that directs it. (from D208)
48. It is also refused on a question the agent marked the owner's, past the answers-alone bound, and where a person's
    rule holds the line (`rule-holds`), at the keeping and again at the release. The rule is read first and its act
    stands. Where the answers-alone rule says `refer`, its count's point holds the line as `hold` does, behind *Go on with this
    answer* and *Not now*, and the act is kept as `refer`. The supervisor's list (`SUPERVISOR_WAITING`) names the point where a
    started terminal holds the supervisor for the line's repository and no decision answers it, a question decision still moving
    on the line answering first; the holder decides it once (`sessions supervise <id> --point --course go-on|hold`,
    `SUPERVISOR_COURSE`, the role's third act). `hold` leaves it to the person at once. `go-on` is held for the role's window,
    *Overrule* taking it back, then the supervisor's look releases it between turns through the count's own door, the
    admission and the rules read again first (`role-changed`, `rule-holds`, `answered` move it stale): the mark taken away and
    `answers.supervised` kept, which resets the count as the person's press does. No course act stops, ends or moves a
    session. With no started terminal for the place the point waits for the person and says so (`supervisor.unreferred`). The
    decision is kept beside the questions' with no `question`; a restart empties memory and leaves the point the person's.
    A quest's point is its rule's act on the quest's last session while the planner's verdicts hold it there, parked (`failures`)
    or asking (`failures-on-choice`); its release is the look after the plan (`CourseReleaseLook`), which reads the same verdicts and
    makes the quest's own *Try again* (`QuestRetry.Decide`: a park's mark, or a policy's one approval, the same writes the person's press
    makes), never while its session runs, and the plan, the rules and the admission read again first. The count's judge reads the
    same hold: a supervisor's answer is refused `rule-holds` where a refer parks. A quest's *Try again* the supervisor made
    is said on the line it acted on (`supervisor.retried`), so the person sees who released it. The decision reads back, in
    memory and from its files after a restart, as `SESSION_SUPERVISOR`'s `courses`: the point's count and value, the rule and its
    place, the act, the holder, when it was kept and goes on, and where it stands (held, went on, overruled, stale with why, left).
    *Details* says the newest in its *Supervisor's decision* row, and the head's card says it with *Overrule* (the person's
    `SUPERVISOR_OVERRULE`) only while it is held. `sessions supervise <id>` prints a point waiting or already decided: its count,
    value, rule and place, the course as it stands, the line's course and its commits, and for a waiting point how to decide it.
    (`SupervisorCourses`, `ReferredPoints`, `SupervisorCourseView`, `SupervisePointPrint`, `Modules/Bridge/SupervisorCourseDoor.cs`,
    `Web/work/head/SupervisorCourseCard.tsx`; from D229)
49. A decision is held whole in memory for the supervisor role's window (1 to 60 minutes, 5 by default), then released in
    one conditional step that asks the admission again. Its word is `From: supervisor`, never the person's; an
    unattributable one is quarantined. (from D208, D233)
50. The owner's own words supersede a supervisor's decision; *Overrule* takes it back until the move to working admits
    it. (from D208)
51. The steward, scoped to this Daoris, makes the acts its role's `authority` lists of seventeen with no card: drive, undrive,
    trees, hold, resume, stop, finish, try again, a repository's workflow, a quest's pause, unpause, done and delete, a
    session's tree land and clean, and a staged update's now and cancel, each one exact form over `ROLE_ACT`, logged
    `role.act` and said on its line; `setup` only where the person lists it. A route act (`StewardRouteAct`: workflow, done,
    delete, land, clean) runs its person's route inside the role's door, a refusal of the route refusing the act; done and
    delete are `QUEST_DONE` and `QUEST_DELETE`, the service's doors sent as the person's records; land is `LAND_SESSION_TREE`
    and clean the unforced `DISCARD_SESSION_TREE`, each over the session alone, and land is refused `landing-pushes` where
    the landing's plan names a plugin that pushes: neither ever pushes to a remote. The pause (`StewardPauseAct`) never writes the
    person's stop: the sessions it stops carry `stopped.paused-quest-steward` (a steward's stop to every reader of the
    person's), its log says `by: steward`, and its unpause, only of a pause the steward made this run and the owner has not
    touched, releases them as the steward's own role release. The update acts (`StewardUpdateAct`) say the install's own
    word (`update.requested`, door `steward`); the pipe has no person's route for an update, so outside the granted acts
    they are refused `not-granted`, never asked. The person grants and takes back each act in the role editor, read at every act. A proved
    holder's act outside them asks the person as its row's person's route (`StewardAsk`, `role.asked`), a card in
    *Notifications*, and confirmed it is the person's own press; a process that does not prove the key is refused, nothing
    asked. Everything else is never a role's act. (from D201, D209, D233)
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
57. Restricted mode adds the access the person configures: housekeeping, chosen settings (notify, language, cap, cool-off
    and quiet) and checkouts, each request
    judged at the call by the door's own judges. No settings grant reaches a role. (from D183, D205)
58. Full access runs its harness's unrestricted mode. Only the person chooses it (Settings, the panel's chip, `help
    mode`), never Ask Daoris. A change of mode or access takes the next turn and is said. (from D183)
59. What each start handed is recorded at the start and shown on the panel, never worked out again. (from D183)
60. A proposal kind, a room's door row or a terminal verb exists only where one is wanted; no census asks one of every
    action. The room's doors table says where each change is and whether Ask Daoris proposes it, a proposing row naming a
    door its kind takes; *Done at a terminal* names the acts no proposal takes, by their commands. (from D110, D170, D233)
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
- **A steward act:** a row in `Modules/Bridge/StewardActRows.cs`, naming its person's route; one that is a person's route
  writing no one's words is a `StewardRouteAct`, one that stops sessions has its own note code (`StewardPauseAct`), and one with no
  person's route on the pipe is refused outside the grant (`StewardUpdateAct`). Its name in `StewardActNames`, the verb's grammar, the web's `ACTS` and
  `STEWARD_ACTS`, and its words in both catalogues.
- **An Ask Daoris door:** a file of its own in `Help/Proposals/`, registered by one line in `HelpProposalKinds.All`, with
  the service's writer `HelpProposalBox.<Kind>.cs`, the page's card, and its row in the room's doors table. Only where one is wanted.
- **The page:** `Web/agents/roles/` and `Web/agents/setup/`.
- **An old way kept:** an entry in `Transitional/Register.cs`.

**Tests that pin it.** Driver: `Roles/*Tests` (catalogue, door, verbs, staffing, walk, judge, ledger, rules, decisions,
holder key, steward), `Install/HomeMigrationTests`, `Course/LineCourseTests`, `Course/StaffCourseLookTests`,
`Loop/StaffPolicy*TickTests`, `Help/HelpProposalKindsTests`, `Help/Proposals/*Tests`, and
`Knowledge/HelpRoom*Tests` with the room goldens. Modules: `Bridge/DriverModuleRoleTests`, `DriverModuleAppointmentTests`,
`Platform/RoleTerminalTests`, `RoleActTests`, `Holder*Tests`, `SupervisorScopeTests`. Service:
`Help/HelpRoleProposalTests`, `Help/HelpProposalBoxKindsTests`, `Mcp/McpRoleFilterTests`, `Sessions/SupervisorWordTests`.
Web: `agents/roles/*.test.ts(x)` and `agents/setup/*.test.tsx`.

**Gates that bite.** `driver`, `modules`, `censuses`, `service` (a proposal's twin, the call table), `web-unit` and
`web`, `cli` (`npm run verify`: the transitional markers, the size ratchet), and `orient-index`, `test-map`, `code-map`.

## Contradictions

1. D212 point 16 (supervisor and steward are seeded catalogue roles; an appointed shape) vs `Roles/RoleCatalog.cs:35,49,281`:
   the catalogue holds working roles alone and reserves both ids. Code reads right.
2. D229 point 5 (a rule is a holder of the supervisor role) vs `Modules/Platform/StandingConfirmation.cs:172-176` (holders
   are `terminal` and `actor`; rules are a store set through the role door). Same meaning; the code's split reads right.
3. D229's 2026-11-04 note (`daoris role rules`, `daoris role supervisor`) vs `Roles/RoleVerbs.cs` (`driver role rules`):
   Ask Daoris's role cards print the note's form as their terminal twin (`Roles/SupervisorRulesFlags.cs`, `Terminal`
   and the role card's command), while the verb's own grammar and help say `driver role`. The verb's grammar reads right.
4. D226 (seven conditions, each with its actions) vs `Roles/StaffPolicyDoor.cs:12-34` (moves for four; signed-out,
   model-refused and budget are the walk's). Code reads right.
5. D226 (moves and `most` live in the policy) vs D229 and `Roles/StaffPolicy.cs` (the policy holds neither; they are the supervisor's rules).
   D229 reads right.
6. D205 points 2 and 7 (choices in `driver.json`'s `actors`) vs `Roles/RoleStaffing.cs` (`role_staffing` alone). Code reads right.
7. D205 points 8-9 (Settings → Actors, `driver actor`, `ACTORS`/`SET_ACTOR`, `actor_propose`) vs `driver role`,
   `ROLES`/`SET_ROLE` and `role_propose` (`Help/Proposals/HelpRoleProposals.cs:20`). Code reads right.
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

- **CONTEXTDRIVE1j (rest):** the installed proof of a parked quest gone on.
- **CONTEXTDRIVE1l, m:** the installed proof. The hooks' coverage rows owed at CONTEXTDRIVE1g are not written: the every-door
  census that would hold them was retired with the door-only-where-wanted rule (D233 point 1), so no test names a hook.
- **STAFFPOLICY1h/i:** selection by measurement; the installed proof.
- **MODELROLE1l/e2:** the installed proof of a design quest on Design; the partner's real-process case. D233 choice 5
  removes Ask Daoris's Accept and hand-over proposals, not the hand-over doors a person or an agent presses, so the
  hand-over's model stands; the four fenced roles are edited in the one role editor.
- **ONEWAY1af:** delete `actor-key-before` and `staffing-copy` once the staffing part's census ends.
- **SUPERVISOR1a-i (rest):** the author sweep test, the page's mark with *Overrule* on a question's answer (a referred point's
  decision has its card), the go-on's process cases.
- **SUPERVISOR1a-iii:** the installed proof of the activation: a place's supervisor held by a started terminal on the install's
  `app/Daoris.Desktop.exe`, a referred point decided and gone on, and the look in the window.
- **QUESTIONUI1b, SUPERVISOR1d, SUPERVISOR1c, SUPERVISOR1f:** the pick on the card; the bench; the `actor` holder; words.
- **STEWARDROLE1d (rest):** the steward's further acts; the installed look.
- **STEWARDROLE1e to 1h:** the steward's hand-over, go-on, set-up, and free words.
- **ROLEPERM1, PERSONDOOR1c-i:** permission answers through appointed actors; the role's boundary at every door.
- **SETUPUX1y:** the places tree lit by default, retiring the Roles sections under Setup.
