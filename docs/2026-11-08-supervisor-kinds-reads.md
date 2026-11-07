# SUPERVISOR2 — the reads of the design

Status: reads of `docs/2026-11-08-supervisor-kinds-design.md` (cited *§n*, *line n*). Paths are written as the design writes them
(`Roles/…` under the driver, `Modules/…`, `Service/…`, `Web/…`).

## Opus (independent), round 1

Read on 2026-11-08 by Claude Opus 5.5, read-only, from main at `31e8acd1a7`. The cited code is unchanged since the design's
`50055ee6d6` (only `Events/StartNoteCodes.cs` moved), so the design's line numbers hold. Nothing was built or run. The owner's
direction read against it: D233's two notes of 2026-11-08 and D214's principle note of the same day.

### Verdict per row

- **a — kinds and grants in the catalogue: another round, narrow.** Finding 2 (a grant removed while a decision is held does not stop
  its release). Findings 6 to 9 are small.
- **b — one decision door: ready to build.** Finding 11 corrects one proof's code.
- **c — the actor holder: ready to build**, with findings 5 and 10 taken in its build. Its agent hold is safe only if its generation is
  the role's revision, read at every admission (finding 2).
- **d — limits and moves: ready to build.** Nothing found.
- **e — allows: another round.** Findings 1, 3 and 4: the design predates the owner's choices that an allow acts at once and that the
  steward's ungranted act goes to the allow supervisor; and a widening is decided at the wrong place.
- **f — landings: another round, one sentence.** Finding 1: whether a landing is an *allow* that acts at once.
- **g — the rule file's allows: ready in contract once finding 1 lands**; its build waits on TREEPOOL1 §30 as written.
- **h — seen on the install: another round.** Its proof overrules an allow, which the owner's choice removes (finding 1).

### The two defects the design found

Both hold at main.

- **The supervisor's routes admit with no grant read.** For `SUPERVISOR_ANSWER`, `SUPERVISOR_DEFER` and `SUPERVISOR_COURSE` the role
  method returns covered at `Modules/Platform/RoleJudge.cs:162-166`, before the `authority` check at `:168-171`, which only `ROLE_ACT`
  reaches. No other reader of a supervisor's `authority` exists: the only `Acts.Contains` on a role's grant in the driver and modules
  is `:171`.
- **The effective supervisor is found twice.** `RoleCatalog.Appointed` (`Roles/RoleCatalog.cs:229-230`) is called only by tests
  (`RoleCatalogJudgeTests.cs:79-81`, `RoleDoorTests.cs:95-101`); production reads `StandingConfirmation.Staffed`
  (`Modules/Platform/StandingConfirmation.cs:179-180`) through `RoleJudge.Supervising` (`RoleJudge.cs:58`). The two bodies are the same
  query.

### Findings

**1. Blocking (rows e, f, g, h; §0, §2.7, §4, §5.2, §6.4, §8, §10, §11). The design predates the owner's four choices.**
D233's second note of 2026-11-08, committed two minutes after the design, settles §10: a go-ahead stays the person's and the bench
gates nothing (both as recommended); **the steward's act outside its grants goes to the allow supervisor first**, under its own grant,
off by default; and **an allow takes effect as soon as it is kept**, a standing allow taken back afterwards (rejected: holding it for
the role's window). The design still recommends the opposite of the last two (lines 594-600), holds every decision for the window
(§4, lines 275-278), keeps the steward's ungranted act the person's (§5.2, lines 317-319; §8, lines 515-516), proves an allow
"overruled before its release" with "a window cut to the request's bound" (row e, lines 563-565) and "one overruled" on the install
(row h, line 580), says *Goes on at 14:06* on an allow (§6.4, line 366), and still asks (§2.7, line 193; §11, lines 607-608).

- *As designed, choice 4:* a session in asking mode asks to run a command; the repository's agent-held allow supervisor keeps
  `allow.once` at 14:01; §4 holds it to 14:06, or to the request's `Until` less ten seconds; the agent's turn waits five minutes on an
  allow the owner said acts at once.
- *As designed, choice 3:* the steward's terminal sends `ROLE_ACT { act: done }` outside its grants; `RoleJudge.cs:171` answers
  `StewardAsk.Outside` (`Modules/Platform/StewardAsk.cs:20-23`), the person's card; the allow supervisor never sees it.

Smallest change, inside the direction:

- §4: the `allow` kind's acts are released at their keeping, with no window and no *Overrule*; the card says who allowed it and, for a
  standing allow, *Take back*. Rows e, g and h prove a standing allow taken back, not an allow overruled.
- §0 says which kinds are *allows*. It lists "a review or an approval before a landing" among them (lines 30-32), so read literally a
  landing acts at once too. I read the note as the `allow` kind, since it answered §10's question 4 about allow-once: landings keep the
  window, and §0 stops calling them allows. Either way the design says it.
- §5.2, §8 and §10's question 3: the steward's ungranted act becomes an `allow` subject under its own act (named, off on a new record),
  raised where `StewardAsk.Outside` asks today, and resolved at the act's target's place (the target's repository and workspace, as the
  gates already resolve them, `SupervisorDoor.DependenciesAsync`, `Modules/Bridge/SupervisorDoor.cs:194-211`). Left, ungranted or
  unavailable, it asks the person exactly as today. Allowed, it is admitted as the steward's own act (`state.Acts.Admit`, `role.act`,
  `by: steward`, naming the allowing decision), never sent on as the person's press (`StewardAsk.Press`, which makes a supervisor's
  word the person's own); and the owner-set check
  (`RoleJudge.cs:177`) is still read. The design also says what this means for the update acts, refused `not-granted` today since they
  have no person's route (area rule 51), and for `allow.standing`: the smallest reading is `once` alone, because a standing allow of a
  steward act is a steward's grant, and that lives in the steward's record, written by the role door (one place, D228).
- §10 and §11 rewritten as settled; §2.7's pointer goes.

**2. Blocking (row a; and c). A grant removed while a decision is held does not stop its release.**
§4 (lines 268-273) checks the grants "at the judge and at the actor's keeping"; §2.2 (lines 92-93) ends an agent hold on a role press
"as it ends a terminal's (rule 43)". The release asks the admission for a generation and nothing more (`Roles/SupervisorReleasing.cs:29`;
`Roles/SupervisorCourses.cs:105`; `Roles/SupervisorTickets.cs:27`). `RoleTerminals.RolesChanged` ends only a hold whose role is no longer
held by the terminal or is of another kind (`Modules/Platform/RoleTerminals.cs:135-146`), so a change of `authority` ends nothing. And
`daoris role set` applies the role door in the verb's own process (`Roles/RoleVerbs.cs:62`, `:114`, `:270`): only `SET_ROLE` and
`SET_ROLES` reach `RolesChangedAsync` (`Modules/Bridge/Routes/DriverRoutes.cs:468-493`).

- *Sequence:* a terminal holds the machine's supervisor with `course.go-on`; it keeps a go-on at 14:00, held to 14:05. At 14:02 the person
  unticks `course.go-on`, in the editor or with `driver role set supervisor --acts question.answer`. The hold stands. At 14:05 the release
  asks the admission, which answers the same generation, and the quest's *Try again* is made under a grant the role no longer holds. For
  an agent hold, a literal "as a terminal's" gives the same, and a terminal's write never reaches the sweep.
- *Smallest change:* the admission answers by kind and act (`Admit(kind, place, act)`), refusing `not-granted` where the effective
  role's grants do not cover the act. The keeping, the release and the tickets already ask it at each step, so the grants are read
  where the act takes effect. An agent hold's generation is the role's id and revision, compared at every admission, so a write from any
  process stales its decisions without a sweep. §2.2 says so instead of pointing at rule 43.

**3. Blocking (row e). A widening is decided at the proposing session's place, not at the place it widens.**
§3.1's allow row (line 208) takes a widening a session proposed; §3.4 (line 235) places a decision at "a repository and its workspace,
resolved once as a line's place is"; §7.3 (line 437) gives the admission `Admit(repository, kind)`. But `permission_propose` takes a
`scope` of machine, workspace or repository with its id (`Service/Daoris.Service.Mcp/RuleProposalTools.cs:27-28`), so a session in one
repository may propose a rule for the machine or for another repository.

- *Sequence:* a build session in repository X proposes `add allow Bash(*)` at the machine's scope. X's agent-held allow supervisor,
  granted `allow.standing` for X, allows it standing. The machine's rules now allow it for every session of every role in every
  repository, the supervise sessions among them, since the read-only shape is laid over the person's composition
  (`Review/Domain/OpinionPermissions.cs:9-12`, `Loop/Session/SessionRules.cs:69`). A repository's supervisor has acted on the machine.
- *Smallest change:* a widening's place is its rule's reach. It is decided by §3.4's resolution at that place: the machine's supervisor
  for a machine rule, the workspace's chain for a workspace's, that repository's chain for a repository's. The admission takes a place
  of any rank, not a repository. A held request and a rule file's allow keep their repository.

**4. Blocking (the row that builds choice 3). One terminal holding the steward and the allow supervisor allows its own ungranted act.**
A role's terminal takes further roles ("add this role to it", `Modules/Platform/RoleTerminals.cs:36-45`), and one holder's roles are
answered together (`RoleJudge.cs:160`).

- *Sequence:* the person staffs the steward and the machine's allow supervisor with one terminal, and grants the allow supervisor its
  steward act. The steward sends `delete` outside its grants; it becomes an `allow` subject, which `SUPERVISOR_WAITING` lists with the
  allow supervisor's generation; the same session decides it. The steward's grants then bound nothing.
- *Smallest change:* an allow of a steward's act is refused `not-granted`, and so is the person's, where the deciding hold's holder
  holds the steward too; the card says why. An agent-held allow supervisor shares no holder, so nothing else changes.

**5. Not blocking (row c). The supervise turn's shape leaves the role door to the classifier.**
§2.3 points 2 and 3 (lines 113-122) run the turn under the reviewer's read-only rules with reading tools alone. That shape takes away
and denies the edit tools, git's writes and the acting connector tools, and keeps the person's other allows
(`Review/Domain/OpinionPermissions.cs:100-117`). `daoris role set` writes the catalogue from whatever process runs it, refusing only in
a role's terminal (`Instance/TerminalRole.cs`).

- *Sequence:* a person's rule allows `Bash(daoris:*)`, or finding 3's widening does. A subject's fenced words tell the supervisor to run
  `daoris driver role set supervisor --acts …`. On Claude Code only the hard-deny sentence to the classifier stands between the turn
  and its own grants. On Codex the reading posture (`read-only`, `Agents/Providers/Codex/CodexProvider.cs:38`) stops the store's write.
- *Smallest change:* the supervise shape denies the install's executable's role, rules and permission verbs in the harness's rule
  language (deny wins, agents area rule 18); its hard-deny sentence names a role's grants; the turn takes the reading posture on every
  harness that has one. The call table's census holds the deny beside its `supervise` column.

**6. Not blocking (row a). An unreadable supervisor record falls through to a wider scope.**
§3.4 (lines 248-252): "the supervisor found answers, or nobody does". `HoldingActs` keeps only records that read
(`Roles/RoleCatalog.cs:223`), and `RoleFor`'s hold for an unreadable record passes appointed slots by (`:174`).

- *Sequence:* repository X's `allow` supervisor, held by rules to keep X's allows the person's, stops reading. The resolver skips it,
  and the workspace's agent allows X's requests.
- *Smallest change:* `Supervising` holds as `RoleFor` does (rule 6): an unreadable appointed slot whose scope reads makes that scope's
  decisions the person's, said with its error; one whose scope does not read holds every supervisor's decision.

**7. Not blocking (row a). `StandingConfirmation.Staffed` also finds the steward.**
§7.3 (line 433) and §7.8 (line 491) remove it, but `StewardStaffed` reads it (`Modules/Platform/StandingConfirmation.cs:176`), and the
desk's filing reads that (`:195`). Row a names the steward's reader, so the desk does not answer `not-staffed` once `Staffed` goes.

**8. Not blocking (row a). The verb takes a new appointed role's kind from its acts.**
§7.1 (line 414) adds `--decides`, and §4 (line 263) leaves a new record's grants off. `Roles/RoleVerbs.cs:383` takes the kind from
`KindOf(acts)` and refuses a new role that names none, so `driver role set gatekeeper --name Gatekeeper --decides allow --holder agent
"claude sonnet medium"`, with nothing ticked as the editor makes it, is refused. `--decides` names the supervisor's kind, as `--acts`
does.

**9. Not blocking (rows a to f). The same-chunk list and the file lists miss sentences and twins.**
§7.8 (lines 487-496) names rules 7, 41, 42, 46 and 48. These rows also rewrite this area's rule 47 (a widening becomes the `allow`
kind's; a free question about access does not) and rule 55 (the Accept a person's own press) at f; the agents area's rule 20 (a
widening waits for the person's yes) and rule 23 (held for the person), with `HeldPermissions`' remark that only the person's press
allows (`Events/HeldPermissions.cs:82`), at e; and the workflows area's rule 18 at f. The page's twin of the vocabulary, `ACTS` and
`HOLDERS` (`Web/agents/roles/appointedHold.ts:6-14`), is in no row's files. §7.5's reader of a decision file with no `kind` (line 454)
names its condition but not the row that deletes it, and an older file's holder `{ kind, parent }` (`Roles/SupervisorDecision.cs:148`)
is read by the same reader beside the new shape, which it should name.

**10. Not blocking (row c). A restart and the count of one attempt per choice.**
§2.2 (lines 94-95) resumes attempts "on subjects still waiting"; §2.3 points 2 and 5 close an attempt with no outcome as failed and end
the subject's attempts.

- *Sequence:* attempt 1, on choice 1, answers a question; the decision is held in memory; Daoris restarts at minute two; the attempt's
  file has an outcome and the question still waits. The design does not say whether choice 1 is spent (attempt 2 runs on another
  model for no failure) or not (a restart loop repeats choice 1 without bound).
- *Smallest change:* one sentence in §2.3: an attempt whose decision a restart dropped is not counted, and an attempt a start closes with
  no outcome ends the subject's attempts, as written.

**11. Not blocking (row b). One proof names the wrong code.**
Row b (lines 538-539) proves "a decide naming a kind its role does not decide refused `role-changed`". Under §3.2's judge a kind
supervisor's `authority` holds only its kinds' acts, so the judge answers `not-granted` first; `role-changed` is the keeping's answer
for a generic supervisor that a kind supervisor shadows (`RoleJudge.Covers`, `RoleJudge.cs:43-55`). Prove both.

**12. Not blocking (§3.4, §8). Grant shadowing, read against the note's words.**
D233's note says an allow is "the person's only where no supervisor's grants cover it". §8 (lines 500-501) argues the structural answer
from availability, which depends on the accounts' state; a scope's grants are the catalogue's, as readable as its holder.

- *Sequence:* a person adds a repository's generic supervisor with `question.answer` alone, to answer questions there. The workspace's
  allow supervisor stops allowing that repository's requests, and the person is asked them.
- *Smallest change:* keep the structural answer and say in §3.4 that it rests on D214's *the place's appointed supervisor*, with *Who
  decides here* (§6.2) naming the gap, as it does for landings; or resolve by kind and grants (the most specific supervisor of the kind
  whose grants cover the act), never by availability. Either stays inside the direction; the design says which.

**13. Not blocking (rows d, f). Whether a supervisor's release reads `owner-set`.**
The steward's act is refused on a target the person acted on this run (`RoleJudge.cs:177`; area rule 52). §5.2 and §7.4 send the
supervisor's releases through the steward's route dispatch "so the owner's marks pass both by", but say nothing of the check. A landing
accepted, or a move made, on a quest the person has just held is the case. The design says whether the kinds' keeping reads
`OwnerHolds`, or why a supervisor differs from the steward there.

### Where I found nothing

- **A decision answered twice.** A role has one holder; a terminal's act is bound to its hold's generation and checked against the
  effective supervisor at the keeping (`RoleJudge.Covers`); the actor's late or second block is dropped (§2.3 point 4); a held
  request takes one end, whoever answers first (`Events/HeldPermissions.cs:168`).
- **A pipe act naming an agent hold.** An unknown generation is refused before any proof is read (`RoleJudge.cs:135-141`), and
  `START_ROLE_TERMINAL` refuses a role no terminal holds (`RoleTerminals.cs:31`), an agent-held one among them.
- **A supervisor giving more than the person may at a held request.** A standing allow at that door is refused for any answer
  (`Events/HeldPermissions.cs:152-158`), and the design releases through that door.
- **An older build's records after the migration.** The launcher refuses an older build (D233's choice 6). A stored record the step did
  not reach still reads, with `answer`, and grants nothing, so it fails toward the person. A queued role proposal carrying `answer` is
  refused `bad-record`, not misread. A home past `nocompat1` runs a step it has not recorded (`HomeMigration.Run`), and an unedited
  supervisor reads the new seed.
- **The person's word across a restart.** No decision survives one, a held request ends with its process, and a standing allow is
  written to the person's own list (§5.1); a restart loses no word of the person's that today's code keeps.
- **Rows d and the rules first.** The rules still act first with no model, `refer` is offered only where a hold sits between turns,
  and a conversation's `refer` on limits stays refused.
- **The claims of §1.2.** Each cited line says what the design says it does, `StewardRouteAct.DispatchAsync` among them
  (`Modules/Bridge/StewardRouteAct.cs:89`, reached from `SupervisorDoor.cs:170`).
