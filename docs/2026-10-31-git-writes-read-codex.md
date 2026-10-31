# GITAPP2: the git writes design, its reads (2026-10-31)

**Standing:** the reads under D191 of `2026-10-23-git-writes-design.md` (D204, accepted 2026-10-31): Codex's of round 1 (another round), Codex's of round 2 (another round) and the Opus foundation reader's of round 2 (accept after named changes). The steward's settlement follows them.

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

## Round 2, the Opus foundation reader's read (2026-10-31)

Read-only, at `7c7bae349`; the design's citations hold at `ebe9cc9f1` (GITTXN1 has since moved the content holds' lines). `W/` = `src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/`.

**1. Verdict: accept after the named changes below.** None needs a new round; the four blocking ones change text at the foundation and leave the acts alone.

**2. Findings**

- **F1, BLOCKING: the census leaves out driven sessions in the checkout.** Trees are opt-in (`Settings/DriverConfig.cs:1485-1486`); by default a driven quest runs in the person's checkout (`Loop/Driver.cs:2124`), and its starts there take nothing (`:2464`, `:2747`). §3 names only conversations. **Change:** the shared hold is taken by every session start in the checkout, driven or conversation; under the lock, any session the ledger holds there (running, waiting or held to go on) refuses; an unreadable ledger refuses; the switch says when sessions already run in that checkout.
- **F2, BLOCKING: a crash can leave an orphaned git that refuses every writer forever.** The bound is the owner process's (`Platform/ChildProcess.cs:73-80`); git is in no job, so it outlives a crashed owner, while the lock goes with the owner's handle (`W/TreeLock.cs:19-20`); a crash between the start and the pid write leaves *started* with no pid. **Change:** each step's deadline journaled (its start plus its `GitBounds`); a reconciler finding a live step past it ends that process tree (pid and start time) and awaits its exit, else says *unended* with the pid and a *Stop*; effect steps run in a job named for the act (`ProcessJob.CreateNamed`, HEAVYRUN1a), so a live step is found without the pid write; every refused writer and held start names the act and the repository's page.
- **F3, BLOCKING: `refs/daoris/acts/` would count as a holder of commits.** `ContentHolds.AnyRef` excludes only `discarded/` and pieces (`W/ContentHolds.cs:377-378`); `TreeStrays.HeldElsewhereAsync` falls back to any `AnyRef` holder (`W/TreeStrays.cs:215-219`); `LandedCleanups.WentAsync` (`W/LandedCleanups.cs:702-713`) likewise. **Change:** the acts prefix joins both; an act ref expires in one transaction at its journaled id; branch and tag deletes and old tips (amend, rebase) keep their recovery under `refs/daoris/discarded/` (its retention, the Recoveries page, its holder exclusion); only index snapshots and Resolve's stages go under `acts/`.
- **F4, BLOCKING: the journal "in the home" contradicts D212 point 17.** **Change:** the journal is a desktop-store family; ownership follows the integration store's claim and step shape (claim generation, taking only from a dead run, fencing); pid and start time identify only the child; replaced bytes stay as files beside it; machine-local, never synced.
- **F5: GITTXN1 is built; two things in it.** `RefTransaction.RunAsync` treats exit -1 (bound reached, unended, not started) as a refusal and says "Not Done means nothing moved" (`W/RefTransaction.cs:127`); `RollBackAsync` says "nothing was moved" (`:164-168`), against §5 and §6 (only postconditions prove an effect). Aliases are checked within one transaction only (`:70-76`), not against existing refs. **Change:** §6 restated against the code; `RefRun` separates "git refused" from "git did not answer", the second read before anything is said (GITTXN2).
- **F6: the lock's key and the deadlock claim.** Key the lock by the checkout's canonical common directory, not TreeLock's key (`W/TreeLock.cs:43-44`). With try-only locks, deadlock-freedom comes from never waiting: the waits that exist are `IntegrationLines`' semaphore (`W/IntegrationLines.cs:89-90`, before the lock) and the process-start gate. **Change:** "no waiting acquisition while the lock is held".
- **F7: reconciliation's scope and timing.** Under another writer it runs inside that writer's memo scopes (`W/WorkingTree.cs:1071-1072`; the look at `W/TreeSync.cs:188`): taking the lock suspends them. At the driver's start it does not hold the come-up (D181 point 1): one checkout at a time, each under its own lock alone; a checkout it cannot take waits for the next look.
- **F8: git's own lock files.** `index.lock`, a ref's `.lock`, `config.lock` left by a bound kill or an IDE's status: refused under the lock, named, never removed by Daoris, the command to copy.
- **F9: the pull and the promotion with a session in the checkout.** Today both fast-forward a clean checkout while an agent runs there (`W/LinePulls.cs:55-75`, `W/IntegrationPromotes.cs:490-495`): state whether that continues; the promotion takes the lock from `EffectAsync`'s first read (`:470`).
- **F10: writers outside any lock** (the person's IDE and terminal, the Node CLI's offline doctrine sync) are covered only by the fresh reads; the census names them.
- **F11: refusals for branches Daoris holds.** Delete and rename refuse or name `daoris/*` branches, landing branches, the line, and a branch `TogetherGuard` binds (`W/TreeSync.cs:243`).
- **F12: the update's drain and relaunch** treat a running effect step as busy.

**3. Beyond the rows' listed proofs.** GITLOCK1: driven starts, conversations, the pull, the whole promotion effect and every act in the census; exclusion across processes and a dead holder letting go; a held start going on at the next look naming the holder; every await under the lock counted, none on another lock; one checkout registered twice, one lock; the reconciliation hook before the writer's judgement, outside the memo scopes. GITTXN2: an unanswered transaction or rollback read, never "nothing moved"; `Feature` refused beside a packed `feature` on a case-blind disk. a: a crash between start and pid write; an orphan past its deadline ended or said; two reconcilers at once; reconciliation inside a sync look and a promotion step; a stale `index.lock` named; the store family refused by an older build; a restart with the store busy. c, d, e: a stray branch held only by an act ref kept; an act ref's expiry verified at its id; a delete's recovery on the Recoveries page; `daoris/*`, landing and together-bound branches refused. h: a driven session in the checkout (trees off) and one waiting there refuse; so does an unreadable ledger. i: an interrupted commit whose hook left children ends within its bound after the owner dies. m: a branch `TogetherGuard` binds refused.

## Round 3, Codex's confirmation of the named changes (2026-10-31)

All seven are closed in the design (not yet built). R2-15's launch ownership: the journaled job identity and deadline precede every effect, children join suspended before release, unread containment holds, the standalone worker survives the terminal (`Platform/ProcessJob.cs:83,105`; join-before-release as `Review/Heavy/HeavyStep.cs:211,221`). Conditional recovery: compared after exclusive acquisition of git's index or config lock, replaced atomically, the configuration file and scope journaled, a mismatch keeping the recovery; it replaces today's unconditional section removal (`Workplace/ContentHolds.cs:435`). `FETCH_HEAD`: `--no-write-fetch-head` present, as `Workplace/TreeRefs.cs:123`, row f proving it. F1, F2, F3 and F4 closed against `Loop/Driver.cs:2464,2747`, `Platform/ChildProcess.cs:73`, `Platform/ProcessJob.cs:260`, `Workplace/ContentHolds.cs:377`, `TreeStrays.cs:215`, `LandedCleanups.cs:702`, `Workplace/IntegrationStore.cs:383,711` and D212 point 17. No new design blocker; GITLOCK1, GITTXN2 and row a's proofs remain its prerequisites.

## The steward's settlement (2026-10-31)

Round 3 (`2026-10-23-git-writes-design.md`) takes every named change of both round-2 reads into its text, Codex confirming each closed. One choice is the steward's own: the line pull and the integration promotion are deferred while the ledger holds a session in the checkout (§3 *Beside a session*), so neither moves a checkout an agent works in. **Accepted 2026-10-31.** Its rows are GITAPP2a to 2n (§12), after GITLOCK1 and GITTXN2.
