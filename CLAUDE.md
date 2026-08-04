# CLAUDE.md — Daoris (道衍)

> Auto-loaded every session. Keep short — detail lives in `docs/` and `.claude/`.

## What this is

**Daoris** (道衍, "the unfolding of the way") is the **cross-repo engineering doctrine** for this family
of projects: one canonical set of agent-facing rules and knowledge, materialized into each repository,
kept from drifting, and improved from wherever the improvement was found.

It is a **zero-dependency Node CLI plus a canon of markdown**. Not a library, not an LLM tool, not a
framework — it makes no model calls and takes no dependency on anything.

道衍 is *propagation and unfolding*: doctrine flows outward into the repositories, refinements flow back
and evolve the canon. Both directions ship, because a one-way push would be distribution, not
cultivation.

## Current state

**v0.1 built and proven; not yet released.** Six commands, 58 tests, canon of 6 core rules + 3 packs.
Daoris carries its own manifest and syncs core into its own `.claude/`. Adopted into **Lyntai** as the
first real consumer — 4 collisions and a renamed twin surfaced and were resolved, its 1337 tests stayed
green, and the budget gate caught a genuine 45% overage on first contact.

**Release is blocked on two owner decisions** — the GitHub account (`OWNER` is a literal placeholder in
`src/commands.mjs`, both manifests, and the README) and the LICENSE copyright holder. See `TASKS.md`.

- `README.md` — the consuming story: install, the six commands, the manifest, the three layers.
- `docs/2026-08-04-daoris-design.md` — the **contract**. Read it first.
- `docs/DECISIONS.md` — the numbered decision log (D1–D12) and why each was made.
- `ROADMAP.md` — the forward sequence. `TASKS.md` — the **active** backlog (open items only).
- `docs/task-archive.md` — completed work, with outcomes.

## The model, in three sentences

**Core** installs into every repository with no opt-out; **packs** are named in the manifest; **local**
documents are the repository's own and are never synced or touched. `daoris.lock` is the authority —
anything absent from it is invisible to the tool, which is what makes a repository's own files safe.
**The tier is the directory**: `rules/` is always-loaded context, `knowledge/` is read on demand, and the
agent harness decides that by path — so there is no `tier` field to disagree with.

## Layout

| Path | Holds |
|---|---|
| `bin/daoris.mjs` | Arg parsing, command dispatch, error → exit code |
| `src/*.mjs` | One module per responsibility; every command is plan-then-apply |
| `canon/core/*.md` | The always-installed rules |
| `canon/packs/<name>/` | `pack.json` + `rules/` + `knowledge/` |
| `test/*.test.mjs` | `node:test`; fixtures under the gitignored `_fixtures/` |

## Dev loop

- **`npm run verify`** — the "am I done?" gate: every test, then `daoris check` against Daoris's own
  doctrine. Run before claiming a change is complete.
- `node --test` — tests only.
- `node bin/daoris.mjs <command>` — run the CLI against this repository.
- `DAORIS_CANON=<path>` overrides the canon root; this is how tests drive a fixture canon.

## Conventions

- **Zero runtime dependencies**, ESM only, Node ≥ 22. `__dirname` does not exist — derive from
  `import.meta.url`.
- **Every write is atomic, BOM-less UTF-8, LF** — write beside, then rename. Never build file content by
  echoing through the console.
- **Exit codes are the contract:** `0` clean · `1` policy failure · `2` tool error.
- **`check` must never touch the network.** No `http`/`https`/`fetch` anywhere in the codebase; a test
  asserts it passes with the canon deleted.
- **Plan and apply are separate functions**, so a plan can be printed or asserted without touching disk.
- **TDD** — failing test first. **Commit per task.** **Never commit without the user's approval.**

## Writing canon files

A canon file installs into repositories you have never seen, so it must be **project-agnostic**: no
product names, no build commands, no directory layouts specific to one repository. State the principle
and the reason; leave the mechanism to the adopting repository's own local documents.

Every canon file carries frontmatter — `name` (matching the filename), `applies_when`, `enforces` — which
generates its row in the index. Tests enforce all of it. See `.claude/knowledge/canon-authoring.md`.
