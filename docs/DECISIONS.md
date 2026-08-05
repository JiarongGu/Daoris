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
