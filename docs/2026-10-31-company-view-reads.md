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
