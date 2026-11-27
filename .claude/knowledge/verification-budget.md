---
name: verification-budget
applies_when: verifying a task in this repository (a builder's checks before hand-back, a run's check, a merge's gates for one branch), reviewing a change (a second opinion, a wide or close review), reaching for `--full`, or changing a gate, the lane table or what a merge runs
enforces: one task's verification takes under ten minutes of wall time, scoped to what its paths reach, the heavy tier after a landing that bears a reason; a full run needs a reason the tools check against the change, a reviewer asks for none the rule gives no reason for; an overrun is said with where its time went and filed
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

- **The rule is data.** `daoris.gates.json`'s `verifyBudget` holds the budget (`budgetMinutes`), the findings file
  (`local/verify-overruns.jsonl`, a fixed file in the checkout, no setting) and the full run's reasons (`fullRunReasons`): the
  declared wide paths (the gates and the test tools, dependency files and lockfiles, build files, the shared contracts, what is
  tracked and the helpers every rehearsal imports), a reach of the declared share or more, and the runs that are a reason by
  their kind (the CI beside after a landing, a release; the heavy tier is judged by the landing's change, below). The lane table's every-gate rule reads the same wide paths, its one source,
  apart from the budget: a budget missing or mistyped never drops them, and a `verifyBudget` declared without them fails at
  load. So a change to a wide path reaches every gate by intent: the shared contracts (`src/Daoris.Contracts/**`) run all of
  them, as the gates file and the lockfiles do.
- **A reach is measured on what could be left out.** At a merge the share counts only the gates the lane table can skip: the
  gates every merge runs (the blocking checks, the baseline, the merge's own suites) and those no rule names reach every
  change, so they are left out of both sides. A service or desktop source reads as scoped; the ports, adapters and fakes,
  which most of the desktop's and the service's suites build on, reach over half and read as full. In the reached run the
  share is of the census's classes.
- **Scoped by default.** A change touching no wide path runs the light gates its paths reach and the reached classes before the
  landing; the Process halves, rehearsals and the rest of the heavy tier run after it, in the background. The merge, `--plan`,
  `--preview` and `reached-checks.mjs` each say the scope first, by what runs: `verification: scoped to its reach: …` (with
  `it bears a full run's reason (…)` where the change bears one and runs its reach), `verification: full: daoris.gates.json is
  a declared wide path`, or, for a branch of a `--full` batch that bears no reason of its own, `verification: full: its
  batch's --full, …`.
- **`--full` needs a reason.** `merge-branch.mjs <branch> --full` is refused where the batch's changes bear none, judged as one
  batch by the merge and by `--plan` alike; `--continue --full` during an open merge is judged on what that merge changes, a
  fix made in it included. The refusal names the reasons it accepts. `--full` on the checkout is a release's run by its kind.
  Asking for the full set is never a reason by itself.
- **An overrun is a finding.** A merge past the budget says it in its summary with each gate's time and the reached run's
  steps, files a line in `local/verify-overruns.jsonl` for the steward (or says it could not, the merge going on), and keeps
  `verification` in its merge record: budget, wall time, the scope the change calls for and a full run's reason, and what
  ran (`ran: full | reach`). The finding's cause is read from what ran: a full run says its reason; a scoped run means the
  scope was too big for the change, or the change was code-wide and should have said so.

- **The heavy tier follows a landing that bears a reason, never every landing** (HEAVYBATCH1). "The run after a landing" is a reason
  for the CI beside and a release by their kind, not for the heavy tier: its runner (`tools/heavy-run.mjs`) judges each landing by the
  paths it changed against the same `fullRunReasons` (a declared wide path, the declared reach share), and a landing with neither
  settles `skipped` and queues nothing, no admission place and no tree. A reasoned landing's tier waits until no landing has come for
  20 minutes, a newer reasoned landing carrying the waiting one's range, then runs only the classes the reasoned landings' own
  changes reach, as a background run: the low class of D365, taken only while no task's check waits, no group started while one
  does. A release, a reason by its kind, asks the tier by hand (`tools/heavy-run.mjs … --now`).
- **Every reviewer reads this rule, and none asks past it.** A second opinion, a wide or close review and a session's own review
  of a change judge its verification by this rule: the opinion's instruction carries the repository's declared rule and a red
  check's words repeat it. A full or end-to-end run is asked for only where the change bears one of the reasons above; for a
  change that bears none, a reviewer does not ask for one and does not count its absence as a finding (the heavy tier runs after
  the landing, as declared). A gap in what the change's own tests cover is still a finding.

Until a supervisor actor holds this rule (WORKSPACERULES1), the merge tool (`tools/merge/verification.ts`) and the reached
checks keep it. Changing the rule is a change to its declaration, never to a call site.
