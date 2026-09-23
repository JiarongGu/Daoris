# Changelog

All notable changes to Daoris are recorded here. Entries are written under `## Unreleased` and stamped
with the version and date at release.

## Unreleased

The first version: doctrine that installs, is checked, and flows back.

### The tool

- **Fifteen commands.** `analyze` reports what adopting would do before it does it; `init` writes a
  manifest and reports what is available without guessing;
  `sync` materializes the selected packs and writes the lock; `check` gates on drift, staleness and
  index freshness, and **reports** the always-loaded budget rather than failing on it (a fact gates, a
  judgement reports — D54); `upstream` promotes a locally-improved file back into the
  canon (`--all` for every edit at once); `index` regenerates `RULES_INDEX.md` from what is on disk;
  `status` summarizes — the remote disclosure declaration included, and this machine's wiring with
  `--machine` — and reports when a newer canon is available; `doctor` reports local documents that
  restate a canonical one under a different name; and the **management** commands (D35, D50) are
  opt-in and never run by a gate — `connect` registers the repository with a knowledge service,
  carrying its declaration and, to a local service only, its root; `retire` takes it off the machine's
  registry without touching a file; `import` registers a folder's subdirectories at once; `remote`
  edits the machine's map of one deployment per workspace, talking to nothing and never printing a key
  back; `harness` manages the agent tools sessions run on and the named credential profiles they run
  as, spawning each harness's own installer and never handling a credential itself; `driver` sets
  what this machine drives; and `plugin` lists, adds, removes and switches the machine's plugins,
  loading no code from any of them. `harness` **spawns**; the rest of the class only edits files
  under the Daoris home.
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

- **The always-loaded tier is a region of `AGENTS.md`, not a directory beside it.** Measured across the
  three harnesses this family drives: `.claude/rules/` is read by exactly one of them, so Daoris had
  been shipping an always-loaded tier whose always-loaded-ness belonged to one tool rather than to the
  doctrine. `sync` now writes the core rules into a marked region of `AGENTS.md` — the only file all
  three read — with `CLAUDE.md` carrying a one-line `@AGENTS.md` import region for the one that reads
  the other name and follows imports. Knowledge and skills do not move. The adopter's own text in
  either file is never touched, and a damaged marker, a reordered pair or a second region is **refused
  with its line number** rather than guessed at, because what is on the other side of that guess is
  their doctrine. `daoris.lock` gains one field: an entry carrying `in` is a span inside a file. See
  `docs/DECISIONS.md` D7 (amended) and D59.
- **Eight core rules**, each confirmed by appearing independently in multiple repositories in the family:
  `sensitive-info`, `task-lifecycle`, `no-tmp-for-repo-files`, `file-tool-discipline`,
  `persist-working-state`, `no-global-memory`, `skills-workflow` — and `repository-owns-its-work`:
  never write into another repository; publish the request and let whoever works there take it.
- **The doctrine does not require the tool that ships it.** An adopted repository stays fully workable
  for contributors who do not run Daoris, their agents included — they load the same vendored markdown.
  That survives almost everything the canon names, because what `sync` writes is committed: a generated
  index, a lock file and every rule are still there for someone who never installed anything. A
  *service* is the exception, and publishing a quest is the only one the canon instructs — so it names
  the alternative in the same breath ("a message to that repository's owner where none does"), and a
  gate refuses any canon file that instructs a quest without one.
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
- **Seven packs.** `windows-machine` (traps that succeed wrongly rather than failing),
  `dotnet-library` (package boundaries, naming, DI variation points, shipping registries, and API design),
  `storage-sql` (type affinity on read, migration numbering, full-text search for scripts without word
  boundaries), `desktop-app` (verifying a real desktop application — driving the running app, what
  synthetic input does not prove), `web-webview` (a web UI inside a native shell — resource serving,
  thread affinity, the silent failures), `durable-jobs` (long-running work that survives a restart
  — lanes, checkpoints, resume), and `localized-ui` (an interface shipped in more than one language —
  what belongs in a string catalogue and what must never, and the parity that stops a missing
  translation from silently working) — each with its reasoning in `canon/CHANGELOG.md`.
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
- **A second door, and four harnesses through it.** Beside the pipe the driver has always had, a
  session may be held over the **Agent Client Protocol** — JSON-RPC over the spawned process's stdio,
  with tool boundaries, turn boundaries, thoughts and context usage arriving *by contract* rather than
  parsed out of another program's stdout. `claude-code-acp`, `codex-acp` and `dsh` are **configurations
  of that one door**, not three more seams, and an `acp-stub` gates the whole of it with no model.
  A permission request over the wire is **refused by the driver**, always. What a session did is still
  concluded from its exit code and its quest's state, never from what it said about itself — the
  protocol flattens an aborted or errored turn into an ordinary ending, so it enriches and never decides.
- **The permission posture is stated per harness, in that harness's own words.** The same boundary —
  edits inside the tree proceed, nothing outward is ever auto-approved — is `acceptEdits` to Claude
  Code, `agent` to Codex, and to dsh is not a wire concept at all but an environment setting. Each is
  named explicitly even where it matches a default, a mode the agent did not offer is never
  substituted for one it did, and a harness whose wire carries no posture is left at its own.
- **What Daoris writes into a harness home it created, and what it will not.** A dsh credential
  profile gets a patch layer turning off the two rows that ship enabled and send session material off
  the machine, plus a root that makes the repository's own skills reachable. Only in a directory
  Daoris made: run without one and dsh uses *your* configuration home, which Daoris does not write to
  — so it runs, and says what that means, and names the one command that fixes it. A home already
  holding a hand-written patch layer is reported, never overwritten.
- **The desktop shell.** `daoris-desktop` brings up the local host (adopting one already running rather
  than double-starting), carries the platform in its window — the same bytes a browser gets — and runs
  the driver loop in-process, re-reading the person's standing choices every tick: drivable and hold
  per repository, stop a running session, all through the platform's own session-control surface.
- **A plugin is a folder that declares, and may speak** (D64). Under the home's `plugins/<id>/`, a
  `plugin.json` names what a plugin declares — configurations of the ACP door, so a fifth harness
  arrives as a file — and what it speaks. The catalogue reads the API version before anything else,
  refuses a newer one naming both numbers, lists a broken manifest with its problem rather than
  crashing, and refuses a harness name this build carries or an earlier plugin declared, naming both
  sides. `daoris plugin list|add|remove|enable|disable`: an add replaces the install wholesale and
  leaves `.data/<id>` alone; a remove names the data folder rather than deleting it; disabled is a
  row, never a rename. No code from a plugin loads into any host.
- **A plugin may speak.** A manifest's `hooks` row names a process and the points it listens on; the
  driver starts it with the loop — in the plugin's own folder, its id, folder and data folder in the
  environment — completes a versioned handshake over JSON-RPC on its stdio, asks it at the points,
  and stops it with the loop. `quest/consider` is a fail-closed waterfall: the first hold in
  catalogue order becomes the quest's own reason for sitting, and a plugin that answers late, wrongly
  or not at all holds too, naming itself and the way out. `session/ended` is contained observation.
  Every line a plugin writes lands on the console under `plugin:<id>`. Reconciled every tick, so a
  plugin disabled between ticks is stopped at the next one. A plugin that only declares is never
  started. The family rehearsal drives one: a declared harness a session runs on, a quest held with
  the plugin's sentence, an ending kept in its data folder, and the switch from a terminal. The
  Machine view's Plugins card is the other door: a row per plugin with what it declares and speaks
  on, running or off, the driver's sentence under a refused one, the switch, and Remove naming what
  the plugin kept; a declared harness on the roster wears the plugin it came from.
  `examples/plugins/hold-by-title` is the tracked example of a plugin that speaks — the one the
  family rehearsal installs with the real `daoris plugin add` and drives.
- **A plugin hands every session its servers** (D65 §1f). A manifest's `servers` row declares MCP
  servers by name, command and environment, `${plugin}` expanded; the driver offers them beside the
  knowledge host over the protocol door, and over the pipe door hands a harness that takes a file at
  spawn one written under the home for that session — never the repository's own. The knowledge
  host's name is refused, and a name two plugins claim keeps the first by id; `plugin list` says what
  each hands. `examples/plugins/browser` declares the Playwright MCP, which is how *test it in a
  browser* becomes something a session can do; the family rehearsal proves it reaches a session from
  the agent's own account of what it was offered.
- **A conversation can run on a declared harness.** Both chat doors resolve the harness through the
  live adapter set — the desktop's through the roster the driver's tick updates, the terminal's by
  reading the home's plugins — so a plugin added after the shell started is a harness a chat can
  use now, without a restart.
- **What the shell adopted stands on the Machine view.** A host already running and serving another
  install's page was said once, as a toast raised before the page existed to hear it; the sentence
  now rides the driver state beside the home's, and stands for as long as it is true.
- **The Machine view is a settings page of rows.** Each setting is a label, a one-line hint naming
  its terminal twin, and the control at the right; the paragraph that motivated it is on an info
  glyph. The home's path sits under the header, the driver's two dials share a card, and wiring a
  deployment is behind a press — five cards of prose became a page a person scans.
- **The application's own folder is the Daoris home, and nothing lives under the user profile** (D63).
  Every machine-local file Daoris owns — the registry, `driver.json`, `harnesses.json` and the
  credential profiles beside it, the remotes map, the index, session records, the installed service
  binaries — lives under `DAORIS_HOME`, and every default in the CLI, the hosts and the driver derives
  from that one variable. The installed desktop sets it to its own `data/` for itself and every
  session it spawns, and once for the account when it has none, so a terminal's `daoris` meets the
  same machine; a `~/.daoris` from before moves in on the first start and the shell says so once.
  With no home set the management commands and the hosts refuse in a sentence naming it, rather than
  writing somewhere nobody pointed them. `publish:service --install` lands the hosts under the home's
  `bin/` and prints the `.mcp.json` snippet with the home filled in.

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
  A two-workspace phase joins it: two circles on one machine, a search answering from one while the
  other holds the same lesson word for word, and a quest across the boundary refused with both sides
  named.

### Workspaces

- **The workspace is the unit of sharing.** Knowledge search, convergence, the registry, quests and
  session records all answer within one workspace and never across one — so a machine can hold a game
  family and a work family without either seeing the other. A machine that never names one runs exactly
  as before: everything lands in `default`, silently.
- **Membership is wiring, like a git remote — never a tracked declaration.** `daoris connect
  --workspace <name>` records it in this machine's registry and **writes nothing into the repository**,
  so a fork, a mirror and a second machine may each wire the same repository differently. Omitting the
  flag preserves whatever wiring already existed, because an ordinary `connect` runs on every sync tick
  and must not re-point anything; the command reports back the workspace that actually took.
- **A quest does not cross workspaces**, and the refusal names both sides, both circles, and the two
  things a person can do about it — re-wire one of them, or carry the request across by hand.
  Addressability offers only the asker's own circle.
- **A session's answers default to its own circle.** The MCP tools take an optional `workspace`; with
  none, the scope is the workspace of the repository the session is running in, resolved from the
  registry by path — an agent never has to know wiring that is not in its tree. Every answer says which
  scope ran, including when it spanned every workspace, and `all` is the spelled way to ask for that.
- **Nothing a feed claims decides where it lands.** Fed entries and fed session records take the
  *receiving* deployment's wiring, because a feed that could name its own workspace could write itself
  into someone else's.
- **One shared deployment serves one workspace**, and says which (`DAORIS_WORKSPACE`): every row it
  takes lands in that circle, and a registration declaring another is refused in a sentence naming
  both sides. A local host, which holds every circle the machine wired, refuses the variable outright
  rather than ignoring it. The machine's remotes became a **map** — `~/.daoris/remotes.json`, one
  deployment per workspace — with the sync running once per circle and the quest relay resolving by
  the quest's own workspace. A circle with no entry syncs nowhere, silently, which is what every
  machine does until someone says otherwise.
- **`daoris remote list|add|remove` and `status --machine`,** plus the desktop's new **Machine** view
  over the same file. Both are editors; the file is the truth, so hand-editing keeps working. A key is
  never printed back — only the audit prefix the deployment's own `keys list` shows — and the
  `remote` verbs are management commands that speak to nothing at all.
- **A feed carries the commit it speaks for.** Two checkouts of one repository are two points in its
  history, and wholesale replacement between them was a flapping generator: each tick, whichever fed
  last overwrote the other. Now the driver stamps `{ commit, committedAt, branch }` from git, a
  deployment takes knowledge **only from the repository's declared canonical line**, and a feed older
  than what it holds is refused. Deletion stays correct for free — an entry absent from the newest
  canonical view is one the repository deleted — and `/api/repositories` answers the commit each copy
  stands on, shown on Projects, because an index is a claim about a commit and staleness someone can
  see beats freshness they must assume.
- **A refusal can be information.** A stale or branch feed is the rules working, not a fault, so it is
  flagged as such on the wire and the sync reports it as news rather than as a wall. Records and
  quests still travel from any checkout; only knowledge waits for the canonical line. A checkout git
  cannot answer for feeds no knowledge, and the machine that holds it says so itself.

### The working surface

- **A session's console, live.** The driver already captured every session's output to a transcript;
  that capture now tees into a bounded window the desktop streams to the session drawer as it
  happens — verbatim, never translated, and desktop-only: output is transcript-class material and has
  no HTTP route at all, so a browser over a keyed remote sees the record and never the stream. The
  window states what it dropped rather than showing two halves of a log as though they joined, and
  the transcript on the driving machine still holds everything.
- **A conversation is a session.** A person can open a chat with an agent in any repository: the same
  record the driver uses, the same observed lifecycle, and the same one-session-per-repository lock —
  because two agents in one working tree corrupt it regardless of who is typing, and the refusal names
  what holds it. A chat serves no quest by default; it may take one mid-conversation through its own
  connector, or end by publishing the work that came up, which is how work that was not yet an ask
  becomes one without anybody editing across. Ending it is two different verbs: finishing lets the
  harness wind up, stopping cuts it off, and the record says which happened.
- **The harness is the chat.** Daoris pipes the person's lines in and streams the session's out, and
  makes no model calls at all: which model answers is that repository's own harness configuration.
  The adapter seam grew one honest capability — an adapter that has not been wired for turn-taking
  says so rather than spawning something that will never answer.
- **Two doors, one conversation.** The desktop has a chat drawer over the live console; a machine with
  no screen has `daoris-driver chat --repository <name>`, where stdin is the person and stdout is the
  session. Same runner, same lock, same record — what stays desktop-only is the *stream*, not the
  capability.
- **Daoris manages the harnesses, and never a credential.** It finds the agent tools on the machine,
  reports their versions, and installs or updates them **through their own official mechanisms, only
  when asked** — never automatically and never mid-session, because a tool that changed between two
  runs nobody diffed is a gate that stopped meaning anything.
- **One harness, many accounts — as named profiles.** A harness holds one login per configuration
  home, so switching accounts used to mean logging in again. Now an account is a named, isolated
  configuration directory whose *location* Daoris owns, selected at spawn through the environment
  variable each harness already has for exactly this. Choose one per machine, one per workspace — a
  work account for the work circle, a personal one at home — or one for a single conversation.
  **Daoris never sees, stores, or copies a credential**: logging in runs the harness's own flow inside
  that directory, and what it obtains stays in the harness's own store under your OS account. What
  Daoris keeps is a directory and a name.
- **A record says which tool and which account did the work.** Every session now carries the harness
  version observed at spawn and the profile it ran as — the same authorship instinct as stamping a
  release, applied to the tool that did the work. The version travels with the record; the account
  name stays on the machine that ran it, guarded like the transcript beside it.
- **A spawn that cannot work refuses before anything is recorded, naming the fix.** A harness that is
  not installed, or a profile nobody has signed into, holds the start with the sentence that says what
  to run — not a bare not-found. The quest stays open and nobody's, and doing what the sentence says
  releases it on the very next tick, with nothing restarted.
- **Two more commands, and both surfaces do the same thing.** `daoris harness list|install|update|
  login|profile …` and `daoris driver list|drive|hold|resume|cap|adapter` set all of it from a
  terminal, because a server with no screen is still a machine; the desktop's roster edits the same
  files. Neither reaches a network.
- **Fixed: every refusal the desktop made reached you as a blank failure.** When the app could not do
  something — a remote missing half its pair, a declaration form pointed at a repository that has not
  adopted, a conversation asked for before the driver is up, an adapter it does not have — it had a
  sentence written for you, and you never saw one of them. Now you do, in your own language, and the
  driver's own wording travels word for word.
- **Fixed: `refresh` re-read the repositories but never the folder.** The root's subdirectories were
  listed once at startup, so a repository created afterwards was invisible to the index while being
  fully registered and quest-addressable — and the refresh reported success either way.

### The managed registry

- **The registry is the authority; being in a folder is not being a member.** The family used to be
  whatever a root folder happened to hold, and that failed the way scans fail: what a scan does not say
  governs as much as what it says, and nobody reviews a silence. Now it is an explicit list — name,
  workspace, declaration, and the checkout path, machine-locally. The index reads the registered paths,
  so a folder nobody added contributes nothing.
- **Two new commands, `retire` and `import`.** `daoris retire [name]` takes a repository off this
  machine's registry and **nothing else** — no file, no history, no doctrine — and says so; retiring
  something already retired is an answer, not a failure. `daoris import [folder]` is the old folder
  scan, demoted to a verb a person runs: safe to re-run, because it states no workspace and unstated
  wiring is preserved.
- **A store that has never been managed imports its root once, and says so.** Without it, a machine
  that had been running on `DAORIS_KNOWLEDGE_ROOT` would come up to an empty family after the upgrade —
  and an empty family is indistinguishable from a broken one. Once, because a second run would
  resurrect everything the person deliberately retired. A deployment that is fed rather than scanned
  imports nothing, by the same rule that keeps it off its own disk.
- **A registered checkout that vanished is named.** `refresh` reports it — moved, deleted, or
  registered from another machine — instead of quietly indexing nothing while the count still looks
  healthy. A registration with no path is not an absence: a teammate's mirrored row has no checkout
  here by construction.
- **The desktop manages the machine's repositories.** Projects gains add, re-wire, declaration and
  retire where a shell is attached — the shell supplies the folder (a browser may never learn a machine
  path) and the page registers through the ordinary loopback door. The two kinds of update are kept
  visibly apart: re-wiring edits one row here and touches no file; editing the declaration writes
  `daoris.json` in that repository and leaves the diff uncommitted for its own review. Adoption stays
  that repository's own act, shown as commands to run rather than a button. Doctrine stays unwritable.
- **The CLI's offline guarantee is now stated over a class, not a command.** `check`, `sync`,
  `upstream` and the rest are offline by construction; `connect`, `retire` and `import` are the opt-in
  management commands, and all three speak through one module — so the test still reads "exactly one
  file may touch the network, and nothing a doctrine command reaches may import it". `remote` joined
  the class and touches nothing at all: it edits a file under the profile, and a test walks its
  imports to keep it that way.

### Proven

- Daoris carries its own manifest and syncs core into its own `AGENTS.md` region and `.claude/`; a test
  asserts it stays clean.
- Adopted into **Lyntai** (a released .NET library): 4 collisions surfaced and resolved deliberately, a
  renamed twin found, 3 packs installed, its own 1337 tests still green — and the budget gate immediately
  caught a real 45% overage on first contact. Lyntai has since stepped back off the tool at its owner's
  request, keeping the synced files as local forks — the adoption remains the proof of the collision,
  twin and budget paths, and re-adoption is a decision that stays with that repository.
- The deployable service was driven live, not only tested: an unauthorized write answered 401,
  `daoris connect` registered through the real endpoint, the host was killed and restarted with the
  pushed registration and a taken quest both still served, and publishing to a non-adopter was refused
  naming who is addressable.
- **A deployment rehearsal** (`npm run rehearse:deploy`) that publishes the desktop shell to a folder
  and drives *that* — the artefact, not the checkout. The deployed window comes up with no help
  finding its service host, and a quest its own driver loop spawns is carried to done with the session
  transcript compared as bytes. Every other loop runs inside the workspace, where the workspace build
  and the console's own encoding hide exactly the defects the first real deployment found.
