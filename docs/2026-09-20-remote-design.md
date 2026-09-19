# The remote — design

> Written 2026-09-20 (DRV3). This is the mechanism for **part 3 of D45** — the remote server, for
> teams — which the decision deliberately left open, and it folds in the old SVC2 hardening. Read with
> `docs/DECISIONS.md` D45/D46 (the direction and the driver), D21 (local-first, sharing as
> configuration), the service design §§4–6/8 (`docs/2026-08-05-knowledge-service-design.md`), and the
> driver design §§4/7/9 (`docs/2026-09-19-driver-design.md`).

## 1. What it is for

One machine's family already coordinates through one store (D36): every session spawns over the same
file, a quest published from one repository is waiting when another's session starts, and the driver
turns that queue into execution (D46). Part 3 is the same loop across machines: a quest published on
one machine triggers a driven session on a teammate's; knowledge a repository has opted in becomes
recallable across the team; session records made anywhere are reviewable everywhere. **The remote is
fed, not authored** (D45): doctrine lives in repositories and moves by `sync`/`upstream` exactly as
today; records are made locally and fed up; the remote originates nothing.

## 2. The principle: local-first — the remote is fed, never required

- **Every machine stays fully useful with no remote.** D21's local mode is untouched; absence of
  configuration is the default and it is silent. Nothing built for part 3 may cost the three
  properties of service design §3.
- **The remote holds records and arbitration — never processes, never doctrine, and no model.** It is
  D36's deployment grown a credential model, not a new artefact.
- **Two kinds of data, two consistency rules.** *State transitions write through* — synchronously, or
  they fail naming the remote — because a lock that is eventually consistent is not a lock. *Records
  sync eventually* — queued, idempotent, resumed after restart — because a session record arriving a
  minute late loses nothing. Every design choice below is one of these two rules applied.

## 3. What the remote is: the same host, in shared mode

**The remote is a deployment of the existing HTTP host** — the same executable D43 ships, the same
`QuestExchange`/`SessionLedger` judgement classes both hosts already share. D36 moved the judgement to
one place so two doors could not drift; a separately-built remote would be that bug at team scale.

**Git-as-store, priced and declined.** §6 of the service design said prefer a git repository before a
database, and §8.1 asked whether shared needs hosting at all. Priced against what now exists:

- **D45 changed the question.** When quests were passive records, "shared may be a sync" (D21). The
  driver made quests an execution queue, and a queue two machines race needs an arbiter that can
  refuse the second `take` *before* work starts. Git detects the conflict at push time — after a
  session has already spawned, worked, and committed. The serialization point a lock needs *is* a
  host, so git-as-store does not remove the deployment; it hides it.
- **The hosting cost git was avoiding has already been paid.** The host ships self-contained (D43),
  persists in one SQLite file (D36), and needs no model (D24). What §8.1 was protecting against — a
  deployment, an account, an ops burden bought before it earned anything — shrank to "run one binary."
- **What survives of §6's argument** does so in its original home: versioned, reviewable knowledge
  lives in the repositories, which remain the source of truth the remote merely indexes. A hosted
  database still earns its place only when the SQLite file measurably fails — that ceiling is
  unchanged, just behind the host instead of instead of it.

**Shared mode is configuration, not a build** (D21). And it carries the sibling's fail-safe inversion
(service design §5): **a host asked to bind beyond loopback without shared mode's credential model
configured refuses to start.** Today the key middleware is skipped entirely when no key is set and
GETs are never gated even with one (`Http/Program.cs:117-135`) — right for loopback trust, and
exactly what must be unreachable on a network interface.

## 4. The disclosure boundary at the sync

The boundary of service design §4, applied to a feed instead of an index. **Two declarations, both in
the manifest** — tracked and reviewed, because what may leave a repository is the repository's call,
not one person's local toggle — **and silence means local** (the asymmetric-cost rule of D21):

| Declaration | What it admits to the remote |
|---|---|
| **join** | The registration (stripped), quests addressed to or from this repository, its session records |
| **share knowledge** | Its indexed knowledge *content* — requires join |

**What never leaves a machine, structurally.** The feed's DTOs carry no field for any of these — the
same construction as "the driver has no push capability at all" (D46 §6): absent, not policed.

- **Repository roots.** Already guarded at both ends — `connect` sends the root only to a
  hostname-allowlisted local service (`connect.ts:41-48,74`), the host answers it only to loopback
  callers (`Http/Program.cs:300-305`) — and the feed closes the promise of D46 §4: no root field
  exists in anything fed.
- **Transcripts.** The record's `transcript` is a machine path and stays home; the transcript file
  was never in the database to begin with. *Today's gap, closed by this build:* `GET /api/sessions`
  emits `transcript` unguarded (`Http/Program.cs:335-337`) — it gains the root's loopback guard on
  the local host and does not exist in shared mode's projection.
- **Private-class content** — the untracked local directory. Never indexed for the feed at any
  setting (D21's hard exclusion; the scanner never reaches it today, and the feed inherits that).
- **Unjoined repositories, entirely.** No registration, no quests, no sessions, no knowledge: a
  repository that never opts in is invisible to the remote, not partially visible.
- **Driver configuration** (drivable, holds, the cap) — per machine, per person (D46 §2), never fed.
- **Keys**, redacted on every path including failures (§5b's near-miss).

**Knowledge feeds as content, never as vectors** (`model-decoupling`). Each deployment embeds with
its own configured provider, and vectors from different models are incomparable — nothing is lost, as
vectors are not persisted even locally (`ServiceFactory.cs:109`, in-memory, rebuilt per process). With
no model, the remote still serves lexical search and the first two convergence passes, saying so on
every response (D24). Only **local-provenance** entries feed: canonical doctrine is already
distributed to every machine by `sync`, and re-indexing it remotely would duplicate the one class
convergence deliberately ignores. The seam already exists: `DisclosurePolicy.Sharing`
(`DisclosurePolicy.cs:14-15`), built for this and never yet composed — the remote's ingest constructs
it over the joined-and-sharing set. The remote never scans a filesystem; fed entries are the only
entries there are, the same shape D36 established for pushed registrations.

## 5. Quests across machines: one home, and the lock becomes code

**A quest has one home, decided at publish, never migrated.** Home follows the receiver: a quest to a
**joined** repository lives at the remote; a quest to a local-only repository lives in the local
store exactly as today. One home means one authority for every transition — there is no
reconciliation step, because there is never a second copy that can disagree.

**The local store mirrors remote-homed quests; verbs write through.** The sync loop pulls quests
touching this machine's joined repositories into a marked mirror, so the platform renders and the
driver plans offline. Every transition on a remote-homed quest goes synchronously to the remote's
`QuestExchange` — publish, take, done, decline — and an unreachable remote is a plain refusal naming
it, never a queued verb (rule 2 of §2: records queue, transitions never).

**The hardening the lock was always owed.** "The quest state machine is the only lock" (D46) is
today honored by convention: sessions check state before acting, but `SetStatusAsync` moves any quest
to any status with only an existence check (`Quests.cs:134-149`) — a `Done` quest can be dragged back
to `Taken`. One machine's driver plus in-order sessions made check-then-act tolerable; two machines'
drivers watching one quest make it a duplicated-work generator. The build gives quests the transition
table sessions already have (`SessionLedger.cs:163-175`):

| From | To |
|---|---|
| Open | Taken (atomic — the UPDATE carries `WHERE status = 'Open'`), Done, Declined |
| Taken | Done, Declined |
| Done, Declined | *(none — a closed quest does not move; a new ask is a new title, D46 §3)* |

A refused `take` names the quest's actual state, and the racing session **stands down** — the path
DRV2 already built and the family rehearsal already drives. The cross-machine race resolves by the
same mechanism as the race with an outside session: no new states, no driver-level lock, and the
hardening lands in the shared judgement class, so local mode gets the same honesty for free.

**Content-derived ids make the mirror cheap.** The same ask has the same id everywhere
(`Quests.cs:100-103`), publish is idempotent by collision, so a double-publish from two machines
converges on one quest instead of two.

## 6. Sessions across machines: records sync, processes never

The promise written into the store's own comments (`Sessions.cs:82-83`) lands here.

- **Records feed up** for joined repositories — state, note, evidence, timestamps — attributed to the
  feeding principal (§7), and keyed by *origin + id*: session ids are random 8-hex and two machines
  will eventually mint the same one. The remote ingests records; it does not re-judge them — the
  `RepositoryBusy` and transition judgements already ran at the store that owns the process, and the
  working tree that is the unit of exclusion (D46 §9) is per machine by definition.
- **Evidence syncs, transcripts stay** (§4). The reviewable record is the repository's own commits;
  the transcript is diagnostics for the machine that ran it.
- **Controls act where a driver is attached** (D46 §6, now with a second half): the platform over a
  remote *reads* every machine's sessions; drivable, hold, and stop act only on the local driver's
  own processes. One machine's driver cannot stop another's — a cross-machine stop *request* (a
  record the owning driver honors on its next tick) is held until a real team asks for it (§12).
- **Remote session records are advisory for scheduling, never a lock.** A driver may defer spawning
  when another machine's session is already working the same repository — an optimization that saves
  a stand-down, not a correctness requirement. The quest state machine remains the only lock.

## 7. Identity: two consumers, two credentials (SVC2 lands here)

Service design §5, built as specified; D46 held per-caller identity precisely for this deployment.

- **Machines carry keys.** Per person *and* per machine, minted by a person, shown once, stored as a
  hash with a short non-secret prefix for audit and revocation, **expiring by default**, redacted on
  every path including failures. A key is issued *as* a principal with repository access (§5c) —
  scope: reads its principal may read, plus the narrow writes a machine makes (feed records, quest
  verbs as its repositories' agent). Sent as today's bearer header; `DAORIS_SERVICE_URL` +
  `DAORIS_SERVICE_KEY` remain the machine contract (§5b).
- **People carry OIDC**, delegated to the team's IdP, validator IdP-agnostic. Where identity is
  configured, the platform is served from the remote and people mint their own keys in it; where it
  is not (one person, two machines), the remote is an API only — keys are minted at the host's own
  console, and the person's window stays the desktop over the local mirror. The development auth
  scheme is honoured **only in the Development environment** — the committed-config inversion of §5,
  unreachable in a deployment by construction.
- **Shared mode gates every route.** Today's key gates `POST /api/*` only — reads were priced for
  loopback trust. A remote serving the family's accumulated knowledge to unauthenticated GETs would
  be the §5 leak with no key leaked; in shared mode, reads authenticate like writes.
- **Attribution falls out.** Keyed writes give quest transitions and session records their principal,
  so "who took this, from which machine" is answerable — recorded because the write carried it, not
  because a second identity model was invented (§5c's rule).
- ~~Local trust (no key, loopback) and single-key (D36's write gate) remain what they are today —
  loopback deployments. §3's startup refusal is what keeps them there.~~ **Amended 2026-09-20, on the
  owner's redesign grant (nothing is deployed):** D36's interim single-key gate is retired rather than
  carried. Two trust shapes only — local trusts the loopback outright (D21; the startup refusal keeps
  it there), shared gates every route with minted keys. `DAORIS_SERVICE_KEY` survives solely as the
  client-side "key I present" (§5b), which in shared mode is a minted key.

## 8. What lives where

| The remote (shared mode) | The local store | The desktop/driver host | Never leaves a machine |
|---|---|---|---|
| Remote-homed quests — the one lock | Local-homed quests; the remote mirror | The sync loop and its queue | Repository roots |
| Fed session records, by origin | Its machine's session records | The machine key, machine-locally | Transcripts and their paths |
| Fed registrations (stripped) | Registrations with roots | Processes and adapters (D46 §7) | Private-class content |
| Fed knowledge content of sharing repositories; its own embeddings | The machine's full index | driver.json — drivable, holds, cap | Unjoined repositories, entirely |
| Keys (hashed) and the OIDC validator | | | driver.json, keys |

## 9. The relay: who talks to the remote

- **The desktop host carries the sync loop** — D45's "fed via the local desktop app," in the shared
  driver/desktop layer so `daoris-driver` headless feeds identically: a server machine with checkouts
  and a key is just another machine, not a special deployment. The loop pushes the feed (idempotent
  upserts, resumed after restart) and pulls the quest mirror on the same cadence as the driver tick.
- **Quest verbs relay through the judgement seam.** The write-through for remote-homed quests lives
  behind `QuestExchange` in Core with the remote client selected at each composition root — both
  local doors (MCP stdio, HTTP) get it identically, the same no-drift argument as D36. Sessions keep
  talking to their local host; the machine key stays in machine-local configuration
  (`~/.daoris/remote.json`, environment overriding per §5b), never per-repository.
- **The CLI is nearly untouched.** `connect` already speaks to a remote and already strips the root
  (`connect.ts:62-76`); it gains only the manifest's two declarations in its payload. `check` and
  every doctrine command stay offline (D8) — the existing import-boundary tests are the proof, and
  they do not move.

## 10. How it is verified

The family rehearsal grows a **remote phase** — driven, not asserted, no model, no real IdP:

- One remote host in shared mode; two simulated machines (two local stores, two sync loops, two
  minted keys). A quest published on machine A for a repository joined by machine B arrives through
  the mirror and is driven to done by B's stub session; A sees the closure and the session record.
- **The race**: both machines drivable for one quest — exactly one session completes, the other
  stands down on the refused `take`; the quest's history shows one taker.
- **The strip, proven by scanning the artefact**: the remote's store contains no root, no transcript
  path, and nothing — registration, quest, session, entry — from the unjoined repository.
- **The gate on the door**: a wrong, expired, or revoked key answers 401 on reads and writes; no
  failure output anywhere contains the key (the §5b near-miss, held by a test that has seen it fail).
- Unit, in the service suite: the quest transition table and the atomic take; the startup refusal on
  non-loopback binding without shared mode; the development auth scheme inert outside Development.

## 11. Deliberately not in part 3

- **No doctrine writes on the remote, ever** — D31/D38's boundary, now with a third host honoring it.
- **No remote-hosted driving.** The service stays spawn-free wherever it runs (D46 §7); a machine
  that should drive runs a driver beside its own checkouts.
- **No cross-machine process control** — records ask, owners act; even the stop *request* is held.
- **No second store technology.** One SQLite file until it measurably fails (§6's ceiling).
- **No knowledge merge or auto-promotion.** Convergence proposes; `upstream` in the owning
  repository disposes (D31) — team scale changes the audience, not the rule.
- **No offline queue for state transitions.** A verb that cannot reach the lock fails; queueing it
  would be the eventually-consistent lock §2 rules out.

## 12. Open questions, deliberately held

1. **The cross-machine stop request** — a record-level ask the owning driver honors on its tick.
   Held until a real team wants it; the advisory session view (§6) is the seam it lands in.
2. **A live channel from the remote** — the mirror polls on the driver's cadence; if real team use
   shows staleness that matters, that evidence picks the mechanism (SSE first, being one-way).
3. **Key storage in the OS secret store** — machine-local file + environment first; the desktop can
   grow keychain storage when a deployment cares about at-rest theft on a trusted machine.
4. **Retention and quota on the remote store** — fed records grow monotonically; pruning policy
   waits for a measured size, not a guessed one.
5. **Team review flow for convergence findings** — who acts when two *teammates'* repositories have
   learned the same thing twice. Today's answer (each `upstream`s in their own repository) may be
   enough; a shared queue for it is not designed until it is not.
