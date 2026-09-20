# The interactive surface — chat sessions, live console, managed harnesses

> Written 2026-09-20, from the owner's direction set in the backlog the same day: chat sessions and a
> console display directly in Daoris (developing via claude code / codex from inside it), and Daoris
> managing the agent CLIs themselves — install and update. This extends the driver design
> (`docs/2026-09-19-driver-design.md`) without weakening its contract: sessions stay observed,
> processes stay on the machine, and Daoris still makes no model calls at all. The direction is D49 in
> `docs/DECISIONS.md`; the companion is `docs/2026-09-20-workspace-design.md`.

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

## 4. The toolchain: harnesses Daoris installs and updates

The driver depends on the harness binaries existing (`claude`, `codex`); today that is the person's
memory. The toolchain manager makes it Daoris's job — **explicitly, never automatically**.

- **Detect freely, act on request.** Each adapter gains a `Probe` — locate the binary, report its
  version (`claude --version` etc.) — run at driver startup and on demand; the platform shows the
  roster: harness, version, present/absent. Detection is free and read-only.
- **Install and update are the person's click**, per harness, using that harness's own official
  mechanism (an adapter obligation, beside spawn: `claude-code` via its documented installer/npm
  package, `codex` via its own). Output streams through §2's console (it is a process like any other).
  **Never mid-session and never unasked**: a harness changing under a running loop is the
  moving-target problem `reaching-in` documents, one layer down. The driver refuses to spawn on a
  missing harness with the sentence naming the install action, instead of a bare not-found.
- **Versions are recorded**: the session record's `adapter` gains the harness version observed at
  spawn, so "which tool produced this" is answerable later — the same authorship instinct as
  version-stamping (D-release), applied to the tool that did the work.
- Rejected: **auto-update** (a gate's tool must not change between two runs nobody diffed); **Daoris
  pinning harness versions in the manifest** (the harness is machine tooling, not repository doctrine —
  the repository's own docs may demand a minimum, but the manifest stays inert data about doctrine).

## 5. What lives where (the D46 §7 table, extended)

| The service (passive; model-free; spawn-free) | The driver / desktop |
|---|---|
| Session records — now with `Kind`, optional quest, harness version | The ring buffers, the console stream, chat input — over IPC only |
| Quests, unchanged — a chat uses the same doors | The `interactive` spawns, PTY/stdio wiring per adapter |
| Nothing about the toolchain — a remote has no binaries to manage | Probe, install, update; the roster surface |

## 6. How it is verified

The same discipline: **driven, not asserted, no model in the gate.** The stub adapter grows an
interactive mode — a script that echoes a canned exchange, takes a quest when told to, and exits —
and the family rehearsal's driver phase gains: a chat session opened in an example repository, input
delivered, output observed in order, the record closing `completed` with `Kind: chat`; the
repository-busy refusal when a chat holds the tree a driven quest wants. The ring buffer and the
tee are unit-tested in the driver suite (bounded, ordered, file unchanged). Probe is unit-tested
against a fake binary; install/update are person-actions on real installers and stay out of gates —
the refusal-names-the-action path is what gets a test. The `claude-code` interactive adapter is then
a deployment choice on a proven loop, reporting itself in every record (D24's split, again).

## 7. Deliberately not in this design

- **No cross-machine chat.** Records sync, processes never (D46/D47); a remote person sees the record.
  If a team wants live shared consoles, that is a new disclosure argument, not a transport detail.
- **No model selection UI.** Which model a harness uses is the harness's own configuration in that
  repository (`model-decoupling`); Daoris displays what the record reports and configures nothing.
- **No doctrine writes from chat surfaces** — a chat session's agent edits doctrine in its repository
  under its own review flow, like any session; the platform's D31 boundary does not move.
- **No second UI.** The chat and console land in the existing session drawer and Projects view; the
  one-UI rule (D38) holds.
