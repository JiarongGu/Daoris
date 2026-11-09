# Generic process steps, landing only a composition of them (PROCSTEPS1)

**Proposed design, documents only** (2026-11-09). The owner's direction: a workflow's steps are each declared in one focus; a
step may do a lot, but today none says what it is focused on, and the landing has no rebase of its own. Then, twice: Daoris runs a
workflow system, so the steps are generic kinds any process composes, and *landing* is a composition at the workflow level, never
a step in code. Read with `docs/areas/workflows-review-landing.md`, the node kit design (`docs/2026-11-06-node-kit-design.md`,
every kind one unit under `Workflows/Kinds/<Kind>/`) and the land node's design (`docs/2026-11-01-land-node-design.md`). Paths are
under `src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root. The code was read at `2d28c2f168`.

## 1. What one `land` does today, and what 2026-11-09 showed

One performed kind, `land` (`Workflows/Kinds/Land/`), started once per node and tip on `LandingDoor`, hands everything to
`Workplace/LandPerformer.cs`, which calls the trees' landing (`LandingPress.LandingAsync`). In one attempt it:

1. judges the work as it stands: landed already since the node started, its tree gone or its slot taken, a live or newer session
   on the tree, the quest reopened, the tip moved since the bind, uncommitted paths (`LandPerformer.JudgeAsync`);
2. reads the gate (the run's part, the second opinion, the review) and the content proof;
3. plans from its options: `method` (`merge`, `branch`, `integrate`), `target`, `pattern`, `key`, `squash`, `message`;
4. judges the tree against the line (`LineHolds`: what the line already holds is left out) and replays the rest onto it in a tree
   of Daoris's own (`TreeLandSyncs.ReplayAsync`, `BranchReplays.ReplayKeptAsideAsync`), settling generated files, the declared
   backlog and row tables by their merge base, the accepted line rules and union's joins, then proves nothing changed beyond what
   the line brought (`BeyondAsync`);
5. writes the delivery ahead by phase (`CurrentDeliveries`, `LandAttempts`, `LandAttemptSettling`), judges the standing authority
   again, and merges `--no-ff` in the line's integration tree, the checkout only fast-forwarded while clean (`TreeMerges`), or makes
   or advances a branch, squashed where asked, recorded in `landings.json`;
6. moves the session's tree and branch to what landed (`TreeLandSyncs.MoveTreeAsync`);
7. on a conflict, refuses with the rebase words and says them to the session through the say door (`RebaseTelling`), or, under
   the review's word, carries it on up to `LandCarry.Bound` times (`LandCarry`); a refusal is tried again only once the tree's tip
   or status moves (`LandTries.ShouldTry`, an in-memory table per session).

**What 2026-11-09 showed, by the code's path.** The backlog's rows are one line each, and the merge base settles a stop only where
the two sides changed different lines (`BacklogMerge.ByBase`). A work that archived its row while the line rewrote that row, or
two rewrites of one row, is "a base line both sides changed": the replay aborts, the plan says "Its session can be asked: Rebase
your work onto `main`…" (`LandingSyncWords.Plan`), the performer tells the session, and the `land` waits for the tree to change.
No step says that it waits, on whom, or until when; nothing bounds the wait; the finished sessions sat for hours and the steward
merged three by hand.

## 2. The kinds

Each kind is one folder under `Workflows/Kinds/<Kind>/`, its declaration `<Kind>Node.cs` marked `[NodeKind(id, group, after…)]`,
as the node kit has every kind. Each declares its ports (inputs and outputs, with the outcomes each output is carried on), its
options, its outcomes, its words (`workflow.step.<kind>`, `workflow.outcome.<kind>.<outcome>` in every catalogue), its door off the
pass where it is performed, and its wait on the node's status, which the run view draws under the step and the session's card
reads (the integrate's card wait, `IntegrationCardWait`, made the kit's). Its retries and its bound are the generic node fields,
`retry.maxAttempts` and `timeoutSeconds`, with the kind's defaults. Each is named by what it does; none by landing.

### 2.1 What flows between them

Values travel on `data` wires, which the graph already has (`from: "node.port"`, mandatory inputs, outputs carried on named
outcomes, `UncarriedOutput`). Four value types:

| Value | What it is |
|---|---|
| `work` | A run's work: its branch, its bound tip, its tree and the session that is its newest executor. The run's entry outputs it (`claimed` for a quest's run, the candidate for a conversation's), which is today's `RunSubject.Branch` given a port |
| `commit` | A commit with the ref that keeps it: a step that makes a commit writes it on `refs/daoris/steps/<run>/<node>` through `RefTransaction`, so a later step, a restart and git's collection all find it |
| `ref` | A ref a step moved, with the commit it moved it to (a branch a `merge` made, which a `push` takes) |
| `problem` | What a step could not settle, structured: a conflict's stop (its commit and subject), each file with its hunks (the base's lines, the target's, the work's), what was already settled beside it; or the paths left uncommitted |

A node fed a value binds to it: a new `RunSubject.Input` replaces `Landing` and the `afterHanded` reading by place, so the nodes after
a `rebase` bind to the commit it made, and a moved input rebinds only what it feeds.

### 2.2 `rebase` (变基)

Replays a work's commits onto a target, in a tree of Daoris's own, and settles what the repository's records say may be settled.

| | |
|---|---|
| Inputs | `work` |
| Options | `onto`: `line` (the place's line, as today; default) or a branch name; `fetch`: fetch the target first, once (default off, as the automatic landing today; the person's press fetches) |
| Outputs | `commit`, on `ready`; `problem`, on `conflicted` and `uncommitted` |
| Outcomes | `ready`: the work replayed onto the target's tip, already standing on it, or wholly held by it (the commit is then the target's tip, and the `merge` after it is `held`). `conflicted`: a stop it may not settle. `uncommitted`: the work's tree holds paths no commit carries |
| State | Active while it replays, under the repository's git lock (and through `GitService` once GITLIB1 has built it); its record keeps what it left out (commits the target holds), what it settled per file and by which rule, and the scratch tree's place until it is removed in the same attempt |
| Words | "Rebased 3 commits onto `main`; settled `TASKS.md` by its rows (2 kept, 1 archived), `CHANGELOG.md` by union." "Could not settle `src/a.cs` (2 hunks) at `abc1234` “Add the reader”." |
| Retries, bound | `conflicted` and `uncommitted` are outcomes, not failures: the step runs again when its input or its target moves, at a `back` wire into it, or at the person's *Try again*. A git error fails it, retried by `retry`. Its bound is the replay's (`SyncBounds.Replay`, `GeneratedPaths.StopsAtMost` stops) |
| Never | The person's checkout, the work's branch and the session's tree: each is byte for byte as it was. It pushes nothing and moves no ref but its own step ref |

It waits, said, while a session is running in the work's tree (the commit may still move). It refuses nothing for a branch on its
remote: it rewrites no branch, so the "never rewrite a pushed branch" rule is the `tidy`'s, whose follow is the only act that would.

### 2.3 What the rebase may settle, and what it never does

**It may settle**, at each stop of the replay, a file in one of these and only these ways, each said in its words:

- **Union-merged records** (`.gitattributes`: `CHANGELOG.md`, `docs/task-archive.md`, `docs/FIX-LOG.md`): git merges them; the step
  sets apart the joins union glued (`UnionJoins`, today's) and keeps once a line union kept twice word for word, which
  `tools/doc-duplicates.mjs` would refuse on the line.
- **Rows and notes merged by their own drivers** (`docs/README.md merge=rows`, `docs/decisions/*.md merge=notes`): git runs the
  driver the repository's setup configured (`npm ci`); where it is not configured the file conflicts as text and is not settled.
- **Generated files** the repository marks generated: the target's copy, written again by the writers the person accepted, in a
  commit on top; a writer that fails refuses the stop (today's `GeneratedPaths`, `GeneratedWriters`). `daoris.lock` (`-merge`) is
  generated: regenerated by its writer where one is accepted, else not settled.
- **Files of an accepted line rule**: its pattern, then its writer (today's `LineRules`).
- **The declared backlog and row tables, by their rows' ids.** A row is a backlog line whose first bold token is an id
  (`- [ ] **PROCSTEPS1 — …`); lines that are no row (headings, prose) merge by the merge base, line by line, as today. For each id,
  three ways, base against the target (ours) and the work (theirs):
  - a row only one side added, removed or rewrote takes that side;
  - a row both sides changed to the same text, after line endings and trailing spaces, is once;
  - **a row the work moved to the archive** (the work removed it and its own commits add a `docs/task-archive.md` line naming the id)
    leaves the backlog, whatever the target did to it; a rewrite the target made since the base is said in the step's words with
    its text, so a person sees what left with it, and nothing waits;
  - **a row the target archived** (removed, and the target's archive names the id) stays out, the work's rewrite of it said the same way;
  - **a row both rewrote** is merged word by word against the base row (`BacklogMerge.ByBase` over the row's words): two rewrites
    that changed different words, such as a new head and a new tail, merge;
  - a row both rewrote where the words overlap, a row the work deleted with no archive line while the target rewrote it, and one id
    both added with different texts **truly differ**: the stop is `conflicted`, its problem naming the row's id with both texts.
  The twin rule holds: `tools/merge-branch.mjs`'s `mergeByBase` and the driver's `BacklogMerge` change together, row for row of
  `fixtures/backlog-merges.json`.

**It never**: settles a file outside those (code, configuration, any other document), so a stop with one such file is the
problem, every file the step settled beside it applied and named; takes one side with `-X ours` or `-X theirs`; skips a commit
(`rebase --skip`) except one git finds empty; settles a delete against an edit, a rename, a binary file or a submodule; keeps a
replay that changed a file the target did not bring and no rule settled (`BeyondAsync`'s proof, kept); chooses one text of a row
that truly differs, or writes both into the row; runs in the person's checkout.

### 2.4 `resolve` (解决冲突)

Hands a problem a step could not settle to whoever the workflow names, with the files and hunks named, bounded, its wait said.

| | |
|---|---|
| Inputs | `problem`, `work` |
| Options | `resolver`: `session` (the work's newest executor, its conversation going on; default), `fresh` (a new session in a tree of Daoris's own), `person` (a task for the person); `retry.maxAttempts` (asks, default 3); `timeoutSeconds` (each ask, default 1800) |
| Outputs | none: a conflict's resolution finishes the rebase attempt it came from (below); an uncommitted problem is resolved by the work's own new tip |
| Outcomes | `resolved`; failed where its bound is spent, its resolver is one the person stopped, declined or stood down, or the resolution changed a file the problem did not name |
| State | `active` with its wait: the resolver, the files, the try, since and until |
| Words | The ask names the target, the stop's commit, each file with its hunks and both texts of a row that truly differs, what the rebase already settled, and that only these files are to change: staged, no commit, no `rebase --continue`. The card and the run view: "Waits on its session to settle `src/a.cs` (2 hunks): try 1 of 3, until 15:02." Past the bound: "Waits on you: …" |
| Retries, bound | Each ask is a try. At the resolver's end of turn the step reads the tree: the named files staged with no markers continue the rebase; anything else asks again. Past `retry.maxAttempts` or `timeoutSeconds` it fails, and the composition routes `failed` (a person's task in the presets) |

**How a conflict is handed over.** The step prepares the resolver's tree with the rebase begun and stopped at the problem's commit,
every file the rebase settled applied and staged, only the named files left conflicted. The session's tree is used only as
`MoveTreeAsync` uses it today: clean, on its branch at the bound tip, no session running in it, not bound to another checkout; the
rebase there runs on a detached head, so the session's branch never moves and nothing bound to it rebinds (the review's Accept
included). Where that tree cannot be used, `fresh` and `person` get a tree of Daoris's own. Once the resolver has staged the files,
the step itself runs `rebase --continue`, settling each later stop by §2.3: a stop it settles goes on, a new one is the next ask.
At the end it proves that only the named files differ from what the rebase would have made, writes the result on the rebase
node's step ref as that attempt's finished commit, switches the tree back to its branch, and is `resolved`. A `back` wire on
`resolved` runs the rebase again, which reads its own finished attempt: where the target has not moved it is `ready` with that
commit; where it moved, it replays that commit's stretch onto the new tip, settling again.

An uncommitted problem asks the session to commit its paths by the repository's conventions or restore those it does not want
(today's `LandCarry` words), and is `resolved` when the tree is clean; the work's new tip rebinds the steps that read it, as today.

Asking needs no Accept, as the automatic landing's ask needs none today; a review's word, where one stands before, stays the
person's yes for the composition after it (today's carried Accept).

### 2.5 verify: the `check` kind, fed a commit

Verification is `check`, and no `verify` kind is added beside it. It gains a `commit` input and passes the same commit on as its
output on `reached`, so what moves is exactly what was verified. Its executor runs the gates its options name (`gates`, `reach`) in a
tree at that commit, as it does for a person's verification of a commit today; `failed: ask` and its fix asks stand.

### 2.6 `merge` (合并)

Moves a commit onto a target ref by a declared method, and nothing else.

| | |
|---|---|
| Inputs | `commit` |
| Options | `method`: `merge` (a merge commit, `--no-ff`, with the repository's hooks; default), `squash` (one commit of the work's change, its message from `message`), `fast-forward` (the ref moved to the commit, which must descend from it); `target`: `line` (default) or a pattern (`feature/{quest}-{slug}`, `{ticket}` from `key`), naming a branch; `message` |
| Outputs | `commit` (what the target now holds: the merge commit, the squash commit or the commit itself) and `ref` (the target), on `merged` and `held` |
| Outcomes | `merged`; `held`: the target held it already, nothing made; failed `behind` (a fast-forward the target's move made impossible), `exists` (the pattern names a branch that is not this work's chain's) |
| State | The delivery, keyed by unit, commit, method, target and generation, its phases written ahead (prepared, ref written, recorded) and settled by phase after a restart, before anything new starts (today's ladder, re-homed); a wait on the checkout's own uncommitted work (`root-dirty`, its status fingerprint, made again when it changes) |
| Words | "Merged 3 commits into `main`." "Waits for the repository's checkout to be clean: it has uncommitted work." |
| Retries, bound | An uncertain effect is never repeated (§3 of the land node's design stands); `behind` is a failure a composition routes `back` to its rebase |

It is the one step that writes a shared ref: the standing automatic authority is judged again just before its first effect, the
person's checkout is only fast-forwarded and only while clean and on the target, a branch target is recorded in `landings.json`,
an entry's combined Accept brings its members into the head's one delivery (`TogetherOperations`), and a press that waits on a run
waits on its `merge`. A graph may hold several, each its own delivery (a release's promotion after the line's merge). A `merge`
into a branch target accepts `fast-forward` (creating or advancing the branch) and `squash`.

### 2.7 `tidy` (整理), made generic

Cleans the work's tree and branches once a target holds the work.

| | |
|---|---|
| Inputs | `work`; `commit` (the work as the target holds it: the rebase's commit) |
| Options | `ends`: `follow` (the session's branch and tree moved to that commit, today's `MoveTreeAsync`; default) or `branch` (each chain part's branch ended with its recovery ref through the journal, today's `TidyDoor`) |
| Outcomes | `tidied`; `partly`, with why each part stays (a session running in its tree, uncommitted work, a branch on its remote, which is never rewritten). Nothing fails it |
| Also | Removes the run's step refs (`refs/daoris/steps/<run>/…`) once every part is ended or followed |

Its save rule `tidy-merged` becomes `tidy-held`: a way to it passes a step that puts the work on a target (a `merge`, an
`integrate`, a `monitor`'s `when merged`). D88's proof is read at its start, as today.

### 2.8 The kinds around them

`push` takes a `ref` input (a `merge` into a branch), replacing `land-tail`'s "after a `branch` land". `integrate` takes a `ref`
(the request's branch a `merge` made), keeping its `request` and `conflicts`. `line` takes a `commit` and is reached when the
line holds it. `monitor` and a plugin's `pull-request` are unchanged. The protected order reads `merge` where it read the landing
(`opinion` and `review` before every `merge`). `IMakesLanding` becomes `IMoves`, marked by `merge`; `LandingRules` goes, since a
reader of a move is fed by its wire and the save already refuses an unfed or uncarried input. The picker's `Landing` group becomes
`Git`: `rebase`, `resolve`, `merge`, `push`, `tidy`, `monitor` and the plugins' steps there.

## 3. Landing, a composition

`dev-line` (the install's copy of *Merge automatically*), with its ids kept so the chart's places and old evidence still resolve:

```json
{ "schemaVersion": 2, "entries": ["work"], "end": "tidy",
  "nodes": [
    { "id": "work", "kind": "claimed" }, { "id": "requests", "kind": "work.requests" },
    { "id": "holds", "kind": "quest.holds" }, { "id": "content", "kind": "content.holds" },
    { "id": "rebase", "kind": "rebase", "options": { "onto": "line" } },
    { "id": "resolve", "kind": "resolve", "options": { "resolver": "session" }, "retry": { "maxAttempts": 3 }, "timeoutSeconds": 1800 },
    { "id": "settle", "kind": "human" },
    { "id": "landing", "kind": "merge", "options": { "method": "merge", "target": "line" } },
    { "id": "line", "kind": "line" },
    { "id": "tidy", "kind": "tidy", "options": { "ends": "follow" } } ],
  "wires": [
    { "id": "a", "kind": "after", "from": "work", "to": "requests" }, { "id": "h", "kind": "after", "from": "requests", "to": "holds" },
    { "id": "i", "kind": "after", "from": "holds", "to": "content" }, { "id": "b", "kind": "after", "from": "content", "to": "rebase" },
    { "id": "r1", "kind": "when", "from": "rebase", "outcome": "ready", "to": "landing" },
    { "id": "r3", "kind": "when", "from": "rebase", "outcome": "conflicted", "to": "resolve", "group": { "id": "stuck", "mode": "anyOf" } },
    { "id": "r4", "kind": "when", "from": "rebase", "outcome": "uncommitted", "to": "resolve", "group": { "id": "stuck", "mode": "anyOf" } },
    { "id": "r5", "kind": "back", "from": "resolve", "on": "resolved", "to": "rebase" },
    { "id": "r6", "kind": "failed", "from": "resolve", "to": "settle" },
    { "id": "c", "kind": "after", "from": "landing", "to": "line" }, { "id": "t", "kind": "after", "from": "line", "to": "tidy" },
    { "id": "d1", "kind": "data", "from": "work.work", "to": "rebase.work" },
    { "id": "d2", "kind": "data", "from": "rebase.problem", "to": "resolve.problem" },
    { "id": "d3", "kind": "data", "from": "work.work", "to": "resolve.work" },
    { "id": "d4", "kind": "data", "from": "rebase.commit", "to": "landing.commit" },
    { "id": "d5", "kind": "data", "from": "landing.commit", "to": "line.commit" },
    { "id": "d6", "kind": "data", "from": "work.work", "to": "tidy.work" },
    { "id": "d7", "kind": "data", "from": "rebase.commit", "to": "tidy.commit" } ] }
```

Each input has one producer: a commit flows `rebase` → `landing` → `line`, and the work from `work` to every step that reads it.

The other presets, each the same kinds composed and set differently:

| Preset | After the holds |
|---|---|
| *Merge after you accept* | `review`, then dev-line's steps from `rebase` |
| *Merge automatically*, *Documents* | dev-line's steps (*Documents* without `work.requests`) |
| *A branch to push* | `review` → `rebase` → `merge` (`fast-forward`, target `feature/{quest}-{slug}`) → `tidy` (`follow`) |
| *A pull request* | `review` → `rebase` → `merge` (`fast-forward`, a pattern) → `push` → the plugin's `pull-request` → `monitor`; `when merged` → `tidy` (`branch`); `when closed` → a person's task |
| *Integrated*, *Integrated with no press* | (`review`) → `rebase` → `merge` (`fast-forward`, a pattern) → `integrate` → `line` → `tidy` (`follow`) |
| *Release* | `rebase` → `check` (fed the rebase's commit) → `review` → `merge` → `line` → `tidy` → the release plugin's steps |

Two workflows using one kind differ only by where it stands and how it is set: a chain's next step rebases `onto` the chain's
branch where dev-line rebases onto the line; a review flow puts `review` before `rebase`, and a release puts `check` between `rebase`
and `merge` so the commit verified is the commit moved; `merge` is a merge commit into the line in dev-line, a fast-forward onto a
named branch for a pull request, and a release's promotion of its line to a `release` branch is a second `merge` (`fast-forward`);
`resolve` asks the session in dev-line and a person where the workflow says `resolver: person`; `tidy` follows in dev-line and ends
the branches once a pull request merged.

**The editor.** The picker's `Git` group offers each kind alone. Adding a `rebase` wires its `work` from the run's entry and offers its
`resolve` tail (the `anyOf` ways, the `back` wire and the `failed` way to a person's task) in one change, as a `monitor` comes with its
`when` ways; adding a step after a commit's producer wires its input from the nearest one on the way before it, and the inspector names
every input and what feeds it. The chart draws each step with its own state; the run view says each step's words and its wait.
`WorkflowPresets` (`workflow new --from`) and `SetupPresets` (which match a place by its delivery) read the composition's `merge`:
its method and target name the delivery.

## 4. Where the land node's duties go

| Today (`land`) | Goes to |
|---|---|
| Started once per node and tip, its door off the pass | Each performed kind's own door, as the kit starts every one |
| Judging the work: a live session in the tree, the tip moved, the quest reopened, superseded | The binding (`work`'s newest executor, its bound tip, the run's reopening) and `rebase`'s wait while a session runs in the tree |
| The holds, the content proof | `quest.holds`, `content.holds` (their own kinds already); the `land`'s own reading of them for an unbound process goes with it |
| `LineHolds`, the replay aside, the settling, the proof beyond | `rebase`; the replay-and-settle unit moves into its folder and the integration's member merge (`IntegrationResolves`) and the tree's update (`TreeUpdates`) call it there |
| The rebase words, `RebaseTelling`, `LandCarry`'s conflict and uncommitted halves | `resolve` |
| `method`, `target`, `pattern`, `key`, `squash`, `message`; the delivery; the merge or the branch; `landings.json`; the authority before the first effect; `TogetherOperations`; `LandOutcomes` for the waiting press | `merge` |
| The integrate method's branch | `merge` (`fast-forward`, a pattern) before `integrate` |
| The tree following what landed; a pushed branch never rewritten | `tidy` (`follow`) |
| `LandTries` (the in-memory tries, `ShouldTry`), `LandTryNotes` | The node lifecycle's tries per binding and each kind's own notes in the conversation |
| `LandingRules`, `land-once`, `land-tail`, `IMakesLanding`, `RunSubject.Landing` | The data wires' own rules, `push`'s and `integrate`'s inputs, `IMoves`, `RunSubject.Input` |

## 5. A run under way at the upgrade

One way, D228: the old `land` path is removed in the cut-over's row, not kept beside the kinds.

- The release migration's workflow step (`Workflows/Migration/WorkflowMigration.cs`) converts every graph holding a `land` once,
  writing a new version of each saved workflow by the table of §3: `merge` keeps the `land`'s id, its options mapped (`method:
  merge` to `merge`; `branch` to `fast-forward`, or `squash` where `squash` was set, with its pattern as the target; `integrate` to
  `fast-forward` onto its pattern), and `rebase`, `resolve`, the person's task and `tidy` added around it.
- It rebinds each run bound to a version holding a `land` to its converted version, as `RunRebindsStep` rebinds runs today: a
  `land` reached maps to `rebase`, `merge` and `tidy` reached; one failed or never started maps to `rebase` not started, so
  the steps judge the work afresh; the steps before the landing keep their states and bindings.
- It runs only once the delivery store holds no unresolved delivery (the reconciler first, as the rollback window already waits),
  so no run is converted mid-effect. An older build reads a graph holding `rebase` as an unknown kind, held whole, and a rollback is
  refused while a step's delivery or step ref is open.
- With it, the `land` kind leaves the kit: `NodeKinds.Land`, `Workflows/Kinds/Land/`, `LandPerformer`, `LandTries`, `RebaseTelling`,
  the `land` half of `CurrentDeliveries`; `NodeKitTests` holds the kit with no `land`. The conversion's reading of `land` is the one
  remnant: it stays while a home saved before that release can start, and the last row deletes it (as G8-CONVERT1 does for `push`).

## 6. What this rejects

- **A `land` kind narrowed to the move.** Named by landing, it reads every move as a landing (a chain's advance, a release's
  promotion), which the owner's direction refuses; the move is `merge`, and the landing is what a preset composes.
- **A composite `land` that expands into the steps when saved or run.** The editor would show one node and run five, the run view
  could not say which waits, and the composition would be said two ways.
- **Rebasing in the session's tree.** It moves the branch the run is bound to, rebinding the holds and the review's Accept, and it
  writes in a tree a session may be running in; the rebase runs in a tree of Daoris's own, and only `resolve` uses the session's
  tree, detached, under `MoveTreeAsync`'s conditions.
- **A `verify` kind beside `check`.** Two ways to run the gates on a commit.
- **Inputs found by place** (today's `afterHanded`) **or unfed inputs defaulting to the run's work.** Each is a second way to name a
  value a wire names; the editor draws the wires so a person composes by placing steps.
- **For a row that truly differs: the driver choosing a side, writing both texts into the row, or landing the target's text while the
  work's waits on a record.** The first loses a rewrite unseen; the second writes the driver's words into authored content (D222) and
  a row with two heads; the third lands a backlog missing the work's own account of its row. The resolver, who wrote one side, merges it.
- **`resolve` handing on a commit of its own.** Every step after the rebase would then take its commit from one of two producers;
  finishing the rebase's attempt keeps one producer and lets the rebase settle anything the target brought meanwhile.
- **Keeping bound runs on the old `land` until they end** (the land node's design kept old versions so). A run waiting on a person
  can live for weeks, so the old performer would stay with no end; the conversion waits only for open deliveries.

## 7. Rows, in order

1. **PROCSTEPS1a, the records by their rows' ids** (driver, tools; waits on nothing): §2.3's backlog rules in `BacklogMerge` and
   its twin `mergeByBase`, the union duplicate kept once. It serves today's landing, the integration's member merge and the tree's
   update at once. Proof: `backlog-merges.json` rows run by both twins, one per rule; over real git, a replay whose `TASKS.md` row the
   work archived while the line rewrote it, and one whose row both rewrote in different words, lands with no session asked
   (`LandBacklogLandingTests`, failing first).
2. **1b, values on the wires** (driver): `work`, `commit`, `ref`, `problem`; `claimed`'s `work` output; `RunSubject.Input`; step refs;
   a `back` wire on an outcome. Proof: a moved input rebinds only what it feeds; a step ref survives a restart and `git gc`; an older
   build holds such a graph whole.
3. **1c, `rebase`** (driver): the kind, its door, the replay-and-settle unit moved into its folder with its other callers. Proof: one
   test per outcome over real git; 2026-11-09's sequence (three works archiving their rows while the line rewrote them, landing one
   after another) reached `ready` with no session asked; the person's checkout, the work's branch and its tree unchanged byte for
   byte; a stop mid-replay leaves no scratch tree.
4. **1d, `resolve`** (driver, modules): the hand-over, the continue with the settling, the proof, the card's wait. Proof: a code
   conflict asked once with its files and hunks and resolved; a second stop asked as try 2; a records stop after it settled with no
   ask; the bound spent failing to the person's task and said on the card; a resolution touching an unnamed file refused; a session
   the person stopped never asked.
5. **1e, `merge`** (driver): the methods and targets, the delivery and its ladder re-homed, the authority, `root-dirty`, the combined
   Accept. Proof: each method onto the line and a branch; a stop at each phase ends in one delivery; a press and a pass merge once;
   `behind` routed back to the rebase.
6. **1f, `tidy` and `check` fed** (driver): `follow` and `branch`, `tidy-held`; `check`'s `commit` in and out. Proof: a free tree
   follows and a running one says why it stays; a pushed branch never rewritten; a check on a rebased commit, its failure stopping the
   `merge`.
7. **1g, the cut-over** (driver, modules): the shipped graphs and `WorkflowPresets` recomposed, `SetupPresets` by the `merge`, the
   conversion and rebinding of §5, the `land` kind and its performer removed, the press waiting on the `merge`, the `Git` group.
   Proof: every shipped file judged; a home with `dev-line` runs at each `land` state converted and carried to its end; `NodeKitTests`
   with no `land`; the census of doors that land.
8. **1h, the editor, the chart and the run view** (web): the group, each kind's inspector and inputs, the `rebase` tail, the outcome
   words and the waits in English and Chinese. Proof: page tests, the names check, stories at 80, 39 and 25rem in both themes.
9. **1i, the installed window** (the steward): `dev-line` on the install through a day's rows: records conflicts settled with no
   session asked, a code conflict resolved within its bound, a restart mid-`merge`, a converted run carried.
10. **1j, the conversion removed** (driver): once no home saved before 1g's release can start.

## 8. Not decided here

- Recomposing the line's integration run (one run per line, fixed steps) from these kinds: its replay already calls the rebase's
  unit after 1c, and its `conflicts` could hand to a `resolve`; the run's own shape is a later design.
- Whether a failing `check`'s fix ask (`failed: ask`, `VerifyFixAsks`) becomes a `resolve` of a gate's problem.
- The look's own branch tidy (`BranchTidying`) beside a workflow's `tidy`: which one ends a session branch no workflow tidies.
