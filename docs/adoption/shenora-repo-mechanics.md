<!--
  PREPARED FOR AN ADOPTION THAT HAS NOT RUN YET — see TASKS.md, CANON3.

  This is a draft of the LOCAL `repo-mechanics.md` that the adopting repository should end up with:
  the material its `sensitive-info` and `windows-dev-gotchas` rules carried that the canon does NOT
  cover, and which retiring those two would otherwise lose.

  It lives here, tracked, rather than in the rehearsal fixture where it was written — a gitignored
  scratch directory is not a durable home for something a later task depends on.

  De-identified per `sensitive-info`: roles and mechanisms, no product names or machine paths.
-->

# Repo mechanics — this repository's concrete bindings for the canonical rules

**Local rule. Never synced.** The canonical rules state the principles; this file states how they are
enforced *here*. Where the two appear to disagree, the canonical rule states the intent and this file
states the mechanism.

## Sensitive info — the guard, and installing it

- The scan runs on staged content **and paths** at pre-commit, on the commit **message** at commit-msg,
  and over the whole tree in the "am I done?" gate.
- **It only protects a clone that installed the hooks.** A fresh clone has none, so the guard silently
  does nothing — this was hit live here, with two commits nearly landing unguarded. Install once per
  clone.
- Real tokens live in the gitignored pattern list; the tracked scanner carries only generic path shapes.
  Add new tokens there, never to a tracked file. The scan **fails closed** when that list is missing, so
  CI opts out deliberately rather than by accident.
- Repairing a leak that already landed is `.claude/knowledge/leak-repair.md`. The audit over all history
  is a one-off, not part of the routine gate.

## Windows specifics beyond the canonical rule

Canonical `windows-machine` carries the traps that are true of any Windows checkout. These are this
stack's own:

- **WebView2 ignores the additional-browser-arguments environment variable** once the host sets the
  equivalent option in code. A dev-mode host must re-append the variable's value itself, or the devtools
  debugging port is silently never opened. Proven in two sibling apps; keep the fix in the
  browser-arguments builder.
- **A WinForms handle created with OLE features enabled must be made on a dedicated STA thread.** Test
  runners are MTA, and the failure is not a clean test failure: handle creation throws inside the window
  procedure and a *blocking* unhandled-exception dialog stalls the entire suite.

## Desktop verification without a debugger

Background mouse messages can be posted to the render surface — no focus steal, works while occluded —
and the window can be captured even when hidden. The target process is named in the devtools config.

## Scratch and working files

Scratch, probes and dumps go under the gitignored `devtools/_*`; reusable tooling goes in `devtools/`,
tracked. Never OS temp, and never a sibling or backup folder outside this repository.
