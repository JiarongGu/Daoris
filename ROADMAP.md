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
| **WSP3 · Remotes become a map** | One deployment per workspace (`~/.daoris/remotes.json`), a per-workspace sync loop, the shared host's own `DAORIS_WORKSPACE` identity, and both editors over the one file — `daoris remote`, `status --machine`, the desktop's Machine view | **Built** (2026-09-20) — the rehearsal proves only the wired circle feeds, and a registration declaring another workspace is refused naming both |
| **WSP4 · Knowledge sync semantics** | Git provenance stamped by the driver, with monotonic, default-branch-only replacement, served and shown | **Built** (2026-09-20) — the rehearsal proves a newer feed replacing (a deletion travelling with it), a stale one refused as *information*, and an unmerged branch refused though newer |
| **SES1 · The live console** | The capture pump tees into a bounded per-session buffer; the shell streams it to the session drawer, verbatim and desktop-only | **Built** (2026-09-20) — the transcript stays the durable record; the window states what it dropped |
| **SES2 · Chat sessions** | A conversation is a session: `driven \| chat`, quest optional, the same one-session-per-repository lock; the seam grows `interactive`; two doors (desktop, and a terminal) | **Built** (2026-09-20) — the rehearsal holds one with no model in it, publishing the work that came up rather than editing across |
| **SES3 · The toolchain** | Harnesses Daoris installs and updates, with named credential profiles | **Built** (2026-09-20) — each harness's own mechanism, only on the person's action; a profile is a directory Daoris owns the location of, and never its contents |
| **CANON6 · Coexistence** | Doctrine must not hard-require Daoris: a named mechanism carries its tool-absent path in the same breath | **Built** (2026-09-20) — one case in the whole canon, because what `sync` writes is committed; only a *service* is a dead end without the tool |

**Reviewed after it closed (REV2, 2026-09-20).** Two things no gate could see: the release workflow
ran one of four declared gates, and the shell's 1,128 lines had no tests — so every refusal the
desktop made reached people as a blank failure. Both fixed and gated.

---

## The next arc: the desktop as a working surface (D51/D52, set 2026-09-20, designed 2026-09-21)

**The desktop half is becoming a user-driven application, and it should hold code sessions the way a
terminal agent CLI does — across different agents, repositories and concurrent sessions, designed with
real UI/UX.** That is a different product from what exists: the platform today is an *operations
console*, which optimises for scanning state, and a working surface optimises for holding attention on
one thing while several others run.

**Interposed 2026-09-21 (owner): the dsh evaluation came first, and has run.** **DSH1** probed an
installed [deepseek-harness](https://github.com/deepseek-ai/deepseek-harness) the same day
(`docs/2026-09-21-dsh-evaluation.md`), and **D53 is the answer, accepted 2026-09-21**: *dsh is
adopted as a protocol, not a product* — the adapter seam grows an **ACP door**, dsh and codex arrive
as configurations of it, and the working surface stays `Daoris.Web`. **ACP1 → ACP2 → ACP3** come
first; SURF4a–d and the UI halves of SURF5/SURF6 follow.

The research is `docs/2026-09-20-working-surface-research.md`; the contract is
`docs/2026-09-21-working-surface-design.md` (SURF1, done 2026-09-21). The question that came before
any screen is settled as **D51**: **the tree is the unit of exclusion, and a repository may have more
than one** — "one session per repository" was two claims welded together, and unwelding them lets the
person and the driver work in one repository at once without weakening D46 §9. **D52** settles the
surface: the stream promoted with an observed timeline beside it (and no screen-scraping of another
program's stdout), `AwaitingPerson` given a surface at last, review as a real diff, and no PTY.

**D55 then re-positions it** (2026-09-21, from the owner: *the desktop is becoming more a dev IDE,
but code-gen driven* — and *reference more existing applications*). The second study answered the
method note (`docs/2026-09-21-ide-reference-study.md`: IDEs, not session managers) and found five
patterns recurring in nearly every reference that the platform has **none** of. So **Work is a second
frame, not a sixth view**: Manage ⇄ Work as peers, a status bar, a growable output panel, a frameless
window whose top strip is application chrome (SURF7), and a monitor window (SURF8). The organising
object is a **session, not a file**, and there is no editor in the plan at any point.

| Item | What | Where it stands |
|---|---|---|
| **SURF2 · The lock keys on the tree** | The record names its tree; the ledger's refusals move with it; the planner's rule does not. Nothing creates a tree yet, so behaviour is identical | **Built** (2026-09-21) — two trees in one repository run at once over the real host, and an unknown tree holds every tree, because the lock errs toward refusing |
| **SURF3 · Session trees** | `git worktree` under `~/.daoris/trees/`, on request and opt-in; `connect` refused from a linked worktree; removal that refuses to destroy work; only the registered root ever feeds | **Built** (2026-09-21) — a deliberately dirty root no longer holds the driver: the session ran in its own tree, the person's file untouched, and removal refused while the work sat unmerged |
| **ACP1 · The protocol door** (D53) | An ACP door on the adapter seam: `AcpSession` over the spawned process's stdio, a permission request refused by construction, the record still moved by the exit code and the quest | **Built** (2026-09-21) — the rehearsal drives a quest to done over it with no model, and the refusal reaches both sides |
| **ACP2–3 · The harnesses on it** | `claude-code` through the ACP project's adapter, closing with a real driven run; then dsh and codex as configurations of the same door (HARNESS2 closes into ACP3) | Next — ACP2's closing step spends a real login |
| **SURF4a–d · The Work frame** | Built component by component (D52 as amended, `docs/2026-09-21-working-surface-components.md`): the atoms and the two helpers, then the rail, then the attended session, then — **D55** — the *frame* rather than a sixth nav item. A story before each component, its own test, and no hook inside a molecule. SURF4c's timeline reads the protocol | **SURF4a built** (2026-09-21): three atoms, `SESSION_TONE` moved, `sessionTitle`, a sabotage-tested presentational boundary, every story a smoke test. 4b next |
| **SURF5 · Attention** | Overview's *what needs you*, `AwaitingPerson`'s surface, the sidebar counts, OS notification on park and end | **Built** (5a 2026-09-21, 5b 2026-09-22): the attention band and a parked session's three moves, then the notification — a park is seen by diffing the tick's sessions, an end is known from the driver's own conclusion, so "never for an ending the person caused" needs no suppression list. Closes driver open question 5 |
| **SURF6 · Review** | The diff over the bridge, bounded; **a multibuffer with a per-file *viewed* mark** (D55); accept, or send it back as a quest | **Built** (6a and 6b, 2026-09-22) — what a session landed, read off the checkout, and the two acts over it |
| **SURF7 · The window is the frame** (D55) | `OptimizedForm` with frameless chrome; the top strip becomes app menu + mode switch + scope + caption buttons; `WindowCommandModule` mapped late, Snap Layouts bought by reporting the button rectangles | **Built** (2026-09-22, with SURF10 → **D56**) — the app strip is the title bar, one denser type scale held by a test |
| **SURF8 · The monitor window** (D55) | `SecondaryWindows`: a read-only rail-and-streams window for a second screen, and a detached session — named windows on their own STA threads, geometry per name | **Built** (2026-09-22): both are routes into the same bundle; the second reader was already true (`Tail` keeps no cursor) and is now asserted; a secondary window needs its own thread-affine WebView2 environment and follows the OS theme itself |
| **SURF9 · Command palette** (D55) | The only affordance that scales past ~7 domains, and cheaper before the count grows | **Built** (2026-09-22) — centred in the app strip, printing its own shortcut |

**Every SURF item is built**; the Work frame is usable, and `docs/2026-09-19-platform-ux.md` §4
carries what each looking pass settled.

---

## The arcs since: the toolchain, the instruction file, the deployment, the home, the plugins (2026-09-22 → 23)

Each was set by the owner and measured before it was designed; the archive carries the outcomes and
`docs/DECISIONS.md` the reasons.

| Arc | What | Where it stands |
|---|---|---|
| **TOOL1–3 · The toolchain** (D57, `docs/2026-09-22-toolchain-design.md`) | Daoris owns which binary runs (`harness pin\|unpin`, a pin nobody installed refuses); usage is measured before it is managed (ACP's `usage_update` per session at its high-water mark, totalled per account, machine-local); breadth is more native adapters plus the ACP door, never a registry | **Built** (2026-09-22) — TOOL4 (rotation) is held until TOOL3 has data; TOOL5 is a trigger |
| **CANON8 · The instruction file** (D59, `docs/2026-09-22-instruction-file-design.md`) | The always-loaded tier lives in a region of `AGENTS.md`, because `.claude/rules/` is read by one harness of three; `CLAUDE.md` carries `@AGENTS.md` | **Built and migrated** (2026-09-22) — this repository and both examples |
| **DEPLOY1–4 · Deployed** (D60, D62, D63) | `publish:desktop -- --to <dir> --service` installs one exe, binaries under `app/`, **the Daoris home in `data/`**; a gate drives the published artefact; development happens against the install; **nothing of Daoris's lives under the user profile** — `DAORIS_HOME` is the one seam and a writer with none refuses | **Built** (2026-09-22/23) — two deployments found seventeen defects invisible from the workspace, every one in `docs/FIX-LOG.md`; DEPLOY1's second half is the owner's |
| **PLUG1–6 · Plugins** (D64, `docs/2026-09-23-plugin-design.md`) | A plugin is a folder under the home's `plugins/` that **declares** (harnesses on the ACP door) and may **speak** (a process of its own answering the driver at named points, fail-closed); two doors; no code loads into any host | **Built** (2026-09-23) — the family rehearsal drives one; PLUG7 (service-side points) is held until a plugin asks; PLUG2 is the owner's |

**What is open is decisions and triggers**, not work: ACP2 and DEPLOY1's second half each spend a real
login; PLUG2 reopens D4; the held rows wait on something arriving. Surface work comes from looking at
the deployed application after every change.

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
