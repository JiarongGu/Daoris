# Documentation router

Find a document here. A **contract** states what a part must keep; a **method** how to build it; **studies**
inform decisions; **evidence** records a dated observation; **records** hold history; a **proposal** needs a
decision before adoption. A **design** runs through brief, proposals, reads and synthesis (D191); its row says
which, and whether accepted or proposed. Its banner holds the rounds, findings and backlog rows.

Later decisions override earlier contracts; amendments live in the numbered decision's dated notes. `TASKS.md`
holds remaining work (D127/D159). Paths are relative to this file. Historical documents have the
[archive router](archive/README.md). In the tables a family's rows share a tag (e.g. FOUNDATION1).

## Guides

| Document | Purpose
|---|---|
|`../README.md`|Landing page (D211)
|`guide/README.md`|Commands, manifest, layers, seams
|`../llms.txt`, `../context7.json`|For coding agents (D211)
|`assets/`|Icon, logo
|`../AGENTS.md`|Shared brief, doctrine
|`../CLAUDE.md`|Generated import of AGENTS
|`development.md`|Contributor setup, checks, upkeep
|`../ROADMAP.md`|Forward sequence
|`../src/Daoris.Desktop/README.md`|Desktop installation, bridge
|`../src/Daoris.Service/README.md`|Knowledge, quests, hosts
|`../src/Daoris.Web/README.md`|Views, conventions, test loop
|`../src/Daoris.Devkit/README.md`|Gates, code map, distribution
|`../examples/README.md`|Adoption, family rehearsal
|`../examples/engine/README.md`|Engine adopter
|`../examples/game/README.md`|Game adopter
|`../examples/plugins/README.md`|Plugin examples
|`../examples/plugins/local-release/README.md`|Release steps plugin
|`../examples/plugins/hold-by-title/README.md`|Rehearsal plugin fixture
|`../examples/plugins/stand-in-system/README.md`|Stand-in system adapter
|`../examples/plugins/browser/README.md`|Browser plugin
|`../examples/plugins/daoris-in-app-browser/README.md`|Browser connection plugin
|`../examples/plugins/daoris-github/README.md`|Daoris's GitHub plugin
|`../examples/plugins/daoris-azure-devops/README.md`|Daoris's Azure DevOps plugin
|`../examples/plugins/local-preview/README.md`|Performed step plugin
|`../tools/dsh-probes/README.md`|Protocol probes

## Doctrine, knowledge and development contracts

| Document | Kind and subject
|---|---|
|`2026-08-04-daoris-design.md`|Contract: CLI doctrine
|`2026-08-05-knowledge-service-design.md`|Contract: indexing, disclosure
|`2026-09-22-instruction-file-design.md`|Contract: instruction region
|`2026-09-23-map-design.md`|Contract: maps
|`2026-09-30-parallel-development-design.md`|Contract: records, lanes
|`2026-10-01-self-development-design.md`|Contract: concurrent work, landing queue
|`2026-10-01-agent-layout-design.md`|Contract: agent layouts
|`2026-10-01-development-documents-design.md`|Contract: roles, safe work
|`2026-10-01-workspace-setup-design.md`|Contract: setup quests
|`2026-10-02-session-economy-design.md`|Contract: reading cost
|`2026-10-02-setup-pilot-lessons-design.md`|Contract: preserving knowledge
|`2026-10-02-knowledge-design-review.md`|Contract: discovery, recall
|`2026-10-03-decisions-record-design.md`|Contract: decision files
|`2026-10-04-orientation-everywhere-design.md`|Contract: indexes, ranges
|`2026-10-24-ci-scope-design.md`|Accepted design: CI by reach (D207)
|`2026-10-24-ci-scope-reads.md`|Reads: CISCOPE1

## Driver, sessions, sharing and landing contracts

| Document | Kind and subject
|---|---|
|`2026-09-19-driver-design.md`|Contract: planning, sessions
|`2026-09-20-workspace-design.md`|Contract: membership, sharing
|`2026-09-20-interactive-design.md`|Contract: chat, console, agents
|`2026-09-20-remote-design.md`|Contract: team deployment
|`2026-09-22-toolchain-design.md`|Contract: binaries, accounts, usage
|`2026-09-23-api-key-accounts.md`|Contract: API-key accounts
|`2026-09-23-intake-design.md`|Contract: asks become quests
|`2026-09-23-sync-design.md`|Contract: fetch, rebase, push
|`2026-09-24-permission-scopes-design.md`|Contract: permissions
|`2026-09-27-ask-and-wait-design.md`|Contract: cross-repository questions
|`2026-09-30-machine-log-design.md`|Contract: machine measurements
|`2026-09-30-terminal-design.md`|Contract: shell panel
|`2026-10-01-account-rotation-design.md`|Contract: limits, rotation
|`2026-10-02-account-use-design.md`|Contract: participation, capacity
|`2026-10-02-session-management-design.md`|Contract: groups, archive, deletion
|`2026-10-02-answer-continues-design.md`|Contract: resume conversation
|`2026-10-02-pause-and-clean-up-design.md`|Contract: pause, abandon
|`2026-10-03-session-messages-design.md`|Contract: queued words
|`2026-10-03-update-when-idle-design.md`|Contract: staged update
|`2026-10-03-evidence-design.md`|Contract: completion evidence
|`2026-10-04-landing-and-proof-design.md`|Contract: landing, proof
|`2026-10-04-built-in-git-design.md`|Contract: branches, ref actions
|`2026-10-07-history-clearing-design.md`|Contract: clear history
|`2026-10-08-review-environment-design.md`|Contract: review setup, verdict
|`2026-10-08-second-agent-design.md`|Contract: second maker reviews
|`model-roles.md`|Reference: model per role; living
|`2026-10-09-person-door-design.md`|Contract: authorization
|`2026-10-09-workflow-design.md`|Contract: choices, versions, gates
|`2026-10-14-co-working-design.md`|Contract: one session per line
|`2026-10-16-install-layout-design.md`|Contract: install layout
|`2026-10-16-terminal-rpc-design.md`|Contract: terminal verbs
|`2026-10-16-fabric-events-design.md`|Contract: events; part C proposed
|`2026-10-19-verification-lanes-design.md`|Proposed design: checks as lanes
|`2026-10-19-ask-daoris-modes-design.md`|Proposed design: Ask Daoris modes
|`2026-10-19-cross-agent-brainstorming-design.md`|Contract: question to several makers
|`2026-10-20-message-urgency-design.md`|Accepted design: urgency
|`2026-10-20-session-context-design.md`|Accepted design: session context
|`2026-10-21-company-concept.md`|Contract: company of agents (D193)
|`2026-10-20-fabric-cutover-design.md`|Accepted design: graph-bound runs
|`2026-10-20-pipeline-design.md`|Proposed design: pipelines
|`2026-10-21-alone-path-design.md`|Proposed design: alone path
|`2026-10-21-cleanup-design.md`|Accepted design: keep rule
|`2026-10-21-account-pool-design.md`|Proposed design: account pool
|`2026-10-22-coagent-design.md`|Accepted design: coagents
|`2026-10-22-git-auth-design.md`|Accepted design: git identities
|`2026-10-23-session-watch-design.md`|Accepted design: session watcher
|`2026-10-23-start-path-design.md`|Reading, proposal: desktop start
|`2026-10-23-terminal-trust-design.md`|Accepted design: terminal confirmation
|`2026-10-23-coworker-brief.md`|Brief: COWORKUX1
|`2026-10-23-coworker-proposals.md`|Proposals: COWORKUX1
|`2026-10-23-coworker-reads.md`|Reads: COWORKUX1
|`2026-10-23-coworker-design.md`|Proposed design: co-workers (D203)
|`2026-10-23-limit-goes-on-design.md`|Design, built: limited conversation goes on
|`2026-10-23-git-app-brief.md`|Brief: GITAPP1
|`2026-10-23-git-app-proposals.md`|Proposals: GITAPP1
|`2026-10-23-git-app-design.md`|Proposed design: Review git graph (D202)
|`2026-10-23-git-writes-design.md`|Accepted design: person's git writes (D204)
|`2026-10-31-git-writes-read-codex.md`|Reads: GITAPP2
|`2026-10-23-git-app-reads.md`|Reads: GITAPP1
|`2026-10-23-actors-brief.md`|Brief: MODELROLE1
|`2026-10-23-actors-proposals.md`|Proposals: MODELROLE1
|`2026-10-23-actors-reads.md`|Reads: MODELROLE1
|`2026-10-23-actors-design.md`|Proposed design: actors (D205)
|`2026-10-23-limit-resume-design.md`|Accepted design: limit resume (D206)
|`2026-10-23-limit-resume-reads.md`|Reads: LIMITRESUME1
|`2026-10-24-agent-errors-design.md`|Accepted design: agent errors (D210)
|`2026-10-24-agent-errors-reads.md`|Reads: AGENTERRORS1
|`2026-10-24-supervisor-design.md`|Accepted design: supervisor (D208)
|`2026-10-24-steward-role-design.md`|Accepted design: steward (D209)
|`2026-10-24-steward-role-reads.md`|Reads: STEWARDROLE1
|`2026-10-24-supervisor-reads.md`|Reads: SUPERVISOR1
|`2026-10-24-keep-size-design.md`|Accepted design: install size
|`2026-10-24-keep-size-reads.md`|Reads: KEEPSIZE1
|`2026-10-24-confirm-wait-design.md`|Accepted design: press waits for Confirm
|`2026-10-24-confirm-wait-reads.md`|Reads: CONFIRMWAIT1
|`2026-10-24-cowork-view-design.md`|Design: subagents view; routing proposed
|`2026-10-24-chat-flow-design.md`|Accepted design: chat runs workflow
|`2026-10-24-cowork-view-reads.md`|Reads: COWORKVIEW1
|`2026-10-24-chat-flow-reads.md`|Reads: CHATFLOW1
|`2026-10-24-event-hook-design.md`|Accepted design: events to outside agent
|`2026-10-24-event-hook-reads.md`|Reads: EVENTHOOK1
|`2026-10-24-switch-context-design.md`|Proposed design: moves keep context
|`2026-10-24-switch-context-reads.md`|Reads: SWITCHCTX1
|`2026-10-25-foundation-survey.md`|Evidence: FOUNDATION1
|`2026-10-25-foundation-design.md`|Accepted design: foundation (D212)
|`2026-10-25-foundation-reads.md`|Reads: FOUNDATION1
|`2026-10-25-foundation-brief.md`|Brief: FOUNDATION1
|`2026-10-25-foundation-proposal-opus.md`|Proposal: FOUNDATION1, Opus
|`2026-10-25-foundation-proposal-claude.md`|Proposal: FOUNDATION1, Claude
|`2026-10-25-foundation-proposal-codex.md`|Proposal: FOUNDATION1, Codex
|`2026-10-26-agent-provider-survey.md`|Evidence: AGENTPROVIDER1
|`2026-10-26-agent-provider-brief.md`|Brief: AGENTPROVIDER1
|`2026-10-26-agent-provider-proposal-codex.md`|Proposal: AGENTPROVIDER1, Codex
|`2026-10-26-agent-provider-proposal-opus.md`|Proposal: AGENTPROVIDER1, Opus
|`2026-10-26-agent-provider-design.md`|Accepted design: provider contract (D213)
|`2026-10-26-agent-provider-reads.md`|Reads: AGENTPROVIDER1
|`2026-10-27-provider-infrastructure-design.md`|Accepted D213 amendment: AGENTINFRA1
|`2026-10-26-tree-pool-brief.md`|Brief: TREEPOOL1
|`2026-10-26-tree-pool-design.md`|Proposed design: rooms first, with a kept-tree fallback (D214)
|`2026-10-26-tree-pool-reads.md`|Evidence: TREEPOOL1 reads
|`2026-10-26-tree-pool-proposal-codex.md`|Proposal: TREEPOOL1, Codex
|`2026-10-26-tree-pool-proposal-opus.md`|Proposal: TREEPOOL1, Opus
|`2026-10-26-tree-pool-survey.md`|Evidence: TREEPOOL1
|`2026-10-28-integration-design.md`|Accepted design: integration run (D215)
|`2026-10-30-agent-lyntai-survey.md`|Evidence: AGENTLYNTAI1
|`2026-10-30-agent-lyntai-brief.md`|Brief: AGENTLYNTAI1
|`2026-10-30-agent-lyntai-proposal-opus.md`|Proposal: AGENTLYNTAI1, Opus
|`2026-10-30-agent-lyntai-proposal-codex.md`|Proposal: AGENTLYNTAI1, Codex
|`2026-10-30-agent-lyntai-design.md`|Accepted design: Lyntai providers
|`2026-10-30-agent-lyntai-reads.md`|Reads: AGENTLYNTAI1
|`2026-10-30-memory-currency-brief.md`|Brief: KNOWCENTRAL1
|`2026-10-30-memory-currency-proposal-codex.md`|Proposal: KNOWCENTRAL1, Codex
|`2026-10-30-knowledge-freshness-design.md`|Accepted design: hits name commit
|`2026-10-31-memory-coordinator-brief.md`|Brief: KNOWCENTRAL1 round 2
|`2026-10-31-memory-coordinator-proposal-opus.md`|Proposal: KNOWCENTRAL1 r2, Opus
|`2026-10-31-memory-coordinator-proposal-codex.md`|Proposal: KNOWCENTRAL1 r2, Codex
|`2026-10-31-memory-coordinator-design.md`|Accepted design: memory coordinator
|`2026-10-31-memory-coordinator-reads.md`|Reads: KNOWCENTRAL1 r2
|`2026-10-31-review-flow-brief.md`|Brief: REVIEWFLOW1
|`2026-10-31-review-flow-proposal-opus.md`|Proposal: REVIEWFLOW1, Opus
|`2026-10-31-review-flow-proposal-codex.md`|Proposal: REVIEWFLOW1, Codex
|`2026-10-31-review-flow-reads.md`|Reads: REVIEWFLOW1
|`2026-10-31-company-view-brief.md`|Brief: COMPANYVIEW1
|`2026-10-31-company-view-proposal-opus.md`|Proposal: COMPANYVIEW1, Opus
|`2026-10-31-company-view-proposal-codex.md`|Proposal: COMPANYVIEW1, Codex
|`2026-10-31-company-view-design.md`|Accepted design: Roles mode
|`2026-10-31-company-view-reads.md`|Reads: COMPANYVIEW1
|`2026-10-31-review-flow-design.md`|Accepted design: review as one flow
|`2026-11-01-profile-fixture-proposal.md`|Proposal: PROFILEFIX1; recommends no
|`2026-11-01-profile-fixture-reads.md`|Reads: PROFILEFIX1; no, today
|`2026-11-01-agents-place-brief.md`|Brief: AGENTSPLACE1
|`2026-11-01-agents-place-proposal-opus.md`|Proposal: AGENTSPLACE1, Opus
|`2026-11-01-agents-place-proposal-codex.md`|Proposal: AGENTSPLACE1, Codex
|`2026-11-01-agents-place-design.md`|Accepted design: Agents place
|`2026-11-01-agents-place-reads.md`|Reads: AGENTSPLACE1
|`2026-11-01-land-node-brief.md`|Brief: LANDNODE1
|`2026-11-01-land-node-proposal-opus.md`|Proposal: LANDNODE1, Opus
|`2026-11-01-land-node-proposal-codex.md`|Proposal: LANDNODE1, Codex
|`2026-11-01-land-node-design.md`|Accepted design: land node
|`2026-11-01-land-node-reads.md`|Reads: LANDNODE1
|`2026-11-01-fabric2-plan.md`|Plan: FABRIC2 dynamic workflows
|`2026-11-01-setup-ux-brief.md`|Brief: SETUPUX1
|`2026-11-01-setup-ux-proposal-opus.md`|Proposal: SETUPUX1, Opus
|`2026-11-01-setup-ux-proposal-codex.md`|Proposal: SETUPUX1, Codex
|`2026-11-01-setup-ux-design.md`|Accepted design: set-up flow (D221)
|`2026-11-01-setup-ux-reads.md`|Reads: SETUPUX1
|`2026-11-02-flowchart-brief.md`|Brief: FLOWCHART2
|`2026-11-02-flowchart-proposal-opus.md`|Proposal: FLOWCHART2, Opus
|`2026-11-02-flowchart-proposal-codex.md`|Proposal: FLOWCHART2, Codex
|`2026-11-02-flowchart-design.md`|Accepted design: flowchart components (D223)
|`2026-11-02-flowchart-reads.md`|Reads: FLOWCHART2
|`2026-11-02-canvas-brief.md`|Brief: CANVAS1
|`2026-11-02-staff-policy-brief.md`|Brief: STAFFPOLICY1
|`2026-11-02-canvas-proposal-opus.md`|Proposal: CANVAS1, Opus
|`2026-11-02-canvas-proposal-codex.md`|Proposal: CANVAS1, Codex
|`2026-11-02-canvas-design.md`|Accepted design: canvas platform (D224)
|`2026-11-02-canvas-reads.md`|Reads: CANVAS1
|`2026-11-02-staff-policy-proposal-opus.md`|Proposal: STAFFPOLICY1, Opus
|`2026-11-02-staff-policy-proposal-codex.md`|Proposal: STAFFPOLICY1, Codex
|`2026-11-02-staff-policy-design.md`|Accepted design: staffing policy (D226)
|`2026-11-02-staff-policy-reads.md`|Reads: STAFFPOLICY1
|`2026-11-02-comms-brief.md`|Brief: COMMS1
|`2026-11-02-comms-proposal-opus.md`|Proposal: COMMS1, Opus
|`2026-11-02-comms-proposal-codex.md`|Proposal: COMMS1, Codex
|`2026-11-02-comms-design.md`|Accepted design: communication (D225)
|`2026-11-02-comms-reads.md`|Reads: COMMS1
|`2026-11-02-review2-findings.md`|Record: REVIEW2 findings; open
|`2026-11-02-workflow-cleanup-inventory.md`|Inventory: WORKFLOWCLEAN1
|`2026-11-02-parked-triage.md`|Triage: parked backlog against D228
|`2026-11-02-oneway-census.md`|Census: ONEWAY1 (D228)
|`2026-11-02-workflow-cleanup-design.md`|Accepted design: WORKFLOWCLEAN1 (D227)
|`2026-11-02-workflow-cleanup-reads.md`|Reads: WORKFLOWCLEAN1
|`2026-11-02-oneway-design.md`|Accepted design: ONEWAY1 (D228)
|`2026-11-03-context-counters-brief.md`|Brief: CONTEXTDRIVE1
|`2026-11-03-context-counters-design.md`|Accepted design: CONTEXTDRIVE1 (D229)
|`2026-11-06-context-drive-read.md`|Reads: CONTEXTDRIVE1 against built code
|`2026-11-03-context-one-design.md`|Proposed design: CONTEXTONE1 (D230)
|`2026-11-04-consolidation-plan.md`|Plan: CONSOLIDATE1
|`2026-11-04-codeshape-plan.md`|Plan: CODESHAPE1
|`2026-11-05-backlog-plan.md`|Plan: backlog chunks BL0-BL30
|`2026-11-05-git-design.md`|Proposed design: GITDESIGN1 (D241)
|`2026-11-05-merge-tool-design.md`|Proposed design: MERGETOOL1 (D237)
|`2026-11-06-terminal-conversation-design.md`|Proposed design: RPC1g (D238)
|`2026-11-06-map-ux-design.md`|Proposed design: MAPUX1 (D239)
|`2026-11-10-isolation-beyond-git-survey.md`|Survey: PLACEWORK1a, parallel work beyond git (D254)
|`2026-11-10-place-work-design.md`|Proposed design: PLACEWORK1 (D254)
|`2026-11-11-jj-workspaces-measurements.md`|Evidence: PLACEWORK1i, jj workspaces measured
|`2026-11-11-start-load-measurements.md`|Evidence: STARTPERF1, a start to a full Sessions and Quests view measured
|`2026-11-11-start-load-database-design.md`|Proposed design: STARTPERF1, an index over the session events and a quests' change mark (D275)
|`2026-11-06-backlog-plan-2.md`|Plan: backlog chunks BL31-BL57
|`2026-11-06-event-channel-design.md`|Proposed design: EVENTHOOK1d/e, CHANNELJOIN1 (D244)
|`2026-11-06-event-hook-measurements.md`|Evidence: EVENTHOOK1a measured
|`2026-11-07-dogfood-measurements.md`|Evidence: DOGFOOD1 rows measured
|`2026-11-11-placework1g-clone-measurements.md`|Evidence: PLACEWORK1g, block clones on a Dev Drive measured
|`2026-11-06-node-kit-design.md`|Proposed design: NODEKIT1 (D247)
|`2026-11-06-lyntai-requests.md`|Quest text: needs of Lyntai's agent layer (D248)
|`2026-11-07-backlog-plan-3.md`|Plan: backlog chunks BL70-BL82
|`2026-11-08-supervisor-kinds-design.md`|Proposed design: SUPERVISOR2, supervisors of a kind (D233)
|`2026-11-08-supervisor-kinds-reads.md`|Reads: SUPERVISOR2
|`2026-11-09-git-library-design.md`|Proposed design: GITLIB1, one managed git in .NET (round 4)
|`2026-11-09-git-library-reads.md`|Reads: GITLIB1
|`2026-11-09-process-steps-design.md`|Proposed design, round 3: PROCSTEPS1, generic process steps; landing a composition
|`2026-11-09-process-steps-reads.md`|Reads: PROCSTEPS1
|`2026-11-09-repo-config-design.md`|Proposed design: REPOCONFIG1, a repository's declarations read once and indexed
|`2026-11-09-repo-config-reads.md`|Reads: REPOCONFIG1
|`2026-11-10-agent-free-census.md`|Census: AGENTFREE1, what a session narrows (D253 point 7)
|`2026-11-11-agent-free-session-evidence.md`|Evidence: AGENTFREE1, a real session's abilities checked from inside
|`2026-11-11-ask-scope-ux-design.md`|Proposed design: ASKSCOPE2 (D253)
|`2026-11-11-one-way-to-ask-design.md`|Proposed design: ASKFULL1g (D253)
|`2026-11-11-ask-daoris-second-check-design.md`|Accepted design: ASKSUPERVISE1, Ask Daoris's acts read by a second model (D272)
|`2026-11-11-review-for-every-kind-design.md`|Accepted design: REVIEWUX1, the person's review for every kind of work (D273)

## UI, browser, tools and plugin contracts

| Document | Kind and subject
|---|---|
|`2026-09-19-platform-design.md`|Contract: shared platform
|`2026-09-19-platform-ux.md`|Contract: design language; read for UI changes
|`2026-09-19-frontend-architecture.md`|Method: stack, test boundaries
|`2026-09-21-working-surface-design.md`|Contract: working surface
|`2026-09-21-working-surface-components.md`|Method: stories, molecules
|`2026-09-21-desktop-frame-design.md`|Contract: window, frame
|`2026-09-23-plugin-design.md`|Contract: plugin kit
|`2026-09-24-menus-design.md`|Contract: menus, naming
|`2026-09-27-in-app-browser-design.md`|Contract: browser seam
|`2026-09-28-chromium-host-design.md`|Contract: shipped Chromium
|`2026-09-29-ask-daoris-design.md`|Contract: help conversations
|`2026-09-29-dock-design.md`|Contract: panels, Quick Ask
|`2026-09-30-setup-guide-design.md`|Contract: first-start guidance
|`2026-10-01-naming-design.md`|Contract: bilingual names
|`2026-10-01-frame-model-design.md`|Contract: list and main panes
|`2026-10-01-plugins-screen-design.md`|Contract: plugin pages
|`2026-10-01-tools-design.md`|Contract: tools
|`2026-10-01-plugin-distribution-design.md`|Contract: workshop, packages
|`2026-10-03-context-menu-design.md`|Contract: context actions
|`2026-10-03-plugin-catalogue-design.md`|Contract: offers, icons
|`2026-10-03-language-design.md`|Contract: coded notes, language
|`2026-10-04-plugin-hooks-design.md`|Contract: hook occasions
|`2026-10-04-ux6-redesign.md`|Contract: settings on their things
|`2026-10-05-ux7-design.md`|Contract: menus, accounts, heads
|`2026-10-16-this-daoris-design.md`|Contract: Settings, This Daoris
|`2026-10-20-flowchart-design.md`|Accepted contract: flowcharts
|`2026-10-20-ui-ux-design.md`|Accepted contract: screen rules
|`2026-10-21-notification-design.md`|Accepted design: notifications
|`2026-10-22-park-ask-design.md`|Built: parked session's ask
|`2026-11-11-plugin-ui-design.md`|Proposed design: PLUGINEXT1, a plugin's own UI, commands and sign-in (D274)

## Studies and evidence

Original dates, versions and measurement limits are kept.

| Document | Kind and subject
|---|---|
|`2026-09-20-working-surface-research.md`|Study: working-surface references
|`2026-09-21-dsh-evaluation.md`|Evidence: protocol evaluation
|`2026-09-21-ide-reference-study.md`|Study: IDE reference
|`2026-09-22-acp3-probe-evidence.md`|Evidence: ACP postures
|`2026-09-22-first-deployment-case-study.md`|Evidence: deployment defects
|`2026-09-22-plugin-design-study.md`|Study: extension seams
|`2026-09-24-agt2b-channel-evidence.md`|Evidence: vendor channels
|`2026-09-24-deploy1-acp-trust-evidence.md`|Evidence: folder trust
|`2026-09-24-reference-gap-study.md`|Study: conversation gaps
|`2026-09-25-stream-json-evidence.md`|Evidence: native streams
|`2026-09-25-message-content-evidence.md`|Evidence: message content
|`2026-09-27-first-goal-study.md`|Study: first workspace
|`2026-09-28-console2-streams-evidence.md`|Evidence: ACP streams
|`2026-09-30-console3-native-streams-evidence.md`|Evidence: native streams
|`2026-09-28-after-the-first-workspace.md`|Study: workspace, help gaps
|`2026-09-28-managed-edge-evidence.md`|Evidence: managed Edge
|`2026-09-28-chromium-embedding-evidence.md`|Evidence: embedding
|`2026-10-01-entry-point-evidence.md`|Evidence: instructions read
|`2026-10-01-tools-resources-evidence.md`|Evidence: resource lists
|`2026-10-02-knowledge-bench-results.md`|Evidence: discovery bench
|`2026-10-02-limit-signals-evidence.md`|Evidence: account windows
|`2026-10-02-ask-drift-evidence.md`|Evidence: lost requirements
|`2026-10-03-steer-evidence.md`|Evidence: steering
|`2026-10-03-knowledge-use-evidence.md`|Evidence: questions
|`DAORIS_UNIFIED_COLLABORATION_ARCHITECTURE_AND_PLAN_2026-10-16.md`|Proposal, preserved; not adopted
|`2026-10-03-future-directions-review.md`|Study: proposal weighed
|`2026-10-05-integration-review.md`|Evidence: recovery, integration
|`2026-10-07-second-opinion-review.md`|Evidence: code, UI findings
|`2026-10-07-codex-usage-evidence.md`|Evidence: account windows
|`2026-10-09-review-serving-evidence.md`|Evidence: Chromium serving
|`2026-10-10-subagent-load-evidence.md`|Evidence: subagent cost
|`2026-10-11-entry1-audit.md`|Evidence: what Ask Daoris starts
|`2026-10-12-entry-and-heads-design.md`|Contract: new conversation, heads
|`2026-10-12-mixed-agents-design.md`|Contract: handed tasks, helpers
|`2026-10-12-account-switch-design.md`|Contract: account switching
|`2026-10-12-usage-read-evidence.md`|Evidence: usage read
|`2026-10-12-code-structure-design.md`|Contract: modules, structure gate
|`2026-10-14-duplication-survey.md`|Evidence: duplicated .NET code
|`2026-10-14-partial-types-survey.md`|Evidence: partial types
|`2026-10-16-fabric-contract-audit.md`|Evidence: concept stores
|`2026-10-23-model-effort-doors-evidence.md`|Evidence: model, effort doors
|`2026-10-24-modelrole1b-build.md`|Build record: MODELROLE1b
|`2026-10-24-modelrole1c-build.md`|Build record: MODELROLE1c
|`2026-10-24-modelrole1d-build.md`|Build record: MODELROLE1d
|`2026-10-25-modelrole1e-build.md`|Build record: MODELROLE1e
|`2026-10-25-modelrole1d2-build.md`|Build record: MODELROLE1d2

## Records and generated indexes

Read records by identifier, not whole.

| Document | Job
|---|---|
|`../TASKS.md`|Open work
|`backlog-parked.md`|Parked rows
|`../CHANGELOG.md`|Release-facing changes
|`DECISIONS.md`|Pointer to decisions
|`areas/README.md`|Areas: current truth, open choices
|`areas/agents-and-harnesses.md`|Area: agents, harnesses
|`areas/co-working-and-messages.md`|Area: co-working, messages
|`areas/code-structure-and-process.md`|Area: code structure, process
|`areas/conversations-and-context.md`|Area: conversations, context
|`areas/desktop-app.md`|Area: desktop app
|`areas/doctrine-and-cli.md`|Area: doctrine, CLI
|`areas/driving.md`|Area: driving
|`areas/git-and-remotes.md`|Area: git, remotes
|`areas/install-home-update.md`|Area: install, home, update
|`areas/knowledge-memory-records.md`|Area: knowledge, memory, records
|`areas/plugins.md`|Area: plugins
|`areas/roles-and-supervision.md`|Area: roles, supervision
|`areas/workflows-review-landing.md`|Area: workflows, review, landing
|`decisions/`|Numbered decisions, read for reasoning
|`decisions/README.md`|Rule of each decision by area, generated
|`task-archive.md`|Completed tasks
|`FIX-LOG.md`|Defect cause, repair
|`2026-09-25-rev3-review.md`|REV3/CLEAN1 ledger
|`2026-09-26-ux5-screen-audit.md`|UX5 ledger
|`2026-09-28-sess1-session-view.md`|SESS1 ledger
|`2026-09-29-sess2-session-head.md`|SESS2 ledger
|`2026-10-01-naming-audit.md`|NAME1 ledger
|`2026-10-01-frame-audit.md`|FRAME1 ledger
|`2026-10-10-integration-review.md`|Recovery, review, gate receipts
|`2026-10-27-branch-recovery.md`|Branch audit, landing receipts (BRANCHRECOVER1)
|`2026-10-10-documentation-review.md`|DOCSYS1/DOCSYS2 plans, findings
|`2026-10-10-code-and-docs-review.md`|MAINT1 review, findings
|`adoption/`|Local mechanics draft; adoption has not run
|`code-map.json`|Generated by devkit `map`

`index/` (routes, verbs, catalogues, outlines, the decision digest) is ignored and written per checkout by
`node tools/orient-index.mjs`. `.claude/INDEX.md` is generated by doctrine sync and lists on-demand knowledge
and skills. `node tools/doc-system.mjs` checks this router's coverage; semantic claims need a source review.
