---
name: adoption
applies_when: onboarding a repository onto daoris for the first time
enforces: resolve collisions deliberately; hunt renamed twins by hand; preserve repo mechanics locally; never let adoption silently rewrite doctrine
---

# Adopting a repository — the playbook, learned from the first one

Adopting is not `sync`. `sync` is the mechanical part; the work is deciding what happens to the doctrine
the repository already had.

## Why

The first adoption (a released .NET library) surfaced three things no synthetic test had: four collisions
on rules the repository already owned, a renamed twin the tool structurally cannot see, and a real 45%
budget overage. All three are normal. Expect them.

## How to apply

### 1. `init`, then read what it prints

`daoris init` lists the available packs *and* every document the repository already owns. That second
list is the adoption plan — each entry is something that will either collide, become a renamed twin, or
stay local.

### 2. `sync --dry-run` and read the collisions

A `COLLIDES` line means the repository wrote that file itself, before it ever heard of Daoris. Nothing is
overwritten. For each one, open both versions and separate:

- **The principle** — almost always already in the canonical version, often better generalized.
- **The mechanism** — the commands, paths, guards, and version policy specific to this repository. This
  is what would be *lost*, and it is usually the most load-bearing content in the file.

### 3. Preserve the mechanism in a local companion

Write the repository-specific mechanics into one local rule — `repo-mechanics.md` works well — that says
plainly: the canonical rules state the intent, this file states how it is enforced *here*. It is local, so
Daoris never touches it, and the index marks it `(local)`.

This is the whole point of the three-layer model. A repository that loses its release policy to a generic
rule has been made worse by adoption.

### 4. Hunt renamed twins by hand

The tool **cannot** find these. A repository's `minimise-bash-prompts` and canonical
`file-tool-discipline` are the same rule under different names; the twin is local, and local is invisible
by design. After syncing, read the generated index end to end and look for two rows saying the same
thing. Delete the twin, and check whether the repository's entry document referenced it by name.

### 5. `sync --force`, then `index`, then `check`

`--force` here means "yes, take the canonical version" — a deliberate answer to a question that was
asked, not a way to skip it.

### 6. Expect the budget to fail, and do not paper over it

The always-loaded core is measured for the first time at this moment, and it is usually larger than
anyone thought. Two honest responses:

- **Trim** — usually a long local rule that duplicates a canonical one, or a deep dive sitting in
  `rules/` that belongs in `knowledge/`.
- **Raise the budget to the true number** and record the overlap as a task.

Both are legitimate. What is not legitimate is trimming someone's doctrine as a side effect of adopting a
tool — that is editorial work and deserves its own review.

### 7. Verify the repository, not just the doctrine

Run the adopting repository's own build and tests. Adoption changes what every future session in that
repository reads, so "the tool exits 0" is not the same as "the repository is fine".

### 8. Leave it uncommitted for review

Adoption rewrites always-loaded context. The owner should see the diff before it becomes history.
