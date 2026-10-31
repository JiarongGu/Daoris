# COMPANYVIEW1: Codex's sealed proposal (2026-10-31)

**Standing:** a sealed proposal under D191, written from `2026-10-31-company-view-brief.md` alone.

Sealed proposal, based on repository sources only. Nothing changed; no `local/` directory or other proposal was read.

**1. The model — choose one structural view over the existing owners.**

Add **Company / 公司** as a desktop-only Settings domain, with **List / Graph** views. It covers this Daoris, with workspace and repository filters. Move working-role configuration here from AI features; leave a navigation door there. Other things retain their existing editing homes.

The reason is D193’s company structure crosses repositories, while D150 requires each setting to have one home. This rejects both a tenth activity-bar place and a second configuration store.

| Structural thing and relation | Existing home and door; proposed treatment |
|---|---|
| Working role takes work within a scope; staffing supplies ordered agent/model/effort choices | Desktop-store `roles` and `role_staffing`, through `RoleCatalog` and `RoleStaffing`; currently AI features’ `RolesCard` and `ActorsCard`. Read `ROLES`/`ACTORS`; configure here through `SET_ROLE` and the existing staffing door `SET_ACTOR`. One role row combines definition and staffing. |
| Appointed role grants bounded acts to a holder within a scope | `StandingConfirmation`, its appointment record and live authority; steward in This Daoris’s Roles row, supervisor in repository/workspace Setup. Read `STATE`; open those homes for window-only `SET_APPOINTMENT`. A working-role edit grants no authority. |
| Work has a resolved policy and connector capabilities | `WorkPolicies` resolves existing configuration; `Mcp/CallTable.cs` governs connector calls. Show effective policy, its source and limits. Open the existing rule owner: `SET_HELPERS`, `SET_OPINION`, `SET_HAND_OVER`, `SET_ACCESS`, or the appropriate existing setting. Code-owned capability limits are read-only. |
| Agent has accounts available to its account walk | Agents’ product page; `AccountSet` resolves one account set per agent across workspaces. Read `ACCOUNTS`; open Agents for `ACCOUNT_USE`, account settings and sign-in actions. Accounts are not assigned directly to roles. |
| Workspace contains repositories; each repository resolves a line, landing and rules | Registry and existing driver configuration; workspace/repository Setup. Read registry and `LINES`; open Manage or Setup, using existing doors including `SET_LINE` and `SET_LANDING`. |
| Repository selects workflows and accepts integration declarations | Workflow/Setup, plus repository-owned `daoris.gates.json`; Integration shows execution. Read `WORKFLOW_CURRENT` and, when requested, `INTEGRATION`. Keep `WORKFLOW_LANDED` and blob-bound `INTEGRATION_GATES` at their existing owners. |
| Plugin supplies declared capabilities and may be referenced by configuration | Plugins’ own pages. Read `PLUGINS`; open the named plugin for its existing actions and approvals. |

The role move explicitly amends D205’s UI location and updates driver guidance and Ask Daoris navigation in the same row. It changes neither role selection nor appointment authority.

**2. The list — choose an outline of structure, with one selected detail.**

Sections are **Roles**, **Work and policies**, **Repositories**, **Agents and accounts**, and **Plugins**. Roles distinguish working and appointed shapes in words.

Every row shows its name, scope, structural summary, effective source and any problem. A working-role row says which work it takes, where, its first staffing choice and additional-choice count. Expanding shows the complete order. An appointment says whether it is live, dormant or unavailable, its scope and holder where readable. Repository rows show the resolved line, landing and workflow; agent rows summarize account availability with the reading’s time.

Filters cover workspace/repository, section, work, agent and problems. Machine-wide roles remain visible when relevant to a scoped selection, labelled with their actual scope. Search includes authored names and identifiers.

Working roles edit inline, one editor at a time. Other rows open their exact owner and section; returning restores selection and scroll.

“Default,” “Off,” “Removed,” “No role takes this work,” “Unreadable,” and “Not available from this host” remain distinct. Invalid records retain the owner’s sentence and repair door. A failed read never becomes an empty list.

This makes setup gaps scannable. It rejects a dashboard of counts, repeated full editors and one generic “Not configured” state.

**3. The graph — choose a configuration graph with explicit relationship kinds.**

Nodes represent work, working roles, appointed roles and holders, agents, repositories, resolved lines, selected workflows and referenced plugins. Accounts appear only when an agent is expanded.

Draw only relationships whose source can be named:

- Registry membership groups repositories within workspaces.
- Scoped work resolves to a role through `RoleCatalog.RoleFor`.
- A role has numbered staffing choices naming agents; model and effort stay on those choices.
- An agent owns its account set.
- A live appointment binds role, holder and scope.
- A repository resolves to its line, workflow and explicitly referenced plugin.

Policies appear beside the selected work rather than as dozens of permission nodes. Declared relationships and effective resolutions have different labels and strokes.

Never draw reporting hierarchies, inferred responsibility, role-to-account assignments, access from staffing, or “runs on” edges from preferences. An unread target produces a named unresolved reference, not an invented node. Current configuration never describes a running session’s bound role or workflow version.

Start with workspace groups collapsed. Expand one repository or role neighbourhood; show labelled boundary references and hidden-item counts. Use deterministic ordering, readable card text, pan/zoom and relation filters. Selection highlights incident edges. Keyboard selection and a textual relationship list provide equivalent access.

The existing Map continues to show repository topology and exchanges. Workflow graphs retain control flow; Integration retains run state. Company links to each without absorbing their internals.

The reason is legibility and D137’s evidence boundary. This rejects an all-company hairball and drag-to-connect editing, which would conceal scope, ordering and confirmation semantics.

**4. One model, two views — choose one read model and one selection.**

A container owns subscriptions, selection, filters, expansion and edit state. List and graph receive the same typed entities and relationships through props; neither imports the bridge.

Reuse existing queries. Add a narrowly scoped desktop read, **`COMPANY_STRUCTURE`**, for effective role/work/policy relationships that existing answers do not expose. It calls the existing .NET resolvers; it stores nothing, reserves no account and starts no probe. Each relationship carries its source and relevant revision or reading.

Reads remain progressive. Missing or failed partitions are visible; the graph omits relationships it cannot establish. Existing owner pushes and invalidations refresh both views after edits. Editors retain the revisions their existing doors require; success follows the owner’s answer, not an optimistic graph change.

Switching views preserves selection and filters. List suits comparison and repair; graph suits tracing scope, inheritance and shared dependencies. Both open the same editor or owner door.

This rejects separate graph queries, frontend policy resolution and a universal mutation endpoint.

**5. Setup — choose a resumable checklist of existing decisions.**

Company opens an empty installation with the next actionable gap: an available agent/account, usable default roles, repository registration/adoption, then repository rules and workflow choices.

Keep seeded roles and existing staffing unchanged. Offer additions or staffing suggestions explicitly, with their spending implications; never apply `docs/model-roles.md` as a universal preset.

For a repository, open its existing setup plan through `SETUP_PLAN`; the owner’s `SETUP_PRESS` requests its own session to perform adoption. Show inherited rules before offering overrides. Keep the checklist selection when returning from Agents, Manage, Setup or an acceptance. No blanket “Finish setup” press grants unrelated authority.

Once independently offered and appointed, the steward may perform only D209’s permitted acts. Its optional setup authority remains the owner’s unchecked-by-default appointment tick and fixed setup procedure. It cannot appoint roles, change staffing, sign in, widen access, accept integration declarations or delivered work, or publish.

Both appointment flags are currently false. Company must preserve that darkness.

The reason is fewer repeated choices without hiding consequential ones. This rejects automatic appointments, permissive setup presets and a wizard that writes repository files.

**Sources read**

Records included `docs/2026-10-31-company-view-brief.md`, `docs/decisions/D191.md`, `D193.md`, `D205.md`, `D208.md`, `D209.md`, `D212.md`, `D184.md`, `D125.md`, `D206.md`, `D190.md`, `D215.md`; `docs/model-roles.md`; and `docs/2026-09-23-map-design.md`.

Discovery and UI contracts included `.claude/INDEX.md`, `.claude/skills/doc-loader/SKILL.md`, `docs/README.md`, `docs/index/README.md`, the relevant generated outlines, `docs/2026-09-19-platform-ux.md`, `docs/2026-10-01-naming-design.md`, and `docs/2026-10-20-ui-ux-design.md`.

Code included:

- `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/{RoleCatalog,RoleStaffing,RoleVerbs,StartPlan}.cs`, `Participants/Domain/WorkPolicy.cs`, and `Agents/AccountSet.cs`.
- `src/Daoris.Service/Daoris.Service.Mcp/CallTable.cs`.
- `src/Daoris.Desktop/Daoris.Desktop.Modules/Platform/StandingConfirmation.cs` and `Bridge/Routes/{DriverRoutes,PersonRoutes}.cs`.
- Under `src/Daoris.Web/src/`: `agents/{AgentsView,AgentPage}.tsx`, `settings/ai/{Roles,Actors}.tsx`, `settings/{SettingsView,AiDomain}.tsx`, `settings/wiring/{WiringStanding,WiringSteward}.tsx`, `settings/accounts/AccountUse.tsx`, `settings/plugins/PluginSettings.tsx`, `projects/ProjectPage.tsx`, `projects/setup/{RepositorySetup,SupervisorRow}.tsx`, `projects/integration/{IntegrationTab,RepositoryIntegration}.tsx`, `settings/repository/IntegrationDeclaration.tsx`, `workflow/WorkflowTab.tsx`, `map/{MapView,LayeredMap}.tsx`, `map/{topology,wiring}.ts`, `locales/glossary.json`, and `design/storyWidths.tsx`.

**6. The rows — choose incremental delivery with proof at each boundary.**

Every row requires page tests, stories at **80rem, 39rem and 25rem**, in **en/zh and light/dark**, and installed-window proof of its changed behaviour. These are proposed acceptance requirements; no runtime verification was performed here. Reject one final visual-proof row that lets earlier assumptions accumulate.

1. **COMPANYVIEW1a — Read-only role list.** Add the Settings domain using `ROLES`/`ACTORS`. Prove seeded, custom, removed, unreadable and older-host states; navigation and narrow-window layout.
2. **COMPANYVIEW1b — Complete structural reads.** After a, add `COMPANY_STRUCTURE` and existing `STATE`, `ACCOUNTS`, registry, `LINES`, `WORKFLOW_CURRENT` and `PLUGINS` projections. Prove resolver agreement, partial failures, provenance, account privacy and no probes on opening.
3. **COMPANYVIEW1c — One role editor.** After b, move controls through `SET_ROLE`/`SET_ACTOR`; retire their old controls and update navigation. Prove revision refusal, retained drafts, inheritance and unchanged running work.
4. **COMPANYVIEW1d — Graph.** After b, render the same model and selection. Prove every edge’s source, unresolved references, keyboard access and a 29-repository fixture; inspect legibility in the installed window.
5. **COMPANYVIEW1e — Setup continuity.** After c and d, connect existing owner doors and `SETUP_PLAN`/`SETUP_PRESS`. Prove return navigation, inherited settings, refusals and separate integration acceptance.
6. **COMPANYVIEW1f — Offered appointments.** After e and the respective activation proofs: steward after **STEWARDROLE1d**, supervisor after **SUPERVISOR1a-iii**. Use existing `STATE` and window-only `SET_APPOINTMENT`; prove dark builds expose no controls, reappointment grants nothing silently, and revoke/overrule remain effective.