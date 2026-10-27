# Branch recovery — 2026-10-27

The owner authorized auditing every Daoris branch and worktree, completing and merging their work,
then cleaning up, including repairing Daoris's driving where that prevents completion. Complete the
ongoing FOUNDATION1 refactor before provider infrastructure and new role approval capabilities.
This is an in-progress recovery record; TASKS.md owns its remaining work as BRANCHRECOVER1.

## Initial inventory

At `9e6e3cf6c`, Git listed eight local branches and nine worktrees, counting main and the detached
harness/gate trees. Session records were checked alongside Git; a completed session alone does not
prove its worktree is disposable.

| Branch | Observed tip | Finding and next step |
|---|---|---|
| `main` | `9e6e3cf6c` | Clean integration line. |
| `daoris/s-a002fd69` | `61b10d4dc` | FOUNDATION1 d-i, three commits ahead. Landed afterward in `bb20937bc`; see below. |
| `daoris/s-e6f08d35` | `cab2487f2` | FOUNDATION1 mod-viii, eleven commits ahead at the read; session `69874b96` remains active. Finish its cuts and required checks before integration. |
| `daoris/s-544641a6` | `c66897961` | Tip is already in main, but three authored test edits remain uncommitted. Review and recover them before cleanup. Its nested fixture is a separate registered worktree with an unfinished merge and a conflicted backlog; preserve its files and index stages. |
| `daoris/s-e57d9ada` | `d580caeb4` | Clean, tip already in main, completed session `121378e5`. Check retained private output, then use Daoris's own housekeeping. |
| `limitgoon1-build` | `5a2cd2a1f` | Landed by the explicitly recorded squash `2d2aedca0`. The compared production files agree; the squash adds coverage of an earlier answered exchange. Preserve provenance before retiring the obsolete branch. |
| `quest/fabric-c2a-1ce2a0cd9ebb` | `9b3ac2762` | Two commits not ancestral or patch-equivalent to main. Reconcile with the other attempt and FABRIC-C2a's current contract; do not assume its archive entry means it landed. |
| `worktree-agent-ac4bd4973eec96ec9` | `8d266a3c2` | Four commits; only the initial start is patch-equivalent to main. Reconcile its lineage fixes with the newer attempt before selecting one result. |

The detached trees serve the coding harness, the installed-build gate and the current scoped merge.
Their ownership and active use must be checked before cleanup. The active scope tree is not a cleanup
candidate. The merge tool's prune plan defers both merged `daoris/` branches to Daoris's housekeeping;
it identifies the earlier gate tree's completed stage, not permission to remove authored evidence.

## Foundation landing receipt

`bb20937bc` integrated FOUNDATION1 row d-i on 2026-10-27: keyed destination receipts and host-run
negotiation, plus OPENANSWER1's successful-reply validation. Executor adoption remains D212's later
row d-ii. Integration gates passed: universal, code-map, orientation index, test census, CLI, service,
driver, the reached driver Process classes and the reached Git classes. The integrated fast driver
suite passed 14,943 tests. This receipt does not claim the deferred rehearsals or an installed build.

The first candidate was handed back for a hand-over test's private failure bound, with a separate
Process sighting reading an empty heartbeat. The correction uses shared failure patience and publishes
fixture beats by rename. The interrupted-write regression failed first, both affected classes passed
21 tests, and the branch's whole fast suite passed 14,938 tests. HANDOVERPATIENCE1 and BEATWRITE1's
fix-log entries retain the mechanism and the unproven scheduling cause; neither old failure was waived.

Background CI on `0d4bb4b78` then finished green: ten gates ran and passed, nine verdicts remained
valid, and the family and deployment rehearsals remain owed before a stage. The known-red ledger
still retains an older opinion-resume finding; CILEDGER1 requires checking that exact case and its
clearance rather than treating the green summary as evidence that every historical finding closed.

The exact opinion-resume class subsequently passed five tests on `0046e5f3d`. This standalone result
does not clear the background ledger; a recorded integration pass is still owed under CILEDGER1.

## Knowledge integration checkpoint

Candidate `a34bb83` was handed back without promotion: fourteen gates passed, including all three
Process checks, while the driver's fast suite passed 14,956 tests and failed three. The queue pinned
the marker-writer and participant-reader inventories to the Knowledge change. The older held-file
fixture failure did not reproduce with its signature in the whole-candidate diagnostic; no failure
was waived.

The clean repair tip is `2ef73c9f9`. It preserves the connector's marker writer under its lower-owner
alias, keeps both repository-identity spellings visible in the shared reader scans, and updates the
exact moved-reader inventory. GITSEEDWAIT1 replaces the fixture's timer race with release after an
actual refused deletion, retaining the cleanup bound. Focused driver checks passed 42 tests, modules
and service checks two each, merge/source-parser checks five, CLI record checks 51, and vocabulary
checks two. The structure census remains 431 edges in 48 families. Requeue, full integration and
landing remain owed; the quest remains Taken.

The repaired Knowledge branch subsequently landed as `85fd557`. Its whole nightly run exercised all 21 gates with no
verdicts reused: CI finished green under the declared policy, with twenty passes and one Process flake. The full driver
and Git halves, family rehearsal, deployment rehearsal and browser checks passed. The modules Process flake passed alone;
its original failure was a busy-directory exception in fixture disposal, recorded as HOSTCLEAN1, not a failed host-kill
assertion. This is qualified integration proof, not a claim that the fixture defect was repaired.

Quest `c8f5e116b346` is Done through the owner's supported completion door, with that qualified receipt; it had no
requirements to answer individually. Session `69874b96` remains parked until the window confirms its finish. Its clean,
merged tree is preserved while that request waits; no live-session guard or lock was bypassed for cleanup.

The pending finish subsequently completed through the supported request result. The service now records session
`69874b96` as completed. Daoris independently moved its tree aside under retention; the active folder and Git
registration are absent, and its branch answers missing (exit 2). The merged tip remains in main. Four local branches
and four registered worktrees remain at this read: main, the active Agents branch and the two held Fabric branches;
the registered trees are main, Agents and the two integration trees. Retention preserves the Knowledge output;
this is retirement of its active tree, not reclamation of its retained disk space.

That nightly run also passed the exact historical opinion-resume case and authoritatively cleared its finding on
`85fd557`. CILEDGER1 is archived; no clearance was fabricated or written by hand.

## Agents continuation

The accepted Knowledge prerequisite allowed a fresh Codex conversation to continue mod-ix on `daoris/s-0db6cee1`.
The configured Build actor's choices were unavailable; the supported one-off chat selected Codex without changing those
standing choices. Its native first-turn trace reports `gpt-6.1-sol` at medium effort. Its quest remains Taken.

Reviewed checkpoints through `58bfc98ee` move the Agents sources/tests and cut eight dependency families, preserving
behavior, public serialized shapes and exact instruction output. The census is 390 edges in 40 families, with only
Context's 25 and Course's 16 left for Agents. Each checkpoint's focused proof is in the branch's D212 notes; the complete
mod-ix row and its integration proof remain open.

Context was then committed on the cumulative branch as `acea4d5c4`, with test-only follow-up `c75302ca2`. Its 25 tuples are cut,
leaving 365 edges in 39 families and only Course's 16 for Agents. The corrected 776-case focused selection includes the
original asynchronous transcript behavior; CONTEXTVISIBILITY1 retains its fixture correction through integration.
Course has started under the same conversation, with message acknowledgment and recovery behavior kept as the review
boundary. Nothing here marks the full mod-ix row, its quest or its parent integration complete.

A combined focused run also exposed AGENTPORTISOLATION1: temporary test wiring reached an unrelated prompt consumer.
The four new replacing fixtures were isolated, and a 494-case combined selection passed. The follow-up audit includes
three earlier fixtures with the same shared-state hazard, inferred from their code rather than additional observed
crossovers. Product behavior and assembly-wide parallelism are unchanged; the repair must land with the refactor.

At this checkpoint there are five local branches and five registered trees, including the new active Agents tree and
the still-preserved Knowledge tree. Both remote tracking tips have no commits outside main. The saved credit-refusal
stash and superseded provenance tags remain preserved; neither is treated as disposable unfinished code.

## Recovery order and preservation

1. Finish mod-viii in its saved conversation and tree, then the remaining foundation rows in D212's
   order. Keep changes that share files out of one another's way.
2. Recover the old transient-retry fixture edits and unfinished merge. Compare each change with main
   before adopting it; broad retries are not accepted merely because they make a fixture pass.
3. Reconcile both Fabric attempts against the accepted lineage policy and the current provider doors.
   Keep staging's required installed-root proof distinct from mocked protocol tests.
4. Back up original tips and authored uncommitted state before removing anything that only those
   trees keep. Validate the coherent result through the merge queue and full integration gates.
5. Remove only settled branches and disposable trees through their owning lifecycle. Keep live work,
   private output and required evidence. Ref removal verifies the exact refs its proof read.

## Cleanup receipt

The completed `daoris/s-e57d9ada` was removed through Daoris's own `trees remove` on 2026-10-27.
Immediately before removal its tracked tree was clean, its only ignored entries were dependency
directories, its session was completed, and its tip was proved ancestral to `0d4bb4b78`. Afterward
the directory was absent and `git show-ref --exists` answered missing (exit 2) for the branch. The
session record and its history remain in Daoris. No unfinished worktree was removed.

The landed d-i branch and its worktree were also absent at the later preservation read: the branch
answered missing (exit 2), its directory and Git worktree entry were absent, and `61b10d4dc` remains
in main's history. A subsequent `trees list` read shows that Daoris retained this tree in its moved-aside
area under the configured 14-day policy. Its active path and registration are gone; reclaiming that
retained folder remains part of cleanup. The unfinished trees remain.

A private Git bundle was verified as complete history, containing 27 refs at its read. The old test
worktree's three modified files and the nested merge's 21 working files were copied with matching
hashes, alongside their binary diffs and six raw Git metadata files including the index and merge
heads. These preserve authored state; no unfinished merge was aborted or discarded.

`limitgoon1-build` was retired after comparing all eleven paths of its original commit with the
accepted squash: production agrees, while the squash adds documentation and stronger answered-exchange
coverage. The original tip remains at `superseded/limitgoon1-build-5a2cd2a1f` and in the verified bundle.
Tag creation and branch removal were one Git transaction verifying main and the old branch tip;
the removed branch then answered missing (exit 2), with no worktree using it. Its implementation was
already landed by squash; the old mis-signed history was not merged into main.

The three uncommitted files in `daoris/s-544641a6` were then proved byte-for-byte identical to accepted
main ancestor `9b489edc5`. Its nested integration probe held no additional source implementation:
resolved sources matched its incoming parent, and the remaining conflict was historical backlog text.
After rechecking all 21 saved working-file hashes, the probe's merge was aborted and its clean
registered worktree removed through Git. Only the three proven duplicate outer files were restored
to that branch's own HEAD.

Unforced Daoris removal protected twelve unique ignored outputs. No forced deletion followed.
Daoris's background tidy independently moved the settled tree aside; its active path, branch and
registration were then absent. Its retained fixture output was copied into the private recovery
backup, with nine flake logs hash-verified. The moved-aside folder remains under retention.

The obsolete detached review tree was also removed through normal Git worktree removal after its
tip's ancestry, clean state, containment, absence of reparse points and named live processes were
checked. Its private scratch was copied first. Four local branches and four registered worktrees
remain at this checkpoint: main, Knowledge and the two Fabric attempts; the registered trees are
main, the active Knowledge tree, and the two integration trees. Neither Fabric attempt was discarded.

## Permission recovery

Asking mode restored a person-answer path after an Auto refusal, but selects Codex's `read-only`
posture, making ordinary edits require approval. The installed adapter separately offers user-reviewed
`workspace-write`; CODEXPERM1 holds that mapping review and the unresolved Auto refusal.

The owner subsequently authorized unattended continuation and had already authorized lifting these
controls. The repository's existing permission setting was set to `unrestricted`, leaving its other
pauses as configured. Daoris applies this at a turn boundary; it is not an appointed role's authority
or an approval forged for a held call. ROLEPERM1 remains a later reviewed extension of D208/D209.

The next saved-session turn confirmed effective unrestricted access and completed its ordinary edits
without permission refusals. It still reported missing knowledge tools; CODEXTOOLS1 requires an actual
inventory and fresh-session comparison before assigning that observation a transport cause.

A fresh one-off Codex chat reproduced the empty tool inventory. The pinned native runtime's trace
explicitly omitted `daoris-knowledge` as a pending optional server at its initial catalog capture;
the MCP host independently logged its start. CODEXTOOLS1 now requires proving readiness before that
capture, followed by actual knowledge reads and quest completion. Host startup alone is not that proof.

The unchanged live conversation's next turn exposed 51 Daoris tools, successfully searched knowledge and took its quest.
This contrast retains CODEXTOOLS1 as a first-catalog readiness defect rather than a failed-host diagnosis. Its six live
MCP calls also supplied the Codex measurement for FABRIC-ACP1: D173 records why the current normalized lineage still does
not meet C2a's root contract. The two enforcement branches remain preserved until that staging prerequisite is met.

## Agents integration and tooling checkpoint

The Agents branch's repaired cumulative tip `a9bdbba9e` landed as `93f49d4` through the supported queue. D212's scoped
integration receipt names its passed checks and remaining whole-product and installed proof; the full nightly run is
in progress on that exact commit. This advances FOUNDATION1 mod-ix without closing the complete refactor or activating
either held Fabric enforcement attempt.

Integration exposed a Git configuration problem: with worktree-specific configuration enabled, a shared `core.worktree`
bound linked trees to the main checkout. Their files were intact. The shared value was backed up and migrated to main's
own worktree configuration through Git; each linked tree then reported its correct root and clean state without source
restoration. The actor that wrote the shared value is not established. GITWORKTREECONFIG1 retains the product/tooling
guard against acting on a mismatched root.

SCOPEPLAN1 and QUEUEDROP1's implemented repairs also landed in `93f49d4`; their root causes and qualified regressions
are recorded in the fix log and D207. The separate QUEUEDRAIN1 task still owes immediate cancellation of dropped gates.
The old, explicitly dropped runner was ended after verifying its identity and checking its owned descendants; no installed
session or unrelated process was ended to make a build pass.

At this checkpoint four local branches and five registered worktrees remain. The Agents branch is fully held by main
and stays active while its terminal chat is needed for proof. Both unique Fabric attempts and the credit-refusal stash
remain preserved. The extra clean managed `scope-planning` checkout contains no authored changes from this recovery;
its owning manager twice refused archival as belonging to another task, including after the Git configuration repair.
It remains attached and was not removed through another mechanism. This ownership refusal is distinct from the
original scratch deletions rejected by automatic command review. Neither blocked cleanup is reported as completed.

## Terminal chat live-status defect

CLITURN1 was reproduced on installed build `349e2c5d7` during a read-only knowledge-tool turn. The terminal's user event
at 23:23:06 UTC and turn-end event at 23:24:02 UTC on 2026-10-26 bracket the native screenshot at 23:23:17 UTC; its actual
MCP calls occurred within that turn, while the desktop head and rail showed idle. The service record still said working.
The transcript had refreshed to the preceding completed turn, so this observation does not establish lost transcript data.
Private event and native-capture evidence is retained separately; the marker and knowledge reads completed normally.

The terminal creates its own SessionProcesses and ChatRunner. The window's ConversationRoutes.QueueAnswer reads its own
loop's queues, and ChatRunner.Queue returns ChatQueue.Idle for an id its process does not hold. This explains an idle answer
without establishing that the external process is between turns. The frontend's turn contract explicitly distinguishes an
unknown answer from not taking. CLITURN1 retains the ownership-aware read/routing repair and an installed cross-process
regression; cancellation and message delivery from that window to the external chat were not tested in this reproduction.

## Agents validation follow-up

Repair candidate `3c57df7` passed seven of eight executed gates and retained three earlier passes. Its whole web gate
passed 9,713 tests; Driver passed 15,060 and failed one ReviewGraphCensus combination (`set`, `grown-held`, `both`,
not incomparable): K′ was recorded in round four at depth two. The prior shared-read, analyzer and two signal-patience
failures passed in that full run. Both isolated graph-class probes passed 189 cases; the queue classified the failure
unresolved and handed request `8a02373e-be9d-4598-863e-6062c57e950c` back at 01:14:50 UTC. Main remains `76de91d91`.

The fixture judges only already-created nodes and a timed quiet interval; the graph intentionally creates K′ after E's
answer continuation. This leaves a gap its current snapshot cannot prove closed. GRAPHQUIET1 retains the fixture-only
completion-frontier repair and deterministic regression; the precise scheduling delay is unestablished, and no production
scheduling defect or fix is claimed. The failed full run remains distinct from its passing probes. Integration, stage
checks and publication are still owed; the active native conversation and authored branches remain preserved.

## Agents accepted and installed

The repaired source landed as `83ac0a968`. The full Driver suite passes 15,081 cases and web passes 9,713 across
443 files. Stage CI ended green at 02:29:56 UTC: its 24 browser cases ran freshly, 18 gate passes were retained and
the two Process halves were composed from valid class passes. Normal publication admission passed all 21;
this is qualified stage evidence, not a fresh whole-run claim. Nine completed integration repairs moved to the
archive in the records-only `8968c5087`. CIFINDINGSKEEP1 retains the separate historical finding-ledger discrepancy.

The worker completed quest `23d65c45215e` through its actual MCP tool. Its terminal then received normal end-of-input
and exited successfully with session `66cc32b2` completed. Daoris retired its merged branch and Git registration
and moved its tree aside under retention. The parent repair branch was separately retired after a complete,
verified recovery bundle and an atomic ref transaction verifying main and the judged tip. Authored work and proof
remain preserved; retention is not reclaimed disk space.

The exact gated `83ac0a968` was published locally as build `20261027T024647Z-f103f22a`. The automatic swap is
confirmed installed; the application binary and live HTTP service both report that full commit, and a native
window capture shows the updated application. A fresh installed terminal conversation successfully called the
knowledge search and quest-list tools on its first turn. CODEXTOOLS1 remains open because this passing observation
does not explain its earlier intermittent absence; KNOWCURRENT1 records missing current decisions separately.
The service reports lexical-only knowledge, with semantic capability unavailable; no stronger tier is claimed.

The merge tool removed the completed gate checkout through its normal prune. Before the new Workplace start,
the only remaining branches were main and the two unique held Fabric attempts. The managed checkout's prior
ownership refusal and the earlier command-review refusal still stand. The reusable scope checkout remains with
the integration workflow. The stash and held Fabric commits remain preserved for their prerequisite work.

Workplace's mechanical ownership checkpoint is published as quest `11bc8fdf13db`, ask `7cf83e`, from `8968c5087`.
Its new terminal conversation `1903b870` starts with read-only discovery and inventory verification; implementation
and taking the quest wait for the parent's next explicit release. FOUNDATION1 and BRANCHRECOVER1 remain open.

## Workplace integration and retained Fabric evidence

The mechanical Workplace source `faae42db9` subsequently passed the parent's byte and census audit and landed
through the queue as `9884178`. D212 records its twelve passing gates and one classified flake, retained as
TREECLEANFREE1. The same quest and terminal remain active for the separately reviewed Context eight-edge cut;
the active tree is not cleanup material. Installed acceptance still belongs to `83ac0a968`.

The new installed conversation's lineage projection was also inspected: eleven Codex call rows retain equal
thread/root identifiers, but none retains a valid `source=user`. That projection is lossy and does not prove
the raw metadata omitted the field. It cannot establish D173's required root provenance, so neither held
Fabric enforcement attempt is ready to land. Both branches and their unique work remain preserved.
