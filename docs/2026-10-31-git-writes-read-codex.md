# GITAPP2: the git writes design, Codex's read (2026-10-31)

**Standing:** a maker's read under D191 of `2026-10-23-git-writes-design.md` (D204, proposed); its verdict is another round. The steward's revision follows it.

**1. Verdict: another round.** The remaining questions change concurrency, recovery and process ownership; §7 lists corrections without making them the operative contract. D147 and D150 therefore remain unchanged. D198’s installed-door, separate-grant and GITAUTH1f/1g prerequisites are correctly retained.

Read-only review; no files changed or tests run. No repository `local/` directory was read. Below, `W/` means `src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/`.

**2. Listed open questions**

None is fully answered as written.

| Question | Disposition and required change |
|---|---|
| **R2-11** | **Answerable with a named change:** checkout-writer census. Lock D109’s pull from fresh judgement through effect and reconciliation. It still writes without that lock at `W/LinePulls.cs:51`, before `TreeSync` takes `TreeLock` at `W/TreeSync.cs:220`. Include the newer integration writer below. |
| **R2-12** | **Answerable:** typed transaction builder producing exactly one operation per ref. Put expectations on create/update/delete; verify untouched dependencies; deduplicate identical expectations and refuse contradictions or aliases. Today’s specialized delete already avoids one self-verification collision at `W/ContentHolds.cs:430`; that is not a general builder. |
| **R2-13** | **Answerable:** operation-specific conditional rollback. Delete restoration requires absence; rewrite restoration requires the produced tip; rename reversal checks both names atomically. Report uncertainty if these conditions fail. `W/ContentHolds.cs:495` restores an absent branch only. |
| **R2-14** | **Answerable:** defer Drop. A checkout lock cannot serialize the shared stash reflog, and a message is no ownership record. Reintroduce Drop only with positive provenance and identity-checked removal under concurrent reflog mutation. |
| **R2-15** | **Still open.** Specify journal fields, durable phase boundaries, effect ownership after window/terminal exit, restart reconciliation, conditional configuration restoration, and treatment of an unended process. Preserve bounded execution: `W/WorkingTree.cs:1094` applies bounds; `Platform/ChildProcess.cs:80` kills and awaits termination. “Never killed” cannot stand. Prove interruption at every boundary without blindly retrying hooks or pushes. |
| **R2-16** | **Answerable:** move Resolve before conflict-producing stash Apply. Keep Apply unavailable until that recovery path ships. Non-blocking for earlier rows. |
| **Round-1 20** | **Answerable:** the complete writer census and shared-start/exclusive-write protocol above; today checkout conversations skip `TreeLock` (`Loop/Chat/ChatRunner.cs:670`, `Course/Chat/ChatGoOn.cs:751`). |
| **Round-1 22** | **Answerable:** the transaction builder plus operation-specific rollback above. The existing deletion pattern cannot be generalized verbatim. |
| **Round-1 24** | **Answerable:** defer Drop as above; retaining Apply conflicts does not solve deletion identity. |

**3. Additional findings**

- **BLOCKING — the assumed reader foundation is unbuilt.** The archived GITAPP1a-i/ii/iii work supplies counting, the uncached graph and cancellation. `W/ReviewGraphReads.cs:18` explicitly keeps no observation or watch. Existing `RefWatch` omits tags/stash/index events (`W/RefWatch.cs:229`) and watches repositories holding Daoris work (`:249`). **Change:** declare the actual part-two dependencies, or specify fresh uncached act observations with their own validation, git floors and merge-driver refusal. Do not treat accepted Review as supplying these guarantees.

- **BLOCKING — today’s integration promotion bypasses the proposed lock coverage.** `TreeLands.MergeAsync` delegates at `W/TreeLands.cs:299`; independent integration promotion performs its own checkout fast-forward at `W/IntegrationPromotes.cs:424`. **Change:** take the checkout lock at the actual promotion writer, including background integration, before its fresh checkout/branch/clean checks. Define acquisition order to avoid nested acquisition through legacy merge. Prove mutual exclusion against conversations, Changes acts and line pulls.

- **BLOCKING — “the opposite press” cannot recover an index.** With HEAD=A, index=B, worktree=A, Unstage removes B; Stage cannot recreate it. Today’s `W/WorkingTree.cs:234` already distinguishes staged modes and blob IDs. **Change:** preserve the touched index entries and reachable blobs before mutation, then restore conditionally. Prove staged additions followed by matching worktree reversals, including file-level undo.

- **BLOCKING — annotated-tag recovery needs object-aware retention.** `W/RecoveryRetention.cs:79` judges recovery through commit ancestry; `:153` selects ancestry holders. A branch retaining the peeled commit does not retain its annotation/signature object. **Change:** preserve raw tag object IDs with suitable retention, or initially support lightweight commit tags only and refuse annotated-tag deletion.

- **BLOCKING — remote configuration can widen the planned effect.** `W/GitRemotes.cs:19` reads one push URL; `W/GitDoors.cs:449` classifies Write without judging updates. Git configuration can add [followed tags and multiple push destinations](https://git-scm.com/docs/git-push), while pruning can [delete local tags](https://git-scm.com/docs/git-fetch). **Change:** bind the destination set, disable followed tags and recursive pushes, and constrain fetch mappings/pruning to `refs/remotes/origin/*`. Prove hostile configuration cannot expand the act.

**4. Smallest-first rows**

“Now” identifies prerequisites supportable by today’s code; it does not authorize exposing D204 writes before settlement.

| Row | Proof | Can start? |
|---|---|---|
| Transaction builder | Success, self-rest, alias, contradiction; each required expectation independently refuses movement | **Now**, independently |
| Checkout exclusion | Cross-process shared starts/exclusive writers; complete writer census; integration included | **Now**, independently |
| Three-observation Changes read and switch | Staged/reversed file appears twice; switch defaults off; no writes | **Now**, using fresh reads |
| Durable act ownership/journal | Crash, timeout, unended child and restart at every phase | After R2-15 specification |
| Create branch/lightweight tag | Existing-name refusal; conditional undo; PersonPresses twins | After builder/journal |
| Delete; rename separately | Recovery retained; late checkout; conditional rollback; configuration interruption | After builder/journal |
| Fetch; branch push separately | Confined refs/destinations, hooks, doors, grant refusal/success | Fetch after confinement; push also waits for GITAUTH1f |
| Stage/unstage; commit/amend separately | Exact index recovery; stale bytes/index refusal; hook effects reconciled | After lock/read/journal |
| Resolve | Saved bytes survive restart; stale conflict refused | After lock/read/journal |
| Switch; stash Save/Apply separately | Dirty switch refusal; Apply conflict retained and resolvable | Apply after Resolve; Drop deferred |
| Object history edits | Conflict leaves refs unmoved; every excluded range refused; occupancy rollback | After observation/effect-policy contract and journal; tag push separately waits for GITAUTH1g |

## Round 2, Codex's read of the revision (2026-10-31)

1. **Verdict: another round.** Most placements now hold as design contracts. R2-15 still leaves a process-ownership gap, and conditional recovery needs an atomic comparison rule. The required independent Opus foundation read also remains before acceptance.

Read-only review: no files changed, no tests run, no `local/` directory read. Below, `W/` means `src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/`; `P/` means the adjacent `Platform/`.

2. **Round-1 findings**

“Closed” means resolved in the proposed contract, not already implemented.

| Finding | Disposition |
|---|---|
| **R2-11** | **Closed:** §3 covers line pull from fresh judgement through reconciliation. Confirmed the present gap at `W/LinePulls.cs:58,75`, before `W/TreeSync.cs:220`. |
| **R2-12** | **Closed:** §6 specifies one operation per ref, embedded expectations, deduplication, contradiction and alias refusal. The specialized self-rest exception exists at `W/ContentHolds.cs:432`. |
| **R2-13** | **Closed:** §6 distinguishes deletion, rewrite, rename and creation rollback. Today’s `W/ContentHolds.cs:495` remains deletion-only recovery. |
| **R2-14** | **Closed:** §8 defers Drop rather than claiming a checkout lock owns the shared stash reflog. |
| **R2-15** | **Still needed:** close the launch/identity gap, preserve a bound after terminal death, and specify atomic configuration recovery; see below. Design locations: `docs/2026-10-23-git-writes-design.md:44,48,52`. |
| **R2-16** | **Closed:** Resolve precedes Apply through rows j → l. |
| **Round-1 20** | **Closed:** shared checkout starts plus exclusive writers. Confirmed current checkout starts skip the lock at `Loop/Chat/ChatRunner.cs:671` and `Course/Chat/ChatGoOn.cs:751`. |
| **Round-1 22** | **Closed:** transaction builder plus operation-specific rollback replaces generalization of create-only put-back. |
| **Round-1 24** | **Closed:** Drop deferred. |
| **Unbuilt reader foundation** | **Closed:** §1 explicitly uses fresh starts outside both memos. `W/WorkingTree.cs:1071` otherwise consults `GitFacts` and `RefSnapshot`; `W/ReviewGraphReads.cs:18` confirms no retained observation/watch exists. |
| **Integration promotion** | **Closed:** §3 places acquisition at `W/IntegrationPromotes.cs:466`, before fresh checks and the write at `:495`. This covers the legacy delegation at `W/TreeLands.cs:299` and background promotion without nesting acquisition in the legacy press. |
| **Opposite-press index recovery** | **Recovery data closed; atomic Undo still needed.** §8 retains entries and blobs rather than assuming Stage recreates lost staging. The existing staged observation is `W/WorkingTree.cs:234`. |
| **Annotated-tag retention** | **Closed:** deletion deferred. `W/RecoveryRetention.cs:93,151` judges commit ancestry, which cannot preserve an annotation object merely through its peeled commit. |
| **Remote configuration** | **Closed for effect expansion:** §9 confines mappings, disables followed tags/recursion, rejects multiple push addresses and retains D198’s prerequisites. Confirmed `W/GitRemotes.cs:19` currently reads one address and `W/GitDoors.cs:449` judges class, not updates. |

The steward’s **published-source change is sound**: pick/revert append commits; only rebase rewrites the source range. The journaled branch section is necessary, but its conditional mutation needs the correction below. The standalone Ctrl-C policy requires explicit wiring: today `git` routes at `Driver.Host/Program.cs:440`, before the handlers at `:724` and `:893`.

3. **Remaining and new findings**

- **BLOCKING — launch ownership and terminal death (R2-15).** §5 writes *started* before launch and the PID afterwards. A crash between those events leaves a running child without its recorded identity. `P/ChildProcess.cs:62` starts first; its bound is then maintained by the owner at `:73`. Closing that owner removes the timer. **Concrete change:** specify an act worker that survives terminal closure, with a journaled containment identity and deadline before any effect can run. Start children suspended, join them before release, and treat unread containment as held/unknown. Existing primitives are `P/ProcessJob.cs:77,105`. Prove cuts before launch, after launch before PID persistence, and during a descendant hook.

- **BLOCKING — conditional recovery must compare under Git’s file lock.** A read followed by `update-index --index-info` or `config --rename-section` permits another writer between comparison and replacement. Today’s branch-section removal is unconditional at `W/ContentHolds.cs:449`. **Concrete change:** compare expected entries/sections after acquiring the corresponding index/config lock, then replace atomically; journal configuration file and scope. Refuse mismatches and retain recovery.

- **Required before row f; non-blocking for earlier rows — preserve `FETCH_HEAD`.** §9’s fetch omits `--no-write-fetch-head`, already present at `W/TreeRefs.cs:123` under D109. Add it and prove failed and successful fetches preserve that file.

4. **§12 rows**

The dependency order is buildable **after those corrections**. Once GITLOCK1 and GITTXN1 land and D191 settlement is complete, **a and b can start independently**; b already needs neither foundation. After a: **c, f** (installed doors), and **m** can start independently; **h** also needs b. Then **d → e**, **h → i/j/k**, **j+k → l**. **g** additionally waits for GITAUTH1f; **n** waits for g and GITAUTH1g. Drop remains excluded.
