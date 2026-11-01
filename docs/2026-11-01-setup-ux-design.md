# SETUPUX1: setting work up, composing its workflow and staffing it

Status: proposed, 2026-11-01, the synthesis of the two sealed proposals; reads owed: Codex and an Opus reader; D221 reserved.

**Standing.** This is the synthesis of `2026-11-01-setup-ux-proposal-opus.md` and `2026-11-01-setup-ux-proposal-codex.md`. It answers `2026-11-01-setup-ux-brief.md` and follows the steward's nine settlements. Web paths start at `src/Daoris.Web/src/`, driver paths at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and modules paths at `src/Daoris.Desktop/Daoris.Desktop.Modules/`. Every file:line in §1 was checked in the code on 2026-11-01. The routes stand as built: the design adds read fields and one review token, and changes nothing that a press authorises.

**Widths.** W is the main area at 80rem (1546 px, list open), H is 39rem (680 px, list at its strip), and N is 25rem, the floor (`design/storyWidths.tsx:13`). A two-column layout switches on its own width (R7).

## 1. What both found

**The place**
1. Nothing folds. The page runs facts (`agents/setup/SetupPlaces.tsx:284-324`), Roles (`:325`), then Driving, Workflow and Sessions (`agents/setup/PlaceSessions.tsx:40-50`). Driving belongs in the head (`docs/2026-11-01-agents-place-design.md:63`; `PlaceSessions.tsx:30-31`).
2. With the flag lit, *All roles*, the diagram, *Suggested* and the found staffing changes vanish. The lit branch mounts only `PlaceRoles` (`agents/AgentsView.tsx:179-209`); `RolesCard` is drawn only in the dark branch (`:210-229`, `agents/roles/RolesView.tsx:220-239`).
3. A workspace has no Workflow section (`PlaceSessions.tsx:33,87-91`). Its Workflow and Setup tabs stay on Repositories (`projects/workspace/WorkspaceView.tsx:141-161`, `app/opener.ts:187-192`).
4. A tree row shows only a name and its needs (`SetupPlaces.tsx:146-152`), though `PLACES` gives each value its source (`data/wiring.ts:341-354`). An imported row is a name and a checkout (`:365`). Paging loses the selection (`findPlace`, `SetupPlaces.tsx:211-221`), and *Needs a look* is a checkbox (`:120`).

**Set up…**
5. Only the first repository's suggestion is read (`agents/setup/PlaceSetUp.tsx:111`, `PlaceSetUpPanel.tsx:33`), and a workspace starts from its driven repositories only (`AgentsView.tsx:202`, `SetupPlaces.tsx:336`).
6. Refusals and changes are one block under every part (`PlaceSetUp.tsx:216-240`), though each refusal names its `part` (`placePlan.ts:26`). `moved` gets the page's own sentence (`PlaceSetUp.tsx:309`). Holds run one after another, and a failure is a toast (`PlaceSetUpPanel.tsx:53-60`).
7. Preset words have two sources. `agents.setup.defaultName.*` and `.set.line.*` (`PlaceSetUp.tsx:165,168`, `SetupPlaces.tsx:310`) copy the shipped `names` and `for` (`Defaults/workflows/merge-after-accept.json:4-8`), which `ChangeWorkflow` reads itself (`workflow/place/ChangeWorkflow.tsx:16`). The ids are a literal (`placePlan.ts:7`) beside `SetupPresets.Table` (`Workflows/Domain/SetupPresets.cs:44-48`). `WORKFLOW_LIST` carries no `for`, `order` or `setUp` (`Workflows/WorkflowOperations.cs:67-74`).
8. One declaration is drawn twice (`PlaceSetUp.tsx:44-90`, `workflow/place/PlaceGates.tsx:79-196`). Gates pre-ticks both acceptances (`PlaceGates.tsx:90`), and a tick ignores the blob that was read (`placePlan.ts:128-129`).

**The workflow**
9. One name covers two mechanisms. In *Set up…*, *Merge after you accept* writes `land` options and sets a chosen version back to Current (`SetupPresets.cs:25-31`, `Setup/PlacePlan.cs:169-182`). Under *Change…*, the same record is a graph to copy, save and choose (`ChangeWorkflow.tsx:132-149`, `locales/zh/workflow.place.json:28`).
10. The chart draws Current even where the route names a saved version (`Bridge/Routes/WorkflowRoutes.cs:47`), under the title "Current workflow" (`workflow/WorkflowTab.tsx:54`, `workflow/place/PlaceWorkflowView.tsx:61,71`). The start node is a second `Flow` above it (`workflow/WorkflowStartNode.tsx:178`).
11. The editor sits at the foot of the section (`PlaceWorkflowView.tsx:94-98`) and draws each step twice: in the graph, and as an open card with a way-on form (`workflow/edit/WorkflowEditor.tsx:261-279,466-530`). Its graph has no sections, so `Flow` may lay it left to right (`WorkflowEditor.tsx:261`, `design/Flow.tsx:216`, `design/flowLayout.ts:283-293`).
12. The editor asks for internals: a typed id (`WorkflowEditor.tsx:316-317`), step ids (`:412`), targets as `title (id)` (`:497`), and options by field name (`StepOptions.tsx:69,82,86,98`).
13. Choosing a method does not bring its tail (`graphDraft.ts:186-199`), so the save is refused `land-tail` (`Workflows/Nodes/NodeGraphs.cs:159`). An input needs a hand-made `data` wire (`WorkflowEditor.tsx:510-523`) beside its `when` (`Defaults/workflows/release.json:53-56`).
14. `quest.holds` and `content.holds`, which every default holds (`merge-after-accept.json:18-19`), have no words (`locales/en/workflow.edit.json:18-37`), so they read "A step this page has no words for" (`WorkflowEditor.tsx:65-70`). The stories' release graph has neither (`workflow/edit/editFixtures.ts:56-64`).
15. Endings are drawn wrongly. `none` has no wire and draws nothing. *Finished* hangs off `graph.end` (`workflow/workflowFlow.ts:166-168`), which in the release default is `line`, followed by three steps (`release.json:14,52-56`), so the line forks into *Finished* and *Find*.
16. Choosing has no plan.
    - Both *Use here* presses go straight to `WORKFLOW_USE` (`ChangeWorkflow.tsx:47-55`, `WorkflowEditor.tsx:220-228`), which checks only the version that was read (`WorkflowRoutes.cs:413-418`).
    - An edited draft still offers *Use* for the last saved version (`WorkflowEditor.tsx:173,329-333`).
    - *Save* does not wait for this draft's judgement (`:154-162,327`).
    - *Start from* repeats Change… (`:241-259`).
    - Current's "version" is its digest (`workflow/current.ts:125-126`, `ChangeWorkflow.tsx:71`).

**Staffing**

17. A place's Roles lists all nine works, each with its act (`agents/setup/PlaceRoles.tsx:111-187`). It edits with the whole `RolesCard` (`:173-179`), whose prose opens every time (`agents/roles/Roles.tsx:422-426`), though the prose was to go (`docs/2026-11-01-agents-place-design.md:146`).
18. A role that a place adds takes that work for the whole place (`Roles/RoleCatalog.cs:308-312`). The step's picker neither says so nor offers to add one (`workflow/edit/StepOptions.tsx:43-62`).

**The words**

19. zh borrows words that already mean something else:
    - 交付 is *submission* (`zh/agents.json:373`; `glossary.json:522`);
    - 搁置 says held, where `hold` is 暂停 (`zh/agents.json:324,405`; `:668`);
    - 委任 is used where `appoint` is 任命 (`zh/agents.json:363`; `:572`);
    - 关闭 says a role is off, a word `turn off` avoids (`zh/settings.ai.json:37`; `:652`);
    - 当前 stands beside 现行 (`zh/workflow.place.json:28-29`, `zh/workflow.json:4`);
    - 打开设置 means Settings (`zh/agents.json:202`).

    Several things have more than one name:
    - gates are 检查, 关卡 and 关口 (`zh/workflow.place.json:2`, `zh/workflow.edit.json:144`, `zh/workflow.json:5`);
    - a way back is 回到前面的步骤, 回退 and 退回 (`zh/workflow.edit.json:66,126`, `zh/workflow.run.json:279`);
    - the editor is 编排工作流, 编排步骤… and 编辑步骤… (`zh/workflow.edit.json:2`, `zh/workflow.json:181`, `zh/workflow.place.json:41`).

## 2. The design

### 2.1 The place page

```
1546 · W · a driven repository
┌ Agents ───────────────────┬────────────────────────────────────────────────────────────────────────────┐
│ [ Agents │ Setup ]         │ aurora ›                                                                   │
│ ⌕ Search places            │ engine  ● Driven                                    [ Set up… ] [ Hold ] ⋯ │
│ [ Needs attention · 2 ]    │ Merge after you accept, 2 options of its own · its own tree per session    │
│────────────────────────────│────────────────────────────────────────────────────────────────────────────│
│ This machine               │ ▾ Workflow   open on arrival (§2.3)                                        │
│ aurora           3 of 20   │ ▸ Roles      Second opinions: Reader, set here · Building: a start is held │
│   engine                   │              · 7 more, as aurora has them                                  │
│   Merge after you accept   │ ▸ Sessions   Restricted · English · helpers ask you                        │
│   site  ⚠ landing unread   │                                                                            │
│   ▸ 17 imported, not driven│                                                                            │
└────────────────────────────┴────────────────────────────────────────────────────────────────────────────┘
 ⋯  Stop driving… · ✓ Its own tree per session · Supervisor: Appoint… · Show in Repositories · Copy the terminal's line

680 · H (list at its strip; N is the same column at 25rem)
┌──┬──────────────────────────────────────────────────┐
│◧ │ aurora ›                                          │
│⚙ │ engine  ● Driven                                  │
│  │ Merge after you accept, 2 of its own · own trees  │
│  │                         [ Set up… ] [ Hold ]  ⋯   │
│  │ ▾ Workflow  …                                     │
│  │ ▸ Roles     Second opinions: Reader, set here · 8 more │
│  │ ▸ Sessions  Restricted · English · helpers ask you│
└──┴───────────────────────────────────────────────────┘
```

| Place | Head | Sections (the first opens on arrival) |
|---|---|---|
| This machine | *9 works · 14 roles*; no *Set up…* | **Roles**, *All roles*, and Sessions as doors to their Settings; *Suggested* and the found changes sit above |
| Workspace | *3 of 20 driven*; *Set up…* | **Workflow** (routes, kinds, the line's defaults for its repositories, and *Repositories*; Gates stay with each repository), Roles, Sessions |
| Driven repository | as drawn | **Workflow**, Roles, Sessions |
| Imported | *Not driven · checkout here*; *Set up…* is its primary act | one line on what *Set up…* does, then the sections, folded and read-only, from `PLACES.detail` (row 1d) |

**The head** has UX7's two lines, and says each fact once.
- The name, with its parent as a door.
- A pill: *Driven*, *Held* or *Not driven*.
- Two facts: how its work lands, in Set up…'s words, with *n options of its own* (row 1d's `lands`); and its trees.
- *Set up…* and *Hold* or *Resume*, which take their own row at 640 px or less (`work/frame/ViewMain.tsx:34-37`).
- The ⋯ as drawn: `SET_DRIVABLE`, confirmed in place, and `SET_TREES`.

**The sections** are `SetupSection` folds (`projects/setup/SetupParts.tsx:57`).
- A folded line gives each value its source, from `PLACES` alone: *from aurora*, *from this machine*, or *default*. A value set here is unmarked.
- After the first, a section opens only for a need. A declaration owed acceptance opens Workflow at Gates; an unreadable landing opens it at Landing; a held start opens Roles at that work.
- Opening one section closes none, and a section reads only while it is open.
- For each place, the open sections, the chosen route and step, the editor's draft and the way back are held above the mounts, as Set up…'s drafts are (`placePlan.ts:145-154`). None of this survives a restart.

**The tree**
- A repository's second line says how it lands, *n of its own*, or *Held*. A need adds ⚠ and its first reason.
- *Needs attention* is a chip with its count (`PlacesRead.matching`).
- Selection is kept by place id across pages and searches (row 1d), and a search result opens its workspace.
- Selecting a workspace and expanding it are separate controls.

**Moving between places**
- Every source mark is a door to that place, open at the same section and row. `placeOpening` passes both, and the way back (`app/opener.ts:187`).
- A page reached from elsewhere shows *← Back to engine · Roles* once.
- At 680 px, *Places* opens the list. Back restores the place, section, step and scroll.

*Chosen:* the head, tree and folded lines are Opus's. Opening by kind of place, the kept selection and the way back are Codex's. Together, the head carries the preset and Workflow opens first.

### 2.2 Set up…

```
1546 · W · Set up engine: the choices
│ aurora › engine  ● Driven                                                                [ Close ] │
│ ┌ How it lands ──────────────────────────────────┐ ┌ Adoption ───────────────────────────────────┐ │
│ │ (•) A pull request              [ Suggested ]  │ │ ☑ Ask its session to adopt Daoris           │ │
│ │     Pushed, and a plugin opens a pull request  │ │   What it builds  (•) Not said              │ │
│ │     for you to merge.                          │ │   ( ) A library   ( ) A web app or service  │ │
│ │     Suggested: its remote is on github.com,    │ │   ( ) A desktop app or game  ( ) Docs…      │ │
│ │     and a landing plugin lands there.          │ ├ Driving ────────────────────────────────────┤ │
│ │ ( ) Merge after you accept                     │ │ ☐ Drive all its work                        │ │
│ │ ( ) A branch to push                           │ │   Off: only the adoption quest runs.        │ │
│ │ ( ) Integrated into the line  ( ) Not driven   │ └─────────────────────────────────────────────┘ │
│ │ ☐ A second opinion before it lands             │                                                 │
│ │ ☐ Your look before it lands                    │                                                 │
│ └────────────────────────────────────────────────┘                                                 │
│ The plan: 3 changes · nothing stops it                                [ Review the plan ] [ Close ] │

680 · H · the choices in one column, a refusal under its part
│ engine › Set up engine                  [ Close ] │
│ How it lands                                      │
│ (•) A pull request                [ Suggested ]   │
│     ⚠ No landing plugin lands on github.com here. │  ← the driver's sentence
│       [ Open Plugins ]                            │
│ ( ) Merge after you accept  …                     │
│ Adoption  ☑ Ask its session to adopt Daoris       │
│ Driving   ☐ Drive all its work                    │
│ The plan: 2 changes · 1 stops it  ↑ How it lands  │
│ [ Review the plan ]                               │

1546 · W · Set up aurora: the review (H keeps this order in one column)
│ ← Back to the choices                    Apply to 3 repositories                                    │
│ What changes                                       │ What it starts                                │
│   How it lands  merge into main → pull request     │   Adoption: 3 quests, one per repository      │
│                 engine, site                       │   Driving: unchanged                          │
│   A tree each   off → on          engine           │ Your part  ⚠ [the driver's lowering sentence] │
│   Its workflow  Release for site v2 → Current  site│ Work it releases                              │
│ What stops it                                      │   site: Fix the login · Add the docs page     │
│   docs: [the driver's refusal, whole]              │   [ Hold it after driving ] sets the hold now │
│   ↑ How it lands   [ Leave docs out ]              │   and reads the plan again                    │
│ Acceptances   engine · main a1b2c3d · blob 9f3c1e0   ▸ Read the declaration                         │
│   ☐ Accept for landed verification: verification starts once accepted                               │
│   ☐ Accept for integration: integration asks under this blob                                         │
│ ▸ In a terminal                       [ Apply to 3 repositories ] (not offered while something stops it) │
```

*Set up…* replaces the sections and keeps the head. Its two pages share one draft and one plan, and moving to the review writes nothing.

**How it lands** lists the data's set-up deliveries in the data's order, each with `delivery.names` and `delivery.for` (row 1a).
- The suggestion comes first, with the driver's `why`. *Not driven* is last, and the second-opinion and look ticks follow.
- Where no delivery matches, the part shows *As it is, n of its own*.
- On a place that follows a saved workflow, the plan's own change says that any choice sets it back to Current.

**Adoption** is a tick, with *What it builds* under it, which shapes only the adoption quest. **Driving** is a tick, *Drive all its work*. With it off, only the adoption quest runs.

**The plan's line** counts the changes and what stops the plan, with a jump to each. A refusal stands under its part (`PlaceRefusal.part`), whole and in the driver's words, with the door its code names.

**A workspace** adds a chooser: *All · Driven · Not driven · None*, a count and a search, with imported repositories included. Where the suggestions differ, nothing is preselected, and each choice names the repositories it suits.

**The review** reads in this order:
1. The changes, by part, each identical change once with its repositories.
2. What stops it, linked back to its part. It offers a change of selection, never a partial apply.
3. What lowers your part, and what the plan starts.
4. The work it releases, with *Hold it after driving*: an immediate `SET_HOLD` outside the receipt, with each repository's outcome beside it.
5. The acceptances, by the one *Acceptances* molecule that Gates draws. The text appears once per read. Ticks start empty, keyed by repository, purpose, line and blob, and a change clears them with its reason.
6. The terminal's line, folded, and *Apply n*.

**Applying.** While a plan is read, the last one stays, marked *updating*, and *Apply* waits. *Apply* (`PLACE_APPLY`) carries the digest and the chosen acceptances. `moved` is said in the driver's sentence, and the plan is read again. The result says each operation's outcome and opens the adoption quest. *Configuration applied; adoption pending* is said as itself.

*Chosen:* Codex's two pages, over Opus's plan beside the choices (settlement 2). Opus's refusal doors, `why` and chooser presses.

### 2.3 Composing a workflow

```
1546 · W · engine › Workflow (the editor draws the same way)
│ ▾ Workflow                                                                [ Change… ]  [ Edit steps… ] │
│   (•) Default work             → Release for engine v2 · set here · copied from Release workflow       │
│   ( ) Documentation  docs/**   → Documents workflow v1 · from aurora                                    │
│   ( ) Landed work              → no verification                                          [ Verify… ]  │
│   ┌ Release for engine v2: the saved workflow default work follows ─────────────── Show as list ┐   │
│   │ Before it lands                                                                              │   │
│   │  ╭────▶ [ ◯ The work · Builder, this machine ]                                               │   │
│   │  │      [ ◇ Daoris makes sure · 3 ▸ ]                                                        │   │
│   │  │      [ ◇ The checks ]                                                                     │   │
│   │  ╰─back [ ● Your review · you ]       ↩ if you ask for changes: back to The work             │   │
│   │         [ ◇ The landing · merge into main ]                                                  │   │
│   │ After it lands ────────────────────────────────────────────────────────────────────────────  │   │
│   │         [ ◇ On the line ]  ⚑ Ends here                                                       │   │
│   │         [ ◇ Find a pending release · Local release ]                                         │   │
│   │            release ┆                    ┆ none                                               │   │
│   │   [ ◇ Record the release ]        ◎ No further step                                          │   │
│   │   [ ◇ Publish the package ]  →  ◎ No further step                                            │   │
│   └──────────────────────────────────────────────────────────────────────────────────────────────┘   │
│   ▸ Gates      Landed verification: accepted at main a1b2c3d, blob 9f3c1e0 · Integration: none        │
│   ▸ Landing    merge into main · squash · "{title}", the workflow's                         [ Edit ]   │
│   ▸ The line   main · batch 1 · in order · declared gates, the line's own                             │
│   ▸ More       commits: one per change · heavy gates: 2       In a terminal: daoris driver workflow show │
```

**Routes are rows.** Choosing one picks the graph drawn and writes nothing. Default work comes first. The kinds stay read-only until `WORKFLOW_KIND` has a form, and *Verify…* sits on the landed row.

**The chart is the chosen route's effective graph** (row 1b). Its label is either *Current: read from the rules as they stand* or *‹name› vN: the saved workflow ‹route› follows*. A saved name never stands over Current's drawing.

**The two owners stay apart** (D215). *Landing* is the workflow's: `SET_LANDING` under Current, or the editor at `land` under a version. *The line* owns batching, order, gates and promotion. Gates is the *Acceptances* molecule.

***Edit steps…*** opens the graph drawn. For Current, it opens an editable copy that never touches the live rules.

**One drawing**, `workflow/graphFlow.ts`, serves the chart, the editor and a run. It always passes sections (*Before it lands*, *After it lands*, or *Steps*), so `Flow` lays the graph top to bottom or as the rail, never across (`design/Flow.tsx:216`). At 39rem a 14rem track fits a two-way branch (2 × 14 + 3 = 31rem). Anything wider falls to the rail.

| Thing | At 80 and 39rem | At 25rem, the rail | Edited by |
|---|---|---|---|
| A way on (`after`) | A solid edge down; *＋* between steps in the editor | Numbered rows | *Once done →*; *＋* |
| A branch (`when`) | Dashed edges side by side under the step, labelled by outcome; the card says its routes in words | `├ release → 7 Record → 8 Publish` / `└ none → No further step` | *When it comes to ‹outcome› →* |
| A loop back (`back`) | Its own lane at the left, labelled *back*, crossing no card | `↩ If you ask for changes: back to 1 The work` | *If you ask for changes → Back to ‹step›* |
| A failure | Short dashes to the target | `✕ If it fails → …` | *If it fails →* |
| The configured end | ⚑ *Ends here* on its step; its foot says when steps follow it (§6) | the same | ⋯ *End the work here* |
| No route on | ◎ *No further step*: a note in the drawing, never a node and never a success | the same | *No further step* as a target |
| *Finished* | only from a run's record | — | — |
| `work.requests`, `quest.holds`, `content.holds` | One group, *Daoris makes sure · 3 ▸*, which opens to its members; a refusal opens it | One row, opening inline | each member's row |

```
Change…, inside the section, in place of the chart (680 · H: the tabs, a select, then the preview)
│ Change the workflow default work follows  [ Presets │ Saved here │ Current ]                  [ Close ] │
│ ┌ Saved here ──────────────────────┐ ┌ Release for engine v3 ──────────────────────────────────────┐ │
│ │ Release for engine  v3  ● here   │ │ [the drawing, top to bottom]                                │ │
│ │ Docs for aurora     v1  at aurora│ │ Chosen at: aurora (kind docs) · Running work keeps its own  │ │
│ │                                  │ │ Your part: [the driver's comparison]                        │ │
│ │                                  │ │ [ Use here ]   [ Edit steps from this… ]                    │ │
│ └──────────────────────────────────┘ └─────────────────────────────────────────────────────────────┘ │
```

**Change…** has three tabs.
- ***Presets*** lists the shipped workflows in order, with `names` and `for`. A preset is not stored, so it offers *Save a copy…* (a name only, `WORKFLOW_SAVE`) and *Edit steps from this…*. Once saved, it offers *Use here*.
- ***Saved here*** lists each workflow's newest version and where it is chosen. Older versions can be read, and can start copies.
- ***Current*** is labelled live and chosen by *Use here*. On a repository, *Use aurora's* clears the repository's own choice. Current's digest appears only under *Details*.

Selecting a candidate reads `WORKFLOW_USE` with `plan: true` (row 1c). It answers the reach, what lowers your part, the places it changes, that running work keeps its version, and an `expected` token, which *Use here* carries. A moved choice is refused `moved`, and nothing is written.

*Chosen:* Codex's candidate and preview, over Opus's folding of Change… into Set up… (settlement 4). Opus's routes as rows, with Codex's choosing of what to draw.

```
1546 · W · Edit steps: graph and inspector side by side while the page holds 64rem
│ aurora › engine › Workflow › Edit steps · from Merge workflow (Current)        ↶ Undo  1 to put right ▾  [ Close ] │
│ ┌ The steps ───────────────────────────────────┐ ┌ Find a pending release ───────────────── by Local release ┐ │
│ │ Before it lands                              │ │ Looks for a pending release. Daoris runs it; the plugin   │ │
│ │  ╭──▶ [ ◯ The work · Builder ]               │ │ answers release or none.                                  │ │
│ │  │             ＋                             │ │ What happens next   Goes to                  Carries      │ │
│ │  │    [ ◇ Daoris makes sure · 3 ▸ ]          │ │   release      → [ Record the release   ▾ ] its release  │ │
│ │  ╰─back [ ● Your review ]                    │ │   none         → [ No further step      ▾ ] nothing      │ │
│ │       [ ◇ The landing · merge ]              │ │   If it fails  → [ The run stops        ▾ ]              │ │
│ │ After it lands ───────────────────────────── │ │ ▸ More ways on    ▸ Details: find · local-release/find v1 │ │
│ │       [ ◇ On the line ]  ⚑ Ends here         │ │ ⋯  End the work here · Remove this step…                  │ │
│ │       ┏ ◇ Find a pending release ┓ selected  │ └───────────────────────────────────────────────────────────┘ │
│ │   release ┆              ┆ none              │                                                               │
│ │  [ ◇ Record the release ]  ◎ No further step │                                                               │
│ │  [ ◇ Publish the package ]  ◎ No further step│                                                               │
│ │ [ ＋ Add a step ]                             │                                                               │
│ └──────────────────────────────────────────────┘                                                               │
│ Name [ Release for engine           ]  ▸ Details     These steps hold.                      [ Review and save ]  │

680 · H · the drawing, then the step               400 px · N · the rail, the step inline
│ engine › Edit steps     ↶   1 ▾  [ Close ] │      │ Before it lands                          │
│ ╭▶ [ ◯ The work ]                          │      │ 1 ◯ The work                           › │
│ │  [ ◇ Daoris makes sure · 3 ▸ ]           │      │ 2 ◇ Daoris makes sure · 3              › │
│ ╰─ [ ● Your review ]                       │      │ 3 ● Your review                        › │
│    [ ◇ The landing · merge ]               │      │   ↩ If you ask for changes: back to 1    │
│ After it lands ─────────────────────────── │      │ 4 ◇ The landing · merge                › │
│    [ ◇ On the line ] ⚑                     │      │ After it lands                           │
│    [ ◇ Find a pending release ]            │      │ 5 ◇ On the line · ⚑ Ends here          › │
│  release ┆           ┆ none                │      │ 6 ◇ Find a pending release             ⌄ │
│ [ ◇ Record… ]   ◎ No further step          │      │   release → [ 7 Record the release ▾ ]   │
│ [ ◇ Publish… ]                             │      │   none    → [ No further step      ▾ ]   │
│ ────────────────────────────────────────── │      │   If it fails → [ The run stops    ▾ ]   │
│ Find a pending release   ↑ To the drawing  │      │ 7 ◇ Record the release · 8 ◇ Publish   › │
│ release → [ Record the release ▾ ]         │      │ [ ＋ Add a step ]                         │
│ Name [ … ]               [ Review and save ] │    │ Name [ … ]  [ Review and save ]          │
```

The editor is a page of its own in the main area, under the place's head. One step's inspector shows at a time, and says what the step does and who acts in it (`reachBy`).

**What happens next** is a table of outcomes from the palette's `leaves`: *Once done* (with *＋ also to…*), *If you ask for changes* (*If you say not yet* on a look), *If it fails*, and every declared outcome, whether routed or not.
- A target is a later step, *Back to* an earlier one, *No further step*, *The run stops*, or *＋ A new step…*.
- `trigger` and hand-made wires sit under *More ways on*.
- A review is never drawn as a plugin's decision.

**Inputs are proposed.** Routing to a step whose input exactly one earlier output fits proposes the `data` wire beside its `when`. The wire shows under *Carries* and in the save review. It can be changed, and it never merges into the condition.

**Options** read in chrome words (`workflow.edit.option.‹kind›.‹name›`, with the raw name as fallback), and their keys sit under *Details*.
- `land`'s method is five choices, each of which brings its tail.
- `pattern` is previewed.
- `target` lists the lines.

***Taken by*** appears only on the work and the second opinion (§2.4).

**No ids.** The workflow's id comes from its name, as role ids do (`agents/roles/Roles.tsx:26`). Step ids sit under *Details*, and *Goes to* lists titles.

**Refusals** stand at their row, the page's words over the driver's sentence, gathered under *n to put right ▾*.

***Remove…*** is confirmed in place. It names the ways on it cuts and never bridges the gap.

**Undo** is ↶, or Ctrl+Z in the drawing. ↑↓, Enter and Escape move between the steps and the inspector.

**The picker** (*＋* or *Add a step*) is searchable, and inserts after its step. It is grouped:
- *The work*;
- *You*;
- *Checks*;
- *Landing*;
- *Other repositories*;
- each plugin, by its steps' titles (row 1d);
- *Other*.

With no plugin installed, *Other* holds the door to Plugins.

**Saving.** A judgement carries its draft's fingerprint, and an older one is discarded. *Review and save* needs this draft's holding judgement. It says the changed steps, the involvement, and every place an append reaches (*Let them follow it*), then offers *Save as a new workflow* or *Save vN*.

**Choosing.** *Use here…* shows only while the draft equals its saved version, and reads the plan of one in Change…'s molecule. A changed draft says *Save your changes to use them*. *Start from* goes.

*Chosen:* Opus's page, inspector and picker. Codex's one open form, *Carries*, a removal that never bridges, and judgement by fingerprint. Opus's rail at 25rem, held to Codex's rule that it keeps every branch and return in words.

**A run's view** (`workflow/WorkflowRunView.tsx`) uses the same module.
- Its head names the bound version and how it was chosen.
- A decision says what it came to.
- Ruled-out routes read *route not taken*, apart from *not reached*, *skipped* and *failed*.
- It counts *sent back n times*.
- *Finished* comes only from the record.

### 2.4 Staffing

```
1546 · W · engine › Roles
│ ▾ Roles                                                                                      [ All roles ] │
│   Second opinions  Reader · set here         Codex · gpt-5 · high · cooling 12 min                         │
│                                                                    [ Edit ]  [ Back to aurora's… ]         │
│   Building         Builder · this machine    Claude Code · opus · high (+1) · a start is held              │
│                                                                    [ Give engine its own… ]                │
│   ▸ 7 more, as aurora has them: design, chat, helping, partnering, brainstorms, intake, Ask Daoris         │
│   Appointed        Supervisor: aurora's covers engine                    [ Appoint for engine… ]           │

680 · H · rows stacked, the editor in place           1546 · W · This machine
│ ▾ Roles                        [ All roles ] │     │ This machine   9 works · 14 roles                              ⋯ │
│ Second opinions · Reader · set here          │     │ ◆ Suggested choices for 4 roles, from Daoris's reference of    │
│ ┌ Edit Reader · engine ────────────────────┐ │     │   2026-10-28                         [ Review… ] [ Not now ]   │
│ │ 1 [ Codex ▾ ] [ gpt-5 ▾ ] [ high ▾ ] ↑↓✕ │ │     │ ◆ 2 staffing changes found in driver.json      [ Review… ]     │
│ │ 2 [ Claude Code ▾ ] [ opus ▾ ] [ — ▾ ]   │ │     │ ▾ Roles      Building: Builder · Chat: Chat · 7 more           │
│ │ [ ＋ Add a choice ] ⓘ How choices are used│ │     │ ▸ All roles  14: 9 placed here, 5 elsewhere  [ List │ Diagram ] │
│ │ ▸ Edit role   It takes engine's second   │ │     │ ▸ Sessions   each a door to the Settings that own it           │
│ │ opinions.  [ Save ] [ Cancel ]  ⋯ More   │ │
│ └──────────────────────────────────────────┘ │
│ ▸ 8 more, as aurora has them                 │
```

**Rows** come from `ROLE_STRUCTURE` (`RoleFor` alone).
- A work is a row where it is set here, differs from the parent's, or has a need. The rest fold away.
- A row gives the role, its place, its first choice and that choice's last reading.
- A parent's role offers *Give ‹place› its own…*. One set here offers *Edit* and *Back to ‹parent›'s…*, confirmed with the fallback named.
- *Off*, *Clear staffing* and *Restore role defaults* stay three separate acts, under *More role acts ⋯*.
- A role that takes several of the works shown is edited once.

**One `RoleEditor`**, split from `Roles.tsx:320-445`, serves a place, *All roles* and *Taken by*.
- The place is fixed, and the id is generated.
- The choices come first, from the agent's offers.
- *Edit role* is folded, with the reader's language first.
- The prose sits behind one ⓘ.
- One sentence of consequence stands before *Save*.
- No account is set.

***All roles*** (This machine) lists every role once, with scope filters, as a *List* or a *Diagram*. A role placed elsewhere is read-only there, with *Open at ‹place›*.

***Suggested*** is one line at the top, and opens `SuggestedChoicesCard`: one `SET_ROLES`, judged whole. The found changes are a line beside it.

**A step's role** offers its work's own role, every role that takes that work, and *＋ Give engine its own role for ‹work›…* where engine has none. Where engine has one, it offers *Edit Reader · engine…* instead.
- The inline editor says that the role will then take all of engine's ‹work›, not only this step's (`RoleCatalog.cs:308-312`).
- `SET_ROLE` saves at once, while the step's choice stays in the draft. Both saves are said.
- A refused save keeps both forms.

*Chosen:* Opus's folded rows and sentence of consequence. Codex's single edit for a shared role, and opening an existing role instead of adding one.

### 2.5 The words

**The rename (settlement 3).** The shipped workflows are renamed, and the deliveries keep today's names.
- The head and *Set up…* say the delivery names.
- A workflow's name then always says it is one: *… workflow*, …工作流.
- Each record's `delivery` gains `names` and `for`, holding today's strings. The top-level `names` become the workflow's.

The part is *How it lands*, not *Delivery*. *Delivery* also names the land node's record, and its zh, 交付, is *submission*. A way back is Opus's *Back to…* (退回到…), beside the glossary's `send back`, not Codex's 回到前面的步骤.

| Thing | en | zh | Replaces | Glossary |
|---|---|---|---|---|
| The mode; a place's set-up | Setup; Set up… | 配置; 配置… | 打开设置 | amend `setup` (`glossary.json:701`); `set-up` (搭建) stands |
| A place | place | 位置 | 范围 (`zh/settings.ai.json:79`) | new; avoid 范围 |
| Set up…'s first part | How it lands | 落地方式 | Delivery, 交付 | new; avoid 交付 |
| A delivery | Merge after you accept · A branch to push · A pull request · Integrated into the line · Not driven | 你采纳后合并 · 留给你推送的分支 · 拉取请求 · 集成到主线 · 不驱动 | the same names on workflows | the data's `delivery.names` |
| A shipped workflow | preset; Merge, Automatic merge, Documents, Branch, Pull request, Integration, No-press integration, Release workflow | 预设; 合并, 自动合并, 文档, 分支, 拉取请求, 集成, 免按键集成, 发布工作流 | 默认工作流 | new `preset`; the data's `names` |
| The workflow of the rules | Current | 现行 | 当前 | new; avoid 当前 |
| A saved version | version, v2 | 版本 | the digest drawn as a version | amend `version` (`:644`) |
| Choose; compose | Change… · Use here; Edit steps… | 更改… · 在此使用; 编辑步骤… | 在这里使用; 编排工作流, 编排步骤… | `Use here` (`:584`); `compose` (`:581`) keeps 编排 |
| A step; its way on | step; way on | 步骤; 去向 | *wire*, a remote's word (`:607`) | new; avoid 节点, 连线, 接线 |
| A decision step; its result | decision step; outcome | 决策步骤; 结果 | — | new; `outcome` (`:578`) |
| Back to an earlier step | Back to … | 退回到… | 回到前面的步骤, 回退 | beside `send back` (`:576`) |
| A review's rejection | Ask for changes (on a look, Not yet) | 要求修改 (查看: 还没有) | 你拒绝时 (`zh/workflow.edit.json:51,64`) | each kind keeps its word |
| The person's steps | Your review; Your look | 你的审阅; 你的查看 | *Needs a look*, 需要查看 | new; the filter becomes *Needs attention*, 需要留意 |
| Declared gates | gates | 检查项 | 检查, 关卡, 关口 | new; avoid 关卡, 关口 |
| Endings | Ends here; No further step; Finished | 止于此; 无后续步骤; 已完成 | *Finished* after `end` | `ends here` (`:583`); new; avoid 结束 |
| The pre-landing group | Daoris makes sure · n | Daoris 核实 · n | "no words for" | new |
| Hold; appointed; off | Hold, Held; Appointed; Off | 暂停, 已暂停; 任命的; 停用 | 搁置; 委任; 关闭 | `hold`, `appoint`, `turn off` |
| Staffing | staffing; Clear staffing | 人选; 清除人选 | 清除顺序 | new |
| Adoption; driving | Ask its session to adopt Daoris; Drive all its work | 请它的会话采用 Daoris; 驱动它的全部工作 | 为智能体配置; 通用驱动 | `adopt` (`:626`) |
| Plan; Apply | plan; Apply | 计划; 应用 | — | new `plan`; amend `apply` (`:679`) |
| Landing | landing | 落地 | a meaning from before LANDNODE (`:554`) | amend: the five methods |
| A plugin's publish step | its own title | as written | `publish`, a quest's (`:520`) | amend `publish` |

Plugin names, outcome ids, authored names, paths and refusal sentences stay as written (`.claude/knowledge/translation-parity.md`).

## 3. The flows

1. **First set-up.** Setup → aurora → *17 imported, not driven* → api → *Set up…*. *How it lands* is preselected with its why, Adoption is ticked and Driving is off. → *Review the plan* → tick the acceptances on offer → *Apply 3 changes* (`PLACE_APPLY`). That is five presses plus the acceptances; today it is about fifteen across six screens (`docs/2026-11-01-agents-place-design.md:18`). The result opens the adoption quest, and the head reads *Driven · A pull request*. For several repositories: aurora → *Set up…* → *Not driven* (17) → choose, with each suggestion said per repository → review → *Apply*.
2. **A change.**
   - One row: Workflow → Landing *Edit* → the message → *Save* (`SET_LANDING`).
   - Several rows: *Set up…* → *A branch to push* → review → *Apply*.
   - A workflow: *Change…* → *Saved here* → *Docs for aurora v1* → read the plan of one → *Use here*, carrying its token.
3. **Staffing.** engine → Roles → Building *Give engine its own…* → Claude Code · opus · high, then Codex · gpt-5. It reads "It takes engine's building from Builder, this machine." → *Save* (`SET_ROLE`). The row then reads *Builder · engine · set here*, with *Back to aurora's…*.
4. **An appointment.** aurora → Roles → Supervisor *Appoint…* → the owner's *Appoint*, unchanged. Where it is not offered, the row is a quiet line.
5. **The workflow.** Workflow opens on its routes, the labelled chart and the gates' standing → *Edit steps…* → a change → *Review and save* → *Save v3*. The append names the places it reaches, and running work keeps its version. If the route followed something else: *Use here…* → the plan of one → *Use here*.
6. **A release workflow with a decision.**
   - **From the preset:** *Change…* → *Presets* → *Release workflow* → *Save a copy…* as *Release for engine* → the plan of one (its reach, nothing lowered, the checks added) → *Use here*. Six presses.
   - **Composed:**
     1. *Edit steps…* opens Current, the Merge workflow.
     2. Select *On the line* → *＋* → *Local release › Find a pending release*. Both outcomes read *No further step*.
     3. On the release row: *＋ A new step…* → *Record the release*. *Carries* proposes its input from *Find*.
     4. *＋* → *Publish the package*, with its input proposed.
     5. *Your review* → *If you ask for changes* → *Back to The work*.
     6. *These steps hold* → name it → *Review and save* → *Save as a new workflow* → *Use here…* → *Use here*. About sixteen presses.
   - Without the plugin, the picker has no *Local release* group, and *Other* holds the door to Plugins.
7. **Staffing a step with a role the place adds.**
   1. *＋* above *Your review* → *Second opinion*.
   2. *Taken by: Reader, from aurora ▾* → *＋ Give engine its own role for second opinions…*. The editor opens inline, at engine, as *Reader · engine*.
   3. Choose Codex · gpt-5 · high. It reads "It will then take all of engine's second opinions, not only this step's." → *Save* (`SET_ROLE`).
   4. The picker shows *Reader · engine* in the draft → *Review and save* → *Save* → *Use here*.

   engine's Roles then reads *Second opinions: Reader · set here*.

## 4. What moves, merges and goes

New molecules take props and send presses. Organisms hold the hooks: `PlaceSetUpPanel`, the Workflow organism over `useWorkflowTab`, and the editor's organism over `bridge/repositories/workflowEdit.ts`.

| File | Fate | What |
|---|---|---|
| `agents/setup/SetupPlaces.tsx` | change | The facts (`:284-324`) move into `PageHead`. The slots become the three sections. The tree gains its second line, the chip and the kept selection. |
| `agents/setup/PlaceSessions.tsx` | split | Driving (`:40`) moves to the head. The Workflow mount (`:41-47`) becomes the Workflow organism, which a workspace mounts too. Sessions stays. |
| `agents/setup/PlaceRoles.tsx` | change | Rows for the works that differ. `RolesCard editorOnly` (`:173-179`) becomes `RoleEditor`. |
| `agents/setup/PlaceSetUp.tsx` | split | Into `SetUpChoices` and `SetUpReview`. `Declaration` (`:44-90`) merges into `Acceptances`. Its own `moved` (`:309`) goes. |
| `agents/setup/PlaceSetUpPanel.tsx`, `placePlan.ts` | keep | `PRESETS` (`:7`) goes. A tick's key gains its line and blob. Hold outcomes are kept per repository. |
| `agents/setup/placePage.ts` | add | The page's state, for each place. |
| `agents/AgentsView.tsx` | keep | The lit branch (`:179-209`) mounts *All roles*, the diagram, *Suggested* and the found changes. The dark branch (`:210-229`) goes at the cutover. |
| `agents/modes.ts`, `AgentsModes.tsx`, `roles/RoleSections.tsx`, the section lists in `roles/StructureSections.tsx` | remove at the cutover | A stored `roles` mode reads as `setup`. |
| `agents/roles/Roles.tsx`, `RolesView.tsx` | split | The editor (`:320-445`) moves to `RoleEditor.tsx`. `RolesCard` stays, as *All roles*. |
| `workflow/graphFlow.ts` | add | Absorbs `workflowFlow.ts:158-171` and `WorkflowEditor.tsx:103-114`. `WorkflowChart` and `WorkflowRunView` draw through it. |
| `workflow/WorkflowStartNode.tsx`, `WorkflowTab.tsx` | remove at the cutover | The routes become rows. `WorkflowHead`'s words become the chart's label. |
| `workflow/place/PlaceWorkflowView.tsx`, `ChangeWorkflow.tsx` | re-laid | Routes, chart and folds, with the editor moved out. Change… gains tabs, preview, plan and *Save a copy…*. |
| `workflow/place/PlaceGates.tsx` | keep | Becomes the one `Acceptances` molecule. Its ticks start empty (`:90`). |
| `workflow/edit/WorkflowEditor.tsx` | split | Into `StepGraph`, `StepInspector`, `StepPicker` and `SaveReview`. *Start from*, the id field, the card list and `WayForm` go. |
| `workflow/edit/StepOptions.tsx`, `graphDraft.ts` | keep | Worded fields, the tails, *Taken by*, `insertAfter`, `setExit`, `withTail`, `proposeData`, undo and a fingerprint. Round trips stay lossless. |
| `workflow/edit/answers.ts`, `bridge/repositories/workflowEdit.ts`, `editFixtures.ts` | keep | The fields from rows a–d, and the judgement matched to its request. The fixture is held equal to `release.json`. |
| `projects/useWorkflowTab.ts`, `app/opener.ts` | change | The read now takes a route. `placeOpening` (`:187-201`) passes the section, the row and the way back. |
| `projects/workspace/WorkspaceView.tsx`, `ProjectsView.tsx`, `ProjectPage.tsx` | change at the cutover | The Setup and Workflow tabs go. |
| `design/Flow.tsx`, `flowLayout.ts` | unchanged | Sections and track bounds already exist (`Flow.tsx:89-97`). |
| the stories, locales and `glossary.json` | change | The frame's own placement replaces the 280 px split. §2.5 applies. |
| `Defaults/workflows/*.json`, `WorkflowDefaults.cs`, `SetupPresets.cs` | change | `delivery.names` and `delivery.for`. `Table` (`:47`) reads them. |
| `WorkflowOperations.cs`, `Bridge/Routes/WorkflowRoutes.cs` | change | Preset fields (`:67-74`). `route`/`effective` (`:47`). `plan`/`expected` (`:399-418`). |
| `Bridge/Routes/DriverRoutes.cs` (`PLACES`, `:487`), `PlaceProjection.cs`, `WorkflowPalette.cs`, `Setup/PlacePlan.cs` | change | `detail` and `lands`; step titles; the suggestion's `why`. |
| `Setup/PlaceApply.cs` | keep | It saves no workflow (settlement 4). |
| `.claude/knowledge/twins.md` | change | A twin row for each new field. |

## 5. Rows, in build order

**Every visible row is proved by:**
- page tests;
- stories at 80, 39 and 25rem, in en and zh, light and dark, with the keyboard throughout and measured layout wherever a `Flow` draws;
- the installed window at 1546 and 680 px, wherever the row changes it.

Each row writes its own line of D221's note and its own translations. Rows a–d only add fields; a driver without them leaves them out. "Installed" means the installed window proves the row.

| Row (lanes; after) | Contract | Proof |
|---|---|---|
| **SETUPUX1a** presets from the shipped data (driver, modules) | `delivery` gains `names` and `for`; the workflows are renamed (§2.5); `Table` reads the delivery names; `WORKFLOW_LIST` carries `for`, `order`, `setUp` and `delivery`; `PLACE_PLAN`'s suggestion gains `why`; twin rows | `DriverModuleWorkflowTests` on the fields; each suggestion fixture's `why`; no workflow name equals a delivery name |
| **1b** the effective graph (modules, driver) | `WORKFLOW_CURRENT` takes an optional `route` (`default`, `kind:‹name›`, `landed`) and answers `effective`: the workflow, its source, version or digest, and graph; Current's graph is the one `workflow new --from current:‹repository›` writes, from one handler | the read writes nothing; its identity equals `WorkflowSelection.Resolve`'s for a saved default, an inherited kind, explicit Current and a cleared choice; old callers unchanged |
| **1c** choosing, reviewed (modules) | `WORKFLOW_USE` with `plan: true` writes nothing and answers the reach, what lowers your part, the places changed, that running work keeps its version, and an `expected` token; a press carrying it is refused `moved` where anything moved, and one without it acts as today | plan and moved write nothing; the census keeps the press the person's; the terminal unchanged |
| **1d** one place, and step titles (modules, driver) | `PLACES` takes `detail: {workspace, repository}`, driven or not; every row gains `lands` (`SetupPresets.Match` over the followed workflow's `LandOf`, with its own options); plugin steps carry an optional `title` and `about` | the detail equals its execution readers and probes nothing; a saved version's `lands` equals Current's for the same `land`; an old manifest keeps its kind |
| **1e** the page shell (web; d) | §2.1's head and ⋯; Driving off the page; the folded sections; the first section by kind of place; needs opening sections; state above the mounts; doors to a section and row; *Back*; the imported page | each folded value names its source; opening reads no section; a door opens its row; frame-placed stories; installed at both widths, every kind of place |
| **1f** the tree (web; e) | second lines, the chip, selection kept across pages and searches | a page turn, a search, the chip's count |
| **1g** the choices (web; a, e) | §2.2's choices page, refusals under their parts with their doors, the saved-workflow notice, the chooser, mixed suggestions | a refusal placed and counted; mixed suggestions preselect nothing; the chooser's presses; a key census finds `defaultName.*`, `set.line.*` and `PRESETS` gone; installed |
| **1h** the review and Apply (web; g) | §2.2's review: its order, hold outcomes, *Acceptances* with read-bound ticks, *updating*, the driver's `moved`, the receipt | a changed blob clears its tick; a stale plan offers no *Apply*; partial hold failure per repository; *adoption pending* said as itself; Gates' ticks start empty; installed: a mostly undriven workspace, an outside change mid-review, *Apply*, recovery, retry |
| **1i** Roles on a place, one role editor (web; e) | §2.4's rows and `RoleEditor` | the folds; *Back to* naming the fallback; the two-roles-one-work refusal in the editor; a shared role edited once; installed: edit, visit an account, return, go back to the parent's |
| **1j** This machine's page (web; i) | *All roles*, the diagram, *Suggested* and found changes, reachable lit | a door census and `HelpCoverageTests` re-point every dark Roles door |
| **1k** one drawing (web) | `graphFlow.ts` per §2.3's table: always sectioned, the endings, the holds group and its kinds' words | unit tests over every shipped graph; fixtures equal to `Defaults/workflows/*.json`; measured stories: the release graph down at 80 and 39rem, the rail at 25rem, no crossings, never across |
| **1l** the Workflow section (web; b, e, k) | route rows, the labelled chart, folds with their owners, *Change…*, *Edit steps…*, a workspace's section | a saved default route draws its own graph and says so; choosing a route writes nothing; a step's door opens Landing; installed |
| **1m** the editor's page (web; k, l) | the page, one inspector, keys, picker, *＋*, confirmed removal, the outcome table, no ids, undo, refusal jumps | compose documents and the release workflow by the inspector alone (extending `WorkflowEditor.test.tsx:138`); an unrouted outcome shows; undo; installed by keyboard at 680 px |
| **1n** options, tails, inputs (web; m) | worded options, the method's tail, `pattern`, `target`, the proposed data wire | *A pull request* adds its tail in one change; the release's two data wires proposed; an unmet input refused at its row; unknown fields round-trip |
| **1o** save and choose (web; c, m) | judgement by fingerprint; *Review and save*; name-only save; Change… with the plan of one and its token; *Use here* only on an unchanged draft | out-of-order judgements; no *Use* on a changed draft; an append's reach; stale base; stale and moved *Use*; clear to parent; an older-version copy; *Save* passing while *Use* refuses; installed: an append two places follow, a run bound to the older version |
| **1p** staffing a step (web; i, m) | §2.4's *Taken by* | add a role, return, choose it, save, use; a refused save keeps both forms; a driver regression on the bound role; installed: work starts on it |
| **1q** a run's view (web; k) | §2.3's run view | a release run of each outcome; stories at 300 px, 45% of the window and 25rem; installed: changes sent back |
| **1r** the words (web, glossary; the visible rows) | §2.5's rows, the zh rewording | `names:check --strict` passes, and fails on a planted 交付, 搁置, 委任, 当前, 关卡 or 回到前面的步骤; parity both ways |
| **1s** the installed window (the steward; r) | §3's seven flows on several mostly undriven workspaces, with a refusal, a restart mid-review, a moved plan, an interrupted *Apply*, a release run of each outcome | dated shots, en and zh, both themes, 1546 and 680 px, with run records |
| **1t** lit (web; s) | `daoris.agents.setup` on by default; the old tabs, the Roles mode's list, strip and sections, `WorkflowTab` and `WorkflowStartNode` go | an opener test per old door; the door census; the installed window again |

## 6. What stays open, and what to measure first

**Open**
- **A role that only the steps naming it take.** One role takes a work in a scope, so a role added for one step takes the place's whole work. A role that `RoleFor` never takes needs a driver design of its own.
- **The graph's `end` when steps follow it.** In the release workflow, the line is the end and three steps follow it. The words under *Ends here* wait on the driver: is the end declared, or derived?
- **A review's changes with no `back` wire.** The inspector should say the driver's own sentence for this, which needs a field in the palette.
- **Shared words.**
  - A look's press reads 已审阅, beside the review's *Accept*.
  - The `pull-request` step, *Pull request*, sits beside the delivery *A pull request*.

  Both need a naming pass.
- **A repository's own kinds of task,** where a workspace declares them.
- **Where a place's running work is listed.** No read lists a repository's runs.
- **Unsaved drafts across a restart.** These need a machine-local lifecycle, decided after the losses are measured.
- **Carried from AGENTSPLACE1:** an *Undo* plan built from the before values, and whether the Integration queue sits beside Gates.

**Measure first**
1. **First set-up, unaided, on the installed window, in en and zh.** Adopt an imported repository while unrelated work stays held. Count the presses, the minutes and the wrong turns, and ask whether the person can say what *Apply* starts. Today it is about fifteen presses; the target is five plus the acceptances.
2. **The first open section.** Is another section opened at once? Do the head and Workflow's routes fit the first screen at 1546 and 680 px? Where does *Apply* sit in the review at 680 px?
3. **Reading the release chart cold.** Who accepts? Where do changes go? What does *none* do? Which plugin publishes? What batches the line?
4. **Composing.** From the Merge workflow to the release workflow: time, refusals met and presses.
5. **Staffing.** Add a place's role, give it the work, and say what else it now takes.
6. **Placement.** In measured stories, the release graph runs top to bottom at 80 and 39rem on the 14rem track, and is never laid across.
7. **Word conflicts.** The `names:check --strict` count before and after row r.

Success is doing these without reading a terminal twin or *Details*, while both stay one press away.
