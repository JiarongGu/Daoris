# The process steps design's reads

Both makers' reads of [the process steps design](2026-11-09-process-steps-design.md) (PROCSTEPS1), each read-only and independent,
kept as history; the design's next round answers them.

## Codex, the design

Codex's read of the design at `0422e84b1b`, run by the steward from outside the install, read-only; verbatim.

**Not ready to implement.** [PROCSTEPS1](2026-11-09-process-steps-design.md) follows the owner’s direction: generic kinds, landing composed in workflows, no `land` performer.

The intended foci are clear: `rebase` produces a commit/problem; `resolve` settles a problem; `check` verifies and passes a commit; `merge` moves it; `tidy` cleans its work. Inputs and outcomes are described. Bounds are incomplete: rebase’s live-session wait lies outside its replay bound; merge/tidy defaults are unstated; resolve bounds asks without establishing a bound across rebinding cycles. Resolve also needs explicit ownership of the rebase attempt it finishes.

Blocking findings:

1. **§2.3 — records can lose new work.** Base row X requests A; work completes A and archives X; target adds requirement B to X. Archive precedence deletes B, merely reporting its text. Disjoint word edits can likewise produce wording neither side authored. D215 reserves conflicts of meaning for a resolver. **Smallest change:** archive automatically only against an unchanged row or an explicitly declared mechanical edit; otherwise emit a problem. Require exact archive-id matching and reject duplicate ids before merging.

2. **§§2.5–2.6 — checked content can change at merge.** Rebase produces C on target T; check passes C; target advances to T′; `merge --no-ff` or squash combines C with T′. That resulting tree was never checked. `behind` currently covers only fast-forward. **Smallest change:** carry the expected target commit, verify it atomically for every method, and return through rebase/check when it moves.

3. **§2.4 — timeout leaves tree ownership unresolved.** Resolve detaches a session tree and starts its resolver; the timeout expires while the resolver still writes. Switching back risks losing work; leaving it detached breaks subsequent session use. **Smallest change:** journal the handoff, reserve the tree, stop and await the resolver before restoration, and retain unresolved edits across timeout/restart. Keep the retry budget across equivalent rebinding cycles.

4. **§3 — recovery routes are missing.** A branch target advances, merge fails `behind`, and the shown composition has no return wire. Resolve exhaustion reaches `settle`, whose Done leads nowhere. **Smallest change:** specify both return routes and exercise them through the actual evaluator.

5. **§5 — active-run migration is insufficient.** `RunRebindsStep` explicitly skips existing node records. An active land with no unresolved delivery already qualifies for conversion but has no stated mapping. A reached squash landing also needs concrete commit/ref outputs; marking replacement nodes reached supplies none. Existing downstream attempts depend on old receipts. **Smallest change:** define a restart-safe conversion per old version/digest covering every state, outputs, bindings, receipts, pending words and downstream attempts; prove interruptions between writes.

6. **§2.8 — unchanged plugin readers retain implicit landing selection.** `PluginStep.Subject`, `PullRequestStep.Keep` and `NodeEvaluations.Landed` select by placement/the first landing. With multiple merges, a pull request can read the wrong branch. **Smallest change:** bind these readers and their records explicitly to the producing ref/node.

7. **§2.7 — tidy can discard a live commit.** Rebase C → squash → branch tidy removes step refs; a later consumer of C restarts after collection. Neither the squash nor the original branch’s recovery ref necessarily retains C. **Smallest change:** retain refs until all consumers and unreconciled attempts finish.

8. **§8 — cleanup still has two authorities.** `BranchTidying` can remove a workflow-owned branch before its configured tidy runs. **Smallest change:** make ownership disjoint or route both through the same workflow operation.

Rows **a–f are broadly ordered**, but **g must include h’s required rendering/contracts/translations**. Their proposed proofs need the sequences above, custom and older bound graphs, and interrupted migration. **i** appropriately follows integration; **j** needs a measurable build-floor condition. These are planned proofs, not established results; no files changed or tests ran.

## Opus (independent), the design

The independent Opus 5.5 reader's read of the design at `0422e84b1b`, read-only; as it answered, set down by the steward. Verdict: another round.

**Kinds.** `rebase`, `resolve`, the fed `check`, `merge` and `tidy` each have one focus, with ports, options, outcomes and bounds stated; three contracts have gaps: what makes `rebase` run again, what `merge` does onto a moved target, and `tidy` moving its own input.

**Blocking.**

- **B1. The migration (§5) does not work as `RunRebindsStep` does.** That step skips runs that have node records, because their bindings carry the old digest (`Transitional/Migration/RunRebindsStep.cs:5-6,25`; `NodeAcceptances.Lapsed`), so every kept node lapses on the next pass: the review's Accept is asked again, and a pull-request run with `land` reached performs `push` and `pull-request` again; the mapped `merge` has no `commit` or `ref` output, so `push` and `line` are never fed; an active `land` is not mapped at all; the added `tidy` has the same id as the one in `pull-request.json`. Change: rewrite the acceptances under the new digest, take the mapped outputs from the landing record, map an active `land` to not started, and give each added node its own id.
- **B2. A chain's advance loops or sticks.** Today an advance replays nothing when the line has only moved (`Workplace/TreeBranches.cs:212-221`); with `onto: line`, step 2's commit does not descend from the chain branch, the fast-forward fails `behind`, goes back to `rebase`, and the rebase makes the same commit again. Change: `onto: target` (the merge's target where it is this chain's branch, else the line), replaying only what the target lacks.
- **B3. Nothing bounds "runs again when its target moves".** (a) If the line moves while `resolve` waits, the session's tree is detached mid-rebase; `resolve` lapses, the staged resolution is dropped, and the session's later commits land on no branch. (b) Its own `merge` moves the line, so `rebase` runs again, `merge` turns `held`, and the waiting press hears that nothing merged. Change: rebind only while nothing it feeds is active or reached, and undo `resolve`'s tree set-up when it lapses.
- **B4. `merge` or `squash` onto a moved target is a fresh three-way merge**, unverified, breaking Release's claim that the commit verified is the commit moved; it may conflict, and no outcome covers that. Change: every method fails `behind` unless the target's tip is an ancestor of the input, and every preset routes `behind` back (dev-line routes nothing today).
- **B5. The judgement before the effect is lost.** `UnderHoldAsync` (`LandPerformer.cs:342-353`) re-judges the submission under the hold, but `merge` keeps only the authority check, so a closure-graph merge waiting on the git lock lands even if the submission was made again meanwhile. Change: also re-read the input's currency (work tip, quest, step ref) under the fence.
- **B6. `tidy follow` moves its own input** (the `work` branch from T to R); with `LandTries.Landed` gone, every step fed `work` lapses, the review included, unless the run's end is taken first. Change: read the run's own follow as its binding's revision, with a test.
- **B7. The records merge drops or duplicates text.** (a) `Collide` lets two inserts at one place both stand (`BacklogMerge.cs:222-228`): merging by words, "on A" plus "on B" gives "waits on A on B", both texts in one row, which §6 rejects; inserts at the same position conflict unless equal. (b) Archive-wins drops the other side's rewrite, against D215 point 6 and §6's own reasoning (the line adds "left: e" to row X, the work archives X, and e is lost); archive wins only where the other side added no words. (c) The rules run only at stops, so a clean merge can keep a row the line moved and the work archived, or keep an id twice; a post-check that each id appears once and none is both kept and newly archived, ids matched exactly (compound ids already exist: "WFREVIEW3a, 3d…" in the backlog, "GITLIB1a to GITLIB1d" in the archive).
- **B8. A second way to hand over a conflict survives (D228).** Integration calls `RebaseTelling` (`IntegrationReplays.cs:478,566-567`; `IntegrationDispositions.cs:19`), which 1g deletes and §8 defers: either the build breaks, or a second hand-over stays with no ending row (the session rebasing its own branch, which §6 rejects). Change: send integration's `conflicts` through `resolve` in 1g, or register the kept call with its ending row.

**Not blocking.** `resolve` counts each stop as a try, so a four-stop replay always fails, and rebinds reset its tries, so a busy line keeps asking with no end: count per stop, and ask again only when the work moves. `settle` has no way out: wire it back to `rebase`. Limit union's "keeps once" to non-blank lines both sides added. A missing case: the line deletes a row without archiving it while the work rewrites it. How `TogetherOperations` members are rebased is not stated. Name G8's unpressed conversion as the precedent against D228.

**Rows.** a first is right; b's `back` wire on an outcome already exists (`NodeWires.cs:468-470`); c to f expose the new kinds beside `land` until g, so keep them unoffered and refused at save until then; each kind's words land in its own row, since the names check requires them; missing proofs: a — inserts at one place, moved rows, compound ids; c — the chain advance; d — a target move and a restart mid-resolve; e — `behind` onto a moved target, and the re-judgement; g — a pull-request run with `push` pending, and an accepted run kept. i and j are placed right.


## Codex, round 2

Codex's read of round 2 at `fd03c57109`, run by the steward from outside the install, read-only; verbatim.

**Another round is needed.** Round 2 follows the requested structure: generic kinds, each declared in its own focus, with landing composed only in workflows.

Using the numbering in the [two reads](2026-11-09-process-steps-reads.md): read 1’s findings **2–4 and 6–8**, and read 2’s **B2–B6**, have explicit design answers. **B8’s retirement** is answered, but its replacement has the failure case below. **1/B7** and **5/B1** remain partly unresolved. These are design answers, not demonstrated results.

The five foci are clear: `rebase` takes work and produces a commit/problem; `resolve` takes problem/work and finishes the producer’s attempt; `check` verifies and passes the same commit; `merge` moves a commit and returns commit/ref; `tidy` follows or ends work, producing outcomes without data outputs. Their bounds are stated or inherited: replay limits, per-stop asks/timeouts, check execution bounds, phase reconciliation, and repeatable partial cleanup. The split retains explicit authority and currency checks in `merge`.

Blocking findings:

1. **§2.3: removing words can change meaning.** Base row X says “Do not enable feature A”; the target removes “not”; work archives X under the original requirement. “Only words removed” lets archive precedence discard the new requirement. This still resolves meaning in the driver, contrary to D215. **Smallest change:** automatic archival requires unchanged normalized text, relocation, or an explicitly declared mechanical transformation; otherwise return a problem.

2. **§5: changing digests while preserving receipts breaks acceptance.** `NodeAcceptance.Id` hashes `GraphDigest`, inputs and prerequisite receipts. Change upstream acceptance A’s digest and it becomes A′; a review retaining prerequisite A immediately lapses through `WireLapsing`, losing Accept. Downstream inputs and plugin attempts have the same problem. **Smallest change:** construct a transitive old-to-new acceptance/receipt map, including outcome receipts, port producer references, scopes and firing keys. Preserve external invocation identities separately. “As they were” cannot be the conversion rule.

3. **§5: newest-version conversion does not cover every open binding.** An open run binds v1’s merge delivery; v2 changes it to a branch delivery. Converting only newest v2 either leaves v1 unsupported or rebinds its run to different behavior. A valid graph already at the node/wire limit can also fail expansion and remain permanently held after `land` disappears. **Smallest change:** convert each distinct open source version/digest with its frozen options; explicitly accommodate expansion limits. Refused conversion needs a recoverable path, not only an editor instruction.

4. **§2.8: a failed candidate need not put the conflicting work on the line.** A member conflicts with another candidate member and is handed back; the candidate then fails. Rebase onto the unchanged line produces the same commit without encountering that conflict. The branch was deleted, while the delivered merge key and integration’s `(unit, tip)` key still identify the previous delivery and closed request. Returning through those records can stick or repeat the route. **Smallest change:** distinguish promotion from failure: return automatically only after the relevant content reaches the line; otherwise hold with explicit recovery, including defined same-tip readmission.

Rows **a–f** have sensible dependency order; **g before h** fixes the window dependency; **i**, then floor-conditioned **j**, are correctly placed. Their proofs remain proposed. Add the sequences above—especially receipt-reference preservation, distinct older bindings, maximum-size graphs and failed-candidate recovery—before accepting h’s claimed guarantees.

## Opus (independent), round 2

The independent Opus 5.5 reader's read of round 2 at `fd03c57109`, read-only; as it answered, set down by the steward. Verdict: a round 3; four new blocking findings, all in code round 2 had not read.

**Round 1's findings.** Answered: Codex 1, 3, 4, 5, 7, 8; Opus B1, B2, B3, B6, B7, B8. Codex 2 / B4 answered only on paper (N1). B5 answered for a single work, not a together operation (N3). Codex 6 answered, but its census leaks.

**Would each class census fail on a missed member?** The records post-check catches an id twice and a row both kept and archived, but not lost words in a clean merge: apply the other side's test to every id that left, clean merges included. `behind`: yes, the compare-and-set and `merge-behind` hold. The conversion census covers only `NodeRecord`'s own members and misses nested fields (an attempt's `Call.Kind` is `land`), the delivery record, `landings.json` and the together attempt: run it recursively over every record a `land` writes. The selection census omits `NodeEvaluations.Landed`, `NodeEvaluations.LandingOf`, `NodeObserving.LandingOf` and `ReadsTheLanding`, used by readers outside the twelve (`NodeTelling`, `NodeKept`, `NodeOwing`, `NodeIntegrating`, `CheckNode`, and the Hold, Push, Tidy, PullRequest and Land readings). `BranchDeleteCensusTests`: the journalled delete has more callers than the two named authorities (pool finishes, `TreeRemovals`, `OnlyHereProof`, `GitRefActs`, `ContentHolds`); only the look's tidy is kept disjoint from open runs. The say census misses `TurnCarries`, which also reads `LandPerformer.ReadTreeAsync`, and row h deletes `LandPerformer`.

**New blocking.**

- **N1. The line merge has two possible writers.** Today a merge into the line is admitted to the integration (`TreeMerges` → `IntegrationPresses.MergeAsync`), and the line's run merges it `--no-ff` onto whatever the candidate holds, possibly batched (`IntegrationReplays.cs:378`). Sequence: `check` passes C on T; the merge is admitted; a candidate forms on T′ with another request; C combined with T′ lands unverified and `behind` is never seen. If instead `merge` writes its own `update-ref`, the line has a second writer beside the integration's promotion. Change: say which — a merge request admitted with its expected tip, never batched, the replay refusing a non-ancestor as `behind`; or the promotion calling `merge`'s one ref write.
- **N2. A rebase/integrate loop.** `FindAsync(unit, tip)` also returns closed requests (`IntegrateStart.cs:208`). Sequence: B is handed back and its candidate fails, so the line is unmoved; the rebase returns the same tip; the merge recreates the branch; `integrate` adopts the closed handed-back request and ends `conflicted` at once; back to the rebase, unbounded. Gate hand-backs close with the same `handed-back` (`IntegrationPromotes.cs:298`, `IntegrationPromoteWords.cs:44`) and, read as `conflicted`, loop through the gates. Change: `conflicted` only for a replay-conflict hand-back and only once the line moved past its base; a new attempt never adopts a closed request; a gate hand-back ends `failed`.
- **N3. Together members.** Today a member is `Bound` and starts no delivery; once the operation lands it is reached as `brought-into` from the operation record (`DeliveryReach.cs:48-62`); the combined Accept re-reads under the hold each member's gate and tip and re-accounts each change (`TogetherAccepts.cs:96-115`). Round 2 gives no step that reading: a member's `rebase` starts and its `merge` lands it independently; after the head lands, a member whose replayed commits are new ids, or whose path the head superseded, is replayed again, and where the head reverted a line the member's change lands again. Change: `rebase` waits while its work is bound to an open operation, is held afterwards from the inclusion record, and `merge`'s judgement adds the together re-read.
- **N4. *Rebase it* duplicates the run.** In the composition, commits after the frozen tip reach the request branch only through the run's own `merge`, after its work moved, and the integrate has then rebound and re-requested them. Sequence: request R1 settles and writes a disposition for C2 while R2 already carries C2; *rebase it* replays C2 after the work a second time; once the run has ended *rebase it* is always refused. Change: write no disposition for a branch an open run governs, and retire *rebase it* there.

**The calls flagged in the brief.** Ending only once the candidate ended: feasible (member → candidate), but see N2. Ending the closed request's branch at hand-back: needed (without it `onto: target` picks the stale branch and loops on `behind`); §2.7 must name it as an authority, limited to node requests (a merge request's branch is the session's own). Together shape: confirmed that the head's turn joins the members into its branch and lands one delivery; the members' reach and the re-read are not covered (N3). Home floor: none exists — D232 deleted `HomeFloor`/`OlderBuild`; row j's condition is D232's rule (the next release that changes a shape replaces h's migration); "before `RunRebindsStep`" holds only inside `nocompat1`; the older-build and rollback refusals contradict D232 too.

**Not blocking.** The carried Accept (§2.4) has no rule: the session's `uncommitted` commit lapses the review. A resolver that commits or runs `rebase --continue` itself moves HEAD and the abort drops that work: keep HEAD on the edits ref. An `integrate` waiting on a candidate held for a decide has no bound: say so.

**Rows.** a–d are ordered and proved right; e proves `behind` through `check`, but `check`'s `commit` port is built in f: move it ahead of e; g must also draw what h adds (`integrate`'s `ref` and `conflicted`, `push`'s `ref`, `line`'s `commit`); h must re-home `TurnCarries` and prove the same-tip hand-back, a gate hand-back ending `failed`, and a member moved between ready and the effect; j's condition fixed as above.


## Codex, round 3

Codex's read of round 3 at `fd2c1421c6`, run by the steward from outside the install, read-only; verbatim.

**Another round is needed.** The generic kinds and workflow-level landing composition follow the direction. Several guarantees remain incomplete.

Round 2’s findings:

- **Codex 1: answered.** Archival requires unchanged normalized text or relocation; the post-check covers clean merges too.
- **Codex 2: answered in design.** The transitive acceptance/receipt map, scopes, firing keys and separately preserved invocation identities address the hash dependencies.
- **Codex 3: partial.** Distinct source versions, expansion limits and refused conversions have answers, but the migration’s state and eligibility coverage remains incomplete below.
- **Codex 4: partial.** Failed-candidate readmission is defined, but request-branch deletion still conflicts with delivery idempotence.
- **Opus N1: answered.** Isolated requests retain promotion as the line’s one writer, with currency and authority rechecks.
- **Opus N2: answered for its stated loop.** Closed-request adoption is removed; replay conflicts and gate hand-backs have distinct recovery.
- **Opus N3: answered.** Bound-member waiting, inclusion-based omission and the head’s combined recheck are explicit.
- **Opus N4: answered.** Run-owned requests produce no dispositions; *rebase it* retires.

These are design answers, not demonstrated proofs.

Blocking findings, with code paths relative to the driver project:

1. **Deleting a request branch strands an unchanged retry.** Design §2.10 deletes it on failure. Sequence: rebase returns W; fast-forward creates branch P and settles delivery D; integration’s gates hand W back without moving the line; P is deleted; the person completes the recovery task; rebase returns W again. `Workplace/DeliveryStore.cs:307–311` returns delivered D and allocates no generation. P cannot be recreated through its completed phases; fresh integration admission finds no branch. **Smallest change:** define a new delivery generation after the owning step’s proven branch deletion, preserving D unchanged and reconciling uncertain effects first. Prove this sequence across restart in h.

2. **Ended runs are not necessarily permanent history.** Design §5 excludes them. `Workflows/Nodes/NodeRunKeeping.cs:96–100` explicitly reopens a run when its chain returns; `Workflows/WorkflowRunStart.cs:147` retains its existing binding. Sequence: finish a run, upgrade without converting it, then return its chain here: it still binds the unsupported `land` graph. **Smallest change:** convert every retained binding/version that can reopen, preserving its ended marker. Add this reopening sequence to h.

3. **The conversion table omits stopped and invalidated land states.** Both exist in `Workflows/Nodes/NodeContract.cs:145`. A stopped land can coexist with an independent active task, leaving the run open at upgrade. No mapping specifies how its Stop survives; treating it as fresh permits landing against that word. **Smallest change:** map both states explicitly, preserve Stop, and settle outstanding effects before restarting invalidated work. Require coverage against `NodeStates.All`.

4. **The delete census still misses a deletion path.** §2.7 omits `GitRefActs`. Its `DeleteAsync:127` and creation undo at `UndoAsync:86` construct deletes directly, outside `JournaledDeletes`. Sequence: an act-created branch becomes an open run’s followed work branch; undoing that creation deletes the unchanged ref without the proposed open-run guard. **Smallest change:** put delete, undo and rename removal through the same ownership guard; census all branch-deleting transactions.

The records post-check, target-rebind, selection-symbol, say-symbol, kit and door censuses can catch their declared omissions. The recursive conversion census catches omitted **fields**, not omitted states or excluded runs. The delete census would remain green on finding 4.

**Rows a–j are dependency-ordered correctly**, including check before merge’s proofs, g before h, and D232’s j condition. Their proof plan needs the four sequences above; h’s completeness claim is premature.

## Opus (independent), round 3

The independent Opus 5.5 reader's read of round 3 at `fd2c1421c6`, read-only; as it answered, set down by the steward. Verdict: a round 4; N3 not answered, five new blocking findings, all in code round 3 had not read. Paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

**Round 2's findings.** Codex 1 answered. Codex 2 answered for the node record (`NodeAcceptances.cs:78-114`, `WireLapsing.cs:58-61`, `WireEvaluation.cs:339`), its census too narrow. Codex 3 answered: each open digest converted, `NodeGraphs.cs:25-28`'s limits raised, a refused version recovered as a new run. Codex 4 / N2 answered in the design: adoption by invocation replaces the newest-request-in-any-state lookup (`IntegrationStore.cs:162-165`, `IntegrateStart.cs:208`), and `against` tells a promoted conflict from a failed candidate, but requests written before h carry no invocation (N7). N1 answered: the promotion's compare-and-set (`IntegrationPromotes.cs:380-418`) stays the line's one writer, the epoch loop (`IntegrationReplays.cs:155-162`) can stop before a merge request. N3 not answered (N6). N4 answered.

**Would each census go red on a missed member?** The records post-check: no. `TASKS.md`'s row *WFREVIEW3a, 3d, 3l and 3m* is a compound id in the backlog, but §2.3 parses compound ids only on archive lines; the work archives 3a (the row becomes *3d, 3l and 3m*) while the line archives 3l (*3a, 3d and 3m*): each new head is added by one side, both stand, no literal id repeats and none matches an archived head, so the check passes with two rows reopening finished items. Parse every row's compound id and apply each rule per id. The conversion census: no, scoped to records a `land` writes, it misses what h newly reads (the requests' asking invocation, N7; the binding's resolved `Land`, `WorkflowRunBinding.cs:47`, written at `WorkflowRunBindings.cs:136-139`). The selection census: no; it misses the 23 sources comparing `node.Kind == NodeKinds.Land` and the readers of the bound `Landing` intent (`WorkflowProcesses.cs:118-136`, `IntegrateChoice.cs:37`, `GraphGate.cs:117`, `WorkflowGate.cs:67-76`, `TreeLandPlans`, `LandingGate`); a reader rewritten to take the first `IMoves` node stays green and reads the wrong merge in a release graph. The delete census: no; `GitRefActs.DeleteAsync` and its rename (`GitRefActs.cs:118-165`) run their own `ref.delete`, and `TreeOpenings.cs:118` deletes outside `JournaledDeletes`. The say symbols and `behind`: yes.

**New blocking.**

- **N5. The hand-off recorded as a journal act holds the whole repository.** `ActReconciler.cs:53` reads every unclosed act in the repository's shared git directory; one running here holds every writer (`:143-147`), one not running here is taken over and closed (`:164-177`); the promotion (`IntegrationPromotes.cs:372-375`) and the tree update (`TreeSync.cs:214`) ask it. Sequence: R1's `resolve` records its hand-off and waits 30 minutes; R2's candidate is held at its promotion throughout, or the first writer closes the reservation. Change: the hand-off its own record, read only for its tree.
- **N6. Nothing writes the together operation's landing.** `TogetherOperations.Landing` and `Settle(Landed)` are written only at `TogetherAccepts.cs:116,151`, reached only from `LandPerformer.cs:259-262`, which h deletes. Sequence: the head's `merge` lands; the operation stays `ready`; `DeliveryReach.cs:49-55` reports every member `bound` for ever and their runs never end. The head is not a member (`:47`), so its `rebase` can start mid-join, which `LandPerformer.cs:253-257` holds today. Change: the head's `merge` writes the operation's attempt before its first phase and settles it by phase; the head's `rebase` waits until the operation is `ready`.
- **N7. A run whose request is open at the upgrade fails.** Requests carry no key or invocation column (`IntegrationStore.cs:64-67`) and §2.10 finds closed requests by invocation only. Sequence: an open request settles after h; the reconcile finds none and withdraws the attempt (`IntegrateStart.cs:234,269-284`); the new ask is refused (`IntegrationRequests.cs:226-233` or `:411-414`); the node fails though its work is on the line. Change: the migration writes each waiting attempt's invocation onto its requests.
- **N8. `merge` into a branch drops today's refusals.** A chain branch is advanced today only when no working tree has it checked out and its work is not already on the line (`TreeBranches.cs:320-325,362-390`); §2.6 keeps neither. Sequence: the person's checkout is on the chain's branch; `update-ref` moves it under the checkout; the person's next commit reverts the step. Change: the judgement keeps `LandingAdvance.Refusal`'s facts: a checked-out branch waits, said; a completed one fails.
- **N9. Two askers to commit, against D228.** §4 keeps `TurnCarries` beside `resolve`'s `uncommitted`. Sequence: a finished quest's dirty tree is asked to commit up to `TurnCarry.Bound` times, then three more per stop; today's one shared limit (`TurnCarry.cs:26`) is lost, and `TurnCarries` talks to a session whose tree is mid-hand-off. Change: one asker; `TurnCarries` stands down where a `rebase` follows and reads the hand-off.

**Not blocking.** `exists` refuses an existing `release` branch, against §3's release promotion. Release's `behind` goes back past `review`, so the person is asked again each time the line moves. A `merge` into a branch must write the landing provenance the node route reads (`IntegrationRequests.cs:128-158,255-260`). The conversion opens the desktop store, which `StoreNamesStep` upgrades, so "reads nothing another step writes" is false. What `conflicted` does when the request branch's delete fails and the branch is kept is not said.

**Rows.** a to j ordered right (g before h, i and j placed right). Proofs missing: a, compound backlog rows; d, another run's promotion and a tree update while a hand-off is open; e, the operation's landing written and the head waiting, a branch target checked out, a completed one; h, a pre-h request closing after the upgrade, the binding's `Land` mapped, every `NodeKinds.Land` reader named.


## Codex, round 4

Codex's read of round 4 at `b62716d462`, run by the steward from outside the install, read-only; verbatim.

**Another round is needed.** The generic kinds and workflow-level landing composition follow the direction. Round 4 answers the named round 3 sequences in design, but leaves ownership and request-correlation gaps.

The individual answers are:

- **Codex 1:** §12’s proven delivery end permits a new generation without rewriting the delivered row.
- **Codex 2:** §16 converts retained bindings, including ended runs that can reopen.
- **Codex 3:** §16 covers stopped, invalidated and unknown nodes, preserving Stop and reconciling unsettled attempts separately.
- **Codex 4:** §12 brings delete, creation Undo, rename, rename Undo and opening Undo through one release.
- **N3/N6:** §14 holds the head before readiness and assigns the operation’s landing and settlement to its `merge`.
- **N5:** §13 separates a tree hand-off from repository-wide journal acts.
- **N7:** §16 backfills waiting attempts’ invocations onto pre-upgrade requests.
- **N8:** §15 retains branch occupancy, provenance, movement and completion checks.
- **N9:** §13 assigns one commit asker per tree.

These are design answers, not demonstrated proofs.

Two blocking gaps remain:

1. **Ownership can change after release approval — §§12,18; `WorkflowRunStart.BindOneAsync`, `RefTransaction`.** A remover obtains a release for branch P at W; a start then binds an open run to unchanged P; the remover deletes P successfully because git verifies W, not ownership. The new run loses its work ref and waits for manual repair. A recovery ref preserves commits but does not preserve the run’s operation. **Smallest change:** hold one shared exclusion across the ownership read and removal, and require binding/reopening to participate. Prove both interleavings in f, including restart recovery.

2. **Adopting an existing open request needs durable correlation — §2.10; `IntegrateStart.AnswerAsync`, `IntegrationStore.FindByKey`.** The door may adopt an open request for the same unit/tip that another attempt or the card created. Its asking invocation remains another’s or absent. After that request closes, a restart searches by the adopting attempt’s invocation; closed requests another attempt asked are forbidden, so reconciliation loses the request and fresh admission can fail because the line already holds the work. §16 fixes this at upgrade only. **Smallest change:** persist the adopted request ID on the attempt before relying on it; reconcile by that ID. Preserve the request’s original requester and authority. Add adoption–closure–restart proofs to h.

**Would every census go red? No.**

- Records: a’s compound-backlog fixture addresses the known leak; the rule and post-check must expand compound IDs on **every row**, not just archive lines.
- Conversion: recursive fields and enum coverage catch omitted members/states; captured-home residue and reads catch exercised paths only. §18 correctly admits uncaptured record kinds escape.
- Selection: deleting old symbols makes their readers compile-fail; the source census catches its planted selectors, but helper-based first-move selection escapes.
- Removal: typed staging and caller coverage catch the named bypasses; they do not catch the ownership race.
- Say, target-rebind, kit, door and journal-deadline checks can reject their declared omissions. The orphan-write census establishes a surviving caller, not that execution reaches it.

**Rows a–j remain dependency-ordered correctly.** Their proof plan now includes the prior missing sequences, but needs the two additional proofs above. Installed evidence remains i’s separate obligation; j correctly follows D232’s next shape-changing release.

## Opus (independent), round 4

The independent Opus 5.5 reader's read of round 4 against the code at `b62716d462`, read-only; as it answered, set down by the steward. Verdict: a round 5; three new blocking findings, past the two Codex found.

**Round 3's findings.** Codex 1 answered by §12's end rows and next generation; Codex 2 in §16; Codex 3 in §16 keyed by `NodeStates.All`; Codex 4 by the release inside `RefTransaction`, its census leaking (below). N5 answered for the journal, one remover still ignoring the hand-off (B3). N6 answered for the head, a member's `merge` unguarded (B2). N7 answered but for Codex's adoption id. N8: the refusals kept, but `completed` now loops (B1). N9 mostly: `SessionWaits.CarriesOn(accepted: CarriedAccepts…)` (`Loop/DriverContinuations.cs:418-421`) still decides who asks, and h does not delete it.

**A planted member each census misses.** The records post-check: none found. `BranchRemovalCensusTests`: `update-ref --stdin` fed a hand-built `delete refs/heads/x` line (the shape of `RecoveryDropping.cs:95`); a hand-made `ActStepPlan(Ref, {"input": …})`, which `ActSteps.cs:91-101` runs as given; part 3 checks who calls, not the `by` passed, so a listed caller passing another run's step passes. `ActJournalCensusTests`: an act with bounded steps whose `After` callback waits on a session turn (`ActRuns.cs:195` runs it with the act open and the checkout held). The residue census: `carried-accepts.json`, which names neither `land` nor a digest. `MoveSelectionCensusTests`: `Nodes.Single(n => n.Id == "landing")`, since the conversion keeps the `land`'s id on the `merge`. The orphan-write census checks that a caller exists in source, not that it is reached.

**Blocking.**

- **B1. A chain's completed branch loops with no remover** (§15, §2.2, §12; `LandingAdvance.cs:31-50`, `GitRefActs.cs:210-213`, `TreeSync.cs:276`, `IntegrationPromotes.cs:528`). Sequence, *Integrated*: step 1's `merge` fast-forwards P to C1 and R1 is admitted; step 2's quest keeps the run open; R1 promotes, and the integration's tidy of P is refused since an open run governs P; step 2 rebases onto P (`onto: target`, P exists), its `merge` fails `completed` to the person's task; the task's `done` returns, the `rebase` lands on P again, `completed` again. Nothing can remove P: the person's delete refuses a branch a landing made, *Bring up to date* and the look's sweep are refused by the release. The same when a pull request completes before the next step. Change: `completed` a routed outcome; the `merge`, as the run's own step, ends P through the release (recovery ref, end row) and goes `back` to its `rebase`, which then reads the line.
- **B2. A together member lands alone** (§14, §2.6; `TreeLands.cs:141-152`, `TogetherPress.cs:229-279`). Sequence: a member's `rebase` has reached C_m and its `check` runs (*Release*), or its `merge` is not yet admitted; the person presses Together; the press's guards pass (no request open, the landing records hold C_m, the press compares the member's tree tip); `TogetherBinding` is read only at `rebase`'s start and the `merge`'s judgement has no member check, and it takes the git lock, not the tree's landing hold the press takes; C_m lands alone, the head replays it again, and the member is both delivered and brought-into. Today the landing refuses a bound tree under its landing hold, at the effect. Change: the `merge`'s judgement reads `TogetherBinding.Of(work)` under the member tree's landing hold and withdraws; the press refuses a member whose `rebase` has reached while its `merge` has not settled.
- **B3. The person's discard removes a reserved tree, losing the resolution** (§13, §2.4; `TreeTidies.cs:68-71`, `TreeRemovals.cs:53-70`). Sequence: a resolver's turn ends mid-edit with its hand-off open; the person discards the session; `TreeRemovals` checks the landing hold, the tree lock, `TogetherGuard` and the active sessions, not the holders or the hand-off; `worktree remove --force` drops the staged resolution before §2.4 point 4 writes it to the edits ref; the release keeps the branch and the hand-off row stays open on a tree that is gone. Change: `TreeRemovals` reads `TreeHandOffs.Of(tree)` under its landing hold, or the hand-off holds that landing hold for its life.

**Not blocking.** `TogetherSettle.Decide` (`TogetherSettle.cs:45-55`) reads a request closed `behind` on a moved line as uncertain, so the members stay bound after a restart between the close and the `merge`'s read (and a pre-h together request h's replay closes `behind`): read an unpromoted close as unmade. §2.6 gives no outcome when a `merge`'s line request is closed by the steward's drop or decide. `TidyHolders` reads only the service (`:62-83`), so reading `TreeHandOffs` needs the home. The resolver's own turn must be exempt from *a session's start in that tree waits*.

**Rows.** a to j ordered right. Proofs missing: f, B1's chain with its first step completed; c and e, a member whose `rebase` reached before the press; d, a discard while a hand-off is open.
