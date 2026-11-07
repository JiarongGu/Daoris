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


## Codex, round 1

Codex's read of the design at `31e8acd1a7`, run by the steward from outside the install, read-only, without seeing the independent Opus reader's; verbatim but for its headings set down.

**Verdict: another round before implementation.**

| Row | Verdict | Why |
|---|---|---|
| a | **Another round** | Downgrade safety and revocation of held grants are incomplete. |
| b | **Another round** | The unified form loses decision identity; final closures need restart semantics. |
| c | **Another round** | Durable attempt outcomes and the claimed read-only boundary need tightening. |
| d | **Another round** | It inherits the unresolved identity, release and restart contracts in a–c. |
| e | **Another round** | Allows need effect-based scope, act applicability and atomic arbitration with the person. |
| f | **Another round** | Today’s workflow word queue cannot preserve supervisor attribution and person-first replacement. |
| g | **Another round** | Its contract must agree with the current rule-file design and define consumption of once-only allows. |
| h | **Ready to build after its predecessors** | The installed proof is appropriately bounded; I found no independent defect in that row. |

Reviewed checkout: `31e8acd1a7`; the proposal cites `50055ee6d6`. References below use the proposal’s Driver/Modules/Service path convention. Design line references are to `docs/2026-11-08-supervisor-kinds-design.md`.

The later **D233:45–51** note governs: allows act immediately, and a separately granted supervisor approval may cover an ungranted steward request. Those choices are authorized, not findings against the design.

1. **Blocking — the grant migration permits an older build to misread the new records.**

   **Where:** design §4, lines 263–267; §7.1, line 413. Code: `Roles/RoleRecords.cs:124–128`, `Roles/RoleHold.cs:119–133`, `Roles/RoleCatalog.cs:113–117`; `Modules/Platform/RoleJudge.cs:162–166`; `Store/DesktopStoreReader.cs:14–15`.

   **Sequence:** the new build stores a terminal supervisor deciding only `landing`, with qualified grants. An older build reads it successfully: its parser accepts arbitrary nonempty authority strings and ignores `decides`. Its resolver treats the record as a generic supervisor, and its judge permits a question answer without checking those grants. This is misreading, not refusal.

   A writer-stamp increase alone does not solve this: older readers deliberately read newer stores, and answering a question has effects outside the desktop-store write transaction.

   **Smallest change:** specify an incompatibility boundary existing builds actually obey, covering both reads that authorize effects and writes. Preserve or explicitly refuse pre-migration records, including historical role revisions. Test the old parser and execution path against migrated records; a migration-only test is insufficient.

2. **Blocking — removing a grant need not stop an already-held terminal decision.**

   **Where:** design §2.2, lines 89–93; §3.4, lines 248–252; §4, lines 268–280. Code: `Modules/Platform/RoleTerminals.cs:135–145`, `Modules/Platform/RoleJudge.cs:25–33`, `Roles/SupervisorReleasing.cs:27–45`, `Roles/SupervisorCourses.cs:104–110`.

   **Sequence:** a terminal keeps a granted answer or go-on. During its window, the person removes that grant while retaining the same role and terminal holder. Today, `RolesChanged` preserves the generation when holder and role kind remain unchanged. Release checks admission/generation, not authority, so the decision still acts.

   The proposal adds checks at the judge and keeping, but does not explicitly add the final grant check. Its claim that changing a role ends its hold is stronger than today’s terminal implementation.

   **Smallest change:** revalidate effective role, kind, current grant and subject at the final execution step. Alternatively invalidate affected generations on every authority, scope or `decides` revision, consistently for both holders. Serialize that cutoff with revocation.

3. **Blocking — `subject` does not identify the decision being answered.**

   **Where:** design §2.3, lines 123–136; §7.5, lines 451–454. Code: `Roles/SupervisorProof.cs:153–173`, `Roles/SupervisorCourseForm.cs:6–23`, `Roles/SupervisorKeeping.cs:147–150`, `WorkOrders/ReferredPoints.cs:44–51`.

   **Sequence:** the holder reads question Q1 on a session. The person answers it; that session later parks on Q2, which offers the same label. A delayed form naming only the session, kind and label cannot distinguish Q1 from Q2. The same problem applies to successive referred points, permission requests and workflow attempts.

   Today’s forms carry separate `question` or `point` identifiers. The proposed example makes `subject` the session identifier, removing that protection. Re-reading “the subject” cannot establish which version was originally considered.

   **Smallest change:** define a typed, immutable subject reference: session plus question; line plus referred-point id; session/run generation plus permission request; run plus node and binding/attempt; or file-operation instance. Include it in canonical proof bytes, attempts, keeping and release. No new route is needed.

4. **Blocking — an allow’s scope must follow its effect, and its grant must match its subtype.**

   **Where:** design §3.1, line 208; §3.4, lines 235–246; §5.1, lines 292–300; row e, lines 559–565. Code: `Service/Daoris.Service.Core/Help/RuleProposals.cs:93–99`, `Agents/RuleProposals.cs:150–154`, `:220–230`; `Events/HeldPermissions.cs:152–164`.

   **Sequence:** a session in one repository proposes a machine-wide permission widening. Resolving its repository’s supervisor and invoking the proposal’s yes door changes permissions for every repository. The proposal format already permits this; its origin is not its effect’s scope.

   Separately, the table groups all allow acts together, but accepting a widening installs a persistent rule. Mapping `allow.once` to that yes door would therefore exceed the grant. Conversely, today’s held-request door explicitly refuses standing options.

   **Smallest change:** give each allow subtype an exact effect scope and supported-act table. Resolve against the affected scope: machine changes require the machine’s supervisor; cross-repository effects require a covering scope. Widenings require `allow.standing`; once-only requests use only their offered one-call options. The newly authorized steward approval should bind one proved request and exact operation, without changing the steward’s authority record.

5. **Blocking — dispatching through the person’s route does not make permission decisions atomic or person-first.**

   **Where:** design §3.1, lines 199–200; §4, lines 279–281; §5.2, lines 312–316. Code: `Agents/RuleProposals.cs:220–231`, `:239–251`; `Modules/Bridge/Routes/RulesRoutes.cs:63–75`; `Modules/Bridge/StewardRouteAct.cs:89–93`.

   **Sequence:** the supervisor opens a waiting proposal. The person declines it and receives success. The supervisor then applies its already-read change and marks the proposal accepted. `Answer` performs open, permission save and settlement as separate operations without a shared decision lock or conditional settlement.

   The common dispatcher merely invokes the handler. It supplies neither arbitration nor a durable receipt. A crash between permission save and settlement also leaves an applied proposal apparently unanswered.

   **Smallest change:** use one conditional settlement operation shared by person and supervisor, with source, subject version and idempotency receipt. Check the person’s settlement and current restrictions inside that operation. Recover an interrupted application by receipt instead of blindly applying it again.

6. **Blocking — a supervisor’s pending workflow word can block the person’s replacement and become recorded as theirs.**

   **Where:** design §3.1, line 209; §4, lines 279–280; §7.7, lines 479–480; row f, lines 567–571. Code: `Workflows/Nodes/RunNodeWords.cs:110–127`, `:190–207`; `Workflows/Nodes/NodeRecord.cs:254`; `Workflows/Nodes/NodeRecordFiles.cs:240–250`, `:519–534`; `Workflows/Kinds/Human/HumanNode.cs:54–59`.

   **Sequence:** release queues a supervisor approval. Before the workflow pass consumes it, the person presses Reject. Today, any existing pending word makes the second press fail `worded`, regardless of its author. The supervisor’s approval consequently wins.

   Adding `by: supervisor` only at the route is insufficient: `NodeWord` has no author, its parser reconstructs no author, application records `person-<act>`, and the node move assigns person provenance.

   **Smallest change:** carry source and decision identity through serialization, parsing and application. Under the existing record lock, let the person replace an unconsumed supervisor word. Bind it to the reviewed attempt and acceptance inputs, and define the effect cutoff. Prevent older readers from silently consuming that new word as a person’s word.

7. **Blocking — discarding live authority on restart must not discard final owner decisions or completed attempt outcomes.**

   **Where:** design §2.2, lines 93–95; §2.3, lines 110–140; §7.5, line 454. Code: `Roles/SupervisorSweeps.cs:105–133`, `Roles/SupervisorDecisionFiles.cs:76–83`, `Roles/SupervisorCourses.cs:121–133`, `Roles/SupervisorReads.cs:83–90`.

   **Sequence:** the person overrules an agent’s course decision, or the supervisor leaves the point to them. Restart empties `Courses`. The startup sweep only recognizes question-shaped files, while course files are read as history. The unchanged referred point therefore appears undecided again. Automatic staffing can reconsider what was final.

   A completed attempt whose held result is discarded also needs a defined durable outcome: recording only interrupted attempts as failed does not establish whether another choice may answer that same subject.

   Additionally, state appends are best effort (`SupervisorDecisionFiles.cs:45–54`), so acknowledging an overrule does not prove its closure survived.

   **Smallest change:** durably retain and reload per-subject closure markers for person answers, overrules, leaves and terminal attempt outcomes, independently of live generations. Acknowledgement must follow durable closure. Pending decisions may still become stale on restart; they need not regain authority.

8. **Blocking — the helper exemplar does not establish the promised read-only actor boundary.**

   **Where:** design §2.3, lines 112–122; §7.6, lines 461–469. Code: `Loop/Helpers/HelperTurn.cs:153–171`, `Loop/Session/SessionRules.cs:37–42`, `Participants/Domain/OpinionPosture.cs:4–17`, `Review/OpinionTree.cs:12–16`.

   **Sequence:** an available staffing choice uses a harness with no rule language. The exemplar hands it no permission rules; the posture explicitly says its protection is the copy alone. For a machine-scoped subject, the proposal provides no copy either. Denying connector mutation tools does not stop a native filesystem or command tool from changing permission files directly, bypassing the decision’s grants.

   **Smallest change:** specify the actual execution containment for every supported supervisor harness, including subjects without repositories. Give those turns an isolated working place and enforce their read-only posture independently of model instructions and connector listings. Verify an attempted policy-file write with the real harness. This implements the requested read-only turn, not an additional default restriction on development.

9. **Blocking — row g’s once-only consumption contract conflicts with the current rule-file handoff.**

   **Where:** design §5.1, lines 287–304; row g, lines 573–577. Current counterpart: `docs/2026-10-26-tree-pool-design.md:3423–3443`. Existing analogue: `Roles/StaffMoves.cs:69–73`, `:118–119`.

   **Sequence:** a take encounters an ask, skips the operation and continues. Its supervisor later allows “this take alone,” but that take has already passed the consuming stage. Using the allow at the next take exceeds its identity; refusing it there leaves the acknowledged allow unused.

   The counterpart now explicitly asks for the next start and specifies standing, per-operation grants. The proposal still says §30 has not landed and instead specifies `allow.once` for the current take.

   **Smallest change:** reconcile the two documents before building either contract. Define the operation instance an allow releases, its expiry, and atomic once-only consumption. Either resume that exact pending operation or explicitly authorize the next identified operation. Standing allows remain immediate and revocable as directed.

Both reported defects in today’s code **hold**. `Modules/Platform/RoleJudge.cs:162–166` bypasses supervisor authority; neither question keeping nor course keeping supplies that missing check. `Roles/RoleCatalog.cs:229–230` and `StandingConfirmation.cs:179–180` duplicate scope selection; production admission uses the latter through `RoleJudge.cs:58`.

I found **no additional defect** in the proposed six-step structural resolution order, its refusal to fall through when the selected supervisor cannot act, grant-free leaving, or the separate agent-generation namespace. Today’s question-word path also has substantive person-first protection: `Service/Daoris.Service.Core/Sessions/SessionSays.cs:245–286` and `SaidKeeping.cs:65–74` serialize release and replacement.

No files were changed; no builds or tests were run.

## Opus (independent), round 2

Read on 2026-11-08 by Claude Opus 5.5, read-only, from main at `109487b163`: the sections round 2 names for rows a, c, e, f and g,
with the code they cite and TREEPOOL1 §30.4, against D233's three notes of 2026-11-08 and D214's principle note. No code was built or run.

### Verdict per row

- **a — ready to build.** Finding 7 is small.
- **c — ready to build**, with finding 2's clause in the start's sweep and finding 8 taken in its build.
- **e — another round, narrow.** Findings 1, 2, 4 and 5.
- **f — another round, one sentence.** Finding 3.
- **g — another round, one sentence.** Finding 6.

### Round 1's findings

- **1, 2, 4, 7, 8, 10, 11 and 13: answered.** What remains of the steward's choice is findings 4 and 5.
- **3: answered** for a scoped rule; a default's switch is not one (finding 1).
- **5: answered** on Claude Code and Codex; a `copy-alone` harness is finding 8.
- **6: answered** for a record; the workspace map's own failure is finding 7.
- **9: answered** as listed; the file lists were not read again.
- **12: answered in part**, the declined half argued; nothing to add.

### The author's two readings

- **A `files` allow is the ask's exact text.** It holds, read as the held act's text (`run(npm ci)`), never the `ask` rule's pattern:
  `allow` is read before `ask` (§30.4), so an allow of `run(npm *)` would lift the whole restriction. §30.4's *may be a pattern* is
  then the person's own allow; §5.1 should say both. Its layer is finding 6.
- **The steward's ungranted act waits at most ten minutes.** Only as the request's own clock from its raise: "the actor's turn bound"
  is per turn (§2.5), an attempt starts at the look's `supervise` pass, a limited end takes the next choice (§2.3 point 5), and a
  terminal holder has no turn. Finding 5.

### Findings

**1. Blocking (row e; §5.3). A widening may undo the person's deny, and a default's switch reaches the machine whatever scope it
names.** `RuleProposals.Effect` classes as a widening an `add allow` or `add ask` over a rule in that scope's `deny`, and a `remove`
of a deny (`Agents/RuleProposals.cs:119-142`); `PermissionRules.Add` drops the deny (`Agents/Permissions.cs:404`). A deny asks for no
allow (§30.4), and D233's first note gives a supervisor *an allow an added restriction asks for*. And `permission_propose` takes a
scope for every action (`Service/Daoris.Service.Mcp/RuleProposalTools.cs:27-28`), which a `default` change ignores: `SwitchDefault`
edits one machine-wide list (`Agents/Permissions.cs:419-436`; `RuleProposals.cs:154`).

- *Sequence:* the person denies `Bash(git clean:*)` for repository X. A session in X proposes allowing it for X; X's agent-held allow
  supervisor keeps it under `allow.standing`, and the deny is gone. Or the session proposes `no-publish` off with `scope: repository`,
  `name: X`; X's supervisor decides it, and `no-publish`, which keeps publishing the person's (`Permissions.cs:186-190`), is off for
  every session on the machine.
- *Smallest change:* in §5.3's widening row, a change that removes or moves a `deny` of its scope, or switches off a default that
  denies (`no-publish`, `tree-guard`), is the person's; a `default` switch is placed at the machine whatever scope it names.

**2. Blocking (row e, and c's sweep; §5.3, §2.2). The widening's recovery saves over the person's later edit.** §5.3 saves a proposal
found `settling` again, since `Add` is idempotent; it is so against itself, not against an edit between.

- *Sequence:* at 14:00 X's supervisor allows `add allow Bash(docker:*)`; `Settle` writes `settling`, saves the rules, and Daoris stops
  before the mark. At 14:02 the person runs `daoris agent rules remove`, which acts alone while no Daoris runs
  (`Agents/AgentsCommand.cs:11-13`). At 14:05 the start's tick finds `settling` and saves the allow again: the removal is undone. The
  sweep meanwhile closes the decision `stale`, though its rule stood.
- *Smallest change:* a proposal found `settling` is never saved again: it is marked settled where its change stands in the rules and
  returned to the person where it does not. The sweep reads an allow's own door before closing its decision (the proposal's settlement,
  a `files` allow in the person's list) and records it made where the door holds it.

**3. Blocking (row f; §3.6). A landing's released word outlives the restart that closes its decision.** The word waits on the node
record, a file; the sweep closes its decision `stale`, but only a question's waiting word is taken back
(`Roles/SupervisorSweeps.cs:104`).

- *Sequence:* a supervisor's Accept is released at 14:05; Daoris restarts at 14:05:10 and the sweep makes the review the person's; the
  first pass applies the word as `supervisor-accept`, and the work lands on a decision that reads stale.
- *Smallest change:* in §3.6's *It binds its attempt*, a supervisor's word whose decision is not live in this run is dropped at the
  pass, its decision `stale`; one applied before the restart is recorded made (finding 2's clause).

**4. Blocking (row e; §5.2). The steward's ungranted act: its place, and a landing.**

- *Place.* Given a session, `DependenciesAsync` looks for a quest by the session's id and answers the session alone
  (`Modules/Bridge/SupervisorDoor.cs:201-204`); only the act door resolves a session to its quest first (`:110-132`). Read as §5.2
  cites it, `stop`, `finish`, `land` and `clean` on a session in X name no repository and go to the machine's allow supervisor. A quest
  whose repository does not read answers null, which is not "a target that names no repository". *Smallest change:* the place is read
  as `ActAsync` reads the target, its workspace as §3.4 reads every place's; a dependency that does not read is the person's card.
- *A landing.* `land` lands a tree on its line, which D233's round-2 note holds for the window *since a landed branch is not taken
  back*; allowed under `allow.steward`, it acts at once. *Smallest change:* an allowed `land` waits the allow supervisor's window, with
  *Overrule*, before it runs. Not blocking: whether §2.7's *a deletion* reaches the steward's `delete`.

**5. Blocking (row e; §5.2, §5.3). The ten minutes do not close the supervisor's side before the card.** One request, one id and one
answer become either the steward's own act (`pipe.RunAsync`, `Modules/Platform/PipeAdmission.cs:160`) or the person's press
(`AskThenRunAsync`, `:188`). §5.3 names the settlement "the judge's one admission", but the card is not the judge's.

- *Sequence:* a terminal holding the machine's allow supervisor lists the subject at 14:09:50 and decides at 14:10:05; the card was
  reserved at 14:10:00. The allow runs the act as the steward's, and the person's Confirm at 14:11 runs it again as theirs. An agent's
  second choice, after a first limited at 14:04, answers at 14:12 the same way.
- *Smallest change:* one pending entry under the pipe's gate, settled by the first of an allow, a leave, the steward's withdrawal and
  ten minutes from the raise; the bound closes the subject for the supervisor (`supervisor.dropped`) before the card is reserved.

**6. Blocking (row g; §5.1). Which layer a `files` allow is written into.** The person's `permissions` are the machine's and each
repository's, unioned, with no workspace layer (§30.4); §5.1 says only "the person's `allow`".

- *Sequence:* the machine's layer asks `run(npm *)`. Repository X's start meets `run(npm ci)`; X's supervisor allows it, written into the
  machine's list, the layer whose ask held it. Repository Y's starts now run it unasked, though Y's `files` supervisor is held by rules.
- *Smallest change:* the allow goes into the deciding place's own layer: the repository's for `run` and `copy`, whichever supervisor
  answered by §3.4; for a share, each of the two repositories' layers, since the machine's reaches every repository.

**7. Not blocking (row a; §3.4). A workspace map that does not read falls through to the machine.** `RuleWiderPlaceStore.Read` answers
`None` for a store that fails to read as for one before its step (`Roles/RuleWiderPlaceStore.cs:26-37`), so X's chain skips its
workspace, whose allow supervisor, held by rules, keeps X's allows the person's, and the machine's decides. *Smallest change:* the
resolver holds the place for the person on a map that did not read, as on an unreadable record.

**8. Not blocking (row c; §2.8). Two holes left in the containment.** On a `copy-alone` harness the turn may run `daoris role set` in
its own process and widen its own grants; §2.8 says so, which D233's choice 3 may be read to allow. One check holds on every harness:
the role verb's writes refused from a process in an agents' job, as the pipe refuses a role act from one
(`Modules/Platform/RoleJudge.cs:117`). And the turn's place rests on the tree guard, which the person may switch off; a partner is held
to its copy whatever the defaults say (`Loop/Session/SessionRules.cs:92`), and the supervise turn can be.

### Where I found nothing

- **Row a's order and boundary**: kind first, the admission by act at every step, the cutoff under the decision's lock, and
  `not-converted` before the step, `answer` granting nothing meanwhile.
- **An agent's generation**: never in `Holds`, so a pipe act carrying one is refused (`Modules/Platform/RoleJudge.cs:135-141`).
- **A held request**: one end (`Events/HeldPermissions.cs:216`), its standing options refused.
- **The shared holder**: refused at the hold; an allowed steward act's run reads its generation live again
  (`Modules/Platform/RoleActsLedger.cs:28`).
- **The attempt**: a late, foreign or second block dropped, a closed subject never handed again.
- **The releases**: only the person's stop and pause are read, so a point stays its kind's to release.


## Codex, round 2

Codex's narrow read of rows a, c, e, f and g as round 2 left them, at `30f8e89eff`, run by the steward from outside the install, read-only, without seeing the independent Opus reader's; verbatim but for its headings set down.

Read at `30f8e89eff`. No files changed; no builds or tests run. D233’s latest note settles landings: they retain the window.

| Row | Verdict | Reason |
|---|---|---|
| a | **Another round** | The migration relies on a minimum-build record whose write may fail without stopping conversion. |
| c | **Another round** | Generation and restart semantics are answered; the rule-taking harness’s containment remains incomplete. |
| e | **Another round** | Widening recovery can restore an allow after the person takes it back. |
| f | **Ready to build** | Attribution, replacement, attempt binding and the application cutoff are specified. |
| g | **Ready to build** | The standing allow agrees with TREEPOOL1 §30.4; its stated build dependencies remain. |

My round-1 findings:

1. **Partly answered.** `not-converted`, conversion of historical revisions and rejection of unreached records address the original exposure. The durable launcher boundary still needs the correction below.
2. **Answered.** Admission reads kind, place and act at release, with an explicit cutoff; terminal revocation no longer depends on a sweep.
3. **Answered.** Typed subjects remain whole through proof, attempt, keeping and release.
4. **Answered.** Effect-based placement and subtype-specific grants distinguish held requests, widenings and steward requests. This also requires global default switches to resolve at the machine, regardless of their submitted `scope`.
5. **Partly answered.** Shared conditional settlement addresses competing answers. Its recovery procedure still permits replay over a later withdrawal.
6. **Answered.** The authored node word survives serialization and application, and the person replaces an unapplied supervisor word under the record lock.
7. **Answered.** Every kind and attempt participates in startup closure; interrupted or discarded decisions leave their subjects to the person instead of restarting deliberation.
8. **Partly answered.** Empty attempt folders and truthful per-harness reporting address the missing-place and overstated-posture issues. The explicitly permitted `copy-alone` choice is accepted. The claimed control boundary on a rule-taking harness still has the bypass below.
9. **Answered.** Row g now installs a standing allow for later starts, matching §30.4. No once-only consumption remains to define.

Three blocking findings remain. Code paths below follow the design’s Driver/Modules convention.

**B1 — Blocking, row a: conversion is not conditional on durably excluding older builds.**

**Where:** SUPERVISOR2 §4, migration boundary; `Modules/Update/InstallUpdater.cs:190–198`, `Roles/RoleHold.cs:121–135`, `Modules/Platform/RoleJudge.cs:162–166`; `Install/HomeLastBuild.cs`.

**Sequence:** The update confirms. Keeping its version in the home’s minimum-build record fails; `InstallUpdater` catches that failure and explicitly defers keeping it until another start. Independently, `HomeMigration.RunAfterSwapAsync` proceeds after the swap releases the home. The supervisor step completes, enabling new role writes. The application then ends before another successful stamp. Launching the previous build against that home passes its older minimum-build record. Its parser accepts the qualified authority strings and ignores `decides`; its supervisor judge still bypasses grants.

Thus the role door’s `not-converted` check can open even though the boundary §4 relies on was never established.

**Smallest change:** Before converting supervisor records, successfully keep and verify the release’s minimum-build version. Failure leaves this step incomplete and the role door `not-converted`. Add a proof where that write fails while the migration’s own writes remain possible. This implements the chosen older-build refusal.

**B2 — Blocking, row e: replaying `settling` can undo Take back.**

**Where:** SUPERVISOR2 §5.3, lines 508–515; `Agents/RuleProposals.cs:220–231`; `Agents/Permissions.cs:399–416`.

**Sequence:** A supervisor’s yes writes `settling` and its receipt, then saves an allow. The process crashes before marking the proposal settled. The person removes that visible allow through their rules verb while the application is down. At the next tick, the proposed recovery saves the change again and marks it settled. `PermissionRules.Add` drops the matching rule and adds it again, restoring access after the person withdrew it. If the person replaced it with an exact deny, replay drops that too.

The proposal lock serializes answers to that proposal. The receipt stored there does not establish whether the permission mutation already happened and was subsequently reversed. Idempotence without intervening edits does not preserve a later decision.

**Smallest change:** Save the applied receipt atomically with the permission mutation, and retain its consumed or revoked status when Take back changes the rule. Recovery must complete settlement without reapplying an already-applied receipt. Prove crash → withdrawal → recovery leaves access withdrawn.

**B3 — Blocking, row c: denying management verbs leaves an inherited interpreter allow able to change the controls directly.**

**Where:** SUPERVISOR2 §2.8, particularly lines 249–260; `Review/Domain/OpinionPermissions.cs:100–117`; `Loop/Session/SessionRules.cs`; `Workplace/TreeGuard.cs:14–21`.

**Sequence:** The person’s composition permits `Bash(python:*)`. The supervise shape retains that allow: the reviewer shape removes selected allows and adds named tool, git and connector denies. The added management-verb denies do not match a Python program. The turn runs such a program to modify the home’s permissions directly, widening subsequent access without its decision reaching any kind’s keeping.

The tree guard expressly does not refuse a program’s own file writes, whose targets it cannot read from the command. The classifier’s hard-deny sentence remains the remaining protection for this form. Consequently, checking grants on the returned decision does not bound everything the turn can do.

**Smallest change:** For supervise turns on the rule-taking harness, deny unrestricted shell/interpreter execution, or provide an enforced execution boundary that prevents those writes. Extend the installed proof to an inherited interpreter allow attempting a direct permission-file write. This need not change development sessions or refuse the owner-authorized `copy-alone` choices.

I found nothing further in kind-first resolution, structural refusal to fall through, agent-generation separation, the exact one-request steward approval, or held-request arbitration. I found **no further defect in row f or row g** within the requested scope.
