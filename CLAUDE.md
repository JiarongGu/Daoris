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
the platform's Quests view; the CLI deliberately has no quest command, D31 as amended).

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

**Built and proven; nothing published.** Fourteen commands, 209 CLI tests, a canon of 8 core rules, 5 core
knowledge documents, 5 core skills and 6 packs. `Daoris.Service` adds 258, `Daoris.Devkit` 57, and the
driver 141. Daoris carries its own manifest and syncs core into its own `.claude/`. Adopted into **Lyntai** as the
first real consumer — 4 collisions and a renamed twin surfaced and were resolved, its 1337 tests stayed
green, and the budget gate caught a genuine 45% overage on first contact. **Lyntai has since stepped
off the tool at its owner's request** (2026-08-17; the synced files stayed as local forks), so the
proof stands and the live consumer count is zero — adoption has to be near-free for the family to come
back, which is what the automation-first direction is for.

**All five artefacts exist and are built, and all three parts of D45 with them** — including
`Daoris.Desktop`, the local driver (D45/D46): the shell brings up the local host, carries the
platform, runs the driver loop, and lands the person's session controls. The **remote server**
(D47/DRV5) is the same HTTP host in **shared mode** — every route gated by per-person per-machine
minted keys, no page and no machine path served, refusing to bind beyond loopback in local mode — with
the quest lock hardened into code (an atomic guarded `Taken`, closed quests immovable) and the
desktop's sync loop feeding records and content up and mirroring quests down. The family rehearsal
gates the whole thing with no model: driver loop, a two-machine remote crossing, **the workspace
boundary, the registration lifecycle, the remotes map, which commit speaks, a whole
conversation and a session running as a named account** (151/151). The D48 arc is under way. **The
workspace exists** (WSP1) — it is the unit of sharing, it is **wiring rather than a tracked
declaration** (`connect --workspace`, a registry row, no manifest field), and every cross-repository
entity and answer carries or is scoped by it. **The registry is the authority** (WSP2) — an explicit
list, not a view over a folder: `connect` adds, `retire` removes (touching no file), `import` is the
old scan demoted to a verb, a store that has never been managed imports its root once and says so, and
a registered checkout that has vanished is named rather than silently skipped. **The remotes are a
map** (WSP3) — `~/.daoris/remotes.json`, one deployment per workspace: a shared host carries its own
`DAORIS_WORKSPACE` identity and refuses a registration declaring another, the sync runs per circle, the
quest relay resolves by the quest's workspace, and `daoris remote list|add|remove`, `status --machine`
and the desktop's Machine view are editors over the one file — a key never printed back beyond its
audit prefix. **A feed carries the commit it speaks for** (WSP4) — the driver stamps git provenance,
knowledge feeds only from the declared canonical line, a replacement must be newer than what is held
(a stale one is refused as *information*, not a failure), and every deployment serves the commit its
copy stands on, so staleness is seen rather than assumed. **The console streams** (D49/SES1) — the
capture pump tees into a bounded per-session buffer and the shell relays it to the session drawer,
verbatim and desktop-only: output is transcript-class material and has no HTTP surface at all. **A
conversation is a session** (D49/SES2) — `Kind: driven | chat`, quest optional, the same lock on the
same working tree; the adapter seam grows `interactive`, the harness carries the model and Daoris
pipes text, and a chat runs from the desktop or from `daoris-driver chat` on a machine with no
screen. **The toolchain is Daoris's** (D49/SES3) — it finds each harness, reports its version, and
installs, updates and logs it in **through that harness's own mechanism, only when asked**; many
accounts per harness are **named credential profiles**, isolated configuration directories whose
location Daoris owns and whose contents it never touches, chosen per machine, per workspace or per
conversation. A record names the tool version and the account it ran as; a spawn onto a missing
harness or a profile nobody signed into refuses **naming the action that fixes it**. `daoris harness`
and `daoris driver` do all of it from a terminal (D50).
**Nothing is published**, and development runs at `0.0.x`. **Development is automation-first** (D37):
the person sets the target and verifies the final diff; agents execute and gates verify the middle —
see canon knowledge `autonomous-development`.

**Three things to know before changing anything.** The always-loaded core sits at **23,862 of 24,000
bytes** — 138 bytes of headroom, still far less than any new rule, so the next canon addition fails the
budget gate. That is the gate working, and the answer is to split principle from detail rather than
raise the limit (D28) — CANON6 did exactly that and paid for its own carve-out. And **never write into
another
repository**: that constraint is absolute (D32), it was broken here and cost a sibling an uncommitted
edit, and `.claude/knowledge/reaching-in.md` is the account. And **doctrine must not hard-require
Daoris** (D48 §2a): the canon instructs exactly one thing that needs a service running — publishing a
quest — and it names the alternative in the same breath, held by a canon scan. Everything else `sync`
writes is committed, so it survives the tool's absence; the test is *file or service*, never family
vocabulary (`.claude/knowledge/canon-authoring.md`).

**Never edit the version by hand, and never stamp a changelog heading.** Both belong to the release
workflow (`tools/release-prep.mjs`); the desktop sibling burned a version outright on exactly this. A
hand-bump leaves every file perfectly consistent and still wrong — consistency was never the property at
risk, **authorship** was.

- `README.md` — the consuming story: install, the commands, the manifest, the three layers.
- `docs/2026-08-04-daoris-design.md` — the **contract**. Read it first.
- `docs/DECISIONS.md` — the numbered decision log (D1–D53) and why each was made. **D45 is the
  direction: Daoris drives** — read it before planning anything; **D48–D50 are the closed arc**
  (workspaces; the interactive surface; management parity) — WSP1–4, SES1–3 and CANON6 have all landed.
- **The current arc is the desktop as a working surface** (owner, 2026-09-20; designed 2026-09-21):
  code sessions the way a terminal agent CLI holds them, across agents, repositories and concurrent
  sessions, designed with real UI/UX. The contract is
  **`docs/2026-09-21-working-surface-design.md`** (research behind it:
  `docs/2026-09-20-working-surface-research.md`), carried by **D51** — *the tree is the unit of
  exclusion, and a repository may have more than one*, which keeps "two agents in one tree corrupt it"
  intact and drops only the incidental cap that a repository has one tree — and **D52**, the surface
  itself. SURF2 and SURF3 have landed — the lock keys on the tree, and the trees exist. **Anything
  with a screen in it is built component by component** —
  `docs/2026-09-21-working-surface-components.md` (D52 as amended): a story before the component, its
  own test, and **a molecule imports no hook**, which is what makes every state reachable by passing
  props. **The dsh evaluation has run** (DSH1, 2026-09-21): `docs/2026-09-21-dsh-direction.md` was
  the plan, **`docs/2026-09-21-dsh-evaluation.md`** is the evidence — eight probes against an installed
  `dsh` over a scratch repository, on a scripted provider because no key was supplied — and **D53 is
  the proposed decision, awaiting the owner**: *dsh is adopted as a protocol, not a product*. The
  adapter seam grows an **ACP door**; dsh and codex arrive as configurations of it; the working surface
  stays `Daoris.Web`. SURF4a–d hold on the owner's answer and resume as designed under the proposal;
  the build order under D53 is ACP1 → ACP2 → ACP3 in the backlog. The probe instruments are tracked
  under `tools/dsh-probes/`.
- `docs/2026-09-19-platform-ux.md` — the platform's design language (D41): the shell, the tokens, the
  validated status palette, the interaction rules. Read before changing anything a person looks at.
- `ROADMAP.md` — the forward sequence. `TASKS.md` — the **active** backlog (open items only).
- `docs/task-archive.md` — completed work, with outcomes. `docs/archive/` — superseded documents.

## The model, in three sentences

**Core** installs into every repository with no opt-out; **packs** are named in the manifest; **local**
documents are the repository's own and are never synced or touched. `daoris.lock` is the authority —
anything absent from it is invisible to the tool, which is what makes a repository's own files safe.
**The tier is the directory**: `rules/` is always-loaded context, `knowledge/` is read on demand,
`skills/` is invoked by name, and the agent harness decides that by path — so there is no `tier` field to
disagree with.

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
| `src/Daoris.Desktop/` | **The local driver** (D45/D46): the driver library + `daoris-driver` headless host + the shell's **modules** (every IPC surface the page talks to — plain `net10.0`, so it is tested and gated like everything else) + the `daoris-desktop` window |
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

- **`npm run verify`** — the "am I done?" gate: every CLI test, `daoris check` against Daoris's own
  doctrine, `doc-budgets` (word ceilings for the prose a session reads whole — D28's discipline applied
  to this repository's own standing orders; the append-only records deliberately have none), then
  `release-prep --check` (every shipped version reference agrees, example pins included). Run before
  claiming a change is complete.
- **`npm run rehearse`** — the "would a release work?" gate. Packs the tarball, installs it into a clean
  repository, and drives the whole consumer lifecycle through the `bin` entry: adopt, collide, sync,
  drift, promote, upgrade, rename, check. Everything else tests the source tree; this tests the
  **artefact**. Run before tagging.
- **`npm run rehearse:family`** — the "does the router work?" gate (D39), since D46 the "does the
  driver drive?" gate, since D47 the "does the remote cross?" gate, and since D48 the "does the
  boundary hold?" gate. Both examples current and
  clean, the HTTP host up over them, `connect`, a quest through its whole life, a search crossing
  projects, restart persistence, a quest driven to done by a stub session — then two workspaces on one
  machine, wired by `connect --workspace` with no tracked file touched, a search answering from one
  circle while the other holds the same lesson word for word, and a quest across the boundary refused
  naming both sides — then the registration lifecycle from a terminal: a folder nobody registered
  staying invisible, `import` adding it without re-pointing anyone's workspace, `retire` removing it
  with every file still there, and a vanished checkout named — then a shared host with
  minted keys, two simulated machines, a quest crossing them, a raced take standing down, and the
  remote's store scanned for anything machine-local — then the remotes map: a workspace's own
  deployment wired and unwired from a terminal, only its circle feeding it while a joined-and-sharing
  repository in another circle reaches it not at all, and a registration declaring another workspace
  refused naming both — then which commit speaks: a newer feed replacing wholesale and carrying a
  deletion with it, a stale one refused as information, an unmerged branch refused though newer, and
  the fed commit served back — then a conversation held from a terminal, and the toolchain: a session
  spawned under a named credential profile and proving from its own output that it ran in that
  configuration home, a logged-out profile and an uninstalled harness each holding the start with the
  sentence that fixes it, and both surfaces driven from a terminal.
  No model, no account and no credential anywhere in the gate. Run when touching
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
- **There is no push/PR CI, deliberately.** `.github/workflows/release.yml` is manual-dispatch only,
  with `dry_run` defaulting to true; it runs **every gate `daoris.gates.json` declares** plus both
  rehearsals on Linux before publishing, and builds the service binaries and builds-and-tests the
  devkit on Linux, Windows and macOS. Nothing runs on push — **a gate
  you did not run locally has not been run.** Development happens on Windows and the release gates on
  Linux, which is exactly the gap that hid D25's line-ending assumption. **The declared set and the
  workflow are two lists that must agree**, and they silently did not: the service's 248 tests were
  declared and never run, and the driver's 130 were in neither. A rehearsal driving the same code end
  to end is what hid it — and is not a substitute for the judgement underneath.
- **Changing what `sync` does with a file? Read `docs/DECISIONS.md` D19 first.** That state space is
  lock × disk × canon and is enumerated there; it was corrected four times before it was written down.
- **`npm run desktop -- <doctor|build|run|shot|eval|click|restart|kill>`** — the shell's dev loop, and
  **not a gate**: it starts the real window on a scratch machine of its own and lets you *see* it —
  `eval` is the only instrument that reaches the bridge-attached half (the Machine view, the driver
  controls, the console, chat), which Playwright cannot reach and the vitest loop only mocks. A run
  redirects every `~/.daoris` file because the driver loop starts with the app and spawns **real
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
  class** — `connect`, `retire`, `import` talk to a service; `remote`, `harness` and `driver` edit
  files under the profile — all opt-in and never run by a gate (D35, D50). Three tests hold the line:
  only `service.ts` may contain a network primitive, only `toolchain.ts` may spawn a harness, and
  nothing
  any doctrine command transitively imports may reach either — the last is the one that matters, because
  a gate breaks by an import three modules deep, not by an obvious `fetch`. **Keep the network in that
  one module**: a management class whose members each opened a socket would turn the first test into a
  list, and a list is something people append to. Spawning is the same shape of rule for the same
  reason — what makes it fine (a person asked for it) stops holding the moment `check` can reach it.
- **Plan and apply are separate functions**, so a plan can be printed or asserted without touching disk.
- **TDD** — failing test first. **Commit per task, automatically, once gates are green** (D37 as
  amended) — the landed history is the reviewable record. **Push, publish, release and history
  rewrites stay the owner's call.**

## Writing canon files

A canon file installs into repositories you have never seen, so it must be **project-agnostic**: no
product names, no build commands, no directory layouts specific to one repository. State the principle
and the reason; leave the mechanism to the adopting repository's own local documents.

Every canon file carries frontmatter — `name` (matching the filename), `applies_when`, `enforces` — which
generates its row in the index. Tests enforce all of it. See `.claude/knowledge/canon-authoring.md`.
