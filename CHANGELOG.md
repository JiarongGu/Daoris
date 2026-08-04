# Changelog

All notable changes to Daoris are recorded here. Entries are written under `## Unreleased` and stamped
with the version and date at release.

## Unreleased — first release

The first version: doctrine that installs, is checked, and flows back.

### The tool

- **Seven commands.** `init` writes a manifest and reports what is available without guessing;
  `sync` materializes the selected packs and writes the lock; `check` gates on drift, staleness, index
  freshness and the always-loaded budget; `upstream` promotes a locally-improved file back into the
  canon (`--all` for every edit at once); `index` regenerates `RULES_INDEX.md` from what is on disk;
  `status` summarizes and reports when a newer canon is available; `doctor` reports local documents that
  restate a canonical one under a different name.
- **`doctor` covers the one gap the lock cannot.** A repository's own rule duplicating a canonical one is
  local, and local is invisible by design — it surfaced on the first adoption only because someone read
  the generated index end to end. Advisory by construction: word overlap is crude, and a false positive
  that failed a build would be worse than the duplication. Validated against the real case, where it
  independently finds a 58% overlap that previously took a manual read to notice.
- **Zero runtime dependencies.** Node ≥ 22, ESM, `node:test`. Nothing to install — every command runs
  through `npx` against a pinned reference.
- **The canon ships inside the package**, so the pinned reference *is* the version pin. No command
  fetches anything, which is what makes `check` offline by construction rather than by discipline —
  asserted by a test that deletes the canon and requires a clean exit.
- **Three layers.** Core installs everywhere with no opt-out; packs are named in the manifest; the
  repository's own documents are never synced and never touched. Anything absent from the lock is
  invisible to the tool.
- **Two refusals, distinguished by provenance.** A file in the lock that changed on disk is *drift* — the
  repository edited something Daoris owns. A file *not* in the lock at a canonical path is a *collision* —
  the repository wrote it before adopting Daoris. Both stop the sync; they carry different advice,
  because they are different mistakes.
- **Drift is measured against the lock, not against the current canon.** Comparing on-disk content to
  freshly-rendered canonical content made "the repository edited this" and "the canon improved" the same
  observation, so an improved rule could not propagate: every consumer's `sync` exited 1 over an edit
  nobody made. Now the lock's recorded hash answers "did this repository change it" and the canon answers
  "is there something new to install." See `docs/DECISIONS.md` D13.
- **Retirement.** A file removed from the canon is removed from every repository on the next sync — the
  one thing copy-paste can never do.
- **A one-line provenance header** on every materialized file, because an agent that opens a rule needing
  a tweak will otherwise simply edit it. The lock's hash catches the edit either way.
- Atomic, BOM-less, LF writes throughout; exit codes are the contract (`0` clean, `1` policy failure,
  `2` tool error).

### The canon

- **Six core rules**, each confirmed by appearing independently in multiple repositories in the family:
  `sensitive-info`, `task-lifecycle`, `no-tmp-for-repo-files`, `file-tool-discipline`,
  `persist-working-state`, `no-global-memory`.
- **Three packs.** `windows-machine` (traps that succeed wrongly rather than failing),
  `dotnet-library` (package boundaries, naming, DI variation points, shipping registries, and API design),
  `storage-sql` (type affinity on read, migration numbering, full-text search for scripts without word
  boundaries).
- Every canon file carries frontmatter that generates its index row; tests assert that, plus that no canon
  file contains a machine path.

### Proven

- Daoris carries its own manifest and syncs core into its own `.claude/`; a test asserts it stays clean.
- Adopted into **Lyntai** (a released .NET library): 4 collisions surfaced and resolved deliberately, a
  renamed twin found, 3 packs installed, its own 1337 tests still green — and the budget gate immediately
  caught a real 45% overage on first contact.
