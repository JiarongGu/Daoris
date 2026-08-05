# CLAUDE.md — Daoris (道衍)

> Auto-loaded every session. Keep short — detail lives in `docs/` and `.claude/`.

## What this is

**Daoris** (道衍, "the unfolding of the way") is the **substrate for domain-owning agents to share
knowledge and work** across this family of projects. Each repository has its own agent, which owns that
domain; Daoris is how they hold one canon of doctrine between them, find where they have learned the
same thing twice, and **ask each other for changes instead of reaching in**.

That last part is the constraint everything else serves: **repositories are not developed across.** A
change you need elsewhere is a request filed in that repository's own backlog, worked by whoever knows
that code — because the *why* behind a codebase does not travel, and a request does (`daoris request`).

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

**Built and proven; nothing published.** Nine commands, 120 tests, a canon of 8 core rules, 4 core
knowledge documents, 5 core skills and 6 packs. `Daoris.Service` adds 70 and `Daoris.Devkit` 57.
Daoris carries its own manifest and syncs core into its own `.claude/`. Adopted into **Lyntai** as the
first real consumer — 4 collisions and a renamed twin surfaced and were resolved, its 1337 tests stayed
green, and the budget gate caught a genuine 45% overage on first contact.

**All four artefacts exist**; only `Daoris.Desktop` — the shell that hosts the same web build — is a
brief. **Nothing is published**, and development runs at `0.0.x`.

**Two things to know before changing anything.** The always-loaded core sits at **23,568 of 24,000
bytes**, so the next canon addition fails the budget gate — that is the gate working, and the answer is
to split principle from detail rather than raise the limit (D28). And **never write into another
repository**: that constraint is absolute (D32), it was broken here and cost a sibling an uncommitted
edit, and `.claude/knowledge/reaching-in.md` is the account.

**Never edit the version by hand, and never stamp a changelog heading.** Both belong to the release
workflow (`tools/release-prep.mjs`); the desktop sibling burned a version outright on exactly this. A
hand-bump leaves every file perfectly consistent and still wrong — consistency was never the property at
risk, **authorship** was.

- `README.md` — the consuming story: install, the nine commands, the manifest, the three layers.
- `docs/2026-08-04-daoris-design.md` — the **contract**. Read it first.
- `docs/DECISIONS.md` — the numbered decision log (D1–D35) and why each was made.
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

**Four artefacts, one workspace.** All four exist; only the desktop shell is unbuilt, and it carries a
`README.md` stating its brief.

| Path | Holds |
|---|---|
| `src/Daoris.Cli/` | **The npm package `daoris`** — TypeScript, zero runtime deps. `bin/`, `src/`, `test/` |
| `src/Daoris.Service/` | The cross-repo knowledge service — indexes the family, reachable over MCP |
| `src/Daoris.Devkit/` | The shared dev toolkit — five universal gates, a **.NET AOT binary** |
| `src/Daoris.Web/` | React UI over the service — the only UI; convergence first, read-only |
| `src/Daoris.Desktop/` | Desktop shell hosting `Daoris.Web`, on the desktop sibling (not started) |
| `canon/` | **The doctrine itself** — root-level, because the service reads the same tree the CLI ships |
| `canon/core/{rules,knowledge,skills}/` | The always-installed rules, on-demand knowledge, and discovery skills |
| `canon/packs/<name>/` | `pack.json` + `rules/` + `knowledge/` + `skills/` |
| `canon/CHANGELOG.md` | Why each canon version changed — `status` prints the entries a repo is skipping |
| `tools/` | This repository's own release tooling; not shipped |

`canon/`, `LICENSE` and `README.md` live at the root and are **staged into the CLI package at pack
time** (`tools/stage-package.mjs`, run by `prepack`) — npm's `files` cannot reach outside a package
directory, and D11 makes shipping the canon *inside* the package load-bearing.

## Dev loop

Run every command from the **workspace root**, not from a package directory.

- **`npm run verify`** — the "am I done?" gate: every test, then `daoris check` against Daoris's own
  doctrine. Run before claiming a change is complete.
- **`npm run rehearse`** — the "would a release work?" gate. Packs the tarball, installs it into a clean
  repository, and drives the whole consumer lifecycle through the `bin` entry: adopt, collide, sync,
  drift, promote, upgrade, rename, check. Everything else tests the source tree; this tests the
  **artefact**. Run before tagging.
- **There is no push/PR CI, deliberately.** `.github/workflows/release.yml` is manual-dispatch only, with
  `dry_run` defaulting to true; it runs both gates on Linux before publishing. Nothing runs on push, and
  nothing runs on Windows or macOS — **a gate you did not run locally has not been run.** Development
  happens on Windows and the release on Linux, which is exactly the gap that hid D25's line-ending
  assumption.
- **Changing what `sync` does with a file? Read `docs/DECISIONS.md` D19 first.** That state space is
  lock × disk × canon and is enumerated there; it was corrected four times before it was written down.
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
- **`check` works offline, and so does every doctrine command.** `connect` is the single exception and
  is opt-in (D35). Two tests hold the line: only `connect.ts` may contain a network primitive, and
  nothing `check` transitively imports may reach it — the second is the one that matters, because a gate
  breaks by an import three modules deep, not by an obvious `fetch`.
- **Plan and apply are separate functions**, so a plan can be printed or asserted without touching disk.
- **TDD** — failing test first. **Commit per task.** **Never commit without the user's approval.**

## Writing canon files

A canon file installs into repositories you have never seen, so it must be **project-agnostic**: no
product names, no build commands, no directory layouts specific to one repository. State the principle
and the reason; leave the mechanism to the adopting repository's own local documents.

Every canon file carries frontmatter — `name` (matching the filename), `applies_when`, `enforces` — which
generates its row in the index. Tests enforce all of it. See `.claude/knowledge/canon-authoring.md`.
