# Decisions

Numbered, dated, with the reasoning. A decision recorded here is not re-litigated without a reason to
reopen it — and a decision that was *considered and rejected* is recorded too, because without the reason
someone reverses it later and rediscovers the problem.

**Order.** A number is given when a decision is made, and the entry stays where it was written. Two
runs are therefore out of numeric order, deliberately: D17–D24, written in one session in the order
they were argued, and D54, filed beside D28 because it is the budget decision it amends. An amendment
lands in the entry it amends, dated, or — where it was written elsewhere — with a pointer from it.

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

**Amended 2026-09-24 (D71): a pack may switch a core row off, and the repository confirms it.** The
owner reopened this. A pack's `switchesOff` offers, with a reason, and the manifest's `switchedOff`
accepts, naming the pack. A repository alone still cannot drop a core row, which is the half of the
reason above that survives.

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

**Amended 2026-09-22 (D59): the tier is the LOCATION.** The always-loaded tier moved out of
`.claude/rules/` and into a region of `AGENTS.md`, because that directory is read by exactly one of the
three harnesses this family drives and `AGENTS.md` is the only file all three read. The better half
survives untouched: there is still no `tier:` field to disagree with, and the footprint is still
directly measurable, since a region has a byte count exactly as a directory did. The *gate* half was
separately amended by **D54** — the budget reports and never fails.

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

## D20 — Four artefacts in one workspace; the devkit ships as a binary (2026-08-05)

**Decision.** Daoris is a workspace of four artefacts under `src/Daoris.*`, matching the family's layout:
the **CLI** (npm, Node, zero dependencies), the **devkit** (a .NET AOT binary), the **service**
(ASP.NET Core), and its two clients — a React **web** app and a **desktop** shell hosting the same build.
The canon stays at the workspace root, because it is data the whole project shares rather than the CLI's
private asset.

**Why the devkit reverses the original position.** The design note argued the CLI should stay Node
because "what devtools actually do is orchestrate subprocesses, and a compiled binary that spawns a build
buys nothing while costing per-platform artefacts and a release pipeline." That weighed the *execution*
cost and missed the *distribution* one, which is the only cost this project exists to address:

- Eleven repositories carry a hand-copied `devtools/dev.mjs`, measured 2026-08-05 at **2.6 KB to
  52.6 KB — a 20× spread**. Nine also carry a config file, which is the part that was meant to differ.
  The rest is one tool, re-derived and diverged. That is the thesis, one layer below the documents.
- A .NET repository carrying a Node script has a Node dependency it needs *for tooling alone*.
- A binary has a version. A pasted script has whatever the paste contained.

The CLI stays Node and zero-dependency regardless: it is a different artefact with a different job, and
it must keep running in repositories that have no Node dependencies of their own (D8, D10).

**Why the service may depend on the cognition sibling** where D1 refused to. D1 rejected that dependency
*for the CLI*, because it would have made a build gate depend on a release cadence it does not control.
The service is a separate deployable with no such constraint, and semantic memory, the embedder seam, the
vector store and MCP hosting all already ship there. Rebuilding them would produce a second, worse copy —
which is the failure D1 was actually written to prevent.

**Consequence.** `canon/`, `LICENSE` and `README.md` sit at the root and are staged into the CLI package
at pack time, because npm's `files` cannot reach outside a package directory and D11 makes shipping the
canon *inside* the package load-bearing. The rehearsal asserts all three arrive.

**Not decided here:** how a repository declares its gates, how the binary is distributed without losing
the offline guarantee, and whether the service needs hosting at all — a local-only service queried over
MCP would answer most of the need without a deployment or a privacy boundary. Each is recorded as an
open question in the relevant `src/Daoris.*/README.md`, written before any code.

## D24 — The model is a deployment choice; features are defined independently of it (2026-08-05)

**Decision.** Daoris **does** use language and embedding models — a real part of it depends on them, and
that is not something to design around. What must never be coupled is *which* model. A feature is
specified by what it does; the provider serving it is chosen by **where the deployment runs**.

| Deployment | What Daoris is there | Which model |
|---|---|---|
| **A local repository** | A devtool set beside the working session | Whatever is local — the coding agent already present, or a local runtime |
| **A server** | A centralised knowledge provider for a team | Whatever suits a service — a hosted model, chosen for cost and throughput |

Same features, same logic, different provider. The two deployments have genuinely different constraints
— one has an agent already in the room and no budget for a network round trip, the other has
throughput and cost to answer for — so a single hard-wired choice would be wrong in at least one of
them.

**Why it is worth stating.** The models turn over faster than this project will, and the right one
differs by deployment *today*, never mind next year. A feature welded to a specific model ages at the
speed of the fastest-moving part of the stack rather than its own — and the parts of a codebase that
encode hard-won judgement should turn over far more slowly than the inference layer beneath them.

**The corollary, earned the hard way.** Decoupled also means a feature must not be *unavailable*
because a particular provider is absent. Convergence detection was built to require an embedder — it
returned null without one — and that gap was then reported as *blocked* rather than as the design
defect it was. It now runs whatever passes it can and names which found each result:

| Tier | Needs a model | Finds |
|---|---|---|
| Identical | no | Byte-identical copies. No threshold, no doubt |
| Restatement | no | Substantially the same words — a copy that has drifted |
| Convergent | **yes** | The same meaning in *different* words, which text comparison provably cannot see (D17) |

That is not a claim that models are optional to Daoris. It is that a feature should deliver whatever it
can with what is present, and say plainly what it could not do — because a caller who cannot tell why a
category is empty will assume a bug, and will be right to.

**How to apply.** Specify the feature without naming a model. Take the provider through a seam and
select it by deployment, never in the feature. Report which tier ran. Where a capability genuinely needs
a model — drafting a merged statement does — make its absence an explicit, informative message rather
than silence or an error.

**Consequence.** Provider selection belongs to the composition root, which is why the cognition
sibling's routing is the right thing to compose (D22) rather than something to reimplement. It is also
why the LLM-assisted merge splits as it does: finding candidates needs no model and ships today,
drafting a merged statement needs one and will take whichever the deployment provides.

## D23 — One harness is supported; the others are detected, not guessed at (2026-08-05)

**Decision.** Daoris targets the **Claude Code** harness, and says so. Other harnesses are **detected
and reported** — never partially generated. A second implementation gets written the day a repository
actually adopts one, and not before.

**Why.** Every tier decision in this tool is one harness's behaviour rather than a universal truth.
`rules/` is always-loaded and `knowledge/` is not because that harness decides by path (D7); a skill's
`description` is a trigger because that harness parses it; the provenance header sits *under* the
frontmatter because that harness needs the frontmatter at byte 0 (D14). None of that is true of a
harness that reads `AGENTS.md`.

**And the failure is silent, which is what makes it worth a guard.** Install this tree in a repository
whose agent reads a different file and every document is present, correct, and never loaded. There is
no error, no missing file, and nothing to notice — the worst shape a failure can take. So `analyze`
reports which harness a repository shows signs of, names the evidence, and states plainly that what
Daoris installs will be invisible to the others.

**A contract check for the same reason.** `verifyHarnessContract` checks only the things that fail
silently: a skill without frontmatter installs and never fires; a skill file outside a skill directory
can never be invoked; a rule nested one level down is simply not read. Anything that would fail loudly
needs no check, because the failure is its own report.

**Rejected:** guessing at a translation into another layout. A half-generated `AGENTS.md` would be
doctrine nobody chose, in a format nobody verified, and it would look like support — which is worse
than an honest gap, because an honest gap gets fixed the day it is hit. *(Reversed 2026-09-22 by
D59, on a measurement rather than a guess: `AGENTS.md` is the one file all three driven harnesses
read. The always-loaded tier is now a region in it that Daoris owns — not a translation of this
layout, but the tier itself moved.)*

**Consequence.** This is the seam a second harness grows from, and building it now would be building
for a consumer that does not exist — the same reasoning that keeps a pack unwritten until a repository
is ready to install it.

**Amended 2026-08-05: switching is a first-class concept, with one implementation.** Every harness fact
had been a constant scattered across six modules, each quietly asserting one tool's conventions as
universal — the target directory, the tier names, which tier is always-loaded, the skill entry file,
the required frontmatter, the index path, where the provenance header goes. They now live in one
descriptor, and the manifest selects with `"harness": "claude-code"`.

The canon keeps its own vocabulary — a document is a **rule**, **knowledge**, or a **skill** — because
that describes the *doctrine*. Where each lands on disk is the harness's translation. That separation
is what makes a second harness an addition rather than an excavation, and it is worth having before
the second exists precisely because it is cheap now and expensive later.

An unknown harness is a **tool error naming what exists**, never a silent fallback: a repository that
asked for one layout and quietly received another is exactly the failure this seam prevents. A
*recognised but ungenerated* one says so specifically, and points at this decision.

## D21 — The knowledge service is local-first, with sharing as configuration (2026-08-05)

**Decision.** One service, two modes selected by configuration rather than by build: **local** (the
default — no server, no account, no network) and **shared** (opt-in). Local must stay fully useful
alone. Full design in `docs/2026-08-05-knowledge-service-design.md`.

**Why.** Most of the value is cross-repository recall for *one person* working across a dozen checkouts,
and that needs no server at all. Making sharing the default would have imposed a deployment, an account
and a privacy boundary on everyone in order to serve the case that needs them. The business-manager
sibling already runs this shape — its database provider is configuration and its default needs no
database — so the pattern is proven in the family rather than invented here.

**The disclosure boundary is specific to this project and is decided up front.** Ordinary applications
ask who may read something; this one must first ask what may leave the machine, because several
repositories in the family are private and `sensitive-info` exists to keep their names and paths out of
tracked files. A service that indexes them centralises exactly that. So: indexing is **opt-in per
repository** with silence meaning "keep it local" — the cost is asymmetric, since over-sharing is a
disclosure and under-sharing is an inconvenience — and the untracked local directory is a **hard
exclusion in shared mode**, not a permission.

**Authorization mirrors repository access rather than inventing a second model.** "May this person read
this repository's knowledge" already has an answer at the source host. A separate model would disagree
with it eventually, and would disagree silently.

**The shared store should be a git repository before a database.** Free, versioned, reviewable, access
control that already matches the rule above because it *is* that rule, and it outlives the tool. A
hosted database earns its place when query volume outgrows it — a good problem, not a starting
assumption.

**Consequence.** "Shared" may turn out to be a sync rather than a server, in which case there is no host
to secure and the desktop shell is the product. That is recorded as the first open question, to be
priced before anything is deployed.

**Access, when it is hosted.** Two kinds of consumer, two credentials — conflating them is how one of
them ends up badly served, either a machine pushed through an interactive login or a person handed a
static secret. Machines authenticate with an **API key from the environment**
(`DAORIS_SERVICE_URL` + `DAORIS_SERVICE_KEY`, consistent with the existing `DAORIS_CANON`); people
authenticate with **OIDC**. The sibling's auth setup already reserves the seam for an API-key mode beside
its OIDC one, so this fills in a shape the family designed for rather than inventing one.

Keys are per-person, read-only, expiring by default, stored as a hash with a short non-secret prefix kept
for audit, and redacted on every path *including failures* — a sibling once passed a key on a command
line whose failure branch printed the whole command, exposing it on exactly the run most likely to be
pasted somewhere. **Absence of a URL means local**, silently: a consumer must never have to opt out of
talking to a server, and `DAORIS_SERVICE_URL` must not change what the CLI does — worth a test rather
than a rule, since D8 is the invariant it would break.

## D22 — The knowledge layer is built by composing the two siblings (2026-08-05)

**Decision.** `Daoris.Service` and `Daoris.Desktop` consume the family's cognition and desktop libraries
at released versions. `Daoris.Cli` composes nothing and keeps its zero dependencies.

**Why.** The knowledge layer needs embeddings, a vector store, semantic recall, provider routing, MCP
hosting, a desktop shell, a web surface and an IPC bridge — and every one of those already ships, in two
siblings built to be consumed. Rebuilding them would produce the second, worse copy that D1 was written
to prevent; D1 refused that dependency **for the CLI**, because a build gate must not depend on a
release cadence it does not control, and a separate deployable has no such constraint.

**It runs in both directions, which is the less obvious half.** Daoris is the first external consumer
either sibling has had. A library with no consumer is unvalidated — the same argument this project
already makes about a pack nobody installs and doctrine nobody runs. Building on them *tests* them, and
an awkward seam is a finding for that sibling rather than a workaround here.

**Consequence.** Depend on **released** versions, never on a sibling's working tree: three repositories
coupled at HEAD are one repository with extra steps, and the family's independence is load-bearing. The
CLI's isolation is what keeps this safe — a consumer adopting doctrine never acquires any of it.

**Compose capabilities, not surfaces** (clarified 2026-08-05). The line is what each sibling *is*:

- The cognition sibling is a **library**. Its capabilities compose — embeddings, the vector store,
  routing, semantic recall. Its *serving* surface does not, and asking it to grow one would be asking a
  library to become an API project.
- **There are two MCP surfaces, pointing opposite ways, and both are right.**

  | Direction | Who owns it | What it is for |
  |---|---|---|
  | **Outward** — other processes connect in | **Daoris** | The knowledge index, exposed to a session or another service. Long-lived, and a serving surface, which is not a library's job. |
  | **Inward** — a spawned CLI is handed tools | **the cognition sibling** | Its ephemeral localhost host, for when Daoris *itself* drives an agent — the merge analysis in §7 of the service design. Exactly what that host was built for. |

  Its host looked like a match for the first and is built for the second. Same protocol, inverted roles.
  Using it for the inward direction is composition working as intended; using it for the outward one
  would have been a library growing an API.
- So Daoris owns its own serving surfaces — the MCP server, and later the HTTP API — and consumes the
  siblings as libraries, including that host when it drives an agent. What transferred first was the
  *reasoning* rather than the code: use the protocol package over plain streams and skip the ASP.NET
  dependency, a conclusion that sibling had already reached and written down.

The general form: **take a sibling's capability; never borrow its role.** A library that grows a serving
surface to suit one consumer stops being reusable by the next.

**The protocol itself is a library too**, and the official .NET SDK is used rather than hand-rolled:
`ModelContextProtocol` supplies the DI wiring, the stdio transport and attribute-driven tool discovery.
Only `ModelContextProtocol.AspNetCore` is skipped, and only for the stdio surface — the protocol works
over plain streams there, so the framework reference would buy nothing. **When the hosted HTTP surface
arrives, that package is the right answer for it**, not a second hand-written host.

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

**Amended 2026-09-24 (D71): a core document a pack switches off.** The rows above describe a document
the canon selects. A switch the manifest confirms de-selects a core document, and these are the cells
for it:

| Switched off | In lock | On disk | Disk vs lock | Outcome |
|---|---|---|---|---|
| offered, not confirmed | — | — | — | not switched: the rows above apply, and the offer is reported |
| confirmed | yes | yes | same | **switch off** — delete it (a span leaves the region); the lock records it off |
| confirmed | yes | yes | differs | **edited, switched off** — refuse; `upstream` can still save it, since the canon file exists |
| confirmed | yes | no | — | drop the entry; the lock records it off |
| confirmed | no | yes | — | the repository's own file at that path (D5): untouched |
| confirmed | no | no | — | nothing to do; the lock records it off |
| confirmation withdrawn | — | — | — | selected again: the rows above apply (create, adopt, or collision) |

A switched-off row is never paired as a rename with the pack's replacement, however alike they read.
A switch is a decision the manifest names, not a move, and reporting it as one would hide the decision.

*Amended by D117 (LAYOUT3, 2026-10-01): a move, and three things `sync` writes that are not canon files.* The
table above is lock × disk × canon at ONE root. A **move** is a manifest naming a root its lock was not written
under, and **the lock, not the manifest, says where the files are**: every row above is read and deleted at the
lock's root and written at the manifest's. The cells a move adds — a canonical document under the old root, the
repository's own documents in an old tier, a skills mirror, a room's pointer, a link or a link held as text — are
enumerated in `docs/2026-10-01-agent-layout-design.md` §5.4, one `node --test` case each (`layout-move`,
`layout-mirror`, `layout-rooms`, `layout-links`). Two readings carry over unchanged: an old file whose body is the
canon's now is untouched (the state after `upstream`), and a mirror, too, is drift only when it differs from the
lock.

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

**Amended 2026-08-05, by the second adoption.** Comparison is now restricted to **within a tier**. A
generic skill — "find the exemplar to mirror" — names module, service, handler, test, registration and
naming, which is the vocabulary of *every* architecture document. Run against a real repository it
matched three unrelated knowledge documents at once and buried the genuine twin sitting beside them. A
knowledge document and a skill are different kinds of thing, so one restating the other is not
duplication worth reporting.

That run also vindicated the threshold change: the twin this repository's backlog had predicted for that
adoption, `windows-dev-gotchas` against canonical `windows-machine`, scores **47%** — found at 0.3 and
missed entirely at the original 0.5. A test had been passing via the cross-tier bug, matching a local
*skill* against a canonical *rule*; it now exercises a genuine same-tier case.

## D25 — Line endings are pinned in the repository, and normalized on read by both halves

**Decided 2026-08-05, during the tidy-up.** `.gitattributes` sets `* text=auto eol=lf`, and the service
reads documents through one normalizing helper (`Text.ReadDocument`) exactly as the CLI already read
them through `readText`.

**Why.** `daoris.lock` records a sha256 per installed document and `check` compares against it, so what a
document's bytes *are* has to be the same on every machine. It was not: the repository pinned nothing, so
the answer came from each developer's global `core.autocrlf`. A Windows clone gets CRLF working files, a
Linux clone LF, and the same repository disagrees with itself about its own doctrine.

The CLI was already safe, deliberately — `sha256` hashes normalized text, and the comment says why. The
service was safe too, but by **three separate accidents**: `MarkdownSections` happened to strip CRLF
while splitting, `Tokenize` happened to list `\r` as a separator, and convergence's identical-detection
happened to compare whitespace-insensitively. Every one of those is a local implementation detail that a
later change could drop without any test noticing. A property that holds by coincidence in three places
is not a property of the system, so it now holds in one place by construction.

**Consequence.** The bodies stored in the index are identical whichever machine built it, which matters
because the index is the thing the family shares. The regression test was checked the only way worth
trusting: reverted the fix, watched it fail, restored it.

**Not chosen: leaving it to `core.autocrlf`.** It works until someone clones with a different global
config, and then it fails as a hash mismatch on documents nobody edited — the most confusing possible
symptom for a tool whose entire job is telling you which documents changed.

## D26 — Gates are declared in a file the devkit owns, not in `daoris.json`

**Decided 2026-08-05, settling DEV1.** `daoris.json` names *which* devkit version a repository uses.
What to run lives in `daoris.gates.json`, which the CLI never reads.

**Why.** Every field in the manifest today is a noun: a source, a list of packs, a target directory, a
byte budget. It is inert data, and the CLI's whole safety story rests on that — it never executes
anything and never opens a socket, and there is now a test for each. Gates are verbs. Putting command
strings into the manifest makes the file the CLI parses on every invocation into a file that contains
things that run, and the next reasonable-sounding step is "since we already parsed them, let `daoris
verify` run them".

Splitting on noun/verb keeps the boundary visible instead of merely observed. The manifest still pins
the devkit — a version is data — so there is exactly one place to look for *which* toolkit, and exactly
one place for *what it does*.

**Not chosen: one file for both.** Fewer files is a real benefit and it loses to the above. A reader of
`daoris.json` can currently be certain nothing in it executes; that certainty is worth more than the
saved file.

## D27 — The devkit binary is hash-pinned and explicitly acquired, never implicitly downloaded

**Decided 2026-08-05, settling DEV2.** The binary ships as a release asset. The repository records its
sha256. The devkit verifies itself against that hash **offline**, and a missing binary is an error
naming the exact command to run — never a download that happens on its own.

**Why.** D8 makes `check` offline by construction, and that has since hardened: nothing anywhere in the
CLI may touch the network, enforced by a test that greps for the primitives. So the CLI *cannot* be the
thing that fetches the binary, and that is the right outcome rather than an obstacle — an implicit
download is a network call on a gate that promised not to make one, and it turns a verification step
into an install step at the worst possible moment.

Hash-pinning is the same shape as the lock: record the digest locally, verify against the record, need
nothing else. It also answers the supply-chain question the npm route raises, because the pin is written
into the consuming repository rather than resolved at install time.

**Not chosen: distributing through npm.** It is what esbuild and its neighbours do and it works well —
but the devkit exists partly so that a .NET repository does not carry a Node dependency for tooling
alone. Shipping it through npm would reintroduce exactly the dependency the artefact was created to
remove.

## D28 — The default always-loaded budget is 30000, not 24000

**Decided 2026-08-05.** `coreBudgetBytes` defaults to 30000. The v0.1 design's 24000 was a guess made
before there was a canon to measure.

**Why.** At 24000, core plus **one** pack measured 24,061 bytes in the release rehearsal — so a clean
adopter's very first `check` failed before they had written a single rule of their own. A default that
fails on the most common configuration is not a gate, it is noise, and noise trains people to raise the
number without reading it, which is exactly what the gate exists to prevent.

The sharper version of the problem showed up the same day. Adding one core rule pushed the rehearsal
consumer over, so the budget fired on the **canon** rather than on a repository's own material. That is
backwards: the budget exists to constrain what a repository chooses to carry, not to cap what the
doctrine may contain. Measured, core plus an index is ~19–20 KB and each pack is ~4 KB, so 24000 left
almost nothing for the thing being governed.

30000 leaves core, the generated index and two packs comfortably inside, and fires on a repository's own
always-loaded material getting fat — which is the case it has actually earned its keep on. It caught a
45% overage on first contact with one adopter, and forced the retirement of an 8.3 KB duplicated rule in
another; both were about local material, and both would have fired at 30000 too.

**Consequence.** No existing adopter changes: `coreBudgetBytes` is written into the manifest at `init`,
so a repository that already declared one keeps it. This only moves the starting point for the next one.

**Not chosen: scaling the default by pack count.** It would make the number depend on a choice made
later in the same file, so nobody could read the manifest and know what the limit was — and a budget
whose value you have to compute is one nobody argues with.

**Amended 2026-09-21 (CANON7, the owner's call): Daoris's own number moves to 26000, and it is a
different number answering a different question.** For an adopter, `coreBudgetBytes` asks *how much
always-loaded doctrine is this repository willing to carry* — core, its packs, and its own rules —
and 30000 is the right starting point for that. Daoris declares no packs and owns no always-loaded
rule of its own, so here the field measures **exactly the canon's core**, and the question it answers
is *how big may the doctrine itself be*. Those are two questions, and inheriting the adopter default
for the second one would be answering the wrong one by accident.

**Why it moved at all.** By this entry's own argument the pre-D28 24000 was wrong here from the day
D28 was written: it fires on the canon rather than on a repository's own material, and this is the one
repository whose always-loaded material *is* the canon. The counter-argument kept it for six weeks and
was a good one — every adopter pays for core on every session, so a tight self-imposed limit is a
forcing function, and CANON6 proved it working by finding 126 bytes of genuine duplication rather than
spending any. What ended it is that **138 bytes is not a forcing function, it is a wall**: a forcing
function needs room to push against, and at that margin every candidate rule fails on arithmetic
before anyone weighs whether it is good. CANON5 had been parked behind exactly that.

**Why 26000 and not 30000.** 26000 leaves about 2,100 bytes — room for roughly one substantial rule.
30000 would have handed over 6,138 at once. The number is a stated intention about how big the
doctrine should get, and a smaller one states it more usefully. **What that intention is worth is
settled by D54, taken the same day: the budget reports and never gates**, so this number persuades
rather than blocks.

**A measurement worth recording, found while deciding.** The generated rules index is 5,246 bytes —
22% of the core — and it grows with the *count* of documents, local knowledge and skills included,
neither of which is itself always-loaded. So part of the pressure on this budget is the index of the
doctrine rather than the doctrine, and a repository that adds many local documents pays for them here.
Not acted on: the index is what makes the on-demand tiers discoverable, and an index nobody loads is a
tier nobody reads (D7).

**Unchanged: the 30000 default, and every adopter.** This amendment moves one number in one manifest.

## D54 — A budget reports; only a fact gates (2026-09-21)

**Decision.** Set by the owner, 2026-09-21, while CANON7's number was being moved: *"I don't really
think the budget should be a hard cap."* Both budgets in this repository now **report and never
fail**. `daoris check` prints the always-loaded core against the declared `coreBudgetBytes` on every
run, says loudly and by how much when it is over, and exits 0; `npm run verify`'s doc-budget step
does the same for the prose ceilings.

**The line it draws, which is the part worth keeping.** Everything else `check` reports is a **fact
the tool established**: a file drifted from its hash, one is missing, a pack was declared and never
synced, the index no longer matches what is on disk. Each is unambiguously wrong and the tool can
prove it. A budget is a **judgement**: 26,001 bytes is not wrong, it is one byte past a number
somebody chose. A gate that stops a build over a judgement gets its number raised rather than read —
which is precisely the failure D28 described in its own words about noise, arriving from the other
direction. So: **a fact gates, a judgement reports.**

**What still fails.** Drift, missing files, stale packs, a stale index — and, in the doc budgets, a
ceiling naming a document that no longer exists. That last one is not an opinion about length; it is
a ceiling that has silently stopped applying, which looks from the outside exactly like a document
comfortably under budget. A defect in the manifest, so it gates.

**Why this does not throw away CANON6's evidence.** The tight budget did real work: under pressure it
found 126 bytes of genuine duplication rather than spending any. But what did that work was a number
*in front of the author at the moment of writing*, and that survives — it is printed on every run,
with the overage quantified, and raising it is a reviewable one-line diff. What does not survive is
the build failure, and the build failure is what turned a design question into arithmetic: at 138
bytes of headroom, every candidate rule failed before anyone weighed whether it was good.

**Rejected: deleting the budget.** The number is what makes the size legible. Without it there is
nothing to compare against and no line in the report, and "the core is getting big" becomes something
only a person who happened to measure it can say.

**Rejected: hard for the canon, advisory for this repository's own prose.** The tempting split, on
the argument that a budget should block whoever can fix it and inform whoever cannot. It is a real
distinction, but two budgets behaving differently is an inconsistency somebody re-litigates later,
and the fact-versus-judgement line above is the better rule because it also explains why *drift*
still gates.

**Rejected: a high-water ratchet** — fail only when the size grows without the manifest acknowledging
it. It keeps a build failure in the loop while making it harder to explain, and it answers a question
nobody asked: growth is visible in the diff already.

**Consequence.** CANON5 is now a judgement about *tiering* — does an i18n parity rule belong in the
always-loaded core or in a pack for web repositories — rather than about arithmetic, which is what
the budget was making it. `check`'s exit codes narrow accordingly, and the usage text says so.

## D29 — The `doc-*` maintenance family is not canonized; its useful half became gates

**Decided 2026-08-05, closing CANON4.** The six-skill `doc-*` family — update-technical, update-guide,
update-reference, optimize, monitor, cleanup — does not enter the canon.

**Why.** The family was held rather than deferred, on the argument that these skills automate
hand-maintaining documents that a generated wiki would own outright (D16), and that if the generated
route wins, what stays canonical is *the review of output, not its production*. Reading them settles it,
and the split is cleaner than expected:

- **Production is repo-specific or superseded.** `doc-update-technical` writes into two named documents
  that belong to one repository; it is not project-agnostic and could not be canonized as written.
  `doc-optimize` (shrink documents over 30 KB) and `doc-cleanup` (delete redundant, consolidate
  duplicates) maintain hand-written prose — exactly the work a generator removes rather than automates.
- **Review is already gates here, and gates beat skills.** `doc-monitor` audits four things. Three had
  become tooling without anyone connecting them to it: redundancy is `daoris doctor`, index and skill
  staleness is `daoris check`, version disagreement is the devkit's `version` gate. A gate runs; a skill
  runs when somebody remembers to invoke it.
- **The fourth check was a real gap**, and is now the devkit's `links` gate. Verified the only way worth
  trusting — added a broken link, watched it fail, removed it.

**Consequence.** Nothing is installed into every repository for a workflow that may change, and the
capability the family actually provided is enforced rather than available. The two repositories carrying
the family keep it as local doctrine, which is the correct home for a workflow specific to their
documents.

**The evidence was also weaker than recorded.** The backlog said the family appears in three
repositories; it is two with the identical six, plus a third with two differently-named skills. Worth
noting because the two-repository bar is what makes canonical content trustworthy, and a count that
drifts upward in the retelling is how a bar gets quietly lowered.

**Amended 2026-08-05 — D17 confirmed end to end, on a pair nobody constructed.** The limit is no longer
argued from a survey; it has been measured on real text, and the semantic pass has been shown to clear it.

While canonizing `claims-need-checks`, a sibling turned out to have derived the same principle
independently — from the opposite end, auditing shipped API documentation against its own source rather
than finding an unenforced configuration field. Word overlap scores the two at **25%**, below the 30%
threshold, so `doctor` cannot see them and no retuning would help: at 25% they are indistinguishable
from an unrelated pair.

Indexed into the service with a local embedding endpoint, the semantic pass reports exactly that pair at
**0.785**, labelled *Convergent — same lesson, different words*. It also discriminates rather than
matching everything: at a 0.82 threshold nothing is returned, at 0.70 only the true pair, and at 0.60 an
unrelated storage document joins them. That is the precision/recall curve behaving as it should, and it
supports the parameter having no clever default — the useful value depends on the embedder and the
corpus, which is why the tool asks for a sweep rather than trusting one.

**This is the first end-to-end evidence that the service does the thing it exists for**, and it was not a
constructed test: the convergence was found by hand during an adoption, and the tool independently found
the same pair. It also confirms **D24** from both sides — convergence detection returns copies and
restatements with no model at all, and the model adds the class that text comparison provably cannot
reach.


## D30 — The web UI's primary view is convergence, not search

**Decided 2026-08-05, settling `Daoris.Web`'s first open question.** The landing view is *"these
repositories said the same thing in different words — read them and decide"*. Search exists, and it is a
supporting view.

**Why.** The brief already suspected search was the obvious answer and the wrong one, and a day of real
work settled it. The finding that mattered most this session was a convergence: two repositories derived
the same principle independently, in vocabulary so different that **word overlap scored them at 25%**.
No search could have surfaced that, and not because the search was bad — **to search for it you must
already know it exists.**

Everything else that produced value was comparison too. Adoption is comparison: what collides, what is a
twin, what a pack already covers. `analyze`, `doctor` and the coverage measurements are all comparison
tools. Search answers a question you have; comparison tells you which question to ask, and the canon was
built almost entirely from the second.

**Consequence.** The service's convergence detection is the UI's centre rather than a feature on a menu,
and the semantic tier matters most exactly where the UI matters most.

**Not chosen: a search box as the landing screen.** It is what every knowledge tool ships and it would
make this one a worse `grep` across repositories — a job the CLI already does offline and faster.

## D31 — The web UI reads; it proposes a command rather than writing

**Decided 2026-08-05, settling the second open question.** No editing of doctrine from the browser. Where
a change is warranted, the UI shows the exact command to run in the repository that owns the file.

**Why.** `upstream` deliberately routes an improvement through the repository that found it, where it
meets that repository's review. A web editor competes with that path and wins for the wrong reason —
it is more convenient — and the result is doctrine that changed without passing anyone's review.

The convergence detector already states the principle for itself: it proposes, a person disposes, and a
candidate is a prompt to look rather than a merge (D21). A UI that could apply its own suggestions would
contradict the one component it is built on top of.

Every canonization this session needed judgement a UI could not have made: whether a twin was a merged
pair whose local half had to survive, whether a rule belonged in the always-loaded tier, whether a
document was superseded outright or only overlapping. **Generating the command is more useful than an
edit box** — it puts the change where review happens and leaves the judgement with the person.

**Consequence.** The service stays read-only, and its HTTP surface can be too, which removes
authentication-for-writes from the first version entirely.

**Amended 2026-09-18 (D36).** The HTTP surface now carries the *service-state* writes — registry
registrations and quests — gated by `DAORIS_SERVICE_KEY` when set *(that interim single-key gate was
itself retired 2026-09-20 — D47 as amended: loopback trust in local mode, minted keys on every route in
shared)*. **Doctrine** remains unwritable from the browser and from every endpoint, which is the part
this decision was actually about: no rule, knowledge document or skill can be edited anywhere but the
repository that owns it, through review.

## D32 — Cross-repository work is a quest, not an edit

**Decided 2026-08-05.** Repositories in this family are not developed across. A change one repository
needs from another is a **quest** posted to that repository's backlog, taken and answered there.
`daoris quest post` writes it; `take`, `done` and `decline` move it through four states.

> **Amended the same day** — filed under D33 below, where it was written: `quest post` wrote into
> the receiving repository, which is what this decision forbids. **The service holds quests, the
> receiver pulls them, and the CLI has no quest command.** Addressability followed D34, then D70.

**Why.** This is the design the family was already following informally, and the reason Daoris exists at
all. One repository keeps a "waiting on the sibling repository" section in its backlog; another
separates work needing a decision elsewhere from work it can do itself. Nobody agreed on that — it was
arrived at independently, because it works.

The argument is not etiquette. An outside edit is made by whoever knows that codebase *least*, which is
what being outside means, and it skips the review that repository would have applied. More importantly:
**the knowledge is not portable but the quest is.** Why a rule is worded as it is, what was tried and
rejected, which constraint a file encodes — that stays with the repository, and an outsider will not
reconstruct it before changing something. A quest carries the part that does travel: what is needed, and
why. The judgement stays where the context is.

**Why "quest".** Every backlog here is already full of tasks, so "task" or "request" would be ambiguous
in exactly the file where the distinction matters. A quest is also *taken* rather than assigned, which
is precisely the property that keeps declining a real answer — and declining requires a reason, because
a bare refusal gives the asker nothing to act on.

**Shape.** An ordinary checklist item, because that is what every backlog here already holds: the
checkbox is the coarse state, and the italic line carries asker, date, status and reason. A repository
that knows nothing about Daoris still handles one correctly. It appends under one fixed heading and
never restructures — the backlogs are shaped too differently for a tool to file in the "right" section
without being wrong in someone's repository the day they reorganise.

**The service indexes them**, as their own entry kind. A quest reaches only the repository it was posted
to, so "what has been asked of whom, and is anything sitting" is a question no single backlog can
answer — which is exactly the kind of question a cross-repository index exists for.

**Not chosen: sub-agents reaching into other repositories.** That is the shape this replaces. It scales
badly, produces edits nobody reviewed, and throws away the domain knowledge that makes the change
correct. Daoris is the substrate for domain-owning agents to share knowledge and work — not a way for
one agent to work everywhere.

**Exceptions, narrow:** initializing a repository that has no owner yet, and a change so coupled that
splitting it would leave neither side working. A change that merely *touches* two repositories is not
that — it is two changes and one quest.

*Amended by D115 (DEV4, 2026-10-01): the exchange's self-address refusal is narrowed to a quest that
names no lane. A repository may address one of its own lanes (`repository:lane`), because work for
another lane is work for another session, which is what the refusal protects. A quest to itself with
no lane is still refused, with the same sentence, at every door, since `QuestExchange` judges them all.*

## D33 — The CLI is TypeScript; the dev loop stays buildless and the package stays dependency-free

**Decided 2026-08-05.** `src/Daoris.Cli` is TypeScript under `strict`, with `noUncheckedIndexedAccess`
and `exactOptionalPropertyTypes`. Sources are `.ts` importing `./x.ts`; the emit rewrites those to `.js`.

**Why now.** It was the only untyped artefact left — the web app is TypeScript, the service and devkit
are C# — and it holds the most intricate logic in the project. D19's state space is lock × disk × canon,
corrected four times before anyone wrote it down; those three shapes were described accurately in
comments and checked by nothing. Naming them is most of what this buys.

**Two properties had to survive, and did.** The published package still declares **zero runtime
dependencies** — TypeScript is a build dependency, and the guarantee was always about what a consumer
installs. And **the dev loop needs no build**: Node 24 strips types, so `node --test` runs the sources
directly. Only publishing compiles, because a consumer's Node may be 22, which does not strip types on
its own and would fail on a package of `.ts` files.

`bin/daoris.mjs` stays `.mjs` and stays thin — it is what npm's `bin` names and what every consumer
executes. It resolves `dist/` when present and the sources otherwise, so the same entry works built and
unbuilt. The dispatcher moved into `src/cli.ts` where it can be typed.

**Two configs, deliberately.** `tsconfig.json` typechecks sources, bin and tests with no emit;
`tsconfig.build.json` compiles `src` alone with `rootDir` pinned. Without that pin the tests share a
root, the output nests to `dist/src/cli.js`, and the bin entry silently falls back to running sources —
a build that appears to work and ships nothing.

**Consequence.** `npm test` uses auto-discovery rather than a glob. The glob was mishandled on Windows
and quietly dropped a file: the count fell 130 → 129 at the rename, which is exactly the failure mode a
glob invites.

**The migration was verified by the tests, not by the compiler.** 130 tests and the release rehearsal
ran green at every step, including while hundreds of type errors remained — which is the right order:
the types describe what the code does, so the code proving itself first is what makes the descriptions
trustworthy.

**Amended 2026-08-05, the same day — quests are a SERVICE responsibility, and the first version got it
wrong.** *(This amends D32, not the TypeScript decision above; it was written here and stays here.)* `daoris quest post <path>` wrote the quest straight into the receiving repository's `TASKS.md`.
That is the very thing this decision forbids: an outside edit is still an outside edit when it is one
file and uncommitted, and it still arrives from the party that knows that codebase least. The tooling
for the rule broke the rule, which is the most embarrassing way to find a design error and the most
convincing.

It was also incompatible with **D8**. Reaching a central store means the network, and nothing under
`src/Daoris.Cli` may open a socket — enforced by a test added the same morning. The CLI could not be the
client for this even if writing files had been acceptable.

**So the service holds quests and repositories pull.** An agent publishes through the service; the
receiving repository's own agent reads what is addressed to it and decides — including whether to
materialize it into its backlog, which is then that repository editing itself. The CLI has no quest
command at all, and stays the offline doctrine tool it was.

**Adoption is the gate.** Only a repository the index knows has adopted can be addressed, because one
without the client has no way to see the quest — and a quest nobody can read looks exactly like a quest
that was read and ignored. *(Amended by D70, 2026-09-24: registered is addressable; adopted is
disciplined.)*

Stored beside the index in the same database: quests are service state as the index is service state,
and two files would be two things to back up and two that can disagree about which repositories exist.


## D34 — A repository registers what it owns, and that is what makes a quest addressable

**Decided 2026-08-05.** Each repository declares a `domain` in its manifest — a one-line summary, the
areas it **owns**, and the kinds of quest it **accepts**. The service reads those while indexing and
serves them as a registry.

**Why.** Quests alone are not enough. Without a declaration, an agent publishing one is guessing what
the other side does — which is the same *"the knowledge does not travel"* problem the whole arrangement
exists to solve, moved one step earlier. A quest addressed to the wrong repository wastes both sides,
and the asker is the party least able to tell.

The registry answers a question search cannot. **Search answers "has anyone solved this"; the registry
answers "whose problem is this."** Those have different answers, and only the second tells you where a
change belongs.

**Declared in the manifest, not configured centrally.** It is data — nouns, what the repository *is* —
so it belongs where the manifest already lives (D26). Keeping it next to the thing it describes means it
is reviewed by the people it describes, and a central list would drift the moment a repository changed
and nobody remembered to update the server.

**Adoption gates addressing; declaration does not.** A repository without a manifest has no client and
cannot see a quest, so it is not addressable — and the publish call says so, naming who is. A repository
that has adopted but declared nothing *is* addressable; the asker is simply warned that it may not be
that repository's problem. Refusing until a form is filled in would make adoption a chore, and this all
rests on adoption being easy.

**Non-adopters are listed, not hidden.** "Who cannot be asked yet" is the same question as "who can",
and a silent omission reads as the repository not existing.

**Consequence.** The service is now the thing that connects the family's agents rather than only their
documents: it knows who exists, what each owns, what each will take on, and what is outstanding between
them.

**Amended 2026-09-24 (D70): registered is addressable; adopted is disciplined.** A repository
registered on this machine with a root can be asked without having adopted. A session the driver
starts there over the protocol door is handed its connector on the wire, so the quest has somebody to
answer it. Adoption still gates the declaration, the doctrine and the repository's own connector. The
pipe door, and a shared deployment, still address adopters only.

## D35 — `connect` is the one online command, and D8 was over-broadened

**Decided 2026-08-05.** `daoris init` scaffolds the `domain` declaration; `daoris connect` sends it to a
knowledge service. It is the **only** command in the CLI that touches the network.

**A correction first.** D8 says *`check` works offline*, and that is the invariant: it runs inside build
gates, and a gate that can fail on a network call is not a gate. Earlier the same day this was
broadened to "nothing anywhere in the CLI may open a socket" and enforced with a grep over every
module. That is a stronger claim than D8 makes, it was written by me rather than decided, and it would
have made a client impossible — which is how it was found.

**The real invariant, now tested as two things.** Exactly one module may reach the network, and
**nothing `check` transitively imports may reach it**. The second matters more: a gate would not
realistically break by someone adding `fetch` to `drift.ts`, it would break by an innocuous import
three modules deep acquiring one for it. Both assertions were verified by sabotage — a bare
side-effect import and a named import, each watched failing, each restored.

**Why a client must push at all.** A service on the same machine can read manifests off disk, and does.
One running anywhere else cannot see the repositories at all, so it has nothing to read — and the hosted
deployment was always part of the design. Pushed registrations win over scanned ones, because the client
knows its own manifest and a remote service has nothing else to go on.

**Registering an empty declaration is refused.** It is worse than not registering: it puts a repository
on the map as something that answers nothing, and a sibling reading that learns less than from a gap.

**Everything else still works with no service at all.** `connect` is opt-in, and a repository that never
runs it loses discoverability and nothing else — no gate, no sync, no check depends on it.

**A defect this introduced and its fix.** Making the dispatcher async broke error handling: a `try` does
not catch a rejected promise, so a `DaorisError` from `connect` escaped as an unhandled rejection and
printed a stack trace instead of its message. Exit codes are the contract, and a stack trace is neither
the code nor the message. The dispatcher now routes both paths through one reporter.

## D36 — The service is deployable: nothing needs to run between sessions; state persists and the HTTP host carries transfer (2026-09-18)

**Decision.** SVC1 closes without a daemon. **Local mode:** the MCP host stays spawn-per-session — the
client starts it, the session uses it, it exits — and the *database* is what persists. Every session in
every repository on this machine spawns over the same file, so a quest published from one repository's
session is waiting when another's starts. `.mcp.json` in this repository registers it; a sibling adds
the same entry to its own file. **Remote mode:** the HTTP host carries the transfer — registry and
quests (`POST /api/registry`, `POST /api/quests`, `POST /api/quests/{id}/respond`) — through the same
`QuestExchange` the MCP host uses, with `DAORIS_SERVICE_KEY` gating every `POST /api/*` when set and
absence meaning local trust (D21). It runs with **no model at all** and still carries all of it (D24):
a remote Daoris is purely a transfer of request and task unless a repository opts its knowledge in.

**Why.** "Nothing runs between sessions, so nothing can be published or pulled" conflated two different
gaps, and a daemon would have fixed neither. The first was **state that did not survive**: pushed
registrations lived in a dictionary, so a service restart silently dropped every repository that had
ever run `connect` — and for a remote service, pushed registrations are the only registrations there
are. They now persist in the same SQLite file as the index and the quests (one file to back up, one
answer to "which repositories exist"). The second was **a door that did not exist**: quests could only
be moved over MCP stdio, so a deployment anywhere else was a read-only mirror.

**The judgement moved to one place.** Publish/respond rules — who may be addressed, what a refusal
says, what declining requires, including the message text — live in `QuestExchange`, used by both
hosts. Written per host they would drift, and the same ask would be deliverable through one door and
refused at the other, which for a quest system is the worst available bug: it looks like the sibling
ignoring you.

**Verified on the artefact, not only in tests.** The host was driven live with a key: the unauthorized
write answered 401; `daoris connect` registered through the real endpoint; the host was killed and
restarted and the pushed registration and a taken quest were both still served; a publish to a
non-adopter was refused naming who *is* addressable.

**Not chosen:** a daemon or Windows service (once state persists, nothing needs to run between
sessions — a deployment cost buying no property); per-person expiring keys and OIDC now (deferred until
a deployment leaves a trusted network; the full model is designed in the service design §5); an MCP
relay from the local stdio host to a remote service (build it when a second machine actually exists —
the HARNESS1 reasoning).

**Consequence.** The quest-ledger pattern — holding outbound quests in this repository's backlog
"until a service runs" — ends, because the service runs. Addressing still gates on adoption (D33), so
a sibling that has stepped off the tool is not addressable until it re-adopts; nothing changes there.

**Amended 2026-09-20 (D47, as amended).** The interim single-key write gate is retired, with nothing
deployed. Two trust shapes only: **local** trusts the loopback outright and may bind nothing else (the
startup refusal enforces it), **shared** gates every route — reads included — with minted per-person
per-machine keys. `DAORIS_SERVICE_KEY` survives solely as the client-side "key I present", which
against a shared deployment is a minted key; no server-side code consults it.

## D37 — Development is automation-first: the person sets the target and verifies the outcome; gates verify the middle (2026-09-18)

**Decision.** Set by the owner, 2026-09-18: this family is moving to fully automated development via
code generation, with the human at two points — the initial target and the final verification. Daoris
shapes itself for that operator. Adoption is executed end to end by the adopting repository's own
agent, from `analyze --json` and the playbook, with the owner reviewing the uncommitted diff as the
closing checkpoint (`.claude/knowledge/adoption.md` is rewritten around those two checkpoints). The
canon carries the operating model as core knowledge, `autonomous-development`: take the target and
run; done means gates green plus a reviewable record; mid-run questions batch to checkpoints.

**Why.** A game-scale application is built as many subsystems and many verification runs. A person
approving every reversible step becomes the bottleneck on exactly the work that needed no judgement —
and approval fatigue trains the reviewer to click through, so the one step that deserved a real
decision arrives to a reader who has stopped reading. A gate runs every time, identically, and exits
non-zero; sporadic human verification does not. Human judgement is the scarce input, so the process
delivers it an *outcome* to judge: the diff, the gate results, and the records of what was decided.

**What does not move.** The carve-outs hold at full strength precisely because everything else is
automated: destructive or irreversible actions, anything that leaves the repository (a quest instead —
D32 stays absolute), publishing and releasing, and **committing** remain explicit human decisions.
"Never commit without the user's approval" is not weakened by this decision; it *is* the final
verification checkpoint, stated as a commit gate.

**Tier and evidence, stated honestly.** Knowledge rather than a rule — the same demotion reasoning as
`model-decoupling` (it applies when shaping how a task runs, not on every task), and the always-loaded
core sits at 23,988 of 24,000 bytes after its index row, which is the budget gate saying "knowledge" as
loudly as it can. And it is canonized **on the owner's direction rather than on two-repository
convergence** — recorded plainly because the evidence bar matters (D29's count-drift lesson), and
because an owner setting the target for the family is itself the model in action.

**Amended 2026-09-18, the same day — the commit gate moves to the outward boundary.** The owner's
follow-up to the first landing: committing should be "mostly auto". So a commit belongs to the
automated middle — it is local, reversible, and lands per task once gates are green — and the person's
final verification is the review of the landed history, not the act of landing it. What remains
explicitly human is the boundary that cannot be taken back or that leaves the repository: push,
publish, release, history rewrites, cross-repository writes, and destructive actions. The original
text made the commit itself the checkpoint; that sentence is superseded, and `autonomous-development`,
`CLAUDE.md`, `TASKS.md` and `ROADMAP.md` now state the boundary form.

*Read with D109 (WSR6, 2026-09-30): Daoris fetches a repository's line from its origin, as the person and by
their press, to bring it up to date after a pull request merged. A fetch leaves nothing; the push stays
outside the automated middle, and Daoris never makes one.*

## D38 — The platform is the web app grown into the person's window; service-state actions arrive in the UI, doctrine actions never do (2026-09-19)

**Decision.** The task / knowledge / setup platform the owner asked for is `Daoris.Web` with two more
views — **Quests** (what has been asked of whom; publish; take / done / decline) and **Projects** (the
registry, including who cannot be asked yet and how a project joins) — beside the unchanged
Convergence and Search. Full design in `docs/2026-09-19-platform-design.md`. Web first; the desktop
shape stays the existing brief — the same build hosted in the desktop sibling's shell, at a released
version (D22).

**Why not a new artefact.** A second hand-written UI is this family's own divergence pathology in a
new place; the rule that created `Daoris.Web` ("one UI, two shells") already decided this.

**The write boundary, kept precise.** D31's reason was doctrine — a web editor would beat `upstream`'s
review path for the wrong reason — and doctrine stays unwritable from the platform, permanently. Quests
are service state (D32, D33), already writable over the HTTP surface (D36), and under D37 **filing a
quest is how a person sets a target** — so quest publish and respond belong in the person's window,
through the same key-gated endpoints and the same `QuestExchange` judgement as every other door, with
refusals surfaced verbatim. A keyed remote deployment renders the platform read-only (the browser has
no key; person-auth is OIDC, deferred as SVC2) — stated in the UI rather than worked around.

**Consequence.** D30 stands: convergence remains the landing view until real platform use argues
otherwise. `WEB1` closes into this.

## D39 — A tracked example family under `examples/`, and a family rehearsal that proves the router (2026-09-19)

**Decision.** Daoris carries two example projects — `examples/engine` and `examples/game` — each a
complete miniature adopter: manifest with a declared `domain`, synced doctrine, a local document, a
README. `tools/family-rehearsal.mjs` (`npm run rehearse:family`) drives the whole multi-project story
through the **real artefacts**: doctrine current and `check` clean in both, the HTTP host spawned over
a scratch database rooted at `examples/`, both projects registered through the real `daoris connect`,
a quest published from `game` to `engine` and refused toward a non-adopter, taken and done over HTTP,
still there after a restart, and the examples' own knowledge answerable through search.

**Why.** Three needs, one mechanism. The owner is about to start a real multi-project build with
Daoris as its centralized router, and "the router works" must be a gate, not a belief — the release
rehearsal proves the *doctrine* lifecycle for one consumer, and nothing proved the *routing* lifecycle
across two. Second, "how does a project get set up" needs a worked example more than an explanation —
the examples are the setup story made concrete, and `examples/README.md` is the guide. Third, every
piece the rehearsal drives is exactly what the platform's views sit on, so the example family doubles
as the platform's known-shape population.

**Tracked in full, deliberately.** Manifests, locks and synced doctrine are committed, exactly like
Daoris's own `.claude/` — so the examples are readable as examples, not only as fixtures. The cost is
honest: a canon change must sync the examples in the same change, and the family rehearsal fails if
they lag (it runs `sync` and requires the tree unchanged). That is the same discipline the repository
already applies to itself, extended to two more trees.

**Not chosen:** generating the examples into a gitignored scratch (invisible as examples — the whole
point was that a person can read them), and a third real repository (a consumer that exists only to be
a consumer is a fixture wearing a costume; the examples say so on their face instead).

## D40 — The platform lands on an Overview; the person's first question is "is anything sitting" (2026-09-19)

**Decision.** Set by the owner the day the platform shipped: it is *mostly for the person to use*, so
it gets a real management UI. The landing view becomes **Overview** — family health as stat tiles
(adopted projects, open and in-progress quests with the oldest sitting time, the index's size), the
outstanding quests oldest-first, and the repositories by what the index holds, one hue because one
series. Tab order goes management-first: Overview, Quests, Projects, then the knowledge pair.
Quests gains grouping by state, sitting time made visible, and a publish form behind a deliberate
action; Projects gains the owns/accepts declarations as scannable chips and the local/canonical split.

**Why this does not overturn D30.** D30's measured finding was that *search* must not lead, because a
convergence cannot be searched for — and search still does not lead. Convergence remains the first
view of the knowledge half. What changed is the platform's job: a knowledge browser opens on what the
family knows; a management console opens on the state of the thing being managed. Under D37 the
person's work is targets and outcomes, and the outcome view is "what is outstanding, and for how
long" — a question no amount of convergence detection answers.

**Consequence.** The platform design document carries the amendment; D30 stands for the knowledge
half. The stat tiles and the repository bars follow the visualization discipline (single-hue bars for
one measure, values in ink rather than the mark's color, status pills never color-alone) so the
management surface stays readable rather than decorated.

## D41 — The platform gets a designed language: a console shell, a token system, and a computed status palette (2026-09-19)

**Decision.** Set by the owner the same day: "design the UI/UX properly, because this is used by a
human." The platform stops being a styled document and becomes a console: a fixed **sidebar shell**
(wordmark, icon navigation with the outstanding badge, global state — tier, index size, refresh — in
exactly one place), a **page header** per view with its one primary action, and a small component
language — solid-accent primary buttons, status pills on soft fields, a right **drawer** as the single
detail-and-form surface (knowledge entry, quest compose, quest detail with its actions), **toasts** for
every action outcome carrying the service's sentence verbatim, designed empty states, and static
skeletons with hold-at-reduced-opacity refetches. Full language in `docs/2026-09-19-platform-ux.md`.

**The status palette is computed, not tasted.** Four quest-state hues per theme, validated end to end
with the visualization skill's six-check validator: the first candidate failed exactly where taste
would not have noticed — red↔green at deutan ΔE 3.9, and the bronze accent below the chroma floor
inside a status set. The shipped sets pass all checks (light: worst deutan 13.3, normal 21.0; dark:
worst deutan 9.2, normal 17.5), the red/green pair separated by lightness as well as hue, and the
accent is deliberately *not* a status: it is the interactive identity. Status never appears without its
text label.

**Why a sidebar and a drawer.** A daily console needs a persistent *place* — navigation that stays
put, global state stated once, and a detail surface that does not destroy the list behind it. The
drawer replaces three ad-hoc patterns (a centered modal, an always-open form, inline expanding cards)
with one, and moves quest actions to where there is room to act deliberately — the card returns to
being something you read.

**Not chosen:** a component framework or CSS library (the whole surface is one hand-written stylesheet
and a dozen inline SVG icons; a dependency would buy generic looks at the cost of the paper character
and the zero-dependency posture), and a kanban board for quests (drag-and-drop implies reassignment
semantics the quest system deliberately does not have — a quest is *taken*, never assigned).

**Amended 2026-09-19, the same day — the no-framework half is reversed by D42.** It was the right call
for a one-day build and the wrong one for a long-term product, and the owner said so. The kanban
refusal stands.

## D42 — The front end is properly tooled: headless libraries under the same language, i18n from day one, and the example family as the UI's test loop (2026-09-19)

**Decision.** Set by the owner: the platform is a long-term project — "a good use of UI tooling and
library is a must", with a design tool, internationalization, and the example project wired into
testing, sub-agents and test-in-loop included. The web app is re-platformed accordingly, full design in
`docs/2026-09-19-frontend-architecture.md`: **Tailwind v4** with the validated tokens as its theme,
**Radix UI** primitives (dialog, toast, select, tooltip, checkbox — behaviour without a look),
**lucide** icons, **TanStack Query** as the server-state layer with invalidation after every mutation,
**react-i18next** with `en` and `zh` catalogs, **Storybook** as the living design tool over the real
components, and **Playwright** end-to-end tests that boot the real HTTP host over `examples/` and
drive the shipped bundle — joined to `daoris.gates.json`, so a release whose UI cannot do its job over
the example family does not ship.

**Why headless everywhere.** The design language (D41) is the product's face and it keeps; libraries
were chosen so that every pixel stays ours and every *behaviour* — focus traps, dismissal, ARIA,
cache invalidation, locale plumbing — stops being hand-rolled. A styled framework would have bought
generic looks at the price of the paper character; headless buys correctness at no visual price.

**The i18n boundary.** UI chrome translates; **data does not**: quest content, registry declarations,
knowledge bodies, and the service's own sentences render verbatim — machine-translating a refusal would
break D38's contract that the service's sentence is the message. Keys are structural
(`quests.compose.publish`), `en` is a catalog like any other, `zh` is 简体 in a console register.

**What this does not touch.** `Daoris.Cli` keeps its zero runtime dependencies — that guarantee was
about what a consuming repository installs, never about the web artefact. Doctrine stays unwritable
from the UI (D31); the landing stays management (D40); the computed status palette stays (D41).

## D43 — The service ships as executables, not as a checkout with a build step (2026-09-19)

**Decision.** Raised by the owner: there was no published server executable. `tools/service-publish.mjs`
(`npm run publish:service`) publishes both hosts **self-contained single-file** — deliberately not
trimmed and not AOT, because the MCP SDK discovers tools by reflection; the devkit remains the AOT
artefact and this one values working over three megabytes. `--install` lands them in `~/.daoris/bin`
and prints the `.mcp.json` snippet **with the family root already filled in**. The release workflow's
per-platform binaries job ships them beside the devkit: the MCP host as a bare executable, the HTTP
host as an archive carrying its web bundle, each with a sha256 beside it (D27's distribution shape).

**The hosts became safe to run from anywhere**, because a published binary loses every assumption a
checkout provided: the HTTP host resolves its content root beside the executable when the working
directory has no bundle (otherwise it answers every API call and 404s the page), and the MCP host says
on stderr when it has no workspace above it and no root named, instead of silently indexing whatever
directory spawned it — the ghost shape, one layer up.

**Two traps, found by running the artefact rather than reading about it.** "Single file" leaves
`e_sqlite3` beside the executable, so an install that takes only the exe dies on first store open —
`IncludeNativeLibrariesForSelfExtract` closes it. And a stdio host under a null stdin exits
immediately and *cleanly*, which looks like a crash to a naive probe; the honest verification ran the
installed binaries from a neutral directory and asserted on behaviour, not on staying alive.

## D44 — The loops create their consumer: a project is born inside the tests (2026-09-19)

**Decision.** Raised by the owner: the e2e never *created* a project — the examples are pre-baked, so
the one lifecycle the next real family most needs proven (a brand-new project joins and appears) was
documented but untested. Both loops now run over a **scratch copy** of the example family — the
tracked `examples/` stay a currency gate and are never dirtied — and both include the birth: the
family rehearsal (29 checks) creates a repository from nothing mid-run and takes it through the real
CLI — `init`, the domain declared, `sync`, `check` clean on first contact, `connect` — until the
registry knows three members, a quest reaches the newcomer at once and is answered, and the newcomer
survives the host restart. The Playwright suite (6 tests) does the same through the UI: the project
created mid-test appears as a member in Projects and is quest-addressable from the compose drawer.

**Why the copy.** A newcomer must be creatable without touching the tracked examples, whose job is
different: they are the readable, committed setup story, kept current with the canon by the rehearsal's
first phase. Fixtures mutate; examples are read. One tree cannot be both.

## D45 — Daoris is the driver: centralized workflow management, in three parts (2026-09-19)

**Decision.** Set by the owner, 2026-09-19, as the project's direction: Daoris becomes **the main
driver for all projects** — a centralized workflow-management system, not only the substrate beneath
one. The reasoning is the owner's own: development already runs through agents (claude/codex), and the
work already routes through Daoris as queue/task requests — the quests. The half that is missing is
**triggering**: today a quest waits for a session that happens to exist; under this direction, Daoris
*starts and manages* the sessions. A project with multiple subprojects is controlled by Daoris running
multiple agent sessions — one per domain-owning repository, which is what keeps the domain separation
clean: the orchestration is central, the work never is.

**The core loop this creates.** A target is set — by the person, or by another repository's agent — as
a quest. Daoris spawns (or wakes) the owning repository's agent session with that quest as its target.
The session works inside its own repository under its own gates (D37's middle); done or declined flows
back through the service; the person verifies outcomes in the platform. The quest queue becomes an
execution queue, and "is anything sitting" becomes "is anything sitting *that Daoris should have
started*".

**Three parts, named by the owner:**

| Part | What it is | State |
|---|---|---|
| **1 · The connector** | Per repository: setup — rules, skills, MCP wiring, the join lifecycle. The CLI, the canon, and the `.mcp.json` story, named as a part. | **Exists** — proven by the loops (D39, D44) |
| **2 · The local driver** | `Daoris.Desktop`, re-scoped: a desktop application that hosts the local server, carries the platform UI, and **controls all repositories and their agent sessions** — spawn, monitor, coordinate. | **Next** — design first, in a fresh session (DRV1 → DRV2) |
| **3 · The remote server** | Multi-user, for teams: shared knowledge and quests across machines, updated **via** the local desktop app (local-first; the remote is fed, not authored). Later in the roadmap. | **Later** (DRV3; SVC2 folds into it) |

**What this preserves, deliberately.** `repository-owns-its-work` is not weakened — it is the reason
for the shape: sessions run *inside* each repository, as that repository's own agent; Daoris
orchestrates and never reaches across. Quests remain the only transfer. D37's carve-outs hold: the
person sets targets and verifies outcomes; destructive, irreversible, cross-repository and publishing
actions stay human. The CLI stays the zero-dependency connector. And per D23's lesson, the driver
should be **adapter-based from the start** — claude-code first, codex as a second adapter — one
supported, others explicit, never guessed.

**What it changes.** `Daoris.Desktop`'s brief (it was a shell hosting the web build; it becomes the
orchestration host that also does that); the service will need a trigger/session surface (a quest that
should start work, a session's lifecycle attached to it); and the roadmap is re-sequenced around the
three parts. Design comes first, in a fresh session — this decision records the direction, not the
mechanism.

## D46 — The driver spawns fresh sessions that claim their own quests; driving is additive, never exclusive (2026-09-19)

**Decision.** DRV1 is settled: `docs/2026-09-19-driver-design.md` is the mechanism for D45's part 2.
The driver spawns a **fresh, non-interactive session per quest** — one active session per repository,
onto a clean working tree only, oldest open quest first. **The spawned session claims its own quest**:
take, work, done or decline, over its own connector, as the repository's own agent. Session **records**
live in the service beside the quests, behind a shared judgement class so the hosts cannot drift;
session **processes** never leave the desktop host, and the service stays spawn-free and model-free.
The adapter seam is D23 one layer up — claude-code supported, codex explicit second, an unknown adapter
errors naming what exists, and an adapter names a harness, never a model. The registration gains the
one field spawning needs: the repository root, sent by `connect` (which runs in the repository and
knows it), stored machine-locally and never leaving the machine. The mechanism is gate-verified by a
**stub adapter** phase in the family rehearsal; the real adapter is a deployment choice on a proven
loop, reporting itself in every record.

**The owner's constraint, set the same day, shapes the whole design: driving is additive, never
exclusive.** A repository developed outside driver-managed sessions stays first-class — its connector
still syncs, upstreams and publishes, and a quest it publishes triggers driver sessions elsewhere
exactly as a platform-filed one does. That is why the session claims its own quest rather than the
driver claiming on its behalf: the quest state machine is the only lock, a driver-started session and
an interactive one are indistinguishable at the quest layer, and nothing outside the driver ever needs
to know the driver exists. It is also why the driver holds **no identity**: a component that never
writes quest state needs none to be safe, and per-caller identity waits for the deployment where it
means something (DRV3/SVC2).

**Why spawn rather than wake.** The MCP host already proved the shape (D36): the session is the
ephemeral thing and the store is what persists. A long-lived idle agent cannot be pushed to without
inventing a channel, burns context waiting, and holds doctrine that has since synced — a fresh session
enters through the repository's own discovery skills, which is the doctrine path in. Resuming a session
for a follow-up is an adapter *capability*, held until real driven runs ask for it.

**Session state is observed, never self-reported.** The driver moves a session by process lifetime and
quest transitions — the two signals outside work also produces (minus the process) — rather than an
in-band status protocol an interactive session would not speak. Gates-green is deliberately not a
lifecycle state: it is outcome evidence in the record, because the gates belong to the repository's own
loop (D37), not to the driver.

**Rejected: the driver takes the quest before spawning.** It would attribute the claim to a component
that is not a party to the work, need a repair path for claimed-but-never-started quests, and make
driven work distinguishable from outside work at exactly the layer where symmetry is the guarantee.

**Amended 2026-09-24 (D70): over the protocol door, the connector is the wire's.** A repository
registered with a root and never adopted is drivable over the protocol door. Its session is handed the
knowledge server on `session/new`, not through the repository's own files. It is given no canon,
because the canon is what adoption installs. For such a repository the driven session is the only one
that can answer, and the surfaces say so. The pipe door keeps its own requirements.

*Amended by D131 (ANSWER1, 2026-10-02): resume is built for one follow-up, a park the person answered, which a real
driven run asked for. Every other start is still a fresh session.*

**Amended 2026-10-01 (LOOK2a): the shell says when its driver is up.** Every route that reads the driver's service
refuses *still coming up* (`DRIVER_NOT_READY`) until the loop hands the service over, and never answers an empty list
for it. The service holds its registry the moment it answers at all, so that handing over is the whole of being ready.
The loop then says `DRIVER_READY` once, and the page asks again everything the driver refused as not ready, and the
driver's own answers. Before, only a tick asked again, and the first tick can wait on a remote's sync or on another
driver's lock (DRV8a): Settings → Workspace said no repository had a line until something else asked. The driver's
state carries `ready`, and the status bar says *starting* until it is true, where it said *ready* from the file alone.

## D47 — The remote is the same host fed by the desktop; transitions write through, records sync (2026-09-20)

**Decision.** DRV3's design is settled: `docs/2026-09-20-remote-design.md` is the mechanism for D45's
part 3. **The remote is a deployment of the existing HTTP host in shared mode** — the same executable,
the same judgement classes — fed by the local desktop's sync loop and never required by anything local.
The split that shapes it: **state transitions write through synchronously or fail plainly; records sync
eventually** — because a lock that is eventually consistent is not a lock, and a session record arriving
late loses nothing. A quest has **one home, decided at publish by whether its receiver is joined**, so
there is never a second copy to reconcile; the cross-machine race resolves by the same stand-down path
DRV2 built. What leaves a machine is governed by **two manifest declarations (join; share knowledge),
silence meaning local** (D21), and the strip is structural: the feed's DTOs carry no field for roots,
transcripts, or private content. Identity is service design §5 built as specified — per-person
per-machine expiring keys for machines, OIDC for people, every route gated in shared mode, and a host
asked to bind beyond loopback without that model refuses to start.

**Git-as-store, priced and declined** (§6/§8.1 asked for the pricing before any host). D45 changed the
question: when quests were passive records, "shared may be a sync" — the driver made them an execution
queue, and a queue two machines race needs an arbiter that refuses the second `take` *before* work
starts, where git surfaces the conflict at push time, after the duplicate session already ran. The
serialization point a lock needs *is* a host — git-as-store hides the deployment rather than removing
it — and the hosting cost it was avoiding was already paid by D36/D43. What survives of the git
argument stays in its original home: the repositories remain the versioned, reviewable source of truth.

**The lock becomes code.** "The quest state machine is the only lock" was honored by convention:
`SetStatusAsync` moves any quest anywhere with only an existence check, tolerable under one machine's
in-order sessions, a duplicated-work generator under two. The build gives quests the transition table
sessions already have — Open→Taken atomic in the store, closed quests immovable — in the shared
judgement class, so local mode gets the same honesty for free.

**Knowledge feeds as content, never vectors** (`model-decoupling`): each deployment embeds with its own
provider, nothing is lost because vectors are not persisted even locally, and a model-less remote still
serves lexical search and says so. Only local-provenance entries feed — canon is already distributed by
`sync`. **Records sync, processes never** (D46 §4 landed): session records feed keyed by origin + id,
controls act only where the driver is attached, and even a cross-machine stop *request* is held open.

**Rejected:** a separately-built remote service (D36 moved judgement to one place so doors cannot
drift; a second implementation is that bug at team scale); an offline queue for quest verbs (an
eventually-consistent lock); OIDC as a hard requirement (one person with two machines gets an API-only
remote with console-minted keys — the platform-from-the-remote arrives with identity).

**Amended 2026-09-20, during the build, on the owner's redesign grant** ("since this app is not been
used yet you can redesign entire application to match"): with nothing deployed, D36's interim
single-key write gate is **retired rather than carried**. The design had kept it for continuity; the
grant removed the reason. Two trust shapes only: local trusts the loopback outright and may bind
nothing else (the startup refusal enforces it), shared gates every route with minted keys.
`DAORIS_SERVICE_KEY` survives solely as the client-side "key I present" — which against a shared
deployment is a minted key. The mid-build finding that forced a second small amendment: publishing
across machines needs the remote's **registry** mirrored down beside its quests — foreign rows only,
because the machine holding a checkout is the authority on its own registration and its root must
survive the sync untouched.

*Noted by D115 (DEV4, 2026-10-01): the remote carries lanes both ways. A quest's `lanes` ride its
publish on the quest wire, written only when there are some, so an older build reads such a quest as
one to the whole repository. The remote keeps them as the publishing machine's exchange judged them,
since its own copy of the registration may lag, and refuses only a lane no registration could declare.
A joined repository's lanes travel with its registration as words, always as a list (`[]` for none),
because a deployment keeps a row's lanes when a registration says nothing of them. A teammate's copy
brings them down, and a change to its lanes alone re-files it.*

## D48 — The workspace is the unit of sharing; a server serves one; the registry is managed (2026-09-20)

**Decision.** Set by the owner, 2026-09-20, in the backlog, under the standing redesign grant (nothing
is deployed): repositories belong to **workspaces**, and everything that crosses repositories —
knowledge search, convergence, the registry, quests, session records, and every remote — is scoped to
one. The mechanism is `docs/2026-09-20-workspace-design.md`; the load-bearing choices, argued there:

- ~~Membership is a manifest field~~ **Amended 2026-09-20, the same day, on the owner's correction:
  membership is wiring, like a git remote — never tracked.** Git tracks nothing about its hosting;
  where a clone syncs is local configuration and who you are there is your credential, which is why
  forks and mirrors work. So: a repository's workspace is a **registry row on the machine** (set by
  the desktop or `connect --workspace`), the **workspace's server is the team's authority** on
  membership (only its keyed accounts can register there — account + key identify membership, the
  git-hosting shape; the account is the key's principal today, the OIDC person later), and **nothing
  about workspaces enters a tracked file**. The manifest keeps only the `remote` disclosure flags: MAY
  is tracked and reviewed, WHERE is the machine's wiring, WHO is the account. Two machines wiring one
  repository to different workspaces is a feature (one repo, two remotes), not a conflict. The first
  draft's manifest field is the rejected alternative: it wrote one deployment's grouping into every
  clone, broke the fork case, and taxed contributors who never run Daoris.
- **Coexistence is binding** (set with the correction): local Daoris works alone with no server, no
  account, no wiring; and a Daoris-adopted repository stays fully workable — agents included — for
  contributors who do not run Daoris. Nothing Daoris adds may sit on a non-user's critical path
  (`daoris check` is the only gate-adjacent piece: zero-dep, offline, npx-pinned), and canon doctrine
  must not hard-require Daoris mechanics — a rule naming a mechanism carries the tool-absent path in
  the same breath (CANON6 audits the existing core under the byte budget's discipline).
- **One shared deployment serves one workspace**, declared as its identity (`DAORIS_WORKSPACE`),
  refusing feeds and registrations that name another. The machine's remote config becomes a map,
  workspace → { url, key } (`~/.daoris/remotes.json`), and the sync loop runs per workspace. Rejected:
  a multi-tenant server — it puts the sharing boundary inside one store and one key space, where a
  scoping bug becomes a disclosure; the self-contained host (D43) makes a second workspace a second
  process over a second file.
- **The registry becomes the authority and the folder scan becomes `import`.** The ghost-repository
  fix already showed scan-as-authority failing, and one root folder cannot express membership that
  does not follow disk layout. The desktop manages add/update/remove — registration lifecycle only;
  files are never deleted, adoption stays the repository's own agent's job, doctrine stays unwritable
  (D31).
- **Remote knowledge sync gets real semantics**: feeds carry git provenance stamped by the driver
  (commit, commit time, branch); only the default branch feeds knowledge (records and quests still
  travel from any checkout); replacement is monotonic by commit time so a stale checkout can never
  clobber a fresher one; the remote serves each repository's fed provenance. Delete stays free —
  wholesale replacement by the newest canonical view. Rejected: per-entry merge (the index is derived
  data; merging derivations invents a second truth beside git) and wall-clock last-writer-wins (the
  flapping this exists to end).

**Why now.** The sharing boundary today is an accident of folder layout — right for one person with
one folder of checkouts, wrong the first time one machine holds two circles' repositories. Nothing is
deployed, so the boundary can be drawn deliberately instead of retrofitted around data.

## D49 — Daoris is the working surface: chat sessions, the live console, managed harnesses (2026-09-20)

**Decision.** Set by the owner, 2026-09-20, in the backlog: a person works *in* Daoris — sees a
session's console live, opens a chat session with an agent (claude code / codex) in any repository
from the platform, and Daoris installs and updates those harness CLIs itself. The mechanism is
`docs/2026-09-20-interactive-design.md`; the load-bearing choices:

- **A chat is a session** — the entity D46 built, entered by a person instead of planned from a quest:
  `Kind: driven | chat`, quest optional, the same observed lifecycle, and the same one-session-per-
  repository lock, because two agents in one working tree corrupt it regardless of who is typing. The
  adapter seam grows one capability, `interactive` (the seam D46 §5 held open); the **harness carries
  the model and the conversation** — Daoris pipes text and still makes no model calls at all (D24).
  Rejected: a Daoris-owned chat loop calling a model API — it would duplicate what every harness is
  and produce sessions with no doctrine path in.
- **The console is the existing transcript capture, teed** to a bounded ring buffer and streamed over
  the shell's IPC bridge only — output is transcript-class material and never leaves the machine
  (D47 §4). A browser and a teammate see the record; only the desktop sees the stream.
- **Harnesses are detected freely and installed/updated only on the person's explicit action**, via
  each harness's own official mechanism, never mid-session — a tool changing under a running loop is
  the moving-target problem one layer down. The session record gains the harness version observed at
  spawn. Rejected: auto-update, and pinning harness versions in the manifest (machine tooling is not
  repository doctrine).
- **Multiple accounts per harness are named credential profiles** (added by the owner the same day):
  each profile is an isolated harness configuration home Daoris owns the *location* of, selected at
  spawn through the environment seam every harness carries; login is the harness's own flow run into
  the profile, so **Daoris never sees, stores, or copies a credential** — it manages directories and
  names. Switching accounts is choosing a profile (machine default per harness, optional default per
  workspace, per-session picker), and the record names the profile at spawn. Rejected: Daoris holding
  tokens itself — a second credential store is a second thing to leak, and the harness already has one.
  *Amended 2026-09-23 by D67 §1: an account that is an API key is kept by Daoris, in `keys.json` under
  the home; a sign-in is still the tool's own store, and never read.*

**What this preserves.** Driving stays additive (D46): outside sessions, hand work, and the browser's
read-only view are untouched. The service stays spawn-free and model-free; processes and streams stay
with the driver; D31/D37/D38's boundaries do not move.

## D50 — Everything is manageable from the desktop or the CLI; files are the API, surfaces are editors (2026-09-20)

**Decision.** Set by the owner, 2026-09-20, cross-cutting D48/D49: every configuration Daoris asks a
person to manage has a proper surface — the desktop app or the `daoris` CLI — with no capability whose
only interface is hand-editing JSON, and none stranded on a machine with no screen (a headless server
running `daoris-driver` is just another machine, D47, and must be settable from a terminal). The
mechanism is the pattern the driver already proved with `driver.json`: **the machine-local files and
the service's doors are the truth; the CLI and the desktop are two editors over the same truth**, and
hand-editing keeps working because the file, not the surface, is authoritative. The command map is
workspace design §2b: `daoris remote|driver|harness` families (file-local or spawning the harness's
own tooling — mostly no network at all), registration verbs beside `connect`, `status --machine`.

**The one structural consequence, made deliberately:** the CLI's offline guarantee evolves from "only
`connect.ts` may contain a network primitive" to "only the named management modules may" — with the
half that matters unchanged and still held by the same transitive-import test: **nothing a doctrine
command reaches may touch the network.** `check`, `sync`, `upstream` and the rest stay offline by
construction; the management class is opt-in and loopback-talking, as `connect` always was. The
zero-dependency guarantee is untouched. *(As built, the list never grew: the management modules reach
the network through **one** module, `service.ts`, and spawn through one, `toolchain.ts`. Each rule is
held by its own test, and so is the transitive one — CLAUDE.md, Conventions.)*

**Boundaries that do not move:** server key minting stays the deployment console's operator act, never
a client verb; quests stay out of the CLI (D32 as amended) — parity there is satisfied by the platform
and MCP; doctrine stays unwritable from every management surface.

## D51 — The tree is the unit of exclusion, and a repository may have more than one (2026-09-21)

**Decision.** SURF1's first question, settled before any screen was drawn (the research named it as a
numbered decision rather than a layout choice). **The session lock keys on a working tree, and a
repository may have several trees** — a registered root plus session trees Daoris creates as linked
git worktrees. The mechanism is `docs/2026-09-21-working-surface-design.md` §2.

**Why this is a smaller change than it reads as.** "One active session per repository" was two claims
welded together. The first — *two agents in one working tree corrupt each other's git state* — is the
reason, and it does not move an inch. The second — *a repository has one working tree* — is not a fact
about git at all; it is a fact about how the registry was built, because `connect` runs in one
checkout and `Registration.Root` holds one path. Unwelding them keeps the guarantee and drops the
incidental cap.

**What it does not license.** D46 §9 stands: *wanting parallelism within one domain is a reason to
split the domain, not the tree.* The planner keeps its own rule — one **driven** session per
repository, oldest open quest first — because pacing a domain and preventing corruption are different
jobs with different reasons, and one mechanism serving both would silently make one answer the other.
What the second tree buys is **the person and the driver coexisting in one repository**, and the
person is not a second workstream; they are the operator. Parallel driven sessions, if real use ever
argues for them, are a change to the planner and not to the ledger.

**The rules that make it safe**, each argued in the design: the registered root is the only tree that
feeds knowledge, so WSP4's provenance model is untouched and a session branch is simply not the
canonical line; Daoris owns the *location* of a session tree and git owns its contents, the same
arrangement as a credential profile (D49 §4); a tree exists only on request, so silence means today's
behaviour byte for byte; a fresh tree holds nothing git does not track, and **the sentence that creates
it says so**, because the missing dependencies are the real price of this decision; the clean-tree rule
stays on the registered root and is vacuous in a fresh one, which is how a person's work in flight
stops holding the driver without a session ever being entangled with it; nothing merges itself and
nothing deletes itself; `connect` from a linked worktree is refused naming the main one, because a
registration re-pointed at an ephemeral tree keeps working right up until that tree is removed; and a
tree path is machine-local material carrying the transcript's three guards (D47 §4), since a path
leaks through whichever half somebody forgot.

**Rejected: keeping the repository as the unit.** Coherent, free, and already honest in its refusal —
rejected for what it caps permanently: the working surface's central act refused whenever the driver
works, the repository being actively edited undrivable forever, and "concurrent sessions" collapsing
into "concurrent repositories", which D46 already shipped. Nothing is deployed, and the unit of
exclusion is the worst thing here to retrofit once records exist — D48's "draw the boundary
deliberately rather than around data", applied one layer down.

**Rejected: a container per session** — the field's other answer. It isolates the toolchain as well as
the filesystem, which is more than the problem needs and more than this family can carry: every
harness, every credential profile and every repository's gates would have to live inside an image
somebody maintains. Git's own mechanism composes with what is already here; an image replaces it.
**Rejected: one directory switching branches** — it serialises exactly what needs to run at once and
destroys the person's working state on every switch.

## D52 — The desktop becomes a working surface: Work is a view, the stream is promoted, review is a diff (2026-09-21)

**Decision.** SURF1 is settled: `docs/2026-09-21-working-surface-design.md` is the contract for the
owner's direction of 2026-09-20 — code sessions held the way a terminal agent CLI holds them, across
agents, repositories and concurrent sessions. The load-bearing choices:

- **Work is a sixth view, not a second application.** The console's five views answer questions a
  working surface does not, so they stay; D41's language stays whole, and only Work's *shape* is new —
  it is the one view that breaks the reading-width cap, because it is for watching. Inverting the
  shell around sessions would produce a second visual language inside one app within a week, which is
  what D41 exists to prevent.
- **Session identity is derived, and grouping is by repository.** Tab overload is the field's named
  anti-pattern; identity is carried by what a session is *for* — repository, kind, the quest's title
  or the conversation's first line, state, age. Hand-naming is deliberately deferred until two real
  sessions cannot be told apart.
- **The stream is promoted out of the drawer, and it gets one home.** Mid-run visibility is the
  strongest empirical claim in the research (3× abandonment without it, at identical output quality).
  Beside it sits a **timeline of what was observed** — state and quest transitions, tool and account,
  commits landing. **Rejected: parsing the stream into steps** — the field's activity panel assumes
  structured progress events, Daoris has none, and the only way to get them is to screen-scrape
  another program's stdout, which is the coupling D23/D24 exist to prevent.
- **`AwaitingPerson` finally has a surface** — the warn treatment, its analysis at the top of the
  session's head, and exactly the three moves the ledger already allows. No new states: an invented
  one would be a second lifecycle to keep in step with the first. Attention also gets Overview's
  *what needs you* band, two sidebar counts, and an **OS notification on park and on end** — never for
  an ending the person caused — which closes driver design open question 5 as the shell's own code.
- **Review is a diff, computed where the tree is and carried over the bridge** — machine-local
  material, desktop-only for the same reason as the console (D47 §4); a teammate still reads the
  evidence string, because the record is what syncs. Merge and discard are the person's explicit acts;
  merge is local and reversible but stays a press because it is where D37's verification lands, and
  discard confirms and names what would be lost. **Not in scope:** line comments, review threads,
  hunk-by-hunk approval — the reviewable record is the repository's own history.
- **No PTY.** A transcript with an input box (SES2) is kept; terminal emulation is a large permanent
  commitment to recover an affordance that already has a better door — the person's own terminal via
  `daoris-driver chat`, the same reasoning that gives `daoris harness login` the terminal outright.
  Held, with the trigger stated: a supported harness whose interactive output proves unreadable over a
  pipe in real use.

**What this preserves.** D38 (Work's live half is structurally desktop-only; its records render in a
browser), D31 (doctrine unwritable), D37 (**a better approval surface must not widen autonomy** — the
research's progressive delegation is declined by name, because approval fatigue trains the reviewer
and a surface that learns from a trained reviewer learns the wrong thing), D24 (no model named), and
D41's tokens, status palette and accessibility rules. The build order is five session-sized items,
SURF2–SURF6.

**Amended again 2026-09-21 (owner, mid-arc): the working surface takes deepseek-harness's UI/UX
structure.** [deepseek-harness](https://github.com/deepseek-ai/deepseek-harness) is DeepSeek's MIT
agent console — a shipped answer to exactly this product — and its *structure* is adopted whole:
the three-column frame with its field-tested geometry, **one docking surface per attended session**
(where the timeline and SURF6's diff live), trajectory-beside-conversation with the follow-the-tail
rule, and approvals as in-flow cards. The full adoption, with the geometry numbers, is
`2026-09-21-working-surface-components.md` §3a. **Deliberately not taken:** its plugin runtime
(Cordis) — their plugins show which surfaces matter, and those surfaces are built natively against
claude/codex through the adapter seam (D23) rather than generically against a provider registry; its
visual identity (D41 does not move); its model settings (D24) and workspace picker (D48). Structure
carries no licence obligation; any ported code brings the MIT notice with it in the same commit.

**Amended 2026-09-21, the same day, on the owner's direction — the surface is built component by
component.** "This is a large UI/UX as a whole, so develop it component by component — more of an
atomic design pattern — so each part can be tested one by one." The method is
`docs/2026-09-21-working-surface-components.md`, and it changes the build rather than the design:
SURF4 becomes four items cut along the layers (atoms and helpers, the rail, the attended session, the
view), each part gets a story before it exists and its own test, and **a molecule imports no hook** —
the rule that makes every state reachable by passing props, asserted by a test so it cannot quietly
stop being true. What was deliberately *not* adopted is the folder taxonomy: `atoms/molecules/organisms/`
scatters one feature across three directories and starts a taxonomy argument on every file, so the
layer is the dependency rule and the surface's components sit together in `src/work/`. Nothing about
D41's language or D42's stack moves — stories import the shipped components, so design and product
cannot drift.

## D53 — dsh is adopted as a protocol, not a product: ACP becomes the driver's session door, and the surface stays Daoris's (2026-09-21)

**Accepted by the owner 2026-09-21**, as proposed and without amendment. Recorded because the shape of
the decision is unusual: what it *rejects* is the expansive half, so accepting it is the conservative
act. Option D would have reopened D1 — Daoris is process tooling, not an LLM product — and declining
it keeps D1, D38 and D41 exactly where they were. What it commits to is additive and reversible: one
more door on a seam that already has one, proven by a stub before any harness rides it.

**Decision.** DSH1 ran the plan's probes (`docs/2026-09-21-dsh-evaluation.md`; every claim
below cites an observed run) and closes the dsh direction with **option B, with A folded into it**. The
adapter seam grows a **protocol door**: the driver may hold a session over the **Agent Client Protocol**
(ACP v1, JSON-RPC over the spawned process's stdio) beside the pipe door it has today, and every harness
reached that way is a *configuration* of the door rather than a hand-built `ISessionAdapter` — dsh
natively (`dsh --profile acp`), Claude Code and Codex through the ACP project's Apache-licensed
adapters. **Options C and D are rejected.** D would have reopened D1 and is declined on exactly those
terms: Daoris is process tooling, and the family layer has nothing to gain from living inside another
product's plugin runtime. The evaluation's job was to make a decision with D1-sized stakes at its far
end cheap and well-lit rather than to make it; **SURF4a–d are unblocked by this entry** and resume on
`Daoris.Web` as designed.

**Why B.** The wire was driven, not read about: a session over dsh's ACP profile streamed a **tool
lifecycle with ids, inputs and outcomes, the turn boundary, thoughts and context usage — by contract,
with nothing parsed from stdout** — which is the structured source the working surface's timeline
needed and D52 rightly refused to screen-scrape. Turn-taking becomes a real API (`session/prompt`)
instead of a line written to a pipe. One wire reaches three harnesses. And the standard does not move
when dsh does: the same week dsh changed 1,687 commits and 28 packages, its ACP surface stayed ACP v1.
The Claude adapter was tested keylessly on this machine: `initialize` and `session/new` succeeded with
**`CLAUDE_CODE_EXECUTABLE` pointed at the `claude` the toolchain already manages** and
**`CLAUDE_CONFIG_DIR` honoured** (Claude Code's state landed in the empty scratch profile and nowhere
else), and it exposed Claude Code's permission modes — `acceptEdits` among them — as ACP modes. So the
door can run the managed binary, under the chosen credential profile, at the driver's posture.

**Why not A alone.** Every mechanic a third-harness adapter needs was observed working (`dsh headless`
took a target, exited 0/1 by turn outcome, streamed JSON events, landed a commit, wrote a durable record,
failed closed on escalation with no approver composed). It adds a multi-provider agent option and changes
nothing structural; its interactive half *is* ACP. A is therefore a configuration of B, not a second seam.

**Why not C.** dsh's web profile is a full product — a model picker (D24), a workspace picker (D48),
plugin management, its own visual identity (D41), a bound port and two default-on rows that send
transcript-class material off the machine on its official route (D47 §4) — on a developer preview whose
README promises breaking changes and whose tree moved 1,687 commits in the week measured. D38's one UI
would become two in fact, and D31/D47 would need re-proving inside someone else's host at every upgrade.
Its *structure* was the part worth having, and D52 as amended already took it.

**What does not move.** Session state is **observed, never self-reported** (D46): ACP updates and dsh's
session log are richer than observation and may *enrich* the record and the console; the record still
moves on exit code and quest state, which matters because the wire flattens `aborted | blocked | error`
to `end_turn`. **No model is named** (D24): the model is the harness's configuration — dsh's own
`settings.yaml`, the adapter's profile — and the record reports the exact route the harness wrote. **The
stream stays on the machine** (D47 §4): the structured stream is console-class, desktop-only. **The tree
is the unit** (D51): `session/new` takes an absolute `cwd`. **The D37 boundary does not widen** (D52's
rule): `session/request_permission` is answered by the driver from the repository's checked-in posture
and fails closed; dsh itself has no notion of *outward-facing* — a push is a sandbox-legal command in its
vocabulary — so that line stays where it is today, in the repository's hooks and the prompt. **D23
evolves rather than breaks**: the seam gains a door; "on proof" now means the first real driven run over
ACP, which this session could not spend for (no key, no login handed to it) and which is ACP2's closing
step.

**Rejected within B.** Binding the driver to `dsh headless --json`'s event shape or CLI flags — product
surfaces that changed in 12 commits in the week measured; bind to the standard. Treating ACP updates as
lifecycle — they are self-reports, and the wire hides the honest end reason of an aborted turn. Adopting
dsh's shipped composition unpatched — the two outbound rows are patched off in the profile Daoris owns
the location of, or accepted out loud. Carrying dsh's headless surface as a separate adapter — it would be
the one adapter with no conversation.

**Two facts to carry into the build, both found by running.** dsh's credential scrub strips any child
environment name containing KEY, TOKEN or SECRET (`GIT_CONFIG_KEY_0` vanished while `GIT_CONFIG_COUNT`
survived; dsh's own source says so) — `DAORIS_*` is unaffected, and nothing an adapter passes may be
named like a secret. And on Windows without PowerShell 7, a Claude Code hook that blocks by **exit code 2
does not block** under a PowerShell 5.1 executor, which collapses a native command's exit to 1 — the
structured `permissionDecision: deny` form does. An adapter promising "the repository's own configuration
governs" carries that caveat until PowerShell 7's behaviour is verified.

**Consequence.** The build order in the evaluation note §5: **ACP1** (the protocol door, proven by a stub
ACP agent in the family rehearsal with no model — D46 §8's shape), **ACP2** (`claude-code` over ACP: the
adapter pinned exact as a managed toolchain entry, the executable and config-dir seams, the permission
answerer, and the real driven run), **ACP3** (dsh and codex as configurations — HARNESS2 closes into it).
SURF4a–d resume on `Daoris.Web` as designed once the owner confirms, with SURF4c's timeline reading the
protocol. DOCS1's strand produced one item, **DOCS2** — built 2026-09-21, and it corrected two claims
this entry made from dsh's list rather than from Daoris's own code: the **link check already existed**
as a devkit gate with eight tests, and the doc budget landed in `tools/` rather than the devkit,
because nothing runs the devkit binary over this repository and a gate that does not run is worth
nothing.

## D55 — The desktop is a code-gen-driven IDE, and the platform gains a second frame (2026-09-21)

**Decision.** Set by the owner, 2026-09-21, mid-build of SURF4b: *"the desktop is becoming more a dev
ide (but code gen driven)"*, with the method note that came with it — *"you should reference more
existing application for designing the ui/ux"*. The evidence gathered in answer is
`docs/2026-09-21-ide-reference-study.md`; this records what it settles.

**Daoris.Desktop is positioned as a development environment whose organising object is a session,
not a file.** Not an editor: the person sets targets, attends running work, and verifies outcomes —
so the frame holds a rail of sessions, an attended session, a growable output panel, a review
surface, and a status bar, and it holds **no text editor at any point in the plan**. That is what
*code-gen-driven* means here, and it is the reason the reference class is IDEs rather than dashboards
while the *contents* of the frame stay Daoris's own.

**The platform gains a second frame rather than a sixth nav item** — the study's fork, answered B.
*Manage* (Overview, Quests, Projects, Convergence, Search, Settings) and *Work* are **peers**, with a
mode switch between them, which is the arrangement Cursor 3 shipped in April 2026 and described as an
interface "centered on agents instead of files". One bundle, one router, one component language: D42's
stack is untouched and **D38 survives as written** — the platform is still the only UI, now with two
frames rather than one. Over a keyed remote the Work frame is **not rendered at all**, the same rule
already holding the stream, the diff and a tree path (D47 §4), so the browser is unchanged and
Playwright keeps asserting the absence rather than the presence.

**Why the sixth nav item had to go.** Five patterns recur in nearly every reference and Daoris's
platform has none of them: the list of running things is a first-class always-available region; one
selection binds every other region; output lives somewhere you can grow and hide; ambient state lives
in a status bar; review is its own aggregated surface with a per-unit done mark. A nav item inherits
the management chrome, and a rail plus a stream plus a timeline does not fit in a content column
designed for cards. The item was not too small — it was the wrong *kind* of thing.

**What it costs, concretely.** SURF4d changes shape (the Work frame and the mode switch, not a nav
item and last-view memory); three regions join the inventory (status bar, panel, mode switch);
`SessionRow` gains *where it runs* and *elapsed*, both already in the record; a **per-session** "own
tree" control joins the per-repository one, because that is how the need actually arrives; SURF6
becomes a multibuffer review rather than a tree plus a pane; and a command palette becomes a real
item rather than a nicety. **Nothing built is wasted** — SURF4a's atoms and SURF4b's molecules are
frame-independent, which is exactly what "a molecule imports no hook" bought.

**What the positioning does not license.** No mid-run approval gate, however many references offer
one — D37 and D52 put the person at the target and the outcome, and an approval surface is how
autonomy widens by accident. No model racing inside a repository (`/best-of-n`), because D23 gives one
domain one owner and the premise of racing is that nobody owns it. No writable doctrine (D31). And no
editor, which is the boundary that keeps this a driver rather than a fork of somebody's IDE.

**The window itself is part of the frame** (owner, same session: *"you probably can use shenora
frameless window and setup custom top menu and also we might need multi window for monitoring too"*).
Both are capabilities `Shenora.Windows` 0.16.0 already ships, read from its own API documentation
before deciding — so this is adoption, not invention.

**a. Frameless chrome, and the top strip becomes application chrome.** `OptimizedForm` with
`FramelessChrome`: no OS title bar, native side and bottom resize borders kept, a manual work-area
maximize, the DWM border coloured to the app's edge, rounded corners while windowed. The strip that
was the title bar becomes **the app menu, the Manage ⇄ Work mode switch, the workspace scope and the
caption buttons** — which is what every IDE in the study does, and what makes the mode switch
reachable from everywhere without spending a nav row on it. Four things the framework's documentation
names as traps, each a reason to adopt rather than hand-roll:

- **`Form.WindowState` and `Form.RestoreBounds` lie about a frameless window.** The maximize is a
  manual work-area `SetWindowPos` that keeps `WindowState.Normal`, so `IAppMaximizable.AppPlacement`
  and `AppRestoreBounds` are the truth. Reading the WinForms properties instead persists
  *maximized: false* together with the work-area rect — the next launch fills the screen believing it
  is not maximized, and **restore becomes a permanent no-op**. The existing `WindowStateHostOptions`
  wiring already reads the right ones.
- **`WindowCommandModule` must be mapped late**, from where the window is created, because it needs
  the live form — never from the DI configure callback. Its module name `SHENORA.WINDOW` is reserved;
  the client half is `WindowCommands` in `@shenora/react`.
- **The page reports where it drew the caption buttons** (`SET_CAPTION_BUTTONS`, CSS px converted to
  client px), which is what buys Windows 11 **Snap Layouts** on the maximize button — a page-drawn
  button never gets it otherwise.
- **`SET_THEME` is not optional here.** Daoris follows the OS theme, and without it a runtime
  light↔dark switch leaves the DWM border and the form fill in the old theme.

**b. Multi-window monitoring.** `SecondaryWindows`: named windows, each on **its own STA thread with
its own message pump**, one window per name, re-opening a name activates it rather than duplicating
it, geometry persisted per name through the same window-state stack. That is what lets the study's
first recurring pattern — *the list of running things is always available* — be true **while the main
window is in Manage, on another screen**. Two to start: **`monitor`**, the rail plus live streams,
read-only; and **`session:<id>`**, one attended session detached. Both are routes into the same
bundle, so they are the same components in a different window.

**The constraint found while reading, recorded before it is rediscovered.**
`WindowCommandModule` targets exactly one form and its module name is reserved and singular, so a
*frameless* secondary window would need a second dispatcher or a per-window module, neither of which
the framework offers today. **Secondary windows keep their native frame**; frameless chrome is the
main window's alone. That is also the honest division — a monitor window is a utility, and OS chrome
is what a utility should wear.

**Rejected: frameless everywhere.** It would look consistent and it would mean either reaching past
the framework's one-window contract or maintaining a second IPC dispatcher for chrome alone — a lot
of machinery so a utility window can lose its close button's OS behaviour.

**Rejected: a tray-only monitor** (`TrayIcon` is right there). A tray icon answers *is anything
running* and the question is *what is each session doing*, which needs a surface. Worth keeping for
SURF5's notifications, where the question really is the first one.

**Rejected: a separate monitoring application.** Same components, same bundle, same bridge — a second
executable would duplicate all three to gain a window the framework already opens.

**Rejected: a desktop-only IDE shell hosting the platform's views as tool windows** (the study's
option C). It is the most faithful reproduction of a real IDE and it costs a second frontend — and
worse, it re-opens D38 by making the platform one guest among several. The management views are not
tool windows; they are the other half of the product, and a frame that demotes them is describing a
different application.

**Rejected: keeping Work as a sixth nav item and simply making it full-bleed** (option A). This was
the standing plan and it is a real option: less work, no new concept. It fails on region 3 and 4 of
the five — a full-bleed view can hold a rail, but it cannot hold a panel you grow across the
application or a status bar that stays true while you are in Quests, because both of those are
properties of the *frame* and a view does not have one.

**Rejected: adopting the reference's verbs along with its geometry in review.** Zed's multibuffer is
the right shape for SURF6 and its keep/reject per hunk is not: Daoris reviews work a session has
**already committed**, so the moves are *accept* and *send it back as a quest* — the latter being the
one thing Daoris has that an editor does not, and the reason it never reaches into the repository to
fix what it is reviewing (D32).

**Rejected: deciding this without a second study.** Worth recording because the first study
(2026-09-20) was good and still missed it: it surveyed session managers, which was the right class
for the question *as then framed*, and the framing was what was wrong. A reference class is chosen by
the framing, so it has to be re-chosen when the framing moves — which is the generalisable lesson,
not anything about IDEs.

## D56 — The desktop gets an application frame of its own: an activity bar, an app strip, and one denser scale (2026-09-21)

**Decision.** Set by the owner, 2026-09-21, after SURF5a landed: *"I still dont see good design for
the desktop app itself"*, and confirmed the same day against three stated forks. The brief is
`docs/archive/2026-09-21-desktop-design-brief.md`; the contract is
`docs/2026-09-21-desktop-frame-design.md`; this records what it settles.

**The diagnosis is an allocation, and it was measured rather than eyeballed.** `npm run desktop --
run`, then `shot` and `eval` against the real window — the only instrument that can see any of this.
On a 1267 × 765 CSS-px window: Manage's nav column holds **240px of content in a 738px column**, so
~440px of it is empty in Work while all six of its items belong to the other frame; `StartSession` is
a **permanent 287 × 200 form** at the rail's top; and the attended session — *the organising object of
the whole application* (D55) — reads in a **365px scroll box**. **42% of the width is navigation and a
form, and the session gets about 28% of the window.** The regions SURF4 and SURF5a built are right.
The frame around them is still the management console's, because every default in it is a console
default that Work inherited without anyone choosing it.

**The window gets an application frame.** A 36px **app strip** holds the wordmark, the Manage ⇄ Work
mode switch and the workspace scope — everything true in both frames — with room reserved at its right
for the caption buttons SURF7 draws. A 48px **activity bar** carries Manage's six domains as icons,
**identical in both frames**, so a domain is one click from anywhere and clicking one in Work switches
to Manage on it; that is what "peers" has to mean if it means anything. The labelled 15rem sidebar is
**retired from both frames**, not hidden in one, and its foot redistributes by what each thing *is*:
the workspace scope is global chrome and goes to the strip, the tier pill and the index count are
ambient state and go to the status bar (which serves D24's "stated on every screen" better than a
sidebar does, being on every screen by construction), refresh and language are actions and sit at the
bar's foot.

**`＋` replaces the permanent form**, opening `StartSession` in D41's drawer — the design language's
single detail-and-form surface, already built and already accessible. Every reference in the study
puts *new* behind one control.

**The verbs get one owner at a time.** A parked session today renders the attention band's `finish it
· decline… · stop it` and the composer's `send · finish · stop` **simultaneously**, 400px apart. From
here the state decides: `awaiting person` gives the moves to the band and leaves the composer `send`
alone; live gives them to the composer and renders no band. No capability is lost — every move stays
reachable wherever it is legal, from exactly one place.

**The attended selection is remembered**, alongside the mode. `daoris.mode` survived a restart and
`attending` did not, so relaunching into Work landed on *Nothing attended* while a session sat parked
— the one arrangement SURF5a's whole attention half exists to prevent.

**D41 §3 is amended, application-wide: one denser scale.** Body **0.95rem / 1.55 → 0.8125rem / 1.45**
(15.2px → 13px), the type scale restated, spacing gains **2 and 6** to the 4px scale, nav rows 50px →
30px. Both frames and the browser, because the platform is one UI (D38) and two densities in one
bundle is how a design language forks. **This is an amendment, written down, not a drift** — which is
exactly what the brief asked for. What it does not touch: the warm paper, the thin ink, the one bronze
accent, and the **validated status palette** — all four states in both themes with their six-check
validation intact, status still never colour-alone, accent still never a status. **D41 §6 constrains
the number**: hit targets stay ≥ 28px, which is why the row lands at 30 and not 26.

**The landing is split, and the cost of splitting it is stated.** SURF10 builds everything page-side
and verifiable by the vitest loop, the stories and the real window. SURF7 then takes the window
itself: `OptimizedForm` + `FramelessChrome`, the drag region and caption buttons *in the strip this
landing built*, `SET_CAPTION_BUTTONS` for Snap Layouts, `SET_THEME`, `WindowCommandModule` mapped late
from where the window is created, and `IAppMaximizable` read rather than `Form.WindowState`. **Until
SURF7 lands the window shows the OS title bar and the app strip — two bars.** That is recorded here so
it is read as a known interim rather than reported as a regression.

**Rejected: keeping the labelled sidebar in Manage and collapsing it only in Work.** It keeps
discoverability where the domains are read most. It costs two sidebars kept in step by hand, and the
two frames then look like two applications — which is the complaint one level up.

**Rejected: a dense scale scoped to the Work frame.** Smaller, safer, touches no decision. It makes
switching frames visibly change the type size, and the desktop *is* the platform — a frame boundary is
not a reason for a second design language.

**Rejected: fixing the allocation and leaving the type alone.** A real option: it addresses every
measured number above without amending anything. Rejected because the brief's density bullet would
close unanswered, and because the 365px box is partly *caused* by the scale — at 13px the same column
holds a third more.

**Rejected: a popover for `StartSession`.** The reference arrangement, and new machinery for a form
D41 already has a surface for. If the drawer proves wrong for three fields in a frame whose right side
is the attended session, that is a later correction with evidence behind it.

**Rejected: folding SURF7 in.** It would avoid the two-bar interim entirely, and it doubles a
session-sized landing while putting the riskiest half — frameless chrome, Snap Layouts,
`IAppMaximizable`, all of which only `shot` can verify — in the same run as a design pass.

**Rejected: deciding it from the screenshots alone.** Worth recording because the brief was written
from exactly those and still under-described the problem: it named the empty column and the permanent
form, and it did not have the 365px, the 42%, the doubled verbs or the unremembered selection. A
capture shows what is wrong; `eval` says by how much, and the number is what turns "it feels like a
web page" into a decision someone can disagree with.

**Amended 2026-09-22, building SURF7: the WINDOW paints the caption buttons, not the page.** D56
reserved room in the strip "for the caption buttons SURF7 will draw", which assumed the page draws
them and reports their rectangles for the hit-test. Reading the framework before building showed what
that costs: **claiming the hit-test makes Windows treat those rectangles as non-client**, so the page
stops receiving every mouse event in them — CSS `:hover` never fires, clicks never reach React, and
hover state has to come back over a separate channel, which is a channel that exists for no other
reason. `OptimizedFormOptions.NativeCaptionButtons` inverts it: the window cuts the reported
rectangles out of the WebView2 and paints there itself, the page's whole job becomes reserving the
space, and Snap Layouts is the same either way. **The reservation D56 already built is exactly what
that needs**, so nothing about the strip changed — only who fills the gap. The colours stay Daoris's
(`CaptionButtonColors` from D41's tokens, re-sent on every theme change), which keeps the rule that
structure may come from a reference and identity may not. Recorded as an amendment rather than folded
in silently, because the original sentence is what a reader would otherwise build against.

## D57 — The toolchain becomes Daoris's, usage is measured before it is managed, and breadth is more native adapters rather than a registry (2026-09-22)

**The owner named three gaps** (2026-09-22): no proper credential management across claude/codex and
other LLMs, no multiple-account handling with usage management, and *"we still don't have managed cli
(still reading from the machine)"*. The design is `docs/2026-09-22-toolchain-design.md`.

**The measurement came first, and it corrected doctrine.** CLAUDE.md said *"the toolchain is
Daoris's"*. What is actually Daoris's is the **accounts** — `~/.daoris/harnesses/<harness>/<profile>/`
is a real per-account configuration home with a machine and per-workspace default and a probed login
state, so multiple Claude accounts already work. The **binary** is the machine's: `install` runs
`npm install -g` into the global prefix and `binary: ['claude']` resolves off `PATH`. There is no
usage of any kind, and the one structured signal that already arrives — ACP's `usage_update` — was
being rendered into a transcript line and discarded. The overclaim is what let the gap hide, which is
`claims-need-checks` in its usual shape: the claim and the mechanism were written at different
moments and only the claim was easy.

**a. The toolchain becomes managed, additively.** A managed harness lives under
`~/.daoris/toolchain/<harness>/<version>/`, installed by the harness's own installer aimed there
rather than at the machine. Selection at spawn is **explicit command → managed pin → `PATH`**, and
the pin resolves exactly as a credential profile does (pick → workspace → machine → none). Absent
means `PATH`, which is today's behaviour byte for byte — the same additive shape as session trees and
profiles, and what keeps D48 §2a true for a machine that installed `claude` itself.

**b. Usage is measured before it is managed** (the owner's answer to a direct question). The record
and the surface are built from ACP's real numbers first; rotation is designed and held until there is
data showing what exhaustion looks like, so its rule is written against observed behaviour rather
than a guess. 🔴 **Usage is machine-local material**: per-account usage names a profile, and a profile
name is already served only over loopback — so it inherits that boundary, lives under
`~/.daoris/usage/`, reaches a surface over the shell's bridge, and has no HTTP route. **No credential
is needed for any of this, so D49 §4 is not reopened** — which is the whole reason this ordering was
offered.

**c. Breadth is more native adapters, plus the ACP door.** The owner's words: *"more native support
(so its more match its api and interfaces) also with ACP door"*. That is D23 applied more times — a
native adapter per tool worth matching properly, and ACP for tools that speak the protocol, with a
tool free to be both (which is what `claude-code` becomes after ACP2). **It is not the provider
registry the components plan rejected**, so that rejection stands and D24 stands with it: Daoris
names no model and holds no price table.

**Rejected: adopting the reference's credential model.** `deepseek-harness`'s `credentials/` group
stores secrets itself — configuration names a `CredentialRef` and a private local store holds the
value. It was read before this was written. It is the opposite of D49 §4, and the owner's own answer
("measurement first") makes it unnecessary: nothing in parts a–c needs a credential. Declined on
those terms rather than by default.

**Rejected: vendoring a harness.** Certain versions at the price of becoming a distributor of
somebody else's tool, with their licence and their cadence. A pin plus an asserted version buys the
same certainty.

**Rejected: turning counts into money.** A price table per model per provider, maintained here and
wrong within a month — and D24 says the deployment decides the model, so it would be Daoris claiming
to know something it structurally does not.

**Named and out of scope**: spend caps, billing, and anything reading a provider's console. All three
need either a credential or an API Daoris has no business holding.

*Amended by D121 (TOOLS5, 2026-10-01): an agent's session, probe and action are handed the tools' environment before
its binary is resolved, so a bare name is found on the tools' `PATH`. The resolution rule's home gains the tools, each
held to one way rather than layered. A package pin's `npm` is the npm the tools resolve, and `agent install` keeps the
system's.*

*Amended by D125 (TOOL4, 2026-10-02): §b's hold on rotation is lifted. Five observed limits answered its three
questions, and rotation is designed in `docs/2026-10-01-account-rotation-design.md`, which replaces the toolchain
design's §2 part 4 and §6.*

## D58 — An unattended driver stops after three failures on one quest, and the count is derived (2026-09-22)

**Decision.** A quest that has failed **three** times on this machine is **parked**: the driver stops
considering it and says so, naming `daoris driver retry <quest>`. The limit is `strikes` in
`driver.json`, with `0` meaning never park — the behaviour of every machine before this existed. The
count is **derived** from the session records the driver already writes, and a person's restart is a
**mark** rather than an erasure.

**What forced it.** ACP2's first real driven run. A session could not take its quest — the protocol
door hands over no MCP server (ACP4) — so it ended its turn having touched nothing, and **the driver
started the same quest 18 times before it was stopped by hand**, each one a real login. The mechanism
was in the design all along and read as a virtue: *"a spawn that fails leaves the quest `Open` and
untouched — there is no claimed-but-abandoned state to repair"* (driver design §3). It is a virtue,
and an untouched open quest is eligible again on the very next tick.

**Rejected: exponential backoff.** It assumes the failure is transient, and the one that cost 18
logins was structural — a missing tool, which no amount of waiting installs. Backoff would have spent
the same account more slowly and never stopped. What the driver can honestly tell is *how many times
this has not worked*; the judgement about why belongs to the person. That is `autonomous-development`
applied where it bites: **a step needing a human choice surfaces as a decision, not a pause**, so the
refusal carries the count and the verb.

**Rejected: a tally the driver keeps.** Simpler to write and a second register — the shape that has
bitten this family twice (a `Save` that deleted the harness pin; profiles before it). Derivation costs
one extra read per tick and cannot disagree with the record.

**Rejected: parking as a quest state.** It would have made a machine's decision about its own spending
visible to the whole family, and the driver writing quest state is exactly what design §2 forbids.
Another machine, or a person, must still be able to take a quest this one gave up on.

**Rejected: a per-repository limit.** A repository with one broken quest has to keep working the rest,
so the limit is per quest.

**Why derived.** A tally the driver kept would be a second register, and this family has been bitten
by those twice (the harness pin a `Save` deleted; profiles before it). The records already say what
happened; counting them is the same move as the timeline being derived because the record has no
event log. Only `failed` counts — a stand-down is the race resolving as designed, a decline is a real
answer, a stop was the person, and a held repository never spawns at all, which makes "a held tick
must not count" **structural rather than a rule somebody has to remember**.

**Why a mark, not a reset.** `daoris driver retry` records the failure count it was restarted at, so
the next three park it again and the history still reads true. A reset would make the derived count
disagree with the records it was derived from, which is the property that made deriving worth doing.

**Three consequences worth stating.** Parking is **this machine's decision about spending**, not a
quest state — the driver still never writes one (design §2), and another machine or a person can take
a parked quest freely. **Absent `strikes` means the default, not off** — the opposite reading from
`notify`, because a `driver.json` that predates this field belongs to the machine that has been
driving unattended longest. And the limit is per quest rather than per repository: a repository with
one broken quest must keep working the rest.

*Amended by D104 (DRV8, 2026-09-30): a `stopped` record that says it was interrupted — the orphan sweep's
or a shutdown's, not the person's — counts as a strike too. The person's own stop still never does.*

*Amended by D125 (TOOL4d, 2026-10-02): a `failed` record that says `limit` — an account's limit refused its turn —
is not a strike. The account cools until the reset the agent named, and the quest's carry-on waits for it rather
than being parked for it.*

## D59 — The always-loaded tier lives in `AGENTS.md`, inside a region Daoris owns (2026-09-22)

**Decision.** `sync` stops writing `.claude/rules/` and writes the core rules into a marked region of
`AGENTS.md`, with `CLAUDE.md` carrying a one-line `@AGENTS.md` import. Knowledge and skills do not
move. `docs/2026-09-22-instruction-file-design.md` is the contract, and §4 extends D19's table to a
region. Accepted by the owner on the measurement below.

**What forced it.** Measured across three harnesses (dsh evaluation §6.5): **`.claude/rules/` is read
by exactly one of them.** dsh's own limitations say it is not interpreted; codex's binary pairs
`CLAUDE.md` with *"Migrate skills from … to …"* rather than with loading anything. `AGENTS.md` is the
only file all three load. So Daoris has been shipping an always-loaded tier whose always-loaded-ness
was a property of one agent harness rather than a guarantee the canon made — and a repository driven
through dsh or codex had `repository-owns-its-work` on disk and unread.

**Where the shape came from.** A candidate adopter the owner offered as an example, whose `CLAUDE.md`
is a single line — `@AGENTS.md` — over 133 lines of its own doctrine. The owner read it as *"a common
management style for different agents"*, which is exactly what it is: one file to maintain, one line
per foreign convention. Daoris's contribution is to own a region of that file rather than a directory
beside it.

**Rejected: writing `AGENTS.md` wholesale.** Dead on arrival — the repository that prompted this has
133 lines of its own in that file, and clobbering an adopter's doctrine to deliver doctrine is
self-defeating.

**Rejected: a file of Daoris's own, pointed at.** A pointer only works where the harness follows one,
and `@path` imports are Claude Code's alone. It would have delivered the tier to the one harness that
already had it.

**Rejected: keeping the directory and ALSO rendering the region.** Nothing existing would change and
every harness would get the tier — at the cost of Claude Code loading the largest item in an adopter's
budget twice. The tier is ~24,000 bytes; paying it twice on the harness that already worked is a
strange price for compatibility with a shape that was wrong.

**Rejected: a pointer paragraph naming the rules instead of carrying them.** Cheap in bytes, and it
turns *always loaded* into *always told to load* — the precise failure `skills-workflow` was written
from, since an unread match is indistinguishable from a rule that does not exist.

**D7 is amended and its better half survives.** *The tier is the directory* becomes **the tier is the
location**. There is still no `tier:` field to disagree with, and the always-loaded footprint is still
measurable — a region has a byte count exactly as a directory did, so CANON7's budget keeps working
and keeps meaning the same thing.

**The safety argument is one sentence.** A damaged marker, a reordered pair, or a second region is
**refused, never guessed at** — the file on the other side of that guess is the adopter's own
doctrine, and `file-tool-discipline` already states why computed boundaries take the rest of a file
with them when they are wrong.

*Amended by D117 (LAYOUT3, 2026-10-01): the region stays, and what surrounds it moves.* Under the `agents`
descriptor, knowledge and skills live under `.agents/` rather than `.claude/`; the region, its per-rule
provenance and the root pointer are unchanged. Three things join the file-and-region states of the
instruction-file design's §4: each declared room gets the same pointer, `<room>/CLAUDE.md` holding the import
region (removed, and the file with it when the region was all of it, once the room is undeclared); the roster
gains the mirror sentence and a *Rooms* table, each only when there is a mirror or a room, so a region without
them is byte for byte what it was; and an `AGENTS.md` or `CLAUDE.md` that is a link, or a link held as text, is
refused and never written through, on both descriptors. That last refusal is this decision's *never guess at a
boundary* in another shape.

## D60 — The deployed artefact gets a gate, and it names the host it found rather than pinning it (2026-09-22)

**Decision.** `npm run rehearse:deploy` publishes the desktop shell to a scratch folder and drives
**that** — the seventh declared gate, and the first that runs on Windows rather than beside the
others on Linux. Where the machine legitimately changes the answer, it **reports** rather than
asserts.

**What forced it.** The first deployment found four defects and two of them were invisible from
inside the workspace *because of what the workspace provides* — a host locator masked by a build that
exists on every developer machine and no deployed one, and a transcript decoded through a console
codepage that `daoris-driver` sets and the windowless shell does not. `rehearse` installs and drives
the CLI **package**; nothing did the same for the desktop. Both sabotages were watched failing here:
the locator regression lands the deployed shell on `src/Daoris.Service/…/bin/Debug/`, and the
encoding regression writes `閬撹 鈥?` — `e9 88 a5 3f`, byte for byte the case study's em-dash.

**The one thing a gate cannot control, and what follows from it.** `~/.daoris/bin` outranks a
deployed copy, deliberately: one service, upgraded once for every shell on the machine. But the
profile is **not redirectable** — .NET resolves `SpecialFolder.UserProfile` from the OS token, not
from `USERPROFILE` — so a gate cannot make its own install win that contest. The gate therefore
asserts the weaker, always-true thing (the located host is **not this workspace's build**) and prints
which one it was. That is the assertion that goes red on a real regression and stays green on a
legitimate machine difference.

> **Amended 2026-09-23 (D63).** The installed home is no longer `~/.daoris/bin` — nothing of Daoris's
> lives under the user profile — but `$DAORIS_HOME/bin`, the CLI's `publish:service --install` landing
> place inside the application's own folder. The order and the reasoning below stand with that one
> substitution.

> **Amended 2026-09-23, by the second deployment.** The precedence above was wrong, and this
> paragraph had recorded the defect as a limitation: `--service` published a newer host beside the
> shell, the shell spawned the older machine-wide one, and the gate passed 32/32 while its own
> transcript named `~/.daoris/bin`. **What the install carries now outranks the installed home**
> (`ServiceHostLocator`), a shell published without `--service` still falls through to the machine's,
> and phase 4 asserts the started host is the **install's own** — the non-redirectable profile is
> the decoy that makes the check mean something on a developer machine. `docs/FIX-LOG.md` has the
> mechanism. The two rejections below still stand: neither needed reopening to fix the order.

**Rejected: a `DAORIS_HOME` override so the gate could redirect the profile.** It would make the
assertion exact, and it reopens a question that is the owner's — **DEPLOY4** asks precisely whether
per-install state should join `~/.daoris`, and `~/.daoris` being machine-wide is what makes the CLI
and the desktop two doors onto one machine (D50). A variable added to make a test sharper would have
answered that by accident.

**Rejected: pinning `DAORIS_HTTP_HOST` in phase 4.** It is what `tools/desktop.mjs` does to keep the
dev loop honest, and here it is exactly the variable that skips the code path the defect broke. The
install's own host is proven **separately**, started directly in phase 3 — two questions, two
instruments.

**Rejected: leaving it undeclared because it is Windows-only.** The gate list is checked against the
release workflow by a test, after the two silently disagreed for eight landings; an undeclared gate
is the failure that test exists to catch. The workflow gains a `windows-latest` job instead, which
the devkit matrix already established. ⚠ **That job is unrun** — nothing runs on push here, so 29/29
is a developer machine's result and whether a hosted runner can bring the window up is unobserved.

**Rejected: asserting the served bundle is the install's own.** It is the sharpest check available
and it turns case-study 4d into a gate — and it goes red on any machine whose installed service is
stale, which is a true statement about that machine and not about the change under test. A gate that
fails for the developer's machine state is one people learn to re-run.

*Amended by DEPLOY5 (2026-09-30): the gate starts the install with a debug port, `run --install`'s
own opt-in (`debugEnvironment`) on a port of its own, clear of the dev loop's 9333. The chat left
`working` at close (FIX-LOG, 2026-09-25) lived in the shell's shutdown order, where only the window
saw it, and a chat starts over the bridge, which only the page holds. Phase 6 opens a conversation on
the family rehearsal's protocol stub, now the rehearsal kit's, and closes the shell as before; phase 7
reads the record from the install's host started alone, which runs no sweep, and passes only on the
close's note, since the sweep's is the same `stopped`. The port needs the kit's development switch,
which the shell's host inherits, so the gate sets `ASPNETCORE_ENVIRONMENT=Production` beside it; that
rests on ASP.NET's documented order and is not measured. Rejected: a second start, with the port, for
the conversation alone, which would keep phases 4 and 5 on a person's exact start. It closes the shell
twice, and with the host pinned what is left is the kit's own development mode (the port, and the
engine reading its command-line switches), which none of phase 4's or 5's checks depends on.*

## D61 — Translation parity is a pack, not core (2026-09-22)

**Decision.** The en/zh catalogue lesson enters the canon as **`localized-ui`**, a new pack holding
one on-demand document. Not a core rule, and not a rule even inside the pack. Daoris adopts it, which
is what validates it.

**What forced the question.** CANON5 had been parked on the core budget and CANON7 unparked it —
there is now room for roughly one substantial rule, *and this would spend exactly that room*. So the
budget stopped being the answer and the tier question had to be asked on its merits.

**The merits, and they are one sentence.** A rule every repository loads on every task, to govern a
concern only some of them have, is what the pack tier exists to prevent. Most of this family is a
CLI, a library and a service — **none of them has a user-facing string at all.**

**Measured, because the argument is a size argument.** Adopting the pack here moved the always-loaded
core from 21,817 to **22,171 bytes — 354 for the index row**, against roughly 3,800 had the same
content gone into core. The pack tier is not a filing preference; it is a factor of ten.

**Rejected: a core rule.** The honest version of it, and the one CANON5 named. It would have fit, and
every repository that ships no interface at all would have carried it forever — which is precisely
what D28's "split principle from detail" was protecting, one tier up.

**Rejected: folding it into `web-webview`.** The nearest existing pack, and wrong: that one is about
hosting a web UI inside a native shell — resource serving, thread affinity, caching. A desktop
application with resource files has this exact parity problem and no webview, and a plain web
application would have had to take the shell pack to get it. The concern is **a shipped interface in
more than one language**, not the transport it arrives over.

**Rejected: `rules/` inside the pack.** Narrow — it matters only while touching a catalogue — and it
wants length, which is the on-demand tier's definition.

**Rejected: restating the encoding traps.** Non-ASCII text that renders correctly can still be
destroyed by a console or a redirected stream, and that is real — it cost this repository a defect
the same week. It already has a home in `windows-machine`, so the new document **names the concern
and points away from itself** rather than growing a second copy. Two copies of a rule become two
different rules.

**What it does not cover, deliberately:** choosing a library, a key-naming scheme beyond "structural,
not the default language's text", or how any repository runs its own check. Those are mechanism, and
mechanism belongs to the adopter.

## D62 — The desktop application is the product, and development happens against the install (2026-09-22)

**Decision.** The owner: *"the desktop app itself should be the main focus here since it has most of
the capability also as the host for the machine"*, and *"you should be develop to <install>"*. So:
the desktop is the artefact the rest serves, and the loop that improves it runs against a **published
install** rather than a workspace build.

**Why it is true rather than a preference.** Count what only the shell can do. It carries the
platform, runs the driver loop, brings up and owns the HTTP host, holds the console and the chat, and
is the only surface that can reach a machine-local fact at all — the credential profiles, the
harness roster, the folder picker, the session stream (D47 §4 keeps every one of those off HTTP by
construction). A browser gets the read-only half. The CLI has two doors onto the same files (D50) and
no window. **Most of what Daoris can do, only the shell can do**, and the shell is also what starts
the machine's service. That is what "the host for the machine" names.

**What follows, concretely.** Looking at the real thing is the loop, not a final check. The first
deployment found four defects invisible from inside the workspace (three *because* of what the
workspace provides); the bundle-accumulation defect was found the same way an hour after that; and
the fixture the dev loop runs against has one circle and one account, so it cannot show what a real
machine shows. `run --install` exists for this and the deployment rehearsal (D60) gates it.

**Rejected: keeping the scratch dev loop as the primary surface.** It is faster and hermetic, and it
is a fixture — one workspace, one account, two example repositories. Every UI judgement made against
it is a judgement about a machine nobody has. It stays, because a gate needs a fixture and a scratch
run must never touch a real registry; it is no longer where a surface is *judged*.

**Rejected: opening a debug port in the published application.** It would make every install
inspectable with no flag. A shipped application that exposes a debug port is a shipped application
with a debug port, whoever opens it — so the port is set at launch by the dev loop's environment and
by nothing else.

**What this does NOT reorder.** The CLI keeps its zero dependencies and its offline guarantee (D11,
D35); the canon is still the thing being propagated and the service still indexes it; **every
capability keeps two doors** (D50) — a machine with no screen loses no capability, which is exactly
why the shell may be the focus without becoming the requirement.

## D63 — The application folder is Daoris's home, and nothing of Daoris's lives under the user profile (2026-09-23)

**Decision.** DEPLOY4, decided by the owner and the other way from the recommendation held for it:
*"we should manage files within the app folder instead put it in shared"*, and, on the pointer file
proposed to keep the CLI meeting the same machine, *"you should not keep dump thing into user
folder"*. So: **every machine-local file Daoris owns lives under one home** — `driver.json`,
`harnesses.json`, `remotes.json`, `knowledge.db`, `sessions/`, `trees/`, `usage/`,
`harnesses/<tool>/<profile>/`, `toolchain/`, `bin/` — **and the home is the application's own
folder**, `<install>/data/`, beside the WebView2 profile and the window's geometry that were already
there. `~/.daoris` is no longer a default anywhere, and no artefact writes a file under the user
profile — not a config, not a pointer. *(Amended 2026-09-24 by D73: one write can land there —
`agent trust` moves one flag in the harness's own account file, which is under the profile when no
Daoris profile is named. It is the harness's file, not Daoris's, and moves only on a person's act
that names the folder.)*

**The one seam is `DAORIS_HOME`.** Every default derives from it, in all three artefacts (the CLI,
the service hosts, the driver — three twins sharing no code, the same shape as the remotes map's
three copies, WSP3). Absent, there is **no default**: the management class and the hosts refuse with
one sentence naming what to set; doctrine commands never needed a home and still do not. The per-file
overrides (`DAORIS_DRIVER_CONFIG`, `DAORIS_KNOWLEDGE_DB`, …) still win where set — they are how every
gate stays hermetic, and a scratch run now sets the home too.

**How the two doors still meet** (D50 is untouched). The installed desktop sets `DAORIS_HOME` in its
own process before anything resolves a path — its host and every session inherit it — and, when the
user's environment has no `DAORIS_HOME`, sets one there: a variable, not a file, the way a toolchain's
home is conventionally found. A terminal's `daoris` and a session's MCP host then read the same
machine. It says so once.

**Migration.** An install whose home holds no state, on a machine whose `~/.daoris` does, moves that
state in on first start — the files and directories above, never `bin/` (that is the CLI's own
install, and `publish:service --install` lands the next one under the home) — and says what it
moved. A one-time transition for machines from before this decision; nothing is copied twice.

**Rejected: a pointer file in `~/.daoris`.** It was the smallest thing that would let a terminal find
the home with no environment to read, and it is still a file under the user profile. The owner's rule
is the simpler one. **Rejected: an XDG/AppData configuration directory.** Same folder by another
name. **Rejected: the CLI defaulting to `~/.daoris` "for compatibility".** A default that writes
where nobody pointed it is the thing being removed; a refusal that names `DAORIS_HOME` is the honest
answer on a machine with no application.

**What it changes elsewhere.** D60's "installed home" is `$DAORIS_HOME/bin` (amended in place); the
toolchain design's directory layout is the same tree under a different root; DEPLOY4 closes.

*Amended by D105 (HOME1, 2026-09-30): a `DAORIS_HOME` the install only inherited from the account, naming
another folder, no longer wins over the install's own `data/`. The account's variable is left as it is,
and the window says so. A home named for one start alone is still respected.*

## D64 — A plugin is a folder that declares, and may speak: no code loads into a host (2026-09-23)

**Decision.** The owner's direction, after ARCH1 declined a runtime and named three seams: *"the
plugin is not built at all"*, and, asked what shape it should take, *"you should check
deepseek-harness design for plugin and idea from yaorin."* `docs/2026-09-23-plugin-design.md` is the
design. A plugin is a **folder under the Daoris home** with a manifest (`id`, an integer
`apiVersion` read before anything else, what it declares, what it speaks). It **declares**
configurations of the ACP door — a fifth harness arrives as a file, not as Daoris code — and it may
**speak**: a process of its own, started and stopped with the driver loop, answering typed decisions
over JSON-RPC on stdio at named points. `quest/consider` is a **fail-closed waterfall** whose hold
becomes the consideration's own reason; `session/ended` is contained observation. Two doors edit the
same files (`daoris plugin`, the Machine view); disabled is a row, never a rename; a conflict is
refused before anything loads, naming both sides.

**Why beside, never inside.** Both references load code into their host and then pay for it —
collectible assembly contexts, effects that must unwind, an SDK that is append-only forever. Daoris
has three languages in three artefacts and already chose a protocol over an API for the harness
(D53), and that choice survived the harness moving 1,687 commits in a week. A process on a wire
gives every property the references engineered for nothing: a registration is exactly as alive as
the process (dsh's rule, made structural), any language may write one, a crash takes down only the
plugin, and the host holds no per-plugin knowledge beyond the manifest (Yaorin's catalogue). There
is no SDK to keep compatible; there is a wire with a version.

**What is taken from where.** From deepseek-harness: interception points answered with typed
decisions in a waterfall; registrations as effects; a feature-to-mechanism map that makes the
microkernel claim checkable. From Yaorin: the manifest with an integer API version and declared
capabilities; explicit registration read back as a catalogue the host drives without naming a
plugin; the install folder replaced wholesale and the data folder beside it untouched; a broken
plugin logged and skipped, never fatal. Declined from both: the runtime, the assembly, the plugin UI
bundle, and ordered override layers (a Daoris plugin overrides nothing, so there is nothing to order
but a waterfall).

**Rejected: loading .NET assemblies the way the neighbour does.** It would reach one of three
artefacts, need an SDK package published before the first plugin and kept append-only after, and
put a plugin's crash inside the driver that spends accounts. **Rejected: a script runtime in the
host** for the same reasons in a different language. **Rejected: fail-open at a decision point** —
a policy plugin that timed out and let a spawn through would be the one wrong result nobody sees,
on the one loop that costs money; the hold names the plugin and the person disables it.

**What does not move.** D52 (no plugin views), D24 and D57 (no registry, no model named), D4 and
PLUG2 (core doctrine stays non-optional and is the owner's to reopen), D46 §3 (sitting says why —
now for a plugin's hold too), D49 §2 (a plugin's word is a console line under `plugin:<id>`,
machine-local). ARCH1's three declarative seams stand; this is the fourth, and the first that speaks.

*Amended by D121 (TOOLS5, 2026-10-01): a hook's first word is never handed to the system bare. A name a tool answers
for is the file the tools resolve, and the hook is not started where that tool's way cannot run. Any other name is
found on the child's `PATH` by the agents' resolver and shim rule. The hook process is handed the tools' environment.*

## D65 — A regular task is an ask; the intake is a session; a workflow is a chain of quests (2026-09-23)

**Decision.** The owner described the regular task Daoris exists for — a workspace of real
repositories, a sentence naming a ticket and a change, and Daoris expected to *locate the project,
start the development via an agent, and test via chrome*, with files and URLs sent along, possibly
as a workflow, and *"the local desktop will have its own driver llm"*.
`docs/2026-09-23-intake-design.md` is the design. Five things are decided by it:

1. **The driver's brain is a session, not a model.** An ask opens a conversation (SES2) in a
   working tree Daoris owns under the home, seeded from the registry, with the family's tools; its
   job is to read the ticket, decide the owning repository from the declarations, and publish
   quests. The harness carries the model under the workspace's own credential profile (D24, D49
   §4); with no harness the deterministic tier proposes by declarations and says so.
2. **A quest carries links and attachments.** Links travel with it; attachments are content-
   addressed under the home and machine-local, the transcript's boundary (D47 §4), and a session
   is handed them by name and by a directory in its environment.
3. **A plugin declares MCP servers handed to every session** — over ACP on `session/new` (ACP4),
   over the pipe door by the harness's own `--mcp-config` from a file under Daoris's home, never the
   repository. A browser is such a server (the Playwright MCP, as the harness reference's own
   browser-use providers are), and *testing in Daoris* is a session driving one.
4. **A workflow is a chain of quests**, `then` on a quest published at its close; the driver is the
   engine that already exists. No script engine, no coordinator agent (D1).
5. **Registered is addressable; adopted is disciplined** — proposed: a repository registered with
   a root is drivable over the ACP door, since the connector travels on the wire and not in the
   repository's files. 🔴 This amends the letter of D34/D46 and is **the owner's yes first**
   (INT3). **Decided yes as D70** (2026-09-24).

**Why a session and not a model.** Every path that puts a model inside Daoris reopens D1 and D24
at once: a provider to choose, a credential to hold (D49 §4 refuses), a price to know (D57 refuses).
A session has all of it already — the harness, the account, the tools, the record — and the
intake's answer is then reviewable exactly as a driven session's is: what it published, and why.

**Why no engine.** The harness reference's workflow is a model-written orchestration script; orca's
is a coordinator running task DAGs with decision gates. Both are right for products whose loop IS
the agent's. Daoris's loop is the driver's, and the only orchestration it needs — this, then that —
is a quest that names its follow-up. A decision gate is a parked session asking (SURF5).

**Rejected.** A browser extension (the session drives a browser through MCP). A ticket-system
integration in the host (a plugin declares a server for it). Attachments crossing machines in v1.

**What does not move.** D24 and D1; D32 (the intake publishes, never edits); D37 and D46 §5 (the
session's tool calls stay under the repository's own posture, browser included); D50 (two doors:
the composer and `daoris-driver ask`); D64 (servers are one more thing a plugin declares).

**Amended 2026-09-23, building INT2: the session is TOLD where a file lies, never left to derive
it.** The design had the driver point `DAORIS_QUEST_ATTACHMENTS` at `<home>/quests/<id>/attachments/`.
The driver's home is wherever `driver.json` lives, which is not always `DAORIS_HOME`. The family
rehearsal is one case where they differ, so a driver deriving the path would have handed a session
a directory that is not there. So the layout is `QuestFiles`'s alone. A local host answers a caller
on this machine each kept file's path, only when the bytes are here (the transcript's rule, D47
§4), and the driver sets the variable from that answer. Null is how a mirrored quest's file says it
is elsewhere: to the driver, which tells the session, and to the page. Four more choices landed with
it. A link is an **absolute http or https address**, refused otherwise, because a drawer renders it
as a link and a `javascript:` "link" is a script. A quest carries **at most 10 files and 20 MB
together**, judged once in the exchange, because three doors reach it and a limit two of them
enforced would be a limit the third did not. A **shared door refuses content outright** rather than
dropping it, because dropped looks kept. And the local route that serves a kept file answers
**loopback only and sandboxed** (`Content-Security-Policy: sandbox`, `nosniff`, a download for
anything that is not an image, a PDF or text), because it serves from the platform's own origin, and
an attached HTML file would otherwise run with every route the host answers.

**Amended 2026-09-23, building INT5: a chain is a list, judged when composed, with one home.** `then`
is an ordered list of steps rather than a nested `then` per step, so an agent writes the chain once
as a list. Closing `done` publishes the first step in the **same transaction**, carrying the rest.
Five choices, each with its reason:
- **Every step is asked on behalf of the chain's asker**, because the asker composed the whole of it.
  A step back to the asker is therefore refused as a self-ask.
- **A step's id derives from its parent as well as its words.** Ids are content-derived, and a step
  titled "Verify in the browser" would otherwise collide with any older quest of those words and
  quietly join it. An unchained quest's id is unchanged.
- **Judged when composed, not at the close.** The person or the intake composing the chain can act
  on a refusal; nobody is watching a close.
- **One home per chain** (D47 §5). Each step is published wherever the one before it closes. A
  chain straddling the remote and this machine could only be homed wrongly, so it is refused naming
  both halves.
- **A decline stops the chain**, because a decline is an answer rather than a finish.

`{parent}` in a step's words becomes the parent's id at the moment of publishing, and a chain carries
at most five steps after its first quest.

**Amended 2026-09-23, building INT4a: the ask is a record, and its floor is the declarations.**
INT4 split in three, because its row named three landings: **INT4a** is the ask and the no-model
tier, **INT4b** the intake session, and **INT4c** the desktop door. The floor comes first
(`model-decoupling`: ship what works with no provider, and let the model raise the ceiling). What
INT4a settled:
- **An ask's quests are asked BY the ask.** Their sender is `ask #<id>`, which cannot collide with a
  repository name and leads a reader back to the person's words. The exchange places the quest in
  the ask's own circle (`QuestAsk.Workspace`), because an ask has no registry row to say it.
- **The declarations tier proposes and never publishes.** Word overlap cannot tell a sentence that
  owns a problem from one that mentions it, and a quest published on a guess lands in a repository
  that did not ask for it. The proposal names its evidence, the matched words, and a word a
  repository declares it *owns* counts double.
- **A named receiver the exchange refuses keeps the ask.** The sentence is still what the person
  meant, so the refusal arrives with the declarations' proposal as somewhere to go.
- **Asks are local mode only**, like a quest file's bytes: the intake is this machine's.

**Amended 2026-09-24, building INT4b: the intake is a chat for an ask, off until a harness is named.**
The design's §1b *as built* has the whole of it. Four choices, each with its reason:
- **`intakeAdapter` is off when absent**, not "the machine's adapter". An intake spends a real login
  on every ask, and a machine answering asks by declarations must not start spending accounts on an
  upgrade.
- **The record is a chat**, repository `ask #<id>`, with the circle taken from the ask. An older build
  reads an unknown kind as driven, and a chat as what it is.
- **One intake per ask, and the room's lock is the process.** A second intake would be the loop
  retrying a harness on a question it could not settle. A parked intake has asked and ended, so it
  must not stop every later ask in its circle.
- **"Ask the person" is a park that the person's answer to the ASK ends.** The question stays on the
  transcript, and only the ask's own intake moves its tier to `intake`.

**Amended 2026-09-30, building USE1c: an ask whose work is finished is DONE, and done is derived.** On
the owner's install every ask stayed *published* after every quest it became had closed, so the Asks
list only grew. An ask is now `Done` when it became at least one quest and none of the quests asked
BY it (`ask #<id>`, chain steps included) is open or taken. A step waiting on another repository is
taken, so it counts as open. The default list hides a done ask as it hides a closed one; *include
closed* shows it, and the page reads it with the closed ones. A closed ask stays closed, because the
close is the person's word on it. The same words asked after an ask is done ask anew, for ASKAGAIN1's
reason: that ask ended, and the default list no longer shows it.
- **Derived on every read, never stored.** The desk reads the ask's standing from the quests asked by
  it, so every door (`daoris-driver ask`, the MCP host, the page) sees one answer. A quest can close
  on another machine, and the sync that brings the move here knows nothing of asks, which stay on this
  machine (D68 §2). A stored state would need a write the sync cannot make.
- **Rejected: storing `Done` at the close.** The close that finishes the last quest may happen on
  another machine, or in a rebase. Every place a quest can move would then have to know about asks,
  and a missed one would leave the defect in place. The cost of deriving is one read of the quests
  asked by asks, per list.

## D66 — One bar: Sessions is a view, Settings is a place, and an account is made by signing in (2026-09-23)

**Decision.** The owner looked at the desktop and asked four things (UX1–UX4 in the backlog). Two of
them change a standing decision, and the owner chose the shape of the larger one directly:

1. **One frame, not Manage ⇄ Work** (amends D55 §a and D56 §3a–b). The activity bar was already the
   navigation — the two frame icons at its top were a second navigation stacked on the first. The
   bar is now one list: **Overview, Sessions, Quests, Projects, Convergence, Search**, with
   **Settings** at its foot. *Sessions* is what the Work frame was — the rail, the attended session,
   the dock, the console — as one view among the others. D55's substance stands: the session is
   still the organising object of that view, a timeline is still derived, and the stream still has
   one home. What goes is the mode: nothing is gated behind a switch, and there is no frame to be in.
2. **A settings page** holds the application's own settings — the theme (system, light, dark) and
   the language — above what *This machine* held, which moves inside it. In a browser it holds only
   what a browser may know: appearance.
3. **An account is made by signing in** (amends the SES3/D57 profile rule *"removing one deletes
   nothing"*). *Sign in to another account* opens a fresh profile, runs the tool's own login in it,
   and names the account by who signed in when the tool says (`claude auth status` reports the
   email); a sign-in that does not finish leaves nothing behind. **Removing an account deletes its
   directory**, credentials included — the owner's call: an account a person removes should not
   still be signed in on disk. The tool's own configuration home is never Daoris's to remove.
   **As built (UX1):** the directory keeps a neutral name, the first free `account-N` — it is needed
   before anyone knows whose account it is, and renaming it afterwards would move a home a harness
   may have keyed its credential to. *Who* is the tool's own answer: the login question gained an
   account pattern (the email, for `claude auth status`), read fresh on every probe, taken only on a
   yes, and written nowhere — which amends the probe's "one boolean and nothing else" to "the boolean
   and who"; the organisation and the tier are still never kept. A sign-in is kept when the tool
   exits 0 and does not call that home signed out. A number freed by a removal is reused, so the
   usage ledger, keyed on the name, can total two accounts under one; it is measured, never billed.

**Why.** The frame switch cost a click and a concept for every move between reading and working,
and the bar beside it already did the navigating. The theme was the OS's alone. And an account
that must be named before anyone knows whose it is, then kept on disk after it is "forgotten", was
two steps and a leftover where the person meant one act.

**Rejected.**
- **Two places, Overview and Monitor.** Quests, projects, convergence and search would move one
  level deeper, into tabs inside Overview, and every one of them is a place people go directly. The
  owner chose the flat list.
- **Keeping both frames and only tidying the bar.** That leaves the concept the owner asked to
  lose.
- **CSS `light-dark()` in place of copied forced blocks.** It is one declaration per token, but it
  would rewrite every token. It would also break the desktop's `ChromePaletteTests`, which reads the
  palette block by block, and would buy nothing the equality test does not already guarantee.
- **Removing an account's record and keeping its directory** (the SES3 rule). The owner's call: a
  removed account still signed in on disk is the leftover the person meant to remove.
- **Naming the directory by who signed in** (renaming it once the sign-in ends). A harness may key
  its stored credential to the home's path, so the rename could leave the account signed out; the
  name a person reads is the tool's answer, which needs no second copy to go stale.

**What does not move.** D41's language and D56's density; the status bar; the palette; D47 §4 (a
browser still learns nothing about this machine — Settings in a browser is appearance only); D49 §4
(Daoris never reads a credential — it deletes a directory it made, and never looks inside it).

## D67 — Daoris keeps the key and the higher loop; an agent keeps its own loop; wiring is a map (2026-09-23)

**Decision.** The owner answered the three questions the agents direction left open
(`docs/archive/2026-09-23-agents-direction.md`), in their own words: *"daoris can keep the key"*; *"daoris
already should have a loop but not more like higher level, and agent itself should remine its own and
access other things from daoris via mcp"*; and *"wiring means few things, how daoris agent loop chain
workflow, and how repos wired in the workspace (so a topo map might need to be introduced and this
map will also show for the repo itself for code)"*.

1. **Daoris may hold an API key for an account** (amends D49 §4 for keys only). An account can be a
   key rather than a sign-in, and Daoris keeps it: machine-local under the home, tracked by nothing,
   never on the HTTP surface (D47 §4), never printed back beyond an audit prefix (WSP3's rule for a
   deployment key; *as built, the last four characters, since every key of one maker shares its
   prefix — `docs/2026-09-23-api-key-accounts.md` §2*), and handed to the agent at spawn through that tool's own variable. A **sign-in**
   stays where D49 §4 put it: the tool's own store, which Daoris never reads. How a key is held at
   rest is AGT3's design.
2. **Two loops, and each keeps its own.** Daoris's loop is the higher one: which work, which agent
   and account, what follows (the driver, the intake, `then`). The agent's loop is the agent's: its
   model, its tools, its turns. **An agent reaches Daoris over MCP** (quests, knowledge, asks), and
   Daoris runs no model loop of its own, so D24 stands. An API-driven agent is therefore an agent: an
   existing one on an API-key account (AGT3), or one a plugin declares (D64). AGT4 closes on this.
3. **Wiring is a map, in two layers.** The **workflow**: how Daoris's loop chains an ask through the
   intake, quests, sessions and `then`, with the agent, account and version each step runs on. The
   **topology**: how a workspace's repositories are wired (what each declares it owns and accepts,
   the quests between them, what drives each). Inside a repository the same map shows its **code**,
   which gives the roadmap's long-standing *repository intelligence* its first consumer. It is a
   roadmap arc (MAP), designed before it is built. **Settled the same day**
   (`docs/2026-09-23-map-design.md`): the workspace map is a view of its own, and a repository's
   code map is fed by a tool where one exists and by the repository's agent elsewhere.

**Why.** A key is an account that no sign-in flow holds for Daoris, and the owner wants API-driven
work: Daoris keeping it gives every agent one account shape. An agent with Daoris's loop inside it would be
two agents in one process; an agent that asks Daoris over MCP stays one tool with one loop, whoever
made it. And the owner's word for the wiring was a map, not a form: which repository talks to which,
and how work travels, is a picture a person reads at a glance.

**Rejected.**
- **Daoris running its own agent loop against a model API.** That reopens D24 and makes Daoris a
  second agent beside the ones it drives. The owner kept the agent's loop the agent's.
- **Keeping keys out of Daoris entirely** (the tool's own key login only). The tools differ in
  whether and how they keep a raw key: Codex has a key login, and Claude Code reads a variable. Each
  account would then take a different shape per tool. The owner's call is that Daoris keeps it.
- **Wiring as one more settings form.** The four files are already editable from two doors. What was
  missing is seeing how the parts connect, which a form does not show.

**What does not move.** D24 (the harness carries the model); D49 §4 for sign-ins; D47 §4 (no key, and
no path, crosses the HTTP surface); D32 (a map of another repository's code is read, never written
into — that repository feeds it, as it feeds knowledge).

## D68 — The remote is a git remote: every machine commits locally, sync is fetch, rebase, push (2026-09-23)

**Decision.** The owner: *"the remote share server is more like a git system, and local is the main
driver and we can push/sync/merge/rebase remote so to keep everything in sync, what this gives is the
system works with/without remote share and we can have proper merging logic when there is
conflict"*. `docs/2026-09-23-sync-design.md` is the mechanism. **Every verb commits locally and
always succeeds locally.** A quest is kept as its operations and replayed through the one transition
table. Sync is **fetch, rebase, push** per wired workspace, and the remote orders what it accepts, as
a remote branch orders commits. The owner answered its three trade-offs the same day:

1. **Claim by push.** Before a driven session starts, the take is pushed and awaited when the remote
   is reachable, so there is no duplicate work online. Offline, the take stays local and unconfirmed,
   and the session runs. *(D69 chose the mechanism: the session's own take pushes and awaits, before
   any work. The driver does not take on the session's behalf before the spawn.)*
2. **First push wins, and the loser is kept.** The operation that reached the remote second becomes a
   `conflict` on the quest, with what it attempted and its evidence, for a person. Nothing is dropped
   and nothing is merged into a second truth. A losing session still running is stopped by its own
   machine's driver.
3. **Automatic, and on demand.** Every tick fetches, rebases and pushes. `daoris-driver sync` and
   *Sync now* do it when asked. Ahead, behind and conflicts are shown per workspace.

**Amends D47.** A joined quest no longer has a remote *home*, and its verbs no longer write through
or fail. That rule made offline work impossible for exactly the repositories a team shares. D47's
case against git was that git finds the race at push time, after a duplicate session already ran.
Claim by push keeps that guarantee whenever the remote can be reached. What the owner accepts in
exchange is the offline race, ended by rule 2. **Stays from D47:** the same host in shared mode, the
keys, the two declarations and the structural strip, records keyed by origin, no processes and no
doctrine on a remote, and the one judgement class.

**Why.** A machine is where the work, the checkouts and the person are. A design that stops that
machine's shared work whenever a server is unreachable has the dependency backwards. Git's model is
one every developer already reads: local commits, a remote that orders them, and conflicts that
surface instead of vanishing. The records needed it most. Before this, every tick re-sent full
snapshots, the last writer won on registrations, and nothing was ordered but knowledge.

**Rejected.**
- **Merging two results into one automatically.** Two sessions' work on one quest is two sets of
  commits in someone's repository, and only a person, or that repository's agent, can combine them
  (D32). The loser is kept and shown instead.
- **An operation-based CRDT with no ordering point.** It would merge without conflicts by
  construction, and a `take` is exactly the operation that must conflict. The remote as the ordering
  point is what makes "first" mean something.
- **Git itself as the store.** It was priced in D47 and still loses on the lock. Rule 1 needs an
  answer before the spawn, which a push to a bare git repository can give only by making every take a
  commit in a repository nobody reviews.

## D69 — The take claims by push; the quest sync lives in the hosts (2026-09-24)

**Decision.** The owner, choosing between three mechanisms for D68's rule 1: **the take itself claims
by push.** Every take commits on the machine where it was made. When the quest's workspace has a
remote, the take is then pushed and awaited before the answer returns:

- **accepted**: the quest is now taken;
- **lost** (another machine's take reached the remote first): the take is rebased into a conflict,
  and the answer is the existing *already taken, stand down*, so the session stands down before any
  work;
- **the remote cannot be reached**: the take stays local and unconfirmed, and the session runs.

This holds for a driven session and an outside one alike. So that the take and the tick run one
implementation, **the quest half of the sync moves from the driver into the hosts**. The driver's
tick asks its local host to sync. The driver still owns its processes: a session whose unconfirmed
take later loses is stopped by its own machine's driver (D68 rule 2).

**Amends D68's mechanism, and keeps D46 whole.** The sync design's §4 had the driver take the quest
before spawning. That was the alternative D46 rejected, and the reasons still hold. Taken has no way
back to Open, so a spawn that failed after the driver's take would strand the quest for good. Driven
sessions would need telling not to take. And driven work would become distinguishable from outside
work at the one layer where symmetry is the guarantee. Claiming at the take keeps the session the
claimant. It keeps the table without a release, and it keeps D68's promise of no duplicate work while
the remote answers. D68 §6 placed the sync in the driver because git provenance needs a spawn. That
reason is the knowledge feed's, not the quests', and the knowledge feed stays in the driver.

**Found while deciding, and part of it.** Once a machine's take on a quest has lost, that machine's
later pending moves on the same quest are made on a claim it never held. The rebase turns them into
conflicts too. Without this, an offline session that lost its take but finished its work would
still close the quest over the winner's take, because the table allows done from taken.

**Rejected.**
- **The driver takes, then spawns** (the design's §4 as first written): it reopens D46's rejected
  alternative, for the reasons above.
- **Claim, then stop the loser**: sessions take as before, and the driver pushes each take as soon
  as it sees one and stops a session whose take lost. It keeps the sync in the driver, but a real
  race online would still run a duplicate session for a few seconds. D68 promised none.

## D70 — Registered is addressable; adopted is disciplined (2026-09-24)

**Decision.** The owner said yes to INT3, which D65 §5 and the intake design §1e proposed. A
repository **registered on this machine with a root** can be asked a quest, and the driver drives it
**over the protocol door**, whether or not it has adopted. Adoption stops being what makes a
repository addressable. It becomes what makes one disciplined: its manifest, its declaration, its
doctrine and its own connector.

- **One judgement, `Registration.Addressable`**: adopted, or registered with a root. The exchange's
  publish and its chain steps read it, and so do the MCP `registry` tool, `/api/registry` (a new
  `addressable` field) and every receiver list on the page. No surface offers a receiver the exchange
  refuses (D50).
- **The planner is told the door** the machine's starts ride, which is the configured adapter's wire.
  Over the protocol door, an unadopted repository with a root plans like an adopter. It must still be
  opted in (`drivable`), and holds, strikes and one session per tree still apply. Over the pipe door
  it sits, and the reason names the door that could carry it.
- **A registration records adoption as it is.** The desktop's *add* sends what the shell found there,
  `adopted: false` for a folder with no manifest. `connect` sends nothing, which means adopted,
  because it runs in an adopter. A shared deployment takes no false. Before this the HTTP door stored
  every row as adopted. An added folder with no manifest would then have been driven over the pipe
  door with no connector, and no sentence would have said why.

**Why.** D34's letter was that a repository without a manifest *"has no client and cannot see a
quest"*. That was true while the connector travelled only in a repository's own `.mcp.json`. Since
ACP4, the protocol door hands every driven session the knowledge server on `session/new`, with
nothing written anywhere. The quest tools take their quest and repository as arguments rather than
reading a manifest. So a quest to a registered repository now has somebody to answer it. The regular
task (D65) routes work into a workspace of real repositories, and most of them will not have adopted
on the first day. Requiring each to adopt before it could be asked would put adoption on the path of
every first ask, and adoption is the repository's own act, reviewed by its owner.

**The pipe door keeps its own requirements.** A pipe-door session reaches the knowledge tools only
through the repository's own `.mcp.json`. Adoption writes that file, and the driver may never write it
for the repository (D32). The pipe door also needs the harness's trust for the folder (DEPLOY1). An
unadopted repository driven over the pipe door would get a session that cannot take its quest. This
was measured before ACP4: a real session called `take`, found no such tool, and ended having touched
nothing.

**What such a session is given, and what it is not.** It is given four things:

- the quest, whose composed target itself carries the boundary: never write outside the repository,
  never push or publish;
- the knowledge connector, on the wire;
- the repository's own instructions and permission configuration, exactly as a person working there
  would have them;
- the refusal of every permission request, by construction (D52).

It is **not** given the canon: no `AGENTS.md` region, no rules and no discovery skills. Those are what
adoption installs, and nothing here writes them in adoption's place (D32). A person working there by
hand has no connector and does not see the quest. The publish tells the asker so, the registry marks
the repository, and the Projects view says it.

**Amends** D34's *"adoption gates addressing"*, and D46's claim that a session takes its quest *"over
its own connector"*: over the protocol door, the connector is the wire's. D46's symmetry between
driven and outside work holds for adopters. For an unadopted repository the driven session is the only
one that can answer, and the surfaces say so rather than hide it. A shared deployment still takes
quests only for adopters (`JudgeReceived`). It holds no roots, and an unadopted repository has no
manifest to declare a join with, so its quests never leave the machine that can drive it.

**Rejected.**
- **Adoption first**, the letter of D34. It keeps one rule simple at the cost of putting adoption on
  the path of every first ask to a real workspace. Adoption is exactly the step the doctrine keeps for
  the repository's own agent and its owner's review.
- **Addressable by registration alone, root or not.** A row with no root here has no client and no tree
  a session could start in. A quest to it would sit unread, which is the failure D34 exists to prevent.
- **The pipe door handed the connector too**, by `--mcp-config` from a file under the home, the way
  D65 §1f hands plugin servers. It is possible for Claude Code, but it would be a second decision. The
  pipe door's other requirement, the harness's trust flag, is the owner's open call (DEPLOY1), and
  INT3 was asked for the protocol door. Worth reopening when a harness that has only a pipe door needs
  it.
- **The planner guessing a door per repository.** The adapter is the machine's (`config.Adapter`), and
  a planner that guessed would start what the spawn then cannot serve.

**Proven with the stub agent only (2026-09-24, at integration).** The family rehearsal drives an
unadopted repository to done over the ACP stub, which never asks a permission. A real Claude Code
agent over the same door would ask before calling the connector's tools, because an unadopted
repository carries no allow-list for them, and D52 refuses every request. DEPLOY1's measurement
(`docs/2026-09-24-deploy1-acp-trust-evidence.md`) adds that over this door a room's allow-list is
ignored until the person trusts the folder, and the driver holds an untrusted one. So a real agent
answering a quest in an unadopted repository is unproven, and likely blocked, until the connector's
own tools are allowed at session creation. D52 permits a posture set there. That is INT3b.

## D71 — A pack may switch a core row off; the repository confirms it, and it is never silent (2026-09-24)

**Decision.** The owner reopened D4, 2026-09-24: *a pack may switch core rows off.* PLUG2 took the
idea from dsh, whose profiles compose as ordered layers where a later layer may disable a row.
Daoris's version has four parts.

- **The pack offers.** `pack.json` gains `switchesOff`, a map from a core document to the reason:
  `{ "rules/task-lifecycle.md": "its own rules/ticket-lifecycle.md replaces it for …" }`. A key
  names a core rule or knowledge document by its target, or a core skill by its directory
  (`skills/<name>`). The reason is required, because it is what `status` prints. A pack may switch
  off only core rows: another pack's rows are opt-in already, so there is nothing to switch.
- **The repository confirms.** `daoris.json` gains `switchedOff`, a map from the same target to the
  pack that switches it: `{ "rules/task-lifecycle.md": "some-pack" }`. Only a confirmed row goes
  off. An offer nobody confirmed leaves the core row installed, and `sync` and `status` name the
  offer and the line that would confirm it. A confirmation that no selected pack offers is a tool
  error. A repository alone still cannot switch core off, because D4's reason is about exactly that:
  the rules most worth having everywhere are the ones a repository would forget to keep.
- **The lock records it.** `daoris.lock` gains `switchedOff: [{ target, by }]`, so `check` can say
  what is off without reading any pack. `check` is offline and canon-free (D8).
- **It is never silent.** `sync` names each row it switches off. `check` prints each one on every
  run. It fails only on the fact that the manifest and the lock disagree about one (confirmed and not
  yet synced, or withdrawn and not yet synced), exactly as it fails on a pack declared and never
  synced. `status` names each one with its pack and its reason, and each pending offer. The doctrine
  region's roster says which rows are off and which pack switched them, under the rules table, so a
  session loading the region knows what is not there. `init` names a pack's switches beside its
  description.

**The budget measures what is loaded** (D28, D54). A switched-off rule costs nothing, the pack's own
rule costs its bytes, and the roster's line naming what is off costs its own. `check` reports the
total as before.

**Why the repository confirms.** The owner's sentence settles that a pack MAY switch core off. It
does not settle whether the repository has a say, so the safer default was taken. **The owner
confirmed it the same day** (2026-09-24): the repository confirms. A pack installs into repositories its author has never seen, which is why D4 was
strict. A core rule vanishing because a pack was added reads, to the next session, as if the rule
never existed. With the confirmation, the removal is a reviewable line in a tracked file that names
the pack. Without it, adding a pack would change the always-loaded doctrine in a way the manifest's
diff does not show.

**The state space.** D19's table gains the cells for a core document a confirmed switch covers
(amended there). The destructive one is an edited core row being switched off. It refuses like drift,
not like an edited retirement, because the canonical file still exists and `daoris upstream` can
still save the edit.

**No `apiVersion` bump.** PLUG1 raises it only when a pack written for the new shape cannot work on
the old one. An older CLI ignores `switchesOff` and `switchedOff` and installs the core row with
everything else, which is D4's behaviour and the conservative failure. A lock written here and read
by an older CLI loses its `switchedOff` on that CLI's next sync, and the row comes back. Nothing is
lost in either direction.

**Rejected.**
- **The pack's word alone**, dsh's shape exactly. It honours the owner's sentence most literally, and
  is refused as the default for the reason above. Dropping the confirmation check is a small change
  if the owner wants it.
- **The repository's word alone** (`"disable": [...]` in `daoris.json`). It reverses D4 outright: any
  repository could drop any core rule, which is the failure D4 was written against. A pack switching
  a row off brings, in its author's words, the reason, and usually the replacement.
- **A pack overriding a core row at the same target**, by shipping its own `rules/task-lifecycle.md`.
  That gives one identity two sources. The lock, drift and `upstream` all key on the target, so an
  edit promoted from the repository would reach the wrong canon file. A replacement is the pack's own
  document under its own name, with the core one switched off. Two packs shipping one target is now
  refused when the files are selected, which was an unguarded case before this.
- **Switching off another pack's rows, or ordering the layers** (a precedence list). A pack's rows are
  opt-in already, and an order among packs answers a conflict the canon does not have. It is the part
  of dsh's composition with no Daoris problem under it.
- **Failing `sync` on an unconfirmed offer.** It forces the decision, but it blocks a repository that
  wants the pack and the core rule both, and saying so would need a second manifest field. Leaving the
  core row on, and saying so every time, is the safer of the two.

**Amends** D4's *"core installs everywhere with no opt-out"*: core still installs everywhere unless a
selected pack offers to switch a row off and the repository confirms it.

## D72 — What an agent may do is Daoris's rules in scopes, handed to the harness at spawn (2026-09-24)

**Decision.** The owner answered HELP3 and INT3b together, and wider than either asked: *"so we should
be able to do just like how claude code scopes configured by rules in daoris (which daoris can also
use llm to update those too or modified by user)"*. `docs/2026-09-24-permission-scopes-design.md` is
the design.

- **The rule is the harness's own.** A Claude Code permission rule (`Bash(npm run test:*)`,
  `mcp__<server>__<tool>`), in an `allow`, `ask` or `deny` list. Daoris invents no rule language.
- **Three scopes, in one file under the home** (`permissions.json`, never in a repository, D32):
  **machine**, **workspace** and **repository**. Above them sit **Daoris's defaults**: `connector`
  allows the connector's own quest and knowledge tools (INT3b), and `no-push` denies `git push`
  (D37, HELP3); `commit` and `tree-guard` joined them (amended below). The person can switch a
  default off by id, and nothing else can remove one.
- **Precedence is the harness's.** Daoris unions defaults, machine, the session's workspace and its
  repository, and hands the union to the harness as its command-line tier: `--settings <file>` on the
  pipe door, and `session/new`'s `_meta.claudeCode.options.settings` on the protocol door (read from
  the adapter's source). The harness merges it with the person's and the repository's own settings,
  with `deny` beating `ask` beating `allow`. So Daoris can add a refusal nobody lifts, and cannot lift
  one a repository made.
- **Two doors to edit them** (D50): `daoris agent rules …`, and Settings → *What agents may do* on
  the desktop.
- **Claude Code only, at first.** Codex and dsh have permission models of their own, and a rule is not
  translated until someone asks and it is measured (the TOOL5 bar).
- **An agent may update them in phase 2 (PERM2): narrowing at once, widening on the person's yes.**
  Every change is recorded with who made it. Whether a widening may ever apply without the person is
  put to the owner rather than assumed.

**Why.** Everything Daoris starts refuses what it would have to ask (D52 on the protocol door; `-p`
on the pipe). So a session can only use what is allowed before it starts. The one scope that allows
things today, the repository's own settings, is ignored until the person trusts the folder (DEPLOY1's
measurement), and an unadopted repository has none (INT3b). A scope Daoris owns, handed over at
spawn, is where the connector's allowance and the guard can live without writing into anyone's tree.

**What a rule cannot say.** *Refuse a write outside the session's tree* has no rule form: there is no
negation, and `deny` beats `allow`. The harness's own working-directory boundary holds it for edits,
and a refused ask holds it for the rest, unless a shell command that writes elsewhere was allowed
broadly. A path-checking hook would close that. It is filed as PERM3, not guessed.

**Proven, and not.** Keylessly: the composed file, the pipe door's flag, the protocol door's `_meta`.
Unproven until a real session runs: that the harness honours this tier, and whether it does so in an
untrusted folder. The repository's tier is not honoured there, and `tools/acp-trust-probe.mjs` measures
this one.

**Rejected.**
- **The repository's `.claude/settings.json`.** It is the repository's (D32), it is ignored until
  trusted, and a shared file with no comment markers is D59's region problem.
- **The credential profile's own `settings.json`** (HELP3's second proposal). An account is not a scope
  a person thinks in, and a rule's reach would depend on which account a session ran as.
- **`allowedTools`/`disallowedTools` on the protocol door.** A second shape for the same rules, on one
  door only.
- **Daoris ranking its own scopes** against each other. It would be a second precedence beside the
  harness's, and it would disagree with it exactly when it mattered.
- **An agent widening its own permissions unasked.** A better approval surface must not widen
  autonomy (D37, D52). Narrowing needs no one.

**Amended 2026-09-24: a session may commit (PERM4), and the tree guard is a hook (PERM3).**

- **`commit` is a default, on the owner's answer to PERM4.** It allows `Bash(cd:*)`, `Bash(git
  add:*)` and `Bash(git commit:*)`. The reason was measured: in a folder the agent had never trusted,
  ACP2's real session took its quest, made the edit, was refused `git commit` and declined, because
  the repository's own allow-list does not apply untrusted. It committed only once a rule allowed it.
  D37 already makes a local commit automatic and a push the person's, and `no-push` still refuses
  the push, since deny beats allow. `cd` is in it because the agent prefixes its commit with one, and
  every part of a compound command must be allowed. The person can switch it off by id, like the
  others. **Rejected**: leaving the commit to a rule each machine writes. Then every real driven
  session in an untrusted repository declines, and the default that was meant to let work land would
  be a setup step.
- **`tree-guard` is a default that is a hook, not a rule.** It is a PreToolUse hook Daoris ships in
  the same settings file: exec form (`node`, the script and the session's tree as one argument each),
  on `Edit|Write|MultiEdit|NotebookEdit`. It refuses a write whose path, resolved through links, is
  outside the tree, with `permissionDecision: "deny"` on stdout and exit 0, and says nothing inside.
  The script is carried in the driver's assembly and written under the home. It adds no rule, so its
  row names the tools it judges instead. Switching it off by id hands no hook. **What it does not
  cover**: a shell command's writes, which cannot be judged by reading the command. There the
  harness's working-directory boundary stands, and a command runs only if a rule allowed it. A hook
  that fails or times out does not block, so this stands beside that boundary and never replaces it.
  **Rejected**: shell form, since a path does not survive Git Bash's or PowerShell's quoting reliably;
  a `deny` pattern per outside path, which cannot be written for "everywhere but here"; the hook
  answering `allow` inside, which would lift the harness's own asking; and a script shipped beside the
  binaries, which ties the guard to one publish layout. **Unproven until a real session runs**: that
  the harness honours a hook handed in the command-line tier (its documentation lists hook scopes and
  does not name `--settings`), on each door.
- **A session may read the folder its own quest's or ask's files are kept in (INT4j).** INT4f's real
  intake was refused `Read` twice on its ask's kept file: the file lives under the home, outside the
  room, and D52 refuses every request. The executor now adds exactly one rule for that folder to the
  session's composed file, `Read(//<path>/**)`, and nothing broader: not the home, and not another
  ask's or quest's folder. A driven quest session gets the same for its quest's kept files, because
  they are kept the same way (D65 §2) and its prompt names them too. The form is Claude Code's own,
  read from its 2.1.281 bundle rather than guessed. A pattern starting with `//` is an absolute path
  from the filesystem root. On Windows a Read's target is normalised to POSIX form (`C:\x` →
  `/c/x`, the drive lower-cased) before it is compared, and an allow is compared case-sensitively, so
  nothing else is re-cased. The tree guard is unaffected: it judges writes only, and a read it never
  sees. **Rejected**: `additionalDirectories`, the harness's other way to reach a folder outside the
  tree. It makes the folder a working directory, which under `acceptEdits` also lets an edit there
  through without asking. That is more than a read, and only the tree guard would still stand in its
  way.

**Amended 2026-09-24: phase 2 is built (PERM2 → D74).** An agent proposes over its connector
(`permission_propose`, now in the `connector` default). The driver's tick applies a narrowing and holds
a widening, and 🔴 a widening never applies without the person: the owner's answer to the question
above.

## D73 — The agent's trust in a folder is asked, then written, and never silently (2026-09-24)

**Decision.** DEPLOY1's second half, option (b), is the owner's answer (2026-09-24): Daoris asks per
folder, then writes the flag, and for managed repositories *"we should follow what claude code
does"*. The agent ignores a folder's own `permissions.allow` until someone accepts that folder in the
account's `.claude.json`. That holds on both doors, measured
(`docs/2026-09-24-deploy1-acp-trust-evidence.md`). Daoris now writes that flag, and only on a
person's explicit act that names the folder.

- **The terminal**: `daoris agent trust <agent> <folder> [--profile <name>] --yes`. Without `--yes`
  it is the question. It names the folder, the account's file and what trusting means, grants
  nothing, and exits 1. A terminal command cannot prompt, since a gate runs it with stdin closed, so
  the flag is the answer. The account is the one a session there would run as: the profile named,
  else the machine's default, else the agent's own configuration home. A door (`claude-code-acp`) is
  granted in its owner's account.
- **The screen**: where the driver shows a trust hold, *trust this folder…* opens the same question,
  and the grant is written on the press. That is the quest's drawer, and a `trust` row in *What needs
  you*, which is also where an intake's held room shows. The tick reports each trust hold as a fact
  (`TickReport.Untrusted`: the folder, the account's file it read, the quest or ask it held). The
  bridge's `TRUST_FOLDER` grants only a pair the last tick held, in the file that tick read. It is
  desktop only, because a browser never learns a machine path.
- **The write**: one flag moves (`hasTrustDialogAccepted`), in the harness's own file. An existing
  entry is updated in place under the key the harness wrote; a new one takes forward slashes, the
  form Claude Code writes today. It is written beside, renamed, then read back. A file this build
  cannot read is refused and left as it was. The twins are `ClaudeTrust.Grant` in the driver and
  `trust.ts` in the CLI, with the same cases asserted on both sides.
- **Nothing else writes it**: not adoption, not sync, not a spawn.

**Trust decides only whether a repository's OWN allow-list counts.** Measured the same day, on both
doors: the rules Daoris hands a session at spawn (PERM1, D72) reach it in an untrusted folder. A
session handed the `connector` default takes and closes its quest whoever trusted what. So the
driver holds for trust only where that allowance would **not** reach the session: the harness takes
no rules, or the `connector` default is off, or the person asked for or denied the tool the session
must call (`quest_respond` for a quest, `quest_publish` for an intake). `PermissionRules.AllowsConnector`
reads the same composition `HandRules` hands over. The grant is still how a repository's own rules
come to apply, but it no longer gates driving.

**Why.** The flag *is* the grant, and the owner chose to have Daoris ask for it rather than send the
person into each folder with an interactive agent. Onboarding a workspace of 29 repositories (INT6)
that way is exactly the friction the automation-first direction exists to remove. The question
Daoris asks is the one the agent would ask, so the act stays the person's.

**A concurrent rewrite.** Claude Code rewrites `.claude.json` whole whenever it saves its state. A
Claude Code already running under the same account may save a copy it read before the grant, and so
undo it. That is not silent. The driver re-reads the file before every start, so a lost grant shows
as the same hold again, naming the same folder. `verified` is the re-read at the moment of writing:
when it does not hold, the terminal exits 1 and the screen says so.

**Not settled here.**
- Whether Claude Code honours a trusted *parent* folder for a child. The driver's check matches the
  exact folder, so if the harness walks up, a hold could name a folder that is in fact trusted
  through its parent. That would be a wrong hold, never a wrong grant, and the trust probe can
  measure it on a trusted parent.
- Whether the harness honours a key Daoris wrote exactly as it honours one it wrote itself. The form
  is the one it writes; only a real run under a granted folder proves it. ACP2's proof run is that
  run, once its scratch folder is granted.
- The screen offers no grant before a hold exists, for example from Projects when a repository is
  opted in. The terminal names any folder.

**Amends** `ClaudeTrust`'s "read, never written": the driver still only reads the flag, and it is
written only on the person's word.

**Rejected.**
- **(c) A documented first visit**: the person runs the agent in each folder and accepts. It keeps
  Daoris out of the harness's file, and makes onboarding a workspace a manual tour of every
  repository. The owner chose against it.
- **(d) Silently**, at adoption, sync or spawn. Never, because it removes the one step in the chain
  that is the person's.
- **A screen verb that names any folder.** The terminal already does that, where the person types the
  path. A bridge verb taking any pair would let whatever reaches the bridge widen trust, so the
  screen grants only what the driver is holding.
- **Granting by default in the terminal**, with no `--yes`. Granting on the first keystroke is the
  silent option with one more word in front of it.

## D74 — An agent proposes a change to the rules; a narrowing applies at the tick, a widening waits for the person (2026-09-24)

**Decision.** PERM2, the second half of D72's owner's words: *"which daoris can also use llm to update
those too"*. The "LLM" is a session. The harness carries the model and Daoris calls none (D24). 🔴 The
owner answered D72's open question on 2026-09-24: **a widening never applies without the person.**

- **A connector tool, `permission_propose`.** It takes `action` (`add` a rule to `allow`, `ask` or
  `deny`, `remove` one, or switch a `default` on or off), `scope` (D72's three) with its `name`, and
  `why`, which is required. It is in the `connector` default, so every session Daoris starts may call
  it. It **proposes and never applies**. A malformed rule, a scope with no name or an empty reason is
  refused with nothing written.
- **A proposal is a file under the home**: `<home>/proposals/<id>.json`, one per proposal, written
  atomically by the MCP host (`RuleProposalBox`). It records the change, the reason, the session that
  proposed it and its ask when it is an intake, the folder it ran in, and `state: proposed`. The home
  is the one the driver names on every connector it hands over (`DAORIS_RULES_HOME`, beside
  `DAORIS_SESSION_ID`), else `DAORIS_HOME`.
- **The driver's tick settles it before it spawns anything.** It judges the change against the rules
  as they stand. A narrowing is applied at once and marked `applied`, so that tick's sessions are
  already handed it. A widening is marked `waiting`, and the tick says so. A change the rules already
  hold is `unchanged`, and one they cannot take is `refused` in the driver's words.
- **Narrowing, precisely**: adding to `deny`, adding to `ask` unless the rule sits in that scope's
  `deny`, removing an `allow`, switching the `connector` or `commit` default off, and switching
  `no-push` or `tree-guard` on. Everything else that changes the file widens. The judgement is per
  scope and conservative: a change classed as narrowing only adds a refusal or removes an allowance,
  so in no composition does it let a session do more. Some changes classed as widening would change
  nothing in effect (an `allow` another scope denies), and they wait anyway.
- **The person answers from either door** (D50): `daoris agent rules proposals | accept <id> |
  decline <id> [--note "…"]`, and the Settings card's *Proposed by agents*, with a `rule` row in
  *What needs you* for every waiting one (desktop only: the rules never reach a browser). A yes applies the change whatever it does. A no changes
  nothing and keeps the person's reason. Only a proposal not yet settled is answered. A settled one is
  history.
- **Every change is recorded with who made it.** Settling rewrites `state` and `settled` (`at`, `by`:
  `the driver` or `the person`, and the note) and keeps every key the session wrote. The file is the
  contract, read by `RuleProposals.cs` in the driver and `ruleproposals.ts` in the CLI, with the same
  reading and answering cases asserted on both sides. Only the driver classifies.

**Why a file, and not a row in the service's store** (the design's first sketch, "recorded by the
service beside the session"). The rules are machine-local, so a proposal to change them is too. A file
under the home is never fed to a remote and never served over HTTP (D47 §4), with no guard to write
and none to forget. The CLI stays file-only: `daoris agent rules accept` opens no socket, which a
store row would have forced on it. One file per proposal means two sessions proposing at once never
write the same file. And the MCP host already writes machine-local files beside its store: a quest's
attachments.

**Why the tick classifies, and not the door.** Whether a change narrows depends on the rules as they
stand, and only the driver and the person's doors read them. The MCP host is a service artefact that
never opens `permissions.json`. So its answer says both outcomes plainly, and the one place that knows
applies the difference.

**Limits, stated.**
- A pipe-door quest session talks through its repository's own `.mcp.json`, which names no session.
  Its proposal lands under `DAORIS_HOME` as one from *a session the driver did not start*. The protocol
  door, and an intake on either door, name the session.
- A person answering from the terminal while the tick settles the same proposal can both apply it. A
  proposal applies the same edit twice without harm, and the last mark written stands. Two writers of
  `permissions.json` at the same instant lose one edit, as they did before this (PERM1's two doors).
- The folder a session ran in is a machine path. It stays in the file and never rides the bridge. The
  screen is told who proposed, never where they stood.
- **Unproven until a real session runs**: that an agent, handed the tool and a refusal, proposes a
  change it needs. The keyless half is proven: the connector's write, the tick's settling and spawn,
  both doors' answers.

**Rejected.**
- **A row in the service's store**, above: every guard D47 §4 needs, for a fact that never leaves the
  machine.
- **The MCP host applying a narrowing itself.** It would write `permissions.json` from a service
  artefact, while the driver may be composing the same file for a spawn. And the host would need to
  read the rules to classify, so there would be two classifiers.
- **Classifying at the door.** The door cannot see the rules, so it would have to guess, and the guess
  would be wrong exactly when a scope already held the rule.
- **Any widening without the person**, including within one repository's scope or for a tool that only
  reads. The owner said no. A better approval surface must not widen autonomy (D37, D52), and what an
  agent may do is exactly that boundary. Agents that widen their own permissions make the rules
  decoration.
- **Deleting a proposal once settled.** The record of who changed what, and who declined what, is the
  point.

**Amended 2026-09-24 (PERM2b): the target says the tool is there.** The composed quest target named
every connector verb it relies on (take, close, publish) and not this one, so a session refused a
command its work needed had nothing to reach for but a decline. The target now carries one paragraph
after the claim and close: if a needed command is refused and the connector offers
`permission_propose`, propose the narrowest rule that would allow it, with the reason; a rule that lets
agents do more waits for the person, so finish what can be finished or decline naming the refusal. It
is conditional on the connector offering the tool, because the same words reach every door and every
agent. An intake's own prompt is untouched. `tools/perm2-proof.mjs` is the real-session proof.
Rejected: saying it only when rules are handed. The target is composed before the door is known, and a
sentence that names its own condition is honest on every door.

## D75 — The menus are the setup domains, Settings is one page of them, and the workspace is always named (2026-09-24)

**Decision.** The owner, looking at the installed application: *"I think we can use the topbar menu to
have more different domain of setup, this is closer to ide logic, and I dont see workspace
anymore?"* The owner chose each shape below from options put to them the same day. It amends D56 §3a
and D66 §2; `docs/2026-09-24-menus-design.md` is the contract.

1. **The app strip's menus are the setup domains: Daoris · Workspace · Agents · View.** *Daoris* holds
   the application (settings for appearance and language, the driver, plugins, refresh, language,
   about). *Workspace* lists every workspace, choosing one scopes the window, and holds adding a
   repository, importing a folder, wiring a remote and the workspace's settings. *Agents* holds the
   tools and accounts, what agents may do, their proposals with the count waiting, usage, and
   Daoris's own AI. *View* is unchanged. Every setup item opens its own domain. Before this every one
   opened the same long page at its top.
2. **Settings is one page with a list of its domains**, one shown at a time, as an IDE's settings
   are: Appearance, Daoris's own AI, Workspace, Driver, Agents & accounts, Permissions, Plugins. A
   menu item opens the page at its domain. The domains are the cards the page already held, regrouped.
   Nothing moves between the two doors (D50): each setting is still the same file.
3. **The workspace is always named.** The command center and the status bar name the scope whatever
   the machine holds: one workspace by its name, several as *every workspace · N* until one is
   chosen, and none as *no workspace yet*. WSP5's rule, a switcher absent below two, stays for the
   CONTROL and no longer hides the FACT.
4. **One word: workspace / 工作区.** The interface said *workspace* 41 times and *circle* 26, and
   工作区 27 times and 圈子 24, sometimes both in one sentence. It keeps the CLI's word
   (`connect --workspace`, `DAORIS_WORKSPACE`), in every catalogue and in the sentences the CLI,
   the driver and the service print. *Circle* stays prose in the design documents, which explain what
   a workspace is for.

**Why.** A menu bar that names three domains and opens one page for all three is not a menu. And a
scope that decides every number on the screen (workspace design §4) was invisible on the machine
that needed it most, one with no workspace yet, because the rule that hid an unneeded control hid
the fact too.

**Rejected.**
- **One menu per domain** (Daoris, Workspace, Driver, Agents, Plugins, View, Help): seven shallow
  menus and a wide bar, for two domains that fit under the application.
- **A classic IDE's File menu first.** Daoris opens no files, and "File" would hold repositories.
- **Scrolling one long page to a section**, the smallest change. The page stays an essay to scroll
  past, which D66's own settings rows were written against.
- **A drawer per domain.** A setting a person changes twice would open in two places with no page to
  come back to.
- **A Workspaces view on the activity bar.** The owner chose naming the workspace in the chrome over
  a seventh navigation item, keeping D66's one list.
- **An empty *Help* menu.** *About* sits under *Daoris*, and a menu with nothing to open is not a door.
- **Keeping *circle* in the interface.** Two names for one scope, and the CLI can only say one.

**What does not move.** D66's one navigation: the menus open setup and never views. D47 §4: a
browser's menus hold only what a browser may know. D50's two doors. WSP5's scoping itself.

## D76 — A session is a conversation: structured events end to end, kept on the machine (2026-09-25)

**Decision.** The owner: *"lets keep push the ui/ux design and I still think this does not meet the
reference projects capbility"*. `docs/2026-09-24-reference-gap-study.md` measured why, from source on
both sides: the attended session's centre is a record, not a conversation. ACP's updates are flattened
to text lines before the bridge, a chat pipes raw text, and nothing is kept for the page across a
restart. The owner chose option A from that study, with every extra it offered, the dock's file tools
after the conversation, and *"you should check screen by screen and all ui ux logic"*.

1. **The driver keeps a session's structure as typed events**, in Daoris's own small vocabulary: the
   person's message, the agent's message, a thought, a tool call and its updates, a plan, usage, a
   turn's end, the driver's own note, and anything else kept raw. Each door maps its own documented
   wire into it: ACP's `session/update` on the protocol door, and Claude Code's `stream-json` on the
   native door, in that adapter's own code (D23). A harness that offers only text stays text, and
   the page says so rather than guessing structure.
2. **The events are a transcript, kept on the machine** (D47 §4): `sessions/<id>.events.jsonl` under
   the home, beside the verbatim `<id>.log`. They never cross HTTP and never sync. The bridge carries
   them live and reads them back a page at a time, so a conversation outlives a restart.
3. **The page renders a conversation from them**: the person's and the agent's messages, Markdown,
   code with its language and a copy button, thinking folded, tool calls as cards, a turn's work
   folded, meters. The verbatim console stays, as the conversation's raw view.
4. **A chat rides the same structured wire**: ACP's turns on the protocol door, `stream-json` turns
   on the native door. The person's message is part of the record, which it never was.
5. **The platform's stack gains a Markdown renderer and a highlighter** (D42): headless, safe by
   default (no raw HTML), and neither brings a design language. D41 stays the only one.
6. **The round also holds** the composer (attachments, `@` a file in the session's tree, a draft per
   session, stopping a turn without ending the session), the meters, the frame (a resizable,
   collapsible rail and a resizable dock with tabs per session, at the geometry the components plan
   §3a adopted), session search and a row menu, and highlighted review. It ends with **a
   screen-by-screen audit of every surface's UI/UX logic**. A file tree and a document preview come
   after the conversation. The terminal keeps design §6's trigger.

**Why.** Every conversation capability the reference has is rendered from structure, and Daoris threw
the structure away one layer below the page. A chrome pass cannot close that.

**Amends** working surface design §3. Its rejection of *parsing the stream into steps* stands for a
pipe's text, and the structured wires are not parsing, as the IDE study already said of ACP. It also
amends D52's *stream promoted*: the stream becomes the conversation, and the console becomes its raw
view.

**Rejected.**
- **Parsing the console lines on the page** (study option B). That is the scraping design §3
  rejected, one layer up, and any rewording in `Acp.cs` would break it silently.
- **Chrome only** (option C). It leaves every row of the study's §2a missing.
- **Keeping the events in the service.** They are transcript-class: what a session said and did is
  machine-local, like the transcript and the diff.
- **A model picker, mid-run approval, like/dislike feedback.** The reference has all three.
  D24 puts the model with the harness. D37 and D52 refuse a permission request by construction.
  Feedback has no receiver when no provider is named.

**What does not move.** D24's no model named. D37 and D52: the person is at the target and the
outcome. D47 §4's disclosure boundary. D23's adapter per harness. D55: no editor.

**Amended 2026-09-25 (CONV4a): one turn at a time on both doors, and a stop hands back what was
waiting.** Three choices, each with the one it beat.
- **A message sent while a turn runs waits for it**, on the native door as on the protocol door.
  It joins the record when it is sent. Rejected: writing it to Claude Code at once and letting the
  harness decide. Its SDK says it may fold such a line into the running turn (bundle evidence, not
  measured), and then the record cannot say which turn answered it. Holding it makes the question
  moot on every harness.
- **Stopping a turn keeps the session** and is a third verb, beside finishing and stopping the
  session. On the protocol door it is `session/cancel`. On the native door it is the harness's
  interrupt control request, measured first (`docs/2026-09-25-stream-json-evidence.md`). The turn ends
  `cancelled` on both. A door that carries only text refuses in words: it cannot see where a turn
  ends. Rejected: killing the process for a native stop, which is the session's stop under another
  name.
- **A stop withdraws what was waiting and hands it back**, before the turn in flight is stopped.
  Rejected: letting the queue run on, because the next message would start the moment the stopped
  turn ended, and the person's stop would stop nothing they could see. Also rejected: dropping the
  queue silently, which loses what they wrote. Finishing is different: the turns already asked for
  run first, because finishing is not withdrawing.

**Amended 2026-09-25 (CONV4c): an attachment is a file kept for the session, granted read, and
named once per door; `@` a file is text.** Measured on both doors first
(`docs/2026-09-25-message-content-evidence.md`).
- **Kept** under the home at `sessions/<id>/files/`, beside the transcript and the record, and
  **granted** by INT4j's read rule for that folder alone, at spawn.
- **Named** as each door reads best: a line naming its path on the native door, which the agent
  reads with its own tool, and a `resource_link` on the protocol door, the protocol's baseline block.
  Never both, since `claude-code-acp` turns a link into a mention and the file would arrive twice.
- **The record keeps names**, beside the person's words, never the paths or the lines Daoris added.
- **`@path` travels as typed**: both doors expand it themselves.
- **The terminal attaches with an `:attach <path>` line.** `/` is the harnesses' own command
  namespace, and `@` is a mention.

Rejected:
- **Inline image blocks and embedded resources.** Both work on both doors, but they put a file's
  bytes into the message on the wire, and an embedded blob is dropped by the adapter, so a second
  way would be needed for the kinds it drops.
- **Writing the file into the tree.** The tree is the repository's, and an attachment is the
  person's (D32 in spirit; the tree guard in fact).
- **`additionalDirectories`.** INT4j already refused it, because under `acceptEdits` it would also let
  edits there through unasked.

**Amended 2026-09-26 (CONV4d): `@` completes from what git says the tree holds, and writes a path
the way both doors read it.** Measured first, on both doors
(`docs/2026-09-25-message-content-evidence.md`, § CONV4d).
- **The files are git's answer** for the tree the record names: tracked files still there, and new
  ones git does not ignore. This comes from one bridge call, `SESSION_FILES`, read-only and
  desktop-only like the diff, behind the same "git walks up" guard. A tree that is not a repository
  of its own is refused in a sentence, and the typed path still goes.
  Rejected: walking the folder, which knows no ignore rules and would bury a tree's own files under
  its dependencies.
- **Asked for once per mention and ranked on the page.** The bound is 20,000 paths, and what it
  leaves out is counted. Rejected: filtering on the host per keystroke. It is a `git` process per
  letter for a list that changes only as the agent writes, and a tree that size is rare enough to
  state rather than engineer for.
- **A path with whitespace is written `@"…"`**, and every other path bare, whatever its script.
  Both doors expand the quoted form, and neither expands a backslash escape (measured).
- **Directories and other sessions are not offered.** The reference offers both. Neither was
  measured on either door, and a spelling nobody checked is how a completion offers what the
  harness then ignores.

**Amended 2026-09-26 (CONV5): the meters are the harness's counts and the driver's clock, and
nothing reported reads as nothing measured.** Measured on both doors first
(`docs/2026-09-25-stream-json-evidence.md`, § CONV5).
- **A turn's tokens are kept on its own `turn` event**: the four counts the wire reported for the
  whole turn, from `result.usage` on the native door and the prompt response's `usage` on the
  protocol door. Rejected: summing the streamed messages, whose output the probe found under-counted
  (20 against the result's 80).
- **A report whose every count is zero is no report.** `claude-code-acp` answers a turn stopped
  before its `result` with an empty tally, after reading the whole context, and no turn that ran
  read nothing. Rejected: showing the zeros as given, which is the one thing TOOL3 forbids.
- **How long a turn took is the driver's clock**, from the ask to the first thing the agent did and
  to the turn's end, because neither wire reports when the first word came and only one reports a
  duration. Rejected: the native door's `duration_ms`, a measure the other door cannot give.
- **The context ring reads the conversation's own record**, handed up by the organism that holds it.
  Rejected: a bridge call of its own, which would be a second home for the stream.
- **Absent says which absence it is.** A structured door that has not reported yet, a door that
  carries only text, and a door the roster has not named each have their own sentence, and none of
  them draws 0%.

**Amended 2026-09-26 (RAIL1): a conversation is named by its first line, and searched, on the machine
that holds what it said.**
- **The name is derived**: the first thing the person said, read from the event record over the
  bridge, which is the identity working-surface design §3 always named. Rejected: a title on the
  session record, which travels (D47 §4) and would carry what was said off the machine. Also
  rejected: hand-naming, which the derived name makes unnecessary again.
- **Content is searched on the machine's own record**, the person's words and the agent's, with a
  message's chunks joined before matching. It is bounded, and it says so when it left hits out.
  Rejected: indexing transcripts in the knowledge service, which holds knowledge and not what
  sessions said.
- **A row's menu holds only what has no other home**: its own window, its review, its id. Rejected:
  finish and stop in the menu, since each verb has one owner (D56).

**Amended 2026-09-26 (REVIEW2): a review is git's patch, numbered, highlighted a side at a time,
unified or side by side.**
- **git's lines, read, never re-derived.** The patch is parsed into hunks with the numbers each
  side had. Rejected: re-diffing the files on the page, which would put a second diff beside git's
  with no way to tell which to believe.
- **Highlighted a side at a time**: a hunk's old side and its new side are each highlighted as one
  text, then split back into lines with the markup balanced, so a comment spanning lines is coloured
  on each. The language comes from the file's extension, only where the highlighter ships it.
  Rejected: highlighting each line alone, which loses every construct that spans lines. Also
  rejected: guessing a language, as the conversation's code blocks already refuse to.
- **Unified or side by side, the reader's choice**, remembered per viewer like the frame's widths.
  Side by side, each half wraps, because a long line otherwise pushed the other side out of view
  (seen on the window).
- **An edit card in the conversation draws its lines with the same renderer**, unnumbered, since
  its lines are a piece of the file and not the file's.

**Amended 2026-09-26 (UX5): what the screen-by-screen pass settled.** Its ledger is
`docs/2026-09-26-ux5-screen-audit.md`; these are the findings that chose between alternatives.
- **Waiting on the person is open's hue everywhere** (U1): the dot, the pill, the band and the map.
  Rejected: declined's red, which the dot and pill wore, because red is an outcome's hue and a
  waiting session read as a failed one. Also rejected: the notice tone the map used, a second hue
  for the same fact.
- **A line the agent ended stays ended** (U4): every newline left in prose is a break. Rejected:
  CommonMark's soft break, which drew a one-item-per-line answer as one paragraph. The agent wrote
  for a terminal. Also rejected: a plugin dependency for a dozen lines.
- **The dock opens on demand** (U7), as the reference's does: closed to its strip for a viewer who
  never chose, opened at 45% by a tab, a row's review or the palette, and remembered either way.
  Rejected: open by default, which left a 1400px window's conversation 442px. Also rejected: opening
  narrower until Review is asked for, which is two defaults and still crowds the conversation.
- ~~**The head, the conversation and the composer are one column** (U10), at the conversation's
  measure.~~ **They follow the centre's width** (U16, the owner: *"when window is maxed, the inner
  content still only half the size, and this also appears in the chat box"*). The agent's words are
  content, and content is shown as it is (platform language §4). The head's pill follows its title
  rather than the far edge, which keeps what U10 was for. Rejected: a measure on the column, which
  is U10 and was half a maximized window. Also rejected: a measure on the agent's paragraphs alone,
  which at that width reads as half a window again.
- **Every window hears of a session started or ended elsewhere** (U13): the driver's tick signs the
  active sessions and forwards when they changed, as it already did for the asks (INT4d). Rejected:
  a second event for chat lifecycle, which would be one more channel for a fact the tick already
  carries, and would miss a session moved by another door.
- **The protocol door's console is the agent's lines** (U3): streamed chunks are joined, and a line
  left open is shown after two seconds of quiet, measured over 2,968 gaps between chunks (none over
  two seconds). Rejected: a line per chunk, which broke words. Also rejected: joining with no quiet
  flush, which hid a held turn's words and failed the family rehearsal; and half a second, which
  broke real lines on the window. Not taken yet: a console line that can keep growing, which is the
  driver, the shell's relay, the page's console and the terminal door changing together, with
  nothing measured asking for it.
- **A badge counts what its place holds** (U20, the owner's choice, 2026-09-26). Overview carries the
  whole of *What needs you*, beside the band that lists it, and Sessions carries its own sessions
  waiting on the person; both in open's hue. This amends the working-surface design §4's "the
  sidebar carries two counts". Rejected: the whole count on Sessions, where SURF5 put it on the Work
  icon and D66 carried it over, because the press it invited showed one of the six it counted. Also
  rejected: the whole count on Overview alone, which leaves a parked session unmarked on Sessions.
- **Every view follows the window** (U59, the owner, 2026-09-26: *"inner content size does not
  relative to the window size (a few screen still having this issue)"*), amending D41 §2's 72rem
  content column, as U16 amended the session's centre. What has a measure keeps it inside the
  column: prose at 65ch, a form at what it holds, a drawing at one unit a pixel, and the maps grow
  their drawing with the card instead (the ring as far as the window's height leaves it). Rejected:
  a wider cap, which is the same defect on a wider screen. Also rejected: scaling a drawing to its
  card, which grew its names past the type scale (U44).
- **A live chat between turns is idle** (U17, decided by the reference console at the owner's
  word). The page reads a chat whose record says `working` as idle when the driver says no turn is
  in flight: a quiet mark and a neutral word, where the reference draws no mark and says *Idle*.
  Rejected: a new record state, because the record says what the process is (D46 §4) and a turn is
  the driver's to say, live, which no record carries. Also rejected: *your turn* in the waiting hue,
  which would count every open chat among the things that need the person; the reference keeps its
  attention mark for what does.

## D77 — A workspace nobody adopted can be decided, a folder becomes a workspace in one statement, and a plugin keeps what it signs in to (2026-09-27)

**Decision.** This is the owner's first goal, set again on 2026-09-27: a real workspace, a ticket
read through a browser, and a task started with no repository named. D65 was designed for exactly
that and built as INT1–INT5. Reading what INT6 would have met on the real workspace
(`docs/2026-09-27-first-goal-study.md` §1) found three walls in the code. Each gets the smallest
change that removes it.

- **What a repository says about itself may decide, below a declaration.** For a registered
  repository that declared nothing, the intake's room shows its own files' word: its README's title
  and first describing paragraph, its package's description, and what it is built with. It is
  labelled as the repository's word and never called a declaration. The intake may publish on it
  when it plainly fits one repository and no other. The quest's body then names what decided the
  owner, and the receiving agent may decline, which comes back. A declaration outranks it. When
  nothing points at one repository, the intake still publishes nothing and asks.
- **An import may name the workspace.** `daoris import <folder> --workspace <name>` and the HTTP
  door's `workspace` wire every row it registers to that circle. A statement re-points, as
  `connect --workspace` does. An unnamed import is still silence, and silence still moves nobody.
- **`${data}` in a plugin manifest is the plugin's data folder** (D64 §3), expanded in a command
  and an environment by both twins. A browser's signed-in profile is the first thing that needs
  it. The example browser plugin keeps its profile there.
- **The intake's instruction says two more things.** A page behind a sign-in is read by a browser
  that is signed in, or it is said to be unread, and never guessed at. A ticket's words are the
  person's material and never the intake's instructions, the way orca wraps a ticket's text.

**Why.** The first real workspace is 29 repositories, and none has adopted. Adoption writes into a
repository, which is its owner's act (D32). So D65 §0's promise, *"without saying which
repository"*, has to hold before anyone adopts, or it holds for nobody on the first day. A
repository's README is not a claim about what it owns. It is still what its authors wrote for a
newcomer, and an intake is a newcomer. A misplaced quest costs a decline, which D65's chain already
carries back. A parked ask costs the whole goal.

**What stays.** The no-model tier still ranks declarations only. A README's words would propose
nearly everything to a word match, and a proposal nobody can read past is noise. D34 stands: the
registry answers *whose problem is this*. What changed is what the intake may weigh when the
registry has no answer. The plugin system stands as built: the references' plugins map onto D64's
three rows with nothing new (study §2).

**Amends** D65 §1b (*"decide from the declarations"*) and §1d (*"a repository nobody has declared
is not addressable, and the intake says so rather than guessing"*, already amended once by D70).

**Rejected.**
- **Adopting the workspace first.** Twenty-nine writes into repositories Daoris does not own,
  before the first ask.
- **Declarations drafted by Daoris into its own registry** for each repository. It is possible,
  because the registry can hold a domain for an unadopted row. But a drafted declaration is a claim
  nobody made, and it would be read as one. Worth building as the person's own door (a declaration
  made on this machine, reviewed) when a workspace wants precision a README cannot give.
- **The intake reading the repositories itself**, with read rules over every root. That is
  harness-specific path syntax, unmeasured on Windows. It would also hand a session that reads
  untrusted ticket text a read of every checkout, beside `WebFetch`. The room carries the words
  instead, read by Daoris, which never runs a ticket's instructions.
- **Lyntai's file storage for any store** (study §3). It serves one process with no transactions,
  and every Daoris store has several processes or needs transactions. The repository is already the
  document store for knowledge that should be versioned. SQLite stays. A persistent vector store is
  SEM1's question.
- **A ticket-system integration in the host**, orca's shape. D65 §2 stands: a ticket system's
  own MCP server is a plugin's to declare.

**Not covered by any gate.** A real harness reading a real signed-in ticket through the plugin's
browser, and what the real workspace's READMEs yield. Both are FG5, the owner's run.

## D78 — Daoris has its own browser: a window of the shell that the person signs in to and sessions drive over MCP (2026-09-27)

**Decision.** The desktop shell carries a browser of its own: one framed window in its own WebView2
environment, with its own profile under the home and no bridge. It listens for the Chrome DevTools
Protocol on loopback. The person opens it, signs in once, and watches. A session drives the same
page through an MCP server a plugin declares with `${browser}`, the in-app browser's CDP endpoint,
expanded when the session is handed its servers. The driver asks the shell to bring the browser up
before it hands such a server, and withholds the server where no shell answers.
`docs/2026-09-27-in-app-browser-design.md` is the contract.

**Why.** The owner: *"so instead rely on things like claude extension we can have our own built-in
browser system (or use plugin to support this)"*. A harness's own browser extension serves one
harness, from the person's browser, while it runs. FG3's plugin browser serves every harness, but in
a profile the person never sees open. Both references share one browser between the person and the
agents: orca's embedded Chromium, and dsh's attach over CDP. It was measured before it was designed:
Playwright MCP attached over CDP drove a WebView2, navigating, snapshotting and clicking by ref.

**The split is D64's.** The browser is Daoris's own code in the shell, because a plugin cannot add a
view and no code loads into a host. Which MCP server drives it is a plugin's declaration, because
that is a process and a choice: Playwright MCP by default, Chrome DevTools MCP where a machine prefers
it.

**Amends** D65 §2: *"A browser extension, or a browser of Daoris's own"* is half reversed. There is
still no extension, and the session still drives the browser through MCP, as D65 §1f built it.

**Rejected.**
- **The app's own WebView2 as the browser.** The probe navigated the app's page away. A page that
  holds the bridge, under an agent's CDP, hands the agent everything the bridge can do.
- **The harness's extension (Claude in Chrome).** One harness, and a browser that is the person's
  rather than Daoris's, reachable only while it runs connected.
- **A browser launched per session** (FG3's shape, kept as a plugin for machines without the shell).
  It gives no window to sign in to and nothing to watch.
- **An MCP server written by Daoris.** Playwright MCP and Chrome DevTools MCP already speak CDP, and
  a third would be a second implementation of somebody else's protocol to keep in step.
- **Expanding `${browser}` when the manifest is read.** The endpoint exists only while the shell
  runs, and a manifest is read by processes that have no shell.

## D79 — A session that needs another repository asks it, waits, and is resumed with the answer (2026-09-27)

**Decision.** `quest_respond wait on:<quest>` parks a taken quest on a question its session published
to another repository. The quest **stays Taken**, marked as awaiting the question, and the move is
logged as `Waited` and syncs like any other. The driver holds a waiting quest, saying what it waits
on. Once the awaited quest closes, done or declined, the machine whose session asked **resumes** it:
in the same tree, with an instruction that says the quest is already its own and carries the awaited
quest's outcome and closing words. The ledger opens a session on a taken quest only in that case.
The session that waited concludes `completed`, at no strike; a resumed one that ends with the old
wait still standing concludes `failed`, so the strikes bound it. The driven instruction and the tool
descriptions tell agents that a change or a fact belonging to another repository is asked of it.
`docs/2026-09-27-ask-and-wait-design.md` is the contract.

**Why.** The first real development session needed three facts about the backend. It tried to read
the backend's code, was refused, and wrote the front end on assumptions. Its own proposal named the
right move and declined it because blocking was all the workflow offered. The owner: *"so the issue
here is it should request to [the backend]"*. D32's reason applies to knowledge as to code: the why
behind a codebase does not travel, and a request does.

**Rejected.**
- **Reading a sibling allowed by default** (proposed, and declined by the owner). It gets the code
  and not the why, and it would have made the wrong reach the easy one.
- **A new quest status.** *Taken* with an `awaits` field needs no new transition in the lock.
- **Back to *Open* while it waits** (the first draft). An open quest is anyone's to take, so another
  session or machine could start it fresh, without the work in the tree that asked. Kept *Taken*, the
  take stays the one lock (D68) and the resume goes to whoever holds it.
- **A chain step back to the asker** (`then`). That works with today's tools, but the asking quest
  would close as done while nothing was done, and a declined question would stop the chain with the
  work never resumed.
- **Waiting inside the session.** A session is one turn on both doors, and its tree survives it.

*Amended by D107 (READ1, 2026-09-30): the owner reversed the first rejection. A session reads its
workspace's other checkouts by default. What stays of this decision is that a change, or what only the
other repository knows, is asked of it and waited on.*

*Amended by D124 (WSSETUP9, 2026-10-01): asking another repository is the second place a driven session goes. The
first is the sources: the quest and its files, its own repository's documents, code and history, the workspace's
knowledge, and the checkouts it may read. What they settle it decides, and what they lean towards it takes and says
in its close.*

## D80 — A session cut off after its take is carried on in its tree, like a failed start is retried (2026-09-27)

**Decision.** A taken quest, waiting on nothing, whose last session on this machine concluded
`failed` (timed out, refused by its agent, crashed) is this machine's to carry on. The ledger opens a
session on it. A stand-down or a teammate's record never counts, since either means the take is
somebody else's. The driver plans it like a start, behind the person's hold, busy, the cap and the
strikes, in the tree the cut-off session worked in. Its instruction says the quest is already its
own, what cut the last session off, and which changes it left uncommitted, as the driver read them.
A carried-on session that ends with the quest still taken concludes `failed`, so the third cut-off
parks the quest behind `daoris driver retry`. The session timeout is now set from a terminal,
`daoris driver timeout <minutes>`, and `driver list` shows it.

**Why.** FG5's second run. The development session took its quest, changed 45 files, and was running
its repository's gates when the thirty-minute timeout killed it. A failed *start* leaves a quest open,
and the strikes retry it (DRV6). A session cut off *after* its take left the quest taken for good,
with the work in a tree nothing would ever open again. D79 had just built the resume, a taken quest
carried on in its own tree, and a cut-off is the same move for a different reason. The first run had
ended the same way, on an account limit (ACPEND1).

**Rejected.**
- **Releasing the quest to *Open* on a cut-off.** That is D79's rejected draft again: anyone could
  take it fresh, without the tree.
- **A longer timeout alone.** It makes a cut-off rarer and does nothing for the one that still
  happens: an account limit, a crash, a machine that sleeps.
- **Carrying on a clean exit with the quest still taken** (a stand-down). That shape includes
  somebody else having the quest, and the driver cannot tell the two apart.

*Amended by D104 (DRV8, 2026-09-30): a last session that ended `stopped` and interrupted — by the orphan
sweep or the driver's shutdown, not the person — is a cut-off too, carried on and counted the same way.*

*Amended by D125 (TOOL4f, 2026-10-02): the instruction also carries the cut-off session's last plan and last words,
from Daoris's own record of it, and after an account's limit says the last session ran on another account, now
cooling. The carry-on may run on the next account of the person's order; the harness's own conversation stays in the
first account's home and is never read.*

## D81 — A driven session works in its harness's own judged mode, as a regular session would (2026-09-27)

**Decision.** On the protocol door, Claude Code sessions drive in `auto`, the harness's own mode in
which it judges each action, where the adapter offers it. An adapter that does not offer `auto` gets
`acceptEdits`, as before. An adapter now names its postures in order of preference, and the session
sets the first the agent offers. It is still never `bypassPermissions`, which judges nothing. What
the doctrine keeps is kept by the rules handed at spawn, which hold in every mode: no push by default,
the tree guard on writes outside the session's tree, and the connector's tools. A permission request
that still reaches the driver is refused, as D52 says. The pipe door keeps
`--permission-mode acceptEdits` until `auto` is measured there.

**Why.** The owner, 2026-09-27, asked whether the verify session might apply a config to dev only:
*"not dev only this should be able to do a much as it can just like regular claude code or codex"*.
FG5's sessions did the work and then stopped at every command that was not on a short list: `npm`
scripts, a syntax check, a generator, `git merge`. Each refusal became a rule proposal waiting for a
person, which is the friction D37's automation-first direction exists to remove. `acceptEdits`
accepted edits and refused everything else. `auto` is the mode a person runs Claude Code in when it
should act without asking, with the harness's own judgement in place of a list.

**Rejected.**
- **`bypassPermissions` by default.** "As much as it can" is not "with no judgement at all", and the
  owner's own sessions are not run that way. A person may choose it later, explicitly.
- **Answering permission requests on the person's behalf.** That is D52's rejected shape. The driver
  is a component, and the judgement belongs to the harness or to the person.
- **Growing the handed allow-list command by command.** That is what FG5 was doing, one proposal
  per refusal, and it never catches up with a real repository's tooling.

**Amended 2026-10-01 (UNBLOCK4, D122 §3.7): "no push by default" held in the classifier as well.** The rules
handed at spawn did not keep what this decision said they kept. `no-push` denied `git push …` as written, the
harness matches a rule against the command as written, and auto mode's classifier allows a push to the working
repository by default. So `git -C . push` passed both (DOC1's finding, from the maker's documentation). The spawn
file now tells the classifier, as a hard denial after its own `"$defaults"`, that a push in any form, a publish and
a release are the person's, and `no-push` denies a push with options before its subcommand on both doors. D122's
note has the whole account. What this door's classifier does with it is the owner's canary to show.

## D82 — A chain's next step in the same repository starts on the branch the step before landed on (2026-09-27)

**Decision.** When a quest is a chain's next step (D65 §4) and its parent's last session on this
machine ran in the same repository, the next step's tree grows from that session's branch rather than
from the canonical line. The instruction says the parent's work is in the tree and there is no merge
to wait for or make. If the branch is gone, merged and deleted or discarded, the tree grows from the
canonical line and says why. Nothing merges itself (D51), before or after.

**Why.** FG5's verify step grew a fresh tree from the canonical line. The develop step's work sat
unmerged on its session branch, so the verify step had nothing to verify. It asked for a merge
rule, which with `cd` allowed would reach the person's own checkout. Asked how a next step should
start, the owner chose *"On the parent's branch"*.

**Rejected.**
- **Waiting for the person's merge.** It checks exactly what ships, and nothing moves until a person
  acts, which is the friction the chain exists to remove.
- **A merge rule for sessions.** It is a write to a line the person owns, reached through a rule
  whose prefix allows more than it says.

## D83 — A session that holds its quest and stops is waiting on the person, who answers it to carry on (2026-09-27)

**Decision.** A take through a session's own connector is written on its record (`took`). A session
that ends its turn cleanly still holding its quest parks as `awaiting-person`, quoting its last words.
That covers one that took the quest itself, and a resume or a carry-on of a quest this machine
already held. Only a session that did not take the quest reads as a stand-down. The person answers a
parked driven session: `POST /api/sessions/{id}/answer`, the page's *answer and carry on*, or
`daoris-driver answer <session> "…"`. The record ends `completed` with the words kept. The quest is
then carried on in the same tree at the next tick, as after a cut-off (D80), and the session is
handed the answer. A park is never resumed by itself, so D79's and D80's clean exits that used to
conclude `failed` to bound a loop now park instead. A messy exit is still a failure.

**Why.** FG5's verify session took its quest, did everything it could, and ended its turn holding it
with three questions for the person. The record said *"stood-down: someone else has it"*, and nothing
would ever carry the quest on. The quest's state alone cannot tell a session's own take from another
taker's, and the connector already knows which session it speaks for (PERM2).

**Rejected.**
- **Reading the take off the wire** (the ACP tool call and its result). The record moves on facts the
  service holds (D46 §4), and the service saw the take arrive.
- **Answering in the composer.** A driven session takes no person's line (INT4i), and its process is
  gone by the time it parks.

*Amended by D124 (WSSETUP9, 2026-10-01): what may park a session narrows to what no source holds and only the person
can give: a sign-in, a go-ahead for an act outside the repository or on a production system, a preference nothing
records. A choice between options is no longer a reason by itself, and a park says what the session looked at.*

*Amended by D131 (ANSWER1, 2026-10-02): the answer goes on in the session that asked. Once ANSWER1b lands, the answer
keeps the record parked; at the next look its record moves back to working and its harness conversation resumes with the
answer, on the same account, adapter and tree. The carry-on in a new session is the fallback, saying why.*

## D84 — Daoris's browser is a Chromium Daoris ships, and the person's Edge is always an option (2026-09-28)

**Decision (the owner's direction, with its open parts named).** The in-app browser stops being
rebuilt by hand on WebView2. Daoris ships a browser engine for its browser, isolated from the
person's accounts, and **using Edge with a profile is always an option beside it** (the owner:
*"150-250mb per install is okay, there is no big reason why not, and also use profile edge is always
need to be an option too"*). Both are reached the way D78 already reaches a browser: a CDP endpoint
that a plugin's server attaches to (`${browser}`), so the plugins do not change. Whatever the engine,
Daoris keeps a sign-in across a restart itself, over CDP, because no browser measured does (BRW10;
`docs/2026-09-28-managed-edge-evidence.md` §3–4).

**Why.** Measured on Edge 154, started by Daoris on a profile of its own: a real browser has tabs,
history, favorites, devtools and downloads of its own, and **an agent's tab is a tab the person sees**,
where in the WebView2 window it had no window at all. But a fresh Edge profile **signs in to the
person's Microsoft account on its own** and syncs until told not to (§5), so Edge cannot be the browser
that is Daoris's own. It is the right answer for a person who wants their own sign-ins, which is the
option the owner asked to keep.

**The cost accepted.** Some 150–250 MB per install, and **the engine's security updates become
Daoris's**: Chromium ships fixes every few weeks, and a browser that agents drive on signed-in sites
must follow them. The engine is managed like a harness (D57): a pinned version, fetched from its
maker's channel and verified, and updated deliberately.

**Open, and the owner's:**
- **Which form**: a standalone Chromium that Daoris starts as its own process (what the Edge probe
  measured, less the account; its own tabs and history for free), or CEF embedded in Daoris's windows
  (Daoris's own chrome, as BRW4–BRW6 built on WebView2, with deeper hooks). The recommendation is the
  standalone one: everything measured holds for it, and it asks the least code of Daoris.
- **Which build**: Chrome for Testing (Google's pinned builds), a Chromium snapshot, or another; each
  differs in its banner, its media codecs and its licence. It needs a probe of its own.
- **Daoris's own UI** stays on WebView2 for now. It is hosted by the desktop runtime sibling, which is
  another repository, so moving it is a request to that repository's owner, never an edit from here.

**Rejected.**
- **Edge as Daoris's own browser.** The account's automatic sign-in reaches every fresh profile, and
  only a machine policy the person owns stops it.
- **Carrying on the WebView2 window.** Tabs, favorites and history were built (BRW4–BRW6), and each
  further basic (find, zoom, devtools, downloads) is a browser's feature rebuilt. An agent's own tab
  there stays invisible. The window stays until the engine lands, then goes.

**Amended by D85 (2026-09-28).** *Which form* is answered: embedded. *Daoris's own UI stays on
WebView2* is withdrawn, because the page moves to the same engine. *Managed like a harness* does not
hold for an embedded engine, which is pinned by the build. The Edge option and the CDP seam stand.

## D85 — Daoris's page and its browser run on one embedded Chromium it ships, under Shenora's frame (2026-09-28)

**Decision (the owner's direction).** Every Daoris window renders in a Chromium that Daoris ships and
embeds: the main window, the secondary windows and the in-app browser. The system's WebView2 runtime
goes. The owner: *"to shift to chromeiun, because we mostly build the ui itself in react and the only
missing part is the shenora currently dont support this, but the framework itself is still okay to
use since there is no big difference, just webview2 to chromium, we can start the work here and also
file the new task to shenora for this"*. Everything above the web view stays: `Daoris.Web` as the one
UI, and Shenora's modules, dispatcher, event bus, frameless form, window state, paths and secondary
windows. **It is built here first, on Shenora's public engine-neutral surface**
(`IpcHostBridge` with a `NotificationPump` on the host, `ShenoraBridge`'s `transport` on the page).
Shenora takes it in once it has proved itself, which is Shenora's own growth rule (its D15). The
request is filed in Shenora's backlog, at the owner's explicit say-so (*"you can file the TASKS.md"*),
as one uncommitted entry. `docs/2026-09-28-chromium-host-design.md` is the contract.

**Why.**
- **The limits met were WebView2's API, not Chromium's**: a tab an agent opens over CDP has no window,
  and a session cookie ends with the process (BRW4, BRW10). An embedding with deeper hooks may answer
  both, which CHR1 measures rather than assumes.
- **One engine in the install.** D84 already ships a Chromium for the browser, and keeping WebView2
  for the page would ship one engine and depend on another.
- **A machine prerequisite goes.** The shell refuses to start without the Evergreen runtime, and an
  engine in the install is one the artefact gate (D60) can start.

**The cost accepted.**
- The engine's security updates reach the page only through a Daoris release, since an embedded
  engine is pinned by the build.
- The host code Shenora gave for WebView2 is Daoris's to write until the harvest.
- A browser's basics come back to Daoris, because an embedded browser's chrome is Daoris's to draw.
  BRW9 was retired under D84 because a separate browser has its own, and it is refiled after CHR3.

🔴 **The rule it must keep** (D78 §3.1): the page that holds the bridge is never in CDP's reach. An
embedded engine's debug port may be one setting per process. If so, the browser runs in a process of
its own, and that is CHR1's first question.

**Open** (answered by CHR1, below). The embedding library (CefSharp is the candidate; CHR1 measures
it), and with it the Chromium build and its media codecs.

**Rejected.**
- **A standalone Chromium Daoris starts** (D84's recommendation). It gives a browser for free, but the
  page cannot live in another program's window, so two engines would ship.
- **Waiting for Shenora to build it.** Shenora grows by harvest, so an adopter building it first is
  its model, and the owner said to start here.
- **Editing Shenora from here** (D32). The one entry in its backlog is the request, written at the
  owner's say-so, and Shenora's own session or owner commits or declines it.

**Amended the same day, by Shenora's answer** (the owner, in Shenora's session, as its backlog
records it). *The kit builds it now*, as a package of its own (`Shenora.Windows.Chromium` on
CefSharp), with the engine's bytes arriving through its upstream package. The first adopter *takes it
instead of writing its own host*. So *built here first* is withdrawn, the rejection of *waiting for
Shenora* no longer holds, and CHR2 waits on that release. **Measured by CHR1**
(`docs/2026-09-28-chromium-embedding-evidence.md`): the embedding library did everything asked; the
debug port is one setting per process and reached the page's bridge, so the browser runs in a process
of its own; and the build has no H.264, AAC or HEVC. The rest stands. The browser's form, Daoris's
chrome or the engine's own window, is CHR3's and the owner's.

## D86 — A repository's line is this machine's to set, and every door reads it from one place (2026-09-28)

**Decision (WSR2, from the owner's *"master branch can be different branch so this more focus on
default branch or setup by user"*).** The line a repository's work grows from and lands on is, in
order: what the person set **for that repository**, then what they set **for its workspace**, then
the checkout's own guess (`origin/HEAD`, else `main`, else `master`), which is the whole of what it
was before. It is kept in this machine's `driver.json` (`lines`, `workspaceLines`), set by `daoris
driver line` and by Settings → Workspace → *Lines* (D50), and answered with **what said so**. A
repository in no workspace takes the `default` workspace's line (D48 §2). `CanonicalLine` is the one
reader: a session tree's start, the merge door, a tree's removal, sync's feed, and through the tree's
start a chain's next step (D82). A name git would refuse is refused where it is typed, by the same
branch-name rule on both sides of the twin (`driverconfig.ts`, `CanonicalLine.cs`), and skipped where it
is read.

**Why.**
- **A shared repository's work does not always land on its default branch.** The first real
  workspace's team took work on a branch of its own, and the guess named another.
- **One reader, or the doors disagree.** A tree grown from one line and merged into another is the
  failure this prevents, and it is silent until the merge.
- **A line that is only on the remote is grown from `origin/<line>`**, and one that is nowhere is
  refused, naming the verb that changes it, before anything is created.

**Rejected.**
- **A declaration in the repository's own manifest.** Which line this machine's work lands on is
  wiring, like a remote (WSP1): another machine working on a team branch lands elsewhere, and a
  tracked field would make one person's choice everyone's. A repository that is not adopted (D70) has
  no manifest, and it is driven all the same.
- **Setting it only per workspace.** One repository in a circle can take work on a branch the others
  do not have, and the owner's words were about repositories.

## D87 — How work lands is a workspace's rule; Daoris merges or makes a branch, and a push is a plugin's (2026-09-28)

**Decision (WSR1, the owner's call).** Asked whether Daoris may ever push a session's branch and open
a pull request, the owner answered: *"this should be configurable, and lets say no push or open pr on
default but we should be able to support later for plugin to control since there will be different
platform for pr"*. So:
- **A workspace has a landing rule, and a repository may override it.** Two forms ship:
  - *merge*: today's door, into the repository's line (D86);
  - *branch*: the session's work is put on a new branch named by the rule's pattern, from the
    session's branch, which grew from the line. The person pushes it and opens the pull request.
  No rule is *merge*, which is today's behaviour.
- **Daoris itself never pushes and never opens a pull request.** D37 stands. The form that does is
  a **plugin's** (D64): the rule will name a plugin, and that plugin, speaking for its platform,
  pushes and opens the pull request. That form is not built yet (WSR4), and until it is, no rule can
  name one.
- **The branch form writes nothing to the checkout.** It creates a branch in the repository and
  moves no checkout, so the root may be dirty or on any branch. An existing branch of that name is
  refused and never moved.
- **The rule reaches every place work lands.** The review screen says what a press would do before
  it is pressed. The door applies the rule. The session's instruction says how its work will land.
  A chain's next step still starts on the step before's branch (D82), so landing the last step
  carries the chain.

**Why.** A shared repository takes work through review on its own platform, and a merge on one
person's machine skips it (the first real workspace, study §1). Platforms differ in how a pull request
is opened, which is the owner's reason for leaving that step to plugins, and a plugin is the seam
Daoris already has for behaviour it does not carry (D64).

**Rejected.**
- **A built-in push and pull request for one host.** It would make one platform the default and
  write a durable push authorisation into core, where D37 keeps push human.
- **Rebasing the work onto the line's current tip.** It rewrites the session's commits. The branch
  form keeps them as they were made, and the person's review shows where they started.

*Amended by D100 (WSR4, 2026-09-30): the plugin form is built. A branch rule may name a plugin, which
the landing speaks to on `work/land` once the branch exists; the plugin pushes and opens the pull
request, and a plugin that fails leaves the branch. Daoris itself still never pushes.*

*Amended by D109 (WSR6, 2026-09-30): the rejection of a rebase stands for the landing, which keeps the
session's commits as they were made. After the line moves, the person's press may replay a session branch
or a landed branch nobody pushed onto it, only its own commits.*

*Amended by LEFT2 (2026-09-30): the merge alone, the shell's `MERGE_SESSION_TREE`, is retired. Since WSR1
the page lands through `LAND_SESSION_TREE`, which merges only where the repository's rule says merge, and
nothing had called the merge alone since; kept, it was a door that merged into the line whatever the rule
said, and the terminal never had its twin (D50). The review's two acts are landing and discarding.*

## D88 — A session's branch goes once git proves its work is on a branch of the person's (2026-09-28)

**Decision (WSR3, from the owner's *"after merge to master or feature branch we should cleanup daoris
branches"*).**
- **The proof.** A session branch's work is landed when every commit on it is on a branch that is
  not Daoris's: a local branch outside `daoris/`, or a remote-tracking branch. The line, a feature
  branch the branch form made, and a branch the person pushed all count. Commits only Daoris's
  branches hold are unlanded, including a chain's earlier step, whose branch is also Daoris's. One
  proof serves every door: a tree's removal without `--force`, the tidy after a landing, and the
  clean-up.
- **The tidy is the person's rule.** A landing rule may say `tidy`: once a press lands the work,
  its tree and branch go, if the proof holds. Without it, the tree stays, as it always has.
- **The clean-up is the person's press.** Every session branch on this machine is listed first with
  what it holds: landed, empty, unlanded, holding uncommitted work, or in use by a session still
  running or waiting. Then one press removes those the proof clears, checking each again right
  before it goes. Unlanded work, uncommitted work and a session in use are kept and named. The
  terminal twin is `daoris-driver trees clean`, which lists and, with `--yes`, removes.

This amends **D51 rule 7** (*nothing deletes itself*): a removal now happens only by a person's press
or a rule the person set, and always behind the proof.

**Why.** Branches piled up: FG5 left sixteen empty ones in one repository before they were deleted
by hand. The line alone is the wrong proof once work lands on a feature branch: it would call that
work unlanded forever, and a person forcing removals by hand is the risk the proof is for.

**Rejected.**
- **Proving against the line only.** It refuses every branch the branch form landed (D87).
- **Deleting a branch git calls merged (`branch -d`).** Git asks only about the checkout's HEAD, so a
  branch landed on a feature branch would stay, and one merged into whatever the checkout happens to
  be on would go.

*Amended by D102 (WSR5a, 2026-09-30): the clean-up also lists the branches landings made and recorded,
in a group of their own, and the same press removes those whose work reads on the line by content, or
that are inside another that does. The session branches go first, since this proof may count a landed
branch as holding their commits.*

## D89 — Ask Daoris is a session that proposes, on an agent of its own choosing, and every change is confirmed (2026-09-29)

**Decision (HELP1, the owner's calls on `docs/2026-09-29-ask-daoris-design.md` §8).** Ask Daoris is a
conversation with a harness session in a room of its own (`<home>/help/`), opened from the app strip,
the palette and F1 into the right dock. It reads, and it proposes. A setting is proposed through the
same route the screen uses, and an ask through the ask door. **It runs on an agent of its own**, named
under Settings → *Daoris's own AI* as a third job, off until named, as the intake is. **Every change is
confirmed**: its card says what it changes and the terminal command that does the same, and nothing
applies until the person presses Apply. With no agent named, it offers starters from what the machine
lacks, each a door to the screen that fixes it (D24's no-model tier).

**Why.** The owner, setting the first real workspace's landing rule: *"we will need some chat agent to
support configure for workspace"*. Proposing through the screen's own routes keeps what it can do
exactly what the person can do (D50), and PERM2 already settled that an agent proposes and the person
applies (D74).

**Rejected.**
- **Following the intake's agent.** Answering asks and helping a person are two jobs that may want
  two agents, and changing one must not quietly move the other (the intake's own reasoning, INT4b).
- **Remembering a confirmation per kind of change.** Not in this build: every change is the person's
  press.
- **Running `daoris` commands from the room.** The install does not carry the CLI, and a helper that
  ran commands would be a second door with its own approval problem.

**As built (HELP1a, 2026-09-29): its session is the one exception to D81.** Every other session runs
in its harness's own `auto` mode; Ask Daoris's asks the agent for its own asking mode (`default`)
where the agent offers it. The first real conversation ran shell commands under `auto`, reading a
checkout to research its answer, which showed the room's allow-list is no gate in a mode that judges
its own actions — and a helper able to run a command could run `daoris driver …`, the change nobody
confirmed that this decision forbids. In `default` every tool off the allow-list asks, and over the
protocol door every ask is refused by construction (D52).

*Amended by D107 (READ1, 2026-09-30): what it is handed at spawn also reads each checkout reading across
allows, by file and by two git commands on exact prefixes, and its room names those checkouts' paths. It
still has no shell, and it still runs in `default`.*

## D90 — A working driven session hears what the person adds, as its next prompt (2026-09-29)

**Decision (SESS3, amending INT4i).** On the protocol door, a driven quest session has an inbox. What
the person tells it while it works is held there, and when its turn ends the driver prompts it with
each held message in the same session, instead of closing it. *Send now* stops the running turn
(`session/cancel`, which keeps the session), so a held message goes at once; with nothing held it
stops nothing. The words are recorded as the person's when they are handed over. An inbox that is
closing refuses a late message and the person is told, and a session that fails says how many
messages never reached it. **What INT4i protects stands**: nothing is ever written into a stream that
carries the driver's frames, a finish is still refused, and an intake still takes nothing (INT4h).
The pipe door is unchanged: its process has no stdin, so the page offers it no box.

**Why.** The owner, 2026-09-29: *"there is no way to send additional info in middle of the session"*.
A prompt between turns is the protocol's own way of adding words, so the session keeps its context and
its tree, which a carry-on session would have to rebuild.

**Rejected.**
- **Writing the words into the running turn.** No harness guarantees what it does with a line
  mid-turn, and on this door the stream is the driver's (D53, INT4i).
- **A carry-on session for every message.** It loses the running session's context for a sentence.
  It stays the parked answer's path (STANDDOWN2) and the pipe door's only possible one, not built.
- **A conversation's stop semantics**, which hand queued words back (CONV4a): here the person stops
  the turn so their words go, so nothing is withdrawn.

## D91 — What depends on what is declared by the repository that depends, as `domain.uses` (2026-09-30)

**Decision (MAP4e, amending D34).** A repository's `domain` in its `daoris.json` may carry `uses`: the
repositories it depends on, each by the name the registry knows it by. Absent is the same as empty:
nothing declared. The CLI sends it with `connect`, the service keeps it with the registration and
reads it on import, the desktop's sync carries it to a circle's deployment, and the map draws each one
as a line of its own, *says it uses*, from the repository that declared it. A name the registry does
not hold draws nothing; a repository naming itself is dropped. It is not a declaration on its own:
`connect` still refuses a domain that says nothing about the repository itself (D35).

**Why.** The study asked the map for *"what depends on what"* from a declared source, and nothing
guessed from names (`docs/2026-09-28-after-the-first-workspace.md` §6). D34's reasoning is the
reasoning here: a relationship between repositories is data about the repository that has it, kept
next to it and reviewed by the people it describes, and a central list drifts. The repository that
depends is the one that knows it does.

**Declarations are drawn, now, as declarations.** The map's rule was that *owns* and *accepts* are a
node's detail and the edges are what happened (map design §1). A dependency is a relationship, so it
is a line, but it is a line of its own kind, named for what it is (*says it uses*), switchable with the
rest, and never merged with the quests that actually moved.

**Rejected.**
- **Package references** (a `package.json` dependency, a `PackageReference`). Mapping a package to the
  repository that publishes it is a guess unless each repository declares what it publishes, which is
  the declaration this is, one step removed and per stack.
- **The code maps' dependencies** (MAP3). They are modules inside one repository, and say nothing about
  another.
- **Inferring it from the quests.** That is what the quest lines already draw, and it is what happened,
  not what depends.

## D92 — The page moves onto Shenora's Chromium now, and the browser follows when the kit can host it (2026-09-30)

**Decision (the owner's choice, amending D85's *one engine*).** Shenora 0.17 ships the host CHR2 waited
on, as `ChromiumView` on the kit's own CEF binding (CEF 154), not the CefSharp package D85 expected
(`docs/2026-09-28-chromium-host-design.md` §4a). Asked whether to move the page now with two engines in
the install, move the browser onto Daoris's own chrome, or wait, the owner chose **the page now, two
engines**: the main and secondary windows move to `ChromiumView`, and `daoris-browser` stays on CefSharp
until the kit's engine can host it. Asked *"can we use one chromium?"*, the answer is not on 0.17, and the
owner said to file what is missing in Shenora's backlog (*"whats the limitation can you file this to
shenoras TASKS.md?"*). It is filed there as one uncommitted entry, with nothing else in that repository
touched: a browser-only engine with Chrome-style windows made over CDP, a production debug port for a
process that holds no bridge, the engine settings the browser sets, and one CEF layout on disk.

**The page lives on the app's origin now.** A `ChromiumView` serves the bundle from a folder at
`https://{VirtualHost}/`, where WebView2 showed the host's URL. The page reaches its host at the loopback
address, cross-origin, which is the kit's *server-backed profile*: the shell gives the page the host's
address, the page accepts only a loopback one and only in the shell, and the host allows that one origin
in local mode. In a browser the page keeps calling its own origin.

**Why.** The page's move drops the WebView2 runtime as a machine prerequisite and lets the artefact gate
(D60) start the engine it ships. Waiting would have held that on a feature the kit has not planned.

**The cost accepted.** Two Chromium builds in the install, about 350 MB each on disk, until the browser
moves (CHR8). A DevTools port opens only in development, so looking at the install means starting it as
development, and a published app has none (D78 §3.1 holds by construction).

**Rejected.**
- **The browser on Daoris's own chrome around an Alloy view**, for one engine now. It reverses CHR3's
  chosen form and rebuilds a browser's basics.
- **Waiting for the kit**, which leaves the WebView2 prerequisite in place with no date to lose it.

*Amended by D99 (CHR8, 2026-09-30): the browser followed on Shenora 0.18's `ChromiumBrowserProcess`,
as the application's own executable started with `--daoris-browser`. One engine in the install again.*

## D93 — An install is a launcher, `app/` and `data/`, and the one thing to run is `Daoris.exe` (2026-09-30)

**Decision (CHR4, amending D60's layout).** On the Chromium the shell ships (D92), the application's
executable is CEF's launcher, and it has to sit beside `libcef.dll`, the engine's resources and the app's
own libraries: about thirty files and a `locales/` folder. They go in **`app/`**, and the install's root
holds **`Daoris.exe`**, a small launcher, beside `app/`, `data/` and the marker. The application is
`app/Daoris.Desktop.exe`; the browser and the HTTP host keep folders of their own under `app/`. This is
the owner's direction twice over: *"the main entire app should just call Daoris.exe like regular app"*,
and *"for app folder structure you can follow [a sibling application] which is properly structured"* —
whose install is exactly this: a small launcher at the root, the application in `app/`, `data/` beside.

**How the pieces find each other.** The launcher starts `app/Daoris.Desktop.exe` with its arguments and
environment and exits; it references nothing, so it is framework-dependent, single-file and about
220 KB, and it wears Daoris's icon. The application finds the install above its own folder
(`InstallHome.RootOf`: an `app` folder under a marked install), so the home is still the install's
`data/`, and the kit is handed the same root so its data area is not a second `data/` inside `app/`.
The host and the browser were already found beside the shell's own folder. The app's assembly is
`Daoris.Desktop.App`, so CEF's launcher is `Daoris.Desktop.exe`, and the build stamps Daoris's icon and
version info onto it (`StampIdentity.targets`): a copied launcher carries CEF's, and Task Manager and
the taskbar named the window "CEF Bootstrap Application".

**What still holds from D60.** One executable at the root, nothing else a person could double-click,
no symbols and no package doc files, and a publish that never writes over a name it did not write —
the root's names are fixed again. The application's names in `app/` are recorded in
`app/shell-files.txt`; a republish removes exactly those, and the single-file shell's root
`daoris-desktop.exe`, then places the new set, so an engine upgrade leaves nothing of the last one.

**What the tools had to learn.** Chromium starts its renderer, GPU and utility processes from the
application's own executable, with `--type=`. A stop that walked every process from that path waited
fifteen seconds on each windowless one and then force-killed it, which crashes a page. The tools tell
the application from the engine's processes, and the deployment gate asserts the engine by the same
tree: a renderer from the install's executable, and no WebView2 process under the shell. The gate starts
the install the way a person does, through the launcher, and asserts that the launcher has gone.

**Rejected.**
- **A Chromium application's root**, `Daoris.exe` beside its engine as VS Code's `Code.exe` is. Built
  and proven first (52/52), then set aside for the structure above: a busy root, and `--beside` would
  have had to learn every name the engine writes.
- **A native launcher that installs .NET when it is missing**, as the sibling's does. Daoris has
  required .NET since D46 and has never offered to install it; that is a separate question.
- **Keeping `daoris-desktop.exe`.** The application is named for itself.

*Amended by SHEN1 (2026-09-30): Shenora 0.18 lays the launcher out wearing the app assembly's icon,
title, product and version, so `StampIdentity.targets` is gone and the app project names `Daoris` as
its title and product. The kit also lays out only the locales the project names
(`ShenoraChromiumLocales`), which the publish script had trimmed by hand; the script still trims the
browser's own engine until CHR8.*

*Amended by D99 (CHR8, 2026-09-30): the browser has no folder under `app/`; it is the application
started with `--daoris-browser`, and a republish removes the old `app/daoris-browser/` by name. The
script still trims the application's `locales/`, of the grammatical-gender stubs the kit lays out
beside each language.*

*Extended by D108 (TASKBAR1, 2026-09-30): an install's windows name Daoris's taskbar id and a relaunch
command that starts `Daoris.exe` at the root, so a pin made from the running window is the launcher.*

## D94 — The machine keeps a log of what happens on it, without anyone's words, and nothing sends it anywhere (2026-09-30)

**Decision (LOG1).** Every Daoris process writes what happens to it into the home's `logs/`: one JSON
line per event, one file per process kind per day (`<date>.<source>.jsonl`, sources `desktop`,
`host`, `mcp`, `browser`, `driver`), kept thirty days and capped at 20 MB a file. The events are a catalogue:
the lifecycle, every unhandled exception, the logging frameworks' warnings and errors, what the person
runs and how long each part of it takes (a session opening, a turn's first answer, its end), the
refusals they meet by code, and the page's own report of views, commands and messages sent. Two doors
read it (`daoris-driver logs`, a Settings domain), and `tools/usage-report.mjs` summarises an install's
log for a development session. The owner's ask: *"setup proper logging system to moniter my use in
local daoris and we can improve the system by this way"*. The contract is
`docs/2026-09-30-machine-log-design.md`.

**Why.** Nothing but the session transcripts outlived a process: the shell's, the host's and the
driver loop's own lines went to consoles nobody has once the application is installed, and an
unhandled exception left no trace. HELP4's six-second wait to open a conversation was found by a person
noticing; the log times it every time.

**What is never logged**: anyone's words (a message, a prompt, an agent's answer, a tool's input or
output, a quest's title or body, a search), a file's contents, any secret, a URL's query or a visited
page. The words already live in the session's record and transcript; a log holding them would be a
second copy under none of the record's rules. The page is the one writer that could pass a word by
mistake, so the module that takes its events keeps only the catalogue's names and fields.

**Where it stays.** On the machine, like the transcript beside it (D47 §4): no HTTP route serves it,
a browser and a remote see none of it, and there is no telemetry. A process with no home writes no log
and runs anyway; a write that fails is dropped, never thrown.

**Rejected.**
- **One file for every process.** Appending from several processes can overwrite lines without a lock,
  and the artefacts share no code to hold one; the format is the contract between them (the twins
  rule).
- **The words, for a richer report.** The report counts and times; the words are one click away in the
  session, under the record's rules.
- **A logging library.** A file a day and a catalogue are each artefact's own few lines, and a library's
  configuration is one more thing an install carries.

*Amended by LEFT3 (2026-10-01): `proposal.settled` is written once a press settled the proposal. Ask Daoris's sync
card has two presses, and its first, the look, settles nothing: `HELP_APPLY` answers that the card `stands`, and the
page writes no line for it. No event of its own was added for a look: it is a step of one proposal whose settling is
still written, by its press or its Not now, and a look that found nothing to do settled the card and is written as
any Apply is. And `tools/usage-report.mjs` summarises LEFT2's `preview.opened` under what was used most: how many
previews, in how many sessions, and of what kind by extension, never the path.*

*Amended by UNBLOCK5 (2026-10-01, D122 §3.10): the catalogue gains `permission.refused` {session, adapter, tool,
kind, by}, one line per call a session's record marks `refused`, the first time: on the protocol door a call whose
permission request the driver refused (D52), on the native door a call the harness reported denied
(`permission_denied`, and the result's `permission_denials`). `tool` and `by` are the wire's identifiers where it
gives them (only the native door does) and null otherwise, and a value that is not an identifier is written as null,
so the line carries no command. The usage report gains asks per session by adapter and repository, and the rule
proposals under `proposals/` by the week they were made and their state, counted from the files and printing none of
their words. The machine-log design's §4 and §6 say, as built, what each reads.*

*Amended by PLUGUI1d (2026-10-01, D119 §4.2): the catalogue gains seven `plugin.*` events, `started`, `stopped`,
`called`, `failed`, `served`, `tried` and `tested`, written by one writer in the driver library (`PluginLog`) from the
hook set, a landing, a hand-off, the driver's handing of servers and the terminal's trial. Each is names, counts, flags
and times: an answer is its word (`allow`, `hold`, `pushed`…), never its reason, message or pull request, and nothing a
plugin wrote to stderr reaches a line, since no writer takes words as a parameter. `stopped` and `failed` carry `by`
as `started` does, so a reader can tell the loop's process from a landing's one frame. `tested` has its shape and no
writer until PLUGUI1g. The machine-log design's §4 says, as built, what each line measures.*

*Amended by D125 (TOOL4d and TOOL4f, 2026-10-02): the catalogue gains three account events, `account.limited`,
`starts.waiting` and `account.rotated` {session, adapter, from, to, carries}, each naming an account by its profile
name and never a key, its handle, who signed in or the agent's sentence. The machine-log design's §4 says, as built,
what each line measures.*

## D95 — A quest nobody has started on can be deleted, and the delete travels as an operation (2026-09-30)

**Decision (QUEST1).** The owner: *"we do need way to clear or delete quest"*. Clearing is what the
list already does: a closed quest leaves it, and since USE1c so does a done ask. Deleting is new. It
removes a quest or an ask made by mistake, a duplicate or a test, from the ledger, and it removes only
a record that no work stands on:

- **A quest is deleted only while it is open and nobody has started on it.** No session record names
  it, and no taken quest waits on it as its question (D79). The log is enough to know an open quest
  was never taken, because nothing moves back to open. A taken quest is someone's work in a tree. A
  done one is the record of that work, and a declined one is the trace of a decision whose reason is
  the asker's to read. Each keeps its record, and the refusal names what to do instead: decline it
  with the reason, or leave it closed, since a closed quest already leaves the list.
- **An ask is deleted with every quest asked by it, or not at all.** One that must stay keeps the
  whole ask, and the refusal names it and says to close the ask instead. An ask that became nothing
  goes alone. Its kept files go with it, and so do a deleted quest's, on this machine. An intake
  session that served the ask keeps its record. A running one finds the ask gone and stands down, as
  it does when the ask is closed under it, and the driver's next tick ends a parked one as stopped.
  A quest deleted from an ask leaves the ask as if it had never become that quest, read the way
  USE1c reads done. A published ask left with none is a proposal again.
- **A delete is an operation, `deleted`, a tombstone in the quest's history.** It applies only to an
  open quest and replays to no quest. It is pushed like any operation, and a machine that fetches it
  drops the quest. The remote keeps it, so no fetch from any cursor brings the quest back, a new
  machine's first fetch from zero included. That is proved in the sync suite over the real wire. A
  publish after it applies, so the same words asked again make the quest again, under the same id.
- **A quest that never left the machine simply goes.** Its circle has no remote or its receiver is not
  joined, and no remote numbered any of its history. Its operations and its row are then removed,
  because nothing anywhere holds a copy to bring it back from.
- **A shared quest is deleted by push, as a take claims by push (D69).** The delete commits here and
  the pass runs before the answer returns. The answer is one of three. The remote confirmed it. A take
  from another machine reached the remote first, so the quest stays, taken, and the answer says to
  decline it instead. Or the remote could not be reached, so the delete is unconfirmed and travels on
  the next pass.
- **First push wins, as for a take (D68 §5).** A delete that reaches the remote after a take is
  dropped by the rebase. This amends the sync design's §8, which named two things a rebase drops; this
  is a third. The delete's condition, that nobody has taken the quest, stopped holding, and a delete
  carries no work for a person to reconcile. A take that reaches the remote after a delete becomes a
  conflict, as any losing take does. Its machine's claim reads lost, and its driver stops the session
  with the lost-claim sentence, which names a take. The conflict stays in the log at the remote and on
  the machines, on a quest no list shows. Two deletes of one quest are one.
- **Two doors (D50).** The screen has a *Delete* in the quest drawer and on an ask's record, shown only
  when the service says the record may go, and confirmed once. The terminal has `daoris-driver quest
  delete <id>` and `daoris-driver ask --delete <id>`, beside `ask --close`. Both go through the local
  host's `DELETE /api/quests/{id}` and `DELETE /api/asks/{id}`, and the service's sentence reaches the
  person verbatim.

**Why.** A test or a duplicate sits in every list and count until someone declines it, and a decline
says *we will not do this*, which is not what happened. A record that work stands on is different: a
session's record, a tree, a waiting quest and an asker's reason all point at it. Deleting any of those
would leave something pointing at nothing. So the rule deletes exactly what nobody has touched, and
everything else keeps the decline it already had. The delete travels as an operation because the sync
is operations (D68). The remote keeps what it accepted, and a machine fetches by cursor, so an absence
is not something either can carry.

**Rejected.**
- **A removal the sync carries by absence.** The remote and every machine that had synced would keep
  the quest, and a new machine's first fetch would bring it back. That is the resurrection the owner
  ruled out.
- **A fifth status, `Deleted`, kept on the row.** Every list, count and door would have to learn to skip
  it. That is status for its own sake, which D46 §3 refused. The row goes, and the history keeps the
  tombstone.
- **Deleting a taken, done or declined quest.** Its session record, its commits and its reason would
  point at nothing, and a decline, or a close that already hides it, says what a delete would try to.
- **A lost delete kept as a conflict.** A conflict names the status it attempted, and a delete attempts
  none. It carries no work to reconcile either. Online, the person hears it at once, and the quest
  standing taken is the record of why it stayed.
- **Delete over MCP.** An agent's tools are for its work, and removing a record from the ledger is a
  person's act (D37). A shared deployment has no delete door either: a person deletes on their own
  machine, and the tombstone travels.

## D96 — The console panel gains a terminal: a real shell of the person's own, beside the sessions' read-only streams (2026-09-30)

**Decision (CONSOLE4).** The console panel holds a terminal view: a shell under a Windows
pseudo-console, drawn by a terminal renderer in the page, so it behaves as a console window does —
prompts, colours, Ctrl+C, history, tab completion, full-screen programs. PowerShell 7 when installed,
else Windows PowerShell, with the machine's other shells to choose; started in the attended session's
tree, else the workspace's first repository, else the home; the install's `DAORIS_HOME` in its
environment so the `daoris` CLI answers for this machine. Several at once, each a tab, each ended with
its tab and all with the application. The owner's ask: *"we also need to make input line for console
too, so we can control console just like regular console window (more into powershell style)"*. The
contract is `docs/2026-09-30-terminal-design.md`.

**What does not change.** A session's streams stay read-only and the composer stays the one way a
session is spoken to: a keystroke into an agent's input could answer a permission prompt the driver
refuses by construction (D52). The terminal is desktop-only, over the bridge (D47 §4), and a
terminal's words never reach the machine log (D94).

**Rejected.**
- **A line-only input with no pseudo-console**: prompts, progress bars and full-screen programs
  break, and *just like a console window* is the requirement.
- **A native terminal package**: the pseudo-console is a few Win32 calls the driver library can make.
- **Typing into a session's console**: the composer is that door.

*Amended by D121 (TOOLS5, 2026-10-01): the shells come from the tools. PowerShell 7 is the file the tools resolve,
and is not offered where its way cannot run. Git Bash is the bash beside the git the tools resolve, else beside the
system's git. Every shell starts with the tools' environment.*

## D97 — A first start opens on a setup guide built from the facts Ask Daoris's starters read (2026-09-30)

**Decision (SETUP1).** *Get started* is the first domain in Settings: the steps a machine needs, in
order — an agent with an account signed in, Daoris's own agent, a workspace and its repositories, what
is driven, how work lands, and (optionally) what agents may do — each with its state read off the
machine, the screen that does it and the terminal command that does the same (D50). A machine missing
any of the first three opens on it at start until they are done or the person turns that off; the
Daoris menu, the palette and Ask Daoris's starters lead to it after. Its head offers *Set up with Ask
Daoris*, whose proposals are confirmed like every other (HELP1c). The owner's asks: *"we also will
need a setup guide for first use daoris, so setup agent and agent for "daoris" … and other rules"*
and *"we need to have a good ui/ux or easy access for everything use ask daoris"*. The contract is
`docs/2026-09-30-setup-guide-design.md`.

**Why this shape.** Every step already has a screen; the guide orders them and says which is done. One
pure reading of the machine serves the guide and the starters, so the two cannot disagree.

**Rejected.** A wizard of modal screens (a second copy of every setting, drifting from the first), and
doing the setup for the person (signing in and choosing what is driven are theirs).

## D98 — An agent's model and effort are the person's to set from Daoris, in the tool's own terms (2026-09-30)

**Decision (AGT6).** This reverses D49's *no model selection UI* (interactive design §7) on the
owner's word: *"we also need way to adjust the claude setup (since we have command to setup model
effort or other setting in console but no way in daoris rn)"*. They had typed `/model` into a driven
session's message box, which cut its turn short. There are two levels, and each uses the tool's own
mechanism:

- **An account's defaults** live in the tool's own settings file under the account. For Claude Code
  that is `settings.json` in the configuration home: `model`, `effortLevel`, and
  `modelSettings.<model>.effortLevel`, which the tool reads first for that model. This was read from
  `claude-agent-acp` 0.84.0's own settings reader and the Agent SDK 0.3.284's settings schema, not
  guessed. There are two doors (D50): Settings → Agents & accounts → *Model & effort*, and `daoris agent
  settings <agent> [--account <name>] [model <v>] [effort <v> [--for <model>]]`. They are twins
  (`agentsettings.ts`, `AgentSettings.cs`) holding seven rules, and a write moves only the keys it
  names. The choices offered are the tool's own: its SDK's aliases, a free field for a full id, and
  the four efforts its settings keep.
- **One conversation's model and effort** use the protocol door. The agent offers config options on
  `session/new`; the ones in the `model` and `thought_level` categories are offered beside that
  conversation's composer and changed with `session/set_config_option`. The driver keeps each
  session's options as the agent last said them: on `session/new`, in its answer to a change, and in
  its own `config_option_update`. The page asks with `SESSION_OPTIONS`, follows
  `SESSION_OPTIONS_CHANGED`, and changes one with `SET_SESSION_OPTION`. Each change is a note in the
  conversation's record, saying the person made it.

**How D24 and `model-decoupling` still hold.** Daoris names no model and chooses none. Every alias,
effort and option is the tool's own word, read from its artefacts or its wire, and a person picks
one. *The deployment chooses*, and for an agent a person runs, the deployment is the person. Nothing
of Daoris's own uses a model because of this, and no Daoris feature needs one. D57's rejected
registry stays rejected: the list is the tool's, and the free field takes anything the tool accepts.

**Boundaries.**
- **The tool's own configuration home is never written.** The roster already says Daoris never
  touches it, and the tool's own `/model` and `/config` set it.
- **A tool whose settings Daoris has not read (Codex, dsh) is offered nothing** for its accounts, and
  the surface says so in one line. No keys are invented.
- **The session's mode is not offered.** It is the posture D37 and D81 set, so only the model and
  thought-level categories are, and the driver refuses any other option.
- **`max` is refused as an account's default**, because the tool's settings never keep it. A
  conversation can still take it, where the agent offers it.
- dsh's one config option is its model catalogue (ACP3 evidence §1). Daoris still never turns it
  itself. The person may, for one conversation.

**What the gates do not cover.**
- Whether a real Claude Code reads a key Daoris wrote the same way as one it wrote itself. It is the
  tool's own schema, read from its SDK's types and its adapter's reader.
- The cascade: a repository's own `.claude/settings.json`, and `ANTHROPIC_MODEL`, over the account.
  The variable's precedence is read in the adapter's code. The tiers' order is read from the SDK's
  documentation of them, and not observed.
- A real `session/set_config_option` against the real adapter. It was read from its source at 0.84.0,
  and the stub speaks that shape.
- The alias list is the SDK's at 0.3.284, and moves only when a person changes both twins.

**Rejected.**
- **Typing `/model` into a conversation.** It was the owner's attempt, and it cut the turn short. A
  slash command belongs to the tool's own console, not to a message.
- **A model list Daoris maintains.** This is D57's registry, rejected on the owner's word.
- **Writing the tool's own home when no account is named.** It is the one place the roster promises
  Daoris never touches.
- **`ANTHROPIC_MODEL` at spawn.** It would outrank the person's own settings wherever the tool runs
  under Daoris, and it would be Daoris's setting rather than the tool's.

## D99 — Daoris's browser is the application started as a browser, on the kit's engine: one Chromium (2026-09-30)

**Decision (CHR8, closing D92's two engines).** `daoris-browser` stops being an executable of its own on
CefSharp with a CEF of its own. It is the application's own executable, `Daoris.Desktop.exe`, started
with `--daoris-browser` first. `Program.Main` recognises that before anything else and hands the process
to Shenora 0.18's `ChromiumBrowserProcess.Run`, which runs Chromium as a browser: Chromium's own windows
with their tabs, history, find, downloads and devtools, and a debug port open in production because the
process holds no page of the app's. The shell starts it with `ChromiumBrowserProcess.Start`, never
`Process.Start`. What `daoris-browser` set by hand are the kit's options now: the profile folder under
the home (`<home>/browser/engine`, whose `Default` is the profile, D63), the locale,
`PersistSessionCookies`, and the port it is handed, which the kit's relay serves, announcing a new tab as
a `page` from the start, so Daoris's own `CdpRelay` goes. Daoris still prepares the profile before the
engine reads it (the favorites folder, CHR5; the extensions setting, CHR7), makes the first window over
the port, in the background when a session asked (CHR3), and stops the browser once the shell that
started it has gone. The install loses `app/daoris-browser/` and its second Chromium.

**The contract that changed.** `EngineBrowserOptions` carries the same four things (profile, port,
parent, background), spelled `--daoris-profile=<folder>`, `--daoris-port=<n>`, `--daoris-parent=<pid>`
and `--daoris-background` behind `--daoris-browser`. The browser's command line is Chromium's too now,
and Chromium takes a switch's value only after `=`: `--profile <folder>` would reach it as an empty
switch and a loose argument. The prefix keeps every name clear of Chromium's own. The routing argument
comes **first**, because Chromium starts its renderer, GPU and utility processes from the same
executable with `--type=` first, and those belong to the Chromium that started them.

**Why.** D92 accepted two engines only until the kit could host the browser, and 0.18 does, in the form
CHR3 chose. One engine is one Chromium to keep patched, one CEF on disk (about 350 MB less), and one set
of locales. `Process.Start` is ruled out by the kit's own measurement: on Windows it hands the child
every handle inheritable at that moment, a pipe end of Chromium's among them, and a browser that
outlives the app by design then kept the app from returning from Chromium's shutdown.

**What the tools had to learn.** The browser is a fourth kind of process on the application's path,
beside the application and the engine's `--type=` processes. `tools/processes.mjs` tells it apart by its
first argument (`BROWSER_ARGUMENT`, a twin of `EngineBrowser.Argument`); a stop of the application never
walks it, because it has windows of its own and follows the shell out by itself; and
`shot --window browser` names it by process id. A republish removes `app/daoris-browser/` by name, as
D93 removes the retired launcher (`RETIRED_IN_APP`). The publish still trims the application's
`locales/`: the kit lays out only the languages the project names, and puts three 18-byte
grammatical-gender stubs beside each, which D93's amendment did not see.

**Rejected.**
- **A thin `daoris-browser.exe` on the kit's engine.** A second executable needs a CEF layout of its own
  beside it, which is the second engine this removes; the kit runs the browser from the app's own
  executable so that an install carries one.
- **`StartUrl` for the first window.** It cannot ask for a window in the background, which a session's
  bring-up does, and making it over the port is how an agent's windows are made too.
- **The two-word arguments as they were.** They would reach Chromium as loose arguments, which Chrome
  takes as pages to open. Nothing measured them harmless, and one character removes the question.
- **The argument recognised anywhere on the command line.** An engine process that carried it would be
  taken for the browser and refused by the strict parse: a renderer that never starts.

**Not verified when it was written.** No window was started: the owner's install was running on the
machine. The deployment rehearsal is what proves the browser comes up from the install's application,
on its profile under the home, with its engine processes under it, and goes with the shell. **Open:**
the person's Edge (BRW12) is still started with `Process.Start` and outlives the app by the same
design; whether it can hold the app's exit the same way was not measured.

## D100 — A branch rule may name a plugin that pushes its branch and opens the pull request (2026-09-30)

**Decision (WSR4, amending D87).** D87 left the push and the pull request to a plugin *"since there will
be different platforms"*, off by default and configurable per workspace. That form is built:

- **The rule names the plugin.** A branch rule carries `plugin`, a plugin's id, beside its `pattern`:
  `{ "form": "branch", "pattern": "feature/{quest}-{slug}", "plugin": "github-pull-request" }` in
  `driver.json`, `daoris driver landing … branch <pattern> --plugin <id>` at a terminal, and a *who
  pushes it* chooser beside the pattern in Settings → Workspace → How work lands, which offers only the
  plugins here that land work. Absent is the person pushing, as it always was: nothing pushes by default.
  Only the branch form may name one, because the plugin starts from the branch Daoris made and a merge
  makes none. `LandingRules.Problem` refuses a merge naming a plugin, and an id that is not a plugin's
  shape, so a rule never names a path.
- **The plugin must be able to land work here.** It must be installed, switched on, sound, and declare
  the point `work/land`. Each is refused in its own sentence when the rule is set (both doors), again in
  the review's plan before the press, and again at the press, before anything is made. The file itself
  still holds a rule whose plugin went away, because it stays what the person wrote.
- **The hook.** `work/land` is a new point on the hook wire (D64 §4), and the first that *acts*: after
  Daoris has made the branch, and only then, the landing starts the plugin's process for that one landing
  (a terminal's `trees land` has no loop to borrow one from), completes the handshake, sends one frame
  and stops it. The loop never starts a plugin that speaks only here, so installing one runs nothing.
  The frame, `hook/work/land`: `repository`, `workspace`, `root` (the repository's checkout here),
  `branch` (the one just made), `base` (the line it grew from, or null), `title` (the chain's first
  quest's title, else what the person first said), `quest` (`{ id, title }`, or null for a
  conversation), `session`, and `commits` (`[{ sha, subject }]`, oldest first).
- **The result.** `{ "pushed": bool, "pullRequest": "https://…"|null, "message": "…" }`. `pushed` is
  required, a pull request must be an absolute web address, and the sentence is the plugin's own.
  Anything else is not an answer. The plugin has two minutes, because a push and a platform's API are
  round trips a person is waiting on.
- **Where it is said and kept.** In the landing's own sentence: the review's Accept result, with the pull
  request as a link that opens where the person's links open (BRW7), and `daoris-driver trees land`'s
  line, whose exit is 1 when the plugin did not push. The whole sentence is kept as a **note in the
  conversation's record** (D76), not on the session record: a finished session's record does not move
  (the ledger refuses it), and a plugin's word is transcript-class material that stays on the machine
  (D64 §4), where the session record travels to the team's remote.
- **A plugin that fails leaves the branch.** One that cannot start, answers late or wrongly, refuses the
  call, or answers that it did not push never undoes anything. The branch stands, the landing is still a
  landing, and the sentence says the plugin's step failed, in its words or Daoris's, with
  `git push -u origin <branch>` for doing it by hand.
- **Two example plugins, tracked and inert until installed and named:** `examples/plugins/github-pull-request`
  (`git push -u origin <branch>`, then `gh pr create --head … --title … --body-file … --base …`) and
  `examples/plugins/azure-devops-pull-request` (`git push`, then `az repos pr create --source-branch …
  --title … --target-branch … --description …`, the organization and project detected from `origin`).
  Each names no remote, organisation or project.

**Why.** D37 keeps a push human, and D87 answered how with a seam rather than an exception: the person
installs a plugin and names it in a rule, which is the durable authorisation, and the plugin's own
process pushes with whatever its platform's tools are signed in as. Daoris runs no `git push` and no
platform CLI, so no platform becomes Daoris's default, and a new one arrives as a folder (D64).

**Rejected.**
- **A landing form of its own (`form: "pull-request"`).** Every such form is a branch first. A form would
  repeat the pattern, the tidy and the refusals, where a field on the branch rule adds one thing.
- **A nested `after: { plugin }` object.** One field has no reason to be an object. A plugin's own
  options (a draft, reviewers) are the plugin's to keep in its data folder, not the rule's.
- **The loop's running hook process.** A terminal landing has no loop, and a landing-only plugin kept
  running beside the ticks would idle. The process exists for exactly the one frame it answers.
- **The session record's evidence.** The ledger refuses to move a finished session, and the plugin's
  sentence would travel to the team's remote, where D64 keeps it on the machine.
- **Undoing the branch when the plugin fails.** It would delete work the person just accepted to repair
  a step that can be done by hand. The branch is Daoris's act and stands on its own.

**What the gates do not cover.** Neither example has been run against a real platform: the arguments are
`gh pr create`'s and `az repos pr create`'s documented ones, and the tests drive each against a bare
repository and a fake `gh` or `az` that record their arguments, never a network. On Windows, `az` is a
`.cmd` script, and cmd reads what it is handed a second time, so both examples quote every argument,
refuse one holding a double quote, a percent sign or a line break, and respell those in a title. That
this is enough for `az.cmd`'s own `%*` was reasoned from how cmd parses, not measured against a real
`az`. Ask Daoris cannot yet propose a rule naming a plugin: its parser takes `branch <pattern>` and
`--tidy`, and refuses the rest as a pattern.

*Amended by D102 (WSR5b, 2026-09-30): a branch a landing made and recorded can be handed to a landing
plugin after the landing, by the person's press (`daoris-driver trees hand`, the review's *hand it to*, or
an Ask Daoris card), told this frame for the branch as it stands. A hand-off the plugin does not complete
changes nothing.*

*Amended by D121 (TOOLS5, 2026-10-01): a landing plugin runs with the tools' environment, so its own `git`, `gh` and
`az` are the ones the tools resolve.*

## D101 — A plugin is made with the driver's kit: a folder that tests itself, and a trial as the driver would run it (2026-09-30)

**Decision (PLUG8).** The owner wants Daoris able to make plugins, eventually by asking Ask Daoris. The
reading recorded with the ask: a plugin is code that runs on the machine as the person, so making one is
work, done by a session in a repository, tested, reviewed as a diff and installed only by a press. This
is the kit that session, or a person, makes one with. The plugin design's §9 is its contract.

- **What a session in another repository can run: `node`, and nothing of Daoris's.** Checked: the
  published install (`tools/desktop-publish.mjs`, its `INSTALLED.md`) carries the launcher, the
  application and the HTTP host, and neither the `daoris` CLI nor `daoris-driver`. The CLI is an npm
  package nothing publishes yet (DIST1), and `daoris-driver` is a workspace build. A driven session's
  environment is the machine's with Daoris's own variables added (`Spawning.InRoot`), and no PATH. So
  the kit's gate needs neither: `new` writes a **self-contained wire test** beside the plugin,
  `plugin.test.mjs`, which imports nothing but Node. It starts the plugin as the driver does (the
  manifest's command, `${plugin}` and `${data}` expanded, the four environment variables), speaks the
  handshake, one frame at every declared point and the shutdown, and checks every answer by the driver's
  rules and that stdout held only frames. A plugins repository's gate is `node --test`.
  `daoris-driver plugins try` is the second check, where Daoris is on the machine.
- **Where `try` lives: the driver library, behind `daoris-driver plugins new|try` and Settings →
  Plugins.** The CLI may spawn only in `toolchain.ts`, and `dogfood.test.ts` holds it. That rule's reason
  would not forbid a person's `try`, but the second reason decides. The driver already starts plugins
  (`HookProcess.StartInfo`, now public for this) and reads their answers (`HookPeer`), and a `try` built
  anywhere else would be a second reader of the wire, one that could accept what the driver refuses.
  `new` sits beside it, so there is **one scaffold behind both doors**, in C#, because the samples it
  writes are built by the driver's own frame builders. The CLI's `daoris plugin new|try` answer with
  where the kit is, and exit 2, in the shape of a moved verb.
- **The samples are the driver's frames.** `HookFrames.Consider`, `Ended` and `Land` now build what the
  loop and the landing send, and the kit builds each point's sample by calling them on a sample input.
  A test sends the samples through the real waterfall, observation and landing and compares; a point
  added to `HookPoints.All` without a kit entry fails a test.

  | Point | Kind | Frame | Answer | Waits |
  |---|---|---|---|---|
  | `quest/consider` | decision | `quest { id, title, from, to }`, `repository`, `workspace`, `root` | `{ kind: "allow" }`, or `{ kind: "hold", reason }` | 10 s |
  | `session/ended` | observation | `session`, `quest`, `repository`, `state`, `adapter`, `account`, `byPerson`, `note` | anything; `{}` | 10 s |
  | `work/land` | act | `repository`, `workspace`, `root`, `branch`, `base`, `title`, `quest { id, title }`, `session`, `commits [{ sha, subject }]` | `{ pushed, pullRequest?, message? }` | 2 min |
- **The two checkers are twins.** `try` reads answers with `HookPeer`, and the wire test with its own
  JavaScript, since a plugins repository has no Daoris. `PluginKitTests` holds them with one answer
  table: for each of its 22 rows, one plugin answers the row's answer, and both doors must give the
  row's verdict. Both are stricter than the driver in one place: a point the manifest declares and the
  process does not listen on fails, where the driver tolerates it.
- **A sample frame names no repository.** `root` is an empty scratch folder, and `GIT_DIR` points at a
  repository that is not there. git walks up, so an empty folder under a checkout answers for that
  checkout, and a landing plugin that pushes first would push it. A test proves it with the scratch
  inside a real repository, at both doors. With the person's own frame (`--frame`), the frame is theirs.
- **`try` says each check in its own sentence**: the handshake, each point, the shutdown, and stdout. A
  wrong answer is the driver's own sentence and what the driver would then do. A silent plugin "is
  running and did not answer `…` within Ns". A crash "exited (code N) before answering `…`", with its last
  line on stderr. A line on stdout that is not a frame fails `stdout`. A plugin still there two seconds
  after `shutdown` is ended, and fails it. It exits 0 when every answer is one the driver reads, 1 when
  the plugin failed a check, and 2 when it could not do what was asked.
- **`new` refuses rather than overwrites**: a name that is not an id, a point this build lacks, no
  point, a folder that does not exist, and a folder that holds anything. It installs nothing.
- **`--harness` is left out**, and so are servers. A harness is a declaration, not code: there is
  nothing to scaffold but the manifest row §3 shows, a scaffold would have to invent a command, and a
  posture is the adapter's own word (ACP3), never a guess. `try` cannot reach it either, since the ACP
  door is a session and not a frame. `plugins new --harness` says so.
- **The authoring knowledge lives with the kit**, not in the canon: the plugin design's §9, and a README
  the scaffold writes into every plugin from the same table. It covers the wire, the rules (stdout is
  the wire, data in `${data}`, paths told and never guessed, every request answered, a `.cmd` tool's
  quoting on Windows), each declared point's frame and answer, and how to test and install. A landing
  plugin's scaffold carries the WSR4 examples' `run()` helper with that quoting.

**Also.** A line on a plugin's stdout that parses as JSON but is not an object, or is an object that
is neither an answer nor a request, is now noise under the plugin's name. A bare `42` threw from the
pump's property read, past a catch that did not name it, and ended the wire.

**Why.** The kit is what makes the middle of making a plugin checkable by a gate (D37). A session can
run `node --test` in its plugins repository before a person reviews, with nothing of Daoris's on its
PATH, and a person can try the result as the driver would before adding it.

**Rejected.**
- **The scaffold in the CLI, and `try` in the driver.** The screen's New is C#, so that is two
  scaffolds, and a CLI scaffold's samples could not come from the driver's frame builders.
- **A spawn in the CLI for `try`.** It turns "only `toolchain.ts`" into a list, and its answer reader
  would be a TypeScript copy of `HookPeer`.
- **Templates as files both artefacts read.** Only the driver writes them, so there is no second reader
  to agree with. They are embedded in the driver, as the tree guard's script is.
- **A sample frame naming a path on a machine**, or a scratch root with no `GIT_DIR`.

**What the gates do not cover.** The wire test ran under Node 24 here; `node --test` with no arguments
needs Node 21 or later. No real plugins repository exists yet, and no session has made a plugin in one
(PLUG9 proposes that ask). The screen's New and Try were checked by the vitest loop over a mocked bridge
and by the module tests, not yet by looking at the window.

*Amended 2026-09-30 (the owner's call): a trial's scratch is under `<home>/plugins/.trials/`, never the
system's temporary folder, which is under the user profile (D63). Both doors take the home; the terminal's
folder trial refuses without one and points at `node --test`, which needs none.*

## D102 — A branch a landing made is recorded, goes once its work reads on the line, and can be handed to its plugin afterwards (2026-09-30)

**Decision (WSR5, amending D88 and D100).** The first real ticket's pull request was completed as a squash merge,
and it left the two branches its landings had made. Git calls both unmerged, since no commit of theirs is
on the line, and D88 never looked at them, since they are not `daoris/`. So:

- **A landing records the branch it makes**, the moment it exists: `<home>/landings.json` holds the
  repository, workspace, branch, the line it grew from, the commit it was made at, and the session, quest
  and title it was made for; and, once a plugin answers that it pushed the branch, the plugin, its pull
  request and the commit it pushed. Machine-local under the home (D63), never in the repository. An entry
  is forgotten once its branch is gone, or no longer holds the commit the landing made it at. A file that
  does not read is no record, and the landing still lands.
- **Only a recorded branch is judged.** Landings before the record are left out. Recognising them by the
  pattern would judge people's own branches, since `feature/{quest}-{slug}` is how people name them, and
  the one trace such a landing left is the note in the conversation, a sentence, which D48 §6 refuses to
  classify by. Those are the person's to delete once, by hand. A branch that took a recorded name since,
  whose history does not hold the recorded commit, is the person's and is never judged.
- **The proof, by content.** The line is the repository's line (D86) in both forms: its local branch and
  `origin/<line>`. A branch whose every commit is on the line is merged. Otherwise every file it changed
  since it left the line must read on the line as the branch left it, in one form of the line: the paths
  are `git diff --no-renames --name-only` from its merge-base with that form, so a deletion is a path the
  line must not hold and a rename is both its paths, and they are compared as blobs, by `git diff` between
  the branch and the line. A branch whose commits change no file is not proven. **Inside another:** a
  branch whose history is inside another recorded branch that passed is proven through it, judged over
  the whole set before anything goes.
- **What keeps one**, whatever its files say, each named: checked out in any working tree, the
  repository's own checkout included; commits its remote-tracking branch (its upstream, else
  `origin/<branch>`) does not have, pushed and then moved, unless every commit is on the line; a session
  branch that stays and shares commits with it that the line does not hold, since D88's proof for that
  session counts this branch as holding them; and anything git could not answer.
- **The same list and the same press as D88.** Settings → Workspace → Session branches lists them in a
  group of their own, *Branches landings made*, and `daoris-driver trees clean` after the session
  branches. One press removes both. The session branches go first, since their proof may count a landed
  branch as holding their commits; then the landed ones, those inside another before the one they are
  inside. Each is judged again over the whole set right before it goes, and removed only while its tip is
  still the commit it was judged at: `git branch -D` of that one branch in the repository's own checkout.
  No working tree, no other ref, and never a remote branch.

- **A recorded branch can be handed to a landing plugin afterwards (WSR5b).** Until now the plugin ran
  only inside the landing, and pressing Accept again was refused because the branch exists, so a ticket
  landed before its workspace named a plugin, or one whose plugin failed, had no door but a hand push.
  The doors: `daoris-driver trees hand <session|branch> [--repository <name>] [--plugin <id>] [--plan]`,
  *hand it to <plugin>* on the review of the session that landed it (shown whether or not its tree is
  still there, since a tidy removes it), and an Ask Daoris proposal of its own kind, `hand`
  (`hand_propose`), applied through the review's door. The plugin is the one named, else the
  repository's landing rule's. It is told exactly D100's frame for the branch as it stands: the line as
  `base`, the branch's commits the line lacks as `commits`, oldest first, and the quest, session and
  title the landing recorded. Its answer is said as a landing says it, and kept as a note in the
  conversation's record; a push is kept on the record, with its pull request and the commit it pushed.
- **What a hand-off refuses, each in its own sentence, speaking to no plugin:** a branch the record does
  not hold; one gone, or no longer holding the recorded commit; no plugin named; a plugin that cannot
  land work here (D100's four); nothing beyond the line; work that already reads on the line, which is
  the clean-up's; and a branch already on its remote at this very commit with a pull request answered
  for it. **A hand-off the plugin does not complete changes nothing**: the branch, the record and the
  remote stay as they were, and the sentence says how to push by hand.

This amends **D88**: its proof is unchanged for session branches, and a second one, by content, serves the
branches landings made. It amends **D100**: the plugin a branch rule names may also be spoken to after the
landing, by the person's press, for a branch the landing made.

**Why.** D88's proof is ancestry: every commit on a branch of the person's. A squash merge makes one new
commit on the line from the branch's content, so ancestry calls the branch unmerged forever and `git
branch -d` refuses it. What reached the line is the content, so the content is what is compared. The
first real workspace's owner, after the ticket: *"we still have so many branch need to clean up"*. And
of the push: *"if you want to open pr is the azure plugin ready"* — and a ticket landed before a plugin
was named has no door to one but the person's own terminal.

**Rejected.**
- **A branch whose remote branch is gone** (`git fetch --prune`, then `[gone]`). A platform deletes a
  branch when a pull request completes, when it is abandoned, and when a person deletes it by hand: gone
  says nothing about where the work went.
- **Asking the platform whether the pull request completed.** A network call and a platform's API in
  core, which D87 left to plugins.
- **Patch equivalence** (`git cherry`). A squash of several commits matches none of them.
- **Judging by the pattern.** As above: it judges the person's own branches.
- **Pressing Accept again for a hand-off.** Accept makes a branch, and one standing is refused so a branch
  Daoris did not make is never moved (D87). The hand-off is its own act on the branch the landing made.
- **Asking the platform whether a pull request is open before handing on.** A network call in core again;
  the record's answer and the remote-tracking branch are what this machine knows, and the plugin, which
  speaks for its platform, answers for the rest.

**What the gates do not cover.** The real case's two branches predate the record, so this build never
judges them, nor hands them on. Every squash in the tests is `git merge --squash` in a scratch
repository, on the local line or on a clone of a local bare `origin`; no platform's squash was run. A
branch rebased before its pull request no longer holds the recorded commit, so it is never judged: the
safe side. The hand-off's plugins in the tests are fakes on the wire's channel; its frame is D100's, which
the examples' own tests drive against a bare repository, but no hand-off has run against a real platform.
The review's button and Ask Daoris's card are held by the page's tests over a mocked bridge, not yet looked
at on the window.

*Amended by D109 (WSR6, 2026-09-30): a landing now also records its `from`, where the session branch it was
made from started, and a replay Daoris makes moves the recorded tip with the branch, so it stays judged. A
branch rebased by anyone else is still never judged.*

*Amended by D113 (REVIEW2, 2026-10-01): an entry whose branch is gone, or no longer the landing's, is marked
(`goneAt`, and what the clean-up proved where it removed it) rather than forgotten, so the review of the session that
landed it still says where its work went. A trace is never judged, handed on or replayed: the clean-up, the hand-off
and bringing up to date read standing entries only.*

## D103 — An installed plugin remembers where it came from, and the install offers Daoris's own (2026-09-30)

**Decision (PLUG9 c and d).** The owner asked that Daoris make and install plugins easily, "easy access
for everything". PLUG9 (a, b) made a plugin an ask and its install a card. The two parts left: a plugin
added from a repository's folder can be updated when that repository lands a change, and the install
carries Daoris's own example plugins as offers, installed only by a press (D87: off by default).

- **Where a plugin came from is a file in its install folder**, `.daoris-source.json`: `{ "folder":
  <whole path> }` for one added from a folder, `{ "offer": <id> }` for one installed from the install's
  offers. Every add door writes it into the staged copy before the swap: `daoris plugin add <folder>`,
  `daoris plugin add --offer <id>`, the driver's `PluginInstall.Add` and `AddOffer` behind Ask Daoris's
  card and Settings → Plugins' Install. So the record is replaced with the install, goes with it on a
  remove, and is absent from a folder copied in by hand. A plugin with no record is said to have none
  ("added before Daoris kept one, or copied in by hand"), on its row, in `daoris plugin list` and in Ask
  Daoris's room, and is never given a guessed source. A record that does not read is named, not read
  as none. The catalogue reads only the manifest, so it reads past the file.
- **Update, both doors.** `daoris plugin update <id>` prints what would change and replaces nothing;
  `--yes` updates, the question being the command without it, as `agent trust` asks one. Settings →
  Plugins' **Update…** on a row with a record asks the same (`PLUGIN_UPDATE` {id}) and shows it under
  the row; **Update now** makes it (`{id, apply: true}`). Ask Daoris's `plugin_propose` takes action
  `update` with the plugin's `id`, its card showing the changes. Each re-reads the source with the
  catalogue's own reader (`readManifest`/`ReadAsWritten`, placeholders as written) and refuses, in one
  order and one wording on both twins: an id that is not one, a plugin not installed, no record, a record
  that does not read, an offer the install no longer carries, a source inside or holding the home, a
  folder that is gone, one with no manifest, an unsound manifest (the catalogue's words), a manifest of
  another id, and a declaration this build refuses. What changes is five rows, each side as its manifest
  writes it: version, command, points, harnesses, servers. The swap is `add`'s: copied beside as a
  dot-folder, the record written into it, the installed folder moved aside whole (a held folder is
  refused whole, the installed version untouched), the copy renamed in. `.data/<id>` is its sibling and
  is never touched. The driver stops the plugin's hook first, and the loop starts the new one at its
  next look. Nothing it runs starts at the press.
- **The offers are `app/plugin-offers/` in the install**, beside the application and never under
  `data/plugins/`, laid out by `tools/desktop-publish.mjs` (`layOffers`, replaced whole each publish).
  They are the examples meant for people: `github-pull-request` and `azure-devops-pull-request`, which
  land work (D100), and `in-app-browser`, which hands a session the install's own browser (D78). Not
  `hold-by-title`, the rehearsals' fixture, and not `browser`, which launches a browser of its own for a
  machine with no shell and claims `in-app-browser`'s server name, so the second installed would
  contribute nothing.
- **How each door finds them.** The CLI on a terminal has only `DAORIS_HOME`, which the install sets
  to its `data/` (D63), so it looks beside the home: `<home>/../app/plugin-offers`. The driver looks
  beside the running application first (`AppContext.BaseDirectory/plugin-offers`, the way the HTTP host
  is found beside the shell), then beside the home. In an install the two are one folder; where a home
  is redirected (a rehearsal, a dev loop), the application still finds its own. The dev loop lays the
  offers out beside its scratch home.
- **What an offer needs is its README's `## What it needs`**, its bullets read by both twins (a wrapped
  bullet joined, emphasis dropped, code kept) and shown verbatim, never translated: content, not chrome.
  `in-app-browser`'s README gained that section.
- **Offers on every door, installed by a press.** Settings → Plugins lists the offers not installed in
  their own card, *Daoris's own plugins*, each with what it declares and needs and an **Install** that is
  `PLUGIN_INSTALL` {offer}, `add --offer`'s copy. `daoris plugin list` shows them with the command.
  Ask Daoris's room names the sound ones not installed by id, with their points and needs; its
  `plugin_propose` add names one in `offer`, by id and never a path (the service refuses an offer and a
  folder together). Installing one records `{ "offer": id }`, so a republish that brings a newer copy is
  taken by an update.

**Why a file in the install folder, not a row in `plugins.json`.** `plugins.json` is rewritten whole by
two writers, the CLI's and the driver's, and by every older build still on a machine (the owner's install
runs a build before this), none of which has a field for a source: one switch from an older build would
drop every record. A row also outlives a folder deleted by hand, and a later hand-copied folder of the
same id would then inherit a source nobody gave it. The file is one move with the install and nothing
else writes it.

**Why the offers are not installed.** D87 and D100: nothing pushes by default, and a landing plugin runs
as the person with their platform's sign-in. An offer copied into `data/plugins/` would be installed by a
publish nobody pressed for.

**Rejected.**
- **The source in the plugin's data folder.** `.data/<id>` is what the plugin keeps, which an update must
  never touch and a remove leaves; a record there would outlive the install it describes.
- **An absolute path for an offer's source.** An install moved to another folder would leave every
  offer-installed plugin pointing at nowhere. The offer's id is resolved at update time.
- **Offers found by a path written into `INSTALLED.md`.** A second reader of a prose file, where the
  layout is already D93's and the home already names it.
- **A `needs` field in the manifest.** It would change the manifest for a line the README already
  carries for a person; the README is the author's words for exactly this reader.
- **Replacing an installed plugin from Ask Daoris's add, or from Install.** PLUG9's rule stands: the
  driver adds and never replaces. Taking a newer copy is an update, which shows what changes first.

**What the gates do not cover.** The deployment rehearsal now checks the published install carries the
three offers and installs none, and that the deployed driver finds them beside the application with a
redirected home; it was written here and not run (the parent runs it). The screen's Update and Install,
and the proposal cards, were checked by the vitest loop over a mocked bridge in both catalogues, not yet
on the window. No offer has been installed and run against a real platform.

## D104 — A driver loop is asked for by name and holds its home; a take a shutdown or the sweep cut off is carried on (2026-09-30)

**Decision (DRV8, found running the owner's ticket).**

- **A bare `daoris-driver` prints its usage and exits 2.** It never starts a loop. The loop is the verb
  `drive [--once | --until-idle] [--share]`. The spelling before the verb, a bare `--once` or
  `--until-idle`, still runs its tick, since a flag that names a mode is an explicit ask. Any other word
  is the usage and exit 2, saying what was not understood, and `help` is the usage and exit 0. The
  usage names every verb the host answers, and a test holds that.
- **One live driver per home.** A loop takes `<home>/driver.lock` before it reaches the service. The
  file names the driver's kind (`desktop` or `headless`), its process id and that process's start
  time, and when it took the home. It is written beside and moved into place without replacing, so of
  two loops starting at once one takes it. It is released on exit, and only while it still names this
  driver. Every mode takes it, `--once` and `--until-idle` too, since each starts sessions.
- **A second loop is refused, naming the first**: which door, its process id, and since when. At a
  terminal that is exit 1, a refusal and not a tool error. `--share` runs beside a live driver on
  purpose: it takes the lock where it is free, and otherwise runs without it and says whose home it
  shares. **A stale lock never blocks.** One whose process is gone, whose process id now belongs to a
  process started at another time, or that does not read is replaced. That is the session markers'
  test (`SessionProcesses`), and like theirs, a start time that cannot be read counts as alive.
- **The desktop's loop takes the same lock.** Where a headless loop holds its home, the desktop says so
  once on the channel a person acts on (`DRIVER_ERROR`). Its host, its page and its conversations carry
  on, and its loop starts the moment the lock frees.
- **A record the orphan sweep or a shutdown ended says so: `stopped`, with `interrupted`.** This is a
  new field on the session record. It is allowed only on a move to `stopped`, a move to anything else
  asking for it is refused, and the store adds its column by `SchemaColumns`, so a record from before
  it reads false, the old reading. The sweep sets it on every record it ends. A driven session's
  shutdown sets it: the driver closing under it, from the desktop's close or Ctrl+C in a terminal. A
  person's stop never sets it, their stop on an orphan included, since they asked about that one. A
  conversation or an intake the application closes is not marked, since nothing carries either on.
- **An interrupted take is carried on like a cut-off (D80).** The ledger opens a session on a taken
  quest whose last session here ended `stopped` and interrupted, as it does after `failed`. The driver
  counts it as a strike, and the planner carries it on in the tree it worked in, telling it what the
  record said ended it. The third parks it behind `daoris driver retry`. A person's stop stays as it
  was: never carried on, never a strike.

**Why.** Run with no verb to read its usage, `daoris-driver` started a headless loop on the install's
home, and that loop took a fresh quest two seconds before the desktop's own. The quest lock held (D46)
and the desktop stood its session down, but a second loop on one home is almost never meant, and
reading the usage must not start one. The same run showed the other half: the orphan sweep and a
shutdown both ended a record `stopped`, and the planner carries on only `failed` and answered takes, so
the quest stayed taken with nothing to move it, and it was declined with the reason and re-issued by
hand. A person's stop is their decision. A swept or shut-down session's is not.

**Why a field, and not the note or a new state.** The note is a sentence written for a person, and
classifying by it would turn a rewording into a change of behaviour (D48 §6). A new state would reach
every surface that renders one and every catalogue that words one, and a build that reads the store
parses the state strictly, so an older one would fail on a newer record. `stopped` is still true:
something ended the process. What the field adds is whose decision that was, which is the one thing the
planner and the ledger need.

**Rejected.**
- **Recording a shutdown or a swept session `failed`.** The strikes and the carry-on would follow for
  free, but the record would claim a failure that did not happen, and an application closed on purpose
  would read as one on the page.
- **A door that releases the take to *Open*.** D79's and D80's rejected draft: anyone could take it
  fresh, without its tree.
- **Locking only the watch mode.** A one-tick run starts sessions too, and races the desktop the same way.
- **An operating system's file lock held open for the process's life.** It is advisory on the other
  systems the headless driver runs on, and where it is exclusive it keeps the holder's name from the
  loop that has to be refused with it.
- **The desktop refusing to come up beside a headless loop.** Its host, page and conversations race
  nobody for a quest; only its loop waits.

**Amended 2026-10-01 (DEV3, D115): the shutdown reaches every running session, whichever look started it.** A
session now outlives the look that started it, so the watch keeps the running sessions, and a close ends each
on the loop's own token. The loop lets go only once every record says `stopped` and interrupted. A failure that
ends a headless loop (exit 2) ends its running sessions the same way first, so none is left working with
nothing watching it, and each is carried on at the next start.

**What the gates do not cover.** The window between reading a stale lock and removing it is not closed:
two loops starting inside it could both run, and the quest lock still decides the race. A `--share` loop
that outlives the driver it shared with leaves the home unlocked until the next loop starts. The remote
feed does not carry `interrupted`: the strikes and the carry-on read only this machine's own records. The
page shows the record's note and nothing new. The desktop's wait is held by a modules test of its hold
step, not looked at on the window, and no rehearsal was run by this change.

## D105 — An install runs on its own home, the CLI installs from npm, and a session's background work ends with it (2026-09-30)

**Decision.** Three questions REV3 and CONSOLE3 left for the owner, decided the same day: HOME1 (which
home a second install uses), DIST1 (how a consumer installs the CLI) and CONSOLE3's last one (whether a
driven session waits for its own background work).

### 1. HOME1 — a moved or second install runs on its own `data/`

The owner's call: **the install's own `data/` wins**, and the window says when it overrode an
inherited home. REV3 found the defect (modules F4): `InstallHome.Establish` deferred to any
`DAORIS_HOME` already in its environment, and the first install had set one for the account, which
every process the person starts inherits. A second install, or the first one moved, ran on the old
`data/` and said nothing.

- **Which value is inherited.** A process value that equals the account's variable (as a path: full,
  no trailing separator, case ignored on Windows) was inherited, and the install's own `data/` replaces
  it for this process and everything it spawns. A value that differs from the account's, or any value
  when the account has none, was named for this start alone (a gate's scratch home, a terminal's
  one-off), and is still respected whole. Without that exception the deployment rehearsal would run
  its install on the install's `data/`, and on a machine whose account has no variable, set one for
  the account from a gate. A value that already names this install's `data/` establishes nothing, as
  before.
- **The account's variable is left as it is**, whatever folder it names, and the notice says so. D63
  sets it once, when the account has none, and that stands. Rewriting one that names another install
  would move every terminal to whichever install was opened last, and it is the person's setting.
  Removing a stale one is theirs too.
- **Where the window says it: Settings → Driver's *Daoris home* row**, as `homeNotice`, the standing
  line under the home's path, which already carried what establishing the home did. That row is where
  the fact belongs: it corrects the path printed beside it. It also rides the one-time event the home's
  other news takes. It is a sentence the person can act on: *Daoris home: `<install>\data` — this
  install's own data folder, not `<other>`, which DAORIS_HOME names for your account; that variable is
  left as it is, so a terminal's daoris still reads `<other>` until you change it.* It is said on
  every start that overrides, since the fact lasts as long as the variable names the other folder.
  **Not the status bar**, which carries what changes while a person works. **Not the machine log's
  `app.started`**: that log is read in a development session to improve Daoris (D94), and no person
  meets a sentence there.

Off Windows the account's variable is not read (the user-level store is a Windows notion, as D63's code
says), so no process value counts as inherited there; the shell is Windows-only, so no install meets it.

**Rejected.** **The inherited home wins, and the shell says so**, the other way the row offered: the
window would name a home that is not the install's, and D63 made the install's folder the home. **Setting
the account's variable to the install on every start**: the last install opened would own every terminal.
**Treating every value as inherited**: no gate could keep an install off the real machine.

**What the gates do not cover.** `InstallHomeTests` hold the rule over temporary folders and a fake
environment: the override, the moved install, the respected one-off, the account left alone, the
notice, and that it is worth saying. The rest is held by existing tests, joined by that flag: the
state carries the notice of a record worth saying (`DriverModuleTests`), and the row renders whatever
`homeNotice` holds (`shell.test.tsx`). No second install has been started on a real account, and the
deployment rehearsal starts its install with a scratch home, the respected case, so it never shows
the override. The row's hint (`settings.home.hint`) still says a terminal reads the same folder; the
notice under it says when one does not, and qualifying the hint in both catalogues is left open.

*Amended by LEFT1 (2026-09-30) and LEFT2 (2026-10-01): the hint is qualified. LEFT1 read the override from the
notice's English; LEFT2 makes it a field of the driver's state, `homeAccount`, which
`InstallHome.AccountOf` answers from the home, what establishing it did, and the account's variable:
`same` (the account names this home: the hint says a terminal reads it), `overridden` (the hint points
at the notice, which names the folder a terminal reads), or `this-start`, a home named for this start
alone, which D105 respects without a notice and which the notice could therefore never tell; the hint
then says a terminal does not read this folder. A shell older than the field keeps the old hint.
`InstallHomeTests` hold the three, the vitest loop the hint for each over a mocked bridge, and a
`DriverModuleDriverTests` case, in the `Process` half and not run in the branch, the state carrying
it. Nothing has looked at the row on the window.*

### 2. DIST1 — a consumer installs the CLI from npm, `daoris@<version>`

The owner's call: **npm's `daoris@X`**. REV3 found the defect (docs F1): the README's `npx
github:JiarongGu/Daoris#v0.0.1 …` could not run, because the repository's root package is a private
workspace with no `bin`, and no tag exists. The release workflow already packs `src/Daoris.Cli` and
publishes it to npm as `daoris`, so that package is what a consumer runs.

- **The install lines** are `npx daoris@0.0.1 init`, `sync` and `check`, at the version the release
  tooling already holds. Nothing was bumped.
- **`init` writes `"source": "daoris@<canon version>"`**: D11's provenance and command to re-run, now
  literally what follows `npx`. The canon's version is the package's, which a test already holds.
- **Daoris's own manifest and the examples' pin the same**, and their locks were re-synced, since the
  lock records the manifest's `source` and the family rehearsal requires a sync to change nothing.
- **`release-prep` rewrites and checks the new spelling**: every `daoris@X.Y.Z` in the README, and a
  manifest's `source` field alone. `--check` also reports a README with no `npx daoris@` install line,
  which would otherwise pass with nothing to disagree with, and any `github:…#v` ref left in the README
  or a manifest, since nothing rewrites that spelling now. `version.test.ts` holds the rewrite and the
  check together: every file rewritten at a new version must then agree at it.

**What does not move.** D11: the canon ships inside the package, and `source` is never fetched. A
manifest that still names the git ref reads as before, since the tool requires only that `source` is
there; changing it is the repository's own edit, and the live consumer count is zero.

**Rejected.** **A git ref with a root `bin`**, the other way the row offered: the root would have to
become a publishable package beside the one the release publishes. `npx` of a git ref also clones and
builds on every new machine, and needs a tag for every release, which the workflow creates only on
request. The npm package is already built, published with provenance, and gated by the release rehearsal.

**What the gates do not cover.** No release has run, so nothing is on npm. On 2026-09-30 the registry
answered 404 for `daoris`: the name was unclaimed, and nothing yet proves the first publish will get
it. `npm run rehearse` installs the packed tarball through its `bin` locally, which is the nearest
proof of the install lines; it did not run in this change.

### 3. CONSOLE3 — a driven session's background work ends with the session

The owner's call: **today's behaviour is the rule**. A driven session does not wait for background
work it started (a backgrounded command, an async subagent, a dev server) before it closes. That work
ends with the session, on both doors, and no code changed.

- **The protocol door.** The session closes after its prompt's response. The adapter's process tree,
  the background command among it, ends with the session's job object (ORPHAN1), and a stream still
  open is closed and says so (`session-ended`, CONSOLE2). What the agent says after its turn, woken by
  that work finishing, is cut off with it (`docs/2026-09-28-console2-streams-evidence.md`).
- **The native door.** The binary ends its own background work when its turn ends. In the CONSOLE3c
  probe the command was `killed` five seconds after the `result`, and nothing of it ran after the
  binary exited (`docs/2026-09-30-console3-native-streams-evidence.md`). The job object holds the
  same line behind it.
- **A conversation** keeps its process between turns, so what it starts may outlive a turn. It ends
  with the conversation at the latest, under the same job object.

**Why.** A driven session holds its tree and its quest while it runs. Waiting on its background work
would hold both for as long as that work chose to run, with nobody watching: ORPHAN1 was written
after a session's dev servers held two ports and a finished tree for an hour. Work whose result the
session needs belongs in its turn, in the foreground, which both harnesses offer.

**Rejected.** **A bounded wait after the turn**: a dev server never finishes, so the wait is always
its timeout, and the timeout is the time the session holds its tree. The words the agent says after
its turn would also belong to no turn in the record.

**What the gates do not cover.** The job object is Windows-only, a no-op elsewhere, and best effort: a
process that exits before it can join is not held. `ProcessJobTests` holds a child that outlives its
parent ending when the session is untracked. The native door's own kill is the harness's behaviour,
seen once at one binary version, and could change with a later one. Neither door's stop has been seen
against a real session with background work still running.

## D106 — The records stop colliding: union merge, reserved numbers, no moving counters (2026-09-30)

**Decision (MOD1, from the owner's *"we should modulize this project properly so that paralle
development with subagent can run smoothly"*; `docs/2026-09-30-parallel-development-design.md` §3).**
- **The append-only records merge by union.** `.gitattributes` marks `CHANGELOG.md`,
  `docs/DECISIONS.md`, `docs/task-archive.md`, `docs/FIX-LOG.md`, `docs/README.md` and
  `.claude/knowledge/twins.md` `merge=union`. Two branches that add lines at one place keep both.
- **What union can leave behind is refused.** Union also keeps both versions of a line two branches
  changed. `tools/doc-duplicates.mjs`, run by `verify`, refuses what that looks like in each record: a
  decision number, a heading, an index row (by its first cell) or a changelog line that appears twice.
  It fails rather than reports (D54): a duplicated number is a fact about the file. A test holds that the
  records marked union are exactly the records checked.
- **Decision numbers are reserved at dispatch.** The parent names the number in a subagent's brief, so
  two branches never take one. The day before, three branches took D102 between them.
- **No moving counters in always-read files.** `CLAUDE.md` said *D1–D103*, changed with every decision
  and conflicted for nothing. It says *the numbered decision log*.

**Why.** The eighteen merges measured collided most on these records (§1 of the design): nothing about
the work collided, only the insertion point. Serialising the work would remove the conflict and the
parallelism with it; changing the insertion point removes only the conflict.

**Rejected.**
- **One file per decision.** It removes the collision too, but moving a hundred decisions would break
  every anchor that cites one, and union plus reserved numbers already remove the collision.
- **Union for the backlog.** Rows move out of `TASKS.md` when they close, and union would bring a
  removed row back from the other side.
- **Union for the module READMEs.** One long row there lists a module's whole surface and every feature
  edits it. Union would keep every branch's version of that row, so those files stay as they are until
  the splits (MOD3–MOD5) give each feature its own row.

*Amended by DEV2 (D115, 2026-10-01): the parent's records, the backlog and the archive, are the steward's lane
`records` in `daoris.lanes.json`, with the lane map itself. The parent keeps them as the steward until a steward
session does (DEV9). Union still serves the archive and never the backlog.*

## D107 — Agents read the workspace's checkouts by default, and write into another only where the person declared it (2026-09-30)

**Decision (READ1, the owner's call closing READACROSS1 and HELPREAD1: *"should be configuable and
default to read yes write no (because some repo have master/child relationship like plugin repos)"*).**
A driven session and Ask Daoris may read registered checkouts (a file, `git status`, the branch list)
and write none, by default. It is set per workspace and per repository on both doors, and a declared
relationship lets one repository's sessions write into another.

### 1. The setting: `driver.json`, the repository over its workspace

- **Three keys, each written only when set.** `readAcross` {repository: bool}, `workspaceReadAcross`
  {workspace: bool}, `writeAcross` {repository: [repository, …]}. A value of another type is not read,
  names match without case, and a repository naming itself is refused on both doors.
- **Reading belongs to the checkout that is read.** `readAcross.engine: false` means no agent outside
  `engine` reads its checkout: no session in another repository and not Ask Daoris. The repository's own
  value wins, then its workspace's, then **on**. It is the checkout's and not the reader's for two
  reasons. The reason to switch reading off is a repository whose code should stay its own, so the
  question is about what is read. And Ask Daoris belongs to no repository, so a switch on the reader
  could not reach it.
- **Within one workspace**, because D48 scopes everything that crosses repositories to one. A driven
  session reads the readable checkouts of its own workspace. Ask Daoris belongs to none and its room
  already describes every workspace, so it reads every readable checkout.
- **Writing is a declared relationship.** `writeAcross.plugins: ["engine"]` means sessions in `plugins`
  may also write into `engine`'s checkout. A declaration has one direction, so writing both ways takes
  two. It applies within one workspace and where both have a checkout here. It includes reading its
  target, whatever the target's reading says. It is the person's durable say-so that the canon's
  `repository-owns-its-work` asks for, so the canon does not change.
- **Why `driver.json` and not `permissions.json`.** The rules file unions its scopes and leaves the
  precedence to the harness, and D72 rejected ranking Daoris's scopes there. This setting is ranked,
  like a line and a landing rule, which live in `driver.json`. It also reaches past Claude Code's rules,
  into every harness's instruction and Ask Daoris's room, and the rules file is Claude Code's alone.
- **Two doors (D50).** At a terminal: `daoris driver across <repository> read on|off|--clear`,
  `--workspace <name>` in place of the repository for a whole workspace, and
  `daoris driver across <repository> write-to <other> [--clear]`. `daoris driver list` shows all three.
  On the screen: Settings → Permissions → *Reading and writing across* (`ACROSS`, `SET_READ_ACROSS`,
  `SET_WRITE_ACROSS`). It is on Permissions and not Workspace because the person's question is what an
  agent may do, and the rules it becomes are listed on the same page. The twins are `driverconfig.ts`
  and `DriverConfig.cs` for the file. Only the driver resolves it (`Across.cs`), as only the driver
  chooses a landing.

### 2. What a driven session is handed

At spawn (PERM1), for every other registered checkout on this machine, in Claude Code's own forms:

- **Readable to it**: `Read(//<checkout>/**)`, which the harness applies to its Grep and Glob too, and
  `Bash(git -C <checkout> status:*)` and `Bash(git -C <checkout> branch --list:*)`.
- **A declared write target**: also `Edit(//<checkout>/**)`, `Bash(git -C <checkout> add:*)` and
  `Bash(git -C <checkout> commit:*)`. The tree guard takes each target as one more argument and lets a
  write there through.
- **Not a write target**: `deny` `Edit(//<checkout>/**)`. This refusal holds with the tree guard
  switched off and over a person's own allow.
- **Not readable to it** (reading off, or another workspace): `deny` `Read(//<checkout>/**)`, so *off*
  holds in `auto` (D81), which would otherwise judge a read by itself.
- **No deny on a checkout that holds the session's own tree, its kept files, or a checkout it may use.**
  Deny beats allow, so such a deny would refuse the session its own work.

A conversation in a repository gets the same rules, as it already gets the same union. An intake gets
none: it serves no repository, and its room names its own tools.

**The git form** is the path with forward slashes, which Git Bash, PowerShell and git all read,
double-quoted when it holds anything but letters, digits and `_./:@+,=-~%`. The instruction writes the
path the same way. `log`, `diff` and `show` are not handed, because each takes `--output=<file>`, which
writes wherever it names, and a prefix rule cannot refuse a flag appended to it.

### 3. The instruction

The claiming, resuming and carrying-on instructions each gain what applies, and read as before when
nothing does:

- **A reading paragraph** lists each readable checkout by name and path: *You may read these other
  repositories' checkouts on this machine, and change nothing in them*, with its files read where they
  lie and `git -C <path> status` and `git -C <path> branch --list`.
- **The asking paragraph** (D79) keeps its point that a change or the why behind a codebase is asked,
  not guessed. With reading on, it no longer says *do not read into it*.
- **The boundary** names the declared write targets as the one exception to *never write outside this
  repository*. There the session keeps to what its quest needs, follows that repository's own doctrine,
  and commits with `git -C <path> add` and `git -C <path> commit`.

### 4. Ask Daoris

- **Its handed rules** (`HelpRoom.Rules`) add, for each checkout it may read, the read and the two git
  rules, and nothing that writes. The room's own `.claude/settings.json` stays D89's static allow-list.
  That file is the project tier, which the harness ignores until the folder is trusted. The spawn tier
  is the one measured to reach a session untrusted.
- **The room** (`HelpRoomMayDo`) says which checkouts it may read, by name, workspace and path, and how.
  It still says it has no shell and no web. Where checkouts exist and none may be read, the room says
  reading is switched off and names both doors.
- **HELP4's refusal of a shell stays.** Ask Daoris still runs in the agent's asking mode (D89), so
  anything off its rules is asked and refused. Two git commands by exact prefix are not a shell.
- **The room now carries paths**, the checkouts it may read and no others. HELP1a kept every root out,
  and a read needs one. The room is a file on this machine for the helper alone, and it never crosses
  the bridge. It still carries no profile home and no key.

### What the gates do not cover

The composition is proven keylessly: the setting on both twins, precedence, the rules for each
combination, the hook's extra trees as a real process, the instruction, the room, and the handed file
from a real tick. Nothing here has run against a real session. Five things are unproven until one does:
that the harness applies a `Read` rule to Grep and Glob; that an `Edit` deny refuses a Write; that a
`Bash(git -C … status:*)` prefix matches the command as the agent writes it, above all a quoted path;
that `auto` takes an explicit allow before its own judgement; and that the command-line tier's
`deny` beats a person's allow for these paths, as D72 says it does in general.

### Amends

- **D79's rejected "reading a sibling allowed by default"**: reversed by the owner's call. What stays of
  D79 is that a change, or what only the other repository knows, is asked of it and waited on.
- **D89 and HELP1a**: the helper reads checkouts, and its room names their paths.

### Rejected

- **Reading as a property of the reader.** It could not reach Ask Daoris, and it answers the wrong
  question for a repository that should stay private.
- **`permissions.json`**: it has no ranking, and it is Claude Code's alone (§1).
- **A write allowed by a rule the person writes** (`Edit(//<path>/**)` in a repository's scope). The
  tree guard would still refuse it, since a hook is not a rule, and the rule has no pair or direction
  for the instruction to name.
- **`additionalDirectories`**: it makes a checkout a working directory, which under `acceptEdits` lets
  an edit there through unasked. That is D72's reason for the kept files (INT4j).
- **Reading across workspaces**: D48 draws the boundary there.
- **`git log`, `diff`, `show`**: `--output` (§2).

## D108 — An install's windows name Daoris's taskbar id, and a pin made from one starts the launcher at the root (2026-09-30)

**Decision (TASKBAR1, on D93's layout).** The window belongs to `app/Daoris.Desktop.exe`, and the one
thing a person runs is `Daoris.exe` at the install's root, which starts it and exits (D93). Windows
groups a taskbar button by its process's executable unless the window names an application id. So the
running window was expected to be a button of its own beside a pinned launcher, and pinning the running
window pinned `app/Daoris.Desktop.exe`, which a republish replaces. An install's windows now name one
id, `Daoris.Desktop`, and carry the relaunch properties that a pin made from them is built from.

### 1. What a window carries

- **In its own property store** (`TaskbarWindow`, the application):
  `System.AppUserModel.RelaunchCommand` is the root launcher, quoted; `RelaunchDisplayNameResource` is
  `Daoris`; `RelaunchIconResource` is the launcher's own icon (`Daoris.exe,0`); then
  `System.AppUserModel.ID` is `Daoris.Desktop`, set last. They are written as each handle is created,
  because a recreated handle carries nothing of the old one. They are set to empty as it is destroyed,
  which Windows requires of a window's properties. The main window wears them, and so does every
  secondary window, which would otherwise be a second button grouped by the executable.
- **Only an install.** `TaskbarIdentity.For` (the modules) answers from the root `InstallHome.RootOf`
  finds: the marker there and the launcher there, or nothing. A workspace build wears nothing, so the
  dev loop's window never joins the person's pinned Daoris. An install with no launcher wears nothing,
  because nothing a pin could start would outlive the next publish.
- **One id for every install, with no version**, so a pin made before an upgrade is still the button
  after it. Windows' own guidance keeps a version in the id only to let two versions stand apart.
- **The launcher carries the same id for its process** (`SetCurrentProcessExplicitAppUserModelID`,
  first in `Main`), because Windows counts a launcher and the process it starts as one application. The
  only visible effect is that its refusal sits with the pin.
- **A twin.** The launcher's file name (its `AssemblyName`) and its `Launcher.AppId` against the
  modules' `TaskbarIdentity.Launcher` and `Id`, which `TaskbarIdentityTests` reads from the launcher's
  project and source.

### 2. How the person gets one button

Pin Daoris from its running button (right-click it, then *Pin to taskbar*). That pin is the root
launcher with the id, and every later window joins it. 🔴 **A pin made on `Daoris.exe` itself, from
Explorer, carries no id, and Windows cannot relate the window to it.** Windows' documentation says as
much: a launcher that hands off to another process leaves the system unable to relate the running
process to a shortcut that points at the launcher, unless the shortcut names the id. So after the
republish, an existing pin of either kind is unpinned, and the running window is pinned once.

### What the gates do not cover

`TaskbarIdentityTests` holds, with no desktop session: the id, the command built from an install root,
when nothing is worn, that the launcher is named and identified as the window says, and that both forms
wear the identity from their handles. **Nothing here has looked at a taskbar.** Four things are proven
only by looking on the owner's machine after a republish: that the window groups under a pin made from
it; that the pin starts the root launcher and survives a republish; that the order the properties are
set in does not matter or is the right one; and what a pin made from Explorer shows.

### Left open

- **Daoris's browser** (`--daoris-browser`, D99) is not labelled. Its windows are the engine's own, made
  in the kit's browser process, and are not Daoris's window. With it open, it is expected to keep a
  button of its own, grouped by the executable, and a pin made from that button would start
  `app/Daoris.Desktop.exe`. Whether it should join Daoris's button, or refuse a pin, is for the look.
- **The install's `INSTALLED.md`** could say how to pin. It is the publish script's to write.

### Rejected

- **One id for the whole process** (`SetCurrentProcessExplicitAppUserModelID` in the application). The
  relaunch properties are read only beside a window-level id, so it adds nothing for the windows. It
  would also relabel everything else the process shows, the tray icon's hidden window and its
  notifications among them, which nothing here could look at.
- **An id per install**, a hash of its root, as the kit scopes its single-instance guard. Two installs
  would be two buttons, and the deployment gate's scratch install would not join the person's button
  while it runs. Joining it is this decision's one cost: a pin made from that button while the gate
  runs could name the scratch install. But the launcher would have to derive the same hash, a twin of
  an algorithm where a constant is a string, and a person has one Daoris.
- **Writing the id into the person's pin**: the launcher finding the shortcut it was started from and
  labelling it. The pin is the person's file under their profile (D63), and it would be changed unasked.
- **A shortcut of Daoris's own carrying the id.** In the Start menu it is under the profile (D63). At
  the root it is a second thing to double-click (D60, D93).
- **The launcher handing its shortcut on to the application** (`STARTF_TITLEISLINKNAME`), so the window
  would take the pin's identity. That is undocumented as a way to group, and it would conflict with the
  pin made from the window.

## D110 — Every door has an Ask Daoris answer: a proposal, a door owed, or a reason, held by a test (2026-09-30)

**Decision (HELP9, the owner's standing direction: *"a good ui/ux or easy access for everything use ask
daoris"*).** Every `daoris driver` verb and every control a Settings domain holds is one of three: a door a
kind of Ask Daoris's proposal takes, a door owed with what it waits on, or exempt with its reason.
`HelpCoverageTests` derives both lists from the sources the doors live in and fails on anything answered for
nowhere, and on a row whose control is gone. A new door is built with its answer, or it does not pass.

### 1. What is held, and how

- **The verbs** are the CLI's command table (`src/Daoris.Cli/src/cli/driver.ts`): each verb its usage spells,
  and `across` by its two forms.
- **The controls** are read from the page's source: each Settings domain's component (`settings/domains.ts`)
  and every file it imports, not type-only; the bridge hooks among them that change something (a mutation);
  and each action a hook's payload names (`HARNESS_ACTION`'s ten, `RULE_ACTION`'s three, `PLUGIN_ACTION`'s
  three). A control that presses no hook (the theme, the language, the setup guide's step doors) is a row
  named by what its domain's source must still say.
- **A door** names a kind and one of its doors. Each kind now names the doors it takes
  (`IHelpProposalKind.Doors`), and the service's writer of that kind spells each, which the kinds' test reads.

### 2. Built now

- **`across`** is a setting door: `read on|off|--clear` for a repository or a whole workspace, and
  `write-to <other> [--clear]` from a repository, judged by the edits `SET_READ_ACROSS` and `SET_WRITE_ACROSS`
  make. The other repository of a write-to is a name a helper can invent, so it must be registered. READ1
  built the verb and a room row saying `setting_propose` covered it, and the kind refused it.
- **`cap` and `adapter`**, which only a terminal set, are setting doors, the adapter checked against this
  machine's agents as an intake is.
- **The room** names every `daoris driver` verb that changes something in its doors table, a door owed
  included, and the owed doors' screens and commands.

### 3. A write-to is proposable

The canon's `repository-owns-its-work` treats writing across as needing the person's explicit say-so, and D107
made a declared relationship that say-so. A card the person applies is that say-so given. The card says so in
the terminal's own words (one way, and taken back with `--clear`), and nothing applies until Apply (D89).

### 4. The exemptions, by principle

- **The person's own press** (D89): a sign-in, typing into one or stopping it, a key, an account made or
  removed, a remote wired (its key) or unwired (its key dropped).
- **A discard** (D89): the session-branch sweep. A plugin's removal too, since switching it off undoes itself
  and is proposable.
- **Nothing changes**: `driver list`, reading the roster again, opening the log's folder, the folder picker,
  and a plugin's trial, which runs its code as the person and leaves nothing to propose.
- **A viewer's own look**: the theme and the language are this window's, never the machine's (D66).
- **What an agent may do**: a rule is never proposed by an agent (HELP1c, PERM2), and accepting another
  agent's proposed rule is the person's review of it.
- **Install and unpin** (HELP6): an installer run on this machine; a binary nobody chose.
- **Making a plugin with the kit**: a plugin is made as an ask at the repository that holds plugins (PLUG9).
- **`--share`** (DRV8, D104): a flag on the start of a headless loop, `daoris-driver drive --share`, not a
  setting. Nothing is stored to apply, Ask Daoris starts no loop, and the desktop's loop waits on the lock.

### 5. Owed, and what each waits on

Each is the desktop modules' to add, which this branch's lane did not reach:
- **`retry`**: its judge needs the parked quests, the verdict the quest's drawer shows its Retry by, in the
  facts a proposal is judged against (`HelpFactsAsync`).
- **An account made default** (`profile-default`): its Apply is `HARNESS_ACTION`'s own, a door on `IHelpDoors`.
- **The browser's settings** (which browser, where links open, extensions, favorites): their file is the
  modules' (`BrowserSettings`), applied through `BrowserModule`.

Until then the room names each one's screen and command.

*Built by HELP10 (2026-09-30), as this section says, with no new decision* (design §9.10):
- **`retry`** is a setting door. The loop keeps what each tick parked by its strikes (`ParkedQuests`), the facts
  carry it, and a quest not on it is refused; applied as `RETRY_QUEST`'s own edit.
- **An account made default** is the agent kind's `default` door, applied through `IHelpDoors.SetDefaultAccountAsync`,
  which `ScreenDoors` builds on `HARNESS_ACTION`'s own `profile-default`.
- **The browser's settings** are a kind of their own, `browser`, since their file is not the driver's: judged by what
  `BrowserModule` read of its files and applied through `IHelpDoors.ChangeBrowser`, `BrowserModule`'s own edits.
- **WSR6's *Bring up to date*** (D109), built since, is the `sync` kind. D109 fetches nothing until the person presses,
  so its card keeps the screen's two presses: the first Apply is the look (`TREES_SYNC_PLAN`'s list, its rows kept in the
  proposal's file), the second `TREES_SYNC`'s press on those rows only. Its Settings control is a door, and
  `daoris-driver trees sync` is held to the kind while the headless host's usage spells it.

Nothing here needed a decision of its own: each door is this section's, and the look is D109's rule applied to a card.

*Amended by LEFT3 (2026-10-01): clearing an account's default, which the screen's *Make default* on the tool's own row
does and no terminal verb could (D50), is `daoris agent profile default <agent> --clear [--workspace <name>]`, a twin of
`HARNESS_ACTION`'s `profile-default` held by a table on each side. Clearing a workspace's default falls back to the
machine's, as the screen's does, and the verb says which account its sessions run as now. Ask Daoris proposing a clear
is a door owed: the `agent` kind's `default` door takes an account and no clear, and taking one waits on the
service's `agent_propose` and the driver's judge. `HelpCoverageTests` holds it as a form owed, and the room names the
command meanwhile.*

*Amended by LOOK2c (2026-10-01): the screen says it too. *Use for a workspace* on the tool's own row clears that
workspace's account, and with a machine default set its sessions then run as that default, not in the tool's own home
the row names. No file can say "the tool's own home here" over a machine default, so offering that as a second choice
would be a new value in both twins' file, and is not built. Instead each of that row's choices says where it leads
while a machine default is set (*lab, which then runs as personal, this machine's default*), and every default's press
says what sessions there run as now, the fact the terminal's verb prints: `HARNESS_ACTION`'s `profile-default` answers
`default` (the workspace, the account or none, and whether it came from the workspace, the machine or the agent's own
home), from the resolution a start takes (`DriverModule.DefaultStanding`). The twins' table of edits is unchanged.*

**Why.** HELP6 and HELP8 each found a door built since the last pass that Ask Daoris could not reach, and
READ1 left one the room already promised. A list kept by hand drifts; a list a test derives from the doors'
own sources does not.

**Rejected.**
- **A door for every control**, sign-ins and keys included: D89 keeps them the person's presses, and a helper
  in the path of a key would be one more place a credential passes.
- **Exemptions written in the design only**: nothing would stop the next control.
- **Keying the controls by route**: one route carries several actions, and the hook is what a Settings
  component presses, with its payload naming the actions.
- **Building an owed door's driver half now**: a kind whose judge always refused, or a door with no
  implementation behind it, would be a proposal the room teaches and the driver never takes.
- **Proposing `retry` unjudged**, as its route takes any id: a helper can invent a quest id, and forgiving
  one that is not parked lets it run past its strikes.

**What the gates do not cover.** A real helper choosing `across`, `cap` or `adapter`; the write-to card on
the window in both themes; and a control that reaches the bridge without a hook, which the reading would not
see (none does today).

## D111 — A file's preview is a tab of the side bar, read from the session's tree and never written (2026-09-30)

**Decision (PREVIEW1, D76's held file tools, their trigger met).** The conversation's tool cards and
the review name files: a read, an edit, a file written, the review's changed files. A person wants to
read one without leaving the window. D76 held *a file tree and a document preview* until after the
conversation; this is the preview. D55 still binds: there is no editor, so this reads and never writes.

### 1. Where it opens

- **A tab of the right side bar**, after the views standing there, named for the file and closed by its
  own ×, which goes back to the tab it covered. It is keyed to the attended session, as the dock's tab
  is (FRAME6): each session keeps its own preview while the window is open, and none across a launch.
  One preview a session: opening another file replaces it, as VS Code's preview tab does.
- **It is not a view that moves** (DOCK1b). A view is a place that exists before anyone asks for it;
  a preview exists only after a click, and it goes where the reading room is, which is the side bar.
- **Two doors.** A file path in a tool card (the call's location, or an edit's file), and a file's
  button in the review's list, whose row still opens its patch in place as before. **A door that could
  only refuse is not offered** (UX5 U66): a path the page can see is outside the session's tree stays
  text, and a file the review lists as deleted has no button. With no opener (a browser, a detached
  session's window, Ask Daoris) a path is text, as a link is text with no link opener (BRW7).

### 2. What it reads

- **The file on disk now, in the tree the session's record names**: its own tree, or the repository's
  checkout for a conversation in it, since the record names the checkout then (D51). One route,
  `SESSION_FILE`, read-only and desktop-only, like the diff (D47 §4). It never crosses HTTP.
- **Three refusals, each a code** (`Refusals`, both catalogues, a throw site): a path outside the tree;
  a link inside the tree that leads out of it, followed segment by segment, since the string is inside
  and the bytes are not; and a path under `.git`, which is git's and not the work's. Two answers are
  information, in the review's class: the record names no tree here or the tree is gone, and the path
  is not a file now (deleted, moved, or a folder).
- **A bound, stated.** The first 256 KiB, cut at a line's end, and a sentence saying how much the file
  holds and that the file on disk has the rest (design §5's rule). **A binary file is said in a
  sentence** with its size: a NUL byte in its first 8,000 bytes, which is git's own test.

### 3. What it shows

- **The path relative to the tree**, never the machine's path (platform language §4). A line-numbered
  monospace view, highlighted in the file's language only where the highlighter ships it (REVIEW2).
- **The lines a tool call named, marked and scrolled to, where the card knows them**: a read's `offset`
  (the line it starts at, in the tool's own terms) and `limit`, from the call's own input as the wire
  carried it, and only a read's, since a search's `offset` is not a line. Nothing is inferred from a
  title, an edit's text or the output.
- **The file's changes against the line where the review already holds them**, one press away (*File ·
  Changes*): the review's own patch for that path, drawn by `PatchView`. The preview asks git for
  nothing: it reads the review's answer when the review has one, and says nothing of changes when it
  has none.

### What the gates do not cover

The driver's suite holds the reader's refusals through its link seam, and `FilePreviewLinkTests` holds
them over a real link (a junction on Windows), in the `Process` half the merge runs. The modules' suite
holds each refusal's code and the answer's shape; the web's holds the doors, the tab, the marked lines
and the review's patch over a mocked bridge. **Nothing here has looked at the window**: the tab and the
preview in both themes, 中文, and the side bar at its 300px floor are for the look. **Whether the
protocol door carries a read's `offset` and `limit`** depends on the adapter's `rawInput`, which was not
measured here; the native door carries the tool's input as it was given.

### Rejected

- **A view that moves**, beside the timeline and the review. A place that is empty until a click is a
  tab that says nothing until then, and every region's tab list would carry it.
- **Opening the file in the system's editor.** It leaves the window, which is the whole ask, and hands
  an agent's file to whatever the system opens that extension with.
- **A drawer** (D41 §4). It covers the conversation that named the file, and the two are read together.
- **Reading the file from git** (`HEAD`) rather than the disk. A live session's last edit is not
  committed yet; the preview says what the file is now, and the review's patch says what was committed.
- **Asking git for the file's diff from the preview.** The review already computes it, bounded and
  stated; a second path to the same answer is a second answer to keep in step.
- **The line an ACP location carries** (`locations[].line`). The driver keeps a location's path and
  drops its line today; carrying it changes the event's shape on both doors, and a read's input already
  says which lines it read.

*Amended by LEFT2 (2026-10-01): the line an ACP location carries is kept after all. A tool event gains
`line`, the line of its first location that names a path (the path a card opens), a whole number of zero
or more, and absent on the native door, which carries none; the page keeps it with the locations it came
with, replaced only when they are. The card marks it where a call's own input names no lines. What the
adapter installed here says (claude-agent-acp 0.84.0, read from its source, not measured on the wire):
a read's location is its `offset`, or 1 when it has none, and an edit's is its first hunk's start in the
file as it now reads. So a read's input stays the authority on a read: one whose readable input names no
lines read the whole file and marks nothing, since line 1 there is the adapter's default and not a line
it named, and only a read whose input the wire did not carry falls back to its location's line. An edit
now opens at the place its change starts. A line under 1 marks nothing. Nothing has looked at an edit's
marked line on the window.*

*Amended by D113 (REVIEW2, 2026-10-01): once a session's tree is gone, its preview reads the file from its landed
branch in the repository's checkout (`git cat-file blob`, the same bound and binary test), and says in a sentence that
it shows the file as that branch holds it. A path the branch lacks, a folder or a link is `PREVIEW_NOT_ON_BRANCH`;
`PREVIEW_NO_TREE` is left for neither the tree nor the landing's branch being here.*

## D109 — After a pull request merges, one press brings a repository up to date; Daoris fetches and never pushes (2026-09-30)

**Decision (WSR6).** The owner, after their first real pull request merged: *"we also need a post merge and
rebase logic pull latest master delete the merged branch and rebase working branches"*. The real case: the
ticket's branch was squash-merged, so one new commit on the line holds its content and none of its commits,
and a session still working had grown from that branch's tip. Bringing a repository up to date is three
steps, **listed first and done by a press**, on both doors, each row judged again right before it acts:

### 1. Pull the line

- **Fetch** the repository's line (D86) from `origin` — `git fetch origin <line>`, nothing else. It runs as
  the person, with their git credentials and their credential helper, with `GIT_TERMINAL_PROMPT=0` so a prompt
  nobody can answer fails instead of waiting, and within two minutes, as a plugin's push is (D100). A fetch
  that fails is named and the rest is judged from what the checkout already knows. **The list fetches**: it
  moves only origin's own refs, and nothing of the person's.
- **Fast-forward only.** In the repository's own checkout, `merge --ff-only` while it is clean and on the
  line, both read immediately before; where nothing has the line checked out, the ref is moved from the
  commit it was judged at (`update-ref` with the old value). Each of these is left and named: a local line
  with commits origin lacks (nothing to pull, and Daoris never pushes), one that diverged, a checkout on it
  with work in flight, and one checked out in another working tree. **Never a merge commit, a force or a push.**

### 2. Replay what still works on it

- **What is replayed:** a session branch whose tree no session running or waiting holds, and a branch a
  landing made and recorded (D102) whose work is not on the line. Each is replayed onto the new line with
  `rebase --onto <line> <cut>`: only its own commits.
- **The cut, for each kind.** A tree's opening now records where its branch started: the commit, the line,
  and the step before's branch when a chain's step grew from it (CHAIN2). A landing records the start of the
  session branch it was made from as its `from`. Where that start is on the line, the cut is where the branch
  leaves the line, a plain rebase. Where it is beyond the line (a chain's step on the step before's tip), the
  cut is that start, **only once its work reads on the line by WSR5's proof by content**: then the squash-merged
  parent's commits drop. Until then the branch **waits**, since replaying only its own would lose the work it
  builds on. A branch with no record (a tree opened before this build) is cut at its newest commit whose work
  reads on the line, walking its first parents back to where it leaves the line.
- **Where:** in a tree of Daoris's own, never the person's checkout. A session branch in its own tree, which
  is detached where it stands; a landing's branch, which nobody may have checked out, in a tree made for it
  under the trees home and removed after. Every git call says `core.longpaths`. The person's configuration
  still speaks (identity, signing, hooks), except `rebase.updateRefs` and `rebase.autoStash`, which would reach
  beyond the one branch judged.
- **Kept only once proven.** Between the old tip and the new one, only files the line itself changed between
  the cut and the new line may differ; then the branch moves, from the commit it was judged at. A conflict
  aborts and names the files, and the branch and its tree are as they were.
- **What keeps one:** a session still running or waiting; uncommitted work in its tree; checked out in a tree
  that is not a session's own (a landing's branch anywhere); **on its remote** — a remote-tracking branch of
  its own name, or a push the record kept — since replaying it would need a force push; and git unable to say.
- **Shared commits stay shared.** A landing's branch made at a session's tip takes the session's new commit,
  and a branch inside another is replayed onto the other's new commits, so the clean-up's proofs read them
  together afterwards as before.
- **The records follow the move**: the session branch's start becomes the line's commit it was replayed
  onto, and a landing's branch keeps its record with its new tip and `from`, so it stays the landing's.

### 3. Delete what merged

WSR5's landed half of the clean-up, after the replays: a landed branch a staying session branch leaned on
goes once that branch was replayed past its commits. Only local branches, never a remote one.

### 4. The doors

`daoris-driver trees sync [--repository <name>] [--yes]`: the list, and with `--yes` the press, exit 1 where
something the proofs cleared did not happen. Settings → Workspace → Session branches → *Bring up to date*:
nothing is asked until *Look for updates* (the list, `TREES_SYNC_PLAN`), and the press (`TREES_SYNC`) does
not fetch again and acts only on the rows the list showed. Ask Daoris's room names both doors.

### 5. What the first real post-merge run added (the parent, by hand, 2026-09-30)

- A tree made outside the trees home without long paths could not check out the repository's deepest files:
  the replay's tree is under the trees home, and long paths are on every call.
- `git diff <old tip> <new tip>` was empty after the replay: the proof above, made before anything moves.
- **A tree folder something held open.** `git worktree remove` lets go of the tree, then fails on its folder
  and exits non-zero. A removal, the tidy included, now says plainly that the tree and its branch are gone
  and the empty folder is left, and why; the clean-up deletes every empty folder under the trees home once
  nothing holds it. Only an empty folder is ever deleted.
- A rebase by hand leaves the landing's record at a tip the branch no longer holds, so WSR5 treats it as the
  person's. Daoris's own replay moves the record with it.

This **amends D37's reading**: Daoris now reaches the network for one thing, a fetch, as the person, by
their press; it still never pushes, and the push stays human or a plugin's (D87, D100). It **amends D87's
rejection of a rebase**: a landing still keeps the session's commits as they were made; a replay after the
line moved is the person's press, and never of a branch on its remote. It **amends D102**: a replay Daoris
made moves a landed branch's recorded tip, so the branch stays judged. D51 rule 7 stands: a replay rewrites a
branch nobody pushed, by a press, and deletes nothing.

**Why.** The line moves on the platform, and every tree Daoris opens grows from the local line, so a stale
line grows the next session from before the merge. A squash merge puts none of a branch's commits on the
line, so a child branch replayed plainly carries its parent's commits back in, where they conflict or land
twice; only its own commits belong on the new line.

**Rejected.**
- **`git pull`.** It merges or rebases by the person's configuration, and may make a merge commit in their
  checkout.
- **Replaying in the person's checkout**, or switching its branch: the trespass `reaching-in` was written from.
- **`rebase --update-refs`.** It moves every branch pointing into the range, the person's and pushed ones too.
- **Git's fork point, or patch equivalence** (`--fork-point`, `git cherry`): the first reads the line's
  reflog, which expires and knows nothing of a platform's squash; the second matches no squash of several
  commits (D102's reason).
- **The session record's base commit as the cut.** A session carried on in the same tree begins where the one
  before stopped, and a chain step's start on the step before's tip is written nowhere else.
- **`fetch --prune`.** A remote branch gone says nothing of where its work went (D102), and pruning would move
  refs the list did not need.
- **Replaying a pushed branch and leaving the force push to the person.** The branch on the remote and the one
  here would disagree, which is the trap a force push exists to settle; that is the person's to decide.
- **Fetching when the screen opens.** Looking reaches the network as the person, so it waits for their press.

**What the gates do not cover.** The shape is held by `TreeSyncTests` (ten tests, in the `Process` half: the
owner's case end to end, a missing record, a step that waits, a conflict, the line in each checkout state, a
diverged line, a failed fetch, pushed and dirty branches, a press on listed rows only, and a held folder).
**They were written in the branch and not run there** (MOD8: the parent runs the `Process` half at its merge).
The git sequence itself was run by hand in scratch repositories: a squash-merge on a bare `origin`, the fetch
and fast-forward, the detached replay cutting at the parent's tip, the proof, the moves by compare-and-swap, a
conflict aborted and put back, and a folder held by a native process (git 2.53, Windows). No platform's squash
was run, and no fetch reached a real remote or its credentials; an SSH passphrase prompt is bounded only by
the two minutes. A driver resuming a session in a tree between the list's check and the replay is not closed.
The screen's section is held by the vitest loop over a mocked bridge, not looked at on the window. A landing
branch rebased by hand keeps a record it no longer matches, and no door re-records it. An Ask Daoris proposal
for this press was not built: it would reach into the service's proposal kinds and the page's cards. *(HELP10
built it since: the `sync` kind, a card whose first press is the look; see D110.)*

*Amended by LEFT2 (2026-10-01): the window between the list and the replay is closed. A repository's trees
have a lock under the home (`locks/trees/<workspace>/<repository>.lock`, `TreeLock`), taken by the file
system's own sharing: a driven start and a conversation's own tree take it shared, from before the tree is
chosen or resumed until the session's record is open, when the ledger holds the tree; the press takes it
alone around a repository's replays, and asks the ledger again inside it, so a session opened since the
list is seen in use. A start that meets a replay is held for that look and carried on at the next; a
replay that meets a start leaves that repository's replays, each said. Nothing waits for the lock, and a
process that dies lets go of it with its handles. The line's fast-forward and the deletions are not held,
since neither touches a session's tree. `TreeLockTests` hold the lock's sharing in the fast half; the
press leaving a repository a session is starting in, and seeing one opened since the list, is a
`TreeSyncTests` case in the `Process` half, written in the branch and not run there (MOD8). No driver tick
was run against a held repository. A caller of the press that hands no fresh look at the sessions in use
(both doors hand one) is still judged by the list's look inside the hold.*

*A family-rehearsal check (LEFT2) drives the terminal's door over the example family's newcomer: a session's
work put on a branch by a branch rule, pushed to a bare `origin` in scratch and squash-merged there, the
session carried on in its tree with one commit; then `daoris-driver trees sync --repository newcomer` lists,
and `--yes` fast-forwards the line, replays the session branch with only its own commit (the squashed one
dropped as already on the line), and deletes the landed branch, pushing nothing. It was written in the
branch and not run there; the git sequence it relies on (the squashed commit dropped, the proof's diff
empty) was run by hand in scratch repositories. It stays with `--repository`, since the examples'
registered roots are not repositories of their own and git walks up from a folder that is not one.*

*Amended by WSR7 (2026-10-01), from the owner's workspace of 29 repositories. **A look fetches four repositories at a
time** (`SyncBounds.FetchesAtOnce`), and **the page waits as long as the host may work** on each long route
(`bridge/call.ts`'s `hostBounds`, a twin of `SyncBounds` held by `SyncBoundsTests`): the bridge's default 30 seconds
gave up on a look that ran for minutes, and the host's answer reached nobody. **The fetch writes no `FETCH_HEAD`**
(`--no-write-fetch-head`, git 2.29 and later): a fetch that fails empties that file and stamps it with the failure's
time, so the person's own last fetch could no longer be read, and only origin's refs are the look's to move. **What was
not fetched is said once, before the rows**, on both doors: how many, grouped by git's reason; when each last heard
from origin, the newer of `FETCH_HEAD`'s time where it holds a fetch and the time `origin/<line>` last moved here, or
never; and what the git on the path needs to reach an origin over SSH (a key its own ssh reads, or `core.sshCommand`)
or HTTPS (a credential helper that answers without asking), since the person's own Git client may carry a git and an
ssh of its own. A row carries a short mark. Which repositories a look takes is D112. The bound, the words and the
twin are held in the fast half. That a failed fetch leaves `FETCH_HEAD` as it was, and that a fetch that lands logs
`origin/<line>` with its time, were run by hand in scratch repositories (git 2.53, Windows), and a `TreeSyncTests` case
in the `Process` half asserts both, written and not run here (MOD8). No fetch reached a real SSH remote. `HANDOFF` and
`LAND_SESSION_TREE` still wait the bridge's default, since the work frame's tests hold those calls' exact shape.*

*Amended by D121 (TOOLS5, 2026-10-01): the fetch's git is the git the tools resolve, started by its whole path. A git
the person chose that cannot run is the fetch's answer, in the resolution's words. What was not fetched does not yet
name Settings → Tools, which waits for TOOLS7's screen.*

## D112 — Bringing up to date looks at the repositories that hold Daoris's branches; every other is listed apart, and included by the person (2026-10-01)

**Decision (WSR7 c).** The first look on the owner's workspace of 29 repositories fetched all 29, and after one
pull request merged it proposed fast-forwarding the line of seven repositories where Daoris held nothing. The
owner's ask that D109 was built from names three things, all of them Daoris's: pull the latest line, delete the
merged branch, rebase the working branches. So:

1. **By default a look, and a press, take the repositories that hold a branch of Daoris's**: a session branch
   (`daoris/…`), or a branch a landing recorded (`landings.json`, D102) that still stands. That is read on the
   machine, one `for-each-ref` per repository beside the landings record, and reaches no network. A repository
   git cannot answer for is looked at, so git's own words reach its row rather than the repository vanishing.
2. **Every other repository with a checkout is listed apart**, by name: not fetched, not judged, and nothing of
   it moves. The person includes it. On the screen the list sits under the section, collapsed, each repository
   with a box and one box for all of them; the next look fetches and judges what was ticked, and keeps it
   included when it looks again. On the terminal, `daoris-driver trees sync --all` includes every one, and
   without it the list ends with one line naming the rest. **Naming a repository includes it**:
   `--repository <name>`, and an Ask Daoris proposal targeting one.
3. **The press acts on what the person was shown.** A row the screen listed names its repository, so a
   repository included at the look is included at the press without being asked for again, and the press
   never reaches one it did not list.
4. **Ask Daoris's sync card takes the same default**, and a card naming a repository includes it. Its look's words
   name the repositories it left apart, since its rows say only what it looked at, and its press asks the sessions in
   use again inside each repository's hold, as the screen's does (LEFT2).

**Why.** Fast-forwarding the line of a repository Daoris holds nothing in is Daoris moving the person's branch
for no work of its own. D109's reason to pull a line, that a stale line grows the next session from before the
merge, holds where Daoris's work stands, and after a pull request merges the repository it merged in holds the
landed branch, so it is in the default at the moment it matters. The cost was real as well: every fetch is a
round trip as the person, and 29 of them, one after another, were the look the window stopped waiting for
(WSR7 a).

**Rejected.**
- **Every repository with a checkout** (D109 as built): the seven fast-forwards nobody asked for, and a look
  that pays a network round trip for each repository Daoris has nothing in.
- **Only the repository whose pull request merged.** Daoris cannot know which one merged before it fetches,
  and a session branch elsewhere may be waiting on a landing that merged a day earlier.
- **Judging the others against their last fetch, without fetching.** Cheap, but it fills the list with stale
  answers about repositories nobody asked after, which is the noise this removes.
- **Leaving the others out altogether.** Once Daoris's branches in a repository are gone, the person could no
  longer bring its line up to date from here, and a later session will grow from that line. Listed apart,
  collapsed and included by a tick keeps it one press away.
- **Counting the repositories the driver drives as Daoris's.** Considered, since the next driven session grows
  from the local line. Left out: driving says Daoris may start work there, not that it holds any, and the tree a
  session opens grows from the local line or origin's (D86), whichever the person last brought here. A driven
  repository with nothing of Daoris's in it yet is one tick from holding a branch, and one tick from the default.

**What the gates do not cover.** The scope rule without git (`SyncScope`, which repositories a scope includes)
is held in the fast half. Reading which repositories hold Daoris's branches, the look taking only those, the
press including a repository only where a listed row names it, and `--all` are `TreeSyncTests` cases in the
`Process` half, written in the branch and not run there (MOD8). The screen's list apart is held by the vitest loop
over a mocked bridge, and was not looked at on the window. The card's words are held by `HelpSyncProposalsTests`, and
that every modules door's press hands the sessions in use again is a source-reading test, since only a live driver
opens a session between a look and its press.

*Amended by LEFT3 (2026-10-01): Ask Daoris's sync card says what was not fetched and which repositories were left
apart on the card itself, not only in its look's words. The look keeps both in the proposal's file beside its rows
(`notFetched`: each line not fetched, with git's reason, when it last heard from origin and how origin is reached;
`apart`: the repositories' names), and the card says them as the screen does: a note of what was not fetched, grouped
by reason, with what the git on the path needs, and the repositories left apart collapsed, with how a proposal
includes one. A file a look kept before LEFT3 holds neither, and its card says nothing more. Held by
`HelpSyncProposalsTests`, `DriverModuleHelpTests` and the card's vitest cases; not looked at on the window.*

## D113 — A landed session reads as landed: its review and preview read the landed branch once the tree is gone (2026-10-01)

**Decision (REVIEW2, found by the parent looking at the installed window).** A session accepted under a branch rule
that tidies (D87, D88) keeps no tree. Its review then said git could not read its range (`SESSION_RANGE_UNREADABLE`)
and still offered Accept, which named the branch that already existed, Send back and Discard the tree; its preview
answered `PREVIEW_NO_TREE`. A finished session is usually a landed one, so its review and preview were the ones that
said nothing. So:

### 1. What a landed session is

- **The session's landing is the newest entry for it in `<home>/landings.json`** (D102), standing or a trace (§4).
  A merge makes no entry, so a session merged into its line is not "landed" here; §5 says what its review does.
- **Where that branch stands**, read in the repository's own checkout (the registry's root): *standing* (the branch is
  there and still holds the commit the landing made it at), *gone* (no branch of the landing's is there, or the record
  says it went), *not-ours* (a branch of that name that does not hold that commit: rebased or replaced by hand, so
  never read as the session's work), or *no-checkout* (no root here, or a root that is not the top of a repository of
  its own). **The checkout is proven before git is asked anything else**, since git walks UP (FIX-LOG): a root inside
  another repository would otherwise answer for that one.
- **The review reads as landed while its landed branch stands, or once its tree is gone.** A tree still here after its
  branch went is a session that may have carried on after its landing (WSR6 replays its own commits), so its review is
  the tree's again, with every act. The host decides it (`landed.asLanded`); the page follows it.

### 2. The review (`SESSION_DIFF`)

- **The tree first, while it is here**: it holds what the session did and anything it did after landing. A landed
  session's answer then also carries where it landed.
- **Once the tree cannot be read, the landed branch**: the changes from where its work grew from — the recorded `from`
  (WSR6) while it is in the branch's history, else the branch's merge-base with the line — up to the branch, as
  `git diff <from>..<branch>` in the checkout. Every question is a read of refs and objects (`rev-parse`,
  `merge-base`, `cat-file`, a diff of two commits), so the person's checkout, its working tree and its index are never
  touched, whatever state they are in. The bound is the review's, and its sentence names the checkout.
- **A branch gone since is said plainly**, with what the clean-up proved when it removed it (§4) and whether its work
  reads on the line now: WSR5's proof by content on the commit the landing made it at, while git still holds it
  (on the line, merged, the files that differ, git's words where it could not say). Once git has pruned that commit,
  it says so and does not guess. No files are shown for a gone branch.
- **The answer carries** `source` (`tree` or `branch`) and `landed`: the branch, repository, line, when, the plugin,
  whether it pushed, the pull request, the state, `asLanded`, `reads`, `removed`, and git's words where a standing
  branch's changes could not be read. Never a machine path.
- **A tree gone with no landing recorded** is its own information, `SESSION_TREE_GONE`, in the review's class, so the
  page offers nothing that acts on a tree.

### 3. The acts and the preview

- **While the review reads as landed: no Accept and no Send back.** A second landing is refused while the branch
  stands (D87 never moves a branch it did not make), and with the tree gone there is nothing to land; the work's next
  move is its branch's. **Discard only where a tree is still here.** **The hand-off stays where one applies** (WSR5b,
  `HANDOFF_PLAN`), and where none does, nothing is offered. The note at the top of the review says where the work
  landed, when, and links the pull request a plugin opened.
- **The preview (`SESSION_FILE`) reads the landed branch once the tree is gone**: what the branch's tree names at that
  path (`ls-tree`, literal pathspecs), read as bytes with `cat-file blob`, with D111's bound (256 KiB at a line's end,
  the whole size said) and binary test (a NUL in the first 8,000 bytes). `.git` and a path outside the repository are
  refused as in the tree; a path the conversation named inside the tree that is gone is the same path on the branch.
  Git does not follow a link inside a tree and neither does this: a link, a folder or a path the branch lacks is
  `PREVIEW_NOT_ON_BRANCH`, naming the branch. The answer names the branch, and the page says in a sentence that the
  file is as that branch holds it, and that the branch has the rest of a long one. **`PREVIEW_NO_TREE` is left for
  neither**: no tree, and no standing branch of the landing's here.
- **The terminal's twin** (D50): `daoris-driver trees land <session> --plan`, for a session whose review reads as
  landed, prints what the review's note says (`LandedReviewWords`), and the press lands nothing again and says why,
  exit 1, naming `trees hand` where the branch stands unpushed. For a session landed before whose branch went while its
  tree stayed, `--plan` prints the plan and then the same sentence.

### 4. The record keeps a trace

D102 forgot an entry once its branch was gone, which left the review of a session whose branch the clean-up removed
nothing to find. **An entry is now marked, not forgotten**: `goneAt`, and where the clean-up removed it, `removedAs`
(on the line, merged, inside another) and `removedOn`. A trace is never judged, handed on, replayed or pushed:
`All()`, which the clean-up, the hand-off, bringing up to date and Ask Daoris read, returns standing entries only, and
a new landing of the same name replaces only a standing one, so an earlier session's trace stays its own. Traces
accumulate one per landing, and nothing prunes them yet.

This **amends D102** (an entry is marked gone rather than forgotten) and **D111** (the preview of a session whose tree
is gone reads its landed branch). D87 stands: accepting is refused while the landed branch stands, and the review now
says so before the press instead of offering it.

### 5. Rejected

- **Reading the person's checkout** (its working tree, or `git show` into it, or a worktree made for the review). The
  checkout is theirs and may hold work in flight; a read of objects answers the question without touching it.
- **The landed branch first, whenever a landing exists.** A tree still here holds the session's work after its
  landing, which the branch does not; and it is the file on disk that D111 says a preview shows.
- **Refusing a second landing always.** A session that carried on, whose landed branch WSR6 deleted after its pull
  request merged, lands its new work as any other.
- **Keeping Send back for a landed session.** The work has left the session; its follow-up is its branch's pull
  request, or a quest the person writes from Quests.
- **Following a link in the branch's tree, or showing a link's target as text.** The preview reads files; git itself
  does not resolve a link inside a tree.
- **Re-recognising a merged session as landed** (a merge records nothing, D102): its review after a tidy is
  `SESSION_TREE_GONE`, which offers nothing that acts on a tree. A merge's own record is not built.

**Amended 2026-10-01 (LOOK2b): the rail reads as landed too.** The session list said where a session's tree was
(*in s-2394e5d9*) after its landing tidied that tree away and its branch went. A row now says where the work landed
(*landed on `<branch>`*, and *gone since* for a trace) by the review's own rule: while the landed branch stands, or once
the tree is gone. A tree still here after its branch went is named as before, and a tree gone with no landing (a merge,
which records none) is not named at all. The rail asks once for the rows it shows (`SESSION_WHERE`), naming the tree
each record holds; the host reads `landings.json` and whether a tree of its own home's is there, and runs no git, so the
row states the branch as the record holds it and the review says where it stands. It is not a field of the sessions
listing, which is the service's records and travels (D47 §4), where a landing is this machine's (D102). Held by
`DriverModuleSessionsTests` and the vitest loop (`SessionRow`, `SessionRail`); not looked at on the window.

### What the gates do not cover

The record's traces, the sentences, the answer's shape and each refusal's code are in the fast halves
(`LandedRecordTests`, `FilePreviewTests`' listing, `DriverModuleTreesTests`); the page's note, the acts and the
preview's sentence are held by the vitest loop over a mocked bridge (`LandedNote`, `DiffPane.landed`, `FilePreview`).
The review and the preview over real git — a tidied landing read from its branch with the checkout dirty and on
another branch, a branch removed after a squash, one deleted by hand, one whose commits were pruned, one rebased by
hand, a root inside another repository, and the preview's bound, binary test and refusals — are `LandedReviewTests`,
in the `Process` half, written in the branch and not run there (MOD8). The same sequences were run by hand against
this build's library in a scratch repository (a probe, not committed), and each answered as written. The terminal's
twin was not run: it needs a service. **Nothing has looked at the window**: the note in both themes, at the side
bar's 300px floor, and 中文.

*Amended by LEFT3 (2026-10-01): the traces are bounded. Each repository keeps its newest 50
(`LandedBranches.TracesKept`), by when each went, dropped at the record's next write; a standing entry is never
dropped. A count per repository rather than a span: the file grows with landings, not with the clock, and a person
back after a month away still reviews last month's sessions, while a busy repository cannot push a quiet one's
traces out. A session whose trace was dropped reads `SESSION_TREE_GONE` once its tree is gone, as one merged does.
Held by `LandedTracesTests` in the fast half.*

*A merge's own record was considered again (LEFT3) and is still not built, for §5's reason and one found looking:
a merge's "branch" is the line itself, and every reader of the record treats an entry as a branch Daoris made. The
clean-up deletes a recorded branch whose work reads on the line (`git branch -D`), bringing up to date replays one
and counts its repository as holding Daoris's branches (D112), and the hand-off pushes one. A merge entry that
reached any of them would delete, move or push the person's line. Building it is a change to what a landing record
is: an entry of its own kind that `All()` never returns, the review reading the merge's own range (its first parent
up to the merge commit) and the preview reading at that commit rather than at the line's tip, each with its test.*

## D115 — Daoris develops Daoris: a repository declares its lanes, a queue lands them, a steward keeps the records (2026-10-01)

**Decision (DEV1, from the owner's *"we should be able to run sub-agents cross darois development … daoris
itself need to have a proper develpment cycle too"*).** The contract is `docs/2026-10-01-self-development-design.md`.
The parallel cycle MOD1–MOD9 built is run by an assistant session acting as the parent. Daoris's driver runs it
instead, and every piece is a family feature, so an adopter with lanes gets the same cycle.

1. **Lanes are domains a repository declares** in `daoris.lanes.json` at its root, which replaces
   `tools/lanes.json`. Each lane has an `id`, a `title`, a `summary` and path globs in today's grammar. One lane
   may be the `steward`'s, and its paths are the records `parent` listed. A lane may name the declared gates its
   landing needs, and only the steward's narrows. No file means no lanes.
2. **A quest addresses a lane as `repository:lane`, or `repository:lane+lane`.** The quest keeps `to` as the
   repository and gains `lanes`. Its id widens only when there are lanes, so every existing id stands. `connect`
   sends the lanes' words to the registry, never their globs. The exchange refuses a lane nobody declared. It
   allows a quest from a repository to one of its own lanes, and still refuses one to itself with no lane.
3. **Staying in a lane is told, then checked.** The session's target names its lanes and what they own. At landing
   the branch's changed paths are classified against the **line's** copy of the file. Any path in another lane,
   in the steward's lane, or outside every lane sends the branch back. The merge tool only reported this, since a
   parent judged it; with no parent in the middle, the steward names every lane a piece of work needs when it
   dispatches.
4. **Sessions run beside each other in one repository.** A session outlives the tick that started it, in every
   repository: today a tick waits for every session it started, so nothing new starts until the last one ends. Work in flight holds
   its lanes, from a session's start until its quest closes, the wait in the queue included, and that lock is
   the planner's, never the ledger's. The oldest waiting quest reserves its lanes. `laneCap` (default 3) counts
   a repository's running sessions plus the queue's gate run. No new session starts while the queue runs a gate
   declared `quiet`. Apart from outliving the tick, a repository that declares no lanes keeps today's behaviour.
5. **The queue is a third landing form beside merge and branch.**
   - A driven session calls `session_ready` and ends holding its quest. Its record parks `queued`, a field and not
     a new state.
   - The queue gates serially in a queue tree of its own: a detached linked worktree under `<home>/queue/`.
   - Each entry is merged `--no-ff`, lane-checked, and run through the repository's declared gates by `kind`
     (check, suite, rehearsal), with the rehearsals once per batch.
   - A failed `quiet` gate is run once more, whole, and reads FLAKE if it passes.
   - On green the queue fast-forwards the line in the root checkout under the merge door's guards and
     `TreeLock`. What lands is exactly what was gated.
   - A conflict or a failed gate is sent back through the answer door (D83) with the log's tail, and the session
     carries on in its tree. It is never forced, never rebased, never pushed. A third failure is not sent
     back: the record stays parked for the person.
   - A landing is answered too, and the carry-on closes the quest `done`, so **done means landed**.
   - `daoris-driver queue add` queues a branch from outside.
6. **A steward session keeps the records.** A quest to a laned repository with no lane goes to the steward's
   lane. The steward splits the work into lane quests, reserves their decision numbers, and gives each a `then`
   step that records it. Its lane lock spans its time in the queue, so two stewards never reserve one number.
   Lane sessions write their own decision, changelog line and other union records. The steward moves backlog
   rows, the one record union cannot serve. The queue writes no record.
7. **The person** sets the target, reads the landed history, republishes the install and looks at the window.
   The queue never republishes, because the install is what runs it. Push, publish, release and history stay
   the person's.

**Why.** Each piece is the mechanical half of something the parent does by hand today. The judgement halves go
to sessions: the split, the work, the words and the records. The queue is the merge tool made project-agnostic.
Kinds and quiet are declared rather than read from Daoris's own command strings, and the trailer rule becomes a
declared check. The lane is D46 §9's answer made concrete: *wanting parallelism within a domain is a reason to
split the domain*.

**Rejected** (the design's §10 has the full list):
- **One repository per lane.** Twins change in one commit, and the collisions were inside files.
- **Lanes in `daoris.json`.** The manifest is the doctrine tool's inert contract, and lane globs change with
  every new folder.
- **The lane inside `to`.** Every repository-keyed lookup and older builds would read a repository that does not
  exist.
- **Enforcing lanes by permission rules.** A deny cannot be carved back, and the protocol door refuses the ask.
- **The lane lock in the ledger.** Pacing is not corruption (D51).
- **Gating in the person's checkout.**
- **Rebasing lane branches.**
- **A queue branch instead of a detached `HEAD`.** D88's proof would read unlanded work as landed.
- **The queue closing the quest.** D46.
- **Closing `done` before landing.** Done would lie, and a chain would start on unlanded work.
- **The queue or the intake keeping the records.**
- **Choosing gates by what a branch touched.** MOD9's incident.

**What it amends, when built.**
- D51 rule 6 (*nothing merges itself*): a repository whose rule is `queue` lands on green, because the person set
  that rule.
- D82: a queue repository's next step grows from the line, where the step before has landed.
- D87: a third form.
- D106 and the dispatch skill: the parent's records become the steward's lane.
- The exchange's self-address refusal: narrowed to a quest that names no lane.
- The driver design §9 (*no parallel sessions within one repository*): PAR1 already relaxed it for trees, and
  lanes are its *split the domain*.

Each row that builds a piece notes the amendment where it lands.

**Built 2026-10-01 (DEV3): a session outlives the look that started it** (§3.1, point 4 above).
- The watch keeps the running sessions (`RunningSessions`) and hands them to the driver it builds for each
  look. A look syncs, stops a lost claim, plans and begins every start. It waits for each start only until the
  session's record is open or the start came to nothing, then returns. A hold, a refusal or an error before the
  spawn is still that look's to report, and every session it opened is in the ledger before the next look plans.
- Each ending joins the next look's report, and wakes the watch at once, so its slot is used and the ending is
  said without waiting out the poll.
- `RunUntilIdleAsync` looks until nothing runs, a look starts nothing and no ending is left unreported. Between
  looks it waits for the next ending or the poll. `RunOnceAsync` is one look and then every session it started
  to its end, with the sync beside them; the headless `--once` uses it, so what it prints is what it printed.
- Unchanged: the sync at every look with the lost-claim stop after it (D68 §5, §6), the orphan sweep, the
  shutdown's interrupted record (D104, amended there), the machine's cap, PAR1 and `driver.lock`.
- Building it found three things the design did not say:
  1. With trees on, a look could find a quest still open while its own session works, before that session
     takes it or when it never does. Nothing held that quest, so a second session would start on it in a
     second tree. The planner now holds a quest an active session serves (`RepositoryBusy`, naming the
     session), from the quest each active record carries.
  2. A nudge that arrived during a look was lost until the next poll. Looks are short now and the person's
     controls nudge often, so the watch counts nudges and looks again at once.
  3. A failed look leaves its endings unreported. After one the watch waits out its pace rather than wake on
     them again and again against a service that is down.
- **What a person sees.** A quest published while a long session runs starts at the next look instead of
  after that session, which could take up to its 30-minute timeout: at once when it is published on the
  window, whose publish nudges the loop, and within the poll (15 seconds by default) when it arrives any other
  way. Every control that nudges the loop is heard while sessions run, where before it waited for them. Each
  session's ending is said as it happens, not when the last session of its tick ends, and the window hears a
  look when a session starts, not only when it ends.
- **What the gates do not cover.** The fast half holds the scheduling with an in-process stand-in for each
  start's run. The same cases over a real stub harness, and the shutdown marking two sessions interrupted, are
  in the `Process` half, which this branch did not run, and neither did it run the family rehearsal.

**What the gates do not cover.** This change is documents only, and nothing is built. Its statements about
today's code were read from the files the design's §0 names: the planner, the tick and the watch, the trees, the
tree lock, the landing rules, the ledger, the exchange, the merge tool, the lane map, the gates and the skill.
`verify` checks the records' shape and the budgets, and none of those words. Its statements about the future
are design. The design's §9 says which of them a rehearsal can prove, and which wait for DEV10's real run.

*Built in part by DEV2 (2026-10-01): point 1's file. `tools/lanes.json` is now `daoris.lanes.json` at the root,
with eight lanes by id: `web-shell`, `web-settings`, `driver`, `modules`, `service`, `cli`, `tools` and the
steward's `records`. Each has a title and a one-line summary. `records` holds what the parent's list held and the
map itself, with `gates: ["universal", "cli"]`, and *Tools and records* is now *Tools*. The docs' laneless group
carves the archive out (`!docs/task-archive.md`), so no tracked file has two places. `tools/merge-branch.mjs`
reads the file. Its lane report names lanes by id and title, and names the steward's records where it named the
parent's. It refuses an unreadable file by §2.1's rules, naming each problem. The commit check, the laneless report
and the gates are unchanged: the tool still runs every gate whatever a branch touched, so it reads `gates` only to
check each names a declared gate, until the queue (DEV5). Every tracked file classifies as before, with the
steward's paths where the parent's were. Held by `merge-branch.test.ts`, which also holds the parallel design's
§5 table to the file by id and title. One consequence for a later row: a branch that adds a path no lane owns must
place it in the steward's file, or the lanes test fails. The dispatch skill names that as the one exception and
the merge tool reports it; in the driven cycle §2.3's check would send such a branch back, so DEV9's steward
instruction needs an answer for it. §11's twin waits for the driver's reader (DEV6).*

*Built in part by DEV4 (2026-10-01): point 2. `connect` reads `daoris.lanes.json` with the CLI's own code
(`lanes.ts`: the file's rules in the merge tool's sentences, all but `gates`, which the queue judges, and the
words rule) and sends each lane's `id`, `title`, `summary` and `steward`, never its globs, always as a list. It
refuses an unreadable file, naming each problem. The registration keeps the words by `Declared.Lanes`, the
CLI's `laneWords` twin with the same table. Unlike `uses`, a registration silent about lanes keeps the row's:
the page's add, an import and an older client re-register without reading the file. The registry's HTTP and
MCP answers list them. The exchange splits the address once (`QuestAddress`). It refuses a lane the
registration does not declare, and any lane of a repository that declares none, naming the lanes there are,
and it allows a self-addressed quest only when it names a lane. The quest keeps `to` and gains `lanes`, sorted,
in the declared spelling, and `MakeId` appends `@<lanes>` only when there are some. The log, the cache, the
wire, both doors, the remote sync (D47's note) and the intake's room carry them, and the page shows them on
the Quests card and drawer. Left out: the import reads no lanes, since the service stores what `connect`
sends; a `then` step takes no lane address yet (the steward's record steps, DEV9); and the page's composer
offers no lane, while the terminal's `--to` and `quest_publish` take one. Held by `lanes.test.ts`,
`connect.test.ts`, `RegistrationStoreTests`, `QuestLaneTests`, `McpToolsTests`, `LocalHostTests`, the driver's
`LaneReadingTests` and `RemoteSyncTests`, `QuestsView.test.tsx`, and the family rehearsal's phase 4b, which the
parent's serial run at merge proves.*

## D117 — One repository, every agent: knowledge and skills under `.agents/`, a mirror for the agent that reads `.claude/`, rooms, and a set-up the repository's own session does (2026-10-01)

**Decision (LAYOUT1, the owner's *one repository, every agent*: the reference harness's own layout, applied to
this repository and to every repository Daoris manages, with a way to set one up).** The contract is
`docs/2026-10-01-agent-layout-design.md`. It extends D59, which moved only the always-loaded tier.

1. **The agents layout.** `knowledge/` and `skills/` live under `.agents/`; the always-loaded tier stays a region
   in `AGENTS.md`. A second descriptor, `agents`, selected by the manifest's `harness`, serves several harnesses
   at once: one target, plus what each harness that does not read it needs. `claude-code` stays for every
   repository that has not moved.
2. **No links.** Where the reference links, Daoris writes files. `CLAUDE.md` holds the `@AGENTS.md` import, at
   the root and in each room. `.claude/skills/` holds a mirror of every skill in `.agents/skills/`, canonical
   and local, each `SKILL.md` with a mirror header under its frontmatter. The lock records each mirror, and a
   mirror is measured against the lock (D13): an edited one is refused, naming its source, and `upstream` takes
   a canonical mirror's edit.
3. **Rooms.** A folder with an `AGENTS.md` of its own is declared in `daoris.json`'s `rooms`. `sync` keeps its
   `CLAUDE.md` pointer, the roster lists every room, and a declared room with no instructions fails `check`.
   Daoris never writes a room's text. A lane names its rooms, and its session's prompt names them (D115).
4. **A move is the repository's manifest change, and `sync`'s cells**, enumerated as D19's. Daoris moves its
   own files. The repository's own documents in an old tier refuse the move until the repository moves them,
   since the index would stop listing them and nothing would say so. A link, or a link held as text, is refused
   and never written through. The lock, not the manifest, says where the files are.
5. **The service reads the same root**, skips mirrors and indexes rooms.
6. **Decision records stay in `docs/`.**
7. **A repository is set up by its own session.** *Set up for agents* on the screen, `daoris-driver setup` and
   an Ask Daoris `setup` proposal each publish one ask to one repository, carrying what was read on its line,
   the steps and how to close it. Its session runs the CLI on its branch and lands by the workspace's rule. The
   screen shows each repository's adoption, its layout and the agents it serves, from measured cells only.
8. **Measured before relied on.** LAYOUT2 measures every cell of the design's §1 that is not measured, and
   nothing in the layout depends on a nested instruction file being loaded.

**Why.** `AGENTS.md` is the one instruction file all three agents read (D59's measurement), and `.agents/skills/`
is the skill root most of them read: dsh natively, per working directory, in any home, and the reference ignores
per-agent metadata there in a file named for codex's maker. The reference's mechanism is links, and on a checkout without links, the
owner's, its `CLAUDE.md` is 9 bytes reading `AGENTS.md`: Claude Code loads the path. A copy and an import work on
every checkout, and the lock already knows how to keep a copy honest. A set-up rewrites what every future session
reads, so it is the repository's own act and its owner's review (D32). The quest carries the playbook because the
playbook is Daoris's own document, which a session in another repository cannot read.

**Rejected** (the design's §9 has the full list):
- **Links**, the reference's mechanism: D3, and measured failing on the owner's checkout.
- **`.agents/skills/` with no mirror**: Claude Code does not read it.
- **The source in `.claude/`, mirrored into `.agents/`**: the source goes where most agents read.
- **A mirror of knowledge**: nothing auto-reads knowledge.
- **Rooms found by walking the tree, or written by Daoris.**
- **Decision notes under `.agents/`**: the records already converge with the reference's in substance, their
  numbers are cited everywhere, and people read them.
- **Daoris replacing a repository's link, or setting a repository up by writing into it.**
- **One quest for a whole workspace.**
- **Syncing from the driver or the service**: a second implementation of D19's table.

**What it amends, when built.**
- D7 as amended by D59, and the instruction-file design's §3: knowledge and skills move under `.agents/` in the
  `agents` layout.
- D18: containment over the roots the descriptor declares and the declared rooms.
- D23: a second descriptor, and the first to serve several harnesses.
- D106: the union attribute moves with `twins.md`.
- D115: a lane gains `rooms`.
- HELP2's dsh profile root, on LAYOUT2's finding.
- The adoption playbook: the layout's steps, and the uncommitted diff becomes the landed branch.

Each row that builds a piece notes the amendment where it lands.

**What the gates do not cover.** This change is documents only, and nothing is built. Its statements about
today's files were read from them: the manifest, the lock, `harness.ts`, `materialize.ts`, `upstream.ts`, the
service's `DaorisLock` and `RepositoryScanner`, the dsh profile, `.gitattributes`, the lane map and the package's
staging. Its statements about the reference are the parent's measurement on the owner's checkout, which this
design did not read. Three cells of its §1 are observations from its own session, one build of one harness; the
rest are marked *not measured*, for LAYOUT2. `verify` checks the records' shape and budgets, and none of those
words.

*Built by LAYOUT3 (2026-10-01): the agents layout in the CLI, the design's §2, §3 and §5.1–§5.4. `agents` is a second
descriptor in `src/harness.ts` carrying its mirror (`skills` to `.claude/skills` for Claude Code, each skill's
`agents/` folder left out) and the root it moves from (`formerly`). The lock gains `harness`, `target`, `mirrors` and
`rooms`, each only when there is one, so a lock on the older layout is unchanged byte for byte; `layout.ts` answers
where the files are from the lock. Every cell of §5.4 is a `node --test` case, written first and watched failing.
Five choices the design left open, each held by a test: (1) `init` keeps writing `claude-code` and takes
`--harness agents`, since the family and deployment rehearsals' newcomers write `.claude/knowledge/` after `init`
and the service reads `.claude` until LAYOUT4; flipping the default belongs with LAYOUT4/LAYOUT5. (2) Rooms and the
link refusals apply on both descriptors, since both write `AGENTS.md` and a `CLAUDE.md` pointer. (3) A mirror's
`SKILL.md` carries the mirror header in place of the source's provenance line, one instruction rather than two;
any other file is copied as its bytes, hashed as text unless it holds a NUL. (4) A link held as text is a file
whose whole content is one token naming its partner (`AGENTS.md` for `CLAUDE.md`), starting `./` or `../`, or
resolving beside it; a file where a folder must go refuses either way. (5) The repository's own documents left
in an old tier refuse even `--force`, as links and a room with no instructions do: `--force` discards an edit, and
none of those is one. The index names a room by its first heading. `check` reports `AGENTS.md` against codex's
32,768 bytes, LAYOUT2's smallest measured limit, and never fails on it (D54). Not built here: `analyze` and
`status` naming what reaches each harness (LAYOUT7's table and its twin), the service's half (LAYOUT4), the
lanes' rooms (LAYOUT9). The release rehearsal's move phase is written and was not run in the branch.*

*Built by LAYOUT4 (2026-10-01): the service reads the layout, the design's §5.5, and the scanner no longer assumes
`.claude`. `DaorisLock` reads the lock's `harness`, `target` and `mirrors` and resolves its root by the CLI's
`lockLayout`, row for row: the lock's target, else the manifest's while both are on the older layout, else the
descriptor's. `RepositoryLayout` says where the scanner reads: at the lock's root and, for a layout that moved, the
root it moved from, where a skill kept for one agent sits beside the mirrors; with no lock, at the manifest's root
and both `.agents` and `.claude`. The lock's mirrors are never indexed, so a skill is found once, at its source.
Each declared room's `AGENTS.md` is a local knowledge entry named by its folder. `RepositoryLinks` skips a link, a
junction or a link held as text, by the CLI's `heldAsText` cases, and never follows one. Four choices, each held by
a test: (1) the reader's answer where the CLI refuses: a lock whose target leaves the repository, or whose
descriptor it does not know, reads as no lock, everything local; a room the CLI refuses is not read, and the good
rooms beside it still are. Never outside the repository, and never the whole corpus for one repository's file.
(2) Rooms are the manifest's, not the lock's: a room's `AGENTS.md` is the repository's own file, and the lock's
`rooms` record pointers, one import line each. (3) The link rule covers every file the scanner reads, the logs and
the region's file included, and a region file the lock names outside the repository is not read (D18). (4) A room
whose file a tier already yielded is one entry. A repository on the older layout indexes exactly as before: a golden
test was run against the scanner before this change and after it. The CLI still writes `claude-code` from `init`;
the service no longer holds that default back (LAYOUT3's choice 1). Not run in the branch: the family rehearsal's
*each document indexed once* (§5.6), the parent's at merge.*

*Built by LAYOUT7 (2026-10-01), as D124 §2 amends §6.1–§6.3: the layout read from a repository's line, the set-up
quest's composer and `daoris-driver setup <repository> [--plan]`. `LayoutReader` (`LayoutFacts.cs`) reads the line's
commit by `git ls-tree -r -z`, the manifest, the lock and the two instruction files by `cat-file`, and the checkout's
`core.symlinks`, never a working file: the layout by the lock, then the manifest, then `.claude`; a link from its mode,
then a file held as text by the CLI's content cases, with what sits beside read from the line's tree; the rules,
skills and knowledge under both roots; the lock's mirrors; every folder with an `AGENTS.md` of its own outside the
doctrine's folders; the declared rooms lacking one. `SetupBrief` composes the title from `SetupQuests`' words, which
gained a named constant each and `Title(stem, day)`, and the body from the facts in the canon's words: what is asked
and whose it is, what was read at the named commit, the steps, the bounds, the close. The playbook gained the layout's
steps (`init --harness agents`, the `git mv`, the `LINK` lines), *Initialise the knowledge* as its step 6, and a
hand-over that, for a driven set-up, is the committed branch the workspace's rule lands. Three choices the design left
open, each held by a test: (1) the press asks no doctrine command, so *already on the agents layout and clean* is read
from the line (`LayoutFacts.Clean`: the lock and the manifest both on `agents`, the region in `AGENTS.md`, no link or
link held as text where the tool writes, nothing left in `.claude/knowledge/`, every declared room with its
instructions) and what else `check` holds stays that repository's own gate; (2) an adopter whose line is not clean
on the agents layout is asked to move, the move's title, which finishes a move half made; (3) §6.5's per-agent table
and its CLI twin are not built here: the quest's facts need neither, and they are LAYOUT8's screen's. Not run in the
branch: `SetupLineProcessTests` (a mode-120000 `CLAUDE.md` under `core.symlinks=false`, read from the line while the
checkout is on another branch with edits in flight), the parent's at merge; the family rehearsal's set-up phase
(`setup game --plan`, a stub session running the CLI and landing) is the tools lane's and not written.*

*Amended by D124 (WSSETUP5, 2026-10-01), §6.5: the driver registers a repository from what its line declares, after Daoris
moves the line, once as a watch starts, and when the person asks (`daoris-driver register`). D124's note has the rest.*

## D118 — Every view has the same frame: its own list pane and main area, beside the frame's side bar and panel (2026-10-01)

**Decision (FRAME1a).** The owner, 2026-10-01: *"why only session has more layout option we do need to make
everything consitant"*. `docs/2026-10-01-frame-audit.md` reads each view's layout from the code, and found
that only Sessions has a list that collapses, resizes and is remembered. On four views a record opens in a
modal drawer over the side bar and the panel. Only Sessions remembers what was open in it.
`docs/2026-10-01-frame-model-design.md` is the contract. It settles:

1. **The same regions on every view, with two owners.** The list pane and the main area are the view's:
   they change with it, and each view remembers its own. The activity bar, the strip, the status bar, the
   right side bar and the panel are the frame's: they are the same on every view, as DOCK1a made the side
   bar and the panel. **The frame's regions hold only what is the same on every view**, so a view's own
   detail never goes to the side bar.
2. **What each view puts there** is the design's §2 table:
   - Quests' asks and quests, Projects' repositories, Search's hits, Convergence's findings and Settings'
     domains become list panes;
   - the quest, the repository, the entry, the finding and the domain become the main area;
   - Overview and Map have no list, for §4's reasons;
   - Plugins, PLUGUI1's view, is built on the frame.
3. **A record is the main area and a form is a drawer.** A record opens in the main area of the view whose
   list holds it, and every door into a view names the item it opens, through one opener. A form stays a
   drawer, and every overlay opens above a full side bar.
4. **One list pane everywhere.** A header with `＋` and ⋯; a resize within per-view bounds; a person's
   closing that leaves a 56 px strip; four doors that toggle it (the strip, the View menu, Ctrl+B, and a
   press on the current place); ↑, ↓, Home, End and Enter inside it.
   - **A list becomes a strip by room, not below a fixed width.** That happens when the main area would
     fall below its 400 px floor with the side bar as it stands.
   - **A strip the window drew opens the list over the main area.**
5. **Remembered per view:** the list's closing, its width, the chosen item and the list's filters.
   Sessions' and Settings' existing keys are kept. What the frame remembers stays for every view, as now.
6. **The main area lays out by its own width**, never the viewport's, and shows skeleton rows while its
   item loads, never the empty state.
7. **A browser keeps the list and the main area**, and never the side bar or the panel. The monitor's rail
   and a detached window's console take the same `ListPane` and `OutputPanel`.

**Why.** The owner asked for one product rather than a set of screens. The audit showed the frame DOCK1a
put on every view stopped at the frame's own regions. Every view kept its own arrangement inside the
centre, and none of them had Sessions' list. Two things follow from DOCK1a itself:
- **the drawer**, the platform's detail surface since D41, now lies over the regions that hold Ask Daoris
  and the attended session;
- **a view's detail in the side bar** would make the side bar change with the view, which is what DOCK1a
  made it the frame's to prevent.

So a record moves to the main area, and the side bar keeps only what is the same everywhere.

This **amends**, each marked where it is amended:
- **D41 §4** (platform language §4 *Drawer*): a record leaves the drawer once its view has a list;
- **FRAME6** (components plan §3a): a strip the window drew offered no open, and the rail was a strip below
  a fixed 1024 px;
- **DOCK1a** (dock design §4): a browser keeps the view alone;
- **D56 §3**: the rail as Sessions' alone.

D40's landing, D55's session as the organising object, and D47 §4 are unchanged.

**Rejected.**
- **A shared main area with tabs across views** (VS Code's editor groups): a second navigation beside the
  list, and the file model D55 declined for a session.
- **A view's own detail as a side bar view**: the side bar would change with the view.
- **Keeping a record in the drawer**: it is modal over the side bar and the panel.
- **One list state for every view** (VS Code's): each list differs in kind and width, and closing Sessions'
  rail for room is not asking to lose Quests' list. VS Code shares one side bar because it shares one
  editor.
- **One viewport threshold for every list** (1024 px, Sessions' today): it strips Settings' 176 px list
  where it fits, and it keeps Sessions' rail a strip with 540 px free beside it.
- **A list on Overview or on Map**: a second copy of *What needs you*, and Projects twice.
- **Stacking a list above the main area when narrow** (Settings' today): the arrangement D56 rejected for
  the activity bar.
- **Hiding a list when narrow** (the monitor's today): it leaves no way back.

**What the gates do not cover.** This is a design, and nothing is built. The audit was read from the source
at `7a3fb5f`, and its rows marked *to look at* wait for the window (its §5 lists twelve looks). None of the
widths, the room rule's numbers, or the F6 key has been tried on the window: FRAME1b measures F6 first, and
drops it if the engine keeps F6 for itself. The build rows are the design's §6, and each carries its own
proof.

**Built: FRAME1b (2026-10-01), and what it settled that the design left open.**
- **Every door toggles by what the room made of the list.** An open list closes, and the closing is the
  person's. A list laid over the main area goes, and nothing is remembered. A strip opens, whoever drew it:
  beside the main area where there is room, over it where there is none, so a door is never a press that does
  nothing. The rule is one pure function (`listToggled` in `layout.ts`), and the frame tells the application
  what the list is now, since only the frame measures.
- **The side bar counts at its 300 px floor whenever it is open, full included**, and at its 32 px strip
  closed, as the room rule's words say. Sessions' rail is therefore open from a 760 px window with the side
  bar closed and from 1028 px with it open.
- **A press on a door is not a press outside the list.** The strip's toggle and the current place stand
  outside a list laid over, so a press on either closed it and its click opened it again. A door carries
  `data-list-door`, and the list leaves that press to it.
- **The list is named per view in two keys**, `layout.list.<view>` for the toggle's sentence and
  `layout.menu.list.<view>` for the View menu's item, so each view joins by adding its own two.
- **A list laid over sits above a full side bar**, as §3d has every overlay do.
- **F6 was not measured.** A branch cannot start the window, so F6 and Shift+F6 were built alone in one
  commit, and the look on the window keeps that commit or reverts it. The focus lands on a region's chosen
  item, then its list's first row, then its first control, and on the region itself where it has none.

**Built: FRAME1c (2026-10-01), and what it settled that the design left open.**
- **A view hands the frame what it builds; Sessions' main area stays the frame's.** `ViewLayout` is
  `{ list?: ListSpec; main }`, and the frame draws every view's list through one path
  (`ViewListPane`). Sessions' rail is a `ListSpec` built in `WorkFrame`. Its main area is drawn there too,
  since the attended session, its composers and its scroll are the frame's to hold. A `ListSpec` carries
  its chosen item, and a change of it closes a list laid over the main area, as the `＋` does.
- **One split point: 56rem of the main area's own width** (`@4xl/main`), for Overview's two cards, the
  repositories' cards and the map's detail. That is where a 1024 px window's main area split before. The
  container is named `main`, so a card's own container (USE1's) never answers for it.
- **The opener plans as a value and `App` applies it** (`opener.ts`), as plan and apply are separate
  elsewhere.
  - An ask is named `ask:<id>` among Quests' items, since its list holds asks and quests.
  - Every door records the chosen item of a view with a list, Quests' and Repositories' included. They are
    read once those views have their main areas (FRAME1d, FRAME1e). Until then a quest's record still opens
    in its drawer, as the door asks.
  - A view with no list keeps no chosen item.
  - A door that names a Settings domain and no part opens the domain at its top.
- **`StarterDoor.item` exists, and Ask Daoris's go does not carry it yet.** The go's wire gains `item` in
  FRAME1i.
- **Every overlay lies above a full side bar because the frame is its own stacking context** (`isolate`),
  not by renumbering each overlay. A drawer, the palette and Quick Ask, drawn at the page's root at any
  `z`, lie above everything inside the frame.
- **Sessions draws no *gone*.** A session chosen and absent once the list has loaded stays *Nothing
  attended*: a session just started is attended before the list catches up with it (CONV3), and *gone*
  would show for a running chat. `ViewMain`'s *gone* waits for FRAME1d–g. *Nothing attended* now offers the
  list's `＋`, as §2 says every empty main area does.
- **Off Sessions, with nothing attended, the side bar says so and offers *Open Sessions***, where it named
  the session list.
- **A browser's list is wired and has no first user yet.** It has its toggle on the strip, Ctrl+B and the
  press on its place, and counts no side bar for room. No view has a list in a browser until FRAME1d, so
  nothing on a browser's screen changes with this row.
- **What the gates do not cover.** vitest holds each split by its class, since jsdom lays nothing out. The
  platform spec narrows the main area at a 1280 px window and sees each split stack, and the parent runs it.
  The drawer above a full side bar is held by the frame's `isolate` and the drawer drawn outside the frame.
  The look on the window settles both: Overview, Repositories and Map at 1280 px with the side bar open and
  closed and at 680 px, and a drawer at 680 px with the side bar full.

**Built: FRAME1d (2026-10-01), and what it settled that the design left open.**
- **Quests hands the frame a value, as Plugins does** (`useQuestsView`), held by the application on every view,
  its errors said only while it is in front. The asks' half is a hook of its own (`useAsksPart`, `asks/`), so the
  asks' rows and an ask's page are drawn where the frame decides. Everything under them is a molecule: `QuestList`,
  `QuestPage`, `QuestComposer`, `AskRow` and `AskPage`, and the values in `quests/records.ts`.
- **A record the list leaves out is still a page.** A quest is found among every quest, closed ones and other
  receivers' included, and an ask among every ask. So a closed quest a door names opens, and an act leaves the page
  on the quest as it now stands, where the drawer closed on every act. Nothing chosen says how to choose and offers
  the `＋`'s two kinds; a record no longer here is *gone*, worded for a quest or an ask.
- **The page shows the freshest record**: the list's copy, or the door's last answer while the list is behind,
  and the answer on a tie, since a dismissal moves no time. It is the asks' old rule, now for both. A quest just
  published opens on its page, as an ask has opened on its answer since INT4c.
- **The filters are ticked items in the list's ⋯** (`ListMore` gained a choice of one value and a toggle), kept as
  `{ to, closed }`. A receiver filter is said at the list's head, since the ⋯ that set it is a press away and a list
  that silently holds fewer quests reads as a family that owes less. A receiver kept from before stays choosable, so
  the filter that names it can be undone.
- **The header's line is the state's hint**, which left the record's *State* row. *Decline…* and *Delete…* ask
  under the header, each with *Never mind*; a decline had none, and its reason has a name now.
- **The opener plans no drawer.** Its `quest` and `ask` fields go: the chosen item is the whole of what a door does.
- **The list is a landmark named for its view**, open or a strip, so a reader and a test find it by its name.
  `ListGroup` and `ListRowDoor` are every list's; the Plugins view keeps its own copies until a row of its own takes
  them. `PageHead` and `PageSection` move from the plugin's page to `work/ViewMain`.
- **Quests' strip holds its controls alone**, as §2 says. Quests is a browser's first list, which ends FRAME1c's
  *wired with no first user*.
- **`quests.title` and `quests.description` retire**: the list is named by the view's own name, and the description
  became the main area's sentence with nothing chosen.
- **What the gates do not cover.** vitest holds the list, the page and the memory over a stubbed service and a
  mocked bridge, on jsdom, which lays nothing out. `test:web`'s quest-lifecycle spec was moved to the page and
  typechecked, and **not run** in the branch: the parent runs it. No look at the window was taken: the list beside
  the page at 1280 px with Ask Daoris open, the list laid over at 680 px, both themes and both languages, are the
  parent's to see. The names' budgets are estimates (D116 §4); the new keys added no finding.

**Built: FRAME1e (2026-10-01), and what it settled that the design left open.**
- **Repositories hands the frame a value, as Quests does** (`useProjectsView`), held by the application on every
  view, its errors said only while it is in front. It holds no query the application does not already hold (the
  registry, the index, the driver, and on a shell the roster, the lines and the sweep), so holding it everywhere asks
  nothing more. Below it are molecules: `ProjectList` with `RepositoryMarks`, and `ProjectPage` with
  `ProjectsMainNotice`.
- **A row is the repository and its one line**: an adopter's summary, *no domain declared yet* where it declared
  nothing (D34), or for one not adopted what the index reads of it. Its standing on this machine is in the session
  list's words (`RepositoryGroup`'s): *held* outranks *drives here*, and *not on this machine* where a shell answers
  and the registration names no checkout. The adopted dot and its word retire: the group's name says it.
- **The page is what the card said, in one column.** The cards went, and with them the split by the main area's
  width that FRAME1c gave them: the list holds the repositories now. The header's line is the summary, and the header
  has no id line, since a repository's name is its id (`PageHead`'s `id` became optional). Its acts are *Open code
  map*, the map's own name for the act, and *Manage*, an adopter's on a shell as before. *Manage* left the driving
  row, which stands alone under *This machine*; one not adopted has *Adoption steps* in place of a declaration.
- **The `＋` keeps the act's name, *Add repository***, where the design's prose says *Add a repository*: one act, one
  name (NAME1b), which the empty state and the Workspace menu's *Add repository…* already say. Its one kind makes the
  press the act. The ⋯ holds *Import a folder…*, as the Workspace menu does. Both are a shell's (D48 §7), so a
  browser's list makes nothing, and its strip holds its controls alone.
- **The code map is a door with a part** (`open('map', null, { code })`): the Map has no list (§4), so the repository
  is no chosen item. The opener plans `code`, and the Map is drawn anew on it; any other way onto the Map opens the
  workspace, as it always has.
- **A repository added is chosen, and one retired goes back to choosing**, as an installed plugin is chosen and a
  removed one let go (D119 §3.1). A chosen repository the registry no longer answers is *gone*: retired, or in a
  workspace the window is not showing.
- **`projects.title`, `projects.description`, `projects.adoptedDot` and `projects.outside.title` retire**: the list is
  named by the view's own name, the description became the main area's sentence with nothing chosen, and the
  groups' names carry their counts.
- **What the gates do not cover.** vitest holds the list, the page, the memory, the drawers' doors and the code map's
  door over a stubbed service and a mocked bridge, on jsdom, which lays nothing out. `test:web`'s registry pages were
  moved to the list and the page, a reload's memory and the code map's door added, and the Repositories half of the
  main-area split test removed with the cards; they were typechecked and **not run** in the branch: the parent runs
  them. No look at the window was taken: Repositories at 1280 and 680 px with a repository chosen, both themes and
  both languages, are the parent's to see. The list's toggles are over the button budget (24 characters for 20), as
  Settings' are; the budgets are a report (D116 §3).

**Built: FRAME1g (2026-10-01), and what it settled that the design left open.**
- **Settings hands the frame what it builds** (`useSettingsLayout`): `DomainList` as its list pane and the
  domain chosen as its main area. `SettingsView` draws Settings alone, in a browser's frame of its own, which
  every domain's suite renders, as a surface drawn alone holds its own running action. It has no `＋` and no
  strip items (§4).
- **The main area's header names the domain**, so the domain is named where the list is a strip. The page's
  own header went with the page: the list's header says *Settings*. The shell's page sentence is dropped, and
  a browser's became a note beneath its four domains, saying where the absence is (D47 §4).
- **A domain chosen opens at its top.** The main area is drawn anew for each domain, where one scroll box
  kept the scroll the last domain was left at. A part a door names is brought into view as before.
- **Its doors are the frame's four**, named *the settings list* and *Settings list* (设置列表), in the shape of
  the glossary's *session list*. The domain is still kept under `daoris.settings`.
- **The opener is FRAME1c's**: `open('settings', domain, { anchor })`. No `domain#part` string was added,
  since the opener already carries a part beside the item.
- **A machine domain's first load is skeleton rows in its cards' place**: while it has no answer and one is on
  its way. A failed question ends in its toast rather than loading forever, and an older shell's answer still
  draws no card. Permissions holds both its cards' places.
- **What the gates do not cover.** jsdom lays nothing out: the list's mode at 600, 680, 900 and 1024 px is held
  by the frame's room rule and by the mode each test reads, not by a measured width. The platform spec's tier
  door was edited to the domain's heading and not run. The look on the window settles the rest: Settings at
  1280 and 680 px, English and 中文, both themes, and the first open of Agents, Permissions and Plugins.

**Built: FRAME1f (2026-10-01), and what it settled that the design left open.**
- **Search and Convergence each hand the frame a value** (`useSearchView`, `useConvergenceView`), held by the
  application on every view as Quests is, but **asking the service nothing until in front**: a comparison over a
  real index takes seconds, and nobody on another view asked for one. Below them are molecules under `knowledge/`:
  `HitList`, `EntryPage` with `EntryText` and `EntryPills`, `FindingList`, `FindingPage`, and the values in
  `knowledge/records.ts`.
- **The reader retired, and the wide drawer with it.** Search and Convergence were its only doors, so `Reader` went,
  and so did `Drawer`'s `wide`, which only it passed. An entry reads at the main area's own width, with no measure.
- **Each list's one filter sits where it changes what the list holds**, not in a ⋯ as Quests' do: *local only* under
  the box, the similarity at the list's head with its tier's note, both reached for on every look. Neither view makes
  anything, so neither list has a `＋` or a ⋯, and each strip holds its controls alone. Each head stays in reach as the
  rows scroll under it. ↓ goes from the box into the hits, through the list's own keys, and Escape clears the box.
- **A finding is named by its entries** (`findingId`), since the service names none: an entry's id is its place, so
  the same entries found at another similarity are the same finding, still chosen. One the answer no longer holds is
  *gone* at the similarity asked. An entry is *gone* when the service answers that it holds nothing by that id: a
  read's error carries its status now (`notFound`), so a 404 is told from a read that failed.
- **A finding's page** is titled by its entries' titles, each once, with how alike beside it, the repositories as its
  id line and the kind of likeness as its line. The service's sentence comes first, then each entry whole in a section
  of its own, where one entry still on its way, gone or failed is said in its place and the others still read.
- **What is kept**: each list's chosen item; *local only* only when everything is chosen, the default kept as nothing;
  the similarity once it has held still, its start kept as nothing. What was typed lasts while Daoris is open and is
  never stored.
- **Loading (audit SR11) is built to the platform language's §4 rule**, for the look to confirm: a first answer is
  skeleton rows with its words on the count's line, and a newer search or a moved similarity holds the last rows at
  reduced opacity (`holding`, TanStack's previous data) instead of blanking the list at each answer.
- **The names.** The lists are named for what they hold, *the result list* (结果列表) and *the finding list* (发现列表),
  as *the quest list* is. The count says *findings* (发现), the map's word, where it said *groups*. `search.title`,
  `search.description`, `convergence.title` and `convergence.description` retire: the list is named by the view's own
  name, and each description became its main area's sentence with nothing chosen. `search.noneHeadline` and its body
  became `search.nothing*`, since `search.none.*` now means nothing chosen, as on every view. The box's placeholder
  was cut to fit a list's box.
- **What the gates do not cover.** vitest holds the lists, the pages, the memory and the doors over a stubbed service,
  on jsdom, which lays nothing out. `test:web` gained a Search test and a Convergence test, typechecked and **not run**
  in the branch; the Convergence one asks the host's answer first, since the example family's own entries may share
  nothing at 0.75. No look at the window was taken: Search and Convergence at 1280 and 680 px with an entry and a
  finding chosen, a search typed at 1280 px for SR11, both themes and both languages, are the parent's to see. The
  finding list's toggles are over the button budget (21 characters for 20); the budgets are a report (D116 §3).

**Built: FRAME1h (2026-10-01), and what it settled that the design left open.**
- **The monitor's rail is a list of its own, `monitor`**, in `LIST_BOUNDS` with Sessions' bounds, since it is the
  session list live. Its closing and its width are kept as `daoris.list.monitor.*`, so closing it closes no rail in
  the main window. `ViewFrame`, the browser's frame, draws it, since nothing stands beside the tiles: the rail gives
  way to its strip only for the tiles' 400 px floor, open from a 680 px window. Its strip holds each running
  session's mark, as Sessions' does. Its names and doors are the session list's. It has no `＋` and no ⋯, since the
  window makes nothing (D56's one owner).
- **A press on a session scrolls to its tile**, from the rail or its strip, and lets a rail laid over the tiles go,
  as a choice in any list does. No item is chosen or kept: the tiles are the running sessions.
- **A tile is never wider than the tiles' area** (`minmax(min(26rem, 100%), 1fr)`). Beside the rail at the floor, a
  26 rem tile ran past it.
- **A detached window's console is `OutputPanel`**, kept as one memory for every detached window
  (`daoris.detached.panelHeight` and `.panelClosed`, `useDetachedPanel` in `closings.ts`). It is kept apart from the
  main window's panel, since the windows share one page's storage. It starts at the main window's 200 px. Picking a
  stream opens a hidden console, as the main window's does.
- **`OutputPanel` draws no views menu where it holds one view and has nowhere to move it.** In a detached window the
  ⋯ listed the console alone. It is absent, never a door that does nothing (§3a). The main window always passes its
  move, so it keeps the menu.
- **Each window answers the key of the one region it has**: Ctrl+B on the monitor, Ctrl+J in a detached window,
  wherever focus is, as the main window's keys are. Neither window answers F6, since each has its native frame and
  one region that moves.
- **The monitor's title is `text-view`**, the one heading step a view has, as every page's header. `tokens.test.ts`
  now reads a `text-` class whole, and fails on one for which `tokens.css` declares no step, colour or layout. It
  was seen red first, on `text-h3` alone. It also holds the steps `cn.ts` hands the class merge equal to the ones
  `tokens.css` declares.
- **What the gates do not cover.** jsdom lays nothing out. In the tests, the rail's mode at 600 and 1400 px is the
  room rule's answer for the window less 48 px, where the real window measures its frame and has no activity bar. A
  short window does not cap the console's height, as it does not cap the main window's. No look at the windows was
  taken. `shot --window monitor` at 900 and 1400 px, and `shot --window session:<id>` with a long console and
  narrow, in both themes, are the parent's to see.

## D116 — A name is a UI element, designed in each language; the glossary is the authority, and a check holds it (2026-10-01)

**Decision (NAME1a, the owner's round).** The owner: names in Settings and every other display must be named
properly in both English and Chinese, *"since this is not just translation this is part of the ui element"*. Read
where each of the 1,699 keys renders, 772 are names; many Chinese ones are English names translated (Settings'
「Daoris 自身的 AI」, 「智能体与账户」), sixteen concepts wear two or more words across screens (项目 and 仓库 for one
view's rows, 同归 and 汇聚 for one view, 加入, 采用 and 接入 for *adopt*, 同步 for both *sync* and *bring up to
date*), and English buttons split 133 lower-case to 50 capitalised. `docs/2026-10-01-naming-design.md` is the
contract. So:

### 1. What a name is

- **A name is part of its element, designed in each language for that element**, never a translation of the other
  language's name and never a sentence. It is decided by the concept it names (the glossary), the kind of element
  (its form) and the room the frame gives it (its budget). Chrome is named; content is not, as `translation-parity`
  already says, and a key stays structural: renaming a name never renames its key.
- **Fourteen kinds**, each with its rules in each language: `nav`, `title`, `tab`, `section`, `field`, `choice`,
  `button`, `status`, `menu`, `command`, `headline`, `placeholder`, `toast`, `sentence`. An accessible name or a
  tooltip takes the kind of the control it names. `command`, a palette row, was found building the check: a name, a
  dash and a gloss in a 34rem dialog, which a strip menu's room reported row by row.
- **English is sentence case for every name but a status word and a placeholder**, which are lower case. Buttons were
  the drift; every other kind had settled, and Windows writes sentence case.
- **Chinese names are nouns and verb-object phrases chosen as names**: a place is one noun of two to four
  characters with no 的 and no 与; a section names what it holds, never an English question carried over; a button
  is 动宾 with no pronoun, its confirming press 确认 and the verb, its back-out 取消; a status word is 已 and the verb
  for an outcome, the verb and 中 for a state in progress, a bare word for a condition. Latin stays for products,
  what a person types, and acronyms with no settled Chinese name, never for a concept the glossary names.
- **A door names its destination by the destination's own name**, and a heading and the press under it name one
  act.
- **A budget per kind and language**, from the room the frame gives the kind at 888px wide with the side bar at its
  300px floor, in English characters and Chinese units (a Chinese character 1, a Latin one ½); design §4 has the
  rooms and how the window verifies each.

### 2. The glossary is the authority

`src/Daoris.Web/src/locales/glossary.json`, beside the language folders and merged into neither: one term per
concept, each with its English and Chinese names, a one-line definition, the words it must not be called and how its
English is recognised; the kinds with their budgets and **the keys of each kind**, by key or prefix, the most
specific winning, a key named by no kind a sentence; and the doors with their destinations. A code word never shown on
the window (`harness`, `profile`, `strike`, `tick`) is a term that points at the one that is.

### 3. The check, in report mode

`scripts/names-check.mjs` (`npm --prefix src/Daoris.Web run names:check`) reports glossary conformance, budgets,
form and doors per key, and exits 0; it exits 2 only when the glossary is malformed, since a glossary that cannot be
read has stopped checking anything. A label is held to the term's name; with `--all`, a sentence, a tooltip or a toast
is held only to the words a term must not be called, since a sentence may say a thing its own way. **NAME1b turns on
what is a fact**: conformance, form and doors gate through `--strict` in the web's build once the renames land. **The budgets stay a report** (D54: a fact gates, a judgement
reports): a character count estimates a width, and the window is where a width is a fact. This reads the NAME1 row's
*"so drift fails a gate"* as the facts' half, and says so for the owner to overrule.

### 4. What NAME1a does not do

No catalogue value or key changes and no component is touched; the proposals are the audit
(`docs/2026-10-01-naming-audit.md`), and the renames are NAME1b's, after the owner reads them.

### 5. Rejected

- **Translating the English names more carefully.** A careful translation of a possessive is still a possessive.
- **A kind declared by the key's name** (`….title`, `….button`). Keys are structural, many end in a word that is not
  their kind, and renaming them churns every call site to say what one line of a map says.
- **Measuring widths by rendering in the check.** A browser in the check for an estimate the window confirms anyway.
- **Failing the build before the renames.** A gate red on its first day is switched off rather than obeyed.
- **Title Case**, and **lower case for every button**: the first is neither Windows' nor the page's; the second
  keeps a button's name in another case from the heading above it and the menu item that does the same act.
- **One Chinese word for chat and conversation.** A chat is a kind of session; the conversation is the record
  every structured session keeps. 聊天 and 对话.

### What the gates do not cover

The glossary's shape is held by `src/locales/glossary.test.ts`, and the check's rules, the kind map's resolution,
the measure and report mode by `src/locales/names.test.ts`, both in the web's vitest loop. That the audit's proposals
obey the rules they propose (none left on the facts' half once applied, and every placeholder where it was) was
checked by a scratch run over the catalogues in memory, not by a gate: NAME1b's `--strict` is that gate. The check
does not hold placeholders, which the parity gate does, and cannot see a sentence the driver writes (English
`{{why}}`); the audit lists those it read by hand. That the budgets match the rooms is a claim about the window,
derived from the tokens and the frame's constants in design §4 and **not measured here**: nothing looked at the
window in this branch.

### NAME1b: the owner's calls, and what applying them settled (2026-10-01)

**The owner approved the audit on 2026-10-01**, with the two calls it left to them: the view that lists repositories
is named for them, *Projects* → *Repositories* (项目 → 仓库), on the activity bar, as the view's title and on the
Overview's tile; and an ask is 需求, not 请求, wherever Chinese names one. NAME1b applied every row of the audit and
its one plural form, the sentences its §5 lists (leaving the eight it found to be another thing's word), the
driver's pass-through sentences in 中文, the host's own sentences a person reads (a refusal shown verbatim, a
start's reason, a plugin's problem, a proposal's plan) and Ask Daoris's room, which it names places back from. The
check went from 519 findings (glossary 84, form 288, door 16, budget 131) to 107, all budgets; under `--all`, from
649 to 115, the eight sentences and the budgets.

- **`--strict` gates the web's build**, beside the parity check and before the type check and the bundle, on the
  facts' half; the budgets report (D54), as §3 above says. Its exit is a function, `verdict`, held rule by rule.
- **A name said inside a sentence takes a fragment of its own**, never a name lower-cased in code: the account's
  summary line (`harness.settings.summary.default`), a proposal's and a toast's scope (`settings.rules.where.*`), an
  update's changed field, which now leads its line (*Servers: was → now*). Rejected: lower-casing a sentence-case
  name at the call site, which is one language's rule written into a component.
- **One act, one name, even where two presses do it**: the parked band's *Finish* and the composer's are one name
  now, so the tests that told them apart by name ask the composer's form instead.
- **A door named for a part opens at the part**: the sync menu's *Wiring…* opens the Workspace domain at Wiring, as
  *Wire to a remote…* does (UX5 U72); the Machine log's card, alone in its domain, carries no title (U57).
- **An accessible name moves with the name it begins with**: the map's *Connections* menu kept *Lines:* in its
  accessible name and tip, which would have broken label-in-name.
- **The check sets aside every name the English names**, not only the term's own: *like a git remote* says 远程仓库,
  whose 远程 the workspace's remote must not be called; and *hold* is recognised in *held repositories* and *is
  held*. A short form is never set aside.
- **Kept on purpose**: the CLI's verbs and flags (`daoris agent login`, `--profile`), the MCP tools' descriptions a
  session's agent reads (not the window), the headless host's console text and the driver loop's log lines say the
  code's words; a browser's, Windows' and dsh's own *profile* and git's credential *helper* are theirs. The palette
  keeps *projects*, 项目, *get started* and 请求 as words that still find the renamed places.

**What the gates do not cover.** `--strict` holds the facts over every label, and the parity gate the keys and
placeholders. The Playwright specs were edited to the new names and **not run** in the branch (the parent runs
`test:web`); what they pin was read against Playwright's matching (case-insensitive substrings unless `exact`). The
107 budgets are the window's to judge, and nothing looked at the window in this branch either.

*Noted by NAME2 (2026-10-02): what the build settled that the design left open.*
- **An accepted budget judgement is kept in the glossary** (`accepted`: the key, the name accepted per language, and
  why). The design said the window decides each judgement but not where a decision lives, so NAME1b's 107 came back
  on every run. The check stops reporting a name while it is the one accepted. A rename lapses the acceptance, and a
  name that fits again is reported so the acceptance can go. Both are reports (D54). Rejected: a fifteenth kind for a
  select's option, the commonest acceptance, which would amend the kinds rather than fill what was left open; and
  recording acceptances in the audit, which the check cannot read.
- **A plugin's switch is 启用 and 停用**, and its off state 已停用, held by the terms `turn on` and `turn off`.
  关闭 is close's name, so the press read as close and an off plugin wore a closed quest's 已关闭. The English pair
  stays *Turn on* and *Turn off*, the driver's room says *switch one on or off*, and the CLI's verbs are typed words.
- **A lane is 泳道** (the term `lane`), which DEV4 put on the window before the glossary had a term.
- **What the gates do not cover.** The acceptances' rooms are read from the code (classes, a menu's width, a row
  that wraps), not measured, and the seven judgements left on the report are the window's. Nothing looked at the
  window in this branch.

## D119 — Plugins get a view of their own: a list by what they need, a page per plugin, and Settings keeps where they are looked for (2026-10-01)

**Decision (PLUGUI1a).** The owner, 2026-10-01: *"because plugin will be a big part of daoris so it need to have a
panel/screen itself, and develop proper ui/ux"*. Plugins were one domain of Settings (D64, D101, D103): a row each,
a trial's report and an update's plan under the row while the domain was shown, and no page. Read from the code,
a plugin's words reached a console ring no screen reads, the machine log held no plugin event, nobody kept a trial,
and nothing said whether a plugin answers. `docs/2026-10-01-plugins-screen-design.md` is the contract, extending the
plugin design and built on D118's frame. It settles:

1. **Plugins is a view of the activity bar**, shell-only (D47 §4), after Search. Its list pane holds the installed
   plugins in groups by what they need from the person: *Waiting on you* (failing or refused while on), *On*,
   *Off*, then *Daoris's own plugins*, the offers not installed. Its main area holds a plugin's page, and an
   offer's. This refines D118's §7 order (running, off, refused): the person acts on a plugin's state, and a
   plugin may be of every kind at once.
2. **Five states, one of them per plugin.** `running`, `ready` (on, with nothing to keep running), `failing`,
   `refused` and `off`.
   - **The loop's own record decides a state**: `PluginHealth`, fed by the hook set, the landing and the hand-off
     in the process that runs the loop. A terminal reads the machine log's last word, and says so. One table of
     cases holds both.
   - **Failing and refused wear the waiting hue**, since both wait on the person's act, and the bar's Plugins
     place counts them.
   - **Running wears no live mark**, since a hook process is up between calls. This retires the done-green
     *running* pill, against the platform language's §3.
3. **A plugin's page** has a header with its switch, *Try*, *Update…* and *Remove…*, which now asks once. Then
   its health line, then Points, Agents, Servers, Activity, Tests, Data folder and Source, each with its loading,
   empty and error states.
   - A server's environment is shown by name, never by value.
   - A refused plugin's manifest is shown as written and marked *not taken*.
   - *Make a plugin* and *Install from a folder* are drawers, since they are forms.
   - *Ask Daoris for a plugin* opens Ask Daoris on a whole first message.
4. **The side bar and the panel gain nothing from this view**, as D118 §3c holds. A plugin's live words stay in
   its Activity, since the panel's console follows the attended session.
5. **What a plugin did is kept without its words.** Seven `plugin.*` events join D94's catalogue: started,
   stopped, called, failed, served, tried and tested. Each holds names, counts, flags and times, and never a
   hold's reason, a message, an address, a command line, an environment value or what the plugin wrote to
   stderr.
   - A plugin's words stay in the console's ring for this run, and the page says so.
   - The last trial and the last test run of an installed plugin are kept in `<home>/plugins/.checks/<id>.json`,
     written by both doors and deleted by a removal or an update.
   - A plugin's own tests run as `node --test` in a copy under `.trials/`, with its temporary folders inside the
     run, bounded at five minutes.
6. **The routes stay `DAORIS.DRIVER`'s** (MOD5), since the loop owns a plugin's process and the catalogue is the
   driver's.
   - `PLUGINS` gains servers, the hook, what the process listens on, health, and whether an update waits.
   - The new routes are `PLUGIN`, `PLUGIN_ACTIVITY`, `PLUGIN_READ`, `PLUGIN_ADD`, `PLUGIN_TEST` and
     `PLUGIN_OPEN_FOLDER`.
   - Each reader is the driver library's, shared with its terminal twin: `daoris-driver plugins show`,
     `activity` and `test`.
   - The CLI's `daoris plugin` keeps the catalogue's edits. Every act on the view has its twin named in the
     design's §4.4 (D50).
7. **Every control on the view has its Ask Daoris answer** (D110). *Install from a folder* is the `plugin` kind's
   `add`. *Read*, *Run tests*, *Open folder* and the Ask Daoris openers are exempt, each for its reason. No door
   is owed. A go names a plugin (`open('plugins', id)`, through FRAME1i's `item`), and a go to Settings →
   `plugins` is refused naming the view. `HelpCoverageTests` reads the view as a screen beside Settings' domains.
8. **Settings keeps only what is a setting.** Nothing about a plugin is set there. The Plugins domain retires, and
   the plugins folder, a fact of the home, becomes a read-only row under the Daoris home in Settings → Driver, with
   a door to the view.
   - Every other control moves to the view.
   - Every old anchor points at it: the Daoris menu, Ask Daoris's places (twins), `where.ts`, the room's doors, the
     service's hint and the CLI's kit refusal.
   - Agents' *declared by plugin* chip and a landing rule's named plugin become doors to its page.
9. **Names** follow D116, in the names NAME1b gives the catalogues, each within its kind's budget.
   - The glossary gains four terms: *try* 试运行, *Daoris's own plugins* 「Daoris 自带的插件」 (`offer` its code
     word), *held back* 拦下 (a plugin's hold, never the person's 暂停), and *data folder* 数据文件夹.
   - The doors `command.go.plugins` and `menu.plugins` both open `nav.plugins`.

The build is PLUGUI1b–h, the design's §6. The view comes first, on today's answers, once FRAME1c has landed. The
host's events and readers can run beside it. Settings' half follows the view, and the page is made whole on the
host's answers.

**Why.** The owner asked for a screen, and D118 made one frame every view is built on. A plugin runs as the person
and can hold every quest when it fails closed (D64 §4), so whether it answers is the fact a person most needs, and
nothing said it. What a plugin did had to be kept to be shown. D94 already says what a log may hold, so the events
are the calls without the words.

**This amends:**
- **D64 §6**: the screen is the Plugins view, not the Machine view's card or Settings → Plugins.
- **D101**: the kit's screen half moves to the view, and a trial's report is kept.
- **D103**: *Install* is on an offer's row and page.
- **D118 §7**: the groups' order.
- **D94 §4**: the plugin events, once PLUGUI1d builds them.

**D64 §7 stands**: no plugin adds a view, and none of a plugin's code runs in the page.

**Rejected.**
- **Keeping plugins a Settings domain with a bigger row each.** A domain has no list, no main area and no memory,
  and a record under a row lasts only while the domain is shown (frame audit PL4, PL9).
- **Grouping by kind** (hooks, agents, servers). One plugin may be all three, and the person acts on state.
- **A plugin's words in the panel's console, or its page in the side bar.** Either would make a frame region change
  with the view (D118 §3c).
- **A module of its own, as `BrowserModule` is.** That module stands apart because its files are not the driver's,
  and a plugin's are.
- **A plugin's words in the machine log.** D94 §5 holds, and a second copy of them would have none of the ring's
  bounds.
- **Health from the log alone.** A failed log write is dropped by design. The process that runs the loop knows
  exactly.
- **Aliases for the CLI's verbs under `daoris-driver plugins`.** They would be a second twin of `plugins.ts`.
- **A toggle atom for the switch.** It would be a new control for one place, where a button named for its act reads
  the same in both languages.
- **The raw `plugin.json` on the page.** An environment value may be a key.
- **Running a plugin's tests in place.** An update replaces the install folder whole, and a source checkout is
  another repository's tree.
- **A history of trials.** Activity counts them.
- **A search in the list.** No machine holds a screen of plugins yet.
- **A failing plugin in *What needs you*.** The bar's count says it once, and each quest it holds carries its reason.
- **A marketplace.** D24, D57 and D64 §7 stand.

**What the gates do not cover.** This is a design, and nothing is built. No width, threshold or budget has been
measured on the window: the names' counts are D116's estimates. The health states have not met a real failing
plugin, and the tests' runner has not met a real plugins repository or the PATH an install hands the application
(USE1g). No landing plugin has pushed to a real platform, so Activity's pushes have not been seen. Each build row
names its own proof, and the design's §9 says what only the window and a real plugin can prove.

**Built: PLUGUI1b (2026-10-01), and what it settled that the design left open.**
- **The view hands the frame a value, so it is a hook** (`usePluginsView`, `plugins/PluginsView.tsx`). Its list and its
  page are drawn where the frame decides, so no one component could hold both. The application holds it on every view.
  It asks the driver for nothing until the view is in front (`usePlugins({ enabled })`).
  - So a trial's report, an update's plan and *Remove…*'s ask last as long as the application. A trial stays on its
    plugin's page across a change of view, until Daoris closes. PLUGUI1g keeps it on the machine.
- **`PluginList`, `PluginPage` and `OfferPage` hold no hook**, so by the components method they are molecules, with a
  story for every state §6 names. `PluginsView` is the view's one organism, named in the presentational check, whose glob
  now reads `plugins/`.
- **Today's states are four**: running, on, refused and off. `ready` and `failing` wait for PLUGUI1f, so a sound
  plugin whose process is not up says no word. A refused plugin that is off is off.
- **Gone is read two ways, both by code.** A chosen plugin is gone when the answered catalogue lacks it. An act refused
  with `PLUGIN_UNKNOWN` asks for the catalogue again and says no toast. A removal by the page's own press goes back to
  *Choose a plugin*, not to *gone*.
- **The list pane gained two things every view may use** (`work/ListPane.tsx`). Where the `＋` makes two kinds, its
  empty state offers each by name, the primary first. And `ListMore` is a list's ⋯.
- **The `＋` is named *Add a plugin*.** With one kind left (no Ask Daoris, or a shell with no kit), the `＋` is that act.
  The groups' counts are in their keys, so the offers' group has `plugin.group.offers` beside `plugin.offers.title`.
- **The glossary has a fifth term, `test` (测试).** Under `--all`, the kit's two sentences that name a plugin's tests
  and its trial together read as a trial called 测试. A term for the tests sets that name aside, as NAME1b's check does
  for any term the English names. `try` does not match *Try again*, which is a retry (重试).
- **The page imports the kit and the update's plan from `settings/`** (`PluginKit.tsx`, `PluginUpdate.tsx`), which
  PLUGUI1c moves to `plugins/`. Two things wait on that move:
  - *Update now* wears the default variant, not the primary that §3.2 gives it.
  - The kit drawer shows the card's own *Make a plugin* heading under the drawer's title.
- **Places are unchanged.** `places.ts` is the driver's `HelpPlaces`' twin, and the twins move `plugins` to the views
  together in PLUGUI1c, so a go to the Plugins view waits for that row. `where.ts` names the view.
- **The page's foot names only the terminal verbs that exist**: `daoris plugin enable|disable|update|remove` and
  `daoris-driver plugins try`. `show`, `activity` and `test` arrive with PLUGUI1d and PLUGUI1g.
- **What the gates do not cover.** vitest holds the view over a mocked bridge and the frame's list over jsdom, which
  lays nothing out. No look at the window was taken in this branch, so every width, the strip at 680 px and the laid-over
  list are the parent's to see. The names' budgets are estimates (D116 §4). The disclosure spec's two new lines were
  edited and not run: the parent runs `test:web`.
*As built (PLUGUI1d, 2026-10-01): the host's half of §2 and §4. The machine log's `plugin.*` events are written by
`PluginLog` (D94's note says what each holds). `PluginHealth` is the loop's record, the shell's `DriverLoop.Health`,
handed with the shell's log to its hook set and to every landing and hand-off the routes build. Its state is decided by
one set of rules for the record and for the log's reading (`FromLog`), held by one table (`PluginHealthTests`). Four
readings the design left open are settled there:*
- *A plugin that speaks at a loop point and has no process up, with no failure as its last word, is `ready`: before the
  loop's first look, after the loop ended, or after Daoris stopped. The terminal's line says no process of it is up.*
- *Switching a plugin off, removing it or updating it starts its record afresh; a change to its manifest, or its process
  started again, does not.*
- *The record is its process's own, so the log's reading starts it afresh where the process that ran the plugin's loop
  starts or stops (`app.started`, `app.stopped`) and where another process's loop starts it.*
- *A landing's or a hand-off's failure is the plugin's word, and makes it `failing` until its next good answer, at any
  point.*

*`PluginPage.Read` and `PluginActivity.Read` are the readers the page's routes are to call, and
`daoris-driver plugins show <id> [--json]` and `plugins activity <id> [--since] [--json]` print them. A page answers a
server's environment by name only. Activity's agent sessions are `session.started` lines whose adapter is a declared
agent, and its pushes are the landing record's, dated by when the branch landed, since the record keeps no push time.
Not yet built: a conversation's `plugin.served` and the screen's `plugin.tried`, which wait for the shell to hand its log
to the chat runner and `PLUGIN_TRY`. What only a real plugin proves is unchanged: no failing plugin's process has met the
record.*

*As built (PLUGUI1e, 2026-10-01): the host's answers of §4.1, on `DAORIS.DRIVER` in `DriverModule.Plugins.cs`. `PLUGINS`
gains per plugin `servers`, `hook`, `listening`, `health` and `update`; `PLUGIN` and `PLUGIN_ACTIVITY` call the two readers
with the loop's own record; `PLUGIN_READ`, `PLUGIN_ADD` and `PLUGIN_OPEN_FOLDER` are new, with `PLUGIN_FOLDER_NOT_OPENED`
and `PLUGIN_NOTHING_KEPT` in `Refusals` and both catalogues. `PLUGIN_TRY` writes the screen's `plugin.tried`, and the
shell's conversations write `plugin.served` through one seam of the chat runner. Readings the design left open:*
- *The list's `hook` and `servers` are what the catalogue takes, as its `harnesses` and `points` are, so a refused plugin
  answers none; the hook's command is as its manifest writes it, `${plugin}` and all, as the page's is.*
- *`update` is `waits`, `current`, or null: null with no record, and null where the update would be refused (a source
  gone, or one that does not read), whose sentence the page's Source gives.*
- *`PLUGIN_ACTIVITY`'s `since` is one of the log's spans (`1d`, `7d`, `30d` for the page's three periods), 7 days when
  absent; a span the log cannot read is the log's own `LOG_FILTER_UNKNOWN`.*
- *A folder is judged in Ask Daoris's judge's order: named whole, then `Placement`, then there, then read, then an id
  already installed, which `PLUGIN_READ` answers before the press and `PLUGIN_ADD` refuses, naming Update… and
  `daoris plugin add <folder>`. Both refusals are the driver's sentences, verbatim, since the drawer shows them in place.*
- *`PLUGIN_OPEN_FOLDER` makes the plugins folder before opening it, as the log's module makes its own; a host with no
  launcher answers `opened: false`; an unknown `which`, or a plugin's folder asked for with no id, is the driver's
  sentence.*
- *`PLUGIN` and `PLUGIN_ACTIVITY` read off the caller's thread: a data folder is counted for up to two seconds, and a
  month of the log may be read.*
- *The route tests start no process, so they are a class of the modules' fast half (`DriverModulePluginPageTests`) beside
  `DriverModulePluginsTests`, which tries plugins in real processes.*

*Not built here: `PLUGIN_TEST` and the checks `PLUGIN` is to answer (PLUGUI1g), and the bridge's calls onto these routes
(PLUGUI1f); until then the route test holds each route by the tests that ask it. `daoris-driver chat` writes no
`plugin.served`, as it writes no session lines. A conversation's line is held at the seam, not in a real conversation, and
`PLUGIN_OPEN_FOLDER` has met no file manager: the window is where both are seen.*

## D122 — The development documents are a standard the canon ships, and a repository declares its safe work once for the person's yes (2026-10-01)

**Decision (DOC1 and UNBLOCK1, the owner's: *"research a good development doc pattern for code generation and use it as
standard for all repo setup so its good for sessions, and the goal is to unblock the repo as far as possible so less
ask human permission during development"*).** The contract is `docs/2026-10-01-development-documents-design.md`: the
study (§1), the standard (§2), fewer asks (§3) and the build (§6). It extends D117's layout and D72–D74's scopes.

1. **Documents have roles, and the canon speaks in roles.** Brief, room, knowledge, skill, router, decisions,
   backlog, archive, fixes, changelog, glossary, gates, each with one job and one way it is read: always (the
   brief, beside the doctrine region in the root file every agent reads), on demand (rooms, knowledge, skills, the
   router), or by lookup (the records).
2. **The standard is core canon, as knowledge and a skill.** `development-documents` (core knowledge) states the
   roles, the reading tiers, the brief's content test (*what nearly every task needs and a reader could not
   derive*) and the ceilings' principle. `set-up-documents` (core skill) carries the procedure and the templates
   beside it. Not a rule: the part every task needs, where the records are, is carried as generated data.
3. **A repository binds roles to paths in its manifest** (`documents`). `sync` renders a *Where things are* table
   into the region. `check` fails on a fact (a declared path missing, escaping, a link, or the table stale) and
   reports a judgement (a document over its ceiling in words, the root file over the smallest harness limit in
   bytes, no backlog or decisions declared). A repository that declares nothing sees no change.
4. **Safe work is declared in the gates file** (`safe` beside `gates`): the gates that are not the queue's
   (`kind` check or suite, not `quiet`), the build and test commands, the lockfile install. Git on a repository's
   own branch is Daoris's default, and `commit` gains `git mv`.
5. **The declaration is read from the repository's line, judged, and waits for the person once.** A judge refuses
   the carve-outs (a push, a publish, a release, a history rewrite, a discard, a recursive delete, a path outside,
   an operator, a runner with arguments, an install that adds a package). The rest is one `declare` proposal per
   repository and per widening, D74's rule unchanged: a narrowing applies at the tick, a widening waits. Accepted,
   it is a layer of its own in the repository's scope.
6. **Each harness is handed it in its own words, or nothing.** Claude Code on both doors gets exact rules (and
   `PowerShell(…)` once measured). codex and dsh are handed nothing until measured, and the surfaces say so.
7. **The carve-outs are held harder where auto mode would allow them.** While `no-push` is on, the composed spawn
   settings carry an `autoMode.hard_deny` entry against a push in any form, a publish and a release, always after
   `"$defaults"`.
8. **Asks are counted.** `permission.refused` joins the machine log from both doors, and `session.read` records
   which role each read resolved to, never a path or a word. The usage report gives asks per session, before and
   after.

**Why.** The canon already depends on records it never places (§0.1), so every session in every adopter searches
before it works. The makers converge on the same file, the same content test, exact commands, detail on demand and
a ceiling on what is read whole (§1.3), which is the bar this project believes. Asks fall only if what a session may
run is declared where a tool reads it: an instruction file shapes what an agent tries, not what its harness allows.
The person's yes stays because a declaration is a checked-in repository's allowances. D74 forbids an agent widening
itself, and the harness's maker reached the same rule: a repository's allow rules wait for trust, and `autoMode` is
never read from project settings. One yes per repository replaces one proposal per refused command, which is the
growth D81 rejected.

**Findings the study turned up** (design §1.6), each carried by a row or an owner's call: the CLI's default core
budget, 30,000 bytes, leaves at most 2,768 bytes of brief under codex's 32,768-byte cut (BUDGET1); a push written as
`git -C . push` passes `no-push` and is allowed by auto mode's defaults (UNBLOCK4); a project skill's
`allowed-tools` is honoured untrusted, so no canonical skill may carry it (DOC3's test); package-manager wildcard
rules are dropped in auto mode (exact rules); four of this repository's gates are not a session's to run (`kind`
and `quiet`).

**Rejected** (the design's §8 has the full list):
- **Allowances applied from the declaration without the person**: D74, the maker's own trust rule, and a session
  could edit the declaration.
- **A skill whose `allowed-tools` names the gates**: honoured untrusted, for one turn, on one harness, unreviewed.
- **Writing the repository's `.claude/settings.json`**: the repository's file, and trust-gated (D72).
- **Translating into codex's `.rules`**: per account, and an execpolicy `allow` runs outside the sandbox. Measured
  first (UNBLOCK7).
- **One proposal per command** (D81), **runner prefixes** (`Bash(npm run *)`), **the declaration in the manifest**
  (D26), **reading it from the session's tree**, and **every declared gate offered**.
- **The standard as an always-loaded rule, a pack, or in the adoption playbook**: the budget; a dependency of core
  cannot be opt-in; the playbook cannot be read from another repository.
- **Ceilings that fail** (D54), **notes by lifecycle folder as the standard** (D117 §2.4), **a glossary required
  everywhere**, **a command-reading push hook** (held behind the canary), and **`dontAsk` or `bypassPermissions`**
  (D81).

**What it amends, when built.**
- D72: a fourth layer per repository, `declared`; `commit` gains `git mv`; the composed file gains the
  `autoMode.hard_deny` entry.
- D74: a proposal whose author is a repository's declaration at a commit, with a `declare` action.
- D81: the carve-outs are held in the classifier as well as by the textual deny.
- D94: three lines, `permission.refused`, `session.read` and `session.skill`.
- D115: the gates file gains `safe`, and DEV5's `kind` and `quiet` decide what a session is offered.
- D117: the set-up quest carries the standard's three steps and the declaration; its press's rule loses `git mv`;
  the root file's bytes report is one line with DOC3's.
- The adoption playbook (local): the same three steps.

Each row that builds a piece notes the amendment where it lands.

**What the gates do not cover.** This change is documents only, and nothing is built. Its statements about this
repository were read from its files: the canon's rules and skills, `Acp.cs`, `Adapters.cs`, `Permissions.cs`,
`RuleProposals.cs`, `SessionLog.cs`, `SessionEvents.cs`, `GateDeclaration.cs`, `RepositoryScanner.cs`,
`canon.ts`, `config.ts` and `drift.ts`. Its statements about the harnesses are their makers' documentation, read
2026-10-01 and cited in design §9, or LAYOUT2's reading of their shipped code; none was measured on a turn. The
account of what AR-2201's sessions read is the parent's, recorded in no tracked document. `verify` checks this
entry's shape and the design's links and budget, and none of their words.

**Built 2026-10-01 (DOC2): the standard is canon** (points 1 and 2, design §2.1–§2.6 and §4).
- Core knowledge `development-documents` and core skill `set-up-documents`, the skill with seven templates in
  `templates/`: `brief.md`, `room.md`, `knowledge.md`, `router.md`, `decision.md`, `backlog-row.md` and
  `archive-entry.md`. §4 named five. `archive-entry.md` joins them because §2.5 states that shape, and
  `knowledge.md` because the skill moves a brief's deep dives into the knowledge tier, whose frontmatter is its
  index row. The fix log's shape stays the `fix-log` skill's, and a skill's stays the harness's, so neither is
  written twice. The skill carries no `allowed-tools`.
- Measured, not estimated: this repository's region went from 22,672 to 23,306 bytes of 26,000, 634 bytes for the
  two index rows (§2.6 estimated about 450). Each example's region grew by the same two rows, to 21,758 (engine)
  and 21,975 (game) of 30,000. This repository and both examples were re-synced in the same commit.
- Canon-authoring holds. No file names a product, a harness, a build command or one repository's path. The byte
  limit is *one widely used agent's*, 32,768 bytes when written, and the file names are left to the index (§2.6).
  The records' list is described as the brief's *Where things are*, written by hand or generated where a tool
  does it, which is true before DOC3 and after.
- The adoption playbook (local) gained steps 6 to 8: write the brief, declare the documents and the rooms,
  declare the safe work. Its later steps renumber to 9 to 11, and the hand-over names each new item. It says
  plainly that the CLI ignores a `documents` field until DOC3 lands, and that nothing reads `safe` until
  UNBLOCK2; those rows update the playbook when they land.
- Found building it: `dogfood.test.ts`'s *every shipped canon skill carries the frontmatter the harness needs*
  takes every file under a skill's folder for its entry file, so the first canonical skill with supporting files
  fails it, though `canon.ts` ships a skill's whole folder by design and `canon.test.ts` holds that. The test is
  the CLI lane's, and DOC2 names it rather than changing it.
- What the gates do not cover: the knowledge document's statements about agents are the design's sources (§9),
  not measured here. *No shared skill carries one*, of a skill pre-approving its tools, is true of the canon
  today by search; DOC3's test is what will hold it. Whether the templates produce a good brief is judgement,
  which only a real set-up shows (§7, point 5).

**Built 2026-10-01 (DOC3): roles bound to paths in the CLI** (point 3, design §2.7–§2.8). `documents` in the
manifest, `init` and `analyze` naming candidates, `sync`'s *Where things are* table, `check`'s facts and
reports, the canon scan for `allowed-tools`, and a seventh release-rehearsal phase. Each case was a failing
`node --test` case first. The choices the design left open, each held by a test:
- **Ten declarable roles.** `knowledge` and `skill` are roles and are refused here, since the index already
  lists them from the target. `brief` and `room` take `{ "words": n }` and refuse a path. A field nobody reads,
  and a ceiling that is not a whole number above zero, are refused, so a typo never silently drops a ceiling.
- **Where each refusal lands.** A shape problem, an unknown role, a path that escapes, is the root, or sits
  inside the target or the mirror root, and a role declared twice are refused where the manifest is read
  (exit 2), as an escaping room is. So `check` fails on them without touching the path. A role declared twice
  is found in the text, because `JSON.parse` keeps the last of two keys silently. A link, or a link held as
  text, is a fact about the disk: `check` fails on it (exit 1) and never reads through it, and `sync` refuses
  it even with `--force`, since the table would send every session through it. An absent document fails
  `check`, and `sync` names it and proceeds: blocking every canon update on one record would hold the
  doctrine to it.
- **A declared path may be a folder**: a folder of decision records is the decisions role (§2.1). A ceiling on
  a folder is reported as measuring nothing.
- **The table** is the roster's last section, after *Rooms*: `| Role | Where | Its job |`, one row per role
  with a path, in the roles' order, each path a code span with its pipes escaped. A code span is the brief
  template's shape, at half a link's bytes. `check` compares it apart from the tiers' tables, so a stale one
  is named as itself and never as the roster. It comes from the manifest alone, and the lock records
  nothing, so D19's table gains no cell. Measured, not estimated: seven rows cost 639 bytes of the region,
  with each job cut to what finding a record needs (§2.6 estimated about 400; the canon's whole sentences
  cost 729).
- **The brief's words** are the root file's text outside the region. A brief still in `CLAUDE.md` is not
  measured until it moves (LAYOUT5). Words are whitespace-separated tokens, `tools/doc-budgets.mjs`'s count,
  duplicated deliberately until DOC4 makes that tool read the manifest. *No backlog or decisions declared*
  is reported only when the repository declares something.
- **The root file's bytes** stay LAYOUT3's one `size` line. The brief's words are a separate line.
- **Candidates** are conventional names, matched without regard to case, files before folders, and never the
  project's own readme. `analyze --json` carries them as `documents`. `status` carries the declaration and
  the three facts.
- **Two defects DOC2 found.** The dogfood test now holds each skill folder to a `SKILL.md`, with the name and
  description checks on entry files only, and a flat `skills/foo.md` still fails. `doctor`, and `analyze`'s
  search before adoption, compare a skill's entry file only, so `dispatch-subagent` no longer reads as 61%
  like `set-up-documents/templates/backlog-row.md`.
- **The `allowed-tools` scan** reads frontmatter only, in any spelling of the field and either YAML shape,
  over every file of a skill's folder: a skill's template copied into a repository would carry the field.
  It was seen failing on a fixture and on a field planted in a real canon skill.
- What the gates do not cover: the release rehearsal's phase was written and not run in the branch, and its
  patterns were checked against the source bin in a scratch consumer. The service's reader of `documents`
  is DOC5's. The twins table gains its row when that reader lands, matched against
  `documents-manifest.test.ts`.

**Built 2026-10-01 (DOC4): this repository declares its documents.** `daoris.json` names eight roles (router,
decisions, backlog, archive, fixes, changelog, the naming glossary, the gates), and `sync` writes the *Where things
are* table into the region. Two things the design's row did not foresee:
- **The table costs about 1 KB of the always-loaded core** (23,306 → 24,064 of 26,000 bytes). Kept whole: the
  table is the standard's point, and each row answers a search a session would otherwise make.
- **`doc-budgets.json` does not retire; it shrinks.** Of its five documents only the backlog has a role. The
  standing orders are `CLAUDE.md`, and the `brief` role measures the root instruction file outside the region,
  which here is `AGENTS.md` with nothing outside it, until LAYOUT6 moves the brief there. The consuming story,
  the forward sequence and the contract have no role, and inventing one for each would grow the closed set
  for one repository. So the tool reads a declared ceiling from the manifest, keeps the rest in its own file,
  and fails on a document given a number in both, so a ceiling is still written once. The backlog's ceiling
  is now also `check`'s, which reports it with the role.
- Found building it: a scripted rewrite of the tool dropped every backslash, so `\s+` counted the letter *s*
  and every document read as within budget, exit 0. `words` is now exported and pinned by a test.

**Built 2026-10-01 (UNBLOCK4): the carve-outs held harder, and `git mv`.** Point 7 and the `commit` half of point 4,
as design §3.6 and §3.7 say, with one addition.
- **`commit` gains `Bash(git mv:*)`**, in both defaults tables (`Permissions.cs`, `permissions.ts`), held together by
  the test that reads the CLI's source. This amends D72's `commit` default.
- **The spawn file carries `autoMode.hard_deny`** while `no-push` is on: `"$defaults"`, then the sentence the design
  wrote. `SpawnSettings.Write` puts `"$defaults"` first whatever it is handed, and takes the list as a required
  argument, so no spawn on either door forgets to say. `no-push` switched off hands no `autoMode` key at all. The
  sentence rides the `no-push` default in the driver's table (`HardDeny`), and has no twin in the CLI's, which
  writes no spawn file. This amends D72's composed file.
- **The addition: `no-push` denies a push with options before it**, as `Bash(git -* push)` and `Bash(git -* push
  *)`. The maker's rule syntax puts a `*` anywhere, and only an allow with one before the subcommand draws its
  warning. So the forms the maker names as passing a `git push` rule (`git -C . push`, `git -c <key>=<value> push`)
  and their cousins (`--git-dir`, `--no-pager`) are refused structurally, on the pipe door too, before any
  classifier. What no rule can name, a quoted subcommand, an alias, a path to git or a shell running it, is left to
  the classifier's entry, and on the pipe door nothing allows it.
- **Rejected**: listing each option (`git -C * push`, `git -c * push`, …), which misses the next one; `Bash(git *
  push *)`, which also refuses any commit whose message says *push* as a word before another; and leaving the pipe
  door to the rule that nothing else allows a push, which holds only until a person or a trusted repository allows
  `git` broadly. **The price, stated**: `git -C <dir> commit -m "…"` with *push* as a word before another is refused,
  and a session rewords or commits from inside the tree.
- **Proven, and not.** Keylessly: the composed file on both doors (the fast half's `PermissionRulesTests`, and the
  real-tick `PermissionSpawnTests` for the pipe and protocol doors), and which form meets a deny, by a model of the
  maker's matching that `BashRuleTests` holds to the maker's own rows, with the same forms in `permissions.test.ts`.
  Unproven until the owner's canary: that the harness matches the option-first rules as its documentation says, and
  that the classifier reads `autoMode` from this file on the protocol door and refuses `git -C . push`.

*UNBLOCK5 built point 8's `permission.refused` (2026-10-01), so asks are counted from here on, before any
declaration lands. The line has a fifth field beside the design's four, `by`, what decided the refusal, because the
pipe door's documented frame carries it (`decision_reason_type`: `rule`, `mode`, `classifier`, `asyncAgent`) and it
separates a refusal working as designed (a deny rule) from one declared work should remove; the protocol door's is
null, as its `tool` is, since its wire names neither. The pipe door's frames are written from the Agent SDK's
TypeScript reference (`SDKPermissionDeniedMessage`, `SDKPermissionDenial`) and labelled so in the tests: no turn has
shown them yet. A subagent's refusal is not counted on either door, since its calls run beside the session
(CONSOLE3c). The usage report gives asks per session by adapter and repository, and the rule proposals by week and
state. `session.read` and `session.skill` stay DOC7's. The machine-log design's §4 and §6 say what was built.*

**Built 2026-10-01 (DOC5): the service reads declared records** (design §5's first twin, §6's DOC5 row).
`RepositoryDocuments` reads `documents` with the service's own code, and `RepositoryLayout.Documents` carries it
beside the rooms, checked against the same target and mirror root. The choices the row left open, each held by a
test in `RepositoryDocumentsTests`:
- **The declared path is the first candidate.** The scanner's rule that the first present candidate is the log
  stands, and a declared decisions, fixes or archive goes in front. A log at a name no candidate knows is found;
  one a candidate also names is found once; a candidate beside a declared log is not read as a second log. A
  declared path the disk does not hold falls through to the candidates. A declared log that is a link is not read,
  and no candidate is read in its place, as for a candidate that is a link (LAYOUT4).
- **A declared folder is one record per file**, every markdown file in it or below, read whole, since an ADR's
  headings are its own parts. A folder reached through a link is never entered. The service's candidates stay files.
- **The router is a document**: one knowledge entry, local, read whole, titled by its file name as the knowledge
  tier's are. Declared, never guessed: the service gives the router no candidates.
- **One file is one place in the index.** A file the tiers, a room or the router already read is not read again as
  a log, and a file declared for two roles is read once, by the first reader. Read twice, its entries would share
  ids, and the store's primary key fails the whole refresh on the second (REV3).
- **A declaration the CLI refuses is read as none, role by role.** The CLI is a gate: one role it cannot honour
  refuses the whole manifest (exit 2), so `check` fails and `sync` refuses, and the repository hears it from its own
  tool. The scanner is an indexer with nobody to tell: a refusal there would drop the repository from search, or
  fail the refresh for every repository after it. So a role the CLI refuses is undeclared and the roles beside it
  are read, as a refused room is (LAYOUT4). `documents` that is not a map or is held twice, and a manifest that is
  not JSON, are read as none. The scanner then reads its candidates as before, never a path the CLI refuses, and
  never a guess at which of two declarations was meant.
- **Rejected**: refusing the whole declaration for one bad role, which drops a good role's path for an unrelated
  typo and departs from how the same reader treats rooms; reading the last of two declarations of one role, the
  silent choice the CLI refuses to make; and candidates for the router, since a `docs/README.md` in a repository
  that declared nothing may be a site's front page.
- **Found building it**: the CLI checks a declared path as spelled, so a `..` inside one passes. `docs/..` is
  accepted and names the root, and `x/../.claude/knowledge/g.md` is accepted and lands in the target. The reader
  checks the spelling and where the path lands, and reads where it lands, so it is never looser than the CLI. The
  CLI's check is the CLI lane's, and this note names it rather than changing it.
- What the gates do not cover: the twin table's rows were copied from `documents-manifest.test.ts` and are compared
  by hand, as every twin's are. The service-only rows about the CLI (`1e3`, `2500.0`, the `..` paths) were checked
  once against `checkDocuments` from the source and are held by no CLI test. A real link was made and read on this
  machine; on one that makes none, the link test returns early. No repository in the family declares documents on
  this branch (DOC4, DOC6), so no refresh of a real corpus has read a declaration.

## D121 — Every tool Daoris runs is the system's, managed, or a file the person names; a list built in says where each version downloads, and more locations extend it without a release (2026-10-01)

**Decision (TOOLS1).** The owner: *"all tools that daoris using like git, [terminal] should all have a self managed
option (and can be setup in settings) which can be download from locations … I perfer provide default download
location and built into the app with a resouce json file that can be updated if need other resouce location"*. The
contract is `docs/2026-10-01-tools-design.md`, which extends D57 from agents to tools. So:

1. **The tools are declared in code, in both artefacts**: Git, Node.js (with `npm` and `npx`), PowerShell, GitHub
   CLI and Azure CLI. A list may offer versions of these. It can never make Daoris run a program its code does not
   name. Windows PowerShell, Command Prompt and Daoris's own programs are not tools.
2. **Each tool is run one of three ways, and holds exactly one**, in `$DAORIS_HOME/tools.json`, twins in the CLI
   and the driver:
   - **the system's**, from `PATH`. Absent means this, and it is today's behaviour byte for byte;
   - **managed**: one exact version downloaded into `<home>/tools/<tool>/<version>/` and verified;
   - **a file** the person names.

   The way set decides every question about the tool. A managed version nobody downloaded refuses, and so does a
   named file that is gone. Neither ever falls back to `PATH`, as D57's pin does not. Nothing switches to managed on
   its own.
3. **One answer for Daoris and every child.** Daoris's own starts use the resolved file by its path: git, a hook's
   first word, npm in a pin, and the tree guard's node. Every child the driver and the modules start gets the tools'
   environment:
   - the folders of each tool that is managed or a file, first on its `PATH`;
   - `GIT_CONFIG_GLOBAL`, when git carries a setting.

   Nothing else changes, and the application's own environment is never rewritten. A source-reading test holds
   every start, as `NoConsoleWindowTests` holds `CreateNoWindow`.
4. **Git carries settings from an allow-list, and its first key is `core.sshCommand`**, which answers the failure
   WSR7 measured. It travels in a global file Daoris writes under the home:
   - it includes the person's own global configuration first, which is read and never written;
   - Daoris's keys follow, between markers, and a child's own writes there are kept;
   - a repository's own configuration still wins over both.

   The resolved git's version is asked: below 2.32 a setting refuses, since that git would ignore it silently, and
   below 2.29 the fetch says it cannot run as D109 needs.
5. **`resources.json` is built into the install**, at `app/resources.json` beside the application.
   - **Schema 1.** For each tool: its source and licence, then each version's files by platform, each with a URL,
     a sha256 and a size, an archive kind (`zip` or `tar.gz`), the executable inside, and the folders for `PATH`.
   - **Never rewritten in place.** A newer list is a **resource location**: the address of another list, `https://`
     or loopback `http://`, set in Settings and in `tools.json`. It is fetched only on the person's press and kept
     under the home.
   - **The merge.** The person's locations are read in order, then the built-in list.
     - One tool, version and platform is one download: lists that disagree on it refuse that version, naming both.
     - Lists that agree under different addresses are mirrors, tried in order.
     - A tool's versions are the union, and the newest is the highest by number.
     - An unknown tool or schema is refused.
   - **Every file is checked** against its list's hash and size, staged, and moved whole. Finding its record is the
     proof.
   - **Trust.** A list is trusted as its host until lists are signed, which waits for a release key Daoris does not
     have.
   - **No default location** is named until Daoris publishes one of its own.
6. **Agents stay on their makers' channels** (D57 §3a). The two share:
   - the staging and layout discipline;
   - HTTPS only;
   - nothing redistributed or patched;
   - the CLI's fetcher seam;
   - update resolving the newest to one exact version.

   Node is the one crossing: a pin's `npm` is Tools' npm, and `agent install` keeps the system's.
7. **The doors.**
   - **Settings → Tools**: a machine domain after *Agents*.
   - **The terminal**: `daoris tool list|path|use|download|update|delete|git ssh|locations|look`.
   - **Ask Daoris**: an eleventh kind, `tool`. Its doors: *use* the system's or a managed version, *update*, the
     SSH command as git's own or Windows', removing a location, and the go places.
   - **Exempt, each with its reason**:
     - a named file, a custom SSH command and a new location are the person's own press: a program, or a source of
       programs;
     - *Download* and *Look for updates* change nothing;
     - deleting a version is a discard.
   - **Names**: three glossary terms, *tool* 工具, *managed* 托管 and *resource location* 资源位置, within D116's
     budgets, after NAME1b renames *Tools & accounts*.

**Why.** Git is the program Daoris can least do without and the one it chooses least. It is a bare name that
nothing resolves, its version is never asked, and nothing but the command line can hand it a setting. So WSR7 could
only say what the git on the path needed; it could not give it. Five resolvers answer *which program is this*, and
git and a hook's command use none of them. A session's tools are whatever `PATH` it inherited, as USE1g found from
the other side. D57 already answered the same question for agents, and its answer carries: a way the person
chooses, absent meaning today, a choice that cannot run refusing rather than guessing, and one resolution that both
doors and every child share.

**The alternative weighed, as the owner asked: a separately released resource package.** A sibling project in the
family ships one (read, not changed).
- **It bundles three parts**, and every other tool is a constant in its application's code.
- **A new package still needs a new application**, since the application names the package's version, and the
  sibling's own publish script warns of exactly that.
- **The package is not hashed.** It is trusted as TLS and an immutable registry.
- **Installed means marker files exist**, so a newer package is never noticed.
- **The registry's size limit** trimmed what it could carry.
- **Its version once drifted** across three places.

The list built in, with more locations, needs no release for a newer version, redistributes nothing, hashes every
file, knows each version by its folder, and moves each tool alone. Its cost is keeping the list's hashes current
from the makers' published sums. From the sibling it keeps:
- an HTTPS-or-loopback rule for an override address;
- staging then moving;
- asking the resolution again at each start;
- meeting an agent's need for Git Bash from the machine before downloading one for it.

**Rejected** (the design's §8 has the full list):
- **The separately released package.**
- **Rewriting the built-in list in place.** The install folder is the publish's.
- **Agents under the list.** It is a weaker check for the same bytes, and a list that lags the makers.
- **Layering the three ways as D57 layers an agent's.** A pin hidden under a file is invisible on a screen of one
  choice.
- **Managed by default.**
- **A tool set a list can extend.**
- **Rewriting the application's own `PATH`.**
- **Carrying git's settings as `-c` on Daoris's calls alone.** The driver and a session would get two answers.
- **`GIT_CONFIG_COUNT` or `GIT_SSH_COMMAND`.** Their command-line scope overrides a repository's own configuration.
- **Writing the person's global configuration, or a checkout's.**
- **Patching a managed git's own system configuration.**
- **Git's full portable distribution, for its bash**, until TOOLS10 measures the need.
- **A `file:` location.**
- **Accounts for gh and az.**
- **Per-workspace tools.**
- **Ask Daoris proposing a named file, a custom SSH command, or a new location.**

**What it amends, when built.**
- D57 and the toolchain design: the resolution rule's home gains tools, held to one way rather than layers.
- D96 and the terminal design: the shells come from the tools' `PATH`, and Git Bash is the bash beside the git
  Tools resolves, else the system's.
- D64 and D100: a hook's first word that a tool answers for is the resolved file, so a landing plugin's `git`, `gh`
  and `az` are Tools'.
- D109 as amended by WSR7: the fetch's git is Tools' git, and what was not fetched can name Settings → Tools.
- D110: an eleventh kind.
- D116: three terms.

Each row that builds a piece notes the amendment where it lands.

**What the gates do not cover.** This change is documents only, and nothing is built.
- **Read from the code:** the statements about today's code, from the files the design's §1 names, at `d618cbb`.
  The service starts no process, and no test holds that.
- **Read from outside:** the sibling's package, from its folder, which was not changed.
- **Not measured:**
  - which of the makers publish an archive Daoris can unpack, which TOOLS3 confirms before a list line is written;
  - which ssh reaches the owner's SSH remotes;
  - how git reads an include whose file is gone;
  - what a minimal git's configuration reads differently from Git for Windows';
  - how Claude Code finds its Git Bash with a minimal git first on `PATH`;
  - where a managed npm puts a global install;
  - whether an agent passes its `PATH` on to the servers it starts;
  - whether gh and az stay signed in when managed.

`verify` checks the records' shape, budgets and duplicates, and none of these words. The design's §6 says what a
rehearsal can prove and what waits for TOOLS11's real downloads.

**As built (TOOLS2, 2026-10-01): `tools.json` and its resolution, twins, with the terminal's door.** The CLI's
`tools.ts` and the driver's `Tools.cs` read and write the file, and `daoris tool list|path|use <tool> system|file
<path>` is the terminal's door. Nothing starts a tool through them yet: that is TOOLS5. What the design left open,
settled here:
- **A `tools.json` that does not read refuses every tool**, and so does one whose `tools` is not an object. It is
  never read as empty, as `harnesses.json` is (D57). Empty would run the program `PATH` finds in place of the one
  chosen, which rule 2 already refuses for a single entry.
- **Rule 2's *both* is read for each way.** An entry that names another way's field is refused: the system's with a
  version or a file, managed with a file, a file with a version. JSON `null` is no field, and a `null` entry is no
  entry.
- **A managed version's record** is `<home>/tools/<tool>/<version>/tool.json`. Its `exe` names the executable under
  `package/`, as a relative `/` path with no `..`. TOOLS4 writes the record in that shape.
- **A system tool that `PATH` does not find is said, not refused.** The resolution names the three ways, and a
  caller keeps today's behaviour (§2.3).
- **A whole path is .NET's `Path.IsPathFullyQualified`**, spelled again in the CLI, so `\x` and `C:x` are not whole
  on either side.
- **`use … system` writes `{"use": "system"}`** rather than removing the entry, and a write keeps every key it has no
  field for.
- **The tables are held line for line by a gate.** `tools.test.ts` parses `ToolsTests`' theories and holds each row
  to its own table, so a row changed on one side alone fails `npm run verify`.
- **Not yet built.** `use … managed` refuses until TOOLS4, so the managed refusal names only `daoris tool use
  <tool> system`. The design's *which downloads it* joins with the download. `list` shows each file but not its
  version, since asking the version starts the program, and that is `toolchain.ts`'s job. The write-side checks of
  rules 4 and 5 (`gitKeyProblem`, `locationProblem`) wait for TOOLS6's and TOOLS3's verbs.
- **Rejected:** reading a file that does not read as empty, for the reason in the first point.

**As built (TOOLS3, 2026-10-01): the resource lists and their merge, twins, with the list built in.** The CLI's
`resources.ts` and the driver's `ToolResources.cs` read schema 1 and merge every list for one platform; the
driver's `resources.json` is the list built in, laid out at `app/resources.json` by `tools/desktop-publish.mjs`.
Nothing reads the lists at run time yet: the verbs that fetch and use them are TOOLS4's. What the design left open,
settled here:
- **Only the list's shape refuses it whole**: text that is not JSON, not an object, a `schema` other than the number
  1, or a `tools` that is no object. Below that, each thing that does not read is skipped and said, one note each,
  and never guessed at: an undeclared tool, a version that is not exact, a platform off the table, a file missing a
  field.
- **Every address in a list holds the download's rule**: a file's `url`, a tool's `source` and its licence's `url`
  are `https://`, or `http://` to this machine. A licence needs an `id`.
- **`sha256` is read in either case and kept in lower case**, since PowerShell publishes its sums in capitals.
  `paths`, when named, is a list of at least one folder inside the archive, and `.` is its root.
- **Both readers walk every object in ordinal order.** JSON's own order is not one both runtimes keep: Node puts
  keys that look like whole numbers first, so a version `2` would reorder.
- **The merge is for one platform.** A refusal names the first field that differs (sha256, size, archive, exe, then
  paths), the first list that named the download and the one that disagreed. Later lists are not compared with a
  refused version, and a refused version is never the newest.
- **A location's copy** is `<home>/tools/locations/<sha256 of its address as written>.json`, the bytes as fetched.
  A location never fetched is a list that names nothing and says so.
- **What vouches for a list**: *built in*; *this machine* for any loopback host, `https` included; otherwise the host
  with any port it names, in punycode.
- **The platform is the process's architecture** on both sides (Node's `process.arch`, .NET's
  `ProcessArchitecture`). An x64 build on arm64 Windows reads `win-x64`.
- **Where the list built in is.** Every build that references the driver carries it beside itself
  (`CopyToOutputDirectory`). The driver reads it beside the application first, then beside the home, and the CLI
  reads it beside the home. In an install the publish is its one writer: it copies the tracked file byte for byte, and
  refuses one that is not schema 1. The project marks it `CopyToPublishDirectory="Never"`, and no publish ran in this
  row to watch that hold.
- **The first entries** are one version of each tool for `win-x64`, the platform an install is published for. Each
  was read from its maker's published sum and recorded in `docs/2026-10-01-tools-resources-evidence.md`, where every
  whole file was also streamed once and matched. A test refuses a list line whose hash, size, address and executable
  that document does not carry. On the way, MinGit was found to carry an ssh of its own and no bash, and az's
  executable is a batch file, `bin/az.cmd`.
- **Not built, and corrected.** The note above said the write-side check of rule 5 waits for *TOOLS3's verbs*. The
  design's §7 puts `locations add|remove` with TOOLS4's, so no verb and no writer of `locations` is built here.

**As built (TOOLS4, 2026-10-01): download, verify, unpack, lay out, twins, with the terminal's verbs and the driver's
followed action.** The CLI's `toolinstall.ts` and `zipfile.ts` (beside `tarball.ts`) and the driver's `ToolInstall.cs`
plan a version, download it, verify it, unpack it and lay it out; `daoris tool download|use … managed|update|delete|
locations|look` is the terminal's door, and the driver's `ToolActions` starts the same work as an action the page follows
and the person's stop cancels. Nothing starts a managed tool yet: that is TOOLS5. What the design left open, settled here:
- **Every refusal names its check**, a code both twins spell: `archive`, `address`, `unreachable`, `size`, `hash`,
  `absolute`, `outside`, `stream`, `link`, `entry`, `encrypted`, `method`, `checksum`, `truncated`, `damaged` and `exe`;
  a plan's are `unknown`, `version`, `conflict`, `machine`, `file` and `platform`; a delete's `missing`, `in-use` and
  `held`; an action's `busy`. One table of archives built in the test holds both readers, and the CLI's tests parse the
  driver's theories for it, the plan, the download, the delete, the look, the record's keys and the bounds.
- **The driver reads the archives by hand and inflates with .NET.** Measured on .NET 10 before a line was written:
  `ZipArchive` reads a stored entry whose CRC-32 is wrong and names no entry's method, `TarReader` reads a header that
  fails its checksum and an archive with no end, and `GZipStream` reads a gzip cut before its trailer as whole. So both
  sides walk the records, and the driver holds a gzip's trailer, the CRC-32 and size of everything it holds, itself.
  Zip64's records are read on both, since an archive of more than 65,535 entries needs them.
- **Where the twins still differ, and no list carries the input:** a deflate stream cut short inside an entry's stated
  size is `damaged` to Node's inflater and `checksum` to the driver's, whose inflater ends early; both refuse. A gzip of
  more than one member reads whole in Node and fails the driver's trailer check.
- **Mirrors.** Each address is tried in read order, the person's first; one whose bytes fail the size or hash is passed
  over, since the bytes are the check whichever host served them. When every address fails, the last one's check names
  the refusal, and the sentence lists each. An unpacking refusal is not retried: the bytes that verified are the same
  from any address.
- **Every hop is held to the address rule**, redirects followed one at a time by Daoris and never by the HTTP client, ten
  at most. In the CLI the rule is an option of `service.ts`'s one fetcher, which the `tool` row hands in; with no option
  it fetches as `agent pin` always has.
- **The record** is `tool`, `version`, `platform`, `sha256`, `size`, `archive`, `url` (the list's address that served
  it), `lists`, `exe`, `paths` and `at`, in that order. The archive itself is not kept, and an empty tool folder a
  refusal leaves is removed, so a refusal leaves nothing under `tools/<tool>/`.
- **`use … managed` writes only after the download verified**, and the writer itself (`useManaged`, `UseManaged`)
  refuses a version that is not downloaded. The managed refusal now names the download: *`daoris tool use git managed
  2.51.0` downloads it*.
- **A look keeps a copy only of a list that reads.** A list this build cannot read, or a host's error page, keeps the
  last copy and says its age, from the copy's write time. Each location is bounded at 30 seconds. Removing a location
  leaves its copy, read again only if the address is added again, with its age said.
- **Exit codes:** a refusal by a check or a plan is 1, a malformed call 2.
- **The verb moved from `tools.ts` to `toolinstall.ts`**: it merges the lists, and `resources.ts` imports `tools.ts`, so
  `tools.ts` importing it back would be a cycle that fails at load. `DriverException` is no longer sealed, so
  `ToolRefusal` travels every road a driver error does.
- **The machine log** gets `tool.download.started`, `.verified`, `.refused` (with its check) and `.stopped`, and
  `tool.location.fetched` and `.failed`, by a location's place in the list, from the driver library. The CLI is no
  machine log source (D94) and writes none.
- **Not built:** the routes and the screen (TOOLS7), Ask Daoris's kind (TOOLS8), starting a managed tool (TOOLS5) and
  `tool git ssh` (TOOLS6). `tool list` names each tool's downloaded versions, not their sizes.
- **Not covered by a gate:** a real host. The redirect rule is proven against stand-ins on both sides: Node's `fetch`
  answering a redirect itself under `redirect: 'manual'`, and .NET's handler with `AllowAutoRedirect` off, are each
  runtime's documented behaviour, exercised first by TOOLS9's loopback server and TOOLS11's real downloads.
- **Rejected:** keeping the bytes of whatever a location answers, which would let a captive portal's page replace a
  good list; retrying an unpacking refusal at the next mirror, which would fetch the same bytes again.

**As built (TOOLS5, 2026-10-01): one answer for every child.** The driver's `Tools.Children.cs` and the CLI's
`tools.ts` build a child's environment and resolve a command's first word, twins held by `ToolsChildrenTests` and
`tools-children.test.ts`. Daoris's own git (`WorkingTree.GitStart`, which `GitAsync` and `GitBytesAsync` now share), a
hook's first word, a pin's `npm` and the tree guard's node are the files the tools resolve, by their whole paths. Every
session, chat, intake, helper, probe, agent action, hook, landing plugin, plugin trial and terminal shell is handed the
tools' environment. What the design left open, settled here:
- **Which home.** A start with no home of its own reads `$DAORIS_HOME`, D63's one seam, at that start. These are git, a
  session's shell (`Spawning`), a probe, an action and the terminal. A hook, a plugin's trial and the tree guard read the
  home they are handed, which is the one the hook is told as its own `DAORIS_HOME`. With no home nothing changes, and
  git is the system's.
- **The system's git now starts by its whole path**, found by `CommandPresence` with PATHEXT, where it was a bare name
  that only `.exe` could answer. A git `PATH` does not find keeps the bare name, so the start fails in the system's words,
  as before.
- **A way that refuses puts no folder on a child's `PATH`**, and Daoris's own start of that tool refuses in the
  resolution's words:
  - git's answer is the refusal;
  - a hook is not started, and is logged and skipped;
  - a pin's `npm` is not run;
  - the tree guard refuses the session's start rather than write a guard that would start `PATH`'s node.

  A child's own lookup then meets the `PATH` it inherited, which Daoris does not rewrite.
- **A managed version's folders** are its record's `paths` when they are folders inside the package (`.` is the
  package), else the folder its `exe` is in. A name a tool answers for other than its own (`npm`, `npx`) is found beside
  the tool's file by PATHEXT, and never on `PATH` when the tool is managed or a file.
- **Byte for byte.** With every tool the system's, nothing is set at all: the driver adds no variable, and the CLI hands
  back the environment it was given. On Windows the CLI writes `PATH` under the spelling the environment holds (`Path`),
  never both, and `onPath` now reads `PATH` and `PATHEXT` in any case.
- **A hook's first word that is no tool's** is found on the child's `PATH` by `CommandPresence`, through the agents'
  shim rule. A `.cmd` there is held to its argument rule rather than started bare and not found.
- **The terminal.** PowerShell 7 is the file the tools resolve, and is not offered where its way cannot run. Git Bash is
  the bash beside the git the tools resolve, else beside the system's git found on the inherited `PATH`. The shell's
  environment block takes the tools' variables before the launch's own.
- **`agent install`** finds a first word a tool answers for on the system's own `PATH` (this process's, never the
  tools'), and refuses naming `agent pin` where there is none. Any other installer word is left as named.
- **The CLI starts every child in one place**, `startChild` in `toolchain.ts`, and finds a bare name on the child's
  `PATH`, as the driver's shim rule does. The dogfood test holds that its one `spawnSync` is handed `handTools`.
- **Held by source scans.** `EveryChildIsHandedTheToolsTests` (the driver) and `EveryModuleChildIsHandedTheToolsTests`
  (the modules, the application and the launcher) hold every `new ProcessStartInfo` to a `Tools.Hand(` before its start,
  or a comment above it that says `Not the tools' environment (TOOLS5):` and why. The exempt starts are the host and the
  application, which are Daoris's own, and Edge, which is the system's. The terminal is held by name. Each scan was seen
  failing on a removed call, a commented-out call and a removed exemption.
- **Amended where they land**: D57 (an agent is found on the tools' `PATH`), D64 and D100 (a hook's first word and a
  landing plugin's tools), D96 (the shells) and D109 (the fetch's git).
- **Not built.** `GIT_CONFIG_GLOBAL` is TOOLS6's: it joins `ChildEnvironment` and `childEnvironment` with the file it
  names. What was not fetched does not yet name Settings → Tools, which is TOOLS7's screen.
- **Not covered by a gate run here.** `ToolsChildProcessTests`, in the `Process` half, starts a stub git named as a
  file and wants its answer three times: from Daoris's own git, from a hook's own shell, and from a session's own
  shell. It was written and not run (MOD8). Not measured:
  - whether an agent passes the tools' `PATH` on to the servers it starts;
  - off Windows, where no shim resolves a session's bare first word, .NET finds it on this process's `PATH` and not the
    child's, though the child is still handed the tools' environment. An install is published for Windows only.
- **Rejected:** falling back to `PATH`'s copy when a managed `npm` is not beside its node, which is the silent
  substitution D57's pin exists to prevent; and handing a hook's first word to the system bare, which finds only an
  `.exe`.

*Amended by D124 (WSSETUP3, 2026-10-01): a child's `PATH` begins with the install's `app/bin/` beside the home, before
the tools' folders, so every child finds the install's `daoris`. With no install beside the home it is as above, byte
for byte. D124's note has the rest.*

**As built (TOOLS7, 2026-10-01): Settings → Tools.** A machine domain after *Agents*, on the routes of
`DriverModule.Tools.cs` that the page's `bridge/tools.ts` calls, the molecules in `settings/Tools.tsx` and the organism
`settings/ToolsDomain.tsx`. What the design left open, settled here:
- **The routes**, beside §4.1's: `TOOLS_LIST` reads files only, and with `ask` asks each resolved file its version, which
  starts it, so the page draws from the first answer and fills in from the second. `TOOLS_USE` takes `action` as
  `system`, `managed` or `file`. **`TOOLS_STOP`** is the person's stop, which §4.1 named no route for; one with nothing
  running answers `stopped: false`. **`TOOLS_PICK`** is the system's file picker for *Browse…*, a `PickFile` delegate the
  application hands in as it hands `OpenFolder`. `TOOLS_GIT` answers what a switch of git changes; TOOLS6 adds the SSH
  command to it.
- **The refusal codes.** Nine are built, each with a throw site: §4.1's codes but `TOOL_GIT_TOO_OLD`, which waits for
  TOOLS6's version floors, plus `TOOL_FILE_NO_VERSION` (a named file that does not start, or answers no version) and
  `TOOL_HELD` (a delete the system refused, with its reason). The file's own refusals, such as a `tools.json` that does
  not read, travel as the driver's words (`DRIVER_REFUSED`), which name the file to fix.
- **A named file is checked before it is written** (§4.1): a whole path to a file that is there, then started with its
  version question (bounded at fifteen seconds, its input closed) and refused unless it says a dotted number. Each file's
  answer is kept while its write time and size stand; an answer that did not come in time is not kept.
- **Nothing applies on choosing, for every tool.** A segment shows a way's controls and a press applies it, so git's
  switch can be said before it applies without a second rule for git. Git's press asks first: the four checkout keys
  from each git's own `config --system --list`, a git with no system file read as no keys, a side that cannot answer
  comparing nothing and saying why, and the git that runs now changing nothing. The switch is the second press. A
  managed git not yet downloaded is offered *Download* first, since the switch is asked of that git.
- **A download is followed** as an agent's install is: the press answers once started, its lines go to the loop's
  console buffer under `tools:<tool>`, closed at its end, so a console opened later reads the backlog through
  `TAIL_SESSION`, and its end is the `TOOLS_ENDED` news. The card shows the console and *Stop download* while the list
  says the action runs.
- **The look's wait** is a twin row: `hostBounds` gains `toolLookMinutes: 0.5`, `ToolInstall.LookBound`, held by
  `SyncBoundsTests`, and the page waits that for each location plus two minutes.
- **The list's query keys** are a root of their own in `bridge/tools.ts`, as Ask Daoris's proposals keep theirs, not
  under the driver's, which every tick asks again; the version question starts each program.
- **The machine log** gets `tool.used` (tool, way, and the version when managed) for a way set from the screen, never a
  path. The terminal's `daoris tool use` writes none, since the CLI is no machine log source (D94).
- **Ask Daoris** (D110). `HelpCoverageTests` answers *System*, a managed *Use this version* and removing a location as
  doors owed to TOOLS8's `tool` kind, and a named file, *Add location…*, *Download*, the stop, *Delete*, *Look for updates*
  and the picker as exempt with §4.3's reasons. Settings → Tools is not a place a go names until TOOLS8 adds it to the
  driver's `HelpPlaces` and the page's `places.ts` together; `places.test.ts` sets it aside, as it set Plugins aside.
- **Names** (D116, amended). The glossary gains *tool* 工具, *managed* 托管 and *resource location* 资源位置, and *version*
  now means a tool's too; `names-check --strict` finds nothing in them, and no existing label names a tool. The house's
  words for two presses §4.4 did not name: *Use the system's* 改用系统 and *Use this file* 使用这个文件.
- **D109 as amended by WSR7.** The page's sentence for what was not fetched now says Settings → Tools names the git
  Daoris runs, where it said the one on the path. The driver's own copy of it, in `SessionTrees.Sync.cs`, still says
  the path, and is the driver's to change.
- **Not built:** the SSH command and the global file's lines (TOOLS6); an *Update* press, which §4.1 does not put on the
  screen; the size a version takes on disk (a downloaded version shows the size its record says it downloaded); the
  menu atom, which is not on this base, so the version choice is `SelectField`.
- **Not covered by a gate run here.** `DriverModuleToolProgramsTests`, in the `Process` half, starts stub programs: a
  named file answering its version, the list asking it, and a switch of git over two stub gits. It was written and not
  run (MOD8). The window was not looked at, and the application's file dialog was compiled, not opened.

## D120 — Plugins leave the repository: a workshop in the home makes them, Daoris.Plugins keeps Daoris's own, and NuGet is where they are found (2026-10-01)

**Decision (PLUGREPO2, PLUGDIST1).** The owner made an empty folder, Daoris.Plugins, beside this repository, for
the default plugins' development, *"later will be release to nuget so we can use nuget as plugin site … (or we
can use npm, you can decide …)"*. They then answered where a plugin Daoris makes should live: by default in the
Daoris home, or in a place the person sets, with no repository needed. Daoris.Plugins is where Daoris's own
plugins are developed and what is published, and a plugin made in the home may be handed over to it.
`docs/2026-10-01-plugin-distribution-design.md` is the contract. It settles:

1. **A machine has a plugin workshop**, where Daoris makes a plugin a person asks for.
   - **Its kinds**: the home (the default, `<home>/plugins/.workshop/`), a folder outside any checkout, or a
     registered repository.
   - **It is stored in `<home>/plugin-sources.json`**, a new file with no older writer.
   - **It has two doors and a setting door**: `daoris plugin workshop …`, Settings → Driver, and Ask Daoris's
     `workshop`.
   - **A workshop is a small git repository Daoris owns.** A workshop session works there on an ask addressed to
     `workshop`: one session at a time, with its room rendered like the intake's. Its record is concluded from its
     exit, then the plugin's own tests and the kit's trial, run by the driver.
   - **Nothing in a workshop runs on the loop** until the person installs it. It installs from the workshop as a
     folder source.
2. **A plugin moves to a repository through that repository's own door.** *Send to a repository…* publishes a
   quest with the plugin as one zip. That repository's session writes it and lands it by its rule. D103's update
   gains a source the person names, `--from <folder>|package`, shown before the press.
3. **Daoris.Plugins is a repository with an agent of its own**:
   - one folder per plugin under `plugins/`, as the kit makes it;
   - its own brief and gate (`node --test`, needing nothing of Daoris's);
   - D117's `agents` layout with `windows-machine`;
   - registered, drivable, with trees on and landing by `merge`.
   The parent sets it up (§3.4 of the design, after LAYOUT3), and every plugin after that is an ask its own
   session takes. The two landing plugins and `in-app-browser` move there. `hold-by-title` and `browser` stay in
   `examples/` as the rehearsals' contract.
4. **The package source is NuGet.**
   - **The package**: a Daoris plugin is a package of the custom type `DaorisPlugin`, whose type version is the
     wire's `apiVersion`, with the plugin folder under `plugin/` and no dependencies.
   - **The reader** searches with `packageType=DaorisPlugin`, reads the `.nuspec` before a download, downloads
     from the flat container over plain HTTPS, and checks the package's SHA-512 against the catalog leaf's
     `packageHash`, with no NuGet client.
   - **Where it lives**: in the driver, behind `daoris-driver plugins find|show|install|update`. Only `service.ts`
     may reach a network in the CLI, and a package source is not a knowledge service.
   - **The record**: a package source's record is `{ package, version, sha512, source }`.
   - **Another publisher's plugin installs off.** *Daoris's own* is decided by the owner account, and by
     `verified` once the prefix is reserved, never by a name.
5. **The offers come from pinned packages** once the three are published. `plugin-offers.json` holds each
   `{ package, version, sha512 }`, the publish checks each, and a republish works from a cache.
6. **The Plugins view gains *Find plugins*** (D119): a *Find* mode in its list, a package's page with its trust,
   what it runs, what it needs and its versions, and two doors and Ask Daoris's doors for every new act.
7. **Publishing is the owner's.** The workflow is manual, a dry run by default, by trusted publishing. The account,
   the prefix reservation and every push are the owner's press.

**Why a workshop in the home.** The owner's answer, and PLUG9's gap: with no plugins repository, making a plugin
had nowhere to go. A plugin a person asks for is often theirs alone, such as a server for their own tool or a
rule for their own access, and needs no repository to be made, tested and installed. The workshop keeps every
property that made making a plugin *work* (D101): a session, tests, a diff, and a press to install. Daoris owns
the folder, so it reaches into nothing.

**Why NuGet.**
- **Its filter names what a package is.** A custom package type is a declaration of intended use, filtered
  exactly by the server (observed on two types). Its version carries the wire's `apiVersion`, readable before a
  download, and Visual Studio and nuget.exe will not put the package into a project.
- **Its rules match a plugin's.** No dependencies, and no installer run.
- **Its search result says who published**: `owners` are accounts, and `verified` is an identity-reviewed
  prefix.
- **Integrity takes HTTPS and SHA-512.** The catalog's hash matched a served package when checked.
- **The owner leaned to it**, and the reader is .NET.

**What it amends, when built**, each row noting it where it lands:
- **D64 §7 and D119 §8**: *no registry, no marketplace, no catalogue of third-party plugins* becomes *Daoris runs
  no registry and loads no code, and reads a public package source whose plugins land as folders by a person's
  press*. D24's and D57's registry was one Daoris would keep, and this is someone else's index.
- **PLUG9** (Ask Daoris design §9.7): a plugin is made at the machine's workshop, and at a repository only when
  the workshop names one. An ask to a repository carries the kit's scaffold.
- **D103**: an update may take a source the person names, and a package source is a source.
- **D101's gap**: a real plugins repository exists.
- **The offers' twins** (`OFFERED_PLUGINS`, `layOffers`, `PluginOfferTests`): from pinned packages.

**Rejected.**
- **npm.** Its `keywords:` qualifier filtered exactly when observed, so the backlog's *less precise* did not
  survive checking. But a keyword has no version and no meaning, and a package on npm is an ordinary dependency
  whose install-time machinery Daoris would not run. npm is stronger on integrity (inline SRI, ECDSA registry
  signatures, provenance) and on namespaces (a free, immediate scope). D120 answers with the pinned hash, the
  owner account, and a held signature check (PLUGDIST1h).
- **Both npm and NuGet**: two readers, two records and two trust rules, for no plugin that needs the second.
- **A release asset listing Daoris's own**, a sibling's way: one publisher, no search, and a trust by address
  alone.
- **An index Daoris runs**: a registry to operate (D24, D57).
- **The workshop's setting in `driver.json` or `plugins.json`**: older writers drop a field they do not know.
- **Installing another publisher's plugin on**: its code would run before the person read its page.
- **The workshop writing into a repository**, or a button that copies into a checkout: D32.
- **Trusting `Daoris.*` by name** before the prefix is reserved: anyone may publish under an unreserved prefix.
- **Checking the repository signature in the first build**: it needs a CMS reader and NuGet's signed-content
  rules, and is held as PLUGDIST1h.

**What the gates do not cover.** This is a design, and nothing is built.
- **Read from the code at `d618cbb`**: the examples, the offers, the tests that read them, and the source record.
- **The two siblings' repositories were read, not run.** The design names them only as *the first* and *the
  second*.
- **NuGet's and npm's facts** are their makers' documents, cited in the design. The observations were live
  queries on 2026-10-01:
  - search counts for two types and an unknown one;
  - a custom type on an unlisted package;
  - one package's served SHA-512 equal to its catalog hash;
  - npm's `keywords:` answers.
- **No custom type with a listed package was queried.** The first publish is that proof.
- **The workshop, the reader, *Find* and the pack** exist only as rows. `verify` checks this document's links
  and the log's shape, and none of its words.

**As built (PLUGDIST1a, 2026-10-01): the package and its reader, offline, with the record in both twins.** The
driver's `PluginPackage` reads a `.nupkg` and installs its plugin through `PluginInstall`, and `daoris-driver plugins
install <file.nupkg>` is the terminal's door. The CLI reads and lists the record a package leaves. Nothing reaches a
network: a package source is PLUGDIST1c's. What the design left open, settled here:
- **The type is `DaorisPlugin` alone**, compared without case, as NuGet compares type names. A package that also
  declares another type is refused, since §5.1 says *no other type*. The type's version is a `System.Version` whose
  major number is the plugin API. No version, a major of 0, or a major this build does not speak is refused before
  anything is extracted, the last naming both numbers.
- **The plugin's own `apiVersion` must equal the type's**, as its version must equal the package's. Otherwise the
  check a source makes before downloading would trust a type that says something else.
- **Any `dependency` element refuses the package**, in a group or not.
- **A part's name is read as NuGet reads it**: unescaped, with `\` a separator, and only then is the `plugin/` guard
  applied, so an escaped `..%2F` is refused as `../` is. A name twice, compared without case, refuses the package
  too, since Windows would write the second over the first, and so does a name holding a control character, which
  an escape can spell (`%00`) and no path holds. The guard judges every `plugin/` entry before anything
  is written, and each target is checked again against the stage's whole path as it is written.
- **The file is opened once, shared for reading only**, so the bytes hashed are the bytes extracted.
- **The stage is `<home>/plugins/.unpacking-<guid>/`**, a dot-folder the catalogue skips, gone whether the install
  succeeds or not. `PluginInstall` gains an internal `Add` for that stage, the one folder inside the home an add
  copies from; `Placement` still refuses every other.
- **A package file's source is the folder that held it**, a whole path. The record so names a folder source, §5.3's
  offline kind, which PLUGDIST1c can update from as from an index.
- **The record's rules**: a NuGet package id (ASCII, at most 100 characters); a version of one to four numbers, with
  an optional prerelease label and metadata; a SHA-512 as standard base64; and a source that is a whole path or an
  address by TOOLS3's rule (https://, or http:// to this machine). A record naming a package beside a folder or an
  offer does not read, and `{}` now names all three kinds.
- **The record's table is held by a gate**, as TOOLS2's is: `plugin-sources.test.ts` parses `PluginSourceTests`'
  record and update theories and holds its own to them, and was seen failing on a one-sided change.
- **A plugin from a file lands on**, as a folder's does at `daoris plugin add`: the person named the file. §5.7 step
  6's *another publisher's lands off* needs the owner account a search result carries, so it joins PLUGDIST1c.
- **Update refuses a plugin from a package, on both sides, in the same words**: a newer package takes its place by
  `daoris plugin remove <id>`, then `plugins install`, and what it kept stays. §5.8's *`daoris-driver plugins update
  <id>` does it* is PLUGDIST1c's to say, once that verb exists. The CLI's list names a package's source and offers no
  update, and `daoris plugin install` answers as a moved verb does.
- **Not built here**: a package source over HTTP, `find`, `show`, `install <Id>`, an update from a source, the off row
  for another publisher, and `plugin.installed` in the machine log (§5.10), all PLUGDIST1c's. The modules' `PLUGINS`
  answer still names a package record's kind `folder`, with no folder: PLUGDIST1d's to say.

## D124 — A workspace is set up one repository at a time by its own sessions: the install carries the doctrine tool, a set-up writes the knowledge a neighbour needs, registration follows the line, and a session looks before it asks (2026-10-01)

**Decision (WSSETUP1).** A driven session in the owner's work workspace stopped to ask the person a question its
own repository's notes and code could answer. The owner's diagnosis: *"the repo should be registered and apply the
doctrine and also initialize the knowledge"*. Measured on the install the same day: 29 repositories, all drivable,
none adopted, none registered (`connect` refuses a manifest that declares no `domain`, and none has a manifest), 23
with no indexed knowledge and 6 with 16 to 195 entries. The contract is
`docs/2026-10-01-workspace-setup-design.md`. It builds on D117 §6's set-up quest, D122's standard and D121's tools'
environment.

1. **The install carries its packed CLI, and every child finds `daoris` on its `PATH`.** `publish:desktop` packs
   `src/Daoris.Cli`, the release's own artefact, into `app/cli/` with two launchers in `app/bin/`, and TOOLS5's one
   environment puts `app/bin/` first for every process the driver and the modules start. It is Daoris's own
   program, not a tool. npm stays the channel outside Daoris and the manifest's `source` (D105).
2. **An older doctrine tool never rewrites a newer lock.** `sync` and `upstream` refuse a lock whose canon version
   is newer than their own. Two versions on one machine is a hazard with npm alone, and the tool answers it.
3. **The set-up quest initialises knowledge.** For a repository that is addressable, not adopted and declaring
   nothing, its steps check the tool's version, take up the doctrine in the agents layout, then write the domain
   (`summary`, `owns`, `accepts`, `uses`) and the repository's own knowledge documents a neighbour's session would
   need: what it owns and where, its contracts and data, and the computations others depend on, each fact at its
   place in the code and each unconfirmed one said. Then the brief, the documents, the safe work and the checks.
   It writes only in its tree, on its branch; it never pushes, publishes a quest, runs `connect` or `upstream`,
   declares a join, or changes code. *Not registered here* is said by what the press finds, each refusal with its
   door. The press's rule is exact `daoris` verbs.
4. **Registration follows the line.** The driver registers a repository from `daoris.json` and `daoris.lanes.json`
   read on its line as git objects, sending what `connect` would send, for the checkout's root and never a tree. It
   reads after Daoris moves a line (a `merge` landing, *Bring up to date*'s fast-forward), once at start, and on the
   person's press, and says every refusal on the row.
5. **The workspace press is a plan of single quests.** One set-up open at a time by default and never the last of
   the `cap`'s slots; the repositories other work touches first; a pilot of two, after which the plan pauses until
   the person resumes it; pause, resume and stop on the screen, on `daoris-driver setup --workspace` and as Ask
   Daoris doors (D50, D110).
6. **Until a repository adopts, the service indexes its README** as the repository's own word, split at its
   headings, labelled by its path, and dropped once a lock exists.
7. **A session looks before it asks.** The driven instruction sends it to the quest and its files, its repository's
   documents, code and history, the workspace's knowledge and the checkouts it may read first. What they settle is
   decided; what they lean towards is taken and said in the close; only what no source holds and only the person can
   give stops the session. `autonomous-development` gains the same line in the canon's words.

**Why.** The two failures are separate. The instruction offered *a choice between options that is theirs* as a
reason to stop, and a choice the repository's own documents settle read as one. And the workspace held nothing for
a neighbour to find. A set-up is the repository's own act, carried by its own session and reviewed by its owner
(D32, D117), so Daoris publishes it rather than performing it. The install's copy of the CLI makes it possible now,
needs no network, and lets the press's rule be exact verbs where `npx` would be a runner the judge refuses (D122).
The registry is read from the line because the line is what a review reached. The plan paces the set-ups because
oldest-first under a cap of two would hold every slot for hours.

**Rejected** (the design's §10 has the full list):
- **Waiting for npm**: the arc on a press with its own unknowns, and even after it a runner rule and a fetch codex
  cannot make.
- **A machine path to the install's CLI in the quest**: it lands in whatever the session commits.
- **A global npm install, the account's `PATH`, or a single-file build of the CLI.**
- **The driver or the service running the doctrine commands into the repository** (D32, D46, D117 §9).
- **Registering from the session, at its `done`, by re-importing, or by reading every line at every tick.**
- **Publishing every set-up at once, a chain of set-ups, a set-up priority or a *deferred* status.**
- **The plan in `driver.json`**: a field only the driver uses, kept by two twins.
- **Set-ups asking their neighbours**, **indexing code as a baseline**, and **the README read after adoption**.
- **The instruction alone, or the canon alone**: each misses the sessions the other reaches.

**What it amends, when built.** D117 §6.1, §6.2, §6.3, §6.5 and §6.6, and its §9's rejection of *the install carrying
the CLI onto a session's path*, which is reversed; LAYOUT10 no longer waits on the first publish. D105 §2: npm
outside Daoris, the install's copy at the same version inside it. D121 §2.4: the environment carries `app/bin/`
first. D122 §3.9: the press's rule is `daoris` verbs. D79 and D83: what reaches the person narrows. The canon's
`autonomous-development` and the adoption playbook (local). Each row that builds a piece notes the amendment where
it lands.

**What the gates do not cover.** This change is documents only, and nothing is built. Its statements about today
were read from the code at `21787b8`: `TargetPrompt`, the planner and the driver's configuration, the intake's room,
`connect.ts`, `manage.ts`, `commands.ts`, the registry and its import, the registry module, the scanner, the
publish script and the prompt's tests. The workspace's numbers are the parent's measurement on the install; the long
turn's are the backlog's (COST1, METER1). Not measured: which shell each harness runs `daoris` from and whether it
finds it by its bare name, whether codex's sandbox runs a program outside the workspace, what one set-up costs, and
whether a real set-up's knowledge is true. During `0.0.x` every build answers `0.0.1`, so the version guard cannot
tell two builds apart until the first release. `verify` checks the log's shape and the design's links, and none of
these words.

**As built (WSSETUP9, 2026-10-01): a session looks before it asks.** `TargetPrompt.Asking` (`Adapters.cs`), which
the claiming, resuming and carrying-on instructions all compose, opens with §6.1's look-first paragraph, keeps the
paragraph on asking another repository, and ends with the narrowed stop. Two things the build settled:

- **The stop opens *Stop only for what no source holds and only the person can give*,** where §6.1 wrote *Stop for the
  person only for … only they can give*. The meaning is the same, and §6.2 keeps the phrase *only the person can give*
  as one of the three the tests hold.
- **The checkouts clause points to where the instruction lists them.** A declared write target is named in the
  boundary, below the paragraph, and not in the read-only list above it, so the clause says *listed above*, *listed
  below*, or *listed above and below*. With reading across off it is absent, and the look names no checkout.

Held by `AskAndWaitPromptTests` (five new cases, each seen failing first) and by the across tests unchanged. The old
words were searched in the desktop suites' `Process` half, `tools/*rehearsal*.mjs` and the web's `e2e/`, and none
names them. **Not covered**: whether a real session now settles what it would have asked, which only the pilot's
canary shows (WSSETUP12).

**As built (WSSETUP11, 2026-10-01): set-ups and parks, counted.** The machine log gains `session.parked {session,
kind, repository, workspace}`, `session.started` gains `workspace` and, for a set-up's session only, `setup: true`,
and `turn.ended` gains the turn's tokens (`input`, `cacheRead`, `cacheWrite`, `output`), its tool `calls` and its
context (`used`, `size`). `tools/usage-report.mjs` gains a *Set-ups* section, each set-up session's cost, and a
*Parks* section, per week by workspace beside the sessions started there. The machine-log design's §4 and §6 say
what each line measures and what each section reads. What the build settled that §7.3 left open:

- **The park is written where it is made, not from the attention watch.** `SessionLog` writes it as the service
  client moves a record into the state `SessionStates.IsParked` names, which only the driver does. The shell's
  watch lives in the modules, its first look is a baseline that drops a park made just before a restart, and the
  headless `--once` and `--until-idle` run none, so writing from it would undercount and need a modules change. Both
  read the one predicate, and `SessionLogTests` and `AttentionTests` hold the same rows.
- **The line names its workspace and its kind**, beside §7.3's session and repository: parks per week by workspace
  needs the first, and an intake's park (the person asked about an ask) is told from a driven session's by the
  second.
- **A set-up is known by its quest's title.** A quest carries no kind, and the press publishes an ordinary ask, so
  `SetupQuests` holds the three titles the press composes (D117 §6.2, §2.1–§2.2 here), and LAYOUT7's composer takes
  its words from there. The driver passes the mark with the open; the ledger is not told.
- **A set-up's cost is read from the log alone**, not the usage record: its context comes from each turn's usage
  reports at their high-water, the same reports the usage record keeps, so the report reads no file but the log and
  `usage.json` gains no reader.

**Not covered**: the driver's open passing the mark is reached only by a real tick (the `Process` half); its
decision and the client and log beneath it are held in the fast half, and LAYOUT7's family rehearsal runs the first
set-up. No week of *before* exists until an install carrying this runs.

**As built (LAYOUT7, 2026-10-01): the set-up quest, as §2 says.** `daoris-driver setup <repository> [--plan]` plans
from the registry, the driver's choices, the repository's line (D117's note says how it is read), the tools a child
finds, the open quests and the index, then publishes one ask to that repository through the ask door with `--to`, as
the person's, so no intake runs. The body carries §2.2's facts, §2.3's steps with the tool's check first and the
knowledge step after the sync, §2.6's bounds and §2.7's close, `daoris` and never `npx`. What the build settled:

- **Every refusal that applies is said at once**, in §2.1's order, so a person fixes them in one pass; only *not on this
  machine's registry* and *no checkout here* stop the reading, since nothing after them can be read. Each names its
  door. *Not driven here* for the pipe door names `daoris driver adapter <agent>`, the door that changes it, and applies
  to a repository the registry calls unadopted, as the planner judges it (D70); an adopter rides either door.
- **The tools are found as a child would find them**: `node` by Tools' resolution on the child's `PATH` (TOOLS5), asked
  `--version` and refused under 22; `daoris` on that same `PATH`, refused unless it prints a version. The probe is
  LAYOUT7's, since the refusals need it; WSSETUP3 still owes the install's `app/bin/` first on that `PATH`, so until it
  and WSSETUP2 land a press finds `daoris` only where the person put one, and refuses saying so.
- **The rule goes in before the ask, and comes out if the ask is refused.** The session a look starts right after the
  publish is handed it, since it cannot ask for it (D52); a press the service refuses takes back the rules it added and
  keeps any the person already had. `Bash(…)` only, §2.4's nine verbs (`SetupPress.Verbs`), each one the body asks for.
- **The ask's words are the title, a blank line and the body**, so the quest the service makes is titled by the first
  line, which `SetupQuests.IsSetup` knows. The ask's id comes from its whole words: a second press on the same day with
  nothing it read changed is the same ask, and the service says so; one after the line or the index moved would be a
  new ask, and the *set-up already open* refusal stops it while the first is open or taken.
- **What it says of itself is read from the checkout** (D77's `SelfDescription`), and the body says so; the index's
  entries come from `/api/entries`, counted by file; the neighbours are the registry's rows in the same workspace.
- **Ask Daoris owes it a door**: `HelpCoverageTests` holds the verb as owed to LAYOUT8's `setup` kind, and the room
  names it meanwhile.

**Not covered**: the Process-half case and the family rehearsal's set-up phase (D117's note); whether a real session
follows the body, which only WSSETUP12's pilot shows; `PowerShell(…)` rules, held for D122 §3.5's canary.

**As built (LAYOUT7a, 2026-10-01): the family rehearsal sets a repository up.** Its phase 17c registers a scratch
repository that has a README and nothing for agents, without adopting it, and drives the press over it through the
real host and driver. The doctrine tool its session finds is the workspace's CLI behind a launcher
(`tools/setup-kit.mjs`), first on the `PATH` the phase starts the driver with: the install's `app/bin/` is the
deployment rehearsal's to test (WSSETUP2). The phase's machine log is in a home of its own. The checks:

- **The plan.** It says both refusals at once, each with its door. Then it prints the line's commit, the layout, the
  agent, the landing and the tool's version, then the set-up's title and a body that promises that version, names
  the neighbours and asks for the knowledge, then the nine verbs. It publishes nothing.
- **The press.** It publishes one quest as the person's ask and adds the nine rules. A second press is refused while
  the first is open.
- **The session.** The protocol stub's set-up branch (`ACP_STUB_AGENT`) runs in its own tree. It runs each verb by its
  bare name, and the quest asks for each one. It commits, and it closes the quest done. The checkout is not touched.
- **The record.** `session.started` says `setup`, and a later ordinary session there does not. The usage report counts
  one set-up of two sessions, with its six calls.
- **The landing.** The work merges into the line, and a plan made after it reads the line as already set up.

`tools/setup-kit.test.mjs` holds the launcher, the reading of the command's output, and the stub's set-up branch
against a stand-in quest door.

**Found**: the protocol stub can end on a libuv assertion (`0xC0000409`) in `process.exit` after a `fetch` on Windows
with Node 24, on its ordinary quest path too. The driver concludes from the quest, so the record still ends
`completed`, with the exit noted.

**Not run in the branch**: the family rehearsal itself, which the parent runs at the merge. `verify` does not run
`setup-kit.test.mjs`.

**Built 2026-10-01 (WSSETUP8): the README as an unadopted repository's word** (point 6, design §5). The scanner
reads the root's `README.md` for a repository its layout reads with no lock (`RepositoryLayout.Locked`), after
every other reader. It splits at level-two headings with `MarkdownSections.Split`, and the part before the first
comes from `MarkdownSections.Preamble`, the text the splitter drops for a log. Each entry is local knowledge with
the path the disk spells. The choices §5 left open, each held by a test in `RepositoryReadmeTests`:
- **"No lock" is the scanner's own test**: no lock, or one it reads as none (not JSON, no entries, a target that
  leaves the repository), the same test that reads both roots. A manifest with no lock is mid-adoption and has
  declared nothing yet, so its README is still read. Once a lock is read, the next scan reads no README. The
  refresh replaces a repository's entries whole, so that scan's refresh drops them.
- **The part before the first heading keeps its title line.** For a README, `# Name` and the paragraph under it
  say what the repository is. It is titled *README* and has no anchor, so it points at the top of the file and its
  id is never a section's. A README whose sections are all level one is one entry, as a log's split would leave
  it. An empty section is skipped, and a heading used twice gets a count in its anchor (REV3).
- **Any case, one file.** The root's file named `README.md` in any case, labelled as the disk spells it, so a
  session can open it on a case-sensitive disk. Two spellings side by side, which only such a disk holds, read the
  first in ordinal order.
- **Read last, so a declaration wins.** A README that a manifest with no lock declares as its router or a log is
  read by that role's reader and not again. Read twice, the router's entry and the part before the first heading
  would share the id `<repository>:README.md`, and the store's primary key would fail the refresh (REV3).
- **Rejected**: splitting at every heading level, which cuts a section's subsections into entries too small to
  answer anything; titling the first part by its `#` heading, since §5 names it *README* and the heading stays in
  its body; reading `README.markdown`, `README.rst`, `README.txt` or `README` with no extension, or a README
  below the root.
- **Not a twin** (design §12): the driver's `SelfDescription` reads the same file for the intake, a title and a
  paragraph, and neither is held to the other.
- What the gates do not cover: no refresh of a real workspace has read a README on this branch, so whether its
  sections help a neighbour's search is WSSETUP12's canary. The real-link test made a link on this machine. On a
  machine that makes none it returns early, and the held-as-text rows hold the rule. Two spellings side by side
  cannot exist on this machine's disk, so that rule is untested. The family rehearsal's unadopted repository (17b)
  now has its README indexed. No check there reads its knowledge, and the rehearsal was not run on this branch.

**As built (WSSETUP2, 2026-10-01): the install carries its doctrine tool.** `tools/desktop-publish.mjs` runs `npm
pack` in `src/Daoris.Cli`, asserts the pack's staging gone as the release rehearsal does, and `layCli` unpacks the
tarball with the CLI's own tar reader (`tarball.ts`) and writes the two launchers. Both folders are staged beside
and swapped in whole. What the design left open, settled here:
- 🔴 **The package lands in `app/cli/node_modules/daoris/`, not in `app/cli/` itself.** The CLI reads the canon it
  ships only when its own folder sits under `node_modules` (`resolveCanonRoot`). Unpacked straight into `app/cli/`, it
  takes itself for a development checkout and reads `<install>/canon`: a folder nothing publishes, or a neighbour's
  under `--beside`. Laid out as npm lays a package out under a prefix, the package finds its canon by the rule that
  already holds everywhere it is installed, so the CLI needs no second case. The launchers run
  `../cli/node_modules/daoris/bin/daoris.mjs`. `desktop-publish.test.ts` holds the layout to `resolveCanonRoot`.
- **The launchers run bare `node`**, the one the caller's `PATH` finds, and say so with exit 2 when there is none.
  `daoris.cmd` is CRLF and both are ASCII, since cmd.exe reads a batch file in the console's code page. The script
  turns its folder into a Windows path with `cygpath` where one exists, as npm's shim does.
- **What the layout refuses**, before anything is replaced and naming why: a tarball whose root is not npm's
  `package/`, a package not named `daoris`, one without its bin entry, `dist/cli.js` or `canon/canon.json`, a canon at
  another version than the package, and `src/`, which only the source tree carries. The publish also refuses an
  application build that carries a `cli` or `bin` folder, which the tool's folders would replace.
- **Measured on the development machine, against the real pack laid out by `layCli` in a scratch folder (not a
  publish):** Command Prompt runs `daoris.cmd` (`where` lists the extensionless script first, and cmd.exe runs the
  batch file); PowerShell's `Get-Command daoris` answers `daoris.cmd` by `PATHEXT`; Git Bash's `command -v daoris`
  answers the script. All three print `0.0.1` and hand back exit 2 for an unknown verb, and `init`, `sync` and `check`
  ran clean in a scratch repository. The deployment rehearsal's phase 8 holds the same against a published install.
- **Not run by this row:** the deployment rehearsal (phase 1 reads the layout back, phase 2 the republish, phase 8
  runs the tool from each shell); that is the parent's at merge. Nothing puts `app/bin/` on any `PATH` yet: that is
  WSSETUP3. The desktop README's install paragraph does not mention the tool yet, since this row could not touch the
  desktop tree.

**As built (WSSETUP4, 2026-10-01): an older tool never rewrites a newer lock.** `lockversion.ts` holds the rule:
`newerLock` compares the lock's canon version with the canon the tool carries by number (`compareVersions`, so
`0.10.0` follows `0.9.0`), and `refuseNewerLock` throws exit 1 naming both and the command at the lock's version,
`npx daoris@<locked> <the command as given>`. What the design left open, settled here:
- **`sync` refuses before anything is planned, in every mode.** A dry run answers with the same refusal, since it is
  how a person asks whether `sync` would refuse; `--force` does not pass it, since it discards local edits, which is a
  different question from discarding a newer canon's text. The state space D19 enumerates assumes the canon is not
  older than the lock.
- **`upstream` refuses it too, one file or `--all`**, reading the version from `canon.json` alone. A canon with no
  version to read is left to the refusals that already name it.
- **`status` says it instead of offering an update.** It said *canon 0.0.1 available (lock has 0.0.6) — run 'daoris
  sync'*, sending the person to the command that now refuses. It prints a `newer lock` line naming both versions and
  `npx daoris@<locked>`, and `--json` carries `newerLock` (`locked`, `carried`, `run`) with `update` null.
- **`check` says nothing of it**: it reads the lock and the disk and never the canon (D8), so it cannot know.
- **Held by** `newer-lock.test.ts` (each case failing first: the older tool synced, returned 0 and rewrote) and the
  release rehearsal's phase 5 (f): the packed tool, which carries canon `0.0.1`, run on the consumer the phases above
  moved to `0.0.6`. That phase was not run by this row; its checks were run by hand against a fresh pack of this
  source. During `0.0.x` two builds both answer `0.0.1` and the guard cannot tell them apart, as D124 says.

**As built (WSSETUP3, 2026-10-01): `daoris` on every child's `PATH`.** TOOLS5's one environment puts the install's
`app/bin/` first, before the tools' folders, in both twins: the driver's `Tools.ChildPath` (`InstallBin`) and the CLI's
`childPath` (`installBin`), so every start already handed the tools' environment (a driven session on either door, a
conversation, an intake, a hook, a landing plugin, the terminal's shells) finds the install's `daoris` by its bare name.
What the design left open, settled here:
- **"Where it exists" is the home's sibling `app/bin/` being a folder.** In an install the home is `data/`, so the
  folder is the install's own; a home anywhere else has none beside it, and nothing is added. No file names it, so no
  setting can point a child at another `daoris`: it is Daoris's own program, not a tool (§1.3). An `app/` with no `bin/`,
  an install from before WSSETUP2, and an `app/bin` that is a file add nothing.
- **Found from the home, not from the running application.** Every child's environment is built from the home it is
  handed (`Tools.Hand`), and the headless host and the CLI have no application folder; the offers and the list built in
  look beside the application first only because the application reads them itself.
- **Byte for byte with no install.** The tables' earlier rows are unchanged, and a new table holds the install's rows
  in both twins, cell for cell (`ToolsChildrenTests`' `A_childs_PATH_begins_with_the_installs_doctrine_tool_beside_the_home`
  and `tools-children.test.ts`' `INSTALL_ROWS`); each new row was seen failing with the folder taken out of each side.
  `desktop-publish.test.ts` holds `INSTALL_BIN` to the publish's `CLI_BIN` and the driver's `InstallBinLayout`.
- **The press's facts were LAYOUT7's already**: `SetupTools` carries `node` with its version and `daoris` with what it
  answered, read on the `PATH` a child starts with, so the press now finds the install's. Nothing else was owed.
- **Not covered by a gate run here.** `ToolsChildProcessTests.A_sessions_daoris_is_the_installs_beside_the_home`, in the
  `Process` half, lays stub launchers in an install's `app/bin/` and wants a stub driven session's own shell to answer
  `daoris --version` with them. It was written and not run (MOD8). Whether each real harness's shell finds `daoris` by
  its bare name stays §9's fourth item, for the pilot.

**As built (WSSETUP5, 2026-10-01): registration follows the line.** `LineRegistration.Compose` (the driver) composes what
`connect` would send from `daoris.json` and `daoris.lanes.json`, a twin of `connect.ts`'s `registration()` and the reads
before it, held by `LineRegistrationTests` and `connect-twin.test.ts`, one table cell for cell. `RegistrationFollow` reads
the two files on each repository's line as git objects (`LineDeclarationReader`, as `LayoutReader` reads the layout), for
the row's own root, and sends through the registry door. What the design left open, settled here:
- **"After Daoris moves a line" is a record, not a call.** `SessionTrees` writes `<home>/lines-moved.json` where a line
  moves, its merge and its fast-forward, so a press from the screen, a terminal or Ask Daoris is followed without each
  door knowing the service. The next look follows each moved line before it reads the registry, so a set-up that just
  landed is planned by what it declares; `trees land` and `trees sync` at a terminal follow at once. A move made while no
  loop runs is followed when one does.
- **"Once at start" is a watch's**: the shell's loop and `daoris-driver drive`, beside its first looks on the pool, since
  a workspace's lines are seconds of git and no start should wait on them. It is said in the next look's report and
  tried again until the service answers. `--once` and `--until-idle` follow only the lines Daoris moved.
- **Sent only where the row holds something else**, read as the service stores it: adopted, the words (a blank summary
  is none), `uses` by its rule, the packs, the join and the knowledge as the service narrows them, and the lanes' words.
  `ServiceClient.RegistrationsAsync` reads the whole row for it.
- **"The declaration left as the row held it" sends the row's own declaration back**, since the registry door replaces
  the declaration whole; the packs, the remote flags and the lanes are the line's. A row that already records adoption
  and holds the same is sent nothing again.
- **Where each refusal is said.** `<home>/registry-followed.json` keeps each repository's outcome, its sentence, the line
  and the commit, for the row WSSETUP7 draws; `registry.followed {repository, outcome}` is the machine log's line, written
  by `SessionLog` from the client's `RegistryFollowed` event. A look or a terminal says what was sent and what must be
  fixed; a standing state (unchanged, not set up, no line, no checkout here) is kept and logged, and not said at every
  start. *Not set up* names a set-up's landed branch still standing (the landings record, by its quest's title).
- **Shapes the service would refuse are refused first**, naming the field (a domain that is not an object, a summary
  that is not text, a list that is not of text); the layout's fields are neither sent nor judged. A root that is a linked
  worktree is refused by `LinkedWorktree.MainOf`, `linkedWorktreeMain`'s twin, and a root not on disk asks git nothing.
- **The refresh is asked once per pass**, when anything was sent, and a refusal of it is said.
- **The doors.** `daoris-driver register [--repository <name>]` (exit 1 only for a repository named and refused), and the
  modules' `REGISTRY_REFRESH` {repository} in `DriverModule.Registry.cs`, the row's *Refresh*, whose button and bridge are
  WSSETUP7's. Ask Daoris's `register` door is owed to WSSETUP7 (`HelpCoverageTests`), and the room names the terminal's.
- **Not covered by a gate run here.** `RegistrationLineProcessTests`, in the `Process` half, lands a manifest and a lanes
  file by a real `merge` landing, moves the checkout to another branch with other words in flight, and wants the line's
  declaration registered for the checkout's root with one refresh. It was written and not run (MOD8). The family
  rehearsal's check (a set-up under `merge` reads adopted and declared with no `connect`) is the tools lane's and not
  written. Whether a real *Bring up to date* after a real merge registers what a set-up declared is §9's sixth item.

**As built (WSSETUP6, 2026-10-02): the workspace plan** (point 5, design §4.1–§4.3 and §4.5's terminal).
`daoris-driver setup --workspace <name>` previews and presses (`WorkspaceSetup.PreviewAsync`, `PressAsync`): it writes
`<home>/setup/<workspace>.json` (`WorkspaceSetupFile`, named by the intake room's rule) and adds the nine verbs once, at the
workspace's scope. Each look works every plan that is live and not paused (`WorkspaceSetup.TickAsync`), publishing the next
to go as the single press does (`SetupPress.PlanAsync`, then `PublishAsync`, the ask without a rule). What the build settled:
- **The tick works the plans before the look reads the quests**, after it follows the moved lines, so a set-up it publishes
  is planned and started in that same look. A home with no working plan reads nothing.
- **Each state is read from the facts.** *Set up* is the registry door's own `registered` word (the service's `Registered`:
  adopted, and a summary, an area owned or a kind of work accepted), read onto `RegistrationRow.Registered` and never
  recomputed, so there is no twin to keep; absent is no. A set-up quest open or taken, from any door, is *setting
  up*, or *parked* by the planner's strikes rule. One closed done is *waiting for your review*, one declined is *declined*,
  and one the plan published that is gone is *deleted*. Else the plan's own record says *skipped*, or it is *to go*. A
  set-up the single press opened holds its turn and a slot, so no repository is asked twice, and the press's own `open`
  refusal is the second guard.
- **A parked set-up frees its slot, and has not closed.** It holds no session and waits on the person, so the plan moves on.
  The pilot waits for it.
- **`atOnce` is held to `cap − 1` at every tick**, whatever the file says, and to one at a cap of one. The press refuses more,
  naming `daoris driver cap`.
- **The pilot is the first `pilot` set-ups the plan published**, in its order; a skipped repository is not one of them.
  Nothing more is asked until all of them have closed. Then the plan writes `paused` with `by: pilot` once, says it in the
  look's report and the log, and a resume sets `pilotResumed`, so the pilot never pauses it again.
- **A skip keeps the refusal's code and its sentences**, and the repository is not judged again until the person resumes the
  plan, since a resume clears `skipped`. An ask the service refuses is skipped as `service-refused`.
- **A refusal of the machine's tools pauses the plan** (`by: tool`) rather than skip every repository in turn, and the press
  refuses up front on the same refusals.
- **Stop marks `stopped`.** The quests the plan published stay, and the terminal names each one still open that nobody has
  started, with `daoris-driver quest delete <id>`. A new press replaces a stopped plan, never a working one.
- **The order is read at the press, never at a tick.** It counts quests of any status addressed to the repository by anyone
  but itself, set-ups left out; an ask's quest counts whichever tier routed it, since the quest does not say. It counts reads
  from each session record's conversation record (`locations` on tool calls) by another repository's session, each call
  once, with the deepest root owning a path. It counts sessions from the ledger's records, this machine's alone. A row with
  no checkout here is left out of the order, and the press says so.
- **The lines** are `setup.planned`, `published`, `skipped`, `paused`, `resumed` and `stopped`, from one catalogue
  (`SetupLine`). The service client raises them (`SetupLined`) and `SessionLog` writes them. A look's report carries a
  `setup  <workspace>: …` line for each publish, skip and pause.
- **The doors.** The terminal is built. Ask Daoris's `workspace`, `pause`, `resume` and `stop` doors are owed to WSSETUP7
  (`HelpCoverageTests`), and the room names the terminal's forms meanwhile. The screen and its modules route are WSSETUP7's.

**Not covered by a gate run here**: the real world (`WorkspaceSetupWorld`: the git reads, the tools' versions, the service's
doors), which only a real tick or terminal reaches. The family rehearsal's check (two scratch repositories set up one at a
time by the stub, pausing after a pilot of one) is the tools lane's: written as phase 17d (WSSETUP6a, below). Whether one set-up at a time keeps
pace with the owner's review is §9's fifth item.

**As built (WSSETUP5a, 2026-10-02): the family rehearsal checks registration from the line.** Phase 17c reads atlas's
row through the host's `/api/registry` before its landing and after it. Before, the row is not adopted and declares
nothing, though the set-up is done. After, `trees land` has printed `registry  atlas: registered from its line …` at the
landed commit, and the row reads adopted and declared, with the summary, `owns` and `accepts` of the `daoris.json` on
the line, for the checkout's root. Nothing in the phase runs `connect`. The machine log, read through `logs --event
registry.followed --json`, holds one line for atlas, `registered`, carrying the name and the word and nothing else. Then
`daoris-driver register --repository atlas` finds it `unchanged` and registers none, the row's declaration is as it was,
and the log's second line says `unchanged`. `tools/setup-kit.mjs` gains `readRegister` and `readFollowed`, held in
`tools/setup-kit.test.mjs` against output spelled as `RegisterCommand` and `MachineLog` write it.
**Proven without the rehearsal**: the checks were cut out of the phase and run against stand-ins answering in the host's
and the driver's formats. 31 breakages, each of one claim, were each caught by the check that owns the claim.
**Not run in the branch**: the family rehearsal itself, which the parent runs at the merge.

**As built (WSSETUP6a, 2026-10-02): the family rehearsal sets a workspace up one repository at a time.** Phase 17d
registers two repositories that hold only a README, without adopting them, in a workspace of their own. Both are drivable,
each in a tree of its own, on the protocol stub, at a cap of two. Its checks:
- **The list.** `setup --workspace meridian --plan` lists both, each to go and refused nothing, one at a time, with the
  nine verbs a press adds. It writes no plan, publishes no quest and adds no rule.
- **The press.** `--pilot 1` writes `<home>/setup/meridian.json` with both, one at a time and a pilot of one, and publishes
  nothing. The nine verbs land in `permissions.json` under `workspaces.meridian.allow`, and under neither repository.
- **The pilot.** `drive --until-idle` publishes the first in the plan's order and carries it to done in its own tree. It
  does not ask the second.
- **The pause.** A look then finds the plan paused by `pilot`. A look's report says so, and the log holds one
  `setup.paused` with `by: pilot`.
- **The rest.** `--resume` carries the plan on past its pilot, and `drive --until-idle` sets up the second. `logs --event
  setup.published` shows two lines, the pilot's first.
- **Registration.** Both land under `merge` and are registered from their lines, adopted and declared, still in their
  workspace, with no `connect`. The plan then reads `Setting up — 2 set up`.
Last, the plan is stopped, so no later look works it, and both repositories are retired. `tools/setup-kit.mjs` gains
`readWorkspaceSetup` and `readEvents`, held in `tools/setup-kit.test.mjs` against output spelled as
`WorkspaceSetupCommand` and `MachineLog` write it.
**Proven without the rehearsal**, as WSSETUP5a's were: the phase was run against stand-ins in the formats WSSETUP6's code
writes, and 44 breakages, each of one claim, were each caught by the check that owns the claim.
**Not run in the branch**: the family rehearsal. This branch does not carry WSSETUP6's code: the phase was written from
that branch's sources, read and not merged, so it needs WSSETUP6 merged first.

*Built by WSSETUP10 (2026-10-01): §6.3, the canon's line.* The canon's `autonomous-development` gained *Look before
you ask*, with an entry under the canon changelog's Unreleased, and this repository and both examples were re-synced in
the same commit. Its archive entry has the rest.

## D123 — A long entry is embedded whole, in pieces the deployment's window bounds; its best piece speaks for it, and a refresh says how many were split (2026-10-01)

**Decision (SEM3, found upgrading Lyntai to 3.5.3, LYN1).** The semantic tier embedded an entry's title twice and
the first 2,000 characters of its body, and dropped the rest without saying so. An embedder then cuts whatever
passes its own context the same way: Ollama's embed endpoint does it silently. So a search by meaning could not
find what a long entry says past its opening, however close the meaning. Lyntai 3.3 offers `MaxInputChars` and
`Segmentation` on its providers. Daoris segments instead, in Core, one vector per piece.

### 1. Measured first

- **The fixture** (`LongEntryTests`): a knowledge document whose only statement of a fact sits past character
  2,000, and a short decision that mentions the meaning once among six build words. The vectors are the
  deterministic stand-in's, and the query shares no word with either entry, so the order is the semantic half's
  alone. **Before**: the document's one vector held build words only (cosine 0), and the decision (0.164) took the
  one place. The test failed as the reading predicted. **After**: the piece that holds the fact scores 0.316, and
  the document is first.
- **The corpus**, scanned by the service's own scanner (a scratch probe, not committed). This repository holds
  636 entries and 1.61 million characters of body. 293 entries (46%) were longer than 2,000 characters, and
  562,000 characters (35%) never reached a vector. The longest is a decision of 17,518 characters. The medians by
  kind: a decision 3,378, a knowledge document 6,839, a task outcome 1,890, a fix 1,466. Each example repository
  holds 21 entries, all canon; 15 were longer, 44% of their text never reached a vector, and the longest is the
  canon's `development-documents` at 8,662.
- **Against a typical window**: a small embedder takes 512 tokens, about 2,000 characters of English and fewer of
  code or 中文. A large one takes 8,192 tokens, about 30,000. The longest entry fits the large one and is nine
  times the small one.

### 2. What an entry becomes

- **Every part of its body is embedded, in pieces no longer than the window** (`EntryPieces`). A piece ends at the
  last blank line in the latter half of the window, else a line break, a sentence end, a space, or the window
  itself, never inside a surrogate pair. The next piece starts at a line or sentence inside the last 15% of the
  one before, so a sentence a cut falls through is whole in one of them. Each piece is led by the title: twice, as
  the entry always was, or once where two would take more than half the window. An entry within the window is
  one piece, the same text as before.
- **The window is the deployment's** (D24): `DAORIS_EMBED_WINDOW`, the most characters one embedded text carries,
  the title included. Unset, it is 2,000, the number the code already cut at, now a statement rather than a
  silence. Below 200, or not a whole number, both hosts refuse to start, as for `DAORIS_MODE`. Characters only
  approximate tokens, so a deployment leaves margin for code and for 中文.
- **Each piece is its own vector, and its payload is the entry's id.** A search ranks pieces and **names an entry
  once**, at its best piece's score. It reads further while pieces crowd out the places it was asked for, so a
  long entry does not cost the answer its other entries. A hit found in a later piece shows that piece's passage
  (the vector's id carries where the piece starts), so a reader can see why it matched.
- **A refresh embeds everything first, then replaces the collection whole.** An embedder that fails part-way
  leaves the previous vectors as they were, and an edited entry's old pieces and a deleted entry's vectors leave
  with them: an entry is several ids now, so writing into the collection would let a stale piece be found for
  words its entry no longer has.
- **A refresh says what it embedded**: entries, the vectors they became, how many were split, at what window. The
  agent's door (`knowledge_refresh`) says it in a sentence, and the HTTP door's refresh answer carries it as
  `embedded`.
- **Convergence** seeds from an entry's first piece, as before, compares it with every piece, and names each
  neighbour once.
- **The lexical tier is unchanged.** It always read the whole body.
- **The cost, measured**: at 2,000, this repository's 636 entries become 1,267 vectors, 322 entries split, at most
  14 pieces for one entry, and 1.70 times the characters the old cut sent. Of that, 1.49 is the text it dropped,
  and the rest is the title on each piece and the overlap. At 8,000 it is 663 vectors. It is paid once per process
  (SEM1), which raises what SEM2's trigger would measure.

### 3. Rejected

- **Capping**: embed the first window and say in the record what was cut. Honest, and still blind. The fact past
  the cut stays unfindable by meaning, and the measure says that is a third of this repository's text.
- **The provider's segmentation** (Lyntai 3.3's `MaxInputChars` with `Segmentation`). It splits an input and
  returns its pieces' unit vectors averaged by length, one vector per input. That names an entry once by
  construction, and dilutes a fact in one piece by all the others. On the fixture the pooled vector scores 0.111,
  below the decision's 0.164, so the document would still be missed, and the dilution grows with the length it
  is meant to fix. It is also invisible to Daoris, since the answer carries one vector and no plan, so the record
  could not say an entry was split. It exists only on the HTTP and Ollama providers, when a deployment sets it,
  and Daoris's own cut sat in front of it.
- **The provider's bound as a guard**: `MaxInputChars` set to the window, so Ollama is sent `truncate: false`
  and a piece that still overflows the model fails the call instead of being cut. It is the right shape for a
  window set too wide, and it is not taken yet. It turns one overflowing piece into a semantic tier that does not
  answer, and choosing its default needs a machine that runs an embedder, which none here does (SEM2's trigger).
- **A cap on pieces per entry** (Lyntai's `MaxPiecesPerInput`): a cut by another name, with gaps in what is
  covered. The cost is measured instead (§2).
- **Storing a piece's text in the vector store**: the payload stays the entry's id, so the text lives in the store
  alone and the two cannot disagree. A piece's passage is read back from the entry by where it starts.

### What the gates do not cover

The splitter, the window's parsing, the search, the refresh's report, the replacement, the excerpt, convergence
and the agent's sentence are held in the service suite (`EntryPiecesTests`, `LongEntryTests`, `McpToolsTests`), all
over the stand-in embedder and no network. A mutation of each of the dedupe, the read further, the replacement and
convergence's dedupe was seen failing them. The HTTP host's refusal of a bad window is held by `StartupTests`; the
MCP host's identical refusal is not run by any test. The HTTP door's `embedded` field is not run with a model,
because no HTTP host test has one. **No real embedder has embedded a piece**: whether 2,000 characters fits a
given model's context, and what segmenting does to recall on the real corpus, need a machine that runs one. The
page shows `semanticError` from a refresh, and since SEM3b `embedded` too, in the refresh's notice beside the count;
Settings → AI names `DAORIS_EMBED_WINDOW` beside the model and the address. A refresh with no `embedded` says nothing
of embedding.

## D125 — An account's limit is read from the agent's own words, cools that account until the reset it names, and is never a strike; rotation moves the next start to the next account of the person's order (2026-10-02)

**Decision (TOOL4).** The owner, 2026-10-01, continuing after their own assistant hit its weekly limit and they signed
in to another account: *"this is also good to test for account switch"*. D57 §b held rotation until real output
answered three questions. Five observations now do (design §0.2): Claude Code's ACP adapter refusing `session/prompt`
in driven sessions on 27 and 29 September and 1 October, and the same maker's CLI refusing with an HTTP 429 outside
Daoris on 1 and 2 October. Every one is one sentence shape, *You've hit your <what> limit · … · [your <window>
limit ]resets <when> (<zone>)*, and every one names its reset: a time of day within a day, a date when days away. On
1 October a driven quest was carried on twice into the same refusal, three events each, and parked on its strikes in
seconds; and the person's sign-in to another account at their terminal moved Daoris's next session with it, since the
session named no account. The contract is `docs/2026-10-01-account-rotation-design.md`.

1. **The signal is the door's failure, read by a table.** A turn the harness refused, on a door that carries the
   refusal apart from the agent's words (today the protocol door's JSON-RPC error; the native door's failed `result`
   once recorded), whose words an entry on the agent's toolchain recognises. An entry grows only with a recorded
   sentence, held by a test both ways. Never the transcript or the agent's output, never a provider query, never token
   counts; a field beats a sentence where a door carries one.
2. **The cool-off is the reset the agent named**, read in its zone (the machine's own when the zone is not an IANA
   name), plus a 2-minute margin, with a 15-minute grace for clocks that disagree and nothing believed past 8 days.
   Otherwise 60 minutes, settable (`cooloff`). Kept per account in `cooling.json` under the home, on disk, with no words
   and no route; ended early only by *Try now*, a sign-in or key into that account, and, for the tool's own home, the
   roster's refresh.
3. **A limit is not a strike** (amends D58). The record says `limit`, naming no account. The quest waits for the reset
   or rotates, and is never carried on into the same refusal nor parked for it.
4. **Rotation is at a start, by the person's order.** `rotation` and `workspaceRotation` in `harnesses.json`, resolved
   workspace, then machine, then none; none is today's behaviour. The resolved account runs if ready; if not, and a
   default named it and the order lists it, the next ready account in the order runs. A person's pick, the tool's own
   home, an account outside the order and a running session never rotate. A carry-on rotated keeps its tree and is
   handed the cut-off session's last plan and last words from Daoris's own record (amends D80); the harness's own
   conversation stays in the first account's home, never read.
5. **Rotation needs accounts of Daoris's own.** Daoris chooses an account only by its directory; the tool's own home is
   whichever account the person last signed in to, which Daoris neither chooses nor sees change. The screen says so
   while starts run there, and offers *Sign in to another account*.
6. **When every account a start may use is cooling, starts wait**: held at spawn with one sentence naming the first
   reset, no process started, said once by the attention and *What needs you*, the queue unchanged.
7. **Reproducible from Daoris's records.** Each record names its account (loopback only) and version; the carried-on
   session's conversation opens with a note naming both accounts, the cut-off session and its refused turn; the log
   writes `account.limited`, `account.rotated` and `starts.waiting` by profile name, never a key, a handle, who signed
   in or the agent's words. The travelling note never names another account, since the scrubber knows only the
   record's own.
8. **Two doors and Ask Daoris** (D50, D110): `daoris agent profile order|ready`, `daoris driver cooloff`, `daoris agent
   list`'s cool-offs, and the same on Settings → Agents and → Driver.

**Why.** The agent says when its limit lifts, so the cool-off is read rather than guessed, and D57 §b's worry, a string
match on someone else's text written blind, is answered by writing it against five recorded sentences and refusing an
entry without one. A strike says trying again spends an account without progress; a limit is the account's state with
its own reset, so counting it parked a healthy quest in seconds. Rotation at a start, by an order the person wrote,
moves only work that would have started anyway, and the records already name each session's account, which is what
makes rotated work reproducible. The tool's own home cannot be rotated because Daoris cannot choose it: the 1 October
sign-in showed it moving under Daoris unseen.

**This lifts D57 §b's hold on TOOL4**, because the evidence it waited for exists: what a limit looks like (§0.2), how
long a cool-off is (the agent says), and whether rotated work stays reproducible (yes, from Daoris's records, for named
accounts). The toolchain design's §2 part 4 and §6 are replaced by the new design.

**Rejected** (the design's §9 has the full list):
- **Reading the transcript or the agent's output**: a session that quotes a limit would cool an account not spent.
- **Asking the provider what is left, guessing from token counts, or a 429 reader**: a credential and an API Daoris
  holds none of (D49 §4), what was used is not what is left, and no door of Daoris's sees a status.
- **One cool-off for every limit, or exponential backoff**: the reset is stated.
- **Spreading work across accounts**, **rotating inside a running session**, and **copying the harness's conversation
  between account homes** (a read inside an account's directory, D66 §3).
- **A limit counted as a strike, or a separate count that parks after some number.**
- **The cool-offs in `harnesses.json` or in memory**; **a do-not-rotate list beside the order**.
- **Rotating the tool's own home, giving its sessions a profile silently, or keying its cool-off by who signed in.**
- **Sleeping the loop or reordering the queue while it waits**; **`StartVerdict.Exhausted` for a cooling account**.

**What it amends, when built.** D57 §b (lifted), D58 (a limit is no strike), D80 (a carry-on gains the last plan and
last words), D94 §4 (three events), D110 (three doors), D66 §3 and AGT3b (an account action ends that account's
cool-off). Unchanged: D49 §4, D48 §2a, D67 §1, D73. Each row that builds a piece notes the amendment where it lands.

**What the gates do not cover.** This change is documents only, and nothing is built. Its statements about today were
read from the code at `4f65cd3`: the conclusion and the protocol door's failure, the native door's mapper, AGT3b's
refusal and the roster, the profile resolution and its environment seam, the planner and the strikes, the attention
watch, the usage record, the conversation runner, the service's record and its note scrubber, the CLI's toolchain and
`agent` usage. The observations are the install's and the owner's, given to the branch, not read from its records. Not
measured: the protocol door's words for a bare weekly limit, the native door's words, whether a reset lands at its
minute, whether a carried-on session on another account finishes, whether an hour is a good default, and whether a
quest spends windows without landing anything. Found and not filed: AGT3b reads the transcript's last lines for its
phrase, the hazard this decision refuses for limits; and the ACPEND1 note carries the agent's sentence, zone included,
to every reader. `verify` checks the log's shape and the design's links, and none of these words.

**Built 2026-10-02 (TOOL4a): the limit table and its reader** (points 1 and 2, design §1.3, §1.5, §2.1).
`HarnessToolchain.Limits` is a `LimitWords` entry: its markers, its resets, and the recorded sentences they stand on.
Claude Code declares `ClaudeLimits.Words`, and the stub declares the same entry, as it mirrors `Refused`.
`AccountLimits.Read(entry, failure, seen, machineZone, coolOff)` in `AccountLimits.cs` is the one reader, and it is
pure. What building it settled, each held by `AccountLimitsTests`:
- **An abbreviation is the machine's zone on every platform.** Asking the platform whether a name is a zone was not
  enough: on Windows, ICU resolves `PST` as a three-letter alias of its own, and Windows resolves its own ids
  (`Pacific Standard Time`). So a zone is read as named only when it has the IANA shape (`Area/Location`, or `UTC`)
  and the platform resolves it. `PST`, `CST`, `EST` and `Pacific Standard Time` are the machine's zone, said as
  assumed. `EST` is a legacy name in tzdata, and is read as the abbreviation it is used as.
- **`LimitSeen` says when a date was not believed** (`NotBelieved`), apart from a sentence that named no time, since
  §2.1 says the default is *said so*. Both are `Stated: false`.
- **The grace looks back across midnight**: a reset at 11:55pm seen at 00:05 is yesterday's, landed, and waits the
  margin.
- **The grammar reads** a twelve-hour time (`7am`, `7:50am`, `12am` as midnight), and a three-letter English month, a
  day and such a time, the comma optional. A 24-hour clock, a full month name, an impossible date or hour are not
  read, and take the default.
- **The reset is a clause after the marker**, as every recorded sentence has it.
- **Observation 2's marker clause is written, not quoted.** The record given to the design kept the note's start and
  the reset and elided the rest. TOOL4's row names it the spend-limit refusal, so it is written in observation 1's
  words, and its provenance says so. Observation 4's elided middle is kept as `…`, a clause that matches nothing.
- **The both-ways check is the test's.** Each marker and reset is matched by a recorded sentence, each recorded
  sentence by a marker, and an entry holds one at least. A test feeds it an unproven marker, reset and sentence and an
  empty entry, and it refuses each. A recorded sentence carries `<zone>` once and no zone of its own.
- **For TOOL4d**: nothing reads `Limits` yet. `acp-stub` declares no toolchain, so no limits: a limit gated over the
  protocol door with no account needs that door to read the stub's entry as its owner's (AGT7), or to declare one.

**What the gates do not cover.** How Claude Code prints the zone: the five sentences were given with it elided, so
whether a real one is an IANA name, and so read as named, was not seen. The default as a setting is TOOL4e's.

**Built 2026-10-02 (TOOL4c): the record says a limit** (point 3, design §5.2). `Session.Limit`, as D104's
`Interrupted` was added: the ledger takes `limit` only on a move to `failed` and refuses it on any other, naming
`failed`; the store keeps it once said, in a column `SchemaColumns` adds, so a record from before reads false. What
building it settled:
- **The column is `limited`**, since `LIMIT` is SQL's own word. The record, the HTTP door and the wire say `limit`.
- **It travels, where `Interrupted` does not.** It names no account, so `ToSession` answers it to every caller, the
  feed carries it up and a page down, and a mirrored record keeps it on its first copy and each later one. The wire
  says `limit` only where it is true, so a feed or a page from a build before the field reads false on both sides.
- **The ledger still opens the carry-on** of a taken quest whose last session here failed on a limit (D80). The
  wait for the reset, and passing a limit record in the strikes, are the driver's at spawn (TOOL4d), and the
  client sending `limit` with them.

**What the gates do not cover.** Nothing sends `limit` yet: the driver's client is TOOL4d's. The page shows nothing
new (TOOL4g), and no rehearsal was run by this change.

**Built 2026-10-02 (TOOL4d): the cool-off and the hold** (points 2, 3, 6 and 7; design §2, §4, §5.2, §5.4). What
building it settled, each held by `AccountCoolingTests`, `AccountLimitHoldTests` and `StrikeTests`:
- **`cooling.json` sits beside `harnesses.json`**, in the roster's home, as the accounts and their keys do
  (`AccountCooling`). An entry is `until`, `stated`, `window`, `seen` and `session`, with `assumedZone` and
  `notBelieved` written only when true, every moment UTC to the second. A moment is read as ISO 8601 and nothing else:
  a lenient parse read *Oct 3* as this year's, which would make a hand-mangled entry an account cooling. One writer at
  a time in the process; the next write drops a passed or unreadable entry and keeps what it has no field for, since
  TOOL4e's CLI reads and ends entries too.
- **The failure read is the door's** (`turnFailed`, ACPEND1), through `HarnessRoster.Limited`, which reads the door's
  own entry or else its owner's (AGT7). A driven session and an intake read it at the conclusion, for `failed` alone
  (`Driver.AccountLimited`): the record moves with `limit`, which the client sends only when true, and its note gains
  *The account it ran on is cooling until Oct 3, 16:02 (<zone>), as the agent said*, naming no account. A
  conversation's refused turn reads it too (`ChatRunner.Limited`): the account cools, the record gets a note, and the
  conversation goes on.
- **D58 amended**: `ReadStrikes` passes a `limit` record. A quest parked on its strikes before this stays parked
  behind Retry.
- **`SelectAsync` reads the cool-off first**, a file read before AGT3b's refused-key check and before any probe, and
  holds with §4's sentence and the cool-off on the selection (`HarnessSelection.Cooling`). A start held so is `Blocked`
  with that sentence and never `Exhausted`, and the look's report carries one `Waits` entry per account: the adapter,
  the agent, the account, the workspace where the held starts share one, the reset, and the quests, asks and
  repositories held.
- **The early ends**: `HarnessRoster.Ready`, the driver's half of *Try now*; a sign-in that exits 0 through
  `HarnessActions.LoginAsync`, which both of the screen's sign-ins call (`AccountCooling.SignedIn` reads the account
  from the directory's place under the home); a key made by `HarnessKeys.Add`; and the roster's refresh, which ends
  every tool's own home's cool-off and no named account's.
- **Said once**: the attention watch's `Waiting` kind, *<repository> — waits for an account*, once per
  cool-off, its first look a baseline as for parks; `starts.waiting` once per cool-off, which the roster remembers;
  `account.limited` per limit, its `turn` the turns the record ended plus one. Each names an account by its profile
  name, and a name that is not an identifier is null.
- **The protocol stub reads the stub's words as its owner's** (TOOL4a's hand-back). `acp-stub` keeps no toolchain:
  one would make it an agent with accounts to probe and list, and the modules refuse a default on it because it has
  none. `ISessionAdapter.LimitsOf` names the agent a door with no toolchain reads limits as, and whose own sign-in a
  limit on it cools; it is `acp-stub`'s alone. The protocol stub's starts and the pipe stub's share that cool-off, as
  Claude Code's two doors share theirs. Two stub accounts on the protocol door, TOOL4h's rehearsal, need that door to
  run as the stub's accounts, which this does not give.
- **The native door's failed result is not handed to the conclusion.** The row said *an empty table: no change*, but
  an entry is per agent, not per door: `claude-code` declares its entry for its protocol door's sake, and handing the
  native door's failure to it would read sentences on a door none was recorded on (§1.2). It waits for TOOL4b's
  evidence. The default is the constant `AccountLimits.DefaultCoolOff` until TOOL4e's `cooloff`.

**What the gates do not cover.** The `Process` half was not run by this branch: `AccountLimitTickTests` replays
observation 4 through real ticks (a cut-off, then a carry-on refused by the recorded sentence; the account cools until
Oct 3, 16:02 in the test's zone; the next looks spawn nothing and park nothing; the wait is said once; at the reset the
carry-on closes the quest), and `AcpTurnFailureTickTests` was changed with it: its first case now says `limit`, and its
carry-on case is cut off by a refusal no table knows. A sign-in's end through `LoginAsync` is reached only by a real
process, which no test runs. The note's time names the machine's zone, which travels with the note as the agent's own
sentence already did. Nothing on the screen shows any of it yet (TOOL4g).

**Built 2026-10-02 (TOOL4e): the twins and the terminal** (points 2, 4 and 8; design §2.2, §2.4, §3.1, §3.7, §6), held
by `RotationTwinTests`, `CoolingTwinTests` and `CoolOffTests` on the driver's side and, cell for cell, by the CLI's
`rotation.test.ts`, `cooling.test.ts` and `driverconfig.test.ts`. What building it settled:
- **Each writer of `harnesses.json` keeps the other's sections, and both write the same bytes** for the same wiring.
  `HarnessSettings.Save` writes `rotation` and `workspaceRotation` (only where an order is set), and now keeps what it
  has no field for (`Kept`, first, as the CLI's `rest`): a section a newer build writes outlives this build's save.
  Holding the bytes found that the driver's save wrote CRLF on Windows (the JSON writer's line end is the platform's);
  it writes LF now, and so does `cooling.json`'s writer. Byte for byte holds for names in ASCII; a key neither knows
  holding other characters is the same JSON, escaped differently.
- **The order is read leniently and refused strictly.** Reading trims each name, skips a blank or a name that is not
  text, and reads a name written twice, in any case, once; a door refuses a name that is no account here, compared
  exactly as `profile default` compares one, or one named twice in any case, the first problem said
  (`HarnessSettings.OrderProblem`, `RotationProblem.Sentence` for the screen's route). Clearing is naming nobody.
- **An account removed leaves no order and no cool-off naming it.** `WithoutAccount` takes it out of every default and
  order, and `RemoveProfile` ends its cool-off, since the next account made takes the first free `account-N`, which may
  be its name. The terminal's `profile remove` does both. The screen's *Remove* still clears only defaults: the modules'
  `ProfileRemove` should call `WithoutAccount` (TOOL4g, or a modules row).
- **The terminal's spelling of the tool's own sign-in is `--own`**: `daoris agent profile ready <agent> <profile>|--own`.
  `ready` on an account not cooling says so, names what is, and writes nothing. A sign-in (`login`, `login --new`) or a
  key (`agent key`) made from the terminal ends that account's cool-off, as `LoginAsync` and `HarnessKeys.Add` do.
- **`cooloff` is read as a whole number of at least 1**, anything else the hour; `HarnessRoster.Limited` takes it, from a
  driven session or an intake (`Driver.AccountLimited`) and a conversation (`ChatRunner`), and
  `AccountLimits.DefaultCoolOff` stays the default. `daoris driver cooloff <minutes>` refuses less than 1.
- **`daoris agent list`** says each account's cool-off under it (until when in the machine's zone, how long, why, and the
  `ready` that ends it), the tool's own sign-in's under the agent, the order, and §3.7's line where a start would run on
  the person's own sign-in: an agent that is here, signs in by its own flow and names no machine default.
- **The room names the three terminal doors** (`HelpRoomDoors`, *no screen yet*), and `HelpCoverageTests` holds
  `driver cooloff` as a door Ask Daoris owes until TOOL4g builds the `setting` kind's `cooloff`.

**What the gates do not cover.** Nothing reads an order to choose an account yet (TOOL4f). `login --profile`'s end of a
cool-off is reached only by a real login, and a conversation's `cooloff` only through a real protocol chat; both are one
line each, beside tested ones. The `Process` half, the rehearsals and the screen were not run by this branch, and the
screen, Ask Daoris's doors and the room's cooling facts are TOOL4g's.

**Built 2026-10-02 (TOOL4f): rotation** (points 4, 6 and 7; design §3, §3.7, §4, §5.4), held by `AccountRotationTests`,
`AccountRotationHoldTests` and `CarryOnHandedTests`. What building it settled:
- **The walk is a list, then a look.** `AccountRotation.Candidates` is pure: the resolved account, and only where a
  default named it and the applicable order lists it, the rest of the order after it, wrapping. An order naming an
  account with no directory here is never rotated into, since a start there would fail where it should wait.
  `SelectAsync` reads each candidate's cool-off and AGT3b's refusals first, and probes only when one is ready on both
  counts; signed out is read from the probe, re-asked once per start, and walked past alike. The first ready runs,
  and the selection carries `Rotated`: the account the default named and why, as a clause naming it.
- **A wait over an order holds on the first reset.** The selection's `Cooling` is the cool-off that ends first, so the
  look's wait, `starts.waiting` and the attention name that account and time. The sentence says every account is
  cooling, or lists each account and why when some are refused or signed out. With none cooling, nothing comes ready
  by itself, so the default's own refusal, which names its fix, is the answer.
- **A pick on a cooling account** is refused with the hold's sentence and the accounts ready now, by name. Naming them
  asks the probe the conversation was about to make, since a pick is a person's start, never a look.
- **Every rotated start says it once its record opens** (`RotatedOpening.Say`): its conversation record's first line
  names the account it opened on and why, and `account.rotated` is written. That covers a driven start, a carry-on,
  an intake, a conversation and Ask Daoris's opening. A carry-on's line adds the cut-off session and, after a limit,
  its refused turn and its context: usage's high-water, else the record's own usage reports. `carries` is the
  cut-off session for a carry-on alone.
- **The carry-on is handed what Daoris kept** (D80 amended): its record's last plan, at most 50 steps, each with its
  status in the wire's word; and its last words, bounded as a parked session's card bounds them, from the record or
  else the transcript's last plain lines. After a limit it is told the last session ran on another account, now
  cooling, which needs three facts: the record says `limit`, it ran on another account (`PriorSession.Profile`, read on
  loopback with `Limit`), and that account still cools. None of it names an account. With nothing kept, the
  instruction reads as it did, byte for byte.
- **The note, which travels, says `on another account`** where a carry-on runs on a different account than the cut-off
  session did, and names neither.
- **The wiring panel shows the account a start takes**: `StartWiring.Profile` is the rotated account, and
  `RotatedFrom` the one its rung named, so MAP1b's rule holds. The modules' `STARTS` route does not send
  `RotatedFrom` yet.
- **The words are the driver's, verbatim** (D24), in one place (`RotationWords`), with the glossary's names: an
  account, signed in, the tool's own sign-in. The page shows them as it shows every driver sentence. Saying them in
  both languages needs a code and parameters, which is the modules' and the web's (TOOL4g).

**What the gates do not cover.** The `Process` half was not run by this branch. `AccountRotationTickTests` runs a
carry-on on the pipe stub with two accounts, account-1 cooling, and checks the result: it runs on account-2 in the
cut-off session's tree, its record names account-2, its note names the cut-off session and no account, it is handed
the last words, its record opens naming both accounts, the log line is written, and account-1 takes the next start
once ready. It also walks past a signed-out account through a real probe. The limit there is replayed as the driver records one, since the protocol
stub reads the stub's table but runs on its own sign-in alone (TOOL4d): TOOL4h's two stub accounts on the protocol
door need that door to run on the stub's accounts, a driver change no row holds yet. A real rotation is TOOL4i's.

**Built 2026-10-02 (TOOL4j): the protocol stub on the stub's accounts** (point 4; design §1.3 rule 4, §8), held by
`AccountCoolingTests` and `AccountRotationTests`. What building it settled:
- **`acp-stub` is a door onto the stub, as `claude-code-acp` is onto Claude Code** (AGT7). Its toolchain says
  `AccountOf: "stub"` and the stub's variable (`StubAdapter.ConfigVariable`), with no binary, sign-in, login question
  or limit table of its own. So the stub's default and order choose its account, a limit on it cools the stub account
  it ran as, and both doors onto that account hold.
- **Present by a file look** (`ProbeByPresence`), as a plugin with no version question is: the agent a rehearsal names
  waits on its stdin, so a version asked of it would stall the probe. With no command named, it is absent, as the stub
  is, and a machine lists no such door.
- **TOOL4d's `ISessionAdapter.LimitsOf` is gone.** It existed so a door with no toolchain could read another agent's
  table, and `acp-stub` was its only user. A door's limits are now its owner's by one route, `AccountOf`.
- **The modules' refusal of a default on an agent with no toolchain has no built-in subject left.** Every built-in
  adapter now declares one, so `HelpFileDoorsTests` in the modules, which named `acp-stub`, fails; it is the modules'
  to change.

**What the gates do not cover.** The `Process` half was not run by this branch. `AccountRotationTickTests` gains a
case on the protocol door, where the driver reads the door's failure itself. Stub account 1 is refused with observation
4's sentence; the record says `limit`; that account cools until Oct 3, 16:02 in the test's zone, for both doors. The
carry-on opens on stub account 2 in the same tree, with the opening line, the note and `account.rotated`, and account 1,
once ready, takes the next start. A presence look reads this process's `PATH`, not the managed tools' (TOOLS5), as a
plugin's does; that is unchanged. TOOL4h's rehearsal phase is still to be written.

**Fixed 2026-10-02: a time of day gets no grace** (amends §2.1; FIX-LOG). The first real rotation on the install read
`your weekly limit resets 4pm (<zone>)`, refused at 4:12pm, as today's 4pm within the 15-minute grace, and cooled the
account for two minutes. The agent drops the date once a reset is under a day away (the same account had said
`resets Oct 3, 4pm` the day before), and a refusal says the reset has not come. So the grace now holds only for a dated
reset, and a time of day is the first such moment after the refusal. `AccountLimitsTests` holds the install's sentence.

*Amended by D130 (TOOL6, 2026-10-02): a start reads one scope, and a scope's list is the whole set of accounts its
starts may run on, for any number of accounts, so a workspace that names a default and no list rotates nowhere, and
one that lists accounts starts within them. A list may start on the account with the most left or the soonest reset,
run its sessions in parallel, keep one account for conversations, and switch before a limit, each on the agent's own
word (`docs/2026-10-02-account-use-design.md` §13). Point 4's reading and the rejection of spreading are amended
there.*

**Read 2026-10-02 (TOOL4b): what each door says about an account's limits** (point 1, design §1.2; D130 §0.3,
§5.1, §5.2), in `docs/2026-10-02-limit-signals-evidence.md`: keyless but for one short headless turn, and no limit
was met. What it found:
- **The native door's `rate_limit_event` carries a number on every frame.** Its `unifiedWindows` gives each window's
  use, as a fraction, and its reset, on ordinary turns: 117 frames recorded on Claude Code 2.1.287, one shape, all
  `allowed`. The SDK's declaration omits the field, so D130 §4.4's *clear, said without a number* does not describe
  Claude Code (evidence §1, §6).
- **The warning word is the CLI's own**, derived from use and time elapsed, and still `allowed` at 88% of a five-hour
  window (evidence §1.3).
- **A limit's failed `result` is `success` with `is_error: true`, `api_error_status` and the sentence**, read and not
  measured, so TOOL4d's reason for not handing the native door's failure to the table stands (evidence §2).
- **The protocol door forwards the frame** as `usage_update`'s `_meta["_claude/rateLimit"]`, except on a turn refused
  before the model answered, and its limit error carries `data.errorKind`. Daoris drops both today (evidence §3).
- **`codex-acp` forwards no limits, and a Codex limit's error message is `Internal error` alone**, with the sentence
  in `data.message`: no Codex entry can match on the door as built (evidence §4).
- **`/usage` and the status line stay unread** (§1.4, D130 §5.1): a separate fetch, and a person's terminal
  (evidence §5).

**What the gates do not cover.** No warning, refusal or Codex frame was measured. The protocol door's forwarded frame
and the CLI under it, 2.1.284, were not read on a turn, and two reads of the CLI binary were not made (evidence §7).
`verify` checks the note's links and none of these words.

**Built 2026-10-02 (ACPDATA1): the protocol door keeps a refusal's `data`** (point 1, design §1.2; evidence §3, §4),
held by `AcpTests`. A JSON-RPC error is now an `AcpRefusal`, still a `DriverException`. What building it settled:
- **The data's `message` is said after the error's**, as `<message>: <data.message>`, the shape the protocol's own
  `Internal error: …` has. So a Codex limit reaches the conclusion, the note and `AccountLimits.Read` as
  *the ACP agent refused the call: Internal error: You’ve hit your usage limit. …*, where it said `Internal error`
  alone. Words the message already says are not said twice, and Claude Code's message, which carries its sentence,
  reads byte for byte as before.
- **`errorKind` is kept beside the sentence, never in it** (`AcpRefusal.ErrorKind`, with `Said` for the data's
  message). Written into the sentence after Claude Code's reset clause, it would become part of the reset and the
  grammar would read none.
- **Read without trusting the shape** (REV3): a `data` that is not an object, or fields in it that are not text, add
  nothing, and the call still ends in a refusal.

**What the gates do not cover.** Nothing reads `ErrorKind` yet, and it does not travel past the door: the conclusion
takes the refusal's sentence alone. Reading it, or Codex's `codexErrorInfo`, which is not kept, is §1.2's *a field
beats a sentence*, and waits for a refusal recorded on the door. No real refusal with a `data` was seen; the shapes
are the evidence's, read from the adapters' code.

**Built 2026-10-02 (TOOL4k): a weekday reset, and Codex's clock** (points 1 and 2; amends design §1.3 and §2.1;
evidence §2, §4), held by `AccountLimitsTests`. What building it settled:
- **A weekday and a time** (*resets Mon 12:00am*, the maker's errors page) is the first moment after the refusal on
  that weekday at that time, with no grace, as a time of day is since the fix above. It stands where the date was
  dropped, so one just past is next week's; one whose time is still ahead today is today's, since a guess too short
  costs one refused start and one too long a week. Claude Code's entry records the page's sentence as documented,
  never seen, with no zone, so the machine's stands in.
- **A date may carry an ordinal and a year** (*Oct 3rd, 2026 4:05 PM*). A year names one date: more than 8 days ahead,
  or past beyond the grace, it is not believed, said so. A year not next to this one is not read. A dated reset keeps
  its grace.
- **The reset may be the rest of the marker's own clause** (§1.3 said a later clause): Codex writes its refusal as
  sentences, with no ` · `. A marker that ends its clause, as Claude Code's does, leaves no rest, so its sentences read
  as before.
- **Codex's entry is `codex-acp`'s** (`CodexLimits`), since no `codex` adapter exists to own it; a cool-off is still
  the `codex` account's (`AccountOf`). Its marker is *You've hit your usage limit[ for <model>].* at a clause's start
  or after a door's `: `, since a marker that does not end its clause cannot otherwise tell a refusal from a
  quotation. Its reset is *try again at <when>*, in the machine's zone, said as assumed; *Try again later.* is the
  default. `hit` is `usage`, never the model's name, since the log writes it.
- **Its four sentences were read in Codex's source** (codex 0.159.3, as the evidence cites it), the plan's next step
  and the model's name elided as `…`: the evidence records the template, and no sentence seen on a door. The
  both-ways check holds them as it holds Claude Code's.

**What the gates do not cover.** No Codex refusal and no weekday reset was seen on a door. Still unrecorded: Codex's
four workspace sentences (*Your workspace is out of credits…*, *You hit your spend cap…*, elided in the evidence, naming
no reset), its plans' next steps, and whether the app server's message is that text; Claude Code's *Opus limit*,
*Sonnet limit*, *team's shared budget* and *usage limit* on usage-based billing, of which the marker reads all but the
budget, unrecorded; and whether a real weekday reset prints a zone.

## D126 — A session is managed where it is: listed by what it needs, every act on its row and its page, what ended cleared, and a stop that holds (2026-10-02)

**Decision (SESSUX1).** The owner, 2026-10-02: *"there is no way to easily managed sessions in daoris right now and
it's not really smooth for ui/ux"*. Read from the code the same day: a session's row offered its review and its id; a
running driven session was stopped only from its quest's page; a quest parked on its failed sessions was started again
only from its quest's page, where the owner's work stood after an account limit on 1 October; a person's stop left a
taken quest with no way back and an open one planned again; nothing that ended could be cleared; and a driven session's
title was its quest's whole title. `docs/2026-10-02-session-management-design.md` is the contract; its §1 is the audit,
state by state and act by act. It settles:

1. **The list shows sessions by state**, the default, in five groups in the order the person acts on them: *Waiting on
   you* (a parked session, and the last session of a quest parked on its failed sessions, shown *parked*), *To review*
   (an ended session whose own tree holds work no branch of the person's holds, by D88's proof), *Working*, *Resumes
   later* (*awaiting reply*, D79; *account cooling*, D125) and *Ended*. *Group by repository*, today's arrangement,
   stays a choice in the list's new ⋯, remembered. *Ended* shows twelve and then a press for the rest.
2. **One reader decides a session's group**, `SessionGroups.Read` in the driver library: the records, the quests, the
   planner's own verdicts, the trees' judgement, D125's cool-off once built, and the archive marks. `SESSION_GROUPS`
   hands it to the page and `daoris-driver sessions` prints it. Nothing is written back to the record: **no new session
   state**.
3. **Open's hue is the person's alone.** *Waiting on you* and *parked* wear it; `queued` moves to neutral. Red stays
   an outcome, and every mark keeps its word.
4. **Every act is where its session is**: the row's ⋯ and a page header pinned at the top of Sessions' main area, both
   calling one module, `work/sessionActs.ts`. The acts are *Stop…*, *Try again*, *Review*, *Open folder*, *Open a
   terminal here*, *Open in its own window*, *Archive*, *Unarchive*, *Delete…* and *Copy session ID*, each offered only
   where it applies. The parked card keeps *Answer and carry on…*, *Finish* and *Decline…*. This amends RAIL1's rule
   that a row's menu holds only what has no other home.
5. **The stop has one owner and one word.** *Stop…* in the header asks once and says what follows by kind. The chat
   composer keeps *Finish* and *Stop turn*; the parked and intake cards and the quest page lose their stops. Ask
   Daoris's panel keeps its own, since the side bar holds that conversation on every view.
6. **A person's stop holds its quest on this machine**, open or taken: the planner's new `Stopped` verdict, with a
   sentence where a taken quest had none and an open one was planned again. **Try again releases it**: one act on the
   session and on the quest's page, recording RETRY1's mark for a parked quest and `released` in `driver.json` for a
   stopped one (twins: `DriverConfig.cs`, `driverconfig.ts`). A stop is still not a strike (D58).
7. **What ended is cleared by archive**, a mark kept on this machine (`<home>/sessions/archived.json`), never on the
   record, which travels. Archive refuses a live session and never hides what needs the person. *Archive what
   ended…* lists, then archives. **Delete** is for a conversation that served no quest only, ended, this machine's,
   named by nothing, with no tree here and held by no remote; it removes the record and what this machine kept of it,
   never a tree, a branch, the usage or the log.
8. **What needs you holds a parked quest**, and a quest's park is said once, as a session's is (`QuestParked`).
9. **A quest carries a short title**, `short`, written by whoever publishes it and travelling with it; a list shows it,
   a page shows the whole title. Sessions are still not named by hand (D52).
10. **The doors.** `daoris-driver sessions` lists by group and stops, finishes, declines, archives, unarchives and
    deletes; a stop reaches a session another process runs through a request the running loop honours. Ask Daoris
    gains a `session` kind (`stop`, `archive`, `unarchive`) and the `delete` kind's `session` door, and
    `HelpCoverageTests` reads Sessions' acts. Answering, finishing and declining stay the person's.
11. **Names** follow D116 in both languages; the glossary gains *try again* 重试, *archive* 归档 and *short title*
    短标题.

The build is SESSUX1a–l, the design's §9.

**Why.** A list is where many sessions are managed, and every act that lived on another view, or nowhere, was a trip
the owner had to know to make. The quest that stood parked on 1 October needed one press, and nothing where its
sessions were said so. A stop that left its quest in silence, or undid itself at the next look, is not one a person can
manage with. What ended had nowhere to go but further down.

**This amends:**
- **The working surface design's §3** (RAIL1's row menu; the list grouped by repository) and **§4** (the parked
  card's stop).
- **D104 and D80**, as the planner reads them: a person's stop holds its quest, open or taken, until *Try again*.
- **RETRY1**: *Try again* also releases a stop.
- **The platform language's §3** (open's hue) and **§5** *Sessions*.
- **D110**: the `session` kind, the `delete` kind's `session` door, and the coverage of Sessions' acts.
- **D94 §4**: `session.deleted` and `sessions.archived`, once SESSUX1f and SESSUX1g build them.

**D55, D118, D51 rule 7 as D88 amended it, D58, D46 §4 and D52 stand.**

**Rejected.**
- **Naming a session by hand.** D52's trigger is two sessions that cannot be told apart, and the complaint was length.
- **A short title cut from the title.** It keeps the start, where a family's quests tend to agree.
- **Archive as a field on the record.** The record travels; one person's tidy is not a fact about the work.
- **Archiving or deleting by age.** A session gone without a press is one the person never saw go.
- **Deleting a session that served a quest.** It is that work's record, and the strikes are counted from it.
- **A delete that travels to a remote.** A session record does not travel as a deletion.
- **Several sessions chosen at once.** A second selection beside the attended one; the bulk archive covers the case.
- **Unlanded work in Overview's *What needs you*.** The proof is a git walk per repository on every open.
- **New ledger states** for *parked*, *awaiting reply* or *account cooling*: each is a derived fact about the quest.
- **Ask Daoris answering a parked session.** The answer is the person's (D37).
- **The grouping as a helper on the page.** It needs the planner's verdict and a git judgement, which only the driver
  has, and a terminal twin would make it a twin of an eleven-case rule.

**As built (SESSUX1a, 2026-10-02): the reader of a session's group, and the archive marks.** `SessionGroups.Read`
(`SessionGroups.cs`) is pure over a `SessionLook`: the records, every quest, the planner's verdicts, each quest's last
session here and its strikes as `ServiceClient`'s own readers derive them, the trees' judgement and the marks.
`SessionTrees.WorkAsync` is D88's proof of one session's own tree. `SessionArchive` keeps `<home>/sessions/archived.json`.
`SESSION_GROUPS` and `SESSION_ARCHIVE` are on `DriverModule.Sessions.cs`, with `SESSION_UNKNOWN`, `SESSION_LIVE` and
`SESSION_NEEDS_YOU` in `Refusals` and both catalogues. `bridge/sessions.ts` has `useSessionGroups` and
`useArchiveSessions`, and no screen reads them yet. What the build settled that §2 and §5 left open:

- **To review needs a tree nothing will go back into.** §2.1 says a session is in the first group it qualifies for.
  Read literally, that would put in To review every awaiting reply with a commit, and every cut-off a carry-on is
  writing. So a tree a live session holds is in use, as D88's proof keeps a tree in use. So is a tree whose quest is
  still taken and still considered by the planner: its resume or its carry-on goes back into that tree (D79, D80).
  Only the newest session on a tree stands for it. A person's stop leaves its quest unconsidered, so the stop's work
  is to review, as the proof's case says.
- **A teammate's record is grouped by its state only.** Its park waits on the teammate, so it is listed under Working.
  It is never parked, to review or resuming later: the verdicts, the strikes and the trees are this machine's, as the
  park notification already reads them (D47 §6). It may be archived here.
- **The planner's seam is the reader's, not `Planner`'s.** `SessionGroups.VerdictsAsync` takes the loop's last look
  (`LastLook`, which `DriverLoop.Look` records beside the parked quests) and plans over a fresh snapshot where none has
  looked. `Planner.cs`, the strikes and the client were TOOL4d's while this was built, so nothing there changed. For
  the same reason `SessionRecords.ReadAsync` asks the records door with its own client and the loop's key, rather than
  adding a method to `ServiceClient`. Moving it there later changes no answer.
- **A parked row's number is the planner's count**: the records' strikes less RETRY1's mark, which is the expression
  the planner parks by. Whether the quest is parked is the planner's verdict alone.
- **A state this build does not know is live**, so nothing archives a session a newer host says is still running.
- **git is asked only of a tree that could be to review, and only of a repository of its own.** A count git cannot give
  is kept as work, as the clean-up keeps what it cannot clear. A folder under the trees that is no repository of its
  own is never asked, since git walks up.
- **Archive refuses per session, and the answer depends on how many were asked.** Asked of one session, a refusal is
  the answer. Asked of several, the route archives what may go and lists each kept session with its code, as the
  clean-up keeps what changed since its list. Unarchive needs no service, and a session that was not archived is named
  in the answer, never refused (D48 §6). Each write drops marks for records that are gone.
- **`SESSION_NEEDS_YOU` names which group kept a session through i18next's context.** The refusal carries `group` and
  `context`, and `errors.SESSION_NEEDS_YOU_review` is the To review sentence beside the base one, in both languages.

Held by `SessionGroupsTests` (a table of 44 cases over §1's states, the order, the trees judged, the readers),
`SessionArchiveTests`, `SessionTreeWorkTests` (real git, the `Process` half, run by the parent),
`DriverModuleSessionsTests`, `HelpCoverageTests` (the archive is a door owed to SESSUX1h's `session` kind) and
`bridge/sessions.test.tsx`. Each was seen failing first, except `SessionTreeWorkTests`, which this branch could not
run. **Not built here**: `deletable` (SESSUX1f), *account cooling* (SESSUX1k) and the stop's hold (SESSUX1b, which adds
its verdict to the reader's table: a quest held by a stop leaves its tree to review). **Not measured**: how long
`SESSION_GROUPS` takes over a workspace of 29 repositories with their trees (§12).

**As built (SESSUX1b, 2026-10-02): a person's stop holds its quest, and Try again releases it.** The planner's
`StartVerdict.Stopped` holds a quest, open or taken, whose last session here the person stopped (`stopped`, not
interrupted: `PriorSession.PersonStopped`), saying *you stopped session `s`; Try again carries it on* (or *starts it
again*) with the terminal's door, and naming the stop on `Consideration.HeldBy`. `released` in `driver.json` holds each
release, the quest against the session (twins: `DriverConfig.Released`, `driverconfig.ts`, one table, `twins.md`'s row).
Released, a taken quest is carried on in the stop's tree, its instruction saying the person stopped it and released it
(`SessionTarget.Released`), and an open one is planned as a first start. `RETRY_QUEST` reads the planner's verdict (the
loop's last look, else a fresh plan, as Sessions' groups read it) and marks a parked quest, releases a held one, or
refuses with `QUEST_NOT_HELD` (both catalogues). Its answer is still the state, with `retried` saying which. Ask
Daoris's `retry` is judged against the facts' `Held` beside `Parked`, and the room lists the held quests. The room's
retry row names the quest's page, and a row of its own gives a stop's release. `SessionGroups` exempts `Stopped` in
`ReviewableTree`, and each row carries `HoldsQuest`, which `SESSION_GROUPS` answers. What the build settled that §3.3
and §3.4 left open:

- **A stop is the reason its quest sits, before every other.** A hold, an opt-out or the strikes would each say
  something true, and none of them is what moves the quest. A verdict naming the take would also hide the stop's tree
  from review.
- **The terminal names the session.** `daoris driver` talks to nothing (D50), so it cannot see whether a quest is parked
  or held, or which session holds it. §3.4's *do whichever applies, and refuse when neither does* holds for
  `RETRY_QUEST` and Ask Daoris, which read the verdict. At a terminal, `retry <quest>` marks, as RETRY1 did, and
  `retry <quest> --session <id>` releases. The stop's sentence spells the second. The two flags together are refused.
- **A release moves no mark**, since a stop is not a strike (D58). Strikes forgiven before a stop still count from
  their mark once it is released.
- **A stopped quest that waits on a question is held too**; released, it waits or resumes as D79 says.

This amends **RETRY1's reading**: the route took any id and marked it; it now does what the verdict says applies, and
refuses the rest, as Ask Daoris's `retry` already did. **D58 stands.**

**Not built here.** 🔴 **The service's ledger refuses a released take's carry-on.** `SessionLedger.OpenAsync` carries a
taken quest on only after `failed`, an answered park or an interrupted stop. A person's stop is *never carried on*
there. Against a real host the planner plans the carry-on and each look's open is refused (*Quest is Taken*), until the
ledger takes a person's stop as a carry-on, which the driver now releases. That is the service lane's, for the parent
to schedule. An open quest's release works end to end. The page's *Try again* on a `Stopped` verdict, and its toast
for a release, are the web shell's: `QuestPage.tsx` offers it for `Exhausted` only, and until it changes the page shows
the stop's sentence under *Sitting*, which names the terminal's door. The service's `setting_propose` words still say
*the quest its failed sessions parked* (`HelpProposalBox.Setting.cs`), and its box takes a held quest's id unchanged.

Held by `PlannerTests`, `ReleasedTests` and `driverconfig.test.ts`, `SessionGroupsTests`' new rows, `ParkedQuestsTests`,
`AskAndWaitPromptTests`, `HelpSettingProposalsTests`, `HelpRoomMachineNowTests`, `HelpRoomDoorsTests`, the room's golden
files and `DriverModuleRetryTests`. Each was seen failing first, bar `ReleasedTests`' rows on the driver's side, written
with the reader they test: the CLI's copy of that table, which parses them cell for cell, was seen failing first.
`StopHoldTickTests` (the `Process` half, a real stub harness) is written and was not run here. It covers a take
stopped, held across two looks with nothing spawned, then carried on in its tree once released, and an open quest stopped
before its take, held, then started. Its stand-in opens any session, so its taken case proves the driver's half only.
`DriverModuleDriverTests`' retry case now records a parked look first, and was not run here either.

**As built (SESSUX1b2, 2026-10-02): the ledger carries on a released stop.** `SessionLedger.OpenAsync` opens a session
on a taken quest whose last session here ended `stopped` by the person, as it does after a cut-off, when a session of
this machine's took the quest (`SessionStore.TookHereAsync`, STANDDOWN2's `took`). The driver holds such a quest until
*Try again* releases it, so an open that reaches the ledger is the release. The ledger reads no `driver.json`, which is
the driver's and machine-local. What the build settled:

- **The take must be this machine's.** A person's stop before its session took the quest leaves no take here. Taken
  since, the quest is somebody else's: another machine's driver, whose record arrives without its `took`, or a person
  working outside the driver, who leaves no record. Carried on as a cut-off is, the release would start a second session
  on that work. So the stopped record, or an earlier one of this machine's on the quest, must have taken it. An earlier
  one counts because a carry-on takes nothing itself.
- **Nothing else moved.** A stand-down and a declined quest still refuse, with this machine's take on record. A cut-off,
  an interrupted stop and an answered park carry on as before, with no `took` asked. The strikes are the driver's, and
  the ledger counts none (D58, D125 §3).
- **The door's answer keeps its shape.** `POST /api/sessions` maps the same refusals. No HTTP test was added: the HTTP
  door cannot mark a take (only a session's connector does, over MCP), and a test reaching into the store proves no more
  than the ledger's.
- **Ask Daoris's `retry`** names the quest its failed sessions parked *or the person's stop holds*, in the box's
  refusal, its target and `setting_propose`'s description.

**Not covered.** A take its connector did not mark (a build before STANDDOWN2, or a take through the HTTP door) is still
refused after a person's stop, as before. A cut-off's carry-on asks no `took` (D80), so a start that failed before its
take, on a quest taken elsewhere since, is still opened; that is D80's as it stands, and this build left it. A take
this machine made offline and lost (D68 §5), stopped by the person before the driver stood it down, would be carried on
once released.

Held by `SessionLedgerTests`: a released stop of the session that took, and of a carry-on whose earlier session took,
seen failing first; a stop before the take with the quest then taken by another machine or by nobody here, refused,
which passed before and was seen failing under the rule without `took`; a stand-down and a decline after a released
carry-on, refused. And `HelpSettingProposalTests`' retry rows and the tool's words, seen failing first.

**As built (SESSUX1c, 2026-10-02): the list by state.** `SessionRail` asks `useSessionGroups` and hands `SessionList`
and `SessionStrip` (molecules, `work/SessionList.tsx`) the records and the reader's answer; `work/groups.ts` draws
the groups in the reader's order, the list by repository, *Ended*'s twelve and the strip, and took over `partition`.
`SESSION_TONE` and `SESSION_DOT` gain *parked* (open's hue, the waiting mark) and *awaiting reply* (neutral), and
`queued` is neutral. Sessions' ⋯ is `ListMore` with *Group by*, kept in `daoris.list.sessions.filters` as
`{ group, archived }`, the default kept as nothing. What the build settled that §2 and §4 left open:

- **The badge reads the tick, not the reader.** Group 1 is counted as this machine's sessions parked to ask plus the
  quests the planner's last look parked (`useConsidered`, verdict `Exhausted`), the facts Overview's band reads for
  SESSUX1i. Asking `SESSION_GROUPS` from the activity bar would walk git in every tree to review on every view, every
  tick, for a count that needs no tree. A teammate's parked session is not counted: the reader lists it under Working.
- **A record the reader has not answered for is placed by its record alone**: one that started after the reader
  looked, or every one while no answer has come. Live and parked to ask on this machine is Waiting on you, any other
  live one Working, an ended one Ended, ahead of the reader's ended rows since it ended after the look. That is the
  reader's first step and nothing more; parked, to review and resumes later are only ever the reader's.
- **No reader, no grouping by state.** A browser asks no driver, so its list is the arrangement it always had, by
  repository. The monitor's list is the present tense only and asks the reader nothing. A failed answer is not
  toasted, as `SESSION_WHERE`'s and `SESSION_OPENINGS`' are not: the list keeps the records' placement, and a refusal
  while the driver comes up is asked again when it says it is ready (LOOK2a).
- **The page draws what its scope holds.** The reader answers for every record on the machine; a session outside the
  chosen workspace is not drawn.
- **The attended session is never cut out**: past *Ended*'s twelve it stays listed, and archived while archived is
  hidden it is listed under Archived alone. *Show N more* is not remembered.
- **A row's line.** Parked says how many sessions failed, or *after its failed sessions* where the count is null;
  awaiting reply names the question, and who it was asked of where the service still lists it; to review says its
  commits, its uncommitted changes only where the commits are a proven zero, and *work to review* where git could
  not count, since a count git cannot give is kept as work.
- **The Archived group is drawn when the filter says so**, last, from the reader's sixth group; the ⋯'s *Show
  archived* and its empty state are SESSUX1e's. `work.rail.endedMore` is retired for `work.list.showMore`.

Held by `groups.test.ts`, `SessionList.test.tsx`, `SessionRail.test.tsx` (by state over a stubbed `SESSION_GROUPS`,
a browser's and the monitor's lists asking nothing), `SessionRow.test.tsx`, `ui.test.tsx` (open's hue),
`attention.test.ts` and `sessionsBadge.test.tsx` (the badge), `WorkFrame.test.tsx` (the ⋯, remembered), and the
stories. Each was seen failing first, `groups.test.ts` only as a module not yet written. **Not built here**: a stopped session's line *held here until you try again*,
which needs SESSUX1b's verdict in the reader, and *account cooling* (SESSUX1k). **Not measured**: the groups'
headings and the rows' lines at 888 and 680 px in both languages, which the window decides (§12).

**As built (SESSUX1e, 2026-10-02): archive on the screen.** A row's ⋯ offers *Archive* where the reader placed the
session in Ended and *Unarchive* wherever its mark stands. Sessions' ⋯ gains *Show archived*, the filter's `archived`
kept beside the arrangement, and below a rule (`MoreItem.rule`) *Archive what ended…*. `SessionRail` sends each through
`useArchiveSessions`. `ArchiveEndedAsk`, a molecule in `work/SessionList.tsx`, is the bulk's first press, listed by
`endedToArchive` in `work/groups.ts`. What the build settled that §3.1 and §5 left open:

- **The acts are the rail's until the header exists.** §3.1 gives every act one module, `work/sessionActs.ts`, for the
  row and the header to share. The header is SESSUX1d's, after SESSUX1b, so the row's two archive acts and the bulk's
  second press live in `SessionRail` for now, and SESSUX1d moves them when the header gives them a second door.
- **Archive is offered only where the reader placed a session in Ended.** A record the reader has not answered for is
  placed in Ended by its record alone (SESSUX1c's note), and may yet be to review, so it offers no *Archive* and the
  bulk does not take it. *Unarchive* is offered wherever the mark stands, on a row that waits on you too, since the
  reader keeps a session that needs the person in its group whatever its mark says.
- **The bulk's first press holds what it listed.** It opens once the reader has answered. It says how many it would
  take, and how many stay under *Waiting on you* and *To review*, counted as those headings count within the page's
  scope. The second press sends exactly the ids the first listed, held from when the ask opened, since the reader
  answers again on every tick. With nothing to take it says so and offers only *Close*, never a press that archives
  nothing.
- **What the second press kept is counted, not listed.** The route archives what may go and returns each kept session
  with its code. The toast says *Archived N of M; the rest changed since the list*, and each kept row stands in the
  group that now holds it. Asked of one session, a bulk list of one included, the route's refusal is the answer, said
  in the catalogue's words, `SESSION_NEEDS_YOU`'s group through its context.
- **An archived row says so where no heading does.** Under *Archived* the heading says it. By repository and in a
  search (§4.5), the row's line starts with *archived*, and its tip says how it comes back. *Show archived* with
  nothing archived draws *Archived (0)* and *Nothing archived*, so the tick never seems to do nothing.
- **The glossary gains *archive*** (归档; its inverse 取消归档; never 存档 or 隐藏). The Chinese refusal SESSUX1a wrote
  said 归档从不隐藏…, and now says 归档绝不会把需要你处理的会话移出列表, so the term's own avoid list holds of the
  sentences `names-check --all` reads.

Held by `groups.test.ts` (what the bulk takes), `SessionRow.test.tsx` (each act offered or absent by group, the line's
mark, 中文), `SessionList.test.tsx` (Archived and its empty state, the ask's two presses, nothing to archive),
`ListPane.test.tsx` (the rule), `SessionRail.test.tsx` (each act and refusal over a mocked bridge, the bulk's kept count,
a search's mark), `WorkFrame.test.tsx` (the ⋯, remembered, and the ask opened from it), and the stories. Each was seen
failing first. Two negative cases passed before the build: no *Archive* on a live, waiting or to-review row, and no
Archived group while hidden. Each was then seen failing under a sabotage of the rule it holds. **Not built here**: the
header's *Archive* and *Unarchive* (SESSUX1d) and *Delete…* (SESSUX1f). **Not measured**: the bulk's ask at 1280 and
680 px, and *Archived* in both languages and both themes, which the window decides (§12).

**As built (SESSUX1d, 2026-10-02): the page header, and every act where its session is.** Sessions' main area has a
pinned `SessionPageHead` with the acts by state and `StopAsk` under it. A row's ⋯ draws the same acts. The parked
card, the intake cards, a chat's composer and the quest page lose their stops. The quest page's *Try again* also
takes a `Stopped` verdict. `SESSION_OPEN_FOLDER` is on `DriverModule.Sessions.cs`. What the build settled that §3
left open:

- **One rule and one owner, in two files.** `work/acts.ts` is the rule, pure, which the molecules read
  (`offeredActs`, `primaryAct`, `folderOf`, `stopAsk`). `work/sessionActs.ts` is the owner both doors call
  (`useSessionActs`), an organism. A molecule imports no hook, so the rule could not live beside the bridge. What only
  the frame can do (attend and answer, attend and ask to stop, review, a terminal there) goes through its `doors`, and
  the monitor's list, which has none, offers its window and its id.
- **A teammate's record offers Archive, Unarchive and its id at both doors.** §3.2's *Copy alone* rests on nothing
  reaching its process, and an archive mark is this machine's (§5.2).
- **Review is offered where there is work to read**: a tree its record names here, a landing, or *To review*. RAIL1
  offered it on every row. **Open folder and a terminal** are offered where `folderOf` finds the tree, else the
  checkout, never for an intake or Ask Daoris. The route opens only a tree this home opened or the registry's
  checkout. Anything else, a teammate's record included, is `SESSION_FOLDER_GONE`. A system that will not open it is
  `SESSION_FOLDER_NOT_OPENED`, as the log's and a plugin's are, and an id no record has is `SESSION_UNKNOWN` with
  the `folder` context.
- **A driven session whose quest the page holds as open has not taken it**, so its ask says another machine may.
- **One toast per outcome on both pages**: `retryNotice` reads `RETRY_QUEST`'s `retried.did`, so a mark is
  `quests.detail.retried` and a release `quests.detail.released`.
- **中文 says a stop's hold with the session the tick names.** `DriverLoop.TickConsideration` adds `heldBy`, and
  `work.sitting.Stopped` reads it, never the driver's English. `i18n-check`'s `PASSED_FACTS` lets a passed-through
  sentence's other language say the facts the page hands beside it. With no `heldBy`, the driver's words stand.
- **SESSUX1c's leftover line**, *held here until you try again*, is built: `holdsQuest` reaches the page's grouping,
  and *Try again* reads it too.
- **Names.** 中文 `work.act.stopMeanIt` is 确认停止会话, not §8's 确认停止: the names check holds a button that says
  *session* to say 会话. `work.composer.stop` stays for Ask Daoris's panel, §3.3's exception. `work.awaiting.stop`,
  `quests.session.stop` and `work.rail.menu.review` are retired, since the review is `work.head.review` on both doors.
  The glossary gains *try again* 重试.

Held by `acts.test.ts`, `sessionActs.test.tsx`, `SessionPageHead.test.tsx`, `SessionRow`, `SessionList`,
`SessionRail`, `WorkFrame`, `Composer`, the cards' tests, `QuestsView.shell.test.tsx`, `signals.test.ts`,
`bridge/sessions.test.tsx`, `DriverModuleSessionsTests` and `TickConsiderationTests`, and the stories. Each was seen
failing first, bar the row's tests, rewritten with the rule they draw. **Not built here**: the header's *Delete…*
(SESSUX1f) and *Stop…*'s Ask Daoris door (SESSUX1h). **Not measured**: the header at 1280, 888, 680 and 560 px in both
themes and languages, the long run's toolbar pinned beneath it, and a real stop's ask (§12).

**As built (SESSUX1i, 2026-10-02): What needs you holds a parked quest, and a park is said once.** Overview's band has
a `parked-quest` row after the parked sessions (`needsAPerson`, from the tick's `Exhausted` verdicts), its door the
quest's page. `AttentionKind.QuestParked` is said once by `AttentionWatch`: a toast, an OS notification and the headless
host's line, behind the notify switch. What the build settled that §4.6 and §4.7 left open:

- **A park's facts are read from the records, and only when the parks change.** A verdict names the quest and says the
  rest in English. The notice needs how many failed and the last failure's note, and the row when the last session
  ended. `SessionGroups.Parks` reads them from what the list's *parked* row is placed by (the last run, the strikes less
  RETRY1's mark, the record's end), so the row, the notice and the list count one number. `QuestParkReader` asks the
  records door only when the quests the planner parked change, since the records are every session ever run (M9) and a
  park lasts every look. A refused read is asked again at the next look and says nothing meanwhile. The planner and its
  snapshot were outside this branch's lanes; a count carried on the consideration would save the read.
- **A park is its last session.** The watch keys each quest's park by its last session here, so a hold that hides it
  for a look (a held repository outranks the strikes) is the same park, and *Try again* then new failures is a new one.
  The first look is a baseline, as a session's park is, so a relaunch says nothing. A quest the planner no longer
  considers is forgotten.
- **The last failure is said by its park.** The notice carries that failure's note, so the same look's *a session
  failed* for that session is not said beside it.
- **The tick carries `strikes` and `since` for a park** (`TickConsideration`), never its session or note. 中文 says the
  sitting sentence from the number (`work.sitting.Exhausted`; `failed` joins i18n-check's passed facts), and without it
  the driver's words stand, as for a stop with no `heldBy`. The row waits since the last session ended, else since the
  quest's last move.
- **Overview's badge counts every row the band lists.** It had left out the rule proposals (PERM2), and now takes the
  band's every input.
- **Names.** `work.attention.parked-quest`, *parked after failed sessions* / 会话失败后挂起, and its door *open the quest
  to try again* / 打开委托以重试. The notice is the driver's sentence, *engine — `#q` parked after 3 failed sessions*,
  untranslated as every notice is (D24). The OS notification wears the park's warning, and pressing it attends the last
  session, whose header offers *Try again*.

Held by `QuestParksTests`, `AttentionTests` (once, a hold between, tried again, the first look, the failure folded, never
for a stop), `TickConsiderationTests`, `attention.test.ts`, `signals.test.ts`, `AttentionRow.test.tsx`,
`ShellSignals.test.tsx`, `sessionsBadge.test.tsx` (the band, its door and both badges over a mocked shell) and the
stories. Each was seen failing first, as a compile error or an assertion, bar one negative case that passed before (no
parked quest before a tick). The watch's key and fold, the tick's verdict check and the badge's inputs were also seen
failing under a sabotage. `SessionNotifier` has no test, as before. **Not built here**: the row's short title
(SESSUX1j), so it shows the quest's title. **Not run here**: the `Process` half and the rehearsals; no rehearsal reads
the attention lines, which only the watch mode prints. **Not measured**: the row and its 中文 sentence on Overview at
1280, 888 and 680 px in both themes, and a real park's notification on the install (§12).

## D127 — A session pays for what it reads on every step after: rows and entries point to their detail, and a report holds their shape (2026-10-02)

**Decision (SESSOPT1, the owner's, asked whether the task archive should be split: *"this is not about how we split
its about how we optimize the session and this should also belong to doctrine too"*).** The contract is
`docs/2026-10-02-session-economy-design.md`: what a session here reads and writes, measured (§1), the doctrine
(§2), the report (§3), this repository's practice (§4) and the build (§6). It folds in DOC7.

1. **The principle is doctrine.** A session pays for what it reads on every step after it reads it, so what is read
   whole stays short by how each entry is written, and what is long is read by lookup. Each fact has one home, and
   every other place names it in a line.
2. **The canon says it in the two documents that already own the subject.** `task-lifecycle` gains one sentence of
   why and the shapes in its bullets: a row is what and why in two sentences, its contract and its proof; an outcome
   is a line or three, saying what changed and where its detail lives; what a decision, a design or a commit already
   says is pointed to. `development-documents` gains the failure, the shapes of a backlog row, an archive entry, a
   router row and a decision's amendment, *look up by identifier*, and the shapes' numbers beside the ceilings (60
   words for a row, 60 for an outcome). `set-up-documents`' archive-entry template asks a line or three. No new
   knowledge document.
3. **A report holds the shapes, and never fails** (D54). `tools/doc-shapes.mjs`, in `verify`, reads the declared
   backlog, archive and router, and prints rows, router rows and archive outcomes past the cut-over over 60 words. A
   configuration it cannot read fails. It stays this repository's, not `check`'s.
4. **This repository writes records that way.** The dispatch skill's hand-back carries the outcome in one line and
   where its detail lives. `TASKS.md` is trimmed by moving each line to its home, never by deleting what nothing else
   says: logs of sightings to open entries in the fix log, histories and held reasoning to their records, owner
   quotations to the designs that already open with them, traps to the folder they are about. The router's
   *Where it stands* becomes a line naming its decision.
5. **The archive is neither split nor compacted.** Its 349 entries stand as written. From the cut-over, every new
   entry takes the shape.
6. **DOC7's line carries what economy needs**: `session.read {session, adapter, role, how, call}`, where `how` is
   whole, part or search (null where the door does not say) and `call` the read's place among the session's calls.

**Why.** Measured at `7e4cb1c` (design §1). Every session here starts with 48,554 bytes of `CLAUDE.md` and
`AGENTS.md`. Picking work reads `TASKS.md` whole: 8,503 words over a 6,600 ceiling, trimmed twice and grown back
both times. Its length is logs, histories and quotations, not rows of work: its rows copy almost nothing from the
designs (a median of 3% of their six-word runs). The archive's 149 outcome paragraphs have a median of 214 words in
September and 190 in October, and a median of 75% of the code spans in an outcome are in the decision it cites (80%
since 2026-10-01): a second account of the note, in other words. The canon asked for that paragraph in its own
template, while its always-loaded rule asked for a line. The one long driven turn on record re-read about 103 tokens
from its cache for every token it read anew (COST1, METER1), which is the mechanism: a harness carries what was read
into every request after.

**Rejected** (design §8 has each with its reason):
- **Splitting the archive.** A reader by lookup pays per entry, not per file, so a split changes no session's read
  and breaks every link to it.
- **Compacting the 349 entries.** Only part of their detail is provably elsewhere, no session reads the file whole,
  and old entries stand as written (`set-up-documents`). What it would take, if chosen, is in §8.
- **A new core knowledge document**: an index row in every adopter, and one subject split in two.
- **Changing `task-lifecycle`'s frontmatter**: its body is in the region whole, so the row only repeats it.
- **A shape that fails** (D54), **the report in `check` now** (it would guess at every adopter's record format;
  reconsidered when a second repository keeps the template's shape), **raising the backlog's ceiling**, **trimming
  without a shape**, **deleting what a long row holds**, and **changing `persist-working-state`**.

**What it amends, when built.**
- The canon's `task-lifecycle`, `development-documents` and `set-up-documents` (SESSOPT1a). The region grows 458
  bytes in this repository and in each example, measured on the drafts: 24,064 → 24,522 of 26,000 here.
- D122 §2.5: an archive entry's outcome is a line or three, not a paragraph. D122 §1.5 and §6: DOC7's line gains
  `how` and `call`.
- D115 §5: the steward's archive entry carries the outcome line, and FLAKE lines go to the fix log's FLAKE1 entry.
- The dispatch skill's hand-back (MOD9).
- D117 §4.2: LAYOUT6's brief is held to the brief's content test as it moves.
- D106's *no moving counters in always-read files* reaches the backlog's row count.

**What the checks do not cover.** This change is documents only, and nothing is built. The measures are scratch
scripts' over the tree at `7e4cb1c`, untracked, and the design says what each counted. Repetition is measured by
proxies, six-word runs for words and code spans for facts, which read no meaning. The region's bytes were measured
by syncing scratch copies against drafted canon text, which the build may reword. No token was counted, and what
sessions here open, and when, is not measured (DOC7). Whether a line or three is enough for a later reader is
judgement, read from the first entries after the cut-over. `verify` checks this entry's shape and the design's links
and budget, and none of their words.

**As built (SESSOPT1a, 2026-10-02): the doctrine, in the canon.** §2.2's text landed in `task-lifecycle` as drafted.
`development-documents` took §2.3 with one change of placement: the entries' shapes are a paragraph after the two
ceiling bullets, saying they apply whether a record is read whole or by lookup, so the *two units* the section
opens with stay two. `set-up-documents` took §2.4. This amends D122 §2.5 (an outcome is a line or three). The
region grew exactly 458 bytes in each repository: 24,522 of 26,000 here, 22,216 and 22,433 of 30,000 in the
examples, re-synced in the same commit. Not covered: the family rehearsal (the parent's), and whether the words
read well to an adopter.

**As built (SESSOPT1b, 2026-10-02): the shape report.** `tools/doc-shapes.mjs` runs in `verify` after
`doc-budgets`, with its numbers in `tools/doc-shapes.json`. `archiveCutOver` is `null` until the steward writes
it (§3.5). An entry is counted as written, the list marker and a table's pipes included, as `doc-budgets` counts
a document. Over the tree at `7e4cb1c` it reports §3.1's 17 rows and 12 router rows. A backlog row runs on across a
blank line when the next line is indented, since a reader sees that paragraph as the row's, so FLAKE1 measures
831 words, not §1.4's 769. An archive heading with no date is counted as undated and is not measured. Proof:
`doc-shapes.test.ts`, ten cases, each seen failing against a stub first. Not covered: the adopter's own record
formats, which stay out of `check` (§3.2).

## D128 — A set-up keeps the repository's checks green, and the doctrine region lists only what the canon bounds: knowledge and skills are an index read on demand (2026-10-02)

**Decision (WSSETUP14, from the first real set-up, WSSETUP12's).** On the report repository, the set-up's own
session wrote good knowledge, a brief and rooms, closed its quest `done`, and left a branch that cannot be merged.
It had moved 169 knowledge documents that its CI and a hook read by path, its region grew to 53,398 bytes with a
row per document, and 166 rows read *needs frontmatter*. The contract is
`docs/2026-10-02-setup-pilot-lessons-design.md`: checks kept green (§1), the index (§2), a document without
frontmatter (§3), the pilot's branch (§4) and the build (§6).

1. **Move only what an agent needs moved.** The repository's own knowledge stays where it is, declared as
   `documents.knowledge`: a folder the index lists, the service indexes and `sync` never writes. Its skills move to
   `.agents/skills/`, and the mirror keeps their old paths readable. Nothing else a check, script, hook or CI
   configuration reads by path moves. Where a move still breaks a reader, the set-up rewrites that path, and only it.
2. **A set-up runs the repository's checks before its first change and at its close.** One that passed before and
   fails after means it does not close `done`. If the fix is outside its bounds, it stops and asks.
3. **The bounds are rewritten** (design §1.5): a moved path is the one change allowed in a source, build or CI file,
   and a set-up adds no frontmatter to a document the repository already had.
4. **The region holds what the canon bounds**: the rules table, a pointer, the rooms, *Where things are* and every
   rule in full. The knowledge and skill tables move to `<target>/INDEX.md`, which `sync` writes, `check` keeps true
   and the lock names. `doc-loader` reads it, whole when short and by search when long.
5. **A knowledge document without frontmatter is listed by its first heading**, in a table of its own, and `check`
   reports how many once, never failing.
6. **The pilot's branch is never merged red.** When the press finds a set-up's branch standing and not on the line,
   it composes *Finish setting up this repository*: the checks first, that branch merged by one exact rule, the
   knowledge put back and declared, a re-sync, and the same close.

**Why.** The quest moved the documents and forbade repairing what the move broke, and nothing asked whether the
checks still passed. Knowledge is reached through the index wherever it lives, while skills are read by folder
(Claude Code `.claude/skills/`, codex and dsh `.agents/skills/`, from the entry-point evidence). So moving the
knowledge bought no agent anything. `renderRoster` writes every knowledge and skill row before the rules. Measured on
a fixture shaped like the pilot (design §0.3): a region of 46,279 bytes whose first rule starts at byte 37,046, past
the 32,768 one agent reads; 63,527 had every document been described, so fixing frontmatter first makes it worse.
The proposed region is 19,010 bytes with no knowledge of its own and with 169 documents. Here it drops from 24,064
to about 19,650 bytes, which is what lets LAYOUT6's brief fit under 32,768.

**Rejected** (design §1.7, §2.8, §3.4 and §4.3 have each with its reason):
- **Rewriting every reader and keeping the move**: a large diff in files a doctrine change is not about, for
  knowledge no agent reads by folder.
- **Declaring skills in place**: a skill outside an agent's skill root reaches no agent.
- **Only *red never closes done***: every repository with its own knowledge folder would stop the same way.
- **A knowledge mirror or a link at the old path**, and **the canonical knowledge in the declared folder**.
- **The canonical rows in the region and the repository's own on demand**, **knowledge by folder**, **a cap**,
  **shorter rows**, **the rules before the tables**, and **raising the budget**.
- **The set-up describing every old document**, **a capped number**, **a description guessed from the heading**,
  and **keeping the warning**.
- **Growing the follow-up's tree from the red branch**, and **repairing it by hand**.
- D59's rejection of a pointer in place of the rules stands: this moves the list of the on-demand tiers, not a tier.

**What it amends, when built.**
- D124 §2.1 (the follow-up's case), §2.3 (the checks first, the knowledge in place), §2.5, §2.6 (the bounds) and
  §2.7 (the close).
- D117 §2.1 and §5.4 (a declared knowledge folder is read in place), §6.2's steps, and `LayoutFacts.Clean`.
- D122 §2.7 (DOC3): `knowledge` takes a folder; `skill` stays refused.
- D59, and D7 through it: the region's on-demand tables move to `<target>/INDEX.md`.
- The canon's `doc-loader` (step 2) and `development-documents` (one sentence), with a changelog entry for every
  adopter; the adoption playbook (local); BUDGET1's arithmetic.

Each row that builds a piece notes the amendment where it lands.

**What the checks do not cover.** This change is documents only, and nothing is built. The pilot's facts are the
owner's report: the repository is private and was not read. The fixtures were synced by today's CLI in a gitignored
scratch folder, and their names are shorter than the pilot's. *Proposed* bytes are the rendered region with its
tables cut and a drafted pointer put in, never rendered by built code. No token was counted. What each agent lists
and reads is the entry-point evidence's reading of shipped code, not a turn. Whether a real session runs the checks
first and keeps them green is WSSETUP14f's to show. `verify` checks this entry's shape and the design's links, and
none of their words.

**Amended 2026-10-02 (KNOW2, D129).** The split stands. `doc-loader` step 2 searches a long index in more than one
wording and asks a connected search too, `skills-workflow` sends a session to its agent's own skill list first, and
`development-documents` gains a sentence on naming a document by its subject
(`docs/2026-10-02-knowledge-design-review.md` §4.2–§4.4).

**WSSETUP14a, built 2026-10-02: the index leaves the region.** `sync` writes `<target>/INDEX.md` (`renderIndex`),
its content decided at plan time over the tiers as the sync leaves them (`planIndex`), so §2.4's *as it would be
written* is known before anything is. The lock's `index` names it, and `check` rebuilds it offline and names it apart
from the region (`index`, `roster`). The region keeps the rules table, §2.2's pointer as written (D129 §4.1), the
mirror sentence, the rooms and *Where things are*; the canon takes the review's §4.2–§4.4 words. Choices the design
left open:
- A skill's row names its entry file, `…/SKILL.md`, since an agent opens a row as written.
- A lock naming an index anywhere but its own root's `INDEX.md` is refused, as D18 refuses a crafted path.
- A collision at the index yields to `--force` like every collision, and says what it overwrote.
- `doc-loader` keeps *a shortcut table elsewhere is a convenience, not the registry*, which D129 §4.5 cites.

Measured after the re-sync: this region 24,522 → 20,067 bytes (§2.6's ≈20,100 plus 22, so 55 under), its first rule
at byte 3,014; each example 19,309, first rule at 2,256; `skills-workflow` +24 bytes as wrapped. Proof:
`layout-index.test.ts` (each §2.4 cell, the 169-document fixture leaving the region byte for byte, the migration) and
the D48 §2a scan widened to a connected search (`dogfood.test.ts`), seen failing on `doc-loader` drafted without
*where none is*. Left to their rows: §3's table (WSSETUP14c), `documents.knowledge` and the service test (14b), and
`SetupBrief.Twins` and the playbook, which still say *the generated index in `AGENTS.md`* (14d). Not covered: whether
a session follows the pointer or searches a long index (KNOW3a; every bench session read a 23 KB one whole), and the
release rehearsal's updated checks, written and not run here.

## D129 — Knowledge is found from files first: D128's index is the floor, searched in several wordings, with recall by meaning pulled where connected and pushed only where Daoris writes the prompt (2026-10-02)

**Measured (KNOW3, `docs/2026-10-02-knowledge-bench-results.md` §3, §6).** 72 real headless sessions over 73
documents, one harness and one model: every design found every document, the control too, so the designs differ in
cost, not recall. The region's table took the fewest calls and cost 9,447 tokens on every request (about 21,800 at
169). D128's index cost one more call, and every session read it whole rather than searching it. A pushed keyword top
5 was cheapest per hit when its ranking was right, which was 5 of 12 times. That confirms the index leaving the region
and recall as an accelerator over a floor that finds without it. It leaves two things open, for KNOW3a: whether a
54 KB index is still read whole, and whether a ranker by meaning lifts the push's 5 of 12.

**Decision (KNOW2, the owner's question whether D128's index is the best knowledge design, and whether Lyntai's file
storage is better).** The contract is `docs/2026-10-02-knowledge-design-review.md`: what each agent does today (§1),
eight candidates (§2), the comparison for the 169-document repository (§3), the recommendation (§4) and the build
(§5).

1. **D128's split stands as the floor.** The region keeps the rules, the pointer, the rooms and *Where things are*;
   `<target>/INDEX.md` lists the knowledge and the skills, generated by `sync` and held by `check`. It is the only
   design that is bounded in the region, reaches every agent, needs nothing running and proves the list whole.
2. **A long index is searched in more than one wording**: the task's words, their synonyms, and the folders it
   touches. Where a search over the repository's knowledge is connected, it is asked too, beside the index searches.
   A search that finds nothing has not shown that nothing applies.
3. **A document is findable without the index**: named by its subject, with when it applies in its first lines.
4. **`skills-workflow` names the agent's own skill list first** (+22 bytes in every region), since every agent lists
   its skills and the region no longer does.
5. **A set-up's close names each hand-written index** the repository keeps and how it differs from the generated
   one, and deletes none.
6. **Recall by meaning is the driver's accelerator, held for measurement**: up to five headlines in the driven
   target prompt, one slot reserved for the best keyword match and one for the best meaning match, failing open;
   then a prompt hook for chat sessions on Claude Code. Never in the canon or a committed file.
7. **Lyntai's file storage is declined as the knowledge store**, the first-goal study §3's three reasons re-tested and
   standing. Its reserved slots go into the recall's ranking; *headlines, then expand* is already Daoris's shape.

**Why.** The makers converge on a headline per entry always loaded and the body on demand, each capping the headlines
in its own budget by use, which a committed file cannot know; so the headlines go on demand, in one checked file. Every agent's own search is lexical, and on the owner's own memory store keyword search fell from 9 of 12 at 16
entries to 4 of 13 at 112, while keyword and meaning together found 11 of 13. So D128's *search it when it is long*
inherits the miss rate that grows with the store, and recall by meaning is the one channel cheap per find and robust
to wording, which needs a running service and so can only accelerate. The owner's install shows sessions reading
knowledge (25 of 89 reads in one session) and opening hand-written index files unprompted. The prompt Daoris composes
reaches every agent on both doors, where a hook reaches one or two.

**Rejected** (the review's §2 and §7 have each with its reason):
- **The index in the region**: it grows with the repository, and the rules fall past codex's cut.
- **Knowledge as skills**: the catalogs shed descriptions at 169 entries, and it is the move D128 forbids, mirrored.
- **Knowledge in rooms as the mechanism**: cross-cutting knowledge has no folder, and codex never loads a room.
- **Search with no index**: nothing proves the list whole, and an undescribed document escapes a frontmatter search.
- **Path-scoped rules**: one format per agent, none on codex or dsh.
- **Lyntai's file store**: one owner where git and every branch write, sequential ids, no vectors, and a .NET process
  between the doctrine and its offline `check`.
- **Push as doctrine or a committed hook** (D48 §2a, D32), **a threshold that moves the table in and out of the
  region**, and **a cap on the index**.

**What it amends, when built.** D128 §2.5 and the rows WSSETUP14a and WSSETUP14d; D124 §2.7 through D128 §1.6 (the
close names hand-written indexes); the D48 §2a canon scan, which gains a connected search beside *quest*. The
first-goal study §3 stands, re-tested. Each row that builds a piece notes the amendment where it lands.

**What the checks do not cover.** This change is documents only, and nothing is built. The install's counts, the
owner's store and its measurements, and Lyntai's lock and scan facts are the parent's reports, not re-run here. The
makers' cells are their documentation as fetched on 2026-10-02, or the entry-point evidence's reading of shipped code;
dsh's skill budget and prompt hooks, and hooks under codex-acp and in Daoris's composed settings, are not measured.
Whether recall by meaning beats word searches on repository knowledge is KNOW3's to show, and its numbers were not in
hand when this was written. `verify` checks this entry's shape and the review's links, and none of their words.

## D130 — The person says which accounts may run each workspace and how they are used, for any number of accounts; Daoris weighs what each has left only by the agent's own word, spreads only what runs at once, and infers nothing (2026-10-02)

**Decision (TOOL6).** The owner, 2026-10-02, asked which account should run a workspace whose account was
spend-limited: *"there are multiple accounts we should be able to set option how switch works and how to optimize the
account use since there are 3 accounts"*. Asked the open questions, they answered: *"yes all three accounts can run the
work workspace this really depends on how many token left since all 3 are sub based and how accounts been used like
one by one or 3 parallel or other logic should be configurable"*, and *"3 is because I only have 3 not limited to account
numbers"*. D125 has one policy: walk the order when the default is not ready. Read from the code at `7cca0e1`, with
TOOL4f landed, two of its readings infer what nobody said. A workspace that names its own
default and no list takes the machine's list, so a work workspace rotates onto personal accounts. A workspace that
lists accounts and names no default starts on the machine's default, outside its own list. And D125's observation 2
was two driven sessions on one account refused by one limit: a limit takes every session on its account at once. The
contract is `docs/2026-10-02-account-use-design.md`.

1. **Five aims pull apart** (design §1): keep the person's own accounts fresh, lose little work to a limit, get the
   most done in a week, keep work readable, and keep work where it belongs. The last is the person's to state, never
   inferred. The others are traded by settings, and the default is D125's.
2. **Any number of accounts.** An agent may have one account or many, and nothing counts them: every rule reads the
   list it is given, with one account each setting falls back to it, and no door refuses a list for its length.
3. **Per agent and scope, the machine or one workspace, the person states**: the list (D125's order); *start on*,
   `list` (the default), `left` (most left first) or `soonest` (soonest reset first); *sessions at once*, one by one
   (the default) or in parallel; one account *kept for conversations*; and *switch before the limit*, off by default,
   with its *near*. A start reads one scope: its workspace's when that names a default or a list of its own, else the
   machine's; a scope with no list is its one account, D125's behaviour byte for byte. A workspace's list comes with
   its own settings, never mixed with the machine's. *List order, one by one* is D125's rotation as built.
4. **The list is the whole set.** An account a scope does not list never carries its starts; a person's pick is the
   one exception. A scope with a list begins at its default, or else its first, and a default outside its scope's list
   is refused at both doors. A workspace that names a default and no list rotates nowhere. The tool's own sign-in runs
   only where no default and no list name an account.
5. **The wait asks.** When every account a workspace may use is cooling and others are not, the hold's sentence
   names them, with *Let `account-1` run `work`…*, the `order` command and Ask Daoris's card. Daoris never takes the
   answer.
6. **One walk** (design §4.1): the list begun at the start's account; a driven start drops the kept account; *start
   on* orders it; *in parallel* orders by Daoris's live sessions on each account, fewest first, so *start on* breaks
   ties; outside *list order, one by one* a quest's carry-on or resume stays on its last account while that is ready
   and not near; with *switch* on, a near account goes last; the first ready runs.
7. **What is left is the agent's own word, and nothing else.** What each agent says about its windows on the door its
   session runs on is kept per account in `windows.json`, as a floor as of when it was said, gone at its reset; a
   stated weekly reset is kept, since the maker fixes it per account. `left` ranks accounts said to be clear (the less
   used first), then accounts that said nothing, then accounts said to be near; `soonest` ranks by the earliest reset
   known, then accounts with none. **Absent is never zero, and never full**: an account that said nothing is unknown,
   between clear and near, and with nothing said by any account both are list order and say so. No probe is made to
   find out. A table per agent reads the field and grows only from a frame recorded on Daoris's door (TOOL4b, amended).
8. **Switching before the limit** passes an account its agent said is near (its own warning word, else at or over
   *near*, 90 by default, or drawing on usage credits) while another account is ready, and runs it when none is: never
   a wait, a cool-off or a stop.
9. **A limit history and Daoris's measured use are reported, never acted on** (D54): the limits each account met per
   window, with Daoris's sessions and tokens before each, labelled as Daoris's share. No capacity, share or price.
10. **The cap stays one number for the machine**, K. *In parallel*, N accounts hold at most ⌈K ÷ N⌉ sessions each, and
    one limit cuts off no more; *one by one*, one account holds them all. There is no cap per account.
11. **The doors.** `daoris agent profile use <agent> [--prefer …] [--parallel …] [--keep …] [--early …] [--near …]
    [--workspace W]`, `order`'s new refusals and `daoris agent list` on the terminal; the screen's controls fold into
    TOOL4g; Ask Daoris gains a `use` door, and a change that widens what Daoris may spend is a card. A conversation cut
    off by a limit offers *Continue on `account-2`*: a new conversation, handed the last plan and last words.
12. **Each account's own plan and terms apply**, and the screen says so. Daoris uses only accounts the person holds
    and listed, for that person's work, signed in through the unmodified tool's own sign-in, and claims nothing about
    whether a person's use of several accounts fits a maker's terms.

The build is TOOL6a–d, the design's §10, which also amends TOOL4b, TOOL4g, TOOL4h and TOOL4i rather than duplicating
them.

**Why.** *Which accounts may run this work* is a policy the person holds (a work seat, personal plans) and Daoris
cannot see, so the only safe reading is the one they wrote, and an account they did not list for a workspace must
never reach it. That is also the answer to the owner's question: the workspace's list says it once, and the wait asks
when it matters. Spreading was rejected in D125 because it spends windows the work did not need and splits a quest's
sessions. It stays off by default, and as a choice it spreads only what runs at once, which is exactly what one limit
cuts off together, while a quest keeps its account. Daoris knows an account's state only from what the agent says:
its limit sentence today, and its own field about its windows once a door is recorded carrying it. Context
high-water is not spend, and Daoris sees only its own share of an account, so a capacity learned from either would
cool an account that is not spent or miss one that is: D125 §1.4's reason against guessing from tokens. The makers'
own pages, read on 2026-10-02 (design §0.3), say the rest: windows reset on the maker's clock, every five hours and
weekly at a time fixed per account; one allowance covers every surface, which is why Daoris sees only its share; a
team member's spend limit pauses credits to the month's end while the seat's window still resets; and the agent has
its own word for what is left (`allowed`, `allowed_warning`, `rejected`, a reset per window), which no door of Daoris's
is yet known to carry. So *how many token left*, the owner's measure, is weighed by that word alone, as a floor as of
when it was said, and an account that has said nothing is neither spent nor fresh: guessing either way would cool, or
spend, an account on a number Daoris made up. Switching early is a pass on that word, never a wait. The owner's *one by
one or parallel or other logic* is two questions, which account first and how many at once, so they are two settings
that compose. The tools that rotate accounts learn quota by asking and switch by replacing a credential, which is what
D49 §4 and D125 §1.4 refuse; their shapes, fill first, round robin, least busy and most quota left, are the ones
weighed.

**Rejected** (design §12 has each with its reason):
- **Inferring a workspace's accounts** from an address, a name, a team plan or where its work last ran.
- **A workspace that names a default inheriting the machine's list**, today's reading, and **mixing scopes**.
- **One choice of four modes**: it cannot say *most left first, in parallel*.
- **Spreading as the default**; **round robin by start**, **a share per account**, **a cap per account** and **ranking
  by Daoris's own measured use**.
- **Treating an account that said nothing as spent or as fresh**, and **probing an idle account** to learn what it has
  left: the probe spends what it measures and misses the person's own use.
- **Learning capacity and acting on it**, and **asking the agent's usage command before a start** (a text door, and a
  process spent to ask); **polling for quota**, and **switching by replacing the active sign-in or its credential
  file**, as the rotating tools do.
- **Near as a wait or a cool-off**; **switching a running session or conversation**.
- **A kept account as a second list**, and **keeping or listing the tool's own sign-in**.
- **Any rule or door that counts accounts**, and **a new `daoris driver` verb**: every choice here is per agent and
  per scope.

**What it amends, when built.** D125 §3.1 (one scope; the list the whole set; a default outside it refused), §3.3 and
§3.4 (the walk is the design's §4.1; the tool's own sign-in only where nothing is named), §5.1 and §9 (spreading a
person's choice), §6 (*Rotate* becomes *Use*; the `use` door). D94 §4 (`account.rotated` gains `why` and `scope`;
`account.near`). D110 (the `use` door). Unchanged: D57 (no price, no provider's console), D58 as D125 amended it, D48 §2a, D49 §4, D66 §3, and D125
§1–§2, §3.2, §3.7, §4 and §5. Each row that builds a piece notes the amendment where it lands.

**What the checks do not cover.** This change is documents only, and nothing is built. Its statements about today
were read from the code at `7cca0e1`: the walk, the selection and its readiness, the resolution and the order's
reader, the log lines, the usage record and the protocol door's measure, the native door's mapper, the planner's cap
and its session view, the driver's settings, the conversation runner, and the CLI's `agent` usage. The evidence is
D125 §0.2's, given to that branch, and no transcript or record was read here; that observation 2's two sessions shared
one account rests on their one reset. The owner's answers were relayed to this branch by the parent. The makers' cells are their documentation as fetched on 2026-10-02, and the
design's §0.3 marks each line not read on the maker's own page: among them when a five-hour window starts, the
TypeScript declaration of `rate_limit_event`, every help.openai.com article and OpenAI's terms. The install's cap of 4
is the brief's. Not measured: every item of the design's §11. Found, and the owner's to weigh: the consumer terms'
sentence on automated access bears on driven sessions as a whole, and this decision does not answer it. `verify`
checks this entry's shape and the design's links, and none of their words.

**The owner's reading (2026-10-02).** Shown the terms' lines on automated access and on coordinating several
accounts, the owner answered: *"we are not coordinate, we are doing the same work in the same workspace/repo account by
account just to optimize the usage"*. One person runs their own work, in one workspace, on accounts they each hold,
one after another, to use what they pay for. That reading is the owner's and stands. Point 12 stays as written: the
screen names each account's own plan and terms, and Daoris claims nothing about them.

**What *in parallel* means (the owner, 2026-10-02):** *"parallel mostly means if we running multi repo or multi
sessions"*. It is the sessions running at once, across repositories or within one, that spread over the accounts,
each start going to the account running the fewest. A quest's carry-on stays on its last account while that is
ready, as point 6 says, and moves to the next account when that one hits its limit (the owner: *"a quest can be
running on different account if one account runs out of the limit"*), as TOOL4f's first real rotation did on 2 October.
TOOL6b builds it so; TOOL6a's words for the setting say so.

**The goal, in the owner's words (2026-10-02):** *"so the goal is to optimize the limit and usage of multiple
accounts"*. Every setting above serves it: work keeps moving when one account is spent, and no account sits idle while
another is cut off. It is judged by TOOL4h's report (the limits each account met, the sessions each ran, the starts a
setting moved) and TOOL4i's run, never by a number Daoris made up.

## D131 — An answer continues the session: its record reopens and its harness conversation resumes where the account, the adapter and the tree are the same; otherwise today's carry-on, saying why (2026-10-02)

**Decision (ANSWER1).** The owner, 2026-10-02: *"whenever I input anything say the session was waiting for my input
and after I input it starts a new session? isn't this should be continue"*. Seen on the install the same day: a driven
session on `claude-code-acp` 0.84.0 parked to ask, the person answered, and the driver started a new session in the
same tree, the agent's own conversation gone and a second row on the screen. Read from the code at `32cbf03`, the
answer ends the parked record `completed` (D83) and the planner opens a new one (D80's carry-on); nothing resumes an
agent's own conversation, though the adapter offers `session/resume` and `session/load` and the ledger already allows
`awaiting-person` → `working`. The contract is `docs/2026-10-02-answer-continues-design.md`.

1. **Resume when it can work.** An answered park is continued by its own record: `awaiting-person` → `working`, the
   harness's own conversation resumed in the same tree, and the answer its next prompt, verbatim. The protocol door
   sends `session/resume` where the agent advertises it (no replay, since the record already holds the conversation),
   else `session/load` with its replay not kept again; the native door runs `claude -p <answer> --resume <id>`. The
   conversation's id is kept the moment the wire says it, beside the transcript, never read from the agent's home.
2. **It needs** the record still parked with its answer, the same adapter, the same account (the conversation lives
   in that account's configuration home, and a record names one account), the same tree standing, a kept id, and a
   door that can resume. A changed harness version is said on the resumed run's first line, never refused.
3. **Otherwise, today's carry-on, said why.** The park ends `completed` with its answer and the reason, and a new
   record carries the quest on in the same tree with the answer, the last plan and the last words. The reason is one
   line on its note, by a code: `account`, `adapter`, `unkept`, `tree`, `unable`, `offered`, `gone`, `refused` or
   `ended` (design §2). A note never names an account nor quotes the agent's refusal. The machine log writes
   `session.answered` {session, adapter, resumed, why} once per answer taken up.
4. **One Daoris record for one harness conversation.** A resume reopens the record that parked; a fallback is a new
   record because it is a new conversation.
5. **The other entries stay new sessions**: *Try again* after a person's stop, a carry-on after a time-out, a crash,
   a refused turn or an interrupted take, and D79's resume, which is the same shape and held. Each one's record is
   finished, and a finished record does not move.
6. **The service and the page follow** (design §5): ANSWER1b keeps an answered record parked, and clears `answer` on a
   move into `awaiting-person`; ANSWER1c shows an answered park as carrying on. Until ANSWER1b, the driver carries every
   answer on as today, saying `ended`.

**Why.** The person answered a question, and a conversation that asked it is the one that should hear the answer: a
new session rebuilds from a handed summary what the first one knew, and the screen shows the person a second session
for one piece of work. D46 held resume as an adapter capability until a real run asked for it, and D90 already keeps
a working session's context for what the person adds; a park the person answers is the same need after the turn has
ended. The record is the authority (D46 §4), so one record standing for one conversation is what makes the page, the
log, the review and a teammate's view show one session without each learning a fold.

**Rejected.**
- **A linked record that the page folds into one thread.** It leaves the ledger alone, but every reader of records
  would have to learn the fold, one conversation would have two homes, the log would count two sessions, and a
  teammate would see two rows unless the link travelled as a new field.
- **Reopening a finished record.** A finished record does not move, has travelled, and is what the strikes count.
- **Resuming the conversation in a new record** for a fallback, a stop or a cut-off. That is the linked shape: one
  conversation over two records.
- **`session/load` first.** It replays the whole history, which the record already holds; `session/resume` sends none.
- **Checking for the conversation in the agent's home before resuming.** A read inside an account's directory (D66 §3);
  the agent's own refusal is the answer, and the fallback takes it.
- **Refusing a resume across a harness version.** The native `claude` updates itself, so nearly every answer would
  fall back; the run says its version instead.

**This amends:** D83 (an answer no longer ends the record, once ANSWER1b lands, and a continued park is not a new
session), D80 and STANDDOWN2's carry-on (now the fallback, said why), D46's held resume (built for an answered park),
and D94 §4 (`session.answered`). D51, D58, D79, D90, D104, D125 and D130 stand.

**What the gates do not cover.** This entry's statements about today were read from the code at `32cbf03` and from
the adapter's and SDK's published packages at their pinned versions; no real agent was resumed. The native door's
`--resume` and its `init` line are the maker's published shapes, not a run on this machine. `verify` checks this
entry's shape and the design's links, and none of their words.

**Built 2026-10-02 (ANSWER1a): the driver's half** (points 1–4; design §1–§3), held by `ContinuationTests`,
`AnswerContinuesPlanTests`, `HarnessConversationsTests`, `AcpResumeTests`, `NativeResumeTests` and `SessionLogTests`.
What building it settled:
- **The id is kept at the wire's first word.** `AcpSession` tells `session/new`'s id as it arrives (`onConversation`), so
  a turn that then fails or parks still leaves it; the native door keeps its `init` line's `session_id` once its output
  ends. Both land in `HarnessConversations`, for a quest's session only, since an intake is answered through its ask.
- **A refusal keeps its code and the agent's own sentence** beside ACPDATA1's data (`AcpRefusal.Code`, `.Words`): a
  resume refused `resource_not_found` is `gone`, any other `refused`. A load's replay is dropped by its `sessionId`
  while the load is answered.
- **The judgement's order is the record, the adapter, the account, the tree, the kept id, then the door**
  (`Continuations.Judge`), so the line said is the first a person can act on.
- **The planner continues through the carry-on's verdict**, `continuing`: the park is not busy with its own tree, quest
  or repository, and already holds its slot of the cap. The person's hold and the strikes still stand.
- **A refusal on the wire ends the record from `working`**, since a run moves its record to working at spawn; the park's
  note keeps what it asked and adds why, and the carry-on starts in the same look, taking the starting hold (LEFT2) again
  until its own record holds the tree. A resumed run that could not start ends the park the same way, as `refused`,
  whose line became *its conversation could not be resumed* to cover it.
- **The resumed run's evidence counts from the record's own base commit**, so the review's range is the whole session's.
- **An answer that keeps its park tells the watchers nothing** (`ServiceClient.AnswerSessionAsync`): the log would
  otherwise write a second park, or an ending that did not happen.
- **`session.answered` rides the account lines' channel**, which writes a catalogued line as it is given; the machine
  log design's §4 lists it.

**What the gates do not cover.** The `Process` half was not run by this branch: `AnswerContinuesTickTests` runs three
real ticks on a protocol stub that speaks `session/resume`. An answer resumes the parked record's own conversation, with
one record from start to end. A conversation the stub no longer has is carried on in a new session in the same tree,
saying why. An answer on another stub account never asks the stub to resume. Its stand-in service models ANSWER1b, and
the real service still ends the record as it takes the answer, so until ANSWER1b lands every answer reads `ended` and
is carried on as before. The page still shows an answered park as waiting on the person for up to one look (ANSWER1c).
The family rehearsal's protocol stub was not taught `session/resume`: no rehearsal reaches a resume before ANSWER1b.

**Built 2026-10-02 (ANSWER1b): the service's half** (point 6; design §5), held by `SessionLedgerTests`,
`SessionStoreTests` and `LocalHostTests`, and the family rehearsal's 17a and section 4. What building it settled:
- **The answer keeps the park in one step**, under the store's write lock as a move is (REV3), so a second answer and
  the driver taking the park up never both read it parked. It is a new revision, so its note travels as a move's does;
  the words themselves are still answered to this machine only.
- **A second answer replaces the first** on the record and on the note. The note's *Answered:* line is replaced only
  where it ends the note and quotes the answer held, so nothing else the note says is ever cut.
- **Clearing is the ledger's to say** (`SetStateAsync`'s `clearAnswer`), on a move into `awaiting-person` alone: working
  keeps the answer while it is worked on, and `completed` keeps it for the fallback's open, which still carries on a
  `completed` record with an answer.
- **The reply is the session as it stands**, still parked, which is how `ServiceClient.AnswerSessionAsync` tells the
  watchers nothing moved; its sentence says it carries the quest on at the driver's next look. An answer once the
  driver has taken the park up is refused naming the state the record is in.
- **The rehearsal's park is a carry-on's.** The protocol stub takes through the service's HTTP door, which marks no
  session as the taker, so a first session ending with its quest taken reads as a stand-down; a session carrying a
  take on parks (D80). So 17a makes the cut-off through the doors as section 4 does, the driver carries it on, its
  session asks, `daoris-driver answer` answers, and the next look resumes the same record's conversation over
  `session/resume`. The stub advertises `resume` as the real adapter does, and resumes whatever conversation it is
  asked, keeping no history.

**What the gates do not cover.** The family rehearsal was written, not run: it is the parent's to run at the merge,
with `AnswerContinuesTickTests`. A take through a connector, which a real agent's first session parks on, is
`McpToolsTests`' (STANDDOWN2), not 17a's. Words in lanes this branch does not hold still say an answer ends the
record: the headless host's usage for `answer`, and the comments on the page's answer box, its query and its frame
(ANSWER1c's lane).

## D132 — An ask's work is paused and resumed whole on this machine, and abandoned on a listed second press that discards only what nothing else holds (2026-10-02)

**Decision (PAUSE1).** The owner, 2026-10-02, wanted to stop a request whose work had gone the wrong way: *"so there
is a pause and cleanup feature needed"*. The parent paused it by hand: `daoris driver hold <repository>` held every new
start in the whole repository, and *Stop…* on the running session held its one quest (D126, SESSUX1b). Nothing paused
the ask, every quest and session of it at once. Nothing removed what it left: quests open or taken, trees under the
home, `daoris/*` branches with commits nobody pushed, and sessions in the list. *Close ask* writes only the ask's row,
and its quests go on. `docs/2026-10-02-pause-and-clean-up-design.md` is the contract; its §0 is what was true at
`32cbf03`. It settles:

1. **An ask's work is one thing, read by one reader.** It is the quests asked by the ask, chain steps included; every
   quest a session of the work published, which brings in a question asked of another repository (D79), applied
   again to what that adds; their sessions and the ask's intake; and their trees, branches and landings here. A
   quest's work starts from that quest. `AskWork.Read` answers it for both doors (`WORK_PLAN`, `daoris-driver`), and the
   look reads it again each time, so a step or a question that appears after a pause is paused too.
2. **Pause stops the work on this machine and keeps everything.** It stops every live session of the work as the
   person's stop, starts nothing of it (the ask's intake included), and keeps each open quest's place in the queue and
   each taken quest's take, tree, wait and strikes. **It takes nothing *Resume* cannot give back**, so a session waiting
   on you stays parked, and a running intake goes on, its publishes paused with the rest. It asks once when it ends
   work in flight.
3. **The planner's `Paused` verdict comes before every other**, a person's stop included. Its sentence names the pause
   and *Resume*, with its terminal line. The look computes the paused set and the planner reads it.
4. **Resume releases the pause and every stop it made** (`released`, SESSUX1b's field), so a taken quest is carried on
   in its tree and an open one starts in its place. What still holds (a stop made before the pause, the strikes, a
   repository's hold, an account's cool-off, another pause) is named, never released.
5. **A pause is kept in `driver.json`** as `pausedAsks` and `pausedQuests`, each with when it was made and the stops it
   made. They are twins (`DriverConfig.cs`, `driverconfig.ts`). `daoris driver list` shows them; `daoris-driver` pauses
   and resumes, since it reads the work and reaches the running loop.
6. **Abandon is listed first and done on the second press, with the person's reason**, as *Archive what ended…* is.
   The second press sends exactly what the first listed, and judges each piece again. In order, it pauses the scope,
   stops and ends the work's sessions, declines each open quest and each quest this machine's session took with the
   reason, closes the ask with it, syncs, discards trees and branches, archives sessions, tidies `driver.json`, and
   writes the record.
7. **Abandon discards a tree and its branch only when nothing it holds is anywhere else**: no commit after the
   session's base on any ref but this machine's local `daoris/*` branches. Landed, a branch of the person's, a branch
   a landing made, a tag, and **pushed, a remote `daoris/*` branch included**, each keep it, named. So does a session a
   landing names, standing or a trace, and a count git cannot give. Uncommitted changes in Daoris's tree go, named on
   the first press. This is D88's proof inverted: the clean-up removes what is proven elsewhere, abandon what is
   proven nowhere else, and a branch partly elsewhere is kept by both.
8. **Abandon takes only what Daoris made for the work on this machine.** A quest taken on another machine, or taken here
   outside Daoris, is kept and named. A done quest keeps its record. A branch of the person's, the line, a remote
   branch, the intake's room, kept files, the usage and the log are never touched. Records are archived, never
   deleted.
9. **What the record keeps.** Each declined quest's note is the person's reason, verbatim, and travels. The ask's close
   note is the same reason. `<home>/abandoned.json` keeps, per abandon, what went (each tree's repository, branch, tip
   and counts) and what stayed with its reason, and never a path. The log gains `work.paused`, `work.resumed` and
   `work.abandoned`, names and counts only.
10. **A pause is this machine's; a decline travels.** A teammate's driver may still take an open quest of paused work,
    and their running sessions are not reached (D47 §4). A taken quest's pause holds everywhere, because its take
    does. **A decline the abandon makes of an open quest carries `whileOpen`**: it applies only to an open quest, so
    pushed after another machine's take it becomes a conflict on the quest (D68 rule 2) and the take stands. The
    abandon runs a sync pass before it answers, and says which declines were confirmed, lost or unconfirmed. A remote
    has no pause or abandon route.
11. **The doors.** Pause, *Resume* and *Abandon…* are on the ask's page and the quest's page. *Pause quest…*, *Pause
    ask…* and *Resume* are also on a session's header and row, where work going wrong is met. Abandon is on the two
    pages only, where an ask or a quest is decided. At a terminal: `daoris-driver ask --pause|--resume|--abandon` and
    `quest pause|resume|abandon`, an abandon listing until `--reason` and `--yes`. Ask Daoris gains a `pause` kind
    (`pause`, `resume`). Abandon is exempt there, since its reason is the person's answer (D37).
12. **Names.** *Pause* is 暂缓 and its inverse *Resume* 恢复. *Abandon* is 放弃, never *clean up*: the glossary's *clean
    up* (清理) keeps exactly the work this throws away, and a destructive press must not wear a safe press's name.
    Both are new glossary terms.

The build is PAUSE1a–g, the design's §9.

**Why.** The owner's work went wrong in one ask, and every act they had reached either too far (a whole repository) or
too little (one session, one quest). A pause that stops what runs and keeps the take, the tree and the place is the
act a person reaches for when they want to look before they decide. Once they decide, giving the work up should be one
listed press. That press must throw away only what nobody else can have: unpushed commits and uncommitted changes in
Daoris's own trees. Anything that reached a branch of theirs, the line or a remote has left Daoris's hands, and is
named, not touched.

**This amends:**
- **D126 §3.3 and SESSUX1b's note**: a pause is the reason its quest sits before a stop, and *Try again* is not offered
  while it holds; `RETRY_QUEST` refuses with `QUEST_PAUSED`.
- **D51 rule 7 as D88 amended it**: a second removal by a person's press, behind the inverse proof.
- **D68 rule 2 and the sync design's §5**: a `whileOpen` decline after another machine's take is a conflict.
- **D65**: an ask's close leaves its quests, and its page now says so; abandon closes it and declines them.
- **D94 §4**: three events. **D110**: the `pause` kind; abandon exempt. **D116's glossary**: *pause* and *abandon*.

**D46 §3–§4 (no new state), D58 (a stop is no strike), D68's one lock, D95, D47 §4 and D32 stand.**

**Rejected.**
- **Naming the second act *Clean up***, as the owner and the dispatch did. The glossary's *clean up* is D88's removal of
  what landed, the safe press in Settings, and it keeps exactly what this one throws away.
- **A pause that travels.** It would be a new operation every machine's planner obeys, a second lock beside the take.
- **Stopping a running intake, or a parked session, on pause.** Neither comes back on *Resume*: one intake per ask, and a
  parked session's question would be lost.
- **Narrowing a repository's hold with a filter.** A hold leaves what runs running, and `holds` is read by two twins.
- **Deleting the quests nobody started**, as D95 allows. A delete says the ask was a mistake; abandon keeps the reason.
- **Discarding the unlanded half of a partly landed branch.** It would decide which half the person meant; its review
  does.
- **Abandon judging landed work, or declining a quest taken elsewhere or outside Daoris.** Neither is Daoris's work
  here; *Decline…* on the quest's page is one deliberate answer.
- **Prefixing each decline's note with Daoris's words.** It would put English into content; the abandon record says
  what went.
- **Ask Daoris abandoning**, **several asks at once**, **an undo**, and **a new ask state** for *paused* or *abandoned*.

**What the gate does not cover.** This decision is documents only. The code was read at `32cbf03`, as the design's
§13 lists. That SESSUX1g's request folder is built as D126 §7.1 says is assumed, and PAUSE1b waits on it. An older
remote applies a `whileOpen` decline as a plain one. Found while reading and filed: D124's set-up plan *Pause* stops
nothing that runs, so by this glossary it is a hold, for WSSETUP7 to name. The code's comments call a repository's
hold *paused*, which PAUSE1b rewords. Not measured: every item of the design's §12. `verify` checks this entry's shape
and the design's links, and none of their words.
**The rules around the goal (2026-10-02).** The owner: *"you can design the rules around this purpose"*. So the
defaults are re-decided on the goal: the most work from the accounts' combined allowance, the fewest stalls, and no
allowance left unused at a reset (design §16).

1. **Every listed account is used toward the goal by default** (`use: goal`). *List order, one by one* is an override
   (`use: order`), D125's walk, kept for a person who wants their accounts used in their order.
2. **No start is held** while an account it may use is ready and a slot is free, so allowance lapses only where there
   was no work or no slot for it.
3. **The default walk** (design §16.3), each step stable: a driven start drops the kept account; an account its agent
   said is near goes last; fewest of Daoris's sessions running first; an account whose known weekly reset falls within
   the next day first, sooner first; furthest behind its week's pace first, where the agent said; least recently
   started first; the list's order last. While every account is ready, none runs more than ⌈K ÷ N⌉ at once.
4. **With nothing said** (today), steps 2 and 5 are inert. The walk spreads by Daoris's own sessions and over time,
   and learns each account's weekly reset from the weekly limits it meets, carried a week at a time where the maker
   fixes it (Claude, C1). It says so on the screen, in `daoris agent list` and in each start's first line, and no
   number stands in for a reading.
5. **Switching before the limit is on by default**, inert until a door carries the agent's word. A kept account stays
   an option, off by default.
6. **A quest's next session goes where the walk sends it.** A carry-on carries no conversation, so its account buys
   only readability, which its first line gives. This amends point 6 and the note on *in parallel* above.
7. **The settings are the list, `use`, `keep`, `early` and `near`**, per agent and scope, for any number of accounts.
   TOOL6a's vocabulary changes: `prefer` and `parallel` are gone; `use` (`goal` or `order`, absent `goal`) is new;
   `early` is on when absent and off only when `false`; `keep` and `near` are unchanged. The terminal door is `daoris
   agent profile use <agent> [goal|order] [--keep <account>|--no-keep] [--early on|off] [--near <percent>]
   [--workspace W]`, and Ask Daoris's `use` door takes the same four. A machine with an order written before changes
   to `goal` when TOOL6b lands.
8. **It is judged** by TOOL4h's report per account and week: sessions landed, hours waited, limit cut-offs, allowance
   left at each weekly reset where the agent's word shows it (*not measured* where not), the limits met, the starts
   each step moved, and whether the cap rather than the accounts was the limit; and by TOOL4i's run under `goal`.

**Why.** One by one meets a limit as soon as the load passes one account's five-hour window while the others sit idle,
and that limit cuts off every session on the account; spreading meets none while the load fits all accounts together,
and a limit cuts off at most ⌈K ÷ N⌉. A week recovers in days and five hours in hours, so the week's risks, lapsing and
running ahead, rank above any five-hour order, and the five-hour window is a gate: passed when near, kept below its
limit by spreading. A week is ranked by its reset only in its last day, when what is left is at risk of lapsing;
ranked so all week, it would pile one account's work onto it while the others' weeks went by. Pace keeps every week
moving, but alone it would pass a small leftover about to lapse, so it ranks below the last day. The fallbacks use only
what Daoris knows: its own sessions, its own starts, and the resets the agents named.

**Rejected** (design §16.5 and §16.9):
- **Keeping *list order, one by one* as the default**: the most cautious choice, and the goal argues against it on
  every measure.
- **The five-hour window's reset as an order above the week**: unknown without the agent's word, and it would spend a
  week to save hours.
- **Ranking every week by its reset all week**, and **holding work to pace a week**.
- **A quest kept on its account**, and **a cached prompt as a reason to keep it** (no page read says one counts less).
- **Keeping `prefer` and `parallel` beside `use`**, and **a cap per account or a cap raised to use more windows**.

**What it amends.** D130 points 3 (the settings), 6 (the walk), 7 (`left` and `soonest` fold into pace and the lapsing
week), 8 (on by default), 10 (spreading is the default), 11 (the `use` door's fields), and the note on *in parallel*
(a quest's carry-on no longer stays on its last account). The design's §2, §4, §6–§10 and §12–§14 carry notes, and the
TOOL6a–d, TOOL4g, TOOL4h and TOOL4i rows in its §10 are amended in place. D125 §3.3 and §3.4 are the walk under
`order`. Unchanged: eligibility (point 4), the wait that asks (point 5), what is left by the agent's own word and
absent never zero (point 7's rule), the cool-off, a limit not a strike, D57, D49 §4 and D66 §3.

**What the checks do not cover.** Documents only, and nothing is built. The arithmetic of spreading is an argument,
not a measurement: neither an account's allowance nor the load is known, and §16.8 names what only a real run shows,
the day's horizon among them. The first real rotation and its weekly sentence are the fix log's and D125's notes, not
re-read here. `verify` checks this note's place and the design's links, and none of their words.

**Built 2026-10-02 (TOOL6a): the settings and their terminal door** (points 2, 4 and 11 and the goal's point 7; design
§2, §3.1, §4.6, §14, §16.6), held by `RotationUseTwinTests` and `RotationTwinTests` on the driver's side and, cell for
cell, by the CLI's `rotation-use.test.ts` and `rotation.test.ts`. What building it settled:
- **A value or a default changes by a row.** Today's defaults live in one place (`USE_DEFAULTS`, `RotationUse.Default`)
  and the ways to use accounts in one list (`USE_MODES`, `RotationUse.Modes`). A value the reader does not know reads as
  today's default and is said by `agent list` and `profile use`; a setting it has no name for is kept as written and
  said. `prefer` and `parallel` are skipped and gone at the next write (§16.6).
- **A choice is written as made**, one equal to today's default included, so a later default never overturns what a
  person chose; only absence means the default. Known settings go out in one order, normalised, then the rest as read;
  both writers write the same bytes.
- **A scope's settings come with its list.** Clearing a list clears its settings; an account removed is kept nowhere.
  `profile use` refuses a scope with no list of its own, naming the `order` that gives it one. `--clear` returns a
  scope to today's defaults.
- **One rule binds a scope** (`scopeProblem`, `ScopeProblem.Of`): its default and its kept account are of its list,
  compared exactly, and a kept account leaves driven work another. `profile order` refuses all three, naming both sides
  and the fix; `profile default` the first, where its scope has a list of its own (a workspace with none takes any
  account and is then that account alone); `profile use --keep` the kept ones. The order door's two kept-account
  refusals keep §4.6's state unreachable by the back door. Tested with one account and with six.
- **`agent list`** prints beneath each list how it is used, a default outside it, a kept account alone in it, and what
  the build does not know. `profile use` with the agent alone adds what each account last said, which today is its
  cool-off or nothing: no door carries the agent's word about its windows yet (§5.3), and the switch says so.
- **The room names the door** (`HelpRoomDoors`, *no screen yet*).

**What the gates do not cover.** Nothing reads these settings to choose an account: `SelectAsync` still walks D125's
order, and `ResolveScope` waits for TOOL6b. §3.1's refusal is the terminal's alone: the screen's `profile-default`
route (`DriverModule.DefaultEdited`, modules) and Ask Daoris's `default` door do not ask `ScopeProblem.Of` yet (TOOL4g,
or a modules row). The `Process` half, the rehearsals and the screen were not run by this branch.

**Built 2026-10-02 (TOOL6b): the walk toward the goal** (the goal's points 1–4 and 6, and points 4 and 5; design §3.1, §3.3, §4.6,
§7, §16.2–§16.4 and §16.6's `order`), held by `AccountRotationWalkTests` (the walk's tables per step, and ⌈K ÷ N⌉ for one
account to six), `AccountRotationGoalTests` (through `SelectAsync`), `AccountWindowsTests` and, in the `Process` half,
`AccountGoalTickTests`. As §16.6 says, a machine whose order was written before now follows the goal unless its scope
says `order`, so *rotation never moves work off a ready account* (D125 §3.3) holds under `order` only. What building it
settled:
- **One scope for every start but a pick** (`ResolveScope`, D130 §3.1): a workspace naming a default and no list rotates
  nowhere; a list begins at its default, else its first, never on the machine's default or the tool's own sign-in; a
  default outside its list is read with the list winning, where D125 held the start on it.
- **The count is Daoris's own records and its own choices.** Each look marks the last start chosen, reads this machine's
  records (`Snapshot.Started`: adapter, account, opened, running) and hands them to the roster (`Look`), which keeps every
  start chosen after the mark. A look begins only once the last look's starts opened their records, so none is counted
  twice or lost. Choices and their counts are made one at a time, so starts begun together spread; one held after it was
  chosen counts until the next look. The wiring panel asks the same walk and counts nothing. A door's sessions count
  for its owner (AGT7).
- **Least recently started**: never first, then by when the record opened, and a start chosen since the look is the most
  recent of all, in the order chosen, whatever the clocks say.
- **A weekly reset is told by a limit** whose window, or what was hit where it named none, is weekly, at a time the agent
  named. `windows.json` keeps it (names, times, the session; what it has no field for kept). It is carried a week at a
  time where the toolchain declares `WeekFixed` (Claude Code, and the stub mirroring it), declared beside `Limits` and not
  in it, since that table grows only with recorded sentences and this is the maker's page (C1); elsewhere it is dropped
  at its reset (Codex). Within a day it ranks first; beyond, nothing.
- **Which step chose it.** `account.rotated` is written when a start ran somewhere other than where its scope begins:
  `why` is the step that moved it (`kept`, `cooling`, `refused`, `signedOut`, or the goal's `fewest`, `lapsing`,
  `leastRecent`), `scope` the workspace or null, `said` false. Under the goal every start whose walk had more than one
  account opens naming its step, against where the scope begins or, where it ran there, against the next account (`list`
  where nothing told them apart), adds the step among the rest where the first was passed for itself, and ends *No
  account has said what it has left yet.* Under `order` a rotation alone speaks, in D125's words. A list of one, a pick
  and a scope with no list say nothing.
- **Keep**: driven work drops the kept account; under the goal a conversation takes it last, or first where it is the
  default; under `order` in its place. A kept account that would leave driven work none is read as none.
- **The wait asks** where the scope names accounts of its own: it adds the agent's other accounts, neither cooling nor
  refused, and the `profile order` that adds the first; a driven start whose kept account is ready says it is kept. A
  machine with no list keeps its sentence byte for byte.
- **`profile use`** prints *next start*: the goal's steps, or the list's order under `order`, and the kept account driven
  work passes. The CLI reads no session record, so it names the steps, not the account they would choose.

**What the gates do not cover.** `AccountGoalTickTests` and the two updated `AccountRotationTickTests` are the `Process`
half, written by this branch and not run by it. Near and pace, and `early` and `near`, choose nothing until TOOL6c. Not
measured: whether a day is the right horizon, or whether spreading meets fewer limits (§16.8). `agent list` and the screen
say neither §16.4's sentence nor the learned weekly resets yet (TOOL4g). The modules build against the change; their
suites, the web's and the rehearsals were not run.

**Built 2026-10-02 (TOOL6c): what the agents say, read** (the goal's points 3–5 and point 7's rule; design §4.4, §4.5, §5.2,
§5.3, §6, §16.3 steps 2, 4 and 5, and §16.4, as `docs/2026-10-02-limit-signals-evidence.md` corrects them), held by
`AccountReadingsTests` (the table and the frame), `AccountWindowsTests` and `WindowsTwinTests` (the file, twinned with the
CLI's `windows.test.ts`), `AccountRotationWalkTests` and `AccountRotationGoalTests` (near and pace, per step and through
`SelectAsync`), the doors' `ClaudeStreamJsonTests` and `AcpTests`, and, in the `Process` half, `AccountReadingTickTests`.
What building it settled:
- **One reader, a table per agent, both doors.** `AccountReadings.Read` reads Claude Code's `rate_limit_info`, which the
  native door's `rate_limit_event` carries and its protocol door forwards unchanged as `usage_update._meta["_claude/rateLimit"]`
  (evidence §1, §3), by the agent's `HarnessToolchain.Windows`. `ClaudeWindows.Words` names `five_hour` the `session` window
  and `seven_day` the `weekly` one; `allowed`, `allowed_warning` and `rejected` clear, near and refused; the scale, a
  fraction; and `isUsingOverage`, drawing on usage credits. Each stands on the evidence's recorded frame, and the two
  unmeasured words on the SDK's declaration, said so. A door reads its owner's table (AGT7); Codex's door forwards none of
  Codex's limits (§4), so it has none. A window the table does not name, or with no reset, says nothing.
- **Kept as the door carries it, per window, by every session on an account**: driven starts, intakes, conversations and
  Ask Daoris, on either door. `windows.json` keeps each window's newest reading (its use, its reset, the standing and
  credits on the window the frame named, when, and which session) and drops it at its reset. The weekly window's reading is
  the account's week for step 4 too, carried a week on as a limit's is; its use is not. A week a limit told says no use. The
  tool's own sign-in keeps nothing.
- **Near, as the evidence corrects §6**: the agent's warning word or its word that a limit was reached, drawing on usage
  credits, or any window's use at or over the scope's *near*. §6 had the word win over the number, on C11's reading that
  Claude Code's word comes without one; the evidence found a number on every frame and the word `allowed` at 88% with two
  hours left (§1.3), so either passes. A near account goes last (step 2) under `goal` and under `order`, unless *switch
  before the limit* is off: a pass, never a wait.
- **Pace** (step 5): the share of the week gone, the seven days before its reset, less the share of its weekly limit said
  used; furthest behind first. An account that said nothing about its week ranks as on pace, between behind and ahead. Below
  fewest running and a week lapsing, above least recently started; nothing under `order`.
- **What it says.** Under the goal every start's first line ends with what each account of its list said, `What each
  account said: …`, each with its age and numbers or `nothing yet`, and says *No account has said what it has left yet.*
  only while none has; under `order` a rotated start adds it where one has. Near names the account passed and what it said;
  pace the account behind, or the one ahead. `account.rotated` gains `why` `near` and `pace`, `said` true where any account
  of the list had said, and `fromSaid` and `toSaid`: Daoris's word for what each of its two accounts said (`refused`,
  `near`, `clear`, or null), never a number.
- **The CLI reads the file through its twin**, `windows.ts`: `agent list` says each account's last reading and its age
  beneath it, and `profile use` the same against the scope's *near*, dropping the sentence once one has said. The
  toolchain's `windows`, twin of `Windows`, says whose sessions speak; any other agent's switch still says its sessions do
  not. This amends §14's *not a twin*: the file is read by both.

**What the gates do not cover.** `AccountReadingTickTests`, and the two `AccountRotationTickTests` rows updated for the
log's new fields, are the `Process` half, written by this branch and not run by it. No frame was recorded on Daoris's own
door, and no `allowed_warning`, `rejected` or overage frame anywhere (evidence §7): those rows rest on the declaration.
`account.near` (§13) is not written: near by number is a scope's own threshold, so *once per account and window* needs a
scope the reading does not carry; each start's line and `account.rotated`'s `why` log every pass. Not measured: whether
near comes early enough (§11 item 3), or whether pace leaves less at a weekly reset (§16.7). The screen says none of it
yet (TOOL4g). The modules build against the change; their suites, the web's and the rehearsals were not run.

## D133 — The person's words are the ask's record: kept verbatim, handed whole to every session on the ask, quoted by a quest's requirements, and answered at done (2026-10-02)

**Decision (DRIFT1, the owner's, of an ask whose build went another way: *"what I asked is to use v3 bridge +
common-report but it's not doing that at all … we need to investigate why the decision drifted"*).** The evidence is
`docs/2026-10-02-ask-drift-evidence.md`: one ask, its intake, two quests, ten sessions and two branches, read from the
install's records.

1. **The person's words are the ask's record.** Every sentence the person gives on an ask is kept on the ask verbatim,
   with when it was said, to which session and on which quest: the ask, each answer to a parked session, and each
   message added to a running one. A record of the agent's words (a park, last words, a plan, a closing note) never
   stands in for it.
2. **Every session on the ask is handed all of them**, newest last, beneath its quest: a first start, a resume, a
   carry-on after a cut-off or a stop on any account, and a follow-up step's sessions. This replaces the previous
   session's answer (STANDDOWN2) as the source of what the person said. ANSWER1's resume (D131) keeps the agent's own
   conversation where it can; these words are what survives where it cannot.
3. **A quest's requirements quote the person.** The intake names each requirement in the person's own words, with
   the check that proves it, and the service refuses words found in neither the ask nor a recorded answer, naming
   them. The intake's paraphrase stays in the body, where it helps a receiver, and is never the requirement. A
   follow-up step inherits its parent's requirements. A quote is a fact a gate checks with no model (D24, D54).
4. **A done answers each requirement**: met, or departed, with the reason and the person's words it relied on,
   quoted. A departure is shown on the quest and holds the follow-up for the person's yes. A reading attributed to the
   person without their words ("per your answer") is what this ends.
5. **A follow-up measures against the ask, and a correction goes back to the work.** A verifying session is told that
   its parent's closing note is the build's account and the requirements are the measure. A correction to what was
   built becomes a requirement of the quest that built it, which reopens; it is not carried out under the follow-up's
   title. How the parent reopens, and what the person sees, is the design's.

**Why.** The ask named the v3 bridge; in the report repository the bridge carries common-report reports and has
carried nothing else. The intake kept the word and lost what it meant. The person then said "common-report" twice, and
each time it reached one session. Said to the first session mid-turn, it drew a push-back and a two-way question; the
record kept only the message's closing sentence (PARK1 has since fixed that reading), so the next session was handed
the answer without the question, read it as a formula, built a dedicated report type and closed the quest "per your
answer". Said again as an answer on the verification quest, it reached a session the account's spend limit cut off
before it kept a plan; the four carry-ons after it, the last on another account, were each handed the previous
session's record, which was now the limit. Two later sessions quoted the build's reading back to the person as their
decision. Nothing in Daoris held what the person said after the ask, so no repair to one hop would have kept it across
three.

**Rejected.**
- **ANSWER1's resume alone.** The conversation lives in the harness's account home: after a limit the carry-on runs on
  another account and reads none of it (D125 §3.5), and a follow-up quest, or a start after the strikes, has no
  conversation to resume.
- **Handing the previous sessions' transcripts.** They are the agents' words, they are long, and the reading that
  drifted is in them.
- **A model comparing the intake's paraphrase with the ask.** A quote checks as a fact with none; a comparison by
  meaning may later report, never gate.
- **The parent's closing note as the follow-up's measure**, which is what happened by default.
- **An instruction to re-read the ask, alone.** The words the later sessions needed were nowhere they could re-read.
- **Asking the intake to paraphrase more carefully.** It read the right document and wrote a true sentence that lost
  the implication. Only the person's words carry their meaning to a reader who has not read what they read.

**What it amends, when built.** D65: the intake writes requirements in the person's words, and a `then` step carries
the ask's words and its parent's requirements. D79 and D80: a resume and a carry-on are handed the ask's words, not one
answer. D125 §3.5: after a limit, the ask's words carry where the conversation cannot. D131 is complementary and
unchanged. The clean-up of this ask is D132's; the evidence's §7 lists what it must find, four records written in a
development environment outside the repository among them. The rows are the evidence's §6 (DRIFT1a–DRIFT1e).

**What the checks do not cover.** Documents only, and nothing is built. The account is read from the install's
records, transcripts, event logs and trees; no session was re-run with the words this would hand it, so whether they
would have changed the second session's reading is not measured. What the person meant by the first answer is read
from the question and the answer together, and the owner's later sentence agrees. `verify` checks this entry's place
and the evidence's links, and none of their words.

**DRIFT1a, built 2026-10-02: the person's words are kept on the ask.** The ask record gains `Words`: its own sentence
first, derived from the record and given to no session, then each answer to a parked session and each message added to
a running one, verbatim, with when, the session and the quest it works. They are appended in one statement
(`AskStore.RecordWordAsync`, for REV3's reason). `SessionLedger.KeepOnAskAsync` derives the ask from the session: an
intake's own, or its quest's sender `ask #id`, a chain step's included. The answer door
(`POST /api/sessions/{id}/answer`) keeps the answer beside `AnswerAsync`, not inside it. A message added to a running
session never reached the service: it goes from the page to the modules, the driver's inbox and the protocol door. So
it has a door of its own, `POST /api/sessions/{id}/added` (local mode), which nothing calls yet. Both ask routes answer
`words`. Choices §1 left open:
- **Not back-filled, and said so.** An ask from before this build keeps its row and reads its sentence as its first
  word. Its `wordsKeptFrom` is the moment its store first opened on this build, so it never reads as though the person
  said nothing more. An ask made since carries none: its words are whole from the first.
- **A blank answer keeps nothing**, since it is no sentence; the session is still handed STANDDOWN2's *carry on.*
- **Trimmed at the ends only**, as the ask's sentence is.
- **A session on no ask** (a quest one repository asked of another, a conversation on no quest) is answered
  `kept: false` with the reason, a 200: its own record holds what it was told, and no ask gets words not given on it.
- **A word of a kind this build does not know is passed over** on read, never a failed read of the ask.
- **Served as the sentence is**: the ask routes are a local host's alone.

Proof: `AskWordsTests` (an answer, added messages newest last, a chain step, an intake, no ask, the refusals, a later
publish and close keeping the words, a store from before, an unknown kind), seen failing on a stub that kept nothing;
`LocalHostTests` (both read back from both ask routes; the added door's `kept: false`, 404 and 400), the answer door's
keep seen failing with its line removed. Left to their rows: calling `/added` when a driven session's inbox holds the
person's message or an intake is told one (the modules' `SESSION_INPUT`), and handing the words to sessions (DRIFT1b).
Not covered: until `/added` has a caller, a message typed into a running session is still only in that session's
record; a message's files are not kept, only its words; nothing is drawn, so there is no look.

**DRIFT1b, built 2026-10-02: every session on an ask is handed the person's words.** At each start of a quest an ask
asked (its sender `ask #id`, a chain step's included), the driver reads the ask's `words` from `GET /api/asks/{id}`
(`AskWords.ReadAsync`, through `Driver.WithAskWordsAsync`), and the instruction carries them beneath the quest, oldest
first and newest last (`AskWordsText`): a first start, a D79 resume and a carry-on on any account alike. An intake is
handed what was said on its ask after the ask itself, which its instruction already quotes. Choices §2 left open:
- **Quoted verbatim, each saying how it was given** (asked; answered a session; added while one ran; a kind this build
  does not know is said, never dropped), **when**, to the minute in UTC, **and on which quest**, this one or another by
  its id. The heading says they are the person's own words and that a body, a plan or a note is someone else's reading
  of them: a fact, not the measure DRIFT1e designs.
- **Bounded, and said so.** The ask's sentence is kept whole, since the body already carries it. Each later word is cut
  at 2,000 characters with a line saying how many more there were; of the rest, the newest that fit in 8,000 are kept,
  since a correction is newer than what it corrects, and the older are one line between the sentence and the newest,
  saying how many, between when, and that the ask's record keeps every one. The size: the native door hands the
  instruction as one argument, and Windows caps a command line at 32,767 characters, shared with a carry-on's plan, its
  last words and the body. The words the drift lost were a few hundred characters each.
- **A carry-on's answer is quoted once.** Where the answer its last record holds is among the words shown, the
  instruction points to it; otherwise (a quest no ask asked, an answer from before the words were kept, words unread) it
  is quoted as STANDDOWN2 quoted it.
- **Unread is said, never a hold.** A service that does not answer the words (unreachable, refusing, unparsable, an ask
  it does not hold, or a host from before DRIFT1a, which answers no `words`) leaves today's instruction and one line saying
  they could not be read; the driver closing is a cancellation, not an unread. An ask from before the words were kept
  says from when they are (`wordsKeptFrom`).
- **A quest no ask asked reads exactly as it did**, and nothing is read for it.
- **ANSWER1's resume is not composed around** (D131 §1): its conversation was handed the words at its own start, and the
  answer is its next prompt, verbatim. A word given since to another session on the ask reaches it at its next start.

Proof: `AskWordsHandedTests`, seen failing on stubs before it was built: the drift's own chain (an answer, two cut-offs,
then a carry-on on another account, the last run read through `ReadLastRun` and the words through `WithAskWordsAsync`
from a stand-in ask) quotes the answer that one hop lost; every kind of start carries the words beneath its quest; the
kinds, the bound, the unread line and today's instruction beside it; the reads' refusals; the sender's table, a twin of
the service's `AskDesk.AskOf`; the intake. In the `Process` half, `AccountRotationTickTests`' first row now has its
rotated carry-on handed an answer the stand-in's ask holds (written, not run). Not covered: no real agent was handed
the words, so whether they keep a session's reading on the ask is not measured (the evidence's §8); the family
rehearsal's sessions read them from the real service, and it checks none of them.

**DRIFT1a2, built 2026-10-02: a typed message reaches its ask.** The modules' `SESSION_INPUT` posts the person's words
to `POST /api/sessions/{id}/added`, by the session's own id, once the session has taken them: a driven session's inbox
holding them (SESS3), or a conversation told them (`Chat.Say` true). The service judges which ask, if any (DRIFT1a),
through `ServiceClient.AddedToSessionAsync`. Choices:
- **Never awaited by the page's answer**, which stays whether the session took the message. `kept: false` (a session on
  no ask), a refusal, a host without the door and one that does not answer change nothing the person is told, and the
  words are still in the session's own record.
- **Only once taken.** A message the session no longer takes (its inbox closed as it ends) reached nobody, so nothing is
  kept for it.
- **The words alone**: not its files, and not where the person is (HELP1b's preface), which is Daoris's framing.
- **An intake still takes no messages** (INT4h): its line is refused before either door. So the `Chat.Say` path posts
  for a conversation, which the service answers `kept: false` unless its record is on an ask.

Proof: `DriverModuleAddedTests` (a held message posts once, with its session's id and its words; one its inbox no longer
takes posts nothing; `kept: false`, a refusal, a failure and an unreachable service leave it sent), seen failing on a
client that posted nothing; `SessionAddedTests` (the body, `kept` and its sentence, a refusal, a host without the door).
Not covered: no test drives the `Chat.Say` path, since a conversation that takes a message needs a real process; nothing
is drawn, so there is no look.
