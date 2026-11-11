# KNOWCENTRAL1, round 2: Daoris as the coordinator of a shared, model-kept memory — the brief

## Why now

Lyntai has built its half of the memory design (its D241, amended after both reads of round 1): an entry ended by one call, guarded by an opaque revision (MC1); candidates a write may update (MC3); `Disputes` and `Amends` as attributed relations (MC4); an approver seam with no approver that approves by itself (MC6); write and end tools for an agent (MC2); a host's re-check through `Recheck`, `ConfirmAsync` and `DueAsync` (MC5); and an isolated-arms instrument for a model's wrong ends (MC8, no verdict yet). MC7, a read-only view of another task's memory, waits for this repository's contract. D241 places these in the coordinator, which is Daoris:
- caller identity, with read, propose, write and end grants enforced on every operation, an expansion by reference included;
- the change log with subscriptions and cursors (the library's `DueAsync` sweep is eventual repair, not proof of currency at presentation);
- the request that carries a reader's proposal to a fact's owner;
- citing this repository's records by reference and the revision read, with `Recheck` asking Daoris's index;
- the check that cached material is current before it is presented as current.

## What binds every proposal

- D220: a fresh fact replaces the one it updates; one home per present fact; history apart.
- KNOWFRESH1a as built: the knowledge index's hit standing (commit, uncommitted state, superseded and amended marks).
- D241 as amended: retirement through the decay seam, the writer or confirmer deciding; nothing ends with no approver; no approver approves by itself.
- The constraint every change serves (AGENTS.md): orchestration central, ownership local, a session changes its own repository and asks another through a quest. Doctrine works without a service. Model-dependent capabilities state the tier that ran.
- FOUNDATION1 as built: the participant's work read once, one policy shape keyed by the work (`WorkPolicies.Of`), the connector by work, failing closed (`CallTable`), a child's calls read-only by their lineage (FABRIC-C2a).
- Shared hosts answer no machine paths and serve no UI.

## The questions

1. **Where memory lives and who owns a fact.** One memory per repository, per workspace, or per role's task? Which session or role writes and ends a fact, and how is that ownership recorded?
2. **Grants on every operation.** How do D241's read, propose, write and end grants map onto the work's policy and the call table (FOUNDATION1q, r) without a second policy shape? What does a child, a helper, a reviewer or Ask Daoris get?
3. **The change log and cursors.** What records each change, who subscribes, and how a reader's cursor resumes after a restart: the event journal (FOUNDATION1k to m), or a log of its own?
4. **A reader's proposal.** How a session that observes a fact it does not own proposes the change to the owner: a quest, a word, or a new kind of request. Who confirms it (MC6's approver), and what a person sees.
5. **Records cited.** How a memory entry cites this repository's records (a decision, a note, an archive entry): the reference and revision shape, and how `Recheck` reads the index's standing.
6. **MC7's contract.** What Daoris asks of Lyntai's read-only view, so a reader in one repository reads another's memory without copying it.
7. **The order.** The rows, smallest first, each with its proof, and what waits for an installed measurement.

## The form

At most 1,800 words. Answer each question with a choice, its reason, and what it rejects. Name the code and records you read, with paths. End with the rows in order. Write without reading the other maker's proposal.
