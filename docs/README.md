# Documentation router

Find a document here, then read the relevant section. A **contract** states what a part must keep;
a **method** describes how to build it; **studies** inform decisions; **evidence** records a dated
observation; **records** hold history; a **proposal** needs a decision before adoption. A **design**
runs through brief, proposals, reads and the synthesis (D191); a row says which of them it is and whether it is
accepted or proposed. Its banner holds the rounds, the findings and the backlog rows.

Later decisions override earlier contracts. A contract's baseline is historical; implementation
and amendments live in its numbered decision's dated notes. `TASKS.md` holds remaining work.
This router points to those homes rather than maintaining another build-status list (D127/D159).
Paths below are relative to this file. Historical documents have their own [archive router](archive/README.md).

## Guides

| Document | Purpose
|---|---|
| `../README.md` | Landing page on GitHub and npm (D211); links absolute
| `guide/README.md` | Reference behind the README: commands, manifest, layers, seams
| `../llms.txt`, `../context7.json` | For coding agents (D211); rules held equal to README's
| `assets/` | Package icon, its SVG, the README logo
| `../AGENTS.md` | Shared brief and generated doctrine
| `../CLAUDE.md` | Generated import of AGENTS
| `development.md` | Contributor setup, checks, integration, documentation upkeep
| `../ROADMAP.md` | Forward sequence; TASKS owns acceptance
| `../src/Daoris.Desktop/README.md` | Desktop installation, machine bridge, instruments
| `../src/Daoris.Service/README.md` | Knowledge, quests, hosts, trust, configuration
| `../src/Daoris.Web/README.md` | Platform views, frontend conventions, test loop
| `../src/Daoris.Devkit/README.md` | Universal gates, code map, distribution
| `../examples/README.md` | Adoption and routing; family rehearsal
| `../examples/engine/README.md` | Example engine adopter
| `../examples/game/README.md` | Example game adopter
| `../examples/plugins/hold-by-title/README.md` | Family rehearsal plugin fixture
| `../examples/plugins/browser/README.md` | Standalone browser plugin example
| `../examples/plugins/in-app-browser/README.md` | Browser connection plugin example
| `../examples/plugins/github-pull-request/README.md` | GitHub landing plugin example
| `../examples/plugins/azure-devops-pull-request/README.md` | Azure DevOps landing plugin example
| `../examples/plugins/local-preview/README.md` | Performed step plugin example
| `../tools/dsh-probes/README.md` | Dated protocol probe instructions

## Doctrine, knowledge and development contracts

| Document | Kind and subject
|---|---|
| `2026-08-04-daoris-design.md` | Contract: CLI doctrine lifecycle
| `2026-08-05-knowledge-service-design.md` | Contract: indexing and disclosure
| `2026-09-22-instruction-file-design.md` | Contract: always-loaded instruction region
| `2026-09-23-map-design.md` | Contract: workspace, workflow and code maps
| `2026-09-30-parallel-development-design.md` | Contract: records, splits, lanes
| `2026-10-01-self-development-design.md` | Contract: concurrent work, landing queue
| `2026-10-01-agent-layout-design.md` | Contract: agent layouts, mirrors, rooms
| `2026-10-01-development-documents-design.md` | Contract: roles, safe-work declarations
| `2026-10-01-workspace-setup-design.md` | Contract: setup quests, registration
| `2026-10-02-session-economy-design.md` | Contract: reading cost, record shapes
| `2026-10-02-setup-pilot-lessons-design.md` | Contract: preserve knowledge, green checks
| `2026-10-02-knowledge-design-review.md` | Contract: discovery floor, recall
| `2026-10-03-decisions-record-design.md` | Contract: per-decision files, checks
| `2026-10-04-orientation-everywhere-design.md` | Contract: repository indexes, source ranges
| `2026-10-24-ci-scope-design.md` | Accepted design (D207): modular CI by reach
| `2026-10-24-ci-scope-reads.md` | Reads: CISCOPE1 rounds 1-5

## Driver, sessions, sharing and landing contracts

| Document | Kind and subject
|---|---|
| `2026-09-19-driver-design.md` | Contract: planning, session lifecycle
| `2026-09-20-workspace-design.md` | Contract: workspace membership, sharing
| `2026-09-20-interactive-design.md` | Contract: chat, console, managed agents
| `2026-09-20-remote-design.md` | Contract: optional team deployment
| `2026-09-22-toolchain-design.md` | Contract: binaries, accounts, usage
| `2026-09-23-api-key-accounts.md` | Contract: API-key account declarations
| `2026-09-23-intake-design.md` | Contract: asks become repository quests
| `2026-09-23-sync-design.md` | Contract: fetch, rebase, push
| `2026-09-24-permission-scopes-design.md` | Contract: permissions, reading across
| `2026-09-27-ask-and-wait-design.md` | Contract: cross-repository questions, carry-on
| `2026-09-30-machine-log-design.md` | Contract: machine-local measurements
| `2026-09-30-terminal-design.md` | Contract: person's shell in the panel
| `2026-10-01-account-rotation-design.md` | Contract: limits, cool-off, rotation
| `2026-10-02-account-use-design.md` | Contract: participation, capacity
| `2026-10-02-session-management-design.md` | Contract: groups, actions, archive, deletion
| `2026-10-02-answer-continues-design.md` | Contract: resume on the same conversation
| `2026-10-02-pause-and-clean-up-design.md` | Contract: pause, resume, abandon
| `2026-10-03-session-messages-design.md` | Contract: queued words, reopened runs
| `2026-10-03-update-when-idle-design.md` | Contract: staged update, drain, rollback
| `2026-10-03-evidence-design.md` | Contract: completion evidence checks
| `2026-10-04-landing-and-proof-design.md` | Contract: automatic landing, kept proof
| `2026-10-04-built-in-git-design.md` | Contract: branches, history, ref actions
| `2026-10-07-history-clearing-design.md` | Contract: clear local finished history
| `2026-10-08-review-environment-design.md` | Contract: setup, person's review verdict
| `2026-10-08-second-agent-design.md` | Contract: another maker's agent reviews
| `model-roles.md` | Reference: which model does which role; living, dated
| `2026-10-09-person-door-design.md` | Contract: person/driver authorization
| `2026-10-09-workflow-design.md` | Contract: choices, versions, gates
| `2026-10-14-co-working-design.md` | Contract: one session per line; an ask's channel
| `2026-10-16-install-layout-design.md` | Contract: the Daoris folder's layout
| `2026-10-16-terminal-rpc-design.md` | Contract: terminal verbs over a pipe; home lease
| `2026-10-16-fabric-events-design.md` | Contract: events over polling; part C proposed
| `2026-10-19-verification-lanes-design.md` | Proposed design: checks as lanes beside work
| `2026-10-19-ask-daoris-modes-design.md` | Proposed design: Ask Daoris's modes
| `2026-10-19-cross-agent-brainstorming-design.md` | Contract: one question fanned to several makers
| `2026-10-20-message-urgency-design.md` | Accepted design: message urgency levels
| `2026-10-20-session-context-design.md` | Accepted design: session context is Daoris's record
| `2026-10-21-company-concept.md` | Contract (D193): Daoris as a company run by agents
| `2026-10-20-fabric-cutover-design.md` | Accepted design: runs bound to a graph version
| `2026-10-20-pipeline-design.md` | Proposed design: start node routes into pipelines
| `2026-10-21-alone-path-design.md` | Proposed design: driver verbs run alone or via Daoris
| `2026-10-21-cleanup-design.md` | Accepted design: one keep rule per kind
| `2026-10-21-account-pool-design.md` | Proposed design: one account pool for all workspaces
| `2026-10-22-coagent-design.md` | Accepted design: agents of different makers in one session
| `2026-10-22-git-auth-design.md` | Accepted design: Daoris's own git identities and access
| `2026-10-23-session-watch-design.md` | Accepted design: no limit on a working session; a watcher
| `2026-10-23-start-path-design.md` | Reading and proposal: the desktop's start timed
| `2026-10-23-terminal-trust-design.md` | Accepted design: standing confirmation for a terminal
| `2026-10-23-coworker-brief.md` | Brief: COWORKUX1 co-workers
| `2026-10-23-coworker-proposals.md` | Proposals: COWORKUX1, sealed
| `2026-10-23-coworker-reads.md` | Reads: COWORKUX1 rounds 1-2
| `2026-10-23-coworker-design.md` | Proposed design: a session's co-workers (D203)
| `2026-10-23-limit-goes-on-design.md` | Design note, built: a limited conversation goes on
| `2026-10-23-git-app-brief.md` | Brief: GITAPP1 git app and fast Review
| `2026-10-23-git-app-proposals.md` | Proposals: GITAPP1, sealed
| `2026-10-23-git-app-design.md` | Proposed design: Review as a parallel git graph (D202)
| `2026-10-23-git-writes-design.md` | Proposed design: the person's git writes (D204)
| `2026-10-23-git-app-reads.md` | Reads: GITAPP1 rounds 1-4
| `2026-10-23-actors-brief.md` | Brief: MODELROLE1 actors
| `2026-10-23-actors-proposals.md` | Proposals: MODELROLE1, sealed
| `2026-10-23-actors-reads.md` | Reads: MODELROLE1 rounds 1-3
| `2026-10-23-actors-design.md` | Proposed design: actors, each an ordered range (D205)
| `2026-10-23-limit-resume-design.md` | Accepted design: limit-cut work resumes (D206)
| `2026-10-23-limit-resume-reads.md` | Reads: LIMITRESUME1 rounds 1-2
| `2026-10-24-agent-errors-design.md` | Accepted design: one error policy per agent (D210)
| `2026-10-24-agent-errors-reads.md` | Reads: AGENTERRORS1 rounds 1-3
| `2026-10-24-supervisor-design.md` | Accepted design: appointed roles; the supervisor (D208)
| `2026-10-24-steward-role-design.md` | Accepted design: the steward role (D209)
| `2026-10-24-steward-role-reads.md` | Reads: STEWARDROLE1 rounds 1-3
| `2026-10-24-supervisor-reads.md` | Reads: SUPERVISOR1 rounds 1-4
| `2026-10-24-keep-size-design.md` | Accepted design: the install's size kept (D195 note)
| `2026-10-24-keep-size-reads.md` | Reads: KEEPSIZE1 rounds 2-3
| `2026-10-24-confirm-wait-design.md` | Accepted design: a press waits for the person's Confirm
| `2026-10-24-confirm-wait-reads.md` | Reads: CONFIRMWAIT1 rounds 1-2
| `2026-10-24-cowork-view-design.md` | Row a accepted design: subagents in *At work*; routing half proposed
| `2026-10-24-chat-flow-design.md` | Accepted design: a chat in its own tree runs the workflow
| `2026-10-24-cowork-view-reads.md` | Reads: COWORKVIEW1 rounds 1-10
| `2026-10-24-chat-flow-reads.md` | Reads: CHATFLOW1 rounds 1-2 and Codex's
| `2026-10-24-event-hook-design.md` | Accepted design, rows 1a-1c: events pushed to an outside agent
| `2026-10-24-event-hook-reads.md` | Reads: EVENTHOOK1 rounds 1-4
| `2026-10-24-switch-context-design.md` | Proposed design: a move keeps the agent's context
| `2026-10-24-switch-context-reads.md` | Reads: SWITCHCTX1 rounds 1-5
| `2026-10-25-foundation-survey.md` | Evidence: FOUNDATION1 structure by file:line
| `2026-10-25-foundation-design.md` | Accepted design (D212): Part A building, Part B accepted
| `2026-10-25-foundation-reads.md` | Reads: FOUNDATION1 rounds 1-8
| `2026-10-25-foundation-brief.md` | Brief: FOUNDATION1 one foundation
| `2026-10-25-foundation-proposal-opus.md` | Proposal: FOUNDATION1, Opus, sealed
| `2026-10-25-foundation-proposal-claude.md` | Proposal: FOUNDATION1, Claude, sealed
| `2026-10-25-foundation-proposal-codex.md` | Proposal: FOUNDATION1, Codex, sealed
| `2026-10-26-agent-provider-survey.md` | Evidence: AGENTPROVIDER1 sites by file:line
| `2026-10-26-agent-provider-brief.md` | Brief: AGENTPROVIDER1 one provider contract
| `2026-10-26-agent-provider-proposal-codex.md` | Proposal: AGENTPROVIDER1, Codex, sealed
| `2026-10-26-agent-provider-proposal-opus.md` | Proposal: AGENTPROVIDER1, Opus, sealed
| `2026-10-26-agent-provider-design.md` | Accepted design: the provider contract (D213)
| `2026-10-26-agent-provider-reads.md` | Reads: AGENTPROVIDER1 rounds 1-2
| `2026-10-27-provider-infrastructure-design.md` | Accepted D213 amendment: provider infrastructure (AGENTINFRA1)
| `2026-10-26-tree-pool-brief.md` | Brief: TREEPOOL1 a pool of session trees
| `2026-10-26-tree-pool-design.md` | Proposed design: the tree pool (D214)
| `2026-10-26-tree-pool-reads.md` | Evidence: TREEPOOL1 round 1 reads
| `2026-10-26-tree-pool-proposal-codex.md` | Proposal: TREEPOOL1, Codex, sealed
| `2026-10-26-tree-pool-proposal-opus.md` | Proposal: TREEPOOL1, Opus, sealed
| `2026-10-26-tree-pool-survey.md` | Evidence: TREEPOOL1 how a tree is held today
| `2026-10-28-integration-design.md` | Accepted design, round 5: one integration run per line (D215)
| `2026-10-30-agent-lyntai-survey.md` | Evidence: AGENTLYNTAI1 against Lyntai's layer
| `2026-10-30-agent-lyntai-brief.md` | Brief: AGENTLYNTAI1 providers on Lyntai's layer
| `2026-10-30-agent-lyntai-proposal-opus.md` | Proposal: AGENTLYNTAI1, Opus, sealed
| `2026-10-30-agent-lyntai-proposal-codex.md` | Proposal: AGENTLYNTAI1, Codex, sealed
| `2026-10-30-agent-lyntai-design.md` | Accepted design, round 5: providers on Lyntai's layer
| `2026-10-30-agent-lyntai-reads.md` | Reads: AGENTLYNTAI1 rounds 1-5
| `2026-10-30-memory-currency-brief.md` | Brief: KNOWCENTRAL1 memory kept current
| `2026-10-30-memory-currency-proposal-codex.md` | Proposal: KNOWCENTRAL1, Codex, sealed
| `2026-10-30-knowledge-freshness-design.md` | Accepted design, round 3: search hits say their commit
| `2026-10-31-memory-coordinator-brief.md` | Brief: KNOWCENTRAL1 round 2, Daoris coordinating Lyntai's memory
| `2026-10-31-memory-coordinator-proposal-opus.md` | Proposal: KNOWCENTRAL1 round 2, Opus, sealed
| `2026-10-31-memory-coordinator-proposal-codex.md` | Proposal: KNOWCENTRAL1 round 2, Codex, sealed
| `2026-10-31-memory-coordinator-design.md` | Synthesis, revised after round 2's reads: KNOWCENTRAL1, the memory coordinator
| `2026-10-31-memory-coordinator-reads.md` | Reads: KNOWCENTRAL1 round 2's synthesis

## UI, browser, tools and plugin contracts

| Document | Kind and subject
|---|---|
| `2026-09-19-platform-design.md` | Contract: shared platform
| `2026-09-19-platform-ux.md` | Contract: design language; read for UI changes
| `2026-09-19-frontend-architecture.md` | Method: stack, test boundaries
| `2026-09-21-working-surface-design.md` | Contract: session-oriented working surface
| `2026-09-21-working-surface-components.md` | Method: stories, hook-free molecules
| `2026-09-21-desktop-frame-design.md` | Contract: window and frame
| `2026-09-23-plugin-design.md` | Contract: declarations, wire, plugin kit
| `2026-09-24-menus-design.md` | Contract: domain menus, workspace naming
| `2026-09-27-in-app-browser-design.md` | Contract: browser seam, agent tools
| `2026-09-28-chromium-host-design.md` | Contract: shipped Chromium
| `2026-09-29-ask-daoris-design.md` | Contract: help conversations, proposals
| `2026-09-29-dock-design.md` | Contract: movable panels, Quick Ask
| `2026-09-30-setup-guide-design.md` | Contract: first-start guidance
| `2026-10-01-naming-design.md` | Contract: bilingual names, glossary
| `2026-10-01-frame-model-design.md` | Contract: each view's list and main panes
| `2026-10-01-plugins-screen-design.md` | Contract: plugin pages, actions
| `2026-10-01-tools-design.md` | Contract: system, named, managed tools
| `2026-10-01-plugin-distribution-design.md` | Contract: workshop, packages
| `2026-10-03-context-menu-design.md` | Contract: context actions, engine boundary
| `2026-10-03-plugin-catalogue-design.md` | Contract: offers, icons, monograms
| `2026-10-03-language-design.md` | Contract: coded notes, session language
| `2026-10-04-plugin-hooks-design.md` | Contract: hook occasions, PR state
| `2026-10-04-ux6-redesign.md` | Contract: settings on their things
| `2026-10-05-ux7-design.md` | Contract: menus, accounts, page heads
| `2026-10-16-this-daoris-design.md` | Contract: Settings → This Daoris
| `2026-10-20-flowchart-design.md` | Accepted contract: processes drawn as flowcharts
| `2026-10-20-ui-ux-design.md` | Accepted contract: every screen audited; shared rules
| `2026-10-21-notification-design.md` | Accepted design: one view for all notifications
| `2026-10-22-park-ask-design.md` | Settled, built: a parked session's ask

## Studies and evidence

These retain their original dates, versions and measurement limits.

| Document | Kind and subject
|---|---|
| `2026-09-20-working-surface-research.md` | Study: working-surface references
| `2026-09-21-dsh-evaluation.md` | Evidence: protocol evaluation
| `2026-09-21-ide-reference-study.md` | Study: IDE reference
| `2026-09-22-acp3-probe-evidence.md` | Evidence: ACP postures
| `2026-09-22-first-deployment-case-study.md` | Evidence: deployment defects; read for desktop
| `2026-09-22-plugin-design-study.md` | Study: extension seams
| `2026-09-24-agt2b-channel-evidence.md` | Evidence: vendor channels
| `2026-09-24-deploy1-acp-trust-evidence.md` | Evidence: folder trust
| `2026-09-24-reference-gap-study.md` | Study: conversation gaps
| `2026-09-25-stream-json-evidence.md` | Evidence: native streams
| `2026-09-25-message-content-evidence.md` | Evidence: message content
| `2026-09-27-first-goal-study.md` | Study: first real workspace
| `2026-09-28-console2-streams-evidence.md` | Evidence: ACP background streams
| `2026-09-30-console3-native-streams-evidence.md` | Evidence: native background streams
| `2026-09-28-after-the-first-workspace.md` | Study: workspace, sessions, help gaps
| `2026-09-28-managed-edge-evidence.md` | Evidence: managed Edge profiles
| `2026-09-28-chromium-embedding-evidence.md` | Evidence: embedding, sign-in, debug port
| `2026-10-01-entry-point-evidence.md` | Evidence: instructions read by harnesses
| `2026-10-01-tools-resources-evidence.md` | Evidence: resource lists, digests
| `2026-10-02-knowledge-bench-results.md` | Evidence: headless discovery bench
| `2026-10-02-limit-signals-evidence.md` | Evidence: account windows by door
| `2026-10-02-ask-drift-evidence.md` | Evidence: requirements lost across runs
| `2026-10-03-steer-evidence.md` | Evidence: live steering boundaries
| `2026-10-03-knowledge-use-evidence.md` | Evidence: questions, owner-only answers
| `DAORIS_UNIFIED_COLLABORATION_ARCHITECTURE_AND_PLAN_2026-10-16.md` | Proposal, preserved: unified collaboration plan; no implicit adoption
| `2026-10-03-future-directions-review.md` | Study: proposal weighed against record
| `2026-10-05-integration-review.md` | Evidence: earlier recovery and integration
| `2026-10-07-second-opinion-review.md` | Evidence: code and UI findings
| `2026-10-07-codex-usage-evidence.md` | Evidence: own-sign-in account windows
| `2026-10-09-review-serving-evidence.md` | Evidence: Chromium serving
| `2026-10-10-subagent-load-evidence.md` | Evidence: dispatched subagents' context cost
| `2026-10-11-entry1-audit.md` | Evidence: what Ask Daoris can start
| `2026-10-12-entry-and-heads-design.md` | Contract: New conversation from a prompt; bounded heads
| `2026-10-12-mixed-agents-design.md` | Contract: task handed to another agent; helpers
| `2026-10-12-account-switch-design.md` | Contract: account use, switching curve
| `2026-10-12-usage-read-evidence.md` | Evidence: plan usage read with no prompt
| `2026-10-12-code-structure-design.md` | Contract: domain modules, structure gate
| `2026-10-14-duplication-survey.md` | Evidence: duplicated .NET code by cluster
| `2026-10-14-partial-types-survey.md` | Evidence: hand-split partial types, their gate
| `2026-10-16-fabric-contract-audit.md` | Evidence: each collaboration concept's store and writer
| `2026-10-23-model-effort-doors-evidence.md` | Evidence: what each door accepts for model, effort
| `2026-10-24-modelrole1b-build.md` | Build record: MODELROLE1b
| `2026-10-24-modelrole1c-build.md` | Build record: MODELROLE1c
| `2026-10-24-modelrole1d-build.md` | Build record: MODELROLE1d
| `2026-10-25-modelrole1e-build.md` | Build record: MODELROLE1e
| `2026-10-25-modelrole1d2-build.md` | Build record: MODELROLE1d2

## Records and generated indexes

Read records by identifier; do not read a whole log to find one entry.

| Document | Job
|---|---|
| `../TASKS.md` | Open work, dependencies, contract, proof
| `backlog-parked.md` | Open rows set aside; moved back to `TASKS.md` when taken up
| `../CHANGELOG.md` | Release-facing changes; workflow stamps headings
| `DECISIONS.md` | Pointer to decisions; append none here
| `decisions/` | Numbered decisions, dated amendments, one file each
| `task-archive.md` | Completed tasks: wording, date, outcome
| `FIX-LOG.md` | Defect cause, repair, verification
| `2026-09-25-rev3-review.md` | REV3/CLEAN1 ledger
| `2026-09-26-ux5-screen-audit.md` | UX5 ledger
| `2026-09-28-sess1-session-view.md` | SESS1 ledger
| `2026-09-29-sess2-session-head.md` | SESS2 ledger
| `2026-10-01-naming-audit.md` | NAME1 ledger
| `2026-10-01-frame-audit.md` | FRAME1 ledger
| `2026-10-10-integration-review.md` | Recovery, review, gate receipts
| `2026-10-27-branch-recovery.md` | Branch audit, landing receipts (BRANCHRECOVER1)
| `2026-10-10-documentation-review.md` | DOCSYS1/DOCSYS2 plans, findings
| `2026-10-10-code-and-docs-review.md` | MAINT1 review, findings
| `adoption/` | Local mechanics draft; adoption has not run
| `code-map.json` | Generated by devkit `map`; checked by `map --check`
| `index/` | Generated routes, verbs, catalogues, outlines, decision digest

`node tools/orient-index.mjs` regenerates `index/`; `--check` fails on stale output. The generated
index links hand-kept indexes and does not restate their rows. `.claude/INDEX.md` is generated by
doctrine sync and lists on-demand knowledge and skills. `node tools/doc-system.mjs` checks this
router's coverage; semantic claims still require a source review.
