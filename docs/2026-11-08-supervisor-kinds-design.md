# SUPERVISOR2: supervisors staffed through the actor system, generic or of a kind

Design, proposed for the owner's yes. Round 1, written by Claude Opus 5.5 on 2026-11-08 from main at `50055ee6d6`, on the owner's
direction in D233's note of 2026-11-08 and the principle in D214's note of the same day. Both makers' reads are owed; nothing here is
accepted, and no decision records it yet. Read with `docs/areas/roles-and-supervision.md` (cited *rule n*), the supervisor design
(`docs/2026-10-24-supervisor-design.md`, D208) §3 to §6, the context-counters design (`docs/2026-11-03-context-counters-design.md`,
D229) §4 and §5, and the staff-policy design (`docs/2026-11-02-staff-policy-design.md`, D226). Paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Modules/` (`src/Daoris.Desktop/Daoris.Desktop.Modules/`),
`Service/` (`src/Daoris.Service/`) or `Web/` (`src/Daoris.Web/src/`), as the area writes them. Line numbers are main's at
`50055ee6d6`. Nothing was built or run.

## 0. In short

- **The holder.** The supervisor takes a third holder, `agent`. Its staffing is an order of one to four choices of agent, model and
  effort, set through the role door and walked as every role's order is (rules 11 to 20), under the staff policy's rule, weights, shares
  and budgets. A decision is one read-only turn of that actor; its answer is the turn's last message, kept by the same path as a
  terminal's. The steward keeps its terminal holder, and `appointed-not-configurable` becomes the steward's refusal alone.
- **Kinds.** A decision kind is a set of decisions that wait for an allow or a choice: `question`, `course`, `limit`, `allow` and
  `landing`. A supervisor's record names the kinds it `decides`. One that names none is its scope's generic supervisor and answers every
  decision no kind supervisor there takes. A scope holds at most one generic supervisor and at most one supervisor of each kind (rule 7's
  *two of one kind never share a scope*, read by decision kind). The machine always holds a generic one.
- **Resolution.** A decision at a place goes to the most specific scope's supervisor of its kind, else that scope's generic one, else
  the next scope out. The catalogue alone decides. Whether the supervisor found can act (its holder, its grants, its actor's
  availability) never sends the decision on to another: when it cannot act, the decision falls to the person's rules, which acted first,
  and then to the person.
- **Grants.** Each kind's acts are named in the role's `authority` (`question.answer`, `allow.once`, …). They are off on a new record, as
  every role act is, and one check enforces them at the judge and at the actor's keeping; today's code checks none of the supervisor's.
  Leaving a decision to the person needs no grant. A decision is logged as the role's act and said on its subject's line and the
  supervisor's line. It is held for the role's window with *Overrule*, and the person's own word supersedes it (rule 50).
- **Allows.** Some of the person's added restrictions ask for an allow: an asking-mode permission request, a widening a session
  proposes, a review or an approval before a landing, and a command the rule file holds (TREEPOOL1 §30). Each such allow is a decision of
  its kind. The supervisor gives it under its grants, and the person gives it only where no grant covers it (D214's principle note).
- **Rows a to h** (§9): kinds and grants in the catalogue; one decision door; the actor; limits and moves; allows; landings; the rule
  file's allows with TREEPOOL1; the install's proof.

## 1. What is asked, and what is true today

### 1.1 The direction

The owner's direction of 2026-11-08 (D233's note) says that Daoris's structure holds what a person would otherwise allow by hand, and a
supervisor answers it under its grants. A place may have one generic supervisor or several supervisors of kinds, and the generic one
answers what no kind takes. A supervisor is configured through the actor system: its staffing chooses its agent, model and effort as any
role's is chosen. The note rejected an appointed role refusing an agent holder and actor choices (`appointed-not-configurable`) for the
supervisor. D214's principle note says Daoris is a development tool, so restrictions are the person's to add; where one asks for an
allow, the place's appointed supervisor gives it under its role's grants, and the person gives it only where no role covers it.

### 1.2 Today

| Part | Today | Where |
|---|---|---|
| Holders | The supervisor is held by `rules` (the person's rules alone) or `terminal`; the steward by `terminal` or no one. `agent` reads as a record's shape and is refused `appointed-not-configurable`, and so is a staffing of choices on any appointed role | `Roles/RoleHold.cs:75-83`, `:150-153`; `Roles/RoleDoor.cs:210-213`, `:222-224` |
| One per scope | Two appointed roles of one kind (supervisor, steward) never share a scope; the machine's supervisor may not be removed | `Roles/RoleCatalog.cs:317-321`, `:324-328` |
| What it decides | A parked session's recorded question (`SUPERVISOR_ANSWER`, `SUPERVISOR_DEFER`), and a `refer` point at answers alone, failures since *Try again* or failures on one choice (`SUPERVISOR_COURSE`), both listed by `SUPERVISOR_WAITING` | `Modules/Bridge/SupervisorDoor.cs:226-288`, `Modules/Bridge/SupervisorCourseDoor.cs:12-46`, `Modules/Bridge/Routes/SessionsRoutes.cs:249-274`, `Modules/Bridge/Routes/SupervisorCourseRoutes.cs:14`; rules 46 to 48 |
| Grants | The supervisor's `authority` vocabulary is `answer` and `defer`, but no code reads it: the role method covers a supervisor's route without looking at `authority`. The steward's acts are checked against it, and one outside them asks the person (`StewardAsk`) | `Roles/RoleHold.cs:19`; `Modules/Platform/RoleJudge.cs:162-166`, `:168-171` |
| The effective supervisor | Two copies of *the most specific scope's role of a kind*: `RoleCatalog.Appointed`, which no production code calls, and `StandingConfirmation.Staffed`, which the admission reads. The repository's workspace comes from the loop's registry, handed in as `WorkspaceOf` | `Roles/RoleCatalog.cs:229-230`; `Modules/Platform/StandingConfirmation.cs:179-180`, `:219-223`; `Modules/Loop/LoopRun.cs:247` |
| Its admission | Only a started terminal admits. Held by rules, the admission answers `not-staffed`, and a `refer` point waits for the person, said `supervisor.unreferred` | `Modules/Platform/RoleJudge.cs:25-35`; rule 42 |
| The actor | Designed as SUPERVISOR1c (the supervisor design §3.5, §11): one read-only turn, ten minutes, one attempt per question, its result the run's last message. Parked in `docs/backlog-parked.md` behind SUPERVISOR1d's bench | — |

This design reverses rule 7's refusal for the supervisor. What rules 41 to 52 hold for a terminal holder holds for each kind and for
the actor, except where §2 to §4 say otherwise. SUPERVISOR1c is this design's row c; the parked SUPERVISOR1 row's 1c and 1d are folded
into SUPERVISOR2 once it is accepted.

## 2. The supervisor held by an actor

### 2.1 Its holder and its staffing

- **`holder: agent`** is accepted for a supervisor at any scope. The steward refuses it with `appointed-not-configurable`, whose
  sentence names the steward alone, and that is the code's only use.
- **Its staffing is a role's staffing** (rules 11 and 12): an order of one to four `{ agent, model?, effort? }`, judged by the one judge
  of an order's shape. That judge is `ActorKey.Judge` without its id check, which today limits orders to the seeded working roles. The
  order is kept as the role's own row of `role_staffing` through the role door's one write, as an added role's row is
  (`RoleStaffing.AddedRows`, `Roles/RoleStaffing.cs:404-416`). An agent-held supervisor with no choice is refused `choices`, and so are
  choices on a supervisor held by rules or a terminal, which has no order to walk; choices on the steward stay refused
  `appointed-not-configurable`. Moving the holder off `agent` deletes the row in the same transaction.
- **Its policy** is a working role's (rules 22 to 28): the rule (`order` or `weight`), weights, shares, and the daily budgets of starts
  or reported tokens, judged by `StaffPolicyDoor.Judge` against its choices. `works` is refused (`policy`). A supervisor takes no work,
  and a kind is the person's way to give some decisions their own staffing (§3). The presets write no rules for it, since its turns are
  not lines a rule moves.
- **The walk** is the one walk (rules 16 to 20). It goes choice by choice, asking each choice's accounts before the next. A provider not
  opened to the decision's place is passed `not-opened`. An account cooling for one model family passes that family's choices. A model
  refused at the open is re-asked once (`OneTurnReask`, as a helper's turn is). A running turn is never moved. The tier that ran is said
  on the decision as on any record (§2.4).
- **Suggested choices** gain the supervisor's dated row, judgement work as Design's is (`docs/model-roles.md`), applied only by the
  person's press (rule 21).

### 2.2 Its hold

A terminal's hold is drawn when the window starts its terminal (rule 42). **An agent's hold needs no press**: the role's record, saved
by the person through the role door, is the appointment. For each agent-held supervisor at its revision, the judge draws a generation
in memory at the run's first admission that needs one. These generations are kept in a list of their own (`StandingState.AgentHolds`),
never in the terminals' `Holds`, so no pipe act can name one: an act carrying an agent hold's generation finds no terminal's hold and is
refused as an unknown generation is (`Modules/Platform/RoleJudge.cs:135-141`). A role press through the door that changes the role ends its agent hold as it ends a terminal's (rule 43), and the
decisions kept under it are swept (`SupervisorDoor.RolesChangedAsync`). A restart begins with none, and the first look draws them again.
No decision survives a restart (rule 49; the supervisor design §3.4), so the actor's attempts resume only on subjects still waiting
(§2.3).

The admission (`IDecisionAdmission`, `Roles/SupervisorDecision.cs:8-12`) answers, for a decision's kind and place, the effective
supervisor's generation and window. That is the started terminal's hold where the holder is `terminal`, the agent hold where it is
`agent`, and `not-staffed` where it is `rules`.

### 2.3 How a decision is made: the attempt

The supervisor design §3.5 fixed the contract. This design keeps it and gives it the walk.

1. **When.** The look's `supervise` pass (`Loop/Look/SuperviseLook.cs`, after `course-release`) reads every subject waiting at a place
   (the kinds' lists, §3.1). It takes those whose effective supervisor is agent-held, whose grants cover at least one act of the
   subject's kind (§4), and on which no decision is held and no attempt runs. It starts at most one attempt per line at a time, and at
   most one per choice for one subject. One point is referred once (the context-counters design §4.6 point 3): a course or limit point
   waits while a question decision on its line is open.
2. **The attempt.** An attempt id is written in the subject's decisions folder before the start. An attempt found there with no
   outcome at a start, or past its bound, is closed as failed. The turn has a helper's shape (`Loop/Helpers/HelperTurn.cs`, the
   exemplar): a read-only copy of the subject's repository at the subject's commit with its work in progress laid over it
   (`OpinionTree`, `HelperCapture`), and no copy for a subject that names no repository. It runs one turn under the reviewer's read-only
   rules, and the copy is removed however the turn ends. Its record is marked `decides` with the attempt's id, and `serves` with the
   subject's session where it has one, so its work reads `supervise` (§7.6). The start is bound to its role and the role's revision
   (rule 8) and keeps `actor`, `choice`, `asked`, `ran` and `chosen` as every start does (rule 20). It takes no place under the cap. It
   decides for work already under the cap and waiting on it, and holding a slot would leave a full cap's running session waiting on its
   own allow. §2.5 bounds its spend instead.
3. **Its tools.** The connector serves a `supervise` session the knowledge and reading tools alone (`knowledge_search`,
   `knowledge_get`, `knowledge_repositories`, `knowledge_convergence`, `line_read`, `handback_read`, `memory_get`, `memory_search`,
   `quest_list`, `registry`). It gets no say, no answer, no act and no door that names a session. It asks for no co-worker (rule 54). A
   permission request it makes is refused (agents area rule 23), and no decision about a `supervise` session is any supervisor's.
4. **Its result** is the turn's last message, holding one fenced block in the decision door's form less its generation and proof
   (§7.5):

   ```text
   <<< supervisor decision
   { "kind": "question", "subject": "s-1a2b3c4d", "do": "answer", "choice": "Keep the old name",
     "reasons": [ { "says": "The rename is the owner's to choose.", "source": "docs/decisions/D201.md point 3" } ] }
   >>>
   ```

   A leave reads `{ "kind", "subject", "leave": "<bound>", "reasons" }`. The driver reads the block from that run alone, binds it to the
   attempt's generation, and keeps it through the kind's own keeping (§3.1). The grant (§4), the subject and the admission are read again
   at the keeping, as they are for a terminal's act. A block from another session, a late one after a revoke, a changed role or a changed
   subject, or a second one for one attempt, is dropped and logged `supervisor.dropped`.
5. **One attempt per choice.** A failed end, a timeout, a block that does not read, or a block whose kind or subject is not the
   attempt's ends the subject's attempts. The subject is then the person's, and its card says why (`supervisor.attempt-failed`). A limited
   or transient end is a pass of the walk and not a failure (rule 33): the next attempt takes the walk's next choice at once, or waits
   for the walk's soonest way out. A leave is a decision (`left`) and is final.
6. **The person first.** The person's word on the subject at any time ends a running attempt (the driver stops its own turn) and drops
   its result.

### 2.4 What it is handed, and what it answers with

The prompt (`Roles/SupervisorInstruction.cs`, composed as `HelperInstruction.Compose` is) hands what the terminal's print hands (the
supervisor design §3.2; the context-counters design §5). One composer serves both the prompt and `sessions supervise <id>`'s print, so
the actor and a terminal holder read the same facts:

- the subject's own words, as the asking agent's claims, fenced under a nonce (the supervisor design §4.4);
- the go-aheads on its ask;
- the work order and the owner's own words on the ask, quoted with their dates;
- the standing answers;
- the line's course, its counts and the rules in force, as Daoris's facts;
- for an allow, the call or the command and the restriction that asked;
- for a landing, the step, the branch's commits and the review's state;
- the offered acts, marked by whether its grants cover them.

It answers as a terminal does: one act its grants cover, or a leave with a bound, and one to five reasons each naming its source
(`no-source`); a `count:` source names a count it was handed. The decision keeps its holder as `{ kind: "agent", role, revision, agent,
model, effort, ran, session }`, so its card says the tier that actually ran.

### 2.5 Its bound and spend

- **Per turn:** ten minutes, or the subject's own bound less one minute where that comes first (a held permission request's `Until`).
- **Per subject:** one attempt per choice of the role's order, so at most four.
- **Per line:** one attempt at a time. The answers-alone count (rule 48) bounds questions and courses in a row, as it bounds a
  terminal's.
- **Per day:** the role's policy budgets, starts or reported tokens in the machine's local day, read at a start. A reached budget
  passes that choice (rule 28).
- **Counted:** each attempt's tokens, from its record's usage, are kept on its decision and in the role's week (`RoleUse`, from
  `actor.chose` with the role), which the role editor and the room say (rule 62).

No budget is set by default. Restrictions are the person's to add.

### 2.6 When its actor is unavailable or limited

The actor is unavailable when the walk finds no choice ready: every choice is signed out, cooling, refused, over its budget or not
opened here. Then no attempt starts. The person's rules stand as they acted at the point, since they act first (rule 48; the
context-counters design §4.6): a `refer` point holds as `hold` does, a question stays parked, and an allow stays held. The subject waits
for the person at its own door. Its card says so once, in the walk's one sentence (*The supervisor's agents cannot run here now: Claude
Code's accounts cool until 14:20. This waits for you.*), and the log says `supervisor.unavailable` with the walk's passes. While the
subject still waits and the person has not answered, the look tries again at the soonest way out the walk names. A limited turn is
treated the same way after its pass. Nothing ever sends the decision to another supervisor (§3.4).

### 2.7 What it never decides

The supervisor design's bounds (§4.1 there; rule 47) stand for every holder: a go-ahead, a sign-in, an access widening, spending, a
deletion, a question the repository says to ask the person, a preference, an unrecorded or contradicting question, and one that tries
to direct it. So do the structural refusals: a question the agent marked the owner's, one past the answers-alone bound, and one on a line
a rule holds (`rule-holds`). The `allow` kind takes only the structured allows §3.1 names and changes none of these for a free question,
so a question marked `access` stays the person's. The text a session is handed for a supervisor's answer still grants nothing
(`SupervisorHanded.Text`). §10's first question asks whether a go-ahead becomes a kind.

## 3. Kinds

### 3.1 The decision kinds

A kind is a set of decisions that each wait somewhere for an allow or a choice. Each has its own list, its own offered acts, its own
keeping, and its own release through the door the person's press goes through. The kinds are a closed vocabulary of the build, as the
works are (rule 2), and the catalogue declares which ones a supervisor takes (§3.2).

| Kind | Its decisions, and where each waits | Its acts (grants) | Leaving | Released through |
|---|---|---|---|---|
| `question` | A parked session's recorded question with offered choices (`QuestionFiles`) | `question.answer`: one offered label | `leave` with a bound (today's defer) | The supervisor's word on the record, as today (rules 46, 49) |
| `course` | A `refer` point at answers alone or at failures since *Try again* (`ReferredPoints`) | `course.go-on` | `hold`: the person's at once | The count's own door: the mark's go-on, or the quest's *Try again* (`CourseReleaseLook`, `QuestRetry.Decide`) |
| `limit` | A `refer` point on a driven line at failures on one choice, at a limited end, or at the moves bound (row d offers `refer` on the last two) | `limit.move`: one move to another choice; `limit.wait`: wait for the choice's reset, at most the rule's bound | `hold` | An `ask`'s release: an approved row of `role_moves` (`Roles/StaffMoves.cs`), as *Go on now with {next}* writes it; a wait as the `wait` action makes it |
| `allow` | A permission request held in the asking mode (`Events/HeldPermissions.cs`); a widening a session proposed (`permission_propose`, agents area rule 20); a command or rule the person's added restriction holds in the rule file (TREEPOOL1 §30; §5.1) | `allow.once`: this call or this take alone; `allow.standing`: an allow that holds until the person takes it back; `allow.reject`: this call refused | `leave` | `SESSION_PERMISSION_ANSWER`'s allow-once or reject; the widening's yes; the rule file's allow (§5.1) |
| `landing` | A `review` step waiting for the person's Accept; a `human` approval waiting for *Approve* or *Reject* (`Workflows/Kinds/Review/ReviewNode.cs`, `Workflows/Kinds/Human/HumanNode.cs`) | `landing.accept`: Accept, with no changes and no words; `landing.approve`; `landing.reject` | `leave` | The word the person's press keeps on the run's record (workflows area rule 18), `by: supervisor` |

Each count belongs to one kind: `course` takes answers alone and failures, and `limit` takes failures on one choice, limits and moves.
Tries and the quiet end take no `refer`, as rule 32 holds today. A `human` task (a manual operation the person does) and a `go-ahead`
step belong to no kind. A landing whose plan names a plugin that pushes is refused `landing-pushes`, as the steward's `land` is (rule
51), and a run a go-ahead holds is never any supervisor's.

### 3.2 Declared in the catalogue

A supervisor's record gains `decides`: one or more kinds it answers, written beside `authority`. A record with `decides` absent or empty
is its scope's **generic supervisor**, and the seed's `supervisor` is the machine's generic one. A person adds a supervisor of a kind as
any role is added (rule 1): an id, a name, `kind: supervisor`, its `decides`, its scope, its holder, its staffing where an agent holds
it, its acts and its window. `decides` names only kinds of the vocabulary, and `authority` only the acts of the kinds the role decides
(all kinds for a generic one); either refused `bad-record`.

### 3.3 One of a kind per scope

- A scope holds **at most one generic supervisor** and **at most one supervisor deciding each kind**. Two that share a kind in one
  scope are refused `two-roles-one-work`, its sentence naming the kind and the other role. A supervisor deciding `allow` and `landing`
  can stand beside one deciding `question` in the same repository.
- **The machine always holds a generic supervisor.** It holds the person's rules (rule 7), so removing it, scoping it away or giving it
  kinds is refused `may-not-be-off`.
- The steward's rules are unchanged: one steward, on the machine.

### 3.4 Which supervisor answers

For a decision of kind *k* at a place (a repository and its workspace, resolved once as a line's place is, rule 36):

1. the repository's supervisor deciding *k*; else
2. the repository's generic supervisor; else
3. the workspace's supervisor deciding *k*; else
4. the workspace's generic supervisor; else
5. the machine's supervisor deciding *k*; else
6. the machine's generic supervisor, which always stands.

One resolver answers this, `RoleCatalog.Supervising(kind, place)`. It replaces both of today's copies (§1.2), and the admission, the
lists, the release and the window's *Who decides here* (§6.2) all read it. The repository's workspace is the one the rules read in every
process (`RuleWiderPlaceStore`, rule 33), and the judge's own `WorkspaceOf` hand-in goes.

**The answer is structural.** The supervisor found answers, or nobody does. If it is held by rules, if its grants do not cover the act,
or if its actor is unavailable, the decision is the person's, never the next scope's. So a repository's supervisor held by `rules` that
decides `allow` keeps that repository's allows the person's while the workspace's agent decides allows elsewhere, and a person reads who
decides from the catalogue alone. A decision kept under one supervisor goes stale at its release where another is effective now
(`role-changed`, as rule 48's release reads the admission again).

### 3.5 The rules first

The person's supervisor rules stay the place's, whoever answers (rules 31 to 40). They are read first at every point with no model, and
any act other than `refer` stands. `refer` hands the point to the supervisor of the point's kind (`course` or `limit`) by §3.4. Where
none can act, the point holds as `hold` does, said `supervisor.unreferred`, as today.

## 4. Grants

- **Named per act.** A supervisor's `authority` lists acts as `<kind>.<act>` from §3.1's table. The steward's acts stay as they are.
- **Off on a new record**, as every role act is: the editor and the verb tick none. The seed's machine supervisor lists
  `question.answer`, `course.go-on` and `limit.move`, the three decisions a terminal holder makes today with no grant check, so a home
  behaves as before and a person who gives it an agent sees it answer what a terminal would. Every other act (`limit.wait` and those of
  `allow` and `landing`) is off. A migration step (§7.1) rewrites a home's stored supervisor records once: `answer` becomes
  `question.answer` with `course.go-on` and `limit.move` beside it, and `defer` goes, since leaving needs no grant.
- **One check**, `SupervisorGrants.Covers(role, kind, act)` (`Roles/SupervisorGrants.cs`). The role method reads it for a terminal's
  decision (`Modules/Platform/RoleJudge.cs`, which covers a supervisor's route today with no check), and the actor's keeping reads it
  too. An act outside the grants is refused `not-granted` and keeps nothing. Unlike the steward's ungranted act (rule 51), nothing is
  asked, because the subject already waits for the person. The kinds' lists (`SUPERVISOR_WAITING`, the actor's prompt) name the acts the
  grants cover, and no actor is started on a subject whose kind no grant covers (§2.3).
- **Leaving needs none.** Any supervisor may leave a decision to the person with a bound and its reasons, as the steward's desk decline
  needs no grant.
- **Logged as the role's act**: `role.act` with `role` the role's id, `kind: supervisor`, the decision kind, the act and its outcome,
  beside the decision's own `supervisor.*` events. It is **said on its line** (§6.4) and held for the role's window, 1 to 60 minutes,
  with *Overrule*; where the subject's own bound less ten seconds comes first, it is held until then, and the card says the earlier
  time.
- **The person's own words supersede** (rule 50). The person's press on the subject's own door while a decision is held supersedes it
  (`superseded`). Once a decision is released, the person's later press is their own act on what it did, such as taking back a standing
  allow.

## 5. The rule file's allows, and the steward's acts

### 5.1 TREEPOOL1's allows

TREEPOOL1's round 9 (`docs/2026-10-26-tree-pool-design.md` §29, on main) handles every file git does not manage by rules. D214's notes
of 2026-11-08 settle three things: a repository's own commands run under an allow list shaped as an agent's permission settings are;
the rule file restricts nothing by default; and where a restriction asks for an allow, the place's supervisor gives it. §30, which writes
that principle into the rule file, has not landed on main. This design fixes what §30 meets:

- **A subject of kind `allow`.** A take, the files stage (§29.7 there) or a finish may meet one of the person's added restrictions that
  asks for an allow (a command a pattern holds, a copy, a deletion). It then raises a subject `{ kind: "allow", repository, what:
  "command" | "copy" | …, text, rule, asked }`, kept in the home and listed by the kinds' list. The stage goes on as §29.7 says a step's
  failure does, its folder absent or its copy unmade, said; the next stage that reads the rules takes the allow once it is given.
- **Answered by the repository's `allow` supervisor** by §3.4, under `allow.once` (this take alone) or `allow.standing`; otherwise by
  the person's press on the repository page or `daoris driver files`.
- **One allow list.** A standing allow is written into the same list the person's own allows are in, the person's layer of
  `data/files.json`, carrying `by: { role, decision }`. The page then says who allowed it, and *Take back* removes it as it removes the
  person's own. An allow is never kept in a second place. §30 chooses the field.
- **The person's restrictions only.** What the analysis finds and what a repository's own rules name act as found (D214's principle
  note). A supervisor decides only where the person has restricted.

Row g builds this with §30's rows, and §30's own row names it.

### 5.2 The steward's acts

The steward and the supervisor do different work: the steward makes the management acts its grants list (rule 51), and a supervisor
answers what waits. They meet in three places.

- **The steward never answers a decision.** No kind is the steward's, and its holder stays a terminal.
- **A restriction meets the steward as it meets the person.** A steward's act runs its person's route inside the role's door
  (`StewardRouteAct`). Where that route meets a restriction asking for an allow (a review waiting for Accept, an approval, an
  asking-mode request), the route refuses or waits as it would for the person's press, and the subject waits for its supervisor or the
  person. The supervisor's releases go through the same dispatch (`StewardRouteAct.DispatchAsync`, made the roles' one route dispatch,
  §7.4), so both roles' presses are made one way and the owner's marks pass both by.
- **The steward's act outside its grants** still asks the person (`StewardAsk`), never a supervisor. The steward's grants are the
  person's boundary for another appointed role, and one role does not widen another's. §10's third question asks whether that should
  change.

A supervisor's release and a steward's act on one quest meet at the quest's own doors, as any two presses do. The course's *Try again*
and the steward's `retry` both reach `QuestRetry.Decide`, and the later one finds the point settled (`answered`, stale).

## 6. What the person sees

### 6.1 The role editor (Agents → Setup → This machine → All roles, and a place's Roles)

A supervisor's editor (`Web/agents/roles/HoldFields.tsx`) shows, in order:

- **Decides:** *Every decision no other supervisor here takes* (generic), or the kinds ticked: *Questions*, *A line's course*,
  *Limits and moves to another choice*, *Allows*, *Landings*. The machine's generic supervisor shows this fixed and says why.
- **Held by:** *Your rules, then you* · *A terminal* · *An agent*. Choosing *An agent* shows the choices table a working role has
  (agent, model and effort, one to four rows, with up and down), *How it chooses*, and the limits fold with its budgets (no works
  column), plus the suggested choices card with the supervisor's dated row.
- **What it may decide:** each kind it decides as a group of its acts, every one unticked on a new role, and under them, once: *It may
  always leave a decision to you.*
- **Overrule window**, as today.
- Under the form, **its week** (rule 62): decisions made, left to you and overruled, and the tokens its turns spent, by choice.

The judge's refusal is said in place from its closed code, as for every role press. The page judges nothing.

### 6.2 A place's Roles: who decides here

A place's appointed rows (`Web/agents/setup/AppointedRows.tsx`) list every supervisor placed there. Each line says what it decides, who
holds it (an agent's line names its first choice: *Claude Code · opus · high*), its acts and its window. Under them, **Who decides
here** gives one line per kind from `ROLES`' resolution for this place (§3.4), naming the supervisor and its scope, or *you*: *Allows:
Gatekeeper (this repository) · Questions and a line's course: Supervisor (this machine) · Landings: you (Gatekeeper may not accept).*
*Add a supervisor for…* offers the kinds no supervisor here decides, and opens the editor with that kind, this scope and nothing
ticked.

### 6.3 The Agents place

- **All roles** lists every supervisor once with its kinds. The diagram (`Web/agents/roles/StructureCanvas.tsx`, `roleStructure.ts`)
  draws a supervisor node per role at its scope, edged to the kinds it decides, dashed where the person's rules hold it.
- **An agent's page** (*Used by*, `Web/agents/AgentPage.tsx`) names each supervisor whose choices use that agent, at its place in the
  order.
- **The call table**, read-only on the page (`Web/agents/roles/callTable.ts`), shows the `supervise` column.

### 6.4 The decision on its subject

- **On the subject's own surface**, a decision shows as held, with the time it goes on and *Overrule*; as final, with its state's words;
  or as left, *left to you*, with the bound and reasons. A question shows on the head card, as today; a course or limit decision on the
  course card (`Web/work/head/SupervisorCourseCard.tsx`); an allow on the held permission's card or the repository page's allow; a
  landing on the review step's card or the approval's.
- **Every decision names its supervisor and how it was held**: *Gatekeeper (an agent: Claude Code · sonnet · medium) allowed this
  once. Goes on at 14:06.* The supervisor's turn opens from there, read-only, as a helper's does.
- **The supervisor's line** in *Notifications* (`Web/notifications/SupervisorLine.tsx`, beside `StewardLine.tsx`) lists its decisions
  this run, newest first, each with *Overrule* while held.
- **What needs you**: a subject a decision holds is not a wait, as the supervisor design §4.2 has it for a question. A subject left to
  the person, refused by the grants, or waiting on an unavailable actor is a wait, and says why in one sentence.

### 6.5 Words, en and zh

| Term | en | zh | Note |
|---|---|---|---|
| decision kind | kind of decision | 决定类别 | glossary: what a supervisor decides; never *type*, and never the role record's own `kind` |
| generic supervisor | supervisor for every decision | 通用主管 | glossary |
| holder `agent` | an agent | 智能体 | the glossary's *agent* |
| holder `rules` | your rules, then you | 你的规则，再由你决定 | replaces *your rules* |
| `question` | questions | 问题 | |
| `course` | a line's course | 一条线的去向 | *line* as `work.supervision.thisLine` says it |
| `limit` | limits and moves to another choice | 额度用尽与改用其他选择 | *move*, never *switch* (rule 29) |
| `allow` | allows | 允许 | the glossary's 允许 |
| `landing` | landings | 落地 | the glossary's 落地 |
| `question.answer` | answer a question | 回答问题 | as today |
| `course.go-on` | let a line go on | 让这条线继续 | |
| `limit.move` | move a line to another choice | 让这条线改用其他选择 | |
| `limit.wait` | wait for a limit's reset | 等额度恢复 | |
| `allow.once` | allow once | 允许一次 | |
| `allow.standing` | allow until you take it back | 一直允许，直到你收回 | |
| `allow.reject` | refuse a call | 拒绝这次调用 | |
| `landing.accept` | accept work to land | 采纳工作以落地 | the glossary's 采纳 |
| `landing.approve` | approve | 批准 | |
| `landing.reject` | reject | 驳回 | |
| leaving | leave it to you | 交给你处理 | today's course word |
| Who decides here | Who decides here | 这里由谁决定 | |

Both languages' catalogues and the glossary gain these words in the rows that show them.

## 7. What changes in the code

Test paths are under `src/Daoris.Desktop/` (`Daoris.Desktop.Driver.Tests/`, `Daoris.Desktop.Modules.Tests/`) and
`src/Daoris.Service/Daoris.Service.Tests/`.

### 7.1 The catalogue and its judge

| Unit | File | Change | Row |
|---|---|---|---|
| `AppointedRoles`, `RoleHolders`, `RoleHold` | `Roles/RoleHold.cs` | The kinds' vocabulary and their acts (`SupervisorKinds`, replacing `SupervisorActs` at `:19`); `RoleHold.Decides`, read and written as `decides`; the judge refusing `agent` for the steward alone (`:150-153`) and holding `decides` and `authority` to the vocabulary | a; `agent` accepted at c |
| `RoleCatalog` | `Roles/RoleCatalog.cs` | One generic supervisor and one per kind per scope (`:317-321`); the machine keeps a generic one (`:324-328`); `Supervising(kind, place)` in place of `Appointed` (`:229-230`); `Named` admitting an agent-held supervisor for the `supervise` work alone (`:208`) | a, c |
| The seed | `Defaults/roles.json` | The supervisor's `authority`: `question.answer`, `course.go-on`, `limit.move` | a |
| The grants | `Roles/SupervisorGrants.cs` (new) | `Covers(role, kind, act)`, the one check | a |
| Migration | `Transitional/Migration/SupervisorGrantsStep.cs` (new), one entry of `HomeMigration.Steps` | Stored supervisor records' `answer` rewritten to `question.answer` with `course.go-on` and `limit.move`; `defer` dropped; the old names read nowhere after | a |
| `RoleVerb` | `Roles/RoleVerbs.cs` (`:21`, `:364-384`) | `--decides <kind,…>` (or `every`), `--holder agent`, and for an agent-held supervisor the order's words a working role's set takes | a, c |
| Ask Daoris's role card | `Help/Proposals/HelpRoleProposals.cs`, `Service/Daoris.Service.Core/Help/HelpRoleProposalWriter.cs` | The card carries `decides`, the holder and the choices | a, c |

### 7.2 The staffing and policy readers

| Unit | File | Change | Row |
|---|---|---|---|
| `RoleDoor.Judge`, `Staffed`, `Publish` | `Roles/RoleDoor.cs` | Choices on an agent-held supervisor judged and written as its row (`:210-213`, `:222-224`); its policy judged as a working role's, `works` refused (`:135-138`); the holder moved off `agent` deletes the row | c |
| `ActorKey.Judge` | `Settings/ActorKey.cs:39-73` | The order's shape judged apart from the id check, so an appointed role's order and a working role's share one judge | c |
| `RoleStaffing.AddedRows`, `StaffingProjection.Rows` | `Roles/RoleStaffing.cs`, `Roles/StaffingProjection.cs` | Unchanged: an appointed role's row is an added row, which the projection already keeps | — |
| `StaffPolicyDoor`, `StaffCapabilities` | `Roles/StaffPolicyDoor.cs` | The fields a build enforces for an appointed role: the rule, weights, shares and budgets | c |
| `StaffLedger`, `RoleUse`, `StaffWeek` | `Roles/StaffLedger.cs`, `Roles/RoleUse.cs`, `Roles/StaffWeek.cs` | A supervisor's starts counted and its week read as any role's | c |
| `ActorSuggestion` | `Roles/ActorSuggestion.cs`, `docs/model-roles.md` | The supervisor's dated row | c |
| `RoleDoor.Rows`, `ActorView` | `Roles/RoleDoor.cs:41-50`, `Roles/ActorChoices.cs` | `ROLES` answers the supervisor's staffing view, and each place's resolved supervisor per kind (`deciders`) | a, c |

### 7.3 `StandingConfirmation` and the role judge

| Unit | File | Change | Row |
|---|---|---|---|
| `StandingConfirmation.Staffed`, `WorkspaceOf` | `Modules/Platform/StandingConfirmation.cs:179-180`, `:219-223` | Gone: the admission reads `RoleCatalog.Supervising` and the store's wider places | a |
| `RoleJudge.Admit`, `Covers`, `Supervising` | `Modules/Platform/RoleJudge.cs:25-58` | By the decision's kind; for an agent-held supervisor, the agent hold's generation | a, c |
| `RoleJudge.Judged` | `Modules/Platform/RoleJudge.cs:102-183` | A supervisor's decision covered only where `SupervisorGrants.Covers` allows it (none checked at `:162-166` today) | a |
| `StandingState` | `Modules/Platform/StandingState.cs` | `AgentHolds` by role and revision, never in `Holds`, ended by `RoleTerminals.RolesChanged` as a terminal's hold is | c |
| `IDecisionAdmission`, `SupervisorHolder` | `Roles/SupervisorDecision.cs:8-33`, `:148` | `Admit(repository, kind)`; the holder gains the agent's role, revision, choice, tier and session | b, c |

### 7.4 `RoleActs` and the kinds' rows

| Unit | File | Change | Row |
|---|---|---|---|
| `StewardActs.TableOf` | `Modules/Platform/RoleActs.cs:77-79` | Unchanged for the steward. The supervisor's table stays empty: its decisions are the kinds' rows below, not `ROLE_ACT` acts | — |
| The kinds' rows | `Modules/Bridge/SupervisorKinds/` (new): `ISupervisorKind`; `QuestionKind`, from `SupervisorDoor.AnswerAsync`, `DeferAsync` and `WaitingAsync`; `CourseKind`, from `SupervisorCourseDoor`; `LimitKind`; `AllowKind`; `LandingKind` | Each kind one unit, registered once as a steward's act is a row: its list of waiting subjects, its offered acts, its keeping against the subject as it stands, and its release through the person's route | b; d, e, f |
| The roles' route dispatch | `Modules/Bridge/StewardRouteAct.cs` | `DispatchAsync` made the one dispatch by which a role makes a person's press inside the role's door, for the steward's route acts and the supervisor's releases alike | e |

### 7.5 The supervisor's door, routes and verb

| Unit | File | Change | Row |
|---|---|---|---|
| Routes | `Modules/Bridge/Routes/SessionsRoutes.cs:249-274`, `Modules/Bridge/Routes/SupervisorCourseRoutes.cs:14`, `Roles/SupervisorProof.cs:134-150` | `SUPERVISOR_DECIDE { kind, subject, generation, proof, do, choice?, reasons }` and `SUPERVISOR_LEAVE { kind, subject, generation, proof, bound, reasons }`, both class `Role`, in place of `SUPERVISOR_ANSWER`, `SUPERVISOR_DEFER` and `SUPERVISOR_COURSE`, which are removed in the same chunk; `SUPERVISOR_WAITING` lists every kind's subjects with the acts the grants cover | b |
| Forms | `Roles/SupervisorProof.cs` (`SupervisorForm`, `SupervisorDeferForm`), `Roles/SupervisorCourseForm.cs` | One decide form and one leave form, each proved over its canonical bytes as today | b |
| The holder's verb | `Roles/SessionsSuperviseCommand.cs`, `Roles/SupervisePointPrint.cs` | `sessions supervise <subject> --do <act> [--choice <label>] --reason …` and `--leave <bound>`; `--point` and `--course` go; one print composer shared with the actor's prompt | b, c |
| The decisions | `Roles/QuestionSupervisorDecisions.cs`, `Roles/SupervisorCourses.cs`, `Roles/SupervisorDecisionFiles.cs` | A decision keeps its `kind`. The course's lifecycle (held, went on, overruled, stale, left) serves `limit`, `allow` and `landing`. A file with no `kind` reads as `question` or `course` by its shape, a reader that goes once the retention pass has removed the last such file with its session (D195) | b |
| The door | `Modules/Bridge/SupervisorDoor.cs` (`:236`, `:259`) | The keeping's holder taken from the hold, no longer written as `RoleHolders.Terminal` | b |

### 7.6 The actor's run

| Unit | File | Change | Row |
|---|---|---|---|
| The look's pass | `Loop/Look/SuperviseLook.cs` (new), `[LookPass("supervise", LookPhase.Planned, "course-release")]` | §2.3 point 1 | c |
| The turn | `Loop/Supervise/SuperviseTurn.cs` (new), mirroring `Loop/Helpers/HelperTurn.cs` | §2.3 points 2 to 6 | c |
| The attempts | `Roles/SupervisorAttempts.cs` (new) | An attempt written before its start, closed at a start or past its bound | c |
| The prompt | `Roles/SupervisorInstruction.cs` (new) | §2.4 | c |
| The start | `Roles/StartPlan.cs:273-285` | `RoleOf("supervise")` is the supervisor; the plan walks the order of the supervisor the start is bound to | c |
| The work | `Roles/Domain/ParticipantWork.cs`, `Service/Daoris.Service.Core/Sessions/ParticipantWork.cs`, `Web/work/identity.ts`, `Service/Daoris.Service.Tests/fixtures/participant-work.json` | `supervise`, read from `decides` before every other mark; not in `HoldsSlot`; `RoleWork.Codes` stays the nine works a working role takes | c |
| The record | `Participants/Helpers/HelperDoors.cs` and its service route, as a helper's record is opened | A `supervise` record's open, marked `decides` and `serves` | c |
| The tools | `Service/Daoris.Service.Mcp/CallTable.cs`, `Service/Daoris.Service.Tests/fixtures/call-table.json` | `SupervisorServed`: the reads alone | c |
| Who may ask | `Participants/Domain/StartAdmission.cs` | A `supervise` session asks for no co-worker | c |

### 7.7 The censuses

| Census | File | What it holds after | Row |
|---|---|---|---|
| The staffing's writers | `Daoris.Desktop.Driver.Tests/Roles/RoleCatalogCensusTests.cs` | The role door is still the one writer, the appointed staffing named under it | c |
| Records held to their role (census C) | `Daoris.Desktop.Driver.Tests/Roles/RoleCensusTests.cs` | A `supervise` record is held to the agent-held supervisor its start was bound to | c |
| The order's readers | `Daoris.Desktop.Driver.Tests/Roles/OrderReadersCensusTests.cs` | No new direct reader: the attempt walks through `StartPlan` | c |
| The pipe's routes and classes | `Daoris.Desktop.Modules.Tests/Platform/PipeCensusTests.cs`, `PipeGroundsCensusTests.cs`, `Daoris.Desktop.Modules.Tests/Bridge/WindowCensusTests.cs` | `SUPERVISOR_DECIDE` and `SUPERVISOR_LEAVE` in `Role`; the three old routes gone | b |
| The owner's marks | `Daoris.Desktop.Modules.Tests/Platform/OwnerSetCensusTests.cs` | A supervisor's release, dispatched as a person's route inside the role's door, marks nothing as the owner's | e |
| The node kinds | `Daoris.Desktop.Driver.Tests/Workflows/Kinds/NodeDispatchCensusTests.cs` | The landing kind presses `review` and `human` through the kit's press door, the runtime naming no kind | f |
| The call table | `Mcp/McpRoleFilterTests.cs` over `fixtures/call-table.json` | The `supervise` column | c |
| The participant work | each side's `ParticipantWorkTests` and `ParticipantWorkReadersTests` over `fixtures/participant-work.json` | The `supervise` row | c |
| Suggested choices | `Daoris.Desktop.Driver.Tests/Roles/ActorSuggestionTests.cs` | The table held to `docs/model-roles.md`, the supervisor's row included | c |
| The scopes | `Daoris.Desktop.Modules.Tests/Platform/SupervisorScopeTests.cs`, `RoleActTests.cs` | §3.4's six steps; the grants at the judge | a |
| Translations | `Web/locales/i18nCheck.test.ts`, `Web/locales/glossary.test.ts` | §6.5's words in both catalogues | a to f |

### 7.8 What goes in the same chunks (D228)

- `appointed-not-configurable` on the supervisor, and its holder sentence naming *your rules alone* (row a for kinds, row c for the
  holder).
- `RoleCatalog.Appointed`, `StandingConfirmation.Staffed` and `StandingConfirmation.WorkspaceOf` (row a).
- The `defer` act and the bare `answer` act name (row a's migration step).
- `SUPERVISOR_ANSWER`, `SUPERVISOR_DEFER` and `SUPERVISOR_COURSE`, with `sessions supervise`'s `--point` and `--course` (row b).
- The `RoleHolders.Terminal` written at the keeping (row b).
- The area's rule 7, rules 41, 42, 46 and 48 sentences that name the terminal alone, each rewritten in place by the row that changes it;
  the Open list's SUPERVISOR1c line, at row c.

## 8. Rejected

- **Falling through to the next scope's supervisor when the one found cannot act.** Who decides would then depend on accounts' state
  at the moment; the person reads it from the catalogue alone (§3.4).
- **A role kind per decision kind** (`kind: allow-supervisor` beside `supervisor`). It copies the supervisor's whole shape for each
  kind; one shape with `decides` holds them all.
- **The policy's `works` reused for decision kinds.** A field would mean two things; a kind supervisor carries its own staffing.
- **A route per kind beside the three of today** (`SUPERVISOR_ALLOW`, …). That is five routes for one job (D228); one decide route and
  one leave route carry a `kind`.
- **The actor answering through a connector tool**, as a helper's `helper_answer` does. The supervisor design §3.5's contract stands:
  no tool names a session, and the result is read from the run alone.
- **A holder key for the actor.** Nothing of the actor crosses the pipe; the driver reads its own run.
- **The actor taking a place under the cap.** A full cap's running session would wait on its own allow (§2.3).
- **Grants left unchecked, as today.** A grant the judge never reads is a word on a screen; the direction is that a supervisor answers
  under its grants.
- **`defer` kept as a grant.** Leaving changes nothing of Daoris's controls, as the steward's desk decline does not.
- **The steward taking an agent holder too.** The direction keeps its terminal holder.
- **A supervisor answering the steward's act outside its grants.** One appointed role would widen another's grants (§5.2; §10's third
  question).
- **A supervisor's standing allows kept in a list of its own.** Two lists of allows would be two ways to read one fact (§5.1).

## 9. The rows, in build order

Rows d, e and f each follow b and need no actor: a terminal holder decides their kinds as soon as they land. Row g follows TREEPOOL1
§30's rows. Row h is last.

**SUPERVISOR2a — kinds and grants in the catalogue** (driver, modules, web). Changes: `decides`, the kinds' vocabulary and qualified
acts, the judge per kind and scope with the machine's generic supervisor, one resolver (`RoleCatalog.Supervising`) with the store's
wider places, the grants checked at the role method for today's question and course routes, the seed, the migration step, the verb's
`--decides`, the role card, and `ROLES`' `deciders`. The person sees the editor's *Decides* and its acts grouped by kind, *Who decides
here* on each place's Roles, *Add a supervisor for…*, and a terminal holder's ungranted act refused `not-granted` in the verb's words.
Proof: two supervisors of one kind in one scope refused, and a generic one beside one of a kind standing; the machine's generic one not
removable or given kinds; each of §3.4's six steps; a repository's supervisor held by rules keeping its kind the person's under an
agent-held workspace supervisor; the migration step's rewrite on a home with `answer` and `defer`; an ungranted answer and an ungranted
course go-on refused, a leave admitted with none; HoldFields' and AppointedRows' tests in both languages.

**SUPERVISOR2b — one decision door** (modules, driver). Changes: `SUPERVISOR_DECIDE` and `SUPERVISOR_LEAVE` with `kind`; the kinds'
rows `QuestionKind` and `CourseKind`, moved from `SupervisorDoor` and `SupervisorCourseDoor`; the admission by kind; the decision's
`kind` on its files; `SUPERVISOR_WAITING` by kind; the verb's `--do` and `--leave`; the three old routes and their flags removed. The
person sees nothing new on the window; the holder's verb takes its new words, and *Details* and the cards name each decision's kind.
Proof: every refusal the question and course routes answer today, ported onto the new routes; a decide naming a kind its role does not
decide refused `role-changed`; the pipe census with two routes in and three out; a decision file from before the row read as history.

**SUPERVISOR2c — the actor holder** (driver, service, modules, web). Changes: `holder: agent` for the supervisor with its staffing and
policy through the role door; the agent hold and its admission; the `supervise` work, its record, its call-table column and its start
rule; the attempt, the turn, the prompt from the shared composer, and the look's pass; the unavailable path; the suggested row; the
censuses of §7.7 for c; the editor's *An agent* with its choices, policy and week; *Used by*. The person sees a supervisor held by an
agent answering questions and courses at its places within its grants, each decision on its card with *Overrule* and the tier that ran,
its turn readable, and its week in the editor. Proof (a stub harness): answering, leaving, timing out, and answering late; an attempt
interrupted by a restart closed as failed and never started again; a limited end moving to the next choice; no choice ready falling to
the person, said, then answered once the account's cool-off ends; a reached budget passing its choice; a forged connector naming the
actor's session refused, and a pipe act naming an agent hold's generation refused; a result after a revoke, a role change
or a changed subject dropped; the person's word ending a running attempt; the record's actor fields and each decision's tokens.

**SUPERVISOR2d — limits and moves** (driver, modules, web). Changes: `refer` offered on `limits` and `moves` for a driven line (the judge
still refuses it on a conversation's line, `do`); the `LimitKind` row (`limit.move` as an approved move in `role_moves`, `limit.wait` as
the `wait` action makes it); failures on one choice moved from the course kind to the limit kind. The person sees *Ask the supervisor*
on the limits and moves counts in a place's rules fold for driven work, and the line's card saying the supervisor's move with
*Overrule*. Proof: a driven line limited under a `refer` rule moved by the supervisor, overruled inside the window, and left; a wait up
to the rule's bound; a conversation's `refer` on limits refused; `moves-unbounded` still judged.

**SUPERVISOR2e — allows** (driver, modules, service, web). Changes: the `AllowKind` row over held permission requests (released as
`SESSION_PERMISSION_ANSWER`'s allow-once or reject, by the roles' one route dispatch) and over `permission_propose` widenings (released
as the widening's yes, recorded `by` the role); the window bounded by the subject's own `Until`. The person sees the held permission's
card saying *the supervisor allowed this once* with *Overrule* until it goes on, and a widening allowed by a supervisor on the Agents
page with who allowed it and *Take back*. Proof: a held request allowed once by an agent supervisor, refused by one, overruled before
its release, and left; a window cut to the request's bound; a widening allowed standing and taken back by the person; an allow no
grant covers waiting for the person; the owner's-marks census.

**SUPERVISOR2f — landings** (driver, modules, web). Changes: the `LandingKind` row over a `review` step's Accept and a `human`
approval's *Approve* and *Reject*, pressed through each node's own press door and kept on the run's record `by: supervisor`; refused
`landing-pushes` where the landing's plan names a plugin that pushes, and on any run a go-ahead holds. The person sees the review step
saying *the supervisor accepted this; it lands at 14:10* with *Overrule*. Proof: a review accepted by the supervisor and landed after
the window; overruled inside it; a pushing landing refused; an approval approved and rejected; the node kinds' census.

**SUPERVISOR2g — the rule file's allows** (driver, web; with TREEPOOL1 §30's rows). Changes: the stages that meet the person's added
restriction raise `allow` subjects (§5.1); a standing allow written into the one allow list with `by`. The person sees the repository
page's waiting allow answered by its supervisor, and a standing allow saying who gave it, with *Take back*. Proof: a command a
person's pattern holds, allowed once at a take by the repository's allow supervisor and asked again at the next; allowed standing,
taken back, and asked again; no supervisor covering it, so the person's press decides.

**SUPERVISOR2h — seen on the install** (the parent with the install). An agent-held generic supervisor on the machine and an `allow`
supervisor on one repository: a question answered, an allow given and one overruled, a budget reached and falling to the person. The
spend is reported, run once at the least that proves it.

## 10. What is the owner's

1. **Should a go-ahead become a decision kind?** A go-ahead is the person's yes to an act outside a repository, such as a push, a
   publication or a production write (D135 point 2). *Recommended: no.* It stays the person's alone, and every kind refuses a subject a
   go-ahead holds, since what a go-ahead lets happen leaves the machine and cannot be overruled once released. *Yes* would add a
   `go-ahead` kind, off by default, answering `go_ahead_ask` and the workflow's `go-ahead` step under a grant; the bound in §2.7 and the
   handed text's *grants nothing* would be rewritten for it. See §2.7 and §3.1.
2. **Should SUPERVISOR1d's bench gate the actor holder?** The supervisor design required the bench, with zero wrong answers, before an
   actor was offered (§11 there). *Recommended: no.* The bench measures each choice and the role editor shows its last scorecard, but
   the person's choice of an agent holder is not held behind it, since restrictions are the person's to add (D214's principle note).
   *Yes* would refuse `holder: agent` with a choice no qualifying bench run names. See §2.1 and the supervisor design §11.
3. **Should an `allow` supervisor answer the steward's act outside its grants?** Today that act asks the person (`StewardAsk`).
   *Recommended: no.* The steward's grants are the person's boundary for another appointed role. *Yes* would send `StewardAsk`'s request
   to the place's `allow` supervisor first, under a new act `allow.steward`, off by default. See §5.2.
4. **Should an allow-once go on at once, with no overrule window?** An allowed call cannot be taken back once it runs, so the window is
   the person's only chance to overrule it, and it holds the agent's call while it lasts. *Recommended: keep the role's window* (1 to
   60 minutes, cut to the request's own bound). A person who wants allows fast gives an `allow` supervisor a one-minute window. *Yes*
   would release an allow-once as soon as it is kept, its card saying it went on with no *Overrule*. See §4.

## 11. Whether it is ready for both makers' reads

- **Rows a to f and h** are ready for both makers' reads. Each names its changes, its files at main and its proof, and none reopens an
  accepted choice but the one the direction reverses (rule 7's refusal for the supervisor).
- **Row g** is ready in its contract (§5.1); its build waits on TREEPOOL1 §30's rows, which should name it.
- **The owner's four questions** (§10) change rows c, e and f only where answered *yes*; the reads can proceed on the recommended
  answers.
