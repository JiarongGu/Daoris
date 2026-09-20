# Daoris (道衍) — Roadmap

The forward sequence. `TASKS.md` is the open backlog; `docs/task-archive.md` is what has closed;
`CHANGELOG.md` is the release-facing log.

The ordering principle throughout: **ship what a repository is ready to adopt.** A pack nobody installs
is unvalidated doctrine, and a capability nobody has asked for is a guess. Every phase below is gated on
a real consumer, not on a calendar.

---

## The direction: Daoris drives (D45, set 2026-09-19)

Daoris becomes **the main driver for all projects** — centralized workflow management. Work already
routes through it as quests; the missing half is **triggering**: Daoris starts and manages the agent
sessions (claude/codex, adapter-based) that take the work — one session per domain-owning repository,
which is what keeps the domain separation clean. The core loop: a target becomes a quest → Daoris
spawns the owning repository's session with it → the session works under its own gates (D37) → done or
declined flows back → the person verifies outcomes in the platform.

Three parts, in build order:

| Part | What | Where it stands |
|---|---|---|
| **1 · The connector** | Per-repo setup: rules, skills, MCP, the join lifecycle — the CLI + canon + `.mcp.json` | **Exists**, proven by the loops (D39, D44) and shipped as executables (D43) |
| **2 · The local driver** | `Daoris.Desktop` re-scoped: hosts the local server, carries the platform UI, controls repositories and agent sessions | **Built and proven** (D46, 2026-09-19) — the driver, both adapters, the shell, the controls; a real `claude-code` session drove a real quest to done (DRV4) |
| **3 · The remote server** | Team mode: shared knowledge and quests across machines, fed via the local desktop app | **Built and proven** (D47/DRV5, 2026-09-20) — shared mode with minted keys, the desktop sync loop, the quest lock hardened into code; a quest crossed two machines and drove to done in the family rehearsal's remote phase |

**All three parts of D45 are built.** Everything below this line is the foundation they stand on.

---

## The current arc: workspaces and the working surface (D48–D50, set 2026-09-20)

The sharing boundary was an accident of folder layout — right for one person with one folder of
checkouts, wrong the first time one machine holds two circles' repositories. So: **the workspace is the
unit of sharing**, a server serves one, the registry becomes managed, a person works *inside* Daoris
(live console, chat sessions, managed harnesses), and everything a person manages has a real surface in
the desktop **or** the CLI. The contracts are `docs/2026-09-20-workspace-design.md` and
`docs/2026-09-20-interactive-design.md`; the build items are in `TASKS.md`.

| Item | What | Where it stands |
|---|---|---|
| **WSP1 · The workspace exists** | Membership as wiring (`connect --workspace`, a registry row, nothing tracked); every cross-repo entity carries it; search, convergence, registry and quests scoped by it | **Built** (2026-09-20) — the family rehearsal's two-workspace phase proves the boundary, and the refusal names both sides |
| **WSP2 · The managed registry** | The registry becomes the authority and the folder scan becomes `import`; `retire` beside `connect`; absences named; the desktop manages add/update/remove | **Built** (2026-09-20) — the rehearsal proves a folder nobody registered stays invisible, and that retiring touches no file |
| **WSP3–WSP4 · Remotes as a map, knowledge sync semantics** | One remote per workspace and a per-workspace sync loop; git provenance with monotonic, default-branch-only replacement | Designed; next in order |
| **SES1–SES3 · The working surface** | The live console, chat sessions, the managed harness toolchain with credential profiles | Designed (D49) |

---

## Five artefacts

Daoris is a workspace, not a single tool (`docs/DECISIONS.md` D20). All five exist:

| | What | State |
|---|---|---|
| `Daoris.Cli` | The doctrine tool — npm, TypeScript, zero runtime deps | **built and proven** |
| `Daoris.Devkit` | The shared dev toolkit, as a .NET AOT binary | **built** |
| `Daoris.Service` | Knowledge index, convergence, quests, sessions and the registry | **built and deployable** |
| `Daoris.Web` | The platform — knowledge, quests, projects; doctrine read-only (D38) | **built** |
| `Daoris.Desktop` | **The local driver** (D45/D46): the driver library, `daoris-driver`, and the `daoris-desktop` shell | **built** |

## Versions

Nothing has been published, and **the CLI alone is not the product** — releasing it now would invite
adoption of a quarter of the thing. Development stays at `0.0.x` until there is something whole to adopt.
The release workflow exists and is manual-only, with `dry_run` defaulting to true; the version belongs to
that workflow and is never edited by hand.

## 0.0.x — doctrine that installs, is checked, and flows back — **built**

Eleven commands, a canon of 8 core rules, 5 core knowledge documents, 5 core skills and 6 packs,
147 CLI tests. Core installs everywhere;
packs are named in the manifest; the repository's own files are invisible to the tool. Drift and adoption
collisions are distinguished by provenance and both refuse. Retirement removes a rule from every
repository at once, and a rename is reported as one. `check` is offline by construction and gates on the
always-loaded byte budget.

Proven by adoption into Lyntai rather than by assertion: four collisions and a renamed twin surfaced, its
1337 tests stayed green, and the budget gate caught a real 45% overage on first contact. The version bump
to `0.0.1` then surfaced D13 — drift was measured against the wrong side, so an improved canonical rule
could not propagate at all.

The skills layer that once stood between here and a release is done: `doc-loader` and `pattern-finder`
start a task, `post-feature` and `fix-log` close one, `caveman` governs output, and `skills-workflow`
joined the core rules. Each was canonized from the copies found across twelve repositories and reduced
to what they share (D14). The `doc-*` maintenance family is deliberately not canonized (D29;
`docs/task-archive.md` CANON4).

## Built — `Daoris.Devkit`: the same pathology, one layer down

Eleven repositories carry a hand-copied `devtools/dev.mjs`, measured at **2.6 KB to 52.6 KB — a 20×
spread**. Nine also carry a config file, which is the part that was *meant* to differ. The rest is one
tool, re-derived and diverged. This was the strongest evidence in the family, and the artefact answers
it the way the CLI answers the document version: gates get **declared, not copied**.

**Built 2026-08-05.** One 2.7 MB self-contained binary, 57 tests, five universal gates, and it runs this
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

## Built — the knowledge layer: `Daoris.Service` and `Daoris.Web`

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
exactly what `sensitive-info` keeps out of tracked files. The shared store is one SQLite file behind
the shared-mode host: git-as-store was priced and declined when the driver turned quests into an
execution queue (D47 §3) — a queue two machines race needs an arbiter that refuses the second take
before work starts, and the serialization point a lock needs is a host.

**Built by composition** (D22), which is what made the scope plausible. Embeddings, the vector store,
semantic recall, provider routing and MCP hosting already shipped in the cognition sibling; the shell,
WebView2 surface and IPC bridge in the desktop one. The wiring is done, and Daoris became the **first
external consumer either had**, validating both. Released versions only: three repositories coupled at
HEAD are one repository with extra steps.

**LLM-assisted merge is the capability none of the existing tooling can supply.** `doctor` provably
cannot see convergence — the same principle in different words scores like an unrelated document (D17) —
and that is precisely the gap. It proposes; a person disposes, through `upstream`, under review.

_Checked against the agent platform's own features before committing further (D15): its workspaces are
billing and access segmentation, its skills are a format rather than a distribution mechanism, and its
per-project memory is machine-local and untracked. Nothing here is superseded._

**It comes after the canon deliberately**, because indexing content that is still divergent indexes the
divergence.

**Deployable since 2026-09-18 (D36).** Local mode needs no daemon: the MCP host is spawned per session
and the persistent store is what survives, shared by every repository's sessions on the machine — a
quest published from one session is waiting when another starts. The HTTP host is the remote half:
registrations persist, quests publish and answer over the same `QuestExchange` the MCP host uses, and
shared mode (D47) gates every route with minted per-person keys — local mode trusts the loopback and
may bind nothing else. It needs **no model**: a remote deployment is purely a transfer
of request and task until a repository opts its knowledge in (D21, D24).

**The platform since 2026-09-19 (D38), proven by the family rehearsal (D39).** `Daoris.Web` grew into
the person's window — Quests and Projects beside Convergence and Search; doctrine stays unwritable from
every view. And the router is a gate rather than a belief: `npm run rehearse:family` drives two tracked
example projects through adoption, registration, a quest's whole life and a restart, through the real
artefacts. This is the readiness story for the next real family — a game and its subsystems, with
Daoris as their centralized router.

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

- **Automation-first (D37, as amended).** A person sets the target and verifies the outcome; agents
  execute the steps between, and gates verify them. Commits land automatically per task once gates
  are green — the landed history is the reviewable record. Destructive, irreversible,
  cross-repository and publishing actions — push, publish, release, history rewrites — stay
  explicitly human.
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
