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

## Choices only the owner can settle

The area documents list about 140 places where a decision's text and the code, or two decisions, disagree. Nearly all are older
text a later decision or the code replaced, and the area document now states what holds. These are the ones that need a choice:

1. **Trees between lines.** D214 hands a free tree from one line of work to the next; D51 never reuses one, and the code follows D51.
   Build D214, or keep D51?
2. **Starting a conversation from words.** Ask Daoris's start-from hands the earlier transcript (D158 point 4); a repository's
   *Start a conversation with these words* hands the words alone. Which one way?
3. **The person's denies on Codex.** D184 promises the person's denies hold in every setup; Codex takes no rules file, so they do
   not reach it. Enforce them another way, or say Codex holds none?
4. **Naming a model.** D24 and D98 say Daoris names no model; the role suggestions name `sonnet` and `opus` (D205). Keep the
   suggestions, or drop the names?
5. **Delivery acts as proposals.** D161 says no quest-delivery act becomes a proposal; Ask Daoris proposes Accept and hand-over. Keep
   those proposals, or remove them?
6. **An older build at a closed start.** The launcher's swap at a closed start never asks whether rolling back is allowed, so the
   home's floor can be wrong after it. Refuse such a swap, or let it and mend the floor?
7. **The git move.** Every home is moved off ambient credentials at its first start and its SSH remotes with no identity are held
   (GITMOVED1); D198's steward note says a release without the doors leaves git as it was. Which is right?
