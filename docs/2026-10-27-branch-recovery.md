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
in main's history. The two settled trees are gone; the unfinished trees remain.

A private Git bundle was verified as complete history, containing 27 refs at its read. The old test
worktree's three modified files and the nested merge's 21 working files were copied with matching
hashes, alongside their binary diffs and six raw Git metadata files including the index and merge
heads. These preserve authored state; no unfinished merge was aborted or discarded.

## Permission recovery

Asking mode restored a person-answer path after an Auto refusal, but selects Codex's `read-only`
posture, making ordinary edits require approval. The installed adapter separately offers user-reviewed
`workspace-write`; CODEXPERM1 holds that mapping review and the unresolved Auto refusal.

The owner subsequently authorized unattended continuation and had already authorized lifting these
controls. The repository's existing permission setting was set to `unrestricted`, leaving its other
pauses as configured. Daoris applies this at a turn boundary; it is not an appointed role's authority
or an approval forged for a held call. ROLEPERM1 remains a later reviewed extension of D208/D209.
