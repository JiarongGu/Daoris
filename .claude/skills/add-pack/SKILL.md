---
name: add-pack
description: Use when adding a new pack to the Daoris canon (a stack-specific set of rules and knowledge, e.g. desktop-app, web-webview, durable-jobs). Covers the layout, the project-agnostic constraint, frontmatter, and the adoption gate.
---

# Add a pack to the canon

A pack is a named, opt-in set of rules and knowledge for one *shape* of repository. Core installs
everywhere; a pack installs where the manifest asks for it.

## Before writing anything: is it a pack?

- **Two or more repositories must already carry a version of this doctrine.** One repository's practice
  is a local document, not canon. Count first — the survey in `docs/task-archive.md` Part 2 is how the
  first three packs were chosen.
- **A repository must be ready to adopt it.** A pack nobody installs is unvalidated doctrine that looks
  authoritative. If no adopter is lined up, add it to `TASKS.md` under canon growth and wait.
- **Would it install cleanly into a repository you have never seen?** If it needs a build command or a
  directory layout to make sense, it is not canon — see `.claude/knowledge/canon-authoring.md`.

## Steps

1. **Create the pack directory.**

   ```
   canon/packs/<name>/pack.json
   canon/packs/<name>/rules/<file>.md        -> installs to rules/     (always loaded)
   canon/packs/<name>/knowledge/<file>.md    -> installs to knowledge/ (on demand)
   ```

   The subdirectory *is* the target tier. There is no `tier` field (`docs/DECISIONS.md` D7).

2. **Write `pack.json`.**

   ```json
   { "name": "<name>", "description": "<what it covers, in one line>" }
   ```

   The description is printed by `daoris init` to someone choosing packs — write it for that moment, not
   as a summary.

3. **Write the documents**, each with complete frontmatter whose `name` matches the filename:

   ```yaml
   ---
   name: <filename without .md>
   applies_when: <when a reader should stop and read this>
   enforces: <the one-line invariant>
   ---
   ```

   Lead each with the failure that motivated it. Prefer traps that *succeed wrongly* over ones that fail
   loudly — the loud ones teach themselves.

4. **Be ruthless about `rules/` versus `knowledge/`.** Anything in `rules/` is loaded in every session of
   every repository that takes the pack. Long, or narrow, means `knowledge/`.

5. **Verify.**

   ```sh
   npm run verify
   ```

   Tests assert frontmatter completeness, the filename match, a non-empty pack description, at least one
   file per pack, and the absence of machine paths.

6. **Adopt it somewhere.** Add the pack to a real repository's `daoris.json`, sync, and follow
   `.claude/knowledge/adoption.md` — expect collisions and renamed twins. The pack is not done until a
   repository is running it and its own tests still pass.

7. **Record it.** Move the task from `TASKS.md` to `docs/task-archive.md` with the outcome, and add a
   `CHANGELOG.md` entry under `## Unreleased`.

## Do not

- Copy a document from one repository into a pack unchanged — it will carry that repository's vocabulary.
  Generalize it, and keep the mechanism in the source repository as a local companion.
- Add a pack "for completeness". Membership is a budget, and every pack is a promise to maintain.
