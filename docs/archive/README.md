# Archive — superseded documents, kept for the reasoning

Nothing here describes the project as it is now. Each document was true when it was written and has
been overtaken; they are kept because the *reasoning* in them explains choices the current documents
only state.

| Document | What it was | What replaced it |
|---|---|---|
| `2026-08-01-framework-note.md` | The original sketch: fourteen .NET packages across five pillars | `docs/decisions/D1.md` — six of those already shipped in a sibling, so building them again would have produced a second worse copy. Most of the rest were premature or belonged elsewhere; naming that early is what kept the first version small enough to finish |
| `2026-08-04-daoris-v0.1-plan.md` | The task-by-task execution plan for the CLI, written before any code | Fully executed. Outcomes are in `docs/task-archive.md`; reasons are in `docs/decisions/` |
| `2026-09-20-post-redesign-review.md` | The review of the driver and remote arc (D45–D47), every item checked off | Closed. Its outcomes are in `docs/task-archive.md`; the reviews after it are REV2 and REV3 |
| `2026-09-21-dsh-direction.md` | The plan for evaluating deepseek-harness: the option space and the probes | Executed by `docs/2026-09-21-dsh-evaluation.md`, and decided as D53 |
| `2026-09-21-desktop-design-brief.md` | The brief for the desktop's own design: what was wrong, and what must not move | Answered by D56 and `docs/2026-09-21-desktop-frame-design.md` |
| `2026-09-23-agents-direction.md` | The owner's asks about agents, accounts and wiring, and what the code answered | D67 holds the owner's answers; toolchain design §3a, `docs/2026-09-23-api-key-accounts.md` and the map design the shapes. AGT5 became the MAP arc, and every AGT item is archived but AGT2c, an open row in `TASKS.md` |
| `2026-10-10-development-instructions.md` | The former Claude-specific project and development guide, preserved as written | `AGENTS.md` holds the shared brief; `docs/development.md` holds current contributor procedures |
| `2026-10-10-roadmap.md` | The former roadmap's historical arcs and status summaries | `ROADMAP.md` gives current sequence; `TASKS.md` holds open work |
| `2026-10-10-document-router.md` | The full router before condensation, including its historical summaries | `docs/README.md` keeps every route in shorter subject tables; detail remains in the original decisions |

Read `docs/2026-08-04-daoris-design.md` for the contract, `docs/decisions/` for why, and
`ROADMAP.md` for what is next.
