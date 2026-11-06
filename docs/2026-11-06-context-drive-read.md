# CONTEXTDRIVE1: the other maker's read, taken as an Opus read

Status: read 2026-11-06 (BL57), in Codex's place under D240 point 9, of `2026-11-03-context-counters-design.md` and what was built
against it, at main `928a2ad10`. A read, not a build: nothing here changes code. Read for it: the design and its brief, D229 with its
notes, the backlog's CONTEXTDRIVE1 row, `docs/areas/roles-and-supervision.md`, and the code below. Driver paths start at
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, the modules' at `src/Daoris.Desktop/Daoris.Desktop.Modules/` (written `Mod/`), the web's at
`src/Daoris.Web/src/` (written `Web/`).

## 0. In short

- **One blocking finding** (§1): the window's rules fold keeps only a rule's first role and writes the set back with it, so a rule set
  at the terminal for two roles silently loses one on any save in the window. The installed proof (a rule changed in the window and at
  the terminal) crosses it.
- **Six rows' worth** (§2): two ways to name a line's role for its rules; one set of rules decided two ways (`Decide` has no caller);
  the judge's wider places differ by process and the fold reads only the machine's; the acts say the rule's number, not the count read,
  and a hold is recorded once per new answer; `tries` is ruled but never counted; leftovers of the old numbers.
- **Notes** (§3): `refer`'s latent hazards (j's contract), a held line after its rule is removed, line places, two failure counts that
  part on quiet and cut-off ends, the chip's *held*, Ask Daoris's card and its twin, and design text that D232 superseded.
- **What holds** (§4): the answers-alone admission is one serialized judge with one count, the counts' sources are the executors', the
  supervisor's answer is judged again at its release, and a `count:` source is checked against a fresh read.
- **The rows** (§5): CONTEXTDRIVE1o to t, and k and j rewritten for the code as it stands.

## 1. Blocking

### B1. The fold keeps one role of a rule, and a save writes it back so

**Evidence.** The driver takes a list of roles per rule: the door (`Roles/SupervisorRuleDoor.cs:111-117`), the terminal's
`role rules --roles <id,…>` (`Roles/RoleVerbs.cs:126-127`) and `role_propose`. The fold reads one: `roleOf = rule.roles?.[0]`
(`Web/agents/roles/supervisorRulesDraft.ts:14`, used by `draftOf` at `:17-24`), *Set here instead…* copies the first
(`Web/agents/roles/SupervisorRulesFold.tsx:56`), and `pressOf` writes `roles: [row.role]` (`supervisorRulesDraft.ts:75`). `sameAsStored`
compares first roles only (`:57-63`), so the draft reads unchanged until another row moves.

**Reproduction.** At a terminal, `role rules --place repository:X --roles build,design --set tries=2:weighted --set moves=3:ask`. Open
X's Roles fold: both rules read *for Build*. Change the moves rule to 4 and Save: the press carries `roles: ["build"]` for both, the
judge accepts it (Design's lines fall to the machine's `tries at 5: hold`, which bounds them), and Design's lines on X stop moving on a
transient end with nothing said.

**Severity.** Blocking for the installed proof (the design's row m: *a rule changed in the window and at the terminal*), and a person's
rule lost by an edit to another.

**Row.** CONTEXTDRIVE1o (web): a fold row holds a rule's roles whole (a multi-select, or the row drawn with its roles and kept as read
where the fold cannot edit them); a set with a two-role rule round-trips through `draftOf` and `pressOf` unchanged; a story.

## 2. Worth a row

### R1. Two ways to name a line's role for its rules (D228), parting on a scoped added role

**Evidence.** `SupervisorRuleBook.RoleOf` reads the record's `actor` (`Roles/SupervisorRuleBook.cs:39-44`). It is what the answers-alone
hold reads (`Course/GoOn/AnswersAloneHold.cs:36`, `:78`), what the line's facts read (`Course/LineCountsRead.cs:62-68`: the brief, *Details*,
the head's chip, the supervisor's print and `rule-holds`), and a conversation's rules (`Course/Chat/ConversationRules.cs:28`,
`Course/Chat/LimitRuled.cs:52`). `StaffCourse.RoleOf` reads the start's binding role while the catalogue holds it, else the role taking
the work at the place (`Course/Chat/StaffCourse.cs:71-90`); it is what the planner's park, the staffing course, a run's tries and the
rule acts read (`WorkOrders/StaffSnapshotCourse.cs:23`, `:63`, `:149`; `Loop/Run/Choose/StartAgent.cs:22`; `Loop/Look/RuleActsLook.cs:19`).
Its own comment says why they differ: a driven record's `actor` is the seeded role's id, so an added role is never read from it. The
place parts the same way: the hold and the facts take the record's `workspace` (`Service/Domain/SessionViews.cs:199-200`), the planner
the registry's (`WorkOrders/StaffSnapshotCourse.cs:11-12`).

**Reproduction.** Add a role `build-x` scoped to repository:X taking build. At X set, naming `build-x`, `answers-alone at 4: hold` and
`failures at 5: hold`. Drive a quest in X. The planner parks at 5 failures; *Details*, the brief and the print say *n of 3 (This
Daoris)*; at 3 failures a supervisor's answer is refused `rule-holds` though nothing parks; and the third answer alone is held at the
machine's 2, not 4.

**Row.** CONTEXTDRIVE1p (driver): one resolver of a line's role and place for its rules, read by every reader; `SupervisorRuleBook.RoleOf`
deleted. Proof: a line under a scoped added role read equal by the hold, the facts, the print and the planner.

### R2. One set of rules decided two ways: `Decide` has no caller

**Evidence.** `SupervisorRules.Decide` is the design's act (§4.4-§4.5: a ladder's highest reached `at`, then the narrowest act;
`Roles/SupervisorRules.cs:149-167`). Its only caller is its test (`Daoris.Desktop.Driver.Tests/Roles/SupervisorRulesTests.cs:93`). Every
executor reads the lowest `at` instead: `First`, `Lowest` and `TriesLimit` (`Roles/SupervisorRuleBook.cs:54-64`, `:128-133`), the facts'
watch (`Course/Domain/LineCounts.cs:166`) and the act's rule (`SupervisorRuleBook.Acting`, `:47-48`). Yet the judge accepts a ladder and
two rules at one `at` (it refuses only `off` beside another, `Roles/SupervisorRules.cs:191-193`), the terminal writes ladders
(`Roles/RoleVerbs.cs:167`), and the fold draws every row.

**Reproduction.** `role rules --place machine --roles build --set limits=1:weighted,1:wait:30 --set moves=3:ask` is accepted. The course
reads `weighted`, the first stored; `Decide` would say `wait`, the narrowest. Written the other way round, the course waits. The fold
shows both rows and nothing says which acts. Latent today: `failures-on-choice=1:next,2:ask` on a role with one choice, and any rung
below a `refer` (j).

**Row.** CONTEXTDRIVE1q (driver, web): one way. While no allowed ladder has a reachable second rung (every count's lower rung either
resets the count or holds), the judge refuses a second rule on one count for overlapping roles at one place (a code `ladder`), the
terminal's `--set` takes one step, and `Decide` goes with its test. If j wants a `refer` rung below a hold, j brings the ladder back
through `Decide`, read by every executor.

### R3. The judge's wider places differ by process, and the fold reads only the machine's

**Evidence.** The judge reads a repository's workspace from a map the driver's last look keeps in memory (`Roles/RuleWiderPlaces.cs:24-28`,
kept at `WorkOrders/StaffPolicyLook.cs:14`); a terminal's own process reads `None`, and the terminal judges every `role rules` press in
its own process before sending it (`Roles/RoleVerbs.cs:185`). A line place has no wider place at the judge at all
(`Roles/RuleWiderPlaces.cs:31-37`), though its lines read their repository and workspace (`Roles/SupervisorRules.cs:112-120`). The
window's fold reads only the machine as the wider place and ignores roles in what it draws as inherited
(`Web/agents/roles/SupervisorRulesFold.tsx:39-41`; `Web/agents/roles/supervisorRulesDraft.ts:85-95`).

**Reproduction.** In the window, workspace W (holding repository R) holds `moves at 3: ask`; R holds `limits at 1: weighted` for build,
accepted because R's chain reaches W. At a terminal, any rules write anywhere (`role rules --place machine --set answers-alone=3:hold`)
is refused `moves-unbounded` naming repository:R, judged on [R, machine]. With no desktop running, the converse: a write the window
refuses (R's `failures-on-choice` reaching W's `failures`) is accepted. In the fold, R's rules say *replaces This Daoris's* where they
replace W's, W's rules are drawn nowhere on R, and a Build-only rule on a count at R hides the machine's every-role rule that R's Chat
lines still read (the design's own §4.4 example).

**Row.** CONTEXTDRIVE1r (driver, web): a place's chain read from one durable source in every process (the registry's record, never the
last look's memory), `ROLES` answering each place's chain, and the fold drawing what each place replaces and inherits from it, by role.

### R4. The acts say the rule's number, not the count read, and one hold is recorded as several acts

**Evidence.** `RuleActed.N` is meant as the count read (§4.7). The answers-alone hold passes the rule's `at` (`Course/GoOn/AnswersAloneHold.cs:80`);
`PlannedRuleActs` passes `n ?? rule.At`, with `n` null for the transient hold and the moves bound (`WorkOrders/PlannedRuleActs.cs:25`, `:36`,
`:39`) and, for a park, `Consideration.Failures`, the records' count before *Try again*'s mark (`WorkOrders/Domain/RoundConsidering.cs:95-109`).
A new answer arriving while a line waits marks it again and records another act (`AnswersAloneHold.cs:56-68`), and *Details* says how
many times the rules acted (`Web/work/head/lineSupervision.ts:71-80`).

**Reproduction.** A quest parks at 3 failures; *Try again*; three more fail. The note and the decision file say *failures stood at 6, and
the rule says hold at 3* (`Roles/SupervisorRuleActs.cs:68`), *Details* says 3 of 3 (`Course/Domain/LineCounts.cs:93-94`), and the
supervisor's print hands both. A line held on answers alone that receives two more answers while it waits shows *your rules acted 3
times*.

### R5. `tries` is a ruled count that nothing counts

**Evidence.** The seed and the window hold a `tries` rule, but `LineCounts.Of` reads no `tries` (`Course/Domain/LineCounts.cs:55-61`,
`:79-108`): no line says its tries, the chip never names one, and `rule-holds` never sees a tries hold (`Roles/SupervisorRuleHold.cs:12-14`).
The design's shown counts are not read either: `tokens`, `minutes`, `quests-handed`, `results-waited`, `refusals.*`, `over-budget`,
`posts`, `starts` (§2.2; §3 point 1's *This part: 34 minutes, 118,000 tokens reported*; §7.4's *Details*).

**Row for R4 and R5.** CONTEXTDRIVE1s (driver): each act records the count its executor read (the park's count past the mark, the
admission's count, the episode's tries, the line's moves), once per hold; `tries` read from the newest part's `ended.transient` count.
The shown counts the design lists and no reader asks for leave the design (§3 N8) rather than wait in a row.

### R6. Leftovers of the old numbers (D228)

**Evidence.**
- Two seeds: a fresh home's `Seeded()` holds no moves rule (`Roles/SupervisorRuleBook.cs:103-110`, written by `Workflows/HomeSeed.cs:26`);
  the migration writes the same plus `moves at 3: ask` (`Transitional/Migration/RulesStep.cs:29-30`, `:38-39`). A fresh and a migrated
  home hold different machine sets, against §6.1; on a fresh home a person's first move rule is refused `moves-unbounded` until a moves
  rule is added with it.
- The constant bound beside the rule: `AnswersAlone.Bound`, `Holds` and `AtBound` (`Course/Domain/AnswersAlone.cs:22`, `:35-36`, `:43`)
  have no caller outside tests; `CourseAlone.Bound` defaults to it (`Course/Domain/CourseFacts.cs:192`); `HeldAt` falls back to it where
  no rule holds (`Course/GoOn/AnswersAloneHold.cs:40`).
- *Daoris's own number*, dead since row e: `CountWatch.Place` may be null (`Roles/Domain/CountTexts.cs:22-23`), the print says *Daoris's
  own number* and, where the records did not read, *No rule of the person's is in force on this line: Daoris acts on its own numbers*
  (`:230`, `:236-238`; `Course/LineCountsRead.cs:62-65`), which is false; the web keeps `ownAt` (`Web/work/head/lineSupervision.ts:62`).

**Row.** CONTEXTDRIVE1t (driver, web): one seed list for a fresh home and the migration; the constant and its readers gone; the
*own number* branch gone, and a line whose rules did not read says so; with §3's N3 and N4.

## 3. Notes

- **N1. `refer` is latent at every executor (j's contract).** `AnswersAloneAt` and `FailuresAt` read only `hold` (`Roles/SupervisorRuleBook.cs:54`,
  `:57`): `answers-alone at 2: refer` alone would leave answers alone unbounded, since an admission handed no `at` admits every go-on
  (`Course/ContinuationAdmissions.cs:253`). `SupervisorRuleBook.Course` hands the first rule's `do` to the staffing course as a move
  (`:74-78`), so `refer` would reach `StaffJudge.OnCondition` as an action it does not know. `ReferOffered` is set nowhere
  (`Roles/SupervisorRuleDoor.cs:63`); `ROLES` lists `refer` (`Roles/SupervisorRuleRows.cs:16`) and the fold strikes it by name
  (`Web/agents/roles/SupervisorRulesFold.tsx:116`): two places say whether it is offered.
- **N2. A held line after its rule is removed.** The mark stays, as §7.1 says, but `HeldAt` names the constant 2 (`AnswersAloneHold.cs:40`)
  and the chip and *Details* show no rule. Nothing acts on the stale reading; what is said is stale. With t.
- **N3. Line places.** No retention removes a `line:<key>` place's rules (§4.4's last point; `supervisor_rules` is read only by its store and
  its step), places are judged well formed, never as driven (`Roles/SupervisorRuleDoor.cs:63` sets no `Known`), and a line's rules are
  set only at a terminal or by a card, drawn in the window only as *this line*, yet weighed on every write: a refusal can name a place
  the window cannot open. With t: retention, or line places refused until a reader wants them.
- **N4. Two failure counts part on quiet and cut-off ends.** `Strikes` counts a cut-off (`stopped`, interrupted) and a quiet end
  (`Service/SessionReads.cs:112-117`); `FailedRuns` counts a quiet end as a failure on its choice and starts again on a cut-off (`:138-155`);
  the course never treats a quiet end as `failed` (`WorkOrders/StaffSnapshotCourse.cs:33-35`). One quiet end can feed both the `quiet` rule
  and the next `failures-on-choice`. With t: one definition.
- **N5. The chip's *held*.** It is the count at the rule's number (`Web/work/head/lineSupervision.ts:95`), which for answers alone and moves
  means the next go-on or move waits, not that the line waits now; it reads no hold. Its words are neutral (*answers alone 2 of 2*); the
  `data-supervision-chip="held"` and the weight say more.
- **N6. Ask Daoris's rules card and its twin.** The card's sentence is the terminal's codes (`Help/Proposals/HelpRoleProposals.cs:163-166`).
  Its twin writes one command per group of roles (`:169-179`), each a whole judged write, so a card whose move rule names Build and whose
  moves rule names every role is accepted as a card and refused at its twin's first command (`moves-unbounded`). → k.
- **N7. Cost of a look.** The rules are read from the store again for each quest (`Roles/StaffLedger.cs:219` through
  `Course/Chat/StaffCourse.cs:21-22`), so one look can decide two quests by two revisions; the acts' pass reads each held session's
  decision folder at every look (`Loop/Look/RuleActsLook.cs:29`).
- **N8. Design text superseded.** §6.2-§6.4 (the conversion door's `rules` part, reached homes, `convert-first`, the census and its kept reads)
  are D232's migration step and l's deletions now; §6.1's policy moves are passed over (the area, point 35); §2.2 names the executors
  among `LineCounts.Of`'s readers, but they read the same sources directly, which agree except where R1 and R4 part them; the shown
  counts of R5 are unread. The design's status line names none of this. The backlog's letters also collide with §9's: the row calls l
  and m built (the old doors and the policy's `moves` and `most` gone) and keeps *the installed proof of l and m*, which is §9's m.
- **N9. The area's contradiction 3** says `daoris role rules` is printed nowhere; Ask Daoris's role cards print `daoris role …`
  (`Help/Proposals/HelpRoleProposals.cs:173-175`, `:249-254`).

## 4. What holds

- The answers-alone count has one source and one judge: every path goes on through `ContinuationAdmissions.Admit` under its lock and the
  store's transaction (`Course/ContinuationAdmissions.cs:126-157`), the same go-on is admitted again without counting twice (`:237-258`),
  and the facts, the hold and the supervisor's `AtBound` read the same count (`Course/LineCountsRead.cs:92`, `AnswersAloneHold.cs:123-127`).
- The other counts read the executors' own sources: the failures past *Try again*'s mark (`LineCounts.cs:93-94` against
  `RoundConsidering.cs:95-97`), the moves less approvals (`Roles/StaffMoves.cs:54-67`), and a move is decided and written under one lock
  (`:86-92`), so two looks cannot both pass the bound.
- A supervisor's answer is judged again at its release with the rules and records read that look (`WorkOrders/SupervisorLook.cs:21-27`),
  and a `count:` source must match a fresh read (D229's i note).
- A place's rules are written whole, judged inside the write's own transaction against the revision read (`Roles/SupervisorRuleStore.cs:72-98`).

## 5. The rows

| Row | Lanes | Finding | Contract | When |
|---|---|---|---|---|
| **CONTEXTDRIVE1o** | web | B1 | a fold row keeps a rule's roles whole; round-trip test; a story | before the installed proof |
| **CONTEXTDRIVE1p** | driver | R1 | one resolver of a line's role and place for its rules; `SupervisorRuleBook.RoleOf` gone | now |
| **CONTEXTDRIVE1q** | driver, web | R2 | the judge refuses a ladder (`ladder`); `--set` one step; `Decide` gone | now, before j |
| **CONTEXTDRIVE1r** | driver, web | R3 | a place's chain from one durable source in every process; `ROLES` answers it; the fold draws from it by role | now |
| **CONTEXTDRIVE1s** | driver | R4, R5 | each act's count as read, once per hold; the `tries` count | now |
| **CONTEXTDRIVE1t** | driver, web | R6, N2-N4 | one seed; the constant bound and *own number* gone; line places kept or refused; one failure definition | now |

**k, rewritten.** Ask Daoris's rules card carries the place and its rules before and after as fields, not `RulesPlan`'s sentence, and
the window draws it in the fold's words in English and Chinese; its terminal twin is one command and one judged write (the `role rules`
grammar takes roles per `--set`), so a card the judge accepts is never refused at its twin; the room's goldens at SCOPEVERIFY2.

**j, rewritten.** `refer`, with the supervisor's activation (SUPERVISOR1a-iii) and the actor holder (SUPERVISOR1c), after q: at each
executor a `refer` holds behind its count's own release door until the *course* act decides (the answers-alone mark, the park, the
transient hold, `policy-ask`), never read as a move by `SupervisorRuleBook.Course` nor as no hold by `AnswersAloneAt` and `FailuresAt`;
`refer` offered by the activation, with `ROLES` saying whether, so the fold stops striking it by name; the `course` act, its defer bound,
one point referred once and read again at its release; `SupervisorRuleHold` counting a referred hold. A `refer` rung below a hold, if
wanted, brings the ladder back through `Decide` (R2).

**The backlog row's new text.** *CONTEXTDRIVE1 — behaviours counted in a session's context, and a supervisor that acts on them* (driver,
modules, web; D229): a to i, k's words, l, n and the old doors' deletions are built, and the owed read is done
(`docs/2026-11-06-context-drive-read.md`). Left: **o** the fold keeps a rule's roles whole (before the installed proof); **p** one
resolver of a line's role and place for its rules; **q** one decide (a ladder refused, `Decide` gone); **r** a place's chain from one
durable source, at the judge and in the fold; **s** each act's count as read, once per hold, and the `tries` count; **t** the leftovers
(one seed, the constant bound, *own number*, line places, one failure definition); **k** the rules card in catalogue words, its twin one
write, the room's goldens at SCOPEVERIFY2; **j** `refer` with the supervisor's activation, after q; the installed proof (§9's m), after
o. → `docs/2026-11-03-context-counters-design.md` §9; this read; `docs/areas/roles-and-supervision.md`, Open
