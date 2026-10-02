# The driver — design

> Written 2026-09-19 (DRV1). This is the mechanism for **part 2 of D45** — the local driver — which the
> decision deliberately left open. It is the contract for `Daoris.Desktop` re-scoped (DRV2). Read with
> `docs/DECISIONS.md` D45 (the direction), D37 (the operating model), D32 (quests), D22/D23 (composition
> and the one-supported-harness seam), and `src/Daoris.Desktop/README.md` (the brief this settles).
>
> **Paths.** `~/.daoris/…` below is the Daoris home as it was when this was written. Since D63
> (2026-09-23) the home is `$DAORIS_HOME`, the install's own `data/`, and nothing lives under the user
> profile. The tree beneath it is unchanged.

## 1. What it is for

The loop D45 names: a target becomes a quest → the driver starts the owning repository's agent session
with that quest as its target → the session works inside its own repository, under its own doctrine and
gates (D37's middle) → done or declined flows back through the service → the person verifies outcomes in
the platform. Today everything in that loop exists except the third arrow: a quest waits for a session
that happens to exist. This document settles how the driver supplies that arrow — how a session starts,
what its lifecycle is and where it lives, the adapter seam, where the person's checkpoints surface, and
which half of the machinery belongs to the service versus the desktop host.

## 2. The principle that shapes everything: driving is additive, never exclusive

Set by the owner alongside the direction (2026-09-19): **a repository developed outside driver-managed
sessions stays first-class.** Work done by hand, or in an interactive session the driver never started,
still flows through the connector exactly as before — doctrine syncs outward, refinements go back via
`upstream`, and quests publish sideways. And a quest published from such a session **triggers driver
sessions in other repositories** exactly as a platform-filed one does. The driver adds a way for work to
*start*; it takes nothing over.

Three consequences, each load-bearing:

- **The quest's own state machine is the only lock.** `Taken` is the mutex between a driver-started
  session and anything else working the family. The driver itself **never writes quest state** — sessions
  and people do, through the same `QuestExchange` door with the same refusals
  (`src/Daoris.Service/Daoris.Service.Core/QuestExchange.cs`). A driver-started session and an
  interactive one are indistinguishable at the quest layer, which is precisely the property that keeps
  outside development first-class — and it means the driver needs no identity of its own to be safe.
- **The driver observes; it does not own.** Its two signals are quest transitions (from the store it
  already shares with every session on the machine) and process lifetime (of the sessions it spawned).
  Work it did not start produces the first signal and not the second, and that is enough to stay out of
  the way.
- **Drivable is per repository, per machine, chosen by the person.** Whether *this* machine's driver may
  start sessions in a repository is the person's call in the driver's own configuration — not repository
  doctrine, not a manifest field. A repository that never opts in loses nothing it has today.

## 3. The trigger: spawn fresh, one session per repository

**Spawn, not wake.** The driver starts a fresh, non-interactive agent session per quest, in the owning
repository's working tree. The MCP host already proved this shape (D36): the session is the ephemeral
thing and the store is what persists. A long-lived idle agent cannot be pushed to without inventing a
channel, burns its context waiting, and holds doctrine that has since synced — where a fresh session
enters through the repository's own discovery skills, which is the doctrine path in. Waking (resuming an
earlier session for a follow-up) is held as an **adapter capability**, not the mechanism (§10).

**Queue semantics.** At most **one active session per repository** — the working tree is the unit of
exclusion, and two agents in one tree corrupt each other's git state. Across repositories, concurrency is
the point of the whole direction; a configurable cap bounds it. Within a repository, oldest `Open` quest
first — the ordering the service already returns and the Overview already renders.

**A session spawns only onto a clean working tree.** Uncommitted changes are somebody's work in flight —
the lesson `reaching-in` was written from — so the driver holds that repository's queue and says why,
rather than entangling a session with work it cannot see the shape of.

**"The driver should have started this," precisely.** A quest qualifies when all of these hold: it is
`Open`; its receiver is registered and adopted — or, since D70, registered with a root and started
over the protocol door, which hands the session its connector on the wire; the receiver is drivable on this machine; no session is
active there; the person has not held the repository; the tree is clean. The Overview's "is anything
sitting" splits in two: sitting because nobody *can* take it (not adopted, not drivable — the person's
setup work) versus sitting because the driver has not started it (the driver's state to explain, visible
with the reason: queued behind another session, held, dirty tree).

**Claiming: the spawned session takes its own quest.** The delivered target instructs the session, as the
repository's own agent, to `take` the quest over its own connector as its first act, work it, and close
it with `done` or `decline` — the same three verbs everything else uses. If it finds the quest already
`Taken`, someone else got there first and it stands down cleanly; a race with an outside session resolves
in the outside session's favour by construction. A spawn that fails leaves the quest `Open` and
untouched — there is no claimed-but-abandoned state to repair, because claiming never preceded working.

**One edge the id scheme sets.** Quest ids are content-derived — `SHA256("{from}->{to}:{title}")`
truncated — and publishing the same ask returns the existing quest unchanged, *including one already
closed* (`Quests.cs:100-111`). So one title is one quest forever: a recurring target needs a new title,
and the driver keys sessions by quest id and never re-opens closed quests.

## 4. The session lifecycle

| State | Meaning |
|---|---|
| `queued` | The driver decided to start this; waiting on the repository's slot, hold, or tree |
| `starting` | The process is being spawned |
| `working` | The process is alive |
| `awaiting-person` | Parked at a checkpoint only the person can clear (§6) |
| `completed` | Exited; its quest reached `Done` |
| `declined` | Exited; its quest reached `Declined`, with the reason |
| `stood-down` | Exited without working; the quest was already taken or closed by someone else |
| `failed` | Exited with the quest still `Open`/`Taken` and no explanation, or the process died |
| `stopped` | The person cancelled it — or, with `interrupted`, the orphan sweep or the driver's shutdown ended it (D104) |

*Amended by D104 (DRV8, 2026-09-30).* A `stopped` record says whether it was **interrupted**: the orphan
sweep found nothing running it, or the driver shut down under it. The person's stop never says so. A
take ended so is carried on like a `failed` one (D80) and counts as a strike; a person's stop stays their
decision. The note stays a sentence for a person, and nothing is decided from it.

**Transitions are observations, not reports.** The driver moves a session by what it can see — process
lifetime from the spawn, quest transitions from the store — rather than trusting an in-band status
protocol that an interactive session would not speak anyway. **Gates-green is not a state; it is outcome
evidence.** The session works under its own repository's loop — commit per task once gates are green
(D37 as amended) — so the record carries the evidence bundle the person reviews: gate results, landed
commits, and the quest's closing note.

**A driven session takes no person's line** (INT4i, 2026-09-24, as built). It was handed its whole
quest at once and works it in one turn. On the pipe door it has no stdin; on the protocol door its stdin
carries the driver's own frames (D53), so a line written there would land in the middle of the JSON-RPC
stream. Measured before the rule: a `Send` for a protocol-door driven session answered `true`, which
means the line was written into that stdin. The process registry tracks the session with the driver's
sentence, so `Send` and `CloseInput` refuse before anything is written, and the bridge answers
`SESSION_INPUT` and `END_CHAT` with that sentence (`DRIVER_REFUSED`) rather than a `false` that reads as
*it ended*. The same rule as an intake's (INT4h, intake design §1h). **Only a conversation takes turns
with a person**, and its tracking is unchanged. The page never offered a driven session a box; this
closes the door a caller naming its id could still use.

*Amended by D90 (SESS3, 2026-09-29).* On the protocol door a driven session also has an **inbox**
(`DrivenInbox`, held in the process registry because a session outlives the tick that started it).
`SESSION_INPUT` holds the person's words there ahead of the refusal above, and when the turn ends
`AcpSession.RunAsync` prompts each one in the same session before closing it; `CANCEL_TURN` stops the
turn so what is held goes now, and `SESSION_QUEUE` says whether the session is listening, which is when
the page shows a box. The refusal still guards everything written into the stream and a finish, and
an intake and a pipe-door session keep it whole.

*Amended by D136 (STEER1, 2026-10-03).* Where the agent's `initialize` says it takes a prompt during a turn
(`agentCapabilities._meta.claudeCode.promptQueueing`, `claude-agent-acp`), `SESSION_INPUT`'s words are sent at once as
a prompt in the same session and reach it at its next step; `AcpSession.RunAsync` ends only when nothing is held and no
word is on its way, and `CANCEL_TURN` stops nothing there. Every other agent keeps the turn's end. The record keeps the
words the moment they are said, with `reaches`, and again where the session took them, under one id
(`docs/2026-10-03-steer-evidence.md`).

*Amended by D137 (MSG1, 2026-10-03).* An ended state has one way out: to `working`, with the person's words waiting, on
this machine's record, when its harness conversation resumes with them. A native driven session holds words too, and
resumes its conversation with them before it concludes (`docs/2026-10-03-session-messages-design.md` §2).

**Session records live in the service, beside the quests.** A `sessions` table in the same store
(`~/.daoris/knowledge.db`), for the same reason quests do: every client benefits — the platform renders
them, the record survives a driver restart, and part 3 later syncs records where it could never sync
processes. **Process handles never leave the driver.** Writes go through a shared judgement class
mirroring `QuestExchange` — one implementation both hosts use, so they cannot drift — and the HTTP
surface grows `GET /api/sessions` plus the driver's key-gated writes, with every new DTO registered in
`ApiJson` (`Http/Program.cs`), which is load-bearing: an unregistered DTO breaks only in the
AOT-published build.

**Transcripts are local files**, under `~/.daoris/sessions/<id>/`, referenced by the record and never in
the database. They are diagnostic, not the record — the reviewable record is the repository's own commits
and documents, per `autonomous-development`.

**The registration gains the one field spawning needs: the root.** The registry today holds no
filesystem path anywhere (`Registry.cs:15-26`), and reconstructing `knowledge-root/<name>` breaks for any
repository living elsewhere. `connect` runs *in* the repository and already knows its root
(`connect.ts:44-56`); it sends it, and the service stores it **machine-locally, never leaving the
machine** — part 3's local→remote sync strips machine-local fields, which is the disclosure boundary
(service design §4) applied to paths exactly as `sensitive-info` applies it to tracked files.

## 5. The adapter seam

D23, one layer up: **one harness is supported; the others are explicit, never guessed.** An adapter is a
descriptor with four obligations:

1. **Spawn** a non-interactive session in a repository root.
2. **Deliver the target** — the quest verbatim (id, title, body, asker) plus the claiming instruction of
   §3 — however that harness accepts an initial prompt, and ensure the repository's connector tools are
   available to the session, however that harness requires that to be stated.
3. **Map the harness's permission surface onto the D37 boundary** — reversible in-repository work
   proceeds; nothing at the outward boundary is ever auto-approved. The repository's own checked-in
   permission configuration governs; the adapter grants nothing beyond what an interactive session there
   would have. *Amended by D72 (2026-09-24): what a session may call is the union of Daoris's
   defaults and scopes, handed to the harness at spawn, and the repository's own settings — the
   harness merges them and a `deny` beats an `allow`. Daoris adds allowances (the connector, a commit)
   and refusals (no push, the tree guard); it never lifts a repository's deny.*
4. **Report process lifetime**, and declare capabilities (resume, richer progress) honestly.

**`claude-code` is first and supported.** **`codex` is second and explicit.** An unknown adapter is a
tool error naming what exists — never a silent fallback — the same amended-D23 rule that governs
harnesses in the CLI.

**An adapter names a harness, never a model** (`model-decoupling`). Which model a session runs on is that
harness's own configuration in that repository; the session record reports the adapter and whatever tier
the harness reports. The driver itself makes no model calls at all — it is scheduling and process
control, and every part of it works with no model on the machine.

## 6. The person's checkpoints (D37 in the loop)

- **Setting targets is publishing quests** — already in the platform, unchanged.
- **The session-control surface** is the one addition the brief allows to the one UI: sessions and their
  states beside the quests they serve, with the person's controls — drivable per repository, hold, stop a
  session, start-now. In a browser over a keyed remote it reads; the controls act where a driver is
  attached, which is the desktop.
- **`awaiting-person` arrives with its analysis** — options, recommendation, reason — never a bare
  "may I?" mid-run (`autonomous-development`). Under a non-interactive harness this mostly lands at
  session end: a session that reaches a decision only the person can make closes its run by surfacing
  the decision, and the record parks there.
- **Outcome verification is the second checkpoint**: the quest `Done`, the session record's evidence
  bundle, and the landed history, reviewed in one sitting.
- **The outward boundary is structural, not policed.** The driver has no push, publish, or release
  capability at all — a session that reaches that boundary parks, and the person acts or declines.
- **Notification**: the shell forwards tick reports over the runtime's notification path — which is,
  measured (2026-09-19), a host→page IPC channel rather than OS toasts — so session events surface as
  the platform's own toasts while the window is open; the browser keeps polling. An OS-level toast for
  a parked session while the window is closed is the app's own code, held as open question 5.

## 7. What lives in the service versus the desktop host

| The service (passive; model-free; spawn-free) | The driver (in the desktop host) |
|---|---|
| Session records, behind a shared judgement class | The scheduler loop: watch → queue → spawn → observe |
| The registration's machine-local root | Process control, and the adapters |
| Quests, exactly as today | Transcript capture under `~/.daoris/sessions/` |
| No push channel in part 2 — the driver and the UI poll the local surface, which is cheap where it runs | Driver configuration (`~/.daoris/driver.json`): the drivable set, holds, the concurrency cap |
| | OS notifications, and hosting: the HTTP host, the platform, the session-control surface |

**The service never spawns a process.** It must stay deployable where "spawn" is meaningless — a remote
host driving nothing (D36) — so the trigger/session *state* is service surface and the trigger *action*
is the driver's alone. The driver is a client of the service like every other component, through the same
doors.

## 8. How it is verified

The way every layer here is verified: **driven, not asserted, with no model in the gate.** A **stub
adapter** — a script that takes its quest, makes a commit in the target repository, and closes it —
drives the whole mechanism deterministically: the family rehearsal grows a driver phase in which a quest
is published to an example project, the driver claims nothing, spawns the stub, and the session record
ends `completed` with its evidence, then a decline and a stand-down exercise the other terminals. The
loop creates its consumer (D44), and the mechanism is gate-verified end to end. The `claude-code` adapter
is then a deployment choice on top of a proven loop — verified by use, reporting itself in every record —
which is `model-decoupling`'s split applied to the driver: the feature does its useful part with the stub
tier, and says which tier ran.

## 9. Deliberately not in part 2

- **No auto-push, auto-publish, or auto-release, ever** — not scoped out of part 2; scoped out (D37).
- **No doctrine writes** from driver, service, or platform — D31/D38's boundary is untouched.
- **No parallel sessions within one repository** — the working tree is the unit of exclusion; wanting
  parallelism *within* a domain is a reason to split the domain, not the tree.
  *Amended by D51 and PAR1 (2026-09-27): this line and §3's one active session per repository hold only
  for a repository whose sessions run in its root. Where they open trees of their own, several run side
  by side, up to the cap. D115 (DEV1, `docs/2026-10-01-self-development-design.md`) designs the split
  this line asks for: lanes, domains a repository declares, and a queue that lands them. Its first
  piece is built (DEV3, 2026-10-01): a session outlives the tick that started it, so sessions started at
  different ticks run side by side, and a quest an active session serves is not started again. Lanes and
  the queue are not built yet.*
- **No cross-machine driving** — DRV3's problem, fed local-first by this design's records.
- **No per-caller identity** — local trust and the single write key, as today; identity folds into
  DRV3/SVC2 (OIDC and per-person keys), where it is needed to mean anything.

## 10. Open questions, deliberately held

1. **Resume as a follow-up mechanism** — when a closed quest gets a successor on the same ground, is a
   fresh session (today's answer) ever worse than waking the one that did the work? Evidence from real
   driven runs decides; the capability flag in §5 is the seam it lands in. *Answered in part by D131
   (2026-10-02): a park the person answered resumes its own harness conversation where the account, the
   adapter and the tree are the same (`ISessionAdapter.Resumes`); a successor quest is still a fresh
   session.*
2. **A live channel** — polling is right-sized for a local host; if the platform's session view proves
   too stale in real use, that evidence picks the mechanism (SSE first, being one-way).
3. **Session visibility over MCP** — should a repository's agent be able to ask "is a session already
   working near this?" before publishing a near-duplicate quest? Held until an agent actually wants it.
4. **Recurring targets** — the content-derived id makes one title one quest forever (§3); if real use
   wants standing or scheduled targets, that is a new entity feeding quests, not a change to quest ids.
5. ~~**OS-level notification**~~ — **closed 2026-09-22 (SURF5b).** It resolved as this note
   predicted: the shell grew its own toast over a `NotifyIcon`, because the runtime ships no toast API
   and its `TrayIcon` carries a menu and no balloon. What the note did not anticipate is where the
   *judgement* belongs — `AttentionWatch` lives in the driver library, so a machine with no screen
   reaches the same answer and prints it instead (D50), and the decision is testable where a balloon
   is not. A park is seen by diffing the tick's active sessions; an end is known from the driver's own
   conclusion, which already carries whose decision it was.
