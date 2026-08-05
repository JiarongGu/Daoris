# Daoris (道衍) — Roadmap

The forward sequence. `TASKS.md` is the open backlog; `docs/task-archive.md` is what has closed;
`CHANGELOG.md` is the release-facing log.

The ordering principle throughout: **ship what a repository is ready to adopt.** A pack nobody installs
is unvalidated doctrine, and a capability nobody has asked for is a guess. Every phase below is gated on
a real consumer, not on a calendar.

---

## Four artefacts

Daoris is a workspace, not a single tool (`docs/DECISIONS.md` D20). Only the first exists:

| | What | State |
|---|---|---|
| `Daoris.Cli` | The doctrine tool — npm, Node, zero dependencies | **built and proven** |
| `Daoris.Devkit` | The shared dev toolkit, as a .NET AOT binary | **built** |
| `Daoris.Service` | Cross-repository knowledge service | brief written |
| `Daoris.Web` + `Daoris.Desktop` | One React UI, two shells | brief written |

## Versions

Nothing has been published, and **the CLI alone is not the product** — releasing it now would invite
adoption of a quarter of the thing. Development stays at `0.0.x` until there is something whole to adopt.
The release workflow exists and is manual-only, with `dry_run` defaulting to true; the version belongs to
that workflow and is never edited by hand.

## 0.0.x — doctrine that installs, is checked, and flows back — **built**

Eight commands, a canon of 7 core rules, 1 core knowledge document, 5 core skills and 3 packs,
118 tests. Core installs everywhere;
packs are named in the manifest; the repository's own files are invisible to the tool. Drift and adoption
collisions are distinguished by provenance and both refuse. Retirement removes a rule from every
repository at once, and a rename is reported as one. `check` is offline by construction and gates on the
always-loaded byte budget.

Proven by adoption into Lyntai rather than by assertion: four collisions and a renamed twin surfaced, its
1337 tests stayed green, and the budget gate caught a real 45% overage on first contact. The version bump
to `0.0.1` then surfaced D13 — drift was measured against the wrong side, so an improved canonical rule
could not propagate at all.

The skills layer that once stood between here and a release is done: `doc-loader` and `pattern-finder`
start a task, `post-feature` and `fix-log` close one, `caveman` governs output, and `skills-workflow` is
a seventh core rule. Each was canonized from the copies found across twelve repositories and reduced to
what they share (D14). The `doc-*` maintenance family is deliberately held (`TASKS.md` CANON4).

## Built — `Daoris.Devkit`: the same pathology, one layer down

Eleven repositories carry a hand-copied `devtools/dev.mjs`, measured at **2.6 KB to 52.6 KB — a 20×
spread**. Nine also carry a config file, which is the part that was *meant* to differ. The rest is one
tool, re-derived and diverged. This was the strongest evidence in the family, and the artefact answers
it the way the CLI answers the document version: gates get **declared, not copied**.

**Built 2026-08-05.** One 2.7 MB self-contained binary, 39 tests, four universal gates, and it runs this
repository's own gate set end to end.

- **Shipped as a .NET AOT binary**, reversing the earlier position that the tooling should stay Node
  (D20). That position weighed the execution cost and missed the distribution one — and distribution is
  the only cost this project exists to address. A .NET repository carrying a Node script has a Node
  dependency it needs for tooling alone; a binary has a version, a pasted script has whatever the paste
  contained.
- **The CLI stays Node and zero-dependency.** Different artefact, different job: it has to keep running
  in repositories that have no Node dependencies of their own.
- Daoris ships the gates that are genuinely universal — sensitive scan, doctrine drift, version
  authorship, documentation freshness — and each repository declares its own stack gates.
- Both open questions are settled. Gates are declared in `daoris.gates.json`, a file the CLI never
  reads, because the manifest is inert data and gates are commands that execute (**D26**). The binary is
  hash-pinned and explicitly acquired, never implicitly downloaded — which falls out of D8 rather than
  working around it, since nothing in the CLI may open a socket (**D27**).
- **`doctrine` delegates to `daoris check`** instead of reimplementing drift. A second answer to a
  question that already has one would be this project's own pathology, committed by the tool built to
  remove it.

## Then — the knowledge layer: `Daoris.Service`, `Daoris.Web`, `Daoris.Desktop`

Doctrine is now consistent across repositories, but what each repository *learned* — its decisions, its
fix log, its task outcomes — is still visible only from inside it. That is how the same problem gets
solved twice by the same person in two directories.

**One UI, two shells.** A React application over the service, served over HTTP and hosted unchanged
inside a desktop shell built on the family's desktop runtime. A second hand-written desktop UI would be
this project's own pathology in a new place. It also makes Daoris the first real external consumer of
that runtime, which is worth something on its own — a runtime with no consumer is unvalidated, exactly as
a pack nobody installs is.

**Local-first; sharing is configuration** (D21, and `docs/2026-08-05-knowledge-service-design.md`). One
service, two modes: local needs no server, no account and no network, and must stay fully useful alone.
Shared mode is opt-in, indexing is opt-in per repository, and the untracked local directory is a hard
exclusion rather than a permission — several siblings are private, and centralising their content is
exactly what `sensitive-info` keeps out of tracked files. The shared store should be a **git repository**
before a database.

**Built by composition** (D22), which is what makes the scope plausible. Embeddings, the vector store,
semantic recall, provider routing and MCP hosting already ship in the cognition sibling; the shell,
WebView2 surface and IPC bridge already ship in the desktop one. What remains is wiring — and Daoris
becomes the **first external consumer either has had**, so building on them validates them. A library
with no consumer is unvalidated, exactly as a pack nobody installs is. Released versions only: three
repositories coupled at HEAD are one repository with extra steps.

**LLM-assisted merge is the capability none of the existing tooling can supply.** `doctor` provably
cannot see convergence — the same principle in different words scores like an unrelated document (D17) —
and that is precisely the gap. It proposes; a person disposes, through `upstream`, under review.

_Checked against the agent platform's own features before committing further (D15): its workspaces are
billing and access segmentation, its skills are a format rather than a distribution mechanism, and its
per-project memory is machine-local and untracked. Nothing here is superseded._

**It comes after the canon deliberately**, because indexing content that is still divergent indexes the
divergence.

_Also checked against generated-wiki tools (D16). They are the complement: a wiki is **derived** from the
code and fails by going stale, doctrine is **authored** because something went wrong and fails by
diverging. They meet inside `doc-loader`, which routes first to the repository's own documentation router
— what a generator maintains — and then to the rules index, which `sync` writes. The dependency runs one
way: a wiki generated over divergent copies documents the divergence, so canonizing first is what makes
the generated layer worth having. Prefer pointing at such a tool over growing one._

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
