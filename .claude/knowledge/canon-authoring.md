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
- **Where a mechanism must be named, name its tool-absent path in the same breath.** A rule that
  instructs an action only one tool can perform reads as a *dead end* to anyone working without it —
  and that is not a hypothetical reader. An adopted repository stays fully workable for contributors
  who do not run this tool, their agents included (`docs/decisions/D48.md`); those agents load the same
  vendored markdown and find an instruction they cannot carry out. So: *never write into another
  repository; publish a quest where a request system exists, and file the request with that
  repository's owner where none does.* **The principle is canonical; the mechanism degrades.**
  - **The test is a file or a service, not a vocabulary.** What `sync` writes is committed, so a
    generated index, a lock file and a vendored rule are all still *there* for someone who has never
    installed anything — naming them costs a non-user nothing. A **service** is the opposite: publish a
    quest, search the family, ask what a sibling accepts. Those are the dead ends, and they are the
    whole list. Applying this by hunting for family vocabulary instead finds the wrong rules: the
    audit that established it first "fixed" a rule naming the generated index, which needed nothing.
  - **The budget makes the cost real, and that is the point.** A carve-out costs always-loaded bytes in
    every repository forever, so one that does not fit is a *split* — the detail moves to the on-demand
    tier and the rule keeps the principle — never a raised limit (`docs/decisions/D28.md`). The audit
    that established this found its own bytes: the rule it corrected had been restating, in
    always-loaded text, what its own on-demand document already said better.
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

A file missing any of them is still listed rather than dropped — visible, not silent: marked `⚠ needs
frontmatter`, or, for a knowledge document with no frontmatter at all, listed by its first heading in a
table of its own (D128 §3.1), which is for an adopter's own documents and never a licence for the canon's.
Tests assert every canon file has all three and that `name` matches the filename, so a rename that misses
the frontmatter fails the build.

### Placement

- **`rules/` is always-loaded — and since D59 it does not land as a file.** What you write here is
  rendered into a region of the adopter's `AGENTS.md`, the one instruction file every harness this
  family drives actually reads; `.claude/rules/` reached exactly one of the three. Every session in
  every adopting repository pays for it, so put a document here only if nearly every task needs it. The
  core budget measures exactly this span and **reports** it rather than failing on it
  (`docs/decisions/D54.md`) — a fact gates, a judgement reports.
- **`knowledge/` is read on demand** — the right home for anything long, or anything that only matters
  when touching one area.
- **`skills/` is invoked by name** — a procedure, not a rule. Only its `description` is ever loaded
  unasked, so a skill's body is cheap and its description is not.
- There is no `tier` field; the **location** *is* the tier (`docs/decisions/D7.md`, as amended by D59) —
  a directory for the two on-demand tiers, a region inside a file for the always-loaded one.

### Writing a skill

A skill lives at `skills/<name>/SKILL.md` — the **directory** is the name, and every file is `SKILL.md`.

```yaml
---
name: <must match the DIRECTORY name>
description: <what it does, and when to use it — this is the trigger>
---
```

- **The frontmatter is the harness's, not ours.** It parses `description` to decide whether to surface
  the skill at all, so a skill without one installs, costs bytes, and silently never fires. Write the
  description for the moment of matching: what it does *and* when to reach for it.
- **The provenance header goes UNDER the closing `---`,** because frontmatter is only frontmatter when it
  starts at the first byte. `withHeader` does this; never hand-stamp a skill.
- **Canonical skills are parameter-free** (`docs/decisions/D14.md`). No paths, no build commands, no
  roster of other skills. Where a skill needs repository specifics, send the reader to the **generated
  index** — it is built from the adopter's own disk, so it is right in repositories the canon has never
  seen. Measured evidence: across twelve repositories the shared procedure was ~15 lines and the rest was
  each repository's own routing content, which no substitution could have supplied.
- **A roster is generated, never written.** "Which skills does this repository have" is the index's job.
  A hand-written version is wrong the moment anyone adds a skill.

### Adding a pack

1. `canon/packs/<name>/pack.json` with `name` and a `description` — the description is what `daoris init`
   prints to someone choosing packs, so write it for that moment.
2. Files under `rules/` and `knowledge/` inside the pack; the subdirectory is the target tier.
3. **Write a pack when a repository is ready to adopt it**, and validate it by that adoption. A pack
   nobody installs is a draft that looks like doctrine.
4. **A pack whose own document replaces a core one may offer to switch it off** — `switchesOff` in
   `pack.json`, the core row mapped to the reason (`docs/decisions/D71.md`). The replacement ships under
   its own name, never at the core target, and the row goes off only where a repository confirms it.
5. `npm run verify` — tests assert frontmatter, filename match, pack description, and the absence of
   machine paths.

### Changing an existing canon file

Consumers hold a hash. Any edit shows up in their next `sync` as an update, which is intended — but a
consumer who had improved that file locally will see drift instead. That is also intended: it is the
conversation the tool exists to force. Prefer `daoris upstream` from the repository that found the
improvement over editing the canon directly, so the reasoning arrives with the change.
