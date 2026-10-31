# GITAPP2: the person's git acts and writes, round 3 (2026-10-31)

**Standing:** accepted 2026-10-31 after the named changes of both round-2 reads (Codex; the Opus foundation reader), the steward's settlement under D191; Codex confirms the named changes.

Codex returned rounds 1 (`67d806f2c`) and 2 (`7c7bae349`) for another round; the Opus foundation reader accepted round 2 after named changes (`docs/2026-10-31-git-writes-read-codex.md`). Round 3 (D204) takes every named change into its text. `W/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/`, other bare paths under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; lines at `e06076141`.

## 1. What it rests on

Only what is built: GITAPP1a-i's counting seam (`W/GitLaunch.cs`), 1a-iii's runners that await a killed process's exit or say `git.unended` (`Platform/ChildProcess.cs:82-104`), REVIEWLOCK1's flags, GITTXN1; not GITAPP1's part two, whose reader, refs observation, cache and watch are unbuilt (`W/ReviewGraphReads.cs:18`).

- **Fresh reads**: every plan and re-judgement read starts through the seam as written, outside a landing's or judgement's memo (`W/WorkingTree.cs:1067-1073`), nothing kept across opens; the plan holds the ids and byte digests it showed, the re-judgement compares.
- **No watch**: `RefWatch` hears no tag, stash or index change and watches only repositories holding Daoris's work (`W/RefWatch.cs:229-241,249`); the Changes tab reads on open, focus, each act and *Refresh*, saying when.
- **Floors**, read at the plan: 2.40 for history edits (`merge-tree --merge-base`), 2.43 for an act resting on absence (`show-ref --exists`); below, not offered.
- **Configured programs**: a history edit's `merge-tree` does not start where configuration, read at the plan and under the lock, defines `merge.<name>.driver` or sets `merge.renormalize` beside a filter. Staging patches come from `diff --no-ext-diff --no-textconv --full-index`, applied with `--whitespace=nowarn`.

## 2. Where the working-tree acts live

The person's registered checkout, behind the switch *Daoris may change this checkout's files*, off by default, in Setup's **Reach** (D150 point 3), named in every plan; on, it says when sessions already run there. Rejected: a manual tree (none of the person's edits are there) and an idle session tree (D51, D164). A session's tree is refused whatever the switch says.

## 3. The checkout exclusion (GITLOCK1)

**One lock per checkout**, keyed by its canonical common directory, not `TreeLock`'s workspace and name (`W/TreeLock.cs:43-44`), so a checkout registered twice has one; otherwise `TreeLock`'s pattern: the handle is the lock, let go with a dead process's handles (`:19-20`), never waited for.

**Alone**, from fresh judgement through effect and reconciliation, by every writer: D109's line pull (`W/LinePulls.cs:51-79`, today before any lock, `W/TreeSync.cs:209,220`); the integration promotion, from `EffectAsync`'s first read (`W/IntegrationPromotes.cs:470`) through the line's move (`:490-504`), so the legacy merge press (`W/TreeLands.cs:299`) and background integration take it unnested; every act here.

**Shared** by every session start in the checkout, driven or conversation, until its record opens and the ledger holds it; none does today (trees opt-in, `Settings/DriverConfig.cs:1485-1486`; a driven start, `Loop/Driver.cs:2124`, `:2464`, `:2747`; a conversation's, `Loop/Chat/ChatRunner.cs:671`, `Course/Chat/ChatGoOn.cs:751`). A start finding the lock held waits for the next look; it and every refused writer name the act and the repository's page.

**Beside a session**, the pull and the promotion today fast-forward a clean checkout an agent runs in (`W/LinePulls.cs:55-75`, `W/IntegrationPromotes.cs:490-495`); GITLOCK1 defers both while the ledger holds one there.

**No waiting acquisition while the lock is held**, so no deadlock: under it every lock is tried, only git's taken, none re-entered; integration claims, `TreeLock` and `IntegrationLines`' semaphore (`W/IntegrationLines.cs:89-90`) come before it; the process-start gate holds no lock.

**Under the lock, before any effect**, fresh: the switch; the checkout's identity (top, common directory, registered); any session the ledger holds there (running, waiting or held to go on), or a ledger unread, refusing; an operation in progress (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `rebase-merge/`, `rebase-apply/`, `BISECT_LOG`); git's lock files (`index.lock`, `HEAD.lock`, `config.lock`, `packed-refs.lock`, a ref's), refused and named with the command to copy, never removed by Daoris; `HEAD`; the touched index entries (`ls-files -s -z`) and bytes; open act records, reconciled first (§5). A difference refuses, named. Only these reads meet writers outside any lock, named in the census: the person's IDE and terminal, the Node CLI's offline doctrine sync, a session's writes once its record opens.

**Refused**: a hard reset, `clean`, any force, a recursive submodule write, a merge or rebase of its branch, any act while a session is held there or another writer holds the lock.

## 4. The Changes tab's read

Three fresh observations under REVIEWLOCK1's flags: HEAD → index (`diff --cached HEAD`), index → worktree (`diff`, unrefreshed), status's untracked and unmerged entries (`--porcelain=v2`), each with mode and blob id; a staged addition with a matching unstaged reversal shows in both. Stage applies the index → worktree hunk or line (`apply --cached`), Unstage the HEAD → index hunk reversed; at the press the entry's blob id and the file's bytes equal the hunk's source.

## 5. The act journal (R2-15)

**A desktop-store family** (D212 point 17: machine-local, never synced) on the integration store's claim and step shape (`W/IntegrationStore.cs:18-24`): claimed by its run and a generation, taken only from a dead run, every write fenced; a step's start written before its effect, its end after. Replaced bytes are files beside it.

**Fields**: id, kind, the press (window or terminal, its Confirm); repository and checkout identity; the plan (refs and ids shown, touched entries and digests, hooks, the switch); claim and job; per effect step its arguments, postconditions, rollback, deadline (start plus `GitBounds`), the child's pid and start time, exit or *unended*; the recovery (refs, index entries, sections with file and scope, replaced bytes); the outcome.

**Phases**: *judged* (fresh reads equal the plan) → *prepared* (recovery written, job made) → *started* (with its deadline, before each step's process) → *ended* → *reconciled* (postconditions read: *done*, *not done*, *unknown*) → *closed*; then the lock goes.

**Contained, bounded.** Each effect step starts suspended and joins the act's job (`ProcessJob.CreateNamed`, `Platform/ProcessJob.cs:83,105`, as HEAVYRUN1a's gates) before it runs; the job ends its hooks and children with its last handle; a step not joined is not run. The job's name finds a live step without its pid; a job that will not open is held live. Every git keeps its `GitBounds` (`W/Domain/GitBounds.cs:19-22`), past which its tree is ended and its exit awaited (`Platform/ChildProcess.cs:73-104`). Cancellation stops an act only before its first effect step; effect steps run under `CancellationToken.None` and their bound (`W/ContentHolds.cs:419`). No exit code, bound or kill proves the effect: postconditions do; an unended process makes it *unknown*, never *failed*.

**Ownership.** Window presses and routed terminal verbs run in the desktop's driver; a closing window cancels only a wait before the effect. The application's exit waits out a running step's bound, saying so; the update's drain and relaunch count it busy (`Install/InstallUpdate.cs:70`). A standalone verb hands its act to a worker, `daoris-driver` started apart from the terminal's console and job, holding claim, job and bounds to *closed*. Its Ctrl-C, wired in the `git` route (`src/Daoris.Desktop/Daoris.Desktop.Driver.Host/Program.cs:440`, before the host's `:724`, `:893`), cancels only before the first effect step; after, Ctrl-C or closing the terminal ends only the watch. A worker that cannot start apart refuses the act.

**Reconciliation**, at the driver's start and by every writer taking the checkout lock, before its judgement and outside its memo scopes (`W/WorkingTree.cs:1071-1072`; a sync's look, `W/TreeSync.cs:188`): a record not *closed* whose run is dead is taken, one reconciler winning. A live step refuses every writer; past its deadline the reconciler ends the job and awaits it empty (`Platform/ProcessJob.cs:260`), else says *unended* with the pid and a *Stop*. Else postconditions decide. At the driver's start it never holds the come-up (D181 point 1): one checkout at a time, one it cannot take left to the next look. *Unknown* is said on the repository's page and where pressed, with what is kept and the command to copy. **Nothing is re-run**: no hook, commit, push or configuration write. A rename stopped between its ref and section steps offers *Finish* and *Undo*, each conditional.

**Configuration and index move under git's own lock**: Daoris creates `config.lock` or `index.lock` exclusively, as git does, compares the journaled section (its file and scope; one elsewhere refuses) or entries, and on a match writes the replacement (an index built on a copy) into the lock and renames it over the file; a mismatch removes only its own lock and refuses, recovery kept. A rename's section moves, or back, where the source equals the record and the target is absent; a deleted branch's section, today removed unconditionally (`W/ContentHolds.cs:434-435`), goes only as journaled and returns where none stands.

## 6. Ref acts (GITTXN1, GITTXN2)

**GITTXN1 as built** (`W/RefTransaction.cs:68-168`): one operation per ref with its expectation inside, `Verify` for refs read and left, identical expectations folded; a name no full ref, a case alias, two acts or values for one ref and a symbolic ref refused before git; conditional rollbacks (`RestoreDeleted` where absent, `RestoreRewritten` from the produced tip, `ReverseRename` checking both names, a create deleted at its id). The judged delete runs on it (`W/ContentHolds.cs:421`, `:484`).

**GITTXN2 first**: exit -1 (bound reached, unended, never started), said today as refused and *nothing moved* (`:127`, `:164-168`), becomes *git did not answer*, read ref by ref before anything is said; an alias is refused against the refs that stand (`:70-76`). A refusal is read (absent only on git's *missing*); an unmet condition is reported uncertain, recovery kept.

**Recovery namespaces.** Deleted branches, tags and old tips go under `refs/daoris/discarded/` (`W/ContentHolds.cs:61`): its retention, its *Kept for recovery* list, its holder exclusion. Only index snapshots and Resolve's stages (§8) go under `refs/daoris/acts/<act>`, which holds nothing: it joins `ContentHolds.AnyRef`'s exclusions (`W/ContentHolds.cs:377-378`, read at `W/TreeStrays.cs:215`), and `LandedCleanups.WentAsync` never counts it (`W/LandedCleanups.cs:702-713`). An act ref expires in one transaction, a `delete` at its journaled id.

**Occupancy**: the trees are read before and after; a branch checked out in between is rolled back by its operation's rule (the create-only put-back, `W/ContentHolds.cs:481-492`, is a delete's alone), said. **Held branches**: delete and rename refuse a `daoris/*` branch, a landing's (`W/RecoveryRetention.cs:32`), the line, one `TogetherGuard` binds (`W/TreeSync.cs:243`).

- **Create** a branch or lightweight tag: `create`; undo deletes at the id.
- **Delete** a branch or lightweight tag: `delete`, the recovery ref's `create`, each untouched proof ref's `verify`; then the section removed (§5), journaled first.
- **Rename**: `RefTransaction.Rename`, the recovery ref; then the section renamed (§5), journaled first; a new reflog, said.
- **Pick, revert, rebase** (§7): objects, then the branch's `update`, the recovery ref, the proof refs' `verify`.

**Tags: lightweight first**; one naming no commit is refused. Annotated or signed tags are listed, not deleted, until retention keeps tag objects by id: today's judges by commit ancestry (`W/RecoveryRetention.cs:79,93,151-154`), which would let the tag object go.

## 7. History edits without a working tree

Pick, revert and rebase onto, by §6's transaction, on a branch **no tree has checked out** (read before and after), never the line, a session's, a landed or a `TogetherGuard`-bound branch; not `git replay` (experimental). A linear range of non-merge, non-root commits; a rebase's commits **unpublished**: reachable from no remote-tracking ref, those refs verified at the ids read (D109). Pick and revert rewrite nothing, so they take a published source.

- Pick C onto T: `merge-tree --write-tree --merge-base=C^ T C`, then `commit-tree` with C's author and message, the committer the checkout's identity first (D198 point 5). Revert: `--merge-base=C T C^`, git's message. Rebase: each picked in order.
- Refused, named: an empty result, a signed commit, a merge, a root, a conflict anywhere (only objects written), past 50 commits (provisional; the command to copy). Ids pinned at the plan; 2n starts, with progress.

## 8. Working-tree acts

Each plan names the hooks its act runs (`pre-commit`, `commit-msg`, `post-commit`, `post-checkout`, `reference-transaction`), never skipped (no `--no-verify`).

- **Stage, unstage.** *The opposite press* recovers nothing: HEAD = A, index = B, worktree = A, and Unstage loses B. So the touched entries (mode, blob id, stage) are journaled first, their blobs held by a commit at `refs/daoris/acts/<act>` until the record expires (30 days, provisional); **Undo** restores them under `index.lock` (§5) only where each still equals the act's result, naming any that changed.
- **Commit, amend**: git's auto maintenance as the person's own commit; amend first keeps a recovery ref at the old tip; reconciled by `HEAD` against the old tip and the plan's message, a hook's rewrite said.
- **Switch**: `switch <branch>`, never `--force`; refused dirty, mid-operation or checked out elsewhere; undone by switching back.
- **Resolve**: per hunk ours, theirs or both, from the stages by `merge-file` (no merge driver), the bytes at the press equal to those shown; *Mark resolved* runs `add`. Replaced bytes and stage entries are journaled, the stages kept at `refs/daoris/acts/<act>`, surviving restart; Undo restores them where still the act's, the index under `index.lock`.
- **Stash Save**: `stash push`, tracked only unless *Include untracked*; the plan says `refs/stash` is shared with every linked tree, sessions' included. **Apply** by **commit id** (`stash apply <id>`, never `pop`), clean checkout and no untracked collision; a conflict stays for Resolve with the stash kept, so Apply ships after Resolve. **Drop is deferred**: the shared stash reflog is beyond any checkout lock and a message proves no ownership; it returns with positive provenance and identity-checked removal under concurrent reflog writes.

## 9. Remote acts and D198's prerequisites

Configuration can widen a remote act, and the door judges the class alone (`W/GitDoors.cs:449`), so each act reads origin's configuration fresh at re-judgement, binding what runs:

- **Fetch**: `git -c fetch.pruneTags=false -c remote.origin.pruneTags=false -c maintenance.auto=false -c gc.auto=0 -c fetch.writeCommitGraph=false fetch --no-tags --no-recurse-submodules --no-write-fetch-head --refmap= --prune origin +refs/heads/*:refs/remotes/origin/*`: only `refs/remotes/origin/*` moves; prune's removals named; `FETCH_HEAD` kept, as D109's fetch keeps it (`W/TreeRefs.cs:123`).
- **Push**: `git -c push.pushOption= push --no-follow-tags --recurse-submodules=no --no-signed origin <id>:refs/heads/<name>`, no `+`, no `-u`; only where `remote get-url --push --all origin` names one address, the plan's and the grant's (`W/GitRemotes.cs:16-21` reads only the first).
- Either refuses where `remote.origin.uploadpack`, `receivepack` or `mirror` is set.

**Kept from D198**: the doors installed, still opt-in at publish (`tools/desktop-publish.mjs:1376-1380`), else not offered. A fetch is a Read. A person's push is a Write needing GITAUTH1f and a `GitGrants` grant the press writes for origin's push address, never a landing's; the plan claims no fast-forward proof the door cannot give. A tag push is Dangerous and waits for GITAUTH1g. Each terminal writing verb (`daoris-driver git fetch|branch|rename|delete|push|tag|pick|revert|rebase …`, `git checkout <repository> stage|unstage|commit|amend|stash|switch|resolve …`, `[--plan|--yes]`) is a `PersonPresses` route of `RouteClass.Person` (`Instance/InstanceRoutes.cs:137`), run on the window's Confirm, else standalone (§5); `DAORIS_SESSION_ID` still refuses. Ask Daoris's `git` kind proposes cards (GIT1k), never applies; the switch goes through the setting door.

## 10. Surfaces

Round 1's table stands (§5 at `67d806f2c`) without *Drop*. Added: *Refresh* (刷新); *Undo* (撤销) · *Finish* (完成) · *Stop* (停止); *Daoris could not tell whether this finished; nothing was retried* (Daoris 无法确定此操作是否完成；未做任何重试); *Annotated tags are not deleted here* (此处不删除附注标签). *Amend*, *hook* and *annotated tag* pass D116's check first.

## 11. Fit

**D147**, on acceptance: points 2 and 3 and design §2.8 and §3.1 take these acts (a free three-way editor, reset, clean, a force and a remote delete stay refused); its rejection of checking out in the person's checkout (`D147.md:58`; design §3.1, `docs/2026-10-04-built-in-git-design.md:136-139`) yields to the switch; §4.2 gains the recovery refs, branch sections and what hooks and auto maintenance write; its fetch becomes §9's. **D150 point 3**: a Changes tab beside Branches and History where the switch is on. **D198, D51, D55, D109, D164 stand**, D109's pull deferred beside a session (§3).

## 12. The rows, smallest first

GITTXN1 is built; GITTXN2 is filed, its proof in `TASKS.md`; GITLOCK1 is building §3, proved also by the foundation read's §3 and the pull and promotion deferred beside a session. On acceptance, GITAPP2a-n, each alone, proof after the colon:

- **a. The journal** (after GITLOCK1, GITTXN2), §5 and act refs on a fixture act with ref, configuration and index steps: crash, bound, unended child and restart at every boundary (before launch, before the pid write, in a descendant hook), nothing re-run; an orphan past its deadline ended or said; a live step refusing writers; two reconcilers; reconciling in a sync look and a promotion step; a stale `index.lock` named; a changed section or entry refused under git's lock; the terminal closed mid-step; an older build refused; the store busy at restart; a stray held only by an act ref kept; an act ref expiring at its id.
- **b. The Changes read, the switch** (no prerequisite), §4: a staged-and-reversed file twice; the switch off by default, naming sessions there; only question starts.
- **c. Create branch, lightweight tag** (after a): `create` refusing an existing name; conditional undo; `PipeCensusTests` twins.
- **d. Delete** (after c): each `verify` red without it; late checkout restored; annotated tag refused; section restored under `config.lock`; the recovery listed; `daoris/*`, landing, line and together-bound branches refused.
- **e. Rename** (after d): reversal checking both names; configuration interrupted, *Finish* and *Undo*; d's branches refused.
- **f. Fetch** (after a; doors installed): hostile `remote.origin.fetch`, `tagOpt`, `pruneTags`, `uploadpack` moving nothing outside `refs/remotes/origin/*`, or refusing; `FETCH_HEAD` kept by a failed and a successful fetch.
- **g. Push one branch** (after f, GITAUTH1f): pre-push runs; grant refusal and success; second address, followed tags, recursive push confined or refused; interruption unknown, never retried.
- **h. Stage, unstage** (after a, b): the A/B/A case undone exactly by hunk and file; stale bytes or index refused; switch off, a conversation starting, a driven session there (trees off) or waiting, an unreadable ledger, a pull or promotion holding the lock each refusing.
- **i. Commit, amend** (after h): a formatting `pre-commit` said; amend's recovery ref; an interrupted commit reconciled, its hook's children ended in bound after the owner dies.
- **j. Resolve** (after h): saved bytes and stages survive restart; stale or non-conflicted file refused.
- **k. Switch, stash Save** (after h): dirty, mid-operation, elsewhere-checked-out refused.
- **l. Stash Apply** (after j, k): by id after ordinals moved; conflict retained, resolvable, stash kept.
- **m. History edits** (after a): conflict, merge, root, signed, empty refused with nothing moved; published refused for rebase, taken for revert; a merge driver in each scope refused before start; mid-way checkout rolled back; a together-bound branch refused.
- **n. Push a lightweight tag** (after g, GITAUTH1g): GITAUTH1g's judge reading the tag's update; refused before it.
