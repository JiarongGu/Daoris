# Canon changelog

**Why each version of the doctrine changed.** `daoris status` prints the entries between a repository's
locked version and the version shipping in the package, so a consumer sees not only *which* documents
moved but whether the change matters to them.

Write one entry per version, newest first, under a `## <version>` heading. Say what changed and what an
adopting repository should do about it — a line that only repeats the filename adds nothing that the
`changed` list did not already say. This file ships inside the package, so nothing here touches the
network.

## Unreleased

- **`claims-need-checks`** (new core knowledge) — verify behavioural prose against the implementation
  rather than the design; ship the check in the same change; say which claims the gate did not cover.
  **Two repositories in the family derived this independently and from opposite ends** — one auditing
  shipped API documentation against its own source, one finding a configuration field parsed and never
  read. Neither could have found the other by searching: at first draft the two shared **25% vocabulary**,
  well under the 30% duplicate threshold, which is D17's point demonstrated live. The canonical document
  merges both, and the merged version now matches the other at 49%.
  **Adopting repositories:** worth reading once, then grep your entry document and readme for *always,
  never, throws, cannot, defaults to, guaranteed, enforced, verified, ensures* and check each against the
  code. "Throws" is the costliest to get wrong, because it fails silently.
- **`leak-repair`** (new core knowledge) — how to repair a credential, machine path or private name that
  has already been committed. `sensitive-info` says a committed leak is a history problem and needs a
  rewrite; this is the deep dive that says how, and it is knowledge rather than a rule because it applies
  only when you actually have one. Assembled from **three** repositories' hard-won versions, including
  one written during a real purge. The traps that cost the most: **the backup bundle you take first is
  itself a complete copy of the leak**; the rewrite tool usually strips the remote, and tags need pushing
  separately; a clean scan deserves the same suspicion as a passing test, so plant a pattern you know is
  present and confirm it is found before trusting a clean result.
- **`windows-machine`** (pack rule, extended) — two more traps that pass silently. PowerShell 5 unwraps a
  nested array of exactly one element, so a find-and-replace built from an array-of-pairs holding a single
  pair replaces one *letter* everywhere; two or more pairs behave, which is what hides it. And a working
  directory past the path-length limit fails as *corrupt input* — tools report they could not open a file,
  which sends you looking at the asset instead of the path.
- **`model-decoupling`** (new core knowledge) — specify a feature without naming a model; the deployment
  chooses one, every model-backed feature still does its useful part with no model at all, and the
  output says which tier answered. Knowledge rather than a rule because it applies when building an
  AI-backed feature, not on every task.
- The first canon: seven workflow rules, and five skills — `doc-loader` and `pattern-finder` to start a
  task, `post-feature` and `fix-log` to close one, `caveman` for terse output. Nothing to upgrade from
  yet.
