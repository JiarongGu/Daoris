# Generic process steps, landing only a composition of them (PROCSTEPS1)

**Proposed design, round 6, documents only** (2026-11-09). The owner's direction: a workflow's steps are each declared in one focus; a
step may do a lot, but today none says what it is focused on, and the landing has no rebase of its own. Then, twice: Daoris runs a
workflow system, so the steps are generic kinds any process composes, and *landing* is a composition at the workflow level, never
a step in code. Round 1 (the document at `0422e84b1b`), round 2 (at `fd03c57109`), round 3 (at `fd2c1421c6`) and round 4 (at
`b62716d462`) were each read by both makers (`docs/2026-11-09-process-steps-reads.md`); round 3 answered round 2's reads in place and
stated the integration against its code (§2.6, §2.10), and round 4 answered round 3's in §11 to §18. Round 5 (at `880e551fe8`)
answered round 4's reads in §19 to §26; it was read by the independent Opus reader alone. Round 6 answers that read in §27 to §33,
which change the sections they name and hold where they differ, rewrites the rows (§7) in place, and says per finding its answer and
its section in §34. §9 says, per round 2 and round 1 finding, the sequence it closes, the code the answer rests on, its
proof's row and the census or test that closes its class; §10 keeps the passages rounds 2 to 6 replaced. Read with
`docs/areas/workflows-review-landing.md`, the node
kit design (`docs/2026-11-06-node-kit-design.md`, every kind one unit under `Workflows/Kinds/<Kind>/`), the land node's design
(`docs/2026-11-01-land-node-design.md`), D215 (the integration), D228 and D232. Paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root. The code was read at `2d28c2f168` and `0422e84b1b`; the
integration, the acceptances, the readers and the deletes again at `56d8498990` for round 3; the ref acts, the act journal, the
together operations, the deliveries, the requests, a chain's advance, the run's keeping and binding, the node states and the
migration's steps at `9e7ba088df` for round 4; the start's bind and the repository's lock, the git gate and its start asks, the act
runner, the holders, the tree removals, the together press and settling, the requests' adoption and the carried Accepts at
`4bef320159` for round 5; the doors and their starts, the act runner's judgement, the repository's lock and its takers, the start's
hold, the pool's undo, the session locks, the git gate's reading of a command, the migration's binding writes and the line's
admission at `799e28c49e` for round 6.

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
   and the submission again under the tree's hold (`UnderHoldAsync`), and merges `--no-ff` in the line's integration tree, the
   checkout only fast-forwarded while clean (`TreeMerges`), or makes or advances a branch, squashed where asked, recorded in
   `landings.json`; a chain's later step advances the chain's branch, replaying only what neither it nor the line holds
   (`TreeBranches`);
6. moves the session's tree and branch to what landed (`TreeLandSyncs.MoveTreeAsync`), the move read as no new work
   (`LandTries.Landed`);
7. on a conflict, refuses with the rebase words and says them to the session through the say door (`RebaseTelling`), or, under
   the review's word, carries it on up to `LandCarry.Bound` times (`LandCarry`); a refusal is tried again only once the tree's tip
   or status moves (`LandTries.ShouldTry`, an in-memory table per session).

Two ways sit beside it. The line's integration tells a member's session to rebase through the same `RebaseTelling`
(`IntegrationReplays.HandBackEffectAsync`; `IntegrationDispositions`' *rebase it*), and the look's own tidy (`BranchTidying`) ends a
session branch the line holds whatever run governs it.

**What 2026-11-09 showed, by the code's path.** The backlog's rows are one line each, and the merge base settles a stop only where
the two sides changed different lines (`BacklogMerge.ByBase`). A work that archived its row while the line rewrote that row, or
two rewrites of one row, is "a base line both sides changed": the replay aborts, the plan says "Its session can be asked: Rebase
your work onto `main`…" (`LandingSyncWords.Plan`), the performer tells the session, and the `land` waits for the tree to change.
No step says that it waits, on whom, or until when; nothing bounds the wait; the finished sessions sat for hours and the steward
merged three by hand.

## 2. The kinds

Each kind is one folder under `Workflows/Kinds/<Kind>/`, its declaration `<Kind>Node.cs` marked `[NodeKind(id, group, after…)]`,
as the node kit has every kind. Each declares its ports (inputs and outputs, with the outcomes each output is carried on), its
options with their defaults, its outcomes, its words (`workflow.step.<kind>`, `workflow.outcome.<kind>.<outcome>` in every
catalogue, landing in the kind's own row, since the names check requires them), its door off the pass where it is performed, and
its wait on the node's status, which the run view draws under the step and the session's card reads (the integrate's card wait,
`IntegrationCardWait`, made the kit's). Its retries and its bound are the generic node fields, `retry.maxAttempts` and
`timeoutSeconds`, with the kind's defaults; §2.9 says, per kind, what runs it again and what bounds that. Each is named by what it
does; none by landing.

**Arriving until the cut-over.** Rows c to f build each kind marked arriving (`[NodeKind(…, Arriving = true)]`), and row g the
ports and outcomes h wires on kinds there already, marked the same: the palette does not offer them, and a person's save (an import
is a save) refuses a graph holding one with its words, so no saved graph holds a kind beside `land` before it goes; the rows' tests
bind their fixture graphs directly, as the kit's tests do. Row h deletes the mark and its refusal in one change, so nothing can
carry it after.

### 2.1 What flows between them

Values travel on `data` wires, which the graph already has (`from: "node.port"`, mandatory inputs, outputs carried on named
outcomes, `UncarriedOutput`). Four value types:

| Value | What it is |
|---|---|
| `work` | A run's work: its branch, its bound tip, its tree and the session that is its newest executor. The run's entry outputs it (`claimed` for a quest's run, the candidate for a conversation's), which is today's `RunSubject.Branch` given a port |
| `commit` | A commit, the commit it was made onto (`onto`: the target's tip a rebase replayed onto, which `merge` compares the target with) and the ref that keeps it: a step that makes a commit writes it on `refs/daoris/steps/<run>/<node>/<attempt>` through `RefTransaction`, so a later step, a restart and git's collection all find it |
| `ref` | A ref a step moved, the commit it moved it to and its producer (the node and attempt), which every reader of it is bound to (§2.8) |
| `problem` | What a step could not settle, structured, with its producer and its stop's identity (the stopped commit's patch id and its conflicted paths), by which `resolve` counts its tries. Two kinds: a `conflict` (the stop's commit and subject, its `onto`, each file with its hunks: the base's lines, the target's, the work's; what was already settled beside it) and `uncommitted` (the paths no commit carries, each with the blob the tree held) |

**Binding.** A node fed a value binds to it: a new `RunSubject.Input` replaces `Landing` and the `afterHanded` reading by place, so
the nodes after a `rebase` bind to the commit it made, and a moved input rebinds only what it feeds. These rules are the node
lifecycle's, so every kind keeps them:

- **A target's move is bounded.** A step with a target (`rebase`, `merge`) reads the target's tip, and a move of it rebinds the step
  only while nothing it feeds is active or reached. Once the `resolve`, `check` or `merge` it feeds has started, the line moving
  pulls nothing from under it; a `merge`'s `behind` (§2.6) is the one way a moved target reaches back, and its own `merge` moving the
  line never runs it again.
- **A run's own move is its binding's revision.** A step that moves a ref its own run is bound to (`tidy`'s follow, §2.7) writes the
  move on the run's record (the ref, from, to, the node and its attempt), and the binding reads that ref's revision as *from* while
  it stands at *to*, so nothing fed `work` lapses because the run moved it, the review's acceptance included. This replaces
  `LandTries.Landed`; a move on from *to* is new work, as ever.
- **A skipped producer** feeds no `data` wire (as `skipped` already satisfies none): a node whose mandatory input only a skipped
  producer feeds is skipped with it, its words naming the producer.
- **Step refs** are kept until the run has ended and none of its attempts or deliveries is unsettled, and only the run's end-keeping
  removes them (no `tidy` does, §2.7). Each attempt keeps its own, so an attempt still reading an older commit finds it after
  `git gc`.

### 2.2 `rebase` (变基)

Replays a work's commits onto a target, in a tree of Daoris's own, and settles what the repository's records say may be settled.

| | |
|---|---|
| Inputs | `work` |
| Options | `onto`: `target` (default: the target of the `merge` its commit feeds, directly or through a `check`, where that target is an existing branch of this run's chain; else the line), `line`, or a branch name; `fetch`: fetch the target first, once (default off, as the automatic landing today; the person's press fetches) |
| Outputs | `commit` (with its `onto`), on `ready`; `problem`, on `conflicted` and `uncommitted` |
| Outcomes | `ready`: the work replayed onto the target's tip, already standing on it, or wholly held by it (the commit is then the target's tip, and the `merge` after it is `held`). `conflicted`: a stop it may not settle. `uncommitted`: the work's tree holds paths no commit carries |
| Replays | Only what the target lacks: a commit the target holds by ancestry, by a squash its landing record names (`Squashed`, as `TreeBranches` reads a chain today), or by the together operation that brought it in (up to the member's bound tip, by the operation's inclusion record, §2.6), is left out, and a work grown from the target's tip replays nothing and is `ready` at its own tip; a line that only moved replays nothing onto a standing branch of the chain, which is never rewritten |
| Starts | Once its input binds and the work is still: no session runs a turn in the work's tree, and the work is bound to no open together operation (`DeliveryReach`'s `bound`). Until then it is `ready`, not active, said "Waits for its session's turn to end" or "Waits for `s-41`'s Accept, which brings this work in with it", which the turn or the operation bounds; no wait sits inside the step |
| Runs again | §2.9: its `work` moved; its target moved while nothing it feeds is active or reached; a `back` wire into it; the person's *Try again*. A git error fails it, retried by `retry` (default 3) |
| Bound | The replay's (`SyncBounds.Replay`, `GeneratedPaths.StopsAtMost` stops), each run one replay |
| State | Active while it replays, under the repository's git lock (and through `GitService` once GITLIB1 has built it); its record keeps what it left out (commits the target holds), what it settled per file and by which rule, and the scratch tree's place until it is removed in the same attempt |
| Words | "Rebased 3 commits onto `main`; settled `TASKS.md` by its rows (2 kept, 1 archived), `CHANGELOG.md` by union." "Could not settle `src/a.cs` (2 hunks) at `abc1234` “Add the reader”." |
| Never | The person's checkout, the work's branch and the session's tree: each is byte for byte as it was. It pushes nothing and moves no ref but its own step ref |

It refuses nothing for a branch on its remote: it rewrites no branch, so the "never rewrite a pushed branch" rule is the `tidy`'s,
whose follow is the only act that would.

### 2.3 What the rebase may settle, and what it never does

**It may settle**, at each stop of the replay, a file in one of these and only these ways, each said in its words:

- **Union-merged records** (`.gitattributes`: `CHANGELOG.md`, `docs/task-archive.md`, `docs/FIX-LOG.md`): git merges them; the step
  sets apart the joins union glued (`UnionJoins`) and keeps once a heading both sides added that union kept more often than either
  side held it (`UnionOnce`, in a commit on top), which `tools/doc-duplicates.mjs` would refuse on the line. Headings only: a line that
  is no heading (an entry's `---`, a shared bullet) can stand twice in two entries that each carry it, and cutting it would cut an entry.
- **Rows and notes merged by their own drivers** (`docs/README.md merge=rows`, `docs/decisions/*.md merge=notes`): git runs the
  driver the repository's setup configured (`npm ci`); where it is not configured the file conflicts as text and is not settled.
- **Generated files** the repository marks generated: the target's copy, written again by the writers the person accepted, in a
  commit on top; a writer that fails refuses the stop (today's `GeneratedPaths`, `GeneratedWriters`). `daoris.lock` (`-merge`) is
  generated: regenerated by its writer where one is accepted, else not settled.
- **Files of an accepted line rule**: its pattern, then its writer (today's `LineRules`).
- **The declared backlog, by its rows' ids**, as row a built it (`BacklogRows`, `BacklogIds`, `BacklogHeld`):
  - A row is a list line whose first bold token opens with an id (`- [ ] **PROCSTEPS1 — …`), keyed by it. Ids match exactly
    (`PROCSTEPS1` is not `PROCSTEPS1a`); a row's list names each id it covers (`X3a, 3d and 3m`, `X2a / 2b`, `X1a to X1d`, a short part
    on the stem whose digit run it shares), and a list that names no id for certain is refused, never guessed. Lines that are no row
    merge by the merge base, line by line, each row standing as its key; there two sides' inserts at one place conflict unless they
    are equal or both insert rows alone.
  - Rows that share any id, on any side, are one row. Base against the target and the work, each id stands as only one side changed
    whether it does, or both alike, and the rest of the line (all but its ids) takes the one side that changed it, or both alike,
    whitespace normalised. A compound row each side archived a part of (`X3a, 3d, 3l and 3m`, the work archiving 3a, the target 3l) is
    one row of the ids neither archived, its list written again (`X3d and 3m`); one a side split or joined is `conflicted`.
  - **A row one side archived** (removed) leaves only where the other side's copy is the base's, whitespace normalised, or only moved.
    Any other change there, words added or removed (removing "not" changes what a row asks), is `conflicted`, both texts in the problem:
    meaning is a resolver's to read (D215 point 6), never the driver's.
  - **A row both sides rewrote differently is `conflicted`**, however far apart the words: a merge of words writes wording neither side
    wrote. A row one side removed while the other changed it, and one id both added with different texts, are `conflicted`.
  - A side covering an id twice, or holding a row whose ids do not read, is `conflicted` before anything merges.
  - **The post-check**, on the made result whether git stopped or merged clean, applies the same rules to every id: a row the rules
    take out that git kept (the line moved a row the work archived) leaves in a commit on top, said; a result that lost a side's
    words, lost a row or covers an id twice is `conflicted`, naming the id. The archive decides nothing: on 2026-11-09, 16 of the 79
    open rows had an archive entry naming them (built parts, budget trims) while still open, so "kept and newly archived" would
    refuse ordinary landings.
  - **The declared row tables** keep the merge base line by line, two rows added at one place both kept: their rows are table lines
    with no id, and two branches each adding a census row is what such a table is for.
  - The twin rule holds: `tools/merge-branch/backlog-merge.mjs`'s `mergeByRows` and `rowsHeld` (wired into the merge tool by
    `backlog-held.mjs`) and the driver's `BacklogRows` and `BacklogHeld` change together, row for row of `fixtures/backlog-merges.json`.

**It never**: settles a file outside those (code, configuration, any other document), so a stop with one such file is the
problem, every file the step settled beside it applied and named; takes one side with `-X ours` or `-X theirs`; skips a commit
(`rebase --skip`) except one git finds empty; settles a delete against an edit, a rename, a binary file or a submodule; keeps a
replay that changed a file the target did not bring and no rule settled (`BeyondAsync`'s proof, kept); merges a row's words,
chooses one text of a row that truly differs, writes both into the row, or lets an archive win over any change the other side
made; runs in the person's checkout.

### 2.4 `resolve` (解决冲突)

Hands a problem a step could not settle to whoever the workflow names, with the files and hunks named, bounded, its wait said, and
the tree it hands over owned from the hand-off to its return.

| | |
|---|---|
| Inputs | `problem`, `work` |
| Options | `resolver`: `session` (the work's newest executor, its conversation going on; default), `fresh` (a new session in a tree of Daoris's own), `person` (a task for the person); `retry.maxAttempts` (asks per stop, default 3); `timeoutSeconds` (each ask, default 1800) |
| Outputs | none: a conflict's resolution finishes an attempt of the `rebase` its `back` wire returns to (below); an `uncommitted` problem is resolved by the work's own new tip |
| Outcomes | `resolved`; failed where a stop's bound is spent, its resolver is one the person stopped, declined or stood down, or the resolution changed a file the problem did not name |
| Tries | Counted per stop: each stop's identity (§2.1) keeps its count on the run's record, across rebinds and restarts, so a four-stop replay asks each stop up to its bound, and a stop met again after the rebase ran again goes on from its count. An ask that ended unresolved is made again only once the work or the tree moved, or at the person's *Try again* |
| Bound | Per stop, `retry.maxAttempts` asks of `timeoutSeconds` each; the stops are the replay's, at most `GeneratedPaths.StopsAtMost`; so the whole is bounded |
| State | `active` with its wait: the resolver, the files, the stop's try, since and until; the hand-off's journal entry while it holds the tree |
| Words | The ask names the target, the stop's commit, each file with its hunks and both texts of a row that truly differs, what the rebase already settled, and that only these files are to change: staged, no commit, no `rebase --continue`. The card and the run view: "Waits on its session to settle `src/a.cs` (2 hunks): try 1 of 3, until 15:02." Past the bound: "Waits on you: …" |
| Save rule `rebase-tail` | A `resolve` returns on `resolved` by one `back` wire to the `rebase` whose work it is fed, and its `failed` reaches a step with a way on (the presets' person's task, whose `done` goes `back` to that rebase); a `rebase`'s `conflicted` and `uncommitted` reach a `resolve` |

**The tree, owned through the hand-off.**

1. **The hand-off is journalled** before anything is written in a tree: an act of the journal, as the deletes are, naming the tree,
   its branch and tip, the rebase's stop, the resolver and the attempt. While it is open the tree is reserved: no landing, tidy,
   follow, start or other step takes it (the trees' holders read the entry, as `TidyHolders` reads a session's tree).
2. **The resolver's tree** is the session's tree only as `MoveTreeAsync` uses it today: clean, on its branch at the bound tip, no
   session running in it, bound to no other checkout; the rebase there runs on a detached head, so the session's branch never moves
   and nothing bound to it rebinds (the review's Accept included). Where that tree cannot be used, `fresh` and `person` get a tree of
   Daoris's own. It is prepared with the rebase begun at the problem's `onto` and stopped at its commit, every file the rebase
   settled applied and staged, only the named files left conflicted.
3. **The ask.** At the resolver's end of turn the step reads the tree, HEAD included: the named files resolved with no markers,
   staged or committed by the resolver (a resolver's own commit or `rebase --continue` is read as its edits, never refused), it
   runs `rebase --continue` itself where the rebase still stands at the stop, settling each later stop by §2.3: a stop it settles
   goes on, a new one is that stop's first ask.
4. **Every end restores the same way**: resolved, a timeout, the person's stop, a declining resolver, a lapse (the run stopped or
   superseded, its work moved by the person) and a restart that finds the entry open. In order: the resolver is stopped (its turn
   through the session's stop door, a fresh session's process ended) and awaited until nothing runs in the tree; unless resolved,
   what the resolver made is written on the node's edits ref (`refs/daoris/steps/<run>/<node>/edits`): HEAD's commits since the
   prepared stop, where it committed or continued, and the index and working files as a commit on top, so the abort drops
   nothing; the rebase is aborted; the tree goes back to its branch at its tip, as it was handed; the entry closes. Nothing is
   restored while the resolver may still write.
5. **Unresolved edits are kept**: the next ask of the same stop, a try or the person's task, starts from the edits ref's tree
   applied over the prepared stop, and the person's task names it, so a timeout or a restart loses nothing the resolver did.
6. **A restart** reads the open entries before anything starts, as the delivery ladder is settled by phase, and runs step 4 for each.

**Resolved.** The step proves that only the named files differ from what the rebase would have made, writes the result on the
returning rebase's step ref as the attempt the problem came from, finished, with its `onto`, and is `resolved`. The `back` wire runs
the rebase, which reads its own finished attempt: where the target has not moved it is `ready` with that commit; where it moved, it
replays that commit's stretch onto the new tip, settling again. No rebind can take the attempt from under the resolver, since the
rebase rebinds on a target's move only while nothing it feeds is active (§2.1).

An `uncommitted` problem asks the session to commit its paths by the repository's conventions or restore those it does not want
(today's `LandCarry` words), and is `resolved` when the tree is clean; the work's new tip rebinds the steps that read it, as today.

Asking needs no Accept, as the automatic landing's ask needs none today.

**The carried Accept, by rule.** A review that stands before the `rebase` is the person's yes for the work it read, and stays it
across the composition after it, as today's carried Accept (`LandCarry`) stays across a carry-on. A conflict's resolution moves
no ref the run is bound to (only the rebase's step ref), so the review stands across it. An `uncommitted` problem ends with the
session's own commit, which moves the work: the binding reads that move as the run's own (§2.1, written on the run's record with
the `resolve` node and its attempt) only where the new commits change exactly the problem's paths, to the blobs the problem
recorded, which is the tree the person accepted. A commit that changes another path, or those paths to anything else, is new work:
the review is asked again, said ("Asks you again: the session's commit changed `src/b.cs`, which you did not review"). The
carry-on's bound (`LandCarry.Bound`) gives way to the `resolve`'s tries per stop.

### 2.5 verify: the `check` kind, fed a commit

Verification is `check`, and no `verify` kind is added beside it. It gains a `commit` input and passes the same commit, with its
`onto`, on as its output on `reached`, so what moves is exactly what was verified. Its executor runs the gates its options name
(`gates`, `reach`) in a tree at that commit, as it does for a person's verification of a commit today; `failed: ask` and its fix
asks stand. While it is active or reached, its rebase does not rebind on the target's move (§2.1); a target that moved reaches back
only through the `merge`'s `behind`, which runs the rebase and so the check again.

### 2.6 `merge` (合并)

Moves a commit onto a target ref by a declared method, and nothing else.

| | |
|---|---|
| Inputs | `commit` |
| Options | `method`: `merge` (a merge commit, `--no-ff`, with the repository's hooks; default), `squash` (one commit of the work's change), `fast-forward` (the ref moved to the commit); `target`: `line` (default) or a pattern (`feature/{quest}-{slug}`, `{ticket}` from `key`), naming a branch; `message` (default: the repository's merge message, today's; a squash's the work's subject with its commits listed); `retry.maxAttempts` (default 3, before the first phase only); no `timeoutSeconds`, since an effect is settled by phase, never cut |
| Outputs | `commit` (what the target now holds: the merge commit, the squash commit or the commit itself) and `ref` (the target, with this node as producer), on `merged` and `held` |
| Outcomes | `merged`; `held`: the target held it already, nothing made; `behind`: the target's tip is not an ancestor of the input, for every method and target, judged before any effect and again where the target is written. Failed `exists` (the pattern names a branch that is not this work's chain's), and failed with its words where the repository's hook refuses the commit |
| Into the line | A request at the line's one door, the integration's promotion its one writer (below) |
| Into a branch | The squash or fast-forward commit made on a detached head, and the branch moved by the step in one `update-ref <target> <made> <expected>` in the delivery's `ref written` phase, `<expected>` the tip the judgement read; a branch moved since fails the update, nothing is written, and the step is `behind` |
| The judgement before the effect | Beside the standing authority, it reads again whether its input is current, as `UnderHoldAsync` does today: the work's tip still the one the input was made from, the quest's submission the same (a closure graph's, `QuestResults.Same`), the step ref still naming the input, the run neither stopped nor superseded; for the head of a together operation, the together re-read (below). For a branch it is read under the repository's git lock before the `update-ref`; for the line, at the request's admission and again at the promotion. One moved: nothing is written, the attempt is withdrawn (its preparation settled withdrawn), and the moved input rebinds it as any does |
| State | The delivery, keyed by unit, commit, method, target and generation, its phases written ahead (prepared, ref written, recorded) and settled by phase after a restart, before anything new starts (today's ladder and `LandAttemptSettling`, re-homed); for the line, the phases are its request's (admitted, promoted, settled), settled from the integration's record as `DeliveryReconciler.TakenAsync` reads it today. A wait on the checkout's own uncommitted work (`root-dirty`), the promotion's hold on a checkout that is not clean, said and waiting on the person |
| Words | "Merged 3 commits into `main`." "Waits in `main`'s integration: candidate `c-7` is promoting." "Behind `main` by 2 commits: rebasing again." "Waits for the repository's checkout to be clean: it has uncommitted work." |
| Retries, bound | An uncertain effect is never repeated (§3 of the land node's design stands); `behind` is an outcome every composition routes on: `back` to the rebase its input comes from (directly or through a `check`), or, for a `merge` fed another `merge`'s commit (a release's promotion), to a step with a way on. The save refuses a `merge` whose `behind` leads nowhere (`merge-behind`) |

Since the target's tip is an ancestor of the input for every method, a merge commit's and a squash's tree is the input's: what lands
is what was replayed and verified. The person's checkout is only fast-forwarded and only while clean and on the target, a branch
target is recorded in `landings.json`, and a press that waits on a run waits on the attempt of its `merge` it started, so a `held`
read later never answers it. A graph may hold several `merge`s, each its own delivery (a release's promotion after the line's
merge). A `merge` into a branch accepts `fast-forward` (creating or advancing the branch) and `squash`.

**Into the line, through its one writer.** The line has one writer, the integration's promotion (`IntegrationPromotes.EffectAsync`,
`IntegrationPromotes.cs:362-432`): under the promoting hold, after the checkout's open acts are reconciled, the fence on
`integration.by` and each requester's authority read again, it moves the line by one `update-ref refs/heads/<line> <tip> <base>`
verifying its old value, or by `merge --ff-only` in the clean checkout that has the line out, and refuses a line not at the
candidate's base. Today a merge rule's press is already a request at that door (`TreeMerges.cs:160-174` →
`IntegrationPresses.MergeAsync`), but its line run forms the candidate from whatever is queued, batching by the first request's
policy and epoch (`IntegrationReplays.cs:144-164`, `IntegrationPresses.cs:34`), and merges the press `--no-ff` onto whatever the
candidate holds, judging only whether the line holds it (`IntegrationReplays.cs:348-379, 429-455`): `check` passes C on T, the line
moves to T′ or another request forms the candidate with it, and C combined with T′ lands unverified, `behind` never seen. So the
`merge` writes no line ref itself; it asks the door, as the press does, with three changes:

- The request carries its input commit, the input's `onto` as the line's tip it expects, its method and message, and the asking
  attempt's invocation (§2.10).
- It is never batched: the form step forms a candidate of it alone, and joins it to no other's (`RunAsync`'s epoch loop stops
  before it).
- Its replay judges first that the candidate's base, the line's tip as the form read it, is an ancestor of the input; else the
  request closes `behind` and nothing is made. Then it makes the merge commit (`--no-ff`, the repository's hooks), the squash
  commit, or takes the input itself (fast-forward) in the line's integration tree, and the promotion's compare-and-set moves the line.

The `merge` waits, active, on its request and reads its close: settled, `merged`, its `commit` what the line now holds; contained by
ancestry, `held`; `behind`, `behind`; a hook's refusal, failed with its words. The requester's authority the promotion reads again
(`IntegrationAuthority.LapsedAsync`) is, for a run's request, the asking attempt still current: its node active on it, and the
`merge`'s judgement above. A member lapsed so is not held for an act, as `LapsedEnd` holds one today: nothing moves, its request is
dropped, and the attempt is withdrawn, the moved input rebinding it. A line moved between the judgement and the promotion is
read where it can be: a request not yet formed is formed on the moved tip and judged `behind` there; a candidate formed is formed
again by the promotion (`MovedWords`) and its member judged again on the new base.

**Together.** Today a member of an open operation is `bound` and starts no delivery; once the operation lands it is reached
`brought-into` from the operation's record, the inclusion record of every form (`DeliveryReach.cs:47-63`); and the head's combined
Accept re-reads under the hold each member's gate at its bound tip, the head at its checked tip, each member at its bound tip, and
accounts each member's change again at the head (`TogetherAccepts.cs:96-115, 167-186`, `TogetherChecks.AccountAsync`). In the
composition the head's work is the operation's: the operation joins its members into the head's branch before the head's run
reaches its `rebase`, so one rebase replays them and the head's `merge` is the one delivery.

- A member's `rebase` does not start while its work is bound to an open operation (§2.2), so no member replays or lands alone.
- Once the operation has landed, a member's `rebase` leaves out every commit up to its bound tip by the inclusion record (§2.2),
  whatever new ids the head's replay gave them, a path the head superseded and a line the head reverted included, so its `merge` is
  `held` and nothing of it lands twice; only the member's commits after its bound tip are new work, replayed as any.
- The head's `merge` adds the together re-read to its judgement: the head at its checked tip, each member at its bound tip with its
  gate, each member's change accounted again at the input. One moved: nothing is written, and the operation is checked again
  ("`s-42` moved since it was bound").

### 2.7 `tidy` (整理), made generic

Cleans the work's tree and branches once a target holds the work.

| | |
|---|---|
| Inputs | `work`; `commit` (the work as the target holds it: the rebase's commit) |
| Options | `ends`: `follow` (default) or `branch`; no retries or timeout: a part that stays is read again at the next pass |
| Outcomes | `tidied`; `partly`, with why each part stays (a session running in its tree, uncommitted work, a branch on its remote, which is never rewritten). Nothing fails it |
| `follow` | The session's branch and tree moved to that commit (today's `MoveTreeAsync`, its conditions), the move written on the run's record as the run's own (§2.1), so nothing fed `work` lapses, the review included |
| `branch` | Each chain part's branch ended with its recovery ref through the journal's delete (today's `TidyDoor`, `JournaledDeletes`) |
| Step refs | Not its own: they stay until the run has ended and settled (§2.1), so a later consumer of the rebase's commit, or an unsettled attempt that reads it, finds it after a squash, a branch tidy and `git gc` |

**One cleanup authority, held in the delete.** A branch an open run governs (its work's units, its chain's parts, a branch one of
its `merge`s made, its `integrate`'s request branch) is that run's. Every branch delete goes through the one journalled delete
(`JournaledDeletes.DeleteAsync`, `ForcedAsync`, and `ContentHolds.DeleteJudgedAsync` over them), so the act, its recovery ref and
its Undo are one, and its callers are, exactly:

| Caller | What it ends |
|---|---|
| `TidyDoor` | The run's own `tidy` (`branch`): its chain's parts |
| `IntegrateStart` (h) | The run's own `integrate`: its request branch, at `conflicted` and `failed` (§2.10); node requests only, since a `merge`'s request names the work's own branch |
| `TreeSweeps`, `LandedCleanups` | The look's sweep and clean-up, which `BranchTidying`'s look and the integration's tidy (`IntegrationTidies`) both reach: a branch the line holds |
| `LandedWithdrawals` | A landing withdrawn |
| `TreeTidies` (from `SessionBranchDiscard`) | A session branch the person discards |
| `TreeRemovals` | A session tree removed with its branch |
| `TreeStrays` | A stray's branch the look moves aside |
| `OnlyHereProof` | A branch held only here, by its forced delete |
| `TreePoolFinishes`, `PoolUndo` | A pool line's branch at its finish, a take's own branch at its undo |

The delete itself refuses a branch an open run governs, read from the open runs as `TidyHolders` reads them, unless its caller is
that run's own step, named by run, node and attempt: its `tidy` and its `integrate`. The refusal is said in the caller's words with
the run ("kept: run `r-12` governs it"), and that caller's next pass takes the branch once the run has ended, behind D88's proof as
today; so the look's tidy, the person's discard, a withdrawal, a removal, a stray and a pool finish never end what a run still reads.
`BranchDeleteCensusTests` names these callers and fails on a new one.

Its save rule `tidy-merged` becomes `tidy-held`: a way to it passes a step that puts the work on a target (a `merge`, an
`integrate`, a `monitor`'s `when merged`). D88's proof is read at its start, as today.

### 2.8 The kinds around them

`push` takes a `ref` input (a `merge` into a branch), replacing `land-tail`'s "after a `branch` land". `integrate` takes a `ref`
(the request's branch a `merge` made), keeping its `request` and `conflicts`, and ends `reached`, `conflicted` or failed by what
closed its request (§2.10). `line` takes a `commit` and is reached when the line holds it. `monitor` and a plugin's `pull-request` keep their behaviour, read through their inputs. The protected order reads
`merge` where it read the landing (`opinion` and `review` before every `merge`). `IMakesLanding` becomes `IMoves`, marked by
`merge`; `LandingRules` goes, since a reader of a move is fed by its wire and the save already refuses an unfed or uncarried input.
The picker's `Landing` group becomes `Git`: `rebase`, `resolve`, `merge`, `push`, `tidy`, `monitor` and the plugins' steps there.

**Every reader bound to its producer.** These find the landing by kind or by place today, each symbol with its readers:

| Symbol | Its readers in source |
|---|---|
| `IMakesLanding`, `NodeEvaluations.LandNodeOf` | `IAsksReads`, `LandNode`, `NodeEvaluations` |
| `RunSubject.Landing` | `GraphRuns`, `NodeEvaluations`, `WorkflowGraphRun`, and the `integrate`, `line`, `monitor`, `push` and `tidy` declarations |
| `RunSubjects.AfterHanded` | `GraphRuns`, `INodeKind`, `NodeEvaluations`, `NodeGoverning`, `NodeOwing`, `NodeTelling`, `WorkflowGraphRun` |
| `RunSubjects.Placed` | `CheckNode`, `PluginStep` |
| `NodeEvaluations.Landed` | `NodeKept`, `NodeTelling`, `PullRequestStep` |
| `NodeEvaluations.LandingOf` | `NodeIntegrating` |
| `NodeObserving.LandingOf` | `HoldReading`, `LandReading`, `NodeEvaluations`, `NodeKept`, `PullRequestReading`, `PullRequestStep`, `PushReading`, `TidyReading` |
| `INodeKind.ReadsTheLanding` | `BuiltInNode`, `LandingRules`, `PluginStep`, and the `integrate`, `line`, `monitor`, `push` and `tidy` declarations |

Each reads its input instead, through `NodeEvaluations.InputOf(node, port)`: the value, its producer node and its attempt, and,
for a `ref`, the landing record of the branch it names. `PluginStep.Subject` is the value on the plugin step's declared input
(`ref` for a `*/pull-request`; a plugin step that declares none is run-scoped, bound to the run as a `human` task is).
`PullRequestStep.Keep` writes the address on the landing record of the ref its own input names. `HoldReading`'s landing and its
`hold-late` rule read the `merge`s its work feeds (`IMoves`); `NodeKept`'s and `NodeTelling`'s landing, the `ref` of the step they
tell of. With two `merge`s, a pull request reads the branch its wire names.

**The integration's hand-back is read by its run's `integrate`** (§2.10): nothing is told to a session, and the run's own `rebase`
and `resolve` take a conflict once what the member met is on the line.

### 2.9 What runs each step again, and what bounds it

| Kind | Runs again when | Bounded by |
|---|---|---|
| `rebase` | its `work` moved; its target moved while nothing it feeds is active or reached; a `back` wire into it (`resolve`'s `resolved`, `merge`'s `behind`, `integrate`'s `conflicted`, the person's task's `done`); *Try again*; `retry` on a git error | the moves that cause it, none of which its own downstream can cause once started (`integrate`'s `conflicted` needs the line to hold what its member met, a move of the line); `retry` 3; the replay's bound per run |
| `resolve` | a new stop; an unresolved ask once the work or the tree moved; *Try again* | per stop `retry.maxAttempts` asks of `timeoutSeconds`; the stops at most `GeneratedPaths.StopsAtMost`; counts kept across rebinds and restarts |
| `check` | its commit moved (a new rebase attempt); `failed: ask`'s fix asks | its producer's moves; the fix asks' own bound |
| `merge` | its commit moved; `retry` before the first phase | `retry` 3; never once an effect may have happened, settled by phase |
| `integrate` | its `ref` moved; a replay's hand-back whose candidate ended with what the member met not on the line: asked again at the same tip (§2.10); *Try again* | once per ended candidate, at most `retry.maxAttempts` (3) times a tip, then the person; a wait on a decide or a hold bounded by that act alone, said |
| `tidy` | a part stayed | an ended part is never ended again (`ChainTidied`) |

### 2.10 The line's integration, read against its code

**What the code does today.** The `integrate` node asks the one door for its unit's request at a tip and adopts what the door
finds there, open or closed (`IntegrateStart.cs:199-240`): the store answers the newest request for that unit and tip whoever asked
it (`IntegrationStore.FindByKey`, `IntegrationStore.cs:162-165`), and a request closed `handed-back` or `dropped` fails the node
(`IntegrateStart.cs:312-317`). Three acts close a request `handed-back`: the replay's conflict of meaning, which also tells the
member's session to rebase through `RebaseTelling` (`IntegrationReplays.HandBackEffectAsync`, `IntegrationReplays.cs:483-537`); a
gates' verdict that hands back or splits (`IntegrationPromoteWords.HandedBack`, `:28-46`); and a decide (`IntegrationPromotes.cs:289-299`).
The settle writes a pending disposition for each commit made on a member's branch after its request froze that the line superseded
(`IntegrationSettles`), and *rebase it* tells its session through the say door (`IntegrationDispositions.cs:33-56`, from
`IntegrateRoutes.cs:141` and the page's `IntegrationTab`).

Round 2 read every hand-back as `conflicted`, back to the `rebase`. The sequence that fails: B, handed back for its conflict with
A in candidate K, and K then fails on A's gates; the line has not moved, so the rebase makes B's same commit, the `merge` makes the
same branch tip, and the `integrate` finds the closed request at that tip and is `conflicted` at once: an unbounded loop that never
puts B on the line. A gate's hand-back read as `conflicted` loops through the gates the same way.

**An attempt adopts only its own request.** A request records the invocation of the attempt that asked it
(`WorkPerformCall.Invocation`, kept on the attempt, so a restart of that attempt finds it). The door finds an attempt's request by
its invocation, or an open request for the unit at that tip (the store keeps one open request per branch); a closed request another
attempt asked is never adopted, so a new attempt, the person's *Try again* included, asks anew.

**Its ends, by what closed the request.** The replay's hand-back of a conflict of meaning closes its request `conflicted`, a close
word of its own beside `handed-back` (a verdict, a split's one member, a decide) and `dropped`, and the member records the commit it
was merged onto (`against`, the candidate's tip so far). The `integrate` stays active, said, until the candidate it was handed back
from has ended, which that candidate's own steps bound, then:

- **`conflicted`**: the request closed `conflicted`, and the line holds `against` by ancestry, past the work's `onto`. What the
  member met is on the line, so a rebase onto it meets it as its own. Every preset routes `conflicted` `back` to its `rebase`.
- **Held, its recovery defined**: the request closed `conflicted`, and the line does not hold `against`: what the member met never
  reached the line (its candidate failed, or formed again without it), and a rebase onto the unmoved line would make the same commit,
  so no way leads back through the `rebase`. The `integrate` holds, active, and its recovery is the same tip asked again: a new
  request, in the same attempt, once per ended candidate (keyed by its invocation, the tip and that candidate, so a restart asks
  once), at most `retry.maxAttempts` (default 3) times for a tip. Past that it waits on the person, said ("Waits on you: handed back 3
  times for work that never reached `main`"), and *Try again* asks once more.
- **failed**: closed `handed-back` or `dropped`, its words naming the gate, the split or the decide. A gate's failure is no conflict,
  and rebasing fixes nothing: presets route it to the person's task, whose `done` goes `back` to the `rebase`.
- **`reached`**: settled, as today.

At `conflicted` and failed, before its outcome is written, the `integrate` ends its request branch through the journalled delete with
its recovery ref, as the run's own step (§2.7), so the `rebase`'s `onto: target` reads the line and not a stale request branch; a
`merge`'s line request names the work's own branch, which no request ends.

**Waits with no time bound, said.** On its member held (`conflicts: hold`) and on its candidate held for a decide, the `integrate`
waits until the steward's or the owner's act, since a request standing in a candidate is the candidate's to close. Nothing times it out; the card and the run view say on whom and since when ("Waits on the steward's decide on candidate
`c-7`, held since 14:02"), and the line's attention lists it.

**Nothing told.** `conflicts: session` hands a run's request back with nothing said: its run's `rebase` and `resolve` take the
conflict. A request no open run asked (a recorded landing requested from the card, a records branch) is held, as `hold` holds.
`RebaseTelling`, `RebaseKeys`, `IntegrationReplayAsk.Say` and the hand-back's say go.

**No disposition for a run's branch.** In the composition a request's later commits reach its branch only through its run's own
`merge`, after the run's work moved, and the `integrate` then rebinds and asks for them; so a disposition written for them would be a
second way to bring them, and *rebase it* would replay them after the run's own request had. The settle writes no disposition for a
request an open run asked. A request no run asked keeps its dispositions, and their one answer is *superseded*, the commit kept on
its recovery ref; *rebase it* is retired with its answer word, its words, its say key (`superseded/<request>/<commit>`), the route's
verb and the page's button, since nothing is left to perform it.

## 3. Landing, a composition

`dev-line` (the install's copy of *Merge automatically*), with its ids kept so the chart's places and old evidence still resolve:

```json
{ "schemaVersion": 2, "entries": ["work"], "end": "tidy",
  "nodes": [
    { "id": "work", "kind": "claimed" }, { "id": "requests", "kind": "work.requests" },
    { "id": "holds", "kind": "quest.holds" }, { "id": "content", "kind": "content.holds" },
    { "id": "rebase", "kind": "rebase", "options": { "onto": "target" } },
    { "id": "resolve", "kind": "resolve", "options": { "resolver": "session" }, "retry": { "maxAttempts": 3 }, "timeoutSeconds": 1800 },
    { "id": "settle", "kind": "human" },
    { "id": "landing", "kind": "merge", "options": { "method": "merge", "target": "line" } },
    { "id": "line", "kind": "line" },
    { "id": "tidy", "kind": "tidy", "options": { "ends": "follow" } } ],
  "wires": [
    { "id": "a", "kind": "after", "from": "work", "to": "requests" }, { "id": "h", "kind": "after", "from": "requests", "to": "holds" },
    { "id": "i", "kind": "after", "from": "holds", "to": "content" }, { "id": "b", "kind": "after", "from": "content", "to": "rebase" },
    { "id": "r1", "kind": "when", "from": "rebase", "outcome": "ready", "to": "landing" },
    { "id": "r2", "kind": "when", "from": "rebase", "outcome": "conflicted", "to": "resolve", "group": { "id": "stuck", "mode": "anyOf" } },
    { "id": "r3", "kind": "when", "from": "rebase", "outcome": "uncommitted", "to": "resolve", "group": { "id": "stuck", "mode": "anyOf" } },
    { "id": "r4", "kind": "back", "from": "resolve", "on": "resolved", "to": "rebase" },
    { "id": "r5", "kind": "failed", "from": "resolve", "to": "settle" },
    { "id": "r6", "kind": "back", "from": "settle", "on": "done", "to": "rebase" },
    { "id": "m1", "kind": "when", "from": "landing", "outcome": "merged", "to": "line", "group": { "id": "on", "mode": "anyOf" } },
    { "id": "m2", "kind": "when", "from": "landing", "outcome": "held", "to": "line", "group": { "id": "on", "mode": "anyOf" } },
    { "id": "m3", "kind": "back", "from": "landing", "on": "behind", "to": "rebase" },
    { "id": "t", "kind": "after", "from": "line", "to": "tidy" },
    { "id": "d1", "kind": "data", "from": "work.work", "to": "rebase.work" },
    { "id": "d2", "kind": "data", "from": "rebase.problem", "to": "resolve.problem" },
    { "id": "d3", "kind": "data", "from": "work.work", "to": "resolve.work" },
    { "id": "d4", "kind": "data", "from": "rebase.commit", "to": "landing.commit" },
    { "id": "d5", "kind": "data", "from": "landing.commit", "to": "line.commit" },
    { "id": "d6", "kind": "data", "from": "work.work", "to": "tidy.work" },
    { "id": "d7", "kind": "data", "from": "rebase.commit", "to": "tidy.commit" } ] }
```

Each input has one producer: a commit flows `rebase` → `landing` → `line`, and the work from `work` to every step that reads it.
Every way out of a stuck step leads on: `resolve` back to `rebase`, its failure to the person's task, whose `done` returns to
`rebase`; a `merge` behind its target back to `rebase`.

The other presets, each the same kinds composed and set differently; every one carries the rebase's tail (`resolve`, the person's
task, their `back` ways) and routes its `merge`'s `behind` `back` to its `rebase`:

| Preset | After the holds |
|---|---|
| *Merge after you accept* | `review`, then dev-line's steps from `rebase` |
| *Merge automatically*, *Documents* | dev-line's steps (*Documents* without `work.requests`) |
| *A branch to push* | `review` → `rebase` → `merge` (`fast-forward`, target `feature/{quest}-{slug}`) → `tidy` (`follow`) |
| *A pull request* | `review` → `rebase` → `merge` (`fast-forward`, a pattern) → `push` (fed the merge's `ref`) → the plugin's `pull-request` (fed the push's `ref`) → `monitor`; `when merged` → `tidy` (`branch`); `when closed` → a person's task |
| *Integrated*, *Integrated with no press* | (`review`) → `rebase` → `merge` (`fast-forward`, a pattern) → `integrate` (fed the merge's `ref`) → `line` → `tidy` (`follow`); `integrate`'s `conflicted` `back` to `rebase`, its `failed` to the person's task, whose `done` returns to `rebase` |
| *Release* | `rebase` → `check` (fed the rebase's commit) → `review` → `merge` → `line` → `tidy` → the release plugin's steps; `behind` runs the rebase and the check again |

Two workflows using one kind differ only by where it stands and how it is set: a chain's next step rebases onto the chain's branch
(its `onto: target` reading the branch its `merge` advances) where dev-line rebases onto the line; a review flow puts `review`
before `rebase`, and a release puts `check` between `rebase` and `merge` so the commit verified is the commit moved; `merge` is a
merge commit into the line in dev-line, a fast-forward onto a named branch for a pull request, and a release's promotion of its
line to a `release` branch is a second `merge` (`fast-forward`); `resolve` asks the session in dev-line and a person where the
workflow says `resolver: person`; `tidy` follows in dev-line and ends the branches once a pull request merged.

**The editor.** The picker's `Git` group offers each kind alone. Adding a `rebase` wires its `work` from the run's entry and offers its
`resolve` tail (the `anyOf` ways, the `back` wires and the `failed` way to a person's task) in one change, as a `monitor` comes with its
`when` ways; adding a `merge` adds its `behind` way back to the rebase that feeds it; adding a step after a commit's producer wires its
input from the nearest one on the way before it, and the inspector names every input and what feeds it. The chart draws each step
with its own state; the run view says each step's words and its wait. `WorkflowPresets` (`workflow new --from`) and `SetupPresets`
(which match a place by its delivery) read the composition's `merge`: its method and target name the delivery.

## 4. Where the land node's duties go

| Today (`land`) | Goes to |
|---|---|
| Started once per node and tip, its door off the pass | Each performed kind's own door, as the kit starts every one |
| Judging the work: a live session in the tree, the tip moved, the quest reopened, superseded | The binding (`work`'s newest executor, its bound tip, the run's reopening), `rebase`'s start (no turn running in the tree), and `merge`'s judgement before the effect |
| `UnderHoldAsync`, the judgement under the hold | `merge`'s judgement before the effect, beside the authority |
| The holds, the content proof | `quest.holds`, `content.holds` (their own kinds already); the `land`'s own reading of them for an unbound process goes with it |
| `LineHolds`, the replay aside, the settling, the proof beyond; a chain's advance (`TreeBranches`) | `rebase` (`onto: target`, replaying only what the target lacks); the replay-and-settle unit moves into its folder and the integration's member merge (`IntegrationResolves`) and the tree's update (`TreeUpdates`) call it there |
| The rebase words, `RebaseTelling` (the landing's and the integration's), `LandCarry` (its conflict and uncommitted halves, its `Skips` of a session the person stopped) | `resolve`; the integration's hand-back read by its run's `integrate` (§2.10), nothing told |
| `LandPerformer.ReadTreeAsync` and `LandCarry.CommitWords`, which `TurnCarries` also reads for a done quest's dirty tree | A work tree's read of its own (`Workplace/WorkTreeReads.cs`: tip, status, paths), read by `TurnCarries`, `rebase`'s start and the `uncommitted` problem; the commit words in `resolve`'s words, which `TurnCarries` says |
| `method`, `target`, `pattern`, `key`, `squash`, `message`; the delivery; the merge or the branch; `landings.json`; the authority before the first effect; `LandOutcomes` for the waiting press | `merge`: into a branch by its own compare-and-set, into the line by a request at the one door (§2.6) |
| `TogetherOperations`: a member `bound`, then `brought-into`; the combined Accept's re-read | The head's `merge` and its together re-read; a member's `rebase` waiting while bound and leaving its work out by the inclusion record (§2.2, §2.6) |
| A superseded commit's dispositions and *rebase it* | None for a request an open run asked; *superseded* alone for one no run asked; *rebase it* retired (§2.10) |
| The integrate method's branch | `merge` (`fast-forward`, a pattern) before `integrate` |
| The tree following what landed; `LandTries.Landed`; a pushed branch never rewritten | `tidy` (`follow`), its move the run's own (§2.1) |
| `LandTries` (the in-memory tries, `ShouldTry`), `LandTryNotes` | The node lifecycle's tries per binding, `resolve`'s tries per stop, and each kind's own notes in the conversation |
| `LandingRules`, `land-once`, `land-tail`, `IMakesLanding`, `RunSubject.Landing`, `afterHanded` | The data wires' own rules, `push`'s and `integrate`'s inputs, `IMoves`, `RunSubject.Input` and `NodeEvaluations.InputOf` |
| (beside it) `BranchTidying`, the look's tidy, and every other caller of the journalled delete | Stay, the delete refusing them an open run's branch (§2.7) |

## 5. A run under way at the upgrade

One way, D228, and no compatibility, D232: the `land` path is removed in the cut-over's row, not kept beside the kinds, and the home
is converted once, by the migration of the release that ships h (D232 point 2: at the first start, before anything reads the home,
unpressed, a copy of every file it changes kept in the salvage folder, each step recorded as it finishes). `LandConversionStep`
(`Transitional/Migration/`) is a step of that migration. It reads nothing another step writes and rebinds every run it converts
itself, so it stands wherever that release's migration orders it, after any step that rewrites the workflow files: inside
`nocompat1` where h ships in its release, else in the migration that replaces `nocompat1`'s steps and name. Each conversion is said
in the start's report. No older build reads the converted home and none is guarded: no floor, no older build's refusal, no rollback's
(D232 points 1 and 3).

**The graphs, per source version.** Every version of a workflow that holds a `land` and is its newest, or is bound by an open run, is
converted, each from its own graph and its own frozen options, so a run bound to v1's merge delivery keeps a merge where v2 would
make a branch. The conversions are appended as the save appends a version (`WorkflowOperations.PlanGraph`), in source order with the
newest's last, so it stays the version a new run binds; the conversion of a graph is a function of that graph alone, so a restart
finds an appended conversion by its digest and appends none twice. By the table of §3: `merge` keeps the `land`'s id, its options
mapped (`method: merge` to `merge`; `branch` to `fast-forward`, or `squash` where `squash` was set, with its pattern as the target;
`integrate` to `fast-forward` onto its pattern); `rebase`, `resolve`, the person's task and their ways are added around it, and a
`tidy` (`follow`) where no `tidy` follows the `land`. Each added node's id is its kind's name where the graph holds none, else that
name with the smallest free numeral (`tidy-2`), so no id is reused (`pull-request.json` holds a `tidy` already).

- **The bounds.** A graph holds at most 24 authored nodes (`NodeGraphs.MaxNodes`, from `WorkflowKindTable.MaxSteps`) and 96 wires
  (`MaxWires`). h raises both, for every graph, by the most a `land`'s composition adds (four nodes, and the tail's, the `behind`
  way's and the data wires), named as constants beside them, so a graph saved inside the old bounds converts inside the new: one
  bound, no exception for a converted graph.
- **A conversion the save refuses** (a custom graph whose wiring around its `land` no composition meets) leaves that version as it
  was, said in the start's report and on the workflow, and recovers each open run bound to it rather than holding it whole: its
  open deliveries are settled by phase first, its record is ended `superseded` and kept as history, and its work is bound afresh to
  the place's choice as a first start binds it (`RunRebindsStep`'s rule, made this step's for a run with a record). The new run
  judges afresh: its `rebase` finds work the line holds, its `merge` is `held`, nothing moves twice, and an Accept it had is asked
  again, said with why. Where the place's choice binds nothing, the run is left held, said by its own problem, as today.

**The runs.** Each open run bound to a converted source version has every unit's record converted, then the run's record, then its
binding to that version's conversion. A run that has ended is finished history and is not converted (its view draws the `land` by its
raw name, as any unknown kind).

**Identities, mapped old to new, transitively.** An acceptance's id hashes its graph digest, its inputs (each port's upstream
acceptance) and its prerequisite receipts (`NodeAcceptances.cs:24-41, 78-100`); an outcome receipt hashes an acceptance id and an
outcome (`WireReceipts.OutcomeReceipt`); a firing key hashes the target's graph digest, the run, the wire and the receipt
(`WireEvaluation.FiringKey`); a status's scope names the graph digest (`NodeScope`); and a binding lapses on a graph digest, a lost
receipt or a lost input acceptance (`WireLapsing.cs:58-61`). So nothing is kept "as it was": change one upstream acceptance and
every review resting on it lapses. The conversion walks the converted graph in its order and, for each node, makes its acceptances
under the new digest from its upstream's already converted ones: its inputs as the new data wires feed them (each port's producer
acceptance, the value's producer node and attempt), its prerequisite receipts as the evaluator computes them from the wire that now
feeds it (an `after` wire's acceptance, a `when` wire's outcome receipt). It records old to new for every acceptance id and outcome
receipt, `land`'s mapping to `merge`'s, and every member that names one takes the map: each status's binding, acceptance and scope,
each attempt's binding, every kept result and history entry, and the fired keys (each kept wire's computed again with the new digest
and receipt, a removed wire's dropped, each new way the mapped states took written fired, so the evaluator fires none again). What
another party keeps is kept byte for byte: each attempt's invocation and idempotency key (`WorkPerformer`'s random `i-` and `k-`,
which a plugin answers by), an integration request's key and its asking invocation, a plugin's resources. A node whose converted
receipt cannot be computed is judged afresh, never kept on a guess.

| What the records hold | Converted to |
|---|---|
| The binding | Its source version's conversion and that digest |
| Every node declared the same in both versions (id, kind, options, node version) | Its acceptances re-identified by the map; their subject, revision, outputs, outcome and evidence as they were |
| `land` reached | `rebase` reached, its `commit` the commit the landing moved (the delivery's commit for a merge, `landings.json`'s tip for a branch, its `Squashed` for a squash); `merge` reached, outcome `merged`, its `commit` (what the target holds) and `ref` (the target, its producer `merge` at the land's attempt) from the landing record and the delivery's settled row; an added `tidy` (`follow`) reached, the land's own follow written as the run's (§2.1). Where the records do not name these outputs, `rebase` and `merge` are not started and judged afresh: the rebase finds the work held, the `merge` is `held`, nothing moves twice |
| `land` active | `rebase` and `merge` not started; the land's open attempts carried onto `merge` (the same id and number, `Step` `merge` at its version, invocation and key kept) as unreconciled attempts, which the merge's settling (`LandAttemptSettling`, re-homed) settles by their deliveries' phases before it starts anew, so an open delivery is settled once and never repeated |
| `land` ready or blocked | `rebase` in that state, `merge` blocked |
| `land` failed, rejected or unknown | `rebase` not started, judged afresh; the failure kept in the history |
| `land` skipped | `rebase` and `merge` skipped, and their readers with them (§2.1) |
| `resolve`, the person's task | Not started (off the way taken) |
| The landing's readers (`line`, `tidy`, `push`, `monitor`, `integrate`, a plugin's `pull-request`) | Their new input fed by the `merge`'s `ref` or `commit`, re-identified by the map; an attempt in flight keeps its invocation, so a push's or a pull request's attempt is answered as its own and nothing pushes twice |
| An `integrate` active on a request closed by a replay's conflict | That request read as closed `conflicted` (§2.10), its `against` the candidate's tip before that member, from the candidate's record |
| Wire firings (`Fired`) | By the map, as above |
| Pending words | A stop of `land` onto `rebase`; a skip onto each node it became; every other node's as it was |
| Repairs (a review Accept waiting on a carried-on session) | Dropped with the `land`, the review's acceptance kept: the new `rebase` finds the uncommitted paths and `resolve` asks, its try 1, said |
| History, observations | Kept, the `merge`'s history gaining "converted from `land`" |
| The delivery store's rows of a `land` (intent's method, target and plugin; settlements; attempts) | The method word mapped as the graph's (`merge`, `fast-forward`, `squash`), everything else kept; a line delivery's open row settled from its request |
| `landings.json`'s entries; a together operation and its attempt (form, target, pinned, replayed, resulting) | Kept, the form word mapped as the method |

**The census, recursive.** A test walks every record type a `land` writes, member by member through nested records and
collections: the node record and all it holds (statuses, bindings, acceptances and their ports, attempts and their calls, kept
results, history, pending words, repairs, fired keys), the delivery store's rows, `landings.json`'s entries, the together operation
and its attempt, and `LandAttempts`' records. It fails on a member the conversion's table neither converts nor names as kept.

**Restart-safe.** Each write is atomic, and each run's are ordered: the unit records, then the run's record, then the binding, each
record converted carrying a mark naming the conversion (from and to version). The conversion of a record is a function of the old
record and the two graphs alone, so a record that holds the mark is not converted again, and a run whose records hold it while its
binding is old has its binding written only. The step is recorded done only after every run; one cut short runs again at the next
start (HomeMigration's rule), and until then a run on an old version is held whole, since this build has no `land`.

With it, the `land` kind leaves the kit: `NodeKinds.Land`, `Workflows/Kinds/Land/` (its settling re-homed in `merge`),
`LandPerformer` (its tree read re-homed for `TurnCarries`, §4), `LandTries`, `RebaseTelling`, `LandCarry`, the `land` half of
`CurrentDeliveries`; `NodeKitTests` holds the kit with no `land`. The conversion's reading of `land` is the one remnant, deleted with
the migration it is a step of (row j).

## 6. What this rejects

- **A `land` kind narrowed to the move.** Named by landing, it reads every move as a landing (a chain's advance, a release's
  promotion), which the owner's direction refuses; the move is `merge`, and the landing is what a preset composes.
- **A composite `land` that expands into the steps when saved or run.** The editor would show one node and run five, the run view
  could not say which waits, and the composition would be said two ways.
- **Rebasing in the session's tree.** It moves the branch the run is bound to, rebinding the holds and the review's Accept, and it
  writes in a tree a session may be running in; the rebase runs in a tree of Daoris's own, and only `resolve` uses the session's
  tree, detached, under `MoveTreeAsync`'s conditions and its journalled hand-off.
- **A `verify` kind beside `check`.** Two ways to run the gates on a commit.
- **Inputs found by place** (today's `afterHanded`, `RunSubjects.Placed`, the first landing node) **or unfed inputs defaulting to the
  run's work.** Each is a second way to name a value a wire names, and with two `merge`s it reads the wrong one.
- **For a row that truly differs: the driver choosing a side, writing both texts into the row, or landing the target's text while the
  work's waits on a record.** The first loses a rewrite unseen; the second writes the driver's words into authored content (D222) and
  a row with two heads; the third lands a backlog missing the work's own account of its row. The resolver, who wrote one side, merges it.
- **Archive winning over the other side's added words, and a row's words merged word by word** (round 1). The first drops a
  requirement the line added to a row the work archived; the second makes wording neither side wrote; D215's point 6 keeps a
  conflict of meaning for a resolver.
- **Archive winning where the other side only removed words** (round 2). A removed word can reverse a requirement, and "only
  removed" is the driver judging meaning.
- **`behind` for a fast-forward only** (round 1). A merge or a squash onto a moved target is a fresh three-way merge nobody verified,
  and it may conflict with no outcome to say so.
- **Taking the run's end before `tidy` follows**, to keep the review from lapsing. It hides the run's own move instead of reading it,
  and any step after the tidy would still lapse.
- **`tidy` removing the step refs.** A later consumer or an unsettled attempt may still read the rebase's commit, which a squash and a
  branch tidy leave on no other ref.
- **`resolve` handing on a commit of its own.** Every step after the rebase would then take its commit from one of two producers;
  finishing the rebase's attempt keeps one producer and lets the rebase settle anything the target brought meanwhile.
- **Tries counted per ask across every stop, reset at each rebind** (round 1). A four-stop replay always fails, and a busy line asks
  forever.
- **The integration's telling kept beside `resolve`.** A second hand-over, with the session rebasing its own branch, which the rebase
  refuses for itself.
- **`merge` writing the line itself beside the promotion** (round 2's `update-ref` for every target). The line would have two
  writers, and the promotion's fence, its authority re-read and its checkout rule would be kept twice or skipped by one of them.
- **A `merge`'s request batched, or merged onto whatever the candidate holds** (today's press). What lands is a merge nobody
  verified, and `behind` is never seen.
- **Every hand-back read as `conflicted`, and a request adopted by unit and tip** (round 2). A candidate that failed leaves the line
  unmoved, so the rebase makes the same commit and the closed request is found again: a loop; and a gate's failure is no conflict.
- **Holding a member for the person whenever its candidate failed.** What it met never reached the line, and it was the 2026-11-09
  wait; it is asked again, bounded, and the person is asked past the bound.
- **Dispositions and *rebase it* for a request an open run asked** (round 2's *rebase it* through the run's `rebase`). The run's own
  `merge` and `integrate` bring those commits already; a second way would replay them twice.
- **Converting only each workflow's newest version** (round 2). A run bound to an older version would run another version's delivery.
- **Holding a run whose conversion the save refused, for the editor to mend** (round 2). This build has no `land`, so the run would
  wait for good.
- **A home floor, an older build's refusal and a rollback's** (round 2). D232 deleted the floor, keeps no older reader and takes the
  salvage copy as the undo.
- **Keeping bound runs on the old `land` until they end** (the land node's design kept old versions so). A run waiting on a person
  can live for weeks, so the old performer would stay with no end; the conversion maps every state at the release's first start.

## 7. Rows, in order

The rows as round 6 leaves them, rewritten in place; round 5's are the document at `880e551fe8`.

1. **PROCSTEPS1a, the records by their rows' ids** (driver, tools; waits on nothing; being built): §2.3's backlog rules and
   post-check in `BacklogMerge` and its twin `mergeByBase`, union's keep-once for a non-blank line both sides added. It serves
   today's landing, the integration's member merge and the tree's update at once. Proof: `fixtures/backlog-merges.json` rows run by
   both twins, one per rule: an archive against a row unchanged but for whitespace and one only moved (each leaves), and against a
   word removed and a word added (each `conflicted`); two different rewrites; a delete with no archive against a rewrite; one id
   added twice; two inserts at one place, equal and not; exact ids beside a longer one; compound ids by list and by range; an input
   holding an id twice; the post-check over a clean merge that kept an archived row and over one that let an archive win over the
   line's change; a backlog row whose head is a compound id (`WFREVIEW3a, 3d, 3l and 3m`), the work archiving 3a while the line
   archives 3l, reopening no finished item. Over real git (`LandBacklogLandingTests`, failing first): a replay whose row the work archived while the line
   moved it lands with no session asked, and one whose row the line changed stops naming the row with both texts.
2. **1b, values on the wires** (driver): `work`, `commit` with its `onto`, `ref` with its producer, `problem` with its producer and
   stop's identity; `claimed`'s `work` output; `RunSubject.Input`; step refs per attempt and their keeping; the bound on a target's
   move; a run's own move as its binding's revision; a skipped producer's readers; the arriving mark. (A `back` wire on an outcome is
   the graph's already, `NodeWires`.) Proof: a moved input rebinds only what it feeds; `RebindBoundTests`, over every kind with a
   target, its target moved while each node it feeds is active or reached rebinds nothing, and moved while none is rebinds it; a
   recorded own move followed by a pass lapses nothing; a step ref an attempt reads survives a restart and `git gc --prune=now`, and
   goes at the run's settled end; a save holding an arriving kind refused.
3. **1c, `rebase`** (driver; its words): the kind, arriving; its door; the replay-and-settle unit moved into its folder with its
   other callers; `onto: target`; its start, reading `TogetherBinding` (§14): a member waiting while its operation is open, the head
   until its operation is `ready` at its tip; the press reading `TogetherBinding.Delivering` under each member's landing hold (§22);
   the inclusion record's leaving out; its reruns. Proof: one test per outcome over real git; a chain's step 2 rebased onto the chain's branch replays only
   its own commits and fast-forwards with the line moved meanwhile, and a squash chain the same (`TreeBranches`' advance cases, run
   through the kind); 2026-11-09's sequence (three works archiving their rows one after another while the line moved) reaches
   `ready` with no session asked where the line left the row as the base had it, and `conflicted` naming the row where it changed
   it; the person's checkout, the work's branch and its tree unchanged byte for byte; a stop mid-replay leaves no scratch tree; a
   session mid-turn holds it `ready`, said; a member bound to an open operation never starts, and once the operation landed a
   member whose commits the head replayed as new ids, and one whose line the head reverted, replays nothing; a head whose operation
   is `asked`, `replaying` or `checking` stays `ready`, said, and starts at `ready`; a member whose `rebase` has reached at its tip
   while a step its commit feeds has not settled, pressed Together: refused, said, and bound once that step settled; a member with
   an unsettled delivery or an open request refused the same.
4. **1d, `resolve`** (driver, modules; its words): the hand-off in its own record (`TreeHandOffs`, §13), written under the tree's
   landing hold and then the repository's lock alone once the holders are read (§29), read by the trees' holders (their reads taking
   the home), by `TreeRemovals` under the landing hold and by every start inside its shared hold (`TryStarting` naming its tree, the
   service's opens asserting the hold, §29), never by the act journal (§23); the run's own step (`StepAuthority`, §28), which this row
   first needs: carried on every door's start and set by the kit's door runner (`TidyDoor`, `LandingDoor`, `PushDoor` and the
   `rebase`'s door moved onto it), by `IntegrateStart` around each attempt's answer, by `WorkPerformer`, and by the hand-off for its
   resolver; the reserved tree, every write or drop touching its folder or the refs it covers refused at the git gate but its
   hand-off's own (§23, §30); the ask's key exempting the resolver's own turn, its session lock and let-go under the row's authority
   (§29); the act runner's deadline over its callbacks, the gate handing a callback's git to the act's launch and the close awaiting it
   (§25, §32); the ask, the continue with the settling, the proof, the tries per stop, the
   restoration with the resolver's HEAD on the edits ref, the `uncommitted` problem with its blobs, one asker to commit
   (`CommitAskers`, §13), the carried Accept, `rebase-tail`, the card's wait. Proof: a code conflict asked once with its files and hunks and
   resolved; a four-stop replay asking each stop as its own try 1; a records stop after it settled with no ask; the line moving 50
   times while it waits making one ask; a stop met again after a rebind going on from its count; the bound spent failing to the
   person's task, said on the card, and that task's `done` returning to the rebase; a resolution touching an unnamed file refused; a
   session the person stopped never asked; a timeout with the resolver mid-edit, a lapse, the person's stop and a restart with the
   hand-off open, each ending with the resolver stopped and awaited first, the tree on its branch at its tip, the edits on the edits
   ref and applied at the next ask, the entry closed; a resolver that commits, and one that runs `rebase --continue`, then times out,
   its commits on the edits ref; an `uncommitted` resolution committing exactly the problem's blobs keeping the review, and one
   changing another path asking it again; nothing else taking the tree while it is reserved; R1's hand-off open for 30 minutes while
   R2's candidate promotes and the checkout's tree update runs, neither held, the look's tidy, a landing, a session start and
   `TurnCarries` keeping off R1's tree, each said, and a writer's reconcile leaving the record open; a done quest's dirty tree whose
   run has a `rebase` asked by its `resolve` alone, and one no `rebase` reads asked by `TurnCarries` alone; the person's discard of
   the session while its resolver is mid-edit: the tree kept, said, and the staged resolution on the edits ref at the hand-off's end;
   a removal that took the landing hold first, the hand-off then using a tree of Daoris's own; a planted door's
   `worktree remove --force` and `switch` in a reserved tree refused at the gate, before and after a restart with the row open; the
   resolver's ask starting in its reserved tree, and the person's word to that session waiting, said, until the row closes;
   a start racing the reservation in both orders: the start holding first, the hand-off reserving nothing and then finding the session
   a holder; the hand-off first, the start held at that look and then waiting on the row, said; a start in a reserved tree through each
   of `TryStarting`'s six callers held, and a planted service open with no hold throwing; the resolver's `worktree lock` at its start
   and `unlock` at its end passing the gate, the look's let-go of a reserved tree leaving its lock, said; `update-ref
   refs/heads/<reserved branch>` and `worktree unlock <tree>` from the checkout's root refused; `StepAuthorityCensusTests`' source half
   and its door half, red on a planted fifth setter, on a door running its work through its own `Task.Run`, and on a door kind whose
   work reads no authority; `ActJournalCensusTests` with its run half, red on an `After` awaiting a turn that never ends, the act
   stopped at its deadline and its claim closed, and on an `After` starting git unawaited, its git run in the act's job and awaited by
   the close, and one started after the close refused.
5. **1e, `check` fed, then `merge`** (driver; their words). First `check`'s `commit` in and out, so `merge`'s proofs run through it:
   a check on a rebased commit, its failure stopping the `merge`, a target moved while it runs rebinding nothing. Then `merge`: the
   methods and targets; into the line, the request at the one door with its expected tip and invocation, never batched, `behind` at
   the replay, the promotion's compare-and-set; into a branch, the `update-ref` compare-and-set; the delivery and its ladder re-homed
   with `LandAttemptSettling`, a line delivery's phases read from its request; the authority and the judgement before the effect, read
   again at the promotion; the judgement under the `work` tree's landing hold, reading `TogetherBinding.Of` and withdrawing a bound
   member, the delivery's prepared phase asserting the hold (§22); `behind`, `completed` routed `back` with its way (its branch's removal f's,
   §21) and `merge-behind` over both; `root-dirty`; into a branch, today's facts by the target's kind
   (`LandingAdvance` re-homed, §15) and the landing provenance; the together re-read, and the operation's attempt written and settled
   by the head's `merge`, the look's settling of `landing` and `uncertain` moved into it with an unpromoted close read as unmade (§14,
   §22), its writers asserting the head's `merge` by authority (§32); a line request dropped or handed back by a decide failing to the
   person's task (§24); the line's admission under the landing hold and the lock shared, both let go before the line's run is sent off
   the flow, `IntegrationLines.RunAsync` asserting no hold (§31); the press waiting on its attempt and answered from the run's record
   by its `merge`'s end or the node the run stands at, `LandOutcomes` no longer read by it (§33); a withdrawal at a moved tip keyed by
   the tree's tip and the start's (§33). Proof:
   each method onto the line and onto a branch; for each method onto the line, the line moved between the check and the admission,
   between the admission and the form, and between the form and the promotion, and another epoch's request queued beside it: `behind`
   or formed again, never batched, nothing unverified lands, and through the evaluator back to `rebase` and `check` and on to one
   merge; for each method onto a branch, moved before the judgement and at the `update-ref`: `behind`, nothing written; each currency
   read (the work's tip, the quest's submission, the step ref, the run stopped) moved between the admission and the promotion: the
   member lapsed, nothing moved, the `merge` rebound; a stop at each phase ends in one delivery; a press and a pass merge once; a
   squash's and a merge commit's tree equal to the input's; a two-member together operation in one delivery, and a member moved
   since it was bound refused with nothing written; a save of a `merge` whose `behind`, or a pattern's `completed`, leads nowhere
   refused; the head's `merge`
   writing the operation's attempt before its first phase and settling it `landed`, each member's run then replaying nothing, `held`
   and ending, `behind` setting it back to `ready`, and a restart after each phase settling it once; a restart between a request
   closed `behind` on a moved line and the head `merge`'s read: `Unmade`, the operation `ready`, its members free; a member pressed
   Together while its `merge` judges: whichever takes the member's landing hold first, the press bound and the `merge` withdrawn with
   nothing written, or the `merge` admitted and the press refused, said; a planted delivery start outside the hold throwing; a `merge`'s
   request dropped by the steward and one handed back by a decide, each failed with its words and reaching the person's task, and
   one a decide lands `merged`; the orphan-write census by its recorder (§25), red on the head's call to `TogetherOperations.Landing`
   removed, on it moved under a condition no sequence meets, and on a member's `merge` calling it (§32); a
   pattern branch checked out waiting, said, and going on once switched off, a completed one ending `completed` and routed `back` to
   its `rebase`, one moved by another hand failing `moved`, one Daoris never made failing `exists`; a release promotion onto a
   standing `release` advancing it; a branch `merge`'s landing record admitting its `integrate`'s request; a line admission whose
   request the same process then promotes, never refused by its own hold, and a planted line run reached under a hold throwing; each
   end of a `merge` (closed already, gone, superseded or undone, nothing to land, a gate's wait, a moved tip, a dirty carry-on) giving
   the press an outcome the desk words, never *landing* where nothing will land, and *landing* only while its delivery is prepared
   (WFREVIEW3a); a refusal at a moved tip keyed by the tree's tip and the start's, so a tree moving T1→T2→T1 is tried again at T1
   (WFREVIEW3l).
6. **1f, `tidy` and the branch removals** (driver; its words): `follow` writing the run's own move, `branch`, `tidy-held`, the step
   refs left to the run's end; `BranchRemovals` (§12, §20), the one release of a branch's removal, read under the repository's
   `TreeLock` held alone through the removal, refusing an open run's branch to every remover but that run's own step read from the
   flow's `StepAuthority`, its node active on that attempt and its kind declaring the branch its own (`IEndsBranches`, §28);
   `ActPlan.Judge` and `Make` returning their releases, `ActRuns` running the effects under them (§28); `RunGovernance` with its five
   inputs, the assertion at each one writer by the governing fields it changes, *Keep* passing by what it writes, and the migration's
   binding writes routed through the bindings' writer under its run (§32), the start's bind and a run's reopening under the lock
   shared; the git gate's one table (`GitCommands`), total by `GitCommandsCensusTests` and the strict run, refusing a drop without a
   release, `MayWrite`, the bounds and the waits reading its columns (§30); one taker of the lock alone (§31); the opening's undo under
   the flow's hold, the pool's taking it alone where it has none (§31); a delivery's end written by the release and the next generation
   read from it; the `merge`'s end of its branch on `completed` (§21). Proof: a free tree follows and
   the pass after it lapses nothing, the review's acceptance included; a running tree says why it stays; a pushed branch never
   rewritten; a squash run with a `branch` tidy and `git gc --prune=now` between every pass finds every commit a later step or an
   unsettled attempt reads; both interleavings of a start's bind and a release of its branch: the bind first, the removal kept, said
   with the run; the release first, the start going on with its branch gone, said with the act and its recovery ref; each again with
   the driver killed after the removal's act was written ahead and after its effect, then a bind before the act's reconcile, ending as
   one of the two orders; a run's reopening the same; a bind and an edit lifting `Ended` outside the lock throwing;
   `BranchRemovalCensusTests` and `BranchOwnershipCensusTests`, red on each of §20's planted members; each caller over an open run's
   branch keeps it, said with the run, and takes it once the run has ended, a create's Undo, a rename and a rename's Undo among them;
   the run's own `tidy` ends it; an opening's undo released; a delivered branch its run's own step removed, then the same intent
   started: the next generation, the first delivery's row unchanged; refused while the removal's act is open; a delete's Undo
   taking the end back; B1's chain in *Integrated* with its first step completed: R1 promoted, step 2's `merge` `completed`, P ended
   by it with its recovery ref and end row, the `rebase` onto the line replaying step 2's commits alone, a fresh P, R2 admitted, step
   1's delivery row unchanged, and a restart between the end and the `rebase`; the same in *A pull request* with step 1's pull request
   merged before step 2, its `tidy` later refused at the fresh P's tip; a completed P checked out in a tree: the `merge` waiting,
   said, with no outcome. Round 6's: the run's own `tidy` at its end node ending its chain's parts and the run then ending; the
   `integrate` at `conflicted` ending its request branch (its fixture closing the request with h's word, h proving it again at its own
   close); the `merge` at `completed` ending P under its git lock, the release reusing that hold; a release read in `Judge` reaching its
   effect step, and a removal step whose judgement returned none refused at the gate; a door's task that outlived its node's stop not
   exempt; `StepAuthorityCensusTests`' end half over every `IEndsBranches` kind, red on one whose door drops the authority; *Keep*
   outside the lock passing and a planted governing write outside it throwing; `RunRebindsStep`'s rebinds through the bindings' writer
   under the migration; `BranchOwnershipCensusTests` red on `RunRebindsStep.cs:41` before it is routed and on a planted sixth input;
   the pool's make and take undone under the start's hold, a takeover under the lock alone, and one finding it held leaving the claim
   for the next look; each drop refused without a release and passing with one: `update-ref -d HEAD`, `branch -m <new>` with no old
   name, `symref-delete` and `symref-update` lines, a fetch pruning by the repository's configuration into a mirror refspec,
   `branch -f`, `branch -C`, `switch -C` and `worktree add -B`; `merge-tree`, `diff-tree`, `diff-index`, `grep`, `count-objects` and
   `check-ref-format`, and `notes`, `replace`, `submodule` and `maintenance` by their verbs, started and never refused for want of a
   row; `GitCommandsCensusTests` red on a planted command with no row, on a command held in a variable, and in the strict run on
   `notes add` with its row removed; a planted direct `TryWriting` in a flow holding the lock red, and a take alone in a flow holding
   it shared throwing.
7. **1g, the editor, the chart and the run view** (web, contracts, and the declarations h wires): the `Git` group, each kind's
   inspector and inputs, the `rebase` tail and the `merge`'s `behind` way added in one change, the chart's ports and `back` ways, the
   run view's words and waits, in English and Chinese, the generated contracts and types; and what h adds, declared here arriving as
   the kinds are, so the contracts are generated once: `integrate`'s `ref` input and its `conflicted` outcome, its same-tip and
   decide waits and its wait on a branch it could not end, `push`'s `ref`, `line`'s `commit`, a `merge`'s wait in the line's
   integration and on a branch a tree has checked out, its `completed` outcome with its `back` way and its wait on a branch it could
   not end, its failed way to the person's task, the head's wait on its operation. Drawn and tested on the arriving
   kinds and ports, offered at h, so the cut-over ships nothing the window cannot draw. Proof: page tests, the web typecheck,
   `i18n:check` and `names:check -- --strict`, stories at 80, 39 and 25rem in both themes.
8. **1h, the cut-over** (driver, modules; web for *rebase it*'s retirement): the shipped graphs and `WorkflowPresets` recomposed by
   §3, *Release* with `review` before `rebase` (§15), `SetupPresets` by the `merge`, every preset's `merge` routing `completed`
   `back` and its failure to the person's task (§21, §24); §5's conversion as §16 and §24 complete it (every
   retained binding, every state, attempts carried apart from states, each waiting `integrate` attempt's adoption, the binding's
   `Resolved`, last in its migration); the readers bound to their producers (§2.8), the bound intent and `NodeKinds.Land` deleted and
   `GraphMoves` the one reader of moves (§17); the integration by §2.10 (the request's invocation, an adoption recorded before it is
   relied on and read by its kept id, the request's requester and authority unchanged, §24; the `conflicted` close and `against`, the
   same-tip asking, the request branch's end, its outcome waiting for the removal, §12, nothing told, no disposition for a run's
   request, *rebase it* retired with its route verb and the page's button); `TurnCarries` re-homed onto `WorkTreeReads` and
   `resolve`'s commit words, standing down by `CommitAskers`, and `SessionWaits.CarriesOn` asking it, `CarriedAccepts` and its file
   deleted (§25); the `land` kind, `LandPerformer`, `LandOutcomes` (§33), `LandTries`, `LandCarry`,
   `RebaseTelling`, `LandingRules`, `IMakesLanding`, `TogetherAccepts` (its re-read re-homed in the head's `merge`) and the
   arriving mark with its refusal removed; the press waiting on the `merge`; the area document rewritten. Proof: every shipped
   file judged by the save; §5's table over every `land` state and every preset, a custom
   graph, two open runs bound to two source versions (each keeps its own delivery), a graph at the old bounds, and a refused
   conversion recovered as a new run with nothing moved twice; each converted then passed once: no acceptance lapses (an upstream
   acceptance's id changed and the review resting on it still accepted), no effect or ask repeated, every input fed, a pull-request
   run with `push` pending pushing once and its plugin attempt answered by its kept invocation; the recursive conversion census; a
   fault after each write, then the step again, ending byte for byte as an uninterrupted one; the added ids distinct over every
   fixture; the selection census (no source names a symbol of §2.8's table); the say census (no source names `RebaseTelling`,
   `RebaseKeys`, `IntegrationReplayAsk.Say`, `LandPerformer` or `LandCarry`, and `LandCarryTests`' look moved: `TurnCarries` says
   the commit words over a done quest's dirty tree as before); the same-tip hand-back (B conflicting with A, A's candidate failing on
   its gates, the line unmoved: B asked again at the same tip once, no closed request adopted, B lands; three such hand-backs waiting
   on the person); a member whose candidate promoted ending `conflicted`, its request branch ended, its rebase onto the line meeting
   the conflict and its `resolve` asked; a gate hand-back ending failed and reaching the person's task, never the rebase; a together
   member moved between the operation's ready and the head's effect: nothing written, checked again; no disposition for a run's
   request; `NodeKitTests` with no `land`; the census of doors that land. Round 4's: the request branch across a restart (the
   `rebase` makes W, a fast-forward makes P and delivery D settles, the gates hand W back, the `integrate` ends P, the person's
   task is done, the `rebase` makes W again: the next generation makes P, a new request is admitted; a restart after the removal's
   act, after the end's row, and between the new generation's phases, each ending with one P, one new request and D unchanged);
   a removal kept by a tree holding the request branch, the `integrate` waiting, said, with no outcome; a run ended before the
   upgrade, converted, then reopened by its chain and going on bound to the conversion; a stopped `land` beside an active task
   staying stopped; an invalidated and an unknown one with an attempt unsettled, settled before anything starts; a request open at
   the upgrade settling after it, its `integrate` reached; the binding's resolved options read by the `merge`; §16's three-part
   census over the captured home; §17's selection census and the two-`merge` release graph; the conversion after `StoreNamesStep`,
   its own copy of the store kept. Round 5's: an `integrate` adopting the card's open request at its tip, the request then closed
   settled, in turn handed back by a gate and dropped by the steward, then a restart: the attempt reads it by its kept id, reached or
   failed by the close, the request's requester still the card's and its authority re-read the person's; a restart between the
   adoption's write and the attempt's, the request closing meanwhile, found by its invocation through the adoption; the node's stop
   letting an adopted request go, never dropping it; a waiting attempt at the upgrade given its adoption, the pre-h request keeping
   its requester and no invocation; a went-on resume of a session whose run's `rebase` will read its tree carried on by its `resolve`
   alone; the residue census by the home's declared layout and the repository's `refs/daoris/` namespaces and configuration keys, red
   on `carried-accepts.json` before its row, on a planted file name, on a planted `refs/daoris/land/<run>` and on a planted
   `daoris.land.<run>` key (§32); `GraphInvarianceTests` over every shipped preset, the two-`merge` release graph and the converted
   fixtures, every id the contract marks renamed, red on the planted selection by `"landing"`, by list order, behind a helper and by a
   wire's id (§32).
9. **1i, the installed window** (the steward): `dev-line` on the install through a day's rows: records conflicts settled with no
   session asked, a code conflict resolved within its bound, a resolve cut by a restart and taken up again, a restart mid-`merge`, a
   converted run carried.
10. **1j, the conversion removed** (driver): by D232's rule, the next release that changes a stored shape replaces the migration
    `LandConversionStep` is a step of, and deletes the step with its reading of `land`; the condition is that release's own new
    migration, read, not judged. Proof: a source census names no `land`.

## 8. Not decided here

- Recomposing the line's integration run (one run per line, fixed steps) from these kinds: its replay calls the rebase's unit after
  1c, and its hand-back is read by a run's `integrate` after 1h; the run's own shape is a later design.
- Whether a failing `check`'s fix ask (`failed: ask`, `VerifyFixAsks`) becomes a `resolve` of a gate's problem.

## 9. The reads answered

### 9.1 Round 2's reads, answered by round 3

Each blocking finding, the sequence it closes, the code the answer rests on, its proof's row, and the census or test that closes its
class.

**Codex, round 2.**

- **1, the records judge meaning.** Base "Do not enable A"; the line removes *not*; the work archives the row: round 2's "only words
  removed" let the archive win. Answer: §2.3, the rule row a builds: an archive leaves the row only where the other side's copy is
  unchanged after normalising whitespace, or only moved; any other change, a removal included, is `conflicted` with both texts.
  Proof: a. Class: the post-check holds every id that left the backlog to that test, clean merges included, so no merge path lets an
  archive win over a change.
- **2, changed digests lose Accept.** An acceptance's id hashes its graph digest, its inputs' acceptances and its prerequisite
  receipts (`NodeAcceptances.cs:24-41, 78-100`); an outcome receipt hashes an acceptance (`WireReceipts.OutcomeReceipt`), a firing key
  the digest and the receipt (`WireEvaluation.FiringKey`), a scope the digest (`NodeScope`); a binding lapses on any of them
  (`WireLapsing.cs:58-61`). Change upstream A to A′ and the review resting on A lapses. Answer: §5's transitive map, every acceptance
  remade in the graph's order from its converted upstream, every id and outcome receipt mapped old to new and every member naming one
  taking the map, the fired keys computed again; what another party keeps (each attempt's random invocation and idempotency key,
  `WorkPerformer.cs:173`; an integration request's key; a plugin's resources) kept byte for byte. Proof: h, an upstream acceptance
  re-identified with the review on it still accepted, a pull-request attempt answered by its kept invocation. Class: the recursive
  census over every record a `land` writes, and the pass after the conversion lapsing nothing.
- **3, one version converted, the bounds, a refusal.** Answer: §5: every version holding a `land` that is newest or bound by an open
  run converted from its own graph and options, the newest's appended last; the bounds (`NodeGraphs.MaxNodes` 24, `MaxWires` 96)
  raised for every graph by what a composition adds; a refused conversion recovering each run bound to it as a new run on the place's
  choice, its deliveries settled first, nothing moving twice. Proof: h, two runs on two versions, a graph at the old bounds, a refused
  one recovered. Class: a conversion per open source digest, and the bound named beside the expansion it must hold.
- **4, a failed candidate (and Opus N2).** Today the node adopts the newest request for its unit and tip, open or closed
  (`IntegrateStart.cs:199-240`, `IntegrationStore.FindByKey`), and a replay's conflict, a gates' hand-back and a decide all close
  `handed-back` (`IntegrationReplays.cs:483-537`, `IntegrationPromoteWords.cs:28-46`, `IntegrationPromotes.cs:289-299`), so B,
  handed back for A, whose candidate then fails, loops through the same tip. Answer: §2.10: an attempt adopts only the request its
  invocation asked; a replay's conflict closes `conflicted` with its `against`; `conflicted` only once the line holds `against`, past
  the work's `onto`; otherwise held, its recovery defined, never a loop through the `rebase`: the same tip asked again, once per ended
  candidate, at most 3 times, then the person; a gate's or a decide's hand-back, or a drop, is failed. Proof: h, the same-tip
  sequence and a gate hand-back. Class: no attempt can find a closed request it did not ask, and the outcome is read from what closed
  the request.

**Opus, round 2.**

- **The censuses.** Records: the post-check's test on every id that left (a). Conversion: recursive over every record a `land`
  writes (§5). Selection: every symbol with every reader in source (§2.8), `NodeEvaluations.Landed` and `LandingOf`,
  `NodeObserving.LandingOf` and `ReadsTheLanding` with `NodeTelling`, `NodeKept`, `NodeOwing`, `NodeIntegrating`, `CheckNode` and the
  hold, push, tidy, pull-request and land readings among them. Delete: every caller named (§2.7), and the open-run refusal moved into
  the delete itself, so every caller is disjoint from open runs, not only the look's. Say: `RebaseTelling`, `RebaseKeys`,
  `IntegrationReplayAsk.Say`, `LandPerformer` and `LandCarry`, with `TurnCarries` re-homed onto `WorkTreeReads` and `resolve`'s
  words (§4, h).
- **N1, two writers of the line.** Today the press is admitted at the one door (`TreeMerges.cs:160-174`,
  `IntegrationPresses.MergeAsync`), batched by the first request's policy and epoch (`IntegrationReplays.cs:144-164`) and merged
  `--no-ff` onto whatever the candidate holds (`:378-379, 429-455`); the promotion is the line's compare-and-set
  (`IntegrationPromotes.cs:362-432`). Answer: §2.6, the first of the read's two ways: a `merge` into the line is a request carrying
  its expected tip, never batched, its replay refusing a base that is not the input's ancestor as `behind`, and the promotion stays
  the one writer. Its fence, its authority re-read and its checkout rule are already the promotion's, so calling a `merge` write from
  it would move them or keep them twice. Proof: e. Class: every method onto the line, the line moved at each point and another
  epoch's request beside it.
- **N2.** With Codex 4 above.
- **N3, together members.** Today a member is `bound`, then `brought-into` from the operation's record (`DeliveryReach.cs:47-63`), and
  the combined Accept re-reads and re-accounts every member under the hold (`TogetherAccepts.cs:96-115, 167-186`). Answer: §2.2 and
  §2.6: a member's `rebase` waits while its work is bound, then leaves its work out to its bound tip by the inclusion record, so its
  `merge` is `held`; the head's `merge` adds the together re-read. Proof: c (a bound member never starts; new ids and a revert replay
  nothing), e (one delivery, a moved member refused) and h (a member moved between ready and the effect).
- **N4, *rebase it* duplicates the run.** Today the settle writes dispositions for commits after the frozen tip (`IntegrationSettles`)
  and *rebase it* tells the session (`IntegrationDispositions.cs:33-56`, `IntegrateRoutes.cs:141`, the page's `IntegrationTab`).
  Answer: §2.10: no disposition for a request an open run asked; *superseded* alone for one no run asked; *rebase it* retired. Proof:
  h. Class: a disposition is written only where no run brings the commits.
- **The calls the brief flagged.** The closed request's branch: ended by the run's own `integrate`, node requests only, at
  `conflicted` and failed rather than at the hand-back, since the same-tip asking needs it (§2.7's table, §2.10). The home floor:
  none (D232); `LandConversionStep` is a self-contained step of h's release's migration (§5), row j's condition is D232's rule, and
  the older-build and rollback refusals are gone.
- **Not blocking, placed.** The carried Accept: §2.4's rule, exactly the problem's blobs keeping the review and anything else asking
  again (d). A resolver's HEAD: kept on the edits ref with its index and files (§2.4 points 3 and 4, d). An `integrate` waiting on a
  held candidate: no time bound, said with who decides and since when (§2.10).
- **Rows.** e builds `check`'s `commit` before the `merge` it proves `behind` through; g declares and draws what h adds; h re-homes
  `TurnCarries` and proves the same-tip hand-back, a gate hand-back failed and a together member moved; j by D232's rule (§7).

### 9.2 Round 1's reads, answered by round 2

Round 2's answers, kept as they were given; where round 3 changed one, §9.1 says how and the body is current.

- **Codex 1, Opus B7 — the records lose or invent text.** Base row X asks for A; the work archives X; the line adds B to X: round 1
  dropped B. Two inserts at one place both stood. A clean merge kept a row the work archived. Answer: §2.3 as row a builds it.
  Proof: a. Class: the post-check runs over every replayed commit, so a case the per-id rules miss (a clean merge, a moved row, an
  id twice) still fails it; one fixture row per rule, run by both twins.
- **Codex 2, Opus B4 — what was checked is not what lands.** The rebase makes C on T; `check` passes C; the line moves to T′; a merge
  or a squash combines C with T′, unverified. Answer: §2.6, `behind` for every method, the target compared and set at once, every
  preset routing `behind` back through `rebase` and `check`. Proof: e. Class: the method-by-target table moved at each point, and
  `merge-behind` refusing any graph, shipped or saved, whose `merge`'s `behind` leads nowhere.
- **Codex 3 — a timeout leaves the tree unowned.** Answer: §2.4's journalled hand-off, reserved tree, the resolver stopped and
  awaited before restoration, edits kept on the edits ref. Proof: d. Class: one restoration for every end (resolved, timeout, stop,
  decline, lapse, restart), each a row of one test.
- **Codex 4 — no way back.** A branch target moves and the merge fails with no return; `settle`'s Done leads nowhere. Answer: §3's
  `behind` and `settle` ways, in every preset. Proof: e and h, through the evaluator. Class: `merge-behind` and `rebase-tail`, save
  rules every graph meets.
- **Codex 5, Opus B1 — the migration of runs under way.** `RunRebindsStep` skips runs with node records; kept nodes lapse on the old
  digest, so the review is asked again and a pull-request run pushes again; the mapped `merge` has no outputs; an active `land` has no
  mapping; the added `tidy` reuses `pull-request.json`'s id. Answer: §5. Proof: h. Class: the conversion table over every state and
  graph, the pass-after-conversion test (no lapse, no repeat), the `NodeRecord` member census, and the fault after each write.
- **Codex 6 — plugin readers select by place.** Answer: §2.8, every reader bound to its producer. Proof: h. Class: the selection
  census over the twelve units and any later one.
- **Codex 7 — `tidy` discards a live commit.** Rebase C, squash, branch tidy removes the step ref, a later consumer restarts after
  collection. Answer: §2.1 and §2.7, step refs kept to the run's settled end, one per attempt. Proof: b and f. Class: `git gc` between
  every pass of a run, every commit any step reads found.
- **Codex 8 — two cleanup authorities.** Answer: §2.7, disjoint by the open runs, one journalled delete. Proof: f. Class:
  `BranchDeleteCensusTests`, a new caller of the delete failing it.
- **Opus B2 — a chain's advance loops.** Step 2's commit, rebased onto the line, does not descend from the chain's branch: `behind`,
  back, the same commit again. Answer: §2.2's `onto: target`, replaying only what the target lacks. Proof: c. Class: `TreeBranches`'
  advance cases run through the kind.
- **Opus B3 — reruns unbounded.** The line moves while `resolve` waits and its tree is detached mid-rebase; the run's own `merge`
  moves the line and the press hears nothing merged. Answer: §2.1's bound on a target's move, §2.4's restoration, the press waiting
  on its attempt. Proof: b, d, e. Class: `RebindBoundTests` over every kind with a target; §2.9's table.
- **Opus B5 — the judgement before the effect lost.** Answer: §2.6, the input's currency read again under the lock. Proof: e.
  Class: each currency read moved between the judgement and the effect, one row each.
- **Opus B6 — `tidy follow` moves its own input.** Answer: §2.1, the run's own move as its binding's revision. Proof: b and f.
  Class: the rule is the lifecycle's, for any step that moves a ref its run is bound to.
- **Opus B8 — a second hand-over survives.** Answer: §2.8, integration through its run's `rebase` and `resolve`, `RebaseTelling`
  deleted in h. Proof: h. Class: the say census.
- **Not blocking, placed.** Tries per stop, asked again only on a move: d. `settle`'s way out: h, `rebase-tail` in d. Union's
  keep-once limited to non-blank lines both sides added, and a row the line deleted without archiving while the work rewrote it: a.
  The together operation's members: e. G8's unpressed conversion as the precedent: §5, h. Rows c to f arriving, refused at save: b
  and h. Each kind's words in its own row: c to f. Codex's bounds: §2.2's start (no wait inside the step), §2.6's and §2.7's defaults,
  §2.4's tries kept across rebinds, and `resolve`'s ownership of the rebase attempt it finishes (§2.4, `rebase-tail`). The web before
  the cut-over: g. A measurable floor for j: §7.

## 10. Earlier rounds, superseded where a later one changes them

Round 5 is the document at `880e551fe8`. Round 6 rewrote its §7 in place; the passages of §2 to §26 that round 6 changes stand as
written above, and where they differ §28 to §33 hold: §20's `WorkPerformer` as the one setter of `StepAuthority` (§28); §20's table
of removals beside `RefSnapshot.MayWrite`, and its refusal at run time of a command the table does not class (§30); §20's three
inputs of `RunGovernance` and its writers asserting the lock at every write (§32); §20's opening's undo under the start's shared hold
alone (§31); §22's judgement into the line under the landing hold and then the git lock (§31); §23's row written under the landing
hold alone, the gate keyed by the folder git runs in, and the resolver's commands kept off the gate as a whole (§29, §30); §25's
callbacks starting git through the act's launch by their own choice, its residue by the home's layout alone, its invariance by node
ids alone, and its recorder by presence alone (§32); and §26's second open item, which §30 closes.

Round 4 is the document at `b62716d462`. Round 5 rewrote its §7 in place; the passages of §2 to §17 that round 5 changes stand as
written above, and where they differ §20 to §25 hold: §2.7's caller "named by run, node and attempt", §12's release reading the open
runs before its transaction, `RefTransaction` refusing to stage a branch's removal, `BranchRemovalCensusTests` checking the callers,
and the opening's undo released by the opening alone (§20); §2.6's and §15's `completed` failing to the person's task (§21); §2.6's
judgement under the git lock alone and §14's reading of `TogetherBinding` at the `rebase`'s start alone (§22); §2.4 point 1's and
§13's reservation kept by the holders alone, `TreeHandOffs` read whole only at the driver's start, and a session's start in a
reserved tree waiting with no exception (§23); §2.10's door finding an attempt's request by its invocation or open at its unit and
tip, and §16's invocation written onto a pre-h request (§24); §13's `TurnCarries` standing down beside `CarriedAccepts`, §16's residue
census by the kind's name and the digests, §17's census by source alone, and §14's orphan-write census by callers in source (§25);
and §18's first open item, which §20 closes.

Round 3 is the document at `fd2c1421c6`. Round 4 rewrote its §7 in place; the passages of §2 to §5 that round 4 changes stand as
written above, and where they differ §11 to §17 hold: §2.2's start and §2.6's together (§14); §2.4's hand-off as "an act of the
journal" (§13); §2.6 into a branch, with "failed `exists`" for every branch (§15); §2.7's callers and its census (§12); §2.10's
request branch ended "before its outcome is written" with nothing said of a removal that fails (§12); §3's *Release* row (§15); §4's
`TurnCarries` and `TogetherOperations` rows (§13, §14); §5's "reads nothing another step writes", "a run that has ended is finished
history and is not converted", the table's "`land` failed, rejected or unknown" row and its states with no stopped or invalidated
row, and its census of records a `land` writes alone (§16); §2.8's selection census by symbols alone (§17).

Round 2 is the document at `fd03c57109`; these are its passages round 3 replaced, kept as history.

- §2.2: "A finished attempt `resolve` wrote onto a commit the target does not hold (an integration's candidate, §2.4) starts once
  the target holds it or the request it waits on has ended, said."
- §2.3: a row one side archived "leaves the backlog only where the other side added no words to it: left as the base had it, only
  words removed, or moved", and the post-check with no archived row's test on a clean merge.
- §2.4: "unless resolved, the tree's state (its index and working files) is written as a commit on the node's edits ref", so the
  abort dropped a resolver's own commit or `rebase --continue`; and "a review's word, where one stands before, stays the person's yes
  for the composition after it", with no rule for a session's commit.
- §2.6: "The merge, squash or fast-forward commit is made on a detached head (in the line's integration tree …), and the target ref
  moves in one `update-ref <target> <made> <expected>`" for every target, the line included; the together operation's members
  "reaching when the target holds its commits".
- §2.7: "Both delete through the one journalled delete …, and every caller of that delete is a named authority", the look's tidy
  alone kept off an open run's branches.
- §2.8: twelve units named as the landing's readers; the `integrate` "ends … `conflicted` once the candidate it was handed back from
  has ended (promoted or failed)", "the closed request's branch is ended at the hand-back"; "A superseded commit's *rebase it* is the
  person's act on the member's open run's `rebase`".
- §5: "after `PushStepsStep` and before `RunRebindsStep`"; "unpressed, as G8's `push-steps` … are: that is the precedent against
  D228's press"; "Each saved workflow whose newest version holds a `land` gets a new version"; acceptances "under the new digest
  …; their subject, revision, inputs, outputs and prerequisite receipts as they were"; "Downstream attempts and their receipts: As
  they were"; "A version the save refuses is left as it is and said, the editor being where it is mended"; "An older build reads a
  graph holding `rebase` as an unknown kind, held whole, and a rollback is refused while a step's delivery or step ref is open".
- §7: e `merge` proving `behind` through a `check` whose `commit` port f built; g drawing the new kinds only; b's "an older build
  holds such a graph whole"; j "once the oldest build an install may update from is h's release or later (the home's floor, D228's
  ONEWAY1 note)".

Round 1 is the document at `0422e84b1b`; these are its passages round 2 replaced, kept as history.

- §2.3, the backlog: "**a row the work moved to the archive** … leaves the backlog, whatever the target did to it; a rewrite the target
  made since the base is said in the step's words with its text, so a person sees what left with it, and nothing waits"; "**a row both
  rewrote** is merged word by word against the base row (`BacklogMerge.ByBase` over the row's words): two rewrites that changed
  different words, such as a new head and a new tail, merge"; and union "keeps once a line union kept twice word for word".
- §2.2: "It waits, said, while a session is running in the work's tree (the commit may still move)", and "the step runs again when
  its input or its target moves", with no bound on the target's move.
- §2.4, retries: "Each ask is a try. … Past `retry.maxAttempts` or `timeoutSeconds` it fails", the tries counted across the stops,
  and the tree switched back at the end with no hand-off journalled, no reservation and no restoration on a timeout or a lapse.
- §2.6: "failed `behind` (a fast-forward the target's move made impossible)", and "the standing automatic authority is judged again
  just before its first effect", with no reading of the input's currency.
- §2.7: "Removes the run's step refs (`refs/daoris/steps/<run>/…`) once every part is ended or followed", and the follow with no
  reading of the run's own move.
- §2.8: "`monitor` and a plugin's `pull-request` are unchanged", their readers left on the first landing.
- §3: `onto: line`; the `merge` reached led on by an `after` wire; no way back on `behind`, and `settle` led nowhere.
- §5: the conversion "runs only once the delivery store holds no unresolved delivery"; "a `land` reached maps to `rebase`, `merge` and
  `tidy` reached; one failed or never started maps to `rebase` not started", rebinding "as `RunRebindsStep` rebinds runs today".
- §7: the rows were a records, b wires (with a `back` wire on an outcome), c `rebase`, d `resolve`, e `merge`, f `tidy` and `check`,
  g the cut-over, h the web, i the installed window, j the conversion removed "once no home saved before 1g's release can start".
- §8: "The look's own branch tidy (`BranchTidying`) beside a workflow's `tidy`: which one ends a session branch no workflow tidies."

## 11. Round 4: what it changes, and the code it rests on

Round 4 answers both makers' reads of round 3, read against the code at `9e7ba088df`: the ref acts and the opening's undo, the act
journal, the together operations and their readers, the delivery store, the integration's requests and admission, a chain's advance,
the run's keeping and binding, the node states and the migration's steps. Each answer is a construction: one unit every member of its
class passes through, so a member nobody has found yet meets it too, and a census that names the planted member it goes red on.
§12 to §17 change the sections they name and hold where they differ (§10 lists them); §7 is rewritten in place as the rows now are;
§18 says, per finding, its answer and its section, and what round 4 does not close by construction.

## 12. Round 4: one owner of every branch removal (§2.7, §2.10)

**What removes a branch today**, read from every `RefTransaction` that deletes or renames and every git command line in the driver:
the journalled delete (`JournaledDeletes.DeleteAsync`, with `ForcedAsync` and `ContentHolds.DeleteJudgedAsync` over it); the person's
ref acts, each its own transaction outside it (`GitRefActs.DeleteAsync:127`; a create's Undo, `UndoAsync:86`; a rename, whose
`RefTransaction.Rename` deletes the old name, and a rename's Undo, whose `ReverseRename` deletes the new one, `GitRefActs.cs:368-545`);
and an opening's undo, a bare `git branch -D` (`TreeOpenings.cs:118`, reached from `TreeOpening`, `TreePoolMakes`, `TreePoolTakes`
and `PoolTakeovers`). Every other ref removal is of a ref of Daoris's own, never a branch: recovery refs (`CommitActs`,
`ContentHolds.DropKeptVerifiedAsync`, `GitHistoryEdits`), act refs (`ActRefs`), a partner piece's ref (`PartnerActRunner`), and the
step refs, which only the run's end-keeping removes (§2.1).

**The guard, inside the transaction.** `BranchRemovals` (`Workplace/BranchRemovals.cs`) is the one door a branch leaves by.
`RefTransaction` refuses to stage a delete or a rename of a `refs/heads/` ref (its `Delete`, `Rename` and `ReverseRename` throw
`ArgumentException`, which every act already reads as a refusal before anything is written); a branch's removal is staged only by
`RefTransaction.Release(BranchRelease)`, and a `BranchRelease` is made only by `BranchRemovals.ReleaseAsync(repository, branch, commit,
by)`, its constructor private to that file. `by` names who removes it: the person (window or terminal), the look, an opening, or a
run's step (run, node, attempt). The release reads the open runs' governed branches, as `TidyHolders` reads the trees, and refuses a
branch an open run governs unless `by` is that run's own step, said in the caller's words with the run (§2.7). So a delete, a
create's Undo, a rename's old name, a rename Undo's new name and an opening's undo meet one rule, and a caller not yet written cannot
stage a branch's removal without asking it. The opening's undo becomes a released transaction verified at the commit the opening made
the branch at: it removes only a branch that open made a moment ago and recorded nowhere, which no run governs.

**The census, `BranchRemovalCensusTests`**, in three parts, each with the member it goes red on: no source outside `RefTransaction`
hands git a branch removal (`branch -d`, `-D`, `-m` or `-M`, `update-ref -d` of a `refs/heads/` ref), red on a planted
`["branch", "-D", name]`; staging a delete of `refs/heads/x` without a release throws, red on a planted unit that stages one; every
caller of `ReleaseAsync` is one of §2.7's table with the person's four ref acts and the opening's undo added, red on a new caller.

**A delivery's ref ended, and its next generation (Codex 1).** A delivered delivery answers every later start of the same unit,
commit, method and target with itself, and only a withdrawn one takes the next generation (`DeliveryStore.cs:307-311`), so a request
branch the run's own `integrate` ended could never be made again at the same commit, and the next admission finds no branch. The
release records ends: where the branch it releases is the ref a delivered delivery made, standing at the commit that delivery
settled, the removal's act names the delivery in its plan, and once the act has closed done (its postcondition: the ref absent) a row
of `delivery_ends` is written (the delivery's key, the act, its recovery ref, when); the delivery's own row is unchanged.
`DeliveryStore.Start` answers a delivered delivery with a proven end as it answers a withdrawn one, by the next generation. Uncertain
effects first: it refuses the next generation while the ending act is open or its outcome unknown, which every writer's reconcile
settles first (`ActReconciler.UnderLockAsync`), and, as today, while any delivery of the unit is not settled. A restart that finds
the act closed done with no end row writes the row, keyed by delivery and act, so it is written once. A delete's Undo that puts the
branch back removes the end row in the same act, and is refused where a later generation has made the branch again, the name being
taken (the Undo's rule today). The ends are the release's, so a delivered branch any remover ends (the run's own `integrate`, the
look's clean-up of an ended run's branch, the person's delete once the run ended) lets the same commit be delivered again, a run
reopened by §16 included.

**`conflicted` and failed wait for the removal.** The `integrate` writes its outcome only once its request branch's removal is
proven. A removal refused or kept (a tree has the branch checked out, it moved, or an act on it is open) holds the node active, said
with the removal's clause ("Waits: `feature/q-1` was not ended, since `wt-3` has it checked out"), tried again at each pass, the wait
on that tree's or that act's end alone; so no way leads back to the `rebase` while a stale request branch stands for its
`onto: target` to read.

## 13. Round 4: the hand-off's own record, and one asker to commit (§2.4, §4)

**Not an act of the journal (N5).** The journal holds acts that close within their steps' deadlines, and every writer of a checkout
reconciles them: `ActReconciler.UnderLockAsync` reads every unclosed act of the repository's shared git directory (`:53`), holds every
writer while one runs here (`:143-147`), and takes over and closes one that does not (`:164-177`); the promotion
(`IntegrationPromotes.cs:370-375`) and the tree update (`TreeSync.cs:214`) ask it. A hand-off recorded there would hold another run's
promotion for the resolver's 30 minutes, or be closed by the first writer while the resolver still writes.

**`TreeHandOffs`** (`Workflows/Kinds/Resolve/TreeHandOffs.cs`) is a table of the desktop store, one row per tree: the tree, its
repository, branch and tip, the run, node and attempt, the stop's identity, the resolver, the phase (`reserved`, `prepared`, `asked`,
`restoring`), opened and closed. A row is written before anything is written in the tree, each phase written ahead, and closed by
§2.4 point 4's restoration. It is read by tree, `Of(tree)`, and whole only at the driver's start, `Open()`, which restores each open
row by its phase before any pass (§2.4 point 6). Its readers are the trees' holders: `TidyHolders.ReadHoldersAsync`, behind
`InUseAsync`, `HeldTreesAsync`, `HoldersAsync` and `BesideAsync`, names a reserved tree held by "run `r-12`'s `resolve`", so every
door that already reads the holders before it removes, rebases, lands in or writes a session's tree keeps off it; a session's start
in that tree waits, said; the same work's `rebase` stays `ready` behind its own `resolve`. A writer of the repository that writes no
session tree (the promotion, the checkout's tree update, a replay in a tree of Daoris's own) does not read it, and no writer's
reconcile closes it. `ActJournalCensusTests` names every act kind with its steps, each bounded by its deadline: red on a planted act
kind that holds a tree across turns with no bounded step.

**One asker to commit (N9).** Today a done quest's dirty tree is asked by `TurnCarries` at a turn's end and an Accept's by
`LandCarry`, sharing one bound (`TurnCarry.cs:26`, `LandCarry.Bound`); beside `resolve`'s `uncommitted` that would be two askers with
two counts. `CommitAskers.Of(tree)` (`Workplace/CommitAskers.cs`) is the one answer to who asks a tree's session to commit: the
`resolve` of an open run whose `rebase` is fed that tree's work and has neither reached nor been skipped; nobody while a hand-off
reserves the tree; else the turn's end. `TurnCarries` asks only where it answers the turn's end, so it stands down where a `rebase`
will read the tree: the `rebase` waits for the turn to end (§2.2), finds the paths, and its `resolve` asks, try 1. A tree is asked by
one door and counted by that door's bound: `resolve`'s tries per stop for a run's work, `TurnCarry.Bound` for work no `rebase` reads;
`LandCarry`'s share of the bound goes with it. The commit words (`LandCarry.CommitWords`, `resolve`'s words at h) are read by
`TurnCarries` and `resolve` alone, both behind `CommitAskers.Of`, and the say census (§7 h) is red on a third reader of them.

## 14. Round 4: the together operation's landing, by the head's `merge` (§2.2, §2.6, §4)

**Today** `TogetherOperations.Landing` and `Settle(Landed)` are written only by the combined Accept (`TogetherAccepts.cs:116, 151`),
reached only from `LandPerformer.cs:259-262`, which also holds the head until its operation is `ready` (`:253-257`); the look settles
an operation left `landing` or `uncertain` by `TogetherSettle.Decide` (`TogetherWatch.SettleAsync`). The head is no member
(`DeliveryReach.cs:47`), so round 3's start rule, reading `bound` alone, let the head's `rebase` start mid-join, and once
`LandPerformer` went nothing wrote the landing: every member `bound` for ever.

**`TogetherBinding.Of(work)`** (`Workplace/TogetherBinding.cs`) is the one read of an operation's hold on a work: a member of an open
operation; the head of one before `ready` (`asked`, `replaying`, `checking`); the head of one `ready` at the work's tip; the head of
one `landing` or `uncertain`; or none. `rebase`'s start reads it: a member waits while its operation is open (§2.2); the head waits
until its operation is `ready` at the work's tip, said ("Waits for `s-41` and `s-42` to be brought in: checking"), and a head whose tip
left the operation's checked tip waits for the look to check it again (`TogetherOperations.HeadAt`). `DeliveryReach`'s `bound` is this
read.

**The head's `merge` writes the attempt and settles it.** After its judgement and the together re-read (§2.6), before its delivery's
first phase, the head's `merge` writes the operation's attempt (`TogetherOperations.Landing`, `ready` to `landing`): the form its
method's, the target its own, `Expected` the target's tip the judgement read, `Pinned` the operation's checked tip (the `work` its
`rebase` was fed), `Replayed` its input commit; a line request admitted is correlated (`Correlate(request)`). The attempt is written
with the delivery's prepared phase, so a restart reads them together. It is settled by phase: `merged` or `held`, `Landed`, the
resulting tip correlated, so every member is reached `brought-into` and its `rebase` leaves its work out (§2.6); `behind`, a hook's
refusal or an attempt withdrawn with nothing written, `Unmade`, back to `ready`; cut by a restart, the merge's settling (§2.6's
ladder) decides it by `TogetherSettle.Decide` over the delivery's phases before anything new starts. The look's settling of `landing`
and `uncertain` moves into the merge's settling, so one unit settles the attempt; `TogetherWatch` keeps stepping the phases before
`ready` and dropping an operation whose sessions went. `DeliveryReach`'s brought-into finds the head's delivery by `Replayed`, the
commit the merge delivered, not by the pinned tip.

**Census.** The orphan-write census: each write member of the records the `land` path writes (§5's census names them: the together
operation's `Landing`, `Correlate` and `Settle`, the delivery's phases, `landings.json`'s record and advance, the press's outcome) has
a caller in source outside the units h deletes; red on a planted removal of the head `merge`'s call to `TogetherOperations.Landing`,
which leaves it called by nothing.

## 15. Round 4: `merge` into a branch keeps today's refusals (§2.6, §3)

**What a chain's advance refuses today** (`TreeBranches.cs:320-390`, `LandingAdvance.Refusal`): a branch no landing of Daoris's made
(`exists`); one moved off its landing record's tip, since what moved it may be somebody's; one a working tree has checked out; one
whose work the line already holds, or whose pull request completed or was abandoned; one the work does not grow from; one that already
holds it all. Round 3 kept only the compare-and-set, so an `update-ref` could move a branch under a person's checkout, whose next commit
would revert the step.

**The judgement reads those facts**, under the git lock before the `update-ref`, through `LandingAdvance`'s own unit re-homed in
`merge`'s folder, by the target's kind:

| Target | Read |
|---|---|
| A pattern: a work's branch (`feature/{quest}-{slug}`) | Daoris's own, by its landing record. None: failed `exists`. Off the record's tip: failed `moved`, today's words. Checked out in a working tree: waits, active, said ("Waits for `wt-3` to switch off `feature/q-1`"), the wait on that tree's act alone, read again at each pass. Its work on the line by the content proof, or its pull request completed or abandoned by the platform's kept word: failed `completed`, today's words, to the person's task. Not grown from: `behind`. All held: `held` |
| A fixed name (`release`) | A standing branch the workflow advances, no work's: no landing record is asked and `exists` never applies. Checked out: waits, as above. Not grown from: `behind`. Absent: made |

**Release** composes `review` → `rebase` → `check` → `merge` → `line` → `tidy` → the release plugin's steps: `review` before `rebase`,
as in every other preset, so its Accept is carried across the composition (§2.4's rule) and a moved line runs the rebase and the check
again without asking the person again.

**Provenance.** A `merge` into a branch writes `landings.json`'s record or advance with who accepted (the person where a review's
Accept stands before it in the run, else automatic) and the `integrate` provenance (the version's `integrate` fed its `ref`, with that
node's `request`), as the `land` writes them, so the node route's request is admitted on the same reading
(`IntegrationRequests.NodeJudgement`, `:128-158`; `RecordedAsync`, `:226-260`).

## 16. Round 4: the conversion, over every retained run and every state (§5)

**Every retained binding (Codex 2).** An ended run is not finished history: `NodeRunKeeping.Reopen` (`:96-100`) lifts its end when its
chain comes back here, and the start keeps its binding (`WorkflowRunStart.cs:147`), so it would go on bound to a `land`. The conversion
reads every binding the run files keep, open or ended, held or not, and converts each bound to a version holding a `land` as §5
converts an open one, its `Ended` marker kept as it was. Where the save refuses that version's conversion, an ended run keeps its end
and its records as history, its binding marked unconverted, and its reopening binds it afresh to the place's choice as a first start
binds it: §5's recovery, taken when the run comes back.

**Every state (Codex 3).** §5's table is keyed by `NodeStates.All` and gains two rows: `land` stopped, `rebase` and `merge` stopped,
each with the stop's words, time and door, and `resolve` and the person's task not started, since a stop is the person's word for this
work and a new binding keeps it (`NodeStates.Bound` leaves it out); `land` invalidated, `rebase` and `merge` not started, judged afresh
on the converted binding. `land` unknown leaves the "judged afresh" row for the active one: an outcome not established is reconciled
before any new attempt. Attempts move apart from states: every `land` attempt not settled, whatever its node's state (an active or
unknown node's, a stopped or invalidated node's `Unreconciled`), is carried onto `merge` as unreconciled, and the merge's settling,
run at the start before any pass, settles each by its delivery's phases before any converted node starts an attempt; nothing restarts
over an effect not settled.

**What h newly reads.** *The requests' invocation (N7).* Requests carry no invocation (`IntegrationStore.cs:64-67`), so a request open
at the upgrade that settles after it would be found by no attempt: the attempt withdrawn (`IntegrateStart.cs:234, 269-284`), the new
ask refused (`IntegrationRequests.cs:226-233, 405-414`), the node failed with its work on the line. The requests gain an `invocation`,
added by the desktop store's own schema step at its next version; the conversion writes, for each `integrate` attempt waiting on the
door (not settled with an outcome, its node active or unknown on it), the attempt's invocation onto the request `FindByKey(unit, tip)`
returns, which is the request that attempt adopted before h. A request no waiting attempt reads keeps none and is never adopted after.
*The binding's resolved `Land`* (`WorkflowRunBinding.cs:47`, resolved from the first `land` at `WorkflowRunBindings.cs:136-139`)
becomes `Resolved`, each node's open options as the bind resolved them, keyed by node id; the conversion writes the `land`'s under the
`merge` that keeps its id (its `pattern` as the target, its `key`) and its `plugin` under the plugin step its composition feeds, and a
bind after h resolves every node that leaves options open.

**Its place in the migration.** The conversion opens the desktop store (deliveries, requests, the journal), which `StoreNamesStep`
copies before its own open upgrades it, and reads the workflow files `WorkflowsStep` and `PushStepsStep` write and the bindings
`RunRebindsStep` writes; so §5's "reads nothing another step writes" is false. It stands last in its migration, after every step, and
keeps its own copy of each store and file before it opens it to write.

**The census, in three parts.** *Residue*: a home captured from the build before h (`fixtures/homes/pre-h/`, written by that build's
own runs through every `land` state of `NodeStates.All` and every attempt state, with an ended run, a held one, a request open, a
together operation landing, a branch delivery and a pull-request run) is converted, then every file and store row of it scanned: none
but the workflows' kept versions and the salvage names the kind `land` or a digest of a version holding one; red on a planted record
kind that keeps the bound digest and that the table omits, and on an ended run left unconverted. *States*: the table's rows over
`NodeStates.All` and `AttemptStates.All`; red on a planted state. *Reads*: each run of the captured home passed once after the
conversion, under h's readers: no node fails, withdraws, asks again or lapses where its record had an answer; red on N7's sequence with
the invocation left unwritten, and on a `merge` whose resolved options are missing.

## 17. Round 4: the selection census, over the comparers and the bound intent (§2.8)

**What compiles away.** h deletes `NodeKinds.Land`, `LandNodes` with `LandingOf` and `BoundLand`, `WorkflowProcess.Landing` and
`WorkflowRunBinding.Land`, so every reader of them is a compile error at h, found or not: the 42 comparisons of `NodeKinds.Land` in 23
sources, and the readers of the bound intent (`WorkflowProcesses.cs:118-141`, `IntegrateChoice.cs:37`, `GraphGate.cs:117`,
`WorkflowGate.cs:67-93, 356`, `TreeLandPlans.cs:91, 164`, `LandingPress.cs:130`, `OpinionGateLook.cs:237`,
`WorkflowGraphRun.cs:180, 367-370`, `TogetherAccepts.cs:133`). Each goes:

| Readers | Go to |
|---|---|
| `GraphRuns.AfterHanded`, `GraphStanding`, `WorkflowGraphRun` (the handed node and its defaults) | The data wires (§2.1): `RunSubject.Input`; the `merge`'s own options |
| `GraphGate` (the handed node), `OpinionGateLook` (the automatic accept) | The `merge`s the reviewed work feeds, by wire; the protected order |
| `WorkflowGate` (`JudgesHolds`, `LandsByRuntime`, `Land`, the pressed settings), `WorkflowProcesses` (no landing, the bound intent), `IntegrateCarry`, `IntegrateChoice` | `GraphMoves.Any`; the `merge` feeding each `integrate` its `ref`; a `merge`'s options with the binding's `Resolved` for that node |
| `TreeLandPlans`, `LandingPress` | The `merge` the press names, waiting on its attempt (§2.6) |
| `WorkflowNamed` (its kinds; a graph lands once) | A named graph holds a move; several `merge`s allowed |
| `WorkflowPresets`, `WorkflowDefaults.DerivedLand`, `WorkflowWords`' graph from words, `HelpWorkflowRoutes.HoldsLanding` | Every move (`GraphMoves.All`), the composition's `merge` naming its method and target |
| `IntegrateNode`, `PushNode` (`after` the `land`, their save rules) | Their `ref` inputs (§2.8) |
| `WorkflowRunBindings` (the first `land`) | `Resolved`, every node (§16) |
| `MigratedGraphs`, `PushedGraphs`, `WorkflowMigration` | Steps of the migration h's replaces, deleted with it; where h ships inside `nocompat1` they run before the conversion, which converts what they write |
| `Kinds/Land/` | Deleted (§5) |

**What would compile: a reader of the first move.** `GraphMoves` (`Workflows/Nodes/GraphMoves.cs`) is the one reader of moves by kind:
`Feeding(node, port)`, the `IMoves` producer a wire names (through `NodeEvaluations.InputOf`); `All(graph)`, every move in the graph's
order; `Any(graph)`. It has no first and no single. `MoveSelectionCensusTests`: `IMoves` and `NodeKinds.Merge` are named only by the
kit's declarations and save rules, `GraphMoves` and the conversion; `GraphMoves.All` is called only by the chart, `WorkflowPresets`,
`SetupPresets` and the save rules; no source outside the conversion writes `"land"` as a kind. Red on a planted
`graph.Nodes.First(node => kinds.Of(node) is IMoves)`, on a planted `GraphMoves.All(graph)[0]` in a new reader, and on a planted
`kind == "land"`. Beside it, over a release graph with two `merge`s, each reader asked its question answers for the `merge` its wire
names.

## 18. The reads answered (round 4)

Each finding of round 3's reads, its answer and its section; the proofs are in §7's rows.

**Codex, round 3.**

- **1, a deleted request branch strands an unchanged retry** (`DeliveryStore.cs:307-311`). Answer: the release of §12 records a
  delivered delivery's end once its removal's act closed done; `Start` takes the next generation from a proven end, the first row
  unchanged, refused while the act is open or unknown; an Undo takes the end back. The `integrate` writes no outcome until its branch's
  removal is proven. §12; proof f, and the sequence across a restart in h.
- **2, ended runs reopen** (`NodeRunKeeping.cs:96-100`, `WorkflowRunStart.cs:147`). Answer: every retained binding converted, its
  ended marker kept; a refused one rebound at its reopening. §16; proof h.
- **3, stopped and invalidated unmapped** (`NodeContract.cs:145`). Answer: both mapped, Stop kept on `rebase` and `merge`; `unknown`
  moved to the active row; every unsettled attempt carried onto `merge` whatever its node's state and settled before anything starts;
  the table keyed by `NodeStates.All`. §16; proof h.
- **4, deletes outside `JournaledDeletes`** (`GitRefActs.cs:86, 127`, the rename and its Undo, `TreeOpenings.cs:118`). Answer: a
  branch's removal staged only with `BranchRemovals`' release, every remover through it. §12; proof f.
- **The conversion census, its states and excluded runs.** Answer: §16's residue, states and reads parts.

**Opus, round 3.**

- **The records post-check, compound ids in backlog rows.** Row a's proof line (§7 a); §2.3's rule is row a's, stated as it is built.
- **The conversion census.** §16: the residue over a home captured before h, the states, and the reads h newly makes (the requests'
  invocation, the binding's `Resolved`).
- **The selection census.** §17: the comparers and the bound intent deleted, so the compiler finds every reader; `GraphMoves` with no
  first, and its source census.
- **The delete census.** §12.
- **N5, the hand-off holds the repository** (`ActReconciler.cs:53, 143-147, 164-177`). Answer: `TreeHandOffs`, a record read by tree
  through the trees' holders, never the journal. §13; proof d.
- **N6, nothing writes the together landing** (`TogetherAccepts.cs:116, 151`, `LandPerformer.cs:253-262`, `DeliveryReach.cs:47-55`).
  Answer: `TogetherBinding` holding the head's `rebase` until `ready`; the head's `merge` writing the attempt before its first phase and
  settling it by phase, the look's settling moved into it. §14; proof c and e.
- **N7, requests open at the upgrade carry no invocation** (`IntegrationStore.cs:64-67`, `IntegrateStart.cs:234, 269-284`,
  `IntegrationRequests.cs:226-233, 411-414`). Answer: the column, and the conversion writing each waiting attempt's invocation onto the
  request it adopted. §16; proof h.
- **N8, `merge` into a branch drops today's refusals** (`TreeBranches.cs:320-325, 362-390`). Answer: `LandingAdvance`'s facts read by
  the target's kind, a checked-out branch waiting and a completed one failing. §15; proof e.
- **N9, two askers to commit** (`TurnCarry.cs:26`). Answer: `CommitAskers`, one asker per tree, `TurnCarries` standing down where a
  `rebase` reads the tree and while a hand-off holds it. §13; proof d and h.
- **Not blocking.** `exists` against the release promotion: a fixed-name target asks no landing record (§15). Release's `behind` past
  `review`: `review` before `rebase` (§15). The landing provenance a branch `merge` writes (§15). The conversion's open of the store
  `StoreNamesStep` upgrades: last in its migration, its own copy kept (§16). `conflicted` when the branch's removal fails: the
  `integrate` waits, said, with no outcome (§12).
- **Rows.** The proofs found missing (a's compound rows; d's promotion and tree update beside a hand-off; e's together landing and a
  branch checked out or completed; h's pre-h request, the binding's options and every comparer) are in §7.

**What round 4 does not close by construction.**

- The release's read of the open runs and the branch's removal are not one step across the workflow store and git: a run that binds a
  branch between the read and the transaction finds it gone at its next pass and is held by its own problem, the commits on the
  recovery ref and the Undo putting the branch back. The guard reads; it does not lock a run's binding, which nothing here designs.
- The residue census reads what the build before h writes on the paths the capture drives; a record kind that build writes only on a
  path the capture does not drive is in no part of it. The capture drives every `land` state, every attempt state and the record kinds
  §5's census names, so it is as complete as those lists.
- `MoveSelectionCensusTests` is a source census: a reader that finds a move through a helper of its own, naming neither `IMoves`, the
  constant nor the word, passes it; the two-`merge` graph proves the readers that exist.
- `TidyHolders` is read by the doors that read it today; a door that writes a session's tree without reading the holders keeps off
  neither a session nor a hand-off, and round 4 adds no census of those doors.

## 19. Round 5: what it changes, and the code it rests on

Round 5 answers both makers' reads of round 4, read against the code at `4bef320159`. Each answer is placed where every member of its
class passes, so a member nobody has found yet meets it: one lock across a branch's ownership read and its removal, which a start's
bind and a run's reopening take too, and one gate every git Daoris starts passes, which refuses a branch's removal without a release
(§20); the run's own step read from the performance's flow, never passed by a caller (§20); `completed` a routed outcome the `merge`
acts on as the run's own step (§21); the together binding and every move ordered by the member tree's landing hold (§22); the
hand-off's tree kept at the same gate (§23); an adopted request recorded before it is relied on (§24); and censuses that run what
they census rather than only read its source (§25). §20 to §25 change the sections they name and hold where they differ (§10 lists
them); §7 is rewritten in place; §26 says, per finding, its answer and its section, and what stays open by construction.

## 20. Round 5: one lock across a branch's removal, and one gate every removal passes (§2.7, §12)

**The race (Codex 1).** §12's release read the open runs, then its transaction ran; git verifies the branch's commit, not who governs
it, so a start that bound a run to the unchanged branch between the two lost its work ref (`WorkflowRunStart.BindOneAsync`,
`WorkflowRunStart.cs:136-165`, writes the binding with no lock; `RefTransaction.RunAsync`, `:133-159`, verifies only the refs' values).

**One lock: the repository's `TreeLock`.** It already orders a start against a replay (`Workplace/TreeLock.cs`): one per checkout,
keyed by its common git directory, taken shared by every start and alone by every writer, never waited for, its handle the lock, so it
goes with its process. The release takes it alone: its read of the governed branches and the removal run under one hold, from the
read through the transaction and the act's close. A removal run as an act of the journal (the journalled delete) reads in the act's
judgement (`ActPlan.Judge`), which the act runner runs under the lock it holds alone before any effect; one run outside the journal
(a person's ref act, an opening's undo) holds its lock around its read and its transaction alike. The
writes that make a run govern a branch take it shared: the start's bind, moved inside the shared hold the start takes before it
chooses its tree (`TreeLock.TryStarting`); a run's reopening (`NodeRunKeeping.Reopen`, `:96-101`); and the take that names a unit's
tree. None waits: a release that finds the lock held is kept, said and asked again at its caller's next pass, and a bind or a
reopening that finds it held is carried to the next look, as every start held before its record is.

**Every governing write, under it.** `RunGovernance` (`Workflows/RunGovernance.cs`) is the one read of the branches the open runs
govern, and the release's only one. It reads three records, each declared as an input naming its writers: the bindings
(`IWorkflowRunStore.Bind` and `Write`), the runs' ends (any edit of a record that lifts `Ended`, which `NodeRecordFiles.Edit` compares
before and after) and the runs' units and parts (the take's tree record). Each of those writers asserts that its flow holds the
repository's `TreeLock`, shared or alone, and throws where it does not, so a writer added later meets the assertion at its first
test.

**Both interleavings.** Bind first: the release's read under the lock finds the run, and the removal is kept, said ("kept: run
`r-12` governs it"). Release first: the bind, under the lock after it, reads the branch gone and goes on as a start whose branch is
gone does today, its words naming the release's act and its recovery ref, whose Undo puts the branch back while the name is free; no
run governed the branch when it went. After a restart the lock went with the process and the removal's act is open in the journal;
its effect ran once or not at all, since no reconciler runs an effect again, so a bind before the act's reconcile sees the branch as
one of the two orders left it, and the reconcile closes the act by its postconditions, writing a delivery's end row for a done close
(§12).

**The gate every removal passes.** Every git Daoris starts is placed by the git gate (`GitScheduler.RunAsync`) on an ask read from the
whole start before it queues (`GitStartAsks.For`, `:26-41`: its repository, its wait, whether it writes, the tree a `worktree remove`
removes); `GitStartsCensusTests` holds every process start to its seam, and the gate refuses a git with no place. The guard sits there,
not at the callers. The ask gains the input the start is given (`RunGitAsync` and `ActLaunch.HeldGitAsync` hand it to the ask and
write the ask's input, never another) and `Unbranches`, the `refs/heads/` refs the start removes, read from its arguments and its
input by a closed table of git's write commands (`GitBranchRemovals`): `branch` with `-d`, `-D`, `--delete`, `-m`, `-M` or `--move`;
`update-ref -d`, `update-ref` to the zero id, and `update-ref --stdin` lines `delete` and `update` to the zero id, in either line
format; `push` to the repository itself with a delete refspec; `fetch` into `refs/heads/` with `--prune` or a delete refspec;
`symbolic-ref -d`. A command `RefSnapshot.MayWrite` reads as a write and the table does not class is refused at the gate ("what it
removes is unread"), so a git command new to Daoris's source fails at its first run until its row is written. A start that removes a
branch is refused unless its flow holds a release (`BranchRelease`, flow-scoped as the gate's other facts are) whose read passed under
the lock that flow holds, for that branch at the commit the start verifies; a removal that verifies no commit is refused.
`RefTransaction`'s refusal to stage a branch's removal (§12) goes: the gate is the one guard.

**The run's own step, from the flow.** `BranchRemovals.ReleaseAsync` takes no `by` for a run's step. `WorkPerformer` sets the
performance's authority (`StepAuthority`: run, node, attempt and invocation) over the door it calls, flow-scoped, and is its one
setter; the release reads it from the flow and exempts a governed branch only where that run governs it and the branch is that node's
own: a `tidy`'s chain parts by its input, an `integrate`'s request branch, a `merge`'s own target on `completed` (§21). The person, the
look and an opening name themselves by kind and are never exempt. An opening's undo removes the branch its own flow made a moment
ago, by the opening's authority, under the shared hold its start already has; no other flow can govern a branch recorded nowhere.

A governance ref verified inside each removal's transaction (`refs/daoris/governs/…`) would make git itself the exclusion, but it is a
second record of who governs a branch beside the runs' own, kept in step by every governing write; it is not taken.

**The censuses.** `BranchRemovalCensusTests`, run against the gate, red on each planted member: `update-ref --stdin` fed
`delete refs/heads/x <commit>` with no release (`RecoveryDropping.cs:95`'s shape); a hand-made
`ActStepPlan(ActStepKinds.Ref, {"input": "delete refs/heads/x …"})` run through `ActRuns`; `update-ref refs/heads/x` to the zero id;
`push . :refs/heads/x`; a release for `y` used to remove `x`; a planted write command the table does not class; and, inside run A's
performance, a release of run B's branch. `BranchOwnershipCensusTests`, red on a planted bind outside the shared hold, a planted edit
lifting `Ended` outside it, and a planted fourth input to `RunGovernance` that names no asserting writer. Proof: f (§7).

## 21. Round 5: a completed branch, ended by the run's own `merge` (§2.6, §15)

**The loop (B1).** In *Integrated*, step 1's `merge` fast-forwards P to C1 and R1 is admitted; step 2's quest keeps the run open; R1
promotes, and the integration's tidy of P is refused, since an open run governs P (`IntegrationPromotes.cs:528`); step 2's `rebase`
replays onto P, its `merge` fails `completed` to the person's task (`LandingAdvance.cs:31-50`), whose `done` returns to the `rebase`,
which replays onto P again. Nothing removes P: the person's delete refuses a branch a landing made (`GitRefActs.cs:210-213`), and
*Bring up to date* and the look's sweep are refused by the release (`TreeSync.cs:276`).

**`completed` is an outcome, and the `merge` acts on it.** A `merge` into a pattern whose branch's work is on the line by the content
proof, or whose pull request completed or was abandoned by the platform's kept word (§15's table), ends `completed`, no longer failed.
Before its outcome is written, the `merge`, as the run's own step (§20), ends the branch through the release at the tip its landing
record names: its recovery ref kept, its delivery's end row written (§12), so the next generation may make it again. `completed` then
goes `back` to the `rebase` its input comes from, as `behind` does; the `rebase`'s `onto: target` finds no branch of the chain and
reads the line (§2.2), replays only what the line lacks (step 1's work is on it), and the `merge` makes a fresh P, its next
generation, which its `integrate` asks for, or its `push` and the plugin's `pull-request` open a new pull request on. Today's words
name the same act ("then Accept lands this work on a fresh `feature/q-1`"), which the step now does.

**A removal kept.** Where the release keeps P (a tree has it checked out, an act on it is open), the `merge` writes no outcome and
waits, said with the removal's clause and read again at each pass, as §12's `integrate` waits; no way leads back to a `rebase` that
would read P again.

**Bounded.** `completed` ends one generation of P once; the fresh P holds work the line lacks and a pull request not completed, so a
second `completed` needs the line to take that work first. A `rebase` that replays nothing onto the line (step 2's work all on it) is
`ready` at the line's tip, and a `merge` fed a commit equal to its `onto` makes nothing and is `held`, so no empty P is made.

**A pull request completed before the next step.** In *A pull request*, step 1's `tidy` (`branch`) ends P once its `monitor` reads it
merged; where step 2's `merge` comes first, it reads `completed` and ends P itself. Step 1's `tidy` later releases P at its part's
recorded tip, at which the fresh P no longer stands: the transaction verifying that commit refuses, nothing is removed, and the part
reads as ended (`ChainTidied`).

**The save.** `merge-behind` refuses as well a `merge` into a pattern whose `completed` leads nowhere; every preset routes it `back` to
its `rebase`, and the editor adds that way with the `behind` one. Proof: e for the outcome and its way, f for the removal and the chain
(§7).

## 22. Round 5: a together member, kept from landing alone (§2.6, §14)

**The leak (B2).** `TogetherBinding` was read at the `rebase`'s start only. A member whose `rebase` had reached C_m (its `check`
running, or its `merge` not yet admitted) could be bound by the press, whose guards pass: no request open, no landing at C_m, the
member's tree tip compared (`TogetherPress.cs:229-279`). The `merge`'s judgement read no binding and took the git lock, not the tree's
landing hold the press takes, so C_m landed alone and the head brought it in again. Today the landing refuses a bound tree under its
landing hold, at the effect (`TreeLands.cs:141-152`).

**One hold orders the binding and the move.** The member tree's landing hold (`LandingHolds.TakeAsync`), which the press takes over each
member and a landing over its tree today, is taken by every move of a work before its first write. A `merge`'s judgement (§2.6) takes
the landing hold of its `work`'s tree, then the git lock, in that order, and reads `TogetherBinding.Of(work)` under it; a member of an
open operation is withdrawn there with nothing written, and its `rebase` then waits while it is bound (§2.2). Into the line the hold
spans the judgement and the request's admission; into a branch, the judgement through the `update-ref` and the record. The delivery's
prepared phase, every move's first write, is written only by a flow holding the work tree's landing hold with that read made under
it (the hold carries the read, and the delivery store asserts both), so a move written later meets it.

**The press refuses a member mid-delivery.** Under each member's landing hold the press reads `TogetherBinding.Delivering(work)`: the
member's run has a `rebase` reached at the member's tip whose commit a node reads that has not settled at that commit (its `check`, its
`merge`, any step the commit feeds), or the work has an unsettled delivery or an open request. Such a member is refused, said ("`s-42` is delivering: bring it in once its merge settles").
Whichever takes the hold first wins: the press binds and the `merge` withdraws, or the `merge` admits or writes and the press refuses.

**An unpromoted close is unmade.** `TogetherSettle.Decide` (`TogetherSettle.cs:45-55`), re-homed in the head's `merge`'s settling
(§14), reads a correlated request closed with any word but settled, whose member never promoted (`IntegrationStore.MemberOf` neither
promoted nor settled), as `Unmade`, whatever the line reads: the promotion is the line's one writer, so a request closed unpromoted
moved nothing. A restart between a close `behind` on a moved line and the `merge`'s read, and a pre-h together request that h's replay
closes `behind`, both go back to `ready` and free the members.

**Census.** A delivery's prepared phase written outside the work tree's landing hold throws: red on a planted `merge` path that skips
the hold. Proof: c and e (§7).

## 23. Round 5: a reserved tree, kept at the gate (§2.4, §13)

**The leak (B3).** The person's discard reaches `TreeRemovals` (`TreeTidies.cs:68-71`), which checks the tree's landing hold, the tree
lock, `TogetherGuard` and the active sessions (`TreeRemovals.cs:53-77`), not the holders or the hand-off; `worktree remove --force`
drops the staged resolution before §2.4 point 4 writes it to the edits ref, and the row stays open on a tree that is gone.

**The record read under the landing hold, not the hold kept for the hand-off's life.** A hand-off lives across turns and restarts. The
landing hold is a file handle that goes with its process: held for the hand-off's life it would be lost at a crash, exactly when the
restoration needs the tree kept, and every other taker (a press, a landing, a removal) would wait out a 30-minute ask and be told a
landing is under way, which N5 refused for the journal. The row is durable. So the hand-off writes its row under the tree's landing
hold, after reading the tree as §2.4 point 2 requires, and lets the hold go; `TreeRemovals` reads `TreeHandOffs.Of(tree)` under the
same hold, before the tree lock, and keeps the tree, said ("the tree at … stays: run `r-12`'s `resolve` holds it until 15:02; stop
that step to discard it"). The removal first: the hand-off finds the tree gone and its resolver takes a tree of Daoris's own (§2.4
point 2). The row first: the removal keeps the tree.

**At the gate as well.** Every write git (`RefSnapshot.MayWrite`) started in a tree an open row reserves, and every `worktree remove`
or `worktree move` of one (`GitStartAsks.Removed`), is refused at the git gate (§20) unless its flow carries that hand-off's own
authority (its `resolve` attempt's `StepAuthority`, for its preparation and its restoration). The resolver's own commands run in its
session's processes, not through Daoris's git, and do not pass the gate. A door not yet written that removes, switches or writes a
reserved tree without reading the holders is refused there, and across a restart, since the gate reads the row.

**`TidyHolders` reaches the home** through its reads' parameters: `HeldTreesAsync`, `HoldersAsync` and `BesideAsync` take the home
beside the service, as `InUseAsync` does (`TidyHolders.cs:16-59`), so every caller passes it, a compile error at each that does not;
`ReadHoldersAsync` adds the trees the open rows reserve (`TreeHandOffs.Reserved(home)`), each named "run `r-12`'s `resolve`".
`TreeHandOffs` is read by tree, by its reserved trees, and whole at the driver's start.

**The resolver's own turn.** A session's start in a reserved tree waits (§13), except the turn that is the hand-off's ask: the ask
carries its key (`resolve/<run>/<node>/<attempt>/<try>`), the row records it, and a start carrying that key in that tree is the
hand-off's own and not held. Any other start there, the person's word to that session included, waits until the row closes, said.

**Census.** Red on a planted door removing a reserved tree with `worktree remove --force`, on a planted `switch` in a reserved tree
(each refused at the gate), and on a planted holders' read with no home (it does not compile). Proof: d (§7).

## 24. Round 5: an adopted request, by its id (§2.10, §16)

**The loss (Codex 2).** The door finds an attempt's request by its invocation or adopts an open one for the unit at that tip (§2.10);
the store answers the newest request for that unit and tip whoever asked it (`IntegrationStore.FindByKey`, `:162-166`), and
`IntegrateStart.AnswerAsync` (`:199-240`) keeps nothing of which request it adopted. Once the adopted request closes, a restart looks
by an invocation that asked nothing, a closed request another asked is never adopted, the attempt is withdrawn, and a fresh ask can be
refused with the work already on the line.

**Recorded before it is relied on.** Adoption is one write of the door's store: the door's find, for an attempt whose invocation asked
no request, of an open request for the unit at that tip records the attempt's invocation as that request's adopter
(`integration_adoptions`: request, invocation, when) in the store transaction that reads it, and returns it; only then does
`AnswerAsync`'s step 3 read it onto the attempt, keeping its id (`NodeAttempt.Request`). An attempt's request is found by its kept id,
and before step 3 has kept one (a restart between the two writes) by its invocation, asked or adopted; never by unit and tip once it
is closed. `FindByKey` is called only by the adoption and the conversion.

**The request stays its asker's.** An adoption changes no member of the request: its requester, door, act and generation, and its
asking invocation. The promotion's authority re-read (`IntegrationAuthority.LapsedAsync`) reads the request's own requester (the
person's card, the steward's act), never its adopter. An adopting attempt withdrawn or stopped lets the request go (its adoption
closed) and never drops it: the person's stop of the node (`IntegrateStart`'s `StoppedBy`, `:215-219`) drops only a request its own
attempt asked.

**The conversion writes adoptions (§16).** For each `integrate` attempt waiting on the door at the upgrade, the conversion records an
adoption of the request `FindByKey(unit, tip)` returns and keeps its id on the attempt; it writes no invocation onto a request, so a
pre-h request keeps its requester and no asking invocation.

**A `merge`'s line request closed by the steward's drop or a decide.** A `merge`'s request is its attempt's own. Closed `dropped` (the
steward's drop, the person's `--drop`) or `handed-back` by a decide, the `merge` is failed, its words naming the act and who did it
("Dropped from `main`'s integration by the steward's act `a-12`"); the presets route a `merge`'s failure to their person's task, as an
`integrate`'s, whose `done` goes `back` to the `rebase`, which asks anew. A decide that lands settles the request, and the `merge` is
`merged`. Proof: e for the `merge`'s ends, h for the adoption (§7).

## 25. Round 5: censuses that run what they census (§13, §14, §16, §17)

**An act's callbacks, bounded by the act (`ActJournalCensusTests`).** An act's `Judge`, `Make` and `After` run with the act open and
the checkout held (`ActRuns.cs:195`), and nothing bounds them, so an `After` that awaits a session turn holds the repository across
turns while every step is bounded. The deadline becomes the act's: `ActRuns` journals one at the act's opening (its steps' bounds and
`ActRuns.CallbackBound` for each callback it runs) and runs each callback with a token cut at it. A callback still running then stops
the act there, as a step that did not end does (`:187-192`): its job's processes ended (a callback starts git through the act's
`ActLaunch`, in the act's job), the act left to its postconditions, its words naming the callback; a callback that goes on after finds
its claim closed, and `ActLaunch` starts nothing more for it. The census gains a run of every act kind's own tests under a recorder
that fails an act whose run outlasted its journalled deadline: red on a planted act kind whose `After` awaits a turn that never ends.

**The residue census, by the home's layout (`carried-accepts.json`).** A scan for `land` or a digest misses a record that names
neither. The census's members are the home's declared layout: every file name the driver writes under the home (each
`DaorisHome.Entry` with a literal name and each `FileName` constant, read from the source) and every table of the desktop store's
schema. Each has a row in the conversion's table: converted, kept as it is with why, or retired by h; the captured home's scan stays
beside it. Red on `carried-accepts.json` before its row, and on a planted `DaorisHome.Entry(home, HomeEntry.Sessions, "planted.json")`.

**N9's remainder: `CarriedAccepts` goes with `LandCarry`.** `SessionWaits.CarriesOn(…, accepted: new CarriedAccepts(home).Of(sessionId)
is not null)` (`Loop/DriverContinuations.cs:418-421`, `SessionWaits.cs:60-73`) still decides who carries a resumed session on. In the
composition the carried Accept is the review's acceptance across the composition (§2.4), so h deletes `CarriedAccepts`, its file and
the `accepted` parameter, and `CarriesOn` asks `CommitAskers.Of(tree)`, returning none where a run's `resolve` answers: the turn's end
and the resume of a went-on run have one asker. The conversion's row for `carried-accepts.json` retires it, each row's session's run
keeping its review's acceptance and the file's copy kept in the salvage; the say census (§7 h) is red on a reader of `CarriedAccepts`
left.

**Move selection, by invariance (`"landing"`).** The conversion keeps the `land`'s id on the `merge`, so `Nodes.Single(n => n.Id ==
"landing")` finds it naming no kind, constant or `IMoves`. Node ids are authored, and a reader that selects by one, or by place,
answers differently once the ids or the order change. `GraphInvarianceTests` run each shipped preset, the two-`merge` release graph and
the converted fixtures through their sequences twice: as written, and with every node's id renamed to a fresh one (wires and records
remapped) and the nodes listed in another order; every node's outcome, every effect and every word, mapped back, is the same. Red on
the planted `Nodes.Single(n => n.Id == "landing")`, on a helper returning the first `IMoves` in list order, and on
`GraphMoves.All(graph)[0]` behind a helper; `MoveSelectionCensusTests` stays as the source half.

**The orphan-write census, by execution.** Reachability matters: a write whose only caller sits on a path no sequence takes leaves the
record unwritten as surely as a write with no caller. A recorder (`RecordWrites`) wraps the stores whose write members §14's census
names (the together operation's `Landing`, `Correlate` and `Settle`, the delivery's phases, `landings.json`'s record and advance, the
press's outcome) through row e's and h's sequence tests, and the census fails on a member no sequence called. Red on the head
`merge`'s call to `TogetherOperations.Landing` removed, and on it moved under a condition no sequence meets. The source half stays as
the quick check.

## 26. The reads answered (round 5)

Each finding of round 4's reads, its answer and its section; the proofs are in §7's rows.

**Codex, round 4.**

- **1, ownership changes after the release** (`WorkflowRunStart.BindOneAsync`, `RefTransaction`). Answer: the repository's `TreeLock`
  held alone across the release's read and the removal, the start's bind and a run's reopening taking it shared, every governing
  writer asserting it. §20; proof f, both interleavings and each after a restart.
- **2, an adopted request needs a durable id** (`IntegrateStart.AnswerAsync`, `IntegrationStore.FindByKey`). Answer: the adoption
  written in the door's store before the attempt relies on it, its id kept on the attempt, reconciled by that id; the request's
  requester and authority unchanged. §24; proof h, adoption, closure, restart.
- **The censuses.** Records: compound ids on every row are row a's, unchanged. Conversion: by the declared layout (§25). Selection: by
  invariance (§25). Removal: the gate and the lock (§20). Orphan writes: by execution (§25).

**Opus, round 4.**

- **B1, a completed branch loops.** Answer: `completed` routed `back` to the `rebase`, the `merge` ending its branch through the release
  as the run's own step, a kept removal waiting, a pull request completed before the next step. §21; proof e and f.
- **B2, a member lands alone.** Answer: the member tree's landing hold taken by every move before its first write, `TogetherBinding`
  read under it, the press refusing a member mid-delivery. §22; proof c and e.
- **B3, a discard removes a reserved tree.** Answer: the row read by `TreeRemovals` under its landing hold, chosen over the hold kept
  for the hand-off's life, and every write or removal of a reserved tree refused at the git gate; `TidyHolders` takes the home. §23;
  proof d.
- **N9's remainder.** Answer: `CarriedAccepts` deleted in h, `CarriesOn` asking `CommitAskers`. §25; proof h.
- **The census leaks.** `BranchRemovalCensusTests`: the gate (§20). `ActJournalCensusTests`: the act's deadline over its callbacks
  (§25). The residue: the declared layout (§25). `MoveSelectionCensusTests`: invariance (§25). The orphan writes: execution (§25).
- **Not blocking.** `TogetherSettle.Decide`: an unpromoted close is unmade (§22, e). §2.6's request closed by the steward's drop or a
  decide: failed, to the person's task (§24, e). The resolver's own turn: exempt by its ask's key (§23, d).
- **Rows.** a to j stay in order; f proves the chain completed, c and e the member reached before the press, d the discard while a
  hand-off is open.

**What stays open by construction.**

- The gate holds the git Daoris starts. The person's own git, another tool's, and a resolver's commands in its tree do not pass it, so
  a branch or a tree removed by them is found gone and said; Daoris cannot hold what it does not start.
- The gate's table reads git's write commands as git documents them today. A command new to Daoris's source is refused, but a new
  way to remove a branch inside a command already classed passes until its row is rewritten.
- `RunGovernance` asserts the lock at the writers its three inputs name; a governing fact kept in a record it does not read is in no
  assertion until it reads it.
- The invariance tests and the write recorder see the sequences the fixtures drive: a reader no sequence reaches compares nothing, and
  a write called on a wrong path passes the recorder.
- The layout census reads literal file names; a file whose name is computed per session or run is classed by its folder's entry, not
  by its name.
- A door that writes a session's tree without reading the holders still writes beside a running session: the gate keeps reserved trees
  only, and the session's half is not designed here.

## 27. Round 6: what it changes, and the code it rests on

Round 6 answers the independent Opus reader's read of round 5 (the last section of the reads), read against the code at
`799e28c49e`; Codex's read of round 5 did not run. Each answer is placed where a member not yet found meets it: the run's own step
carried on each door's start and set where the door's work begins, and a release returned by the act's judgement, never set inside it
(§28); the reservation read inside the start's shared hold, which every session start takes (§29); one table of git's commands, total
by a census at test time, reading what each start drops and the trees and refs it touches (§30); one taker of the repository's lock
alone that reuses the flow's own hold, and no hold carried into the line's run (§31); and the census leaks closed at the writers, the
gate and the declared ids (§32). Two open defects of the land path, WFREVIEW3a and 3l, are answered by the `merge` kind's ends (§33).
§28 to §33 change the sections they name and hold where they differ (§10 lists them); §7 is rewritten in place; §34 says, per finding,
its answer and its section, and what stays open by construction.

## 28. Round 6: the run's own step, carried on each door's start (§20)

**The gap (N1).** §20 made `WorkPerformer` the one setter of `StepAuthority`, but it starts only `check` (`CheckNode.cs:38`) and
plugin steps (`PluginStep.cs:60`). A `tidy` is started from `NodeRunKeeping.StartDoors` (`:17-26`) through `TidyNode.Keep` (`:51-55`)
on its door, whose work runs in a `Task.Run` under `ExecutionContext.SuppressFlow` (`TidyDoor.cs:37-54`), its `TidyStart` (`:6`)
naming no run, attempt or invocation; `integrate` keeps its own attempts (`IntegrateStart.cs:147-165`, reached from
`NodeRuntime.cs:507`) and answers them at every pass (`:174-187`); `merge` is a door kind, as `land` is. So the run's own `tidy` at its
end node finds P governed by its own run and keeps it, `partly` at every pass, and the run never ends; an `integrate` waits at
`conflicted` for ever (§12), and a `merge` at `completed` (§21). And a release set flow-scoped inside `ActPlan.Judge` (`ActRuns.cs:130`),
an awaited callee, never reaches the effect steps its caller runs (`:185`): what a callee sets in its flow does not flow back.

**Carried on the start, set where the work begins.** `StepAuthority` (run, node, attempt, and the attempt's invocation where its kind
keeps one, else the door's start key) has no setter: a flow gets one only through `StepAuthority.RunAsync(authority, work)`, which runs
`work` under it. Its callers are four, each where a kind's work begins:

- **The kit's door runner** (`NodeDoorRuns`, under `Workflows/Kinds/`). Every door kind's start is a `DoorStart` carrying its
  authority, made by `NodeKept` from the run, the node and its active status (and on the pass by `NodeStarting`), so a door's start
  without one does not compile. The runner is the one way a door's work leaves the pass: idempotent by the start's key, refused while
  the drain says no, the pass's flow suppressed, and then the work run under `StepAuthority.RunAsync(start.Authority, …)` inside the
  task. `TidyDoor`, `LandingDoor` and `PushDoor` each keep their own copy of the first three today (`TidyDoor.cs:31-58`,
  `LandingDoor.cs:26`, `PushDoor.cs:28`); they become the runner's callers, and `rebase`, `resolve` and `merge` are written on it.
- **`IntegrateStart`**, around each attempt's answer (`AnswerAsync`), from its start and its reconcile alike, by that attempt.
- **`WorkPerformer`**, for `check` and plugin steps, as §20 has it.
- **`TreeHandOffs`**, for its resolver: the hand-off's ask and the session lock of its tree (§29).

**The release reads the authority current, and the kind's own declaration.** It exempts a governed branch only where the
authority's node is active on that attempt in its run's record, read under the lock, and the node's kind declares the branch its own.
Each kind that ends a branch declares what it ends (`IEndsBranches`, in its own folder): `tidy` its chain's parts by its input and, by
its follow, its work's branch; `integrate` its request branch; `merge` its own target on `completed`. The release asks the kind of the
authority's node, keeping no table of its own. So a door's task that outlives its node's stop is no longer exempt, and a kind added
later that ends a branch declares it there or meets the refusal, said with its own run.

**The judgement returns its releases.** `ActPlan.Judge` returns an `ActJudgement`, its refusal or the releases its reads made, and
`Make` returns the releases its steps need beside them; `ActRuns.RunAsync` runs the effect steps and the close under them
(`BranchReleases.RunAsync(releases, …)`), so the gate meets them in the flow of every step's launch. A release, like an authority, has no
setter, so a callee cannot set one for its caller, and the shape that lost it does not compile.

**The census, `StepAuthorityCensusTests`**, in two halves. Source: `StepAuthority.RunAsync` is called by the four alone, and
`ExecutionContext.SuppressFlow` and `Task.Run` under `Workflows/Kinds/` only by the door runner; red on a planted fifth caller and on a
planted door that runs its work through a `Task.Run` of its own. Execution: every `IStarts` kind of `NodeKit.Built` whose door is wired
is started on a fixture whose work reads `StepAuthority.Current`, equal to its start's; and every `IEndsBranches` kind runs its end over
a fixture run that governs the branch, which goes; red on a planted `IEndsBranches` kind whose door drops the authority (kept, said
with its own run). Proof: d for the setters and the source half, f for the ends (§7).

## 29. Round 6: the reservation read inside the start's hold (§23)

**The gap (N2).** §23 wrote the hand-off's row under the tree's landing hold, which no start takes. A start's exclusion is the
repository's `TreeLock` held shared, from before it chooses its tree (`GrowTree.cs:96-98`) until its record is open
(`OpenRecord.cs:122`), as `TreeLock.cs:13-17` defines it; and a start whose record is not yet open is no holder
(`TidyHolders.cs:62-83`). So the person's word starts S, which reads no row; the hand-off reads the holders, writes its row, detaches
the tree and rebases; S's agent runs in the tree mid-rebase, and at the restoration S's commits and edits go to the edits ref and the
abort resets the tree, so they leave S's branch.

**The reservation takes the start's exclusion.** The hand-off takes the tree's landing hold, then the repository's lock alone (through
the one taker, §31, its holder word `reserving`), reads the holders (the home passed, §23), writes its row (`reserved`) and lets both
go; only then does it prepare the tree. As a replay asks the ledger inside its hold (`TreeLock.cs:13-17`), whichever comes first the
other sees. A hand-off that finds the lock held writes nothing and is asked again at the next pass, said ("Waits: a session is starting
in `daoris`").

**Every start reads the row in its hold.** `TreeLock.TryStarting` names what it starts in (`StartIn.Tree(tree, key)`, `StartIn.Fresh`
for a tree it opens, `StartIn.None` for a hold that starts no session, as `TogetherGuards.cs:93`'s), and once it holds reads
`TreeHandOffs.Of(tree)`: a row whose key is not the start's gives no hold, said ("Waits: run `r-12`'s `resolve` holds `wt-3` until
15:02"). Its six callers (`GrowTree.cs:28, 96`, `OpenRecord.cs:27`, `ChatStarts.cs:201`, `GoingOnRun.cs:170`, `TogetherGuards.cs:93`)
each name it, and a start not yet written cannot take the hold without naming its tree; the service's opens (`OpenSessionAsync`,
`OpenSessionMovingAsync`) assert that their flow holds the start's hold for the tree they open in (the shared hold registered in the
flow, §31). A start's bringing up to date before its hold (`GrowTree.cs:90-93`) reads the row first, so it does not write a reserved
tree only to be held; its git would be refused at the gate in any case (§23).

**Both orders.** The start first: the hand-off cannot hold the lock alone while S holds it shared, so it reserves nothing; once S's
record is open the holders name S, and the hand-off does as §2.4 point 2 says of a tree a session runs in. The reservation first: S
cannot hold the lock while the hand-off holds it alone, and is carried to the next look; there it holds, reads the row and waits, said,
until the row closes.

**The resolver's own start and lock.** The row records the ask's key and, at `asked`, the resolver's session. The start carrying the
key is held by no row and runs under the row's authority (`TreeHandOffs.AuthorityFor`), so its `worktree lock` (`OpenRecord.cs:116`,
`SessionLocks.HoldAsync`, `:59-72`) passes the gate; the session's let-go (`SessionLocks.LetGoAsync`, `:78-84`) runs under the
authority the row gives that session while it is open. Both take the authority as a parameter, so each caller passes the row's or
none. The look's let-go (`LetGoUnheldAsync`) reads the reserved trees among the holders (§23) and leaves their locks, said; were it to
try, the gate refuses it. Proof: d (§7).

## 30. Round 6: one table of git's commands, total by a census (§20, §23)

**The gaps.** §20's table missed shapes that drop a branch: `update-ref -d HEAD`, which git follows to the branch HEAD names;
`branch -m <new>`, which renames the current branch, named by no argument; `symref-delete` and `symref-update` lines in
`update-ref --stdin`; a fetch that prunes by configuration; and overwrites that drop a branch's commits (`branch -f` and `-C`,
`switch -C`, `worktree add -B`). And it refused what Daoris needs: `MayWrite` reads every command it does not know as a write
(`RefSnapshot.cs:250`), so `merge-tree` (eleven sites), `diff-tree`, `diff-index`, `grep`, `count-objects` and `check-ref-format`, and
`notes`, `replace`, `submodule` and `maintenance`, would each be refused at run time until given a row.

**One table, three classes.** `GitCommands` (`Workplace/Git/GitCommands.cs`) is the one table of the git commands Daoris starts, a row
per command and, where git takes one, per verb. `RefSnapshot.MayWrite`, `GitBounds`' questions (`GitBounds.cs:25-31`) and
`GitStartAsks`' local writes (`:13-17`) read their columns from it, so a command gets its row once (D228). Each row classes a start,
read from its arguments, its input, the folder it runs in and the effective configuration:

- **Read**: nothing written but objects. `rev-parse`, `rev-list`, `log`, `show`, `diff`, `diff-tree`, `diff-index`, `diff-files`,
  `cat-file`, `ls-files`, `ls-tree`, `for-each-ref`, `show-ref`, `merge-base`, `name-rev`, `describe`, `blame`, `cherry`, `shortlog`,
  `grep`, `count-objects`, `check-ignore`, `check-attr`, `check-ref-format`, `var`, `version`, `status`, `ls-remote`; `merge-tree`,
  whose `--write-tree` writes objects no ref holds; `config`'s reading forms, `branch`'s listing forms, `symbolic-ref` with one name,
  `worktree list`; `notes list`, `show` and `get-ref`; `replace -l`; `submodule status` and `summary`.
- **Write**: refs, the index, a tree or configuration, never by its shape a branch's removal or a move that drops its commits. A
  commit, merge, cherry-pick, revert or `am` moving the branch HEAD names to a descendant; a branch, tree or tag created; a switch of
  HEAD; a `rebase` on a detached head (the `rebase` kind's and the hand-off's); `update-index`, `read-tree`, `add`, `rm`, `mv`,
  `apply`, `restore`; `worktree add` without `-B`, and `lock`, `unlock`, `move`, `remove` and `repair`, each with the tree it names;
  `notes` `add`, `append`, `copy`, `edit`, `merge`, `remove` and `prune`, which write under `refs/notes/` alone, where git places any
  notes ref; `replace` with a replacement, `-d` or `--graft`, which write under `refs/replace/` and change what every later read of
  those objects answers; `submodule` `update`, `init`, `sync`, `add`, `deinit` and `absorbgitdirs`, which write `.gitmodules`, the
  configuration and the submodules' own repositories; `maintenance run`, `gc`, `prune`, `repack`, `pack-refs`, `commit-graph` and
  `multi-pack-index`, which write objects, packs and refs that are no branch (`refs/prefetch/`); a fetch or a push whose destinations
  reach no branch here. `maintenance register`, `start`, `stop` and `unregister` write the person's own configuration and scheduler:
  their row says Daoris starts none, and a source naming one fails the census.
- **Drops**: may remove a branch or move it to a commit that does not hold its tip. The row names the branches. `branch` with `-d`,
  `-D`, `--delete`, `-m`, `-M` or `--move` (the old name, or the folder's current branch where only the new one is given, and the new
  one under `-M` where it stands), `-C`, `-f` or `--force` (the name overwritten); `switch -C`, `checkout -B` and `worktree add -B`
  (the name); `update-ref -d` and `update-ref` to the zero id, followed through a symbolic ref unless `--no-deref`, so `-d HEAD` names
  the branch HEAD names in the folder; `update-ref <ref> <new> <old>` of a branch where `<new>` does not hold `<old>`, which the gate
  reads before the start; `update-ref --stdin` lines `delete`, `update` to the zero id or to a commit not holding the old one,
  `symref-delete` and `symref-update` (the ref named, followed unless `option no-deref`), and a verb the reader does not know, read as
  dropping its first field's ref; `symbolic-ref -d`, and `symbolic-ref` naming a branch first; `reset` to a commit, `commit --amend`,
  `rebase` on a branch with its `--continue` and `--skip` (the branch its state names), `cherry-pick --abort`, `revert --abort`, and
  `pull --rebase` or `pull` under `pull.rebase` (the folder's current branch, where the target does not hold its tip); `filter-branch`
  (each ref it rewrites); `fetch`, `pull`, `remote update` and `remote prune` whose effective refspecs (their arguments, else
  `remote.<name>.fetch` and `remote.<name>.mirror`) reach `refs/heads/`, forced (`+`, `--force`, `--update-head-ok`) or pruned
  (`--prune`, `-p`, `--prune-tags`, `-c fetch.prune`, `-c remote.<name>.prune`, or the repository's or the person's configuration of
  those, unless `--no-prune`): every branch the destination's pattern matches; `push` to a remote that is this repository (`.`, or a
  path whose common directory is its own) deleting, forcing, mirroring or pruning a destination under `refs/heads/`.

A drop needs a release for each branch it names, at the commit it verifies, as §20's removal does; a drop that verifies no commit (an
`update-ref` of a branch with no old value) is refused. Daoris's own drops are few, and each carries its release: the journalled
delete, the person's ref acts, the opening's undo, a history edit's rewrite (its `Make` returning the releases, §28), the `tidy`'s
follow and the `merge`'s end on `completed`.

**Total by a census at test time.** `GitCommandsCensusTests` reads every git start in the source, each call of the gate's seams
(`WorkingTree.GitAsync`, `RunGitAsync`, `ActLaunch.HeldGitAsync`, `RefTransaction`'s verbs, `ActStepPlan`'s kinds): its command and
verb are literals, or constants of a declared set, each with its row; red on a planted `["frobnicate", …]` and on a start whose command
is a variable. Its run half: the tests run the gate strict, so a start whose command or verb has no row fails the test that starts it;
red on a fixture starting `notes add` with its row removed. At run time the gate refuses nothing for want of a row: the census is
total over what Daoris starts, so an unknown command fails the census, never a person's run.

**What a start touches, for the reserved tree.** The ask (`GitStartAsks.For`) reads from the table, beside `Unbranches`, the trees a
start touches (the folder it runs in, `-C`, `--work-tree`, and the tree a `worktree` verb names) and the refs it writes or drops,
wherever it runs. A row reserves its tree's folder and the refs it covers, the tree's branch and the node's edits ref; a write or a
drop touching either is refused unless its flow carries that hand-off's authority. Red on `update-ref refs/heads/<reserved branch>` and
on `worktree unlock <tree>`, each started from the checkout's root. Proof: f for the table, d for the reserved tree (§7).

## 31. Round 6: holds that never refuse their own write (§20, §22)

**One taker of the lock alone.** A second open of `TreeLock` in one process is refused (`TreeLock.cs:34-40`), so a flow that holds it
and asks again refuses itself. `TreeLock.Alone(home, checkout, repository, by)` is its one taker alone: it returns the flow's own
alone hold, borrowed (`HeldHere`, its dispose doing nothing), or takes it; `TryWriting` and `TryReplaying` become private to it. A take
alone in a flow that holds the same checkout shared throws, since at run time it would refuse itself. The start's shared hold is
registered in the flow as an alone one is (`LockWaits.Holding`), so `TreeLock.InFlow(home, checkout)` answers either, which the
governing writers' assertion (§32), the service's opens (§29) and the line's entry read. Census: no source outside `TreeLock` calls
`TryWriting` or `TryReplaying`; red on a planted direct take in a flow that holds the lock. So a release reached from a flow already
holding the lock (the `merge`'s end on `completed` under its git lock, a removal under an act's hold) reuses it, as `ActRuns` does
today (`ActRuns.cs:119-124`).

**A line admission that does not refuse its own promotion.** A press's admission runs the line inline today
(`IntegrationPresses.cs:38`), and the promotion takes the lock alone (`IntegrationPromotes.cs:365`), so a hold kept across the
admission would refuse it in this very process, as `TreeMerges.cs:166-168` says. The `merge` into the line holds the work tree's
landing hold and the repository's lock shared (the request's write governs a branch, §32) across its judgement and the request's
admission, writes the request, lets both go, and sends the line's run off the flow to the transport, as an `integrate`'s request is
sent (`IntegrateStart.cs:238`); it never runs the line itself. `IntegrationLines.RunAsync` asserts that its flow holds no `TreeLock` of
the repository, shared or alone, since its promotion takes it; a path that reaches the line holding one throws at its first test. Into
a branch the `merge` holds the landing hold and the lock alone through the `update-ref` and the record (§22), and reaches no line.

**The pool's opening undo, under the lock.** `TreeOpenings.UndoAsync` takes the flow's hold as a parameter, so each caller holds one.
A pool make or take reached from a start runs it under the start's shared hold, beside which no release passes, and the branch it made
a moment ago is recorded by no run. One reached with no hold (`TreePoolMakes.cs:55, 62`, `TreePoolTakes.cs:112`,
`PoolTakeovers.cs:117`, a takeover from the look among them) takes the lock alone through the one taker first; one that finds it held
leaves the claim at its phase for the pool's next look, as a takeover held does today. Proof: e for the line, f for the taker and the
undo (§7).

## 32. Round 6: the census leaks, closed at the writers, the gate and the declared ids (§20, §25)

**Ownership.** `RunGovernance` reads five inputs, each declared with its writers: the bindings; the runs' ends; the units and parts; the
branches an open run's `merge` made (the delivery store's rows of a branch delivery, and `landings.json`'s record and advance naming
them); and the requests an open run's `integrate` asked or adopted (the integration store's requests by their asking invocation and
`integration_adoptions`, §24, read by the attempt's key until h writes those). The assertion sits at each input's one writer and fires on
a write that changes what a run governs: the writer compares the record's governing fields before and after (a binding's run,
repository, workspace and units; a record's `Ended`; a part's tree and branch; a delivery's ref; a request's branch and asker), as
`NodeRecordFiles.Edit` compares `Ended`, and throws where the flow holds neither the repository's lock, shared or alone
(`TreeLock.InFlow`), nor the home's migration. The person's *Keep* (`WorkflowRunBindings.cs:192-198`) writes `Kept` alone, which no
release reads, so it passes with no hold, by what it writes. The migration's binding writes (`RunRebindsStep.cs:41`,
`BindingFilesStep`, h's conversion) go through the bindings' one writer (`WorkflowRunFiles.Write`, taking the migration's write as
its sink, so the salvage copy is still kept) under the migration's run, which holds the home's lease before anything reads the home
(`HomeMigration.cs:15-18`), so no start or release runs beside it. `BranchOwnershipCensusTests` reads each input's paths and tables
and finds every write of them in the source, each its input's writer: red on `RunRebindsStep.cs:41`'s `run.Write(path, …)` until it
is routed, on a planted sixth input with no writer, and on a planted governing write outside any hold; a *Keep* outside the lock
passes.

**Journal.** An `After` that starts git without awaiting it returns within its deadline, its git running after the act closed and
outside the lock. `ActRuns` runs each callback in a flow marked with its act's claim, and the gate hands a git started in that flow to
the act's launch (`ActLaunch.HeldGitAsync`), so it runs in the act's job; the act closes only once every git its callbacks started has
exited, bounded by the callback's deadline, past which the job's processes are ended as a step's are; a start in that flow after its
act closed is refused. A callback leaves the flow only by suppressing it, which §28's census forbids outside the door runner and
`MachineFacts.cs:538`. Red on the planted `After` starting `WorkingTree.GitAsync` unawaited: its git runs in the act's job under the
act's lock, the close waits for it, and one started after the close is refused.

**Residue.** The census's members gain the repository's own layout: every ref namespace Daoris writes under `refs/daoris/` (each
writer's literal prefix: the step refs, the act refs, the recovery refs, the edits ref) and every git configuration key it writes (each
key or section passed to `config` in a writing form, read through the table's `config` row). Each has a row in the conversion's table;
the captured home's repositories are scanned beside its files, every ref under `refs/daoris/` and every key in a Daoris section matched
to a row. Red on a planted `refs/daoris/land/<run>` writer and on a planted `daoris.land.<run>` key, each before its row.

**Invariance.** `GraphInvarianceTests` rename every id the graph's contract marks as one (the nodes', the wires', and any a later field
adds), read from the contract's schema rather than a list, and list the nodes and the wires in another order. Red on
`Wires.Single(w => w.Id == "d4")`.

**Recorder.** Each write member §14 names declares its writer by authority and asserts it: `TogetherOperations.Landing`, `Correlate`
and `Settle` throw unless the flow's `StepAuthority` names a `merge` of the operation's head's run (the operation records its head),
and the `merge`'s settling at the driver's start runs each attempt under that attempt's authority. The recorder keeps the authority
beside each call and fails a call from another writer. Red on a member's `merge` calling `TogetherOperations.Landing`, beside round 5's
red on the head's call removed.

**Reserved-tree gate.** Keyed by the trees and refs a start touches (§30). Red on a write to the reserved branch's ref and on
`worktree unlock <tree>`, each from the checkout's root. Proof: d for the journal and the reserved tree, e for the recorder, f for
ownership, h for the residue and invariance (§7).

## 33. Round 6: a press answered by its `merge`'s end, and a moved tip keyed by both tips (§2.6)

**The press's outcome (WFREVIEW3a).** Today a press that waits on the performer reads an in-memory table the performer fills only after
`trees.LandAsync` returns (`PerformedWaits.cs:13-26`, `LandOutcomes.cs:10-21`), so a closed land (already, gone, superseded or undone),
nothing to land, a gate's wait, a moved tip and a dirty carry-on keep nothing, and the press says *accepted; landing* where nothing will
land (`TreesLandDesk.cs:164-166, 188`). In the composition the press waits on the attempt of its `merge` it started (§2.6), and its
answer is read from the run's record, never from a table a performer fills: the `merge`'s settled outcome, or, while none it started
has settled, the node the run stands at with that kind's wait (§2), which the record always holds. Each end is one shape the desk words:
`merged`; `held`, already on the target or nothing to land (the `rebase` `ready` at the target's tip); `behind`; failed `exists`,
`moved` or a hook's refusal; `completed`; withdrawn by the judgement, naming what moved (the work's tip, the quest's submission, the
run stopped or superseded, the landing undone); a gate's wait (the `review` or `opinion` before the `merge`, on the person); the
`rebase`'s `uncommitted` with its `resolve` asking (the dirty carry-on, which `TreesLandDesk`'s `Carried` reads from that ask); and
`root-dirty`. *Landing* is said only while the `merge`'s delivery is prepared and unsettled, and a press past its bound answers with the
node the run stands at. `LandOutcomes` goes with `LandPerformer` at h.

**A moved tip, keyed by both tips (WFREVIEW3l).** Today a refusal at a moved tip is kept with the run's tip (`LandPerformer.cs:187`),
so a tree that moves T1→T2 and back to T1 with the same status is not tried again until a press. The `merge`'s judgement that finds
the work moved withdraws its attempt and records both tips on it: the tree's tip it read and the input's tip its start was made from.
The node starts again once the tree's tip differs from the one recorded: back at T1 it is tried again, and at T2 the moved input
rebinds it, as any does. Proof: e (§7).

## 34. The reads answered (round 6)

Each finding of the independent Opus read of round 5, its answer and its section; the proofs are in §7's rows.

- **N1, the run's own-step authority has no setter on an exempt path** (`CheckNode.cs:38`, `PluginStep.cs:60`,
  `NodeRunKeeping.cs:17-26`, `TidyNode.cs:51-55`, `TidyDoor.cs:6, 39`, `IntegrateStart.cs:147-165`, `NodeRuntime.cs:507`,
  `ActRuns.cs:130, 185`). Answer: the authority carried on each door's start and set by the kit's door runner inside the door's task,
  by `IntegrateStart` around each attempt's answer, by `WorkPerformer`, and by the hand-off for its resolver; the release reading the
  authority's node current and its kind's declaration of what it ends; `Judge` and `Make` returning their releases, under which
  `ActRuns` runs the effects. §28; the setters in d, the four ends proved in f.
- **N2, the reservation does not exclude a session start** (`GrowTree.cs:96-98`, `OpenRecord.cs:122`, `TreeLock.cs:13-17`,
  `TidyHolders.cs:62-83`). Answer: the hand-off reads the holders and writes its row under the landing hold and then the lock alone;
  every start reads the row inside its shared hold, `TryStarting` naming its tree; the service's opens assert the hold. §29; proof d,
  both orders.
- **The gate's table.** Total by a source census and a strict run at test time, refusing nothing at run time for want of a row; every
  shape found classed as a drop with the branches it names; each read command given its row, and `notes`, `replace`, `submodule` and
  `maintenance` classed by their verbs; one table for the snapshot, the bounds and the waits (§30). The pool's undo under the lock
  (§31). The resolver's `worktree lock` and `unlock` under its row's authority (§29). Proof d and f.
- **The census leaks.** Ownership: five inputs, the assertion at each one writer by what the write changes, the migration's writes
  routed through it, the census by the inputs' paths and tables. Journal: a callback's git handed to the act's launch, the close
  awaiting it. Residue: the repository's refs and configuration keys. Invariance: every id the contract marks. Recorder: the writer
  asserted by its authority. Reserved-tree gate: keyed by the trees and refs a start touches. §30, §32.
- **Self-refusals.** The line's admission letting every hold go before the line runs off the flow, the line's entry asserting none;
  one taker alone, reusing the flow's hold. §31; proof e and f.
- **Not blocking.** *Keep* passes the assertion by what it writes (§32; proof f). The reserved tree keyed by its refs (§30; proof d).
- **Rows.** a to j stay in order; N1's setters in d; f's assertion ships with *Keep* and the migration's binding writes handled; d
  proves a start racing the reservation, e a line admission that does not refuse its own promotion, f the run's own `tidy` at its end
  node, a release read in `Judge` reaching its effect step, *Keep* and the pool's undo.
- **WFREVIEW3a and 3l**, open defects of the land path h deletes, are answered by the `merge`'s ends. §33; proof e.

**What stays open by construction.**

- The gate holds the git Daoris starts; the person's own git, another tool's and a resolver's own commands in its tree do not pass it,
  so what they remove is found gone and said.
- The table reads git's commands as the git Daoris supports documents them; the census makes it total over the commands and verbs Daoris
  starts, not over an option a later git adds to a command already classed.
- A configuration changed between the gate's read and git's own (prune set in that moment) is read as it was; the window is the
  start's own.
- A session started outside the driver (the person's own agent in a tree) takes no hold and reads no row: the reservation keeps
  Daoris's starts.
- A kind that ends a branch without declaring it is refused its own branch, said: the gap shows as a wait, never as a loss.
- The invariance tests and the recorder see the sequences the fixtures drive, as round 5 said; the recorder's writer assertion runs in
  every run, not only those.
