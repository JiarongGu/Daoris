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
than an honest gap, because an honest gap gets fixed the day it is hit.

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
wrong.** `daoris quest post <path>` wrote the quest straight into the receiving repository's `TASKS.md`.
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
that was read and ignored.

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
zero-dependency guarantee is untouched.

**Boundaries that do not move:** server key minting stays the deployment console's operator act, never
a client verb; quests stay out of the CLI (D31 as amended) — parity there is satisfied by the platform
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
`docs/2026-09-21-desktop-design-brief.md`; the contract is
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
profile — not a config, not a pointer.

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
   (INT3); until then the exchange keeps refusing an unadopted target.

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
