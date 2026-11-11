# SETUPUX1 — Codex's proposal: set up a place, compose its workflow, give its work a role

Status: sealed proposal, 2026-11-01, from `docs/archive/2026-11-01-setup-ux-brief.md`; Codex, read-only, kept as it came.

Sealed proposal for D221. All changes and proofs below are proposed.

The Setup mode should answer three questions on one place page: **How will work move? Who will take it? How will its sessions run?** *Set up…* provides the short route through the choices most repositories need. The workflow editor provides the detailed route without becoming the page’s default form.

This keeps AGENTSPLACE1’s place tree, reviewed setup plan and local ownership, while completing their presentation as one experience (`docs/2026-11-01-agents-place-design.md:26`). The workflow continues to own a unit’s process; the line owns batching, ordering, integration gates and promotion (`docs/2026-11-01-land-node-design.md:41`).

Several present seams explain why assembling the existing pieces is insufficient:

- The lit Setup mode mounts place roles and sessions, while the older catalogue and diagram remain in the other rendering branch (`src/Daoris.Web/src/agents/AgentsView.tsx:164`).
- A workspace’s place currently exposes its session settings; its remaining configuration still lives on the repository surface (`src/Daoris.Web/src/agents/setup/PlaceSessions.tsx:87`, `src/Daoris.Web/src/app/opener.ts:197`).
- The workflow section draws Current and then mounts its editor below the chart, gates and landing settings (`src/Daoris.Web/src/workflow/place/PlaceWorkflowView.tsx:61`). The editor repeats the drawing as a list of fully expanded editing forms (`src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:261`).
- `WORKFLOW_CURRENT` derives live rules even when its start routes name saved workflows. The diagram beneath the selected route therefore needs an explicit source contract (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:47`).
- Appending a version can affect several places’ future starts. Choosing a version currently checks that the version read is still newest; it does not pin a place permanently to that version (`src/Daoris.Web/src/workflow/edit/answers.ts:69`, `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:415`).

**1. Setup is one place page with three sections.**

At a driven repository, Workflow opens first. Roles and Sessions begin folded to useful summaries. A direct door opens the named section and setting. Opening another section does not close one the person opened.

```text
1546 px — place list beside the main area

Agents | Setup          Repository                                      [Set up…] [⋯]
[Search places       ]  Workspace · Driving · Held                    [Driving options…]
[ ] Needs a look
                        ▾ Workflow        Release after you accept · v3 · from workspace
This machine              Default work [selected]   Documentation   Landed work
▾ Workspace               Chosen by Workspace for default work
    Repository            [Change…] [Edit steps]
    Another repository
  ▸ 17 imported,           Start → selected route → selected workflow
    not driven             [vertical chart]
                          ▸ Gates             Integration acceptance needed
                          ▸ Line              main · declared gates · ordered batches
                        ▸ Roles             Build from workspace · 1 role set here
                        ▸ Sessions          Own trees · inherited access · English
```

```text
680 px — the frame’s list is collapsed

[Places]  Agents | Setup
Repository                                           [Set up…] [⋯]
Workspace · Driving · Held
[Driving options…]

▾ Workflow
  Release after you accept · v3
  From Workspace
  [Change…] [Edit steps]

  Start
    Default work → Release after you accept
    Documentation → Documents
    Landed work → Verify the line
  [selected workflow, top to bottom]

  ▸ Gates — integration acceptance needed
  ▸ Line — main · declared gates

▸ Roles — Build from Workspace; 1 set here
▸ Sessions — own trees; inherited access
```

The head leads with the place’s name, followed by at most two relevant facts. Driving options, trees, adoption status and configuration details move into its disclosure. Workflow identity appears in Workflow, rather than again in the head. This follows the existing two-line head and “fact once” contracts (`docs/archive/2026-10-05-ux7-design.md:112`, `docs/archive/2026-10-05-ux7-design.md:533`). It replaces the present definition list of driving, hold, trees, changes and workflow ancestry (`src/Daoris.Web/src/agents/setup/SetupPlaces.tsx:284`).

The place variants are deliberate:

| Place | Opens first | Its page contains |
|---|---|---|
| This machine | Roles | Machine roles, the Suggested line, All roles and the scoped diagram. Machine session defaults have precise doors to their existing owners. No machine workflow is invented: the workflow read currently takes a repository or workspace. |
| Workspace | Workflow | Default workflow and kind routes, Roles, session defaults, and repository exceptions. Line settings are labelled as defaults for repositories. Gates remain per repository. |
| Driven repository | Workflow | Effective configuration, its sources and its available edits. |
| Imported, not driven | Setup introduction | Checkout status, *Set up…*, then configuration sections once their selected-place read arrives. Reading or editing configuration does not enable driving. |

The imported list currently answers only repository identity and checkout presence. Showing its configuration requires the selected-place projection in build row S1; the page must not manufacture inherited values from that minimal row (`src/Daoris.Web/src/data/wiring.ts:359`).

The place list retains search, the driver’s *Needs a look* reasons, driven repositories first, and the imported fold. Workspace selection and expansion are separate controls. Opening a search result reveals its ancestors. Paging retains the selected place, even if the next list page no longer contains it; selection identity must not depend on the currently displayed page. Today `findPlace` searches only the answer in hand (`src/Daoris.Web/src/agents/setup/SetupPlaces.tsx:211`).

At 680 px, *Places* opens the frame’s existing list surface. Choosing a place returns focus to its page head. Back returns to the previous place, section, selected step and scroll position. A breadcrumb’s workspace name opens that workspace’s Setup page. Account, plugin and session doors carry a return destination.

Page state belongs above the mounted sections: place, disclosures, editor draft, selected step and return destination. This extends the existing setup draft preservation across navigation (`src/Daoris.Web/src/agents/setup/placePlan.ts:139`). It does not promise recovery of an unsaved editor after application restart.

**2. Set up… is a choice page followed by a review page.**

Both pages occupy the main area. They share one draft and one driver plan. Moving to review writes nothing.

```text
1546 px — choices

← Repository
Set up Repository

Delivery                         Adoption
(●) Merge after you accept       [x] Ask its session to set up for agents
( ) A branch to push                 What it builds
( ) A pull request                   [Web app or service              ▾]
( ) Integrated into the line
( ) Not driven                   General driving
[x] Second opinion               [ ] Enable general driving
[ ] Your look

Plan reading: 5 changes · no refusal
[Review plan] [Cancel]
```

```text
680 px — choices

← Repository
Set up Repository

Delivery
(●) Merge after you accept
    Work merges after your Accept.
( ) A branch to push
( ) A pull request
( ) Integrated into the line
( ) Not driven
[x] Second opinion
[ ] Your look

Adoption
[x] Ask its session to set up for agents
    What it builds
    [Web app or service          ▾]

General driving
[ ] Enable general driving

[Review plan] [Cancel]
```

The choice page keeps the driver’s suggestion, preselected once. Selecting another delivery never replaces the person’s choice when the plan refreshes. Adoption and general driving remain separate. *What it builds* appears only while adoption is selected and shapes only the adoption quest. These distinctions already exist in the draft and request (`src/Daoris.Web/src/agents/setup/PlaceSetUpPanel.tsx:31`, `src/Daoris.Web/src/agents/setup/placePlan.ts:101`).

A workspace adds a repository chooser before Delivery. Its initially selected driven repositories remain the existing starting point, with imported repositories available explicitly. Search and *Select shown* affect only selection; they never apply configuration. A mixed suggestion says so and shows each repository’s suggestion instead of presenting the first repository’s suggestion as universal. The current panel takes the suggestion from the first plan row (`src/Daoris.Web/src/agents/setup/PlaceSetUpPanel.tsx:33`).

```text
1546 px — review

← Edit choices
Apply setup to 3 repositories

What changes                              What starts
Delivery: branch → integration            Adoption: 3 quests
  Repository A, Repository B               General driving: unchanged
Own session trees: off → on
  Repository A                            Your part
                                          [driver’s lowering sentences]
Repository exceptions
  Repository C — [driver’s refusal]        Queued work released
                                          [titles, grouped by repository]
Accept declarations                       [Hold it after driving]
  Repository A · main · commit · blob
  [Read declaration]
  [ ] Accept for landed verification
  [ ] Accept for integration
  [standing and consequence for each]

[Apply to 3 repositories] [Edit choices]
[terminal twin ▸]
```

```text
680 px — review

← Edit choices
Apply setup to 3 repositories

What changes
  Each identical change once
  Repository names beneath it

Repository C
  [driver’s refusal, whole]
  [Return to delivery]

Your part
  [driver’s lowering sentences]

Queued work released
  Repository A
    Work title
  [Hold it after driving]
  Sets hold now and reads the plan again.

Accept declarations
  Repository A · main
  commit … · blob …
  [Read declaration]
  [ ] Accept for landed verification
      [standing and consequence]
  [ ] Accept for integration
      [standing and consequence]

[Apply to 3 repositories]
[Edit choices]
```

The review has four reading priorities: changes, refusals and reduced involvement, work released, then acceptances. Technical commands sit in a disclosure. Refusals also appear beside the choice that caused them, using the driver’s `part` association; the review provides links back to those choices. The existing plan already carries these associations and grouped changes (`src/Daoris.Web/src/agents/setup/placePlan.ts:24`).

A refused workspace plan stays one refused plan. The page offers selection changes and replanning; it does not silently apply the successful subset.

Acceptance ticks start empty. Their local identity includes repository, purpose, line and blob. A changed declaration clears its ticks and announces why. Apply still sends the existing purpose selections with the reviewed plan digest. This strengthens the presentation without broadening `PLACE_APPLY`.

The same declaration text appears once when purposes share the same read identity. Equal text from different repositories is not treated as one acceptance. Changed landed verification says verification stops; changed integration says the last accepted blob continues. Those are already separate standings (`src/Daoris.Web/src/workflow/place/PlaceGates.tsx:73`).

*Hold it after driving* remains a separate immediate `SET_HOLD` act. Its caption says that it sets hold now, before Apply, and rereads the plan. Partial failure across several holds lists each outcome beside that press. The existing implementation performs holds serially and currently reports failure through a toast (`src/Daoris.Web/src/agents/setup/PlaceSetUpPanel.tsx:53`); D191 explicitly records that hold is outside the plan’s receipt (`docs/decisions/D191.md:63`).

While a new plan is being read, the previous review remains visible and labelled as updating; Apply is unavailable. A moved plan preserves the driver’s actual refusal, refreshes the review and requires another press. The current component substitutes its own sentence for `moved`; that substitution goes (`src/Daoris.Web/src/agents/setup/PlaceSetUp.tsx:309`).

After Apply, the page shows the receipt’s precise result and operation outcomes. Adoption opens its quest or session through a door. “Configuration applied; adoption pending” remains a valid outcome, rather than being flattened into success or failure (`src/Daoris.Web/src/agents/setup/PlaceSetUpPanel.test.tsx:126`).

**3. Workflow is a readable vertical flow, with one selected step’s editor.**

The start node chooses which flow is being inspected. Default work is initially selected; each kind and Landed work is selectable. Selection changes the displayed diagram without writing a choice. Kind names and paths remain readable even where this proposal supplies no kind-definition editor.

A selected route’s heading names its effective workflow, newest version where saved, and source. It never places a saved workflow’s name over Current’s diagram. Current is labelled *live rules*. A saved workflow is labelled *saved workflow*. A run is labelled with its bound version.

```text
1546 px — workflow editing in the main area

← Repository / Workflow
Release after you accept · draft from v3                    [Review and save] [Close]

[Add step…]                         Selected: Your review
                                    Taken by you
            Start                   When accepted
              │                       Continue to [Landing         ▾]
            Work                    When you ask for changes
              │                       Go back to [Work             ▾]
       Requests and holds           [Remove step…]
              │
            Checks                  Details ▸
              │                       step id · kind · version
        Your review ─────────┐
              │              │ changes
            Landing          └──────────────↶ Work
              │
          On the line
              │
       Find pending release
          /             \
      release           none
         │               └─ No further step
   Record release
         │
   Publish package
         │
   No further step
```

```text
680 px — same flow, one column

← Repository / Workflow
Release after you accept · draft
[Add step…] [Review and save]

Start
  ↓
Work
  ↓
Requests and holds [Expand]
  ↓
Checks
  ↓
Your review                              [Options]
  Accepted → Landing
  Changes ↶ Work
  ↓
Landing
  ↓
On the line
  ↓
Find pending release                     [Options]
  release → Record release
  none → No further step
  ↓ release
Record release
  ↓
Publish package
  No further step

Options open as the full available drawer width:
Your review                              [Close]
When accepted → Landing
When you ask for changes → Work
[Done] [Remove step…]
```

The graph remains top to bottom at 1546 and 680 px. At wide widths, sibling branches share a rank where both readable cards fit. At 39 and 25rem, sibling branches become sequential branch groups, each headed by its outcome and with shared destinations drawn once. Each source retains a textual destination link, so geometry is never the only explanation.

The existing `Flow` already provides measured boxes, textual edge feet, keyboard navigation and a list fallback. Sections prevent horizontal placement (`src/Daoris.Web/src/design/Flow.tsx:89`). The editor should use that facility with workflow sections, rather than allowing its unsectioned drawing to choose another orientation.

A branch carries its condition beside the split and in the source card: *When outcome is `release` → Record release*. A loop goes around the outside of the flow, labelled *Changes → Work*, using the existing `back` style. It never crosses through cards. At narrow widths the source and target carry reciprocal links instead of squeezing an outer loop into the text column.

The two drawings above abbreviate holds for readability. Any folded group says how many steps it contains and opens to its individual steps; a refusal opens the relevant group automatically.

The editor has four focused interactions:

| Interaction | Presentation and effect |
|---|---|
| Add a step | A searchable picker grouped into Daoris steps and installed plugins. Decision steps are the offered steps with declared outcomes. Adding a step creates a draft node; it does not execute anything. |
| Edit a step | Selecting its card opens options beside the graph where space permits, otherwise in the form drawer. Only this step’s form is open. |
| Edit a route | Select an outcome or destination in the source card. Choose *Continue*, *Go back*, or another wire kind the palette permits, then a destination. No dragging is required. |
| Remove | An inline confirmation names the step and affected connections. The draft removes those connections without silently bridging the gap. The driver then judges the result. |

The palette remains authoritative for kinds, options, permitted outgoing wires and end eligibility (`src/Daoris.Web/src/workflow/edit/answers.ts:40`). `graphDraft` remains the lossless draft representation; it already preserves fields the current editor does not draw (`src/Daoris.Web/src/workflow/edit/graphDraft.ts:3`).

**Decision options are an outcome table.** Every declared outcome is visible, including an outcome with no outgoing route:

```text
Find pending release — Local release

Outcome       Goes to                   Carries
release       Record release             release → release input
none          No further step            nothing

[Add destination…]
Data and evidence ▸
```

Selecting *release → Record release* can propose the compatible data connection alongside its `when` connection. Both appear before confirmation and in the save review. The person can inspect or change the input mapping. A data wire stays distinct from the condition that makes the step eligible.

`when` and `back` are available today, with declared outcomes and upstream-return rules (`docs/2026-11-01-fabric2-plan.md:49`). They are not expressions the person types. The review’s Accept is its forward route; its changes verdict is rejection, optionally connected back to Work. A built-in review is not presented as a plugin decision with invented named outcomes.

**An ending must distinguish a graph declaration from observed completion.** The release default declares `end: line` and also connects Line to Find, Release and Publish (`src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/release.json:14`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/release.json:52`).

Therefore:

- The graph’s configured end is a marker on its step, with an explanation when further steps follow. Its setting remains editable only where the palette permits it.
- A branch without a next step shows a neutral *No further step* terminator. This is a drawing annotation, not a synthetic saved node or a successful result.
- The run’s *Finished* state comes only from the driver’s run record. The page never infers it from reaching `graph.end`, an empty branch or faded nodes.

The shipped `none` outcome has no `when` wire. The editor displays that fact without inserting a stop node. The declared output exists only on `release`, as the default and plugin contract specify (`src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/release.json:25`, `examples/plugins/local-release/README.md:31`).

**Plugin steps name who performs them and what the installed plugin declares.** Release and Publish cards retain the plugin’s identity and declared inputs, outputs and evidence. Their role field is absent: these are plugin-performed steps, not agent sessions (`docs/2026-11-01-fabric2-plan.md:69`). For the example plugin, the wording says it records a release and copies a package into its own registry folder; it does not imply publication to an external registry (`examples/plugins/local-release/README.md:25`).

**Change… previews before it writes.**

```text
1546 px

Change workflow
[Defaults] [Saved] [Current]

Candidate list                  Selected candidate
  Merge after you accept        Release after you accept · v3
  Documents                     [vertical preview]
  Release after you accept      Used by: [places]
                                Your part: [driver’s comparison]
                                Applies to: Repository default work
                                [Use here] or [Start from this…]
```

```text
680 px

Change workflow
[Defaults] [Saved] [Current]
[Choose a workflow            ▾]

Release after you accept · v3
[vertical preview]
Used by: [places]
Your part: [driver’s comparison]
Applies to: Repository default work
[Use here] / [Start from this…]
```

Defaults open drafts and are saved before use. Saved workflows open their selected version for inspection. Older versions are readable and can start a new workflow; *Use here* chooses the latest version read, preserving today’s route semantics. *Use the workspace’s* clears the repository choice; explicit Current chooses live rules.

*Edit steps* opens the graph currently being inspected. For Current, it opens a copy that can become a saved workflow. It does not open an empty editor, and editing that copy does not alter live landing rules.

Save and Use remain separate acts:

1. **Review and save** shows step and connection changes, changed involvement, and every place an append reaches. The person chooses *Save a new workflow* or *Save vN*. New workflows ask for a name; their generated id is available under Details.
2. **Save** invokes `WORKFLOW_SAVE`. Appending retains its base-version and affected-scope authorization.
3. **Use here** invokes `WORKFLOW_USE` for the displayed place and route. The preview addition in S1 makes this a reviewable choice.
4. A dirty draft cannot offer *Use here* for its previously saved version as though it contained the new edits.

The judge’s result and scope authorization are tied to the exact draft fingerprint. Editing the graph or changing its target invalidates that review. Current code retains an earlier judgment while changes debounce, and its Save availability does not require a judgment of that exact draft (`src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:154`, `src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:333`).

**The run retains the same shape.** Its header names the bound version and selection source, then one sentence about where work stands. Selecting a step reveals its record and the existing owner’s controls. A decision shows the returned outcome; other routes say *Not taken*, separately from *Not reached*, *Skipped* and *Failed*. These distinctions already have page tests (`src/Daoris.Web/src/workflow/WorkflowRunView.test.tsx:600`).

At 1546 px the Workflow dock can show the chart beside the conversation. At 680 px it uses the existing dock placement and the one-column flow. Its narrow list preserves branch and return destinations; it must not substitute a sequential rail that loses graph wiring. It never invents a chronological execution trail from current node states.

**4. Staffing begins with the work, then opens its role.**

```text
1546 px

▾ Roles                                              [All roles]
Work              Role and source                  First choice / last reading
Build             Workspace build · Workspace      Agent · model · effort · read 8 min ago
                  [Give Repository its own…]
Design            Repository design · set here     Agent · model · effort
                  [Edit] [Back to Workspace’s…]
Second opinion    Reviewer · This machine           Agent · model · effort

Appointed roles
Supervisor        From Workspace                    [Open appointment]

Suggested — machine-wide choices available          [Review…] [Not now]
```

```text
680 px

▾ Roles
[All roles]

Build
Workspace build · from Workspace
Agent · model · effort
Last reading: signed out · 8 min ago
[Give Repository its own…]

Design
Repository design · set here
Agent · model · effort
[Edit] [Back to Workspace’s…]

Appointed roles
Supervisor · from Workspace
[Open appointment]
```

Each effective row uses the driver’s chain answer. The page does not resolve roles again. That boundary is already explicit in `roleStructure` (`src/Daoris.Web/src/agents/roles/roleStructure.ts:7`).

The role editor keeps place fixed, generates the id, and leads with ordered agent choices. Names and work sit under *Edit role*. The active language’s authored name appears first; the other name remains reachable and opens if required information is missing.

```text
1546 px — role form

Edit Repository build
Placed in Repository

Order    Agent               Model                 Effort               Actions
1        [agent ▾]           [offered model ▾]      [offered effort ▾]   ↑ ↓ Remove
2        [agent ▾]           [account default]       [default]            ↑ ↓ Remove
[Add choice]

▸ Edit role — names and work
[Save] [Cancel]                         [Off…] [Clear staffing…]
```

```text
680 px — role form

Edit Repository build
Placed in Repository

Choice 1
Agent  [agent                 ▾]
Model  [offered model         ▾]
Effort [offered effort        ▾]
[Move up] [Move down] [Remove]

Choice 2
…
[Add choice]
▸ Edit role
[Save] [Cancel]
[More role actions…]
```

Models and effort come from the selected agent’s offers, with the existing typed alternative. Accounts remain the agent’s responsibility; staffing never assigns an account. These controls already exist (`src/Daoris.Web/src/agents/roles/Roles.tsx:361`).

*Back to parent’s*, *Off*, *Clear staffing* and *Restore role defaults* retain separate meanings and presses. Removal confirmations use the effective fallback returned for this place. A role shared by several work rows is edited once, with those works named.

**All roles** is a subpage of This machine, with a return link to the originating place. It lists every role once, filters by scope, and offers List or Diagram. Machine roles edit there. A role owned by a workspace or repository has *Open at place*, preserving one editor per role.

The diagram keeps its three columns at wide widths and its textual list at narrow widths. Inheritance remains highlighted rather than becoming extra edges (`src/Daoris.Web/src/agents/roles/RoleDiagram.tsx:73`). The existing catalogue’s scope groups and owner-place door are reused (`src/Daoris.Web/src/agents/roles/Roles.tsx:543`).

The Suggested line opens a review of machine-wide changes. One *Use these choices* invokes one `SET_ROLES`, retaining its all-or-nothing judgment. The existing eligibility, dated dismissal and omitted-agent explanation remain (`src/Daoris.Web/src/agents/roles/SuggestedChoicesCard.tsx:34`). Repository pages may link to it; they do not relabel it as a repository-specific suggestion.

**A step’s Role field offers the effective work role and compatible named roles.** Each named choice includes its place. *Give this place its own…* opens the same role editor, retaining the workflow draft and selected step. Saving the role immediately updates the catalogue through `SET_ROLE`; selecting it for the step changes only the workflow draft. The two saves remain visible.

The product must explain that adding a repository Build role also changes the repository’s inherited Build staffing. It is not a private staffing pool for one node. The catalogue allows at most one role for the same work in one scope (`src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleCatalog.cs:308`). If one exists, the door opens it. The driver supplies any conflict refusal.

**5. The same words identify the same things throughout.**

The glossary already distinguishes Setup from review-environment set-up, landing from merge, and an outcome from a state (`src/Daoris.Web/src/locales/glossary.json:554`, `src/Daoris.Web/src/locales/glossary.json:560`, `src/Daoris.Web/src/locales/glossary.json:578`, `src/Daoris.Web/src/locales/glossary.json:701`).

| English | Chinese | Use |
|---|---|---|
| Setup / Set up… | 配置 / 配置… | Mode and repository configuration action |
| set-up | 搭建 | Review environment showing only |
| Workflow | 工作流 | The configured process |
| Edit steps | 编辑步骤 | Entry into composition |
| Compose | 编排 | Arranging workflow steps |
| Role / All roles | 角色 / 全部角色 | Staffing position and catalogue |
| Appointed roles | 任命的角色 | Supervisor and steward; use the glossary wording consistently |
| Landing | 落地 | The workflow step; its method may be merge |
| Merge | 合并 | Git merge |
| Integration | 集成 | The line’s integration process |
| Review / Your look | 审阅 / 你的查看 | Distinct steps with distinct existing controls |
| Outcome / Route | 结果 / 路线 | A declared result and its way through the graph |
| Not taken | 未走 | An outcome excluded that route |
| Use here | 在此使用 | Choose the displayed saved workflow here |

New labels needing glossary rows are *Decision step / 决策步骤*, *Go back / 回到前面的步骤*, and the distinct release and package-publish captions. The broad existing *publish / 发布* entry currently describes publishing a quest; its meaning needs an explicit contextual extension for plugin publishing (`src/Daoris.Web/src/locales/glossary.json:520`).

```text
1546 px — Chinese chrome follows the same structure

仓库                                                    [配置…] [⋯]
工作区 · 已驱动
▾ 工作流    你采纳后发布 · v3 · 来自工作区        [更改…] [编辑步骤]
  起点 → 默认工作 → 工作流
▸ 角色      来自工作区；1 个在此配置
▸ 会话      独立工作树；继承访问设置
```

```text
680 px — words wrap; names remain whole

[位置]
仓库
工作区 · 已驱动
[配置…] [⋯]

▾ 工作流
你采纳后发布 · v3
来自工作区
[更改…] [编辑步骤]

查找待发布版本
  release → 记录发布版本
  none → 无后续步骤
```

Built-in option labels and choices receive chrome labels such as *Landing method*, rather than displaying `method` as the primary label. Technical keys remain in Details. Plugin names, outcome identifiers, authored role names, paths, messages and refusal sentences stay as written.

Every refusal uses the driver’s sentence as its primary text. Localized guidance can identify the setting to reopen; it cannot replace the judgment. This follows translation parity’s verbatim-message rule (`.claude/knowledge/translation-parity.md:51`).

**The seven required flows share these surfaces.**

| Flow | Person’s path and resulting behavior |
|---|---|
| First setup | Setup → workspace → imported fold → repository → *Set up…*. Keep the driver’s delivery suggestion, choose adoption and what it builds, leave general driving as desired → review changes and acceptances → Apply. Return to the place with its receipt and adoption door. |
| Change configuration | Open one row → Edit → Save. For a setup recipe spanning settings: *Set up…* → choices → reviewed plan → Apply. For a workflow choice: Change → candidate preview → Use here. |
| Staffing | Repository → Roles → Build → *Give it its own…* → ordered agent choices → Save. The row returns with its actual source and last reading. |
| Appointment | Workspace → Roles → Supervisor → Appoint. Reuse the existing owner-only control. An unoffered appointment stays a quiet explanation with no action; the current component already distinguishes this (`src/Daoris.Web/src/agents/roles/AppointedRoles.tsx:125`). |
| Edit an existing workflow | Workflow → select route → Edit steps opens that route’s graph → edit → review save and affected places → Save. Bound runs keep their versions. Use here is needed when choosing a newly saved workflow; an authorized append already affects future starts at its listed places. |
| Compose a release workflow with a decision | Change → Defaults → Release after you accept → Start from this. Inspect checks, review, landing and line; inspect Find’s `release` and `none` outcomes. Connect `release` to Record release with its input, then Publish with its input. Keep `none` without a next step. Set review changes back to Work. Review, save, then Use here. Missing plugin or invalid input routing stays the driver’s refusal. |
| Staff a step with a role added here | Select Work → Role → *Give this place its own…*. Save the place’s Build role through `SET_ROLE`, return to the intact workflow draft, choose that role, then review and save the workflow. A refused role save retains both forms’ state. A role already occupying that work and scope is edited, not duplicated. |

The release composition page test already exercises `when`, `back`, data connections and a named role through a stubbed bridge; it is a starting regression, not installed-window proof (`src/Daoris.Web/src/workflow/edit/WorkflowEditor.test.tsx:138`).

**The implementation changes have these file owners.** Paths below are repository-relative; grouped prefixes are stated explicitly.

| Existing file | Keep, move, merge or remove |
|---|---|
| `src/Daoris.Web/src/agents/setup/SetupPlaces.tsx:101` | Keep the place list and driver filtering. Add separate workspace expansion, stable selected-place identity and the compact head. Move detailed facts into disclosures. |
| `src/Daoris.Web/src/agents/AgentsView.tsx:164` | Merge the catalogue and diagram into the lit Setup navigation. Hold place-section state and drafts above their mounts. Remove the old section-navigation branch only after replacement doors work. |
| `src/Daoris.Web/src/app/App.tsx:435` | Keep organism composition and central opening. Supply exact section, setting and return destinations. |
| `src/Daoris.Web/src/app/opener.ts:187` | Extend the current redirect to workspace configuration and preserve requested anchors; today repository redirects lose the distinction between Setup and Workflow. |
| `src/Daoris.Web/src/agents/setup/PlaceSessions.tsx:35` | Remove temporary Driving and Workflow composition from the Sessions wrapper. Retain session-setting molecules; compose all three sections at the place-page owner. |
| `src/Daoris.Web/src/agents/setup/PlaceSetUp.tsx:125` | Keep choices, grouped plan facts and receipt rendering. Split choices from review; move refusals beside their choices and retain the original moved sentence. |
| `src/Daoris.Web/src/agents/setup/PlaceSetUpPanel.tsx:20` | Keep plan/apply hooks. Add mixed-suggestion presentation, per-repository hold outcomes and local refusal state. |
| `src/Daoris.Web/src/agents/setup/placePlan.ts:75` | Keep request shapes and per-place drafts. Add review state and declaration-specific tick identity; retain existing Apply authority. |
| `src/Daoris.Web/src/agents/setup/PlaceRoles.tsx:59` | Keep effective per-work rows and parent fallback confirmation. Extract the common role-editor presentation and add the step-editor return path. |
| `src/Daoris.Web/src/agents/roles/Roles.tsx:320` | Keep ordered choices and catalogue operations. Reuse one editor in place rows, All roles and step staffing; remove duplicate editor ownership. |
| `src/Daoris.Web/src/agents/roles/RolesView.tsx:127` | Move reusable catalogue/diagram ownership into All roles. Stop using the former multi-section Roles mode as the lit Setup destination. |
| `src/Daoris.Web/src/agents/roles/RoleDiagram.tsx:89` | Keep the diagram molecule, selection and narrow list. Mount it with the place or All roles scope. |
| `src/Daoris.Web/src/agents/roles/roleStructure.ts:39` | Keep pure projection and driver resolution. Adapt its scope inputs for selected-place and catalogue reads without eager workflow reads for every repository. |
| `src/Daoris.Web/src/agents/roles/AppointedRoles.tsx:28` | Keep its scoped organism and existing appointment controls. Mount under the common appointed-role label. |
| `src/Daoris.Web/src/agents/roles/SuggestedChoicesCard.tsx:60` | Keep atomic `SET_ROLES` and dated dismissal. Merge its presentation with the added-role suggestion into one expandable Suggested region. |
| `src/Daoris.Web/src/workflow/place/PlaceWorkflowView.tsx:42` | Make this the common repository/workspace workflow section. Replace the long stack with route selection, chart and selected details. |
| `src/Daoris.Web/src/workflow/place/PlaceGates.tsx:90` | Keep purpose-specific routes and partial outcomes. Start choices empty, bind them to read identity, and share declaration presentation with setup review. |
| `src/Daoris.Web/src/workflow/place/ChangeWorkflow.tsx:60` | Replace immediate list-row Use actions with selection and preview. Preserve Current, saved and default sources. |
| `src/Daoris.Web/src/workflow/WorkflowTab.tsx:86` | Merge shared heading/chart presentation into the place section. Retain the tab as the flag-off adapter until cutover is proved. |
| `src/Daoris.Web/src/workflow/WorkflowStartNode.tsx:124` | Keep route and landed standing. Add route selection; open Gates instead of duplicating acceptance controls on a place page. |
| `src/Daoris.Web/src/workflow/WorkflowChart.tsx:363` | Keep shared step presentation and run marks. Give configuration, editing and narrow graph views explicit graph wiring; retain a branch-aware textual form. |
| `src/Daoris.Web/src/workflow/WorkflowRunView.tsx:54` | Keep record doors and owner controls. Add selected-step disclosure and distinguish configured end markers from driver-reported finish. |
| `src/Daoris.Web/src/workflow/workflowFlow.ts:158` | Keep `when`/`back` projection. Replace the unconditional visual finish attached to `graph.end` with explicit declaration and completion presentations. |
| `src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:233` | Remove the simultaneous graph and all-open form list. Keep source loading, judgment, save and use callbacks; add selection, route editing and save review. |
| `src/Daoris.Web/src/workflow/edit/StepOptions.tsx:26` | Keep palette-driven fields. Add readable built-in labels, source-aware role choices and the common role-editor door. |
| `src/Daoris.Web/src/workflow/edit/graphDraft.ts:72` | Keep lossless round trips. Add explicit route-edit operations and draft undo; preserve groups, retry and cleanup fields. |
| `src/Daoris.Web/src/workflow/edit/answers.ts:19` | Add the read metadata named in S1 and role scope/revision in the editor projection. Keep write shapes compatible. |
| `src/Daoris.Web/src/bridge/repositories/workflowEdit.ts:78` | Keep hooks in the organism. Preserve drafts through role navigation, read exact source versions and associate judgments with request fingerprints. |
| `src/Daoris.Web/src/projects/useWorkflowTab.ts:20` | Generalize the shared workflow read owner for repository and workspace place pages. |
| `src/Daoris.Web/src/locales/glossary.json:484` | Reconcile term meanings and add the named gaps. Update `en/agents.json`, `zh/agents.json`, `en/workflow.edit.json`, `zh/workflow.edit.json` and the workflow catalogues together. |

**Build rows are ordered by the contracts the visible work needs.**

Every visible row requires page tests, stories at **80, 39 and 25rem × en and zh × light and dark**, keyboard operation and measured browser layout. Those widths are the existing frame-story contract (`src/Daoris.Web/src/design/storyWidths.tsx:9`). Installed proof uses actual **1546 and 680 px windows**, separately from stories and bridge mocks.

| Row | Contract | Proof |
|---|---|---|
| **S1 — Complete the reads and review fields** | Add `PLACES.detail: {workspace, repository}` for a selected imported place’s configuration projection. Add an optional inspected-route request to `WORKFLOW_CURRENT` and an `effective` answer containing selected workflow identity, source, version/digest, graph and a driver-produced editable Current graph. Add `WORKFLOW_USE.plan: true` returning comparison/reach plus an `expected` review token; the normal Use press supplies that token and refuses a moved review. Add optional step display names/descriptions to `WORKFLOW_PALETTE`; preserve raw fallback. Retain role scope and revision already returned by `ROLE_STRUCTURE`. | Route tests prove reads write nothing, Current remains available, graph identity matches the resolver, stale use writes nothing, and old callers retain behavior. Page tests distinguish Current from a selected saved graph. No new authority or route. |
| **S2 — Place navigation and page shell** | Compact heads, section disclosures, selected-place detail, anchor and return state. Workspace configuration has the same home. | Extend SetupPlaces and application-opening tests for imported selection across paging, workspace Workflow doors, direct Gates opening and return focus. Replace the artificial full-page story split at `SetupPlaces.stories.tsx:27` with actual frame placement. Installed: navigate all place kinds at both widths. |
| **S3 — Quick setup and declaration review** | Choices/review separation, mixed workspace suggestions, empty acceptance ticks, read-bound selections, truthful hold timing and complete local outcomes. | Extend `PlaceSetUpPanel.test.tsx:114` for mixed suggestions, refusal correction, changed blobs, stale plans and partial hold failure. Stories include several repositories, long driver sentences and uncertain adoption. Installed: review, change configuration externally, Apply, recover and retry. |
| **S4 — Staffing and All roles** | Reachable catalogue/diagram, one role editor, explicit scope, one Suggested region and unchanged appointment ownership. | Extend `PlaceRoles.test.tsx:118` through catalogue-to-owner navigation and duplicate-work refusal. Stories cover inherited, own, unreadable, off and unavailable choices. Installed: edit, visit an account, return, then restore the parent role. |
| **S5 — Shared workflow reading** | Selected route draws its effective graph. Top-to-bottom sections, branches, loop links, configured end marker and truthful finish. | Extend WorkflowStartNode, WorkflowChart and run tests for a saved default, inherited kind, `none`, returns, shared destinations and narrow graph fallback. Measure edge/card clearance in both themes and languages. Installed: inspect a release run on both outcomes. |
| **S6 — Focused step editor** | One selected form, palette-driven addition, outcome table, explicit input mappings, preserved unknown fields, local undo and exact-draft judgment. | Extend the existing release composition test at `WorkflowEditor.test.tsx:138`; add out-of-order judgments, source changes, unsatisfied inputs and lossless group/retry/cleanup round trips. Stories include a branched release graph at every width. Installed: compose by keyboard at 680 px. |
| **S7 — Save and workflow choice review** | New workflow versus authorized append is explicit; every affected place is shown. Use follows a fresh reviewed choice; dirty drafts cannot masquerade as saved versions. | Tests cover appended reach changes, stale base, stale Use, clear-to-parent, explicit Current, older-version copy and Save succeeding while Use refuses. Installed: append a workflow shared by two places while an existing run remains bound to its earlier version. |
| **S8 — Staff a step and complete run presentation** | The common role editor returns to the unchanged graph. Named role selection saves into the workflow; automatic/plugin steps offer no agent role. Run controls remain their existing owners’. | A page test adds a place role, returns, selects it, saves and uses the workflow. Driver regression proves scope/work refusals and the recorded bound role. Installed: start work on the added role; ask for changes and observe the return to Work. |
| **S9 — Language, frame and cutover** | Finish glossary parity, keyboard/focus behavior and all destination redirects. Remove superseded lit-mode sections and workspace tabs only after replacement proof. | Catalogue key and placeholder parity, naming checks, molecule-boundary checks, measured layouts and the complete story matrix. Installed: all seven flows, including refusal and interrupted Apply, in both languages and themes at both window widths. Only then enable the mode by default. |

S1’s additions are read and review support. `PLACE_APPLY` remains the person’s setup Apply; `SET_ROLE(S)` remains staffing; `WORKFLOW_SAVE` remains saving; `WORKFLOW_USE` remains choosing; `SET_LANDING` remains Current’s landing-rule edit. Acceptances retain their own purpose-specific routes and records.

The line’s policy stays in its own disclosure and editor. Selecting a landing step cannot edit batching, ordering, gates or promotion. A Current landing edit uses `SET_LANDING`; a saved graph edit uses `WORKFLOW_SAVE`. The distinction is already specified by LANDNODE1 (`docs/2026-11-01-land-node-design.md:149`).

**What remains open is limited to questions that observation should settle.**

- **First-open section choice.** Start with Workflow open for repositories and workspaces, Roles open for This machine. Measure whether people immediately open another section; change the default if the observed task pattern warrants it.
- **Large workspace selection.** Measure repository counts and selection time before adding saved selections or more elaborate bulk controls.
- **Long graph folding.** Start with explicit fold summaries and refusal-driven opening. Measure whether users can find an outcome and follow its return without expanding the whole graph.
- **Restart recovery of unsaved drafts.** Preserve drafts through in-window navigation now. Decide durable unsaved-draft storage separately, after measuring losses and establishing its machine-local lifecycle.

The first usability measurement is an unaided installed-window task: configure an imported repository for adoption while keeping unrelated work held. Record time, presses, wrong destinations and whether the person can explain what Apply will start.

The second is comprehension: given the release graph, identify who accepts, what changes sends back, what `none` does, which plugin publishes, and which settings govern line batching. The third is staffing: add a place role, assign it to Work and explain which other work inherits that role.

Success means completing those tasks without reading terminal twins or technical Details, while still being able to reach both. Story and page-test evidence will establish component behavior; the installed window must establish that the experience works as a whole.