# Daoris (道衍) — Roadmap

The forward sequence. `TASKS.md` is the open backlog; `docs/task-archive.md` is what has closed;
`CHANGELOG.md` is the release-facing log.

The ordering principle throughout: **ship what a repository is ready to adopt.** A pack nobody installs
is unvalidated doctrine, and a capability nobody has asked for is a guess. Every phase below is gated on
a real consumer, not on a calendar.

---

## Versions

Nothing has been published, so there is no released version to preserve compatibility with. **Development
is `0.0.x`; the first release is `0.1.0`.** Work that was once scoped behind a "v0.2" boundary simply
lands in the first release instead — a version boundary between two unreleased states is bookkeeping
nobody consumes.

## 0.0.x — doctrine that installs, is checked, and flows back — **built**

Seven commands, a canon of 6 core rules and 3 packs, 72 tests. Core installs everywhere; packs are named
in the manifest; the repository's own files are invisible to the tool. Drift and adoption collisions are
distinguished by provenance and both refuse. Retirement removes a rule from every repository at once.
`check` is offline by construction and gates on the always-loaded byte budget.

Proven by adoption into Lyntai rather than by assertion: four collisions and a renamed twin surfaced, its
1337 tests stayed green, and the budget gate caught a real 45% overage on first contact. The version bump
to `0.0.1` then surfaced D13 — drift was measured against the wrong side, so an improved canonical rule
could not propagate at all.

## 0.1.0 — the first release: skills, and the workflow rule that references them

Skills were held out of the initial build for a reason: they carry frontmatter the harness interprets,
and they often need per-repository parameterization (a build command, a package layout). That is a design
problem, not a copy — and it was the last one standing between here and a release.

- ~~**The parameterization question.**~~ **Settled (D14).** Answered from a survey of twelve
  repositories rather than from taste: canonical skills are parameter-free and delegate to the generated
  index. The substitution map was rejected because the measured spread between copies of the same skill
  is the adopter's own routing content, which no placeholder supplies. `skill-loader` turned out to be
  generated content rather than doctrine, which removed the hardest case entirely.
- ~~**Then `skills-workflow`**~~ — **shipped** as a seventh core rule, at 6 of 11 repositories.
- **The rest of the frequent skills** (`TASKS.md` CANON4) — `fix-log` first, whose three copies sit
  within 100 bytes of each other, so the invariant is nearly the whole file.
- **The two release blockers** — the GitHub owner and the LICENSE holder (`TASKS.md` Part 1). Both need
  an owner decision, and neither is engineering work.

## 0.2 — the harness layer: gates, not scripts

The same pathology one level down. Every repository carries a hand-copied devtools script, and those
copies have diverged further than the documents had. The shape follows this design: gates are
**declared, not copied**.

- The manifest grows a `verify` block. Daoris ships the gates that are genuinely universal — sensitive
  scan, doctrine drift, version authorship, documentation freshness — and each repository declares its
  own stack gates as commands.
- **The CLI stays Node.** What devtools actually do is orchestrate subprocesses; a compiled binary that
  spawns a build buys nothing while costing per-platform artifacts and a release pipeline. That is
  self-defeating for a tool whose purpose is reducing per-repository overhead.
- **.NET earns its place only where the compiler is required** — see the long-term section below.

## 0.3 — the centralized knowledge service

_Checked against the agent platform's own features before committing further (D15): its workspaces are
billing and access segmentation, its skills are a format rather than a distribution mechanism, and its
per-project memory is machine-local and untracked. Nothing here is superseded._


A service every agent session can query: cross-repository semantic recall over doctrine, decisions, and
past task outcomes. Deliberately *after* the canon exists, because indexing content that is still
divergent indexes the divergence.

This is where a dependency on **Lyntai** becomes correct rather than premature — semantic memory, the
embedder seam, the vector store and MCP hosting all already ship there, so the service is mostly
composition rather than new primitives. It remains a separate deployable; the CLI keeps its zero
dependencies.

## Long term — repository intelligence

Symbol graphs, dependency graphs, real API-surface diffing, AST-aware transforms. This is the one pillar
from the original framework note that survives contact with reality, and the one place **.NET** is the
right tool rather than a preference: Roslyn cannot be replaced from Node for a C# codebase.

When it lands it is a capability the CLI invokes — a separate tool the manifest can name — not a rewrite
of the CLI. The framework note's other thirteen packages were not wrong so much as premature and
mis-scoped; most of them are Lyntai's job, and saying so early is what kept v0.1 small enough to finish.

---

## Standing policies

- **Adoption gates growth.** A pack is written when a repository is ready to install it, and validated by
  that installation. Doctrine nobody runs is a draft.
- **The core stays small.** It is loaded into every session in every repository, so every byte is paid
  for repeatedly. `check` measures it; the budget is deliberate, and raising it is a decision, not a fix.
- **Canon files are project-agnostic.** The principle and the reason are canonical; the mechanism belongs
  to the adopting repository.
- **`check` never touches the network.** It runs inside build gates, including in repositories that have
  no Node dependencies and may be building offline. This is structural — the canon ships in the package —
  not a rule to remember.
- **Both directions, always.** Any change that makes `upstream` harder is a change in the wrong
  direction: one-way push is distribution, not 衍.
