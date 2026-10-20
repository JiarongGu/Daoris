# Daoris (道衍) — roadmap

Daoris runs a development company whose staff are agents (D193): the person owns it and gives it work, Ask Daoris is
its front desk, each repository is a department that owns its work, sessions are its staff, the fabric is how each
department works, message levels keep its rhythm, the CLI and the connector are its network, knowledge is its growing
know-how, and machines are its offices. [The concept](docs/2026-10-21-company-concept.md) maps each part to the
mechanism that serves it. The desktop is where the owner sees and steers it (D45).

This file gives the forward sequence. [TASKS.md](TASKS.md) holds open work and dependencies;
[the archive](docs/task-archive.md) holds completion receipts. A design states a contract, not a
promise that every step is built. [The document router](docs/README.md) names the contracts and
their later amendments.

## Sequence, by the part of the company each makes work

| Priority | Part of the company | Outcome | Contract and open work |
|---|---|---|---|
| 1. How each department works | The fabric | Work is routed into pipelines by its kind; the heavy gates run as a verification pipeline of their own, never in a worker's session; a run is governed by its bound graph | D157, D173, D190, D192; TASKS: Collaboration Fabric (FABRIC-PIPELINE1, CUTOVER) |
| 2. A network every door shares | The CLI, the connector and the service | The running app owns the machine's state and every terminal verb is its client; a verb acting alone runs the same .NET; the UI's foundations and the driver's tests split so a change builds one area | D50, D182; TASKS: RPC1e, PUSH1f2, UIDESIGN1, DRIVERTESTSPLIT1 |
| 3. Staff who are counted, released and briefed truly | Sessions and accounts | A seat is held only by work under way; a worker's context travels with it to another agent or seat; a run ends when its work does | D130, D189; TASKS: Sessions (SESSCTX1), the cap rows |
| 4. A rhythm nobody breaks | Communication | No member of staff stops another's work; departments hear each other at natural points; answers carry work on at most twice before the owner; meetings bring departments to one question | D188, D176; TASKS: COWORK1e, MEETUX, AGENTROOM |
| 5. A quiet view for the owner | The desktop | The owner's corrections first: one row and one name per piece of work, counts that agree, the Overview listing only what the owner can act on, in their language, and a department's knowledge after a move | D150, D152, D158, D185; TASKS: the owner's UI/UX findings, UI/UX first, Consistent screens |
| 6. A front desk that knows the company | Ask Daoris | It answers from what is driven and decided, holds only the grants the owner chose, and turns a request into an ask | D110, D183; TASKS: HELPMODE1c4, STARTERSCOPE1 |
| 7. Trustworthy landings and delivery | Quality assurance and delivery | Review the work where it runs, obtain a second opinion where configured, show every hold before Accept, and follow landed work through its checks and stages | D154–D157; TASKS: Workflows, Person-only doors, Review environments, Second opinions |
| 8. Know-how that grows and travels | Knowledge and offices | What holds everywhere is promoted to the canon; offices of a workspace exchange records through the remote and agree on how work closes | D21, D47, D48, D129, D151; TASKS: Economy, setup and recall, FABRIC-CLOSE1i |
| 9. Departments set up dependably | Doctrine and set-up | Existing knowledge is kept, checks stay green, and each repository has a generated orientation index | D117, D122, D124, D128; TASKS: Development and agent layout |
| 10. Equipment and capacity from real use | Tools, plugins and finance | Plugin distribution and managed-tool boundaries; a session never idle for want of the machine; the capacity estimate once history allows | D120, D121, D148, D179; TASKS: Plugin distribution, Declarations and managed tools, PLUGINEVAL1, USAGECAP1b |
| 11. Management the company runs itself | The steward | The parent's planning, dispatch, merging and verification move into Daoris's own roles | D160, D187, D191; TASKS: Development and agent layout |

**Alongside every priority:** the owner's install builds this repository's rows (DOGFOOD1), so each part is tried on the company's own work as it is built, and what blocks that work is filed as it is found.

The table is an order for choosing work, not a relaxation of a row's dependencies or proof. UI
changes need tests, stories and installed-window evidence where their contract calls for it.
Owner-only account, installation and publication steps remain identified in the backlog.

## Growth criteria

A pack needs a repository ready to adopt it. A new adapter needs measurements of its actual door.
Account policy and session limits need usage evidence. A release needs the declared gates and
rehearsals; adoption and publication remain the owner's call. Development is currently unpublished
`0.0.x` (D105); the [release workflow](.github/workflows/release.yml) owns version stamping.

The longer horizon is weighed in [the future-directions review](docs/2026-10-03-future-directions-review.md).
Its proposals require their own decisions before becoming work. Earlier milestones and the former
roadmap are retained in [the historical roadmap](docs/archive/2026-10-10-roadmap.md).
