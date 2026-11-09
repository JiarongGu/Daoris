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
