# Daoris.Devkit — the shared developer toolkit, shipped as a binary

**Status: not started.** This document is the brief, written before the code so the shape is argued
rather than assumed.

## The problem, measured

Eleven repositories in this family carry a hand-copied `devtools/dev.mjs`. Surveyed 2026-08-05:

| Repositories carrying it | Smallest | Largest | Spread |
|---:|---:|---:|---:|
| 11 | 2.6 KB | 52.6 KB | **20×** |

Nine also carry a `project.config.mjs`, which is the part that was *meant* to differ. The other twenty
thousand lines are the same tool, re-derived and diverged — the exact pathology Daoris exists to fix,
one layer below the documents it already fixes.

## Why a binary rather than a copied script

This reverses the original design note, which argued the CLI should stay Node because "what devtools
actually do is orchestrate subprocesses, and a compiled binary that spawns a build buys nothing." That
reasoning weighed the *execution* cost and missed the *distribution* one:

- A copied script is copied, and copies diverge. That is the whole thesis.
- A `.NET` repository that carries a Node devtools script has a Node dependency it does not otherwise
  need — for tooling alone. A binary removes it.
- A binary has a version. A pasted script has whatever the paste contained.

The CLI stays Node and zero-dependency; this is a separate artefact with a separate job.

## Shape

- **.NET 10, native AOT**, one self-contained binary per platform. The toolchain is already proven in
  this family: the desktop sibling ships native C# tools built the same way.
- **Commands come from the repository, not the binary.** The devkit orchestrates; what to build, test
  and scan is declared per repository. That is the same core/packs/local split Daoris uses for
  documents, applied to gates — declared, not copied.
- **Distribution is Daoris's job.** The manifest names the devkit version; `daoris sync` fetches or
  verifies the binary the way it materializes documents today.

## Open questions, to settle before writing code

1. **How does a repository declare its gates?** A `verify` block in `daoris.json`, or a separate file
   the devkit owns. The manifest is already the declaration point for everything else.
2. **How is the binary distributed?** GitHub release assets are the obvious answer, but `check` is
   offline by construction (D8) and that property must not be lost — so verification and execution have
   to work without a network once the binary is present.
3. **What is genuinely universal?** The survey says: a sensitive-content scan, a doctrine-drift gate, a
   version-authorship check, and documentation freshness. Everything else in those eleven copies is
   stack-specific and belongs in the repository's own declaration.
