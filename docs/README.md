# The documents — what each one is now

Every document under `docs/`, by what kind of thing it is and where it stands. A document is written
once for a moment, and later decisions amend it. When a document and a later decision disagree, the
decision wins, and the amended documents say so where they were amended. The superseded ones are in
[`archive/`](archive/README.md), with what replaced each.

**Kinds.** A *contract* is what a part is and must keep being; it is built against. A *method* is
how something gets built. A *study* is input to a decision, read for its reasoning. *Evidence* is
what was measured, and it stays a record of that moment. A *record* is append-only history.

## Contracts and methods

| Document | Kind | For | Where it stands |
|---|---|---|---|
| `2026-08-04-daoris-design.md` | contract | the CLI | Current. Its notes carry D54 and D59 where they changed it |
| `2026-08-05-knowledge-service-design.md` | contract | the service | Built. Its banner names what later decisions answered; §3–§4, the disclosure classes, still bind |
| `2026-09-19-driver-design.md` | contract | the driver (D45, D46) | Current, with D72's permission change noted |
| `2026-09-19-platform-design.md` | contract | the platform (D38) | Current in shape; the views it names have moved since (D55, D66, D75) |
| `2026-09-19-platform-ux.md` | contract | the design language (D41, D56) | Current. §4 records what each looking pass settled; read it before changing anything a person looks at |
| `2026-09-19-frontend-architecture.md` | method | the platform's stack and tests (D42) | Current |
| `2026-09-20-workspace-design.md` | contract | workspaces (D48, WSP) | Current |
| `2026-09-20-interactive-design.md` | contract | chat, console, managed harnesses (D49, SES) | Current, with D67 noted |
| `2026-09-20-remote-design.md` | contract | the remote (D47) | Current; its superseded list names what D68 replaced |
| `2026-09-21-working-surface-design.md` | contract | the working surface (D51, D52) | Current, with D66 noted |
| `2026-09-21-working-surface-components.md` | method | how a screen is built | Current: a story first, and a molecule imports no hook |
| `2026-09-21-desktop-frame-design.md` | contract | the desktop's frame (D56) | Current, with D66 and D75 noted |
| `2026-09-22-instruction-file-design.md` | contract | the always-loaded tier in `AGENTS.md` (D59) | Current |
| `2026-09-22-toolchain-design.md` | contract | binaries, pins, accounts, usage (D57) | Current, with D63 and D67 noted. The home of the resolution rule |
| `2026-09-23-api-key-accounts.md` | contract | an account that is an API key (D67 §1) | Current |
| `2026-09-23-intake-design.md` | contract | an ask becomes quests (D65) | Current, with D70, D72 and D77 noted |
| `2026-09-23-map-design.md` | contract | the maps (D67 §3, MAP) | Current |
| `2026-09-23-plugin-design.md` | contract | plugins (D64) | Current, with D71, D77's `${data}` and D78's `${browser}` noted |
| `2026-09-23-sync-design.md` | contract | the remote as a git remote (D68) | Current, with D69 noted |
| `2026-09-24-menus-design.md` | contract | the menus as setup domains (D75) | Current |
| `2026-09-24-permission-scopes-design.md` | contract | what an agent may do (D72, D74) | Current |
| `2026-09-27-in-app-browser-design.md` | contract | Daoris's own browser, driven over MCP (D78) | Current. BRW1 and BRW2 built; BRW3 is the owner's run |
| `2026-09-27-ask-and-wait-design.md` | contract | A session asks another repository, waits, and is resumed with the answer (D79) | Current. ASK1–ASK3 built; §1 amended while building (the quest stays taken); §1.6 is D80, the carry-on after a cut-off |

## Studies and evidence

| Document | Kind | Carried by |
|---|---|---|
| `2026-09-20-working-surface-research.md` | study | SURF1, the working-surface design |
| `2026-09-21-dsh-evaluation.md` | evidence | D53 |
| `2026-09-21-ide-reference-study.md` | study | D55 |
| `2026-09-22-acp3-probe-evidence.md` | evidence | D53, ACP3 |
| `2026-09-22-first-deployment-case-study.md` | evidence | D60, D63. Read it before touching the desktop |
| `2026-09-22-plugin-design-study.md` | study | the plugin design, D64 |
| `2026-09-24-agt2b-channel-evidence.md` | evidence | D67, AGT2b |
| `2026-09-24-deploy1-acp-trust-evidence.md` | evidence | D73 |
| `2026-09-24-reference-gap-study.md` | study | D76, the live direction |
| `2026-09-25-stream-json-evidence.md` | evidence | D76, CONV3a, CONV5 |
| `2026-09-25-message-content-evidence.md` | evidence | D76, CONV4c, CONV4d |
| `2026-09-27-first-goal-study.md` | study | D77, the first goal: what INT6 would have met, the references, Lyntai's storage |
| `2026-09-28-console2-streams-evidence.md` | evidence | CONSOLE2: a subagent, a background task and a command's output on the protocol door, once declared |

## Records

| Document | What it holds |
|---|---|
| `DECISIONS.md` | Every decision, numbered, with its reasoning; from D51 on, with what it rejected |
| `task-archive.md` | Every finished task, with its date and outcome |
| `FIX-LOG.md` | Root cause, fix and verification for each non-trivial defect |
| `2026-09-25-rev3-review.md` | REV3's ledger, and CLEAN1's item by item |
| `2026-09-26-ux5-screen-audit.md` | UX5's ledger: every surface and state looked at, and what each finding became |
| `adoption/` | A local mechanics draft prepared for an adoption that has not run |
| `code-map.json` | Generated by the devkit's `map`, and gated by `map --check`; never edited by hand |
