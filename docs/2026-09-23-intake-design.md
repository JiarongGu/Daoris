# A regular task through Daoris — from a sentence to a landed, tested change

> Written 2026-09-23 from the owner's direction: *"think about a regular task we will do via
> daoris"* — a workspace of real repositories, a command like *"check the jira ticket <url> and
> this should be using media config instead hard code the video/image field name"*, and Daoris
> expected to **locate the project, start the development via an agent, and test via chrome** —
> *"we might be able to support the testing directly in daoris instead of a browser extension, so
> the plugins' support for deepseek-harness matters; this may need to be a workflow (orca is a good
> example); the local desktop will have its own driver llm; and we should be able to send files and
> urls."* Both reference projects and the named workspaces were read on this machine and nothing
> in them was written. The decision is **D65**.

## 0. The task, concretely

The workspace is a folder of **29 git repositories** — Angular front ends, .NET back ends, Azure
functions, a schema, an importer — none adopted, nine carrying an instruction file of their own.
The command names a ticket by URL and states a change in the vocabulary of the product, not of any
one repository. What the person expects back is: the change made in the repository that owns that
field, proven in a browser, and a report — without saying which repository, and without opening
one.

Every stage of that already exists in Daoris except the first and the last-but-one:

| Stage | What Daoris has | What is missing |
|---|---|---|
| Say what is wanted, with a ticket and a screenshot | A **quest**: title, body, `from`, `to` (D31, D34) | A quest cannot carry a file or a link as such; nothing turns a *sentence* into a quest |
| Find whose problem it is | The **registry**: what each repository owns and accepts, which no search can answer (D34, WSP2) | Nothing reads a sentence against the declarations |
| Do the work as that repository's own agent | The **driver** (D45/D46): a fresh session per quest, claiming its own quest over its own connector, observed never self-reported | A repository must have *adopted* to be addressed — 29 have not |
| Test it in a browser | The **ACP door** hands a session its servers on `session/new` (ACP4); **plugins** declare things (D64) | No server but the knowledge host is ever handed over; nothing puts a browser in a session's hands |
| Then verify, then report | The quest state machine and the session records | Nothing follows one quest with another |
| The brain that decides all of it | Chat **sessions** (SES2): the harness carries the model, Daoris pipes text | No session runs *for the workspace* rather than in one repository |

## 1. The shape

**A person's sentence becomes an intake session, an intake session becomes quests, quests become
sessions, and a chain of quests is the workflow.** Nothing new runs a model; nothing new orchestrates.

### 1a. Intake — a sentence, with files and links, at the workspace

The command enters at the **workspace**, not at a repository: from the desktop's composer with the
workspace scope set, or from a terminal (`daoris-driver ask --workspace <name> [--file …] [--url …]
"…"`) — two doors, D50. It is held as an **ask**: the sentence, its links, its attachments, who asked,
when, and what became of it.

*As built (INT4a, 2026-09-23; D65 as amended):* the ask is a service record, local mode only, and
`daoris-driver ask` is its terminal door. With no intake harness, the declarations tier proposes,
publishes nothing, and says *by declarations only; no intake harness ran*. `--to` publishes at once.
`ask --publish <id> --to <repo>` turns a proposal into a quest, and `ask --close <id> --reason` ends
it. An ask's quests are asked by `ask #<id>`, in its circle. The intake session (§1b) is INT4b; the
desktop composer is INT4c.

*As built (INT4c, 2026-09-24):* an ask is made at the scope the status bar names. Its door lives in
**Quests**, not on the bar: the bar says what is true, and an ask is an action. *Ask* is the Quests
header's primary action and the palette's *Ask the circle…*. The asks sit above the quests as a group
of their own, because quests come from them and a proposal waits on a person. **The circle is the
scope, or the only circle held**. Scoped to none of several, the composer asks which, and never
assumes `default`. The record shows:
- the words;
- the tier, in words (a tier this page does not know is shown raw);
- each proposal as *publish to …*, and any other adopter in the circle;
- the quests it became, as doors;
- its links, and its files by name and size, never where they lie;
- *close* with a reason.

A browser on this machine has the same door, because an ask is the local host's HTTP, not the shell's
bridge. The links, drop, paste and chooser are one molecule both composers share (`compose/carry`), so
the two cannot disagree about how a file arrives.

### 1b. The intake session — the driver's brain is a session, not a model

Daoris opens a **conversation** (SES2) for the ask, in a working tree it owns: `<home>/intake/
<workspace>/`, a directory seeded on first use with an `AGENTS.md` rendered from the registry —
every repository in the circle, what it owns and accepts, where it is — and with the family's
knowledge tools (the connector, ACP4). The session's instruction is the intake's job: read the
ticket with its own web tools, decide the owning repository **from the declarations**, publish the
quest(s) — carrying the ticket's content, the links and the files — and report; where the
declarations do not settle it, ask the person rather than guess.

This is *"the local desktop will have its own driver llm"* in Daoris's terms, and it keeps D24
whole: **no model is named, the harness carries it**, under the credential profile the workspace's
default names (D49 §4). Which harness is the intake's is a driver choice (`intakeAdapter`, defaulting
to the machine's adapter); dsh over the ACP door (ACP3) makes it any provider the profile's own
settings choose. **The no-model tier is deterministic and reports itself**: an ask with an explicit
`--to` publishes directly; without one, the best overlap between the sentence and the declarations
is proposed *as a proposal* — never published unasked — and the report says *"by declarations only;
no intake harness ran"*. The intake never edits a repository: it publishes, which is the whole
constraint (D32).

**Traps for the build (INT4b), found by mapping the conversation plumbing on 2026-09-23.** Each is
something the existing code assumes that an intake session breaks:

- **A chat is handed no connector** on either door. ACP's `KnowledgeConnector.Offer` and the pipe
  door's `SpawnServers` hand only the driven path's.
- **`ChatRunner` writes raw lines to stdin**, so an ACP `intakeAdapter` would receive text that isn't
  JSON-RPC. The intake is one turn (the ask is its target), so it belongs on the **driven capture
  path**, where `AcpSession` frames one prompt.
- **The MCP `quest_publish` cannot name the ask or its circle**, and the room matches no
  registration, so the ambient workspace is null. The connector handed to an intake session needs
  the ask in its environment, and must publish *as* `ask #<id>` in that circle.
- **`SessionLedger.OpenChatAsync` refuses an unregistered repository**, so the intake needs its own
  open, with the workspace from the caller. An older build reads an unknown `SessionKind` as
  `Driven`.
- **`DriverConfig.ToJson` writes fixed keys**, so `intakeAdapter` must be modelled in the C# record,
  the CLI twin (`driverconfig.ts`) and `DriverModule.State`, or the next toggle deletes it.
- **`WorkingTree.HeadAsync` walks up.** A room under a home inside a checkout (the rehearsal's) would
  report that checkout's HEAD. Copy `SessionTrees`' `--show-toplevel` guard, or record no base.
- **Never route the intake through `SessionTrees`**: its paths are `<workspace>/<repository>`.
- **`chat` and `trees` in the driver host sit outside its `try`**, so a `DriverException` there
  crashes rather than exiting 2. `ask` catches its own.

*As built (INT4b, 2026-09-24; D65 as amended):* the intake is the driven loop's second kind of start,
and each trap above is met where it lies.

- **Off until a harness is named.** `intakeAdapter` in `driver.json` names the intake's harness;
  absent is off, and the ask is the declarations tier's alone, exactly as INT4a built it. This amends
  *"defaulting to the machine's adapter"* above. An intake spends a real login on every ask, and a
  machine that has been answering asks by declarations must not start spending accounts on an
  upgrade. The harness is named rather than switched on, because which harness answers asks and which
  does the work are two choices. The two doors are `daoris driver intake <adapter>|off` and the
  bridge's `SET_INTAKE`. The C# record, the CLI twin and `DriverModule.State` all model the field and
  write it only when named, so no toggle deletes it.
- **Where the loop picks asks up.** Each tick, after the quest plan and only in the slots the quests
  left, it takes the asks that are `Proposed` and that no intake has served. It takes the oldest in
  each circle, and one per circle per tick.
- **How it is opened.** `POST /api/sessions/intake` goes to `SessionLedger.OpenIntakeAsync`, the
  intake's own open. The record is a **chat** (SES2): it serves no quest, its repository is
  `ask #<id>`, and its circle comes from the ask. Its tree is the room and it has no base commit. A
  new `ask` field names the ask. A new kind would read as `Driven` in an older build. A chat is what
  every build already reads as a session nothing plans from. The ask gains `intake`, the session
  that served it. The open is refused for an ask that is gone (404), and for one that is published,
  closed, or served before (409). One intake per ask, so the loop never retries a harness forever on
  a question it could not settle. **The room's lock is the process**: one intake queued, starting or
  working per room. A parked intake has asked and ended, so it leaves the room to the next ask.
- **The room.** It is `<driver home>/intake/<circle>/`, one folder per circle, whatever the circle
  is called. It is re-rendered at every open, because declarations change. It holds `AGENTS.md`:
  who owns and accepts what, where each repository is, and who declared nothing or is not adopted.
  `CLAUDE.md` carries `@AGENTS.md`. `.claude/settings.json` allows the family's read tools,
  `quest_publish` and `WebFetch`, and nothing else. The design's *"its own web tools"* is `WebFetch`.
  Over ACP a permission request is refused by construction (D52), so an unlisted tool is a stall. The
  room gets the driven path's trust preflight (DEPLOY1). It is never a `SessionTree`, and git is never
  asked about it.
- **How it publishes as the ask.** The spawn carries `DAORIS_ASK_ID` and `DAORIS_SESSION_ID` and no
  quest variable. The connector offered to it carries both too: on `session/new` over ACP, and first
  in the `--mcp-config` file under the home over the pipe door. A chat is handed neither. The MCP
  host reads them as an `IntakeScope`. `quest_publish` then publishes through
  `AskDesk.PublishAsync` with the session's draft: its title, body, links, files and chain. The
  quest is asked by `ask #<id>` in the ask's circle, and the ask's own links and files always travel.
  The body is the intake's words with the asker's own quoted beneath them. The ambient workspace is
  the ask's circle. `POST /api/asks/{id}/publish` takes the same draft and `session`, and that is the
  stub's door. **The tier moves to `intake` only when the publishing session is the ask's own
  intake.** A session naming itself is a claim, and the ask is what checks it.
- **How it ends.** The ending is observed, never self-reported (D46 §4): the exit code, and what
  became of the **ask**. If the intake published onto it, the session is `completed`. If the ask was
  closed, or someone else published it, while the intake ran, it is `stood-down`. A non-zero exit
  with nothing published is `failed`.
- **How "ask the person" surfaces.** A clean exit that published nothing is the intake asking, so the
  record parks `awaiting-person`. Its note names both answers: `ask --publish <id> --to <repository>`
  and `ask --close <id> --reason`. The next tick sees it parked and says so once (SURF5b), as
  *"ask #id — a session needs you"*. The question is the intake's own last words on its transcript
  and console, and it never enters a record that travels. The person answers the **ask**, and the
  next tick ends the parked record: `completed` for a publish, `stopped` for a close. A parked intake
  has no process left, so its question being answered is the only thing that can close it.
- **The HTTP answers grew two fields.** An ask answers `intake`, and a session answers `ask`. Both
  are null for everything that is not an intake.

Proven by service tests over the ledger, the desk and the MCP door. Driver tests run a real node stub
intake through `Driver.TickAsync` against a stand-in service on loopback: it publishes a chain, and
it parks and is ended by the person's answer. The CLI and module tests hold the switch. A
family-rehearsal phase, written with this change and **not yet run**, drives the same over the real
host's new doors (`/api/sessions/intake`, `GET /api/asks/{id}`, the drafted publish), which no unit
test reaches. **Not covered by any gate:** a real harness's intake, which is the owner's to run on a
real ask. The MCP host's publish-as-ask under a real harness's `--mcp-config` or `session/new`
environment is built by construction and was not observed. The room's trust hold, and what the
pipe door's servers file holds for an intake, are shared with the driven path and not asserted for
the intake.

### 1c. Files and links on a quest

A quest gains **`links`** (strings, travel with the quest, shown as links) and **`attachments`**
(files copied under `<home>/quests/<id>/attachments/` by content hash, the way the harness reference
keeps attachments durable and content-addressed; **machine-local**, the same boundary as a
transcript, D47 §4 — a remote sees the names and not the bytes, stated on the record). A session is
handed them the way it is handed everything else: the composed target names them, and
`DAORIS_QUEST_ATTACHMENTS` points at the directory. The desktop's composer takes drops and pastes;
the terminal's `--file` and `--url` take paths and addresses.

*As built (INT2, 2026-09-23; D65 as amended):* the local host tells a caller on this machine where
each kept file lies, and the driver sets the variable from that answer rather than deriving it,
because the driver's home is not always the host's. A link is an absolute http or https address. A
quest carries at most 10 files and 20 MB together. A shared door refuses content outright. The host
serves a kept file loopback-only and sandboxed. `--file` and `--url` arrive with the ask (INT4);
until then the MCP door's `quest_publish` takes `links` and `attachments` (paths).

### 1d. Locate — declarations first, a session second

The registry is the answer to *whose problem is this* (D34), and it stays the answer: the intake
session reads the declarations and decides; a person can always say `--to`. What this asks of the
named workspace is **declarations for 29 repositories**, which is the Projects view's *declare*
form per repository (WSP2) or `daoris init` inside each — the adoption playbook, agent-executed
with the owner's review. A repository nobody has declared is not addressable, and the intake says
so rather than guessing.

### 1e. Develop — registered is drivable

Today the exchange refuses a quest to a repository without a manifest (`Adopted`) and the planner
sits it (*"has not adopted, so there is no agent to be"*). That rule predates ACP4: the connector
travelled only in a repository's own `.mcp.json`, so an unadopted repository had no voice. Over the
protocol door the session is handed its connector on `session/new` with nothing written anywhere.
**Proposed amendment**: a repository that is *registered with a root* is drivable over the ACP
door; adopted doctrine is what the session reads if present, and the pipe door keeps its own
requirements (a repository's `.mcp.json` and trust, DEPLOY1). The distinction becomes the honest
one: **registered is addressable; adopted is disciplined.**

### 1f. Verify — a browser in the session's hands, declared by a plugin

The harness reference's browser-use is a provider seam whose providers are **MCP servers**
(Playwright MCP, Chrome DevTools MCP). Daoris does not need the seam; it needs the servers. A plugin
gains **`servers`**: MCP servers to hand to every session, beside the knowledge host — over ACP on
`session/new` (ACP4's shape), over the pipe door with the harness's own `--mcp-config` pointed at a
file Daoris writes **under its home**, never into the repository (the shape HELP3 measured for
`--settings`). The browser is then an ordinary tool of the session: it opens the app, clicks
through the change, reads the page. *"Testing directly in Daoris instead of a browser extension"*
is exactly this — the session drives a browser; no extension, no Daoris runtime in the loop. The
example plugin ships with the Playwright MCP declared, so a machine with Node has a browser one
`daoris plugin add` away. Permission posture is unchanged: the session's tool calls are the
harness's, under the repository's own configuration (D37, D46 §5).

### 1g. Workflow — a chain of quests, and the driver is the engine

A workflow here is *develop, then verify, then report* — and Daoris already has the engine: the
driver turns quests into sessions. A quest gains **`then`**: a follow-up to publish when this one
closes `done` — its `to`, title and body written with the parent's id in hand ("verify #a1b2 in
the browser at http://localhost:4200: the video and image fields read their names from the media
config"). The exchange publishes the follow-up at the moment of closing, atomically with the
close; the driver picks it up at its next look. The intake session composes the chain. A chain is
data on the quest, not a script: the harness reference's engine runs model-written orchestration
scripts and orca's coordinator runs task DAGs with decision gates — both are the right shape for
*their* products and both are declined here (D1). A decision gate is what `AwaitingPerson` already
is: a session that parks and asks. v1 chains are linear; a fan-out is a later `then` list.

*As built (INT5, 2026-09-23; D65 as amended):* `then` is an ordered list of steps, judged when the
chain is composed. Every step is asked on behalf of the chain's asker, and every step shares the
first one's home. A step's id derives from its parent, and `{parent}` in its words becomes that id.
A decline stops the chain. The composer offers one next step; `quest_publish` and the HTTP door take
up to five.

### 1h. Report

The ask's record links the intake session and every quest it published; the Work frame reads a
chain as one thread (the attended session's timeline already reads the protocol). The report is
the records, which is what a person verifies (D37).

*As built (INT4d, 2026-09-24):* an ask that waits on a person is listed in Overview's *What needs
you*, and its record says who answered it.

- **Which asks wait.** A live ask waits on the person unless its intake session is queued, starting
  or working. While an intake runs, the ask is the harness's until it ends. Two kinds of row come
  from it. A **proposal** is an ask no intake is serving, or whose intake ended without publishing:
  the declarations proposed, and only a person publishes. **Its intake asked you** is an ask whose
  intake parked `awaiting-person`. The page cannot know whether this machine names an intake
  harness, so an ask the loop has not picked up yet shows as a proposal until it does. The proposal
  is the person's to take either way, and the band never guesses what a driver will do next.
- **One thing, one row.** A parked intake is also a parked session, and the ask's row stands for
  both. The parked session leaves the list only while its ask is in hand and live. A parked intake
  whose ask the page has not loaded stays a parked-session row, so nothing waiting is ever dropped
  because a list did not arrive.
- **Where the row leads.** Both kinds open the ask's record in Quests, in a browser as on the
  desktop. The answer is there: publish to a repository, or close. The question is on the intake's
  transcript, and the record is the way to it.
- **What the row says.** The ask's first line, its circle, and how long it has waited: since it was
  asked, or since its intake parked. Under that is the service's sentence about a refused receiver,
  verbatim, or what the declarations proposed. An intake that asked shows its own parked note,
  verbatim, as any parked session does.
- **The record names who answered.** The tier `intake` has words in both catalogues. A service test
  holds every tier the desk can write against both catalogues, from the side that decides which
  tiers exist. The intake session is a line of the record: its state, then its tool. On the desktop
  the tool is a door into Sessions. A browser has no Sessions, so there the session is named and is
  not a door. An intake the page has not loaded is named by its id.
- **Order and count.** Parked sessions come first, because they hold a working tree. Asks come next,
  oldest first, because nothing downstream moves until the person settles one. Quests nobody can
  take come last. The band and the Sessions badge share `needsAPerson`, so the badge counts these
  asks too, as it already counted the quests nobody can take.

## 2. What is deliberately not built

- **A model inside Daoris.** The intake is a session; the harness carries the model (D24, D1).
- **A browser extension, or a browser of Daoris's own.** The session drives one through MCP.
- **A workflow engine or a coordinator agent.** A chain is data; the driver is the loop.
- **A ticket-system integration in the host.** The session reads a ticket with its own web tools;
  a machine that wants more declares a Jira or Linear MCP server through a plugin — orca's ticket
  skill is a skill over its own CLI, and Daoris's equivalent is a server a plugin declares.
- **Attachments crossing machines** in v1; they are machine-local like transcripts, and a remote
  reads the names.

## 3. Build order

| Item | What lands | Proven by |
|---|---|---|
| **INT1** | Plugins declare `servers`; the driver hands them over ACP beside the knowledge host and over the pipe door by `--mcp-config` from a file under the home; the example plugin declares the Playwright MCP | the family rehearsal's stub reports every server offered; a driver test holds the pipe door's file |
| **INT2** | `links` and `attachments` on a quest — store, HTTP and MCP surfaces, the compose drawer, `DAORIS_QUEST_ATTACHMENTS` and the composed target | service and driver tests; the rehearsal drives a quest with a file and a link |
| **INT3** | Registered is drivable over the ACP door (the exchange and the planner amended; the pipe door unchanged) — **the owner's yes first** | the rehearsal drives a registered, unadopted newcomer over ACP |
| **INT4** | The intake: the ask record, the room under the home seeded from the registry, `daoris-driver ask`, the desktop composer at workspace scope, the declarations-only tier reporting itself | a rehearsal ask with no model resolves by declarations and says so; a real ask on the named workspace is the owner's proof |
| **INT5** | `then` on a quest, published at close | the rehearsal chains develop → verify over the stubs |
| **INT6** | Onboarding the named workspace: `import`, declarations per repository, the first real ask — owner present | the report it produces |
