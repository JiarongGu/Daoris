# Daoris as a company run by agents — the concept (2026-10-21)

**Standing:** contract for the project's framing (D193). It names what Daoris is in one picture, maps that picture to the
parts Daoris already has, and orders the roadmap by it. It changes no mechanism: each part keeps its own decisions and
designs, which this document points to.

## 1. The picture

Daoris runs a development company whose staff are agents and whose product is software. The person owns the company:
they set its direction, give it work, and accept what it delivers. Everything else is the company's own running: who
takes a request, which department does the work, how each department works, how departments talk, what the company
learns, and how its offices stay in step.

The picture is useful because each of its parts already has a home in Daoris, and because the gaps in the picture are the
gaps in the product: a company whose departments interrupt each other mid-task, whose quality checks run inside every
worker's desk, or whose know-how stays in one office is a company that does not work, and the same is true of Daoris.

## 2. The parts, in Daoris's words

| In the company | In Daoris | Its home |
|---|---|---|
| **The owner** | The person: sets direction and targets, accepts or declines outcomes, holds the accounts and the money, and is asked only what is theirs | D37's carve-outs, D191 point 3 |
| **The front desk** — the one place every request arrives | **Ask Daoris**: hears the person in plain words, answers what the company knows, proposes changes to its settings, and turns a request into an ask; the intake routes an ask into quests | D110, D183, the intake (INT rows) |
| **Departments** | **Repositories**: each owns its domain and its work, and no department edits another's | D45, `repository-owns-its-work` |
| **Staff** | **Sessions**: agents working in a department on a quest, each on an account (a paid seat with an allowance); the cap is how many work at once | D130, D189, the account-use design |
| **Work orders between departments** | **Asks and quests**: a request travels, the edit never does; a quest names what is needed and why, and its outcome comes back | D45's core loop, D65 |
| **The handbook** — how staff behave | **Doctrine and harness**: the rules, skills and knowledge every repository receives from the canon, and each agent's settings and permissions | D11, D13, D183, the canon |
| **How a department works** | **The fabric**: workflows, their graphs and, from FABRIC-PIPELINE1, their pipelines; the start node routes work into a pipeline | D157, D173, D190 |
| **Quality assurance** | **Verification**: the declared gates, a second opinion and a look before a landing, and a verification pipeline of its own that works like CI, never inside a worker's session | D115, D154, D180, FABRIC-PIPELINE1 |
| **The rhythm of work** — a sprint | **Message levels and natural points**: work runs undisturbed between natural points (a turn's end, a part's start); an ordinary message waits for the next one, an urgent one reaches the next step only when the work in flight would be wrong without it, and only the person stops work; outcomes are reviewed when the work lands | D188, the co-working skill |
| **The communication network** — the company's internet, its chat | **The CLI, the connector and the service**: the CLI is how anyone reaches the company from a terminal, the connector is the agents' tools for posting, reading, asking and publishing, and the service carries the records between them | D45, D50, D182 |
| **The company's know-how** — what it learns over time | **Knowledge**: each repository's decisions, fixes and rules, indexed and searchable; what holds across repositories promoted into the canon (`daoris upstream`) and handed back to every repository | D9, D21, D129, D151 |
| **Offices** | **Machines and workspaces**: each machine runs its own staff on its own accounts; a remote exchanges the permitted records between offices so knowledge and quests travel, never a machine's private state | D45 part 3, D47 |
| **Records and minutes** | **Decisions, the archive, the changelog, session records**: what was decided and why, what was done, what a user sees changed, and what each session did | D127, `task-lifecycle` |
| **Equipment and vendors** | **Managed tools, Daoris's browser and plugins**: what staff work with, and outside capabilities a plugin brings in under the person's approval | D99, D120, D121, D148 |
| **Management** | **The steward**: plans, dispatches, merges and verifies; today a session outside the install, to become a role the company runs itself (DOGFOOD1) | D160, D187, D191 |

## 3. What the picture asks of the product

Read against the picture, the product's open gaps are these, each a row in the backlog:

- **The front desk sees the whole company.** Ask Daoris answers from what is driven and decided, and every grant it holds is
  the person's (HELPMODE1c4, STARTERSCOPE1).
- **Staff are counted and released truthfully.** A seat is held only by work under way and given back the moment a quest
  closes (CAPCOUNT1, DONEEND1, SESSBADGE1, merged); a worker's context travels with its work when it moves to another
  agent or seat (SESSCTX1).
- **Each department's way of working is configured, not instructed.** The fabric routes work into pipelines and keeps
  verification out of a worker's session (FABRIC-PIPELINE1, then the CUTOVER rows).
- **Communication keeps the rhythm.** Nothing stops work but the person; departments hear each other at natural points;
  answers do not chain without the person (COWORK1e); meetings bring several departments to one question (MEETUX, AGENTROOM).
- **Know-how grows and travels.** A repository's knowledge stays its own when it moves between workspaces
  (OVERVIEWNOISE1d), what holds everywhere is promoted to the canon, and offices exchange it through the remote.
- **The owner's view is quiet.** The desktop shows what needs the owner and little else, in the owner's language
  (the owner's UI/UX findings of 2026-10-20).

## 4. What it is not

- **Not a hierarchy of agents commanding agents.** A department never directs another's work; it sends a work order, and
  the receiving department decides how (`repository-owns-its-work`). Management dispatches and verifies; it does not edit
  a department's work from outside it.
- **Not a model judging outcomes.** Whether work is done, how it routes and whether it may land are decided by declared
  rules and the person, never by a model's reading (D24, D54, D157).
- **Not a replacement for the owner.** The owner is asked less, not never: direction, money, sign-ins, publishing and their
  data remain theirs (D191 point 3).
