# A repository's integration is a fabric workflow Daoris runs (INTEGRATE1, 2026-10-28: round 4, proposed; round 3 accepted)

**Standing (round 4, proposed under D191, 2026-10-28; D215 re-opened):** the owner's direction of 2026-10-28 (§0.1 point 3) re-opens
round 3's fixed cycle: how work moves is a flow each workspace configures from step kinds, and integration is a step in it. Round 4 is
written by Claude Opus 5.5 from round 3's tip `4b9621c37`, whose `src/` is `7ebeba1ba`'s, on the branch `parent/integrate1-flow`. §17 says
what it decides: a unit's whole flow is its chosen workflow's; `integrate` and `request` are graph node kinds; the work's own requests are
drawn and may hold it; two built-in presets; the install's policy split between the unit's step and the line's rule. Each section it changes
is edited in place and listed in §17.12 with what it replaced. The line's run keeps its mechanics as round 3 accepted them (§5.2 to §5.7,
§5.9), its request now made by each unit's step. Rows a, c, d, j, b, e and f are unchanged; g, h, i and k change and n to r are new,
waiting for the steward's settlement after Codex's and Claude Fable's reads. Nothing is built.

**Round 3's standing, as it was written (accepted by the steward, 2026-10-28, under D191; round 3, D215):** three makers read it. Claude
Opus 5.5 wrote it. Codex and Claude Fable each read round 1 (each *another round*) and round 2 (each *accept after named changes*).
Round 3, from main at `7ebeba1ba`, makes the second reads' named changes (§16):
- **A steward's request never lands a unit** (F31).
- **The leftovers' admission path**, an existing recorded landing bound to its unit and its exact accepted tip, its tidied-tree case among
  them (F32, C27).
- **The live tool runner's upgrade before the flip** (C13).
- **Fable's F33 to F39**.

The steward settled the readers' disagreements (§15.2) and filled §14's verdicts. Nothing is built; FOUNDATION1 (D212) stays the building
priority, and §11 marks the rows that can run beside it.

**Round 2's standing, as it was written (proposed under D191, D215):** **Both makers read round 1** at `6f1ee69cb`, each without the other's read. Codex asked
another round: 26 findings, 10 blocking (C4 to C13). Claude Fable asked another round, narrow: 30 findings, 6 blocking (F5 to F10), the body
standing in substance. **Round 2 is written by Claude Opus 5.5 from main at `7ebeba1ba`**, unchanged since round 1. Every finding of both was
checked against the code before it was taken (D191 point 2); each is a row of §14 with the file and line it was checked at, and each changed
passage below is marked *(Cn)* or *(Fn)*. Every blocking finding is taken. Two non-blocking ones are taken in part (F12's mechanism, C25's
`probe`), each said in §14 and §15. Where the reads differ on a choice, §15.2 gives both and proposes one for the steward. The main changes:
- **Integration rides a branch rule** (C9, F8): an older build makes the branch and nothing more, and `work.landed` keeps its meaning.
- **No node is added to any run's graph** (C10, F21): integration is a transport outside every run, observed by `line` as a person's merge is.
- **One admission path**: every request is the unit's own landing through its gate at the pinned commit (C5, C11). The steward's acts are
  re-checked at promotion (C12, F9), and both integrators are fenced at promotion (C13).
- **The strict failure policy is the permanent default** (C6, C16, F14, F19). The whole admission is held by ordinary entries an older writer
  counts, and a confirm run whose admission cannot be shown is inconclusive (C7).
- **The runner contract is D192's, extended** (C8, F3, F5, F6). D192 is amended by name only where a caller hands the runner its tree.
- **Smaller pieces:** containment in an unpromoted member is provisional (C4); the commit check moves into the request step (F7); a line
  rule settles only by a writer (F10, C21); the landing agent is deferred (C19, F25).

The rows are re-ordered (§11, §15.1). A second read can be confined to the sections §15.3 lists. Nothing here is built, spends an account,
signs in, pushes, publishes or deletes. FOUNDATION1 (D212) stays the building priority: §11 marks the rows that can run beside it. The decision
is [D215](decisions/D215.md).

**Round 1's standing, as it was written:** written by Claude Opus 5.5, maker 1 of 3, from main at `7ebeba1ba`, reading the three in-flight
branches of §1.4 by `git show` alone; Claude Fable and Codex to read it next, each without the other's read, and the steward to settle it; a
foundation change, so no part accepted on one maker's read; nothing built.

**In one paragraph.** A repository's integration is declared in two halves:
- **The repository's facts**, in its `daoris.gates.json`: `integration` (the gates that run at integration, the commit check, its
  disposable scratch, the files whose rows merge by base or whose lines a writer recomputes, the merge message, who integrates it) and D192's
  `verification` runner, extended. The owner accepts them by their blob, in an acceptance of their own.
- **The install's policy**, on a branch landing rule: `integrate` (who requests, how each member lands on the line, the batch, the order,
  what a failure that does not reproduce does, where a conflict of meaning goes). An older build reads the rule as the branch rule it is and
  makes the branch, nothing more.

Daoris runs one integration run per line. Its fixed steps (request, form, replay, resolve, conflict, gates, judge, promote, settle, tidy,
close) are each a fabric event the page shows. The run stays a transport outside every run's graph, and each unit's own flow requests it
*(R4)*: the flow its workspace or repository chose holds an `integrate` step after its landing, which requests the line's run through the one
door and waits on it, and its `line` observes the promotion as it would a person's merge (§17).
- **Requests**: a unit is requested by its own landing through its gate at the commit it pins, or, where that landing was already made, from
  its recorded landing at its exact recorded tip; a records branch on the owner's card. The steward requests, and never lands. Each revision
  is requested again by the same rule.
- **Conflicts**: what is mechanical is settled by declared rules. A member contained in another member waits for that member's promotion.
  A conflict of meaning goes to the member's session, or holds.
- **Gates**: they run in the candidate's own job, under the machine's one admission, in an environment Daoris composes, through the
  repository's runner by D192's contract extended. The runner reads its commands from the accepted declaration, never from the members' copy.
- **Failures**: a failure is confirmed before it is blamed. The failing run is repeated once, as composed, holding every admission place. The
  strict policy stays the default until the owner chooses otherwise, and only where a whole-set verification runs on the line.
- **Promotion and after**: promotion re-checks who integrates the line and every requester's authority. Later commits settle as contained,
  superseded or new, never as waiting work.

`tools/merge-branch.mjs` keeps this repository's gate plan as its declared runner. Its queue retires at a fenced flip that the owner's single
sitting prepares. Beside it stands the owner's second direction of the day, this design's principle: the procedures the development's day
depends on are product, not scripts (§3, §7).

## 0. What this rests on

### 0.1 The direction

Three directions of the owner's, 2026-10-28, in this design's words (the third re-opened round 3, §17):

1. **Integration is Daoris's to complete.** Merging should not need a queue the parent runs by hand: Daoris gets a proper merge step and
   carries it to its end, with the deeper git-workflow logic configured in the fabric. A merge task becomes a landing task that includes what a
   person would otherwise do around it, rebasing when the line moved among it.
2. **The foundation moves away from unmanageable scripting.** What the development's day depends on is built as product: fabric workflows with
   steps, events and tests, shown on the page and operable by the appointed steward. Contributor tools stay for the checks and generators that
   read the repository.
3. **How work moves is a flow each workspace configures** *(R4, §17)*, not a fixed cycle (start, working, landing) that integration then
   follows from outside. A flow is composed from step kinds: a line-integrated workspace (a family of game repositories) runs start, work, merge
   into the line (integration, with its replay when the line moved and its gates), completed; a pull-request workspace (the work workspace) runs
   start, work, a pull-request branch, completed. Steps inside the work may ask another repository or agent (a quest), shown and waited on as
   part of the flow, and later kinds (a push, a release) extend the same flow.

FOUNDATION1 (`docs/2026-10-25-foundation-design.md`, D212) stays the priority for building; this design may be written now and is built after
or beside it as the steward sequences.

### 0.2 What was read

The brief and `docs/development.md`; the discovery skills' matches (`autonomous-development`, `claims-need-checks`, `development-documents`,
`held-clocks`, `ref-removal-transactions`), and for round 2 `compatible-builds`, `session-record-fields` and `pushed-slices`; D87 and its notes
(LANDSYNC0 to LANDREBASE2, GENREGEN1, LANDBACKLOG1), D145, D164, D173's fabric events design parts B, C and E
(`docs/2026-10-16-fabric-events-design.md`), D190, D192 and its pipeline design §4, D207, D209, D191, D212's §4 and §14, the event hook design's
kinds; `tools/merge-branch.mjs` (its header, the queue's sections, the commit check, the private files' copy, the prune) and
`tools/merge-queue.mjs` whole, the driver's landing in `Workplace/`, `Settings/` and `Workflows/` at `7ebeba1ba`; the in-flight branches by
`git show`; the queue's own records of 2026-10-26 to 28 under the main checkout's ignored `local/` (read, never written); the backlog rows
QUEUEDRAIN1, HEAVYQUEUE1, TIDYOUTPUT1, RUNENDS1 and DOGFOOD1; and both reads of round 1 (§14). The integration reviews of 2026-10-05 and
2026-10-10 were not read past the router's rows (§13). Every `file:line` below was read at `7ebeba1ba` unless it names a branch.

**Round 4 read** *(R4)*, at `4b9621c37`: the brief and the discovery skills' matches (`development-documents`, `claims-need-checks`,
`autonomous-development`, `compatible-builds`); the router; D157 whole and its design's §2 to §4; D190 whole; the fabric events design's §C2,
§E2, §E6 and §E7; this design whole and D215; D148, D164 and D192 where cited; D173's part E note; FOUNDATION1's §14. The code read for it:
`Workflows/Domain/WorkflowNamed.cs`, `WorkflowPresets.cs`, `WorkflowSelection.cs` and `WorkflowRun.cs`; `Trees/Nodes/NodeContract.cs`,
`NodeGraphs.cs`, `NodeRuntime.cs`, `NodeWires.cs` and `BuiltInNodes.cs`; `Settings/LandingKey.cs`; `Service/Domain/QuestViews.cs`;
`Sessions/Domain/AskWork.cs`.

## 1. Today's paths

### 1.1 This repository: a branch the install lands, a queue the parent runs

- **The install lands, never merges.** Its rule for this repository is `branch quest/{slug}-{quest}` (TASKS.md:602, DOGFOOD1). The parent then
  integrates every branch, the install's and its own subagents', with `tools/merge-branch.mjs` (`docs/development.md:139-286`).
- **Preview and freeze.** `--preview` merges with `git merge-tree` against main's tip and prints the diff, any conflict the resolvers cannot
  take, and the scoped run (`merge-branch.mjs:6679-6712`, the blocking set leaving out union records, generated folders and the backlog at
  `:6691`). `--message <file>` runs the commit check (every commit of the branch's own carrying an allowed maker's `Co-Authored-By:` line, and
  nothing left uncommitted in its worktree, `:2836-2858`, `:6725-6728`), then freezes the tip and the message's bytes, refusing a tip other
  than the last preview's (`merge-queue.mjs:131-145`; `merge-branch.mjs:6715-6740`).
- **A runner the parent starts, detached.** The first enqueue starts `--queue-run` detached and unreferenced (`merge-branch.mjs:6651-6673`,
  `:6657-6658`), so it outlives the invocation; it is spawned with no environment of its own and inherits the parent's (`:6657`), adds only the
  build-server isolation (`:6636`, `BESIDE_ENVIRONMENT` at `:5111`), and every gate's shell inherits that again (`runStep`, `:3568`) *(C2)*.
- **The candidate.** Up to eight queued requests in arrival order (`merge-queue.mjs:72`, `:182-185`), each member's **tip** merged `--no-ff` on
  the last with its frozen message in `local/scope-tree` (`mergeMember`, `merge-branch.mjs:6239-6258`, the merge at `:6241`) *(F27)*. The
  resolvers: the backlog by its merge base (`resolveBacklog`, `:1522-1544`; `mergeByBase`, `:1485-1510`), the declared generated folders written
  again (`:1360-1389`), union records by their attribute (`.gitattributes:39-43`) with what union glued set apart (`setApartJoins`,
  `:1624-1644`), the router by its row driver (`.gitattributes:50`). Any other conflict, and a merge commit git will not make (an empty merge
  among them, `:6252-6256`), sets the member aside and hands it back (`formCandidate`, `merge-queue.mjs:192-209`). The tree is given the
  checkout's private files the gates read (`copyPrivateFiles`, `merge-branch.mjs:5027-5048`, at `:6360`) and reads its gate plan from its own
  tree (`readPlan(tree)`, `:977-980`, at `:6361`), so the plan is the candidate's, the members' declaration included *(F6, F20)*.
- **The scoped set.** The cheap tier always, each reached gate, the Process halves by class, the git half within eight minutes, browser specs
  and rehearsals deferred (`scopedSet`, `merge-queue.mjs:441-473`; `gitBudget`, `:418-432`; `runCandidate`, `merge-branch.mjs:6357-6406`). The
  admission record notes only the scoped tier's state (`:6400-6406`); the background run does not read it and gates run in no job object yet
  (`docs/development.md:283-286`).
- **The judge** (`judgeFailure`, `merge-queue.mjs:293-333`; D207 point 3). A death of memory holds the candidate (`merge-branch.mjs:6420-6424`).
  An unexpired known red last seen on the base is inherited with no base run; otherwise the failing selection runs on the base first, where a
  gate naming no test that fails stops the queue, a test is inherited, and a test known red past 72 hours stops it (`merge-queue.mjs:300-312`;
  D207's two exceptions, `docs/decisions/D207.md:139-148`) *(C14)*. Then the whole candidate and every prefix (`:313-316`), the selection being
  the failing test's class alone (`prefixCommand`, `merge-branch.mjs:6308-6317`), the last transition pinned and its neighbours run again
  (`merge-queue.mjs:319-325`). A whole candidate that does not fail again, a prefix that cannot run, or neighbours that disagree leave the
  failure unresolved (`placeOnPrefixes`, `:271-281`): a candidate of several then runs its members one at a time, one of one is handed back as
  unresolved (`candidateOutcome`, `:363-394`; `judged`, `merge-branch.mjs:6609-6613`), and by D207 "a passing re-run" never makes a candidate
  green (`docs/decisions/D207.md:63-65`), nor may it be re-formed until a run passes (`:163`).
- **Promotion.** Main fast-forwards to a green candidate only in a clean main checkout on `main`, held and tried each tick otherwise, re-formed
  when a direct commit moved main (`merge-branch.mjs:6516-6550`; `promotion`, `merge-queue.mjs:246-250`). The loop reads and promotes on its own,
  whatever any later invocation does (`merge-branch.mjs:6494-6554`) *(C13)*.
- **A drop.** `--drop-batch` takes a request off, or ends the candidate and hands its members back (`dropQueued`, `merge-queue.mjs:151-160`);
  nothing ends the gates already running (QUEUEDRAIN1, TASKS.md:22).
- **Its own flake rule.** For a merge in place, a failure outside FLAKE1's classes is never re-run, because a test that passes alone after
  failing in its suite may be polluted by another, which is a defect (`merge-branch.mjs:245-251`).
- **After it.** The CI watch gates main's newest tip each time it polls (`watchCi`, `merge-branch.mjs:6031-6066`), so promotions made while a
  run runs are taken together, and an ordinary run keeps standing verdicts and runs the Process halves by reach, the whole set being the nightly
  run's and the sweeps' (`docs/development.md:288-361`) *(C3)*. An install is staged only on the background run's receipt (D207 point 5).

### 1.2 Other repositories: a merge on a press, a rebase by words, no gates

- **Two forms.** A rule merges into the line or makes a branch (`Settings/LandingKey.cs:11-18`), with its pattern, tidy, plugin, automatic
  acceptance, ticket key, squash and post-landing cycle (`:48-61`). A merge rule never accepts automatically: both doors refuse it
  (`LandingKey.cs:118-120`; D145 point 1, `docs/decisions/D145.md:16-28`, for *no human step after it*, `:70`), and the look closes such an
  entry as off (`Workplace/AutoLander.cs:188-192`). So a merge lands on a press, the person's: landing is no role's act (D209 point 3,
  `docs/decisions/D209.md:63-65`).
- **The press.** The plan says what would land (`Workplace/TreeLands.cs:52-84`, `:111-151`). The landing reads the head's own judgement first
  and refuses work the line already holds (`:525-530`; `:258-262`, *nothing to merge*); it judges what the line holds commit by commit
  (`LineHolds.JudgeAsync`, `Workplace/LineHolds.cs:73-116`; at `TreeLands.cs:578`) and refuses a tree it holds wholly (`:583-591`); it pins
  the commit a graph run's gate judged and refuses a branch that moved (`:598-604`); it replays the rest aside onto the line's tip where it moved
  (`:607-612`; D87's LANDREBASE1 note, `docs/decisions/D87.md:183-221`) and merges the **replay's tip** `--no-ff` in the repository's own
  checkout (`:287-289`, `:614`), only while it is clean and on the line, read immediately before (`:264-282`), a failure aborted and the
  checkout left as it was (`:290-303`) *(C1, F1, F27)*.
- **A conflict.** The replay refuses; the window offers the session rebase words, and the automatic and terminal landings say them as
  Daoris's words keyed by the tip (`Workplace/RebaseTelling.cs:16-26`; `docs/decisions/D87.md:284-295`). Generated files take the line's copy
  and are written again under the person's acceptance of the declaration's blob (`:232-255`); the backlog settles by its base (`:257-282`).
- **No gates.** The landing gate is the run's workflow part, the second opinion and the review (`Workplace/TreeGates.cs:40-57`), the graph's
  part binding the run, the branch and the commit (`Workflows/Domain/GraphGate.cs:113-140`); the session's own checks are trusted (D192 point 4).
- **The tidy.** A tree goes with the rule's tidy only with nothing in flight (`Workplace/TreeTidies.cs:43-50`), its containment git's ancestry
  (`:43-45`); ignored files under thirteen build-folder names go with it, and any other ignored file written since the tree opened keeps it
  (`Workplace/TreeScratch.cs:15-19`, `:36-58`; TREEKEEP1). A repository cannot say that its own verify output is scratch (TIDYOUTPUT1,
  TASKS.md:31). A work held on the line by content leaves the review (REVIEWLANDED1, `Workplace/LandedReview.cs`; D102's note, `:465`).
- **After landing.** `trees land --plan` lists commits the line lacks as work to land (`docs/development.md:235-241`), so a session that goes on
  after its landing makes commits that read as waiting.

### 1.3 What the fabric already holds

- **A rule carries what follows the landing.** `cycle` on the landing rule is a graph fragment (`Settings/LandingKey.cs:54-60`; the fabric events
  design §C2 and §E7, `docs/2026-10-16-fabric-events-design.md:529-611`, `:1784-1791`), inherited whole with the rule (D164). A unit's own
  landing graph is `handed` then `line`, with `pushed` and `pull-request` between them where a branch rule names a plugin
  (`NodeGraphHomes.Landing`, `Trees/Nodes/NodeGraphs.cs:747-762`).
- **Nodes and their authority.** `handed` and `line` are observed by git's proof (§E2's table and §E4, `:1464-1466`, `:1520-1534`;
  `Trees/Nodes/NodeContract.cs:433-435`). `work.landed` is EVENTHOOK1's: the install landed a session's work as a branch the line does not hold
  yet, told from the automatic landing's try (`docs/2026-10-24-event-hook-design.md:490`, `:496`), never for a merge rule; FOUNDATION1 keeps
  its kinds' meaning through its adapter (`docs/2026-10-25-foundation-design.md:1169`) and names `work.integrated` once the line holds the work
  (`:1157`) *(F4)*.
- **Two engines.** A run bound to a schema-1 version stays D157's to its end; no run is governed by a decoded graph; a steps version reaches the
  runtime only as a new graph version a person saves (D190 point 1, `docs/decisions/D190.md:13-18`) *(C10)*.
- **A performed check on the line.** D192's verification pipeline: a `check` Daoris performs, a run per batch on the line run to its verdict
  (D192 points 6 and 7), its gates the declaration the person accepted by its blob **on the landed route** (point 2;
  `Workflows/Domain/WorkflowSelection.cs:52-56`; `Workflows/LandedGates.cs:12-19`, `:80-88`), its runner `verification` answering by file
  (`docs/2026-10-20-pipeline-design.md:319-343`) and owning its checkout (`:366-368`), an automatic bisect rejected for landed work
  (`docs/decisions/D192.md:90-91`). FABRIC-PIPELINE1d to 1j are open (TASKS.md:176-181) *(F2, F3)*.
- **Twins already made.** The driver's `BacklogMerge` (`Workplace/BacklogMerge.cs:22`) and `GeneratedWriters` (`Workplace/GeneratedWriters.cs:21`)
  twin the tool's backlog and generated resolution for a landing's replay; the writers run only under the landed route's acceptance
  (`GeneratedWriters.cs:44-52`).
- **The environment.** A start drops the role variables it inherits and keeps `DAORIS_HOME` (`Agents/Adapters.cs:1661-1677`; ENVLEAK1,
  `docs/FIX-LOG.md:2930-2941`).
- **The map.** Settings holds the keys' shapes (`docs/2026-10-25-foundation-design.md:2335`), Workplace the trees, git and the landing
  (`:2342`), Review the heavy gates, checks and a check's verdict (`:2348`), Workflows the node runtime (`:2350`), Events the vocabulary
  (`:2339`), Roles the role catalogue (`:2343`); a subdomain names only lower ones, and a `LoopContext`-built executor stays in Loop
  (`:2363-2373`).
- **Settings keep what a build does not own.** A write of `driver.json` keeps every key the writing build does not know
  (`Settings/DriverConfig.cs:938-944`), but a build reads a rule by the fields it knows (`LandingKey.cs:359-388`) *(C9)*.
- **A graph already orders what the gates do not** *(R4)*. Its rules hold only that an `opinion` and a `look` reach every `handed`
  (`protected-order`, `protected-wire`, `Trees/Nodes/NodeGraphs.cs:241-288`), and a named version's graph one `handed` and at most one of each
  gate (`Workflows/Domain/WorkflowNamed.cs:296-309`); a steps version keeps the runtime's fixed order (`OrderProblem`, `:623-688`) and reads no
  kind outside its table (`:397`; the table, `:60-102`).
- **A waited node is declared and never begun** *(R4)*. `wait.unit` has its reach and authority (`NodeContract.cs:17-18`, `:443`), and the runtime
  leaves a waited node ready for WAITUNIT1 (`NodeRuntime.cs:851`, `Begins` at `:1757`); the choices' graphs arm it only where they name a
  plugin's node or a closure (`Armed`, `:318-322`; `Acts`, `:1764`), and a run bound to a graph version by its binding (D190 point 3). A
  wire into another repository's node is refused, its way being a local `wait.unit` (`NodeWires.cs:299`).
- **A session's requests are joined to its work** *(R4)*. A quest a session of the work published is `WorkJoin.Published`
  (`Sessions/Domain/AskWork.cs:12`; `QuestView.PublishedBy`, `Service/Domain/QuestViews.cs:198-202`), the one its taker waits on is `Awaits`
  (`:182-194`), and the work step says it (`Workflows/Domain/WorkflowRun.cs:701-702`).
- **The presets are steps** *(R4)*: four, each a steps version (`WorkflowPresets.cs:26-35`), with the opinion and look variants (`:44-56`).

### 1.4 In flight

- **HEAVYQUEUE1** (`quest/heavyqueue1-one-admission-3d2f1806497d`, QUEUEDRAIN1 and CIWATCHHOLD1 merged into it): one machine-wide admission in
  the Daoris home, `locks/admission/admission.json`, written by twins (`tools/machine-admission.mjs`, `Sessions/Heavy/MachineAdmission.cs`) under
  the keeping lock; capacity one per seven processors, two to six; first come first served, a run admitted where the live holds and the waits
  before it are fewer than the capacity, each entry counted once (`machine-admission.mjs:114-119` on that branch); an entry rebuilt from its
  known fields alone at every read (`:78-88`) and written back whole (`:151-154`); where the home cannot be placed or the record cannot be read or
  written, a run goes on without waiting (`:145-170`, `:200-213`). The merge tool's heavy gates and probes take a place, finding the home by
  `DAORIS_HOME` *(C7)*.
- **QUEUEDRAIN1** (`quest/queuedrain1-drop-ends-its-gates-3f56befcd2b9`, `035059081`): a dropped candidate's gates and probes end.
- **CIWATCHHOLD1** (`quest/ciwatchhold1-ci-watch-test-7f249ff631aa`): a CI run's green status is written only after it lets the gate tree go.

### 1.5 What the survey finds

1. **Two integrators, two rules for one act.** The tool gates, batches, bisects and fast-forwards; the driver's merge form merges one press at a
   time into the checkout, ungated. **Containment** is a refusal and a review reading in the driver (*nothing to land*, REVIEWLANDED1), a
   set-aside in the tool, and a settled outcome nowhere; union joins and the router are resolved only in the tool *(C1, F1)*.
2. **Integration is a procedure, not product.** An external process the parent starts, its state in files under `local/`, the parent's
   environment in its gates, no owned cancellation, recovery by its own reading of files, and none of it on the page or operable by a role *(C2)*.
3. **A failure that does not reproduce is blamed on the batch.** It is the queue's commonest red (§2), and D207's rule makes it a hand-back.
4. **Cancellation is not owned.** Ending a candidate ended no process until QUEUEDRAIN1.
5. **After landing, nothing is settled.** Later commits read as waiting work, and a tree's own verify output keeps it.
6. **Nothing orders two rows that change the same files.** DOGFOOD1's note (TASKS.md:602) is a practice the parent remembers.

## 2. The evidence of 2026-10-28

**The queue's own records** (`local/scope-runs.jsonl` and `local/merge-queue.log` under the main checkout, read for this design; times in UTC as
kept; neither reader read them, so these counts are the author's, §13). From 2026-10-26 10:28 to 2026-10-28 04:40 the queue made 29 scoped runs:
14 green; 9 red only on failures its judge left unresolved (13 such judgements in them, beside one inherited); 3 with a member pinned, 4
pinnings, each a census test (`ParticipantWorkReadersTests` twice, `SessionMarkerTests`, `GitSeedTests`' no-deletes census); 2 stops on a check
failing on the base; 1 dropped. Across all 29 runs the judge left 22 failures unresolved and pinned 4. The longest gate of the last three runs
took 25 to 29 minutes, against 5 to 12 before. Candidate `7ebeba1` (seven members) promoted at 23:28 on 2026-10-27; the next, `268477e` (six),
was red at 04:40 on two unresolved timing tests (a meeting's stalled turn cut at its minutes; a session another process runs stopped through its
request) and split into one member at a time. Set aside on 2026-10-28: a branch whose row met another's at one line of `docs/development.md`'s
census table; a branch whose merge commit could not be made, its work already in an earlier member; a branch conflicting in `package.json`.

**What the steward reported** (the brief; cited as evidence):
- The queue promoted one batch in more than five hours while about eleven changes waited; every candidate failed only on load-sensitive tests
  none of its members touched (short fixed waits, a web test's 20 s timeout, a structure gate's wall-clock bound, a held-file move), each sending
  the batch back. The steward dropped and requeued candidates by hand, ended runners because a drop did not end a candidate's gates
  (QUEUEDRAIN1), resolved set-asides (the two above), and once found a real census miss.
- The steward's shell environment, the install's home variable among it, leaked into the queue's gates (QUEUEENV1; no row of it stands in
  `TASKS.md` at `7ebeba1ba`, and this design's row a is its home). HEAVYQUEUE1 (TASKS.md:33) records the load beside: candidates at twice the
  fast Driver gate's time while a driven session's thousand-test run and a memory bench ran.
- In the library repository (Lyntai) five landings conflicted in a day, most on one status line every row edited, each needing a full session
  turn to rebase. One session committed after landing a change the line had already overtaken, leaving a review entry nobody could clear without
  a forced removal, since its tree held its own verify outputs (TIDYOUTPUT1, TASKS.md:31: two landings kept for 119 ignored files), and its
  closing note said its work waited for an Accept when nothing was left (beside RUNENDS1, TASKS.md:30).
- Two parallel library rows each added the same runner method; built from one base, they conflicted in meaning at landing. This repository met
  the same on 2026-10-22 with KEEP1d and KEEP1e (DOGFOOD1's note, TASKS.md:602).

**What the evidence says.** The judge did what D207 asks; the rule is what cost: a failure that does not reproduce is treated as a member's. The
steward's hand work is the queue's missing parts: cancellation, a clean environment, two more mechanical resolutions, and a way to decide a held
candidate. In the library, landing had no gates and no notion of a line's own rows, so a conflict of meaning reached landing and a status line
cost a session's turn.

## 3. The principle: what the day runs on is product

**The procedures the development's day depends on are product, not scripts.** Each is a fabric workflow (D157, D190, D192): steps of fixed
kinds with their own state, each step a fabric event (D212 §4), each with tests, shown on the page, and operable by the appointed steward through
its recorded acts (D209). A script is kept only where it is one of:
- a **check** or a **generator** that reads the repository (a census, a document check, the index and code-map generators, a git merge driver
  git itself runs);
- a **harness** that proves the product from outside it;
- an **instrument** a person starts;
- a **harness adapter** that enforces, inside an agent's own harness, a rule for an agent Daoris does not start (§7.1's shell-edit guard),
  named as such with the product part that replaces it *(C26)*.

The test is the same as the doctrine's for memory: a procedure a fresh install would lack, or that only the parent's shell can run, is a missing
product part.

Why: integration fails where a script's ownership ends. Its process is the parent's to start and to find again, and nothing else ends it or
what it started. Its state is files the page cannot read. A role cannot be granted an act that is a shell command. And the parent's environment
becomes the gates' *(C2)*. Each of the day's hand repairs (§2) was a step the product did not own.

## 4. The declaration

### 4.1 Two homes, one for each kind of fact

| Fact | Home | Why there |
|---|---|---|
| What the repository is: its gates at integration, its commit check, its scratch, its row tables and line writers, its merge message, who integrates it, its runner | its `daoris.gates.json`: key `integration`, and D192's `verification` runner extended (§5.9); read from the line's tip and **run only as the owner accepted it by its blob**, in an **acceptance of its own**, `integrationGates` in `driver.json` keyed by repository, set by the person's *Accept the gates* (D156) as D192's landed route sets its own, so a repository needs no verification pipeline to integrate; GENREGEN1's writers read either acceptance *(F2, F11, C20)* | versioned and reviewed with the files it describes; a session cannot declare a command and have its own landing run it |
| How this install integrates it: who requests, how members land on the line, the batch, the order, what an unconfirmed failure does, where a conflict of meaning goes | the landing rule in `driver.json`, key `integrate`, **on a branch rule with no plugin** (§4.2, §4.7), inherited whole with the rule (D164), set at Setup, by the terminal and by Ask Daoris's proposal at the person's press; **under a named workflow, the unit's half (`request`, `conflicts`) is its `integrate` step's, and the line's half stays the rule's** *(R4, §17.7)* | the install's trust choices, which no repository file can lower *(C20)* |

**The repository's half** (`daoris.gates.json`):

```json
"integration": {
  "by": "daoris",
  "gates": "reached",
  "commits": "runner",
  "scratch": ["local/scratch/**", "_fixtures/**"],
  "merge": [
    { "paths": "docs/development.md", "by": "rows" },
    { "paths": "docs/STATUS.md", "line": "^Status: ", "run": "node tools/status.mjs" }
  ],
  "message": "Integrate {name}: {title}",
  "env": { "pass": [] }
},
"verification": { "run": "node tools/merge-branch.mjs --full --beside --daoris-verify", "result": true, "modes": ["plan", "probe", "commits"] }
```

| Key | Means | Absent |
|---|---|---|
| `by` | who integrates the line: `daoris`, or `tool` for a repository whose own tool still does (this one until row k); read at promotion by both (§5.6) *(C13)* | `daoris` where the rule integrates |
| `gates` | the gates at integration: a list of declared gate names, each run whole, or `reached`, the runner's `plan` choosing among the accepted declaration's gates (§4.3) | every declared gate, whole |
| `commits` | the commit check on each request's range: `runner` (the runner's `commits` mode), or a declared gate's name run on the range *(F7)* | none |
| `scratch` | globs of ignored output the **landing's** tidy may take with the tree (LAND3, at `handed`, `TreeScratch.cs:36-58`), beside the thirteen build folders `TreeScratch` lists *(F35)* | none; today's rule |
| `merge` | files whose conflicts settle by a rule: `by: rows` (by base: a row a side added kept, one removed dropped, rows both add at one place both kept, one base row both changed a conflict, MERGETASKS1's rule widened from the backlog), or a `line` pattern with a `run` writer that recomputes the line after the merge, accepted with the rest; **no rule keeps one side's copy** *(F10, C21)* | none; a conflict |
| `message` | the merge commit's subject, from `{name}`, `{title}`, `{quest}`, `{repository}`, `{branch}`; its body the unit's closing summary and the members' `Co-Authored-By:` trailers, deduplicated; checked with the range (step 1) *(F7)* | `Merge branch '{branch}'`, today's merge form's (`TreeLands.cs:287-289`) |
| `env.pass` | exact variable names a gate needs from the install's environment beyond the composed base (§5.5); never a `DAORIS_` name | none |
| `verification` | D192's runner key, unchanged where no mode is asked, gaining `modes` (§5.9); its command D192 point 9's, whose flags are the runner's own, a handed tree taking the place of `--beside`'s gate tree *(F5, F39)* | Daoris runs each named gate's `run` itself |

**The install's half** (the landing rule):

```json
"landings": { "Daoris": { "form": "branch", "pattern": "quest/{slug}-{quest}", "tidy": true, "autoAccept": true,
  "integrate": { "request": "done", "commit": "merge", "batch": 8, "order": "arrival", "gates": "declared",
                 "unconfirmed": { "unreached": "split", "reached": "split" }, "conflicts": "session", "overlap": "warn" } } }
```

| Key | Values | Absent |
|---|---|---|
| `request` | who makes the request (§4.6): `owner` (the person's Accept, or `trees land`), `done` (the quest's done, through the rule's own `autoAccept`, which must be on), `steward` (the steward's act requests a records branch or a landing the owner's press or a done already made, and never lands, §8); `done` and `steward` only where `gates` is `declared` and the accepted declaration names at least one gate *(F9, F26, F31)* | `owner` |
| `commit` | how each member lands: `merge` (one `--no-ff` merge commit per member), `rebase` (rebase-and-merge: replayed and the line fast-forwarded, no merge commit), `squash` (one commit per member, PROCESS1b-ii's message); the rule's own `squash` is refused beside `integrate` | `merge` |
| `batch` | members per candidate, 1 to 8 | 8 where gates run, 1 where none |
| `order` | `arrival`, or `apart` (a member changing a path an earlier member of the forming candidate changed waits for the next candidate) | `arrival` |
| `gates` | `declared` (the accepted blob's `integration.gates`), or `none` (today's trust of the session's own checks) | `none` |
| `unconfirmed` | §4.5: for a unit no member reaches and for one a member reaches, each `split`, `hold` or `land` | `{ "unreached": "split", "reached": "split" }`, **permanently**: `land` is the owner's choice alone, refused where no whole-set verification runs on the line *(C6, F14)* |
| `conflicts` | a conflict of meaning goes to the member's `session`, or is held (`hold`) for the steward or owner *(C19, F25)* | `session` |
| `overlap` | a start whose declared paths meet an open quest's: `warn` (said on both) or `wait` (held until the earlier is integrated, §9) | `warn` |
| `line` | the line by name | the repository's canonical line (`daoris driver line`) |

`integrate` is refused beside a `plugin` (a pull request is the platform's integration) and on a merge rule (§4.7) *(F17, C9)*.

**Two halves of the key** *(R4, §17.7)*. `request` and `conflicts` say how one unit is integrated: they are the process, so under a named
workflow its `integrate` step holds them, as a landing's `form` and `autoAccept` are its step's (D157 point 4), and under Current this key does.
`commit`, `batch`, `order`, `gates`, `unconfirmed`, `overlap` and `line` say how the line is integrated, whichever flow each member follows,
since one run per line batches, orders and gates them together: they stay this key's always, and a rule without it reads their *Absent*
column.

### 4.2 The landing shapes

| Shape | The rule | What integration does | The line reached |
|---|---|---|---|
| A branch for review | `branch <pattern>` | nothing: the branch is the result | observed when the person merges it (`line`, as today) |
| A pull request | `branch <pattern> --plugin <id>` | nothing; `integrate` is refused beside a plugin *(F17)* | `pull-request`, then `line`, observed (D148) |
| Merge | `branch <pattern>` with `integrate`, `commit: merge` | the landing makes the branch as today; integration takes it into the line by every step of §5.2 | promoted |
| Rebase and merge | `commit: rebase` | as merge; the line linear, each member's commits keeping their own trailers | promoted |
| Squash | `commit: squash` | as merge; one commit per member | promoted |
| Today's merge rule, the named legacy exception | `merge` | from row g, carried by the integration transport as `{ request: owner, gates: none, batch: 1, commit: merge }` with today's message, so the checkout is only ever fast-forwarded; it claims no gate, so a build without integration merging it on the person's press as today loses nothing it promised *(C9, C18)* | promoted |

Under a named workflow *(R4)* the shape is its graph's: a `handed` on a branch with no plugin followed by `integrate` is the merge, rebase or
squash shape by the rule's `commit`; `pushed` and `pull-request` after `handed` are the pull request; `handed` alone, or `line` after it, the
branch for review (§17.2).

Integration never rewrites the line or a pushed ref: the line moves only forward to a gated commit (§5.2 step 8), and what is replayed is a
session's unpushed commits, into new commits in Daoris's own tree, as D87's replay already does (D109, D87 point 2). A push stays the owner's
(D37; D191 point 3).

### 4.3 The gates at integration

`gates: declared` runs the accepted blob's `integration.gates` on the candidate's tip; `reached` lets the runner's `plan` choose among them, which
for this repository is today's scoped set (D207 point 3). **The commands, and the list of gates a plan may choose from, are read only from the
line's accepted blob**: Daoris writes that blob's text beside the run and hands the runner its path (§5.9); the runner reads its gates and
commands from it and never from the tree. What the tree may still supply is the repository's knowledge of which accepted gates a change reaches
(the lane table, the censuses), and the code each command runs, which is the candidate's, as D192 states of its runner *(F6)*. A member that
changes the declaration integrates under the line's accepted one, and once promoted its blob is offered for the owner's acceptance; integration
goes on under the last accepted blob meanwhile, and a gate whose accepted command no longer runs on the line stops the line's integration as a
check failing on the base does, the acceptance named as the remedy.

### 4.4 Batching and ordering

A candidate takes up to `batch` requests in their frozen order. Under `apart`, a request whose changed paths meet an earlier member's in the
forming candidate waits for the next, so two overlapping members never share a bisection and the later meets the earlier's promoted work at its
own replay. The 2026-10-26 to 28 records show no pinning between overlapping members, so this repository keeps `arrival`; `apart` is for a
repository whose rows collide (§9).

### 4.5 A failure that does not reproduce

**The confirm run.** Before a failure is placed on any member, the failing gate's run is repeated once on the candidate's tip **as composed**
(the same units and settings as the failing run, which the runner keeps with its result, §5.9; the gate whole where it keeps none), **holding
the whole admission**, one confirm run per failing gate per candidate, answering for every failure that gate had *(C8)*.

**The whole admission, as ordinary entries** *(C7)*. The confirm run takes every place by adding, to HEAVYQUEUE1's record, as many entries of
today's schema as the capacity it reads, each its own id under the same process, each beating, each admitted by today's first-come rule. So a
writer of any build reads, counts and keeps each one: no field is added for an older writer to drop.
- **One write.** The entries go into `waits` in one locked write (`machine-admission.mjs:151-156`), so they stand together in first-come order,
  and no second confirm run or session's ask can fall between them *(F34)*.
- **It starts once every entry holds.** While it waits, the holds it sees are the runs it waits for (`:114-119`), never a reason to doubt it.
- **Inconclusive.** From its start, at each beat and at its end, it reads the record again. Any live hold not its own (a writer with a larger
  capacity, a run that took a place while the record could not be read) makes the confirm run **inconclusive**. So does an admission it could
  not establish: no home, or a read or a write that failed, which HEAVYQUEUE1 lets run without waiting (`:145-170`, `:200-213` on that branch)
  *(F34)*.
- **A `drop` ends it.** The steward's `drop` ends a confirm run with its candidate's job *(F16)*.

- **Fails again with its signature: confirmed.** Judged as today (§5.3): the base, then the prefixes, the last transition pinned.
- **Passes, holding the whole admission: unconfirmed.** The first failure is filed as a **sighting** on its unit with the candidate's tip and the
  confirm run's admission evidence. The candidate goes by the rule's `unconfirmed`, by whether any member's paths reach the failing unit by the
  runner's reach (a unit the census cannot place counts as reached):
  - `split` (the default for both): today's rule, the members one at a time, one of one handed back as unresolved with the sighting;
  - `hold`: the candidate waits for the owner's or the steward's `decide` (§8), the evidence shown;
  - `land`: the confirm run's pass is the gate's verdict on the candidate's tree, and the candidate goes on to promotion.
- **Inconclusive:** read as `split`, whatever the rule says, and said. It is never `land`. Inconclusive is judged first, before either reading
  below or above *(F34)*.
- **Fails another way, holding the whole admission:** the new failure is the candidate's, confirmed, since it failed with nothing heavy beside it.

**`flake` at promotion** *(F33)*. A runner's `flake`, a failure that passed when run again alone, is read as a pass, D192's reading
(`pipeline-design.md:333-334`) and today's queue rule (`passing`, `merge-branch.mjs:1997`). This repository's runner gives it only for FLAKE1's
named classes (`:245-251`, `:3736-3742`), the one exception to §10's rejection of confirming by the test alone, kept as today. Every other
failure goes to the confirm run under the strict policy.

**The default is `split` for both, and it stays so** *(C6, C16, C17, F14)*. The confirm run is useful evidence and no causal proof:
- **It can miss a member's own race.** A race that fails one run in three passes it as readily as one that was already there.
- **It reproduces less than it seems.** It repeats a gate's composition, but not the schedule of the driver's Process half, eight classes at
  once (PROCPAR1, `docs/development.md:87-90`), nor xunit's collection parallelism, so a pollution across classes can pass it *(F15)*.
- **The census is not a closure** (D207, `docs/decisions/D207.md:22-24`), so "unreached" is no guarantee.

`land` is therefore the **owner's choice alone**, set on the rule like any lowering (§8). It is refused for a line on which no whole-set
verification runs: this repository's CI watch until FABRIC-PIPELINE1g, or a D192 landed route elsewhere (D192 point 7).

That backstop is not every tip whole *(C3)*. The watch gates main's newest tip each time it polls, so promotions made while it runs are taken
together (`merge-branch.mjs:6031-6066`), and an ordinary run keeps standing verdicts and runs by reach. A unit is run whole by the sweeps and the
nightly run (`docs/development.md:288-361`), and the install waits for the background's receipt (D207 point 5).

**What the records can and cannot show** *(C16)*. The queue's records hold gate summaries and the judge's run counts
(`merge-branch.mjs:6409-6413`, `:6448`), never a quiet confirm run, so row b's replay of §2's 29 runs tests the policy's accounting only (which
candidates each setting would have promoted, split or held). The confirm run itself is measured from row b on, prospectively, each one recording
its composition, its admission evidence and its wait. Row b also counts how many recurrences were across classes (F15) and how long the whole
admission keeps driven sessions' heavy runs waiting (F16).

**Sightings feed the fixes.** Three sightings of one unit within seven days are shown as a finding on the Integration view with the row its
fix needs (a held clock, `held-clocks.md`; TREECLEANFREE1, PLUGINSHUTDOWN1, CHATGOONWAKE1 and HOSTCLEAN1 are such rows today, TASKS.md:15-25). A
wider window is never the policy's answer.

This amends D207 point 3 by name in two places: the confirm run comes **before** the base (a failure that does not recur needs no base run),
and where the owner chose `land`, *a passing re-run never making a candidate green* gives way for the confirm run alone. Its rejection of
re-forming a candidate until a run passes stands (`docs/decisions/D207.md:163`): one confirm run, bounded.

### 4.6 Who requests, and through which door

**One door for every producer** *(C5)*. A request is admitted by one function, `IntegrationRequests.Admit` (Workplace), which records the
requester (the person and their door, Daoris at a done, or the steward with its act's id and appointment generation), the rule and the policy as
they stood, and the frozen tip, and which takes a unit's request only from **that unit's own landing**:
- `owner`: the person's Accept, or `trees land`, lands the unit by its branch rule through `SessionTrees.LandAsync` and its whole landing gate
  at the commit it pins (the run's workflow part, the second opinion, the review and the graph's part binding the run, the branch and the
  commit: `Workplace/TreeGates.cs:40-57`, `Workflows/Domain/GraphGate.cs:113-140`). The branch it makes at that commit is the request's
  frozen tip.
- `done`: the rule's own `autoAccept` makes the same landing at the quest's done, through the same gate (`AutoLander`).
- `steward`: **the steward's act never makes a landing** *(F31)*. A unit's landing under a branch rule is the Accept
  (`SessionTrees.LandAsync`, the press's own path, D145's LAND2b), and every acceptance of what is delivered is never a role's act
  (`docs/decisions/D209.md:63-65`; a steward's finish neither accepts nor closes its quest, `:311`). So the steward requests only what is
  already landed or is no unit's: an existing recorded landing (below), made by the owner's press or by a done under `autoAccept`, or a
  records branch. Granting a role the Accept itself would be the owner's decision and a named amendment of D209 (D191 point 3), not this
  design's.

So no request can pass a session's stopped `handed`, a required opinion or a review.

**Its caller is the unit's `integrate` step** *(R4, §17.4)*. The node's start calls this door, keyed by the unit and its landed tip, so a
restart finds the request it made. Under `owner` and `done` a landing made under the step is its request; under `steward` the steward's act
starts it; a recorded landing made before waits for the doors below. The door's rules are unchanged; only its caller is new.

**An existing recorded landing** *(F32, C27)*. Branches landed under the old rule, by an older build in the rollback window, before the rule
carried `integrate`, or left behind by a request the flip hands back (§6.3) are already landed: their landing is not pressed again, since
`TreeLands.cs:598-604` refuses a moved branch and D145 §3 advances only on a new tip. `IntegrationRequests.Admit` takes such a landing,
beside the landing's own call, only on all of the following:
- **Its record.** It is a standing entry of the machine's landings (`LandedBranches`, `Workplace/LandedBranches.cs:8-10`), bound to the unit,
  session and quest that entry records.
- **Its exact accepted tip.** The tip is the entry's recorded tip, or its newest advance's (LAND2c). The branch must stand at that commit,
  else it is refused as moved, a revision being its unit's own next landing (C11).
- **Its gate proof.** The proof is the landing's own, as it recorded it: the review, the second opinion and the workflow version that let it go
  (the `LandedBranch` record's `Review`, `Opinion` and `Workflow`, `TreeLands.cs:657-679`). It is read, never judged again, since the Accept it
  records is done.
- **No double.** No request is open for it, and the line does not already hold it.

Its doors are the owner's card (`daoris-driver integrate --request --landed <branch>`, a Person route), the steward's act where the rule says
`request: steward`, and **a come-up sweep** where the rule says `request: done`. The sweep runs once at each come-up and once when the rule is
set, and requests every such landing of the repository. **A landing whose tree was tidied** (LAND3's tidy at `handed`; the entry says so,
`LandedBranches.cs:50-55`) needs no tree: the candidate replays from the branch, and settlement judges the branch alone.

**A records branch** (no unit, no run: the steward's records, a person's own branch) is requested only by the owner's card (`daoris-driver
integrate --request`, a Person route, D156), or by the steward's act where the rule says `request: steward`. Such a request carries no workflow
gate, and the declared gates and the commit check run on it as on any member. A session's branch, and a landing branch the record does not hold
as above, are refused there: a unit comes only through its own landing or its recorded landing.

**Each revision is requested again by the same rule** *(C11)*. A commit made after the frozen tip is no part of the request. Under `done`, the
session's next done advances its landing branch (LAND2c) and makes the next request. Under `owner` or `steward`, the new revision is offered to
that requester, never queued by itself, as today's landing pins its commit and refuses a moved branch (`Workplace/TreeLands.cs:598-604`) and the
queue freezes a tip (`tools/merge-queue.mjs:131-145`).

**Why declared gates suffice for `done`** *(F26)*. D145 refused a merge at done because it wrote into the person's checkout with no human step
after it (`docs/decisions/D145.md:70`). Integration into a local line keeps a human step after it, the owner's push of the line (D37; D191 point
3). Declared gates answer what D145 point 1 guards for a local line, and never for a rule whose landing pushes, which `integrate` refuses beside
a plugin (§4.1).

### 4.7 Defaults, inheritance and older builds

**Integration rides a branch rule** *(C9)*. An older build reads the fields it knows (`LandingKey.cs:359-388`): a branch rule with a pattern
and, for `request: done`, `autoAccept`. On the person's press or at the done it makes the landing branch and stops there, pushing nothing
because no plugin is allowed beside `integrate`, and merging nothing. So an older build in the rollback window (D139) can never merge work the
rule meant to gate. It leaves a recorded landing the line lacks. Once the newer build returns, that landing is requested through §4.6's
recorded-landing door: by the come-up sweep under `request: done`, else by the owner's card or the steward's act *(F37)*. It keeps `integrate`
on its writes (`DriverConfig.cs:938-944`). The named legacy exception, today's `merge` rule, promises no gate, so an older build merging it on the
person's press as today removes no protection (§4.2).

The repository's rule replaces its workspace's whole, `integrate` with it (D164). No bound version and no run bound before any row changes
*(R4)*, since the `integrate` kind enters only a graph version a person saves, so a run bound before reads as it did *(C10, F21)*. A build
without the kind reads a version naming it as unreadable (`NodeRefusal.UnknownKind`, `Trees/Nodes/NodeContract.cs:483`) and holds a run
bound to it whole, as D190 holds a version that stops reading; so it neither integrates nor lands such a run's work. A Current branch unit
gains the node only under a rule carrying `integrate`, which an older build reads as the branch rule it is (§17.6). A build that does not know
the store's integration tables (§5.6) never reads them (`compatible-builds.md`).

## 5. The integration run

### 5.1 A transport outside every run, requested from inside each flow

**The line's run is added to no graph; the unit's flow holds a step that requests it** *(R4, §17.4; round 3's text, C10, F4, F8, F21, C23,
said no node was added to any graph, and §17.12 quotes it)*.
- **The unit's graph gains `integrate`, after `handed`** *(R4)*. Under Current it is its landing rule's own graph, `handed` then `line`
  (`NodeGraphHomes.Landing`, `Trees/Nodes/NodeGraphs.cs:747-763`), and `handed`, `integrate`, `line` where the rule carries `integrate`; under a
  named graph version, the version's nodes. `handed` is reached by git's proof when the landing makes the branch, as today, and EVENTHOOK1's
  `work.landed` is told then with exactly its published meaning: a branch the line does not hold yet.
- **The `integrate` node requests the line's run and waits on it** *(R4)*: its start is §4.6's door, its states the integration record's
  (§17.4), its subject the landed branch at `handed`'s recorded tip.
- **The integration run moves the line**, and the unit's `line` node observes that promotion as it observes a person's merge today: from
  the unit's end, D102's content-held proof kept as the landing record's `Held` (`Trees/Nodes/BuiltInNodes.cs:153-160`). Ancestry alone would
  never reach it, since under every `commit` shape the promoted commit's parents are replayed tips (step 3). `work.integrated` is told when
  `line` is reached (FOUNDATION1 §4.2) *(F38)*.
- **Every bound run is untouched.** A run bound to a schema-1 version and a run bound to a graph version saved before the kind are left as
  they are (D190 point 1); a schema-1 run's landed branch follows Current's landing graph after its landing, as it does today (D190's third
  round), and so integrates where its rule carries `integrate` *(R4)*.
- **A unit's integration state** (queued, in a candidate, held, handed back, promoted, settled) is read from the integration record by the
  readers that show it (the session's head, the review, the quest's page, the run's view, the CLI, Ask Daoris). The node's own lifecycle state
  is on its record, written by the runtime alone; the integration record's state is never copied there *(R4)*.
- **The `integrate` node kind**, deferred here in round 3, is round 4's row n *(R4)*.

**The line's integration run.** One per line, built in: its steps fixed, its options the declaration's, candidates one at a time, each to its
verdict unless dropped or stopped. It has a record of its own in the store (§5.6), as D192's landed run has, and it is no named workflow (§10).
Its machinery is shared with D192's performer where they meet: the job, the slots, the reconcile and the runner contract *(C23)*.

### 5.2 The steps

| # | Step | What it does | Its event |
|---|---|---|---|
| 1 | **request** | admits the request by §4.6's door, called by the unit's `integrate` node *(R4)*; runs the commit check on `base..tip` and on the composed message (the declaration's `commits`; today's maker-trailer and uncommitted-work rules, `merge-branch.mjs:2836-2858`), refusing with its words; freezes the tip and the message *(C5, F7)* | `integration.queued` |
| 2 | **form** | judges each request's requester's authority first, so a withdrawn member is never formed (step 8 keeps the check for the race alone) *(F36)*; takes up to `batch` requests by `order` and makes a candidate on the line's tip in the line's integration tree: one per line, under the trees home beside the session trees as a replay's tree is (`Workplace/BranchReplays.cs:244-252`), kept warm, held by the run's lease while a candidate uses it, reset only once the last candidate's job holds no process, its evidence kept outside what a reset clears; **it counts against KEEPSIZE1's allowance and is never evicted while a run holds it** (the steward's decision, §15.2); the checkout's private files the gates read copied in, refreshed each candidate (GATEWT1b's `privateGateFiles`) *(C22, F20, F28)* | `integration.formed` |
| 3 | **replay** | for each member, onto the candidate so far: what the line holds is left out by D87 point 1's judgement (`LineHolds`, content then patch), and said; what only an **earlier member** holds is left out too, and the member is recorded **contained by that member**, provisional: it settles only when that member promotes, and goes back to its own request, re-judged, if that member is handed back, split off or dropped *(C4)*; a member the line itself holds wholly is contained at once; the rest replayed aside (D87 point 2, LANDREBASE1) and landed by `commit`, so a merge commit's second parent is the replayed tip, not the session branch's *(F27)* | `integration.replayed` |
| 4 | **resolve** | each stop settled where every conflicted path is mechanical: a generated folder (written again by its accepted writer, GENREGEN1), a union record (git's own; the joins union glued set apart, MERGEJOIN1 and UNIONGLUE1, given a driver twin), the backlog (by base, LANDBACKLOG1), a declared row table (`by: rows`), a declared line rule (its accepted `run` writer), the router (its row driver, which git runs from the clone's own configuration) | `integration.resolved` |
| 5 | **conflict** | any other conflict is one of meaning: the member goes by `conflicts` (§5.4) and the candidate goes on without it | `integration.conflict` |
| 6 | **gates** | the accepted gates on the candidate's tip, through the runner or directly (§5.9); each heavy gate takes a place in the machine's admission; every process in the candidate's job; the environment composed (§5.5) | `integration.gates` |
| 7 | **judge** | each failure by its signature (§5.3) | `integration.judged` |
| 8 | **promote** | under the line's integration lock and immediately before the line moves: the line's committed declaration at the base still says `by: daoris` (else held: another integrator owns the line); every member's requester's authority still stands, judged again here for the race since step 2 (§8: a steward's act's appointment and generation live, no owner act on its repository, quest or session since, the rule still integrating and its `request` still admitting it), a member failing it leaving the candidate, which is formed again without it; then the line to the candidate's tip: `merge --ff-only` in a clean checkout on the line, read immediately before (`TreeLands.cs:264-282`), or, where no worktree has the line out, one `update-ref` verifying the line's old value; held and said otherwise, tried at each look; formed again where the line moved *(C12, C13)* | `integration.promoted`, `integration.held` |
| 9 | **settle** | for each member: its request settled; a member contained by it settled with it; its branch's commits past the frozen tip judged **contained** (said), **superseded** (asked, §5.7) or **new** (offered by §4.6's rule) *(C4, C11)* | `integration.settled` |
| 10 | **tidy** | the session's tree and its `daoris/` branch went already at `handed`, by the landing's tidy (LAND3), which takes the declared scratch with them; this step retries only a tree the landing kept, nothing forced, a tree that stays following the landed tip (D87 point 7), and removes the landed branch once the line holds it by D102's content-held removal, its recovery ref kept (`docs/decisions/D102.md:243`; `ref-removal-transactions.md`) *(F35)* | `integration.tidied` |
| 11 | **close** | the unit's integration note written from its record (a code and its facts), which the review, the session's head, the quest's page, the CLI and Ask Daoris read: *Accept* or *waits for you* is said only where `request` names a press still due | `integration.closed` |

A member handed back at step 1, 5 or 7 says why (`integration.handed-back`), to whom and with which words. A line whose base fails a gate that
names no test stops (`integration.stopped`), lifted by a commit that moves the line, a `recheck`, or the run's own recheck an hour on, as today.

### 5.3 The judge

In order, for each failure of a candidate's gates:
1. **A death of memory** is the machine's, held as D207 holds it, never run on the base or the prefixes.
2. **A known red**: unexpired and last seen failing on the base, inherited with no run, as today; past its 72 hours, run on the base, and
   failing there with its signature it stops the line's integration (`tools/merge-queue.mjs:300-311`; D207's exception,
   `docs/decisions/D207.md:139-148`), a case of the judge's table with its regression *(C14)*.
3. **The confirm run** (§4.5). Unconfirmed or inconclusive: by the policy, with no base run. Confirmed: on.
4. **The base.** The failure's selection on the base: failing with its signature, a test is inherited and filed known red; a gate that names no
   test stops the line's integration.
5. **The prefixes.** The selection on every prefix, as composed where the runner can run it so; the last transition pinned and handed back to its
   member's session with the failure's log; neighbours that disagree, or a prefix that cannot run, leave it unresolved, and a candidate of several
   then runs its members one at a time. A one-member candidate's confirmed failure on a passing base is pinned to it.

Signatures are D207's (a test by its full name, case, exception type and assertion location; a gate naming no test by its step; a build by its
error codes), read by a twin of `tools/test-failures.mjs` in Review that holds its fixture table.

### 5.4 A conflict of meaning

`session`, the default: the member's session is told through the say door, as Daoris's words keyed `rebase/<session>/<tip>` (LANDREBASE1 point 3,
`RebaseTelling.TellAsync`, `Workplace/RebaseTelling.cs:16-26`), with the conflicted files, the line's commits that met them, and the member it met
where that was another member of the same candidate; it resolves in its own tree, runs what its change reaches, commits, and its next done or its
requester makes the next request (§4.6). Nothing else waits on it. Where the session cannot go on (its record, tree or conversation gone; held by
an account's limit past its reset, D206), or under `conflicts: hold`, the member holds for the steward's or the owner's act, shown with the
conflict.

**A landing agent is deferred** *(C19, F25)*. A session Daoris would start to resolve another's conflict needs a record (a part of the member's
unit, its `from` and `why`, a tree, a cap seat, the unit's channel), its own authority and attribution, and a re-review, and a working role of
the catalogue's later design (D212 §2.7). The case is rare. The holds are counted first, and the landing agent is a later design's.

### 5.5 The gate environment

A gate never inherits a person's or the steward's shell: Daoris starts it, and its environment is composed from the install's own, in one list
both the driver and the tool's twin hold, its names held by the wiring census (`SystemWiringTests` for every `DAORIS_` name):
- **removed:** every `DAORIS_*` variable (the role variables ENVLEAK1 already removes, and `DAORIS_HOME`, the role key and the supervisor key);
  git's repository-locating variables (`GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES`,
  `GIT_COMMON_DIR`, `GIT_NAMESPACE`, `GIT_CEILING_DIRECTORIES`, `GIT_CONFIG*`; `docs/development.md:459-462`); the merge tool's `MERGE_BRANCH_*`
  switches;
- **added:** the build-server isolation (`BESIDE_ENVIRONMENT`'s three), the admission's hold marker (`DAORIS_ADMISSION_HOLD`, so a heavy run a
  gate starts sees itself inside its hold), and the declaration's `env.pass` names;
- **the runner's own inputs** (§5.9's `DAORIS_VERIFY_*`) set on the runner's job alone, which the runner strips from every process it starts *(F5)*.

**The tool's runner, in the meantime** (row a): it reads the home, `DAORIS_HOME`, before it composes anything, uses it for the admission's file,
and starts every gate, probe, re-run and rehearsal with the composition above, `DAORIS_HOME` removed *(F20)*.

### 5.6 State, recovery, cancellation and ownership

- **The store.** Workplace's family in the desktop store (D212 §13): `integration_requests`, `integration_candidates`, `integration_members`,
  `integration_steps`, every column required, each step's start and end written in one `BEGIN IMMEDIATE` transaction before and after its
  effect. **Sightings and known red are Review's** (`integration_sightings`, and the findings moved from `local/ci-findings.json` with their rules
  unchanged, D207 point 4, once FABRIC-PIPELINE1e runs the background check) *(C15, F22)*.
- **Who owns what** *(C15)*. Settings owns the keys' shapes. Workplace owns integration's policy and its steps (requests, candidates, replay,
  resolution, promotion, settlement) and declares a verdict port. Review implements that port: the gates' execution, the admission, the job, the
  judge and the sightings. Workflows holds the run's executor off the pass, shared with D192's performer. A `LoopContext`-built executor stays
  in Loop. Every edge points down the map.
- **One integrator per line, fenced at promotion** *(C13)*. The run is the home's lease holder's, one per line. `integration.by` in the line's
  committed declaration names who integrates it, and **each integrator reads it immediately before its promotion effect**, not only at its doors
  (§5.2 step 8; the tool's runner the same, row k).
- **Recovery at the come-up.** A candidate whose tip the line holds is promoted, read by ancestry (`resumeCandidate`'s rule). A promotion is
  judged by ancestry before anything is retried. A step whose job is gone runs again from its start.
- **Cancellation is owned.** Every process a candidate starts, its confirm run among them, runs in one job object named by the candidate
  (CISCOPE1e2's job, QUEUEDRAIN1's lesson). A drop, a stop, a supersession or the run's end closes the job, its descendants ending with it, before
  its admission places are let go.

### 5.7 After landing: contained, superseded, new

A commit made on a unit's branch after its request froze is judged at step 9 against the promoted line. A replayed member's merge holds replayed
commits, so the judgement rests on patch id and content (D87 point 1), never on the session branch's ancestry *(F27)*:
- **contained**: the line holds it by content or patch, or its replay is empty: said, never offered again;
- **superseded**: its replay conflicts, and in every conflicted hunk the line's side was changed after the frozen base by a commit outside this
  unit's work. It is kept on a recovery ref (D102's), said as superseded by that commit, and offered to the session (or, where it cannot go on,
  the steward) as *superseded* or *rebase it*. Until one of them answers, it stays **visible as a pending disposition** on the Integration view
  and the session's head, even once the review row has gone *(C24)*. It is never discarded by itself, never said as waiting for an Accept, and
  its recovery ref goes only by D102's and `ref-removal-transactions.md`'s rules;
- **new**: anything else, offered by §4.6's rule for its request.

The review row of a unit whose request is settled and which holds no new commit leaves the review (REVIEWLANDED1, D102), its tree going by its
rule without `--force`.

### 5.8 The page and the terminal

- **The Integration view** (集成) on the repository: the queue (each request's unit, its frozen tip, since when), the candidate with its members
  and each step's state as it moves (a fixed strip of steps, not a graph), what holds it and the act that releases it with who may press, what
  was handed back, to whom and in which words, superseded commits awaiting a disposition, the sightings and known red by unit, and the promoted
  history. A pushed slice, `INTEGRATION`, partitioned by repository, its owner in .NET (`pushed-slices.md`).
- **The run's view** *(R4)*, the session's side bar's *Workflow* and `daoris-driver workflow run`, draws the unit's `integrate` node in the
  flow with its record's detail (the request's state, the candidate's step, what holds it, to whom it was handed back), and the work's own
  requests inside its work node (§17.3).
- **The session's head** says its unit's integration state in one line from the same record. *What needs you* lists a candidate held for the
  owner, a superseded commit awaiting its disposition and a declaration owed acceptance (each a wait kind, `person-waits.md`).
- **The terminal**: `daoris-driver integrate <repository>` shows. `--plan <branch>` is the preview (objects only). `--request <branch> [--message
  <file>]`, `--drop <request>`, `--recheck`, `--decide <candidate> split|hand-back|land` and `--order <request> first|last` are **Person routes**:
  a card in the window (D156). The steward's acts are `ROLE_ACT` forms of the same names, `land` not among them (D209 point 4, §8). The pipe
  census classes each route so *(F30)*.
- **Words** in English and Chinese; the glossary gains *integration* (集成), *candidate* (候选), *handed back* (退回), *contained* (已包含) and
  *superseded* (已被取代), beside *landing* (落地), *line* (主线) and *merge* (合并). The verb is spelled `hand-back` on every door *(F30)*.

### 5.9 The runner contract: D192's, extended

**One key, one command, D192's own** *(F3, F5, C8, C25)*. A repository's runner is D192's `verification` runner, `{ run, cwd?, clock?, result? }`
(`docs/2026-10-20-pipeline-design.md:319-343`), unchanged where nothing more is asked. It gains `modes`, the extra answers it gives. Its inputs
are set on its job alone and read only where its declared command says so, D192's rule (`:323-326`):
- D192's own: `DAORIS_VERIFY_COMMIT` (the tip) and `DAORIS_VERIFY_RESULT` (the file to answer in).
- New, for integration's calls:
  - `DAORIS_VERIFY_MODE`: `run` (the default), `plan`, `probe` or `commits`.
  - `DAORIS_VERIFY_BASE`: the candidate's base.
  - `DAORIS_VERIFY_DECLARATION`: the path of the accepted blob's text, from which alone the runner reads its gates and commands (§4.3, F6).
  - `DAORIS_VERIFY_GATES`: the names a `run` runs.
  - `DAORIS_VERIFY_FAILURE`: the file naming the failure a `probe` repeats.
  - `DAORIS_VERIFY_TREE`: a tree the caller made.

**The one amendment of D192, by name.** Its *a runner owns its checkout* (`:366-368`) gains: where the caller hands `DAORIS_VERIFY_TREE`, the
runner runs there and makes no tree of its own. The candidate exists only in Daoris's integration tree, so today's `prepareScopeTree`
(`merge-branch.mjs:6223-6236`) moves to the caller. Everything else is an extension D192's readers never see.

**The answers.** With no mode asked, the runner writes D192's schema 1, `{ schema: 1, commit, gates: [{ name, verdict, exit?, wallSeconds?,
log? }] }` or `busy`, so a D192 `check` reads it as accepted. With a mode asked it writes schema 2: `{ schema: 2, mode, commit, base,
declaration }`, where `declaration` is the blob id it read, plus:
- `run`: `gates` as schema 1, each with `composition` (the units, the settings file kept beside its log, and their digest, which a `probe`
  takes) and `failures: [{ unit, test, signature }]`;
- `plan`: `run`, `kept` (each with the commit and tree its pass stands on) and `deferred`, each `{ name, why }`;
- `probe`: `{ result: pass | fail | cannot, signature }`;
- `commits`: `{ problems: [...] }`.

**Judged as D192 judges, and more** (`:327-340`). The answer is **unknown**, never a pass, where:
- the file is missing, does not read, or names another schema;
- it names another commit, another base, or another declaration blob than the accepted one;
- it names a gate the accepted declaration does not declare, or a plan names one;
- a `run` lacks any gate its plan's `run` named, or holds one `ended` or `not-run`.

Promotion consumes only a `run` answer for the candidate's exact tip and base, under the accepted blob. That answer must hold every gate of the
accepted plan as `pass` or `flake`, beside the plan's `kept` gates on the standing the runner states. Busy is exit 75 or `busy: true`, as D192
has it.

**`probe` is a mode integration requires and a `check` may answer** (§15.2). A repository with no runner has each named gate's `run` run whole as
a step in the integration tree, its exit its verdict, a probe being the gate run again, `commits` the gate `commits` names.

## 6. What becomes of `tools/merge-branch.mjs`

### 6.1 What Daoris takes, and where it lands (D212 §14.2)

| Logic | Today | Lands in |
|---|---|---|
| Requests, the one door, freezing, the preview | `merge-queue.mjs:122-145`; `merge-branch.mjs:6679-6740` | **Workplace** |
| The candidate, a member's merge, re-forming, promotion and its fence, recovery by ancestry | `merge-queue.mjs:182-256`; `merge-branch.mjs:6223-6258`, `:6494-6571` | **Workplace** |
| The mechanical resolvers | `merge-branch.mjs:1360-1644`, `:4657-4661`; the twins `BacklogMerge`, `GeneratedWriters` | **Workplace**, twinned with the tool while it lives |
| Contained, superseded, the declared scratch | the driver's `LineHolds` and `TreeScratch`, reused | **Workplace** |
| The verdict port integration declares | none | **Workplace** declares it |
| The gates' run, the admission, the environment, the job, the confirm run | `merge-branch.mjs:3557-3580`, `:5111`, `:6400-6406`; HEAVYQUEUE1's `MachineAdmission` | **Review**, implementing the port (with `Sessions/Heavy/`, at mod-xvi) |
| The judge: signatures, base, prefixes, the outcome, memory | `merge-queue.mjs:261-394`; `tools/test-failures.mjs` | **Review**, its rules pure in `Review/Domain`, the failure reader a twin |
| Known red and sightings | `tools/ci-background.mjs`' findings | **Review** |
| The line's run and its executor off the pass | `merge-branch.mjs:6623-6673` (the detached runner) | **Workflows**, shared with D192's performer, composed by **Loop** |
| The `integration.*` codes | the queue's log lines | **Events** |
| The rule's `integrate` and the acceptance's shape | none | **Settings** |
| The steward's integration acts | none | **Roles**, with the shell's act tables |

### 6.2 What stays the repository's own

As this repository's declared runner (`--daoris-verify` with its modes, row j) and the parent's tools:
- the gate plan read from the handed declaration and the release workflow;
- the lane table (`REACH`) and `daoris.lanes.json`, and PROCREACH1's and the test map's censuses;
- the two lanes and their order (GATELANES1, GATELANES2, GATEORDER1, GATEBUILD1, REHLANE1 to 2b);
- the FLAKE1 and LEFT1 re-runs, MEMGATE1's waits, and class verdicts and standing (GATE6);
- `--full`, `--full --beside`, `--ci` and its watch, until FABRIC-PIPELINE1e runs the background check, and the stage's evidence (`--passed`);
- the commit check's rules, as the runner's `commits` mode, which integration's step 1 calls *(F7)*.

These read this repository's own layout, which the product must not carry.

**What retires at the flip** (row k), each refusing where the line's declaration says `by: daoris` and naming `daoris-driver integrate`: the
queue's verbs (`--preview`, `--message`, `--queue`, `--queue-run`, `--drop-batch <branch>`), and the merge in place with its resume verbs
(`<branch>` alone, `--batch`, `--continue`, `--rerun` and `--stale` on an open merge, `--gated`; `docs/development.md:183-192`), since it is a
second integrator *(F13)*. A direct commit to the line stays a person's act and re-forms the candidate, as today (`merge-queue.mjs:235`). The
subagents' worktree prune is §7.1's named exception.

### 6.3 The move, without a gap and without two integrators

1. **Rows a, c, d, j and b** (§11). Each fixes today's queue in place, or prepares a seam both integrators share: the composed environment,
   mechanical containment and rows, the declaration and its acceptance, the runner extended, then the confirm run under the strict policy. The
   steward's day improves before any driver row lands.
2. **Rows e to h, in Daoris, dark.** The run over git, the gates and judge, the transport wired, the page. Nothing of this repository changes. A
   test repository is the first user once row h lands.
3. **Row k's shadow.** For a day, Daoris plans every request the tool's queue takes (objects only: the merge tree, the resolution, the runner's
   `plan`), records what it would do beside the queue's outcome, and the steward reads where they differ.
4. **Row k's flip, fenced** *(C13, F12)*:
   1. **The fence lands on its own, and the running runner is the fenced one** *(C13)*. Committed code does not change a running process. The
      runner loops on the `queueStep` it loaded (`merge-branch.mjs:6638-6642`, its promotion at `:6531-6548`), and an invocation keeps any live
      runner by its pid (`:6651-6653`). So, before the owner's sitting:
      - **The fence row lands first.** It makes the runner read `by` immediately before each promotion, and record its own code identity (the
        blob ids of `tools/merge-branch.mjs` and `tools/merge-queue.mjs` it loaded) in the queue's record at its start. An enqueue that finds a
        live runner with another identity, or none, refuses with *an older runner runs: let it drain*.
      - **The old runner drains and ends.** Nothing more is queued behind it; it finishes its candidate and ends by itself once nothing is
        queued (`:6640`). Stale enqueue invocations only wait on the record, so draining the queue drains them.
      - **A fenced runner then shows its refusal.** The next enqueue starts a runner from the code on disk (`:6657`). It records the fenced
        identity, and, given a line whose declaration says `by: daoris` on a scratch repository of the tool's own tests, refuses to promote.
        `--queue` shows the identity before the flip proceeds.
   2. **The owner's one sitting.** The owner accepts the integration declaration's blob of the prepared flip branch, `integration.by: daoris`
      and the runner (an acceptance names a blob, never a moment, FABRIC-PIPELINE1a's point 1), and sets the install's rule for this repository
      to the branch rule carrying `integrate`. Daoris holds meanwhile, since the line's committed declaration says `by: tool`.
   3. **The flip is the tool's last request.** The tool closes its enqueue behind it. A request queued after it, or one the flip's own
      candidate splits off or hands back, is handed back with *integrated by Daoris from `<commit>`: request it there* (F32).
   4. **The flip promotes.** From that commit, the fenced runner, reading `by` immediately before each promotion, promotes nothing more and
      ends; Daoris's run, its rule and acceptance already in place, takes the next request.
   5. **The leftovers** *(F32, C27, F18)*. Each kind is admitted through §4.6's door for it:
      - **This repository's landings** (the install's `quest/` branches, landed under the old branch rule, queued or never queued, those handed
        back at step 3 among them): the recorded-landing door, bound to each landing's recorded unit and exact tip. The come-up sweep takes
        them where the rule says `request: done`; else the owner's card (row h) or the steward's act once row i lands.
      - **A landing whose tree LAND3's tidy took**: admitted from its record alone, its branch standing at its recorded tip. It needs no tree.
      - **The parent's own branches** (records, a subagent's): the records-branch door, the owner's card or the steward's act.
      - **A session branch or an unrecorded landing branch**: refused at the records-branch door, as everywhere.

   Neither integrator can promote outside its own committed interval, and the only interval in which neither integrates is the flip's own
   promotion. Round 1's *between the two, the tool integrates* is corrected: with the owner's acceptance and rule set before the flip, no hold
   interval remains *(C13)*.

## 7. The scripts the development runs on

### 7.1 The day's scripts and their homes

| Script, or procedure | What it does today | Its home | When |
|---|---|---|---|
| `tools/merge-branch.mjs` (6,837 lines, 233 top-level functions by `grep -c '^(export )?(async )?function'`) with `tools/merge-queue.mjs` (638) | integration: the queue, candidates, gates, bisection, promotion; and this repository's gate plan, CI and stage evidence | **this design**: the queue into Daoris (§6.1), the plan as the declared runner (§6.2) | rows a to k |
| The merge in place and its resume verbs | merges into the checkout on the cheap tier, for the parent to commit | **retires at the flip** (§6.2) *(F13)* | row k |
| The commit check | refuses a branch whose commits carry no allowed maker or whose worktree holds uncommitted work | integration's **step 1**, through the runner's `commits` mode *(F7)* | rows j, e |
| The private files' copy into the gate's tree | copies the declared patterns file the sensitive scan reads | integration's **step 2** *(F20)* | row e |
| The prune of subagents' worktrees (GATE2) | removes merged subagents' branches and trees and lifts the harness's lock, at every merge | **a named retained exception** while the parent's subagents are the parent's harness's and not the install's sessions; its home then is the install's own tidy (D88, D102, KEEPSIZE1) *(C26)* | follow-up SUBAGENTS1 |
| The dispatch of subagents (the `dispatch-subagent` skill, their worktrees, `tools/subagent-usage.mjs`) | starts and sizes the parent's own builders outside Daoris | **the install's driven sessions** (DOGFOOD1), their coordination CHANNELJOIN1's; until then the parent's | follow-up SUBAGENTS1 |
| `tools/shell-edit-guard.mjs` (40) | a PreToolUse hook refusing a branch worker's edit through the shell (D161) | **a named harness adapter**: it enforces, in the harness of an agent Daoris does not start, what the start's containment (D201, D212 §2.3's admission) enforces for the install's sessions; it goes with SUBAGENTS1 *(C26)* | follow-up SUBAGENTS1 |
| `tools/desktop-publish.mjs` (1,475) with `tools/service-publish.mjs` (155) | builds and publishes the desktop and service hosts; `--stage --from-gated` stages the install from the gate tree | **Daoris's own update step**: an `update` workflow of the install (build from a commit the background check passed, stage, install when idle, D139), the build the publisher's code run as its declared command | follow-up UPDATESTEP1 |
| `tools/family-rehearsal.mjs` (6,239) and `tools/deployment-rehearsal.mjs` (1,815), with `tools/release-rehearsal.mjs` (585), `tools/rehearsal-kit.mjs` (1,071) and `tools/setup-kit.mjs` (226) | prove the product end to end from outside it: the family's routing over real hosts; a published install driven | **stay harnesses**: a proof of the product must not run inside the product it proves, nor share its process, its home or its failure; *when* and *on which commit* they run, and their verdict, become the verification pipeline's `check` (D192, FABRIC-PIPELINE1d, 1g) | FABRIC-PIPELINE1g |
| `tools/usage-report.mjs` (1,483) | the machine log summarised for a session | **the product's usage view** (This Daoris), the machine log's readers in Events; the tool stays the terminal's read until then | follow-up USAGEVIEW1 |
| The steward polling the install's sessions in a loop | finds finished, failed and waiting sessions by asking again | EVENTHOOK1's push (`work/event`, the relay), EVENTHOOK1b, c and h | existing rows |
| The steward reading a session's handback out of its event log | finds what a session said it did | a **handback record** the session writes once, which the CLI and the MCP serve, and which RUNENDS1's end reads | follow-up HANDBACK1 |
| Hand-written briefs repeating the same proof clauses | each ask carries the same test rule, censuses and records steps | a workflow kind's standing instruction or a brief template per kind (D157's kinds), filled by the ask | follow-up BRIEFKIND1 |
| The steward coordinating its own subagents outside Daoris | its subagents work beside the install's sessions and meet only at the merge | an outside agent joining a line's channel (D188's channel) as a participant | follow-up CHANNELJOIN1 |
| Ending stuck runs and clearing its own environment by hand | ends a runner, unsets the home's variable | integration's owned cancellation (§5.6) and the composed gate environment (§5.5) | rows a, f |

UPDATESTEP1, USAGEVIEW1, HANDBACK1, BRIEFKIND1, CHANNELJOIN1 and SUBAGENTS1 are named follow-ups, each its own design or row after this one, none
part of this design's first rows.

### 7.2 Every other tool, by kind

Line counts by `wc -l` at `7ebeba1ba`; tests beside each are not listed.

| Kind | Tools (lines) | Keep or move |
|---|---|---|
| Integration and CI | `merge-branch.mjs` (6,837), `merge-queue.mjs` (638), `ci-background.mjs` (485), `test-failures.mjs` (354), `processes.mjs` (205), `as-merged.mjs` (176), `index-lock.mjs` (66) | the queue, the judge, the findings and the process-tree ending **move** (§6.1); `as-merged.mjs` stays (a gate's command), `index-lock.mjs` with the tool |
| Censuses and checks that read the repository | `process-reach.mjs` (906), `test-map.mjs` (701), `test-recall.mjs` (379), `doc-duplicates.mjs` (298), `doc-shapes.mjs` (265), `doc-links.mjs` (257), `optional-locks.mjs` (254), `live-logins.mjs` (166), `partials.mjs` (161), `doc-budgets.mjs` (142), `read-shares.mjs` (141), `doc-system.mjs` (139) | **keep** |
| Generators and git's drivers | `orient-index.mjs` (1,425), `merge-rows.mjs` (308), `move-files.mjs` (244), `release-prep.mjs` (181, the release workflow's), `stage-package.mjs` (55) | **keep** |
| Publishing and the install | `desktop-publish.mjs` (1,475), `service-publish.mjs` (155) | **move** to the update step (UPDATESTEP1) |
| Rehearsals and their kits | `family-rehearsal.mjs` (6,239), `deployment-rehearsal.mjs` (1,815), `rehearsal-kit.mjs` (1,071), `release-rehearsal.mjs` (585), `setup-kit.mjs` (226) | **keep as harnesses**; their scheduling moves (FABRIC-PIPELINE1g) |
| Instruments and measurements | `usage-report.mjs` (1,483), `desktop.mjs` (1,090), `knowledge-bench.mjs` (900), `orient-report.mjs` (715), `question-bench.mjs` (683), `ux-count.mjs` (665), `knowledge-server.mjs` (593), `testbed.mjs` (308), `subagent-usage.mjs` (211), `cdp.mjs` (205) | **keep**; `usage-report.mjs` moves (USAGEVIEW1); `subagent-usage.mjs` goes with SUBAGENTS1 |
| Probes and proofs (dated evidence, run by a person) | `chromium-probe.mjs` (390), `proof-kit.mjs` (321), `int4f-proof.mjs` (266), `acp2-proof.mjs` (260), `edge-probe.mjs` (237), `acp-trust-probe.mjs` (223), `perm2-proof.mjs` (221), `console2-probe.mjs` (213), `console3-probe.mjs` (137) | **keep** |
| Helpers and harness adapters | `fsx.mjs` (222), `listen-port.mjs` (47); `shell-edit-guard.mjs` (40), a harness adapter (§7.1) | **keep**; the adapter goes with SUBAGENTS1 *(C26)* |

## 8. Who acts

- **Daoris** (the home's lease holder) runs every step of §5.2 under the accepted declaration and the rule. It requests only through a unit's
  landing where the rule's `autoAccept` makes one at done. It never pushes, publishes, rewrites the line or a pushed ref, removes by force, or runs
  a command its accepted blob does not name.
- **The steward** (D209), once STEWARDROLE1d activates the role, gains new acts. Each is one route in one exact form, with a record of its own
  naming the steward and its appointment's generation, overruled by the owner. Each is refused for a repository whose landing pushes, and for a
  rule whose `gates` is `none` *(F9)*:
  - `request`: a records branch, or an existing recorded landing the owner's press or a done already made (§4.6), only where the rule says
    `request: steward`; **never a unit's landing**, which is the Accept *(F31)*; for a unit, the act is the start of its `integrate` node under
    `request: steward`, the step's or the rule's *(R4, §17.4)*;
  - `drop`: a request, or a candidate, its job closed;
  - `requeue`: a handed-back request at the same tip, once;
  - `recheck`: a stopped line;
  - `decide`: a candidate the policy held, by `split` or `hand-back`, **never `land`**, which is the owner's press on the Integration view or the
    rule's `unconfirmed: land` *(F9)*;
  - `order`: a request first or last.

  **Its pending effects meet an execution cutoff at promotion** *(C12)*. A steward's request is recorded with its act's id, its generation, the
  rule and policy as they stood, and its frozen tip; step 2 checks, and step 8 checks again, that the appointment and generation are live, that the owner has
  acted on none of its repository, quest or session since (D209's owner-set), and that the rule still integrates and admits a steward's
  request. A failure drops that member from the candidate, said. A revoke cancels every steward effect not yet executed. A candidate the
  owner holds, or a request only the owner may make, is released by no steward act. Work authorized independently (the owner's press, a done
  under `autoAccept`) is not a steward effect and is not withdrawn by a revoke.

  This amends D209 point 3: integrating into a local line under an accepted declaration, by the acts above, is no longer among *landings* there.
  Those keep meaning a push or a publication and every acceptance of what is delivered, the Accept of a unit's landing among them; closing a
  quest as done stays the owner's.
- **The owner** is still asked:
  - to accept each repository's integration declaration by its blob, and every later blob, since it runs commands unasked (a widening, D209
    point 3);
  - to set a rule's `request: done` and `unconfirmed: land`, each lowering the owner's part (D192's lowering table gains both);
  - for `decide land` on a held candidate;
  - for every push, publication and history rewrite (D191 point 3);
  - for a removal by force;
  - for what a hold names as theirs.

  The owner is no longer asked for a press per integration where `request` is `done`.

## 9. Rows that touch the same files

**The rule, stated:** two rows that change the same files are built one after the other, never from one base at once. Daoris holds it where the
paths are declared and the rule says `overlap: wait`, and says it everywhere else it can see it:
1. **Declared paths** *(F24)*. An ask and a quest may carry `paths` (globs), as a backlog row names its `file:line`. It is a record field of the
   ask and the quest in the service, with its home, its one writer (the publisher: the person, the steward's publish, or Ask Daoris's proposal at
   the person's press) and its travel with the quest as its body does, added through every place of `session-record-fields.md`'s chain. An
   agent's own declaration may add paths that hold its start, and never removes another's. At a start the planner compares a quest's paths with
   every open quest's of the repository, declared, or already changed on its branch against the line: one `git diff --name-only` per open unit
   of the repository, bounded by its running sessions. An overlap is said on both (`overlaps <quest>: <paths>`). Under the rule's `overlap: wait`
   the later start waits until the earlier unit's request is settled, read from the integration record (§5.1).
2. **Observed overlap.** Two running units whose branches change one path are each told once (an ordinary word), and under `order: apart` the
   later is formed after the earlier (§4.4), so it meets the earlier's work at its own replay rather than at a shared bisection.
3. **The steward's own publishing** keeps DOGFOOD1's practice, now the planner's where paths are declared: same-file rows are published one
   after the other.

No route reads a model's judgement of which files a row will touch (D192 point 1).

## 10. Rejected

- **Keeping the tool as the integrator, fixed in place.** Rows a, c, d, j and b do fix it, as stepping stones; kept as the end it leaves the
  procedure outside the product, unseen by the page and inoperable by the role (§3).
- **Daoris carrying this repository's gate plan.** Lanes, reach and the Process census are the repository's knowledge of itself; the runner
  contract keeps them its own.
- **A runner contract of its own** beside D192's (round 1's `plan|run|probe --report <dir>` under a second key): two contracts for one runner,
  the weaker of the two; D192's extended instead *(F5, C8)*.
- **The runner reading its gates from the candidate's tree.** The members' declaration would choose the commands *(F6)*.
- **`integrate` on a merge rule, or anywhere an older build would merge** *(C9)*: an older build ignores the key and merges ungated.
- **An `integrate` node in any graph by this design** *(C10)*: a schema-1 run is D157's to its end, a bound graph is never mutated, and Current
  cannot govern a named run; a node kind may come later as a new version a person saves. **Reversed in round 4** *(R4, §17.8)*: the owner's
  direction makes integration a step of each unit's flow; the three reasons stand as constraints on the kind (graph versions only, new versions
  only, Current's own branches only).
- **A pipeline marked `integration`** (D192's mark). Its steps are fixed and ordered by safety (replay before gates before promotion), the
  person's choices are options, and a version per change of options would bind runs to graphs nobody edits. Reopened if a repository needs steps a
  plugin contributes inside integration. *Stands in round 4* (§17.8): a flow requests the line's run and never versions it.
- **A node per step on each unit.** A candidate is shared by several units; the steps are the line's run's. *Stands in round 4* (§17.8): a unit
  has one `integrate` node, whose detail names the run's step.
- **Promoting by a merge in the checkout** (today's merge form). It can half-apply in a checkout Daoris did not create; a fast-forward of a clean
  checkout, or an `update-ref` where none has the line out, moves only to a commit already gated.
- **A weight field on an admission entry** *(C7)*: an older writer rebuilds entries from the fields it knows and counts entries, so it would drop
  the weight and admit work beside the confirm run.
- **`unconfirmed: land` as a default, or flipped by a replay's reading** *(C6, C16, C17)*: the confirm run is evidence, not proof (§4.5); `land`
  is the owner's choice where a whole-set verification runs.
- **Landing every unconfirmed failure, reached or not.** A member's own race would land with only a sighting.
- **Re-running a red candidate until it passes.** D207's rejection stands (`docs/decisions/D207.md:163`); one confirm run, bounded.
- **Confirming by the class alone** (today's prefix selection). A test polluted by another in its class passes alone (`merge-branch.mjs:245-251`).
- **A wider window or a longer timeout.** No fix (`held-clocks.md`); the sightings feed the held-clock rows.
- **A line rule that keeps one side's copy** (round 1's `by: line`) *(F10, C21)*: the merge drops the member's hunk while its tip stays an ancestor,
  so the tidy proves containment by ancestry and removes the branch with no recovery ref.
- **Containment in an unpromoted member settled at once** *(C4)*: the line never held it if that member fails.
- **Daoris resolving a conflict of meaning, or a strategy option.** D87's rejection stands (`docs/decisions/D87.md:104-107`).
- **A landing agent in this design** *(C19, F25)*: deferred (§5.4).
- **`request: done` or `request: steward` with no gates.** A merge with neither a press nor a check is what D145 point 1 guards, and a role's act
  landing ungated work on the line is a landing in D209's sense.
- **The steward's `decide land`** *(F9)*: landing over the owner's `hold` is the per-candidate form of the lowering.
- **`autoAccept` on a merge rule as the switch.** An older build skips a merge rule carrying it (`LandingKey.cs:118-120`, `:385-387`).
- **The whole declaration in `driver.json`**, or **the whole in the repository**: the repository's facts belong with its files; a trust choice in a
  file a session edits is an agent granting itself permission (`development-documents.md`).
- **Requiring a verification pipeline to integrate** *(F11)*: the landed route's acceptance belongs to D192's route; integration takes one of its own.
- **Two integrators during the move**, or a flip fenced only at its doors *(C13)*.
- **Reverting a promoted candidate the background run finds red.** D192's rejection of an automatic revert stands; it is filed and fixed forward.
- **Moving the rehearsals into the product** (§7.1).
- **Detecting same-file rows by a model's reading** (§9).

## 11. The rows, in dependency order

⚑ marks a row that can run beside FOUNDATION1 without touching its files. Each row is its own branch, lands through the integrator of its day, and
names its proof. Round 2 re-orders them: environment, mechanical, declaration, runner, then the confirm run, each before any driver transport
*(C16)*. Round 4 *(R4, §17.9)* keeps a, c, d, j, b, e and f as they stand, amends g, h, i and k, and adds o and r (the work's requests, Current's
step) before the driver transport, n (the `integrate` node) before g, q (the presets) after h, and p (the `request` node) after l.

- **INTEGRATE1a ⚑ — the queue's gates start clean (QUEUEENV1).** Tools only, after HEAVYQUEUE1 lands (one file). The runner reads the home first,
  then starts every gate, probe, re-run and rehearsal with §5.5's composition. Proof: a tool test that starts the runner with `DAORIS_HOME`, a
  role variable and `GIT_DIR` set and a stub gate printing its environment, red before, green after, the admission still found *(F20)*.
- **INTEGRATE1c ⚑ — mechanical resolution, the command-free part.** The tool and the driver's twins: a contained member recorded, provisional
  where an earlier member contains it, carried through split and re-form; declared row tables (`by: rows`) in `mergeByBase`'s and
  `BacklogMerge`'s twin, one fixture table; union joins set apart in the driver's replay. Line rules' `run` writers wait for row d's acceptance
  *(C6)*. Proof: the table in both; §2's two set-asides as fixtures, landed; a contained member whose container is then handed back, re-judged.
  The driver half touches `Workplace/`, moved at mod-x: beside FOUNDATION1 while no open branch holds those files. *Unchanged by round 4, and
  valid as the line run's internals* (§17.9): its resolutions are steps 3 and 4 of §5.2 and the landing's replay, neither of which round 4 moves.
- **INTEGRATE1d ⚑ — the declaration, read and accepted, nothing acting on it.**
  - Contents: `daoris.gates.json`'s `integration` (tool and driver twins); the rule's `integrate` on a branch rule (`LandingKey` and the CLI's
    `driverconfig.ts`, one table); the acceptance of its own, `integrationGates`, with *Accept the gates*; GENREGEN1's writers reading either
    acceptance, and the line rules' `run` writers run under it; the declared `scratch` taken by the landing's own tidy (LAND3, at `handed`),
    TIDYOUTPUT1 folded in here *(F35)*; Setup's rows shown.
  - Refusals: `integrate` beside a plugin, on a merge rule, or beside the rule's `squash`; `done` without `autoAccept` or gates; `steward` without
    gates; `land` where no whole-set verification runs on the line.
  - Proof: the twins' table; an older build's read of a rule carrying `integrate` as the branch rule it is; each refusal; a landing's tree whose
    only extra files are its declared scratch tidied without `--force`.
  - *Round 4* (§17.7, §17.9): the key's shape is unchanged; its `request` and `conflicts` are read as Current's source of the unit's half, which a
    named workflow's `integrate` step replaces (row n), and the rest as the line's.
- **INTEGRATE1j ⚑ — the runner contract, D192's extended, and this repository's runner.** `verification`'s `modes` and inputs (§5.9);
  `tools/merge-branch.mjs --full --beside --daoris-verify` (D192 point 9's command) answering `run`, `plan`, `probe` and `commits`, its gates read only from the handed declaration *(F6)*,
  the commit check as its `commits` mode *(F7)*, `DAORIS_VERIFY_*` stripped from what it starts; shared with FABRIC-PIPELINE1g. Proof: the contract's
  validation table (each unknown case of §5.9); a schema-1 answer when no mode is asked, read as D192 reads it; a probe as composed reproducing a
  pollution within a class; a member's edit of a gate's command not run. After row d.
- **INTEGRATE1b ⚑ — the confirm run, under the strict policy.**
  - Contents: the confirm run with the whole admission taken as ordinary entries in HEAVYQUEUE1's record, through its two twins and their shared
    table, with nothing added to an entry's schema *(C7)*; inconclusive where the admission is not shown; sightings; the policy `split` for both,
    read from the tool's own switch file under the main checkout's ignored `local/` until row d's rule reads it, set to `land` only on the owner's
    say and never by the steward *(F19)*.
  - Measured: the historical replay of §2's 29 runs, for the policy's accounting alone; prospectively each confirm run's composition, admission
    evidence and wait, the share of recurrences across classes, and the wait it imposes on driven sessions *(C16, F15, F16)*.
  - Proof: tool tests (a stub gate failing once beside a held load and passing quietly splits under `split` and promotes under `land`; one failing
    twice is bisected; a pollution within a class fails again as composed; another live hold during the confirm run makes it inconclusive; an
    admission that cannot be written makes it inconclusive; a `drop` ends it).
  - Its driver half edits `Sessions/Heavy/MachineAdmission.cs`, which FOUNDATION1's mod-xvi moves to Review: beside FOUNDATION1 only while no
    mod-xvi branch is open.
- **INTEGRATE1o — the work's own requests drawn, and held where the flow says** *(R4, §17.3)* (Workflows, web).
  - Contents: each quest a session of the run published (`WorkJoin.Published`) drawn inside the work node on the run's view and by `daoris-driver
    workflow run`: its repository, its state, whether its session waits on it (`Awaits`), its outcome (the close's note); a helper's ask beside
    it as a request to an agent. `claimed`'s option `requests`, `wait` or `go-on`, absent being `go-on`, read on a graph version alone; under
    `wait` the work is not reached while one is open, and a decline holds it for the person, whose *Go on without it…* with words, a Person
    route kept on the run's record, passes that request over: a wait kind in every place `person-waits.md` names, its route in every place
    `bridge-presses.md` names.
  - Proof: a run whose session published to another repository draws it, en and zh and on the terminal; under `wait` the work waits until that
    quest closes, and a decline holds it until the person's press; a run with the option absent, and every Current run, reads as before.
  - Beside FOUNDATION1 while no mod-xviii branch is open: it touches the run's derivation in `Workflows/` and the web alone.
- **INTEGRATE1r — Current and the choice know the step** *(R4, §17.7)* (Workflows, web; after d).
  - Contents: Current's derivation (`WorkflowCurrent`) and its twin table gain the `integrate` step where the effective rule carries `integrate`,
    its source and its limit said (*not integrated yet* until row g); the Workflow tab and `daoris driver workflow show` draw it; the choice's
    `use` refuses, and the gate holds as *cannot start*, a version its repository's declarations cannot carry, naming the Setup row (D157 §2.3):
    an `integrate` with no pattern declared, after a landing whose plugin (the version's or the rule's) is one, or under `done` or `steward` with
    no accepted gates; a `pushed` or `pull-request` after a landing no plugin pushes (§17.7's table).
  - Proof: the twin table's rows; the tab in en and zh; each refusal and hold.
  - Beside FOUNDATION1 while no mod-xviii branch is open.
- **INTEGRATE1e — the integration run over git, unwired** (Workplace).
  - Contents: the store family; the one door with its recorded-landing path (bound to the recorded unit and exact tip, the recorded gate proof
    read) and its come-up sweep under `request: done` *(F32, C27)*; request with the commit check, form under the trees home with the private
    files and the requester's check, replay with provisional containment, resolve, conflict as data, promote with its fence and authority check,
    settle, the landed branch's content-held removal with its recovery ref *(F35)*, recovery by ancestry; gates behind the verdict port.
  - Proof: a Git Process class per step, each crash point (after the line moved, before its record), each promotion path, the fence (a
    `by: tool` line refused at promotion), the ref transactions (`ref-removal-transactions.md`), §2's library cases (a status line by its
    writer, a superseded commit kept visible), and the recorded-landing door's cases. Those cases: a landing whose tree LAND3's tidy took,
    integrated from its record; a recorded landing whose branch moved, refused; a session branch refused at the records-branch door; the sweep
    requesting each standing landing once. Its classes join the git half within its target of
    about 28 class-minutes (D207 point 7), each measured *(F29)*.
  - Starts once the open branches cutting Workplace's edges have landed: mod-x and mod-xi are built (`docs/decisions/D212.md:1309-1313`), and it
    adds new files in Workplace's folder alone *(F23)*.
- **INTEGRATE1f — the gates and the judge in the driver** (Review). The verdict port's implementation; the runner contract's caller and the direct
  run; the admission's places and the whole admission; §5.5's environment; the candidate's job; the failure reader's twin; the judge pure in
  `Review/Domain`, twinned with the tool's table, the known-red expiry branch among its cases *(C14)*. Proof: the twins' tables; a job closed ending a
  grandchild; a confirm run waiting for a session's heavy run. After mod-xvi.
- **INTEGRATE1n — the `integrate` node kind** *(R4, §17.4, §17.6)* (Workflows).
  - Contents: the kind, its reach (`waited`), its authority (the integration record, a new `NodeSource`), its subject (the landed branch at
    `handed`'s recorded tip), its graph rules (one, after a `handed` on a branch with no plugin, no `pushed` or `pull-request` beside it, never in
    a verification pipeline); Current's branch graph under a rule carrying `integrate`, a `cycle` wiring from it and never into it; the arming on
    it; **the waited reach's start**, an adapter's start by key, its reconcile and its wake on the record's events, `integrate` its first adapter,
    calling row e's door; the lowering table's row (`request` moved from `owner` to `done` or `steward`). Dark until g: an admitted request waits
    queued.
  - Proof: a node through each state over a stand-in record; a restart finding its request by its key; a new landing tip invalidating it and
    requesting again; a version naming it read as unreadable by the older reader's table; an older build's Current graph for the same rule
    holding no node and writing no record; a schema-1 run's landed branch integrating through Current's graph.
  - After e and mod-xviii.
- **INTEGRATE1g — the transport wired** (Workflows, Loop).
  - Contents: the line's run off the pass, shared with FABRIC-PIPELINE1d's executor; ~~a branch rule carrying `integrate` requesting through its
    landing~~ requests made by the unit's `integrate` node (row n), Current's or a version's *(R4)*; the merge rule's press carried by the transport
    as the named legacy exception; conflicts to the session or held; the events; `work.integrated` at `line`.
  - Unchanged: ~~no graph, no run's record,~~ `work.landed`; the node's state is its record's, the integration's the integration record's *(R4)*.
  - Proof: LANDREBASE1's and LANDBACKLOG1's Process classes green through the transport with their outcomes unchanged; a done quest integrated
    under `request: done`, through Current's branch graph and through the *Integrated into the line* preset's *(R4)*; a stopped `handed` refusing
    its request *(C5)*; a revision after the request offered, not queued, under `owner` *(C11)*.
  - After row n, FABRIC-PIPELINE1d and mod-xviii.
- **INTEGRATE1h — the page and the terminal.** The Integration view and its slice, the head's line, *What needs you*, `daoris-driver integrate` as
  Person routes, Ask Daoris's coverage, words and glossary; the run's view drawing the `integrate` node with its record's detail, and *What needs
  you*'s wait for a recorded landing whose request is the owner's card *(R4, §5.8, §17.4)*.
  Proof: the slice's tests, the page's tests in both languages, the pipe census, the installed window.
- **INTEGRATE1q — the two presets** *(R4, §17.7)* (Workflows, web; after h).
  - Contents: *Integrated into the line* and *A pull request* as graph presets in `WorkflowPresets` beside the four steps presets, each line
    composed from its nodes, with the opinion and look variants and *with no press*; `daoris driver workflow new --from <preset>` saving a graph
    version; the Workflow tab's offer; words in en and zh.
  - Proof: each preset saved reads as a graph version and binds a run; *with no press* refused where the line declares no accepted gates; the pull
    request's preset over a stand-in plugin reaching its end; the line preset integrating a done quest end to end on a scratch repository.
- **INTEGRATE1k — this repository moves.**
  - Order: the fence row first, landed alone (the runner reading `by` before each promotion and recording its code identity, an enqueue refusing
    behind a runner of another identity); the old runner drained and ended; a fenced runner shown refusing; then the shadow day; then the owner's
    sitting and the flip (§6.3); then the leftovers through the recorded-landing door (the sweep or the owner's card) and the records-branch door.
    `docs/development.md`'s integration section is rewritten.
  - *Round 4* (§17.13 question 6): at the owner's sitting this repository follows Current with its rule carrying `integrate`, as round 3
    accepted, its units integrated through Current's branch graph (row n); choosing the *Integrated into the line* preset instead is the owner's
    later press.
  - Proof:
    - an upgrade test that **begins with a live unfenced runner**: it keeps running and is not replaced, the enqueue refuses until it ends, and
      a fenced runner then starts and refuses a `by: daoris` line *(C13)*;
    - a day's shadow with its differences read;
    - the fenced runner refusing to promote on a `by: daoris` line;
    - a leftover landing whose tree was tidied, integrated from its record *(C27)*;
    - the first promoted candidate on the install.
  - After row h *(F18)*.
- **INTEGRATE1i — the steward's integration acts** (Roles and the shell's act tables), amending D209 point 3, with the execution cutoff at
  promotion *(C12)*. Proof: each act's record; a revoke cancelling a pending request; an owner's act on a repository making its candidate's
  steward-requested member leave at form or at promotion; `decide land` refused to the role; **a steward's request of a unit not yet landed
  refused**, since the role never makes the Accept *(F31)*. *Round 4* (§17.4): for a unit, the act starts its `integrate` node under `request:
  steward`, the step's or the rule's, so its proof gains that start's record naming the act. After STEWARDROLE1d and row n.
- **INTEGRATE1l — rows that touch the same files** (§9): the `paths` field's chain, the start's comparison and its bound, the told overlap. Proof:
  an overlapping start held and said; two running units told once each; an agent's declaration unable to release another's hold.
- **INTEGRATE1p — the `request` node** *(R4, §17.5)* (Workflows, service; after n).
  - Contents: the kind, its options (`to`, `title`, `body`, `start`, `until`, `declined`), its reach and authority (the commitment's, as
    `wait.unit`'s row has it), its subject (the quest it published, at its revision); the quest's requester, the run and the node, as a field
    through every place `quest-operations.md` names; its start publishing once, found first by that field; its wait read from this machine's
    projection of the quest; its outcome drawn; its words in en and zh. Built on row n's waited start and the exchange as it stands, as
    FABRIC-DELEGATE1's first form and with FABRIC-WAITUNIT1's adapter (§17.13 question 5).
  - Proof: published once across a restart; reached on the quest's done, or its taking under `until: taken`; a decline holding for the person;
    a quest another machine took read through the projection; nothing of the receiving repository's files read; `start: automatic` said as a
    standing say-so where it is set.
- **INTEGRATE1m — another repository on integration.** A quest published to the library repository asking for its own `integration` declaration
  (its gates, commit check, scratch and line writers), never written from here; the install's rule for it set by the owner.

**Changed in round 2:** a, b, c, d, e, g, j and k (§15.1); the landing agent is dropped from the rows; the order is a, c, d, j, b, e, f, g, h,
k, i, l, m. **Changed in round 3:** d (the declared scratch in the landing's tidy), e (the recorded-landing door and its sweep, the landed
branch's removal), i (no steward request of an unlanded unit), j (D192 point 9's command), k (the fence row first, the live runner drained,
the leftovers' doors) (§16); the order is unchanged. **Changed in round 4** *(R4, §17.9)*: g (requests through the node), h (the run's view),
i (the act starts the node), k (Current at the sitting); c and d noted, unchanged; n, o, p, q and r new. The order is a, c, d, j, b, o, r, e, f,
n, g, h, q, k, i, l, p, m.

Follow-ups named in §7.1, outside these rows: UPDATESTEP1, USAGEVIEW1, HANDBACK1, BRIEFKIND1, CHANNELJOIN1, SUBAGENTS1; and later designs: ~~an
`integrate` node kind (§5.1)~~ (round 4's row n), a landing agent (§5.4), gates before a plugin's push, and round 4's later kinds, a `push`
and a `release` (§17.10).

## 12. Decided by the steward

Round 2's open questions, each settled by the steward on 2026-10-28 under D191 (§15.2):

1. **`probe`**: a mode integration requires and any runner may answer; a `check` asks it only once known red by signature exists.
2. **The acceptance**: separate, `integrationGates` (§4.1).
3. **The flip**: the owner accepts the flip branch's declaration and sets the install's rule in one sitting before it promotes (§6.3).
4. **The integration tree**: it counts against KEEPSIZE1's allowance and is never evicted while a run holds it (§5.2 step 2).
5. **The legacy merge rule**: the ungated merge rule rides the new path as a named exception until row g. Read here: today's press-merge path
   stands until row g lands, and row g carries it onto the transport as the named exception (§4.2's last row).

## 13. What stays unmeasured, and what this design's checks do not cover

It is accepted and a document, nothing of it built: the documentation gates prove its links, routing and shape, not its claims. Every
`file:line` of §1 was read at `7ebeba1ba`, and the in-flight branches by `git show` at their tips of 2026-10-28; nothing was built or run but those reads, the decision index's
generator and the document checks. §2's numbers were counted from the queue's records under `local/` by a reading script, never written there, and
neither reader read them; the steward's accounts in §2 (the five hours, the eleven waiting, the census miss, the library's day) are the brief's
and were not read from a record. Unmeasured, each the named row's to measure:
- how many confirm runs under the whole admission pass, and how long that admission keeps driven sessions waiting (row b, prospectively);
- how many recurrences were pollutions across classes, which a confirm run as composed does not reproduce (row b; F15);
- whether a member can make a unit outside its census reach flaky without its failure recurring, the exposure a `land` choice accepts and the
  backstop (later tips, standing evidence, sweeps and nightly runs, not every tip whole: C3) may find late;
- the integration tree's disk (row e);
- the Git Process classes' cost against the git half's target (row e).

Not read: the library repository's own files (another repository, described only from the brief and TIDYOUTPUT1), the install's live home, the
steward's own procedures (described from the brief), and the integration reviews of 2026-10-05 and 2026-10-10 past their router rows.

Round 4's *(R4)*: §17.14.

## 14. Reads

Round 1, read at `6f1ee69cb`: Codex, *another round* (C1 to C26, 10 blocking); Claude Fable, *another round* (F1 to F30, 6 blocking). Round 2,
read at `cd12f0399`, confined to its changed sections: Codex, *accept after named changes* (C4 to C12 resolved, C13 in part, C27 new and
blocking); Claude Fable, *accept after named changes* (F5 to F10 resolved, F31 and F32 new and blocking, F33 to F39 non-blocking). Each finding
was checked at the lines named. The steward's column gives the steward's verdict of 2026-10-28; *taken* is the steward's verdict on the
disposition as written.

**Round 1's findings**

| Reader | Id | Blocking | Verified at | Disposition | Where | Steward |
|---|---|---|---|---|---|---|
| Codex | C1 | no | `LineHolds.cs:73-116`; `TreeLands.cs:525-530`, `:583-591` | taken: the driver's containment proofs told apart from the missing settled outcome | §1.2, §1.5 | taken |
| Codex | C2 | no | `merge-branch.mjs:6657-6658` (detached, unreferenced) | taken: the weakness is ownership, cancellation, recovery and visibility, not the shell's lifetime | §1.1, §1.5, §3 | taken |
| Codex | C3 | no | `merge-branch.mjs:6031-6066` (the newest tip per poll); `docs/development.md:288-361` | taken: the backstop is later tips, standing evidence, sweeps and nightly runs | §1.1, §4.5, §13 | taken |
| Codex | C4 | **yes** | `merge-queue.mjs:192-209`, `:232-240`; foundation `:1157` | taken: containment in an earlier member provisional, settled only at its promotion, carried through split and re-form | §5.2 steps 3, 9; §10; row c | taken |
| Codex | C5 | **yes** | `TreeGates.cs:40-57`; `GraphGate.cs:113-140` | taken: one door; a unit's request only through its own landing and gate at the pinned commit, or (round 3) its recorded landing; a records branch on the owner's card or the steward's act | §4.6, §5.2 step 1; row g's proof | taken |
| Codex | C6 | **yes** | `GeneratedWriters.cs:44-52` | taken: `split` the permanent default; `land` the owner's; row b's switch not in the repository; executable line writers wait for row d's acceptance | §4.1, §4.5, §11 rows b, c | taken; decided: the acceptance is separate |
| Codex | C7 | **yes** | HEAVYQUEUE1's `machine-admission.mjs:78-88`, `:114-119`, `:151-154`, `:145-170`, `:200-213` | taken: the whole admission as ordinary entries any writer counts; another live hold, or an admission not established, inconclusive and never `land` | §4.5; §10; row b | taken |
| Codex | C8 | **yes** | `pipeline-design.md:289-294`, `:327-340` | taken: D192's result rules carried, with base, declaration blob, composition and the complete accepted plan required for promotion | §5.9 | taken |
| Codex | C9 | **yes** | `LandingKey.cs:359-388`; `TreeLands.cs:287-290` | taken: integration rides a branch rule, so an older build makes the branch and merges nothing; today's merge rule the named legacy exception | §4.1, §4.2, §4.7; §10 | taken |
| Codex | C10 | **yes** | `D190.md:13-18`; `NodeGraphs.cs:747-762` | taken: no node added to any graph; a transport outside every run, observed by `line`; a node kind only by a later explicit version | §5.1; §10 | taken |
| Codex | C11 | **yes** | `TreeLands.cs:598-604`; `merge-queue.mjs:131-145` | taken: each revision requested again by the same rule; only a done under `autoAccept` makes one by itself | §4.6, §5.2 step 9, §5.7 | taken |
| Codex | C12 | **yes** | `D209.md:66-78`, `:95-105` | taken: the steward's request recorded with its generation, policy and tip, re-checked at form and at promotion; revoke cancels; owner-held never released by the role | §8, §5.2 steps 2, 8; row i | taken |
| Codex | C13 | **yes** | `merge-branch.mjs:6494-6554` | taken: both integrators read `by` immediately before promoting; the flip closes the tool's enqueue; round 1's *between the two* corrected; completed in round 3 by the live runner's upgrade | §5.6, §6.3; row k | taken |
| Codex | C14 | no | `merge-queue.mjs:300-311`; `D207.md:139-148` | taken: the expiry branch in the judge's table with its regression | §1.1, §5.3; row f | taken |
| Codex | C15 | no | foundation `:2335`, `:2342`, `:2348-2353`, `:2363-2373` | taken: Settings the shape, Workplace the policy and its verdict port, Review the execution and sightings, Loop's executors kept | §5.6, §6.1 | taken |
| Codex | C16 | no | `merge-branch.mjs:6409-6413`, `:6448` | taken: the replay tests accounting only, the confirm run measured prospectively; the rows re-ordered | §4.5, §11 | taken |
| Codex | C17 | no | `D207.md:22-24` | taken (Q1): `reached: split`; agrees with Fable's answer | §4.5 | taken |
| Codex | C18 | no | `TreeLands.cs:537-558` | taken (Q2) with C9: one transport, authorization unchanged | §4.2 | decided: the legacy ungated merge rule rides the new path as a named exception until row g |
| Codex | C19 | no | `RebaseTelling.cs:16-26` | taken (Q3): hold first, landing agent deferred; agrees with Fable | §5.4 | taken |
| Codex | C20 | no | `LandedGates.cs:12-19` | taken (Q4): two homes, `unconfirmed` the install's; agrees with Fable | §4.1 | taken |
| Codex | C21 | no | `D87.md:232-247` | taken (Q5): `run` only; agrees with F10 | §4.1 | taken |
| Codex | C22 | no | `merge-branch.mjs:6223-6235` | taken (Q6): one warm tree per line with a lease, reset only after its processes end | §5.2 step 2 | decided: the integration tree counts against KEEPSIZE1's allowance and is never evicted while a run holds it |
| Codex | C23 | no | design §5.1 | taken (Q7): built in, machinery shared with D192's performer | §5.1 | taken |
| Codex | C24 | no | design §5.7 | taken (Q8): a superseded commit's disposition stays visible | §5.7, §5.8 | taken |
| Codex | C25 | no | `pipeline-design.md:327-340` | partly (Q9): one contract taken; `probe` a mode a check *may* answer, the readers differing on whether it belongs to the check (§15.2) | §5.9, §12 | decided: probe is a mode integration requires and any runner may answer, a check asking it only once known red by signature exists |
| Codex | C26 | no | `merge-branch.mjs:3408-3424`; `tools/shell-edit-guard.mjs:3-14` | taken: the prune a named retained exception, the guard a named harness adapter, each with its later home | §3, §7.1, §7.2 | taken |
| Fable | F1 | no | `TreeLands.cs:258-262`, `:583-591`; `LandedReview.cs`; `D102.md:465` | taken, with C1 | §1.2, §1.5 | taken |
| Fable | F2 | no | `WorkflowSelection.cs:52-56`; `LandedGates.cs:80-88` | taken: the acceptance is the landed route's today | §1.3, §4.1 | taken |
| Fable | F3 | no (feeds F5) | `pipeline-design.md:319-343`, `:366-368` | taken | §1.3, §5.9 | taken |
| Fable | F4 | no (feeds F8) | `event-hook-design.md:490`, `:496`; `NodeContract.cs:433-435` | taken | §1.3, §5.1 | taken |
| Fable | F5 | **yes** | `pipeline-design.md:319-343`, `:366-368` | taken: D192's `verification` extended by modes and inputs, schema 1 kept where no mode is asked; D192 amended by name only for a caller-handed tree | §5.9; D215 | taken |
| Fable | F6 | **yes** | `merge-branch.mjs:977-980`, `:6361` | taken: the accepted blob handed to the runner, its gates and commands read only from it | §4.3, §5.9; row j | taken |
| Fable | F7 | **yes** | `merge-branch.mjs:2836-2858`, `:6725-6728` | taken: the commit check, and the composed message, at step 1 through the runner's `commits` mode | §4.1, §5.2 step 1, §6.2, §7.1 | taken |
| Fable | F8 | **yes** | foundation `:1169`; `event-hook-design.md:496` | taken: with integration on a branch rule, `handed` and `work.landed` are today's, unredefined; `integration.queued` at the request | §5.1 | taken |
| Fable | F9 | **yes** | `D209.md:63-65` | taken: the steward's `decide` is `split` or `hand-back`; `land` the owner's; no steward request where gates are none | §8, §4.1 | taken |
| Fable | F10 | **yes** | `TreeTidies.cs:43-45`; `D102.md:243` | taken: no rule keeps one side's copy; a line settles only by its accepted writer | §4.1, §10 | taken |
| Fable | F11 | no | `WorkflowSelection.cs:52-56` | taken: an acceptance of its own, read by GENREGEN1's writers too | §4.1; row d | decided: the acceptance is separate |
| Fable | F12 | no | `merge-branch.mjs:6679-6740` | partly: no gap, by the owner accepting the flip's blob and setting the rule before the flip promotes, not by the tool reading `driver.json` (which would hand the tool the install's home) | §6.3 | decided: the flip is the owner accepting the flip branch's declaration and setting the install's rule in one sitting before it promotes |
| Fable | F13 | no | `docs/development.md:183-192` | taken: the merge in place retires at the flip; a direct commit stays a person's act | §6.2, §7.1 | taken |
| Fable | F14 | no | D192 point 7 | taken: `land` refused where no whole-set verification runs on the line | §4.1, §4.5 | taken |
| Fable | F15 | no | `docs/development.md:87-90` | taken: the confirm run's reach narrowed; cross-class pollution counted by row b | §4.5, §13 | taken |
| Fable | F16 | no | §2's walls | taken: the whole admission's cost measured; `drop` ends a confirm run | §4.5; row b | taken |
| Fable | F17 | no | `LandingKey.cs:110-135` | taken: `integrate` refused beside a plugin; the dangling row dropped | §4.1, §4.2 | taken |
| Fable | F18 | no | `TASKS.md:424` | taken: row k after h, the leftovers by the owner's `--request` (round 3: through the recorded-landing door) | §6.3; row k | taken |
| Fable | F19 | no | design §4.1 | taken: row b's policy from the tool's own switch, never the repository's file | row b | taken |
| Fable | F20 | no | `merge-branch.mjs:5027-5048`, `:6360`; `docs/development.md:205-207` | taken: the private files at step 2; the runner reads the home before composing | §5.2 step 2, §5.5 | taken |
| Fable | F21 | no | `compatible-builds.md` | taken: moot once no graph or run record changes, and said | §4.7, §5.1 | taken |
| Fable | F22 | no | foundation `:2342`, `:2348` | taken: sightings Review's | §5.6 | taken |
| Fable | F23 | no | `D212.md:1309-1313` | taken: row e starts once the open Workplace cuts land | row e | taken |
| Fable | F24 | no | `session-record-fields.md` | taken: `paths` a record field with its chain, its one writer and the read's bound; an agent's declaration only holds | §9; row l | taken |
| Fable | F25 | no | design §5.4 | taken: the landing agent deferred, with C19 | §5.4 | taken |
| Fable | F26 | no | `D145.md:70` | taken: the human step after a local integration is the owner's push | §4.6 | taken |
| Fable | F27 | no | `merge-branch.mjs:6241`; `TreeLands.cs:607-614` | taken: the merge's second parent is the replayed tip; settlement rests on patch id and content | §1.1, §5.2 step 3, §5.7 | taken |
| Fable | F28 | no | `BranchReplays.cs:244-252`; `pipeline-design.md:369-372` | taken: the integration tree under the trees home | §5.2 step 2 | taken |
| Fable | F29 | no | D207 point 7 | taken: row e's classes within the git half's target, measured | row e | taken |
| Fable | F30 | no | `D209.md:66-78` | taken: one spelling; the terminal's doors Person routes, the steward's `ROLE_ACT` | §5.8 | taken |

**Round 2's findings**

| Reader | Id | Blocking | Verified at | Disposition | Where | Steward |
|---|---|---|---|---|---|---|
| Codex | C13 | **yes** (in part) | `merge-branch.mjs:6638-6642`, `:6651-6653`, `:6531-6548` | taken: the fence row lands alone; the runner records its code identity and an enqueue refuses behind another; the old runner drains and ends; a fenced runner shows its refusal before the flip; an upgrade test begins with a live unfenced runner | §6.3 step 4.1; row k; D215 | taken |
| Codex | C27 | **yes** | `LandedBranches.cs:8-10`, `:50-55`; `TreeLands.cs:598-604` | taken, with F32: the recorded-landing door, bound to the recorded unit and exact tip, its gate proof the landing's own, from the owner's card, the steward's act or the come-up sweep; arbitrary branches refused at the records-branch door; the tidied-tree case | §4.6, §6.3 step 4.5; rows e, k; D215 | taken |
| Fable | F31 | **yes** | `D209.md:63-65`, `:311`; `TreeLands.cs:510-558` (a unit's landing is the Accept) | taken: the steward's request never makes a landing; it admits a records branch or a landing the owner's press or a done already made | §4.1, §4.6, §8; row i; D215 | taken |
| Fable | F32 | **yes** | `TreeLands.cs:598-604`; `LandedBranches.cs:50-55` | taken, with C27; a request the flip's own candidate splits off or hands back is handed back with the same words | §4.6, §4.7, §6.3 steps 4.3, 4.5 | taken |
| Fable | F33 | no | `pipeline-design.md:333-334`; `merge-branch.mjs:1997`, `:3736-3742`, `:245-251` | taken: `flake` read as a pass, as D192 and today's queue read it, this repository's runner giving it for FLAKE1's named classes alone | §4.5 | taken |
| Fable | F34 | no | HEAVYQUEUE1's `machine-admission.mjs:114-119`, `:151-170` | taken: inconclusive judged only from the confirm run's start, its entries written in one locked write, inconclusive read first | §4.5 | taken |
| Fable | F35 | no | `TreeScratch.cs:36-58`; `D102.md:243` | taken: the declared scratch belongs to the landing's tidy (row d); step 10 retries a kept tree and removes the landed branch by D102's content-held removal (row e) | §4.1, §5.2 step 10; rows d, e | taken |
| Fable | F36 | no | `D209.md:95-105` (owner-set) | taken: the requester's authority judged at form, step 8 keeping the race | §5.2 steps 2, 8; §8 | taken |
| Fable | F37 | no | design §4.7 | taken: the door named, the sweep or the card | §4.7 | taken |
| Fable | F38 | no | `Trees/Nodes/BuiltInNodes.cs:153-160` | taken: `line` reached from the unit's end, D102's content-held proof | §5.1 | taken |
| Fable | F39 | no | `D192.md:66-67` | taken: the runner's command aligned with D192 point 9, its flags its own | §4.1, row j | taken |

## 15. Round 2: both reads of round 1 answered

### 15.1 What changed, and where

| Change | Findings | Sections |
|---|---|---|
| Integration rides a branch rule; `integrate` refused on a merge rule and beside a plugin; today's merge rule the named legacy exception | C9, C18, F17 | §4.1, §4.2, §4.7 |
| No node in any graph; the run a transport observed by `line`; `work.landed` unredefined | C10, F4, F8, F21 | §1.3, §5.1 |
| One door for every request through the unit's landing gate; records branches by the owner's card; each revision requested again | C5, C11 | §4.6, §5.2, §5.7 |
| The strict policy permanent; `land` the owner's, only with a whole-set verification; inconclusive confirm runs; the backstop stated truly | C3, C6, C16, C17, F14, F15, F16, F19 | §4.1, §4.5, §13 |
| The whole admission as ordinary entries | C7 | §4.5 |
| D192's runner extended; the accepted blob handed to the runner; the commit check as its mode | C8, C25, F3, F5, F6, F7 | §4.1, §4.3, §5.9, §6.2 |
| Provisional containment | C4 | §5.2 |
| The steward's acts cut at promotion; `decide` without `land`; no request without gates | C12, F9 | §8 |
| Both integrators fenced at promotion; the flip prepared by the owner's one sitting | C13, F12, F18 | §5.6, §6.3 |
| A line settles only by its writer | F10, C21 | §4.1 |
| The landing agent deferred | C19, F25 | §5.4 |
| The integration tree under the trees home, leased, with the private files | C22, F20, F28 | §5.2 |
| Ownership by subdomain; sightings Review's | C15, F22 | §5.6, §6.1 |
| Survey corrections | C1, C2, C14, F1, F2, F27 | §1 |
| The procedures §3 had left unnamed | C26, F13 | §3, §7 |
| Spelling, routes, the paths field, the human step, the rows' starts and budget | F23, F24, F26, F29, F30 | §4.6, §5.8, §9, §11 |

Rows changed: a (the home read first), b (strict, ordinary entries, the tool's own switch, prospective measurement), c (no command before d,
provisional containment), d (the branch rule, an acceptance of its own, the refusals), e (its start and budget, the one door, the fence), g (no node,
the legacy exception, no landing agent), j (D192's contract extended, moved before b), k (fenced, after h). The order is a, c, d, j, b, e, f, g, h,
k, i, l, m.

### 15.2 Where the readers differ

- **`probe` and D192's `check`** (C25, Fable's Q9). Codex: share the protocol, `probe` optional for a check, which needs execution and verdicts,
  not bisection. Fable: one contract, and `probe` belongs in it, since a check's filing of known red by signature needs the same re-run. **Proposed:**
  `probe` is a mode of the one contract, required of a runner integration calls and answerable by any runner, and a `check` asks it only where its
  own row later files known red by signature; D192's check needs nothing new now. **Decided by the steward:** probe is a mode integration
  requires and any runner may answer, a check asking it only once known red by signature exists.
- **The acceptance's home** (C6, F11). Codex allows the existing landed-route acceptance reused or a new seam; Fable prefers an acceptance of its
  own. **Proposed:** an acceptance of its own (§4.1), since a repository should not need a verification pipeline to integrate, read by GENREGEN1's
  writers beside the landed route's. **Decided by the steward:** the acceptance is separate.
- **The flip's gap** (F12 against round 1, and C13's correction). Fable proposes the tool keep integrating until the install's rule is set, read
  through the CLI's twin of `driver.json`; **proposed instead** the owner's one sitting before the flip (§6.3), since the tool reading the
  install's `driver.json` needs the install's home, which row a removes from everything the tool starts. Codex's fence at promotion applies either
  way. **Decided by the steward:** the flip is the owner accepting the flip branch's declaration and setting the install's rule in one sitting
  before it promotes.

Round 2's two other open questions, which no reader disputed, were settled by the steward too:
- **The integration tree.** It counts against KEEPSIZE1's allowance and is never evicted while a run holds it (§5.2 step 2).
- **The legacy merge rule.** The legacy ungated merge rule rides the new path as a named exception until row g (§4.2's last row, §12 item 5).

Every other answer to round 1's questions agreed between the readers and is taken (§14: C17 to C24 with Fable's §4).

### 15.3 What a second read may be confined to

§4.1, §4.2, §4.5, §4.6, §4.7, §5.1, §5.2, §5.6, §5.9, §6.3, §8 and §11, with D215. Other changes are corrections and placements (§1, §3, §5.3,
§5.4, §5.5, §5.7, §5.8, §7, §9, §10). Both second reads were confined so, and each accepted after named changes (§16).

## 16. Round 3: the second reads' named changes, and the acceptance

Both makers read round 2 at `cd12f0399`, confined to §15.3's sections and D215, and both asked **accept after named changes**:
- **Codex** found C4 to C12 resolved, C13 resolved in part, and one new blocking finding, C27.
- **Claude Fable** found its F5 to F10 resolved, two new blocking findings (F31, F32) and seven non-blocking (F33 to F39).

Each was checked at its lines (§14, round 2's table) and every one is taken. They change these sections:

| Change | Findings | Sections |
|---|---|---|
| The steward's request never makes a unit's landing; it admits a records branch or a landing the owner's press or a done already made | F31 | §4.1, §4.6, §8; row i; D215 points 4 and 10 |
| The recorded-landing door: a standing landing record, its unit and exact tip, its gate proof the landing's own; from the owner's card, the steward's act or the come-up sweep; a tidied tree's landing admitted from its record; arbitrary branches refused | F32, C27, F37 | §4.6, §4.7, §6.3 step 4; rows e, k; D215 points 4 and 12 |
| The live runner's upgrade before the flip: the fence alone first, the code identity recorded, the old runner drained, a fenced runner shown refusing; the upgrade test from a live unfenced runner | C13 | §6.3 step 4.1; row k; D215 point 12 |
| `flake` read as a pass for FLAKE1's named classes alone, as today | F33 | §4.5 |
| The whole admission's ordering and the inconclusive judgement from the start | F34 | §4.5 |
| The declared scratch in the landing's tidy; the landed branch's content-held removal | F35 | §4.1, §5.2 step 10; rows d, e |
| The requester's authority judged at form | F36 | §5.2 steps 2, 8; §8 |
| `line`'s proof named | F38 | §5.1 |
| The runner's command D192 point 9's | F39 | §4.1; row j |
| The steward's decisions on round 2's questions | — | §5.2 step 2, §12, §14, §15.2 |

**Accepted by the steward, 2026-10-28, under D191.** Three makers read it:
- **Claude Opus 5.5** wrote it.
- **Codex** read rounds 1 and 2: another round, then accept after named changes.
- **Claude Fable** read rounds 1 and 2, as a foundation: another round, then accept after named changes.

Both second reads accepted after the named changes this round makes, each checked against its finding by the steward. Nothing is built.
FOUNDATION1 stays the building priority. The rows build in §11's order, the ⚑ rows beside FOUNDATION1 where no open branch holds their files.

## 17. Round 4: the flow is the workspace's

**Proposed, 2026-10-28, under D191.** Written by Claude Opus 5.5 on the branch `parent/integrate1-flow` from `4b9621c37`, round 3's tip, whose
`src/` and `tools/` are `7ebeba1ba`'s (`git diff --stat 7ebeba1ba 4b9621c37 -- src tools` prints nothing). Every `file:line` below was read
there. Nothing is built. The steward settles it after Codex's and Claude Fable's reads, which may be confined to this section and the sections
§17.12 lists. D215 carries round 4's dated note; every other decision is amended only by the note of the row that makes its change (§17.11).

### 17.1 The direction, and what it re-opens

**The decision proposed** (§0.1 point 3): how work moves is a flow each workspace configures from step kinds, not a fixed cycle (start,
working, landing) that integration then follows from outside. A flow is a workflow D157 chooses (its point 10) and D190 runs:
- **a line-integrated workspace** (a family of game repositories): start, work, merge into the line (integration, with its replay when the line
  moved and its gates), completed;
- **a pull-request workspace** (the work workspace): start, work, a pull-request branch, completed;
- **steps inside the work** may ask another repository or agent (a quest), shown and waited on as part of the flow;
- **later kinds** (a push, a release) extend the same flow.

**What it re-opens.** D215's *What re-opens it* names the owner's word. Round 3 kept each unit's graph fixed and made integration a transport
outside every graph: D215 point 5's *no node is added to any graph and no run's record changes*, §10's rejection of an `integrate` node (C10)
and §5.1's deferral of the kind. That fixed cycle is what the direction rejects. In D157 it re-opens point 1 (what of the work a flow declares),
point 2 (the order before the landing, which D190 already gave graphs), point 8 (the kinds) and §3.8 (another repository's work as a step).

**What it keeps.** D215's line run whole (§5): one run per line, its eleven steps, the gates through D192's runner extended, the confirm run under
the strict policy, the composed environment, the promotion fence, the acceptance `integrationGates`, the one request door (§4.6) and who acts
(§8). D190's two engines; D157's choice order and Current with no migration; D192's start node.

### 17.2 A unit's whole flow is its chosen workflow's

1. **The start chooses the flow**: D192 point 1's start node, by D157 point 10's order (the task's choice, the repository's for the kind and its
   default, the workspace's for the kind and its default, Current). One flow governs a unit, by its binding (D190 point 1).
2. **In a graph version the flow orders every step.** The graph rules hold only the gates' order (§1.3: `NodeGraphs.cs:241-288`,
   `WorkflowNamed.cs:296-309`), so any other node may sit before the landing or after it; a steps version keeps the runtime's fixed order
   (`WorkflowNamed.cs:623-688`). D157 point 2's *before the landing the order is the runtime's* becomes, for a graph, *the gates' order is the
   graph rules'; every other step's place is the flow's*. D190 began it (its point 6: a go-ahead before the landing holds it); round 4 states it.
3. **The kinds a flow is composed from**:

| The person sees | Node kind | Reach | Its subject (D190 point 2) | Reached on | Where it runs | Built |
|---|---|---|---|---|---|---|
| start | the start node | — | — | the route chosen at the bind (D192 point 2) | before the graph | FABRIC-PIPELINE1a |
| work | `claimed` | observed | the run's chain's quests here | its last step's done; under `requests: wait`, every request its sessions published here closed (§17.3) | both engines (`work`) | built; `requests` row o |
| a request to another repository | `request` | waited | the quest it published | that quest's done (§17.5) | graphs only | row p |
| the second opinion, your look | `opinion`, `look` | observed | the run's branch at its tip | D190 point 5 | both | built |
| a go-ahead, a person's task | `go-ahead`, `human` | pressed | the ask; the run | the person's yes | both; `human` graphs only | partial; FABRIC-HUMAN1 |
| the landing | `handed` | observed | the run's branch | the branch made at its recorded tip | both (`landing`) | built |
| merge into the line | `integrate` | waited | the landed branch at `handed`'s recorded tip | its member promoted and settled (§17.4) | graphs, and Current's branch graph | row n |
| a pull-request branch | `pushed`, `pull-request` | observed | the landed branch | the plugin's push and the remote-tracking ref; its completed answer (D148 point 4) | both (`pull-request`) | built |
| on the line | `line` | observed | the landed branch | the unit's end, D102's content-held proof (`BuiltInNodes.cs:153-160`) | graphs, Current | built |
| checks, stages, a group | `check`, a plugin's node, `all` | performed, observed | as D192 and D190 | as D192 | graphs only | FABRIC-PIPELINE1d, PERFORM1l |
| completed | the graph's `end`; `quest.close` where it holds one | — | — | the end reached (§E5) | graphs | `quest.close` FABRIC-CUTOVER1h |
| later: a push, a release | `push`, a production `stage` | pressed | the line; the stage | the person's press | graphs only | later designs (§17.10) |

4. **The new kinds are graph nodes only**, as `check` is (D190's rejection of *a steps engine for `check` and `stage`*; D192 point 6). A steps
   version naming `integrate` or `request` does not read (`WorkflowNamed.cs:397`, *is no kind of step*), so schema 1 and every run bound to it
   stay D157's to their end (D190 point 1). A steps run's work still integrates where its repository's rule carries `integrate`: its landed branch
   follows Current's landing graph after its landing (`docs/decisions/D190.md:131-133`), which round 4 draws with the node (§17.4).

### 17.3 Inside the work: what the flow declares, what is observed

D157 point 1 keeps the chain the intake's and the person's (D65), and round 4 keeps it: a flow never says which quests a chain holds, which
repository a session asks, or what an agent does. What changes is what a flow may hold the work on, what it shows, and that it may make a request
of its own.

**Observed from the session, never declared:**
- the chain's own steps (D65), each session's park, the go-aheads it asks (D135), its helper's asks (D163) and its partner (D199);
- **the requests its sessions make.** A quest a session of the work published is joined to the work already (`WorkJoin.Published`,
  `Sessions/Domain/AskWork.cs:12`; `QuestView.PublishedBy`, `Service/Domain/QuestViews.cs:198-202`), the one its taker waits on is read
  (`Awaits`, `:182-194`), and the work step says it as *waits on the answer to quest #n* (`Workflows/Domain/WorkflowRun.cs:701-702`). Row o draws
  each such quest as a request inside the work on the run's view and the terminal: its repository, its state (open, taken, done, declined),
  whether its session waits on it, and its outcome, the close's note (`QuestView.Note`, `:204-205`). A helper's ask is drawn beside it as a
  request to an agent.

**Declared by the flow:**
- **whether the work's own requests hold it**: `claimed`'s option `requests`, `wait` (the work is not reached while a quest its sessions published
  here is open; a decline holds it for the person, whose *Go on without it…* with words passes that request over, a wait the person answers,
  `person-waits.md`) or `go-on` (shown, holding nothing: today's). Absent is `go-on`, so nothing set is today (D157 point 15). It holds the work
  and never composes it;
- **a request the flow makes itself**: a `request` node at its place (§17.5);
- the gates, the go-aheads and the person's tasks, as today.

**Why.** The direction asks that a request be shown and waited on as part of the flow. The records already join a session's request to its work,
so drawing it adds no record, and waiting on it is a hold the person sets on the flow. A plan for the agent stays the intake's (D65, D1), and a
request the flow declares is the person's own words published at its place, whose work the receiving repository plans for itself (repository
owns its work).

### 17.4 `integrate`: the unit's step, the line's run

**The node.** `integrate` is a built-in kind whose reach is `waited` (`NodeReach.Waited`, `NodeContract.cs:17-18`): its start has an effect
outside its record, so a new attempt waits until the last is known (`NodeAuthority.Dispatches`, `:394`).
- **Its subject** is the landed branch at the tip `handed` recorded, as every node after `handed` binds the landing's tip from its record (D190's
  third round, point 2).
- **Its start calls the one request door** (`IntegrationRequests.Admit`, §4.6), keyed by the unit and that tip, so a restart finds the request
  it made and never makes two. The door's rules stand whole; the node's `request` option chooses among them:
  - `owner` and `done`: a landing made under this step, by the person's Accept or by the done under the landing's automatic accept, is its
    request, so the node starts once `handed` is reached;
  - a recorded landing made before (by an older build, under the old rule, before the step was chosen) waits for the owner's card, or, under
    `done`, the come-up sweep, which is now the runtime's pass (§4.6's recorded-landing door, unchanged);
  - `steward`: the node waits for the steward's `request` act, which is its start, or the owner's card (§8).
- **Its states** are read from the integration record, never copied (§5.6): ready (its request not made, saying who may make it; where that is
  the owner's card, a wait *What needs you* lists, `person-waits.md`, row h); active
  (queued; in a candidate, at the run's step; held for the owner or the steward; the line's integration stopped); reached (its member promoted
  and settled, or contained and settled with the member that holds it, §5.2 steps 3 and 9); failed (handed back, with its words and to whom);
  unknown (the record not read); stopped (the person's stop, which drops its request as `--drop` does).
- **Its authority**: reach and failure by the integration record, a new `NodeSource`, never a person's word and never a plugin's. It is not
  skippable, since a skip satisfies `after` (§E2) and would read as integrated where nothing was; the person's stop is its door, and work the
  person then merges by hand reaches its removal at *held* (§C3).
- **Each revision is requested again by the same rule** (C11), by the lifecycle the node already has: a new done or a new Accept advances the
  landing (LAND2c), `handed` binds the new tip, and `integrate`, bound to the old one, is invalidated and judged again (§E2's *any bound state →
  invalidated*), its next start the new revision's request. Under `owner` the new revision waits for the person's Accept, as a landing does today.
- **Its options** are the unit's half of the install's policy, `request` (`owner`, `done`, `steward`) and `conflicts` (`session`, `hold`); the
  line's half stays the rule's (§4.1, §17.7). `done` and `steward` need the line's accepted gates, judged where the version is chosen (row r).

**The line's run stays one per line** (D215 point 5), because a candidate batches several units, orders them and bisects across them, and the
promotion fence spans every integrator of the line. Its steps, gates, environment, confirm run, judge, fence, acceptance and who-acts table are
unchanged as its mechanics; the node is its client, as a landing is a client of `SessionTrees.LandAsync`. So §10's *a node per step on each unit*
stands: a unit has one `integrate` node, whose detail names the run's step.

**The graph rules** gain: at most one `integrate`; after a `handed`; that `handed` a branch with no plugin, as the version or its repository's
rule says (D215 §4.1); no `pushed` or `pull-request` beside it; never in a verification pipeline (`VerificationProblem`,
`WorkflowNamed.cs:317-328`, whose list gains it, its subject being a chain's landing). A `line` after it observes the promotion (§5.1).

**Current draws it.** Current's branch graph is the landing's own nodes, `handed` then `line`, `pushed` and `pull-request` between them where a
branch rule names a plugin (`NodeGraphHomes.Landing`, `NodeGraphs.cs:756-763`). Where the effective rule carries `integrate`, it is `handed`,
`integrate`, `line`. The rule's `cycle` comes after the landing's nodes as today (`OfLanding`, `:770-795`): it may wire from `integrate`, never
into it, and may not declare one, as it may not declare `pushed` (`landing-kind`, `:778-781`). A rule set nowhere draws as today.

**Arming.** Today the choices' graphs arm the runtime only where they name a plugin's node or a closure (`NodeRuntime.Armed`,
`NodeRuntime.cs:318-322`; `Acts`, `:1764`), a run bound to a graph version arms it by its binding (D190 point 3), and the runtime begins only
observed built-ins, leaving a waited node ready (`Begins`, `:1757`; `:851`). Row n arms the choices on a Current branch graph holding
`integrate`, and begins a waited node, Current's or a version's, through its kind's adapter: a start by key, a reconcile that reads the other record
before any new attempt, a wake on that record's events (`integration.*`, §5.2), never a timer. That is the waited reach's start, which `request`
(row p) and `wait.unit` (FABRIC-WAITUNIT1) take next.

### 17.5 `request`: another repository asked, as a step

A `request` node publishes a quest to a named repository at its place in the flow and waits on it. It is the flow's form of the fabric's waited
dependency on other work (§E6: another repository's work only through a quest commitment, read from this machine's projection; `NodeWires.cs:299`
refuses a wire into another repository's node and names a local `wait.unit` as the way), and of the delegated node FABRIC-DELEGATE1 names.
- **Its options**: `to` (a repository, with lanes as a quest's address takes them); `title` and `body` (the person's words, kept as written,
  D142); `start` (`you`, the default, the person's *Send*; or `automatic`, the person's standing say-so, said as it is set, as a stage's start
  is, D157 §3.6); `until` (`done`, the default, or `taken`); `declined` (`wait`, the default, for the person; or `stop`).
- **Its start** publishes the quest through the exchange as Daoris's own publish, carrying its requester (the run and the node) as a field of
  its own, by which it is found first among the quests published, so a restart never publishes twice (D192 point 8's rule for a fix ask). The
  quest says what is needed and why, never a change; its taking and its work are the receiving repository's.
- **Its reach** is the quest's done, or its taking under `until: taken`, as the exchange's operations carry it here (D68); **its failure** the
  quest's decline; a quest not read is unknown, never passed (D148 point 5). Its outcome shown is the close's note and its coded parts.
- **No chain step.** It is no quest of the run's chain (D65), and the receiving repository's work follows its own flow, one run per repository
  (D157 §3.8, which stands in that). What changes in §3.8 is that a flow may wait on another repository's work through the quest it published,
  which part E's §E6 already made the fabric's way.
- **A request to another agent** is a session's helper ask, observed (§17.3), or the flow's `opinion`. Round 4 declares no other agent step.

### 17.6 What D190's two engines and versions need for a node that waits on another record

1. **No third engine.** The line's run is no workflow run (D215 point 5). The node runtime governs `integrate` and `request`, and a schema-1 run
   holds neither (§17.2 point 4). One governs a run, by its binding (D190 point 1).
2. **A subject per kind**: D190 point 2's table gains `integrate`, the landed branch at `handed`'s recorded tip, and `request`, the quest it
   published, at its revision (§E6: a newer revision is never followed silently).
3. **An authority per kind**: §E2's table gains `integrate`, reached and failed by the integration record, and `request`, by the commitment
   (its done; its decline), as `wait.unit`'s row has it (`NodeContract.cs:443`).
4. **The waited start** (§17.4, *Arming*): an attempt keyed so a restart finds it (the request by unit and tip; the quest by its requester), a
   reconcile before any new attempt (§E2's *unknown is reconciled before any new attempt*), a wake on the other record's events.
5. **One writer per record** (D190 point 3): the runtime writes the node's state on the run's record, or a Current branch unit's; the line's
   run writes the integration record; the exchange writes the quest. The node touches the other record only through its door: the request's
   admission, the publish, and the stop's drop.
6. **Versions** (D157 point 6): the kinds enter only graph versions saved after them, so no bound version, its digest or its run changes. A
   build without a kind reads a version naming it as unreadable (`NodeRefusal.UnknownKind`, `NodeContract.cs:483`), and a run bound to it is
   held whole and said (D190's CUTOVER1c note). So a build in the rollback window (D139) never integrates or publishes for such a run, as it
   never merged a rule carrying `integrate` (C9).
7. **Current's units and older builds** (`compatible-builds.md`). An older build reads a rule carrying `integrate` as the branch rule it is and
   draws `handed` then `line`. Unless the rule's `cycle` names a plugin's node, that graph does not arm the older build (`Armed`), which then
   writes no record of the unit, its keeper observing `line` by git as today. Where it does arm it, the older build governs the unit by its own
   graph and may write the unit's record without `integrate`'s state; on the newer build's return the node is judged again, and its start finds
   the open request by its key, so nothing is requested twice.

### 17.7 Presets, Current, and where the install's policy lives

**Two presets, built in, each a graph**, beside the four steps presets (`WorkflowPresets.cs:26-35`): offered by `daoris driver workflow new
--from <preset>` and the Workflow tab, saved as a graph version, each with the opinion and look variants (`WithGates`, `:44-56`, extended to
wire each gate into the landing as `protected-wire` asks).
- ***Integrated into the line***: `claimed` (`requests: wait`); `handed` (`form: branch`, `accept: you`, its pattern and plugin left to the rule);
  `integrate` (`request: owner`, `conflicts: session`); `line`; its end `line`. Its variant *with no press*: `accept: automatic` and `request:
  done`, refused where it is chosen for a repository whose line declares no accepted gates (D215 §4.1).
- ***A pull request***: `claimed` (`requests: wait`); `handed` (`form: branch`, `accept: you`, the rule's plugin); `pushed`; `pull-request`;
  its end `pull-request`, the platform's completion (D148 point 4).

```json
{ "id": "integrated", "name": "Integrated into the line", "versions": [ { "version": 1, "schema": 2, "graph": {
  "schemaVersion": 2, "entries": ["work"], "end": "line",
  "nodes": [ { "id": "work", "kind": "claimed", "version": 1, "reach": "observed", "options": { "requests": "wait" } },
             { "id": "landing", "kind": "handed", "version": 1, "reach": "observed", "options": { "form": "branch", "accept": "you" } },
             { "id": "integrate", "kind": "integrate", "version": 1, "reach": "waited",
               "options": { "request": "owner", "conflicts": "session" } },
             { "id": "line", "kind": "line", "version": 1, "reach": "observed" } ],
  "wires": [ { "id": "a", "kind": "after", "from": "work", "to": "landing" },
             { "id": "b", "kind": "after", "from": "landing", "to": "integrate" },
             { "id": "c", "kind": "after", "from": "integrate", "to": "line" } ] } } ] }
```

**Current, derived, never migrated** (D157 point 7). Today's rules draw as they do: a merge rule (D87) as the named legacy exception (§4.2's last
row), a branch rule as a branch, a branch rule naming a plugin as a pull request through it (D145, D164), and a branch rule carrying `integrate`
as §17.4's graph. Nothing is rewritten, and nothing set anywhere is today (D157 point 15).

**Where the install's `integrate` policy lives** (D215 point 2), split by D157 point 4's rule, *a workflow holds the process; the repository keeps
its declarations* (§4.1's *Two halves of the key*):
- **the unit's process**, `request` and `conflicts`: under Current the rule's `integrate` holds them, as round 3 accepted; under a named
  workflow its `integrate` step's options do, as a landing's `form` and `autoAccept` are its step's;
- **the line's declaration**, `commit`, `batch`, `order`, `gates`, `unconfirmed`, `overlap` and `line`: the branch rule's `integrate` always,
  since one run per line batches, orders and gates units whichever flow each follows. A flow never sets them, and a rule without the key reads
  §4.1's *Absent* column.

**How the choices compose** (D157 point 10; D164's whole-rule inheritance; D215 point 2):

| The workspace chooses | The repository | Its landing rule: its own whole, else its workspace's | What runs |
|---|---|---|---|
| *Integrated into the line* | chooses nothing | a branch rule, with or without `integrate` | the preset's graph; its step's `request` and `conflicts`; the rule's line declaration, or its *Absent* column |
| *Integrated into the line* | chooses `current` | its rule | Current: integrated only where the rule carries `integrate` |
| *Integrated into the line* | chooses *A pull request* | a branch rule naming a plugin | the pull request's graph; the rule's `integrate`, if any, refused beside its plugin as §4.1 says |
| *Integrated into the line* | chooses nothing | a branch rule naming a plugin | *cannot start*, naming the rule's plugin: that repository's platform integrates its work (§4.1), so it chooses *A pull request* or its rule drops the plugin |
| *Integrated into the line* | chooses nothing | a merge rule (no pattern declared) | *cannot start*, naming the missing pattern (D157 §2.3: a step chooses among what is declared), never a merge in integration's place |
| *A pull request* (the work workspace) | chooses nothing | a branch rule naming its platform's plugin | the pull request's graph |
| *A pull request* | chooses nothing | a branch rule naming no plugin, or a merge rule | *cannot start*, naming the missing plugin or pattern |
| nothing | nothing | any | Current, as today |

A repository's choice replaces its workspace's whole (D157 point 10) and its rule replaces its workspace's whole, `integrate` with it (D164), so
neither is merged with its workspace's. A workspace's flow never integrates past a repository's platform, nor pushes where no plugin is declared:
each mismatch is held where it would start and said with the Setup row that settles it (row r). A person's own version may still say
`plugin: none` on its `handed`, a choice among what is declared (D157 §2.3), and then integrates.

### 17.8 What D215 reverses, and what stands

**Reversed**, each with why:
- **§10's *an `integrate` node in any graph by this design*** (C10). The direction makes integration a step of each unit's flow. C10's three
  reasons are kept as constraints on the kind rather than as a reason to exclude it: a schema-1 run is D157's to its end, so the kind is a graph
  node only (§17.2 point 4); a bound graph is never mutated, so the kind enters only versions saved after it (§17.6 point 6); Current cannot
  govern a named run, so Current's branch graph gains the node only for the branches Current governs (D190 point 2).
- **§5.1's deferral** of *an `integrate` node kind, waited on that record*, and §11's follow-up naming it: taken, as rows n and r.
- **Point 5's *no node is added to any graph and no run's record changes*** and §5.1's *never written onto a run's record*: the unit's graph
  holds `integrate`, and its record holds that node's state, written by the runtime alone. The integration's own state stays the integration
  record's, read and never copied (§17.6 point 5). Point 5's *`work.landed` unredefined* and *`line` observes the promotion* stand.
- **§4.7's *no run's record and no graph version gains anything***: a version gains the kind only when a person saves one, and a Current branch
  unit the node only under a rule carrying `integrate`, which nothing sets before row d.
- **Point 2, in part**: under a named workflow, `request` and `conflicts` are its step's (§17.7).

**Stands**, each of §10's other rejections, read again against round 4:
- *a pipeline marked `integration`*: the line's run is still built in, one per line, its steps fixed by safety; a flow requests it and never
  versions it;
- *a node per step on each unit*: one `integrate` node per unit, the run's step its detail;
- *`integrate` on a merge rule*, *`autoAccept` on a merge rule as the switch*, *`request: done` or `steward` with no gates* and *the steward's
  `decide land`*: each holds at the step as at the rule;
- the rest as written: the tool as integrator, the gate plan, a runner contract of its own, the runner reading the tree, promoting by a merge in
  the checkout, a weight field, `land` as a default, landing every unconfirmed failure, re-running until it passes, confirming by the class, a
  wider window, a line rule keeping one side's copy, containment settled at once, resolving a conflict of meaning, a landing agent, the whole
  declaration in one home, requiring a verification pipeline, two integrators, reverting, the rehearsals in the product, same-file rows by a
  model's reading.

D215's points 1, 3, 4 and 6 to 12 stand as written; point 4's door and point 10's acts gain only their caller, the node (§17.4).

### 17.9 The rows

**The rows beside FOUNDATION1:**
- **a** (the queue's gates start clean): unchanged; tools only, and round 4 touches nothing of it.
- **c** (mechanical resolution, being built now by a driven session): **unchanged and valid**. Its contained members, row tables and union joins
  are the line run's steps 3 and 4 (§5.2) and the landing's replay (D87), neither of which round 4 moves; the node only requests the run.
- **d** (the declaration read and accepted): unchanged in shape; round 4 reads the key's `request` and `conflicts` as Current's source of the
  unit's half, which a named workflow's step replaces, and the rest as the line's (§4.1).
- **j** (the runner contract): unchanged.

**b, e and f** are unchanged; e's door gains only the key a start finds its request by, which its *no double* rule already names (§4.6).

**Changed:** g (its requests made by the node, after n), h (the run's view draws the node), i (its act starts the node, after n), k (Current at
the owner's sitting).

**New**, each in §11 with its contents and proof:
- **the step kinds**: n (the `integrate` node kind, with the waited start) and p (the `request` node);
- **the presets**: q;
- **the D157 change**: o (points 1 and 12: the work's own requests drawn, and held where the flow says) and r (points 7 and 10, §2.3: Current
  and the choice know the step).

**The order:** a, c, d, j, b, beside FOUNDATION1 as before; o and r, beside it while no mod-xviii branch is open; e, f; n; g; h; q; k; i; l; p;
m. The ⚑ marks are unchanged.

**Where their code would live**, by FOUNDATION1's map (D212 §14.2's table; §14.3: a subdomain names only those listed before it):
- the kinds, their authority rows, the graph rules, the waited start and its adapters, the run's drawing, the presets and Current's derivation:
  **Workflows** (row 18), `Trees/Nodes/` moving there at mod-xviii;
- the request door, the integration record and the line's run: **Workplace** (row 10), as §6.1 places them, the executor off the pass in
  Workflows composed by Loop;
- the quest a `request` publishes and the requests a session made: **Service** (row 4, `ServiceClient` and the quest views) and **Work orders**
  (row 15, `AskWork`);
- the rule's key: **Settings** (row 3); the node's codes: **Events** (row 7).

Every edge from Workflows names a subdomain listed before it, so round 4 adds no census exception.

### 17.10 Later kinds

- **`push`**: the line, or a landed branch the person keeps, pushed to its remote at the person's press, never by a standing say-so, since a
  push stays the owner's (D191 point 3, D37). A landing branch's push by its plugin stays `pushed`, under D145 point 5's say-so, as today. It
  waits for D198's git identities (GITAUTH1) and a design of its own.
- **`release`**: a `stage` (D157 §3.6) whose environment reads as production, so only ever the person's press, never run again by itself on an
  unknown outcome, nothing rolled back (D157 point 9).

Neither is a row here; each is a later design, as D215's follow-ups are.

### 17.11 Amendments, each by the note of the row that makes it, never before

| Decision | What it would amend | The row whose note writes it |
|---|---|---|
| D157 | point 1 (the work's own requests drawn and held, a declared `request`; the chain stays the intake's); point 2 (in a graph, every step's place is the flow's, the gates' order the graph rules'); point 7 (Current gains `integrate` from the rule, still no migration); point 8 and §3.2 (`integrate` and `request` as graph nodes only); point 12 (the run's view draws the work's requests and the node); §2.7 (two graph presets); §3.8 (another repository's work waited on through a quest); point 10's lowering table (`request` moved from `owner`) | o, r, n, q, p |
| D190 | point 2's subject table and point 3's arming (two kinds, the waited start); point 4 (a named version's graph holds at most one `integrate`, after a branch `handed` with no plugin) | n, p |
| D173 (the fabric events design) | §E2's authority table, §E4's built-ins and §E6 (`request` as the delegated node's first form, on the waited start); §C2 (Current's branch graph with `integrate`, a `cycle` wiring from it) | n, p |
| D215 | point 2 (the unit's half under a named workflow), point 5 and §5.1 (the unit's graph holds `integrate`), §10 (C10 reversed) | its round-4 note now (status *proposed*); the settlement's note; n |
| D209 point 3 | as round 3 named it: the steward's acts, now starting the node | i |

D87, D145, D192, D207, D115 and D180 keep round 3's amendments as written (D215's *This would amend*); round 4 adds none to them.

### 17.12 Changed in place

Each section round 4 edits, marked *(R4)* there, with what it replaces:

| Section | Change | It replaces |
|---|---|---|
| Title, standing | round 4 proposed; round 3's standing kept, headed *as it was written* | *round 3, accepted* |
| In one paragraph | each unit's flow requests the line's run from its `integrate` step | *The run is a transport outside every run's graph: the unit's `handed` is the branch its landing made, and its `line` observes the promotion as it would a person's merge.* |
| §0.1 | point 3, round 4's direction; *Three directions* | *Two directions* |
| §0.2 | round 4's reads | nothing |
| §1.3 | four facts round 4 rests on | nothing |
| §4.1 | the install's row names the step's half; *Two halves of the key* | nothing removed |
| §4.2 | the shapes under a named workflow | nothing |
| §4.6 | the node as the door's caller | nothing |
| §4.7 | versions naming the kind; Current's units | *No run's record and no graph version gains anything, so a run bound before any row reads as it did* |
| §5.1 | the heading and its list: the line's run outside every run, the unit's step inside its flow | *No node is added to any graph by this design*; *The unit's graph is unchanged. It is still its landing rule's own graph, `handed` then `line`*; *Every run is untouched*; *never written onto a run's record*; *An `integrate` node kind … may come later … It is no row of this design* |
| §5.2 step 1 | called by the node | nothing removed |
| §5.8 | the run's view | nothing |
| §8 | the steward's `request` starts the node | nothing removed |
| §10 | C10's bullet marked reversed; two marked standing | nothing removed |
| §11 | the intro; c and d noted; g, h, i, k amended (g's replaced words struck through); n, o, p, q, r added; the order; the follow-ups | *the order is a, c, d, j, b, e, f, g, h, k, i, l, m*; g's *a branch rule carrying `integrate` requesting through its landing* and *no graph, no run's record*; the follow-up *an `integrate` node kind (§5.1)* |
| §13 | a pointer to §17.14 | nothing |

### 17.13 Open questions for the steward

1. **`commit`**: the line's (proposed, one shape of history per line) or each unit's.
2. **The pull-request preset's end**: the platform's completion, `pull-request` (proposed), or the pull request opened, `pushed`.
3. **`requests: wait`** in both presets (proposed), and never Current's default (proposed: nothing set is today).
4. **What a `request` publishes**: a quest (proposed, as D79's question is and §E6's commitment names) or an ask to the receiving repository's
   intake (as D192 point 8's fix ask is).
5. **The fabric's rows**: FABRIC-WAITUNIT1 and FABRIC-DELEGATE1 re-pointed onto row n's waited start and the exchange as it stands (proposed),
   or `request` waiting for the collaboration checkpoint they sit behind (FABRIC-ACP1, C2a, C2b, COLLAB1).
6. **This repository at the flip** (row k): Current with its rule carrying `integrate` (proposed, as round 3 accepted), or the preset.
7. **`integrate`'s skip**: refused, the stop its door (proposed), or a skip with words meaning *integrated by hand*.
8. **The rows round 4 leaves alone** (a, c, d, j, b, e, f): built on while round 4 is read (proposed), since it changes none of them.

### 17.14 What round 4's checks do not cover

Documents only; nothing is built or run but reads, the index's generator and the document checks the brief names, which prove links, routes
and shapes, not claims. Each `file:line` above was read at `4b9621c37`, and none of the behaviour proposed was tried. Not measured: the arming's
cost once every repository whose rule carries `integrate` arms the runtime for its branch units (row n); how often a work's own requests are
open at its done, which `requests: wait` would hold (row o); a waited start's wake latency on the integration record's events (row n). The two
presets' words were written here and read by no person; their Chinese is row q's. The direction's picture of the two workspaces is the brief's,
and the install's workspaces and their rules were not read.
