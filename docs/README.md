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
|`../examples/plugins/daoris-dev-report/README.md`|Development report plugin
|`../examples/plugins/local-preview/README.md`|Performed step plugin
|`../tools/dsh-probes/README.md`|Protocol probes
|`../tools/dev-metrics/budgets.json`|Declared performance budgets, read by development metrics

## Doctrine, knowledge and development contracts

| Document | Kind and subject
|---|---|
|`2026-09-30-parallel-development-design.md`|Contract: records, lanes
|`2026-10-01-self-development-design.md`|Contract: concurrent work, landing queue
|`2026-10-24-ci-scope-design.md`|Accepted design: CI by reach (D207)
|`2026-11-12-long-lived-repository-design.md`|Accepted design: what a change reads, measured (D278)
|`2026-11-14-usage-model-design.md`|Design (proposed): USAGEMODEL1, provider-declared windows, one cost rule that spends lapsing units first, learned spend rates, dated overrules
|`2026-11-13-layers-design.md`|Design (settled, D286): LAYERS1, ports and adapters: the application on ports, each adapter a package pinned by fingerprint, its dependency tests run only when it moves
|`2026-11-13-performance-matrix.md`|Specification: PERFMATRIX1 performance budgets and receipts; RCASE1 installed real-case catalogue

## Driver, sessions, sharing and landing contracts

| Document | Kind and subject
|---|---|
|`2026-09-23-sync-design.md`|Contract: fetch, rebase, push
|`2026-09-30-machine-log-design.md`|Contract: machine measurements
|`model-roles.md`|Reference: model per role; living
|`2026-10-20-session-context-design.md`|Accepted design: session context
|`2026-10-20-pipeline-design.md`|Proposed design: pipelines
|`2026-10-22-git-auth-design.md`|Accepted design: git identities
|`2026-10-23-terminal-trust-design.md`|Accepted design: terminal confirmation
|`2026-10-23-limit-resume-design.md`|Accepted design: limit resume (D206)
|`2026-10-24-agent-errors-design.md`|Accepted design: agent errors (D210)
|`2026-10-24-supervisor-design.md`|Accepted design: supervisor (D208)
|`2026-10-24-confirm-wait-design.md`|Accepted design: press waits for Confirm
|`2026-10-24-cowork-view-design.md`|Design: subagents view; routing proposed
|`2026-10-24-chat-flow-design.md`|Accepted design: chat runs workflow
|`2026-10-24-event-hook-design.md`|Accepted design: events to outside agent
|`2026-10-24-switch-context-design.md`|Proposed design: moves keep context
|`2026-10-26-tree-pool-design.md`|Proposed design: rooms first, with a kept-tree fallback (D214)
|`2026-10-26-tree-pool-reads.md`|Evidence: TREEPOOL1 reads
|`2026-10-28-integration-design.md`|Accepted design: integration run (D215)
|`2026-10-30-agent-lyntai-design.md`|Accepted design: Lyntai providers
|`2026-10-31-memory-coordinator-design.md`|Accepted design: memory coordinator
|`2026-10-31-review-flow-design.md`|Accepted design: review as one flow
|`2026-11-01-agents-place-design.md`|Accepted design: Agents place
|`2026-11-01-land-node-design.md`|Accepted design: land node
|`2026-11-01-fabric2-plan.md`|Plan: FABRIC2 dynamic workflows
|`2026-11-01-setup-ux-design.md`|Accepted design: set-up flow (D221)
|`2026-11-02-flowchart-design.md`|Accepted design: flowchart components (D223)
|`2026-11-02-canvas-design.md`|Accepted design: canvas platform (D224)
|`2026-11-02-staff-policy-design.md`|Accepted design: staffing policy (D226)
|`2026-11-02-comms-design.md`|Accepted design: communication (D225)
|`2026-11-02-parked-triage.md`|Triage: parked backlog against D228
|`2026-11-02-oneway-design.md`|Accepted design: ONEWAY1 (D228)
|`2026-11-03-context-counters-design.md`|Accepted design: CONTEXTDRIVE1 (D229)
|`2026-11-06-context-drive-read.md`|Reads: CONTEXTDRIVE1 against built code
|`2026-11-04-consolidation-plan.md`|Plan: CONSOLIDATE1
|`2026-11-04-codeshape-plan.md`|Plan: CODESHAPE1
|`2026-11-05-backlog-plan.md`|Plan: backlog chunks BL0-BL30
|`2026-11-05-git-design.md`|Proposed design: GITDESIGN1 (D241)
|`2026-11-05-merge-tool-design.md`|Proposed design: MERGETOOL1 (D237)
|`2026-11-06-terminal-conversation-design.md`|Proposed design: RPC1g (D238)
|`2026-11-06-map-ux-design.md`|Proposed design: MAPUX1 (D239)
|`2026-11-10-place-work-design.md`|Proposed design: PLACEWORK1 (D254)
|`2026-11-12-test-diet-census.md`|Census: TESTDIET1, the test code and fixtures measured, with the cut in the order of what it saves
|`2026-11-13-test-timing-census.md`|Census: TESTTIME1, each suite's time and its slowest classes from the gates' logs, their causes, and the cuts by minutes saved
|`2026-11-11-start-load-database-design.md`|Proposed design: STARTPERF1, an index over the session events and a quests' change mark (D275)
|`2026-11-06-event-channel-design.md`|Proposed design: EVENTHOOK1d/e, CHANNELJOIN1 (D244)
|`2026-11-06-event-hook-measurements.md`|Evidence: EVENTHOOK1a measured
|`2026-11-06-node-kit-design.md`|Proposed design: NODEKIT1 (D247)
|`2026-11-06-lyntai-requests.md`|Quest text: needs of Lyntai's agent layer (D248)
|`2026-11-08-supervisor-kinds-design.md`|Proposed design: SUPERVISOR2, supervisors of a kind (D233)
|`2026-11-09-git-library-design.md`|Proposed design: GITLIB1, one managed git in .NET (round 4)
|`2026-11-09-process-steps-design.md`|Proposed design, round 3: PROCSTEPS1, generic process steps; landing a composition
|`2026-11-09-repo-config-design.md`|Proposed design: REPOCONFIG1, a repository's declarations read once and indexed
|`2026-11-11-agent-free-session-evidence.md`|Evidence: AGENTFREE1, a real session's abilities checked from inside
|`2026-11-11-ask-scope-ux-design.md`|Proposed design: ASKSCOPE2 (D253)
|`2026-11-11-one-way-to-ask-design.md`|Proposed design: ASKFULL1g (D253)
|`2026-11-11-ask-daoris-second-check-design.md`|Accepted design: ASKSUPERVISE1, Ask Daoris's acts read by a second model (D272)
|`2026-11-11-grow-department-design.md`|Design (settled, D276): GROWDEPT1, any agent opens a department or room by a set-up and staffs it by quests, within a person-only switch
|`2026-11-11-review-for-every-kind-design.md`|Accepted design: REVIEWUX1, the person's review for every kind of work (D273)
|`2026-11-12-chat-ux-design.md`|Proposed design: CHATUX1 and ASKPUSH1, chatting with Daoris like a modern assistant's desktop app (D279 reserved)
|`2026-11-12-conversation-foundation-design.md`|Design (settled, D280): CONVO1, one conversation under every chat, driven session and Ask Daoris
|`2026-11-12-access-model-design.md`|Design (settled, D284): ACCESS1, Daoris extends every agent; one approval request; workflow steps the only waits
|`2026-11-12-teamwork-design.md`|Design (settled, D285): TEAMWORK1, a large ask built by a layered team: parts as quests, checks capped instead of agents, hand-backs observed, a train through integration
|`2026-11-13-delivery-framework-design.md`|Design (settled, D290): DELIVERY1, an ask across repositories delivered under one plan: evidenced dependencies, a joint receipt before landing, releases waiting on the upstream receipt, done when released

## UI, browser, tools and plugin contracts

| Document | Kind and subject
|---|---|
|`2026-09-19-frontend-architecture.md`|Method: stack, test boundaries
|`2026-09-23-plugin-design.md`|Contract: plugin kit
|`2026-11-11-plugin-ui-design.md`|Accepted design: PLUGINEXT1, a plugin's interfaces, views, commands and sign-in (D274)

## Studies and evidence

Original dates, versions and measurement limits are kept.

| Document | Kind and subject
|---|---|
|`2026-09-21-dsh-evaluation.md`|Evidence: protocol evaluation
|`2026-09-22-first-deployment-case-study.md`|Evidence: deployment defects
|`2026-10-03-steer-evidence.md`|Evidence: steering

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
|`2026-10-27-branch-recovery.md`|Branch audit, landing receipts (BRANCHRECOVER1)
|`adoption/`|Local mechanics draft; adoption has not run
|`code-map.json`|Generated by devkit `map`
|`metrics/`|Dated performance snapshots from `tools/dev-metrics.mjs --json`; receipts by case

`index/` (routes, verbs, catalogues, outlines, the decision digest) is ignored and written per checkout by
`node tools/orient-index.mjs`. `.claude/INDEX.md` is generated by doctrine sync and lists on-demand knowledge
and skills. `node tools/doc-system.mjs` checks this router's coverage; semantic claims need a source review.
