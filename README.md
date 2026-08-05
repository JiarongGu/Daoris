# Daoris (道衍)

**Cross-repo engineering doctrine.** One canonical set of agent-facing rules and knowledge,
materialized into each repository, kept from drifting — and improved from wherever the improvement was
discovered.

道衍 is *propagation and unfolding*: doctrine flows outward into the repositories, and refinements found
in a repository flow back and evolve the canon. Both directions ship in the first release, because a
one-way push would be distribution, not cultivation.

## Ecosystem

Three public projects, deliberately independent:

| | |
|---|---|
| [Lyntai](https://github.com/JiarongGu/Lyntai) | LLM cognition — providers, routing, memory, evaluation |
| [Shenora](https://github.com/JiarongGu/Shenora) | Desktop runtime — the shell an application is built in |
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
npx github:JiarongGu/Daoris#v0.0.1 init     # write daoris.json, report available packs
npx github:JiarongGu/Daoris#v0.0.1 sync     # materialize the doctrine, write daoris.lock
npx github:JiarongGu/Daoris#v0.0.1 check    # the gate — offline, exit 1 on drift
```

The canon ships **inside the package**, so the pinned reference is itself the version pin — no command
ever fetches anything, and `check` therefore works with no network at all.

## Commands

| Command | What it does |
|---|---|
| `analyze` | **What adopting would do here** — collisions, duplicates, projected budget. Writes nothing |
| `init` | Detects what the repository already has, writes `daoris.json`, reports available packs |
| `sync` | Materializes the manifest's packs into `.claude/`, writes `daoris.lock`, regenerates the index |
| `check` | Drift, staleness, index freshness, core budget. **Offline.** Exit 1 on any failure |
| `upstream <file>` | Promotes a locally-improved canonical file back into the canon |
| `index` | Regenerates `RULES_INDEX.md` from what is on disk |
| `status` | Human summary: packs, versions, drift, local files, and what a pending update would change |
| `doctor` | Reports local documents that look like canonical ones under a different name. **Advisory — never fails** |
| `connect` | Registers this repo with a knowledge service — what it owns, what it accepts. **The only command that uses the network**, and it is opt-in |


`sync` accepts `--dry-run` (print the plan, write nothing) and `--force`. `upstream` accepts `--all` to
promote every locally-edited canonical file at once.

**`--force` is the only way to lose work here**, so it names every file it overwrites or discards. Daoris
otherwise refuses in all three destructive cases: a file you edited, a file you wrote before adopting,
and a file being retired upstream that you had improved — that last one being the worst moment to lose an
edit, since the canonical file it belonged to is gone and `upstream` can no longer save it.

`analyze` answers the question a repository has *before* it adopts: what already exists here, what
would collide, what already says the same thing under another name, and what the always-loaded budget
becomes. It writes nothing, and `--json` gives an agent the exact facts to act on.

The division of labour is deliberate. **Daoris supplies what must be exact** — which paths collide,
which documents duplicate, what it costs — because an agent guessing at a collision is wrong in a way
that destroys files. **The agent supplies judgement** — which packs fit this repository, whether a
suspected twin really is one, how to resolve each collision — because a regex guessing at "is this a
.NET library" is wrong in a way that costs a sentence. Then a person selects.

It is also the only command that compares the working tree against the **canon** rather than the lock,
which is what lets it find a renamed twin *before* adoption rather than after — and, on this
repository, what caught a set of stale provenance headers that every lock-based check agreed was fine.

`doctor` exists because of the one thing the lock cannot catch: a repository's own rule that says the same
thing as a canonical one under a different name is *local*, and local is invisible by design. Word overlap
is a crude signal, so it only ever reports — a false positive that failed a build would be worse than the
duplication it warns about.

Its threshold is set from measurement against real sibling documents, not taste: near-verbatim copies
score ~73%, twins that were *rewritten* rather than copied land at 34–43%, and unrelated documents at
7–16%. It finds **restatement, not convergence** — a document that reaches the same principle through an
entirely different vocabulary scores like an unrelated one, and no threshold separates those. Adoption
still wants a read-through by hand; `doctor` shortens that job rather than replacing it.

## The manifest

```json
{
  "source": "github:JiarongGu/Daoris#v0.0.1",
  "packs": ["dotnet-library"],
  "target": ".claude",
  "coreBudgetBytes": 30000
}
```

`daoris.lock` sits beside it, generated: one entry per materialized file, recording its pack, canonical
path, version, and content hash. Both are tracked, so a reviewer sees exactly what changed.

## Three layers

- **Core** — universal workflow rules and discovery skills. Every repository gets these; no opting out.
- **Packs** — stack-specific sets, named in the manifest.
- **Local** — the repository's own documents. Never synced, never touched, and listed in the generated
  index marked `(local)`.

The rule that makes this safe: **anything not in the lock is invisible to the tool.** Daoris only ever
writes files it put there. A repository that already owns a file at a canonical path gets a refusal, not
a silent overwrite.

## Two things worth knowing

**The tier is the directory.** Files in `rules/` are always-loaded context; files in `knowledge/` are read
on demand; `skills/<name>/SKILL.md` is invoked by name. The agent harness decides that by path, so Daoris
does not carry a redundant `tier` field — and because the tier is measurable, `check` reports the
always-loaded footprint and fails over a budget.

**Canonical skills are parameter-free.** A skill states only the procedure that holds in every
repository and sends the reader to the generated index for anything local — there is no substitution map
in the manifest. Surveying twelve repositories showed why: copies of the same skill ranged over a 6.6×
size spread, and the shared part was ~15 lines. The rest was each repository's own routing content, which
no placeholder could have supplied. The index's skills table is what a hand-written "here are our skills"
skill used to be, except that it is generated and therefore never stale.

**Every vendored file carries a one-line provenance header.** Not decoration: an agent that opens a rule
needing a tweak will otherwise simply edit it, which is exactly how the copies diverged. The header says
where the file came from and to use `daoris upstream`; the lock's hash catches the edit either way.

## Developing Daoris

```sh
npm run verify          # tests, then daoris check against its own doctrine
npm run rehearse        # pack, install into a clean repo, drive the full lifecycle
node --test             # tests only
```

`rehearse` is the release gate. The test suite exercises the source tree; the rehearsal exercises the
**artefact** — the tarball npm would publish, resolved through the `bin` entry the way a consumer runs
it. That is where install stories break: a file missing from `files`, a path that only resolves in a
source checkout, a skill directory that does not survive packing.

`DAORIS_CANON` overrides the canon root, which is how the tests drive a fixture canon.

Daoris carries its own `daoris.json` and syncs core into its own `.claude/`. A tool that cannot hold its
own doctrine cannot hold anyone else's.
