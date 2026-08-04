# Decisions

Numbered, dated, with the reasoning. A decision recorded here is not re-litigated without a reason to
reopen it — and a decision that was *considered and rejected* is recorded too, because without the reason
someone reverses it later and rediscovers the problem.

---

## D1 — Daoris is process tooling, not an LLM library (2026-08-04)

**Decision.** No model calls, no dependency on the LLM cognition library, in v0.1.

**Why.** The original framework note sketched fourteen .NET packages; six of them — prompts, RAG, memory,
evals, harness, tool contracts — already exist, shipped and frozen under semantic versioning, in the
sibling library. Building them again would have produced a second, worse copy of something that already
works, and would have made Daoris depend on a release cadence it does not control.

**Consequence.** The genuinely new pillars are doctrine, repository intelligence, context assembly,
validation, and reflection. A future knowledge service *will* build on that library (see D11 and the
roadmap) — but as a separate deployable, not as a dependency of the CLI.

## D2 — Manifest + vendored copy + drift check (2026-08-04)

**Decision.** A repository declares what it wants; the tool materializes real `.md` files and records
content hashes in a lockfile.

**Why.** The agent harness loads documents from disk, so the doctrine has to *be* files — a runtime import
was never an option. Given files, the only question is whether divergence is detectable. Hashes make it a
build failure instead of a slow leak.

**Rejected:** check-only linting (measures divergence without removing it — six copies of the same rule
stay six copies) and git submodules (clone and CI friction, and no way to take four rules out of twelve).

## D3 — No symlinks or junctions, ever (2026-08-04)

**Decision.** Materialization is always a real file copy.

**Why.** A sibling's build was broken by an absolute junction that survived a directory rename and then
failed as an unrelated-looking module-resolution error. A doctrine system held together by junctions
would reproduce that across every repository, and the failure would not look like a doctrine problem.

## D4 — Three layers: core, packs, local (2026-08-04)

**Decision.** Core installs everywhere with no opt-out; packs are named in the manifest; the repository's
own documents are neither synced nor touched.

**Why.** The repositories genuinely differ — a published library, a desktop devkit, a web application —
so a single flat set would either be too small to be useful or too large to load. Making core
non-optional matters because the rules most worth having everywhere are exactly the ones a new repository
would forget to opt into.

## D5 — Anything not in the lock is invisible to the tool (2026-08-04)

**Decision.** Daoris only reads and writes paths recorded in `daoris.lock`.

**Why.** This is the single invariant that makes a repository's own documents safe to keep in the same
directory as canonical ones. Without it, "local" would be a convention; with it, it is a property.

## D6 — The lock is authoritative; the header is for the reader (2026-08-04)

**Decision.** Drift is detected by content hash. Every materialized file also opens with a one-line
provenance header.

**Why.** An agent that opens a rule needing a small fix will simply edit it — that is precisely how the
copies diverged in the first place. One line at the top, naming the source and pointing at `upstream`, is
the cheapest possible intervention at the only moment it matters. The hash catches the edit regardless,
so the header is guidance rather than enforcement.

## D7 — The tier is the directory, not metadata (2026-08-04)

**Decision.** `rules/` is always-loaded, `knowledge/` is on-demand, and there is no `tier` field anywhere.

**Why.** The harness already decides this by path — it auto-loads one directory and not the other. A
metadata field would be a second source of truth for something the platform has already settled, and the
only thing it could ever do is disagree.

**Consequence.** The always-loaded footprint is directly measurable, so "keep the core small" became a
gate (`check` fails over a byte budget) rather than an aspiration. It caught a real 45% overage on the
first adoption.

## D8 — `check` works offline (2026-08-04)

**Decision.** `check` is pure local hashing: no network, no canon access, no package resolution.

**Why.** It is meant to run inside build gates — including in a .NET repository that has no Node
dependencies at all and may be building offline. A gate that can fail because a network call failed is
not a gate.

**Consequence.** Enforced by a test that deletes the canon entirely and requires a clean exit.

## D9 — `upstream` ships in v0.1 (2026-08-04)

**Decision.** Promoting a locally-improved file back into the canon is in the first release, not a
follow-up.

**Why.** A one-way push is distribution. 衍 is propagation *and* return, and the return direction is what
keeps the canon from ossifying: without it, the correct response to a rule that is subtly wrong is to
edit it locally, which is the behaviour the whole tool exists to prevent.

## D10 — Distributed as an npm package, consumed via `npx` (2026-08-04)

**Decision.** No per-repository dependency and no global install; the manifest pins a reference.

**Why.** It must work in repositories that have no `package.json` at all — the .NET library sibling runs
bare `node` scripts and has none. `npx` needs nothing installed. A global install was rejected because
nothing in a repository would then record which version produced its files; a vendored shim was rejected
because the drift checker would itself be drifting content.

## D11 — The canon ships inside the package (2026-08-04)

**Decision.** `canonRoot` defaults to `<package>/canon`. The manifest's `source` is a record of
provenance and the command to re-run — not something the tool fetches. `DAORIS_CANON` overrides it for
developing Daoris itself.

**Why.** The design left "how does `sync` obtain the canon" unanswered, and the obvious answers all meant
cloning and caching. Shipping the canon *in* the package makes the pinned reference itself the version
pin, which removes that machinery entirely — and makes D8's offline guarantee structural rather than a
rule someone has to remember.

## D12 — Adoption collisions are distinct from drift (2026-08-04)

**Decision.** A file in the lock whose content changed is **drift**; a file *not* in the lock sitting at a
canonical path is a **collision**. Both refuse without `--force`, with different messages.

**Why.** Found during implementation, before release. Drift detection only guarded files already in the
lock, so a repository's *first* sync silently overwrote a rule it had written itself — with no error and
no warning. Two of the repositories due to adopt already have exactly such a file. The two cases look
identical to a hash check and are completely different mistakes: one is "you edited my file", the other
is "I am about to destroy yours".

## D13 — Drift is measured against the lock, not against the current canon (2026-08-04)

**Decision.** For a file already in the lock, `sync` compares what is on disk to the hash the lock
recorded — what Daoris last *wrote*. A difference between the file and what the canon says *now* is an
update, not drift.

**Why.** The original check compared on-disk content to the newly-rendered canonical content, which made
the two indistinguishable. The consequence was the worst one available to this tool: **an improved
canonical rule could not propagate.** Every consuming repository's `sync` would exit 1 accusing it of a
local edit and advising `daoris upstream`, for an edit nobody made — and the only way through was
`--force`, documented as "discard your local edit." One-way push at least distributes; this distributed
nothing.

Found by bumping the version to `0.0.1`, which changes only the provenance header: all six of Daoris's
own rules were reported `DRIFTED` while byte-identical to what the previous canon wrote. Untested because
every drift test edited the repository's copy first — the clean-repo case, which is the common one, had
no coverage at all.

**Consequence.** The three states are now distinguished by what they are compared against: on-disk versus
**lock** answers "did this repository change it", on-disk versus **canon** answers "is there something new
to install", and absence from the lock answers "is this file even ours" (D5, D12). Two tests hold it: a
canon improvement reaching an untouched repository, and a version bump alone not reading as drift.

## D14 — Canonical skills are parameter-free and delegate to the generated index (2026-08-04)

**Decision.** A canonical skill contains only the procedure that is invariant across repositories. It
names no path, no build command, and no roster of other skills. Where it needs repository specifics it
sends the agent to the **generated index**, which `sync` already writes from that repository's own disk.
There is no substitution map in the manifest, and no template placeholders in canon files.

**Why.** Surveyed twelve repositories carrying doctrine — 134 skills, including one deliberately outside
the family's stack (a daily-work Angular/React application). Three skills appear in six repositories
each: `doc-loader`, `pattern-finder`, `skill-loader`. That is the strongest frequency signal observed,
and their copies have diverged the furthest: `pattern-finder` runs from 1,826 to 12,041 bytes, a 6.6×
spread.

Reading the extremes settled the question. All copies of `doc-loader` share the *same ~15-line
procedure*; the entire 5× spread is the repository's own routing content — one names its report features
and twenty knowledge files, another names its job lanes and migrations. **A substitution map could have
supplied one path and would have left the other six kilobytes exactly where they already are.** It solves
the cheap tenth of the problem and adds a second source of truth that can silently disagree with the
files — the same failure that got a `tier` field rejected in D7.

The delegation half is not a design so much as an observation: all six copies already do it, in nearly
the same words — *open the index, scan the "Applies when" column, read every matched document.* One
states outright that its own shortcut table is not authoritative and the generated index is. Six
independent authors converged on it, and Daoris already generates that index.

**Consequence.** `skill-loader` is not canon content at all — its body is "which skills does this
repository have", which is a **generated index**, exactly like `RULES_INDEX.md`. The hardest
parameterization case disappears rather than being parameterized. It also has to be generated, because
the roster is not fixed: one repository's workflow rule names four discovery skills where another names
three, so a canonical rule that hard-coded the roster would be wrong on arrival.

**Rejected:** a manifest substitution map (solves a tenth of the spread, adds a second source of truth);
per-repository skill templates (the divergence *is* the content, so templating it canonizes nothing).

**Confirmed by the platform, after the fact.** The agent harness supplies a variable resolving to a
skill's own directory, which is exactly the one parameter that could not be avoided — a skill invoking a
script it ships with. The platform had already solved it without a substitution map, so a manifest field
would have been a second, worse mechanism for the only case that needed one.

## D15 — The platform overlaps the format, not the problem (2026-08-04)

**Decision.** Daoris stays as scoped. The agent platform's own features are adopted where they are the
better mechanism (the skill format and its open standard, the skill-directory variable), and nothing in
the roadmap is withdrawn on account of them.

**Why.** Checked before building further, because building a distribution layer the platform is about to
ship would be waste. It is not shipping one.

- **Workspaces** are API-key, billing, rate-limit and access segmentation. They are not a knowledge
  feature at all, and the name is the only thing they share with this problem.
- **Skills** are a *format* — a directory with a `SKILL.md`, now an open standard shared across tools.
  A format is a container, not a distribution mechanism: it says how to write a procedure down, and
  nothing about how the same procedure stays consistent across a dozen repositories.
- **Per-project assistant memory** is machine-local and untracked. It is convenient and it fails every
  clause of `no-global-memory`: a teammate never sees it, review never touches it, moving the project
  loses it. That it is now automatic makes the rule more necessary, not less.
- **Plugins and marketplaces** are the genuine adjacency — a real distribution channel for skills. Worth
  revisiting as an *output* (emit a plugin from the canon), never as a replacement: distribution is the
  half of 衍 that was already easy.

**What remains unaddressed by any of it:** detecting that copies have diverged; removing a retired rule
from every repository at once; a return path that promotes an improvement back; a measured budget on the
always-loaded tier; distinguishing "you edited my file" from "I am about to destroy yours"; and covering
the always-loaded and on-demand tiers rather than skills alone. That list is the whole thesis, and none
of it is a platform feature.

**Consequence.** Because canonical skills are parameter-free (D14), they conform to the open standard and
carry beyond one vendor — a portability dividend from a decision made on entirely different grounds.

## D16 — Generated wikis are the complement, not the competitor (2026-08-05)

**Decision.** Daoris stays on *authored doctrine* and does not grow a documentation generator. Where a
generated wiki exists it is treated as an input the canon points at, never as something the canon owns.

**Why.** The "LLM wiki" pattern — an agent that reads a corpus and maintains a structured, interlinked
wiki that compounds instead of being re-derived per query — now has several codebase implementations.
It looks adjacent enough to be worth stating why it is not the same problem.

| | Generated wiki | Daoris |
|---|---|---|
| Where it comes from | **Derived** from a source of truth | **Authored**, because something went wrong once |
| Can it be regenerated? | Yes — cheap, therefore disposable | No. The incident is not in the code |
| Failure mode | Staleness | Divergence across copies |
| Question answered | "What *is* this codebase?" | "How do we work, and why?" |

No generator produces `sensitive-info` or `no-tmp-for-repo-files`: a codebase does not contain the leak
or the mangled encoding that motivated them. The converse holds just as firmly — hand-maintaining an
architecture map that a generator can rebuild from the source is how documentation rots.

**They compose, and in a specific place.** The `doc-loader` skill routes a task first to *the
repository's own documentation router* and then to the generated rules index. The first of those is
precisely what a wiki generator produces and keeps fresh; the second is what `sync` writes. One skill,
fed from both sides, neither of which the other could supply.

**And the dependency runs one way.** A wiki generated over six divergent copies of the same rule
faithfully documents the divergence. Canonizing first is what makes the generated layer worth having,
which is the same ordering the roadmap already applies to the knowledge service.

**Consequence.** Before building the long-term repository-intelligence work, check it against these tools
the way the platform was checked in D15 — parts of that pillar may already exist, and building a second
worse copy is the failure D1 was written to prevent.

## D19 — The sync state space is enumerated, not discovered (2026-08-05)

**Decision.** What `sync` does with a file is a function of three inputs — is it in the **lock**, what is
on **disk**, and what the **canon** now says — and all of it is written down here. New behaviour is
checked against this table before it is implemented.

**Why.** This one area was corrected four times: drift compared against the canon instead of the lock
(D13), then failed after `upstream`, then failed after `upstream` plus a version bump, then silently
destroyed a locally-improved rule that was retired upstream. Every fix was correct and every one was
found by a symptom. Four corrections in one area is not bad luck; it is an unenumerated state space, and
the remedy is a table rather than a fifth patch.

| In canon | In lock | On disk | Disk vs lock | Disk body vs canon | Outcome |
|---|---|---|---|---|---|
| yes | yes | yes | same | same | unchanged |
| yes | yes | yes | same | differs | **update** — improved upstream, untouched here (D13) |
| yes | yes | yes | differs | same | **update** — already promoted; only the lock is stale |
| yes | yes | yes | differs | differs | **drift** — refuse; promote or `--force` |
| yes | yes | no | — | — | recreate; `check` reports it missing |
| yes | no | yes | — | same content | adopt silently — byte-identical, nothing to warn about |
| yes | no | yes | — | differs | **collision** — the repo wrote this first; refuse (D12) |
| yes | no | no | — | — | create |
| no | yes | yes | same | — | **retire** — delete it |
| no | yes | yes | differs | — | **edited retirement** — refuse; `upstream` cannot save it |
| no | yes | no | — | — | drop the lock entry; nothing to delete |
| no | no | — | — | — | invisible to the tool (D5) |

Two rows carry the whole safety argument. *Disk differs from lock* means *this repository changed it* and
is the only thing that ever counts as drift. *Absent from the lock* means *daoris never wrote it*, which
is what makes a repository's own files safe to keep in the same directory.

**Consequence.** The last row of the table was the fourth bug: retirement is the most destructive thing
`sync` does and had the weakest guard, because a retained file that drifted refused while a retired one
was deleted without a word — and at the worst possible moment, since the canonical file the edit belonged
to is gone, so `upstream` is no longer a route. It now refuses, and advises keeping the edit as a local
document, which is what the three-layer model was for.

## D18 — Every path daoris touches must resolve inside the target directory (2026-08-05)

**Decision.** `sync` resolves every write and delete against the target directory and **refuses** any
path that escapes it, before touching anything. Refuses rather than sanitises.

**Why.** D5 established that anything absent from the lock is invisible to the tool. Its complement was
assumed and never enforced: everything *present* in the lock was trusted as a relative path under the
target. A lock entry containing `..` therefore reached arbitrary files — verified before the fix by
deleting a file at the repository root and another in the parent directory, from a `sync` whose only
output was a retirement count.

The lock is **generated**, which is what makes this worse than it first sounds. Nobody reads a generated
file closely in review, so a merge-mangled entry and a deliberately crafted one in a pull request arrive
at the same `rmSync`, and retirement reports a number rather than a path.

Sanitising was rejected: a path that tried to leave the target is not a path to quietly correct, it is
evidence the lock is corrupt or hostile, and continuing would discard that evidence. Paths are also all
resolved *before* the first write, so a bad entry aborts the whole apply instead of half-applying it.

**Consequence.** Found by asking whether the tool was ready for production rather than by a test —
which is the reason to ask that question deliberately rather than infer it from a passing suite.

## D17 — The twin threshold is set by measurement, and its blind spot is documented (2026-08-05)

**Decision.** `doctor`'s containment threshold drops from 0.5 to 0.3, the skills tier is scanned, and the
class of duplicate it *cannot* find is stated in the tool's own description rather than left for someone
to discover.

**Why.** The 0.5 threshold was a guess, and checking it against sixteen real pairs across the family
showed it was set above the band that matters. Near-verbatim copies score 72-74% and were always caught.
Twins that were **rewritten** rather than copied land at 34-43% — and those are the ones worth finding,
because nobody recognises them by eye either. Unrelated documents sit at 7-16%. At 0.5: 2 caught, 9
missed. At 0.3: **7 caught, 4 missed, no false positives.**

The threshold is deliberately asymmetric. `doctor` is advisory and always exits 0 (D12 reasoning), so a
false positive costs one dismissed line while a miss costs duplication that persists indefinitely.

**It bought a false positive immediately, on this repository.** Running `doctor` here now reports
`canon-authoring` as looking like `persist-working-state` at 33%. They are not the same rule — but the
suspicion is defensible rather than nonsense, since both are substantially about writing durable records
and both name the decisions log. That is what 0.3 buys: a reader spends a moment dismissing a plausible
suggestion. It is the trade that was chosen, and it belongs in the record rather than being tuned away
after the fact — a threshold justified by a sample and then quietly raised at the first inconvenience
would be neither measured nor honest.

**The blind spot is structural, not a tuning problem.** All four remaining misses are one class:
documents that reach the same principle through an entirely *different vocabulary*. The clearest case
found in the survey merges two canonical rules but discusses allow-listing, tooling directories and
screen captures where the canonical pair discusses tools, temporary directories and shells — 24% and 23%,
inside the unrelated band. A real twin at 15% cannot be separated from an unrelated pair at 15% by any
threshold, so lowering it further buys noise rather than recall.

**Consequence.** Word overlap detects *restatement*, not *convergence*. Adoption still requires the
manual twin hunt the adoption document already prescribes; `doctor` narrows that job and does not replace
it. Claiming otherwise would be worse than the gap, because a detector believed to be complete stops
anyone looking.
