# The person's review for every kind of work, not only a branch to land (REVIEWUX1)

**Status: accepted (D273, 2026-11-11), round 2.** Written by Claude Opus 5.5 from main at `e1ad4167c1`; revised from main at
`9c100b32e2` after Codex's read (gpt-6.1-sol, D191), whose findings were each checked against the code and taken, bar the three §13
names. Read with `docs/areas/workflows-review-landing.md` (cited *workflows rule n*), `docs/areas/desktop-app.md` (*desktop rule n*),
`docs/areas/driving.md` (*driving rule n*), `docs/areas/roles-and-supervision.md` (*roles rule n*), the place-work design
(`docs/2026-11-10-place-work-design.md`, *place §n*) and PROCSTEPS1 (`docs/2026-11-09-process-steps-design.md`, *steps §n*). Paths are
under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Modules/` (`src/Daoris.Desktop/Daoris.Desktop.Modules/`) or
`Web/` (`src/Daoris.Web/src/`). Nothing is built; the installed window's sitting is row j's.

*Note, 2026-11-11 (the read).* The read kept the central direction (review the run's work, bind the Accept to a revision, watch what
follows as outcomes) and found that the proposal understated the runtime's gaps and that its revision and migration rules did not yet
make what was read the thing carried out. What changed: the subject is one immutable review record per run attempt, carrying the
artifact's reference, the handbacks and what is complete, and the Accept is a receipt bound to it (§2); a branchless review is withheld
`no-subject` today, not reached with no revision, and the run's unit has no executors, so `apply` and `return` find nothing outside their
door tests (§1.3); the special graph end (`ICarriesOut`) gives way to completion as nothing left to move, with every effect ordered after
its review and fed its accepted artifact by reference, and D264 amended (§3); partial effects are kept and said (§3.4); the revision is
checked when the word is kept, when it is applied and when the effect begins (§6.2); send back names the part, Stop says what it can
still prevent (§6); failure is the owner's *failed* state with the recovery doors that exist, not a person wait (§4, §7); the review
opens by run, its previews and *viewed* marks pinned to the revision (§5); the supervisor reads the same record (§8); outcomes in
Notifications are durable by run, node and attempt (§9); and the migration invents no revision-bound consent and changes no pinned
workflow (§11). The owner's five session states, set the same day, are §4.

## 0. In short

- **The subject is the run's review record.** Each attempt of a review reads the run's work into one immutable record: its kind, the
  artifact's reference (a branch's tip, a checkout's turns, a folder's baseline and newest manifest, a copy against its base, a plan's
  record and number), the revision, every part's handback, and what could not be read. The person's Accept is a receipt bound to that
  record; the steps after the review are fed it by reference (§2).
- **A run is complete when nothing in it can move.** Any graph may end where no effect lies past its end; a `review`, where one exists,
  comes before every effect (`land`, `return`, `apply`) and feeds it the accepted artifact; a graph without one lets its effects take
  the work as it stands, the standing authority judged first, as a landing does today. Partial effects are kept and said (§3).
- **Five states.** A session is *pending*, *working*, *needs you*, *done* or *failed*, and so is its run; the list, the head and
  Notifications say those, the finer reason inside. There is no landing task as a person's state: a landing is *working*, a review
  waiting is *needs you* (§4).
- **One review surface, opened by run.** The Review tab is the run's review, reached from any of its sessions, its run view, its
  Notifications and after its trees are gone; what is decided and the presses stay in reach over a long diff; previews and *viewed*
  marks are pinned to the revision; a newer revision is announced, never swapped in under the reader (§5).
- **Accept, send back and stop mean one thing each, said per kind.** One press, `WORKFLOW_NODE accept`, with the revision and attempt
  drawn, checked when kept, when applied and when the effect begins; *Send back…* names the part it goes to; *Stop…* ends what has not
  begun and says what already happened (§6).
- **The steps after the Accept are watched, not decided.** The person is asked only by a step that waits on them: a task, an approval, a
  go-ahead, a held attempt, a return's per-file choice. A failure nothing goes on from is *failed*, with the recovery its door has (§7).
- **The supervisor decides as today, on every kind,** reading the same record and binding its decision to it (§8).
- **Notifications:** *What needs you* holds one row per run waiting on the person, every wait on it; *Done and failed* holds each
  outcome once, by run, node and attempt, kept across a restart (§9).
- **Rows a to j** (§14).

## 1. What is asked, and what is true today

### 1.1 The owner's words

The *Your review* step (你的审阅) "doesn't fit the current process; we might need to redesign the entire approval UI/UX design and
logic, since this is not always creating a branch for commit." The session states are "a mess, and there is no landing task any more":
a session is *pending* (not started: queued, held, or paused, the reason inside), *working* (every state until it is complete that needs
no one, waiting on another session, a helper, a plugin step or a pull request included), *needs you* (any state a human must act on: a
review, a question, a choice, a card, a sign-in), *done* (a person's stop or an abandon is *done*, its reason *stopped*) or *failed* (a
failure nothing goes on from). The owner's standing rules apply to every choice below: visibility is the key; a security improvement
never blocks development; a person who wants more control uses restricted mode or a workflow node.

### 1.2 The kinds of work, and what each asks the person

| Work | What exists to look at | What the person decides | What the Accept lets happen |
|---|---|---|---|
| A session's branch to land (git, worked apart) | commits on the session branch past the line; uncommitted rest | that it may land | the landing, then what follows it (push, pull request, watch, tidy; or integration) |
| Work in place in a git checkout | commits made in the checkout during the run's turns; the run's own uncommitted paths (the lease, D258) | that the work is right; it is already in place | what follows the review, else the run's end; the lease is let go |
| A folder worked in place | the files changed from the run's first turn to its newest | that the changes are right; they are already in the folder | as a checkout |
| A folder worked in a copy | the copy's changed files against its base | that they may come back | `return`, with a per-file choice where both sides changed (D265) |
| A system | the plan: each resource before and after | that the plan may be applied | `apply`; a stale plan goes back to the work, which plans again |
| A measurement or a review that writes only a document | the handbacks, and the document where one was written | that the answer is good enough | as its place's kind, with nothing to carry |
| A chain of steps | the chain's whole change set, part by part | one Accept for the chain in this repository | as its kind |
| A pull request merged and a release, by a plugin | outcomes | nothing: they are watched | the run's end |

Three conditions cross every kind: a done whose quest carried no requirement waits *unchecked* before the review (D255); a supervisor
may decide in the person's place (D233, roles rule 49); an Accept may be pressed before the review begins (D256).

### 1.3 Where the review assumes a branch today

1. **A branchless review is withheld.** `ReviewNode.Subject` is `RunSubject.Branch` (`Workflows/Kinds/Review/ReviewNode.cs:20`), the
   newest session's branch the run's quests produced (`Workflows/Nodes/NodeGoverning.cs:257-262`), and the evaluator names the review's
   subject `scope.Branch?.Id` (`Workflows/Nodes/NodeEvaluations.cs:90`). A run in a folder, a system or a checkout has no session branch,
   so the subject is a present entry whose value is null, which `WireEvaluation.SubjectOf` reads as `no-subject`
   (`Workflows/Nodes/WireEvaluation.cs:313`): the review never becomes active, and an Accept kept early for it (`EarlyAccepts`) waits for
   good. This is Codex's finding on PLACEWORK1e, more severe than first written.
2. **The review is read as a landing.** It observes through `LandReading` (`ReviewNode.cs:36`), which reaches it wherever the session
   branch is held (`Workflows/Kinds/Land/LandReading.cs:34,45`), and a session that committed nothing has its branch held `empty`
   (`Workflows/Nodes/NothingToDeliver.cs`). So a measurement, or any work answered in its handback alone, passes the person's review with
   nobody reading it: APPROVE1's evidence found three such runs on the install.
3. **No window Accept outside a branch, and no terminal one either.** The review tab offers *Accept* only where `hasTree` and the plan
   read (`Web/work/review/DiffPane.tsx:152-176`); the head's *Accept…* and the waits' `accept` stand on the tree's offer
   (`Web/work/waits/waits.ts:194-209`); the press is `LAND_SESSION_TREE`, which needs a tree (`Modules/Bridge/Routes/TreesLandDesk.cs`).
   The run view offers a review only *Ask for changes…* (`ReviewNode.Acts`, `Web/workflow/NodeWord.tsx:16`). The terminal's
   `daoris workflow accept <run> <node>` reaches only a review that is active, and point 1 keeps a branchless one from being active.
4. **Two ways to send work back.** The review tab's *Send back…* opens the quest composer (`DiffPane`'s `onSendBack`); the run view's
   *Ask for changes…* (要求修改…) keeps the review's `changes` word, told to the newest session of the run's current quest, else of its
   branch (`Workflows/Nodes/NodeRunKeeping.cs:196`), which need not be the part the person means. D228 calls the second way a defect.
5. **A folder worked in place shows its newest turn.** `PlaceChanges.Of` compares one record's start and end manifests
   (`Departments/Review/PlaceChanges.cs:19-28`), and a record's start is one file, written again at each of its turns
   (`Departments/Manifest/FolderTurns.cs:22`). A folder worked in a copy is read from the copy's base instead (`PlaceChanges.cs:27`), so
   it is whole. After a send-back in place, the review shows the fix and not the work it fixes.
6. **A checkout keeps no turn history.** `SESSION_DIFF` reads base to `HEAD` where the record's tree is the checkout
   (`Modules/Bridge/Routes/TreesRoutes.cs:52-69`), so a commit the person made between the run's turns is drawn as the session's, and the
   run's own uncommitted paths (D258) are not drawn (`Workplace/SessionReviews.cs:137-138`). The turn's manifests keep `HEAD` at its start
   and end and its own paths (`Departments/Manifest/RunManifest.cs`), but one pair per record, overwritten at the record's next turn
   (`Departments/Manifest/PlaceTurns.cs:42,72`), and the store keeps the newest 20 records and the named ones
   (`Departments/Manifest/ManifestStore.cs:54-63`): the intervals of a record's earlier turns are gone.
7. **A system's plan is drawn as a folder's files.** `SystemChanges.Of` makes each operation a file row (`Departments/Systems/SystemChanges.cs`)
   answered as `source: place`, so the review says *This department is a folder* above a plan (`Web/work/review/PlaceNote.tsx`).
8. **The apply takes the newest plan, and the run's unit has none.** `ApplyDoor` searches the unit's executors for the newest plan
   (`Workflows/Kinds/Apply/ApplyDoor.cs:43`), so a plan written after the Accept would be applied unread; and the unit the runtime hands
   every door, `run:<run>`, is built with no executors (`NodeGoverning.cs:276`, `WorkOrders/Inventory/WorkUnit.cs:143`), so the apply finds
   no plan, logs *no change set* and returns without settling, its node active for good (`ApplyDoor.cs:44-48`), and `ReturnDoor` finds no
   copy and settles *its session worked in the folder itself: nothing to bring back* (`Workflows/Kinds/Return/ReturnDoor.cs:38-43`), a
   success that brought nothing back. Their tests hand executors in (`ApplyDoorTests.cs:97`, `ReturnDoorTests.cs:34`).
9. **The intended graphs cannot be saved; the steps are not ordered.** A graph with no `land` must end at its `review`
   (`Workflows/Domain/WorkflowNamed.cs:185`, `Workflows/WorkflowProcesses.cs:120`), so *review → apply* and *review → return* are
   refused; but nothing refuses an `apply` or a `return` placed before the review: `apply` declares no order and `return` checks only that
   an `isolate` exists (`ReturnNode.Problems`). `GraphStanding` judges a landless graph only at its ending review
   (`Workflows/Nodes/GraphStanding.cs:20-22`). The shipped *Work in place* (`Defaults/workflows/in-place.json`) holds no `apply`, so a
   system that follows it has its plan accepted and never applied.
10. **Only some person steps reach Sessions and *What needs you*.** `PersonWaitReader` lists an active `review`, `human` and `go-ahead`
    and an attempt held unknown (`Workflows/Nodes/PersonWaitReader.cs:36-46`). A `return` waiting on the person's per-file choice, and a
    failure kept for the person's *Try again*, wait on the person and are listed nowhere.
11. **The words are git's.** *{{count}} commits beyond the line wait for you to accept them* (`workflow.run.said.work.land`), *Nothing
    to land: its work made no commits*, *This session committed nothing to its tree, so there is no diff to read*, the send-back's
    *Make them and commit: what you commit is reviewed again* (`NodeRunKeeping.cs:185`); the glossary's *accept* is *a session's work in
    its review, which lands it*.

### 1.4 What the read and its check found besides

- **A run ends silently at a failure.** A run is over when its end node is settled or no node can move (`NodeRunKeeping.Over`,
  `NodeRunKeeping.cs:82-96`), so a step that fails with no wire on from it ends the run; and the end keeps only its quest and time
  (`RunEnd(Quest, At)`, `Workflows/Nodes/NodeRecord.cs:191`), so nothing after says it failed. A failure kept for the person keeps the
  run open, but `PersonWaitReader` reads no failed node (point 10), and the run view's table offers a failed node no act
  (`Workflows/Nodes/RunNodeWords.cs:262-269`); its one recovery is the attempt's `again` (`Workflows/Nodes/WorkPersonWord.cs:21`).
- **The end shortcut can cut a step off.** `Over`'s first half ends a run once its end is settled with every attempt settled, whatever
  is still ready: the shipped *Release* ends at `line`, with its plugin's `find`, `release` and `publish` after it
  (`Defaults/workflows/release.json`), and runs them only where they start in the pass that settles `line`.
- **A send-back waits on a commit.** A review's `changes` keep the run open until the work's next commit moves the branch (`Over`'s
  `changes`), so a corrected answer that changes no file never comes back to review.
- **The press around the Accept does more than keep it.** A conversation's press reserves the conversation, captures one tip and lands it
  pinned (`Modules/Bridge/Routes/TreesLandDesk.cs:75-84`); a carried Accept survives the carry-on's repair, *accepted; lands when its tree
  is clean* (`Workplace/CarriedAccepts.cs`); words are applied before the pass evaluates subjects afresh (`Workflows/Nodes/NodeRuntime.cs:378`).
- **The window's review is a session's.** The session each waiting run stands for is its newest record, and only while the run waits
  (`WorkOrders/Sessions/RunWaitSessions.cs`); *viewed* marks are keyed by path and reset by another session (`DiffPane.tsx:204-207`);
  an ended session's diff is fresh for good once read (`Web/bridge/sessions/trees.ts:242`); a preview reads the file in the tree as it
  stands and draws its source lines (`Web/work/review/FilePreview.tsx`), though the window renders Markdown elsewhere
  (`Web/work/conversation/Markdown.tsx`); *Done and failed* opens only a notice that names a session (`Web/notifications/DoneAndFailed.tsx:95`).

## 2. The review's subject: the run's review record

### 2.1 The options

- **A. A branch for every kind.** Give a folder's, a checkout's and a system's run a synthetic branch unit, so `RunSubject.Branch`
  holds. It keeps the code's shape, and lies about git: every reader of a branch unit (the trees, the keeper, the tidy, the landing
  record) would meet a branch that is not one.
- **B. A subject on the review alone.** The review binds a per-kind subject; what follows it reads what it reads today, the newest plan
  and the copy as it stands.
- **C. A change set on `claimed`'s wire.** `claimed`'s `work` output carries a change set (kind, unit, revision), the review binds it and
  the step after it is fed the accepted revision (round 1's choice).
- **D. One immutable review record per run attempt.** At each attempt the review reads the run's work into a record: its kind, the
  artifact's reference, the revision, the handbacks, what is complete, the parts. The record is the review's subject; the Accept is a
  receipt naming it; the window, the terminal and the supervisor render it; each effect consumes its artifact reference.

**Recommended: D.** It is C's one shape (what was shown, accepted and carried out are one thing) with the gaps C left closed: a branch's
tip names neither its uncommitted files nor its handback, a corrected answer at the same digest is no new revision, and a revision
recomputed at each reader can differ from the one drawn. A record written once is what everyone reads. B fixes the review and leaves its
consequence wrong; A is a second meaning of a branch.

### 2.2 The record

`ReviewRecords` (new, `Workflows/Kinds/Review/`) keeps `workflows/units/<key>.reviews/<node>-<attempt>.json` beside the run's record, as
the return's journal is (`ReturnDoor.JournalOf`), written once, atomically, when the review's attempt begins, and never edited:

- **Identity:** the run, the review node, the attempt, the kind (`branch`, `checkout`, `folder`, `copy`, `plan`), the place.
- **The artifact's reference** and **the revision** (§2.3), a digest over the reference and the handbacks' ids, so a new handback at the
  same content is a new revision.
- **The parts:** each quest and session of the chain in this repository (workflows rule 15), the rows it made, and its handback.
- **The handbacks** (driving rule 7, whole): each with its quest's requirements and what checked them, *unchecked* where D255 holds,
  and the second opinion's findings where one was asked.
- **Completeness:** each row cut, too large to show, unreadable (listed with no hash), or past the manifest's bound, said apart from
  *changed nothing*; and the coverage where history is gone (*since this retained baseline*, §11).

A moved revision is a new attempt of the review, as a moved subject lapses a binding today, and so a new record; the older one stays,
what an earlier receipt accepted. The review's binding is the record (`review:<run>/<node>/<attempt>` at its revision), so a run with no
branch binds as surely as one with. `RunInputs`' rule that a `work` input is the live branch at its tip (`Workflows/Nodes/RunInputs.cs:25`)
gives way to the record: a step after a review reads the record that review accepted, which `ReviewedTip` read for a branch alone.

**The Accept's receipt** is the word kept on the run's record as today (workflows rule 18), naming the record, its revision and its
author (the person, a supervisor's decision, Ask Daoris's press under D261), and how it was given (a press, an early Accept, a carried
one, the terminal).

### 2.3 Per kind

| Kind | The artifact's reference | Its rows | Said where attribution is unsure |
|---|---|---|---|
| `branch` (git, apart) | the run's session branch at its tip, and the uncommitted paths with their hashes | commits past the line by part, files with their diffs, the uncommitted apart (today's `SessionReviews`) | — |
| `checkout` (git, in place) | the run's turn log: each turn's start and end `HEAD` and its own paths, and the own uncommitted paths with their hashes at the newest end | commits reachable from a turn's end and not its start, *made while its turn ran*; commits between turns, *not this work's*; the own uncommitted files | *made while its turn ran*, never *made by it*: a commit the person made during a turn is in the interval too |
| `folder` (in place) | the run's baseline (its first turn's start manifest) and its newest turn's end manifest | each file added, changed or removed, text with its diff (today's `PlaceChanges` rows) | a path changed between turns (what the next turn's start told, `PlaceTold.Outside`) marked *changed outside its turns*: a hash recovers no author |
| `copy` (folder, apart) | the copy, against the base it was made from (`FolderCopies.Base`, as today) | the copy's files, each one the folder also changed since the copy was made marked *changed in your folder too* | — |
| `plan` (system) | the plan's record and number (`SystemPlans`, numbered per record), its system and place, each operation's precondition, and the content's digest | each operation as a resource with its value before and after | — |

1. **The turn log.** `RunTurns` (new, `Departments/Manifest/`) appends each of the run's turns as it ends: its record, its manifests'
   moments, the start and end `HEAD` in a checkout, the own paths, the outside paths its start was told. It is the run's, never
   rewritten, so a record's later turn no longer erases its earlier interval. Where the person rewrote the branch between turns (a
   rebase, a reset), the commits of an interval the end no longer holds are listed as *no longer in the checkout*, and the files are
   drawn at the newest end against the first start.
2. **The baseline is retained.** The run's first start manifest is kept in the place's manifest store as the record
   `run-<key>-base`, which the store's tidy keeps among its named records while a review record of the run names it, and whose blobs
   its content sweep counts (`ManifestStore.Tidy`): a file beside the run's record would keep a manifest whose content the sweep
   deletes.
3. **Empty is a record.** A run whose work changed nothing writes a record of its kind with no rows, and its review waits for the person
   with the handbacks alone (§2.4). The `empty` hold never reaches a review.
4. **A chain is one record per repository.** Its `branch` is the branch the chain lands on (workflows rule 36), its folder rows span its
   records, and each row names the part that made it. An ask's parts in several repositories are several runs, each with its record.
5. **A conversation's run** reads its submitted candidate (workflows rule 20) as a `branch` record at the submitted tip, the candidate's
   number in its reference.
6. **A plan that does not read, or no plan.** A plan record that does not read is listed unreadable, and a run whose work kept no plan
   has none: either way the record carries no plan, its review waits with the handbacks and any document rows, and the `apply` it feeds
   has nothing to carry (§3.4).

### 2.4 What reaches the review other than a word

The person's Accept or a supervisor's (workflows rule 18) reaches a review, and nothing else does, with one exception kept from
today: a `branch` record whose landing is seen made, or whose commits the line holds, reaches it by the landing record (`by: git`),
since there is nothing left to decide. The review reads its record (`ReviewReading`, new, beside `ReviewNode`), and `LandReading` is the
`land`'s alone.

## 3. Completion, and the effects after a review

### 3.1 A run is complete when nothing in it can move

A run is over when no node of its graph can still move (none ready, active, unknown or invalidated), no attempt or word is pending, and
no failure is kept for the person: `Over`'s second half alone. The half that ends a run once its end node is settled goes, since a step
after the end that has not started yet is cut off by it (§1.4). The graph's `end` keeps one meaning: the node whose outcome is the work's,
said on the run's line and in Notifications; steps after it (a release after its line) run to their settle before the run is over.

The run's end keeps its outcome (`RunEnd` gains `Outcome`: `completed`, `stopped` with whose stop, or `failed` with the node), written once
with the end, so the run's state (§4) is read from it rather than reconstructed from attempts a later reopen moves.

### 3.2 Effects, their order, and where a graph may end

- **The effects** are the kinds that write the work where the person keeps it: `land`, `return`, `apply`, each declaring it
  (`IWritesWork`, beside `IMakesLanding`). A plugin's step is no effect of the work: it takes what its wires feed it.
- **Order** (`ProtectedOrder`, widened from landings to effects): where a graph holds a `review`, every way from an entry to an effect
  passes a review, and the effect is fed the record of the last review every way to it passes; the save refuses an effect a way reaches
  around its review (`review-order`), and an effect two reviews feed by different ways (`review-ambiguous`). The holds and gates keep
  their own rules (workflows rule 9).
- **The end:** a graph holds at most one `land`; its end may be any node no effect lies past (`effect-past-end`), so the work's outcome is
  read after what carried it out. A `review` with a `land` after it cannot end the graph, as D264 held.
- **No review:** a graph with no `review` lets each effect take the work as `claimed` produced it, read into a record of its kind at the
  effect's start, the standing authority judged again before the first effect, as a landing flow with no review does today (workflows
  rule 22). It is the person's choice in the editor, and lowering the person's part applies only by their press (workflows rule 12).
- **Reading the process:** `WorkflowProcesses.Graphed` reads a graph with a `land` as today; one with none reads as work done in its
  place, whatever its effects (`InPlaceProcess`), and a landing's press under it is refused as today.

This replaces round 1's `ICarriesOut` exception, which let one effect end a graph and left `review → apply → plugin receipt` and
`review → return → publish` unsavable at once.

### 3.3 The effect is fed the accepted record, by reference

- **`land`** (or PROCSTEPS1's `rebase`) binds the record's tip, as `ReviewedTip` does today, and lands it only while the branch holds that
  tip; a carried Accept stands for the repair its landing asked for (§6.5).
- **`return`** brings the copy back at the record's revision: under the place's lock it reads the copy afresh, and where the copy moved
  since, it refuses `moved`, and the review is judged again at the new revision.
- **`apply`** applies the record's plan, by record and number; a plan written since waits for a review of its own, and never replaces one
  in flight.

The doors read the copy and the plan from the record's reference, never from the run unit's executors, which the runtime's unit does not
carry (§1.3 point 8). Where no review feeds an effect, the record read at its start is the reference.

### 3.4 Partial effects and the outcomes of each case

- **A return is incremental.** `FolderReturn` writes every file only the copy changed before it waits on those changed on both sides
  (`Departments/Return/FolderReturn.cs:32-50`). Its node says both, *12 brought back; 3 wait for your choice*, and keeps the written ones
  on its journal; *Stop…* at that stage says *the 12 brought back stay; the 3 stay in the copy*.
- **A choice is bound to what was shown.** A choice is kept with the hashes of the copy's side and the folder's side the person was
  shown (`ReturnNode.ChoiceKey` gains them, `Workflows/Kinds/Return/ReturnNode.cs:85`). The return applies it only while both sides still
  hash so; where one moved, that path's choice lapses and asks again with the new versions, and the others stand.
- **A stale plan may be partly applied.** An unguarded system applies operation by operation and stops `stale` after earlier ones
  succeeded (`Plugins/Systems/SystemReadBack.cs:16-30`). The attempt's receipt keeps what was applied; the way back to the work tells it
  the stale resource and the applied ones; it plans again from what the system holds now, and that plan is reviewed as its own record,
  which names the operations already applied.
- **Outcomes.** An `apply` settles every case: `applied`, `unchanged` (its plan changed nothing), `none` (no plan, or one that does not
  read: nothing applied, said why), `stale` with its receipt; a return `returned`, `nothing` (the copy changed nothing), `moved`; a
  document-only answer in a system is `none` with its document shown on the review. None of them is left active.

### 3.5 Shipped graphs

*Plan, review, apply* for a system (`claimed` → `work.requests` → `review` → `apply`, the apply's `stale` a way back to `claimed`, place
§5.3) and *Work apart in a copy* for a folder (`isolate` `copy` → `claimed` → `work.requests` → `review` → `return`). The window's add
chooses *Plan, review, apply* for a new system. An existing system department on *Work in place* keeps its choice; its page says *Work in
place applies no plan* and offers *Plan, review, apply* (§11).

### 3.6 D264, amended

D264's note made a landless graph end at the person's review, rejecting an end at any kind that may end because a department's work would
end on its agent's done with nobody having read it. That reason stands for the shipped defaults, which all hold a review; it does not
justify refusing a person the choice, since a landing graph may already go without one (workflows rule 22), and the owner's rule is that
more control is a node the person adds. Under §3.2, a run with no review is still read: its record is written at the effect, shown on its
review read-only and in *Done and failed*. D264 takes a dated note saying so; its rejection of a review ending a graph that lands stands
as `effect-past-end`.

## 4. The five states

### 4.1 The states

A session, and the run it serves, is in one of five states, read in this order (the first that holds):

1. **Needs you** (等你处理): a human must act before it goes on: a review, a question, a choice, a card, a sign-in.
2. **Failed** (失败): a failure nothing goes on from without the person; its recovery, where one exists, is inside.
3. **Working** (工作中): under way and needing no one, whatever it waits on.
4. **Pending** (待开始): not started, or its next start held: queued, held, or paused, the reason inside.
5. **Done** (已完成): complete, a person's stop and an abandon included, the reason inside.

*Archived* stays a mark on a done or failed session, the list's fold, never a state; a mark never hides what needs the person.

### 4.2 Each from today's states

| State | From today (record `state`, `ShownState`, the list's group, the planner's `StartVerdict`, a run's `PersonWaits`, the head's *now*) |
|---|---|
| needs you | `awaiting-person` unanswered and not a teammate's (a park: a question); a run's step waiting on the person: `accept`, `approval`, `task`, `go-ahead`, `held`, and `choose` (new, §7.2); a tool call held for the person's answer (`permission`), a third answer in a row (`answer`), a terminal's press waiting for its Confirm (`confirmation`); `PolicyHeld` (your yes to move), `Unclosed` (your done); a second opinion waiting on the person; a declined request waiting on *Pass over…*; a start held for a sign-in (`signed-out`, an account wait with no account); the steward's stop holding its quest for a *Try again*. The list's *Waiting on you* bar a park after failures, the review half of *To review*, the head's *waits on you* and *N parts to land* |
| failed | a run ended at a failed step (§3.1's outcome); a failure kept for the person's *Try again* (a plugin's attempt, a check's *Not now*); `Exhausted` (`parked` after failed sessions); `TransientHeld` (past its transient bound); `not-started` (a start whose choice is not offered); `limited` where no reset resumes its words; a record `failed` with nothing carrying it on |
| working | `starting`, `working`; `answered`, `going-on`; `idle` (a live chat between turns); `awaiting-reply` (another session), `awaiting-helper`, `WaitsForOpinion`; `TakenElsewhere` and a teammate's park (on their machine); a record `failed` the planner carries on; a run's steps after the work that need no one: landing, push, a pull request open, its merge watched, a release, a return or an apply under way, a slow step. The list's *Working*, the head's *working* and *landing* |
| pending | `queued`; an ended record whose words wait on a hold (*Resumes later*): the cap (`AtCapacity`), a session in its way (`RepositoryBusy`, `busy`), the person's hold of the repository (`Held`, `hold`), a pause of its ask or quest (`Paused`, `paused`), an account cooling (`limit-waits`, `cooling`, until when), a dependency (`Blocked`), the planner's other holds (`waits`) |
| done | `completed` with its run over and not failed; `stopped` and an abandon (*stopped*), `declined`, `stood-down`; a run over with outcome `completed` or `stopped`. The list's *Ended* bar its failures |

There is no landing task as a person's state. *To review* as a group of trees holding commits goes: a run's review waiting is *needs
you*; a tree holding commits no run waits on (a stopped session's, a conversation's before it submits) is said inside its session, on
its Review tab, with what its kind offers there (*Discard tree*; a conversation's press). The head's *N parts to land* is *needs you*'s
reason (*2 parts wait for your review*), and *landing* is *working*'s.

### 4.3 Where they are said

- **The list.** Sessions' groups are the five, in the order the person acts on them: *Needs you*, *Working*, *Pending*, *Failed*,
  *Done*, then the *Archived* fold. `SessionGroups.Read` stays the one reader (`WorkOrders/SessionGroups.cs`), its `SessionGroup` words
  `needs-you`, `working`, `pending`, `failed`, `done`, `archived` (`Workplace/Domain/SessionGroup.cs`), and `daoris session list
  --group` takes them; each row's second line is the finer reason, from today's shown words.
- **The head.** Its state is one of the five; its two facts lead with the reason (*waits for your review*, *queued for a slot*,
  *landing*, *pull request open*, *held: its account resets at 14:10*, *failed at push*, *stopped by you*). The session's life flow draws
  the five (`Web/work/head/sessionLife.ts`).
- **Notifications.** *What needs you* holds every *needs you* row (§9); *Done and failed* holds each *done* and *failed* outcome. A
  pending or working session is not a notification: it asks nothing and nothing has ended.

### 4.4 A run's state

A run's state is the strongest of its steps': *needs you* while any step waits on the person; *failed* where it ended failed or holds a
kept failure; *working* while any step is live; *pending* while bound with nothing begun; *done* where it is over and not failed. The
session a run stands for (§5.2) takes the run's state where it is stronger than its own: a completed session whose run waits on a
go-ahead is *needs you*, as `SessionGrouping.WaitedOn` places it today.

## 5. One review surface

### 5.1 Where it lives: the options

- **A. The session's Review tab, per session (today).** A run spans sessions: a chain's parts, a carry-on, a send-back's next turn,
  two sessions planning one system. Each would show a part of one decision.
- **B. The run view.** The Workflow tab draws the run's steps; it would grow a diff, a file list and a plan beside a chart, in a side
  bar at its floor width.
- **C. The Review tab as the run's review.** The tab every session's page already has shows the review of the run its work belongs to,
  whichever of the run's sessions is open. A session no run governs (a helper, a reviewer, Ask Daoris, a conversation with no candidate)
  shows its own changes there, read-only, as today.

**Recommended: C.** The person already reads a session's work on its Review tab; the change is its scope, a run, and its presses, which
are the run's. The run view keeps the graph and every step's presses, and its review step opens the Review tab (*Open its review*).

### 5.2 Opened by run

The review has a stable identity, its run, and one opener, `review/<run>` (the newest record, or an attempt named), from any of the run's
sessions (by `QuestRuns.Of`, every session of the run, not only the newest), its run view, a *What needs you* row, a *Done and failed*
line, the terminal, and after the run's trees and sessions are gone. `RunWaitSessions` stays what it is, the session a waiting run is
listed by in Sessions, and no longer decides where a review opens.

### 5.3 Its shape

Top to bottom, one column (desktop rule 27):

1. **What is decided**: one sentence of its kind (§5.6), the revision's short form, since when it waits, who it waits on, and the presses
   (§6): *Accept* (采纳), *Send back…* (退回…), *Stop…* (停止…), and *Discard tree* where the kind keeps a tree apart. This bar stays in
   reach as the reader scrolls a long review: Accept never needs every row passed first. A supervisor's held decision stands here with
   *Overrule* (§8); a `return` waiting on the person's choice puts its per-file presses here, in the place of the presses that no longer
   apply.
2. **The handbacks**: each part's note, its requirements and their checks, *unchecked* where D255 holds, the second opinion's findings.
3. **The changes**, drawn by kind (§5.4), in today's multibuffer, with what is incomplete said on its rows and in one line above them.
4. **What follows**: the run's graph after the review as the run's graph contract draws it (`Workflows/Domain/WorkflowGraphRun.cs`):
   conditional routes, untaken ones, ways back and retries, each step with its state, its outcome and its door (a pull request's address,
   a landed branch, a release's evidence). Before the Accept it says what the Accept may start, never implying a route will run; after it,
   it is the live line of what the work came to.

The head's *Next* names the run's review while it waits (the `accept` wait of desktop rule 38, standing on the run's person wait rather
than on a tree's offer), its door the opener; the head holds no *Accept…* of its own.

### 5.4 How each kind is drawn

| Kind | Drawn as |
|---|---|
| Commits (`branch`, `checkout`) | the commits listed by part, then the files with their diffs, unified or side by side; a `checkout`'s commits marked as §2.3 says |
| Files (`folder`, `copy`) | the files with their diffs where text, a row with *Open* where not (today's `DiffFileRow` with `place`), the marks of §2.3 |
| A plan | one row per resource: its operation (create, change, delete), the system's name for it, its value before and after as a diff; the adapter's own rendering where it gives one (place §5.2) |
| A document | a Markdown file in any record: rendered with the window's `Markdown` (`Web/work/conversation/Markdown.tsx`) over the pinned content, the diff one press away |
| Nothing | the handbacks alone, under *It changed nothing* |

### 5.5 Reading while the work moves

- **Pinned.** Every row and preview draws the record's revision: a commit's own object, the manifest store's blob for a folder's file; a
  file in a tree is drawn only while it still hashes as the record says, else it says *changed since this revision*. The run's review is
  keyed by run and revision, so no session's ending makes it final (`trees.ts:242`'s rule stays a session's).
- **Viewed** marks are kept per reader by path and content: an unchanged file keeps its mark across revisions, a changed one loses it.
  The reading position is kept across a refresh.
- **A newer revision** is announced over the bar, *A newer revision is ready: 2 files changed since you started reading*, with its delta
  and *Read it*; the page never replaces what is drawn under the reader, and reading holds no lock and no lease on the place.
- **Incomplete** is information: a cut, too large or unreadable row is said, and never makes Accept wait for every line.

### 5.6 The words

The sentence that says what is decided, in both languages (glossary: *accept* 采纳, *send back* 退回, *line* 主线, *plan* 计划, *apply*
应用):

| Kind | English | 中文 |
|---|---|---|
| `branch` | Accept the {{count}} commits on `{{branch}}`. | 采纳 `{{branch}}` 上的 {{count}} 个提交。 |
| `checkout` | Accept the {{count}} commits made in `{{repository}}`'s checkout while its turns ran: they are there already. | 采纳它的回合期间在 `{{repository}}` 检出里做的 {{count}} 个提交：它们已经在那里了。 |
| `folder` | Accept the {{count}} files changed in this folder during its turns: they are there already. | 采纳它的回合期间这个文件夹里改动的 {{count}} 个文件：它们已经在文件夹里了。 |
| `copy` | Accept the {{count}} files it changed in its copy, and they are brought back into your folder. | 采纳它在副本里改动的 {{count}} 个文件，它们会被带回你的文件夹。 |
| `plan` | Accept its plan of {{count}} changes to `{{system}}`, and Daoris applies it. | 采纳它对 `{{system}}` 的 {{count}} 项变更计划，随后由 Daoris 应用。 |
| nothing | It changed nothing: read its answer, and accept it. | 它什么都没有改动：读完它的回答后采纳。 |

Where the graph puts nothing after the review, *and Daoris applies it* and *they are brought back* are not said. The run view's step
says *Waits for your review:* and the same sentence, in place of `workflow.run.said.landing.accept` and `workflow.run.said.work.land`;
the step keeps its name, *Your review* (你的审阅). The send-back's words to the work are its kind's (*what you change is reviewed
again*), never *commit*.

The glossary's *accept* becomes *the person's yes to a run's work under review, the record of its commits, files, plan or answer, which
lets the run go on to what its workflow puts after the review*; *review*, *reading what a run's work changed and what it says it did,
before accepting it or sending it back*; *plan* widens to *what a place's Setup choices, or a session's work in a system, would change,
read before anything is written and carried out by Apply*; and the five states take a row each.

## 6. Accept, send back and stop, and the press behind them

### 6.1 The press: the options

- **A. Keep `LAND_SESSION_TREE` for a branch and `WORKFLOW_NODE` for the rest.** Two doors for one word (D228).
- **B. The run's node door for every kind.** `WORKFLOW_NODE { run, node, act: accept, record, revision, answers }` keeps the word, as it
  keeps every other word of the person's (workflows rule 18), and `daoris workflow accept <run> [<node>] [--at <revision>]` is its twin.
  What `LAND_SESSION_TREE`'s press does around the word moves behind it as the review kind's: the second opinion's answer token rides on
  the word (`answers`), the early Accept is kept by record (§6.5), duplicates are refused `worded`, and for a run whose next step is a
  `land` the press waits for what the try came to, as `PerformedAnswers` answers it today, within the same bound and with the same
  progress.
- **C. A route of its own, `REVIEW_ACCEPT`.** A third door beside the two that already keep the word.

**Recommended: B.** The node door is already the one door for the person's words, Ask Daoris's press reaches it as a verb (D261), and the
record is a field. Waiting for the landing behind it is orchestration around the one acceptance, not a second one. `LAND_SESSION_TREE`
keeps its plan read (`press: false`), which is the `branch` kind's *what follows*; its press half goes, and `trees land` with it.

### 6.2 The revision is checked three times

A word names the record and revision the page drew, and the review's attempt. It is checked:

1. **when it is kept**, against the record the review stands on; a word on another is refused `moved`, with the newer record, and the
   page draws it;
2. **when the pass applies it**, after the pass has read the review's subject afresh, since a page can be stale before the runtime has
   noticed the work move (`NodeRuntime.cs:378` applies words before evaluation today); a word whose record lapsed meanwhile is dropped
   `moved`, said on the run and to the press that waits;
3. **when the effect begins**, which reads its reference again (§3.3) and refuses `moved` rather than carry out anything the record did not
   hold.

Nothing is accepted that was not drawn, and a stale page never accepts newer work.

### 6.3 Per kind

| Kind | Accept | Send back… | Skip | Stop… / Discard |
|---|---|---|---|---|
| `branch` | the run goes on to its landing (or PROCSTEPS1's `rebase`) at the record's tip; uncommitted work carries the session on (§6.5) | the `changes` word with the person's words, to the part named: its session's next turn; the review asks again at the next record | never | *Stop…* ends the run; *Discard tree* removes the tree, and the record stops the review |
| `checkout` | the run goes on to what follows, else ends; its own uncommitted paths are the person's now, so the lease is let go (D258's *let others work there*), said before the press | as `branch`, in the checkout | never | *Stop…*: the commits stay where they are |
| `folder` | as `checkout` | as `branch`; the next turn adds to the run's record | never | *Stop…*: the changes stay |
| `copy` | the run goes on to `return`, which brings the accepted revision back and asks the person per file where both sides changed | as `branch`, in the copy | never | *Stop…*: nothing more comes back; the tidy sets the copy aside |
| `plan` | the run goes on to `apply` with the accepted plan | as `branch`: the session plans again | never | *Stop…*: nothing more is applied |
| nothing changed | the run ends, or goes on through what follows, which has nothing to carry (`NothingToDeliver`, §3.4) | as `branch`: its next handback is a new record, though no file changes | never | *Stop…* |

A review is never skipped, of any kind: a skip would read as accepted (`ReviewNode.Row`'s `PersonSkips: false`). A place that wants no
review chooses a workflow without one (§3.2). *Send back…* is the one way to return work with words: the `changes` word, which gains the
part it goes to (its quest; the newest part where none is named, which the ask names: *Tells the session that made part 2*), and is kept
until the part's next handback, a commit or none, makes a new record (`Over`'s `changes` waits on that, not on a commit). The composer's
door from the review goes.

### 6.4 Stop

- **Before the Accept**, *Stop…* stops the run: every live step of it stopped as the person's, with their words, as an abandon stops a
  run's steps (`Workflows/Nodes/AbandonRuns.cs`), so nothing after the review runs; its ask says what stays, per kind (§6.3), and the run
  is *done*, *stopped*.
- **After the Accept**, while what follows runs, the bar's press is *Stop what follows…*: the steps not begun never begin; a step under way
  stops where its door can stop it (a return between files, a wait), and the ask says what has already happened and stays (*merged on the
  line; the pull request stays open*, *12 files brought back*, *2 of 5 operations applied*), never promising an undo.
- **A step's own stop** stays the run view's, for the person who means one step (`RunNodeWords.Acts`).

### 6.5 The early Accept, the unchecked done, the second opinion, the carried Accept, the conversation

- **Early Accept (D256)** is kept with the record and revision the person pressed on, where it now keeps a commit
  (`Workflows/Nodes/EarlyAccepts.cs`), and applied at the first pass that finds the review waiting at that record; the review beginning at
  another drops it, said. For a `branch` the revision is the tip with its uncommitted digest and handbacks, so an Accept on a page drawn
  before the handback arrived is not applied to it.
- **The unchecked done (D255)** stands as the `quest.holds` wait before the review. On the review, where the handbacks are drawn,
  *Accept…* asks once naming each unchecked handback (*Its quest carried no requirement, so nothing checked its done: you read its
  handback above*) and keeps two words in one edit of the run's record: the hold's pass, its words *passed by your Accept on its review,
  which named it unchecked*, and the review's early Accept on the record drawn. Both are kept or neither; a press replayed finds them kept
  and is refused `worded`; a record moved before the pass drops both. No other hold is passed this way.
- **The second opinion** stays a gate before the review; its findings are drawn in the handbacks, and an Accept that answers a dispute
  carries the gate's token as `answers`, as the review tab's press does today.
- **A carried Accept** (`CarriedAccepts`) stands for the repair its landing asked for: the carry-on that commits the uncommitted work or
  resolves a conflict with the line, at most `LandCarry.Bound` times. Its receipt names the accepted record and each tip it carried to,
  and the review's *What follows* and the outcome say the carry's delta (*accepted at a1b2; carried on to c3d4 to commit 2 files*). Any
  other move (a send-back's turn, a new part) lapses it, and the review asks again.
- **A conversation's press** is its submission and then the same keep: it reserves the conversation and captures one tip as today
  (`TreesLandDesk.ChatPressAsync`), submits the candidate as the run's next numbered one, and keeps `accept` on that candidate's record.

### 6.6 An entry's or an ask's one Accept

`EntryAccept` stays the press for several runs at once (workflows rule 24): each part drawn with its kind's sentence and the revision read,
the parts of one run grouped into one word on its record, and each run's word kept through the same keep the node door uses. It is not
atomic across runs: a run whose record moved is refused `moved` alone and the others go on, and the answer says which accepted, which moved
and which failed.

### 6.7 The terminal

`daoris workflow review <run>` prints the record whole, and `daoris workflow accept <run> [<node>] [--at <revision>]` keeps the word.
With `--at` it is refused `moved` as a press is; without it, it accepts the record the review stands on when the word is kept, and prints
that record's sentence and revision as it does: a convenience that proves nothing was read, said so in its usage.

## 7. After the Accept: watched steps, and when the person is asked

### 7.1 Watched

The steps after the review (a `land`, a `push`, a plugin's `pull-request`, a `monitor`, a `tidy`, a release plugin's `find`, `release` and
`publish`, a `return`, an `apply`) are watched: each shows on the review's *What follows* and on the run view, with its outcome in the
page's words (`workflow.outcome.<kind>.<outcome>`) and its door, read from the node's attempts and outcomes; the `monitor` stays the pull
request's state reader it is. The head gives the run's newest outcome as one of its facts (*Merged on the host*, *Applied to `{{system}}`*).

### 7.2 The waits

The person is asked only by a step that waits on them, each a person wait (`PersonWaits`), the run *needs you* while one stands:

| Wait | Its step | Today | Now |
|---|---|---|---|
| `accept` | an active `review` | listed | listed, worded by kind |
| `task`, `approval` | an active `human` | listed | listed |
| `go-ahead` | an active `go-ahead` | listed | listed |
| `held` | an attempt held unknown | listed | listed |
| `choose` | a `return` waiting `both-changed` | not listed | listed: its paths counted, each with the versions shown (§3.4) |

### 7.3 Failure, and its recovery

A failure nothing goes on from is the run's *failed* state (§4), said on the run view, the review's *What follows*, the head and *Done
and failed*, never a wait in *What needs you*: it asks the person nothing they must answer. Its recovery is offered where a door for it
exists, and said absent where none does:

- **a failure kept for the person** (a plugin's attempt, a check): *Try again*, the attempt's `again` (`WorkPersonWord`), and a check's
  *Not now*;
- **a held attempt** whose outcome is unknown: the `held` wait's words, which reconcile the invocation before anything runs again;
- **a quest parked or held past its transient bound**: the planner's *Try again*;
- **a run ended at a failed step**: *Open its review* and the step's outcome; the work goes on only by a new turn (*Send back…* on a
  review not yet accepted) or new work.

A release that fails after its merge says both facts, *merged on the line; release failed*, and its *Try again* retries the release's
attempt alone: the landing stays reached, so no merge is repeated, no pull request reopened, and no Accept asked again of unchanged work;
an uncertain publication reconciles its invocation first. Each attempt's failure stays in *Done and failed* after a retry succeeds.

### 7.4 Slow steps

A step that is only slow (a push active long past its bound, a request no answer reached) is said on the run view and on *What follows*
with what it waits on, and is *working*: no approval is made up for it. Where what it waits on is the person's to fix (a signed-out
account, a plugin not installed, a credential missing), that dependency is the person's *needs you* row with its own remedy (a sign-in,
the plugin's page), as an account's wait is today.

## 8. The supervisor's place

Today a landing supervisor's `accept` of a `review`, or `approve` or `reject` of a `human` approval, is kept against the step as it waits
on that attempt, held for the role's window with *Overrule*, and released as the supervisor's word (roles rule 49); `LandingJudge` checks
the step and the holder's authority and reads no content; the decision's facts are the run, step, attempt, repository and acts
(`Modules/Bridge/SupervisorKinds/LandingKind.cs:42-48`); the deciding session must be the one whose quest is the run's id (`LandingKind.cs:52,60`);
and the kept decision names the attempt and no revision (`Roles/SupervisorLandingDecisions.cs:13`).

- **A. As today, on every kind, on the same facts** (recommended). The decision's facts are the review record's, pinned: the kind's
  sentence, the revision, the rows in summary, what is incomplete, every part's handback with its checks. Any session of the run's chain
  in the repository may decide (`QuestRuns.Of`, not the first quest alone). The kept decision names the record and revision; it is checked
  when released and when the pass applies it, `stale` where the record moved, as a word on a lapsed attempt is. Its card
  (`SupervisorLandingCard`) is drawn on the review's bar as well as the head, one press in two places as desktop rule 38 has it, with
  *Overrule*, its sentence the kind's (*The supervisor accepted this; it is applied at 14:10.*).
- **B. A system's plan the person's alone, by the kit.** A plan's Accept lets a write into a live system happen. But today a supervisor
  may accept a review of any kind, so this would be a new block on development, and the person who wants it has the workflow node: a
  `go-ahead` before the `apply`. A supervisor's decisions are held only while a step that holds the run is active
  (`Workflows/Nodes/LandingSteps.cs:26`), so with the go-ahead after the review the supervisor may accept the review, and the go-ahead,
  which no supervisor answers (D233), still holds the write until the person's yes.
- **C. A grant per subject kind.** A supervisor's grants naming the kinds it may accept: a second way to say what the `go-ahead` says.

**Recommended: A**, with B's protection reached through a `go-ahead` where a place wants it; *Plan, review, apply* ships without one.
A `return`'s per-file choice is the person's (D265), its kind offering a supervisor no act. Who may call `system_plan` stays D270's,
which grants no acceptance and no apply. Ask Daoris's press in full access is the person's word (D261), read afterwards by its second
check (D272); the receipt names who gave the word (you, the supervisor with its decision, or Ask Daoris's press beside its line in
*Notifications*).

## 9. Notifications

- ***What needs you*** holds every *needs you* row. A run waiting on the person is one row (`run:<run>`, read from `PersonWaitReader`
  rather than from Sessions' review group, `Modules/Loop/NeedsAPerson.cs:158`), titled by the work and said by its first wait, carrying
  every wait on it with its node and press, so an approval or a go-ahead is never lost behind an Accept. It opens where its first wait is
  answered: the run's review for `accept` and `choose`, the task for `task` and `approval`, the ask's go-ahead for `go-ahead`, the
  attempt for `held`. A wait another row already answers (an ask's go-ahead row, an integration's card) is listed there alone. Its three
  groups, *Holding work*, *Waiting for your word* and *Ready for you*, fold into one list in their order, each row's reason its line: one
  state, the finer reason inside. A row is answered, never dismissed (desktop rule 31).
- ***Done and failed*** holds each outcome once: a session's end as today, and a run's step outcome the person watches (landed, merged, a
  pull request opened, a release made, a plan applied or partly applied, files brought back, a failure with its words), keyed
  `outcome:<run>/<node>/<attempt>` in `data/notices.json` (`Events/Notices.cs`), written when the attempt settles and again by the next
  pass where the write was lost, a key already kept being kept once. A line opens the run's review by run, no session needed, and offers
  the recovery of §7.3 on a failure. None of them is a row of *What needs you*, and none asks anything.
- **Announcing** follows desktop rule 33: a review that waits announces as a finished session's review does today; an outcome announces
  only where the notify switch says.

## 10. What goes (D228)

| Today's way | Goes for | Its row |
|---|---|---|
| `ReviewNode.Subject` → `RunSubject.Branch`; a `work` input read as the live branch (`RunInputs.cs:25`); `ReviewedTip` | the review record, and the effect fed the accepted one | a, b |
| The review read through `LandReading`, reached on an `empty` hold | `ReviewReading` | a |
| `PlaceChanges` per record's newest turn; a checkout's range read from one record's base | the record from the run's baseline and turn log | a |
| `Over`'s end-settled half; `ICarriesOut` (round 1) | completion as nothing left to move | b |
| A graph with no `land` ends at its review, alone (`WorkflowNamed.OneLandingProblem`, `WorkflowProcesses.Graphed`'s `inPlace`, `GraphStanding.Judged`'s landless half) | §3.2's order and end | b |
| `ApplyDoor`'s newest plan from the unit's executors; `ReturnDoor`'s copy from them | the record's reference | b |
| `LAND_SESSION_TREE`'s press and `trees land`'s press | `WORKFLOW_NODE accept` and `daoris workflow accept` | c |
| The head's *Accept…* ask (`SessionHead`'s `lands`, `landing`, `onLand`) | *Next: Review* | f |
| The review tab's composer *Send back…*, and the run view's *Ask for changes…* (`workflow.run.node.changes*`) | one *Send back…*, the `changes` word with its part | f |
| `PlaceNote`, `NothingToLand`, `work.review.empty.*`, `workflow.run.said.landing.accept`, `workflow.run.said.work.land`, the send-back's *commit* | §5.6's sentences | f |
| *viewed* keyed by path per session; a review's diff final once its session ended | by path and content, per run and revision | f |
| Sessions' groups `you`, `review`, `later`, `ended`, the head's *N parts to land* and *landing* | the five states | g |
| *What needs you*'s review row per session, from Sessions' group; its three groups | one row per run, from the runs' waits; one list | h |
| *Done and failed* opening only by session | by run | h |
| A supervisor's landing decision bound to its attempt alone; admission by the first quest | bound to the record; any part of the chain | i |

An older record still read keeps its path only with the condition that ends it (§11).

## 11. Runs in flight

The release's home migration takes one step, `review-records` (beside `run-rebinds`, workflows rule 44), and invents no consent: a word
given before the update keeps the meaning it had, and no pinned graph is edited (`WorkflowProcesses.cs:89` holds a run to its version's
digest).

1. **A `branch` run's review** observes the session branch at its tip, as its record does: the step writes the record of the attempt it
   stands on, and a kept word, a held supervisor decision, a carried Accept and a conversation's candidate stand as they are.
2. **A branchless review withheld `no-subject`**, active never or once: the step writes its first record at the work's present revision
   as a new attempt. No word is carried onto it, since none could reach it.
3. **An unscoped word on a branchless review** (an early Accept kept with no commit, a supervisor's held decision): kept as *legacy*, the
   receipt saying *accepted as it stood at 09:12, before Daoris drew a revision*, never bound to the present record. Under a pinned graph
   with nothing after the review (*Work in place*) it ends the run as it would have, that being all it ever allowed; under any other it is
   dropped, said on the run, and the review waits for a word on its record. A supervisor's legacy decision is dropped `stale` either way;
   the supervisor reads the record and decides again.
4. **A folder run whose baseline is gone**: its baseline is the earliest start manifest of its records still kept, else its newest turn's,
   and its record's coverage says *since this retained baseline*, never *since it started*. A checkout run's turn log begins at the update,
   said the same way.
5. **A system run**: no `apply` ever ran under a saved workflow (§1.3 point 9), so no accepted plan needs finding. A run pinned to *Work in
   place* keeps it: its Accept applies nothing, as it never did, and its review says *this workflow applies no plan*. A system department
   still choosing *Work in place* keeps the choice; its page offers *Plan, review, apply*, the person's press.
6. **A discarded tree** stops its review by the record, as today.
7. **The list, the head and *What needs you*** are computed, never stored: nothing to convert. *Done and failed* gains its run outcomes
   from the update on. The removed catalogue keys go in the row that removes their screens.

The step ends when no home saved before the build can start, and row d names the row that deletes it.

## 12. Rejected

- **A synthetic branch for non-git work** (§2.1 A): a second meaning of a branch for every branch reader.
- **The subject on the review alone** (§2.1 B): what is carried out would still not be what was accepted.
- **A revision recomputed by each reader, and one that leaves out the handbacks** (§2.1 C): readers disagree, and a corrected answer at the
  same content is no new review.
- **The review per session** (§5.1 A): one decision shown in parts; and **the review opened through the waiting session**: an earlier
  part, a finished run and a failure after the trees are gone could not open it.
- **Two press doors, or a third** (§6.1 A, C).
- **An Accept in the head**: it accepts a revision the head never drew.
- **A skip of the review**: it reads as accepted; a workflow without a review is the way.
- **The revision checked only against the stored binding**: a page stale before the runtime noticed the move would accept newer work.
- **The return's per-file choices made at the Accept**: the return reads both sides fresh under the place's lock (D265), and a choice made
  before it would rest on hashes that may have moved; a choice is made on the versions the return shows, bound to them.
- **An undo of work done in place or partly carried out**: Daoris has no revert of the person's folder, checkout or system, and the content
  store does not promise every blob (D265); *Stop…* says what stays.
- **A system's plan the person's alone by the kit, or a grant per kind** (§8 B, C).
- **One effect allowed to end a landless graph** (`ICarriesOut`): the next step after it (a receipt, a publication) is unsavable again.
- **A graph's end that ends the run while a later step could still start**: it cuts off the steps after a work's outcome.
- **A failed step as a wait in *What needs you***: a failure asks nothing the person must answer; it is the *failed* state, its recovery
  inside. And **listing only failures the node table offers a press on**: the table offers a failed node none, so it would list nothing.
- **Outcomes in *What needs you***: they ask nothing, so they would teach the person to dismiss what must be answered.
- **An old unscoped Accept bound to the present folder or the newest plan**: it would assert what the person never saw.
- **A new preset chosen for existing system departments, or a pinned run's graph edited by the update**: the first changes a choice
  silently, the second breaks a version's immutability.

## 13. The read: what was taken, and what was not

Every finding of the read was checked against the code at `9c100b32e2` and stands, with three exceptions:

- **"Reuse the copy's base; do not add a second baseline":** round 1 already read a copy from its base (its §2.2's `copy` row); the new
  baseline is a folder worked in place's, which has none. Nothing changes.
- **"No need to generalize the monitor":** round 1 proposed none; the `monitor` stays the pull request's reader, and plugin outcomes are
  read from the attempts (§7.1).
- **"Rendered document review is not an existing capability":** the preview draws source lines, as the read says, but the window already
  renders Markdown (`Web/work/conversation/Markdown.tsx`, react-markdown with GFM); row f renders a document with it over the pinned
  content instead of building a renderer.

The read's repaired `failed` wait (recovery doors listed first) is taken as §7.3's recovery list, with the wait itself replaced by the
owner's *failed* state; and the check found besides that such a run mostly ends with no outcome kept (§1.4), which §3.1's `RunEnd.Outcome`
answers.

## 14. The rows, in build order

Each row takes its records with it: its area's sentences rewritten in place (workflows rules 8, 9, 17, 18, 22, 23, 24, 44; desktop rules
31, 32, 37, 38, 39; driving rule 45; roles rule 49), its words in both catalogues, its glossary rows. Rows a and b take two parts of
PLACEWORK1's *1e's rest* (a review binding a non-git subject; the apply bound to the plan its review accepted). Every runtime proof runs
the graph through the node runtime, never a door alone.

| Row | What | Proof |
|---|---|---|
| **REVIEWUX1a** | The review record (driver): `ReviewRecords`, one immutable record per run, review node and attempt (§2.2), written at the attempt's start; the per-kind readers (§2.3); `RunTurns`, the run's append-only turn log; the run's baseline as the named record `run-<key>-base` in the place's manifest store, kept by its tidy and counted by its sweep; the review bound to its record, never `no-subject` for want of a branch, read by `ReviewReading`, never reached on an `empty` hold, stopped by the record where its tree was discarded; the revision over the reference and the handbacks' ids; completeness and coverage | Seen failing first: PLACEWORK1e's finding as a full-run test (a folder run's review becomes active on its record, not withheld); APPROVE1's case (work with no commits waits for the person); per kind, a record binds, an Accept keeps it, a moved revision is a new attempt and record; a send-back's corrected answer at the same digest a new record; a checkout's commit between turns and one during a turn marked; a rebase between turns; a folder path changed between turns marked; the baseline and its blobs surviving the manifest tidy; a cut and an unreadable row said |
| **REVIEWUX1b** | Completion and effects (driver): `Over` without its end-settled half and `RunEnd.Outcome`; `IWritesWork` on `land`, `return`, `apply`; `ProtectedOrder` over effects (`review-order`, `review-ambiguous`) and the end rule (`effect-past-end`) in place of the landless end at a review; `WorkflowProcesses.Graphed` reading any landless graph as in place; the effects fed the record by reference, `RunInputs`' `work` rule and `ReviewedTip` replaced; `ApplyDoor` and `ReturnDoor` reading the reference, and settling every outcome of §3.4; the return's partial state and choices bound to the hashes shown; the stale plan's receipt and the way back telling it; *Plan, review, apply* and *Work apart in a copy* shipped, the window's add choosing the first for a system, an existing system department's page offering it; D264's note | Full graph runs, failing first: a system run applies the stand-in system's accepted plan with a newer plan waiting for its own review; a copy run returns its accepted revision, refuses `moved` and asks again; an apply with no plan, an empty one and an unreadable one each settle; a stale second operation keeps the first's receipt and the replan names it; a target moved after a file choice lapses that choice alone; a run with no review applies with the standing authority judged; *Release*'s steps after its `line` run though not started in the pass that settles it; a failed step ends the run `failed`; the save accepts both shipped graphs and a review → apply → plugin graph, and refuses an effect around its review, two reviews feeding one effect, and an effect past the end |
| **REVIEWUX1c** | One press (modules, driver, terminal): `WORKFLOW_NODE accept` with `record`, `revision`, attempt and `answers`, checked when kept, when applied after the pass reads the subject afresh, and when the effect begins (§6.2); the early Accept by record; the unchecked done's two words in one edit (§6.5); the carried Accept as the landing's repair; the conversation's press as its submission then the keep; `PerformedAnswers` behind the node door; `EntryAccept` grouping one run's parts, partial answers per run; `changes` with its part, kept until the part's next handback; *Stop…* as the run's stop and *Stop what follows…* (§6.4); `daoris workflow accept --at` and `daoris workflow review`; `LAND_SESSION_TREE`'s press and `trees land`'s press removed, their census rows with them; the route's view for Ask Daoris's card (D261) | Route and verb tests; the census; a revision moved between the read and the press, the press and the pass, the pass and the effect, each refused `moved`; a restart between keep and pass; the compound press replayed refused `worded`, and dropped whole on a moved record; an early Accept on a page drawn before the handback not applied; a carried Accept landing its repair and lapsing on a send-back; the conversation's press; an entry with one run moved; a send-back to part 1 of two told to that part's session; the existing `PerformedAnswers` and landing tests passing through the node door unchanged |
| **REVIEWUX1d** | Runs in flight (driver): the `review-records` home step (§11): records for the attempts reviews stand on, branchless reviews withheld `no-subject` given their first record, unscoped words kept *legacy* and honoured only under a pinned graph with nothing after its review, a supervisor's legacy decision dropped `stale`, coverage said where the baseline is gone; its ending condition and the row that deletes it | The step on fixtures of each kind: a branch run unchanged with its word, carried Accept and candidate; a withheld folder review active on its new record; a legacy Accept ending a pinned *Work in place* run and dropped under a graph with an apply; a held supervisor decision dropped; a folder run with its first start gone saying its coverage; the step run twice |
| **REVIEWUX1e** | The run's review read (modules): `RUN_REVIEW { run, attempt? }` reading the record (the kind and its sentence's values, the revision, the rows per kind, the parts and handbacks with requirements and checks, completeness, the holds and the opinion shown, *what follows* from the graph-run contract with routes, backs, states, outcomes and doors, the presses offered with the recovery doors of §7.3, the supervisor's decision, the newer record where one is ready with its delta); pinned content for previews; every session's run by `QuestRuns.Of`; `SESSION_DIFF` kept for a session no run governs | Route tests per kind; a finished run's review opened after its trees are gone; an earlier part's session opening its run's review; a preview refused for a tree file that moved; a newer record announced with its delta; the generated service types and the web typecheck |
| **REVIEWUX1f** | The surface (web): the Review tab as the run's review opened by `review/<run>` (§5); the bar of what is decided and its presses in reach over a long review; *viewed* by path and content, the reading position kept; a newer revision announced, never swapped in; completeness said; a document rendered with `Markdown` over the pinned content; the head's *Next: Review*, its *Accept…* removed; one *Send back…* naming its part; Stop's asks per kind and stage; the return's choices with both versions; the supervisor's card on the bar; §5.6's sentences and §10's words removed, in both languages; the glossary's *accept*, *review* and *plan* | Component tests and a story per kind in both languages, seen failing first (a folder in place, a copy waiting on 3 choices after 12 returned, a plan partly applied, an answer with nothing changed, a chain of two parts, a review with a newer revision ready); a *viewed* mark kept on an unchanged file and cleared on a changed one; `i18n:check`, `names:check -- --strict` |
| **REVIEWUX1g** | The five states (driver, web, terminal): `SessionGroups` placing the five and the archived mark by §4.2, its `SessionGroup` words; a run's state (§4.4) and the session a run stands for taking it; the list's groups and second lines; the head's state and its reason facts, *N parts to land* and *landing* gone; the life flow; `daoris session list --group` with the five; *To review*'s tree half gone | The `SessionGroups` table tests with one case per row of §4.2's mapping (a park, a parked quest, a paused one, a cooling account, an awaiting reply, a helper's ask, a person's stop, an abandon, a steward's stop, a kept failure, a run waiting on a go-ahead behind a completed session); `headFacts` and list tests; the terminal's test; `i18n:check` |
| **REVIEWUX1h** | Waits and Notifications (driver, modules, web): `PersonWaits` `choose`; *What needs you* one row per run from the runs' waits with every wait, opening where its first is answered, a wait another row answers listed once, its three groups one list; *Done and failed*'s run outcomes keyed by run, node and attempt, written at the settle and again where lost, opening by run, a failure's recovery offered; the merged-then-failed release said as both | `PersonWaitReader` tests for each wait; the attention tests (a run waiting on an Accept and a go-ahead is one row with both; a held attempt listed once); notices tests (one per attempt, kept once, written after a restart, opened with no session, kept after a retry succeeds); a release failing after its merge retried without a second merge or Accept |
| **REVIEWUX1i** | The supervisor on every kind (driver, modules, web): the landing kind's facts the record's (§8 A); admission by the run's chain (`QuestRuns.Of`); the kept decision naming the record and revision, checked at release and at the pass, `stale` on a move; the card's sentence by kind; a return's choice offered to none; a `go-ahead` before `apply` holding the write after a supervisor's Accept | `LandingKind` and `LandingJudge` tests per kind; a later part's session deciding; a decision released `stale` after the record moved; a run with a go-ahead after the review held after the supervisor accepted; the card's test |
| **REVIEWUX1j** | The installed window, in both languages: on the install, one run of each kind reviewed and accepted (a branch merged, work in place in a checkout, a folder in place after a send-back, a folder in a copy with one file changed on both sides, the stand-in system applied and one stale, an answer with nothing changed and one sent back with a corrected answer, a chain of two parts, a pull request through a plugin watched to its merge), each seen with `tools/desktop.mjs`, the five states read in the list, the head and Notifications, and the owner's read | An evidence record of the sitting, and the areas rewritten in place |

## 15. The questions put to the read, answered

1. **A checkout rewritten between turns, or committed in during a turn.** The turn log keeps each interval (§2.3 point 1): a commit the
   end no longer holds is *no longer in the checkout*, the files are drawn at the newest end against the first start, and a commit made
   during a turn is *made while its turn ran*, never claimed as the session's.
2. **Where the baseline lives.** In the place's manifest store, as a named record its tidy keeps and its sweep counts (§2.3 point 2); a file
   beside the run's record would keep a manifest whose content the sweep deletes.
3. **The wait for the landing behind the node door** is one press: orchestration around the one acceptance, not a second machinery.
4. **The Accept that passes the unchecked hold** is one press keeping two words in one edit, each with its provenance (§6.5); D255 allows
   it, since its pass carries words and these are the person's acknowledgement of the handbacks it names.
5. **Widening *plan*** to a system's changes collides with Setup's nowhere the person reads both: each is what would change, read before
   anything is written, and each says what it is a plan of.
