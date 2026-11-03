# The areas: how Daoris works now

Each document here is the current truth of one area, written from the code as it stands (CONSOLIDATE1, 2026-11-04). A change reads
its area first, through the `change` skill, and rewrites the area's sentence in place when behaviour changes. Each rule ends with
the decisions it replaces. The decisions under `docs/decisions/` are history, opened only for the reasoning; their status table is
`docs/decisions/README.md`.

| Area | What it covers |
|---|---|
| [Doctrine and the CLI](doctrine-and-cli.md) | the canon, sync and the lock, `status`, the reconcile, the region, the command tree, set-up |
| [Knowledge, memory and records](knowledge-memory-records.md) | the knowledge service and index, the connector's tools, memory, the remote, this repository's records |
| [Install, home and update](install-home-update.md) | an install's layout, the home, tools and pins, publishing, staging and updating, older builds |
| [Driving](driving.md) | quests, asks, intakes, driven sessions, their trees, the look, limits, a session's states |
| [Conversations and context](conversations-and-context.md) | chats and Ask Daoris, go-ons, moves and hand-overs, the brief, the standing block, a line's counts |
| [Agents and harnesses](agents-and-harnesses.md) | the agents and their doors, `AgentRun`, accounts and rotation, a session's spawn, errors, models |
| [Roles and supervision](roles-and-supervision.md) | Ask Daoris's doors, appointed roles, staffing and policy, the supervisor and its rules |
| [Workflows, review and landing](workflows-review-landing.md) | saved graphs, gates, runs, review and Accept, landing, the conversion, presets |
| [Co-working and messages](co-working-and-messages.md) | channels and levels, asks, helpers, partners, opinions, quests between repositories, *At work*, *What needs you* |
| [Git and remotes](git-and-remotes.md) | Daoris's git identities and doors, access and go-aheads, remotes and record sync, the person's git acts |
| [The desktop app](desktop-app.md) | the host and its bridge, the page's frame, design language, canvas, translation, the browser, the views |
| [Plugins](plugins.md) | the kit, manifests, offers and updates, plugin harnesses, queries, the plugins page |
| [Code structure and the process](code-structure-and-process.md) | the structure gates, one way, D231's units, `change`, the gates, merging and measuring |

## Choices settled

The seven choices the area documents raised were settled on 2026-11-04 (D233 point 4): a tree is never reused; a fresh start
keeps the words alone and Ask Daoris's start-from hands the brief; Codex holds no denies and the git doors enforce push, force and
delete for every agent; role suggestions name models; the Accept and hand-over proposals go; the launcher refuses an older build;
a home moves off ambient git credentials only when the git doors are on. The every-door rule is retired, the supervisor and the
steward become roles of the one role system, and the speculative parts are frozen (D233). No older reader is kept (D232).
