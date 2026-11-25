# Roles and supervision — how it works now

## What it does

A role is who does a kind of work for the person. A working role takes one or more works (build, chat, review…) at a place, and
its staffing says which agents, models and efforts run it, in order; it may carry a policy of weights, shares and budgets. The
person sets roles through one door: the window, the terminal, or an Ask Daoris press they confirm. The rule supervisor watches a
line's counts by the person's rules, read with no model: *when this count reaches n here, do this*. The supervisor and the steward
are appointed roles staffed through the same door; a supervisor held by a started terminal or an agent decides what its grants
cover, and the steward makes its granted acts. Ask Daoris is the front desk: it reads the machine, takes the person to a place,
starts an ask, or presses a change the person confirms; in full access it acts itself, and a second check reads what it did.

## Its parts

Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Modules/`
(`src/Daoris.Desktop/Daoris.Desktop.Modules/`), `Service/` (`src/Daoris.Service/`) or `Web/` (`src/Daoris.Web/src/`), each type
in a file of its name; `docs/index/README.md` says where a seam, route, verb or fixture is. Which folder owns which job:

| Where | Its job |
|---|---|
| `Roles/`, `Defaults/roles.json` | The reporting line (`ReportingLine`, `OwnerClass`), the catalogue and its seed (`RoleCatalog`), the one door and its verbs (`RoleDoor`, `RoleVerbs`), staffing (`RoleStaffing`, `ActorChoices`, `StartPlan`), policy (`StaffPolicy`, `StaffJudge`, `StaffLedger`), the rule supervisor (`SupervisorRules`, `SupervisorRuleBook`), the appointed shape (`RoleHold`, `AgentHold`), the decisions' memory, files and reads (`Supervisor*`), the steward's verb, the place-risk record read by a role's scope and place (`PlaceRiskScopes`), the line a landing's wait climbs (`LandingWaitLineOperations`, installed into Workplace's port) |
| `Workflows/Cast/` | Each place's cast of every role (`PlaceCastBook`, `PlaceCastFiles`, `PlaceCastView`), a step's tenures begun at its attempt (`AttemptTenures`) and passed by Take, Pass to and Give back (`StepHolding`), and the one door that settles a step by its holder (`SettleDoor`, `SettleBy`, D312); a step its agent holds handed to the run's working session (`StepHandOffs`) and settled through its tool `step_settle` (`StepSettles`, `StepSettleAsks`, taken by the run's progression at the settle's fact, D318) |
| `Settings/` | The place-risk record as `driver.json` keeps it (`PlaceRiskBook`, `PlaceMarks`) and its verb (`PlaceRiskVerb`), and the supervisors' kinds (`SupervisorKinds`), which a place's line mark names |
| `Loop/Supervise/`, `Loop/Look/` | An agent supervisor's look, attempt and read-only turn; the releases between turns; the second check's reading queue; the evidence routine's day (its rule `EvidenceDays` in `Roles/Domain/`, METHODS1): the day's failures handed to it (runs ended by stop, waits with no holder, main reds, review HIGH findings, refused acts), each classed by its kind and its record's code, one row proposed per class for the steward to accept, a declined class not proposed again, due once a day after 21:00 UTC or on the steward's word; its routine (`EvidenceRoutine`) reads the day through `IEvidenceRecords`, keeps each day's rows and their states and the declined classes under the home's `work/evidence/`, and an accepted row reaches the backlog through the records door (`IEvidenceBacklog`) alone, never a file write of its own |
| `Modules/Bridge/` | The decision door (`SupervisorDecisionDoor`, one row per kind under `SupervisorKinds/`), the steward's act rows |
| `Modules/Platform/` | The roles' terminals this run (`StandingConfirmation`), the delegate proof, Ask Daoris's press views (`Views/`) and press record |
| `Help/`, `Help/SecondCheck/`, `Knowledge/` | Ask Daoris's modes, configured access, go links and room; the second check's selection, packets and readings |
| `Participants/Domain/StartAdmission.cs` | Who may ask for a co-worker |
| `Web/agents/roles/`, `Web/agents/setup/`, `Web/work/head/` | The role editor and policy form, a place's Roles and *Who decides here*, the supervisor's cards |

## Its rules

**The roles**

1. A role is a record of the role catalogue in the desktop store, in one of two shapes. A working role: a name (authored text),
   the works it takes, a scope and a staffing. An appointed role: a name, its kind (supervisor or steward), the acts it holds of
   that kind's vocabulary, a scope, its holder and the supervisor's window. A role carries no doors of its own. (D212, D222, D233)
2. The works are build, design, exact, chat, ask-daoris, intake, help, partner, review, brainstorm, plan and orient, read from a
   record's marks by one rule, never stored; plan and orient read from no marks: a lead is an intake, and a scout a helper whose ask names a part (`HelperAsk.Scout`, walked as orient work), by theirs,
   the census admitting a role taking plan on an intake's record and one taking orient on a helper's. Design and exact fall to the
   role taking build when none takes them, plan to the role taking design, orient to the role taking exact (`RoleCatalog.Fallback`).
   (D212, D285)
3. Twelve roles are seeded machine-wide: build, design, exact, intake, opinion, helper, partner, brainstorm, ask-daoris, chat, lead
   and scout. An id reads as its seed until the store holds a record of it, so an upgrade writes nothing, and a home with no
   staffing row for design, exact, lead or scout reads its seed (`StaffingProjection`). Design takes design work on Claude
   Code · `opus` · high; Exact build takes exact work (`weight: exact`) on Claude Code · `sonnet` at the account's default effort,
   a longer window the person's to choose; the lead takes plan work on Design's tier and the scout orient work on Exact's. Each may
   be named by a workflow step and falls to Build's (the lead's to Design's, the scout's to Exact's) when removed, and a start says
   whose order it ran on; a quest marked neither keeps Build's. A seeded role is edited, restored and removed by the one door; no
   suggestion adds it. (D205, D212, D285)
4. The most specific scope's role takes a work: repository, then workspace, then machine. At most one role takes a work in one
   scope (`two-roles-one-work`). (D212)
5. Only Intake and Ask Daoris may be off. A work that may not be off keeps a role on the machine (`may-not-be-off`). A removed
   role's work holds its starts, said, until the person gives it a role. (D205, D212)
6. A record that does not read holds the starts that read it, said with its error. It is never read as the seed. (D212)
7. `supervisor` (held by the person's rules) and `steward` (held by no one) are seeded machine-wide, keeping their shape and kind
   (`mixed-shape`); the steward works on the machine alone and takes no rules, agent or choices. A supervisor's holder is rules,
   the terminal or an agent (staffed as a working role, its row deleted when the holder moves), its window 1 to 60 minutes. Its
   `decides` names the kinds it answers (`question`, `course`, `limit`, `allow`, `files`, `landing`), none making it its scope's
   generic supervisor; a scope holds one generic and one per kind, and the machine's generic one is never removed or given kinds.
   Its `authority` grants `<kind>.<act>` acts of its kinds, each off on a new record; the seed grants `question.answer`,
   `course.go-on` and `limit.move`. The `allow` kind decides a held permission request, a widening and a working session's asked
   press (never Ask Daoris's) before the person's card, a leave or ten minutes falling to the card. The `files` kind decides an
   act a person's `ask` in the rule file held, writing the act's own text into the person's own layer of each repository it
   reaches, never the machine's; an ask naming the person is no supervisor's, nor a share where the policy classes `publish.outside` the owner's (rule 43). No supervisor record is written before the home's
   `supervisor-kinds` step (`not-converted`). (D233)
8. A start keeps its role and the role's revision on its binding, and a census holds the two to the work. Changing or removing a
   role touches no running session; a continuation reads the role that takes its work now. (D212)
9. One door changes roles: `RoleDoor`, reached by `SET_ROLE`, `SET_ROLES` and `daoris role`, Ask Daoris's press shown on the
   Confirm card (`RoleChangeView`), each answering the judge's closed codes alike. A press carries the revision it read, compared
   in the write's own transaction, so two presses at one revision cannot both write. (D212, D228)
10. A batch is judged whole before anything is written. A role, its staffing, its policy and a place's rules land in one store
    transaction or not at all. (D226, D229)

**Staffing**

11. A staffing is an order of one to four choices, each `{ agent, model?, effort? }` in the tool's own words. An absent model or
    effort is *account default*: nothing is passed. One judge serves every door (`ActorChoices.Judge`). (D205)
12. The staffing lives in `role_staffing` alone, and every write is the store's. A fresh home is seeded with each role's seed.
    (D205, D212, D232)
13. A home's staffing is imported once into the store (`RoleStaffing`): `driver.json`'s `actors` as written, an unreadable entry
    kept, or a fresh home's seeds where the file holds none. A file holding only the single-agent keys (`adapter`, `intakeAdapter`,
    `helperAdapter`) is not seeded and gives no order, until NOCOMPAT2 takes the keys out. (D232, D323)
14. A role of the five with no stored order has no agent: nothing reads the old single-agent keys. (D232)
15. A change of `driver.json`'s staffing by hand is read nowhere. (D232)
16. A start walks its role's order choice by choice, each choice's accounts before the next. A choice that cannot run is passed
    with a code, a provider not opened to the start's place `not-opened` before its accounts are asked. An account cooling for one
    model family passes that family's choices and serves the others. Only the choice taken counts under the cap. (D197, D205, D212)
17. With no choice ready, a driven start holds with one sentence naming the soonest way out; a conversation's press is refused in
    that sentence. (D205)
18. The rules say who may; the choices say with which model. A fence (reviewers, helpers, partners, brainstorm agents) walks the
    role's entries for each listed agent, else `rule-default`. A pick and a handed agent walk as given. (D205)
19. A running session never moves to another choice. A model refused at the open asks the walk again once; a refusal after a
    prompt was sent is never a fallback. A resume applies the record's choice again. (D205)
20. The tier that ran is said. The record keeps `actor`, `choice`, `asked`, `ran` and `chosen`; the opening note, the head and the
    machine log (`actor.chose`, `actor.passed`, `actor.moved`) say it. (D143, D205, D226)
21. Suggested choices come from one dated table, held to `docs/model-roles.md` by a census, and apply only by the person's press:
    the card, or `role suggested --use`, one batch. (D174, D205)

**A role's policy**

22. A policy is a row of `role_policy`, bound by a digest to the choices it was set for. Daoris's own writes of the choices re-key
    it; any other writer leaves it *stale*, and the role walks its order and says so. No row means `order`. (D226)
23. Two rules exist: `order`, the default, and `weight`, a deterministic pick over the role's last 20 new starts, ties by order,
    two starts in one look reserved apart, a reservation given back when its walk refuses or throws or its start ends before the
    record opens; a weight is 0 to 10. (D226)
24. A choice may take only some of the role's works, hold a share of the window (1 to 99%), and keep a daily budget of starts or
    reported tokens in the machine's local day. A budget acts at a start only. (D226) A choice whose agent's every account a limit or
    spend cap holds (the account record's cool-offs, no second store) is passed `capped` until the earliest reset while another
    choice remains, its weight left as written; the next choice's reason says `choice 1 skipped: its account is capped until …`.
25. Presets fill the form and never save: *First that can run*, *Spread evenly* and *Spread by weight*; the two spreads write
    their moves (by weight, at most 3) as rules naming the role, beside the policy. (D226, D229)
26. One pure judge, `StaffJudge.Arrange`, orders the role's own entries inside the one walk. The account walk inside a choice is
    unchanged; no account is named or assigned. (D226)
27. A build offers only what it enforces: the rule, weight, share, both budgets and works. A stored `moves` or `most` is passed
    over, and the next write holds neither. (D226, D229)
28. A signed-out sign-in, a refused model and a reached budget are passes of the walk, never fields. (D226)
29. A move to another choice is a new part handed the brief at a part's end, never mid-turn. An uncertain send is never
    replayed. It is said as *move to another choice*, never *switch*. (D226)
30. A policy holds no moves and no `most`, and `role policy` takes no `--on` or `--most`; lines read their moves and bound from the
    effective rules alone. (D226, D229, D232)

**The rule supervisor**

31. A rule is *when this count reaches n here, do this*. The counts are answers alone in a row, failed sessions since *Try
    again*, failures in a row on one choice, transient tries, a quiet end, a limited end, and moves in 24 hours. (D229)
32. The actions are hold, ask, wait (5 to 600 minutes), next, weighted, refer and off, each count allowing its own set. `refer`
    (ask the agent supervisor) holds its line between sessions behind a door a supervisor's act can go through, on five counts:
    answers alone, failed sessions since *Try again*, failures in a row on one choice, a limited end and the moves bound. Limits
    and moves are a conversation's counts too, whose hold only its person releases, so a refer on them names only roles taking
    driven work (build, design), else `do`; a refer on limits takes 5 to 600 minutes as `wait` does. On tries and the quiet end it
    is refused `do`. (D229, D233)
33. A place is the machine, `workspace:<id>` or `repository:<id>`; a line has none of its own (`place`). A failure is a failed end
    or a cut-off, never a limit, a refused account, a choice not offered, a transient end, an update's close or the quiet end. For
    each count the most specific place holding a rule decides, with no layering; rules naming the role beat those naming none, and
    `off` ends inheritance. A repository's wider place is its workspace in every process, from the registry's map the look keeps
    in the store (`repository_workspaces`), and `ROLES` answers each place's wider places. (D229)
34. A write is refused where any place's effective set has no tries rule, a move with no moves rule, or failures on one choice
    acting at or past failures, and where two rules on one count at a place would apply to one line (`ladder`); a rule naming a
    role beside the every-role rule stands. A place's set is written whole at the revision read. (D229, D232)
35. One seed serves every home whose machine's rules were never written, at a start, whether or not its `driver.json` came first
    (`HomeSeed`, with *Second check*'s first choices beside it): answers alone at 2, failures at 3, tries at 5 and moves 3 a day before
    the next asks, for every role. Where the machine holds nothing, or the store
    does not read, the seeded rules decide, and a write is judged against them too. (D229, D232)
36. Every executor reads the effective rules, one rule per count; a count no rule watches acts on nothing. A line's role and place
    are resolved once (`RuledLine`): the role its start was bound to while the catalogue holds it, else the role taking its work
    there; the place its record's repository and workspace. Every reader takes it. (D229, D232)
37. A rule decides at the executor that acts on its count: answers alone at the admission, failures at the planner's start, tries
    at a run's start and past its episode, conditions where the staffing course reads a move. (D212, D229)
38. A rule acts between turns only: never on an unknown count, never ending a running or quiet session, never answering a
    question; each hold is released by its own door. Its act is said on the line (`supervisor.rule`: the count, its value, the
    rule's number, place and act), kept as a final decision file of holder kind `rule` and logged. One hold is one act, and a new
    answer while a line waits joins its mark. A line whose rules did not read says so. (D229)
39. The failed sessions that park a quest are the failures rule's alone: no door sets `strikes` and no configuration reads it. The
    park carries its rule's number, which *Try again*, Ask Daoris's `retry` and the terminal say. The cap stays a machine setting
    outside the rules. (D229, D232)
40. Rules are read with `driver role supervisor` and written with `driver role rules` or `SET_ROLE`/`SET_ROLES` naming
    `supervisor` with `rules`; a press carrying a role's field is `mixed-shape`. The terminal names a rule's roles after an `@`, so
    the Confirm card's command writes what the judge accepted, the card carrying the rules before and after. The page writes them
    in a fold under each place's Roles, whole at the revision read, each rule saying what it replaces; a role's editor says the
    rules naming it and writes none. A hold says its rule in one sentence on the head, the foot and *What needs you*, and *Change
    the rule…* opens the place whose rule it is. `SESSION_SUPERVISOR` answers the line's `counts` with their rules and the `acts`
    the rules made. (D229)

**The supervisor and the steward**

41. They are staffed through the role door like every role (`driver role set --decides --acts --holder --window`, Ask Daoris's
    press), whose judge refuses an unknown kind or act, an act of a kind not decided, an agent holder with no choices, two of a kind
    in one scope, and a kind of the machine's alone (`ask-daoris`) placed below it (`machine-only`); the Confirm card says the
    change before and after. One resolver names who decides a kind at a place: the repository's, the workspace's, then the
    machine's supervisor of the kind, else their generic ones in that order; an unreadable record or workspace map makes it the
    person's. The admission answers that supervisor's hold where its holder is the started terminal or an agent and the act is
    granted (else `not-staffed`, `not-started`, `not-granted`), and every later step asks it again, so a grant taken away stops the
    next one. `ROLES` answers it as `deciders`, worded on a place's Roles as *Who decides here*. Ask Daoris may explain the
    supervisor, never overrule it. The resolver is read as the reporting line (`ReportingLine`, D304 point 3), each level named by
    the supervisor holding it (`LineLevels`, D309 point 4): the repository's or the workspace's supervisor, whichever it finds below
    the machine, then the machine's supervisor, and the owner role the top, held by the person. Help waits on the
    first level that holds it; no level is the person's by default (D305): a level held by the person's rules or by nobody is the
    person's only where the place's risk record marks it (`line`, rule 49a), else vacant, the older reading kept while a home's record
    is unwritten; a level nobody is appointed to, or whose holder is not granted the act, is vacant and passes it up naming why; an owner-class kind (doctrine rule 1's seven: `direction`, `money`,
    `sign-in`, `release`, `history`, `data`, `publish`) passes every level below the owner role (`owner-class`); other help the
    machine's supervisor leaves ends as gave up with what was tried. Every kind's leave climbs it: each is recorded with the level that
    made it (`LineLeaves`, `line.leaves` in the subject's session's decisions folder), and `ReportingLine.Climb` with that map says where
    the subject waits; one the repository's or the workspace's supervisor leaves is listed, kept and released to the machine's supervisor
    where an agent or a started terminal holds it, the admission answering that hold for the subject (`passed-up` once none is above), while
    a leave naming an owner-class bound, the machine's own, or one with no holder above is final at once as before. The vacancy kind
    climbs by its own journal; the allow and files kinds' leaves stay final until their listings read the line. The steward is the catalogue's one steward. The seed's *Second check* (`second-check`, 复核) is
    the machine's supervisor of `ask-daoris`, granted `ask-daoris.report` and held by the person's rules, so it reads nothing until
    the person turns it on; the home's `second-check` step gives it its first choices once (Codex, then Claude Code · opus · high,
    40 starts a day each) unless the person's own record, staffing or supervisor stands. (D233, D272)
42. A role held by the terminal acts once the window starts its terminal (`START_ROLE_TERMINAL`, the window's alone); unstaffed is
    off, and a catalogue that cannot be read ends no hold. A supervisor decides for a place exactly where its holder is a started
    terminal or an agent; held by rules alone, a `refer` point waits for the person. An agent-held supervisor's subjects are its own
    look's: each attempt walks its order less the choices spent and runs one read-only turn, ten minutes at most and under no cap,
    in a copy of the subject's work tree or an empty folder of its own; a subject read from a packet alone has every tool denied
    where its harness takes Daoris's rules, and a harness that takes none is passed (`not-enforced`). It is handed the facts the
    holder's print hands, answers one fenced block kept through the decision door with grant and hold read again, and its decision
    keeps the model its harness reported. A limited or transient end passes to the next choice; a failure, a timeout or a block
    that does not read ends its attempts; with no choice ready the subject waits for the person, said once. The person's word ends
    its turn. The attempt's mechanism is `docs/2026-11-08-supervisor-kinds-design.md`'s. (D229, D233)
43. A hold lives in memory with a generation every act carries, never the key or its hash; a restart starts nothing. A role's
    press through the door ends the holds it no longer staffs and sweeps their decisions. An agent's hold lives nowhere: its
    generation is its role's id and revision, read at every admission, so any write of the role makes a decision kept under the old
    one `role-changed`. Any caller writes the roles: `driver role` from a process an agents' job holds acts as from the person's
    terminal, and a holder's act from inside such a job is judged by its proof alone; only a caller the system cannot place is
    refused (`untold`). A write granting acts to a role held by anything but the person's rules (an agent, a terminal, which an
    agent's session holds too, or no holder yet) is the owner's alone (`PipeOwnerOnly`): an agent's press
    of it goes on the owner's card in every mode, and one from an agents' job proving no key is refused (`owner-only`). Every act
    of a supervisor kind and of the steward declares in one census what it spends (money, a destination, a permission) and its pin,
    the subject it lapses with (`ActSpends`). A spend is an act kind of the risk policy (`ActSpends.KindOf`: money `spend.money`, a
    destination `release.start`, or `publish.outside` for a files ask's `allow-share`, a held permission's `allow.once` and
    `allow.standing` `reach.widen`), judged at the act's place: an act is admitted only where every grant on its chain lists the act,
    and, where the policy gates the spend on a role, the grant covering it if one is named, the gate taken only by the role its row
    names (any supervisor deciding the kind where the row names none; another role is refused as the owner's); a spend the policy passes needs no more,
    one it classes `owner` stays the owner role's, and a refusal names the grant or the spend missing (`missing`). The common
    admission asks it for every kind (`SupervisorGrants.Missing`, the judge's verdict read through `StandingConfirmation.Verdict`), so a
    landing's `approve` and a files ask's `allow-share` are a granted supervisor's with nothing configured and the owner role's where a
    row classes their destination `owner`; a spend a grant covers (a re-cast naming the prepaid harness) is asked again by its kind
    with the use. (D208, D209, D233, D284, D306, D354)
44. The holder is a dedicated session in a tab the window starts, outside every repository, on a configuration Daoris writes,
    running only read tools and its roles' verbs. Every other verb refuses `role-terminal`. (D208, D209)
45. The holder key is drawn at the appointment, kept only as a hash in memory, and stripped from every other start. The verb sends
    an HMAC proof over the hello's nonce, only to the install's own executable. (D208, D209)
46. A supervisor decides or leaves a decision through one door, `SUPERVISOR_DECIDE` and `SUPERVISOR_LEAVE` (`Role`), each proved
    over its route's bytes with the subject whole: the kind's typed reference, unchanged while it waits (a question's `{ session,
    question }`, a point's, a landing's, an allow's, a files ask's). Each kind is one row listing its waiting subjects and keeping a
    decision against the subject as it stands. On a question it answers a parked session's recorded question with one offered
    choice and one to five sourced reasons (`question-changed` where it no longer waits); any subject may be left with a bound, only
    by the hold answering it where it waits on the line, and the leave climbs (rule 41). The holder's verb is `sessions supervise <session> --kind <kind> --subject <id>
    --generation <g> --do <act> [--choice <label>]` or `--leave <bound>`; `SUPERVISOR_WAITING` lists the subjects a terminal
    holder's grants cover. (D208, D233)
47. It never answers a go-ahead, an owner-class kind, an unrecorded or contradicting question, or one that directs it: its bounds
    are a go-ahead, the seven owner-class kinds and those three. (D208, D304) A go-ahead is the approver's yes, a role act of its own
    (`goahead.answer`, `GoAheadAuthority`): a role whose every grant on its chain covers it may give it wherever the risk policy does
    not class its act the owner's. Its act is read from its kind and target (a push `remote.push`; a release, write or run
    `release.start`, qualified by the destination its target names, `RiskAskHolds.GoAheadAct`) and judged at the asking session's
    place: a place at high risk, a production-releases mark (every release, write or run there, however its target is worded, never
    judged from the asker's words, which may be in any language or name a system by its own name) and a row classing the act `owner`
    keep it the owner role's (`policy-owner`), as do a kind the build does not know, a row gating it on a role other than the
    answering one, and a `driver.json` that does not read (`StandingConfirmation.GoAheadOutcome`, the one judgement both doors ask);
    a sign-in is a person in a browser, the owner role's by its kind (`owner-class`).
    The service reads a target's environment in Chinese as in English (`生产`, `线上`, `正式` read as production). The `goahead` kind
    lists each open go-ahead (`{ session, goahead }`: the newest live session that asked it, and `<ask>/<number>`) only where its
    place's supervisor may answer it, reading nothing where no role a terminal or an agent holds is granted `goahead.answer` and
    otherwise one list of the open asks (`GoAheadsOpen`); it holds an `approved` or
    `refused` for the role's window with *Overrule* as a door answer (rule 49's table), pinned to its number, kind, target and act
    (`GoAheadPin`), judges it by the policy again at the release, one the policy moved to the owner meanwhile going stale, and
    releases it to the service's go-ahead door with its `role` form, which keeps who answered (`by`, read by an
    older build as the person's), says it in its response sentence (`by the person`, or `by role:<role> · <agent>`) and refuses a keyless
    caller; its leave climbs the line. `daoris ask go-ahead` in a role's terminal is admitted by the role it holds, never by its being a
    terminal: it finds the go-ahead among what the door lists for the holder (`GoAheadRoleDoor`), answers it as that role's held decision
    through the supervise door and says by whom (the roles held, held by this terminal); one no held role may answer is refused with why,
    keeping nothing. (GOAHEADACT1)
48. It is also refused on a question the agent marked owner-class (`owner-class`), past the answers-alone bound, and where a person's rule holds
    the line (`rule-holds`), at the keeping and again at the release. A `refer` on answers alone holds the line as `hold` does until
    the holder decides `go-on` once; on failures the quest's point is released as the quest's own *Try again*; on failures on one
    choice, a limited end and the moves bound the limit kind decides `move`, or on a limited end `wait`. Each is held for the role's
    window with *Overrule*, then released between turns through the count's own door with the admission, the rules and the
    person's own holds (`owner-held`) read again first, never while a session runs, and said on its line; a go-on resets the count
    as the person's press does. A decide on a point the line passed is `answered`, on one never reached `not-referred`; a leave
    climbs the line (rule 41), the person's at once only where it ends there. No course act stops, ends or moves a session. With no started holder the point waits for the person
    (`supervisor.unreferred`). A restart reads back the held decisions rule 49 keeps in its table; the sweep closes every other held
    and released decision stale (a course's go-on among them) and remembers each subject final to the person as closed
    (`overruled`). Decisions read back as `SESSION_SUPERVISOR`'s `courses`, with *Overrule* while held. (D229, D233)
49. A decision is held whole in memory for the supervisor role's window (1 to 60 minutes, 5 by default), then released in one
    conditional step that asks the admission again. An answer, a landing and a door answer held under an agent's hold are also a row
    of the desktop store's `held_decisions` (`HeldDecisions`, its file's text and its moment), dropped at its first move off held; the
    start's sweep reads each row back whose file still says it and still ends `held` (a go-ahead's answer among the door answers), its window as it stood, so one whose window
    closed during the restart goes on at the next look and one overruled stays overruled. A terminal's hold ends with its run, so its
    held decisions go stale at the restart; an older build has no table and closes them as before, and a row whose file it closed is
    dropped. (D309) Its word is `From: supervisor`, never the person's; an unattributable one is
    quarantined. A landing is the same window with *Overrule*: the supervisor's `accept` of a `review`, or `reject` of a
    `task` approval (its `approve` spends a destination, the owner role's where the policy classes `release.start` the owner's, rule 43), kept against the step as it waits on that attempt and naming the review record's revision, is refused
    `answered` once it moved, `go-ahead-open` under a `go-ahead` and `owner-held` on a line the person stopped or paused. Its
    release reads all again, a moved record going stale, and keeps the node's word on the run's record with its author, which the
    pass applies or drops; a person's word supersedes it, and a `return`'s per-file choice offers a supervisor no act. The head's
    card says an Accept only when it goes on, by the kind of work its record holds. A step's subject names the newest session of its
    run's work in its repository (`LandingSessions`): a chain's quests for a chain's run, and for an ask's delivery run, which has no
    quest, the quests its ask gave that repository, so the ask's one review is answered as a chain's. Who answers a review is the
    place's appointment, never the step's: a place appoints a landing supervisor held by an agent and granted `landing.accept`, or
    none, and its reviews wait for the person. The `memory` kind's `keep` (a session's staged changes, `keep` or `drop`) and `decide`
    (an end, `approve` or `decline`) and the `departure` kind's `accept` (a done held by its departure alone; one held for its
    evidence, review or a reopened source, or departed beside another requirement's evidence still awaited, which the one yes would
    lift with it, is the person's, and the service refuses a role's yes to it) are held and overruled the same way, each listed and prompted with its
    facts (the staged rows and their texts; the end's target, reason and proposer; the departure beside what the quest required),
    a subject whose facts do not compose is said and skipped, never silently. Each answer is pinned to what it judged (the staged
    rows' ids, the end's id, the quest's state and answers digested) as a landing is to its revision; its release reads the pin
    again, a changed one going stale (`subject-changed`), writes `released`, then posts to the service's own door with the role
    named (its `role` form, the driver's), never as the person's, and a post a restart cut off after `released` is settled by what
    the service shows. A keep or a drop names the staged rows its pin holds and the service moves exactly those, each still staged
    on the session's own page or none, so a row staged since, or by a session it continues, waits to be judged on its own; the
    person's moves them all. A departure's accepted move names the role (`by`), so the owner's mark never reads it as theirs, and the
    session's head shows a held one with *Overrule*; an act applies only where a role holding it is set for the scope and the place's
    risk record (rule 49a) lets a role hold it. (D208, D233, D296, D297, D305)
49a. Each place's risk (low, medium or high) and the owner's marks are one record, `placeRisk` in `driver.json` (`PlaceRiskBook`), keyed
    `machine`, `workspace/<name>`, `repository/<name>`; a place it does not name is low and unmarked, the narrowest risk set stands, and a
    destination's mark (`release.production`: the place's production releases stay the owner role's) covers every place under it. The
    owner sets it once with one press and its twins: a place's Settings row (*Risk and your marks*: a workspace's on its page, a
    repository's beside its sessions' access, its own row over the one it takes from its workspace or the machine, said as where it
    comes from, each press `SET_PLACE_RISK` naming that repository), `daoris settings risk`, and Ask
    Daoris's press, which goes on the owner's card in every mode, while a process in an agents' job proving no key is refused
    (`owner-only`, `PipeOwnerOnly`). The roles' admission reads it at each act: at high risk no appointed role acts (`high-risk`) and the
    decision waits on the owner role. The cast reads it alike: at high risk every role there is the person's (`risk`), the one
    place-wide holder left until `placeRisk` retires (RISKGATE1l); elsewhere a role no cast fills is its agent's, never the person's by
    default (`PlaceCastBook.At`, D354 point 4), and a run's step is the owner's row only where the person holds its role. A line's mark (`line`, `line.<kind>`) is the owner holding that place's level of the reporting line,
    and a risk the build does not know reads high. A `driver.json` with no record (an older build
    rewrote it) reads the line as before the record, until no home on a supported update path lacks it. (D305, D309)
    A run step is settled only through one door (`SettleDoor`): by the holder its attempt's tenure names now, in its epoch, by an
    observation, or by the owner role's stop and decision answer in any epoch; a supervisor's held word only where the person holds the
    step. A press speaks as its channel, never as its payload says: the person's window, card and terminal as the person, a working
    session's own press, an appointed role's act and Ask Daoris's as agents. The person's press on a step another holder holds takes it
    and settles it in one act, the take journaled with the press as its reason and made only once the word is admitted; an agent's word
    never takes, and an agent hands on only a step it holds. Every write of a tenure names the epoch it read, compared under the tenures'
    lock; tenures that do not read take only the person's word and no hold act. The owner role is handed to nobody but the person. A marked destination's approver is the owner role's from its attempt's start: its tenure reads `marked`, offers no Take, Hand or
    Give back, and refuses every other holder's word and every hand-over (`marked`). (D312)
    A second opinion nobody is free to give (every declared reviewer cooling, signed out or refused; a reviewer whose accounts the person switched off reads as refused, its sentence saying so) is a `vacancy`, journalled by
    the look beside the gate (`OpinionCasts`, `<chain>.casts.jsonl`, each act naming its level) and listed to the hold that answers
    it now: the repository's or the workspace's supervisor at the repository, the machine's supervisor once that level left it or
    where it holds none of the vacancy's acts, nobody once the machine's supervisor left it (gave up). Its acts, kept at once with
    their reading and no window: `recast` naming a reviewer the rule declares that the look's walk found installed (else
    `not-offered-choice`), the DeepSeek harness only where `vacancy.prepaid` is granted too (else `not-granted`); `relax` (the work's
    own makers read it after the rule's, said as not independent), `prepaid` (the DeepSeek harness's prepaid key, granted), `wait`; a
    leave is its pass up. The look's next walk reads the newest act, and a walk whose reviewer holds the step journals `filled`, which
    ends the vacancy and its acts, so the walk after it reads the rule's order again; the person's own ask is theirs. (D304)
49b. One judge (`RiskGate.Judge`, `Workflows/Risk/`) reads one risk policy and decides nothing yet: it runs in shadow. The policy is `risk` in
    the home's `driver.json` (`RiskPolicy`), rows keyed by place (`machine`, `workspace/<name>`, `repository/<name>`), each a list of
    `{ kind[/qualifier], class none|role|owner, by?, mode? full|restricted }` over a closed census of seventeen act kinds (`RiskActs`;
    a door naming another is refused). The census, its classes and the verdict's shape (`RiskVerdict`) are the settings' vocabulary
    (`Settings/`), so the review's second opinion speaks them and reaches the judge through a port Workflows installs (`IOpinionRisk`). For an act the narrowest place with a row decides, a qualified row over its kind's and a row keyed
    by the place's access mode over one keyed by none; then `placeRisk` read through (a place whose level stands at `high` reads every
    kind at `owner`, a `release.production` mark `release.start/production` at `owner`); then the seed: `none`, but `reach.widen` (`role`
    by the place's approver under restricted access), `command.change` (`owner` under restricted access; it reads its place by its reach, whether the permission prompts resolve `unrestricted`, which full access gives and that pause alone does too, `RiskPlace.Reaching`; every other act reads the place by full access alone), `history.rewrite`, `data.delete`
    and `policy.change` (`owner`); `policy.change` is fixed whatever a row says. An absent record reads as the seed; one that does not read
    reads every act at `owner` with its fault said; a class the build does not know reads `owner`; a row naming no kind of the census is
    named and not read. Beside each hold today's gates open and release (a run step's unchecked done, Accept, approval, go-ahead and
    recovery waits, read at the runtime's pass; a declaration owed acceptance, read at the look; and, read at the look at most once a minute (`RiskAskHolds`), an open go-ahead with a live asker, a push `remote.push` and a release, write or run `release.start` qualified by the destination its target names, placed in its repository's own workspace as its door places it (its today is the policy's own verdict since go-aheads read it, so its lines agree by construction and are kept for the policy's yes still to come), a parked question its agent marked as the owner's by its mark's kind, and a permission request held under asking prompts `reach.widen`, each at its session's place; a sign-in, a person in a browser, is no act of the policy) one `risk.judged` line is appended to
    the home's journal (`data/workflows/risk/judged.jsonl`, history) at its open and at its release, the release judged again under the
    policy then, with what today's gate did and whether the two agree. `daoris settings risk` prints the act table at the place it
    names and a place's Settings row (*Acts and who answers*, beside *Risk and your marks*) shows the same rows, one model read twice (`RiskActTable`, `RISK_TABLE`): per kind its class, the rule it comes from, who answers by the cast, the last seven days' passes and asks counted from the judge's journal at that place, `policy.change` locked, and for a picked change the waits it opens or releases before it is saved; `daoris settings risk why <run> [<step>]` the run's journaled verdicts, each naming the rule that decided and why. The owner's own press writes a row at once on the revision it read, refused `stale` where the policy has moved (`PolicyChanges.Set`, `POLICY_SET`: the window's act table, or `daoris settings risk set` from their own terminal, also with no Daoris running), journaled in the change journal naming its door (`window`, `cli`, `card`); an agent's `set` is a proposal instead, and a working session's or keyless press sets nothing. The assistant's door (`PolicyChanges`, `PolicyRoutes`): an Ask Daoris `settings risk set <place> <act> none|role|owner|remove` keeps a proposal (an id, a digest of its edits and the revision it read, its before and after and the waits it opens or releases, `PolicyShift`; its edits are rows, a place's cast of a role and its access mode, `set <place> cast <role> <holder>|default` and `<place> access full|clear|<pause>=<value>`) and changes nothing; `settings risk apply <proposal> <digest>` writes it only where a turn of the owner's own in the proposing conversation, kept as the person's (`OwnerTurns`, read from the conversation's events: never an agent's claim, a relayed quote, a helper's or an opinion's words, a channel message or Daoris's own), after the proposal, is in the form `apply <id> <digest>` (the owner's latest turn naming the id stands, so a later refusal withdraws it) and the policy has not moved since, or where the owner presses it on their own card in the window, which shows the proposal (`PolicyApplyView`). A press that is not the owner's own (Ask Daoris's, a session's, a terminal's) never carries a waiting proposal's id or digest into a conversation (`PolicyWordsGuard` at `SessionWords.SayAsync`). The channel is read from the pipe's proof, never the payload: a working session's press is refused `agent`, one that proves no session `keyless`, a turn in another conversation `other-conversation`. `policy.change` is refused as an edit. Each applied change is one `policy.changed` line in `data/workflows/risk/changes.jsonl` naming the turn (or the card). (D354)
50. The owner's own words supersede a supervisor's decision; *Overrule* takes it back until the move to working admits it. (D208)
51. The steward, scoped to this Daoris, makes the acts its role's `authority` lists of sixteen with no card: drive, undrive, hold,
    resume, stop, finish, try again, a repository's workflow, a quest's pause, unpause, done and delete, a session's tree clean, a
    staged update's now and cancel, and a session's hand-over, each one exact form over `ROLE_ACT`, logged `role.act` and said on
    its line; `setup` only where the person lists it. A session's work lands only through its run's review. A route act runs its
    person's route inside the role's door, refused as the route refuses. The hand-over is refused `owner-held` where only the owner
    releases its quest's hold; the pause never writes the person's stop, and the steward unpauses only its own pause of this run;
    the update acts, with no person's route, are refused outside the grants. The person grants and takes back each act in the role
    editor, read at the act and again at its step. A proved holder's act outside the grants is first an `allow` subject at its
    target's place, settled once by the allow supervisor's `allow.steward`, its leave or ten minutes (both falling to the card) or
    the steward's withdrawal; with no supervisor there it goes to the card at once. A process that does not prove the key is
    refused. Each allow a supervisor takes, or leaves, is kept as a decision and read back as `SESSION_SUPERVISOR`'s `allows`, the
    card saying *The supervisor allowed this once*, with no *Overrule*. (D201, D209, D233)
52. What the owner does to a repository, a quest or a session makes it the owner's for the run (`owner-set`). Every release the
    steward writes is kept in `roleReleases`, `by: steward`. A working session's own press makes nothing the owner's: it is logged
    `session.pressed`, `by: session <id>`. (D209, D253)

**What a working role may do**

53. A session is served the tools of its work alone, by the call table, which the page shows read-only; a supervisor's turn
    (`supervise`) is served the reads alone. (D212, D233)
54. Only a build, design or chat session may ask for a co-worker. A helper, a reviewer, an intake, Ask Daoris and a supervisor's
    turn may not. (D203, D212, D233)

**Ask Daoris**

55. Ask Daoris answers from the records, takes the person to a place with a go link, starts work as an ask the intake turns into
    quests (`daoris ask`, pressed), or presses a change, a workflow's version included. A go link is
    `[words](daoris:go?view=…&domain=…&part=…&item=…&workspace=…&repository=…&sentence=…)`, its fields the places the room lists,
    opened on the person's press; one naming no place this window has, or outside Ask Daoris's panel, is its words alone. A quest
    it publishes (`daoris:help`) is filed in the workspace of the repository it names. A quest's or session's own acts stay in its
    own conversation: accepting a departure, handing a landed branch to a plugin and a run's Accept (`daoris workflow accept`,
    refused `moved` on another record) are the person's presses. Before it makes an ask, Ask Daoris proposes one to three
    requirements from the person's own words, each their words and the check an agent's done must show, for their yes, and makes it
    with the ones they said yes to, as they edited them (`--require`), so each quest the ask becomes passes its holds step on them;
    where the person asks for work to be delivered without their press, it puts their through words on it too (`--through`, `ask
    through`). Each is verified verbatim against their messages in its conversation, and the ask's card shows each requirement and
    the through words, so the person's yes there is to them; the service's answer for an ask carries its requirements (none for an
    older record) and its page lists each beside the through words. In restricted mode it proposes a run's step on the
    person's card and never presses it; in full access it presses every step of a run (Accept, changes, pass over, stop, task,
    approval, fix) as the person, said once and listed in Notifications. The room says one rule, do it or send it on: a simple act
    on this machine is its own where its mode lets it press, work that changes a repository's content goes to that repository as a
    quest or a session, and a refusal names its rule. (D161, D193, D233, D253, D261, D273, D277, D321)
56. The modes differ only in who says yes to a press of Daoris's verb, which both run. Every session's start is handed a delegate
    key of its own, kept only as a hash by session, stripped from every other start and forgotten at its end and an access swap;
    the verb proves a press with it over the hello's nonce. In restricted mode, the default, a proved press waits on a card naming
    Ask Daoris (and the place it names, where outside the focus) and runs on the person's yes. The press is the one way to ask the
    person: a route with a view (`PressViews`) judges the press at the ask by the route's own rules, refuses in those words with no
    card, and gives the card what the press means and its command. The views cover the routes Ask Daoris presses: a repository's,
    a session's, a quest's and an ask's acts, an account's and an agent's settings, a place's switches and rules, clean-ups, git
    identities, the machine's and the browser's settings, a role's change, a plugin's, a history clear, a sync, a place's set-up
    (a room asked of a repository, `place <repository> --room <path> --purpose "…"`, has a card of its own) and a new ask, and a
    department opened (`repository create <name> --purpose "…"`, whose view judges as its plan `REGISTRY_CREATE_PLAN` does and gives
    the card every value, the steps in order and the first lines of each quest it publishes). A settled press no line waits on is said in its
    conversation; a press that opened a place answers a `PressReceipt`, which the press record keeps in either mode and the windows draw
    once as a line with go links, *Opened `<name>` in `<workspace>`* (a room's the quest's line alone), under the turn and in *Notifications*.
    A working session's tier is its repository's access setup, read at the press: unrestricted runs its proved press as its own
    (rule 52), the rest go on a card naming the session. A press the risk policy classes is read by its verdict instead: the Agents
    place's `pin` and `update` (`HARNESS_ACTION`) are `agent.version` at the machine, judged at the press (`DelegateRisk.Of`) and
    journaled once it is admitted under its request id: a pass runs as the session's own, logged `session.pressed` with its act and
    rule; a gate is a hold opened under that id, its terminal told the verdict whole (class, holder, rule and revision), and goes on the
    owner's card (`risk-owner`) or, for a role, to the session's allow supervisor first, whose allow reads the policy again and falls to
    the owner's card where it is now the owner's (`risk-role` on a card it falls to). Its release is journaled as it ends, the policy read
    again then, naming who said yes. Ask Daoris has no proposal tool; the room's *What you may do* names the verbs.
    `sessions archive --ended` stays the terminal's.
    Which place a request calls for is the room's one table (`HelpRoomGrowth`), asked of the registry's `domain` and the knowledge
    search and said in the turn: the department that owns the work, by an ask; a room in it, where the part needs instructions or checks
    of its own; a new repository, where the work is its own deliverable; a folder department, where it wants no history; nothing, for a
    name, a plan or a throwaway. The earlier row wins and the person's own words go ahead of it. Any agent (Ask Daoris, a lead, a driven
    session) opens a place through the two presses, its tier deciding as for every press; a refusal is the route's own words, answered at
    once and never carded; the intake never creates one. The person's growth switch holds both presses per workspace and repository
    (`mode`, the default, as above; `asked`, a card even in full access, saying so; `off`, a refusal in the route's words, kept in the
    press record as refused, in either mode), read by the plan the route, the card and the pipe all judge by (`GrowthRules`,
    `PipeGrowth`, `DelegateGrowth`, a working session's own press included). It is the person's alone, in Settings or `daoris settings
    growth`: the pipe refuses `SET_GROWTH` to every press that proves a session's key, in every mode, and to a process in an agents' job
    (`person-alone`), and the room lists it as no verb. (D89, D170, D253, D261, D276)
57. Restricted mode adds the access the person configures: the checkouts it reads, each read judged at the call; and the families
    of acts it presses with no card, the one shape a grant has: `pause`, `clean-up`, `discard` and `settings`. A granted family
    runs a proved press as the person's own, read at each press; a look asks no grant. An older record's `housekeeping` and
    `settings` lists grant nothing. Its tool calls outside its allow-list are held for the
    person (agents-and-harnesses 23). No family reaches a role, and none reaches a department's creation or a room's ask
    (`REGISTRY_CREATE`, `PLACE_APPLY`): restricted mode carries a card for them whatever is granted. (D183, D205, D253, D261, D276)
58. Full access runs its harness's unrestricted mode, keeps `checkout_read` and names each readable checkout and where it is, and a proved press runs at once with no card as the person's own, said once
    on its conversation's console, logged `help.pressed` and kept in this run's memory with what it named (the newest 100, a
    restricted press kept there as asked with its card's state), listed in *Notifications*, a run's step by its act, the step and
    the run (`HelpPressStep`). A terminal's press under the tier `act`
    is kept in the tier's own record (desktop-app 15). A proof that does not hold, a used nonce or a session with no key is refused
    `delegate-unproved`. Only the person chooses the mode (Settings, the panel's chip, `help mode`); a change takes the next turn
    and is said. In either mode, after each turn and off its path, the second check selects what ran with no yes of the person's
    (an acting harness card, a connector press that acts, a card no line accounts for, a delegation); one the person allowed is
    never selected, and a reading that cannot see all of it is partial, never clean. Selected turns queue in ranges, one report per
    range, read off the driver's loop one at a time on the machine through the one walk (rule 42) on the machine's supervisor of
    `ask-daoris`, packet-only. The reader assesses acts already done, answering at most eight findings, each a `flag` or a `note`,
    or a leave; the report keeps its reader and independence (`another-maker`, `not-independent`, `not-counted`, `unknown`). A
    range nothing can read closes *unread* with why, never said in the conversation; a terminal holder of the kind reads nothing.
    The window reads the reports alone (`HELP_READINGS`); each unseen flag is a `second-check` row of *What needs you* with *Seen*,
    told to Ask Daoris once ahead of the person's next words (conversations rule 42). The selection, the packet's contents and
    bounds and the report's shape are `docs/2026-11-11-ask-daoris-second-check-design.md`'s. (D183, D253, D271, D272)
59. What each start handed is recorded at the start and shown on the panel, never worked out again. (D183)
60. A route's view, a room's door row or a terminal verb exists only where one is wanted; no census asks one of every action. The
    room's doors table says where each change is and, where Ask Daoris never asks it on the card, why; *Done at a terminal* names
    the acts it never presses. (D110, D170, D233, D261)
61. A plugin's servers and skills reach Ask Daoris only where its manifest declares them for it, approved by the person in
    restricted mode. A plugin changes Daoris only through the person's presses, each on its card. (D170, D183, D261)
62. The room says each role's staffing, policy and last seven days, so *why did this run on Opus?* is answered from the records.
    (D226)

## Where a change goes

- **A new work or kind of start:** `RoleWork`, `StartPlan.RoleOf`, the seed, the participant's work rule
  (`Roles/Domain/ParticipantWork.cs`, with its service and page twins); its tools in the service's call table.
- **A role's field or judge:** `RoleCatalog` and `RoleRecords`, reached only through `RoleDoor`. No second writer.
- **Staffing:** `RoleStaffing.Set`, the one writer; another reader of an order goes through `RoleOrders` (`OrderReadersCensusTests`).
- **A policy field:** `StaffPolicy`, `StaffJudge`, then `StaffCapabilities.Fields`.
- **A supervisor count or action:** `SupervisorRules`, the count in `Course/Domain/LineCounts.cs`, read through `SupervisorRuleBook`.
- **A decision kind:** a row under `Modules/Bridge/SupervisorKinds/` and its typed subject; each act's spend and pin in `ActSpends`.
- **A steward act:** a row in `StewardActRows` naming its person's route, its name in `StewardActNames`, the web's `ACTS`, its words
  in both catalogues.
- **An Ask Daoris door, only where one is wanted:** a view in `Modules/Platform/Views/` registered in `PressViews.All`, its
  sentence under `person.view.<kind>`, the room's line and its doors-table row.
- **Tests:** driver `Roles/*Tests`, `Help/`, `Knowledge/HelpRoom*Tests`; modules `DriverModuleRole*Tests`, `RoleActTests`,
  `Holder*Tests`; service `SupervisorWordTests`; web `agents/roles/`, `agents/setup/`. Gates: `driver`,
  `modules`, `censuses`, `service`, `web-unit`, `universal`.

## Contradictions

1. D229's 2026-11-04 note (`daoris role rules`) vs `RoleVerbs` (`driver role rules`): the verb's grammar.
2. D226 (seven conditions with their actions) vs `StaffConditions` and `SupervisorRules` (moves for four counts): the code.
3. D226 (moves and `most` in the policy) vs D229 (the supervisor's rules): D229.
4. D205 points 2, 7, 8 and 9 (choices in `driver.json`, `driver actor`, `SET_ACTOR`) vs `role_staffing` and `driver role`: the code.
5. D89 (Ask Daoris's agent under Settings → *Daoris's own AI*) vs the `ask-daoris` role's staffing: the code.
6. D170 (never a shell) vs D183 full access: D183; D170 holds in restricted mode.
7. D209 point 1 and the COMPANYVIEW1b and 1e notes (appointed under Settings, then Agents → Roles) vs Agents → Setup: the code.
8. D110 point 4 (no rule proposed by an agent) vs Ask Daoris pressing `daoris role rules`: D110 means permission rules.
9. D208 points 4 and 8 (a workspace's supervisor later; held five minutes) vs workspace appointments, 1 to 60 minutes: the code.

## Open

- **The window redesign** (`docs/2026-11-19-window-design.md` §5, §6, §13.4): the agents' attention kinds go to the appointed
  supervisor and only the owner's kinds wait on the owner (D336, UISH6, UISE9), and every owner act is defined once and can be
  given to an appointed role, but *Confirm it was you* (D340, UISH24, UIRP17, UIRP29). Today the rules below stand.
- **RISKGATE1** (D354): rule 49a's levels and marks become one risk policy of act kinds, each `none`, `role` or `owner` at its place,
  read by one judge at every hold; a gate names a role filled by the cast, refuses an answer from the work's own lineage (`own-work`)
  and never falls to the owner when vacant; a change to the policy, a cast granting an act or an access mode stays the owner's own,
  given in the window, under the person's key or confirmed in the assistant's conversation (built, rule 49b). The judge and its record run in shadow
  (rule 49b); today rules 43 to 49a decide.
- **THROUGH1** (D277): Ask Daoris carries the person's words on an ask (`--require`, `--through`), never pressing a run's step.
- **CONVO1b** (D280): Ask Daoris's reach set by its access mode alone, the card's *Always* the grant of a named family shown before
  the yes, five fences in both modes each held by its owner on each path and said where only the room and the second check hold
  it; a read-only helper about a repository; the *yours alone* rows and *Done at a terminal* go
  (`docs/2026-11-12-conversation-foundation-design.md` §5).
- **GROWDEPT1** (D276): an agent's press creates a repository or a room, per the design; not built.
- **CONTEXTDRIVE1j, l, m**: the installed proof of a parked quest gone on, and of the hooks.
- **STAFFPOLICY1h, i**: selection by measurement; the installed proof.
- **MODELROLE1l, e2, i**: the installed proof of design and exact quests on their roles; the partner's case with COAGENT1.
- **SUPERVISOR1a-i, a-iii, d, f; QUESTIONUI1b**: the sweep test, a question answer's *Overrule* mark, the go-on's process case;
  the activation's installed proof; the bench; the words; the pick on the question card.
- **STEWARDROLE1d**: the installed look of rule 51's acts; **STEWARDROLE1e-h**: the steward's answers, go-on, set-up and words.
- **PERSONDOOR1c-i**: the role's boundary at every door.
- **SETUPUX1y**: the places tree lit by default, retiring the Roles sections under Setup.
