# COMPANYVIEW1: the makers' reads of the synthesis (2026-10-31)

**Standing:** the makers' reads under D191 of `2026-10-31-company-view-design.md`; the steward's settlement follows them.

## Round 1, Codex's read

1. **Verdict: accept after named changes.** The Agents placement, separate domain sections, and list/diagram views follow the owner's settled direction. No placement round is needed. The blocking changes concern the read contract and editing semantics.

2. **Findings**

- **BLOCKING — `STARTS` cannot supply the promised per-choice eligibility.** `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/AgentsRoutes.cs:285` reads Build's configured adapter and Intake per workspace; its answer has no staffing-choice pass codes. `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/StartPlan.cs:309` stops at the first runnable entry, leaving later choices unjudged. Worse, `counts: false` does not prohibit account probes: `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/HarnessRoster.cs:1556` can request another sign-in reading, executed at line 1368. **Change:** specify a cached eligibility projection with work/place/model context, reading time and an explicit unknown/unexamined state. Separate it from actual start selection. Test signed-out accounts overdue for rereading, later choices and scoped roles; opening must execute no probes or reservations.
- **BLOCKING — effective policies are required even where nothing is overridden.** §4 limits `WorkPolicies.Of` to places setting their own rule, while §3.3 promises the filtered scope's effective rule and source. `src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/WorkPolicy.cs:94` resolves repository/workspace inheritance; line 110 additionally resolves machine/default brainstorm policy. **Change:** resolve every requested work/place pair in .NET, including inherited and default answers. Include typed requirements, provenance and per-item failures. Prove inherited policies without local overrides.
- **BLOCKING — *Off* and *Clear* need distinct seeded/added contracts.** §3.1 assigns both to `SET_ACTOR`, but `src/Daoris.Desktop/Daoris.Desktop.Driver/Settings/ActorKey.cs:58` rejects added role IDs. `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleDoor.cs:86` treats null choices as preservation, not clearing. **Change:** keep seeded staffing reset through `SET_ACTOR`; send an added role's permitted *Off* through `SET_ROLE` with `choices: []`. Offer no invented "as before" state for added roles. Distinguish staffing *Clear* from catalogue *Back to the default*.
- **BLOCKING — plugin and integration coverage is not assigned to a buildable row.** §2 promises workflow/plugin lines, but §3.5 and row d omit plugin presentation and integration declarations. These are separate structural facts: `src/Daoris.Web/src/workflow/current.ts:109` carries integration independently, and `src/Daoris.Web/src/settings/repository/IntegrationDeclaration.tsx:18` binds its separate acceptance to the displayed blob. **Change:** name their section, summary, reads and precise owner doors in row d, or explicitly justify their exclusion against the brief. Retain existing acceptance ownership.
- **Not BLOCKING — preserve older-host behavior explicitly.** `src/Daoris.Web/src/bridge/driver.ts:544` permits no role catalogue from an older shell; today `src/Daoris.Web/src/settings/AiDomain.tsx:194` independently draws staffing controls. **Change:** row a must retain one staffing editor when `ACTORS` succeeds but `ROLES` is unavailable, and distinguish unsupported, failed and empty reads.

The synthesis's two remaining staffing uncertainties can be closed: seeded `SET_ROLE` reaches `RoleStaffing.Set` through `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleDoor.cs:64`, which applies `ActorChoices.With` and its legacy-key write-through. Fenced agents absent from staffing receive null model/effort and `rule-default` at `src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/ActorChoices.cs:83`.

3. **Rejections of my proposal**

Adequate: placement is settled; fewer diagram node kinds, inheritance highlighting and avoiding duplicated editors are coherent simplifications. Replacing the checklist with actionable gaps plus Get started is reasonable.

Inadequate: dropping plugin coverage lacks a replacement. "Code nobody sets" also does not answer my proposed **read-only** capability explanation: `src/Daoris.Service/Daoris.Service.Mcp/CallTable.cs:95` contains meaningful differences between works. An editor is unnecessary; retaining a reference or explaining its exclusion would suffice.

4. **Rows**

The sequence is broadly sound, but not fully buildable as written:

- **a can start now**, incorporating the editing and fallback amendments.
- **b follows a.**
- **c's contract/tests can start now**; implementation needs the cached-eligibility and effective-policy amendments first.
- **d follows b+c**, with plugin/integration coverage assigned.
- **e follows b**; relocation can proceed while activation remains independently gated.
- **f follows d+e** if appointed nodes must select their corresponding section.
- **g follows d**; consolidate its repeated *Needs a look*/suggestion ownership.
- **h also needs d**, plus the stated steward activation/setup dependencies.

Read-only source review completed; no files changed, no tests run, and no `local/` directory read.

## Round 1, the Opus maker's read

1. **Verdict: accept after named changes.** Two findings block, each touching one row; the rest holds against the code. §4's arithmetic was checked and the synthesis's four unverified points settled.

2. **Findings**

- **F1, BLOCKING (row 1c).** "Each choice's pass from the walk `STARTS` reads" while "probing nothing" cannot hold. `STARTS` walks only `config.Adapter` and `IntakeAdapter` per workspace (`Modules/Bridge/Routes/AgentsRoutes.cs:278-291`). `WiringAsync` calls `ChooseAsync(..., counts: false)` (`Driver/Agents/HarnessRoster.cs:2050`); `Pass` still returns `Ask` for a signed-out account with an old reading (`:1558-1562`), the walk then asks it (`AskOneAsync`, `:1359-1370`), and asks the binary again where the cached report says the agent is absent (`:1283-1288`); the model's offer reading enters only through an `OfferAsk` (`:1341`). **Change:** compose each choice's state from the last readings alone (`AccountReads.Of`, the cool-off states, `AgentOffers.Judge`, `ReportAsync(refresh: false)`), never `ChooseAsync` or `Pass`; keep the "opening probes nothing" test. Whether `STARTS` itself asks a signed-out account when a view opens, against D150 point 5, deserves its own check.
- **F2, BLOCKING (row 1a).** "A home whose `driver.json` is its own: every editor held" removes a door that works: `SET_ROLE` refuses there (`DriverRoutes.cs:390-391`) but `SET_ACTOR` writes the file (`Modules/Bridge/DriverContext.cs:156-162`). **Change:** on an unkept home the seeded rows keep `SET_ACTOR`'s editor; only add, remove and scope are held.
- **F3 (settles point 1).** `SET_ROLE` writes a seeded role's first agent through (`Roles/RoleDoor.cs:60-64` → `staffing.Set` → `ActorChoices.With`, `Roles/RoleStaffing.cs:325-337`, `:454-464`). *Off* works over `SET_ROLE` with `choices: []` (`RoleDoor.cs:86-87`); only *Clear* (`choices` null, `DriverRoutes.cs:350-353`) has no `SET_ROLE` form. **Change:** keep `SET_ACTOR` for *Clear* and the unkept home only.
- **F4 (settles point 3).** `ACROSS_MOST = 3` (`design/flowLayout.ts:104`); down needs 90.5rem; across 36rem (`:256-258`). The half-width story's inner width is exactly 576px = 36rem (`design/storyWidths.tsx:13`, `:18`; `work/frame/ViewMain.tsx:263`), so across fits only at a 10rem node with no padding; a role node naming *Claude Code · sonnet · high* wants more (FLOWCHART1d took 19rem, `flowLayout.ts:253-254`), and at 12rem across needs 42rem. `placementFor` hard-codes `ACROSS_MOST` (`:286`), so the column bound changes `PlacementBounds` (`:273`). **Change:** state the node floor and container; 39rem is the rail; 1f proves across at 80rem only.
- **F5 (settles point 4).** `rule-default` is in the code: `Roles/ActorChoices.cs:7`, `:55`, `Participants/Domain/ReviewerChoice.cs:172`, `Participants/Helpers/HelperChoice.cs:38`, `Loop/Driver.cs:905`.
- **F6.** `design/flowFolds.ts` folds a process (stacks, settled stretches, focus); a workspace fold is new code. Name it in 1f.
- **F7.** The placement changes D150 point 4's Agents place ("its list holds each agent once"); add a D150 point 4 note to 1b.
- **F8.** *work* / 工作 needs a narrow `match` (工作区, 工作流, 工作记录: `glossary.json:504`, `:522`, `:572`); `Segmented` is in `knowledge/KnowledgeModes.tsx`.

Checked and held: `RoleCatalog.cs:151-176`, `:62-63`; `StandingConfirmation.cs:330`, `:336`; `DriverRoutes.cs:367-382`; `bridge/person.ts:233-259`; `settings/AiDomain.tsx:37`; `locales/en/flow.json:2-3`; the nav budget (`glossary.json:22-25`, *任命的角色* exactly 5); every route §2 names, `StartWiringRouteTests`, `.claude/knowledge/bridge-presses.md`, `PLACE_PARTS`, `HelpPlaces`; Setup's sections (`projects/setup/RepositorySetup.tsx:34`).

3. **The rejections.** The tenth place: adequate, the placement being settled. `placementFor`: adequate and correct. The tree by place: the stated reason ("a second list of 29 repositories") describes §3.5 as well; the real reason, a flat catalogue keeping one editor in one section, is good, so reword §6.

4. **The rows.** Buildable in this order with F1 and F2 applied: 1a and 1c now, in parallel; 1b after 1a (with the D150 point 4 note and `RoleCatalog.cs:183`'s sentence); 1e after 1b, dark; 1d after 1b and 1c; 1f after 1d, F4 and F6 settled; 1g after 1d; 1h after STEWARDROLE1d.

## The steward's settlement (2026-10-31)

Both makers accepted after named changes, and every change is written into the design:
- **The read (Codex's first and second, F1).** `ROLE_STRUCTURE` composes each choice's state from the last readings alone, with each reading's time and *not read yet*, never through `ChooseAsync`, `Pass` or `STARTS`; it resolves every requested work and place's rule, inherited and default answers included, with provenance and per-item failures; opening it runs no probe and takes no reservation (§4, row 1c). Whether `STARTS` asks a signed-out account when a view opens is filed as STARTSPROBE1.
- **The doors per press (Codex's third, F2, F3, the older shell).** *Save* and *Off* by `SET_ROLE`; *Clear* by `SET_ACTOR`, a seeded role's only; *Clear* and *Back to the default* worded apart; the unkept home keeps the seeded rows' editor; an older shell keeps one staffing editor and tells unsupported, failed and empty apart (§3.1, row 1a).
- **Plugins, integration declarations, the call table (Codex's fourth and its rejection).** Lines in By repository and Accounts, acceptance staying at the Integration tab (§2, §3.5, row 1d); what a work may call shown read-only (row 1i).
- **The widths (F4, F6).** A 12rem node floor, across at 42rem of the main area, so across at 80rem and the rail at 39 and 25rem; the workspace fold named as new code (§4, row 1f).
- **The records (F5, F7, F8, §6).** `rule-default` cited; D150 point 4's note joins 1b; the *work* row's narrow match; `KnowledgeModes.tsx`; §6's reason for keeping the roles in one catalogue reworded.
- **The order (Codex's rows).** 1f after 1d and 1e; *Needs a look* owned by 1d alone; 1h after 1d too.

Accepted. 1a and 1c start now.
