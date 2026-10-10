# Documentation router

Find a document here, then read the relevant section. A **contract** states what a part must keep;
a **method** describes how to build it; **studies** inform decisions; **evidence** records a dated
observation; **records** hold history; a **proposal** needs a decision before adoption.

Later decisions override earlier contracts. A contract's baseline is historical; implementation
and amendments live in its numbered decision's dated notes. `TASKS.md` holds remaining work.
This router points to those homes rather than maintaining another build-status list (D127/D159).
Paths below are relative to this file. Historical documents have their own [archive router](archive/README.md).

## Guides

| Document | Purpose and standing |
|---|---|
| `../README.md` | Product and doctrine commands; consumer entry point |
| `../AGENTS.md` | Shared brief and generated doctrine |
| `../CLAUDE.md` | Generated import of AGENTS; no separate guide |
| `development.md` | Contributor setup, checks, integration and documentation maintenance |
| `../ROADMAP.md` | Forward sequence; TASKS owns acceptance criteria |
| `../src/Daoris.Desktop/README.md` | Desktop installation, machine bridge and instruments |
| `../src/Daoris.Service/README.md` | Knowledge, quests, hosts, trust and configuration |
| `../src/Daoris.Web/README.md` | Platform views, frontend conventions and test loop |
| `../src/Daoris.Devkit/README.md` | Universal gates, code map and distribution |
| `../examples/README.md` | Adoption and routing; exercised by family rehearsal |
| `../examples/engine/README.md` | Example engine adopter |
| `../examples/game/README.md` | Example game adopter |
| `../examples/plugins/hold-by-title/README.md` | Family rehearsal plugin fixture |
| `../examples/plugins/browser/README.md` | Standalone browser plugin example |
| `../examples/plugins/in-app-browser/README.md` | Browser connection plugin example |
| `../examples/plugins/github-pull-request/README.md` | GitHub landing plugin example |
| `../examples/plugins/azure-devops-pull-request/README.md` | Azure DevOps landing plugin example |
| `../tools/dsh-probes/README.md` | Dated protocol probe instructions |

## Doctrine, knowledge and development contracts

| Document | Kind and purpose | Governing record |
|---|---|---|
| `2026-08-04-daoris-design.md` | Contract: CLI doctrine lifecycle | D54/D59/D105 amend original |
| `2026-08-05-knowledge-service-design.md` | Contract: indexing and disclosure | D21/D24/D123; banner lists amendments |
| `2026-09-22-instruction-file-design.md` | Contract: always-loaded instruction region | D59/D117/D128 |
| `2026-09-23-map-design.md` | Contract: workspace, workflow and code maps | D67; ORIENT1 note |
| `2026-09-30-parallel-development-design.md` | Contract: records, splits and lanes | D106/D115 |
| `2026-10-01-self-development-design.md` | Contract: concurrent work and landing queue | D115 |
| `2026-10-01-agent-layout-design.md` | Contract: agent layouts, mirrors and rooms | D117/D128 |
| `2026-10-01-development-documents-design.md` | Contract: roles and safe-work declarations | D122/D127/D151/D159 |
| `2026-10-01-workspace-setup-design.md` | Contract: setup quests and registration | D124/D128/D129 |
| `2026-10-02-session-economy-design.md` | Contract: reading cost and record shapes | D127 |
| `2026-10-02-setup-pilot-lessons-design.md` | Contract: preserve knowledge and green checks | D128/D129 |
| `2026-10-02-knowledge-design-review.md` | Contract: discovery floor and recall | D129; KNOW3 bench evidence |
| `2026-10-03-decisions-record-design.md` | Contract: per-decision files and checks | D134 |
| `2026-10-04-orientation-everywhere-design.md` | Contract: repository indexes and source ranges | D151 |

## Driver, sessions, sharing and landing contracts

| Document | Kind and purpose | Governing record |
|---|---|---|
| `2026-09-19-driver-design.md` | Contract: planning and session lifecycle | D45/D46; D72/D104/D131/D137 amend |
| `2026-09-20-workspace-design.md` | Contract: workspace membership and sharing | D48/D68 |
| `2026-09-20-interactive-design.md` | Contract: chat, console and managed agents | D49/D67/D98 |
| `2026-09-20-remote-design.md` | Contract: optional team deployment | D47/D68 |
| `2026-09-22-toolchain-design.md` | Contract: binaries, accounts and usage | D57/D63/D67/D98/D125/D130 |
| `2026-09-23-api-key-accounts.md` | Contract: API-key account declarations | D67 |
| `2026-09-23-intake-design.md` | Contract: asks become repository quests | D65/D70/D72/D77/D133/D154 |
| `2026-09-23-sync-design.md` | Contract: fetch, rebase and push | D68/D69/D95/D153 |
| `2026-09-24-permission-scopes-design.md` | Contract: permissions and reading across | D72/D74/D107/D156 |
| `2026-09-27-ask-and-wait-design.md` | Contract: cross-repository questions and carry-on | D79/D80/D131 |
| `2026-09-30-machine-log-design.md` | Contract: machine-local measurements | D94; §4/§6 define lines |
| `2026-09-30-terminal-design.md` | Contract: person's shell in the panel | D96 |
| `2026-10-01-account-rotation-design.md` | Contract: limits, cool-off and rotation | D125/D130 |
| `2026-10-02-account-use-design.md` | Contract: participation and capacity | D130 |
| `2026-10-02-session-management-design.md` | Contract: groups, actions, archive and deletion | D126/D152/D153 |
| `2026-10-02-answer-continues-design.md` | Contract: resume on the same conversation | D131/D137 |
| `2026-10-02-pause-and-clean-up-design.md` | Contract: pause, resume and abandon | D132 |
| `2026-10-03-session-messages-design.md` | Contract: queued words and reopened runs | D137/D136 |
| `2026-10-03-update-when-idle-design.md` | Contract: staged update, drain and rollback | D139 |
| `2026-10-03-evidence-design.md` | Contract: completion evidence checks | D144/D146 |
| `2026-10-04-landing-and-proof-design.md` | Contract: automatic landing and kept proof | D145/D146/D149/D154–D157 |
| `2026-10-04-built-in-git-design.md` | Contract: branches, history and ref actions | D147 |
| `2026-10-07-history-clearing-design.md` | Contract: clear local finished history | D153 |
| `2026-10-08-review-environment-design.md` | Contract: setup and person's review verdict | D154/D155–D157 |
| `2026-10-08-second-agent-design.md` | Contract: another maker's agent reviews | D155/D157 |
| `2026-10-09-person-door-design.md` | Contract: person/driver authorization | D156; starter integration remains in TASKS |
| `2026-10-09-workflow-design.md` | Contract: choices, versions and gates | D157 |

## UI, browser, tools and plugin contracts

| Document | Kind and purpose | Governing record |
|---|---|---|
| `2026-09-19-platform-design.md` | Contract: shared platform | D38/D55/D66/D150/D152 |
| `2026-09-19-platform-ux.md` | Contract: design language; read for UI changes | D41/D56/D141/D150/D152 |
| `2026-09-19-frontend-architecture.md` | Method: stack and test boundaries | D42/D142 |
| `2026-09-21-working-surface-design.md` | Contract: session-oriented working surface | D51/D52/D55/D66/D113 |
| `2026-09-21-working-surface-components.md` | Method: stories and hook-free molecules | D55 |
| `2026-09-21-desktop-frame-design.md` | Contract: window and frame | D56/D66/D75/D118/D152 |
| `2026-09-23-plugin-design.md` | Contract: declarations, wire and plugin kit | D64/D71/D101/D103/D140/D145/D146 |
| `2026-09-24-menus-design.md` | Contract: original domain menus and workspace naming | D75; D152 replaces menu layout |
| `2026-09-27-in-app-browser-design.md` | Contract: browser seam and agent tools | D78/D84/D85/D99 |
| `2026-09-28-chromium-host-design.md` | Contract: shipped Chromium | D85/D92/D93/D99 |
| `2026-09-29-ask-daoris-design.md` | Contract: help conversations and proposals | D89/D110/D158 |
| `2026-09-29-dock-design.md` | Contract: movable panels and Quick Ask | D118 amendments; §4 records build |
| `2026-09-30-setup-guide-design.md` | Contract: first-start guidance | D97/D150 |
| `2026-10-01-naming-design.md` | Contract: bilingual names and glossary | D116 |
| `2026-10-01-frame-model-design.md` | Contract: each view's list and main panes | D118/D150/D152 |
| `2026-10-01-plugins-screen-design.md` | Contract: plugin pages and actions | D119/D140 |
| `2026-10-01-tools-design.md` | Contract: system, named and managed tools | D121 |
| `2026-10-01-plugin-distribution-design.md` | Contract: workshop and packages | D120 |
| `2026-10-03-context-menu-design.md` | Contract: context actions and engine boundary | D138 |
| `2026-10-03-plugin-catalogue-design.md` | Contract: offers, icons and monograms | D140 |
| `2026-10-03-language-design.md` | Contract: coded notes and session language | D142 |
| `2026-10-04-plugin-hooks-design.md` | Contract: hook occasions and PR state | D148 |
| `2026-10-04-ux6-redesign.md` | Contract: settings on their things | D150/D152 |
| `2026-10-05-ux7-design.md` | Contract: menus, accounts and page heads | D152 |

## Studies and evidence

These retain their original dates, versions and measurement limits.

| Document | Kind and subject | Used by |
|---|---|---|
| `2026-09-20-working-surface-research.md` | Study: working-surface references | SURF1 |
| `2026-09-21-dsh-evaluation.md` | Evidence: protocol evaluation | D53 |
| `2026-09-21-ide-reference-study.md` | Study: IDE reference | D55 |
| `2026-09-22-acp3-probe-evidence.md` | Evidence: ACP postures | D53/ACP3 |
| `2026-09-22-first-deployment-case-study.md` | Evidence: deployment defects; read for desktop | D60/D63 |
| `2026-09-22-plugin-design-study.md` | Study: extension seams | D64 |
| `2026-09-24-agt2b-channel-evidence.md` | Evidence: vendor channels | D67 |
| `2026-09-24-deploy1-acp-trust-evidence.md` | Evidence: folder trust | D73 |
| `2026-09-24-reference-gap-study.md` | Study: conversation gaps | D76 |
| `2026-09-25-stream-json-evidence.md` | Evidence: native streams | D76 |
| `2026-09-25-message-content-evidence.md` | Evidence: message content | D76 |
| `2026-09-27-first-goal-study.md` | Study: first real workspace | D77 |
| `2026-09-28-console2-streams-evidence.md` | Evidence: ACP background streams | D83/D105 |
| `2026-09-30-console3-native-streams-evidence.md` | Evidence: native background streams | CONSOLE3c |
| `2026-09-28-after-the-first-workspace.md` | Study: workspace, sessions and help gaps | D86–D88/D100/D102 |
| `2026-09-28-managed-edge-evidence.md` | Evidence: managed Edge profiles | D84 |
| `2026-09-28-chromium-embedding-evidence.md` | Evidence: embedding, sign-in and debug port | D85 |
| `2026-10-01-entry-point-evidence.md` | Evidence: instructions read by harnesses | D117; canary remains owner-only |
| `2026-10-01-tools-resources-evidence.md` | Evidence: resource lists and digests | D121 |
| `2026-10-02-knowledge-bench-results.md` | Evidence: headless discovery bench | D128/D129 |
| `2026-10-02-limit-signals-evidence.md` | Evidence: account windows by door | D125/D130 |
| `2026-10-02-ask-drift-evidence.md` | Evidence: requirements lost across runs | D133 |
| `2026-10-03-steer-evidence.md` | Evidence: live steering boundaries | D136 |
| `2026-10-03-knowledge-use-evidence.md` | Evidence: questions and owner-only answers | D135 |
| `DAORIS_FUTURE_DIRECTIONS.md` | Proposal: outside suggestions, preserved | Future-directions review; no implicit adoption |
| `2026-10-03-future-directions-review.md` | Study: proposal weighed against record | ROADMAP horizon |
| `2026-10-05-integration-review.md` | Evidence: earlier recovery and integration | Archive receipts; remaining proof in TASKS |
| `2026-10-07-second-opinion-review.md` | Evidence: code/UI findings | Archive outcomes; remaining work in TASKS |
| `2026-10-07-codex-usage-evidence.md` | Evidence: own-sign-in account windows | D125/D130 |
| `2026-10-09-review-serving-evidence.md` | Evidence: Chromium serving behavior | D154 |

## Records and generated indexes

Read records by identifier; do not read a whole log to find one entry.

| Document | Job and standing |
|---|---|
| `../TASKS.md` | Open work, dependencies, contract and proof |
| `../CHANGELOG.md` | Release-facing changes; workflow stamps headings |
| `DECISIONS.md` | Pointer to decisions; append none here |
| `decisions/` | Numbered decisions and dated amendments, one file each |
| `task-archive.md` | Completed task wording, date and outcome |
| `FIX-LOG.md` | Defect cause, repair and verification |
| `2026-09-25-rev3-review.md` | REV3/CLEAN1 ledger |
| `2026-09-26-ux5-screen-audit.md` | UX5 surface and state ledger |
| `2026-09-28-sess1-session-view.md` | SESS1 session-view ledger |
| `2026-09-29-sess2-session-head.md` | SESS2 header ledger |
| `2026-10-01-naming-audit.md` | NAME1 bilingual naming ledger |
| `2026-10-01-frame-audit.md` | FRAME1 layout ledger |
| `2026-10-10-integration-review.md` | Recovery, review and gate receipts |
| `2026-10-10-documentation-review.md` | DOCSYS1/DOCSYS2 plans, drift findings and verification |
| `adoption/` | Local mechanics draft; adoption has not run |
| `code-map.json` | Generated by devkit `map`; checked by `map --check` |
| `index/` | Generated routes, verbs, catalogues, fixtures, outlines and decision digest |

`node tools/orient-index.mjs` regenerates `index/`; `--check` fails on stale output. The generated
index links hand-kept indexes and does not restate their rows. `.claude/INDEX.md` is generated by
doctrine sync and lists on-demand knowledge and skills. `node tools/doc-system.mjs` checks this
router's coverage; semantic claims still require a source review.
