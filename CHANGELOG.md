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
  canon (`--all` for every edit at once); `index` says where the roster went — the `AGENTS.md`
  region, which `sync` regenerates (D59);
  `status` summarizes — the remote disclosure declaration included, and this machine's wiring with
  `--machine` — and reports when a newer canon is available; `doctor` reports local documents that
  restate a canonical one under a different name; and the **management** commands (D35, D50) are
  opt-in and never run by a gate — `connect` registers the repository with a knowledge service,
  carrying its declaration and, to a local service only, its root; `retire` takes it off the machine's
  registry without touching a file; `import` registers a folder's subdirectories at once; `remote`
  edits the machine's map of one deployment per workspace, talking to nothing and never printing a key
  back; `agent` manages the agent tools sessions run on and the named accounts they run as, spawning
  each tool's own installer and sign-in — and keeps an API key only for an account that is one
  (D67); `driver` sets what this machine drives; and `plugin` lists, adds, removes and switches the
  machine's plugins, loading no code from any of them. `agent` **spawns**; `connect`, `retire` and
  `import` talk to a service; `remote`, `driver` and `plugin` only edit files under the Daoris home.
- **A pack may switch a core row off, and the repository confirms it** (D71). A pack's `pack.json`
  offers `switchesOff`: a core rule, knowledge document or skill, with the reason its own document
  replaces it. The row goes off only when `daoris.json` names it under `switchedOff`. Until then it
  stays on, and `sync` says so, with the line that would confirm it. `sync`, `check`, `status`, the
  doctrine region's roster, `init` and `analyze` all name what is switched off and by which pack. An
  edited core row being switched off refuses and points at `upstream`, since the canonical file still
  exists. Two selected packs shipping the same document are now refused.
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
- **Three layers.** Core installs everywhere, except a row a pack offers off and the repository
  confirms (D71); packs are named in the manifest; the
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
  self-contained single-file into the home's `bin/` (D63) and prints the ready `.mcp.json` snippet with the
  family root filled in; releases carry the same binaries per platform with sha256s beside the
  devkit's. The hosts are safe to run from anywhere: the HTTP host finds its web bundle beside its own
  executable, and the MCP host says plainly when no family root is named instead of silently indexing
  the wrong tree.
- **The loops create their consumer.** Both the family rehearsal and the platform's Playwright suite
  run over a scratch copy of the example family and include a project **born mid-run**: created from
  nothing, joined through the real CLI — `init`, the domain declared, `sync`, `check` clean on first
  contact, `connect` — a member in the registry and the Projects view at once, quest-addressable on
  day one, and still there after a host restart.
- **`Daoris.Web` — the platform: the person's window over the family.** Its views land on
  management: **Overview** (is anything sitting and for how long, the family's health as stat tiles,
  the repositories by what the index holds), **Quests** (grouped by where each is in its life, sitting
  time made visible, publish behind a deliberate action — with the service's refusals shown verbatim
  and the form unable to offer the mistakes the service refuses), **Projects** (who is in the family,
  declarations as scannable chips, the local/canonical split, who cannot be asked yet with the join
  steps proposed as text), then **Convergence** — the knowledge half's lead view — and **Search**; *Sessions*, *Map* and *Settings* joined them on one
  activity bar (D66).
  Doctrine is never editable from the browser: where a rule should change, the UI proposes the command
  to run in the repository that owns the file.
- **A designed console, not a styled document.** One activity bar and a status bar with the global
  state stated once (D56, D66), a page header per view with its one primary action, a right drawer as the single
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
- **One supported harness, others explicit.** The adapter seam's native adapter is `claude-code`;
  Codex arrives over the protocol door below as `codex-acp`. An unknown adapter is an error naming
  what exists, never a silent fallback. An
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
  Settings page's Plugins card is the other door: a row per plugin with what it declares and speaks
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
- **What the shell adopted stands on the Settings page.** A host already running and serving another
  install's page was said once, as a toast raised before the page existed to hear it; the sentence
  now rides the driver state beside the home's, and stands for as long as it is true.
- **Settings is a page of rows.** Each setting is a label, a one-line hint naming
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
- **Signing in happens on the account's row, and it can finish.** Measured on the real harness with
  no console: its login prints a sign-in link as a terminal hyperlink, then *paste the code* with no
  newline, and waits on stdin — which nothing held, so the flow could never complete and the page
  stayed disabled. A harness action now has its stdin, delivers a prompt that has no end once the
  stream goes quiet, and strips the terminal's own escapes; the page may answer it or stop it
  (`HARNESS_INPUT`, `HARNESS_CANCEL`, each refused naming the action when nothing runs). A process
  action is answered once it has started and **its end is news** (`HARNESS_ENDED`), because a login
  waits on a person longer than any request may wait on the bridge — the request had timed out at
  thirty seconds and closed the panel on a login still running. The row shows three steps — the link
  with a copy button, the code box once asked, the row's own pill as the result — with the tool's
  output one disclosure away, and a sentence naming the account when it ends.
- **A tooltip follows the editor's rules.** Below its control, aligned to its leading edge, beside a
  rail; gone on any scroll, key, click or loss of the window's focus, and the moment the pointer is
  off its trigger — it had stayed up over a scrolled list, a blurred window and a button that disabled
  itself on the click.
- **No spawn opens a console window.** The installed shell flashed a terminal on every tick: git,
  the harness probes and the sessions were started without `CreateNoWindow`, and a windowed process's
  console child is given a console of its own. A source scan holds it for every spawn site.
- **A quest carries links and files** (D65 §2). The composer takes links one per line, and files by
  drop, paste or *choose files…*; `quest_publish` takes `links` and `attachments` (paths on this
  machine); the drawer shows links as links and files by name and size, a picture as a picture. A
  link must be an http or https address. A quest carries at most 10 files and 20 MB in total, and
  the composer says why a file was left off while you are still choosing. **The bytes stay on the
  machine that published them**, under the home at `quests/<id>/attachments/`, kept only once the
  record exists. A remote learns names and hashes, and a shared deployment refuses content outright.
  A session is handed the files as `DAORIS_QUEST_ATTACHMENTS`, and its target names each link and
  file. A file kept on another machine is said to be elsewhere, never offered as a path. The host
  serves a kept file to this machine only, sandboxed, and anything but an image, a PDF or text as a
  download, so an attached page cannot run as the platform.
- **A quest can name what comes next** (D65 §4). `then` is a list of steps (develop, then verify,
  then report), and closing the quest `done` publishes the first in the same transaction, asked on
  behalf of the same asker. The step carries the rest of the chain and says which quest it follows;
  `{parent}` in its words becomes that id. The driver takes each step at its next look, so the
  chain *is* the loop, with no engine. A decline stops it. The chain is judged when it is composed:
  every step must be addressable, and every step must live in the same home. The composer offers
  a next step; the drawer shows what is coming and what a quest follows; `quest_publish` takes the
  list; and a driven session is told both what its quest follows and what closing it will publish.
- **One navigation** (D66). The activity bar lists Overview, Sessions, Quests, Projects,
  Convergence and Search, with Settings at its foot. The palette offers the same list, minus the
  view you are on. A remembered Sessions reopens it, and a browser has no Sessions to reopen.
- **A settings page, and the theme is yours to choose** (D66). Settings holds *Appearance* (theme:
  system, light or dark; language) and, on the desktop, everything *This machine* held. A chosen
  theme applies before the first paint and repaints the window's own caption buttons. In a browser,
  Settings is appearance alone.
- **The activity bar's counts are circles.** Measured on the window: 14 × 17.2px before, 16 × 16
  after. Waiting sessions wear the status hue, and outstanding quests the accent.
- **A new installation says what to do first.** With nothing registered, Projects says so and offers
  *add repository*. The ask and quest composers say nobody can be asked yet, instead of a form that
  could never send. A command in a sentence reads as code rather than between backticks, in both
  languages and in the service's own sentences. A failed session no longer wears a success-green
  dot. Chinese is never slanted by a made-up italic. And the rules card explains its defaults in
  中文, with one word for "agent" and one for "the driver" throughout.
- **Convergence answers about three times faster**, with the same answers: about seven seconds a
  call on a 1,052-entry index before, under two and a half after. A drawer no longer dims half the
  status bar, and two accounts signed in as one person say which is the tool's own.
- **Asks say what they wait for, and old claims are gone.** An ask's card says when an intake is
  reading it or has asked you, and names its place as a workspace. Its record no longer repeats a
  one-line ask or reads "answered by by". The map says *waiting on you* for a parked session and
  names the number in a repository; a search snippet shows the entry's prose, not its frontmatter;
  Convergence's empty answer offers a lower similarity. Sentences that stopped being true (the
  index scanning a folder, only adopters being askable) are rewritten, the default rules explain
  themselves without decision numbers, and 中文 calls a quest 委托 throughout.
- **A session is a conversation** (D76). An attended session shows what was asked and what the agent
  said and did: its words as Markdown, code highlighted with a copy button, thinking folded, each tool
  call as a row with its file, its status and an edit's line diff, the plan, and a finished turn's
  work folded under its answer. It is read back after a restart, a page at a time, and follows the
  newest words until you scroll up. The console stays, as the raw view. Sessions on the protocol door
  have it, and so do Claude Code's driven sessions, intakes and chats: the adapter reads the tool's
  own structured output, and a chat records each message you send.
- **A chat on the protocol door works.** A conversation with `claude-code-acp`, `codex-acp` or a
  plugin's harness used to open and never answer: the person's words went into the protocol's
  stream as raw text. Now it is one session, each message a turn on it, in the order sent. It has
  the same servers and rules a driven session gets, and *finish* closes the session before the
  harness exits. A fresh chat says nothing has been said yet, instead of claiming its door carries
  only text. And a conversation no longer loses a word when the page reads it while the agent is
  talking. A tool card names a file inside the session's tree relative to it. The context a session
  used is recorded for the native door too.
- **A turn can be stopped without ending the conversation.** Ctrl+C during a turn in
  `daoris-driver chat` stops it and keeps the session, on the protocol door and on Claude Code's own.
  A script sends a line holding only ETX to do the same. The turn ends *cancelled* in the record,
  never as a failure. A message you send while a turn runs waits for it on both doors: Claude Code
  used to be handed it mid-turn, and the record put it inside the turn before. A stop hands back what
  was still waiting, unsent, so nothing you queued fires after you said stop.
- **The composer stops a turn too.** While a turn runs, *send* becomes *queue*, what you sent
  behind it waits above the box, and *stop turn* stands beside *finish* and *stop*. A stop puts
  what was waiting back in the box. A stopped turn reads *the turn was stopped here*, and the tool
  call it cut reads *stopped* rather than failed. What you were typing to each session is kept per
  session, across a switch and a reload.
- **A message can carry files.** Drop them on the composer, paste a screenshot, or use the
  paperclip, and they go with your next message, alone if you type nothing. They are kept for that
  conversation beside its transcript, never in the repository, and the agent is allowed to read
  exactly that folder. On Claude Code's own door the message names each file's path, and on the
  protocol door it links each one. The conversation shows what you attached under what you asked.
  From a terminal, a line `:attach <path>` puts a file with the next message.
- **A chat you start stays open in front of you**, where it used to fall back to *Nothing attended*
  until you picked it from the list. A long code line scrolls inside its block, not widening the
  conversation.
- **A session never outlives its process on paper.** Closing the application while a chat ran left
  its record *working* forever, holding its repository, and *stop* could not end it. Now closing ends
  each chat and records it first. *Stop* ends a session nothing on this machine is running any more.
  And the driver's first look after a crash ends what was left behind and says so. A session a
  terminal's driver holds on the same machine is never taken for one of these, and neither is a
  teammate's.
- **A message is sent once.** Pressing *send* in a conversation sent every message twice. And
  cancelling an API key you had typed saved it anyway. Both came from a button that submitted its
  form as well as doing its own job, and a button now submits only when it is meant to.
- **Retiring the last repository empties the index.** A machine whose every repository was retired
  went on charting, searching and comparing their knowledge; a refresh now leaves it empty, while a
  registered repository whose checkout cannot be read still keeps what it had.
- **The menus are the setup domains** (D75). The title bar carries *Daoris · Workspace · Agents ·
  View*, and each setup item opens its own domain. Settings is one page with its domains in a
  list, one shown at a time, as an IDE's settings are. The Workspace menu lists every workspace with
  what it holds and scopes the window, and can import a folder of repositories as `daoris import`
  does. The workspace is named in the title bar and the status bar in every state, *no workspace
  yet* included. And the scope has one name everywhere, *workspace* / 工作区, in the interface and in
  what the CLI, the driver and the service print.
- **An account is made by signing in, and Remove removes it** (D66 §3). *Sign in to another
  account* runs the tool's own sign-in into a fresh account and keeps it only if the sign-in
  finished; the name box that came first is gone. Accounts are listed by who is signed in (the email
  `claude auth status` reports), the tool's own included. **Remove deletes the account, sign-in
  included**, after asking once — it used to keep a signed-in account on disk and on the list.
  From a terminal: `daoris agent login <agent> --new`, and `profile remove` deletes;
  `agent list` shows who is signed in to each account.
- **An account can be an API key** (AGT3, D67 §1). *Add an API key* on an agent that takes one
  (Claude Code today), or `daoris agent key <agent>` with the key on stdin. Daoris keeps it in the
  home's `keys.json`, beside the account and never inside the tool's own directory, and hands it to
  the agent at spawn through `ANTHROPIC_API_KEY`. It is shown back only as its last four
  characters, and reads *unchecked*, because the tool says signed in for any key, a wrong one
  included. Removing the account removes its key. A sign-in stays the tool's, as before.
- **A remote wired while the driver runs syncs on its next pass** (SYNC0d). It used to need a
  restart, although the remotes editor said the loop re-reads the map. A changed key is used, and a
  removed remote stops, the same way.
- **A repository's own code map** (MAP3a). A repository that keeps a `docs/code-map.json`, listing
  its modules and what each depends on, can be opened from its node on the Map. The modules are
  drawn in layers, with what uses a module above it; choose one to see its path, its summary, what
  it depends on and what uses it. Daoris reads the file and never writes it. A file that breaks a
  rule is refused whole, with the sentence naming the break, rather than half drawn.
- **A shared deployment answers with a fed code map** (MAP3b). The desktop's sync feeds a sharing
  repository's map beside its knowledge, at the same commit and by the same ordering rules. The
  deployment judges it whole again before it keeps it. A commit with no map removes the one held, and
  an older checkout cannot bring it back.
- **The devkit writes a repository's code map** (MAP3c). `daoris-devkit map` writes
  `docs/code-map.json` from the project files. C# projects and their references come from each
  `.csproj`, and packages and their dependencies on one another from each `package.json`, with no
  compiler. A summary or a dependency the files cannot say (a service called over HTTP) is written
  once by a person and kept. `daoris-devkit map --check` is the gate a repository declares, and Daoris
  keeps its own map with it.
- **A teammate's code map, on this machine** (MAP3e). A repository whose checkout is on another
  machine used to answer "no map" here even when it keeps one. Each sync now brings its map down as
  the circle's deployment holds it, only when it moved, and the Map view says where it came from:
  the commit, its line and whose key fed it. A commit that keeps no map says so by that commit.
- **What a start runs on** (MAP1b). Settings shows, for each workspace, the agent, account and
  version a driven session would start with, and which setting chose each: this workspace's
  default, this machine's, or the agent's own sign-in; a pin, or PATH. A start the driver would
  hold says why in the driver's own words. It is the driver's own answer, so it cannot disagree
  with what the loop does. Desktop only.
- **How this work ran** (MAP1a). A quest in a chain shows the whole chain in its drawer: the ask it
  came from, the quests before and after it, the steps still to come, and every session that ran
  each one, with its agent, version and account. Press a quest to open it, or a session to attend
  it. The same strip sits under a session in Sessions.
- **A map of the workspace** (MAP2, D67 §3). *Map* on the activity bar draws the circle's
  repositories, the quests that went between them (one arrow per direction, counted, solid while
  any is open), and where two repositories learned the same thing. Choose a repository or a line to
  see what it holds. It reads only what the service already serves, so it works in a browser too.
- **An account its provider refused is not spent again** (AGT3b). A session that fails with the
  tool's own "API Error: 401" ends saying which account was refused and what fixes it. Further
  starts on that account are held, instead of each sitting through the tool's minutes of silent
  retries, until you change the account or look again.
- **The protocol door runs as the agent's accounts** (AGT7). Claude Code over the protocol door
  (`claude-code-acp`) used to look for accounts under its own name, so an account made for
  Claude Code, a key account included, never reached it. Codex's never reached `codex-acp`. A door's
  accounts, defaults and keys are now the agent's; only its pinned version stays its own.
- **`daoris harness` is `daoris agent` now** (AGT1). The tools a session runs are agents to a
  person: the verb, its help, the CLI's output and the driver's refusals say so. `daoris harness`
  answers with where it went. Each tool names what it is and whose: *Claude Code* (Anthropic),
  *Codex* (OpenAI), *dsh* (DeepSeek), on its Settings card and in `agent list`.
- **A pinned Claude Code stays the version you pinned.** Claude Code updates itself by default, and
  a pinned copy reported exactly that. Every spawn of a pinned binary now runs with
  `DISABLE_UPDATES=1`: sessions and chats over either door, and the version and sign-in questions.
  A `claude` from `PATH` is left as the machine has it.
- **A repository that keeps a code map is asked to keep it current** (MAP3d). A driven session in a
  repository whose tree holds `docs/code-map.json` (or `code-map.json`) is asked, in its prompt, to
  bring the map up to date in the same change when its work adds, removes, moves or rewires a module:
  with the repository's own tool where it has one, else by hand in the map's format. A repository
  that keeps no map is not asked to start one, and the prompt is otherwise unchanged.
- **`daoris agent pin` installs Claude Code and Codex from their makers' own channels, verified**
  (AGT2b). Claude Code's release manifest must carry Anthropic's signature before its SHA-256 is
  trusted for the binary; a version before 2.1.89, which has none, is refused. Codex's package must
  match both of its published hashes. Nothing is pinned unless everything verified. The Machine
  view's pin does the same for Claude Code. npm still installs the ACP adapters and dsh.
- **Ask at a workspace, not a repository** (D65 §1a). `daoris-driver ask [--workspace <name>]
  [--to <repo>] [--file <path>]… [--url <address>]… "…"` records an ask: the sentence, its links
  and files, who asked, and what became of it, naming the tier that answered on every record. With
  no intake harness, the **declarations tier** ranks the workspace's repositories by the words their
  summary, `owns` and `accepts` share with the sentence, proposes with that evidence, and publishes
  nothing. `--to` publishes at once, asked by `ask #<id>` in the ask's circle, carrying its links
  and files. A refused `--to` keeps the ask with its proposal. `ask --publish <id> --to <repo>` and
  `ask --close <id> --reason "…"` finish it. The same words in the same circle are the same ask.
- **Ask from the screen, too** (INT4c, D50). Quests leads with *Ask*, and the palette offers *Ask the
  circle…*. The ask is made in the scoped circle, or the only one; with several and none chosen, the
  composer asks which. It carries links and files exactly as the quest composer does, because both
  now share one set of fields. Each ask's record says which tier answered and what it proposed. It
  publishes to a proposal or to any other adopter in the circle, opens the quests the ask became,
  names its files without saying where they are kept, and closes with a reason. A browser on this
  machine has the same door.
- **An ask can be answered by an intake session** (INT4b, D65 §1b). Name a harness with `daoris
  driver intake <adapter>`, and an ask the declarations did not settle opens a session in the
  circle's room under the home. The room is seeded with who owns and accepts what. The session
  publishes the quests itself, chains included, asked by `ask #<id>` in its own words with the
  asker's beneath. The ask names its intake, and its tier reads `intake`. Where the declarations do
  not settle it, the intake publishes nothing and the session waits for you; publishing or closing
  the ask ends it. The intake is off until a harness is named, because each intake spends a login,
  and `daoris driver intake off` goes back to declarations only.
- **An ask that waits on you is in *What needs you*** (INT4d). A proposal, or an ask whose intake
  parked asking, is a row of Overview's band that opens the ask's record. A parked intake is counted
  once, as its ask. The band stays live on the desktop: an ask made by the other door arrives with
  the next tick, without a reload. The record names who answered: the `intake` tier in words, and
  the intake session, which is a door into Sessions on the desktop. A band row with nowhere to go (a
  parked session, in a browser) is no longer a button.
- **A repository registered without adopting can be asked, and driven over the protocol door**
  (INT3, D70). Registered is addressable; adopted is disciplined. A repository registered on this
  machine with a root takes quests, and a session the driver starts in it on a protocol agent is
  handed its connector with nothing written into the tree. A direct agent holds the quest and says
  which door could carry it, and the publish tells the asker so. Every place that offers a receiver
  offers exactly who can be asked. Adding a folder with no manifest from Projects now registers it as
  not adopted.
- **What an agent Daoris starts may do is yours to set** (PERM1, D72). It uses Claude Code's own
  permission rules, in three scopes Daoris keeps (this machine, a circle, a repository), in
  `permissions.json` under the home. Daoris's defaults allow its connector's quest tools and deny a
  `git push`. Every session Daoris starts on Claude Code is handed them at spawn, on either door, as
  one more settings scope, so the agent's own order decides and a repository's deny still wins. They
  reach a session even in a folder the agent has never trusted, where a repository's own allow-list
  does not. Set them with `daoris agent rules`, or on Settings → *What agents may do*.
- **A session is told it may propose the rule it was refused** (PERM2b, D74). The quest target now
  names `permission_propose`. A session refused a command its work needs proposes the narrowest rule
  with its reason, then finishes or declines, since a widening waits for you. Proven on a real
  session, which proposed a rule for its one repository only.
- **An agent can propose a change to what agents may do** (PERM2, D74), through the connector's new
  `permission_propose` tool. A narrowing (a deny or ask added, an allow removed) applies at the
  driver's next tick. A widening waits for your yes, from `daoris agent rules proposals | accept <id>
  | decline <id>` or Settings → *What agents may do* → *Proposed by agents*, and it waits in *What
  needs you* meanwhile. Every proposal records the session that made it, its reason, and who settled
  it.
- **A session can read the files its quest or ask carries** (INT4j, D72). The files are kept under
  the Daoris home, outside the session's folder, and reading them was refused. Every session Daoris
  starts on Claude Code is now handed a read of exactly its own quest's or ask's kept folder, and
  nothing else under the home.
- **A real intake is proven end to end** (INT4f). A real Claude Code intake read a circle's
  declarations, chose the repository, and published the quest as the ask, carrying its link and file,
  in a room nobody had trusted.
- **A driven session may commit, by default** (PERM4, D72). Daoris's rules now allow `cd`, `git add`
  and `git commit`, so a session in a folder the agent never trusted can land its work instead of
  declining. A push is still refused. Switch it off with `daoris agent rules default commit off` or
  on Settings → *What agents may do*.
- **A session writes files only inside its own tree** (PERM3, D72). Every Claude Code session Daoris
  starts is handed a hook that refuses an edit or a write whose path, followed through links, is
  outside the session's tree, and says why: a change needed elsewhere is a quest. It is a default,
  `tree-guard`, switched like the others, and proven on a real session on both doors. A shell
  command's writes are not its call; the agent's own working-directory boundary stands there.
- **Trusting a folder for the agent is asked, then written** (DEPLOY1, D73). `daoris agent trust
  <agent> <folder> --yes` grants what Claude Code asks the first time it runs in a folder, in the
  agent's own file and nothing else; without `--yes` it only asks. On the desktop, a start held for
  trust shows in *What needs you* and in its quest's drawer, and *trust this folder…* grants exactly
  that hold. Since Daoris's own rules reach an untrusted session, the driver holds for trust only when
  those rules would not let the session reach its connector.
- **Claude Code over the protocol door is proven on a real login** (ACP2). A real quest reached a
  real commit, and the session closed its own quest through its connector, in a folder nobody had
  trusted. Getting there fixed a defect no gate could see: a real tool call carries its content as a
  list, and reading it as an object ended the driver's reader at the first tool call, reported as
  "the stream ended".
- **An unadopted repository can be opted into driving from Projects** (INT3c). One registered here
  with a root carries the same driving row as an adopter: drive, hold, own tree per session. On a
  machine that drives on a direct agent, the row says a quest there will sit until it drives on a
  protocol one. The group's explanation now says only what is proven about a real agent there.
- **A driven session takes no messages either** (INT4i). A line sent to a session the driver started
  on a quest landed, on the protocol door, in the driver's own JSON-RPC stream, and on the pipe door
  came back as "it ended". The driver now refuses it in its own words, naming the quest and saying
  that stopping is the one move that reaches it. Conversations are unchanged.
- **A running intake takes no messages** (INT4h). Sessions gave it a message box, and a typed line
  either went nowhere or, on the protocol door, landed in the middle of the driver's own JSON-RPC
  stream. It has no box now: its head says why, opens the ask it serves, and carries its stop. The
  driver refuses a line or a finish for an intake in its own words rather than answering "it ended".
- **A parked intake in Sessions leads to its ask** (INT4g). It offers *answer ask #id*, which opens
  the ask's record where it is published or closed, and a stop that says the ask then stays a
  proposal, instead of a parked session's finish, decline and stop, none of which answered the ask.
  It has no message box, and Sessions names it as an intake for its ask rather than a conversation.
- **Settings says what Daoris's own AI is** (AGT6): search and convergence with the service's tier,
  verbatim, and the variables that choose its model; on the desktop, the intake's agent — off, or a
  way in this machine has — and the account an intake in each circle runs as. What a start runs on
  lists the intake as a second job, and the status bar's tier leads there.
- **Two conversations in one repository no longer break the driver.** A conversation in a checkout
  beside another in its own tree (D51) made every tick fail on a duplicate key, starting nothing
  until one ended.
- **Scrollbars follow the theme.** A scrollbar is painted by the browser, and with no
  `color-scheme` it stayed light down a dark drawer. The page now declares both schemes, and the
  bar wears the line token.
- **"Send it back as a quest" opens the composer again.** The review's door switched the window to
  Quests and opened nothing, because the composer consumed the draft during render and lost it to
  the parent's clear. The palette's *start a session* and *review* reached Sessions the same
  way. Both are now consumed once, by identity.

### The remote

- **Team mode: the same host, fed by the desktop.** The remote is a deployment of the existing HTTP
  host in **shared mode** (`DAORIS_MODE=shared`), not a second implementation — every route gated by
  **per-person per-machine minted keys** (`keys mint|list|revoke` on the binary; stored as a hash with
  a short non-secret audit prefix, shown once, expiring by default). It serves no page and answers no
  machine path, and a host asked to bind beyond loopback without shared mode refuses to start.
- **The quest lock is code.** A quest's transition table is enforced in the store itself — `Taken` only
  from `Open`, closed quests immovable — and the same table is replayed wherever a quest's history is,
  on every machine and at the remote. The same hardening runs in local mode.
- **A quest is its history, and the remote is where histories meet** (D68). Every verb — publish,
  take, done, decline — commits on the machine where it was made and always succeeds there, whether
  or not a remote is wired, reachable or down. Quest ids are twelve hex characters. The desktop's
  sync rides the driver tick. It feeds registrations and opted-in knowledge content **up**, and
  mirrors teammates' registrations **down**. **Session records travel both ways by cursor**: each is
  sent when it changes, and every machine sees the team's records, read-only and keyed by origin. A
  teammate's session is never this machine's lock. For quests it runs
  **fetch, rebase, push**: the remote orders what it accepts, the first push wins, and a move that
  lost is kept on the quest as a **conflict** rather than dropped. **A take claims by push** (D69).
  A take on a shared quest waits for the remote's answer, so a take that lost stands down before any
  work. Offline, the take stands unconfirmed, and a session whose take later loses is stopped by its
  own machine's driver. A quest leaves a machine only when its receiver is joined. What may leave a machine at all is two manifest declarations — **join**
  and **share knowledge** — and silence means local. Roots, transcripts and a file's bytes have no
  field in anything fed.
- **Proven by a two-machine rehearsal.** The family rehearsal grows a remote phase with a shared host
  and two simulated machines. A quest published on one is driven to done on the other and the closure
  crosses back. A machine that sees a quest already taken leaves it alone. When both machines take one
  quest online, the second stands down before any work. When both take it offline, the second's take
  is kept as a conflict everywhere. A session that took offline and lost is stopped before it lands
  anything. Verbs made while the remote is down are pushed when it returns. Knowledge crosses only where declared, keys are refused without
  being echoed, and the remote store is scanned to hold no machine path — no model in the gate.
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
  rather than ignoring it. The machine's remotes became a **map** — `remotes.json` under the home, one
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
- **Ancestry decides where git can say.** Before it feeds, the driver asks the deployment which commit
  it holds and asks git how its own commit stands to it. A descendant feeds with that commit as its
  base, and the deployment takes it as a fast-forward whatever its clock says. If another machine
  fed in between, the feed is refused as *moved*. A checkout that is behind, or has not fetched the
  held commit, feeds nothing and says whether a pull or a fetch would fix it. Commit time decides
  only for diverged histories.
- **The same commit is one reading.** A deployment hashes what it would store. The same commit with
  the same content is already held; read differently, the first reading stands and the second
  machine hears why, instead of the two replacing each other on every tick. And only a clean
  checkout feeds, because a feed speaks for a commit and work in flight is not yet that commit's.
- **A declaration is ordered the same way** (SYNC5b). A registration names the commit its manifest
  stands on, and a shared deployment orders it like knowledge. Before, the last machine to register
  won, so a checkout that was behind put its older declaration back on every tick. The first
  registration is taken from any line. After that, only the line the declaration calls canonical is
  taken, and a manifest with uncommitted changes names no commit and does not replace one that did.
- **Teammates' repositories stay current, and a retire travels.** The desktop used to copy a
  teammate's registration once and never again, and a retire reached no other machine. Now each pass
  rewrites a copy whose declaration changed, and removes a copy the circle no longer lists. A joined
  checkout that leaves a circle owes that circle a retire: removed, moved to another circle, or no
  longer joined. The store records the retire when the row changes, and the next pass carries it.
- **Sync when asked, and see where a circle stands** (SYNC6a). `daoris-driver sync` runs the tick's
  own pass now, for every circle with a remote or the one `--workspace` names. `daoris-driver sync
  status` prints where each circle stands on this machine: operations not yet pushed, quests left
  behind, quests in conflict, and when it last reached its remote. A pass that could not reach the
  remote keeps that time and names the wall as the last try. The status reads the machine's own
  host and never contacts the remote.
- **The status bar shows where a circle stands** (SYNC6b). A wired circle's remote item becomes a
  control: ↑ for work waiting to go up, ↓ for quests left behind, ⚠ for quests in conflict, `synced`
  when level, and `unreachable` when the last try hit a wall. Pressing it shows when the circle last
  synced, the wall in the host's own words, and the quests in conflict, each of which opens in its
  drawer. The desktop adds *Sync now*, the same pass `daoris-driver sync` runs. A browser on this
  machine reads the same standing and has no *Sync now*. A quest's drawer now shows each move that
  lost the race to the remote, with that session's note.
- **Every wired circle syncs, and a wall no longer hides itself.** A circle where nothing on this
  machine is joined still gets its pass, so the team's repositories and quests come down to a
  machine that has only asks to make. When the feed hits a wall, the quest pass still runs, and that
  is where the try is recorded, so the status bar can no longer say `synced` while the sync is
  failing.
- **A conflict can be dismissed, everywhere** (SYNC6c). The quest's drawer has *Dismiss* beside each
  move that lost the race, and `daoris-driver sync dismiss <quest>` dismisses every one a quest
  carries. A dismissal is an operation like a take, so the next pass carries it and every machine
  stops showing the conflict. It moves no status, and two people dismissing one conflict make one
  dismissal.
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
- **Two more commands, and both surfaces do the same thing.** `daoris agent list|install|update|
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
