---
name: verification-budget
applies_when: verifying a task in this repository (a builder's checks before hand-back, a run's check, a merge's gates for one branch), reaching for `--full`, or changing a gate, the lane table or what a merge runs
enforces: one task's verification takes under ten minutes of wall time, scoped to what its paths reach, the heavy tier after the landing; a full run needs a reason the tools check against the change; an overrun is said with where its time went and filed
---

# Verification budget — this workspace verifies a task in under ten minutes

**One task's verification takes under ten minutes of wall time, scoped to what the change's paths reach. A full run is taken
only for a reason the change bears, and says it. A verification past the budget is said at once, with where its time went,
and filed as a finding.** This is this workspace's own rule (D349), not the product's and not canon: Daoris supplies the means
to keep a workspace's rules, and this one is set here.

## Why

In the week of 2026-11-14 a fix of a few files took one to two hours to verify: a merge's gates ran 25 to 40 minutes for one
branch, one batch's `reached` gate 27 and then 34 minutes, about 95 hours a week of gate time someone waited on. A verification
longer than its change's reach is a scope defect, not a cost of safety; a budget said at each breach turns it into findings.

## How it is kept

- **The rule is data.** `daoris.gates.json`'s `verification` holds the budget (`budgetMinutes`), the findings file
  (`overruns`, `local/verify-overruns.jsonl`) and the full run's reasons (`fullRunReasons`): the declared wide paths (the
  gates and the test tools, dependency files and lockfiles, build files, the shared contracts, what is tracked and the helpers
  every rehearsal imports), a reach of the declared share or more, and the runs that are a reason by their kind (the run after
  a landing, a release). The lane table's every-gate rule reads the same wide paths, its one source.
- **Scoped by default.** A change touching no wide path runs the light gates its paths reach and the reached classes before the
  landing; the Process halves, rehearsals and the rest of the heavy tier run after it, in the background. The merge, `--plan`,
  `--preview` and `reached-checks.mjs` each say the scope first: `verification: scoped to its reach: …` or
  `verification: full: daoris.gates.json is a declared wide path`.
- **`--full` needs a reason.** `merge-branch.mjs <branch> --full` (and `--continue --full`) is refused where the branches'
  changes bear none, and the refusal names the reasons it accepts. `--full` on the checkout is a release's run by its kind.
  Asking for the full set is never a reason by itself.
- **An overrun is a finding.** A merge past the budget says it in its summary with each gate's time and the reached run's
  steps, files a line in `local/verify-overruns.jsonl` for the steward, and keeps `verification` (budget, wall time, scope,
  a full run's reason) in its merge record. The finding means the scope was too big for the change, or the change was
  code-wide and should have said so.

Until a supervisor actor holds this rule (WORKSPACERULES1), the merge tool (`tools/merge/verification.ts`) and the reached
checks keep it. Changing the rule is a change to its declaration, never to a call site.
