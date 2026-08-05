# Canon changelog

**Why each version of the doctrine changed.** `daoris status` prints the entries between a repository's
locked version and the version shipping in the package, so a consumer sees not only *which* documents
moved but whether the change matters to them.

Write one entry per version, newest first, under a `## <version>` heading. Say what changed and what an
adopting repository should do about it — a line that only repeats the filename adds nothing that the
`changed` list did not already say. This file ships inside the package, so nothing here touches the
network.

## Unreleased

- **`claims-need-checks`** (new core knowledge) — a documented guarantee ships with the check that
  enforces it, in the same change; an unenforceable claim is written as intent, not as fact. Written
  after the same defect turned up three times in one day in this repository: a readme describing a
  property no test asserted, a comment deferring to a component that did not exist, and a config field
  parsed and never read. Each read as verified and none was, which is why an unenforced guarantee is
  worse than none — silence invites a reader to check, a confident sentence tells them not to bother.

  Knowledge rather than a rule, and **the budget gate made that call**: as core doctrine it put a
  realistic adopter — core plus one pack — 61 bytes over the default budget. Three instances in one
  repository is also short of the bar core is held to, which is evidence several repositories reached
  the same conclusion independently. It applies when writing a claim about behaviour, not on every task.
  **Adopting repositories:** worth reading once, then grep your entry document and readme for *always,
  never, guaranteed, enforced, verified, cannot, ensures* and check each against reality.
- **`model-decoupling`** (new core knowledge) — specify a feature without naming a model; the deployment
  chooses one, every model-backed feature still does its useful part with no model at all, and the
  output says which tier answered. Knowledge rather than a rule because it applies when building an
  AI-backed feature, not on every task.
- The first canon: seven workflow rules, and five skills — `doc-loader` and `pattern-finder` to start a
  task, `post-feature` and `fix-log` to close one, `caveman` for terse output. Nothing to upgrade from
  yet.
