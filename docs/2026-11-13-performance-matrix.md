# Performance matrix and real-case verification (PERFMATRIX1, RCASE1) — steward's spec, 2026-11-13

Owner (2026-11-13): consider the performance matrix: the goal is a properly working Daoris; the actor / structure / workflow /
communication design is complex, so we need a lot of real-case testing and performance verification.

## 1. The matrix: what is measured, from what, against which budget

Every figure is read from the installed home and logs (never a test fixture), per day and per real case, and is either a value or
not recorded: <where it would have to be logged> (dev-metrics' rule). Each row has a budget; a figure over budget is a defect row.

| Area | Metric | Source today | Budget (first) |
|---|---|---|---|
| Look (the driver's loop) | look duration median / p90 / max; look.slow count | `watch.looked`, `look.slow` in logs | median ≤ 3 s, p90 ≤ 6 s, max ≤ 30 s |
| Quest lifecycle | per quest: published → taken → session started → first commit → done → held/Accept → landed → pushed → PR merged → released | quest records, session records, landing records, workflow events | taken ≤ 1 look; started ≤ 60 s after taken; done→landed ≤ 2 looks after Accept |
| Workflow steps | per run: each node's wait and work time; holds and who cleared them | workflow events (`workflow.*`), run records | a mechanical step ≤ 1 look; a hold names its waiter |
| Actors and roles | per role: starts, carry-ons, hand-overs, failures by kind; the model and tier that ran | session records (`asked`, `ran`, `chosen`) | 0 silent failures; every failure typed |
| Communication | channel post → read by addressee; helper ask → answer delivered; quest response → read; word said → turn started | channel log, helper records, session events | delivered within 1 look; never lost (SAYLINES1-class loss = 0) |
| Ask Daoris | words → first reply; turns per ask; acts in full access; cards asked | session events of the help conversation | first reply ≤ 15 s |
| Person's queue | items waiting on the person, each with its age; waits nobody surfaced | needs-you reads, holds | every wait visible; none older than 1 h unseen |
| Cost | tokens by actor, role, model; cache read; per quest and per ask | session `ran`, usage logs | trend, per case |
| Leftovers | session trees, branches, landed branches not gone, stale worktrees | work/trees, git | 0 after a case completes |
| Development cycle | merge wall time, first-pass rate, gates over budget, full-set wall | merge record, gate verdicts | merge ≤ 10 min, first pass ≥ 80%, full set ≤ 30 min |
| Browser | session tab open latency; sign-in shared (local storage keys seen) | plugin server log / probe | open ≤ 2 s |

## 2. Real cases (RCASE1): a catalogue run on the install, each producing a receipt

Each case is a real piece of work through the installed window and its agents, never a stand-in; its receipt records the matrix's
quest/workflow/communication rows for that case, pass/fail per expectation, and the interventions a person or the steward made
(each intervention is a defect until the case runs with none).

1. One-repository quest, through `dev-line`: ask → quest → session → done → Accept → landed on the line. (Today's install quests.)
2. A quest with a requirement (`--require`) checked against its done (SAYLINES1 fixed the route).
3. A chain across two repositories, the later one building on the first's commit.
4. Co-working: two sessions on one ask exchange a contract on the channel; a helper question answered and delivered.
5. A review with a send-back, a second revision, then Accept; the re-landing moves a pushed branch (LANDAGAIN1).
6. A workflow with push → pull request → watch → clean-up through a plugin (the work workspace's shape).
7. A release step on a declared target (RELEASETARGET1 + DELIVERY1 a), observe-only on a sandbox definition.
8. A restart in the middle (update + restart): every session carries on, nothing re-sent, nothing unwatched (DELIVERY1 h).
9. A limit hit mid-turn: the work moves or resumes by itself (LIMITRESUME1).
10. Ask Daoris drives a case end to end from the person's words, with the second-model read on.
11. A browser-using session on the person's one profile while the person browses (BROWSERSHARE1).
12. The multi-repository delivery case of 2026-11-13 replayed on fixtures repositories (DELIVERY1 j).

## 3. How it runs

- `tools/dev-metrics.mjs` grows the per-quest lifecycle, per-run step, communication and person-queue rows (it already has
  throughput, cost, look, leftovers, Ask Daoris, Lyntai), plus `--case <id>` to scope a receipt to one case's asks/quests.
- A daily snapshot (`--json`) kept under `docs/metrics/<date>.json` with the budgets in one declared file; a figure over budget
  prints the defect it is.
- Cases 1, 2, 5, 6 run every day the install is republished (dogfood rows are case 1); the rest as their rows land.
- The steward reports the matrix with each republish: what moved, what is over budget, what case failed and why.
