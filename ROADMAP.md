# Daoris (道衍) — roadmap

Daoris drives development across domain-owning repositories: a person sets a target, intake routes
it into quests, agents work in their own repositories, and the person reviews the outcome. The
desktop is the working surface. The connector distributes doctrine; the service keeps knowledge,
quests and session records; an optional remote exchanges those records between machines (D45).

This file gives the forward sequence. [TASKS.md](TASKS.md) holds open work and dependencies;
[the archive](docs/task-archive.md) holds completion receipts. A design states a contract, not a
promise that every step is built. [The document router](docs/README.md) names the contracts and
their later amendments.

## Sequence

| Priority | Outcome | Contract and open work |
|---|---|---|
| 1. Make everyday work clear | Readable accounts, conversations, titles, menus and confirmations; settings on the thing they control | D150/D152/D158; TASKS: UI/UX first, Accounts and browser, Consistent screens, History and reliability |
| 2. Make landing trustworthy | Review the work where it runs, obtain a second opinion when configured, and show every workflow hold before Accept | D154–D157; TASKS: Workflows, Person-only doors, Review environments, Second opinions |
| 3. Keep long work recoverable | Preserve requirements and queued words, resume on the right account, and explain stops, pauses and cleanup | D125/D126/D130–D133/D137/D153; TASKS: Sessions, History and reliability |
| 4. Make repository setup dependable | Preserve existing knowledge, keep checks green, and give each repository a generated orientation index | D117/D122/D124/D128/D129/D151; TASKS: Economy, setup and recall, Development and agent layout |
| 5. Make development scale | Complete the landing queue and lane coordination before steward automation; measure process-test cost | D115/D106; TASKS: Development and agent layout, Remaining regressions |
| 6. Extend from real use | Finish plugin distribution and managed-tool boundaries, then add capabilities when a consumer needs them | D120/D121/D148; TASKS: Plugin distribution, Declarations and managed tools |

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
