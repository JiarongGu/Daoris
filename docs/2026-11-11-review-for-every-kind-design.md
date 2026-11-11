# The person's review for every kind of work, not only a branch to land (REVIEWUX1)

**Status:** proposed, for Codex's read and then the owner's yes. D273 is reserved for the decision written after the read. Written by
Claude Opus 5.5 on 2026-11-11 from main at `e1ad4167c1`. Read with `docs/areas/workflows-review-landing.md` (cited *workflows rule
n*), `docs/areas/desktop-app.md` (*desktop rule n*), `docs/areas/roles-and-supervision.md` (*roles rule n*), the place-work design
(`docs/2026-11-10-place-work-design.md`, *place §n*) and PROCSTEPS1 (`docs/2026-11-09-process-steps-design.md`, *steps §n*). Paths are
under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they start with `Modules/` (`src/Daoris.Desktop/Daoris.Desktop.Modules/`) or
`Web/` (`src/Daoris.Web/src/`). Nothing was built or run; every fact about today is read from the code at that commit, and the
installed window's sitting is row h's.

## 0. In short

- **The subject is the run's change set, not a branch.** `claimed`'s `work` becomes a change set with an identity per kind of work
  (a branch at its tip; commits made in a checkout; files changed in a folder or in its copy; a system's plan), and its revision is what
  the review binds, the person accepts, and the step after the review is fed. Every subject carries the work's handback. Work that
  changed nothing is still a subject: its handback alone, waiting for the person (§2).
- **One review surface: the Review tab is the run's review.** Top to bottom: what is decided, in one sentence of its kind; the
  handback; the changes, drawn by kind; what follows the Accept, with each step's state and outcome; the presses. Every session of a
  run opens the same review; the head says *Next: Review* and holds no Accept of its own (§3).
- **Accept, send back and stop mean one thing each, said per kind.** Accept lets the run go on to what its workflow puts after the
  review, or ends it; *Send back…* is the one way to return the work with words (the run view's *Ask for changes…* folds into it);
  a review is never skipped; *Stop…* ends the run and says what stays. The press is the run's node door with the revision the page
  drew, for every kind (§4).
- **The steps after the Accept are watched, not decided.** A pull request, its merge and a release show on the review's *What
  follows* line and in *Done and failed*; the person is asked only by a step that waits on them: a task, a go-ahead, a return's
  per-file choice, an attempt held unknown, and a failed step nothing goes on from (§5).
- **The supervisor decides as today, on every kind,** against the same revision, held for its window with *Overrule*, its card on
  the review. A return's per-file choice stays the person's (D265); a place that wants the person alone before a write into a system
  composes a `go-ahead`, which no supervisor answers (D233) (§6).
- **Notifications:** *What needs you* holds one row per run waiting on the person, worded by its kind; *Done and failed* holds the
  outcomes the person watches (§7).
- **What goes:** the review's branch subject and its reach on an empty branch, the head's *Accept…*, `LAND_SESSION_TREE`'s and
  `trees land`'s press half, the second send-back, the folder note shown for a system, the git words said of every kind, the apply's
  *newest plan*, and the rule that makes a graph with no `land` end only at its review (§8). Runs in flight keep their kept words (§9).
- **Rows a to h** (§11): the change set; the steps fed what was accepted; one press; the run's review read; the surface; waits and
  Notifications; the supervisor; the installed window.

## 1. What is asked, and what is true today

### 1.1 The owner's words

The *Your review* step (你的审阅) "doesn't fit the current process; we might need to redesign the entire approval UI/UX design and
logic, since this is not always creating a branch for commit." The owner's standing rules apply to every choice below: visibility is
the key; a security improvement never blocks development; a person who wants more control uses restricted mode or a workflow node.

### 1.2 The kinds of work, and what each asks the person

| Work | What exists to look at | What the person decides | What the Accept lets happen |
|---|---|---|---|
| A session's branch to land (git, worked apart) | commits on the session branch past the line; uncommitted rest | that it may land | the landing, then what follows it (push, pull request, watch, tidy; or integration) |
| Work in place in a git checkout | commits made in the checkout during the run's turns; the run's own uncommitted paths (the lease, D258) | that the work is right; it is already in place | the run ends; the lease is let go |
| A folder worked in place | the files the run changed, from its manifests | that the changes are right; they are already in the folder | the run ends |
| A folder worked in a copy | the copy's changed files against its base | that they may come back | `return`, with a per-file choice where both sides changed (D265) |
| A system | the plan: each resource before and after | that the plan may be applied | `apply`; a stale plan goes back to the work, which plans again |
| A measurement or a review that writes only a document | the handback, and the document where one was written | that the answer is good enough | as its place's kind; with nothing changed, the run ends |
| A chain of steps | the chain's whole change set, part by part | one Accept for the chain in this repository | as its kind |
| A pull request merged and a release, by a plugin | outcomes | nothing: they are watched | the run's end |

Three conditions cross every kind: a done whose quest carried no requirement waits *unchecked* before the review (D255); a supervisor
may decide in the person's place (D233, roles rule 49); an Accept may be pressed before the review begins (D256).

### 1.3 Where the review assumes a branch today

1. **The review binds the run's branch.** `ReviewNode.Subject` is `RunSubject.Branch` (`Workflows/Kinds/Review/ReviewNode.cs:20`), the
   newest session's branch the run's quests produced (`Workflows/Nodes/NodeGoverning.cs:257-262`); a `work` input is read as that
   branch too (`Workflows/Nodes/NodeEvaluations.cs:75`). A run in a folder, a system or a checkout has no session branch, so its review
   observes the run's own unit with no revision: an Accept accepts nothing in particular, and the work can move under it unseen. This is
   Codex's finding on PLACEWORK1e.
2. **The review is read as a landing.** It observes through `LandReading` (`ReviewNode.cs:36`), which reaches it wherever the session
   branch is held (`Workflows/Kinds/Land/LandReading.cs:34,45`), and a session that committed nothing has its branch held `empty`
   (`Workflows/Nodes/NothingToDeliver.cs`). So a measurement, or any work answered in its handback alone, passes the person's review with
   nobody reading it: APPROVE1's evidence found three such runs on the install.
3. **The window's Accept needs a tree of the session's own.** The review tab offers *Accept* only where `hasTree` and the plan read
   (`Web/work/review/DiffPane.tsx:164,176`); the head's *Accept…* and the waits' `accept` stand on the tree's offer
   (`Web/work/waits/waits.ts:194-209`); the press is `LAND_SESSION_TREE`, which needs a tree (`Modules/Bridge/Routes/TreesLandDesk.cs`).
   The run view offers a review only *Ask for changes…* (`ReviewNode.Acts`, `Web/workflow/NodeWord.tsx:16`). Work in place, a folder
   and a system therefore have no Accept in the window; the terminal's `daoris workflow accept <run> <node>` is the only one.
4. **Two ways to send work back.** The review tab's *Send back…* opens the quest composer (`DiffPane`'s `onSendBack`); the run view's
   *Ask for changes…* (要求修改…) keeps the review's `changes` word, told to the work's session as its next turn. D228 calls the second
   a defect.
5. **A folder's review is the newest turn's.** `PlaceChanges.Of` compares one record's start and end manifests
   (`Departments/Review/PlaceChanges.cs:19-28`), and a record's start manifest is one file, written again at each turn
   (`Departments/Manifest/ManifestStore.cs:29,44`). After a send-back, the review shows the fix and not the work it fixes.
6. **Work in place in a git checkout reads the record's range in the person's checkout.** `SESSION_DIFF` reads base to `HEAD` where the
   record's tree is the checkout (`Modules/Bridge/Routes/TreesRoutes.cs:52-69`), so a commit the person made between the run's turns is
   drawn as the session's, and the run's own uncommitted paths, which hold the place (D258), are not drawn at all, since a checkout's
   uncommitted work is not knowably the session's (`Workplace/SessionReviews.cs:137-138`). The turns' manifests already keep `HEAD` at
   each start and end and name the line's own paths (`Departments/Manifest/RunManifest.cs`, its `Own`).
7. **A system's plan is drawn as a folder's files.** `SystemChanges.Of` makes each operation a file row (`Departments/Systems/SystemChanges.cs`)
   answered as `source: place`, so the review says *This department is a folder* above a plan (`Web/work/review/PlaceNote.tsx`).
8. **The apply takes the newest plan, not the accepted one** (`Workflows/Kinds/Apply/ApplyDoor.cs:43`): a plan written after the Accept
   is applied unread. PLACEWORK1's row holds this.
9. **A `return` or an `apply` can never run in a saved workflow.** A graph with no `land` must end at its `review`
   (`Workflows/Domain/WorkflowNamed.cs:180-195`, `Workflows/WorkflowProcesses.cs:120`), and a run settles at its end node
   (`Workflows/Nodes/NodeRunKeeping.cs:88`), so a step after that review is past the run's end; a version ending at a `return` or an
   `apply` is unreadable. The doors are built and tested on their own (`ReturnDoorTests`, the apply's tests). The shipped *Work in
   place* holds no `apply`, so a system that follows it has its plan accepted and never applied.
10. **Only some person steps reach Sessions and *What needs you*.** `PersonWaitReader` lists an active `review`, `human` and `go-ahead`
    and an attempt held unknown (`Workflows/Nodes/PersonWaitReader.cs:37-42`). A `return` waiting on the person's per-file choice, and
    a failed step nothing goes on from, wait on the person and are listed nowhere.
11. **The words are git's.** *{{count}} commits beyond the line wait for you to accept them* (`workflow.run.said.work.land`), *Nothing
    to land: its work made no commits*, *This session committed nothing to its tree, so there is no diff to read*; the glossary's
    *accept* is *a session's work in its review, which lands it*.

## 2. The review's subject: the run's change set

### 2.1 The options

- **A. A branch for every kind.** Give a folder's, a checkout's and a system's run a synthetic branch unit, so `RunSubject.Branch`
  holds. It keeps the code's shape, and lies about git: every reader of a branch unit (the trees, the keeper, the tidy, the landing
  record) would meet a branch that is not one.
- **B. A subject of its own on the review alone.** The review binds a per-kind subject; what follows it reads what it reads today. The
  review is fixed and the apply still applies the newest plan, and the return the copy as it stands.
- **C. The run's work as a change set, fed to the review and to the step after it.** `claimed`'s `work` output (`WireValues.Work`),
  which today carries the branch's commit, carries a change set: its kind, the unit it is read on and its revision. The review binds
  it, the person's Accept keeps that revision, and the step the review leads to is fed the accepted revision, as PROCSTEPS1 already
  feeds `rebase` the `work` (steps §2.4).

**Recommended: C.** It is the one shape in which what was shown, what was accepted and what is carried out are the same thing, for
every kind, and it is PROCSTEPS1's wire already. B fixes the review and leaves its consequence wrong; A is a second meaning of a branch.

### 2.2 The change set, per kind

A change set is read by one reader per kind (`ChangeSets`, new, beside `Workflows/Nodes/RunInputs.cs`), chosen by the place's kind
and whether the work was set apart (`isolate`):

| Kind | Read on | Its revision | Its rows | Moved when |
|---|---|---|---|---|
| `branch` (git, apart) | the run's session branch, as today | the branch's tip | commits past the line, files with their diffs, uncommitted files apart (today's `SessionReviews`) | the tip moves |
| `checkout` (git, in place) | the run's own unit | the newest turn end's `HEAD` and a digest of the run's own uncommitted paths with their hashes | the commits made during the run's turns (from each turn's start `HEAD` to its end `HEAD`), a commit between turns marked *not this work's* as an unattributed path is; the own uncommitted files | a turn ends at another `HEAD` or with other own paths |
| `folder` (in place) | the run's own unit | a digest of each changed path with its hash, from the run's first start manifest to its newest end manifest | each file added, changed or removed, text with its diff (today's `PlaceChanges` rows), paths changed by none of its tools marked | a turn ends with another digest |
| `copy` (folder, apart) | the run's own unit | the same digest, from the copy's base to the copy as it stands | the copy's files; each one the folder also changed since the copy was made marked *changed in your folder too* | the copy changes |
| `plan` (system) | the run's own unit | the plan's record, number and content digest | each operation as a resource with its value before and after | a newer plan is kept |

Its rules:

1. **The run's, not a session's.** A folder's change set starts at the run's first turn's start manifest, kept once per run beside the
   run's record (`workflows/units/<key>.base`, new), never written again by a later turn; the newest end manifest of any of the run's
   records closes it. A send-back's fix is shown with the work it fixes.
2. **Empty is a change set.** A run whose work changed nothing reads an empty change set of its kind, with a revision of its own, and
   its review waits for the person with the handback alone (§2.3). The `empty` hold never reaches a review.
3. **A chain is one change set per repository.** A chain's parts in one repository are one run (workflows rule 15): its `branch` is
   the branch the chain lands on (workflows rule 36), its folder change set spans its records, and each row says which part made it.
   An ask's parts in several repositories are several runs, each with its change set, accepted at once by the entry's one Accept (§4.4).
4. **A conversation's run** reads its candidate (workflows rule 20) as a `branch` change set at the submitted tip; its press is the
   conversation's, as today.

### 2.3 The handback belongs to every subject

Every review carries the work's handbacks (driving rule 7: the note a session gave at done, read whole), each with its quest's
requirements and what checked them, beside the changes. It is the subject where nothing changed, and the explanation where something
did. A done checked against nothing (D255) is shown there as *unchecked: its quest carried no requirement, so nothing checked it*, the
hold's own wait before the review standing as it is (§4.3).

### 2.4 What reaches the review other than a word

The person's Accept or a supervisor's (workflows rule 18) reaches a review, and nothing else does, with one exception kept from
today: a `branch` change set whose landing is seen made, or whose commits the line holds, reaches it by the landing record
(`by: git`), since there is nothing left to decide. The review reads through its own unit (`ReviewReading`, new, beside
`ReviewNode`), and `LandReading` is the `land`'s alone.

### 2.5 The step after the review is fed what was accepted, and where a graph may end

- **Fed the accepted revision.** `ReviewedTip` (`Workflows/Nodes/ReviewedTip.cs`) becomes the accepted revision for every kind: the
  `land` (or PROCSTEPS1's `rebase`) binds the accepted tip, as today; the `apply` is fed the accepted plan, and a newer plan waits for a
  review of its own; the `return` brings the copy back at the accepted revision and, where the copy moved since, refuses `moved` and
  asks the review again.
- **Where a graph with no `land` ends.** The rule becomes: a graph holds one `land` and ends after it; or holds none and ends at its
  `review`, or at the one step its review feeds that carries the work out (`return` in a folder, `apply` in a system), which itself
  feeds nothing that lands. D264's note rejected ending at *any* kind that may end, which would end the work on the agent's done unread;
  a step the review feeds runs only once the work was read and accepted, so that rejection's reason does not reach it and it stands. The kit declares which kinds carry work out (`ICarriesOut`, new, on
  `ReturnNode` and `ApplyNode`, beside `IMakesLanding`).
- **Shipped graphs.** *Plan, review, apply* for a system (`claimed` → `work.requests` → `review` → `apply`, the apply's failure a way
  back to `claimed` that tells the work the stale resources, place §5.3) and *Work apart in a copy* for a folder (`isolate` `copy` → `claimed` → `work.requests`
  → `review` → `return`). The window's add chooses *Plan, review, apply* for a system in place of *Work in place*.

## 3. One review surface, and its words

### 3.1 Where it lives: the options

- **A. The session's Review tab, per session (today).** A run spans sessions: a chain's parts, a carry-on, a send-back's next turn,
  two sessions planning one system. Each would show a part of one decision.
- **B. The run view.** The Workflow tab draws the run's steps; it would grow a diff, a file list and a plan beside a chart, in a side
  bar at its floor width.
- **C. The Review tab as the run's review.** The tab every session's page already has shows the review of the run its work belongs
  to (`RunWaitSessions`), whichever of the run's sessions is open. A session no run governs (a helper, a reviewer, Ask Daoris, a
  conversation with no candidate) shows its own changes there, read-only, as today.

**Recommended: C.** The person already reads a session's work on its Review tab; the change is its scope, a run, and its presses, which
are the run's. The run view keeps the graph and every step's presses, and its review step opens the Review tab (*Open its review*,
which today exists only for a landing's step).

### 3.2 Its shape

Top to bottom, one column (desktop rule 27):

1. **What is decided**, one sentence of its kind (§3.4), with the revision's short form, since when it waits, and the run's title.
2. **The handbacks**: each part's note, its requirements and their checks, *unchecked* where D255 holds, and the second opinion's
   findings where one was asked.
3. **The changes**, drawn by kind (§3.3), in today's multibuffer with its *viewed* marks.
4. **What follows**: the steps the run's graph puts after the review, in order, each with its state, its outcome and its door (a pull
   request's address, a landed branch, a release's evidence). Before the Accept it says what the Accept starts; after it, it is the live
   line of what the work came to.
5. **The presses** (§4): *Accept* (采纳), *Send back…* (退回…), *Stop…* (停止…), and *Discard tree* where the kind keeps a tree apart.
   A supervisor's held decision stands here with *Overrule* (§6); a `return` waiting on the person's choice shows its per-file presses
   here, in the place of the presses that no longer apply.

The head's *Next* names the run's review while it waits (the `accept` wait of desktop rule 38, standing on the run's person wait
rather than on a tree's offer) and its door opens the Review tab; the head holds no *Accept…* of its own: an Accept is given where the
change set is drawn, so the press carries the revision the person saw.

### 3.3 How each subject is drawn

| Subject | Drawn as |
|---|---|
| Commits (`branch`, `checkout`) | the commits listed by part, then the files with their diffs, unified or side by side; a `checkout`'s commits between turns marked *not this work's* |
| Files (`folder`, `copy`) | the files with their diffs where text, a row with *Open* where not (today's `DiffFileRow` with `place`), unattributed paths marked, a copy's files the folder changed too marked |
| A plan | one row per resource: its operation (create, change, delete), the system's name for it, and its value before and after as a diff; the adapter's own rendering where it gives one (place §5.2) |
| A document | a Markdown file in any change set: rendered, with the diff one press away (today's preview, `Web/work/review/FilePreview.tsx`) |
| Nothing | the handbacks alone, under *It changed nothing* |

### 3.4 The words

The sentence that says what is decided, in both languages (glossary: *accept* 采纳, *send back* 退回, *line* 主线, *plan* 计划, *apply*
应用):

| Kind | English | 中文 |
|---|---|---|
| `branch` | Accept the {{count}} commits on `{{branch}}`. | 采纳 `{{branch}}` 上的 {{count}} 个提交。 |
| `checkout` | Accept the {{count}} commits it made in `{{repository}}`'s checkout: they are there already. | 采纳它在 `{{repository}}` 的检出里做的 {{count}} 个提交：它们已经在那里了。 |
| `folder` | Accept the {{count}} files it changed in this folder: they are there already. | 采纳它在这个文件夹里改动的 {{count}} 个文件：它们已经在文件夹里了。 |
| `copy` | Accept the {{count}} files it changed in its copy, and they are brought back into your folder. | 采纳它在副本里改动的 {{count}} 个文件，它们会被带回你的文件夹。 |
| `plan` | Accept its plan of {{count}} changes to `{{system}}`, and Daoris applies it. | 采纳它对 `{{system}}` 的 {{count}} 项变更计划，随后由 Daoris 应用。 |
| nothing | It changed nothing: read its answer, and accept it. | 它什么都没有改动：读完它的回答后采纳。 |

What *Stop…* leaves, said in its ask: the branch and its tree stay until discarded; the commits stay in the checkout; the changes stay
in the folder, and nothing undoes them; nothing is brought back, and the copy is set aside at the next tidy; nothing is applied to the
system. The run view's step says *Waits for your review:* and the same sentence, in place of `workflow.run.said.landing.accept` and
`workflow.run.said.work.land`. The step keeps its name, *Your review* (你的审阅): what changes is that its sentence says what it decides.

The glossary's *accept* becomes *the person's yes to the work under review, a run's change set (commits, files, a plan, or an answer),
which lets the run go on to what its workflow puts after the review*; *review*, *reading what a run's work changed and what it says it
did before accepting it or sending it back*; and *plan* widens to *what a place's Setup choices, or a session's work in a system, would
change, read before anything is written and carried out by Apply*, one word for one idea.

## 4. Accept, send back and skip for each kind, and the presses behind them

### 4.1 The press: the options

- **A. Keep `LAND_SESSION_TREE` for a branch and `WORKFLOW_NODE` for the rest.** Two doors for one word (D228).
- **B. The run's node door for every kind.** `WORKFLOW_NODE { run, node, act: accept, revision, answers }` keeps the word, as it
  already keeps every other word of the person's (workflows rule 18), and `daoris workflow accept <run> [<node>] [--at <revision>]`
  is its twin. What `LAND_SESSION_TREE`'s press adds moves behind it as the review kind's: the second opinion's answer token rides on
  the word (`answers`), the early Accept is kept by revision (§4.3), and for a run whose next step is a `land` the press waits for what
  the try came to, as `PerformedAnswers` answers it today.
- **C. A route of its own, `REVIEW_ACCEPT`.** A third door beside the two that already keep the word.

**Recommended: B.** The node door is already the one door for the person's words, Ask Daoris's press reaches it as a verb (D261), and
the revision is a field. A press whose revision is not the review's bound one is refused `moved`, with the new revision, and the page
draws the work again: nothing is accepted that was not drawn. The terminal with no `--at` accepts the bound revision and prints the
review's sentence first, as `daoris workflow review <run>` prints the whole review. `LAND_SESSION_TREE` keeps its plan read (`press:
false`), which is the `branch` kind's *what follows*; its press half goes, and `trees land` with it.

### 4.2 Per kind

| Kind | Accept | Send back… | Skip | Stop… / Discard |
|---|---|---|---|---|
| `branch` | the run goes on to its landing (or PROCSTEPS1's `rebase`) at the accepted tip; uncommitted work carries the session on, as today | the `changes` word with the person's words: the session's next turn; the review asks again at its next attempt | never | *Stop…* ends the run; *Discard tree* removes the tree, and the record stops the review |
| `checkout` | the run ends; its own uncommitted paths are the person's now, so the lease is let go (D258's *let others work there*), said before the press | as `branch` | never | *Stop…*: the commits stay where they are |
| `folder` | the run ends | as `branch`; the next turn adds to the run's change set | never | *Stop…*: the changes stay |
| `copy` | the run goes on to `return`, which brings the accepted revision back and asks the person per file where both sides changed | as `branch`, in the copy | never | *Stop…*: nothing comes back; the tidy sets the copy aside |
| `plan` | the run goes on to `apply` with the accepted plan | as `branch`: the session plans again | never | *Stop…*: nothing is applied |
| nothing changed | the run ends, or goes on through what follows, which has nothing to carry (`NothingToDeliver`) | as `branch` | never | *Stop…* |

A review is never skipped, of any kind: a skip would read as accepted (`ReviewNode.Row`'s `PersonSkips: false`). A place that wants no
review chooses a workflow without one (workflows rule 22). *Send back…* is the one way to return work with words, and it is the
`changes` word, whose route already exists; the composer's door from the review goes.

### 4.3 The early Accept, the unchecked done, the second opinion

- **Early Accept (D256)** is kept with the revision the person pressed on, where it now keeps a commit (`Workflows/Nodes/EarlyAccepts.cs`),
  and applied at the first pass that finds the review waiting at that revision; the review beginning at another drops it, as today.
  For a `branch` the revision is the commit, so nothing about it changes.
- **The unchecked done (D255)** stands as the `quest.holds` wait before the review. On the review, where the handback is drawn,
  *Accept…* asks once naming it (*Its quest carried no requirement, so nothing checked its done: you read its handback above.*) and
  keeps two words: the hold's pass, with the words *read on its review*, and the review's early Accept. One read, one press; the hold's
  rule that a pass carries words is kept, and no other hold is passed this way.
- **The second opinion** stays a gate before the review; its findings are drawn in the handbacks, and an Accept that answers a dispute
  carries the gate's token as `answers`, as the review tab's press does today.

### 4.4 An entry's or an ask's one Accept

`EntryAccept` stays the press for several runs at once (workflows rule 24): each part drawn with its kind's sentence and the revision
read, and the press keeps each run's `accept` at that revision through the same keep the node door uses. A part whose revision moved
since it was drawn is refused `moved`, alone, and the others go on.

## 5. After the Accept: the plugin steps' outcomes, and when the person is asked

The steps after the review (a `land`, a `push`, a plugin's `pull-request`, a `monitor`, a `tidy`, a release plugin's `find`, `release`
and `publish`, a `return`, an `apply`) are watched. Each shows on the review's *What follows* line and on the run view, with its
outcome in the page's words (`workflow.outcome.<kind>.<outcome>`) and its door: the pull request's address from its evidence, the
landed branch, the release's receipt. The session's head gives the run's newest outcome as one of its two facts (*Merged on the
host*, *Applied to `{{system}}`*).

The person is asked only by a step that waits on them, each a person wait (`PersonWaits`):

| Wait | Its step | Today | Recommended |
|---|---|---|---|
| `accept` | an active `review` | listed | listed, worded by kind |
| `task`, `approval` | an active `human` (the shipped pull-request delivery's task on a closed request) | listed | listed |
| `go-ahead` | an active `go-ahead` | listed | listed |
| `held` | an attempt held unknown | listed | listed |
| `choose` | a `return` waiting `both-changed` | not listed | listed, its paths counted |
| `failed` | a failed step no wire leads on from, in a run not ended | not listed | listed: *its step failed, and nothing in its workflow goes on from it: try it again, pass it over or stop it* |

A step that is only slow (a push active long past its bound, a request no answer reached) is said on the run view and the review's
*What follows* line, and is no person wait: the person can do nothing the step is not already doing. The `failed` wait is offered
only where the node table offers the person a press on it; a node whose kind offers none is said and listed nowhere.

The options weighed for a failed step: leaving it where it is today (the run sits, unseen, as APPROVE1's stuck pushes did); a toast
(desktop rule 44 keeps toasts for a place that is gone); and the person wait, which is how every other step that only the person moves
is shown. **Recommended: the person wait**, since visibility is the key.

## 6. The supervisor's place

Today a landing supervisor's `accept` of a `review`, or `approve` or `reject` of a `human` approval, is kept against the step as it
waits on that attempt, held for the role's window with *Overrule*, and released as the supervisor's word (roles rule 49); the
`LandingJudge` reads no plan.

- **A. As today, on every kind** (recommended). The supervisor's decision is judged against the review's change set and its revision:
  a supervisor's word on a revision that moved is `stale`, as a word on a lapsed attempt is. Its card (`SupervisorLandingCard`) is
  drawn on the review surface as well as the head, one press in two places as desktop rule 38 has it, with *Overrule*, its sentence the kind's (*The supervisor accepted this; it is applied at 14:10.*).
- **B. A system's plan the person's alone, by the kit.** A plan's Accept lets a write into a live system happen, which D233's note puts
  among the go-aheads. But today a supervisor may accept a system's review, so this would be a new block on development, and the
  person who wants it has the workflow node already: a `go-ahead` before the `apply`, which D233 makes no decision kind, so no
  supervisor ever answers it.
- **C. A grant per subject kind.** A supervisor's grants naming the kinds it may accept: a second way to say what the `go-ahead` says.

**Recommended: A**, with B's protection reached through a `go-ahead` in the system's workflow where a place wants it; *Plan, review,
apply* ships without one. A `return`'s per-file choice is the person's (D265), its kind offering a supervisor no act, as today. Ask
Daoris's press in full access is the person's word (D261) and is read afterwards by its second check (D272); the review's accepted line
names who gave the word (you, the supervisor with its decision, or a press of Ask Daoris's beside its line in *Notifications*).

## 7. Notifications

- ***What needs you*** holds one row per run waiting on the person (`review:<run>`, in place of today's row per session), titled by the
  work and said by its kind's sentence; it opens the run's review. An `accept` row is *Ready for you*; `choose`, `failed`, `task` and
  `held` hold the work and stand under *Holding work*. A row is answered, never dismissed (desktop rule 31).
- ***Done and failed*** holds what the person watches: landed or merged, a pull request opened and merged, a release made, a plan
  applied, files brought back, and each failure with its words; each line opens the run's review. None of them is a row of *What needs
  you*, and none asks anything.
- **Announcing** follows desktop rule 33: a review that waits announces as a finished session's review does today; an outcome
  announces only where the notify switch says.

## 8. What goes (D228)

| Today's way | Goes for | Its row |
|---|---|---|
| `ReviewNode.Subject` → `RunSubject.Branch`; `work` read as the branch (`NodeEvaluations.cs:75`) | the change set | a |
| The review read through `LandReading`, reached on an `empty` hold | `ReviewReading` | a |
| `PlaceChanges` per record's newest turn | the run's change set from its base | a |
| `ReviewedTip` (a commit) | the accepted revision | b |
| `ApplyDoor`'s newest plan | the accepted plan | b |
| A graph with no `land` ends at its review, alone (`WorkflowNamed.OneLandingProblem`, `WorkflowProcesses.Graphed`'s `inPlace`) | §2.5's end | b |
| `LAND_SESSION_TREE`'s press and `trees land`'s press | `WORKFLOW_NODE accept` and `daoris workflow accept` | c |
| The head's *Accept…* ask (`SessionHead`'s `lands`, `landing`, `onLand`) | *Next: Review* | e |
| The review tab's composer *Send back…*, and the run view's *Ask for changes…* (`workflow.run.node.changes*`) | one *Send back…*, the `changes` word | e |
| `PlaceNote`, `NothingToLand`, `work.review.empty.*`, `workflow.run.said.landing.accept`, `workflow.run.said.work.land` | §3.4's sentences | e |
| *What needs you*'s review row per session | per run | f |

An older record still read keeps its path only with the condition that ends it (§9).

## 9. Runs in flight

1. **A `branch` run's review** observes the session branch at its tip today, and its change set answers the same unit and revision:
   nothing rebinds, and a kept word stands.
2. **A run in place** (`checkout`, `folder`, `plan`) whose review observes its own unit with no revision: the home's migration step
   (`review-subjects`, beside `run-rebinds`, workflows rule 44) rebinds it once to its change set's present revision, keeping its attempt
   and any word waiting on the pass, so an Accept given before the update is not asked again. It ends when no home saved before the
   build can start, and row a names the row that deletes it.
3. **A folder run with no base kept**: its base is the earliest start manifest of its records still kept, else its newest turn's,
   and the review says so (*from its newest turn only: earlier turns' changes are not listed*).
4. **An early Accept** keeps a commit today; read as the revision, it is unchanged for a `branch`, and none exists for the other kinds,
   since their press did not exist.
5. **A system run**: no `apply` ever ran under a saved workflow (§1.3 point 9), so no accepted plan needs finding; a review active at
   the update binds the newest plan, and the Accept records it from then on.
6. **Rows of *What needs you*, the head and the words** are computed, never stored: nothing to convert. The removed catalogue keys go
   in the row that removes their screens.

## 10. Rejected

- **A synthetic branch for non-git work** (§2.1 A): a second meaning of a branch for every branch reader.
- **The subject on the review alone** (§2.1 B): what is carried out would still not be what was accepted.
- **The review per session** (§3.1 A): one decision shown in parts.
- **Two press doors, or a third** (§4.1 A, C).
- **An Accept in the head**: it accepts a revision the head never drew.
- **A skip of the review**: it reads as accepted; a workflow without a review is the way.
- **The return's per-file choices made at the Accept**: the return reads both sides fresh under the place's lock (D265), and a choice
  made before it would rest on hashes that may have moved.
- **An undo of work done in place**: Daoris has no revert of the person's folder or checkout, and the content store does not promise
  every blob (D265); *Stop…* says what stays.
- **A system's plan the person's alone by the kit, or a grant per kind** (§6 B, C).
- **Outcomes in *What needs you***: they ask nothing, so they would teach the person to dismiss what must be answered.
- **Ending a graph with no `land` at any kind that may end** (D264's note, kept): only the step the review feeds.

## 11. The rows, in build order

Each row takes its records with it: its area's sentences rewritten in place (workflows rules 8, 17, 18, 22, 23, 38; desktop rules 31,
32, 37, 38; roles rule 49), its words in both catalogues, its glossary rows. Rows a and b take two parts of PLACEWORK1's *1e's rest*
(a review binding a non-git subject; the apply bound to the plan its review accepted).

- **a. The change set and the review's subject** (driver). `ChangeSets`, a reader per kind (§2.2); the run's base manifest kept once
  per run; a `checkout`'s range from its turns' `HEAD`s, a commit between turns marked; `claimed`'s `work` a change set; the review bound
  to it, read by `ReviewReading` (§2.4), never reached on an `empty` hold, and stopped by the record where its tree was discarded; the
  `review-subjects` migration step (§9). *Proof:* per kind,
  a review binds its change set, an Accept keeps the revision, a moved revision lapses the binding and asks again, and a send-back's next
  turn is a new attempt at a new revision; PLACEWORK1e's finding as a test (a folder run's review binds the change set, not a branch);
  APPROVE1's case (work with no commits waits for the person); the migration on fixtures of each kind. Seen failing first.
- **b. The step after the review fed what was accepted** (driver). The accepted revision for every kind (§2.5); `apply` fed the
  accepted plan, a newer one waiting for its review; `return` at the accepted revision, refusing `moved`; the end rule and `ICarriesOut`;
  *Plan, review, apply* and *Work apart in a copy* shipped, the window's add choosing the first for a system; a graph with no `land`
  refusing the steps that read a landing (PLACEWORK1's `LandingRules` early return). *Proof:* the save accepts both shipped graphs and
  still refuses an end at a `work.requests`; the apply with two plans applies the accepted one; a return over a copy that moved asks
  again; a system department added in the window follows *Plan, review, apply*; a run of it applies the stand-in system's plan.
- **c. One press** (modules, driver, terminal). `WORKFLOW_NODE accept` with `revision` and `answers` (§4.1), refused `moved`; the
  early Accept by revision; the unchecked hold passed by the Accept that showed it (§4.3); `PerformedAnswers` behind the node door for a
  `land`; `EntryAccepts` through the same keep; `daoris workflow accept --at` and `daoris workflow review`; `LAND_SESSION_TREE`'s press
  and `trees land`'s press removed, their census rows with them; the route's view for Ask Daoris's card (D261). *Proof:* route and
  verb tests, the census, a stale revision refused with the new one, an early Accept kept and applied by revision, the existing
  `PerformedAnswers` and landing tests passing through the node door unchanged.
- **d. The run's review read** (modules). `RUN_REVIEW { run }`: the kind and its sentence's values, the revision, the rows per kind,
  the handbacks with requirements and checks, the holds and the opinion shown, *what follows* with states, outcomes and doors, the
  presses offered, the supervisor's decision; `SESSION_DIFF` kept for a session no run governs. *Proof:* route tests per kind; the
  generated service types and the web typecheck.
- **e. The surface** (web). The Review tab as the run's review (§3.2, §3.3); the head's *Next: Review*, its *Accept…* removed; one
  *Send back…*; the return's choices and the supervisor's card drawn on it; §3.4's sentences and §8's words removed, in both languages;
  the glossary's *accept*, *review* and *plan*. *Proof:* a component test and a story per kind in both languages (seen failing first),
  `i18n:check`, `names:check -- --strict`.
- **f. Waits and Notifications** (driver, web). `PersonWaits` `choose` and `failed` (§5); *What needs you* per run, grouped; *Done and
  failed*'s outcome lines; the run view's sentences by kind. *Proof:* `PersonWaitReader` tests for each wait, the attention and
  `DoneAndFailed` tests, a failed step's row offering only the presses the table offers.
- **g. The supervisor on every kind** (driver, web). `LandingJudge` judging a review against its change set's revision, `stale` on a
  moved one; the card's sentence by kind; a return's choice offered to none. *Proof:* `LandingJudge` tests per kind, the card's test.
- **h. The installed window, in both languages.** On the install, one run of each kind reviewed and accepted (a branch merged, work
  in place in a checkout, a folder in place, a folder in a copy with one file changed on both sides, the stand-in system applied, an
  answer with nothing changed, a chain of two parts, a pull request through a plugin watched to its merge), each seen with
  `tools/desktop.mjs`, and the owner's read.

## 12. For the read

1. Is a `checkout` change set from its turns' `HEAD`s sound where the person rewrites the branch between turns (a rebase, a reset),
   or commits in the checkout while a turn runs? The design says *the end `HEAD` no longer holds the start's commits* and draws the
   files at the end against the first start, and counts a commit made during a turn as the turn's.
2. Should the run's base manifest be kept beside the run's record, as §2.2 says, or in the place's manifest store with the run
   named, which its tidy (the newest 20 and every named record) would have to keep?
3. Is the wait for the landing's outcome behind the node door (§4.1) one press, or the second machinery D228 forbids?
4. Is an Accept that also passes the unchecked hold (§4.3) one act or two in one press, and does D255 allow it?
5. Does widening *plan* to a system's changes collide with Setup's *plan* anywhere the person reads both?
