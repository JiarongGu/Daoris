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
