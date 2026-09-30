# The interactive surface — chat sessions, live console, managed harnesses

> Written 2026-09-20, from the owner's direction set in the backlog the same day: chat sessions and a
> console display directly in Daoris (developing via claude code / codex from inside it), and Daoris
> managing the agent CLIs themselves — install and update. This extends the driver design
> (`docs/2026-09-19-driver-design.md`) without weakening its contract: sessions stay observed,
> processes stay on the machine, and Daoris still makes no model calls at all. The direction is D49 in
> `docs/DECISIONS.md`; the companion is `docs/2026-09-20-workspace-design.md`.
>
> **Paths.** `~/.daoris/…` below is the Daoris home as it was when this was written. Since D63
> (2026-09-23) the home is `$DAORIS_HOME`, the install's own `data/`, and nothing lives under the user
> profile. The tree beneath it is unchanged.

## 1. What it is for

D46 made Daoris start work; the person still *watches* it from outside — a session is a record with a
note and evidence, and its living output is a file on disk. The owner's direction closes that gap:
**Daoris becomes the working surface.** A person opens the desktop, sees a session's console as it
runs, and can open a conversation with an agent session in any repository — claude code or codex —
without leaving the platform. Three parts: the **console** (see what a session says, live), **chat
sessions** (talk to one), and the **toolchain** (the harnesses those sessions run on, installed and
updated by Daoris rather than remembered by hand).

## 2. The console: the transcript, streaming

The driver already captures every spawned session's stdout/stderr to a transcript file (driver design
§4). The console surface is that capture, streamed as it happens — not a second channel a session must
speak.

- **The capture pump tees.** `Driver.CaptureAsync` already pumps both streams line-by-line into the
  transcript; it gains an observer seam — each line goes to the file (unchanged, the durable copy) and
  to an in-memory **ring buffer per live session** (bounded; the file is the unbounded record). The
  driver exposes: the last N lines, and a subscription for live lines.
- **Desktop-only, structurally.** Output can carry machine paths — it is the same class as the
  transcript, which never leaves the machine (D47 §4) — so the console rides the shell's IPC bridge
  (`DAORIS.DRIVER`: a `TAIL_SESSION` request for the buffer, a `SESSION_OUTPUT` event for live lines),
  never an HTTP surface a remote could reach. A browser sees the record, as today; a teammate's
  machine sees the synced record, never the console.
- **The page renders it in the session drawer** — the record on top (state, note, evidence), the
  console beneath it, verbatim in a monospace well; the driver's own sentences stay toasts. No
  translation, ever: output is data (the platform's i18n boundary).

**Built 2026-09-20 (SES1).** Five choices the building settled:

- **A sequence number per line, monotonic within a session.** It is what makes the backlog and the
  live stream one stream: the page asks once on open, takes events after that, drops anything it has
  already seen, and — if a batch does not continue where the last one ended — asks the driver for the
  gap instead of rendering two halves as though they joined. Neither side keeps a cursor for the other.
- **The bound is stated, not hidden.** The window holds 500 lines per session and says how many fell
  out of it. A console that silently skipped the middle of a build log would be a worse lie than one
  that showed nothing; the transcript on disk still has all of it.
- **Events are batched on a ~120ms window.** One event per line is the obvious shape and the wrong
  one: a session can emit thousands in a second, and a bridge carrying one message each turns
  watching into a stalled window.
- **Eviction never takes a live session's buffer.** Sixteen sessions are retained; the oldest ENDED
  one goes first, and with all of them live nothing is evicted — dropping the console of a session
  someone is watching would be the wrong answer to a full table. A buffer is kept after its session
  ends, because how it finished is what a person most wants to read.
- **The console degrades to absent.** It is the part of the drawer that may be missing — an older
  shell answers something else entirely — so the page tolerates any shape and the record above it
  stands regardless. Found by a test: a mocked bridge answering the wrong object crashed the drawer,
  which is precisely what a console must never do.

**Amended by CONSOLE2 (2026-09-28): a session's console is several streams.** On the protocol door
a session asks its agent for what it runs beside itself, and the driver keeps each as a stream of its
own under the session: every subagent its harness spawns, and every background task it starts. The
evidence is `docs/2026-09-28-console2-streams-evidence.md`. Five rules from building it:

- **A stream is a buffer under a key**, `<session>/subagent/<id>` or `<session>/task/<id>`, tailed
  exactly as a session is. Streams ride with their session. They are not counted as sessions retained,
  they leave when it is evicted, and its end is theirs.
- **A subagent's updates are routed by `params.sessionId`**: its words and tools go to its stream,
  never to the session's console, record or transcript. The driver reads a transcript's last plain
  lines as what the session said to the person, so a subagent's words there would be quoted as the
  session's.
- **A task's stream is its output file**, read as it grows (`OutputTail`), because none of its
  output is on the wire. The path stays in the driver.
- **The session's console says each one started and how it ended.** Its record holds a subagent as
  one card (`stream` names its console), since asking for subagents takes the `Agent` call off the
  session's wire. A task adds no card: its tool call is its card.
- **What is still open when the session ends is ended with it**, and its stream says so.

**What the family rehearsal can reach, and what it cannot.** The gate drives the headless host, which
has no IPC bridge by design, so it proves the DURABLE half: the transcript still holds what the
session said after the pump grew a second destination. The in-memory half — the buffer's bounds,
ordering and eviction, and the page's merge of backlog with live lines — is held by the driver's own
tests and the platform's.

## 3. Chat sessions: a conversation is a session

A chat is **a person-initiated, interactive session in one repository** — the same entity the driver
already manages, entered through a different door.

- **The adapter seam grows one capability: `interactive`** (driver design §5 held exactly this seam
  open). An adapter that declares it can spawn its harness wired for turn-taking — stdin carries the
  person's messages, stdout streams the harness's — however that harness does it (`claude-code` first
  and supported; `codex` explicit second; unknown errors naming what exists — D23 unchanged). The
  **harness carries the model and the conversation**; Daoris pipes text and observes, which is
  `model-decoupling` holding: no model is named anywhere in Daoris, and the chat feature's definition
  ("open an interactive session in a repository and relay its console") contains none.
- **A chat session is a first-class session record.** `Session` gains `Kind: driven | chat` and its
  `Quest` becomes optional — a chat may serve no quest, may *take* one through its own connector
  mid-conversation (the same three verbs, the same door), or may end by publishing new quests. The
  ledger's judgement is unchanged where it matters: **one active session per repository** — a chat
  holds the working tree exactly as a driven session does, because two agents in one tree is the same
  corruption regardless of who is typing. The clean-tree spawn rule stays for driven sessions;
  a chat may open on a dirty tree — the person is present, and it is their work in flight.
- **Lifecycle is still observed.** Process lifetime and quest transitions move the record; a chat that
  exits with no quest closes `completed` with no evidence claim beyond its commits. `stopped` stays
  the person's verb. The transcript rules of §2 apply whole — the conversation *is* the console.
- **Where it runs**: the desktop shell, and only there — the chat is a process on this machine (D46
  §7: processes never leave the driver). Records sync as records; a remote teammate sees that a chat
  happened, never its stream. Input goes over the IPC bridge (`SESSION_INPUT`), output is §2's event.
- **The D37 boundary is the harness's own permission surface**, exactly as for driven sessions
  (adapter obligation 3): the repository's checked-in permission configuration governs; an interactive
  session is precisely what that configuration was written for.

Rejected: **a Daoris-owned chat loop** (Daoris calling a model API with its own prompt) — it would put
a model call inside Daoris, duplicate what every harness already is, and produce sessions with no
doctrine path in; the harness *is* the chat. Rejected: **quest-required chat** — the whole point is
starting work that is not yet shaped as an ask; the quest system is where the work lands, not the toll
to start talking.

**Built 2026-09-20 (SES2).** Six choices the building settled:

- **A headless door exists too: `daoris-driver chat --repository <name>`.** The design said "the
  desktop shell, and only there", and what that clause protects is the STREAM — output is
  transcript-class and never leaves the machine (D47 §4). A terminal on the same machine breaks
  nothing, and D50 forbids stranding a capability on a machine with no screen. It is the same
  `ChatRunner` with a different reporting half, and it is what lets the family rehearsal gate a whole
  conversation with no model in it: stdin is the person, stdout is the session.
- **End of input ends a conversation; stop cuts it off.** Two verbs, two meanings, two records:
  closing stdin lets the harness say what it was going to say and exit (`completed`), while `stop`
  stays the person's interrupt (`stopped`). A surface that offered only one would make "I am done
  talking" indistinguishable from "stop what you are doing".
- **A chat's process has stdin; a driven one structurally does not.** Sending to a driven session
  answers false because there is no stream to write to — it was given its whole target at once and
  has nobody to take turns with. The UI follows that rather than restating it: a repository held by a
  *driven* session offers to start a chat (and gets the ledger's refusal naming what holds it), never
  to "open" a session nothing is listening to.
- **`quest` became nullable by REBUILDING the table and copying the rows.** SQLite cannot drop a NOT
  NULL constraint. The entry store may rebuild by discarding because its contents are derived; a
  session record is the reviewable trace of work that happened and nothing can re-derive it, so the
  rows move across.
- **The ledger reads the registry.** A chat names its repository directly instead of inheriting it
  from a quest, so it is the one path that can name one that does not exist — and the circle its
  record belongs to comes from the registry row, which is the machine's own wiring (D48 §2).
- **`interactive` is a default-false capability on the seam.** An adapter that has not been wired for
  turn-taking says so and refuses in the harness's own terms, rather than spawning a process that
  will never answer — the same rule as an unknown adapter name (D23). `claude-code` and the stub
  declare it; anything new opts in after the work is actually done.

## 4. The toolchain: harnesses Daoris installs and updates

The driver depends on the harness binaries existing (`claude`, `codex`); today that is the person's
memory. The toolchain manager makes it Daoris's job — **explicitly, never automatically**.

- **Detect freely, act on request.** Each adapter gains a `Probe` — locate the binary, report its
  version (`claude --version` etc.) — run at driver startup and on demand; the platform shows the
  roster: harness, version, present/absent. Detection is free and read-only.
- **Two surfaces, one truth** (management parity — workspace design §2b): everything here is equally
  manageable from a terminal — `daoris agent list|install|update|login|profile ...` — because a
  headless machine running `daoris-driver` has no roster page and still needs its harnesses and
  profiles set up. Both surfaces do the same thing: spawn the harness's own tooling into the right
  profile home; neither holds a secret.
- **Install and update are the person's click**, per harness, using that harness's own official
  mechanism (an adapter obligation, beside spawn: `claude-code` via its documented installer/npm
  package, `codex` via its own). Output streams through §2's console (it is a process like any other).
  **Never mid-session and never unasked**: a harness changing under a running loop is the
  moving-target problem `reaching-in` documents, one layer down. The driver refuses to spawn on a
  missing harness with the sentence naming the install action, instead of a bare not-found.
- **Versions are recorded**: the session record's `adapter` gains the harness version observed at
  spawn, so "which tool produced this" is answerable later — the same authorship instinct as
  version-stamping (the release workflow, `tools/release-prep.mjs`), applied to the tool that did the work.
- **Credential profiles: one harness, many accounts** (set by the owner, 2026-09-20). A harness holds
  one login per configuration home, so switching accounts today means re-logging-in — the toolchain
  manager makes accounts **named profiles** instead: each profile is an isolated harness configuration
  directory Daoris owns the *location* of (`~/.daoris/harnesses/<harness>/<profile>/`), selected at
  spawn by the environment seam every harness already has for exactly this (`claude-code`'s config-dir
  variable; `codex`'s home variable — an adapter obligation, beside spawn and probe). **Login is the
  harness's own flow, run into the profile** — a person-action streamed through §2's console like an
  install — so **Daoris never sees, stores, or copies a credential**: the harness's own store holds
  it, inside the profile, under the user's OS account, which is the same boundary it lives behind
  today. *Amended by D67 §1 (2026-09-23): an account that IS an API key is kept by Daoris, in the
  home's `keys.json` — the one credential it holds; a sign-in stays the tool's own store, never read.*
  Switching accounts is choosing a profile: a machine default per harness, an optional default
  per **workspace** (the natural cut — a work account for the work workspace, a personal one at home;
  the wiring layer of the workspace design §2, not anything tracked), and a per-session picker for a
  chat. The session record carries the **profile name** at spawn beside the version — never anything
  from inside it — so "which account did this run as" is answerable without a secret ever leaving the
  profile. The probe reports each profile's login state the way the harness reports it (logged in /
  not), and a spawn onto a logged-out profile refuses naming the login action, the same shape as a
  missing binary.
- Rejected: **auto-update** (a gate's tool must not change between two runs nobody diffed); **Daoris
  pinning harness versions in the manifest** (the harness is machine tooling, not repository doctrine —
  the repository's own docs may demand a minimum, but the manifest stays inert data about doctrine);
  **Daoris holding tokens itself** (a second credential store is a second thing to leak, and the
  harness already has one — Daoris manages directories and names, never secrets; amended by D67 §1
  for API keys alone).

**Built 2026-09-20 (SES3).** The mechanisms were verified against the real binaries *before* a line was
written, because every one of them is a claim about somebody else's program and a guessed one fails in
a person's terminal saying something untrue: `claude auth status` answers **JSON with a `loggedIn`
boolean**, `codex login status` answers a **sentence**, `CLAUDE_CONFIG_DIR` and `CODEX_HOME` genuinely
isolate an account, and `codex` refuses to start when its home names a path that does not exist. Eight
choices the building settled:

- **Silence means the harness's own configuration home — not an empty profile.** With no profile named
  anywhere, the environment seam is not set at all and the spawn is byte-for-byte what it was before
  this existed. Pointing a person who never asked for profiles at a fresh configuration directory
  would log them out of their own tool, which is the loudest available way to break "Daoris works
  alone" (D48 §2a). The whole feature is therefore additive, and an adapter that declares no toolchain
  is checked for nothing.
- **Login state is asked of the harness, never sniffed off disk — and it has three values.** Both
  supported harnesses **exit 0 whether or not they are logged in**, so the OUTPUT is the answer and the
  exit code is deliberately not consulted; an answer this build cannot read is `unknown`, and unknown
  is **permissive**. Only a definite *logged out* refuses. Same shape as WSP4's undeclared canonical
  line: refusing work because a tool reworded its own status sentence would be worse than letting the
  harness refuse for itself. The first pattern written for `codex` was unanchored, and `"Not logged
  in"` contains `"logged in"` — it reported every logged-out profile as logged in, and a test found it.
- **A cached "no" is re-asked before it is given.** Detection spawns a process, so probes are cached
  across ticks; but a cached *absent* or *logged out* would keep refusing after the person did exactly
  what the refusal told them to. A yes is trusted, a no is checked again — one extra process, on the
  path that was about to fail anyway.
- **The profile name stays on the machine that ran the session; the version travels.** The design said
  the record carries both, and it does — but a *name a person chose*, quite possibly after themselves,
  is machine wiring in the sense D48 §2 draws, so it is guarded exactly as the transcript is (D47 §4):
  stripped for a non-loopback caller, absent from the feed's shape, and written as a literal NULL by
  the store's mirror. Two guards and a missing field for one rule, because this is the kind of thing
  that leaks through whichever half somebody forgot. A harness *version* is a fact about a tool, and
  it crosses.
- **A profile is a directory, and the directory is the contract.** There is no register of profiles to
  disagree with the disk — the same argument that made the registry the authority rather than a view
  over a scan (D48 §3). A name that could escape that directory is refused rather than normalised.
  **An account is made by signing in, and removing one removes it** (D66 §3): *sign in to another
  account* runs the harness's own login into the next free `account-N` and keeps it only if the
  sign-in finished; the roster names it by who the harness says is signed in there, read fresh on
  every probe and written nowhere. `profile remove` deletes the directory, sign-in included, and
  every default naming it — the screen asks twice first. The harness's own configuration home is not
  under Daoris's directory, and no name reaches it. **A door's accounts are its owner's** (AGT7): a
  harness that declares `accountOf` runs in, defaults to and takes keys from that agent's accounts,
  and keeps only its pin to itself.
- **Install is a whole command; update and login are the harness's own subcommands.** A machine without
  the harness cannot run the harness, so installing is that harness's package manager; a harness that
  is present updates and authenticates itself. The shapes differ because the meanings do.
- **The CLI inherits the terminal; the desktop streams.** A login flow asks questions and waits for a
  code, so `daoris agent login` gives the harness the person's terminal outright — capturing the
  stream to pretty-print it would turn a working login into a hung one. The desktop relays the same
  process through §2's console, under `<harness>:<action>` rather than a session id, because it is not
  a session and must never look like one.
- **The refusals are asked before the record exists**, in the same place as the clean-tree rule, and
  they mirror each other on purpose: a missing binary and a logged-out profile are the same kind of
  answer, and each names the action that fixes it. A held start records nothing, so the quest stays
  open and nobody's.

**What the CLI and the driver share is a FILE and a LAYOUT, not code** — `~/.daoris/harnesses.json`
plus `harnesses/<harness>/<profile>/` — the same twin arrangement the remotes map established (WSP3,
`.claude/knowledge/twins.md`), with three rules asserted in both test tables: a profile is a directory; resolution is chosen → the
workspace's → the machine's → none; and none means the harness's own home. Two consequences for
whoever extends this: the CLI's managed set (`claude-code`, `codex`) is deliberately **not** the
driver's adapter set — managing a tool and spawning sessions on it are different questions, and
`codex` is managed under that name while its sessions ride the `codex-acp` adapter (ACP3) — and the CLI's `driver` verbs edit a file the
C# side owns, so **every edit preserves the fields it has no verb for**; an editor that rewrote
`driver.json` from its own idea of the shape would silently delete the command the stub adapter runs.

The family rehearsal gates all of it with no account, no credential and no model: the stub is a fake
*binary* as well as a fake session — it answers `--version` and `--login-state` — so a spawn under
profile `alpha` is asserted to carry alpha's configuration home **from the session's own output**
rather than from the record's word for it, a logged-out profile holds the start recording nothing, an
uninstalled harness holds it the same way, and both surfaces are driven from a terminal. Its remote
phase carries the other half: machine b drives under a named account, its own record says so, the fed
record carries the tool version and **never** the account name, and the remote's store is scanned
byte-level for it — three guards drop that name, and "three guards" is a claim about code while the
scan is a claim about the artefact.

**What the browser loop holds, and what it structurally cannot.** The roster and the per-conversation
picker ride the shell's bridge, so Playwright — which drives a real browser over the real bundle —
cannot reach them, and the platform's shell-attached surfaces stay the vitest inner loop's to hold
with a mocked bridge. Two things there ARE browser-reachable and both are gated: the session record's
new fields, rendered end to end through the real host (the chain from request contract to DOM is
where a nullable field quietly stops arriving), and the negative guarantee that **a browser learns
nothing about this machine's harnesses** — no reachable settings tab, no roster, no picker, no
configuration home anywhere on the page. That last one belongs in the browser suite precisely because
a browser is the place it has to hold.

## 5. What lives where (the D46 §7 table, extended)

| The service (passive; model-free; spawn-free) | The driver / desktop |
|---|---|
| Session records — now with `Kind`, optional quest, harness version + profile name | The ring buffers, the console stream, chat input — over IPC only |
| Quests, unchanged — a chat uses the same doors | The `interactive` spawns, PTY/stdio wiring per adapter |
| Nothing about the toolchain — a remote has no binaries to manage | Probe, install, update; the roster surface; credential profiles (directories and names — the secrets stay in each harness's own store) |

## 6. How it is verified

The same discipline: **driven, not asserted, no model in the gate.** The stub adapter grows an
interactive mode — a script that echoes a canned exchange, takes a quest when told to, and exits —
and the family rehearsal's driver phase gains: a chat session opened in an example repository, input
delivered, output observed in order, the record closing `completed` with `Kind: chat`; the
repository-busy refusal when a chat holds the tree a driven quest wants. The ring buffer and the
tee are unit-tested in the driver suite (bounded, ordered, file unchanged). Probe is unit-tested
against a fake binary; install/update and login are person-actions on real installers and stay out of
gates — the refusal-names-the-action paths (missing binary; logged-out profile) are what get tests.
Profile selection is gate-testable with the stub: the environment seam is observable, so the rehearsal
asserts a spawn under profile A carries A's configuration home and its record names A. The
`claude-code` interactive adapter is then a deployment choice on a proven loop, reporting itself in
every record (D24's split, again).

## 7. Deliberately not in this design

- **No cross-machine chat.** Records sync, processes never (D46/D47); a remote person sees the record.
  If a team wants live shared consoles, that is a new disclosure argument, not a transport detail.
- **No model selection UI.** Which model a harness uses is the harness's own configuration in that
  repository (`model-decoupling`); Daoris displays what the record reports and configures nothing.
  *Reversed by D98 (2026-09-30), on the owner's word.* An account's model and effort are now set in
  the tool's own settings file under that account, from Settings or `daoris agent settings`. One
  conversation's are set on the protocol door, from the options the agent itself offers on
  `session/new`. The terms are the tool's and the choice is the person's; Daoris still names no model
  and chooses none.
- **No doctrine writes from chat surfaces** — a chat session's agent edits doctrine in its repository
  under its own review flow, like any session; the platform's D31 boundary does not move.
- **No second UI.** The chat and console land in the existing session drawer and Projects view; the
  one-UI rule (D38) holds.
