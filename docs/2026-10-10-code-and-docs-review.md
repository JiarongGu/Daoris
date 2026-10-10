# Code and documentation maintenance review — 2026-10-10

## Scope and plan

MAINT1: the owner's request for code review alongside documentation, refactoring, deduplication
and cleanup to improve future code-generation sessions. This pass reviews development checks and
generators: the inputs they share, Markdown parsing, invocation chains and contributor guidance.
It is not a claim to have reviewed every application component or historical paragraph.

Starting tree: clean at `77fa48de`. Discovery: doc-loader, pattern-finder and post-feature;
contracts: development-documents, claims-need-checks, twins, D122/D127/D151/D159 and the contributor
guide. Deliberate runtime twins remain independent; code sharing stays within repository tooling.
Historical records and blocked cleanup are preserved.

Completed plan: establish failing-first review cases; consolidate the inventory reader; reuse the existing
fence parser in document checks; update guidance at its existing home; regenerate indexes, run
reached tests and baseline verification, review the final diff and archive MAINT1.

## Findings

| Finding | Evidence before repair | Repair |
|---|---|---|
| Two inventory readers disagree | `orient-index.mjs:listFiles` checks repository root and regular files; `doc-system.mjs` independently lists Git files and accepts any existing path | One shared reader in fsx, preserve the generator's existing exported API and use it in both callers |
| Quoted tables become live entries | `routedPaths`, `backlogRows`, `routerRows` and generic duplicate checks parse fenced examples; archive/decision readers already mask fences | Reuse `doc-duplicates.mjs:fenced`, preserving its tested CommonMark-style rules |

The exemplar is `tools/fsx.mjs`: one named helper per operation, no runner on import, explicit
root and repository scratch for fixture tests. `fenced` already has parity fixtures and callers;
another Markdown implementation would add drift rather than remove it. Tiny role-path helpers
are not extracted merely to reduce a line count, and runtime twins are not merged into a library.

## Verification

Failing-first cases reproduce quoted examples becoming live duplicate, shape and router entries.
The new inventory contract also fails against an empty implementation: real files are required,
and a nested directory must be refused. Existing indented examples still contribute to a real
backlog row's reading cost.

Current practice stays in `docs/development.md`; non-obvious defect mechanisms go to the fix log.

The focused run initially failed six of 57 tests: four reproduce defects and two specify the new
shared inventory API. After implementation, all 62 focused tests pass, including the orientation
generator's existing tests. Both callers now use `fsx:repositoryFiles`; `orient-index:listFiles`
remains an exported alias. Sorting, ignored/untracked inputs, deleted files, replaced directories
and unchanged Git state are covered. Tracked files remain included even under an ignored directory.

The fence parser itself and runtime twins were not changed. Shape/duplicate/router callers now
apply its existing mask. Current contributor guidance identifies these reuse points and their
limits without duplicating the generated helper catalogue.

The first baseline attempt stopped at TypeScript's strict tuple indexing in a new test matrix.
The matrix now declares literal tuples; runtime assertions and production behavior are unchanged.

`npm run verify` passes: TypeScript; 1,484 CLI tests; doctrine; six document budgets; shape reports;
six duplicate-checked records and 158 decision files; the structural audit of 563 Markdown files;
60 tooling tests; release consistency; and all five universal devkit gates. The final staged
universal check also passes: links cover 563 tracked Markdown documents, including this review.
Orientation is fresh across 133 generated files; backlog and router entries are within their
60-word shapes. Existing archive overruns remain advisory; MAINT1's outcome is within its shape.

## Final review

- Wiring: both inventory consumers import the same helper, and `listFiles` remains available.
  Existing CLI generator and shared fence-fixture tests pass alongside the new regressions.
- Records: current practice updated in the contributor guide, defect logged, release-facing check
  behavior noted, router extended, indexes regenerated and original MAINT1 row moved to the archive.
- Reuse: helpers have one implementation within tooling; no change to the independent runtime twins,
  no new package dependency and no runner executes on import.
- Scope: this is a development-tooling and documentation maintenance review. Product UI, installed
  windows, live account flows and deployment were not exercised by this change. Their existing
  backlog/proof boundaries remain; blocked scratch cleanup stays open as DOCSCRATCH1.
