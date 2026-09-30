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
for this press was not built: it would reach into the service's proposal kinds and the page's cards.
