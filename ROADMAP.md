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

## Closed arcs: workspaces, the working surface (D48–D56, 2026-09-20 → 22)

Both are built; the archive has every item's outcome and `docs/DECISIONS.md` the reasons.

| Arc | What it settled | Contract |
|---|---|---|
| **WSP1–4 · Workspaces** (D48) | The workspace is the unit of sharing, as wiring, never tracked; the registry is an explicit list (`connect`, `retire`, `import`); remotes are a map, one deployment per workspace; a feed names the commit it speaks for | `docs/2026-09-20-workspace-design.md` |
| **SES1–3 · The interactive surface** (D49) | The console streams, desktop-only; a conversation is a session; the accounts are Daoris's, the binary the machine's | `docs/2026-09-20-interactive-design.md` |
| **CANON6 · Coexistence** (D48 §2a) | Doctrine never hard-requires Daoris: the one service-bound instruction names its alternative | `.claude/knowledge/canon-authoring.md` |
| **Management parity** (D50) | Whatever a screen can set, a terminal can | workspace design §2b |
| **SURF1–10 · The working surface** (D51–D52, D55–D56) | The tree is the unit of exclusion; the session, not a file, organises the desktop; review is a diff; attention, a monitor window, a command palette, a frameless window with one type scale — then **D66** made Sessions one view on the activity bar | `docs/2026-09-21-working-surface-design.md` |
| **ACP1–4 · The protocol door** (D53) | dsh adopted as a protocol, not a product: an ACP door on the adapter seam, a permission request refused by construction; Claude Code, Codex and dsh ride it | `docs/2026-09-21-dsh-evaluation.md` |

REV2 (2026-09-20) reviewed the first of these after it closed: the release workflow ran one of four
declared gates, and the shell had no tests. Both were fixed and gated.

---

## The arcs since: the toolchain, the instruction file, the deployment, the home, the plugins (2026-09-22 → 23)

Each was set by the owner and measured before it was designed; the archive carries the outcomes and
`docs/DECISIONS.md` the reasons.

| Arc | What | Where it stands |
|---|---|---|
| **TOOL1–3 · The toolchain** (D57, `docs/2026-09-22-toolchain-design.md`) | Daoris owns which binary runs (`agent pin\|unpin`, a pin nobody installed refuses); usage is measured before it is managed (ACP's `usage_update` per session at its high-water mark, totalled per account, machine-local); breadth is more native adapters plus the ACP door, never a registry | **Built** (2026-09-22) — TOOL4 (rotation) is held until TOOL3 has data; TOOL5 is a trigger |
| **CANON8 · The instruction file** (D59, `docs/2026-09-22-instruction-file-design.md`) | The always-loaded tier lives in a region of `AGENTS.md`, because `.claude/rules/` is read by one harness of three; `CLAUDE.md` carries `@AGENTS.md` | **Built and migrated** (2026-09-22) — this repository and both examples |
| **DEPLOY1–4 · Deployed** (D60, D62, D63) | `publish:desktop -- --to <dir> --service` installs one exe, binaries under `app/`, **the Daoris home in `data/`**; a gate drives the published artefact; development happens against the install; **nothing of Daoris's lives under the user profile** — `DAORIS_HOME` is the one seam and a writer with none refuses | **Built** (2026-09-22/23) — two deployments found seventeen defects invisible from the workspace, every one in `docs/FIX-LOG.md`; trust is asked, then written (D73) |
| **PLUG1–6 · Plugins** (D64, `docs/2026-09-23-plugin-design.md`) | A plugin is a folder under the home's `plugins/` that **declares** (harnesses on the ACP door) and may **speak** (a process of its own answering the driver at named points, fail-closed); two doors; no code loads into any host | **Built** (2026-09-23) — the family rehearsal drives one; PLUG7 (service-side points) is held until a plugin asks; PLUG2 built (D71) |

## The current direction: the regular task (D65, set 2026-09-23)

A sentence with a ticket, a file and a link enters at the workspace; an **intake session** — the
harness carries the model — reads the ticket, decides the owning repository from the declarations,
and publishes quests; the driver develops; a plugin-declared **browser server** lets the session
test in a browser; **`then`** chains develop → verify → report, and the driver is the engine.
`docs/2026-09-23-intake-design.md` is the contract. **Built**, INT1–INT5 (INT3 decided as D70:
registered is addressable, adopted is disciplined); INT6, onboarding a real workspace, is the
owner's to run.

## The newest direction: the remote as a git remote (D68, set 2026-09-23)

**Every machine commits locally; sync is fetch, rebase, push** (`docs/2026-09-23-sync-design.md`).
**Built**, SYNC0d through SYNC6c; the archive has each landing.

## The next direction: agents, and the map (D67, set 2026-09-23)

**Daoris's loop is the higher one**; an agent keeps its own and reaches Daoris over MCP; Daoris may
keep an API key; the wiring is a **map**. The asks are in `docs/archive/2026-09-23-agents-direction.md`.

| Step | What | State |
|---|---|---|
| **AGT · Agents and accounts** | *agent* as the word, with each tool's maker; pins that stay pinned; vendor-channel installs; API-key accounts | AGT1, AGT2a, AGT2b, AGT3, AGT3b, AGT6, AGT7 **built**; AGT2c the owner's |
| **MAP2 · The workspace topology** | A circle's repositories, the quests and the shared knowledge between them; no model | **Built** — the Map view (`docs/2026-09-23-map-design.md` §1) |
| **MAP1 · The workflow** | ask → intake → quests → sessions → `then`, each step's agent, account and version | **Built**: the chain strip (MAP1a) and the wiring panel (MAP1b); the intake joins with INT4b |
| **MAP3 · A repository's code** | The same map inside one repository, fed by it (D32): **repository intelligence**'s first consumer | **Built**: MAP3a drawn, MAP3c a tool producer, MAP3d the session prompt, MAP3e a teammate's |

## Since: what an agent may do, and the conversation (D72–D76, set 2026-09-24 → 25)

| Arc | What | State |
|---|---|---|
| **PERM · Permission scopes** (D72, D74, `docs/2026-09-24-permission-scopes-design.md`) | Daoris's rules in scopes, handed to the harness at spawn; the tree guard as a hook; an agent proposes a rule change, a narrowing applies and a widening waits for the person | **Built**, PERM1–PERM4 and PERM2b |
| **TRUST · A folder's trust** (D73) | Asked per folder, then written, never silently; two doors | **Built**; whether the harness honours a key Daoris wrote is TRUST2, the owner's to run |
| **MENU · The menus** (D75, `docs/2026-09-24-menus-design.md`) | The menus are the setup domains, Settings one page of them, the workspace always named | **Built** |
| **CONV · The conversation** (D76) | A session's structured updates kept as typed events on the machine; the page renders a conversation from them, the console its raw view | **Built**, CONV1–CONV4c; CONV4d and CONV5 are open in `TASKS.md` |

## Now: the first goal, run for real (D77, set 2026-09-27)

The owner's words: a real workspace set up, a ticket read through a browser, and a task started
with no repository named. D65 built every stage of it, and INT6, running it on the named workspace,
had never run. Reading what that run would meet (`docs/2026-09-27-first-goal-study.md`) found three
walls in the code: nothing unadopted could be chosen, an import stated no workspace, and a ticket
sits behind a sign-in. **FG1–FG3 removed them**: a repository that declared nothing is weighed by
what its own files say, an import names its workspace, and a plugin keeps a signed-in profile in its
own data folder. **FG5 is the run itself**, and FG4 is its screen door. The references were read
for it. Neither routes a ticket to a repository, so locating stays Daoris's own. Their plugins map
onto D64 with nothing new. Lyntai's document storage fits no store here, so SQLite stays, and a
persistent vector store is SEM1's question.

**What else is open is decisions and triggers**, not work: the owner's calls and the held rows are in
`TASKS.md`, each saying what it waits on. Surface work comes from looking at the deployed application
after every change.

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

Twelve commands, a canon of 8 core rules, 5 core knowledge documents, 5 core skills and 6 packs,
164 CLI tests. Core installs everywhere;
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

Eleven repositories carried a hand-copied `devtools/dev.mjs` at a 20× spread in size; the artefact
answers that the way the CLI answers the document version: gates get **declared, not copied**.
**Built 2026-08-05** as one self-contained .NET AOT binary (D20 — distribution is the cost this
project exists to address; the CLI stays Node and zero-dependency for the opposite reason), five
universal gates, and each repository declaring its own stack gates in `daoris.gates.json` — a file
the CLI never reads (D26), acquired explicitly and hash-pinned rather than downloaded (D27).
`doctrine` delegates to `daoris check` rather than reimplementing drift.

## Built — the knowledge layer: `Daoris.Service` and `Daoris.Web`

What each repository *learned* — its decisions, its fix log, its task outcomes — was visible only from
inside it, which is how the same problem gets solved twice by one person in two directories.

**One UI, two shells** over one service, **local-first with sharing as configuration** (D21,
`docs/2026-08-05-knowledge-service-design.md`): local needs no server, account or network; shared
mode and per-repository indexing are opt-in, and the untracked local directory is a hard exclusion.
**Built by composition** (D22) from the cognition and desktop siblings at released versions, which
made Daoris the first external consumer either had. **Convergence detection is the one capability
no existing tool supplies** — the same principle in different words scores like an unrelated document
(D17) — and it proposes while a person disposes through `upstream`. It came after the canon
deliberately: indexing divergent content indexes the divergence. Checked against the agent
platform's own features (D15) and generated-wiki tools (D16) before committing; neither supersedes it.

**Deployable since 2026-09-18 (D36)** — the MCP host spawned per session over one persistent store,
the HTTP host as the remote half, no model required — and **the platform since 2026-09-19 (D38)**,
proven by the family rehearsal (D39): two tracked example projects driven through adoption,
registration, a quest's whole life and a restart, through the real artefacts.

## Long term — repository intelligence

Symbol graphs, dependency graphs, real API-surface diffing, AST-aware transforms. This is the one pillar
from the original framework note that survives contact with reality, and the one place **.NET** is the
right tool rather than a preference: Roslyn cannot be replaced from Node for a C# codebase.

When it lands it is a capability the CLI invokes — a separate tool the manifest can name — not a rewrite
of the CLI. Its first consumer is named now: MAP3, a repository's code on the map (D67). The framework note's other thirteen packages were not wrong so much as premature and
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
