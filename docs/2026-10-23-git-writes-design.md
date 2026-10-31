# GITAPP2: the person's git acts and writes, round 2 (2026-10-31)

**Standing:** proposed (D204), round 2: the steward's revision under D191 after Codex's read; Codex reads it next.

Round 1 (this file at `67d806f2c`) carried GITAPP1's round-2 write half unchanged. Codex read it (`docs/2026-10-31-git-writes-read-codex.md`: another round, five further blocking findings); each finding is now the contract below, placed in §13. The design changes a foundation (a lock over every checkout writer, a durable act record), so D191's 2026-10-20 amendment adds the Opus foundation reader's read before acceptance, and, since it deletes, one maker's read never accepts it. Its prerequisites GITLOCK1 and GITTXN1 build now as rows of their own. `W/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/`; lines at `ebe9cc9f1`.

## 1. What it rests on

Only what is built: GITAPP1a-i's counting seam (`W/GitLaunch.cs`), 1a-iii's runners that await a killed process's exit or say `git.unended` (`Driver/Platform/ChildProcess.cs:82-104`), REVIEWLOCK1's flags. Not GITAPP1's part two: its reader, refs observation, cache and watch are unbuilt and unread (`W/ReviewGraphReads.cs:18`).

- **Fresh reads.** Every plan and re-judgement read is a start through the seam, asked as written, outside a landing's or judgement's memo (`W/WorkingTree.cs:1067-1073`); nothing kept across opens. The plan holds the ids and byte digests it showed; the re-judgement compares.
- **No watch**: `RefWatch` hears no tag, stash or index change and watches only repositories holding Daoris's work (`W/RefWatch.cs:229-241,249`). The Changes tab reads on open, focus, each act and *Refresh*, saying when.
- **Floors**, read at the plan: 2.40 for history edits (`merge-tree --merge-base`), 2.43 for an act resting on absence (`show-ref --exists`); below, not offered.
- **Configured programs**: a history edit's `merge-tree` is not started where configuration, read at the plan and under the lock, defines `merge.<name>.driver` or sets `merge.renormalize` beside a filter (the read design's §1.1 rule, applied here). Staging patches come from `diff --no-ext-diff --no-textconv --full-index` and apply with `--whitespace=nowarn`.

## 2. Where the working-tree acts live

The person's registered checkout, behind the switch *Daoris may change this checkout's files*, off by default, in Setup's **Reach** (D150 point 3), named in every plan. Rejected: a manual tree (none of the person's edits are there) and an idle session tree (D51, D164). A session's tree is refused whatever the switch says.

## 3. The checkout exclusion (GITLOCK1)

One lock per registered checkout, on `TreeLock`'s pattern (`W/TreeLock.cs`: file-system sharing, released with a dead process's handles, never waited for). **Alone**, from fresh judgement through effect and reconciliation, by every writer:

- D109's line pull in the checkout (`W/LinePulls.cs:51-79`), which today writes under no lock, before `TreeSync` takes `TreeLock` (`W/TreeSync.cs:209,220`);
- the integration promotion's fast-forward of the checkout holding the line (`W/IntegrationPromotes.cs:466-497`), which carries the legacy merge press (`W/TreeLands.cs:299`) and background integration alike: taken there, never in `TreeLands.MergeAsync`, so nothing nests;
- every act here.

**Shared** by a conversation starting in the checkout, until its record opens and the ledger names it; today such a start takes nothing (`Loop/Chat/ChatRunner.cs:671`, `Course/Chat/ChatGoOn.cs:751`). This design needs the lock **innermost**: after any integration claim or `TreeLock`, nothing but git's locks inside, never re-entered. GITLOCK1 fixes the order and keeps the census; this design joins it, adding no lock.

**Under the lock, before any effect**, fresh: the switch; the checkout's identity (top, common directory, registered); conversations there, from the ledger; an operation in progress (`MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `rebase-merge/`, `rebase-apply/`, `BISECT_LOG`); `HEAD`; the touched index entries (`ls-files -s -z`) and bytes; the checkout's open act records (§5). A difference refuses, named.

**Refused**: a hard reset, `clean`, any force, a recursive submodule write, a merge or rebase of its branch, any act while a conversation runs there or another writer holds the lock.

## 4. The Changes tab's read

Three fresh observations under REVIEWLOCK1's flags: HEAD → index (`diff --cached HEAD`), index → worktree (`diff`, unrefreshed), status's untracked and unmerged entries (`--porcelain=v2`), each entry with mode and blob id. A staged addition with a matching unstaged reversal shows in both. Stage applies the index → worktree hunk or line (`apply --cached`), Unstage the HEAD → index hunk reversed; at the press the entry's blob id and the file's bytes equal the hunk's source.

## 5. The act journal (R2-15)

**One record per act in the home, written atomically before each step it announces.**

**Fields**: id, kind, the press (window or terminal, its Confirm); repository and checkout identity; the plan (refs and ids shown, touched entries and digests, hooks, the switch); the owner (process id and start time); per effect step its arguments, expected postconditions, GITTXN1 rollback, process id and start time, exit or *unended*; the recovery (recovery refs, index entries, configuration sections, replaced bytes beside the record); the outcome.

**Phases**: *judged* (fresh reads equal the plan) → *prepared* (recovery written) → *started* (written before each step's process starts, its id after) → *ended* → *reconciled* (postconditions read: *done*, *not done*, *unknown*) → *closed*; then the lock goes.

**Bounded.** *Never killed* goes: every git keeps its `GitBounds` (`W/Domain/GitBounds.cs:19-22`: two minutes for a question or ref move, ten for work), past which its tree is ended and its exit awaited. Cancellation reaches an act only before its first effect step; effect steps run under `CancellationToken.None` and their bound, as the judged delete's do (`W/ContentHolds.cs:429`). No exit code, bound or kill proves the effect: postconditions do, and an unended process makes it *unknown*, never *failed*.

**Ownership.** Window presses and routed terminal verbs (§9) run in the desktop's driver; a closing window cancels only a wait before the effect, since the application outlives a window. The application's exit waits for a running effect step up to its remaining bound, saying so. A standalone terminal verb holds Ctrl-C once an effect starts (git has no console of its own, `W/WorkingTree.cs:970`); closed outright, it leaves git running and the record at *started*.

**Reconciliation**, at the driver's start and by every writer taking the checkout lock (GITLOCK1's included), before anything else: a record not *closed* whose owner is gone is reconciled; a step whose git still runs refuses every writer there; otherwise postconditions decide. *Unknown* is said on the repository's page and where it was pressed, with what is kept and the command to copy. **Nothing is re-run**: no hook, commit, push or configuration write is retried. A rename stopped between its ref and configuration steps offers *Finish* and *Undo*, each under its condition.

**Configuration** moves only where its journaled contents stand: a rename's `config --rename-section`, or its reversal, where the source equals the record and the target is absent; a deleted branch's section (removed as `branch -D` removes it, `W/ContentHolds.cs:449`) restored where none stands.

## 6. Ref acts (GITTXN1)

**One operation per ref**, its expectation inside: `create` (absent), `update <new> <old>`, `delete <old>`; `verify` only for a ref the judgement read and the act leaves, at the id read. Identical expectations fold; contradictions refuse before git runs; two names of one ref (a symbolic ref, a case variant on a case-blind disk) refuse as aliases. The judged delete, which drops its self-verification by hand (`W/ContentHolds.cs:432`), moves onto it.

**Rollback per operation, conditional**: a delete restored only where absent; a rewrite moved back only from the tip it produced; a rename reversed in one transaction checking both names; a create deleted at its id. A refused transaction or rollback is read (absent only on git's *missing*); an unmet condition is reported uncertain, recovery kept.

**Occupancy**: the trees are read before and after; a branch checked out in between is rolled back by its operation's rule (`W/ContentHolds.cs:495`'s create-only put-back is a delete's alone), said.

- **Create** a branch or lightweight tag: `create`; undo deletes at the id.
- **Delete** a branch or lightweight tag: `delete`, the recovery ref's `create`, each untouched proof ref's `verify`; then the section removed, its contents journaled first.
- **Rename**: `create <new>`, `delete <old>`, the recovery ref; then the section renamed, journaled first; a new reflog, said.
- **Pick, revert, rebase** (§7): objects, then the branch's `update`, the recovery ref, the proof refs' `verify`.

**Tags: lightweight first.** A tag naming no commit is refused. Annotated or signed tags are listed, not deleted: retention judges by commit ancestry (`W/RecoveryRetention.cs:79,93,151-154`), so a branch holding the peeled commit would let the tag object go. Deletion waits for retention that keeps tag objects by id.

## 7. History edits without a working tree

Pick, revert and rebase onto, by §6's transaction, on a branch **no tree has checked out** (read before and after), never the line, a session's or a landed branch; not `git replay` (experimental). A linear range of non-merge, non-root commits; a rebase's commits **unpublished**: reachable from no remote-tracking ref, those refs verified at the ids read (D109). Pick and revert rewrite nothing, so they take a published source; round 1's refusal made Revert useless for its commonest case.

- Pick C onto T: `merge-tree --write-tree --merge-base=C^ T C`, then `commit-tree` with C's author and message, the committer the checkout's identity first (D198 point 5). Revert: `--merge-base=C T C^`, git's message. Rebase: each picked in order.
- Refused, named: an empty result, a signed commit, a merge, a root, a conflict anywhere (only objects written), past 50 commits (provisional; the command to copy). Ids pinned at the plan; 2n starts, with progress.

## 8. Working-tree acts

Each plan names the hooks its act runs (`pre-commit`, `commit-msg`, `post-commit`, `post-checkout`, `reference-transaction`), never skipped; `--no-verify` is not offered.

- **Stage, unstage.** *The opposite press* recovers nothing: HEAD = A, index = B, worktree = A, and Unstage loses B. So the touched entries (mode, blob id, stage) are journaled first, their blobs kept reachable by a recovery commit at `refs/daoris/acts/<act>` until the record expires (30 days, provisional); **Undo** restores them (`update-index --index-info`) only where each still equals the act's result, naming any that changed.
- **Commit, amend**: git's auto maintenance as the person's own commit; amend first keeps a recovery ref at the old tip; reconciled by `HEAD` against the old tip and the plan's message, a hook's rewrite said.
- **Switch**: `switch <branch>`, never `--force`; refused dirty, mid-operation or checked out elsewhere; undone by switching back.
- **Resolve**: per hunk ours, theirs or both, from the stages by `merge-file` (no merge driver), the bytes at the press equal to those shown; *Mark resolved* runs `add`. Replaced bytes and stage entries are journaled, surviving restart; Undo restores them where still the act's.
- **Stash Save**: `stash push`, tracked only unless *Include untracked*; the plan says `refs/stash` is shared with every linked tree, sessions' included. **Apply** by **commit id** (`stash apply <id>`, never `pop`), clean checkout and no untracked collision; a conflict stays for Resolve with the stash kept, so Apply ships after Resolve. **Drop is deferred**: the shared stash reflog is beyond any checkout lock, and a message proves no ownership; it returns with positive provenance (entries Daoris made, by commit id) and identity-checked removal under concurrent reflog writes.

## 9. Remote acts and D198's prerequisites

Configuration can widen a remote act (extra push destinations, followed tags, pruned tags), and the door judges the class alone (`W/GitDoors.cs:449`). Each act reads origin's configuration fresh at re-judgement and binds what runs:

- **Fetch**: `git -c fetch.pruneTags=false -c remote.origin.pruneTags=false -c maintenance.auto=false -c gc.auto=0 -c fetch.writeCommitGraph=false fetch --no-tags --no-recurse-submodules --refmap= --prune origin +refs/heads/*:refs/remotes/origin/*`: only `refs/remotes/origin/*` moves; prune's removals named.
- **Push**: `git -c push.pushOption= push --no-follow-tags --recurse-submodules=no --no-signed origin <id>:refs/heads/<name>`, no `+`, no `-u`; only where `remote get-url --push --all origin` names one address, the plan's and the grant's (`GitRemotes.AddressOf`, `W/GitRemotes.cs:16-21`, reads only the first).
- Either refuses where `remote.origin.uploadpack`, `receivepack` or `mirror` is set.

**Kept from D198**: the doors installed, still opt-in at publish (`tools/desktop-publish.mjs:1376-1380`), else not offered. A fetch is a Read. A person's push is a Write needing GITAUTH1f and a `GitGrants` grant the press writes for origin's push address, never a landing's; the plan claims no fast-forward proof the door cannot give. A tag push is Dangerous and waits for GITAUTH1g. Each terminal writing verb (`daoris-driver git fetch|branch|rename|delete|push|tag|pick|revert|rebase …`, `git checkout <repository> stage|unstage|commit|amend|stash|switch|resolve …`, `[--plan|--yes]`) is a `PersonPresses` route of `RouteClass.Person` (`Driver/Instance/InstanceRoutes.cs:137`), run on the window's Confirm, else the standalone rule; `DAORIS_SESSION_ID` still refuses. Ask Daoris's `git` kind proposes cards (GIT1k), never applies; the switch goes through the setting door.

## 10. Surfaces

Round 1's table stands (§5 at `67d806f2c`) without *Drop*. Added: *Refresh* (刷新); *Undo* (撤销) · *Finish* (完成); *Daoris could not tell whether this finished; nothing was retried* (Daoris 无法确定此操作是否完成；未做任何重试); *Annotated tags are not deleted here* (此处不删除附注标签). *Amend*, *hook* and *annotated tag* pass D116's check first.

## 11. Fit

**D147**, on acceptance: points 2 and 3 and design §2.8 and §3.1 take this design's acts (a free three-way editor, reset, clean, a force and a remote delete stay refused); its rejection of checking out in the person's checkout (`D147.md:58`, design §3.1 at `docs/2026-10-04-built-in-git-design.md:136-139`) yields to the switch; §4.2 gains the recovery refs, a branch's configuration section and what an act's hooks and auto maintenance write; its fetch becomes §9's. **D150 point 3**: a Changes tab beside Branches and History where the switch is on. **D198, D51, D55, D109, D164 stand.**

## 12. The rows, smallest first

GITTXN1 and GITLOCK1 build now. On acceptance, GITAPP2a-n, each alone:

- **a. The journal** (after GITTXN1, GITLOCK1): §5 on a fixture act with a ref and a configuration step. Proof: crash, bound, unended child and restart at every boundary reconciled with nothing re-run; a live step refusing every writer.
- **b. The Changes read, the switch** (waits on nothing built): §4. Proof: a staged-and-reversed file twice; the switch off by default; only question starts.
- **c. Create branch, lightweight tag** (after a). Proof: `create` refusing an existing name; conditional undo; `PipeCensusTests` twins.
- **d. Delete** (after c). Proof: each `verify` red without it; late checkout restored; annotated tag refused; section restored.
- **e. Rename** (after d). Proof: reversal checking both names; configuration interrupted, *Finish* and *Undo*.
- **f. Fetch** (after a; doors installed). Proof: hostile `remote.origin.fetch`, `tagOpt`, `pruneTags`, `uploadpack` moving nothing outside `refs/remotes/origin/*`, or refusing.
- **g. Push one branch** (after f, GITAUTH1f). Proof: pre-push runs; grant refusal and success; second address, followed tags, recursive push confined or refused; interruption unknown, never retried.
- **h. Stage, unstage** (after a, b). Proof: the A/B/A case undone exactly by hunk and file; stale bytes or index refused; switch off, conversation starting, pull or promotion holding the lock each refusing.
- **i. Commit, amend** (after h). Proof: a formatting `pre-commit` said; amend's recovery ref; interrupted commit reconciled.
- **j. Resolve** (after h). Proof: saved bytes and stages survive restart; stale or non-conflicted file refused.
- **k. Switch, stash Save** (after h). Proof: dirty, mid-operation, elsewhere-checked-out refused.
- **l. Stash Apply** (after j, k). Proof: by id after ordinals moved; conflict retained, resolvable, stash kept.
- **m. History edits** (after a). Proof: conflict, merge, root, signed, empty refused with nothing moved; published refused for rebase, taken for revert; a merge driver in each scope refused before start; mid-way checkout rolled back.
- **n. Push a lightweight tag** (after g, GITAUTH1g). Proof: GITAUTH1g's judge reading the tag's update; refused before it.

## 13. Codex's read, placed

R2-11, round-1 20 and the promotion: §3. R2-12, R2-13, round-1 22: §6. R2-14, round-1 24: §8. R2-15: §5. R2-16: §8, row l. The reader foundation: §1. The opposite press: §8. Annotated tags: §6. Remote configuration: §9. None dropped; §7's published source for pick and revert is the steward's own change, as are the act reads outside the memo (§1), the standalone terminal's Ctrl-C (§5), the journaled branch section (§5) and the promotion's lock at its effect, so the legacy press and background integration take it with no nesting (§3).
