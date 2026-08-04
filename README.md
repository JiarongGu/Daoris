# Daoris (道衍)

**Cross-repo engineering doctrine.** One canonical set of agent-facing rules and knowledge,
materialized into each repository, kept from drifting — and improved from wherever the improvement was
discovered.

道衍 is *propagation and unfolding*: doctrine flows outward into the repositories, and refinements found
in a repository flow back and evolve the canon. Both directions ship in v0.1, because a one-way push
would be distribution, not cultivation.

## Ecosystem

Three public projects, deliberately independent:

| | |
|---|---|
| [Lyntai](https://github.com/OWNER/Lyntai) | LLM cognition — providers, routing, memory, evaluation |
| [Shenora](https://github.com/OWNER/Shenora) | Desktop runtime — the shell an application is built in |
| **Daoris** | Engineering doctrine — how the work itself is done |

Daoris takes no dependency on either. It is the only one of the three that installs *into* the others.

## The problem

The same doctrine gets independently re-derived in every repository, and the copies diverge. A rule that
turns out to be wrong stays wrong in five places, because nothing knows the copies exist. Measured on
this repository's own seeding: a doctrine set copied from a sibling **three days earlier** already
differed in 12 of 19 files.

## Install

Nothing to install. Every command runs through `npx` against a pinned reference:

```sh
npx github:OWNER/daoris#v0.1.0 init     # write daoris.json, report available packs
npx github:OWNER/daoris#v0.1.0 sync     # materialize the doctrine, write daoris.lock
npx github:OWNER/daoris#v0.1.0 check    # the gate — offline, exit 1 on drift
```

The canon ships **inside the package**, so the pinned reference is itself the version pin — no command
ever fetches anything, and `check` therefore works with no network at all.

## Commands

| Command | What it does |
|---|---|
| `init` | Detects what the repository already has, writes `daoris.json`, reports available packs |
| `sync` | Materializes the manifest's packs into `.claude/`, writes `daoris.lock`, regenerates the index |
| `check` | Drift, staleness, index freshness, core budget. **Offline.** Exit 1 on any failure |
| `upstream <file>` | Promotes a locally-improved canonical file back into the canon |
| `index` | Regenerates `RULES_INDEX.md` from what is on disk |
| `status` | Human summary: packs, versions, drift, local files, available updates |
| `doctor` | Reports local documents that look like canonical ones under a different name. **Advisory — never fails** |

`sync` accepts `--dry-run` (print the plan, write nothing) and `--force`. `upstream` accepts `--all` to
promote every locally-edited canonical file at once.

`doctor` exists because of the one thing the lock cannot catch: a repository's own rule that says the same
thing as a canonical one under a different name is *local*, and local is invisible by design. Word overlap
is a crude signal, so it only ever reports — a false positive that failed a build would be worse than the
duplication it warns about.

## The manifest

```json
{
  "source": "github:OWNER/daoris#v0.1.0",
  "packs": ["dotnet-library"],
  "target": ".claude",
  "coreBudgetBytes": 24000
}
```

`daoris.lock` sits beside it, generated: one entry per materialized file, recording its pack, canonical
path, version, and content hash. Both are tracked, so a reviewer sees exactly what changed.

## Three layers

- **Core** — universal workflow rules. Every repository gets these; there is no opting out.
- **Packs** — stack-specific sets, named in the manifest.
- **Local** — the repository's own documents. Never synced, never touched, and listed in the generated
  index marked `(local)`.

The rule that makes this safe: **anything not in the lock is invisible to the tool.** Daoris only ever
writes files it put there. A repository that already owns a file at a canonical path gets a refusal, not
a silent overwrite.

## Two things worth knowing

**The tier is the directory.** Files in `rules/` are always-loaded context; files in `knowledge/` are read
on demand. The agent harness decides that by path, so Daoris does not carry a redundant `tier` field —
and because the tier is measurable, `check` reports the always-loaded footprint and fails over a budget.

**Every vendored file carries a one-line provenance header.** Not decoration: an agent that opens a rule
needing a tweak will otherwise simply edit it, which is exactly how the copies diverged. The header says
where the file came from and to use `daoris upstream`; the lock's hash catches the edit either way.

## Developing Daoris

```sh
npm run verify          # tests, then daoris check against its own doctrine
node --test             # tests only
```

`DAORIS_CANON` overrides the canon root, which is how the tests drive a fixture canon.

Daoris carries its own `daoris.json` and syncs core into its own `.claude/`. A tool that cannot hold its
own doctrine cannot hold anyone else's.
