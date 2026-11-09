# SUPERVISOR2: supervisors staffed through the actor system, generic or of a kind

Design, proposed for the owner's yes. Round 1 was written by Claude Opus 5.5 on 2026-11-08 from main at `50055ee6d6`, on the owner's
direction in D233's note of 2026-11-08 and the principle in D214's note of the same day, and read by both makers
(`docs/2026-11-08-supervisor-kinds-reads.md`). **Round 2**, by Claude Opus 5.5 on 2026-11-08 from main at `bdab99e6f4`, answers both
reads and writes in D233's second note of 2026-11-08, which settles round 1's four questions. A passage round 2 changes is marked
*(R2-Cn)* for Codex's finding n or *(R2-On)* for the Opus reader's, and its section near the end answers each finding. **Round 3**,
by Claude Opus 5.5 on 2026-11-08 from main at `6b47d1f6ac`, answers both narrow reads of round 2 and writes in D233's third note of
2026-11-08, which keeps a landing's window: a passage it changes is marked *(R3-Cn)* for Codex's finding Bn or *(R3-On)* for the Opus
reader's finding n, and its section answers each. **Round 4**, by Claude Opus 5.5 on 2026-11-08 from main at `0e50a18388`,
answers both confirming reads of round 3, narrowly: a passage it changes is marked *(R4-C1)* for Codex's one finding or *(R4-On)*
for the Opus reader's finding n, and the last section answers each. Nothing here is accepted, and no decision records it yet. Read with
`docs/areas/roles-and-supervision.md` (cited *rule n*), the supervisor design (`docs/2026-10-24-supervisor-design.md`, D208) §3 to §6,
the context-counters design (`docs/2026-11-03-context-counters-design.md`, D229) §4 and §5, the staff-policy design
(`docs/2026-11-02-staff-policy-design.md`, D226), and TREEPOOL1's round 10
(`docs/2026-10-26-tree-pool-design.md` §30). Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with
`Modules/` (`src/Daoris.Desktop/Daoris.Desktop.Modules/`), `Service/` (`src/Daoris.Service/`) or `Web/` (`src/Daoris.Web/src/`), as
the area writes them. Line numbers are main's at `bdab99e6f4`, those round 3 adds main's at `6b47d1f6ac`, and those round 4 adds
main's at `0e50a18388`. Nothing was built or run.

## 0. In short

- **The holder.** The supervisor takes a third holder, `agent`. Its staffing is an order of one to four choices of agent, model and
  effort, set through the role door and walked as every role's order is (rules 11 to 20), under the staff policy's rule, weights, shares
  and budgets. A decision is one read-only turn of that actor; its answer is the turn's last message, kept by the same path as a
  terminal's. The agent's hold is the role's id and revision, read at every admission *(R2-O2)*, and what held its turn read-only is
  said on each decision (§2.8) *(R2-C8)*. The steward keeps its terminal holder, and `appointed-not-configurable` becomes the steward's
  refusal alone.
- **Kinds.** A decision kind is a set of decisions that wait for an allow or a choice: `question`, `course`, `limit`, `allow`, `files`
  and `landing` *(R2-C9)*. Each names its subject by a typed reference that does not change while it waits *(R2-C3)*. A supervisor's
  record names the kinds it `decides`. One that names none is its scope's generic supervisor and answers every decision no kind
  supervisor covering the place takes. A scope holds at most one generic supervisor and at most one supervisor of each kind. The machine
  always holds a generic one.
- **Resolution.** A decision is placed where its effect reaches *(R2-O3, R2-C4)*. It goes to the most specific supervisor of its kind
  whose scope covers that place, else the most specific generic one *(R2-O12)*. The catalogue alone decides, its grants included.
  Whether the supervisor found can act (its holder, its grants, its actor's availability) never sends the decision on to another: when it
  cannot act, the decision falls to the person's rules, which acted first, and then to the person.
- **Grants.** Each kind's acts are named in the role's `authority` (`question.answer`, `allow.once`, …), off on a new record as every
  role act is. One check, read by the admission by kind, place and act at every step from the decide to the release, so a grant taken
  away stops a held decision *(R2-O2, R2-C2)*. The release's migration converts the home once, and no supervisor record of the new shape
  is written before it, so no older build reads one *(R2-C1)*; its step keeps this build's version as the home's newest first, so the
  launcher refuses an older build before any record converts *(R3-C1)*. Leaving a decision to the person needs no grant. A decision is
  logged as the role's act and said on its subject's line and the supervisor's line.
- **When it acts** *(R2-O1)*. An allow (the `allow` and `files` kinds) takes effect as soon as it is kept, and the person takes a
  standing one back afterwards (D233's second note). A question's answer, a course, a move and a landing are held for the role's window
  with *Overrule* (D233's third note), and so is a steward's `land` a supervisor allows *(R3-O4)*; the person's own word supersedes
  them (rule 50).
- **Allows** *(R2-O1, R2-C4)*. The person's added restrictions that ask for an allow are an asking-mode permission request, a widening
  of the person's rules a session proposes, and an `ask` in the rule file's `permissions` (TREEPOOL1 §30.4). The steward's act outside
  its grants is one too: it goes to the place's allow supervisor first, under its own grant, off by default. Each allow is decided at the
  place its effect reaches, under the act its subtype takes. A widening that removes the person's deny, or switches off a default that
  denies, asks for no allow and stays the person's *(R3-O1)*, and so does one whose rule would let a publish run while `no-publish` is
  on, its forms listed in code *(R4-O2)*. Every writer of the rule file reads, judges and writes it under the file's own lock *(R4-C1)*.
  A review or an approval before a landing is the `landing` kind's, not an allow.
- **Rows a to h** (§9): kinds and grants in the catalogue; one decision door; the actor; limits and moves; allows, the steward's
  among them; landings; the rule file's allows with TREEPOOL1; the install's proof.

## 1. What is asked, and what is true today

### 1.1 The direction

The owner's direction of 2026-11-08 (D233's note) says that Daoris's structure holds what a person would otherwise allow by hand, and a
supervisor answers it under its grants. A place may have one generic supervisor or several supervisors of kinds, and the generic one
answers what no kind takes. A supervisor is configured through the actor system: its staffing chooses its agent, model and effort as any
role's is chosen. The note rejected an appointed role refusing an agent holder and actor choices (`appointed-not-configurable`) for the
supervisor. D214's principle note says Daoris is a development tool, so restrictions are the person's to add; where one asks for an
allow, the place's appointed supervisor gives it under its role's grants, and the person gives it only where no role covers it.

**D233's second note of the same day** *(R2-O1)* settles round 1's four questions (§10). A go-ahead stays the person's alone and is no
kind. The bench measures a supervisor's choices and gates no agent holder. The steward's act outside its grants goes to the allow
supervisor first, under its own grant, off by default, the person deciding where that grant is not held. An allow takes effect as soon
as it is kept, the person able to take a standing allow back afterwards.

**D233's third note of the same day** *(R3-O4)* settles round 2's one question (§10). A landing keeps the role's window and is no
allow: a supervisor's Accept or Approve is held for the window with *Overrule*, since a landed branch is not taken back as a standing
allow is.

### 1.2 Today

| Part | Today | Where |
|---|---|---|
| Holders | The supervisor is held by `rules` (the person's rules alone) or `terminal`; the steward by `terminal` or no one. `agent` reads as a record's shape and is refused `appointed-not-configurable`, and so is a staffing of choices on any appointed role | `Roles/RoleHold.cs:77-85`, `:152-155`; `Roles/RoleDoor.cs:210-213`, `:222-224` |
| A record's acts | The parser takes any non-empty `authority` strings; only the judge holds them to the vocabulary, at a write *(R2-C1)* | `Roles/RoleHold.cs:121-135`, `:147-150`; `Roles/RoleRecords.cs:124-128` |
| One per scope | Two appointed roles of one kind (supervisor, steward) never share a scope; the machine's supervisor may not be removed | `Roles/RoleCatalog.cs:317-321`, `:324-328` |
| What it decides | A parked session's recorded question (`SUPERVISOR_ANSWER`, `SUPERVISOR_DEFER`), and a `refer` point at answers alone, failures since *Try again* or failures on one choice (`SUPERVISOR_COURSE`), both listed by `SUPERVISOR_WAITING`. The forms name the session and the question or the point (`question`, `point`) | `Modules/Bridge/SupervisorDoor.cs:227-289`, `Modules/Bridge/SupervisorCourseDoor.cs:12-46`, `Modules/Bridge/Routes/SessionsRoutes.cs:249-274`, `Modules/Bridge/Routes/SupervisorCourseRoutes.cs:14`, `Roles/SupervisorProof.cs:153-173`, `Roles/SupervisorCourseForm.cs:6-23`; rules 46 to 48 |
| Grants | The supervisor's `authority` vocabulary is `answer` and `defer`, but no code reads it: the role method covers a supervisor's route without looking at `authority`. The steward's acts are checked against it, and one outside them asks the person (`StewardAsk`) | `Roles/RoleHold.cs:19`; `Modules/Platform/RoleJudge.cs:162-166`, `:168-171` |
| A held decision's release | Asks the admission for a generation alone; a role press ends only a hold whose holder or kind changed, and `daoris role set` writes from its own process, reaching no sweep *(R2-O2, R2-C2)* | `Roles/SupervisorReleasing.cs:29`; `Roles/SupervisorCourses.cs:105`; `Roles/SupervisorTickets.cs:27`; `Modules/Platform/RoleTerminals.cs:135-146`; `Roles/RoleVerbs.cs:62` |
| The effective supervisor | Two copies of *the most specific scope's role of a kind*: `RoleCatalog.Appointed`, which no production code calls, and `StandingConfirmation.Staffed`, which the admission and `StewardStaffed` read. The repository's workspace comes from the loop's registry, handed in as `WorkspaceOf` | `Roles/RoleCatalog.cs:229-230`; `Modules/Platform/StandingConfirmation.cs:176`, `:179-180`, `:219-223`; `Modules/Loop/LoopRun.cs:247` |
| Its admission | Only a started terminal admits. Held by rules, the admission answers `not-staffed`, and a `refer` point waits for the person, said `supervisor.unreferred` | `Modules/Platform/RoleJudge.cs:25-35`; rule 42 |
| Allows *(R2-C4, R2-C5)* | A held request takes one end, whoever answers first, and only the person's press allows. A widening is answered by `RuleProposals.Answer`, which opens, saves the rules and marks the proposal in three steps under no lock. `permission_propose` takes a scope of the machine, a workspace or a repository. *(R3-O1)* An `add allow` or `add ask` over a rule its scope denies, and a `remove` of a deny, class as widenings; a default's switch edits one machine-wide list whatever scope it names. *(R4-O2)* `no-publish` holds no rule, its sentence to the classifier alone. *(R4-C1)* Every writer of the rule file reads it, edits, and saves, the file's lock held over the save's re-read and rename alone, and a changed list is written whole | `Events/HeldPermissions.cs:81-82`, `:136-174`, `:216`; `Agents/RuleProposals.cs:119-142`, `:154`, `:220-231`; `Agents/Permissions.cs:186-190`, `:333-346`, `:399-436`; `Store/JsonKeeping.cs:111-116`, `:306-314`; `Service/Daoris.Service.Mcp/RuleProposalTools.cs:27-28` |
| A node's word *(R2-C6)* | `NodeWord(Node, Act, Words, At)` names no author; a pending word refuses a second (`worded`); the pass keeps it as `person-<act>`, its move the person's | `Workflows/Nodes/NodeRecord.cs:249`; `Workflows/Nodes/RunNodeWords.cs:113`, `:203`; `Workflows/Nodes/NodeRecordFiles.cs:519-532`; `Workflows/Kinds/Human/HumanNode.cs:54-60` |
| The steward's ungranted act *(R2-O1)* | Asks the person as its row's person's route, a card whose press waits with no expiry; one terminal may hold several roles, answered together | `Modules/Platform/StewardAsk.cs:20-23`; `Modules/Platform/PipeAdmission.cs:85-93`; `Modules/Platform/RoleTerminals.cs:36-45`; `Modules/Platform/RoleJudge.cs:160` |
| A restart *(R2-C7)* | The start's sweep reads a question's decision files alone and remembers a question closed to the owner; a course decision is history after it, and its point waits for a holder again | `Roles/SupervisorSweeps.cs:105-138`; `Roles/SupervisorCourses.cs:131-133` |
| The actor | Designed as SUPERVISOR1c (the supervisor design §3.5, §11): one read-only turn, ten minutes, one attempt per question, its result the run's last message. Parked in `docs/backlog-parked.md` behind SUPERVISOR1d's bench. The helper exemplar hands a harness with no rule language no rules, so its copy alone holds it *(R2-C8)*. *(R3-C3)* The reviewer's read-only shape keeps every other allow of the person's composition, an interpreter's among them, and the tree guard reads no program's own write. *(R4-O1)* A helper's Claude Code is handed its file beside the person's and the repository's settings, and Daoris's connector beside every other server | `Loop/Session/SessionRules.cs:39-41`; `Participants/Domain/OpinionPosture.cs`; `Review/Domain/OpinionPermissions.cs:111`; `Workplace/TreeGuard.cs:14-21`; `Agents/Providers/ClaudeCode/ClaudeCodeSpawn.cs:87-113` |
| The home's newest build *(R3-C1)* | Kept by the updater once a start is confirmed, a write that fails left to the next start; the migration runs once the swap lets the home go, reading no such record | `Modules/Update/InstallUpdater.cs:190-198`; `Install/HomeLastBuild.cs:38-45`; `Transitional/Migration/HomeMigration.cs:98-103` |

This design reverses rule 7's refusal for the supervisor. What rules 41 to 52 hold for a terminal holder holds for each kind and for
the actor, except where §2 to §5 say otherwise. SUPERVISOR1c is this design's row c; the parked SUPERVISOR1 row's 1c and 1d are folded
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
- **Its bench** *(R2-O1)*. SUPERVISOR1d's bench measures each choice, and the editor shows each choice's last scorecard; it gates no
  holder (D233's second note).
- **Suggested choices** gain the supervisor's dated row, judgement work as Design's is (`docs/model-roles.md`), applied only by the
  person's press (rule 21).

### 2.2 Its hold, and a restart

A terminal's hold is drawn when the window starts its terminal (rule 42). **An agent's hold needs no press**: the role's record, saved
by the person through the role door, is the appointment. *(R2-O2)* **Its generation is the role's id and revision**
(`agent:<role>@<revision>`), read from the catalogue at every admission, never drawn or kept in memory. It is never in the terminals'
`Holds`, so a pipe act carrying one finds no terminal's hold and is refused as an unknown generation is
(`Modules/Platform/RoleJudge.cs:135-141`). Any write of the role from any process (the role door in the window, or `daoris role set` in
its own, `Roles/RoleVerbs.cs:62`) moves the revision, so the decisions kept under the old one go stale at their release
(`role-changed`) with no sweep.

The admission (`IDecisionAdmission`, `Roles/SupervisorDecision.cs:8-12`) answers, for a decision's kind, place and act (§4), the
effective supervisor's generation and window: the started terminal's hold where the holder is `terminal`, the agent's generation where
it is `agent`, `not-staffed` where it is `rules`, and `not-granted` where its grants do not cover the act.

**A restart** *(R2-C7, R2-O10)*. No decision survives a restart as live (rule 49; the supervisor design §3.4), and none comes back to a
supervisor. The start's sweep (`SupervisorSweeps.SweepAtStart`) reads every kind's decision files and attempts, not a question's alone.
It closes a held or released decision stale (`restarted`) and an admitted one interrupted, as it does a question's today, and closes an
attempt with no outcome failed. *(R3-O2, R3-O3)* Before it closes one, it reads the door of each kind whose release outlives the
process: a widening's proposal, its `settling` recovered first (§5.3); a `files` allow in the person's layer; a node's word. A decision
whose door holds its release (the proposal settled by it, the allow written `by` it, the word applied) is recorded made, and only one
whose door does not is closed. A held request and a steward's request live in this process alone (`Events/HeldPermissions.cs:77-78`,
`Modules/Platform/PipeState.cs:19-40`), so a restart ends them and their decisions close as every kind's do, the pipe's journal
saying whether an allowed steward's act had begun (`Modules/Platform/PipeJournal.cs:27-30`). It then remembers as closed every
subject whose decision is final to the person (overruled, superseded, stale, left, interrupted) or whose attempts ended. A closed
subject is the person's: no list hands it to a supervisor again, of any holder. So an attempt that answered, whose held decision a
restart dropped, makes its subject the person's, as a question's is today; no choice is spent on it again, and no restart makes a
supervisor reconsider what it decided. A subject with no decision and no attempt
yet is still the supervisor's.

**What is durable.** A decision's file is written atomically before it is live, as the keeping writes it today
(`Roles/SupervisorKeeping.cs:57-61`, `Roles/SupervisorCourses.cs:83`), and an attempt's before its start; a write that fails keeps
nothing and starts nothing. A state line written after it (`SupervisorDecisionFiles.TryAppend`, best effort, `:45-54`) that fails leaves
the decision read as held at the next start, which closes it as the person's, so the person's overrule and a leave hold across a
restart whether or not their line was written.

### 2.3 How a decision is made: the attempt

The supervisor design §3.5 fixed the contract. This design keeps it and gives it the walk.

1. **When.** The look's `supervise` pass (`Loop/Look/SuperviseLook.cs`, after `course-release`) reads every subject waiting at a place
   (the kinds' lists, §3.1). It takes those whose effective supervisor is agent-held, whose grants cover at least one act of the
   subject's kind (§4), which no closed subject's mark names (§2.2) *(R2-C7)*, and on which no decision is held and no attempt runs. It
   starts at most one attempt per line at a time, and at most one per choice for one subject. One point is referred once (the
   context-counters design §4.6 point 3): a course or limit point waits while a question decision on its line is open.
2. **The attempt.** An attempt `{ id, subject, choice, started }` is written in the subject's decisions folder before the start, and its
   outcome (decided, left, failed, limited) beside it at its end *(R2-C7)*. One found with no outcome past its bound is closed as
   failed. The turn has a helper's shape (`Loop/Helpers/HelperTurn.cs`, the exemplar): a read-only copy of the subject's repository at
   the subject's commit with its work in progress laid over it (`OpinionTree`, `HelperCapture`), and for a subject that names no
   repository (a machine or workspace widening, a steward's act on a target that names none) an empty folder made for the attempt
   under the home's own scratch *(R2-C8)*. Either is removed however the turn ends. It runs one turn held read-only as §2.8 says. Its record is marked `decides` with
   the attempt's id, and `serves` with the subject's session where it has one, so its work reads `supervise` (§7.6). The start is bound
   to its role and the role's revision (rule 8) and keeps `actor`, `choice`, `asked`, `ran` and `chosen` as every start does (rule 20).
   It takes no place under the cap. It decides for work already under the cap and waiting on it, and holding a slot would leave a full
   cap's running session waiting on its own allow. §2.5 bounds its spend instead.
3. **Its tools.** The connector serves a `supervise` session the knowledge and reading tools alone (`knowledge_search`,
   `knowledge_get`, `knowledge_repositories`, `knowledge_convergence`, `line_read`, `handback_read`, `memory_get`, `memory_search`,
   `quest_list`, `registry`). It gets no say, no answer, no act and no door that names a session. It asks for no co-worker (rule 54). A
   permission request it makes is refused (agents area rule 23), and no decision about a `supervise` session is any supervisor's.
4. **Its result** is the turn's last message, holding one fenced block in the decision door's form less its generation and proof
   (§7.5), its subject the typed reference §3.1 names *(R2-C3)*:

   ```text
   <<< supervisor decision
   { "kind": "question", "subject": { "session": "s-1a2b3c4d", "question": "q-7" }, "do": "answer", "choice": "Keep the old name",
     "reasons": [ { "says": "The rename is the owner's to choose.", "source": "docs/decisions/D201.md point 3" } ] }
   >>>
   ```

   A leave reads `{ "kind", "subject", "leave": "<bound>", "reasons" }`. The driver reads the block from that run alone, binds it to the
   attempt's generation, and keeps it through the kind's own keeping (§3.1). The admission, with the act's grant (§4), and the subject
   are read again at the keeping, as they are for a terminal's act. A block from another session, a late one after a revoke, a changed
   role or a changed subject, a subject other than the attempt's, or a second one for one attempt, is dropped and logged
   `supervisor.dropped`.
5. **One attempt per choice.** A failed end, a timeout, a block that does not read, or a block whose kind or subject is not the
   attempt's ends the subject's attempts. The subject is then the person's, and its card says why (`supervisor.attempt-failed`). A limited
   or transient end is a pass of the walk and not a failure (rule 33): the next attempt takes the walk's next choice at once, or waits
   for the walk's soonest way out. A leave is a decision (`left`) and is final. An attempt is spent on its choice once it has an outcome;
   a restart closes its subject (§2.2) rather than counting it *(R2-O10)*.
6. **The person first.** The person's word on the subject at any time ends a running attempt (the driver stops its own turn) and drops
   its result.

### 2.4 What it is handed, and what it answers with

The prompt (`Roles/SupervisorInstruction.cs`, composed as `HelperInstruction.Compose` is) hands what the terminal's print hands (the
supervisor design §3.2; the context-counters design §5). One composer serves both the prompt and `sessions supervise <id>`'s print, so
the actor and a terminal holder read the same facts:

- the subject's typed reference *(R2-C3)*, and the subject's own words, as the asking agent's claims, fenced under a nonce (the
  supervisor design §4.4);
- the go-aheads on its ask;
- the work order and the owner's own words on the ask, quoted with their dates;
- the standing answers;
- the line's course, its counts and the rules in force, as Daoris's facts;
- for an allow, the call, the widening, the steward's act or the ask, the restriction that asked, and the place its effect reaches
  (§5.3);
- for a landing, the step, the attempt, the branch's commits and the review's state;
- the offered acts, marked by whether its grants cover them.

It answers as a terminal does: one act its grants cover, or a leave with a bound, and one to five reasons each naming its source
(`no-source`); a `count:` source names a count it was handed. The decision keeps its holder as `{ kind: "agent", role, revision, agent,
model, effort, ran, posture, session }`, so its card says the tier that actually ran and what held it read-only (§2.8) *(R2-C8)*. A
terminal's holder keeps today's `{ kind: "terminal", parent }`.

### 2.5 Its bound and spend

- **Per turn:** ten minutes, or the subject's own bound less one minute where that comes first (a held permission request's `Until`;
  *(R3-O5)* a steward's request's ten minutes from its raise, §5.2, so a later choice's turn ends by it too).
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

The supervisor design's bounds on a question (§4.1 there; rule 47) stand for every holder: one asking for a go-ahead, a sign-in,
spending or a deletion, a question the repository says to ask the person, a preference, an unrecorded or contradicting question, and one
that tries to direct it; they bound a free question, not the steward's `delete`, which §5.2 places *(R3-O4)*. So do the structural
refusals: a question the agent marked the owner's, one past the answers-alone bound, and one on a line a rule holds (`rule-holds`).
*(R2-O9)* The `allow` kind takes only the structured allows §5.3 names, a widening among them under `allow.standing` at its rule's reach
but never one that removes the person's deny or switches off a default that denies *(R3-O1)*, nor one whose rule would let a publish
run while `no-publish` is on *(R4-O2)*, and changes none of these for a free
question, so a question marked `access` stays the person's. The text a session is handed for a supervisor's answer still grants
nothing (`SupervisorHanded.Text`). *(R2-O1)* A go-ahead is no kind and stays the person's
alone (D233's second note; §10).

### 2.8 What holds the turn read-only *(R2-C8, R2-O5)*

The supervisor design §3.5 asked for one read-only turn. What holds it is said per harness, as a reviewer's opinion says it
(`Participants/Domain/OpinionPosture.cs`), and every decision carries it (§2.4). It is the turn's own containment, the contract a
grant rests on, and restricts nothing a person's development does.

- **Its place.** The subject's read-only copy, or the attempt's own empty folder (§2.3 point 2); never the person's checkout, the home,
  or another repository. *(R3-O8)* The tree guard is handed to the turn whatever the person's defaults say, as a partner's is
  (`Loop/Session/SessionRules.cs:91-92`).
- **`rules-and-copy`**, on a harness that takes Daoris's rules (Claude Code, by either door). The reviewer's read-only shape
  (`Review/Domain/OpinionPermissions.cs:100-117`) takes away the edit tools, git's writes and the acting connector tools, but keeps
  every other allow of the person's composition (`:111`), and the copy's own settings may allow more. *(R3-C3)* So the supervise shape
  also denies the harness's shell tools whole (`Bash` and `PowerShell`, as the tree guard's matcher names them,
  `Agents/Domain/TreeGuardHook.cs:10`): no command runs, whatever an allow names (the person's `Bash(python:*)`, a widening's, the
  copy's), so no program of the turn's writes the home's permissions or the catalogue, which the tree guard cannot read from a command
  (`Workplace/TreeGuard.cs:14-21`). The harness enforces a deny whatever the model reads (deny wins, agents area rule 18). The turn
  reads through the harness's read tools and the connector's reading tools, and is handed what it decides on (§2.4), so it is given
  none of the reviewer's git reading allows. The install's role, rules, permission and files verbs are commands, so they are denied
  with the shell, and round 2's denies of them are not written. The hard-deny sentence handed to the classifier names a role's grants.
  *(R4-O3)* `MultiEdit` joins the reviewer's denied writes (`Review/Domain/OpinionPermissions.cs:65-70`): they name `Edit`, `Write`
  and `NotebookEdit` though their sentence says every tool that writes a file, and the tree guard's matcher names `MultiEdit` too
  (`Agents/Domain/TreeGuardHook.cs:7`). The reviewer's and the helper's shapes deny it with the supervise shape.
- *(R4-O1)* **Its sources, on Claude Code.** Spawned as a helper is, the turn would be handed its file beside the person's and the
  copy's settings (`--settings`, merged with both, `Agents/Providers/ClaudeCode/ClaudeCodeSpawn.cs:105-113`) and Daoris's connector
  beside every other server (`--mcp-config`, the repository's `.mcp.json` still governing its own, `:87-95`). So a write tool of a
  server the person allows (`mcp__fs__*`) passes the shape, which keeps every other allow, and no deny or guard names it; and a hook
  the asking session leaves in its uncommitted `.claude/settings.json`, laid over the copy, runs as the turn starts, outside every
  rule. The turn therefore starts as the holder's terminal does (`Modules/Platform/HolderTerminal.cs:46-48`): the user tier and the
  handed file alone (`--setting-sources user`), and Daoris's connector alone (`--strict-mcp-config` beside `--mcp-config`, whose
  plan a helper's start already draws from that connector alone, `Agents/ConnectorPlans.cs:12-13`). On the protocol door the same
  is `settingSources: ["user"]` and `strictMcpConfig: true` beside `settings` in `_meta.claudeCode.options`, which the adapter
  spreads over its own list (`docs/2026-10-01-entry-point-evidence.md` step 1; carried and measured with `["project"]`,
  `docs/2026-10-03-steer-evidence.md` §6). The copy's settings, allows, hooks, servers and instruction files, and the
  person's other servers, are then not the turn's: the tree guard and the lineage hook still come in the handed file, the account's
  user tier stays as every session has it, and the turn reads the copy's own `CLAUDE.md` as a file, if at all, never loaded as its
  instructions (the project tier gates it, `docs/2026-10-01-entry-point-evidence.md` step 5).
- **The reading posture and its place**, on a harness that takes no rules but has a reading posture (Codex's `read-only`,
  `Agents/Providers/Codex/CodexProvider.cs:38`). Its posture is said `copy-alone` until row h measures whether that posture stops a write
  outside its place; then it is said by what was measured. Codex holds no denies, and is said to (D233's choice 3).
- **`copy-alone`** on any other harness, said so.
- *(R3-O8)* **On every harness**, the role verb's writes are refused from a process in an agents' job, its own job read as the pipe
  reads a role act's caller (`Modules/Platform/RoleJudge.cs:116-117`), so a turn held by its copy alone cannot run `daoris role set`
  in its own process to widen its own grants. A program of the turn's own writing the store directly is what `copy-alone` says.

A decision takes effect only through its kind's keeping, which reads the grants (§4), so whatever a turn does in its place, its
decision is bounded by them. The containment keeps the turn from reaching Daoris's own controls on a harness that takes Daoris's rules.
A person may choose an agent whose harness holds its turn by its copy alone: the editor says so beside the choice (§6.1), and nothing
refuses it.

## 3. Kinds

### 3.1 The decision kinds

A kind is a set of decisions that each wait somewhere for an allow or a choice. Each has its own list, its own offered acts, its own
keeping, and its own release through the door the person's press goes through. The kinds are a closed vocabulary of the build, as the
works are (rule 2), and the catalogue declares which ones a supervisor takes (§3.2).

| Kind | Its decisions, and where each waits | Its acts (grants) | Takes effect *(R2-O1)* | Leaving | Released through |
|---|---|---|---|---|---|
| `question` | A parked session's recorded question with offered choices (`QuestionFiles`) | `question.answer`: one offered label | after the window | `leave` with a bound (today's defer) | The supervisor's word on the record, as today (rules 46, 49) |
| `course` | A `refer` point at answers alone or at failures since *Try again* (`ReferredPoints`) | `course.go-on` | after the window | `hold`: the person's at once | The count's own door: the mark's go-on, or the quest's *Try again* (`CourseReleaseLook`, `QuestRetry.Decide`) |
| `limit` | A `refer` point on a driven line at failures on one choice, at a limited end, or at the moves bound (row d offers `refer` on the last two) | `limit.move`: one move to another choice; `limit.wait`: wait for the choice's reset, at most the rule's bound | after the window | `hold` | An `ask`'s release: an approved row of `role_moves` (`Roles/StaffMoves.cs`), as *Go on now with {next}* writes it; a wait as the `wait` action makes it |
| `allow` | A permission request held in the asking mode (`Events/HeldPermissions.cs`); a widening a session proposed (`permission_propose`, agents area rule 20); the steward's act outside its grants (§5.2) *(R2-O1)* | by subtype (§5.3) *(R2-C4)*: `allow.once` and `allow.reject` on a held request; `allow.standing` and `allow.reject` on a widening; `allow.steward` on a steward's act | at its keeping | `leave` | Each subtype's own door, settled once (§5.3) |
| `files` *(R2-C9)* | An `ask` in the person's `permissions` of the rule file holding a `run(…)`, `copy(…)` or `share(…)` at a start (TREEPOOL1 §30.4; §5.1) | `files.allow-run`, `files.allow-copy`, `files.allow-share` | at its keeping | `leave` | An `allow` in the deciding place's layer of the person's `permissions` in `data/files.json`, `by` the role (§5.1) *(R3-O6)* |
| `landing` | A `review` step waiting for the person's Accept; a `human` approval waiting for *Approve* or *Reject* (`Workflows/Kinds/Review/ReviewNode.cs`, `Workflows/Kinds/Human/HumanNode.cs`) | `landing.accept`: Accept, with no changes and no words; `landing.approve`; `landing.reject` | after the window | `leave` | The node's word, carrying its author (§3.6) |

Each count belongs to one kind: `course` takes answers alone and failures, and `limit` takes failures on one choice, limits and moves.
Tries and the quiet end take no `refer`, as rule 32 holds today. A `human` task (a manual operation the person does) and a `go-ahead`
step belong to no kind. A landing whose plan names a plugin that pushes is refused `landing-pushes`, as the steward's `land` is (rule
51), and a run a go-ahead holds is never any supervisor's.

**When each acts** *(R2-O1)*. D233's second note settles that an allow takes effect as soon as it is kept. An `allow` or `files`
decision is released at its keeping, with no window and no *Overrule*; a standing one is taken back afterwards (*Take back*), and a
once-only one has acted. The other four kinds keep the role's window (rule 49). A question's answer, a course's go-on, a move and a
landing are decisions on work, which D233's first note names apart from an allow, and none of them is taken back as a standing allow is
once it acts, so the window is the person's one chance to overrule it. *(R3-O4)* D233's third note keeps a landing's window (§10), and
a steward's `land` a supervisor allows waits the window too (§5.2), since it lands a tree on its line.

**The subject** *(R2-C3)*. Each decision's subject is named by a typed reference that does not change while it waits. Every form,
attempt, decision file, keeping and release carries it whole, and the proof is over it, so a word about one subject is never read as
another's:

| Kind | Its subject |
|---|---|
| `question` | `{ session, question }`: the recorded question's id, as `SUPERVISOR_ANSWER`'s `question` names it today |
| `course`, `limit` | `{ session, point }`: the referred point's id, as `SUPERVISOR_COURSE`'s `point` names it today |
| `allow`, a held request | `{ session, request, held }`: the request's id on its wire and the hold's own moment (`HeldPermission.Since`), so a request id a later process reuses is another subject |
| `allow`, a widening | `{ proposal }`: the proposal's id; its change is written once and never edited |
| `allow`, a steward's act | `{ ask }`: the id the judge draws for the steward's proved request when it asks (§5.2) |
| `files` | `{ repository, start, what, text }`: the start that met the ask, `run`, `copy` or `share`, and the held act's own text (`run(npm ci)`), never the `ask` rule's pattern *(R3-O6)* |
| `landing` | `{ run, node, attempt }`: the attempt the step waits on |

### 3.2 Declared in the catalogue

A supervisor's record gains `decides`: one or more kinds it answers, written beside `authority`. A record with `decides` absent or empty
is its scope's **generic supervisor**, and the seed's `supervisor` is the machine's generic one. A person adds a supervisor of a kind as
any role is added (rule 1): an id, a name, `kind: supervisor`, its `decides`, its scope, its holder, its staffing where an agent holds
it, its acts and its window. `decides` names only kinds of the vocabulary, and `authority` only the acts of the kinds the role decides
(all kinds for a generic one); either refused `bad-record`. *(R2-O8)* The verb names the kind it makes: `--decides` makes a supervisor,
as `--acts` naming a supervisor's acts does, so a new supervisor with nothing ticked is set as the editor makes it (today the kind comes
from the acts alone, `Roles/RoleVerbs.cs:383`).

### 3.3 One of a kind per scope

- A scope holds **at most one generic supervisor** and **at most one supervisor deciding each kind**. Two that share a kind in one
  scope are refused `two-roles-one-work`, its sentence naming the kind and the other role. A supervisor deciding `allow` and `landing`
  can stand beside one deciding `question` in the same repository.
- **The machine always holds a generic supervisor.** It holds the person's rules (rule 7), so removing it, scoping it away or giving it
  kinds is refused `may-not-be-off`.
- The steward's rules are unchanged: one steward, on the machine.

### 3.4 Which supervisor answers

*(R2-O3, R2-C4)* **A decision is placed where its effect reaches**: the machine, a workspace, or a repository with its workspace, as
each subject's row of §5.3 and §5.1 says, and never the place of the session that asked. A repository's workspace is the one the rules
read in every process (`RuleWiderPlaceStore`, rule 33), and the judge's own `WorkspaceOf` hand-in goes.

*(R2-O12)* For a decision of kind *k* at a place, the supervisors whose scope covers it are read **kind first**:

1. the repository's supervisor deciding *k*; else
2. the workspace's supervisor deciding *k*; else
3. the machine's supervisor deciding *k*; else
4. the repository's generic supervisor; else
5. the workspace's generic supervisor; else
6. the machine's generic supervisor, which always stands.

A step whose scope does not cover the place is passed: a workspace's place reads steps 2, 3, 5 and 6, and the machine's steps 3 and 6. A
supervisor of a kind answers that kind everywhere its scope covers, and a generic one answers what no kind takes there, as D233's first
note says. Round 1 read the repository's generic supervisor before the workspace's supervisor of a kind, so a person adding a generic
supervisor to answer one repository's questions took that repository's allows from the workspace's allow supervisor.

One resolver answers this, `RoleCatalog.Supervising(kind, place)`. It replaces both of today's copies (§1.2), and the admission, the
lists, the release and the window's *Who decides here* (§6.2) all read it.

**The answer is structural.** The supervisor found answers, under its grants, or the person does. If it is held by rules, if its grants
do not cover the act, or if its actor is unavailable, the decision is the person's, never another supervisor's. A grant left unticked is
the person's word for that place, so it does not pass the decision to a wider supervisor: a person keeps a place's allows theirs by a
supervisor there deciding `allow`, held by rules or with none of its acts ticked. *Who decides here* names each such gap (*Allows: you
(Gatekeeper may not allow)*), so a person reads who decides from the catalogue alone. A decision kept under one supervisor goes stale at
its release where another is effective now (`role-changed`, as rule 48's release reads the admission again).

*(R2-O6)* **An unreadable supervisor holds its place for the person.** The resolver holds as `RoleFor` does (rule 6;
`Roles/RoleCatalog.cs:171-181`): an unreadable appointed record whose scope reads and covers the place makes every decision there the
person's, said with its error, since its `decides` cannot be read; one whose scope does not read makes every supervisor's decision the
person's. Today's `HoldingActs` keeps only records that read (`:223`), so the resolver would pass an unreadable one by. *(R3-O7)* So
does a workspace map that does not read. `RuleWiderPlaceStore.Read` answers `None` for a store that fails to read as for one before its
step (`Roles/RuleWiderPlaceStore.cs:26-37`), which would skip a repository's workspace and the supervisors there; it tells the two
apart, and on a store that failed the resolver makes every decision placed below the machine the person's, said with its error.

### 3.5 The rules first

The person's supervisor rules stay the place's, whoever answers (rules 31 to 40). They are read first at every point with no model, and
any act other than `refer` stands. `refer` hands the point to the supervisor of the point's kind (`course` or `limit`) by §3.4. Where
none can act, the point holds as `hold` does, said `supervisor.unreferred`, as today.

### 3.6 A landing's word *(R2-C6)*

A landing is released as a node's word, which today names no author, lets a pending word refuse the next, and is kept as the person's
(§1.2). The landing kind's release changes that word, under the record's lock, the runtime its one writer (workflows area rule 18):

- **It carries its author.** `NodeWord` gains `by` (`person`, or `{ role, decision }`) and the attempt it was pressed on. Both are
  written, read and applied whole: a supervisor's word is kept as `supervisor-<act>`, and its move's cause and source are the
  supervisor's, never the person's.
- **It is the person's to replace.** A person's word replaces a supervisor's word the pass has not applied, and the decision moves to
  `superseded`. A supervisor's word is refused `worded` where any word is pending.
- **It binds its attempt.** A word applied on an attempt other than the one it names is dropped, and its decision moves to `stale`.
- **It binds its run** *(R3-O3)*. The word waits on the node record, a file a restart keeps, while no decision survives a restart as
  live (§2.2). So the pass drops a supervisor's word whose decision is not live in this run, its decision `stale`, and the step waits
  for the person. A word the pass applied before the restart is recorded made by the start's sweep, which reads the node's word first.
- **Its cutoff** is the pass that applies it. Once applied, the person's later press is their own act on the run (a review's changes,
  a rejection), never an overrule.

No older build reads such a word: it exists only under a supervisor the role door writes after this release's migration (§4).

## 4. Grants

- **Named per act.** A supervisor's `authority` lists acts as `<kind>.<act>`: `question.answer`; `course.go-on`; `limit.move`,
  `limit.wait`; `allow.once`, `allow.reject`, `allow.standing`, `allow.steward` *(R2-O1, R2-C4)*; `files.allow-run`,
  `files.allow-copy`, `files.allow-share` *(R2-C9)*; `landing.accept`, `landing.approve`, `landing.reject`. The steward's acts stay as
  they are.
- **Off on a new record**, as every role act is: the editor and the verb tick none. The seed's machine supervisor lists
  `question.answer`, `course.go-on` and `limit.move`, the three decisions a terminal holder makes today with no grant check, so a home
  behaves as before and a person who gives it an agent sees it answer what a terminal would. Every other act is off.
- **The migration, and what no older build reads** *(R2-C1)*. This release's migration (`HomeMigration`, install area rule 51) carries
  one step. It rewrites every stored supervisor record, in `roles` and every revision of it in `role_revisions`: `answer` becomes
  `question.answer` with `course.go-on` and `limit.move` beside it, and `defer` goes, since leaving needs no grant. It gives every
  decision file its `kind` (`question` where it names a question, else `course`, the two a decision had before this release), so no
  reader of a file without one is kept (D232). An older build would read the new shape wrongly: its parser takes any non-empty
  `authority` (`Roles/RoleHold.cs:121-135`), ignores `decides`, and its judge covers the supervisor's routes with no grant read. So
  the boundary is one existing builds obey:
  - an update's migration runs only once the update has confirmed (install area rule 51), and from then the launcher refuses any build
    below the last confirmed one (rule 46), so no older build starts on a converted home;
  - *(R3-C1)* the step makes that refusal hold before it converts anything. The updater's own keep of the home's newest build may fail
    and wait for the next start (`Modules/Update/InstallUpdater.cs:190-198`) while the migration still runs once the swap lets the home
    go (`Transitional/Migration/HomeMigration.cs:98-103`). So the step first keeps this build's version through the one keeper
    (`HomeLastBuild.Keep(home, UpdateRefusal.Running)`, `Install/HomeLastBuild.cs:38-45`, `Install/UpdateRefusal.cs:14`), the record
    the launcher refuses an older build by, and reads it back, going on only where the version read is at least this build's. A write
    or a read-back that fails throws, so the step is not recorded (`HomeMigration.Run` records a step once it returns,
    `Transitional/Migration/HomeMigration.cs:76-92`), the role door stays `not-converted`, and the next start runs it again;
  - the role door writes no supervisor record before this release's step is recorded in the home's migration record, refusing it
    `not-converted` (*This home is converted once the update confirms; set the supervisor then.*), so nothing of the new shape stands
    in a home an older build may still be rolled back onto. Before the step, the new build reads `answer` as no act of its vocabulary,
    which covers nothing, so a terminal's answer in those minutes is refused `not-granted` and waits for the person. The refusal goes
    with the step, in the next release that changes a stored shape (D232 point 2);
  - a record the step did not reach, such as a queued role proposal carrying `answer`, is refused `bad-record` by the judge, never
    misread.
- **One check, at every step** *(R2-O2, R2-C2)*. `SupervisorGrants.Covers(role, kind, act)` (`Roles/SupervisorGrants.cs`) is read by the
  admission, which answers by kind, place and act (`Admit(kind, place, act)`): the effective supervisor (§3.4), its holder's generation,
  and `not-granted` where that supervisor's grants no longer cover the act. Every step that asks the admission reads it: the judge at a
  terminal's decide (`Modules/Platform/RoleJudge.cs`, which covers a supervisor's route with no check at `:162-166` today), the actor's
  keeping, the release (`Roles/SupervisorReleasing.cs:29`, `Roles/SupervisorCourses.cs:105`) and the tickets
  (`Roles/SupervisorTickets.cs:27`). So a grant taken away while a decision is held stops its release (`stale`, `not-granted`) from
  whatever process wrote the catalogue, with no sweep and no change to `RoleTerminals.RolesChanged`. The cutoff is the catalogue read in
  the release's own step under the decision's lock: a grant removed before it stops the decision, and one removed after finds the act
  made, as an act made before its grant was taken back always is. An act outside the grants is refused `not-granted` and keeps nothing.
  The kinds' lists (`SUPERVISOR_WAITING`, the actor's prompt) name the acts the grants cover, and no actor is started on a subject whose
  kind no grant covers (§2.3).
- **Leaving needs none.** Any supervisor may leave a decision to the person with a bound and its reasons, as the steward's desk decline
  needs no grant.
- **Logged as the role's act**: `role.act` with `role` the role's id, `kind: supervisor`, the decision kind, the act and its outcome,
  beside the decision's own `supervisor.*` events. It is **said on its line** (§6.4). *(R2-O1)* A question, course, limit or landing
  decision is held for the role's window, 1 to 60 minutes, with *Overrule*. An allow is released at its keeping (§3.1), but for a
  steward's `land`, which waits the window (§5.2) *(R3-O4)*.
- **The person's own words supersede** (rule 50). The person's press on the subject's own door while a decision is held supersedes it
  (`superseded`). An allow has no held time: the person's press that settles a subject first stands, and a supervisor never settles over
  it (§5.3). Once a decision is released, the person's later press is their own act on what it did, such as taking back a standing allow.
- **The person's holds on a line** *(R2-O13)*. A supervisor's release does not read `owner-set` (rule 52): that mark makes a whole quest
  the person's after any press of theirs on it, a question answered among them, and would take every later decision on it from its
  supervisor, while a supervisor answers only what waits on its subject. The course, limit and landing kinds' releases read the person's
  own holds on the line instead: a line the person stopped, or a quest paused, is refused `owner-held`, the stop and the pause
  `StewardTargets.OwnerHeldAsync` reads (`Modules/Bridge/StewardTargets.cs:29`), since a go-on, a move or a landing would release what
  only the person releases. The hold the point itself is (a failures park, a moves bound) is the one the kind releases.

## 5. Allows, the rule file's, and the steward's acts

### 5.1 The rule file's allows *(R2-C9)*

TREEPOOL1's round 10 (`docs/2026-10-26-tree-pool-design.md` §30, on main) settles the rule file's allows, and this design takes them as
written there (§30.4):

- **The `files` kind.** An `ask` in the person's `permissions` (their layer of `data/files.json`) on `run(…)`, `copy(…)` or `share(…)`
  that no `allow` matches holds that act at a start. The act is not made at that start, said (`started.files-waiting`), the session
  starting as after any failed step (§29.7 there), and the allow is asked for the next. That ask is a `files` subject (§3.1), kept in the
  home and listed by the kinds' list.
- **Its grants and places**, as §30.4's table names them, in §4's form: `files.allow-run` and `files.allow-copy` at the repository;
  `files.allow-share` at the narrowest scope covering both repositories, their workspace, else the machine. §30.4's `allow-run`,
  `allow-copy` and `allow-share` are these acts, and its *the place's supervisor of the `files` kind, or the generic one* is §3.4's
  resolution.
- **Standing, at once, in the one list.** A `files` allow is written into the person's `allow`, carrying `by: { role, decision }`, and
  holds until the person takes it back; *Take back* removes it as it removes the person's own, and an allow is never kept in a second
  place. *(R3-O6)* It goes into the deciding place's own layer of the person's `permissions`, which are the machine's and each
  repository's, unioned, with no workspace layer (§30.4): the repository's for `run` and `copy`, whichever supervisor answered by
  §3.4, and for a share each of the two repositories' layers. It is never written into the machine's layer, whose allow reaches every
  repository, even where the `ask` that held the act is the machine's. It is the held act's own text (`run(npm ci)`), never the `ask`
  rule's pattern (`run(npm *)`): `allow` is read before `ask` (§30.4), so an allow of the pattern would lift the whole restriction.
  §30.4's *may be a pattern* is the person's own allow, theirs to write. It acts at its keeping (§3.1) and is read by
  the next stage that reads the rules, at any later start. *(R4-C1)* The person's file is edited under no lock today
  (`PersonFiles.Edit`, `Workplace/Files/PersonFiles.cs:106-127`), so the allow is written as §5.3's one change of the rules is: read,
  judged and written under the file's own lock, every writer of it the same, so neither the person's write nor the supervisor's
  loses what the other made between. There is no once-only `files` allow, so nothing is consumed: the start that
  met the ask went on without it, as §30.4 says, and every later start reads the standing allow.
- **The person decides** where §30.4 says: no supervisor for the place can act, its `authority` lacks the act, or the ask names the
  person (`run(…)@person`).
- **The person's restrictions only.** What the analysis finds and what a repository's own rules name act as found (D214's principle
  note); a supervisor decides only where the person has restricted, and no grant reaches the floor (§30.1 there).

Row g builds this with §30's rows 6a, 3 and 6b, as §30.5 there says (*a supervisor's allow lands with the supervisor kinds' rows; until
then every `ask` is the person's*).

*Built choices, small and reversible:* a held `copy` (a machine-local input the gates read, `TreeCopyPermit`) says `started.local-held`, and a denied one `started.local-denied`, as a held share says `started.share-declined`, since `started.files-waiting` counts the installs alone. A leave of an allow or a held act is the allow decision's own record (act `leave`, state `left`, the bound and reasons), kept in the machine's folder where its subject names no session, as a held act names a repository. The listing carries each allow's and held act's facts so the terminal's print is what an agent's prompt holds.

### 5.2 The steward's acts *(R2-O1, R2-O4, R2-C4)*

The steward and the supervisor do different work: the steward makes the management acts its grants list (rule 51), and a supervisor
answers what waits. They meet in three places.

- **The steward never answers a decision.** No kind is the steward's, and its holder stays a terminal.
- **A restriction meets the steward as it meets the person.** A steward's act runs its person's route inside the role's door
  (`StewardRouteAct`). Where that route meets a restriction asking for an allow (a review waiting for Accept, an approval, an
  asking-mode request), the route refuses or waits as it would for the person's press, and the subject waits for its supervisor or the
  person. The supervisor's releases go through each subject's own door, which settles it once (§5.3), and the owner's marks pass both
  by.
- **The steward's act outside its grants goes to the allow supervisor first** (D233's second note). Where the judge answers
  `StewardAsk.Outside` today (`Modules/Platform/RoleJudge.cs:171`), an act that asks (its row names a person's route and its form is
  exact) raises an `allow` subject `{ ask }` for the act's exact proved form instead. *(R3-O4)* It is placed at the act's target's
  place, the target read as `SupervisorDoor.ActAsync` reads it: a session's resolved to its quest first
  (`Modules/Bridge/SupervisorDoor.cs:110-132`), then the quest's repository (`DependenciesAsync`, `:194-217`), and that repository's
  workspace as §3.4 reads every place's. The machine is the place only for a target whose kind names no repository (a desk request,
  `:196-197`). A target that does not resolve (a session that names no quest) or a dependency that does not read (`DependenciesAsync`
  answering null) goes to the person's card at once, never to the machine's supervisor.
  *(R3-O5)* The steward's request waits on the pipe as **one pending entry** under the pipe's gate (`PipeState.Gate`, beside `Running`
  and `Reserved`, `Modules/Platform/PipeState.cs:19-40`), settled once by the first of: the supervisor's allow; its leave, or the
  subject falling to the person (ungranted, unavailable); the steward's withdrawal of its request (`PipeTakeBacks`); and ten minutes
  counted on the request from its raise. Only that settlement runs the act as the steward's (`pipe.RunAsync`,
  `Modules/Platform/PipeAdmission.cs:160`) or reserves the person's card (`AskThenRunAsync`, `:188`), each outside the gate as its order
  holds (`PipeState.cs:15-17`), so the act runs once. The ten minutes close the subject for the supervisor before the card is
  reserved: a later answer, a terminal's or an attempt's on a later choice, is dropped (`supervisor.dropped`).
  - **Allowed** under `allow.steward`, off on every record, the seed's included: the act is admitted as the steward's own
    (`state.Acts.Admit`, `role.act` with `by: steward` naming the allowing decision), the owner-set check (`RoleJudge.cs:177`) read
    first as for a granted act, and it runs as a granted act does. It is never sent on as the person's press (`StewardAsk.Press`),
    which would make a supervisor's word the person's own. *(R3-O4)* A `land` lands a tree on its line, and D233's third note holds a
    landing for the window since a landed branch is not taken back: allowed, it waits the allowing supervisor's window with
    *Overrule* before it runs, and overruled, the person's card follows. *(R4-O4)* Through the window it stays the pipe's one pending
    entry, the allow having ended its ten minutes, and is settled once more under the gate by the first of: the window's end, which
    runs it; the person's *Overrule*; and the steward's withdrawal of its request (`WITHDRAW` or the kit's `CANCEL`,
    `Modules/Platform/PipeTakeBacks.cs:27-65`, `:136-203`), which drops it as an overrule does but with no card, the request answered
    `withdrawn` and the decision `stale`.
  - **Left, ungranted, unavailable, or undecided within ten minutes of its raise** *(R3-O5)*: the request's own clock, whichever holder
    holds the supervisor, never a turn's, since an attempt starts only at the look's `supervise` pass and a limited end takes the next
    choice (§2.3). The person's card follows, exactly as today, saying the supervisor's outcome in one sentence.
  - *(R3-O4)* **A deletion among them.** §2.7's *a deletion* bounds a free question (rule 47). The steward's `delete` (`QUEST_DELETE`)
    is one of its acts, so outside its grants it is an `allow.steward` subject as its other acts are.
  - **Once alone.** `allow.steward` allows this request and nothing after it. A standing allow of a steward's act would be a grant of the
    steward's, and the steward's grants live in its record, written by the role door alone (D228).
  - **Not the acts that never ask.** An act refused `not-granted` today with no card (the update acts, which have no person's route;
    the hand-over, whose route's judge refuses it before any card, rule 51) stays refused: the person is not asked either.
  - **Never its own holder's** *(R2-O4)*. One terminal may hold several roles (`Modules/Platform/RoleTerminals.cs:36-45`), answered
    together (`RoleJudge.cs:160`). A steward's act is not listed for, and an allow of it is refused `not-granted` from, a hold whose
    holder holds the steward too; the card says why and the person's card follows. An agent-held allow supervisor shares no holder with
    the steward, whose holder is a terminal.

A supervisor's release and a steward's act on one quest meet at the quest's own doors, as any two presses do. The course's *Try again*
and the steward's `retry` both reach `QuestRetry.Decide`, and the later one finds the point settled (`answered`, stale).

### 5.3 Each allow: its place, its acts and its settlement *(R2-C4, R2-C5, R2-O3)*

| Subtype | Its place (§3.4) | The acts that cover it | Settled by |
|---|---|---|---|
| A held request | the session's repository | `allow.once`: the agent's offered one-time allow; `allow.reject`: its offered one-time reject. A standing option is refused, as the door refuses it for the person (`Events/HeldPermissions.cs:152-158`) | the door's one end (`HeldPermissions.Answer`, `:136`; `End`, `:216`): whoever answers first, the other `unheld` |
| A widening | its rule's reach: the machine for a machine rule, that workspace for a workspace's, that repository for a repository's; the machine for a default's switch, whatever scope it names *(R3-O1)* | `allow.standing`: the yes, the rule written; `allow.reject`: the no | one conditional settlement (below) |
| A steward's act (§5.2) | the act's target's place, read as `ActAsync` reads it *(R3-O4)* | `allow.steward`: this request alone | the pending entry's one settlement under the pipe's gate (§5.2) *(R3-O5)* |
| A `files` ask (§5.1) | the repository, or for a share the narrowest scope covering both | `files.allow-run`, `files.allow-copy`, `files.allow-share` | the rule file's one write into the deciding place's layer (§5.1) *(R3-O6)* |

A widening is never `allow.once`: its yes writes a rule that holds until taken back, so only `allow.standing` covers it. A held request is
never `allow.standing`. A narrowing applies at the next look as today and asks no one. So a session in one repository that proposes a
machine rule is decided by the machine's supervisor, and a repository's allow supervisor never acts on the machine.

**The person's own restrictions stay theirs** *(R3-O1)*. A widening that removes or moves a `deny` of its scope (an `add allow` or
`add ask` over a rule that scope denies, or a `remove` of one, `Agents/RuleProposals.cs:119-142`), or switches off a default that
denies (`no-publish`, `tree-guard`, `Agents/Permissions.cs:186-196`), is no `allow` subject: it waits for the person, as every widening
does today. A deny asks for no allow: D233's first note and D214's principle note give a supervisor only the allow an added
restriction asks for. A default's switch is placed at the machine whatever scope the proposal names, since `SwitchDefault` edits one
machine-wide list (`Agents/Permissions.cs:419-436`; `RuleProposals.cs:154`); switching an allowing default back on is the machine's
supervisor's under `allow.standing`.

*(R4-O2)* **A publish stays the person's too.** `no-publish` denies by its sentence alone: it holds no rule
(`Agents/Permissions.cs:186-190`), so `Compose` hands nothing for it, and `add allow Bash(npm publish:*)` for repository X is a plain
widening (`RuleProposals.cs:127`), whose explicit allow passes the publish at the harness in either mode, the classifier never asked.
So `no-publish`'s forms are listed in code beside its sentence, as `no-push`'s four are (`Permissions.cs:180-181`): each publishing
program's publish and a release's creation, in the plain and option-first shapes `no-push`'s rules take (`Bash(npm publish:*)`,
`Bash(npm -* publish)`, `Bash(npm -* publish *)`, and so for `pnpm`, `yarn`, `cargo`, `dotnet nuget push`, `twine upload` and
`gh release create`), held by a test. While `no-publish` is on, a widening whose rule would match a command one of them matches, read
as the harness reads both (`:*` a prefix, `*` any text, a shell tool's rule with no specifier every command, so `Bash(npm:*)` and
`Bash(npm *)` among them), is no `allow` subject and waits for the person. The forms class widenings alone and are never handed as
denies, so a session's handed rules are as today and the person's own allow of a publish stays theirs. A go-ahead stays the person's
(D233's second note): `no-push`'s denies and git access's doors already hold a push whatever an allow says
(`docs/areas/git-and-remotes.md` rules 25 and 27), and the tree guard's hook outranks any allow of an edit, so `no-publish` is the
one denying default a widening's rule could undo. What no rule names (a program that publishes when an interpreter's allow runs it)
no rule of either default holds today, and this design changes neither.

**The widening's one settlement** *(R2-C5)*. `RuleProposals.Answer` (`Agents/RuleProposals.cs:220-231`) opens, saves and marks in three
steps under no lock, so a person's decline and a supervisor's yes can both succeed, and a crash between the save and the mark leaves an
applied proposal reading unanswered. It becomes one settlement, `RuleProposals.Settle(home, id, by, yes, receipt)`, the person's press,
their verb and the supervisor's keeping all calling it. Under an exclusive lock on the proposal's file (the person's verb runs in its own
process as often as the window does), it reads the proposal's state and refuses one already settled (`answered`). *(R4-C1)* Then, as
one change of the rules (below), it reads the rules, judges the change against them as they stand, writes `settling` with its `by`
and receipt, and saves the rules; once the rule file's lock is let go, it marks the proposal settled. A yes the rules already say is
marked settled with no save, as `Unchanged` reads it. A supervisor's yes that an exclusion above now holds, read again (a deny of
its scope the person added since, or `no-publish` switched back on), writes nothing and returns the proposal to the person
(`waiting`), the supervisor's decision closed as the person's. *(R3-C2, R3-O2)* **A proposal found `settling`** (a stop between the
save and the mark) **is never saved again**, since the save is idempotent against itself but not against an edit between:
the person's `daoris agent rules remove` acts alone while no Daoris runs (`Agents/AgentsCommand.cs:11-13`), and saving again would
undo it, or drop a deny that replaced it (`PermissionRules.Add`, `Agents/Permissions.cs:399-416`). Whoever finds it, under the same
lock (the tick, the person's press or verb, the start's sweep), reads the rules as they stand and marks it settled by its `by` where
they already say its change (`Effect` answers `Nothing`, as the tick's `Unchanged` reads it, `Agents/RuleProposals.cs:114`, `:124`,
`:138`), else returns it to the person (`waiting`) and closes the supervisor's decision as the person's. A rules file that does not
read leaves it `settling` for the next tick, as the tick leaves every proposal today (`:174-181`). So a person's Take back or deny
between the save and the recovery stands, nothing reapplies a save that may have run, and no receipt need be kept with the rules. A
person's press that finds it settled by a supervisor says so, with *Take back*, their own act on the rules.

*(R4-C1)* **One change of the rules.** Every writer of the rule file reads it, decides its edit and saves it, and the save holds the
file's lock over its own re-read and rename alone (`Agents/Permissions.cs:333-346`, `Store/JsonKeeping.cs:111-116`), a changed list
written whole, never merged as a set (`:306-314`). So a settlement that reads `allow: [A]` and `ask: [B]`, computes `allow: [A, B]`,
and saves after the person's removal of A has landed writes A back, with no restart or recovery involved. Every writer therefore
goes through one change, `PermissionRules.Change(home, edit)`, as every writer of `harnesses.json` goes through
`HarnessSettingsChange.Change` (`Agents/HarnessSettingsChange.cs:31-56`, whose `read` seam lets a test's other writer wait on the
lock): the file's lock taken, the rules read, the edit decided from them as they stand (it may refuse, writing nothing), and the edit
saved holding the same lock (`JsonKeeping.SaveHolding`, `Store/JsonKeeping.cs:121`), never taking it again; a lock held past its
wait refuses the change, nothing written, as `KeepLock` refuses any (`Store/KeepLock.cs:61-73`). Its writers are the person's rule
door (`Modules/Bridge/Routes/RulesRoutes.cs:32-57`), their verb (`Agents/AgentRulesVerb.cs:62-67`, `:117-120`), the settlement above,
the tick's narrowing, which reads and saves per proposal (`Agents/RuleProposals.cs:170-198`) and so writes back an allow the person
removed when it moves a rule out of `allow`, and the setups' allows (`Setup/WorkspaceSetup.cs:157-173`,
`Setup/SetupPress.cs:251-263`). A proposal's lock is taken before the rule file's, never inside it, and the person's door takes the
rule file's alone. So the person's removal of A lands wholly before the settlement's read, which then computes `allow: [B]`, or wholly
after its save, which it then edits; either way A stays removed. The `settling` recovery reads the rules and saves nothing, so it
needs no change of them.

**No dispatch arbitrates.** The roles' route dispatch (`StewardRouteAct.DispatchAsync`) only calls a route. Each kind settles at its
subject's own door, which takes one settlement whoever presses first: the held request's end, the widening's settlement, the count's
door under the decision's lock, the node's word under the record's lock (§3.6), and the question's path, whose release and the person's
replacement are already serialized (`Service/Daoris.Service.Core/Sessions/SessionSays.cs:245-286`,
`Service/Daoris.Service.Core/Sessions/SaidKeeping.cs:65-74`).

## 6. What the person sees

### 6.1 The role editor (Agents → Setup → This machine → All roles, and a place's Roles)

A supervisor's editor (`Web/agents/roles/HoldFields.tsx`) shows, in order:

- **Decides:** *Every decision no other supervisor here takes* (generic), or the kinds ticked: *Questions*, *A line's course*,
  *Limits and moves to another choice*, *Allows*, *The rule file's allows* *(R2-C9)*, *Landings*. The machine's generic supervisor shows
  this fixed and says why.
- **Held by:** *Your rules, then you* · *A terminal* · *An agent*. Choosing *An agent* shows the choices table a working role has
  (agent, model and effort, one to four rows, with up and down), *How it chooses*, and the limits fold with its budgets (no works
  column), plus the suggested choices card with the supervisor's dated row. Beside each choice: its bench's last scorecard, which gates
  nothing *(R2-O1)*, and what holds its turn read-only, *by its agent's rules and its copy* or *by its copy alone* (§2.8) *(R2-C8)*.
- **What it may decide:** each kind it decides as a group of its acts, every one unticked on a new role, and under them, once: *It may
  always leave a decision to you.* The allow kinds' group says *These take effect at once* *(R2-O1)*.
- **Overrule window**, as today, said as applying to questions, courses, moves and landings, a steward's `land` it allows among them
  *(R3-O4)*.
- Under the form, **its week** (rule 62): decisions made, left to you and overruled, and the tokens its turns spent, by choice.

The judge's refusal is said in place from its closed code, as for every role press. The page judges nothing.

### 6.2 A place's Roles: who decides here

A place's appointed rows (`Web/agents/setup/AppointedRows.tsx`) list every supervisor placed there. Each line says what it decides, who
holds it (an agent's line names its first choice: *Claude Code · opus · high*), its acts and its window. Under them, **Who decides
here** gives one line per kind from `ROLES`' resolution for this place (§3.4), naming the supervisor and its scope, or *you*, with why:
*Allows: Gatekeeper (this workspace) · The rule file's allows, questions and a line's course: Supervisor (this machine) · Landings: you
(Gatekeeper may not accept).* *(R2-O12)* *Add a supervisor for…* offers the kinds no supervisor here decides, and opens the editor with
that kind, this scope and nothing ticked.

### 6.3 The Agents place

- **All roles** lists every supervisor once with its kinds. The diagram (`Web/agents/roles/StructureCanvas.tsx`, `roleStructure.ts`)
  draws a supervisor node per role at its scope, edged to the kinds it decides, dashed where the person's rules hold it.
- **An agent's page** (*Used by*, `Web/agents/AgentPage.tsx`) names each supervisor whose choices use that agent, at its place in the
  order.
- **The call table**, read-only on the page (`Web/agents/roles/callTable.ts`), shows the `supervise` column.

### 6.4 The decision on its subject

- **On the subject's own surface**, a held decision shows the time it goes on and *Overrule*; a final one its state's words; a left one
  *left to you*, with the bound and reasons. A question shows on the head card, as today; a course or limit decision on the course card
  (`Web/work/head/SupervisorCourseCard.tsx`); an allow on the held permission's card, the widening's row, the steward's line or the
  repository page's allow; a landing on the review step's card or the approval's.
- **Every decision names its supervisor and how it was held** *(R2-O1)*: *Gatekeeper (an agent: Claude Code · sonnet · medium) allowed
  this once at 14:01.* A standing allow: *Gatekeeper allowed `Bash(npm run build:*)` for this repository until you take it back.* with
  *Take back*. A steward's act: *Delete: allowed once by Gatekeeper at 14:03.* A steward's `land`: *Land: allowed by Gatekeeper; it
  lands at 14:10.* with *Overrule* *(R3-O4)*. A landing: *Gatekeeper accepted this; it lands at 14:10.*
  with *Overrule*. The supervisor's turn opens from there, read-only, as a helper's does, saying what held it read-only.
- **The supervisor's line** in *Notifications* (`Web/notifications/SupervisorLine.tsx`, beside `StewardLine.tsx`) lists its decisions
  this run, newest first, each with *Overrule* while held and *Take back* on a standing allow.
- **What needs you**: a subject a decision holds is not a wait, as the supervisor design §4.2 has it for a question, and neither is a
  steward's act waiting on the allow supervisor. A subject left to the person, refused by the grants, or waiting on an unavailable actor
  is a wait, and says why in one sentence.

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
| `files` *(R2-C9)* | the rule file's allows | 文件规则的允许 | |
| `landing` | landings | 落地 | the glossary's 落地 |
| `question.answer` | answer a question | 回答问题 | as today |
| `course.go-on` | let a line go on | 让这条线继续 | |
| `limit.move` | move a line to another choice | 让这条线改用其他选择 | |
| `limit.wait` | wait for a limit's reset | 等额度恢复 | |
| `allow.once` | allow a call once | 允许这一次调用 | |
| `allow.standing` | allow a widening until you take it back | 允许放宽规则，直到你收回 | *(R2-C4)* |
| `allow.reject` | refuse a call or a widening | 拒绝这次调用或放宽 | |
| `allow.steward` *(R2-O1)* | allow the steward's act once | 允许管家的这一次操作 | the glossary's 管家 |
| `files.allow-run` | allow a command | 允许运行命令 | |
| `files.allow-copy` | allow a local copy | 允许复制本地文件 | |
| `files.allow-share` | allow a dependency shared across repositories | 允许跨仓库共用依赖 | never the glossary's *share* (占比) |
| `landing.accept` | accept work to land | 采纳工作以落地 | the glossary's 采纳 |
| `landing.approve` | approve | 批准 | |
| `landing.reject` | reject | 驳回 | |
| leaving | leave it to you | 交给你处理 | today's course word |
| takes effect at once | these take effect at once | 这些即刻生效 | *(R2-O1)* |
| Who decides here | Who decides here | 这里由谁决定 | |

Both languages' catalogues and the glossary gain these words in the rows that show them.

## 7. What changes in the code

Test paths are under `src/Daoris.Desktop/` (`Daoris.Desktop.Driver.Tests/`, `Daoris.Desktop.Modules.Tests/`) and
`src/Daoris.Service/Daoris.Service.Tests/`.

### 7.1 The catalogue and its judge

| Unit | File | Change | Row |
|---|---|---|---|
| `AppointedRoles`, `RoleHolders`, `RoleHold` | `Roles/RoleHold.cs` | The kinds' vocabulary and their acts (`SupervisorKinds`, replacing `SupervisorActs` at `:19`), `files` and `allow.steward` among them; `RoleHold.Decides`, read and written as `decides`; `KindOf` reading the qualified acts *(R2-O8)*; the judge refusing `agent` for the steward alone (`:152-155`) and holding `decides` and `authority` to the vocabulary | a; `agent` accepted at c |
| `RoleCatalog` | `Roles/RoleCatalog.cs` | One generic supervisor and one per kind per scope (`:317-321`); the machine keeps a generic one (`:324-328`); `Supervising(kind, place)` in place of `Appointed` (`:229-230`), kind first, at a place of any rank, holding an unreadable slot's scope for the person *(R2-O3, R2-O6, R2-O12)*, and every place below the machine on a workspace map that did not read *(R3-O7)*; `Named` admitting an agent-held supervisor for the `supervise` work alone (`:208`) | a, c |
| `RoleDoor.Judge`, `RoleCatalog.Codes` | `Roles/RoleDoor.cs`, `Roles/RoleCatalog.cs:243-244` | A supervisor's record refused `not-converted` before this release's step is recorded *(R2-C1)* | a |
| The seed | `Defaults/roles.json` | The supervisor's `authority`: `question.answer`, `course.go-on`, `limit.move` | a |
| The grants | `Roles/SupervisorGrants.cs` (new) | `Covers(role, kind, act)`, the one check | a |
| `RuleWiderPlaceStore.Read` *(R3-O7)* | `Roles/RuleWiderPlaceStore.cs:26-37` | Telling a store that does not read from one before its step, so the resolver holds the first for the person | a |
| Migration *(R2-C1, R2-O9)* | `Transitional/Migration/SupervisorStep.cs` (new), one entry of `HomeMigration.Steps`; `Install/HomeLastBuild.cs` | *(R3-C1)* First this build's version kept as the home's newest build and read back, the step throwing where either fails; then stored supervisor records in `roles` and every supervisor revision in `role_revisions`: `answer` rewritten to `question.answer` with `course.go-on` and `limit.move`, `defer` dropped; every decision file given its `kind`; each file and row copied to the salvage first (D232 point 2); the old names read nowhere after | a |
| `RoleVerb` | `Roles/RoleVerbs.cs` (`:21`, `:364-384`) | `--decides <kind,…>` (or `every`) making a supervisor *(R2-O8)*, `--holder agent`, and for an agent-held supervisor the order's words a working role's set takes; every write refused from a process in an agents' job *(R3-O8)* | a, c |
| The page's vocabulary *(R2-O9)* | `Web/agents/roles/appointedHold.ts:6-14` | `ACTS` and `HOLDERS` follow the driver's: the qualified acts by kind, and `agent` for the supervisor | a, c |
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
| `StandingConfirmation.Staffed`, `WorkspaceOf`, `StewardStaffed` | `Modules/Platform/StandingConfirmation.cs:176`, `:179-180`, `:219-223` | `Staffed` and `WorkspaceOf` gone: the admission reads `RoleCatalog.Supervising` and the store's wider places; `StewardStaffed` reads the catalogue's one steward, so the desk's filing (`:195`) still reads it *(R2-O7)* | a |
| `RoleJudge.Admit`, `Covers`, `Supervising` | `Modules/Platform/RoleJudge.cs:25-58` | `Admit(kind, place, act)`, reading `SupervisorGrants.Covers`; for an agent-held supervisor, its role-and-revision generation *(R2-O2)* | a, c |
| `RoleJudge.Judged` | `Modules/Platform/RoleJudge.cs:102-183` | A supervisor's decision covered only where `SupervisorGrants.Covers` allows it (none checked at `:162-166` today); a steward's act outside its grants that asks raises its `allow` subject before the person's card, and one whose holder holds the allow supervisor too is refused there *(R2-O1, R2-O4)* | a; e |
| `StandingState` | `Modules/Platform/StandingState.cs` | Unchanged: an agent's generation is never in `Holds`, and nothing of it is kept in memory *(R2-O2)* | — |
| The releases | `Roles/SupervisorReleasing.cs:29`, `Roles/SupervisorCourses.cs:105`, `Roles/SupervisorTickets.cs:27` | Each asks the admission with the decision's kind, place and act *(R2-O2, R2-C2)* | a |
| `IDecisionAdmission`, `SupervisorHolder` | `Roles/SupervisorDecision.cs:8-33`, `:148` | `Admit(kind, place, act)`; the holder gains the agent's role, revision, choice, tier, posture and session, a terminal's holder unchanged | b, c |
| The person's card after the allow | `Modules/Platform/PipeAdmission.cs:85-93`, `:160`, `:188`, `Modules/Platform/PipeState.cs`, `Modules/Platform/PipeTakeBacks.cs`, `Modules/Platform/StewardAsk.cs` | The card is asked once the steward's `allow` subject falls to the person; an allowed act is admitted as the steward's, never pressed as the person's *(R2-O1)*; the request one pending entry under `PipeState.Gate`, settled by the first of an allow, a leave, the withdrawal and ten minutes from the raise, before either runs *(R3-O5)*; an allowed `land` held for the allowing supervisor's window *(R3-O4)*, still the pending entry there, settled by the first of the window's end, an overrule and the steward's withdrawal, which drops it with no card *(R4-O4)* | e |
| The steward's act's place *(R3-O4)* | `Modules/Bridge/SupervisorDoor.cs:110-132`, `:194-217` | The target's resolution of `ActAsync` read for the `allow` subject's place, a session's to its quest first; a target that does not resolve, or a dependency that does not read, the person's card | e |

### 7.4 `RoleActs` and the kinds' rows

| Unit | File | Change | Row |
|---|---|---|---|
| `StewardActs.TableOf` | `Modules/Platform/RoleActs.cs:79-81` | Unchanged for the steward. The supervisor's table stays empty: its decisions are the kinds' rows below, not `ROLE_ACT` acts | — |
| The kinds' rows | `Modules/Bridge/SupervisorKinds/` (new): `ISupervisorKind`; `QuestionKind`, from `SupervisorDoor.AnswerAsync`, `DeferAsync` and `WaitingAsync`; `CourseKind`, from `SupervisorCourseDoor`; `LimitKind`; `AllowKind` with its three subtypes; `FilesKind`; `LandingKind` | Each kind one unit, registered once as a steward's act is a row: its list of waiting subjects by their typed references, its offered acts, its keeping against the subject as it stands, when it takes effect, and its release through its subject's own door | b; d, e, f, g |
| The roles' route dispatch | `Modules/Bridge/StewardRouteAct.cs` | `DispatchAsync` made the one dispatch by which a role makes a person's press inside the role's door, for the steward's route acts and the supervisor's releases alike; it settles nothing, each subject's door does *(R2-C5)* | e |
| The widening's settlement *(R2-C5)* | `Agents/RuleProposals.cs:220-231`, `:355-362` | `Settle(home, id, by, yes, receipt)` in place of `Answer`: under the proposal file's lock, conditional, `settling` with its receipt; a proposal found `settling` never saved again, marked settled where `Effect` finds its change standing, else returned to the person, by whichever caller finds it *(R3-C2, R3-O2)*; the change judged and saved as one change of the rules, returned to the person where their restrictions now hold it *(R4-C1)* | e |
| The widening's class and place *(R3-O1)* | `Agents/RuleProposals.cs:102-147`, `Agents/Permissions.cs:186-190`, `Modules/Bridge/SupervisorKinds/` (`AllowKind`) | A widening that removes or moves a deny of its scope, or switches off `no-publish` or `tree-guard`, never an `allow` subject; a default's switch placed at the machine; *(R4-O2)* `no-publish`'s forms listed beside its sentence, read by the class alone, and a widening whose rule would match one while it is on never an `allow` subject | e |
| One change of the rules *(R4-C1)* | `Agents/Permissions.cs:333-346`, `Store/JsonKeeping.cs:111-121`, after `Agents/HarnessSettingsChange.cs:31-56`; its writers `Modules/Bridge/Routes/RulesRoutes.cs:32-57`, `Agents/AgentRulesVerb.cs:62-67`, `:117-120`, `Agents/RuleProposals.cs:170-198`, `:220-231`, `Setup/WorkspaceSetup.cs:157-173`, `Setup/SetupPress.cs:251-263` | `PermissionRules.Change(home, edit)`: the rule file's lock, the read, the edit decided from it, the save holding the lock; every writer through it | e |
| The held request's door *(R2-O9)* | `Events/HeldPermissions.cs:81-82`, `:136-174` | Answered by the allow kind at the same door and its one end; the remark that only the person's press allows rewritten to the person's or the place's supervisor's under its grant | e |
| A node's word *(R2-C6)* | `Workflows/Nodes/NodeRecord.cs:249`, `Workflows/Nodes/RunNodeWords.cs:96-127`, `:158-207`, `Workflows/Nodes/NodeRecordFiles.cs:519-534`, `Workflows/Kinds/Human/HumanNode.cs:54-60`, `Workflows/Kinds/Review/ReviewNode.cs:45-46` | `NodeWord`'s `by` and attempt written, read and applied; a person's word replacing a supervisor's pending one; a supervisor's move's cause and source its own; a supervisor's word whose decision is not live in this run dropped at the pass *(R3-O3)* (§3.6) | f |
| The person's holds on a line *(R2-O13)* | `Modules/Bridge/StewardTargets.cs:29` | The person's stop and the pause read through one unit, by the steward's hand-over and the course, limit and landing releases | d, f |

### 7.5 The supervisor's door, routes and verb

| Unit | File | Change | Row |
|---|---|---|---|
| Routes | `Modules/Bridge/Routes/SessionsRoutes.cs:249-274`, `Modules/Bridge/Routes/SupervisorCourseRoutes.cs:14`, `Roles/SupervisorProof.cs:134-150` | `SUPERVISOR_DECIDE { kind, subject, generation, proof, do, choice?, reasons }` and `SUPERVISOR_LEAVE { kind, subject, generation, proof, bound, reasons }`, `subject` the typed reference of §3.1 *(R2-C3)*, both class `Role`, in place of `SUPERVISOR_ANSWER`, `SUPERVISOR_DEFER` and `SUPERVISOR_COURSE`, which are removed in the same chunk; `SUPERVISOR_WAITING` lists every kind's subjects with the acts the grants cover | b |
| Forms | `Roles/SupervisorProof.cs` (`SupervisorForm`, `SupervisorDeferForm`), `Roles/SupervisorCourseForm.cs` | One decide form and one leave form, each proved over its canonical bytes, the typed subject whole among them *(R2-C3)* | b |
| The holder's verb | `Roles/SessionsSuperviseCommand.cs`, `Roles/SupervisePointPrint.cs` | `sessions supervise <subject> --do <act> [--choice <label>] --reason …` and `--leave <bound>`; `--point` and `--course` go; one print composer shared with the actor's prompt | b, c |
| The decisions | `Roles/QuestionSupervisorDecisions.cs`, `Roles/SupervisorCourses.cs`, `Roles/SupervisorDecisionFiles.cs` | A decision keeps its `kind` and its subject. The course's lifecycle (held, went on, overruled, stale, left) serves `limit` and `landing`; an allow's is kept and released at once. The migration gives every earlier file its `kind`, so no reader of a file without one is kept *(R2-O9; D232)* | b |
| The sweep *(R2-C7)* | `Roles/SupervisorSweeps.cs:105-138`, `Roles/SupervisorBook.cs:72` | Every kind's decision files and the attempts read at a start; every subject final to the person remembered as closed, by kind and subject; each durable door read before a decision is closed, and one its door holds recorded made *(R3-O2, R3-O3)* | b, c; each door at e, f, g |
| The door | `Modules/Bridge/SupervisorDoor.cs` (`:237`, `:260`) | The keeping's holder taken from the hold, no longer written as `RoleHolders.Terminal` | b |

### 7.6 The actor's run

| Unit | File | Change | Row |
|---|---|---|---|
| The look's pass | `Loop/Look/SuperviseLook.cs` (new), `[LookPass("supervise", LookPhase.Planned, "course-release")]` | §2.3 point 1 | c |
| The turn | `Loop/Supervise/SuperviseTurn.cs` (new), mirroring `Loop/Helpers/HelperTurn.cs` | §2.3 points 2 to 6; the attempt's own empty folder for a subject with no repository *(R2-C8)* | c |
| The attempts | `Roles/SupervisorAttempts.cs` (new) | An attempt written atomically before its start and its outcome at its end; closed at a start or past its bound; read by the sweep *(R2-C7)* | c |
| The prompt | `Roles/SupervisorInstruction.cs` (new) | §2.4 | c |
| The start | `Roles/StartPlan.cs:273-285` | `RoleOf("supervise")` is the supervisor; the plan walks the order of the supervisor the start is bound to | c |
| The work | `Roles/Domain/ParticipantWork.cs`, `Service/Daoris.Service.Core/Sessions/ParticipantWork.cs`, `Web/work/identity.ts`, `Service/Daoris.Service.Tests/fixtures/participant-work.json` | `supervise`, read from `decides` before every other mark; not in `HoldsSlot`; `RoleWork.Codes` stays the nine works a working role takes | c |
| The record | `Participants/Helpers/HelperDoors.cs` and its service route, as a helper's record is opened | A `supervise` record's open, marked `decides` and `serves` | c |
| The tools | `Service/Daoris.Service.Mcp/CallTable.cs`, `Service/Daoris.Service.Tests/fixtures/call-table.json` | `SupervisorServed`: the reads alone | c |
| Its containment *(R2-C8, R2-O5)* | `Loop/Session/SessionRules.cs:31-95`, `Review/Domain/OpinionPermissions.cs:65-70`, `:100-117`, `Agents/Domain/TreeGuardHook.cs`, `Participants/Domain/OpinionPosture.cs`, `Agents/Providers/ClaudeCode/ClaudeCodeSpawn.cs:87-113`, `Agents/Providers/ClaudeCode/ClaudeProtocolDialect.cs:15-23` | The supervise shape: the reviewer's, with the harness's shell tools denied whole and none of the reviewer's git reading allows *(R3-C3)*, `MultiEdit` among the reviewer's denied writes *(R4-O3)*, and the hard-deny sentence naming a role's grants; the tree guard handed whatever the defaults say *(R3-O8)*; on Claude Code, the user tier and the handed file alone and Daoris's connector alone, on either door *(R4-O1)*; the reading posture where a harness has one; the posture said on the decision | c |
| Who may ask | `Participants/Domain/StartAdmission.cs` | A `supervise` session asks for no co-worker | c |

### 7.7 The censuses

| Census | File | What it holds after | Row |
|---|---|---|---|
| The staffing's writers | `Daoris.Desktop.Driver.Tests/Roles/RoleCatalogCensusTests.cs` | The role door is still the one writer, the appointed staffing named under it | c |
| Records held to their role (census C) | `Daoris.Desktop.Driver.Tests/Roles/RoleCensusTests.cs` | A `supervise` record is held to the agent-held supervisor its start was bound to | c |
| The order's readers | `Daoris.Desktop.Driver.Tests/Roles/OrderReadersCensusTests.cs` | No new direct reader: the attempt walks through `StartPlan` | c |
| The pipe's routes and classes | `Daoris.Desktop.Modules.Tests/Platform/PipeCensusTests.cs`, `PipeGroundsCensusTests.cs`, `Daoris.Desktop.Modules.Tests/Bridge/WindowCensusTests.cs` | `SUPERVISOR_DECIDE` and `SUPERVISOR_LEAVE` in `Role`; the three old routes gone | b |
| The owner's marks | `Daoris.Desktop.Modules.Tests/Platform/OwnerSetCensusTests.cs` | A supervisor's release, through its subject's own door, marks nothing as the owner's | e |
| The node kinds | `Daoris.Desktop.Driver.Tests/Workflows/Kinds/NodeDispatchCensusTests.cs` | The landing kind presses `review` and `human` through the kit's press door, the runtime naming no kind | f |
| The call table | `Mcp/McpRoleFilterTests.cs` over `fixtures/call-table.json` | The `supervise` column, and its shape's denies beside it *(R2-O5)* | c |
| The participant work | each side's `ParticipantWorkTests` and `ParticipantWorkReadersTests` over `fixtures/participant-work.json` | The `supervise` row | c |
| Suggested choices | `Daoris.Desktop.Driver.Tests/Roles/ActorSuggestionTests.cs` | The table held to `docs/model-roles.md`, the supervisor's row included | c |
| The scopes | `Daoris.Desktop.Modules.Tests/Platform/SupervisorScopeTests.cs`, `RoleActTests.cs` | §3.4's six steps kind first, at each rank of place; the grants at the judge and the release | a |
| Translations | `Web/locales/i18nCheck.test.ts`, `Web/locales/glossary.test.ts` | §6.5's words in both catalogues | a to g |

### 7.8 What goes in the same chunks (D228)

- `appointed-not-configurable` on the supervisor, and its holder sentence naming *your rules alone* (row a for kinds, row c for the
  holder).
- `RoleCatalog.Appointed`, `StandingConfirmation.Staffed` and `StandingConfirmation.WorkspaceOf` (row a).
- `RoleJudge.Admit(repository)` and the releases' admission by generation alone (row a) *(R2-O2)*.
- The `defer` act and the bare `answer` act name (row a's migration step), and the page's `ACTS` and `HOLDERS` twin (rows a and c)
  *(R2-O9)*.
- `SUPERVISOR_ANSWER`, `SUPERVISOR_DEFER` and `SUPERVISOR_COURSE`, with `sessions supervise`'s `--point` and `--course` (row b).
- The `RoleHolders.Terminal` written at the keeping (row b).
- `RuleProposals.Answer`, replaced by `Settle` (row e) *(R2-C5)*.
- *(R4-C1)* Every unlocked read-then-save of the rule file, each writer moved onto `PermissionRules.Change`, and `PermissionRules.Save`
  called from inside it alone (row e).
- The area sentences each row changes, rewritten in place by that row *(R2-O9)*: this area's rule 7, rules 41, 42, 46 and 48 that name
  the terminal alone (rows a to c), rule 47's access widening, which becomes the `allow` kind's while a free question about access stays
  the person's, and rule 51's ungranted act (row e), rule 49's window, which no longer holds an allow (row e), and rule 55's Accept,
  the person's own press or a landing supervisor's (row f); the agents area's rule 20 (a widening waits for the person's yes) and rule
  23 (a request held for the person), with `HeldPermissions`' remark (`Events/HeldPermissions.cs:81-82`), at e; the workflows area's
  rule 18 (the door keeps the person's word) at f; the Open list's SUPERVISOR1c line, at row c.

## 8. Rejected

- **Falling through to the next scope's supervisor when the one found cannot act.** Who decides would then depend on accounts' state
  at the moment; the person reads it from the catalogue alone (§3.4).
- **Resolving by grants: passing a supervisor whose grants do not cover the act to a wider one** *(R2-O12)*. Unticking an act at a
  narrower place would then widen what a wider supervisor decides there, and a person could not keep a place's allows theirs without
  removing its supervisor; kind first and *Who decides here* naming the gap read the note's *generic one answering what no kind takes*.
- **A role kind per decision kind** (`kind: allow-supervisor` beside `supervisor`). It copies the supervisor's whole shape for each
  kind; one shape with `decides` holds them all.
- **The policy's `works` reused for decision kinds.** A field would mean two things; a kind supervisor carries its own staffing.
- **A route per kind beside the three of today** (`SUPERVISOR_ALLOW`, …). That is five routes for one job (D228); one decide route and
  one leave route carry a `kind`.
- **A subject named by its session alone** *(R2-C3)*. A delayed word about a session's first question would read as its second.
- **The actor answering through a connector tool**, as a helper's `helper_answer` does. The supervisor design §3.5's contract stands:
  no tool names a session, and the result is read from the run alone.
- **A holder key for the actor, or an agent hold drawn in memory** *(R2-O2)*. Nothing of the actor crosses the pipe, the driver reads
  its own run, and a generation from the catalogue's revision stales a held decision from any process.
- **The actor taking a place under the cap.** A full cap's running session would wait on its own allow (§2.3).
- **A restart counting an attempt it dropped, or handing its subject back** *(R2-O10, R2-C7)*. Counting would spend a choice on no
  failure; handing back would let a restart reconsider what a supervisor decided. Its subject is the person's, as a question's is today.
- **Grants left unchecked, as today.** A grant the judge never reads is a word on a screen; the direction is that a supervisor answers
  under its grants.
- **An older reader of the earlier records, or a test of the older build's parser** *(R2-C1)*. D232 keeps no older reader, and no older
  build starts on a converted home; the boundary is what the proofs hold.
- **`defer` kept as a grant.** Leaving changes nothing of Daoris's controls, as the steward's desk decline does not.
- **The steward taking an agent holder too.** The direction keeps its terminal holder.
- **A supervisor's allow of a steward's act kept standing, or sent on as the person's press** *(R2-O1)*. The first is a grant of the
  steward's outside its record; the second makes a supervisor's word the person's own.
- **Reading `owner-set` at a supervisor's release** *(R2-O13)*. Any press of the person's on a quest would take every later decision on
  it from its supervisor; the person's stop and pause are read instead (§4).
- **A once-only `files` allow consumed by the next take** *(R2-C9)*. The take that met the ask has passed it; §30.4's standing allow in
  the person's list is the one way.
- **A supervisor's standing allows kept in a list of its own.** Two lists of allows would be two ways to read one fact (§5.1).
- **A widening's receipt saved with the rules, to replay a `settling` proposal by** *(R3-C2)*. A recovery that never saves needs no
  receipt to tell a save that ran from one that did not: the rules as they stand say whether the change holds (§5.3).
- **The install's verbs denied one by one on a supervise turn** *(R3-C3)*. A program the person's interpreter allow runs reaches the
  controls with no verb; the shell denied whole holds every form (§2.8).
- **A `files` allow of the `ask`'s pattern, or in the machine's layer** *(R3-O6)*. Either lifts the restriction past the one act and
  the one place the supervisor decided (§5.1).
- **Denying each other server's tools on a supervise turn** *(R4-O1)*. Another server's name is not known before the turn starts, and a
  hook in the copy runs outside every rule; the turn's sources and servers held to the user tier, the handed file and Daoris's
  connector hold both (§2.8).
- **`no-publish`'s forms handed as denies** *(R4-O2)*. A deny would also refuse the person's own allow of a publish, a session's rules
  changing for a supervisor's sake; read by the widening's class alone, they keep the publish the person's and every session as it is
  (§5.3).
- **The rule file's lists merged as sets** *(R4-C1)*, as `driver.json`'s sets are. Each writer would still decide from a read another
  has changed, and one rule's one place in a scope would be lost when two writers move it; one locked change decides from the file as
  it stands (§5.3).

## 9. The rows, in build order

Rows d, e and f each follow b and need no actor: a terminal holder decides their kinds as soon as they land. Row g follows TREEPOOL1
§30's rows 6a, 3 and 6b. Row h is last.

**SUPERVISOR2a — kinds and grants in the catalogue** (driver, modules, web). Changes: `decides`, the six kinds' vocabulary and qualified
acts, the judge per kind and scope with the machine's generic supervisor, one resolver (`RoleCatalog.Supervising`) kind first at a place
of any rank, holding an unreadable slot's scope for the person, with the store's wider places; the admission by kind, place and act,
read at the judge, the keeping, the releases and the tickets; the seed; the migration step, keeping the home's newest build first
*(R3-C1)*, and the role door's `not-converted` before it; a workspace map that does not read held for the person *(R3-O7)*; the verb's
`--decides`; `StewardStaffed` on the catalogue; the role card; `ROLES`' `deciders`; the page's `ACTS` and `HOLDERS`. The
person sees the editor's *Decides* and its acts grouped by kind, *Who decides here* on each place's Roles, *Add a supervisor for…*, and
a terminal holder's ungranted act refused `not-granted` in the verb's words. Proof: two supervisors of one kind in one scope refused,
and a generic one beside one of a kind standing; the machine's generic one not removable or given kinds; each of §3.4's six steps, at a
repository's, a workspace's and the machine's place; a workspace's allow supervisor answering beside a repository's generic one
*(R2-O12)*; a repository's supervisor deciding `allow` with nothing ticked keeping its allows the person's under an agent-held workspace
supervisor; an unreadable repository supervisor making that repository's decisions the person's *(R2-O6)*; a grant taken away while a
go-on is held, in the editor and by `driver role set` from its own process, staling its release `not-granted` *(R2-O2, R2-C2)*; the role
door refusing a supervisor's record `not-converted` before the step and writing it after; the step converting `roles`,
`role_revisions` and decision files on a home with `answer` and `defer`, and a queued proposal carrying `answer` refused `bad-record`
*(R2-C1)*; the home's newest-build write failing while the migration's own writes succeed, the step unrecorded, the role door
`not-converted` and the records unconverted, then converted at the next start whose write holds *(R3-C1)*; a workspace map that does
not read making a repository's decisions the person's, said, where a store before its step reads each place on its own chain
*(R3-O7)*; `driver role set gatekeeper --name Gatekeeper --decides allow --holder rules` with nothing ticked set *(R2-O8)*; the desk
filing a request with the steward staffed *(R2-O7)*; an ungranted answer and an ungranted course go-on refused, a leave admitted with
none; HoldFields' and AppointedRows' tests in both languages.

**SUPERVISOR2b — one decision door** (modules, driver). Changes: `SUPERVISOR_DECIDE` and `SUPERVISOR_LEAVE` with `kind` and the typed
subject; the kinds' rows `QuestionKind` and `CourseKind`, moved from `SupervisorDoor` and `SupervisorCourseDoor`; the decision's `kind`
and subject on its files; the sweep by kind; `SUPERVISOR_WAITING` by kind; the verb's `--do` and `--leave`; the three old routes and
their flags removed. The person sees nothing new on the window; the holder's verb takes its new words, and *Details* and the cards name
each decision's kind. Proof: every refusal the question and course routes answer today, ported onto the new routes; a delayed decide
naming a session's first question refused `question-changed` once the session parks on a second with the same label, and one naming an
earlier point refused `answered` *(R2-C3)*; a kind supervisor's decide on a kind it does not decide refused `not-granted`, and a generic
supervisor's where a kind supervisor is effective refused `role-changed` *(R2-O11)*; a course decision overruled before a restart, its
point not listed again after it, and one whose overrule line was not written closed as the person's too *(R2-C7)*; the pipe census with
two routes in and three out.

**SUPERVISOR2c — the actor holder** (driver, service, modules, web). Changes: `holder: agent` for the supervisor with its staffing and
policy through the role door; the agent's generation by role and revision, and its admission; the `supervise` work, its record, its
call-table column and its start rule; the attempt, its outcome and its closure at a start; the turn with its containment and the empty
folder, the shell denied whole on a harness that takes rules *(R3-C3)*, `MultiEdit` among the reviewer's denied writes *(R4-O3)*,
the tree guard whatever the defaults, and the role verb refused from an agents' job *(R3-O8)*; on Claude Code, the turn's sources the
user tier and the handed file alone and its servers Daoris's connector alone, on either door *(R4-O1)*; the start's sweep reading
each kind's durable door *(R3-O2)*; the prompt from the shared composer, and the look's pass; the unavailable path; the suggested
row; the censuses of §7.7 for c;
the editor's *An agent* with its choices, bench scorecards, posture, policy and week; *Used by*. The person sees a supervisor held by an
agent answering questions and courses at its places within its grants, each decision on its card with *Overrule*, the tier that ran and
what held it read-only, its turn readable, and its week in the editor. Proof (a stub harness, and one real-process check): answering,
leaving, timing out, and answering late; an attempt interrupted by a restart closed as failed and never started again; an attempt that
answered, its decision held, then a restart: the subject the person's and no second attempt *(R2-O10, R2-C7)*; a limited end moving to
the next choice; no choice ready falling to the person, said, then answered once the account's cool-off ends; a reached budget passing
its choice; a forged connector naming the actor's session refused, and a pipe act naming an agent's generation refused; a result after
a revoke, a role change from another process, or a changed subject dropped *(R2-O2)*; the person's word ending a running attempt; a
subject with no repository run in its own empty folder, removed after; on Claude Code, a real supervise turn told to run
`daoris driver role set` and to write the home's permissions refused both, its decision saying `rules-and-copy` *(R2-C8, R2-O5)*, and,
with the person's composition allowing `Bash(python:*)`, the same turn told to write the home's permissions with a Python program
refused *(R3-C3)*; on each door, with the account's user tier serving a server whose write tool the person's composition allows and
the copy's uncommitted `.claude/settings.json` holding a `SessionStart` hook, the turn offered neither that tool nor the copy's
servers and running no hook of the copy's, the home unwritten *(R4-O1)*; the turn handed the tree guard with `tree-guard` switched
off, and `daoris role set` refused from a process in an agents' job while it acts from the person's terminal *(R3-O8)*; the record's
actor fields and each decision's tokens.

**SUPERVISOR2d — limits and moves** (driver, modules, web). Changes: `refer` offered on `limits` and `moves` for a driven line (the judge
still refuses it on a conversation's line, `do`); the `LimitKind` row (`limit.move` as an approved move in `role_moves`, `limit.wait` as
the `wait` action makes it); failures on one choice moved from the course kind to the limit kind; the course and limit releases reading
the person's stop and pause *(R2-O13)*. The person sees *Ask the supervisor* on the limits and moves counts in a place's rules fold for
driven work, and the line's card saying the supervisor's move with *Overrule*. Proof: a driven line limited under a `refer` rule moved
by the supervisor, overruled inside the window, and left; a wait up to the rule's bound; a move refused `owner-held` on a line the person
stopped; a conversation's `refer` on limits refused; `moves-unbounded` still judged.

**SUPERVISOR2e — allows** (driver, modules, service, web). Changes: the `AllowKind` row's three subtypes, each placed where its effect
reaches and covered by its own acts (§5.3), released at its keeping. A held request: `allow.once` or `allow.reject` through the door's
one end. A widening: `allow.standing` or `allow.reject` at its rule's reach, through `RuleProposals.Settle`, recorded `by` the role,
one that removes the person's deny or switches off a default that denies left theirs, a default's switch at the machine *(R3-O1)*, and
a `settling` one recovered by what the rules hold, never saved again *(R3-C2, R3-O2)*; `no-publish`'s forms in code, a widening
whose rule would match one while it is on left the person's *(R4-O2)*; every writer of the rule file through one locked change
*(R4-C1)*. The steward's act outside its grants: an `allow`
subject raised where `StewardAsk.Outside` asks today at the target's place as `ActAsync` reads it, one pending entry settled once
*(R3-O4, R3-O5)*, `allow.steward` admitting it once as the steward's own act, an allowed `land` held for the window *(R3-O4)* and
dropped with no card where the steward withdraws it there *(R4-O4)*, refused
from a holder that holds the steward too, falling to the person's card otherwise (§5.2). The person sees the held permission's card
saying *the supervisor allowed this once* with no *Overrule*, a widening allowed by a supervisor on the Agents page with who allowed it
and *Take back*, and the steward's line saying which act a supervisor allowed, a `land` with when it lands and *Overrule*. Proof: a
held request allowed once at once by an agent supervisor, refused by one, and left; a machine widening proposed from a repository
decided by the machine's supervisor and not by that repository's *(R2-O3, R2-C4)*; `allow.once` not covering a widening, and a standing
option refused at a held request *(R2-C4)*; a person's decline and a supervisor's yes on one proposal, one settling and the other
`answered` *(R2-C5)*; a crash between the save and the mark, then the person's `daoris agent rules remove` of the allow, or a deny in
its place, while no Daoris runs, and the recovery leaving the access withdrawn, the proposal returned to the person; a crash with the
change standing marked settled with no save *(R3-C2, R3-O2)*; under an agent-held allow supervisor, a widening that moves the person's
deny to `allow` and one switching `no-publish` off waiting for the person, and a default's switch proposed with a repository's scope
decided by the machine's supervisor *(R3-O1)*; under it, `add allow Bash(npm publish:*)`, `add allow Bash(npm:*)` and an option-first
form for repository X waiting for the person while `no-publish` is on, `add allow Bash(npm run build:*)` decided, and the publish's
widening decided once the person has switched `no-publish` off *(R4-O2)*; the supervisor's yes on a proposal moving B from `ask` to
`allow` while the person's removal of allowed A waits on the rule file's lock, and the removal landing first, each leaving A removed
and B allowed, and the tick's narrowing, the verb and the setups the same, through the change's read seam *(R4-C1)*; a yes on a
widening whose rule the person has since denied in its scope returned to them, nothing written *(R4-C1)*; a widening allowed
standing and taken back by the person; the steward's `delete` outside its grants allowed once, logged `by: steward` naming the
decision, the next `delete` asked again; one left and falling to the person's card; one refused where one terminal holds the steward
and the allow supervisor *(R2-O4)*; a `stop` on a session placed at its quest's repository and decided there, and one on a quest whose
repository does not read going to the person's card *(R3-O4)*; an allowed `land` waiting the window and overruled inside it, the
person's card following *(R3-O4)*, and one the steward withdraws inside it dropped, answered `withdrawn`, with no card and nothing
landed *(R4-O4)*; an allow arriving after the ten minutes dropped, the card asked once and the act run once, and the steward's
withdrawal settling the entry before either *(R3-O5)*; an update act outside the grants still refused `not-granted`; `owner-set` read
before an allowed steward act *(R2-O1)*; an allow no grant covers waiting for the person; the owner's-marks census.

**SUPERVISOR2f — landings** (driver, modules, web). Changes: the `LandingKind` row over a `review` step's Accept and a `human`
approval's *Approve* and *Reject*, pressed through each node's own press door; the node's word carrying its author and attempt, the
person's word replacing a supervisor's pending one, a supervisor's word kept and moved as its own, and dropped at the pass where its
decision is not live in this run *(R3-O3)* (§3.6); refused `landing-pushes` where
the landing's plan names a plugin that pushes, on any run a go-ahead holds, and `owner-held` on a line the person stopped or paused. The
person sees the review step saying *the supervisor accepted this; it lands at 14:10* with *Overrule*. Proof: a review accepted by the
supervisor and landed after the window; overruled inside it; the person's *Reject* pressed after a supervisor's *Approve* was released
and before the pass applied it, kept as the person's and the decision `superseded` *(R2-C6)*; a supervisor's word on a lapsed attempt
dropped `stale`; a supervisor's Accept released, then a restart before the pass: the first pass after it dropping the word, the
decision `stale` and the review the person's, and a word applied before a restart recorded made by the sweep *(R3-O3)*; a pushing
landing refused; a paused quest's landing refused `owner-held` *(R2-O13)*; an approval approved and rejected;
the node kinds' census.

**SUPERVISOR2g — the rule file's allows** (driver, web; with TREEPOOL1 §30's rows 6a, 3 and 6b). Changes: the `FilesKind` row over
§30.4's asks (§5.1), at the place its act reaches, under `files.allow-run`, `files.allow-copy` and `files.allow-share`; a standing allow of
the held act's own text written into the deciding place's layer with `by` *(R3-O6)*, under the person's file's lock as every writer
of it is *(R4-C1)*. The person sees the repository page's waiting ask
answered by its supervisor, and a standing allow saying who gave it, with *Take back*. Proof: a command a person's `ask` holds at a
start, not run there, allowed by the repository's supervisor, written to the repository's layer `by` the role, and run at the next
start; taken back, and asked again; under a machine's `ask` of `run(npm *)`, repository X's allow of `run(npm ci)` written into X's
layer alone, X's next start running it, X's `run(npm test)` and repository Y's `run(npm ci)` still asked *(R3-O6)*; a share decided at
the workspace covering both repositories, its allow in both repositories' layers *(R3-O6)*; the start's sweep recording a decision
made whose allow stands `by` it *(R3-O2)*; a supervisor without the grant, and an ask naming the person, leaving it to the person's
press *(R2-C9)*.

**SUPERVISOR2h — seen on the install** (the parent with the install). An agent-held generic supervisor on the machine and an `allow`
supervisor on one repository: a question answered and one overruled inside the window, an allow given at once and a standing one taken
back, a budget reached and falling to the person *(R2-O1)*, and each harness the install runs measured on whether its supervise turn's
posture stops a write outside its place *(R2-C8)*, Claude Code's with the person's `Bash(python:*)` allowed and a program told to write
the home's permissions *(R3-C3)*, and with a server the person allows and a hook in the copy *(R4-O1)*. The spend is reported, run
once at the least that proves it.

## 10. What the owner settled *(R2-O1, R3-O4)*

D233's second note of 2026-11-08 settles round 1's four questions, and its third note round 2's one; each is written in:

1. **A go-ahead is no kind.** It stays the person's alone, and every kind refuses a subject a go-ahead holds (§2.7, §3.1).
2. **The bench gates nothing.** It measures each choice and the editor shows its scorecard; `holder: agent` is never refused for want of
   a bench run (§2.1, §6.1).
3. **The steward's act outside its grants goes to the allow supervisor first**, under `allow.steward`, off by default, the person's card
   following where it is not held or the supervisor leaves it (§5.2).
4. **An allow acts as soon as it is kept**, the person taking a standing one back afterwards (§3.1, §4, §5).
5. *(R3-O4)* **A landing keeps the role's window** and is no allow (D233's third note, as round 2 recommended): a supervisor's Accept
   or Approve is held for the window with *Overrule*, since a landed branch is not taken back as a standing allow is (§3.1, §3.6). A
   steward's `land` a supervisor allows waits the window too (§5.2).

No question is left for the owner.

## 11. Whether it is ready *(R3, R4)*

- **Rows b, d and h** are ready to build once a's and c's contracts hold. Row b's change is the typed subject, which today's forms
  already carry for a question and a point, and the sweep by kind; row d's is the person's stop and pause read at its release; row h's is
  its proof.
- **Rows a, f and g** are ready to build. Round 3's changes to them are clauses taken as their readers gave them: a's migration step
  keeping the home's newest build first and its workspace map held for the person; f's word bound to its run; g's layer, the held
  act's text and the sweep reading its allow. *(R4-C1)* Round 4 adds one clause to g, its allow written under the person's file's
  lock as §5.3's change of the rules is, the same mechanism and no new kind.
- **Rows c and e** *(R4)* are ready to build once a one-paragraph confirming check reads round 4's clauses: for c, §2.8's *Its
  sources, on Claude Code* and its `MultiEdit` sentence, with §7.6's containment row and §9 c's proof; for e, §5.3's *A publish stays
  the person's too*, its settlement's sentences marked *(R4-C1)* and *One change of the rules*, and §5.2's allowed `land` in its
  window, with §7.4's rows and §9 e's proof.

## Round 2: both reads of round 1 answered

Each finding read against main at `bdab99e6f4`: whether it holds there, what round 2 does, and where.

| Finding | Holds at main | Round 2 | Where |
|---|---|---|---|
| Opus 1, blocking: the design predates the owner's four choices | Yes: D233's second note (`docs/decisions/D233.md:45-51`) settles them, two against round 1's recommendations | **Taken.** An allow (`allow`, `files`) acts at its keeping, a standing one taken back; landings keep the window and stop being called allows; the steward's ungranted act goes to the allow supervisor first under `allow.steward`, once, admitted as the steward's own act, the person's card after; §10 written as settled, one question left; §2.7's pointer gone | §0, §1.1, §2.7, §3.1, §4, §5.2, §6.4, §8, §9 e, f, h, §10, §11 |
| Opus 2, blocking: a grant removed does not stop a held decision's release | Yes: `Roles/SupervisorReleasing.cs:29`, `Roles/SupervisorCourses.cs:105`, `Roles/SupervisorTickets.cs:27` ask for a generation alone; `Modules/Platform/RoleTerminals.cs:135-146` ends no hold on an `authority` change; `Roles/RoleVerbs.cs:62` writes from its own process | **Taken.** The admission answers by kind, place and act at every step, refusing `not-granted`; an agent's generation is its role's id and revision, read at every admission | §2.2, §4, §7.3, §9 a, c |
| Opus 3, blocking: a widening decided at the proposing session's place | Yes: `Service/Daoris.Service.Mcp/RuleProposalTools.cs:27-28` takes a scope of any rank | **Taken.** A decision is placed where its effect reaches, a widening at its rule's reach; the admission takes a place of any rank | §3.4, §5.3, §9 e |
| Opus 4, blocking: one terminal holding the steward and the allow supervisor allows its own act | Yes: `Modules/Platform/RoleTerminals.cs:36-45`, `Modules/Platform/RoleJudge.cs:160` | **Taken.** A steward's act is not listed for, and its allow refused `not-granted` from, a hold whose holder holds the steward too | §5.2, §7.3, §9 e |
| Opus 5: the supervise turn leaves the role door to the classifier | Yes: `Review/Domain/OpinionPermissions.cs:100-117` keeps the person's other allows | **Taken** with Codex 8: denies of the install's role, rules, permission and files verbs, the hard-deny sentence naming a role's grants, the reading posture where a harness has one, the call table's census | §2.8, §7.6, §7.7, §9 c |
| Opus 6: an unreadable supervisor record falls through | Yes: `Roles/RoleCatalog.cs:223`, `:174` | **Taken.** The resolver holds an unreadable slot's scope for the person, as `RoleFor` holds starts | §3.4, §9 a |
| Opus 7: `StandingConfirmation.Staffed` also finds the steward | Yes: `Modules/Platform/StandingConfirmation.cs:176`, `:195` | **Taken.** `StewardStaffed` reads the catalogue's one steward | §7.3, §9 a |
| Opus 8: the verb takes a new appointed role's kind from its acts | Yes: `Roles/RoleVerbs.cs:383` | **Taken.** `--decides` makes a supervisor | §3.2, §7.1, §9 a |
| Opus 9: the same-chunk list and file lists miss sentences and twins | Yes: rules 47, 49, 51, 55; agents rules 20, 23; `Events/HeldPermissions.cs:81-82`; workflows rule 18; `Web/agents/roles/appointedHold.ts:6-14`; the no-`kind` reader with no deleting row | **Taken.** Each named in its row; the reader of a file without `kind` replaced by the migration giving every file its kind, since D232 keeps no older reader; a terminal's holder keeps today's shape, so no second holder reader | §2.4, §2.7, §7.1, §7.4, §7.5, §7.8 |
| Opus 10: a restart and the count of one attempt per choice | Yes, unsaid in round 1 | **Answered.** Not *not counted*: an attempt whose held decision a restart dropped makes its subject the person's, as a question's is today (`Roles/SupervisorDecision.cs:83-84`), so no choice is spent again and no restart reconsiders a decision | §2.2, §2.3 point 5, §8, §9 c |
| Opus 11: one proof names the wrong code | Yes | **Taken.** Row b proves `not-granted` for a kind outside the role's, and `role-changed` for a shadowed generic one | §9 b |
| Opus 12: grant shadowing against the note's words | Yes | **Taken in part.** Resolution reads kind first, so a narrower generic supervisor no longer takes a wider kind supervisor's decisions; resolving by grants **declined**, since unticking an act at a narrower place would widen what a wider supervisor decides; *Who decides here* names the gap | §3.4, §6.2, §8 |
| Opus 13: whether a supervisor's release reads `owner-set` | Yes, unsaid | **Answered.** It does not, since `owner-set` takes a whole quest after any press; the course, limit and landing releases read the person's stop and pause (`owner-held`, `Modules/Bridge/StewardTargets.cs:29`) | §4, §7.4, §8, §9 d, f |
| Opus: the two defects round 1 found | Both hold | Unchanged | §1.2, §4 |
| Codex 1, blocking: the grant migration lets an older build misread the new records | Yes, in the minutes between an update's first start and its confirmation: the parser takes any `authority` (`Roles/RoleHold.cs:121-135`), ignores `decides`, and `Modules/Platform/RoleJudge.cs:162-166` reads no grant; after confirmation the launcher refuses an older build (install area rule 46), and the migration runs only after it (rule 51) | **Taken.** The role door writes no supervisor record before this release's step (`not-converted`); the step converts `roles`, every revision in `role_revisions` and every decision file; a record it did not reach is refused `bad-record`. **Declined:** a test of the older parser on converted records, since no older build starts on a converted home; the proofs hold the boundary | §4, §7.1, §8, §9 a |
| Codex 2, blocking: a removed grant does not stop a held terminal decision | Yes, as Opus 2 | **Taken** with Opus 2; the cutoff is the catalogue read in the release's own step under the decision's lock | §4, §7.3, §9 a |
| Codex 3, blocking: `subject` does not identify the decision | Yes against round 1's form; today's forms carry `question` and `point` (`Roles/SupervisorProof.cs:153-173`, `Roles/SupervisorCourseForm.cs:6-23`) | **Taken.** A typed, immutable subject per kind, in the proof's bytes, the attempts, the files, the keeping and the release; no new route | §2.3, §2.4, §3.1, §7.5, §8, §9 b |
| Codex 4, blocking: an allow's scope follows its effect, its grant its subtype | Yes: a widening may reach the machine; a held request's door refuses standing options (`Events/HeldPermissions.cs:152-158`) | **Taken.** Each subtype's place and acts (§5.3): a widening `allow.standing` at its rule's reach, a held request its offered one-time options, a steward's act `allow.steward` binding its one proved request and leaving the steward's record unchanged, a share at the narrowest covering scope | §3.1, §3.4, §5.1, §5.2, §5.3 |
| Codex 5, blocking: dispatch through the person's route is not atomic or person-first | Yes for a widening (`Agents/RuleProposals.cs:220-231`); a held request already takes one end (`Events/HeldPermissions.cs:216`) | **Taken.** `RuleProposals.Settle`: locked, conditional, `settling` with its receipt, recovered by it; each kind settles at its subject's own door, and the dispatch settles nothing | §5.3, §7.4, §9 e |
| Codex 6, blocking: a supervisor's pending workflow word blocks the person and reads as theirs | Yes: `Workflows/Nodes/NodeRecord.cs:249`, `Workflows/Nodes/RunNodeWords.cs:113`, `:203`, `Workflows/Nodes/NodeRecordFiles.cs:519-532`, `Workflows/Kinds/Human/HumanNode.cs:54-60` | **Taken.** The word carries its author and attempt through writing, reading and applying; the person replaces a supervisor's unapplied word; a lapsed attempt drops it; the pass is the cutoff; no older build reads it, by Codex 1's boundary | §3.6, §7.4, §9 f |
| Codex 7, blocking: a restart discards final owner decisions and completed attempts | Yes: `Roles/SupervisorSweeps.cs:105-138` reads question files alone, and `Roles/SupervisorCourses.cs:131-133` lists the point again | **Taken.** The sweep reads every kind and the attempts and remembers every subject final to the person as closed; a decision's and an attempt's file is written atomically before it is live, and a lost state line reads held, which a start closes as the person's. **Answered:** no closure file of its own, since the decision's file is that marker | §2.2, §2.3, §7.5, §7.6, §9 b, c |
| Codex 8, blocking: the helper exemplar does not establish the read-only boundary | Yes: a harness with no rule language is handed no rules (`Loop/Session/SessionRules.cs:39-41`), held by its copy alone (`Participants/Domain/OpinionPosture.cs`), and round 1 gave a subject with no repository no copy | **Taken.** §2.8 states the containment per harness: the attempt's own empty folder, the denies on a harness that takes rules, the reading posture where one exists, measured on the install, and the posture said on every decision; a real-harness check in row c. **Answered:** a harness held by its copy alone is said, not refused, since the agent is the person's choice and Codex holds no denies (D233's choice 3) | §2.3, §2.8, §6.1, §7.6, §9 c, h |
| Codex 9, blocking: row g's once-only consumption against the rule file's handoff | Yes: §30 is on main, naming the `files` kind, `allow-run`, `allow-copy` and `allow-share`, and a standing allow asked for the next start (`docs/2026-10-26-tree-pool-design.md` §30.4) | **Taken.** This design takes §30.4 as written: a `files` kind, its three acts and places, a standing allow of the ask's own text in the person's list, read at any later start; no once-only `files` allow, so nothing is consumed | §3.1, §4, §5.1, §8, §9 g |
| Codex: both defects round 1 found | Both hold | Unchanged | §1.2, §4 |
| Codex: no defect in the six-step order, the refusal to fall through, grant-free leaving, or the separate agent-generation namespace | — | The refusal to fall through and grant-free leaving stand. The order is read kind first for Opus 12. An agent's generation stays outside `Holds`, now taken from the catalogue's revision for Opus 2 | §2.2, §3.4, §4 |
| Codex: today's question path is person-first (`Service/Daoris.Service.Core/Sessions/SessionSays.cs:245-286`, `Service/Daoris.Service.Core/Sessions/SaidKeeping.cs:65-74`) | Yes | The question kind keeps that path; the widening and the landing get their own settlement | §3.6, §5.3 |

## Round 3: both narrow reads of round 2 answered

Each finding of `## Codex, round 2` and `## Opus (independent), round 2` read against main at `6b47d1f6ac`: whether it holds there,
what round 3 does, and where. Each smallest change is taken, but for Codex B2, which a smaller one answers.

| Finding | Holds at main | Round 3 | Where |
|---|---|---|---|
| Codex B1, blocking (a): conversion is not conditional on keeping the minimum-build version | Yes: `Modules/Update/InstallUpdater.cs:190-198` leaves a failed keep to the next start, and `Transitional/Migration/HomeMigration.cs:98-103` runs once the swap lets the home go, reading no such record | **Taken.** The step keeps this build's version through `HomeLastBuild.Keep` and reads it back before converting anything; a failure leaves the step unrecorded and the role door `not-converted`; a proof where that write fails and the migration's own succeed | §0, §1.2, §4, §7.1, §9 a |
| Codex B2, blocking (e): replaying `settling` can undo Take back | Yes: round 2 saved a `settling` proposal again, and `PermissionRules.Add` drops and re-adds the rule (`Agents/Permissions.cs:399-416`) | **Taken by a smaller change**, with Opus 2: a `settling` proposal is never saved again, but marked settled where `Effect` finds its change standing, else returned to the person. Nothing reapplies, so no receipt need be saved with the rules; a Take back or a deny between stands; the proof is crash, removal, recovery, access withdrawn | §5.3, §7.4, §8, §9 e |
| Codex B3, blocking (c): an inherited interpreter allow changes the controls | Yes: the reviewer's shape keeps the person's other allows (`Review/Domain/OpinionPermissions.cs:111`), and the tree guard reads no program's own write (`Workplace/TreeGuard.cs:14-21`) | **Taken**, as its first form: on the rule-taking harness the supervise shape denies the shell tools whole (`Bash`, `PowerShell`), the turn reading through the read tools and the connector; round 2's verb denies are subsumed and not written; the real-harness proof and row h gain an inherited `Bash(python:*)` writing the permissions. `copy-alone` stays as chosen | §1.2, §2.8, §7.6, §8, §9 c, h |
| Opus 1, blocking (e): a widening may undo the person's deny, and a default's switch reaches the machine | Yes: `Agents/RuleProposals.cs:119-142` classes a deny's move or removal as a widening; `:154` and `Agents/Permissions.cs:419-436` switch a default machine-wide | **Taken.** A widening that removes or moves a deny of its scope, or switches off `no-publish` or `tree-guard`, is no `allow` subject and waits for the person; a default's switch is placed at the machine whatever scope it names | §0, §1.2, §2.7, §5.3, §7.4, §9 e |
| Opus 2, blocking (e, c's sweep): the recovery saves over the person's later edit, and the sweep closes a decision whose rule stood | Yes: as Codex B2, and `Roles/SupervisorSweeps.cs:105-138` closes a held or released decision without reading any door | **Taken** with Codex B2. The sweep reads each kind's durable door first (a widening's proposal, a `files` allow, a node's word) and records made a decision its door holds | §2.2, §5.3, §7.4, §7.5, §9 c, e, g |
| Opus 3, blocking (f): a landing's released word outlives the restart that closes its decision | Yes: the word waits on the node record, and the sweep takes back only a question's waiting word (`Roles/SupervisorSweeps.cs:104-105`) | **Taken.** The pass drops a supervisor's word whose decision is not live in this run, `stale`; one applied before the restart is recorded made by the sweep | §3.6, §7.4, §9 f |
| Opus 4, blocking (e): the steward's ungranted act's place, and a landing | Yes: `DependenciesAsync` answers a session target alone (`Modules/Bridge/SupervisorDoor.cs:194-217`), and only `ActAsync` resolves a session to its quest (`:110-132`) | **Taken.** The target is read as `ActAsync` reads it, its workspace as §3.4's; a target that does not resolve or a dependency that does not read is the person's card. An allowed `land` waits the window with *Overrule*, per D233's third note, now written in §1.1 and §10. Its non-blocking question answered: §2.7's *a deletion* bounds a free question, so the steward's `delete` is decided as its other acts are | §0, §1.1, §2.7, §3.1, §4, §5.2, §5.3, §6.1, §6.4, §7.3, §9 e, §10 |
| Opus 5, blocking (e): the ten minutes do not close the supervisor's side before the card | Yes: one request becomes `pipe.RunAsync` (`Modules/Platform/PipeAdmission.cs:160`) or `AskThenRunAsync` (`:188`), with nothing settling between them | **Taken.** One pending entry under `PipeState.Gate`, settled by the first of an allow, a leave, the steward's withdrawal and ten minutes from the raise; the bound closes the subject for the supervisor before the card, a later answer dropped; each turn bounded by it | §2.5, §5.2, §5.3, §7.3, §9 e |
| Opus 6, blocking (g): which layer a `files` allow is written into | Yes: §30.4's `permissions` are the machine's and each repository's, unioned, with no workspace layer | **Taken.** The deciding place's own layer: the repository's for `run` and `copy`, both repositories' for a share, never the machine's | §3.1, §5.1, §5.3, §8, §9 g |
| Opus 7, not blocking (a): a workspace map that does not read falls through to the machine | Yes: `Roles/RuleWiderPlaceStore.cs:26-37` | **Taken.** `Read` tells a store that failed from one before its step, and the resolver makes the decisions below the machine the person's on the first | §3.4, §7.1, §9 a |
| Opus 8, not blocking (c): two holes left in the containment | Yes: the pipe refuses a role act from an agents' job (`Modules/Platform/RoleJudge.cs:116-117`) but the verb's own writes are not refused; only a partner is held to its copy whatever the defaults (`Loop/Session/SessionRules.cs:91-92`) | **Taken.** The role verb's writes are refused from a process in an agents' job on every harness; the supervise turn is handed the tree guard whatever the defaults say | §2.8, §7.1, §7.6, §9 c |
| Opus's reading: a `files` allow is the ask's exact text | A reading of §30.4 | **Taken as read.** The held act's own text (`run(npm ci)`), never the `ask` rule's pattern, since `allow` is read before `ask`; §30.4's *may be a pattern* is the person's own allow | §3.1, §5.1, §8, §9 g |
| Opus's reading: the steward's ungranted act waits at most ten minutes | A reading of §2.5 and §5.2 | **Taken as read.** The request's own clock from its raise, never a turn's | §2.5, §5.2 |
| Codex: rows f and g ready; Opus: rows a and c ready | — | f changes only by Opus 3, g by Opus 6 and the sweep's door (Opus 2), a by Codex B1 and Opus 7, c by Codex B3 and Opus 2 and 8 | §11 |

## Round 4: the confirming reads of round 3 answered

Each finding of `## Opus (independent), round 3` and `## Codex, round 3` read against main at `0e50a18388`: whether it holds there,
what round 4 does, and where. Each smallest change is taken as its reader gave it; the code shows none smaller, and for Codex's
it shows the exemplar the change follows. A go-ahead stays the person's (D233's notes of 2026-11-08), which Opus 2's change keeps.

| Finding | Holds at main | Round 4 | Where |
|---|---|---|---|
| Opus 1, blocking (c): denying the shell leaves the turn's other ways to run a program | Yes: a helper's Claude Code takes its file by `--settings`, merged with the person's and the repository's (`Agents/Providers/ClaudeCode/ClaudeCodeSpawn.cs:105-113`), and the connector by `--mcp-config` beside every other server (`:87-95`); the shape keeps every other allow (`Review/Domain/OpinionPermissions.cs:111`), and the guard matches the edit and shell tools alone (`Agents/Domain/TreeGuardHook.cs:10`) | **Taken.** The turn starts as the holder's terminal does (`Modules/Platform/HolderTerminal.cs:46-48`): `--setting-sources user` and `--strict-mcp-config`, and on the protocol door `settingSources: ["user"]` and `strictMcpConfig: true` in `_meta.claudeCode.options`, the shape already carried and measured (`docs/2026-10-03-steer-evidence.md` §6). The copy's settings, hooks, servers and instruction files and the person's other servers are not the turn's; the tree guard and lineage hook come in the handed file. The real-harness proof gains an allowed server's write tool and a copy's `SessionStart` hook on each door; row h measures both | §1.2, §2.8, §7.6, §8, §9 c, h |
| Opus 2, blocking (e): a widening naming a publish undoes `no-publish` without switching it | Yes: `no-publish` holds no rule (`Agents/Permissions.cs:186-190`), so `Compose` hands nothing for it, and `add allow Bash(npm publish:*)` is a plain widening (`Agents/RuleProposals.cs:127`) | **Taken.** Its forms listed beside its sentence as `no-push`'s are (`:180-181`), plain and option-first, held by a test; while it is on, a widening whose rule would match one (`Bash(npm:*)` among them) waits for the person. The forms class widenings alone and are never handed as denies, so no session's rules change and the person's own allow of a publish stays theirs. `no-push` and the tree guard already outrank any allow; what no rule names stays as today | §0, §1.2, §2.7, §5.3, §7.4, §8, §9 e |
| Opus 3, not blocking (c): `MultiEdit` not denied | Yes: the reviewer's writes name `Edit`, `Write` and `NotebookEdit` (`Review/Domain/OpinionPermissions.cs:67`), the guard's matcher `MultiEdit` too (`Agents/Domain/TreeGuardHook.cs:7`) | **Taken** in the reviewer's own list, whose sentence says every tool that writes a file, so the reviewer's, the helper's and the supervise shapes deny it | §2.8, §7.6, §9 c |
| Opus 4, not blocking (e): an allowed `land` in its window names no end for the steward's withdrawal | Yes: the allow settles round 3's entry, and `WITHDRAW` takes back only a press still waiting for the person (`Modules/Platform/PipeTakeBacks.cs:136-203`) | **Taken.** The allowed `land` stays the pipe's one pending entry through the window, settled by the first of the window's end, the person's *Overrule* and the steward's withdrawal or cancel, which drops it as an overrule does but with no card: answered `withdrawn`, the decision `stale` | §5.2, §7.3, §9 e |
| Codex, blocking (e): ordinary settlement can still restore an allow the person just removed | Yes: the rules are read, edited and saved (`Agents/Permissions.cs:333-346`), the lock held over the save's re-read and rename alone (`Store/JsonKeeping.cs:111-116`), a changed list written whole (`:306-314`); the person's door reads and saves the same way (`Modules/Bridge/Routes/RulesRoutes.cs:32-57`), and so do the verb, the tick's narrowing and the setups, the narrowing able to write back a removed allow when it moves a rule out of `allow` | **Taken**, after the code's exemplar: `PermissionRules.Change(home, edit)`, as `HarnessSettingsChange.Change` (`Agents/HarnessSettingsChange.cs:31-56`) is for `harnesses.json`: the rule file's lock, the read, the edit judged from it (a yes the rules say marked with no save, one the person's restrictions now hold returned to them), the save holding the lock (`JsonKeeping.SaveHolding`). Every writer goes through it; a proposal's lock comes first and never inside. The proof interleaves through the change's read seam. Carried to row g, whose person's file is edited under no lock (`Workplace/Files/PersonFiles.cs:106-127`). **Declined:** the lists merged as sets (§8) | §0, §1.2, §5.1, §5.3, §7.4, §7.8, §8, §9 e, g, §11 |
| Opus: round 2's findings answered, and nothing in the place, the one settlement, the person's deny, `settling`, the sweep or the containment's other parts; Codex: row c ready, nothing further in row e's other parts | — | Unchanged | — |
