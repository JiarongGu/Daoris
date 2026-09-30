# CLAUDE.md — Daoris (道衍)

> Auto-loaded every session. Keep short — detail lives in `docs/` and `.claude/`.

## What this is

**Daoris** (道衍, "the unfolding of the way") is the **substrate for domain-owning agents to share
knowledge and work** across this family of projects — and, as of **D45, the driver**: the centralized
workflow manager that triggers and coordinates the agent sessions doing that work, one session per
domain-owning repository. **All three parts are built:** the per-repo **connector**, the **local
driver** desktop app (**proven by a real driven run** — D46, `docs/2026-09-19-driver-design.md`), and
the **remote server** for teams (**built and proven by a two-machine rehearsal** — D47,
`docs/2026-09-20-remote-design.md`). Each repository has its own agent, which owns that
domain; Daoris is how they hold one canon of doctrine between them, find where they have learned the
same thing twice, and **ask each other for changes instead of reaching in**.

That last part is the constraint everything else serves: **repositories are not developed across.** A
change you need elsewhere is a quest published to the service, taken by whoever knows that code —
because the *why* behind a codebase does not travel, and a request does (`quest_publish` over MCP, or
the platform's Quests view; the CLI deliberately has no quest command, D32 as amended).

The **CLI is a zero-dependency Node program plus a canon of markdown** — not a library, not a framework,
and it makes no model calls at all. The **service** is the half that may use one: it indexes the family's
knowledge and can find where two repositories reached the same conclusion in different words, which no
amount of text comparison can do.

That split is the architecture, not an accident of what got built first (D24). **A feature is specified
without naming a model; the deployment chooses one.** Running locally that may be an endpoint on the same
machine; running as a shared service it may be something else entirely — and every model-backed feature
must still do its useful part with no model at all, then say which tier answered.

道衍 is *propagation and unfolding*, and it runs in three directions. Doctrine flows **outward** into the
repositories (`sync`); refinements flow **back** and evolve the canon (`upstream`); work flows
**sideways** as quests, published to the service and taken by whoever owns that domain. All three ship: a one-way push
would be distribution rather than cultivation, and without the third an agent that noticed something
about a neighbour could only either ignore it or trespass.

## Current state

**Built and proven; nothing published.** Sixteen commands and a canon of 8 core rules, 6 core
knowledge documents, 6 core skills and 7 packs; the test counts live in `TASKS.md`'s State, their one
home. Daoris carries its own manifest and syncs core into its own `AGENTS.md` region and
`.claude/`. It was **adopted
into a sibling and proven there** — collisions, a renamed twin and a real budget overage all caught on
first contact — and that sibling **stepped off at its owner's request** (2026-08-17), so the proof
stands and the **live consumer count is zero**. Adoption has to be near-free for the family to come
back, which is what the automation-first direction is for; the archive has the account.

**All five artefacts exist and are built, and all three parts of D45 with them** — including
`Daoris.Desktop`, the local driver (D45/D46): the shell brings up the local host, carries the
platform, runs the driver loop, and lands the person's session controls. The **remote server**
(D47/DRV5) is the same HTTP host in **shared mode** — every route gated by per-person per-machine
minted keys, no page and no machine path served, refusing to bind beyond loopback in local mode, the
quest lock hardened into code, and the desktop's sync feeding up and rebasing quests (D68). **The family
rehearsal gates all of it with no model, no account and no credential** — it names its own
phases when you run it, down to a quest carried over the ACP door and a plugin's hold.

**The D48/D49/D50 arc is closed**, and `docs/DECISIONS.md` carries each piece. Two of its rules bind
every change: **the workspace is wiring and the registry is the authority** (WSP1, WSP2) — a registry
row set by `connect --workspace`, an explicit list `connect`, `retire` and `import` maintain, never a
tracked declaration nor a view over a folder; and **everything has two doors** (D50): whatever a
screen can set, a terminal can.
**Nothing is published**, and development runs at `0.0.x`. **Development is automation-first** (D37):
the person sets the target and verifies the final diff; agents execute and gates verify the middle —
see canon knowledge `autonomous-development`.

**Three things to know before changing anything.** The always-loaded core sits at **23,306 of 26,000
bytes** (CANON7, D28 as amended: this repository's number caps *the canon's core*, a different
question from the 30000 an adopter starts at). **The budget
reports and never fails** (D54): a fact gates, a judgement reports. The answer to a full budget is to
split principle from detail rather than raise the number — CANON6 did exactly that. And **never write
into another
repository**: that constraint is absolute (D32), it was broken here and cost a sibling an uncommitted
edit, and `.claude/knowledge/reaching-in.md` is the account. And **doctrine must not hard-require
Daoris** (D48 §2a): the canon instructs exactly one thing that needs a service running — publishing a
quest — and it names the alternative in the same breath, held by a canon scan. Everything else `sync`
writes is committed, so it survives the tool's absence; the test is *file or service*, never family
vocabulary (`.claude/knowledge/canon-authoring.md`).

**Never edit the version by hand, and never stamp a changelog heading.** Both belong to the release
workflow (`tools/release-prep.mjs`). A hand-bump leaves every file consistent and still wrong:
**authorship** was at risk, not consistency.

- `README.md` — the consuming story: install, the commands, the manifest, the three layers.
- `docs/2026-08-04-daoris-design.md` — the **contract**. Read it first.
- `docs/DECISIONS.md` — the numbered decision log and why each was made. **D45 is the
  direction: Daoris drives** — read it before planning anything; **D48–D50 are a closed arc**
  (workspaces; the interactive surface; management parity).
- **The desktop is a code-gen-driven IDE** (D55): the organising object is a **session, not a file**,
  and there is no editor in the plan. `docs/2026-09-21-working-surface-design.md` is the contract,
  `docs/2026-09-21-working-surface-components.md` the method — **a molecule imports no hook**, held
  by a test — and `docs/2026-09-21-ide-reference-study.md` what changed it. **Every SURF item is
  built**; Sessions is usable (`npm run desktop -- run`). Two rules that still bind: a timeline is
  **derived**, and **the stream has one home**.
- **The application has a frame of its own** (SURF10/SURF7 → **D56**,
  `docs/2026-09-21-desktop-frame-design.md`): a frameless window whose **app strip is the title
  bar**, a 48px **activity bar** as the one navigation (**D66**), one owner for the verbs, a **right dock**, and
  **D41 §3 amended to one denser scale** — seven named type tokens held by `tokens.test.ts`, after
  the literal form had drifted to fifteen values. The diagnosis was **measured**, which is how a
  design complaint became a decision. 🔴 **git walks UP** — a diff of a path that is not a
  repository answers for the one above it (FIX-LOG).
- **Two traps the desktop keeps.** `shot` needs **`--window <monitor|session:ID>`** or it
  photographs whichever window Windows calls main (`eval`/`click` default to the application's
  page). And on Chromium (D92/D93) the engine's renderer, GPU and utility processes run from the
  app's **own exe** with `--type=`, and so does the browser, with `--daoris-browser` first (D99):
  count and stop the application, never every process on the path.
- 🔴 **The desktop app is the focus, and the install is where it is judged** (owner, 2026-09-22 →
  **D62**): it carries the platform, runs the driver loop, hosts the machine's service, and is the
  only surface that reaches a machine-local fact. `npm run desktop -- run --install <dir>` starts the
  **published** application with a debug port so the instruments reach it; the scratch loop's one
  circle and one account are a fixture, not a machine.
- 🔴 **Polishing the UI/UX is standing work, and it is done by LOOKING** (owner, 2026-09-22:
  *"we also need to keep polish the ui/ux you can use screenshot tool to confirm"*). Take
  `npm run desktop -- shot [--theme dark]` to any surface you change. The defects that matter are
  invisible in the source — a 190-character measure, a field with no border in dark, an icon rail
  stacked on the page at 680px — because they are properties of the assembled window at a real
  width in a real theme. `docs/2026-09-19-platform-ux.md` §4 carries what each pass settled.
- **The protocol door is open** (D53, `docs/2026-09-21-dsh-evaluation.md`): *dsh is adopted as a
  protocol, not a product* — the adapter
  seam grows an **ACP door**, dsh and codex arrive as configurations of it, and the working surface
  stays `Daoris.Web`. **ACP1, ACP3 and ACP4 have landed** and four configurations ride the door: the
  seam carries a `Wire`, `AcpSession` speaks JSON-RPC over stdio, a permission request is **refused by
  construction** (D52), and the record still moves on the exit code and the quest — the wire flattens
  an aborted turn to `end_turn`, so it enriches and never decides. 🔴 **The posture is the ADAPTER's,
  in that harness's own words** (`docs/2026-09-22-acp3-probe-evidence.md`): null means the wire
  carries none, and is never a licence to guess a neighbouring mode. **ACP2 is proven** (17/17, a
  real login).
- `docs/2026-09-19-platform-ux.md` — the platform's design language (D41): the shell, the tokens, the
  validated status palette, the interaction rules. Read before changing anything a person looks at.
- **The toolchain is Daoris's** (**D57**, `docs/2026-09-22-toolchain-design.md`): which binary runs
  (`agent pin|unpin`; *explicit command → managed pin → `PATH`*; a pin nobody installed
  **refuses**), usage **measured before managed** (ACP's `usage_update` per session at its
  high-water mark, totalled per account, machine-local; 🔴 **absent is never zero**, no price
  claimed), and breadth as **native adapters plus the ACP door, never a registry**. TOOL4 is held.
- 🔴 **Daoris is DEPLOYED** (2026-09-22): `npm run publish:desktop -- --to <dir> --service` installs
  it — **`Daoris.exe`, a small launcher, at the root; the application (`app/Daoris.Desktop.exe`, on
  its own Chromium, and the browser too, D99) and the host under `app/`** (D93); **the Daoris home in `data/`**;
  `--beside` when
  the folder already holds the repositories it drives — and **starting it starts the driver loop**.
  🔴 **Nothing of Daoris's lives under the user profile** (**D63**): `DAORIS_HOME` is the one seam,
  set by the install for itself and once for the account; unset, the writers **refuse**.
  `docs/2026-09-22-first-deployment-case-study.md` is the first thing to read before touching the
  desktop: four defects were invisible from inside the workspace, three *because* of what the
  workspace provides. **It has a gate now** (DEPLOY2 → **D60**). 🔴 **Stop a running
  shell before building** — an orphaned host holds the build's own assemblies.
- **A plugin is a folder that declares, and may speak** (**D64**, `docs/2026-09-23-plugin-design.md`):
  a manifest under `plugins/`: harnesses on the ACP door, servers every session is handed,
  behaviour spoken over a wire — **never code loaded into a host**, no registry. **One is made with
  the driver's kit** (D101, §9): `daoris-driver plugins new|try`, a folder whose own `node --test`
  needs no Daoris. One updates from where it came from; the install offers Daoris's own (D103).
- **The conversation is built** (owner, 2026-09-25 → **D76**,
  `docs/2026-09-24-reference-gap-study.md`): a session's structured updates are kept as typed events
  on the machine, and the page renders a conversation from them; the console becomes its raw view.
  **UX5 then checked every screen** against the reference; its ledger
  (`docs/2026-09-26-ux5-screen-audit.md`) opens with what the instruments need to look at the window.
  The regular task (D65: an ask becomes quests through an **intake session**) is built.
- `ROADMAP.md` — the forward sequence. `TASKS.md` — the **active** backlog (open items only).
- `docs/task-archive.md` — completed work, with outcomes. `docs/archive/` — superseded documents.
  `docs/README.md` — what each document is now, and what amended it.

## The model, in three sentences

**Core** installs into every repository, bar a row a pack offers off and the manifest confirms (D71); **packs** are named in the manifest; **local**
documents are the repository's own and are never synced or touched. `daoris.lock` is the authority —
anything absent from it is invisible to the tool, which is what makes a repository's own files safe.
**The tier is the location** (D7 as amended by **D59**): always-loaded is a **region in `AGENTS.md`**
that Daoris owns — because `.claude/rules/` is read by one harness of three — with `CLAUDE.md`
carrying `@AGENTS.md`; `knowledge/` is read on demand and `skills/` is invoked by name, both still
directories. No `tier` field to disagree with, and the footprint is still measurable.

Two consequences worth knowing before touching materialization: **drift is measured against the lock**,
never against the current canon (D13) — otherwise an improved rule cannot propagate. And **a skill's
provenance header goes under its frontmatter** (D14), because frontmatter is only frontmatter at byte 0.

## Layout

**Five artefacts, one workspace.** All five exist and are built (D46).

| Path | Holds |
|---|---|
| `src/Daoris.Cli/` | **The npm package `daoris`** — TypeScript, zero runtime deps. `bin/`, `src/`, `test/` |
| `src/Daoris.Service/` | The cross-repo knowledge service — indexes the family, reachable over MCP |
| `src/Daoris.Devkit/` | The shared dev toolkit — five universal gates, a **.NET AOT binary** |
| `src/Daoris.Web/` | **The platform** (D38) — knowledge, quests, projects; the only UI; doctrine read-only |
| `src/Daoris.Desktop/` | **The local driver** (D45/D46): the driver library + `daoris-driver` headless host + the shell's **modules** (every IPC surface the page talks to — plain `net10.0`, so it is tested and gated like everything else) + the window, `Daoris.Desktop.exe` on its own Chromium, started by the `Daoris.exe` launcher (D92, D93) |
| `examples/` | The example family — two miniature adopters the family rehearsal drives (D39) |
| `canon/` | **The doctrine itself** — root-level, because the service reads the same tree the CLI ships |
| `canon/core/{rules,knowledge,skills}/` | The always-installed rules, on-demand knowledge, and discovery skills |
| `canon/packs/<name>/` | `pack.json` + `rules/` + `knowledge/` + `skills/` |
| `canon/CHANGELOG.md` | Why each canon version changed — `status` prints the entries a repo is skipping |
| `tools/` | This repository's own release tooling and dev loops; not shipped |

`canon/`, `LICENSE` and `README.md` live at the root and are **staged into the CLI package at pack
time** (`tools/stage-package.mjs`, run by `prepack`) — npm's `files` cannot reach outside a package
directory, and D11 makes shipping the canon *inside* the package load-bearing.

## Dev loop

Run every command from the **workspace root**, not from a package directory.

- **`npm run verify`** — the "am I done?" gate: **`typecheck` first** (the dev loop strips types
  rather than compiling them, so without this a type error reaches `npm pack` and nothing sooner —
  FIX-LOG 2026-09-21), then every CLI test, `daoris check` against Daoris's own doctrine,
  `doc-budgets` (word ceilings for the prose a session reads whole; reported, never enforced — D54),
  `doc-duplicates` (the union-merged records hold nothing twice — D106), then `release-prep --check` (every shipped version reference agrees, example pins included), then
  the devkit's universal gates (SEN1). Run before claiming a change is complete.
- **`npm run rehearse`** — the "would a release work?" gate. Packs the tarball, installs it into a clean
  repository, and drives the whole consumer lifecycle through the `bin` entry: adopt, collide, sync,
  drift, promote, upgrade, rename, check. Everything else tests the source tree; this tests the
  **artefact**. Run before tagging.
- **`npm run rehearse:family`** — the "does the router work?" gate (D39), and since D46–D48 the
  driver's, the remote's and the boundary's. Its checks run over the example family, from both examples current and clean
  through a quest's whole life, the workspace boundary refused naming both sides, two simulated
  machines crossing a shared host, which commit a feed speaks for, a conversation and a credential
  profile from a terminal — ending at **the protocol door** (D53/ACP1), where a quest is carried to
  done over ACP by a stub agent that speaks the wire and nothing else. **It names its own phases when
  you run it**; the reasoning is in `docs/DECISIONS.md` and the archive. Run when touching
  the service, `connect`, the driver, the remote, the toolchain, or the canon's shape —
  **a canon change must re-sync `examples/` in the same commit**, and this gate enforces it.
- **`npm run test:web`** — the "does the platform work?" gate (D42). Playwright drives the shipped
  bundle over the example family: a quest through its whole life in the drawers, the verbatim
  refusal, a session record naming the tool and account that did the work, **what a browser must
  never learn about this machine**, the workspace scope once the family holds two circles (WSP5),
  中文. Declared in `daoris.gates.json`; it rebuilds the host, so
  stop a running instance first. **A shell-only surface is not out of its reach entirely** — the
  browser still owns what the RECORD shows and what must be ABSENT; the controls belong to the vitest
  inner loop over a mocked bridge (`docs/2026-09-19-frontend-architecture.md` §4).
- **`npm run rehearse:deploy`** — the "does the DEPLOYED thing work?" gate (D60), and the only one on
  Windows: it publishes the shell to scratch and drives the **artefact**, which nothing else does —
  both other rehearsals run inside the workspace, where the workspace build and the console's own
  encoding hide exactly the defects deploying found. Run when touching the desktop, the publish
  scripts or `ServiceHostLocator`.
- **There is no push/PR CI, deliberately.** `.github/workflows/release.yml` is manual-dispatch only,
  with `dry_run` defaulting to true; it runs **every gate `daoris.gates.json` declares** plus both
  rehearsals on Linux before publishing, and builds the service binaries and builds-and-tests the
  devkit on Linux, Windows and macOS. Nothing runs on push — **a gate
  you did not run locally has not been run.** Development is on Windows and release gates on Linux:
  the gap that hid D25's line-ending assumption. **The declared set and the workflow are two lists
  that must agree**, and a dogfood test holds them together: a rehearsal is no substitute for the
  tests underneath.
- **Desktop suites have two halves** (MOD8): a worktree runs `--filter Category!=Process`; the
  real-process half (`process.runsettings`, serial) runs only at the parent's merge (FLAKE1).
- **Changing what `sync` does with a file? Read `docs/DECISIONS.md` D19 first.** That state space is
  lock × disk × canon and is enumerated there; it was corrected four times before it was written down.
- **`npm run desktop -- <doctor|build|run|shot|eval|click|restart|kill>`** — the shell's dev loop, and
  **not a gate**: it starts the real window on a scratch machine of its own and lets you *see* it —
  `eval` is the only instrument that reaches the bridge-attached half (Settings' machine domains, the
  driver controls, the console, chat), which Playwright cannot reach and the vitest loop only mocks. A run
  redirects every machine-local file because the driver loop starts with the app and spawns **real
  sessions**; `--real` is your own machine and says so. `src/Daoris.Desktop/README.md` has the table.
- `node --test` — tests only.
- `node src/Daoris.Cli/bin/daoris.mjs <command>` — run the CLI against this repository.
- `DAORIS_CANON=<path>` overrides the canon root; this is how tests drive a fixture canon.

## Conventions

- **TypeScript, ESM only, Node ≥ 22.** `src/*.ts` importing `./x.ts`; the emit rewrites those to `.js`.
  `__dirname` does not exist — derive from `import.meta.url`.
- **Zero *runtime* dependencies.** TypeScript is a build dependency and the published package has none —
  that is the guarantee, not "no `devDependencies`".
- **The dev loop needs no build:** Node 24 strips types, so `node --test` runs the sources. Only
  publishing compiles, because a consumer's Node may be 22 and will not strip types on its own.
  `bin/daoris.mjs` stays `.mjs` — it is what npm's `bin` names and what every consumer executes.
- **Every write is atomic, BOM-less UTF-8, LF** — write beside, then rename. Never build file content by
  echoing through the console.
- **Exit codes are the contract:** `0` clean · `1` policy failure · `2` tool error.
- **`check` works offline, and so does every doctrine command.** The exceptions are the **management
  class** — `connect`, `retire`, `import` talk to a service and `agent pin` to a maker's release
  channel; `remote`, `agent` and `driver` edit files under the home, and `agent trust` one flag in the
  agent's file — all opt-in and never run by a gate (D35, D50). Three tests hold the line:
  only `service.ts` may contain a network primitive, only `toolchain.ts` may spawn a harness, and
  nothing
  any doctrine command transitively imports may reach either — the last is the one that matters, because
  a gate breaks by an import three modules deep, not by an obvious `fetch`. **Keep the network in that
  one module**: a management class whose members each opened a socket would turn the first test into a
  list, and a list is something people append to. Spawning is the same shape of rule for the same
  reason — what makes it fine (a person asked for it) stops holding the moment `check` can reach it.
- **Plan and apply are separate functions**, so a plan can be printed or asserted without touching disk.
- **A `tools/` script whose helpers are imported guards its runner** behind
  `isMain(import.meta.url)` from `tools/fsx.mjs`, which compares real paths — exports above, phases
  below. Without it `node --test` runs the tool: `desktop.mjs` says so at its foot, and
  `deployment-rehearsal.mjs` published a folder and opened a window during a unit test before it
  learned the same thing. A plain string comparison is false through a junction, and a gate then runs
  nothing and exits 0.
- **A code comment gives the reason, never the owner's words.** Name the task or decision it came
  from (`DOCK1a`, `D81`) and say why; a quotation belongs in the decision or design document.
- **TDD** — failing test first. **Commit per task, automatically, once gates are green** (D37 as
  amended) — the landed history is the reviewable record. **Push, publish, release and history
  rewrites stay the owner's call.**

## Writing canon files

A canon file installs into repositories you have never seen, so it must be **project-agnostic**: no
product names, no build commands, no directory layouts specific to one repository. State the principle
and the reason; leave the mechanism to the adopting repository's own local documents.

Every canon file carries frontmatter — `name` (matching the filename), `applies_when`, `enforces` — which
generates its row in the index. Tests enforce all of it. See `.claude/knowledge/canon-authoring.md`.

<!-- daoris:import — generated; edit the canon, not this -->
@AGENTS.md
<!-- /daoris:import -->
