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

**`doctor` narrows this job; it does not replace it.** It compares vocabulary, so it finds a twin that
was *reworded* and misses one that was *rethought*. The clearest case in the survey — a rule present in
three repositories whose first half is canonical `no-tmp-for-repo-files` and whose second half is
canonical `file-tool-discipline` — scores **24% and 23%**, inside the unrelated band, because it reaches
both principles through an entirely different vocabulary (allow-lists, tooling directories, a `cd`
prefix). No threshold separates that from an unrelated pair, so lowering one buys noise (D17).

**A merged twin is not deleted.** Two harder shapes turn up, and the instinct to delete is wrong for
both:

- **One local rule that is two canonical rules combined.** Both halves are now canonical, so the file
  goes — but read it for the third thing it is carrying. That example also documents which allow-list
  entries exist and how a `cd` prefix defeats them, which is this repository's own mechanics and is
  nowhere in the canon. Move that to a local document *before* deleting, or adoption quietly costs the
  repository something it knew.
- **One local rule that is mostly canonical plus a genuine deep dive.** Retire the always-loaded rule and
  let the deep dive live in the on-demand tier, where it belongs — it was never something every task
  needed. This is the shape that pays: it removes always-loaded bytes without losing a sentence.

The test for anything you are about to delete is not "is this canonical now" but **"is every line of it
somewhere else."** Check each section against the canon *and* the repository's own knowledge tier, and
move what only exists here. A twin removed correctly costs nothing; one removed carelessly loses exactly
the hard-won specifics that were never going to be canonical.

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
