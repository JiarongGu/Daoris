# Generic process steps, landing only a composition of them (PROCSTEPS1)

**Proposed design, round 3, documents only** (2026-11-09). The owner's direction: a workflow's steps are each declared in one focus; a
step may do a lot, but today none says what it is focused on, and the landing has no rebase of its own. Then, twice: Daoris runs a
workflow system, so the steps are generic kinds any process composes, and *landing* is a composition at the workflow level, never
a step in code. Round 1 (the document at `0422e84b1b`) and round 2 (at `fd03c57109`) were each read by both makers
(`docs/2026-11-09-process-steps-reads.md`); round 3 answers round 2's reads in place, and states the integration against its code
(§2.6, §2.10). §9 says, per finding, the sequence it closes, the code the answer rests on, its proof's row and the census or test
that closes its class; §10 keeps the passages rounds 2 and 3 replaced. Read with `docs/areas/workflows-review-landing.md`, the node
kit design (`docs/2026-11-06-node-kit-design.md`, every kind one unit under `Workflows/Kinds/<Kind>/`), the land node's design
(`docs/2026-11-01-land-node-design.md`), D215 (the integration), D228 and D232. Paths are under
`src/Daoris.Desktop/Daoris.Desktop.Driver/` unless they name another root. The code was read at `2d28c2f168` and `0422e84b1b`; the
integration, the acceptances, the readers and the deletes again at `56d8498990` for round 3.

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
  sets apart the joins union glued (`UnionJoins`, today's) and keeps once a non-blank line both sides added word for word, which
  `tools/doc-duplicates.mjs` would refuse on the line; no other repeated line is touched.
- **Rows and notes merged by their own drivers** (`docs/README.md merge=rows`, `docs/decisions/*.md merge=notes`): git runs the
  driver the repository's setup configured (`npm ci`); where it is not configured the file conflicts as text and is not settled.
- **Generated files** the repository marks generated: the target's copy, written again by the writers the person accepted, in a
  commit on top; a writer that fails refuses the stop (today's `GeneratedPaths`, `GeneratedWriters`). `daoris.lock` (`-merge`) is
  generated: regenerated by its writer where one is accepted, else not settled.
- **Files of an accepted line rule**: its pattern, then its writer (today's `LineRules`).
- **The declared backlog and row tables, by their rows' ids**, the rule row a builds (being built), stated as it is built:
  - A row is a line whose first bold token is an id (`- [ ] **PROCSTEPS1 — …`). Ids match exactly (`PROCSTEPS1` is not
    `PROCSTEPS1a`), and an archive line's compound ids are parsed into each id they name (`WFREVIEW3a, 3d…`, `GITLIB1a to GITLIB1d`).
    Lines that are no row merge by the merge base, line by line, and in the backlog's merge two sides' inserts at one place conflict
    unless they are equal (`BacklogMerge.Collide` let both stand).
  - For each id, base against the target and the work: a row only one side added, removed or rewrote takes that side; a row both
    sides changed to the same text, after normalising whitespace, is once.
  - **A row one side archived** (removed from the backlog, its own archive adding a line that names the id) leaves the backlog only
    where the other side's copy is unchanged from the base after normalising whitespace (line endings, trailing and repeated
    spaces), or only moved. Any other change the other side made to it, a word removed as much as a word added, is `conflicted`,
    both texts in the problem: removing *not* from "Do not enable A" turns the requirement round, and meaning is a resolver's to
    read (D215 point 6), never the driver's.
  - **A row both sides rewrote differently is `conflicted`**: no row's words are merged, word by word or otherwise.
  - A row one side deleted with no archive line while the other rewrote it, and one id both added with different texts, are
    `conflicted`.
  - **The post-check**, after every replayed commit whether git stopped or merged it clean: each id stands once in the backlog;
    none stands both in the backlog and newly in the archive; and every id that left the backlog is held to the archived row's test
    against the other side's copy, clean merges included, so a clean merge that let an archive win over the line's change to that row
    is `conflicted`. A result that fails it is settled by the rules above where they settle it (the line moved a row the work
    archived: it leaves) and is `conflicted`, naming the id, where they do not. An input already holding an id twice is not settled
    by ids: the stop is `conflicted`.
  - The twin rule holds: `tools/merge-branch.mjs`'s `mergeByBase` and the driver's `BacklogMerge` change together, row for row of
    `fixtures/backlog-merges.json`.

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
- **Asked again at the same tip**: the request closed `conflicted`, and the line does not hold `against`: what the member met never
  reached the line (its candidate failed, or formed again without it), and a rebase onto the unmoved line would make the same commit.
  The `integrate` asks a new request at the same tip, in the same attempt, once per ended candidate (keyed by its invocation, the tip
  and that candidate, so a restart asks once), at most `retry.maxAttempts` (default 3) times for a tip; past that it waits on the
  person, said ("Waits on you: handed back 3 times for work that never reached `main`"), and *Try again* asks once more.
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

1. **PROCSTEPS1a, the records by their rows' ids** (driver, tools; waits on nothing; being built): §2.3's backlog rules and
   post-check in `BacklogMerge` and its twin `mergeByBase`, union's keep-once for a non-blank line both sides added. It serves
   today's landing, the integration's member merge and the tree's update at once. Proof: `fixtures/backlog-merges.json` rows run by
   both twins, one per rule: an archive against a row unchanged but for whitespace and one only moved (each leaves), and against a
   word removed and a word added (each `conflicted`); two different rewrites; a delete with no archive against a rewrite; one id
   added twice; two inserts at one place, equal and not; exact ids beside a longer one; compound ids by list and by range; an input
   holding an id twice; the post-check over a clean merge that kept an archived row and over one that let an archive win over the
   line's change. Over real git (`LandBacklogLandingTests`, failing first): a replay whose row the work archived while the line
   moved it lands with no session asked, and one whose row the line changed stops naming the row with both texts.
2. **1b, values on the wires** (driver): `work`, `commit` with its `onto`, `ref` with its producer, `problem` with its producer and
   stop's identity; `claimed`'s `work` output; `RunSubject.Input`; step refs per attempt and their keeping; the bound on a target's
   move; a run's own move as its binding's revision; a skipped producer's readers; the arriving mark. (A `back` wire on an outcome is
   the graph's already, `NodeWires`.) Proof: a moved input rebinds only what it feeds; `RebindBoundTests`, over every kind with a
   target, its target moved while each node it feeds is active or reached rebinds nothing, and moved while none is rebinds it; a
   recorded own move followed by a pass lapses nothing; a step ref an attempt reads survives a restart and `git gc --prune=now`, and
   goes at the run's settled end; a save holding an arriving kind refused.
3. **1c, `rebase`** (driver; its words): the kind, arriving; its door; the replay-and-settle unit moved into its folder with its
   other callers; `onto: target`; its start, waiting while its work is bound to an open together operation; the inclusion record's
   leaving out; its reruns. Proof: one test per outcome over real git; a chain's step 2 rebased onto the chain's branch replays only
   its own commits and fast-forwards with the line moved meanwhile, and a squash chain the same (`TreeBranches`' advance cases, run
   through the kind); 2026-11-09's sequence (three works archiving their rows one after another while the line moved) reaches
   `ready` with no session asked where the line left the row as the base had it, and `conflicted` naming the row where it changed
   it; the person's checkout, the work's branch and its tree unchanged byte for byte; a stop mid-replay leaves no scratch tree; a
   session mid-turn holds it `ready`, said; a member bound to an open operation never starts, and once the operation landed a
   member whose commits the head replayed as new ids, and one whose line the head reverted, replays nothing.
4. **1d, `resolve`** (driver, modules; its words): the journalled hand-off and the reserved tree, the ask, the continue with the
   settling, the proof, the tries per stop, the restoration with the resolver's HEAD on the edits ref, the `uncommitted` problem with
   its blobs, the carried Accept, `rebase-tail`, the card's wait. Proof: a code conflict asked once with its files and hunks and
   resolved; a four-stop replay asking each stop as its own try 1; a records stop after it settled with no ask; the line moving 50
   times while it waits making one ask; a stop met again after a rebind going on from its count; the bound spent failing to the
   person's task, said on the card, and that task's `done` returning to the rebase; a resolution touching an unnamed file refused; a
   session the person stopped never asked; a timeout with the resolver mid-edit, a lapse, the person's stop and a restart with the
   hand-off open, each ending with the resolver stopped and awaited first, the tree on its branch at its tip, the edits on the edits
   ref and applied at the next ask, the entry closed; a resolver that commits, and one that runs `rebase --continue`, then times out,
   its commits on the edits ref; an `uncommitted` resolution committing exactly the problem's blobs keeping the review, and one
   changing another path asking it again; nothing else taking the tree while it is reserved.
5. **1e, `check` fed, then `merge`** (driver; their words). First `check`'s `commit` in and out, so `merge`'s proofs run through it:
   a check on a rebased commit, its failure stopping the `merge`, a target moved while it runs rebinding nothing. Then `merge`: the
   methods and targets; into the line, the request at the one door with its expected tip and invocation, never batched, `behind` at
   the replay, the promotion's compare-and-set; into a branch, the `update-ref` compare-and-set; the delivery and its ladder re-homed
   with `LandAttemptSettling`, a line delivery's phases read from its request; the authority and the judgement before the effect, read
   again at the promotion; `behind` and `merge-behind`; `root-dirty`; the together re-read; the press waiting on its attempt. Proof:
   each method onto the line and onto a branch; for each method onto the line, the line moved between the check and the admission,
   between the admission and the form, and between the form and the promotion, and another epoch's request queued beside it: `behind`
   or formed again, never batched, nothing unverified lands, and through the evaluator back to `rebase` and `check` and on to one
   merge; for each method onto a branch, moved before the judgement and at the `update-ref`: `behind`, nothing written; each currency
   read (the work's tip, the quest's submission, the step ref, the run stopped) moved between the admission and the promotion: the
   member lapsed, nothing moved, the `merge` rebound; a stop at each phase ends in one delivery; a press and a pass merge once; a
   squash's and a merge commit's tree equal to the input's; a two-member together operation in one delivery, and a member moved
   since it was bound refused with nothing written; a save of a `merge` whose `behind` leads nowhere refused.
6. **1f, `tidy`** (driver; its words): `follow` writing the run's own move, `branch`, `tidy-held`, the step refs left to the run's
   end, the journalled delete refusing an open run's branch to every caller but that run's own steps. Proof: a free tree follows and
   the pass after it lapses nothing, the review's acceptance included; a running tree says why it stays; a pushed branch never
   rewritten; a squash run with a `branch` tidy and `git gc --prune=now` between every pass finds every commit a later step or an
   unsettled attempt reads; `BranchDeleteCensusTests`, the delete's callers exactly §2.7's; each caller over an open run's branch
   keeps it, said with the run, and takes it once the run has ended; the run's own `tidy` ends it.
7. **1g, the editor, the chart and the run view** (web, contracts, and the declarations h wires): the `Git` group, each kind's
   inspector and inputs, the `rebase` tail and the `merge`'s `behind` way added in one change, the chart's ports and `back` ways, the
   run view's words and waits, in English and Chinese, the generated contracts and types; and what h adds, declared here arriving as
   the kinds are, so the contracts are generated once: `integrate`'s `ref` input and its `conflicted` outcome, its same-tip and
   decide waits, `push`'s `ref`, `line`'s `commit`, a `merge`'s wait in the line's integration. Drawn and tested on the arriving
   kinds and ports, offered at h, so the cut-over ships nothing the window cannot draw. Proof: page tests, the web typecheck,
   `i18n:check` and `names:check -- --strict`, stories at 80, 39 and 25rem in both themes.
8. **1h, the cut-over** (driver, modules; web for *rebase it*'s retirement): the shipped graphs and `WorkflowPresets` recomposed by
   §3, `SetupPresets` by the `merge`; §5's conversion; the readers bound to their producers (§2.8); the integration by §2.10 (the
   request's invocation, the `conflicted` close and `against`, the same-tip asking, the request branch's end, nothing told, no
   disposition for a run's request, *rebase it* retired with its route verb and the page's button); `TurnCarries` re-homed onto
   `WorkTreeReads` and `resolve`'s commit words; the `land` kind, `LandPerformer`, `LandTries`, `LandCarry`, `RebaseTelling`,
   `LandingRules`, `IMakesLanding` and the arriving mark with its refusal removed; the press waiting on the `merge`; the area
   document rewritten. Proof: every shipped file judged by the save; §5's table over every `land` state and every preset, a custom
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
   request; `NodeKitTests` with no `land`; the census of doors that land.
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
  the work's `onto`; otherwise asked again at the same tip, once per ended candidate, at most 3 times, then the person; a gate's or a
  decide's hand-back, or a drop, is failed. Proof: h, the same-tip sequence and a gate hand-back. Class: no attempt can find a closed
  request it did not ask, and the outcome is read from what closed the request.

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
