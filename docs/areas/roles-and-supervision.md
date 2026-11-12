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
| `Roles/`, `Defaults/roles.json` | The catalogue and its seed (`RoleCatalog`), the one door and its verbs (`RoleDoor`, `RoleVerbs`), staffing (`RoleStaffing`, `ActorChoices`, `StartPlan`), policy (`StaffPolicy`, `StaffJudge`, `StaffLedger`), the rule supervisor (`SupervisorRules`, `SupervisorRuleBook`), the appointed shape (`RoleHold`, `AgentHold`), the decisions' memory, files and reads (`Supervisor*`), the steward's verb |
| `Loop/Supervise/`, `Loop/Look/` | An agent supervisor's look, attempt and read-only turn; the releases between turns; the second check's reading queue |
| `Modules/Bridge/` | The decision door (`SupervisorDecisionDoor`, one row per kind under `SupervisorKinds/`), the steward's act rows |
| `Modules/Platform/` | The roles' terminals this run (`StandingConfirmation`), the delegate proof, Ask Daoris's press views (`Views/`) and press record |
| `Help/`, `Help/SecondCheck/`, `Knowledge/` | Ask Daoris's modes, configured access, go links and room; the second check's selection, packets and readings |
| `Participants/Domain/StartAdmission.cs`, `Service/Daoris.Service.Mcp/CallTable.cs` | Who may ask for a co-worker; which tools each work is served |
| `Web/agents/roles/`, `Web/agents/setup/`, `Web/work/head/` | The role editor and policy form, a place's Roles and *Who decides here*, the supervisor's cards |

## Its rules

**The roles**

1. A role is a record of the role catalogue in the desktop store, in one of two shapes. A working role: a name (authored text),
   the works it takes, a scope and a staffing. An appointed role: a name, its kind (supervisor or steward), the acts it holds of
   that kind's vocabulary, a scope, its holder and the supervisor's window. A role carries no doors of its own. (D212, D222, D233)
2. The works are build, design, exact, chat, ask-daoris, intake, help, partner, review and brainstorm, read from a record's marks
   by one rule, never stored. Design and exact fall to the role taking build when none takes them. (D212)
3. Ten roles are seeded machine-wide: build, design, exact, intake, opinion, helper, partner, brainstorm, ask-daoris and chat. An
   id reads as its seed until the store holds a record of it, so an upgrade writes nothing. Design takes design work on Claude
   Code · `opus` · high; Exact build takes exact work (`weight: exact`) on Claude Code · `sonnet` at the account's default effort,
   a longer window the person's to choose. Each may be named by a workflow step and falls to Build's when removed, and a start says
   whose order it ran on; a quest marked neither keeps Build's. A seeded role is edited, restored and removed by the one door; no
   suggestion adds it. (D205, D212)
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
   reaches, never the machine's; an ask naming the person is no supervisor's. No supervisor record is written before the home's
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
13. The release's migration imports an older home's `driver.json` staffing once: Build, Intake, Ask Daoris, Chat and Brainstorm as
    they ran, at account default, an unreadable entry kept; Second opinion, Partner and Helper left to their caller's rule; the
    four keys taken out. (D232)
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
    reported tokens in the machine's local day. A budget acts at a start only. (D226)
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
35. One seed serves a fresh home's first start and the release's migration: answers alone at 2, failures at 3 (an older home's
    `strikes`), tries at 5 and moves 3 a day before the next asks, for every role. Where the machine holds nothing, or the store
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
    supervisor, never overrule it. The steward is the catalogue's one steward. The seed's *Second check* (`second-check`, 复核) is
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
    one `role-changed`. `driver role` refuses every write from a process an agents' job holds. (D208, D209, D233)
44. The holder is a dedicated session in a tab the window starts, outside every repository, on a configuration Daoris writes,
    running only read tools and its roles' verbs. Every other verb refuses `role-terminal`. (D208, D209)
45. The holder key is drawn at the appointment, kept only as a hash in memory, and stripped from every other start. The verb sends
    an HMAC proof over the hello's nonce, only to the install's own executable. (D208, D209)
46. A supervisor decides or leaves a decision through one door, `SUPERVISOR_DECIDE` and `SUPERVISOR_LEAVE` (`Role`), each proved
    over its route's bytes with the subject whole: the kind's typed reference, unchanged while it waits (a question's `{ session,
    question }`, a point's, a landing's, an allow's, a files ask's). Each kind is one row listing its waiting subjects and keeping a
    decision against the subject as it stands. On a question it answers a parked session's recorded question with one offered
    choice and one to five sourced reasons (`question-changed` where it no longer waits); any subject may be left to the owner with
    a bound, only by the hold answering its place. The holder's verb is `sessions supervise <session> --kind <kind> --subject <id>
    --generation <g> --do <act> [--choice <label>]` or `--leave <bound>`; `SUPERVISOR_WAITING` lists the subjects a terminal
    holder's grants cover. (D208, D233)
47. It never answers a go-ahead, a sign-in, an access widening, spending, a deletion, a question the repository says to ask the
    person, a preference, an unrecorded or contradicting one, or one that directs it. (D208)
48. It is also refused on a question the agent marked the owner's, past the answers-alone bound, and where a person's rule holds
    the line (`rule-holds`), at the keeping and again at the release. A `refer` on answers alone holds the line as `hold` does until
    the holder decides `go-on` once; on failures the quest's point is released as the quest's own *Try again*; on failures on one
    choice, a limited end and the moves bound the limit kind decides `move`, or on a limited end `wait`. Each is held for the role's
    window with *Overrule*, then released between turns through the count's own door with the admission, the rules and the
    person's own holds (`owner-held`) read again first, never while a session runs, and said on its line; a go-on resets the count
    as the person's press does. A decide on a point the line passed is `answered`, on one never reached `not-referred`; a leave is
    the person's at once. No course act stops, ends or moves a session. With no started holder the point waits for the person
    (`supervisor.unreferred`). A restart empties memory: the sweep closes held and released decisions stale and remembers each
    subject final to the person as closed (`overruled`). Decisions read back as `SESSION_SUPERVISOR`'s `courses`, with *Overrule*
    while held. (D229, D233)
49. A decision is held whole in memory for the supervisor role's window (1 to 60 minutes, 5 by default), then released in one
    conditional step that asks the admission again. Its word is `From: supervisor`, never the person's; an unattributable one is
    quarantined. A landing is the same window with *Overrule*: the supervisor's `accept` of a `review`, or `approve` or `reject` of a
    `human` approval, kept against the step as it waits on that attempt and naming the review record's revision, is refused
    `answered` once it moved, `go-ahead-open` under a `go-ahead` and `owner-held` on a line the person stopped or paused. Its
    release reads all again, a moved record going stale, and keeps the node's word on the run's record with its author, which the
    pass applies or drops; a person's word supersedes it, and a `return`'s per-file choice offers a supervisor no act. The head's
    card says an Accept only when it goes on, by the kind of work its record holds. (D208, D233)
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
    refused `moved` on another record) are the person's presses. Where the person asks for work to be delivered without their
    press, Ask Daoris puts their requirements and through words on the ask when it makes it (`--require`, `--through`, `ask
    through`), each verified verbatim against their messages in its conversation. In restricted mode it never presses a run's step;
    in full access it presses an Accept or the pass of an unchecked done only where the person asked for the delivery in words and
    the run still waits on it, said once. The room says this one rule in its section and its doors table. (D161, D193, D233, D253,
    D261, D273, D277)
56. The modes differ only in who says yes to a press of Daoris's verb, which both run. Every session's start is handed a delegate
    key of its own, kept only as a hash by session, stripped from every other start and forgotten at its end and an access swap;
    the verb proves a press with it over the hello's nonce. In restricted mode, the default, a proved press waits on a card naming
    Ask Daoris (and the place it names, where outside the focus) and runs on the person's yes. The press is the one way to ask the
    person: a route with a view (`PressViews`) judges the press at the ask by the route's own rules, refuses in those words with no
    card, and gives the card what the press means and its command. The views cover the routes Ask Daoris presses: a repository's,
    a session's, a quest's and an ask's acts, an account's and an agent's settings, a place's switches and rules, clean-ups, git
    identities, the machine's and the browser's settings, a role's change, a plugin's, a history clear, a sync, a place's set-up and
    a new ask. A settled press no line waits on is said in its conversation. A working session's tier is its repository's access
    setup, read at the press: unrestricted runs its proved press as its own (rule 52), the rest go on a card naming the session.
    Ask Daoris has no proposal tool; the room's *What you may do* names the verbs. `sessions archive --ended` stays the terminal's.
    (D89, D170, D253, D261)
57. Restricted mode adds the access the person configures: the checkouts it reads, each read judged at the call; and the families
    of acts it presses with no card, the one shape a grant has: `pause`, `clean-up`, `discard` and `settings`. A granted family
    runs a proved press as the person's own, read at each press; a look asks no grant. An older record's `housekeeping` and
    `settings` lists grant nothing until the home's migration converts them. Its tool calls outside its allow-list are held for the
    person (agents-and-harnesses 23). No family reaches a role. (D183, D205, D253, D261)
58. Full access runs its harness's unrestricted mode, keeps `checkout_read` and names each readable checkout and where it is, and a proved press runs at once with no card as the person's own, said once
    on its conversation's console, logged `help.pressed` and kept in this run's memory with what it named (the newest 100, a
    restricted press kept there as asked with its card's state), listed in *Notifications*. A terminal's press under the tier `act`
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
    the acts it never presses. An older home's Ask Daoris proposals folder is removed once by the home's migration, each file
    salvaged. (D110, D170, D233, D261)
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
- **A decision kind:** a row under `Modules/Bridge/SupervisorKinds/` and its typed subject.
- **A steward act:** a row in `StewardActRows` naming its person's route, its name in `StewardActNames`, the web's `ACTS`, its words
  in both catalogues.
- **An Ask Daoris door, only where one is wanted:** a view in `Modules/Platform/Views/` registered in `PressViews.All`, its
  sentence under `person.view.<kind>`, the room's line and its doors-table row.
- **Tests:** driver `Roles/*Tests`, `Help/`, `Knowledge/HelpRoom*Tests`; modules `DriverModuleRole*Tests`, `RoleActTests`,
  `Holder*Tests`; service `McpRoleFilterTests`, `SupervisorWordTests`; web `agents/roles/`, `agents/setup/`. Gates: `driver`,
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
