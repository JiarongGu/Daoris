# Changelog

All notable changes to Daoris are recorded here. Entries are written under `## Unreleased` and stamped
with the version and date at release.

## Unreleased

The first version: doctrine that installs, is checked, and flows back.

### The tool

- **Eight commands.** `analyze` reports what adopting would do before it does it; `init` writes a
  manifest and reports what is available without guessing;
  `sync` materializes the selected packs and writes the lock; `check` gates on drift, staleness, index
  freshness and the always-loaded budget; `upstream` promotes a locally-improved file back into the
  canon (`--all` for every edit at once); `index` regenerates `RULES_INDEX.md` from what is on disk;
  `status` summarizes and reports when a newer canon is available; `doctor` reports local documents that
  restate a canonical one under a different name.
- **`doctor` covers the one gap the lock cannot.** A repository's own rule duplicating a canonical one is
  local, and local is invisible by design — it surfaced on the first adoption only because someone read
  the generated index end to end. Advisory by construction: word overlap is crude, and a false positive
  that failed a build would be worse than the duplication. Validated against the real case, where it
  independently finds a 58% overlap that previously took a manual read to notice.
- **Zero runtime dependencies.** Node ≥ 22, ESM, `node:test`. Nothing to install — every command runs
  through `npx` against a pinned reference.
- **The canon ships inside the package**, so the pinned reference *is* the version pin. No command
  fetches anything, which is what makes `check` offline by construction rather than by discipline —
  asserted by a test that deletes the canon and requires a clean exit.
- **Three layers.** Core installs everywhere with no opt-out; packs are named in the manifest; the
  repository's own documents are never synced and never touched. Anything absent from the lock is
  invisible to the tool.
- **Two refusals, distinguished by provenance.** A file in the lock that changed on disk is *drift* — the
  repository edited something Daoris owns. A file *not* in the lock at a canonical path is a *collision* —
  the repository wrote it before adopting Daoris. Both stop the sync; they carry different advice,
  because they are different mistakes.
- **Drift is measured against the lock, not against the current canon.** Comparing on-disk content to
  freshly-rendered canonical content made "the repository edited this" and "the canon improved" the same
  observation, so an improved rule could not propagate: every consumer's `sync` exited 1 over an edit
  nobody made. Now the lock's recorded hash answers "did this repository change it" and the canon answers
  "is there something new to install." See `docs/DECISIONS.md` D13.
- **Retirement.** A file removed from the canon is removed from every repository on the next sync — the
  one thing copy-paste can never do.
- **A renamed canonical file is reported as a rename**, not as a retirement plus an unrelated addition.
  Detected by pairing content rather than by a declared field, so it cannot claim a move that did not
  happen, and it covers core as well as packs. Conservative by design: an uncertain pair stays described
  as two separate changes.
- **A one-line provenance header** on every materialized file, because an agent that opens a rule needing
  a tweak will otherwise simply edit it. The lock's hash catches the edit either way.
- **`--force` names what it destroys.** It is the only way to lose work with this tool, and it was
  silent about doing so. A refusal names the file it is protecting; the override that overrules that
  refusal now names it too, or nothing anywhere records what went.
- **Retiring a file the repository has edited refuses instead of deleting it.** Retirement is the most
  destructive thing `sync` does and had the weakest guard: a retained file that drifted refused, while a
  retired one was deleted silently — at the worst moment, since the canonical file the edit belonged to
  is gone and `upstream` is no longer a route. It now advises keeping the edit as a local document.
  The full lock × disk × canon state space is enumerated in `docs/DECISIONS.md` D19, so the next gap is
  found by reading rather than by losing a file.
- **Every path is confined to the target directory.** `sync` resolves each write and delete against the
  target and refuses anything that escapes it, before touching a file. A lock entry containing `..` could
  otherwise reach arbitrary paths — and the lock is generated, so it is the file nobody reads closely in
  review. See `docs/DECISIONS.md` D18.
- Atomic, BOM-less, LF writes throughout; exit codes are the contract (`0` clean, `1` policy failure,
  `2` tool error).
- **A release rehearsal** (`npm run rehearse`) that packs the tarball, installs it into a clean
  repository, and drives the whole consumer lifecycle through the `bin` entry — adopt, collide, sync,
  drift, promote, upgrade, rename, check. The test suite exercises the source tree; this exercises the
  artefact, which is where install stories actually break.

### Skills

- **A third tier.** `skills/<name>/SKILL.md` installs, retires and drift-checks like everything else, and
  core is now laid out exactly like a pack (`core/rules/`, `core/skills/`) so one code path reads both.
- **Canonical skills are parameter-free** and delegate to the generated index; there is no substitution
  map in the manifest. Decided from a survey of twelve repositories and 134 skills — see
  `docs/DECISIONS.md` D14.
- **The index gained a skills table**, which is what a hand-written "here are our skills" skill always
  was: generated content. It marks the repository's own skills `(local)` like every other row.
- **The provenance header moves under the frontmatter.** Frontmatter is only frontmatter at byte 0 — the
  harness parses a skill's `description` to decide whether to surface it, so a comment above the opening
  fence would have made every canonical skill silently unreachable, with no error anywhere.
- **`skills-workflow` is now a core rule** — it appears in six of eleven surveyed repositories, tying
  `sensitive-info` as the strongest signal in the family, and its copies diverge the most.
- **A skill's supporting files travel with it.** A skill is a directory, and the platform lets it carry a
  reference document, a template, or a script it invokes through its own directory variable. Only the
  `SKILL.md` was being materialized, so such a skill would have installed with its first step pointing at
  a file that never arrived. Markdown is stamped with the provenance header; other files are copied
  verbatim, because an HTML comment in a script is a syntax error.
- **The return path closes without `--force`.** After `upstream`, the file on disk already *is* what the
  canon would write, so only the lock hash is stale — but `sync` read that as drift and demanded
  `--force`, whose documented meaning is "discard your local edit". The last step of contributing an
  improvement advised throwing it away. A file matching the current canon is no longer drift whatever the
  lock says.

### The canon

- **Eight core rules**, each confirmed by appearing independently in multiple repositories in the family:
  `sensitive-info`, `task-lifecycle`, `no-tmp-for-repo-files`, `file-tool-discipline`,
  `persist-working-state`, `no-global-memory`, `skills-workflow` — and `repository-owns-its-work`:
  never write into another repository; publish a quest and let its own agent take it.
- **Five core knowledge documents**, each knowledge rather than a rule because it applies to a
  situation, not to every task — a distinction the budget gate enforced more than once.
  `model-decoupling` (the model is a deployment choice: specify the feature without naming one, select
  the provider by deployment, report which tier ran), `claims-need-checks` (behavioural prose is
  verified against the implementation, with the check shipped in the same change), `leak-repair` (a
  committed leak is a history problem — how to actually scrub one), `reaching-in` (what happens after
  writing into another repository, and why the repair is worse), and `autonomous-development`
  (development is automation-first: a person sets the target and verifies the outcome, agents execute
  the steps between under gates, and destructive, irreversible, cross-repository and publishing actions
  stay explicitly human). The canon's own `CHANGELOG.md` carries the full reasoning per document.
- **Five core skills**, each canonized from copies found across the family and reduced to what they share.
  `doc-loader` and `pattern-finder` (six repositories each) start a task; `post-feature` (four) and
  `fix-log` (three) close one; `caveman` (five) governs output. `fix-log`'s copies sat within 100 bytes of
  each other, so the invariant was nearly the whole file. `post-feature`'s looked least alike of any —
  one a stack checklist, another a diff-detection procedure — and the shared shape turned out to be the
  value. `caveman` is canonized for its **carve-outs** rather than its terseness: never compress a
  destructive or irreversible action, a security finding, or an order-sensitive sequence, and never write
  a durable artefact in the mode at all. A compressed warning reads as fluent English right until someone
  approves it without registering the consequence.
- **`status` names what a pending update would change** — `changed` / `new` / `retired` per file, instead
  of only reporting that a newer canon exists. Computed from the lock, so it stays offline; the
  provenance header is excluded, so a pure version bump reports "version only" rather than listing every
  file and training people to skip the list. `--json` renders the same facts machine-readable for the
  agent operator, computed once with the text so the two views cannot disagree.
- **…and why it changed.** The canon carries its own `CHANGELOG.md`, and `status` prints the entries for
  exactly the versions a repository is skipping. Which files moved is computable; whether it *matters* is
  a sentence only the author of the change can write, so the canon ships it alongside the documents.
- **A repository's own skills are reported as local** by `init` and `status`, as its own rules already
  were.
- **`doctor` scans skills, and its threshold is now set by measurement.** Checked against sixteen real
  pairs across the family: near-verbatim copies score 72–74%, twins that were *rewritten* rather than
  copied land at 34–43%, and unrelated documents at 7–16%. The old 0.5 sat above the middle band and
  caught 2 of 11; 0.3 catches 7 with no false positive. The threshold is asymmetric on purpose — the
  command is advisory, so a false positive costs a dismissed line and a miss costs lasting duplication.
  It also now states the duplicate it *cannot* find: word overlap detects restatement, not convergence.
- **Three packs.** `windows-machine` (traps that succeed wrongly rather than failing),
  `dotnet-library` (package boundaries, naming, DI variation points, shipping registries, and API design),
  `storage-sql` (type affinity on read, migration numbering, full-text search for scripts without word
  boundaries).
- Every canon file carries frontmatter that generates its index row; tests assert that, plus that no canon
  file contains a machine path.

### The service

- **`Daoris.Service`** — the knowledge layer beside the CLI (a separate deployable; the CLI keeps its
  zero dependencies and never learns about it). It indexes every repository's doctrine, decisions,
  fixes and task outcomes into SQLite with FTS5, answers ranked queries, and finds **convergence** —
  where two repositories reached the same conclusion in different words, which no text comparison can
  see. Semantic recall is opt-in by naming an embedding model; without one the service is lexical-only
  and says so on every answer.
- **The registry and quests.** Each repository declares in its manifest what it owns and what it
  accepts; the service serves that as the registry — search answers "has anyone solved this", the
  registry answers "whose problem is this". Cross-repository work moves as a **quest**: published to
  the service, pulled by the repository it addresses, taken / done / declined — declining needs a
  reason. Nothing is ever written into anyone's tree.
- **Deployable, two modes, one binary each.** Local needs no daemon: the MCP host (`daoris-knowledge`)
  is spawned per agent session and the persistent per-user store is what survives, shared by every
  repository's sessions on the machine. Remote is the HTTP host: registrations pushed by
  `daoris connect` persist across restarts, and quests publish and answer over the same shared judgement
  (`QuestExchange`) as the MCP host. There are two trust shapes and no third: **local** trusts the
  loopback — the OS account is the boundary, and the host refuses to bind anywhere else — and **shared**
  (the team deployment) gates every route with minted keys (below). It runs with **no model at all** and
  still carries the whole transfer of request and task.
- **The server ships as executables.** `npm run publish:service -- --install` publishes both hosts
  self-contained single-file into `~/.daoris/bin` and prints the ready `.mcp.json` snippet with the
  family root filled in; releases carry the same binaries per platform with sha256s beside the
  devkit's. The hosts are safe to run from anywhere: the HTTP host finds its web bundle beside its own
  executable, and the MCP host says plainly when no family root is named instead of silently indexing
  the wrong tree.
- **The loops create their consumer.** Both the family rehearsal and the platform's Playwright suite
  run over a scratch copy of the example family and include a project **born mid-run**: created from
  nothing, joined through the real CLI — `init`, the domain declared, `sync`, `check` clean on first
  contact, `connect` — a member in the registry and the Projects view at once, quest-addressable on
  day one, and still there after a host restart.
- **`Daoris.Web` — the platform: the person's window over the family.** Five views, landing on
  management: **Overview** (is anything sitting and for how long, the family's health as stat tiles,
  the repositories by what the index holds), **Quests** (grouped by where each is in its life, sitting
  time made visible, publish behind a deliberate action — with the service's refusals shown verbatim
  and the form unable to offer the mistakes the service refuses), **Projects** (who is in the family,
  declarations as scannable chips, the local/canonical split, who cannot be asked yet with the join
  steps proposed as text), then **Convergence** — the knowledge half's lead view — and **Search**.
  Doctrine is never editable from the browser: where a rule should change, the UI proposes the command
  to run in the repository that owns the file.
- **A designed console, not a styled document.** A sidebar shell with the global state stated once at
  its foot, a page header per view with its one primary action, a right drawer as the single
  detail-and-form surface (a knowledge entry, a quest's detail and its actions, the compose form),
  toasts carrying every outcome verbatim, designed empty states, and static skeletons. The quest-state
  palette is computed rather than tasted: both themes pass all six checks of a color-vision validator,
  and a status never appears without its text label.
- **Properly tooled, and bilingual.** The platform is built on headless libraries under the same
  design language — Tailwind v4 with the validated tokens as its theme, Radix primitives, lucide
  icons, TanStack Query for server state — and speaks **English and 简体中文** with an en/zh parity
  gate in the build. UI chrome translates; data and the service's own sentences render verbatim.
  Storybook serves as the living design tool over the shipped components, and the test pyramid runs
  as one gate: a Vitest inner loop over the view logic, the primitives and both catalogs, then a
  Playwright suite that boots the real host over the example family and drives the shipped bundle —
  members visible, a quest through its whole life in the drawers, the verbatim refusal, the language
  switch — so a release whose UI cannot do its job over the example family does not ship.
- **A refresh retires what the disk no longer has.** A repository renamed or removed used to stay in
  the index forever, served as though it were alive; a refresh now retires any repository the scan did
  not see — guarded on the scan having seen anything at all, so a mis-set root cannot wipe a good
  index. Found on the platform's own Overview, serving a repository renamed weeks earlier.
- **An example family under `examples/`**, tracked in full: two miniature adopters and
  `npm run rehearse:family`, which proves the router through the real artefacts — adoption,
  registration through `daoris connect`, a quest's whole life including its refusals, knowledge
  crossing projects, and a restart losing nothing.

### The driver

- **Daoris drives.** The service turns a quest queue into an execution queue: the local driver
  (`Daoris.Desktop`) watches the service, and where the person has opted a repository in, **spawns a
  fresh non-interactive agent session per open quest** — one active session per repository, onto a clean
  working tree only, oldest first. The spawned session **claims its own quest** over its own connector
  and closes it `done` or `declined`, exactly as an interactive session would; the quest state machine
  is the only lock, so a driven session and a hand-run one are indistinguishable. Driving is additive,
  never exclusive — a repository developed by hand loses nothing.
- **Observed, never self-reported.** A session's life is concluded from the two signals outside work
  also produces — the process's exit and the quest's own transitions — not an in-band protocol.
  Session **records** live in the service beside the quests (so the platform renders them and they
  survive a restart); the **process** never leaves the machine that spawned it. The record carries the
  reviewable evidence — the commits that landed — and a machine-local transcript path that is answered
  only to a caller on that machine.
- **One supported harness, others explicit.** The adapter seam is `claude-code` (supported) and `codex`
  (explicit second); an unknown adapter is an error naming what exists, never a silent fallback. An
  adapter names a harness, never a model. Gate-proven with a **stub adapter** — real spawn, real claim,
  real commit, no model — and then by a **real `claude-code` run**: a quest became a session became a
  commit became `done` in 71 seconds.
- **The desktop shell.** `daoris-desktop` brings up the local host (adopting one already running rather
  than double-starting), carries the platform in its window — the same bytes a browser gets — and runs
  the driver loop in-process, re-reading the person's standing choices every tick: drivable and hold
  per repository, stop a running session, all through the platform's own session-control surface.

### The remote

- **Team mode: the same host, fed by the desktop.** The remote is a deployment of the existing HTTP
  host in **shared mode** (`DAORIS_MODE=shared`), not a second implementation — every route gated by
  **per-person per-machine minted keys** (`keys mint|list|revoke` on the binary; stored as a hash with
  a short non-secret audit prefix, shown once, expiring by default). It serves no page and answers no
  machine path, and a host asked to bind beyond loopback without shared mode refuses to start.
- **The quest lock is code.** A quest's transition table is enforced in the store itself — `Taken` only
  from `Open` as one atomic guarded write, closed quests immovable — so two machines' drivers racing one
  quest resolve to a single taker, and the loser stands down. The same hardening runs in local mode.
- **One home per quest, decided at publish.** A quest to a joined repository lives at the remote; verbs
  on it write through synchronously or fail plainly — a lock that queued would not be a lock. The
  desktop's sync loop rides the driver tick: it feeds registrations, session records (keyed by origin),
  and opted-in knowledge content **up**, and mirrors the family's quests and teammates' registrations
  **down**. What may leave a machine is two manifest declarations — **join** and **share knowledge** —
  and silence means local; roots and transcripts have no field in anything fed.
- **Proven by a two-machine rehearsal.** The family rehearsal grows a remote phase with a shared host
  and two simulated machines: a quest published on one is driven to done on the other, the closure
  crosses back, a raced take stands down, knowledge crosses only where declared, keys are refused
  without being echoed, and the remote store is scanned to hold no machine path — no model in the gate.

### Proven

- Daoris carries its own manifest and syncs core into its own `.claude/`; a test asserts it stays clean.
- Adopted into **Lyntai** (a released .NET library): 4 collisions surfaced and resolved deliberately, a
  renamed twin found, 3 packs installed, its own 1337 tests still green — and the budget gate immediately
  caught a real 45% overage on first contact. Lyntai has since stepped back off the tool at its owner's
  request, keeping the synced files as local forks — the adoption remains the proof of the collision,
  twin and budget paths, and re-adoption is a decision that stays with that repository.
- The deployable service was driven live, not only tested: an unauthorized write answered 401,
  `daoris connect` registered through the real endpoint, the host was killed and restarted with the
  pushed registration and a taken quest both still served, and publishing to a non-adopter was refused
  naming who is addressable.
