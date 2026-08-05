# Daoris v0.1 — design

> **道衍** — doctrine that unfolds outward into the repos, and evolves from what they learn.
> Status: **approved 2026-08-04**, and built. This is the contract for the **CLI**, and it still holds.
>
> **Scope note.** Everything below describes `Daoris.Cli`, which is still Node, still zero-dependency, and
> still makes no model calls. It does **not** describe the whole project: `Daoris.Service` arrived later,
> depends on the cognition sibling, and may call an embedding endpoint — see
> [`2026-08-05-knowledge-service-design.md`](2026-08-05-knowledge-service-design.md) and D24. The split is
> deliberate, and §1's "no model calls at all" is a statement about this artefact rather than a promise
> the project as a whole ever made.

## 1. What Daoris is

Daoris standardizes **the engineering process across repos**: one canonical set of agent-facing rules and
knowledge, materialized into each repo, kept from drifting, and improved from wherever the improvement was
discovered.

It is **not** an LLM framework. The sibling library Lyntai already ships the LLM cognition layer — providers,
routing, prompts, semantic memory, embedders, scoring/eval, an agent tool loop, durable jobs, MCP in both
directions. Six of the fourteen packages sketched in the original framework note would have duplicated it.
Daoris v0.1 takes **no dependency on Lyntai and makes no model calls at all**.

**v0.1 is markdown + a node CLI.** Nothing else. It is pre-1.0: the command set below is a starting set, not a
frozen surface, and commands may be added or reshaped without ceremony until 1.0.

## 2. The problem, with evidence

The same doctrine has been independently re-derived in every repo, and the copies have diverged:

- Two public siblings each maintain their own always-loaded core, their own index format, and their own
  tooling for it — one exposes `verify`-style gates, the other a separate `knowledge` command with
  `new`/`check`/`footprint`. A third is described in its own notes as carrying the "latest org-system design",
  i.e. a third variant.
- Rules that are genuinely universal — no leaked machine paths or private names, no OS temp for repo files,
  the task/backlog lifecycle, file-tool discipline — exist as independent rewrites in each repo. A fix to one
  reaches none of the others.
- **This repo is the proof.** Daoris was seeded by copying a sibling's `.claude/` wholesale, and arrived
  carrying that sibling's WinForms, WebView2 and IPC knowledge plus a private-context file still naming the
  sibling as "this repo". The new project caught the disease on day one.

Copy-paste doctrine has no retirement path either: a rule that turns out to be wrong stays wrong in five
repos forever, because nothing knows the copies exist.

## 3. Decisions

**D1 — Daoris is process tooling, not an LLM library.** No model calls, no Lyntai dependency in v0.1. A future
centralized knowledge service (cross-repo RAG over doctrine and task history) is a separate sub-project that
*would* build on Lyntai; it is out of scope here and must not leak into v0.1's shape.

**D2 — Manifest + vendored copy + drift check.** A repo declares what it wants; the tool materializes real
`.md` files into `.claude/` and records hashes. Rejected: check-only linting (measures divergence without
removing it), and git submodules (clone/CI friction, and no way to take four rules out of twelve).

**D3 — No symlinks or junctions, ever.** A sibling's `verify` was broken by an absolute npm-written junction
that survived a folder rename and failed as an unrelated-looking module-resolution error. A doctrine system
held together by junctions would reproduce that across every repo.

**D4 — Three layers: core (automatic) · packs (opt-in) · local (untouched).** Core is the small universal set
every repo gets without asking. Packs are stack-specific and named in the manifest. Local files are the
repo's own, never synced, never modified.

**D5 — Anything not in the lock is invisible to the tool.** Daoris only writes files it put there. This is
what makes a local rule safe to keep in the same folder as a canonical one.

**D6 — Lockfile is authoritative; a one-line header is for the reader.** The lock detects drift. The header
exists because an agent that opens a rule file needing a tweak will simply edit it — that is precisely how
the divergence happened — and one line naming the canonical source at the top is the cheapest intervention at
the only moment it matters.

**D7 — The tier is the directory, not metadata.** The harness auto-loads every `.claude/rules/*.md` into
session context and does not load `.claude/knowledge/`. Placement *is* the tier; a `tier:` field would be a
second source of truth for something the platform already decides. It also means the always-loaded footprint
is measurable, so "keep the core small" becomes a gate rather than an aspiration.

**D8 — `check` works offline.** It is pure local hashing against the lock, with no network and no registry
access, because it is meant to run inside repo build gates — including one in a .NET repo that has no node
dependencies at all today. Only `init`, `sync` and `update` reach the source.

**D9 — `upstream` is half the product.** A one-way push is distribution; 衍 is propagation *and* return. The
command that promotes a locally-improved file back into the canon is what keeps the canon from ossifying, and
it ships in v0.1, not later.

**D10 — Distributed as an npm package, consumed via `npx` from git first.** No registry publish while the
surface churns; the manifest pins a tag or commit so no repo is silently upgraded. Publishing later does not
change the invocation. Rejected: a global machine install (nothing in a repo records which version produced
its files) and a per-repo vendored shim (the drift checker would itself be drifting content).

## 4. Repo-side model

```
<repo>/
  daoris.json          tracked, hand-edited
  daoris.lock          tracked, generated
  .claude/
    rules/             core + packs' rules      ← vendored: header + locked
    knowledge/         packs' deep dives        ← vendored: header + locked
    rules/RULES_INDEX.md                        ← generated
    rules|knowledge/<own>.md                    ← local: not in the lock, never touched
```

`daoris.json`:

```json
{
  "source": "github:OWNER/daoris#v0.1.0",
  "packs": ["dotnet-library", "windows-machine"],
  "target": ".claude",
  "coreBudgetBytes": 24000
}
```

`source` is written by `daoris init`, so no repo hand-writes it and no absolute path can end up in a
tracked file. The pinned ref is what stops a silent upgrade.

**How the canon is obtained — resolved during implementation:** it isn't. The canon **ships inside the
package**, so the pinned ref in `source` selects the canon by selecting which package version `npx` runs.
`source` is therefore a record of provenance and the command to re-run, not something the tool fetches.
That removes clone/cache machinery entirely and makes D8's offline guarantee structural rather than a
rule to remember. `DAORIS_CANON` overrides the canon root for developing Daoris itself.

**Adoption collisions — added during implementation.** D5 says anything not in the lock is invisible, but
`sync` still had to *write* to those paths on a first sync, which silently overwrote a rule the repo had
written itself. The two cases are now separated by provenance: **in the lock** means Daoris owns the file
and the repo edited it (drift); **not in the lock** means the repo wrote it before adopting Daoris
(collision). Both refuse without `--force`, with different advice, because they are different mistakes.

`daoris.lock` records, per materialized file: pack, canonical path, target path, canon version, and a sha256
of the written content. Retiring a canonical file removes it from every repo on the next `sync` — the thing
copy-paste can never do.

## 5. Canon-side model

```
canon/
  core/                       every repo, no opt-in
    sensitive-info.md
    task-lifecycle.md
    no-tmp-for-repo-files.md
    file-tool-discipline.md
    persist-working-state.md
  packs/
    dotnet-library/           rules/ + knowledge/
    desktop-winforms/         knowledge/
    web-webview/              knowledge/
    windows-machine/          rules/
  packs/<name>/pack.json      name, description, and each file's target directory
```

Seeding is a judgment pass, not a copy. Where two siblings both have a rule and disagree, the canonical
version is written fresh from the better of the two; whatever was genuinely repo-specific stays local. Each
such call is made deliberately and recorded, not resolved by whichever file was read first.

## 6. CLI surface (v0.1)

| Command | Behaviour |
|---|---|
| `daoris init` | Detect what the repo already has, propose packs, write `daoris.json` |
| `daoris sync` | Materialize packs into the target, write the lock. Refuses to clobber a drifted file without `--force`; `--dry-run` prints the plan |
| `daoris check` | Drift, staleness, index freshness, core budget. Non-zero exit; offline; wired into each repo's own verify gate |
| `daoris upstream <file>` | Copy a locally-improved vendored file back into the canon checkout for review and commit there |
| `daoris index` | Regenerate `RULES_INDEX.md` from what is on disk — canonical and local alike |
| `daoris status` | Human summary: packs, versions, drift, what is local |

## 7. Doc format and the index

Frontmatter carries exactly what the index table needs, and nothing else:

```yaml
---
name: sensitive-info
applies_when: writing tracked files, commit messages, or rewriting history
enforces: no machine paths, no private sibling names, no tokens; a leak is a history problem
---
```

`daoris index` regenerates the table from disk, marking unsynced files `(local)`. A file without frontmatter
is listed with a `⚠ needs frontmatter` marker rather than silently skipped — the failure mode to avoid is an
index that looks complete while omitting a rule.

`daoris check` additionally reports the byte footprint of the always-loaded `.claude/rules/` directory and
fails above `coreBudgetBytes`.

## 8. Error handling

- **Never destructive.** `sync` refuses to overwrite a drifted file without `--force`; `--dry-run` prints the
  plan; the tool touches only paths present in the lock.
- **Atomic writes**, BOM-less UTF-8, LF endings, written by node directly — never through a shell, because a
  GBK console mangles CJK and em-dashes on the way through. No `fs.cpSync` (a documented crash on the Node
  version in use); explicit read/write instead.
- **Exit codes:** `0` clean · `1` policy failure (drift, stale, over budget, index out of date) · `2` tool
  error. Only `1` should appear in normal use.

## 9. Testing

`node:test`, no framework. Unit tests for hashing, lock diffing, and the sync decision table. Integration
tests drive fixture repos materialized under a gitignored scratch directory in this repo — never OS temp.

The cases that must hold: fresh sync writes the expected tree and lock; a local edit is detected and named; a
retired canonical file disappears from the repo; a local file is never touched; `upstream` round-trips content
into the canon; `index` output is deterministic; an over-budget core fails `check`.

## 10. Bootstrap and adoption

1. `git init`, `CLAUDE.md`, `package.json` with the `bin`, and the first six commands with tests.
2. **Daoris carries its own `daoris.json`** and syncs core into its own `.claude/`. A tool that cannot hold
   its own doctrine cannot hold anyone else's.
3. Seed the canon from both parent repos, file by file.
4. First adopter: **Lyntai** — it has a real verify gate to wire `check` into, and its doctrine is the most
   mature. Then the desktop devkit sibling, then the rest.

Housekeeping, before any of the above: this repo still carries the seeding sibling's `.claude/knowledge/`
(WinForms, WebView2, IPC, extraction sources), its private `local/` material, and built devtools binaries.
Each file is to be confirmed present in its home repo **before** anything is deleted here — one of the
carried files is described as a pre-wipe history backup and may be the only copy.

## 11. Out of scope for v0.1

> **Amended 2026-08-04.** Nothing was ever published, so the version boundaries below buy nothing:
> development is `0.0.x` and the first release is `0.1.0`. **Skills move into that first release** — see
> `ROADMAP.md`. The rest of this section stands, with its "v0.2" naming shifted one place down.

- **Skills.** They carry frontmatter the harness interprets and often need per-repo parameterization (a build
  command, a package layout). That is a design problem, not a copy — deferred past the initial build,
  now in scope for the first release.
- **Owning regions of `CLAUDE.md`.** It is the most repo-specific file in every project, and partial ownership
  of a hand-written file is where sync tools start fighting their users.
- **The centralized knowledge service (cross-repo RAG).** A separate sub-project with its own spec; it would
  build on Lyntai's semantic memory, embedder seam, vector store and MCP hosting rather than reinventing them.
- **The harness layer — gates and devtools.** The phase after the first release, because the
  observation behind it is the same one that motivates v0.1: every repo carries a hand-copied
  `devtools` script, and those copies have diverged further than the documents have. The shape follows
  this design one level down — gates get **declared, not copied**. The manifest grows a `verify` block;
  Daoris ships the gates that are genuinely universal (sensitive scan, doctrine drift,
  version-authorship, docs freshness) and each repo declares its own stack gates as commands. Same
  core / packs / local layering, applied to gates instead of documents.
- **Any .NET package — with one reservation.** The original framework note sketched fourteen and v0.1
  needs none. The CLI stays node: what devtools actually do is orchestrate subprocesses, and a compiled
  binary that spawns `dotnet build` buys nothing while costing per-platform artifacts and a release
  pipeline — self-defeating for a tool whose purpose is reducing per-repo overhead. **.NET earns its
  place only where the compiler is required**: symbol and dependency graphs, real API-surface diffing,
  AST-aware transforms — the framework note's *Repository Intelligence* pillar, which is .NET-shaped
  because Roslyn is. When that lands it is a capability the CLI invokes, not a rewrite of the CLI.
