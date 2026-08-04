---
name: canon-authoring
applies_when: writing or changing a canon file, or adding a pack
enforces: project-agnostic content, frontmatter that matches the filename, principle-and-reason not mechanism
---

# Authoring canon — writing doctrine for repositories you have never seen

A canon file installs into other people's repositories. That single fact drives every rule below.

## Why

The failure mode is subtle: a rule written while looking at one repository reads perfectly *in that
repository* and becomes noise everywhere else. It names a build command that does not exist, a directory
that is laid out differently, a product concept the reader has never heard of. The adopting repository
either edits it — which is the drift the tool exists to prevent — or ignores it, which is worse, because
an ignored rule still costs context on every single session.

## How to apply

### Content

- **State the principle and the reason. Leave the mechanism to the adopter.** "Never hand-edit the
  version; the release workflow bumps from whatever the file says" is canonical. "Run `dev.mjs doctor`"
  is not — that belongs in the adopting repository's own local document.
- **No product names, no build commands, no repository-specific layouts.** Say "the always-loaded rules
  directory", not a specific path; say "a scan run by the pre-commit hook", not a script name.
- **Lead with the failure that motivated it.** Every rule worth canonizing exists because something went
  wrong; the `## Why` section is what lets a reader judge an edge case instead of following blindly. A
  rule with no reason gets deleted by the first person who finds it inconvenient.
- **Prefer the trap that succeeds wrongly.** Rules that prevent a loud failure are worth little — the
  failure teaches the same lesson. Rules that prevent a *silent* wrong result are worth a great deal.

### Frontmatter

Three fields, all required, all used to generate the index row:

```yaml
---
name: <must match the filename without .md>
applies_when: <when a reader should stop and read this>
enforces: <the one-line invariant>
---
```

A file missing any of them is marked `⚠ needs frontmatter` in the index rather than dropped — visible, not
silent. Tests assert `name` matches the filename, so a rename that misses the frontmatter fails the build.

### Placement

- **`rules/` is always-loaded** — every session in every adopting repository pays for it. Put a document
  here only if nearly every task needs it. The core budget is measured and gated for this reason.
- **`knowledge/` is read on demand** — the right home for anything long, or anything that only matters
  when touching one area.
- There is no `tier` field; the directory *is* the tier (`docs/DECISIONS.md` D7).

### Adding a pack

1. `canon/packs/<name>/pack.json` with `name` and a `description` — the description is what `daoris init`
   prints to someone choosing packs, so write it for that moment.
2. Files under `rules/` and `knowledge/` inside the pack; the subdirectory is the target tier.
3. **Write a pack when a repository is ready to adopt it**, and validate it by that adoption. A pack
   nobody installs is a draft that looks like doctrine.
4. `npm run verify` — tests assert frontmatter, filename match, pack description, and the absence of
   machine paths.

### Changing an existing canon file

Consumers hold a hash. Any edit shows up in their next `sync` as an update, which is intended — but a
consumer who had improved that file locally will see drift instead. That is also intended: it is the
conversation the tool exists to force. Prefer `daoris upstream` from the repository that found the
improvement over editing the canon directly, so the reasoning arrives with the change.
