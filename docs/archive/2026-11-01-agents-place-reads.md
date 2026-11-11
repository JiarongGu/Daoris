# AGENTSPLACE1: the makers' reads of the synthesis (2026-11-01)

**Standing:** the makers' reads under D191 of `../2026-11-01-agents-place-design.md` as round 1 synthesized it; the steward's settlement follows them, and the design was rewritten to it.

## Codex's read

**Accept with named changes.** The synthesis combines the proposals well: place pages answer local questions, the catalogue preserves the company view, and recipes avoid another precedence layer. The application and admission contracts need strengthening before implementation.

1. **Keep two modes; make the catalogue’s scope and action destinations explicit.**

   Folding Roles into Set-up does not inherently lose what my three-mode structure protected. The synthesis retains a cross-place catalogue, inspector and scoped diagram (`docs/2026-11-01-agents-place-design.md:56`). Those capabilities matter more than a separate mode.

   **Edit §3:** label the catalogue under This machine **All roles**, with searchable scope filters; distinguish it from roles placed machine-wide. Make each per-work row open the same role inspector/editor, including when one role takes several works. Preserve selection, scope and return destination when opening the exact policy, account or session. Explicitly retain **Off**, **Clear staffing**, and **Restore role defaults** as distinct acts.

   These are substantive protections: the current setting door loses the precise destination (`src/Daoris.Web/src/agents/roles/RolesView.tsx:215`), while the accepted role contract distinguishes staffing clearing from catalogue restoration (`docs/archive/2026-10-31-company-view-design.md:90`).

2. **Replace the one-file atomicity claim with recoverable, bounded application.**

   Opus’s argument is reasonable for changes genuinely confined to one file (`docs/archive/2026-11-01-agents-place-proposal-opus.md:170`). It does not cover the synthesis’s selected parts.

   Role application writes staffing and catalogue records separately (`src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleDoor.cs:64`, `:69`). Adoption writes permission rules before publishing, then removes its additions on a reported publication failure (`src/Daoris.Desktop/Daoris.Desktop.Driver/Setup/SetupPress.cs:164`). These effects cannot join one `DriverContext.Change`.

   Moreover, saving a workflow first is harmless only when its id is unused: a new start selects the newest saved version (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/WorkflowRunBindings.cs:124`). `Change` saves and immediately nudges the loop (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/DriverContext.cs:149`).

   Half-completion can therefore mean staffing without its catalogue change, rules without a quest, configuration without adoption, or a published quest whose response was lost. A failed response cannot establish that publication failed.

   **Edit §4, Apply and its outcome paragraph:** enumerate every persistence boundary; retain a durable application receipt recording authorized operations and completed or uncertain outcomes; suspend affected new starts until prerequisites settle; retry publication through proven deduplication or reconciliation. Save a fresh unused workflow id, or disclose and authorize every scope affected by appending to an existing one. Revalidate configuration under its write guard. Recovery must survive restart and must not replay uncertain publication blindly.

   I do not require global rollback. “Configuration applied; adoption pending/uncertain” is sufficient only when durable, precise and recoverable.

3. **Add setup-only admission; queue disclosure alone is insufficient.**

   The synthesis adds disclosure of released queued work (`docs/2026-11-01-agents-place-design.md:91`), but my proposal required a setup-only request to remain setup-only (`docs/archive/2026-11-01-agents-place-proposal-codex.md:144`).

   Today adoption requires repository-wide driving (`src/Daoris.Desktop/Daoris.Desktop.Driver/Setup/SetupPress.cs:69`), and the planner’s participation check admits by repository (`src/Daoris.Desktop/Daoris.Desktop.Driver/WorkOrders/Domain/Planner.cs:1266`). Enabling it can release unrelated work before adoption publishes; later arrivals also escape a preview’s queue list.

   **Edit §4:** separate delivery configuration, adoption, and general driving. Preserve participation by default. Give the identified adoption quest narrowly scoped admission while ordinary driving remains off. Offer **Enable general driving** as an explicit reviewed choice, retaining the queue disclosure for that choice. Prove queued and newly arriving unrelated work stay held through application and restart.

4. **Keep one Gates row and one Confirm, but separate the acceptance choices.**

   Separate records are already protected by §2 (`docs/2026-11-01-agents-place-design.md:38`); the unconditional shared tick in §3 weakens purpose-specific consent.

   The acceptances differ operationally. Changed landed gates stop verification (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/LandedGates.cs:29`); integration continues under its previous accepted blob (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/IntegrationAcceptance.cs:31`). Integration also requires no verification workflow.

   **Edit §3’s Gates bullet and §4’s review:** show the declaration once, with independently selectable **Accept for landed verification** and **Accept for integration**. Name each purpose, blob and resulting standing; preserve independent withdrawal. One Confirm applies the selected acceptances to their existing records. Keep the proposed landed expected-blob check.

5. **Split the largest rows and repair dependencies before retiring surfaces.**

   **Edit §7:** split 1a into effective projection and recipe planning; split 1b into recoverable application and bridge/terminal authorization; split 1g into shared catalogue/inspector and place projections/appointments/diagram.

   Put shared workflow writers and landing validation before planner/application integration. Make 1h depend on 1a/1b because *Change* and Gates use them. Retire the old Workflow tab only after 1h, rather than during 1f (`docs/2026-11-01-agents-place-design.md:149`). Keep translations and contract updates with their owning rows. Extend 1l beyond restart mid-review to interrupted writes and uncertain publication.

   Finally, 1e must replace the eager detail consumer as well as page `PLACES`: today it requests every place regardless of filters (`src/Daoris.Web/src/agents/roles/structureRead.ts:10`). Pagination alone would leave that cost intact.
## The Opus maker's read


Read `docs/2026-11-01-agents-place-design.md` and Codex's proposal on `parent/batch-1031h`, against main's code.

**Verdict: accept, with the thirteen named changes below.** The spine holds: one list of places, one page per place, one plan, review and Apply. What was taken from Codex improves the design and breaks nothing. Changes 1–3 correct errors my own proposal shares.

### 1. Errors kept from my proposal

- **Change 1: two ticks in the Gates row, not one.** D215 point 2 keeps two acceptances apart: landed work verified unasked, and integration's gates. One tick spends one consent on two purposes; Codex was right. In §2 point 4, §3 Gates and §4 step 2: one tick per purpose, each naming what it lets run, shown only where the plan needs it. Both may pin the same blob.
- **Change 2: "one write" is false once roles are in the plan.** `SET_ROLE` writes `roles` and `role_staffing` in `data/desktop.db` (`Roles/RoleStaffing.cs:93`), not `driver.json`.
  - §4: drop *its own roles* from the preset's parts; roles are given on the Roles section.
  - The *Suggested* line gets a batch write, `SET_ROLES`, judged whole in one desktop-store transaction, in place of today's sequence (`SuggestedChoicesCard.tsx:59-73`).
- **Change 3: the mode's English is *Setup*, not *Set-up*.** The glossary's *set-up* is 搭建 (the review environment's step), and its pattern `\bset-ups?\b` matches the hyphenated name; *Setup* is the 配置 term. The button stays *Set up…*; the adoption part stays *Set up for agents*. Amend the *setup* row in the same row: the tab is gone, and Get started's count takes its own word.

### 2. What was taken from Codex

- **Change 4: one editor per role.** As written, the catalogue on This machine gives a role a second editor beside its place's row. A role is edited only on the page of its place. In the catalogue, a role placed elsewhere is read-only, with *Open at \<place\>*; machine-scope roles are edited there. This keeps D150 point 1 and COMPANYVIEW1's one row, one editor.
- **Change 5: the routing region's two gaps.**
  - The landed route's *choice* is set in the routing region; its *acceptance* stays only in the Gates row.
  - A kind and its paths is a workspace's `workflow kind` write, and 1c has no route for it. Add `WORKFLOW_KIND` to 1c, or draw kinds read-only until a row does.
- **Change 6: rename *kind* and give it plumbing.**
  - *Kind of task* (工作类型) is the glossary's, and the routing region on the same page says it. Name it by its question, e.g. *What it builds* (它构建什么), with a glossary row.
  - Show it only while the adoption part is ticked.
  - Its driver half is in no row: `SETUP_PLAN` and `SETUP_PRESS` taking it, the quest body in the canon's project-agnostic words, and the quest-text fixture. Put it in 1a.
- **Change 7: the expected-blob check, first and optional.** The landed route reads the tip at apply time (`WorkflowRoutes.cs:222`); `INTEGRATION_GATES` refuses a blob that moved (`:294`).
  - Make `expected` an optional field: older callers are unchanged, the window always sends it, and `use --landed` prints the blob it accepted.
  - Move it into a first row, **1a0**, which waits on nothing and hardens today's *Accept the gates*.
- **Change 8: queued work, said and holdable.**
  - The plan lists the queued quests by title.
  - With no adoption part, it offers *Hold it after driving* (`SET_HOLD`).
  - With one, a hold would hold the set-up quest too, so the plan says that queued work starts with it. Admitting the set-up quest alone (Codex's narrow admission) goes to §8's open list.
- **Change 9: scope `ROLE_STRUCTURE` too.** Codex's risk is `ROLE_STRUCTURE` asking every place (`structureRead.ts:9-17`); paging the list alone leaves that whole.
  - `PLACES` returns every driven place, plus counts for the not-driven fold, whose members are read a page at a time with the name search.
  - *Needs a look* is judged in the driver over all places, from configuration alone (no git, no probe), so the filter stays whole.
  - `ROLE_STRUCTURE` is asked for the chosen place's chain (place, workspace, machine); the catalogue asks for the catalogue alone.

### 3. Names

*Roles* for the system and each page's staffing section reads right: the section answers "who takes each work here". With change 3, *Setup (配置)* reads right for the mode.

### 4. Rows

Twelve is about right; the order needs four edits.

- **Change 10: no removal before its replacement.** 1e builds the mode dark, or keeps the old Roles sections reachable, until 1g merges; otherwise a build between them cannot edit a role. 1f removes only the Setup tab; Workflow leaves in 1h.
- **Change 11: roles first.** 1g waits on 1e alone; the suggestion as one plan, with `SET_ROLES`, becomes **1g2**, after 1b. The worst surface is fixed earliest.
- **Change 12: shrink 1i.** The workflow design's §6.3 editor is for *steps* versions, but the presets that matter, *integrated* and *pull-request*, are graphs (`WorkflowPresets.cs:57-80`).
  - 1i edits a graph version's node options in a drawer, and adds or removes a second opinion and your look by `WithGraphGates`' rule, with the list editor.
  - Adding nodes and wires (Codex's *Connect*, *Set end*) waits for its own design.
- **Change 13: each decision note goes with the row that makes the change** (the integration design §17.11):
  - D194 with 1c;
  - D150 points 3–4 with 1f;
  - COMPANYVIEW1 §1 and §3 with 1g;
  - D157 §6.1 with 1h.
  - 1k keeps Ask Daoris, the goldens and the glossary.

Order: 1a0, 1a, 1b, 1c, 1d, 1e, 1g, 1f, 1h, 1j, 1g2, 1i, 1k, 1l.

## The steward's settlement (2026-11-01)

Both makers accept with named changes, and the steward takes them as follows. The design is rewritten to them.

**Taken from both:**
- **The Gates row offers two choices, *Accept for landed verification* and *Accept for integration*.** Each names its purpose, blob and resulting standing, and each keeps its own record and withdrawal. One Confirm applies the ones selected. The two acceptances differ in operation: changed landed gates stop verification (`LandedGates.cs:29`), while integration goes on under its last accepted blob (`IntegrationAcceptance.cs:31`).
- **Apply is not one write.** Roles write the desktop's store (`RoleDoor.cs:64`, `:69`), and adoption writes rules before it publishes (`SetupPress.cs:164`). Roles leave the preset's parts. The *Suggested* line becomes a batch `SET_ROLES`, judged whole in one desktop-store transaction (Opus). The plan's other effects are recorded in a durable application receipt (Codex):
  - a home entry under `data/`;
  - the authorized operations, each with its outcome done, failed or uncertain;
  - written before the first effect;
  - read again at a restart.
  - The adoption quest is published with the plan's digest as its key, so a retry after an uncertain publication finds the one already published rather than making a second.
  - A named workflow is saved under a fresh id, or the scopes that appending to an existing one reaches are said and authorized (`WorkflowRunBindings.cs:124`).
  - The configuration is judged again under its write guard.
- **The removal of an old surface waits for its replacement.**
  - The Setup mode stays dark, or the old Roles sections stay reachable, until Roles on a place merges.
  - Repositories' Setup tab goes with the page head row; its Workflow tab goes with Workflow on a place.
- **Reads are scoped.** `PLACES` returns every driven place plus counts for the not-driven fold, a page at a time. *Needs a look* is judged in the driver from the configuration alone. `ROLE_STRUCTURE` is asked for the chosen place's chain alone, which replaces the eager consumer (`structureRead.ts:10`).

**Taken from Codex:**
- **Setup-only admission.** Delivery configuration, adoption and general driving are three parts.
  - The adoption quest is admitted alone, by its quest, while general driving stays as it was. Today adoption requires driving (`SetupPress.cs:69`), and the planner admits by repository (`Planner.cs:1266`).
  - *Enable general driving* is a separate choice. When it is chosen, the plan lists the queued work it releases, by title, and offers *Hold it after driving* (Opus).
  - The proof: queued and newly arriving unrelated work stays held through apply and restart.
- **The catalogue is labelled *All roles*,** with scope filters, and distinguished from the roles placed machine-wide. *Off*, *Clear staffing* and *Restore role defaults* stay three acts. Opening a policy, account or session keeps selection, scope and the return.
- **The rows are split,** and the shared writers come before the planner. Workflow on a place depends on the plan and the apply. The installed proof includes an interrupted write and an uncertain publication.

**Taken from the Opus maker:**
- **The mode is *Setup* (配置), not *Set-up*.** The glossary's *set-up* is 搭建, the review environment's step, and its pattern matches the hyphenated name. The mode's row amends that glossary entry.
- **One editor per role.** A role is edited on its own place's page. In *All roles*, one placed elsewhere is read-only, with *Open at \<place\>*.
- **The routing region's kinds stay read-only until `WORKFLOW_KIND` lands in the workflow writes row.** The landed route is chosen in the routing region and accepted in the Gates row.
- **The repository-kind choice is *What it builds*,** not *kind*, which the glossary keeps for the kind of task (工作类型). It shows only while the adoption part is ticked. Its driver half (the adoption quest's body, `SETUP_PLAN`/`SETUP_PRESS` and their fixture) is in the plan row.
- **The expected-blob check on `WORKFLOW_LANDED`** is an optional field, so older callers are unchanged. It is the first row, which waits on nothing and also hardens today's *Accept the gates*.
- **The step editor is shrunk.** It edits a graph version's node options, and adds or removes the second opinion and your look by `WithGraphGates`' rule. Adding nodes and wires needs its own design.
- **Each decision note goes with the row that makes its change.**

**Rows, in order:** 1a0, 1c, 1d, 1a, 1a2, 1b, 1b2, 1b3, 1e, 1g, 1g1, 1f, 1h, 1j, 1g2, 1i, 1k, 1l (the design's §7).

**Accepted 2026-11-01, as rewritten.** The open questions stay open in the design's §8.
