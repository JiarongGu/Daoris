# SETUPUX1: setting work up, composing its workflow and staffing it

Status: accepted by the steward 2026-11-01 (D221), revised after both reads (docs/2026-11-01-setup-ux-reads.md)

**Standing.** This is the synthesis of `2026-11-01-setup-ux-proposal-opus.md` and `2026-11-01-setup-ux-proposal-codex.md`, answering `2026-11-01-setup-ux-brief.md`. It has been revised to carry both reads and the steward's settlement in `2026-11-01-setup-ux-reads.md`. §7 names the settlements it follows.

Web paths start at `src/Daoris.Web/src/`, driver paths at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and modules paths at `src/Daoris.Desktop/Daoris.Desktop.Modules/`. Each file:line was checked in the code on 2026-11-01.

The routes stand. The design adds read fields, one review token (row c) and one plan ask (row e), and no press authorises more than it does today.

**Widths.**
- **W** is the main area at 80rem: 1546 px, with the list open.
- **H** is 39rem: 680 px, with the list at its strip.
- **N** is 25rem, the floor (`design/storyWidths.tsx:13`).

A two-column layout switches on its own width (R7).

## 1. What both found

**The place**
1. **Nothing folds.**
   - The page runs facts (`agents/setup/SetupPlaces.tsx:284-324`), Roles (`:325`), then Driving, Workflow and Sessions (`agents/setup/PlaceSessions.tsx:40-50`).
   - Driving was meant for the head (`docs/2026-11-01-agents-place-design.md:63`).
2. **Lit, the Roles mode's surfaces disappear.**
   - *All roles*, the diagram, *Suggested* and the found staffing changes go.
   - The lit branch mounts only `PlaceRoles` (`agents/AgentsView.tsx:179-209`).
   - `RolesCard` is drawn only in the dark branch (`:210-229`).
3. **A workspace has no Workflow section** (`PlaceSessions.tsx:33,87-91`).
   - Its Setup tab keeps the line, landing, commits, review, opinion, supervisor, remote and rules (`projects/setup/WorkspaceSetup.tsx:108`, `projects/workspace/WorkspaceView.tsx:141-161`).
4. **The tree shows less than `PLACES` answers.**
   - A tree row is a name and its needs (`SetupPlaces.tsx:146-152`), though `PLACES` gives values with sources (`data/wiring.ts:341-354`).
   - An imported row is a name and a checkout (`:365`).
   - A work names its role by id, and its `source` is the work policy's (`Workflows/PlaceProjection.cs:36,137`).
   - Paging loses the selection (`SetupPlaces.tsx:211-221`).

**Set up…**

5. **Suggestions and workspace scope.**
   - Only the first repository's suggestion is read (`agents/setup/PlaceSetUp.tsx:111`, `PlaceSetUpPanel.tsx:33`).
   - A workspace starts from the driven repositories on the page in hand (`SetupPlaces.tsx:336`).
6. **Refusals and changes.**
   - Refusals and changes are one block under every part (`PlaceSetUp.tsx:216-240`), though each refusal names its `part` (`placePlan.ts:26`).
   - `moved` gets the page's own sentence (`PlaceSetUp.tsx:309`).
   - Holds fail as a toast (`PlaceSetUpPanel.tsx:53-60`).
7. **A plan cannot leave how work lands alone.**
   - `PLACE_PLAN` refuses anything outside the delivery table (`Setup/PlacePlan.cs:113-116`).
   - Driving and the ticks ride a delivery (`:157-191`).
   - Every delivery resets a chosen saved workflow to Current (`:169-182`).
   - With *Drive* off, driving is left as it was: a not-driven place stays not driven (`:187`, `:194-201`).
8. **Preset words have two sources.**
   - `agents.setup.defaultName.*` and `.set.line.*` (`PlaceSetUp.tsx:165,168`) copy the shipped `names` and `for` (`Defaults/workflows/merge-after-accept.json:4-8`).
   - The ids are a literal (`placePlan.ts:7`).
   - *Not driven* is English-only (`Workflows/Domain/SetupPresets.cs:46`).
   - `WORKFLOW_LIST` carries no `for`, `order` or `setUp` (`Workflows/WorkflowOperations.cs:67-74`).
9. **One declaration is drawn twice** (`PlaceSetUp.tsx:44-90`, `workflow/place/PlaceGates.tsx:79-196`).
   - Gates pre-ticks both acceptances (`PlaceGates.tsx:90`).
   - A tick ignores the blob it was read at (`placePlan.ts:128-129`).

**The workflow**

10. **One name, two mechanisms.** *Merge after you accept* writes `land` options to Current in *Set up…* (`SetupPresets.cs:25-31`), and is a graph to copy and choose under *Change…* (`workflow/place/ChangeWorkflow.tsx:132-149`).
11. **The chart draws the wrong graph, and a bare graph would lose its cells.**
    - The chart draws Current even where the route names a saved version (`Bridge/Routes/WorkflowRoutes.cs:47`).
    - The start node is a second `Flow` (`workflow/WorkflowStartNode.tsx:178`).
    - Current's chart carries each step's settings, sources, runtime and limit (`workflow/current.ts:26-42`; `WorkflowRoutes.cs:484-491`), and a bare graph carries none of them.
12. **The editor.**
    - It sits at the section's foot, and draws each step twice: in the graph and as an open card (`workflow/edit/WorkflowEditor.tsx:261-279,466-530`).
    - Its unsectioned graph may lay left to right (`design/Flow.tsx:216`).
    - It asks for a typed id (`:316-317`), shows step ids (`:412`) and lists targets as `title (id)` (`:497`).
    - Options appear by field name (`StepOptions.tsx:69,82,86,98`).
13. **Tails, inputs and holds.**
    - A method brings no tail (`graphDraft.ts:186-199`), so the save is refused `land-tail` (`Workflows/Nodes/NodeGraphs.cs:159`).
    - An input needs a hand-made `data` wire (`WorkflowEditor.tsx:510-523`).
    - `quest.holds` and `content.holds` read "A step this page has no words for" (`:65-70`; `locales/en/workflow.edit.json:18-37`).
14. **Endings.**
    - `none` draws nothing.
    - *Finished* hangs off `graph.end` (`workflow/workflowFlow.ts:166-168`), which in the release default is `line`, followed by three steps (`release.json:14,52-56`).
    - The run's state is the end step's (`Workflows/Domain/WorkflowGraphRun.cs:262`).
15. **Choosing has no plan.**
    - *Use here* writes at once (`ChangeWorkflow.tsx:47-55`).
    - `WORKFLOW_USE` checks only the version read (`WorkflowRoutes.cs:415-419`), and ignores fields it does not know.
    - After an edit (`WorkflowEditor.tsx:177`), *Use* still offers the old version (`:337-341`).
    - *Save* does not wait for this draft's judgement (`:154-162,333`).
    - Current's "version" is a digest (`workflow/current.ts:125-126`).

**Staffing**

16. **A place's Roles lists all nine works** (`agents/setup/PlaceRoles.tsx:111-187`) and edits with the whole `RolesCard` (`:173-179`), whose prose opens every time (`agents/roles/Roles.tsx:422-426`).
17. **A role a place adds takes that work for the whole place** (`Roles/RoleCatalog.cs:308-312`). The step's picker does not say so (`workflow/edit/StepOptions.tsx:43-62`).

**The words**

18. **zh borrows words that already mean something else:**
    - 交付 is *submission* (`zh/agents.json:373`);
    - 搁置 is used for held (`:324,405`) and 委任 for appointed (`:363`);
    - 关闭 is used for a role turned off (`zh/settings.ai.json:37`);
    - 当前 sits beside 现行 (`zh/workflow.place.json:28-29`);
    - 打开设置 means Settings (`zh/agents.json:202`).

    **Names are multiplied:** gates are 检查, 关卡 and 关口, and a way back is 回到前面的步骤, 回退 and 退回 (`zh/workflow.edit.json:66,126`).

    **Steps collide:**
    - `handed` and `land` are both 落地 (`zh/workflow.edit.json:19,35`);
    - `pull-request` is 拉取请求, as the delivery is (`:21`; `zh/agents.json:342`);
    - `integrate` is a bare 集成 (`:32`).

## 2. The design

### 2.1 The place page

```
1546 · W · a driven repository: its sections at the left, the chosen route's chart at the right (≥ 64rem)
┌ Agents ────────────────┬──────────────────────────────────────────┬──────────────────────────────────────┐
│ [ Agents │ Setup ]      │ engine  ● Driven               [ Set up… ] [ Hold ] ⋯                           │
│ ⌕ Search places         │ aurora › · Merge after you accept, 2 options of its own · its own tree per session│
│ [ Needs attention · 2 ] │──────────────────────────────────────────┬──────────────────────────────────────│
│ This machine            │ ▾ Workflow      [ Change… ] [ Edit steps… ] │ Release for engine v2, the saved   │
│ aurora        3 of 20   │ ▌ Default work   → Release for engine v2 │ workflow default work follows        │
│   engine                │   Documentation  → Documents workflow v1 │ Before it lands                      │
│   Merge after you accept│   Landed work    → No verification here  │ ╭▶ [ ◯ The work · Builder ]          │
│   api                   │   ▸ Gates          accepted, blob 9f3c1e0│ │  [ ◇ Daoris makes sure · 3 ▸ ]     │
│   Adoption running      │   ▸ Landing        merge into main       │ │  [ ◇ The checks ]                  │
│   site ⚠ landing unread │   ▸ Second opinion Reader reads it       │ ╰─ [ ● Your review ]                 │
│   ▸ 16 imported, not    │   ▸ Your look      off                   │    [ ◇ The landing · merge ]         │
│     driven              │   ▸ Landed route   no verification here  │ After it lands ────────────────      │
│ beta          0 of 4    │   ▸ The line       main · batch 1        │    [ ◇ On the line ]  ⚑ Work done here│
│                         │   ▸ More           commits · heavy gates │    [ ◇ Find a pending release ]      │
│                         │ ▸ Roles    Second opinions: Reader, set  │  release ┆          ┆ none          │
│                         │            here · 7 more as aurora has   │ [ ◇ Record the release ] ◎ No further│
│                         │ ▸ Sessions Restricted · English · helpers│ [ ◇ Copy to its registry ]  step     │
│                         │            ask you                       │    ◎ No further step                 │
└─────────────────────────┴──────────────────────────────────────────┴──────────────────────────────────────┘
 ⋯  Stop driving… · ✓ Its own tree per session · Supervisor: Appoint… · Show in Repositories · Copy the terminal's line

680 · H (list at its strip; N is the same column at 25rem): the chart one press away
│ engine  ● Driven                                     │
│ aurora › · Merge after you accept, 2 of its own      │
│                            [ Set up… ] [ Hold ]  ⋯   │
│ ▾ Workflow               [ Change… ] [ Edit steps… ] │
│ ▌ Default work   → Release for engine v2             │
│   Documentation  → Documents workflow v1             │
│   Landed work    → No verification here              │
│   [ Show the chart ▸ ]                               │
│   ▸ Gates  ▸ Landing  ▸ Second opinion  ▸ Your look  │
│   ▸ Landed route  ▸ The line  ▸ More                 │
│ ▸ Roles     Second opinions: Reader, set here · 8 more│
│ ▸ Sessions  Restricted · English · helpers ask you   │
```

**The first screen.** The head and all three section lines fit the first screen at both widths. At W, the open Workflow section's chart stands in its own column. At H and N, the chart is one press away, and the folds wrap as one row.

| Place | Head | Sections (the first opens on arrival) |
|---|---|---|
| This machine | *9 works · 14 roles*; no *Set up…* | **Roles**, *All roles*, and Sessions as doors to its Settings; *Suggested* and the found changes above |
| Workspace | *3 of 20 driven*, and how its repositories land (*Merge after you accept 2 · A pull request 1 · own 1*); *Set up…* | **Workflow** (routes, kinds, the line's defaults, *Repositories*; Gates stay per repository), Roles (with the supervisor), Sessions (with the rules, row v) |
| Driven repository | as drawn | **Workflow**, Roles, Sessions |
| Not driven | *Not driven · checkout here*, or *Not driven · adoption running*; *Set up…* primary | the same three sections, editable through their existing presses; editing never enables driving |

**The head** keeps UX7's two lines.
- **The pill:** *Driven*, *Held*, *Not driven*, or *Not driven · adoption running*.
- **The parent:** `PageHead`'s `line` is a string (`work/frame/ViewMain.tsx:32`), so the parent's door is the head's `fact` door.
- **Two facts:** how it lands in Set up…'s words, with *n options of its own* (row d's `lands`); and its trees.
- **The acts:** *Set up…* and *Hold* or *Resume*, on their own row at 640 px or less.
- **The ⋯:** `SET_DRIVABLE`, confirmed in place, and `SET_TREES`. Driving's note and the steward-set time stay (`projects/setup/RepositorySetup.tsx:363-367`).

**The sections**
- **Folded lines.** Each folded line gives every value its source (*from aurora*, *from this machine*, *default*; a value set here is unmarked). It reads from `PLACES` alone; row d adds the role names and scope, and the values Workflow and Sessions need.
- **Opening.** The first section opens on arrival. Another opens itself only for a need: a held start opens Roles at that work; a declaration owed acceptance opens Workflow at Gates, and an unreadable landing at Landing (both with row o).
- **Reads.** A folded section reads nothing, and an open one reads as today.
- **State.** `SetupSection` keeps its own open state (`projects/setup/SetupParts.tsx:57-61`), so it gains a controlled `open`. Per place, the open sections, chosen route and step, editor draft and way back are held above the mounts (`placePlan.ts:145-154`), never across a restart.

**The tree**
- **Second lines** say how the place lands, *n of its own*, *Held*, or *Adoption running*. A need adds ⚠ and its first reason.
- **Adoption running.** A not-driven repository whose adoption runs leaves the fold and stands among the driven (row d).
- ***Needs attention*** is a chip with its count.
- **Selection** is kept by place id across pages and searches. A search result opens its workspace. Selecting and expanding a workspace are separate controls.

**Moving**
- **Doors.** Every source mark is a door to that place at the same section and row. `placeOpening` passes both, and the way back (`app/opener.ts:187`).
- **Back.** A page reached from elsewhere shows *← Back to engine · Roles* once.
- **At 680 px,** *Places* opens the list. Choosing returns focus to the head, and Back restores the place, section, step and scroll.

*Chosen:* the head, tree, folded lines and workspace head are Opus's. Codex's are the opening by kind of place, the editable not-driven configuration, the kept selection and the way back.

### 2.2 Set up…

```
1546 · W · Set up engine, the choices
│ aurora › engine  ● Driven                                                                  [ Close ] │
│ ┌ How it lands ───────────────────────────────────┐ ┌ Adoption ──────────────────────────────────┐ │
│ │ (•) Keep how it lands: Release for engine v2    │ │ ☑ Ask its session to adopt Daoris          │ │
│ │ ( ) A pull request               [ Suggested ]  │ │   What it builds  (•) Not said             │ │
│ │     Pushed, and a plugin opens a pull request   │ │   ( ) A library  ( ) A web app or service  │ │
│ │     for you to merge.                           │ │   ( ) A desktop app or game  ( ) Docs…     │ │
│ │     Suggested: a landing plugin serves its      │ ├ Driving ───────────────────────────────────┤ │
│ │     remote's platform.                          │ │ ☐ Drive all its work                       │ │
│ │ ( ) Merge after you accept  ( ) A branch to push│ │   Off: driving stays as it is. engine is   │ │
│ │ ( ) Integrated into the line  ( ) Not driven    │ │   driven; for api, only its adoption quest │ │
│ │ ☐ A second opinion before it lands              │ │   would run.                               │ │
│ │ ☐ Your look before it lands                     │ └────────────────────────────────────────────┘ │
│ └─────────────────────────────────────────────────┘                                                │
│ The plan: 3 changes · nothing stops it                                 [ Review the plan ] [ Close ] │

680 · H · the choices in one column; a refusal under its part, with its door
│ api › Set up api                             [ Close ] │
│ How it lands                                           │
│ (•) Merge after you accept           [ Suggested ]     │
│ ☑ A second opinion before it lands                     │
│     ⚠ A second opinion names the agents that read      │  ← the driver's sentence
│       api's work, and none is set for it…              │
│       [ Name them: Workflow › Second opinion ]         │  ← repairs, then returns here
│ Adoption  ☑ Ask its session to adopt Daoris            │
│ Driving   ☐ Drive all its work                         │
│ The plan: 2 changes · 1 stops it   ↑ How it lands      │
│ [ Review the plan ]                                    │

1546 · W · Set up aurora, the review (H keeps this order in one column)
│ ← Back to the choices                    Apply to 3 repositories                                    │
│ What changes                                       │ What it starts                                │
│   How it lands  merge into main → pull request     │   Adoption: 3 quests, one per repository      │
│                 engine, site                       │   Driving: unchanged; docs stays not driven   │
│   A tree each   off → on          engine           │ Your part  ⚠ [the driver's lowering sentence] │
│   Its workflow  Release for site v2 → Current  site│ Work it releases                              │
│   [the driver's reach sentences; "docs: its line   │   site: Fix the login · Add the docs page     │
│    could not be read: …"]                          │   [ Hold it after driving ] sets the hold now │
│ What stops it   docs: [the driver's refusal]       │   and reads the plan again                    │
│   ↑ How it lands   [ Leave docs out ]              │                                               │
│ Acceptances   engine · main a1b2c3d · blob 9f3c1e0   ▸ Read the declaration                         │
│   ☐ Accept for landed verification: changed landed gates stop verification until accepted           │
│   ☐ Accept for integration: integration goes on under its last accepted blob until then              │
│ ▸ In a terminal                       [ Apply to 3 repositories ] (not offered while something stops it) │
```

*Set up…* replaces the sections and keeps the head. Its two pages share one draft and one plan, and moving to the review writes nothing. A set-up seeded by Ask Daoris (`app/opener.ts:188-193`, `agents/AgentsView.tsx:138-146`) opens on the review.

**How it lands** offers four kinds of choice:
- ***Keep how it lands***, read from row d's `lands`. Under it, `PLACE_PLAN`'s keep ask (row e) writes no landing and no workflow choice, and allows driving and the ticks. It is preselected where the landing was chosen or a saved workflow is followed.
- **The data's set-up deliveries**, each with `delivery.names` and `delivery.for` (row a). The suggestion comes first, its `why` worded by code with the driver's sentence beneath. Any of them sets a chosen saved workflow back to Current, and says so in the plan's own change.
- ***Not driven***, from a chrome key.
- **The second-opinion and look ticks.**

**Adoption and Driving**
- **Adoption** is a tick. *What it builds*, under it, shapes only the adoption quest.
- ***Drive all its work*** captions the plan's before and after for each repository. Off, driving stays as it is: a not-driven place stays not driven, and only its adoption quest runs.

**The plan's line and refusals**
- **The plan's line** counts the changes and what stops the plan, with a jump to each.
- **A refusal** stands under its part (`PlaceRefusal.part`), whole, with the door its code names. A not-driven place is editable, so a door for *no reviewers* (`Setup/PlacePlan.cs:252`) or *no review environment* (`:271`) opens the fold that repairs it, and its *Back* returns with the plan read again.

**A workspace's chooser** offers *All · Driven · Not driven · None*, a count and a search. It reads the workspace's whole list with each repository's driving (row d's `members`). Where suggestions differ, nothing is preselected, and each choice names the repositories it suits.

**The review**, in order:
1. The changes, by part, each identical change once with its repositories, together with the driver's reach sentences and *its line could not be read* (`PlaceSetUp.tsx:248-255`).
2. What stops the plan, linked back. It offers a change of selection, never a partial apply.
3. What lowers your part, and what the plan starts.
4. The work it releases, with *Hold it after driving*: an immediate `SET_HOLD`, outside the receipt, with each repository's outcome beside it.
5. The acceptances, drawn by the one *Acceptances* molecule Gates draws.
   - The text shows once per read.
   - Ticks start empty, keyed by repository, purpose, line and blob, and are cleared with a reason on change.
   - Each says its standing: changed landed gates stop verification; integration goes on under its last accepted blob.
6. The terminal line, folded, and *Apply n*.

**Apply**
- **While a plan is read,** the last stays, marked *updating*, and *Apply* waits.
- ***Apply*** (`PLACE_APPLY`) carries the digest and the chosen acceptances.
- **`moved`** is said in the driver's words, and the plan is read again.
- **The result** says each operation's outcome, and a door opens the adoption quest. *Configuration applied; adoption pending* is said as itself.

*Chosen:* Codex's two pages, over Opus's plan beside the choices (settlement 2). Opus's refusal doors, `why` and chooser presses.

### 2.3 Composing a workflow

**Routes are a selection.** A list with one chosen row, not radios. Choosing a row picks the chart and writes nothing.
- Default work and each kind carry *Change…* and *Edit steps…*.
- Landed work keeps `WORKFLOW_LANDED`, with today's `LandedRoute` choices and their acceptance consequence in the *Landed route* fold (`settings/repository/LandedRoute.tsx:52,99`): a verification workflow, *No verification here*, or the workspace's.
- *Change…* is never offered for landed work, since `WORKFLOW_USE` excludes it (`WorkflowRoutes.cs:392`; `WorkflowSelection.cs:277`).
- Landed *Current* reads *No verification here* and draws no chart (`WorkflowSelection.cs:287`).
- *Verify…* shows only once a landed pipeline is accepted (`WorkflowStartNode.tsx:78`).

**The chart is the chosen route's effective graph** (row b), with each step's cell as today.
- Its label reads *Current, read from the rules as they stand* or *‹name› vN, the saved workflow ‹route› follows*.
- The head's part sentence and the unregistered note stay (`WorkflowTab.tsx:39-66`).

**The folds**, each with its owner:
- **Gates**: *Acceptances*.
- **Landing**, the workflow's: `SET_LANDING` under Current, the editor at `land` under a version.
- **Second opinion** and **Your look**: today's rows (`RepositorySetup.tsx:271-285`).
- **Landed route**.
- **The line**, the line's own (D215).
- **More**: commits, and the heavy gates, moved from Sessions (`RepositorySetup.tsx:214`).

***Use the workspace's*** clears the repository's default alone. Its plan says, in the driver's words, what the kinds keep: while the repository maps a kind of its own, its default work follows Current, not the workspace's (`WorkflowSelection.cs:220-225`; `Settings/WorkflowKey.cs:38`). Clearing a kind's mapping is that kind row's own reviewed press.

***Edit steps…*** opens the chart's graph. For Current, that is an editable copy. Current's merge graph ends at *The landing*, because the compiler adds *On the line* only for an integration (`Workflows/Domain/WorkflowWords.cs:340-357`).

**One drawing**, `workflow/graphFlow.ts`, draws the chart, the editor and a run.
- It always passes sections (*Before it lands*, *After it lands*, or *Steps*), so `Flow` draws it top to bottom or as the rail, never across (`design/Flow.tsx:216`).
- Its compact 14rem track replaces `WORKFLOW_TRACK`'s 19rem (`workflow/workflowFlow.ts:34`): a step's title and one fact, with its cell on selection.
- A two-way branch needs 2 × 14 + 3 = 31rem. That fits H's 36rem but not N's 23.5rem, so N draws the rail.

| Thing | At 80 and 39rem | At 25rem, the rail | Edited by |
|---|---|---|---|
| A way on (`after`) | a solid edge down; *＋* between steps in the editor | numbered rows | *Once done →*; *＋* |
| A branch (`when`) | dashed edges side by side under the step, labelled by outcome; the card says its routes in words | `├ release → 7 Record → 8 Copy` / `└ none → No further step` | *When it comes to ‹outcome› →* |
| A loop back (`back`) | its own lane at the left, labelled *back*, crossing no card | `↩ If you ask for changes: back to 1` | *If you ask for changes → Back to ‹step›* |
| A failure | short dashes to the target | `✕ If it fails → …` | *If it fails →*; *The run stops* is no `failed` wire, never an inserted `stop` |
| The configured end | ⚑ *Work done here*: the run's state is read there; where steps follow, its foot says *Steps after it still run* | the same | ⋯ *Work is done here* |
| No route on | ◎ *No further step*, a drawing-only `FlowNode`, never saved and never a success | the same | *No further step* as a target |
| *Finished* | only from a run's record | — | — |
| `work.requests`, `quest.holds`, `content.holds` | one group, *Daoris makes sure · n ▸*, owned by `graphFlow` (`Flow` stacks only three or more like nodes, `design/flowLayout.ts:61`; `documents` holds two); a refusal opens it | one row | each member's row |

```
Change…, inside the section, in place of the chart (680 · H: the tabs, a select, then the preview)
│ Change the workflow default work follows  [ Presets │ Saved here │ Current ]                  [ Close ] │
│ ┌ Saved here ──────────────────────┐ ┌ Release for engine v3 ──────────────────────────────────────┐ │
│ │ Release for engine  v3  ● here   │ │ [the drawing]  Chosen at: aurora (kind docs)                │ │
│ │ Docs for aurora     v1  at aurora│ │ Your part: [the driver's comparison] · running work keeps   │ │
│ │                                  │ │ its own version                                             │ │
│ │                                  │ │ [ Use here ]   [ Edit steps from this… ]                    │ │
│ └──────────────────────────────────┘ └─────────────────────────────────────────────────────────────┘ │
```

**Change…** has three tabs.
- ***Presets*** shows the shipped workflows with their `names` and `for`. A preset is not yet stored, so it offers *Save a copy…* (a name only) and *Edit steps from this…*. Once saved, the copy offers *Use here*.
- ***Saved here*** shows each workflow's newest version and where it is chosen. Older versions can be read and copied.
- ***Current*** is labelled live, with its digest only under *Details*.

**Selecting a candidate** sends `WORKFLOW_USE {plan: true}` only where `WORKFLOW_LIST` announces it (row c).
- The plan answers the reach, the lowering, the places changed, that running work keeps its version, and an `expected` token that *Use here* carries.
- Unannounced, the preview shows the drawing, and *Use here* is today's press.
- A token that no longer holds is refused `moved`. `stale` stays for presses without one: `moved` joins it rather than replacing it.

*Chosen:* Codex's candidate and preview, over Opus's folding of Change… into Set up… (settlement 4).

```
1546 · W · Edit steps: graph and inspector side by side while the page holds 64rem
│ aurora › engine › Workflow › Edit steps · from the Merge workflow           ↶ Undo  1 to put right ▾  [ Close ] │
│ ┌ The steps ───────────────────────────────────┐ ┌ Find a pending release ───────────────── by Local release ┐ │
│ │ Before it lands                              │ │ [the plugin's about]                                      │ │
│ │  ╭──▶ [ ◯ The work · Builder ]               │ │ What happens next   Goes to                  Carries      │ │
│ │  │    [ ◇ Daoris makes sure · 3 ▸ ]  ＋       │ │   release      → [ Record the release   ▾ ] its release  │ │
│ │  ╰─back [ ● Your review ]                    │ │   none         → [ No further step      ▾ ] nothing      │ │
│ │       [ ◇ The landing · merge ]              │ │   If it fails  → [ The run stops        ▾ ]              │ │
│ │ After it lands ───────────────────────────── │ │ ▸ More ways on    ▸ Details: find · local-release/find v1 │ │
│ │       [ ◇ On the line ]  ⚑ Work done here    │ │ ⋯  Work is done here · Remove this step…                  │ │
│ │       ┏ ◇ Find a pending release ┓ selected  │ └───────────────────────────────────────────────────────────┘ │
│ │   release ┆              ┆ none              │                                                               │
│ │  [ ◇ Record the release ]  ◎ No further step │                                                               │
│ │  [ ◇ Copy to its registry ] ◎ No further step│                                                               │
│ │ [ ＋ Add a step ]                             │                                                               │
│ └──────────────────────────────────────────────┘                                                               │
│ Name [ Release for engine           ]  ▸ Details     These steps hold.                      [ Review and save ]  │

680 · H · the drawing whole; a step opens a drawer   400 px · N · the rail; a step opens the same drawer
│ engine › Edit steps     ↶  1 ▾  [ Close ]│┌ Find a pending release ─ ✕ ┐  │ 5 ◇ On the line · ⚑            › │
│ ╭▶ [ ◯ The work ]                        ││ release → [ Record… ▾ ]      │  │ 6 ◇ Find a pending release     › │
│ │  [ ◇ Daoris makes sure · 3 ▸ ]         ││   Carries its release        │  │   ├ release → 7 Record → 8 Copy  │
│ ╰─ [ ● Your review ]                     ││ none    → [ No further… ▾ ]  │  │   └ none → No further step       │
│    [ ◇ The landing · merge ]             ││ If it fails → [ stops ▾ ]    │  │ 7 ◇ Record the release         › │
│ After it lands ───────────────────────── ││ ▸ More ways on  ▸ Details    │  │ 8 ◇ Copy to its registry       › │
│    [ ◇ On the line ] ⚑                   ││ [ Done ]  ⋯                  │  │ [ ＋ Add a step ]                 │
│ ┏ ◇ Find a pending release ┓             │└──────────────────────────────┘  │ Name [ … ] [ Review and save ]   │
```

**The editor** is its own page in the main area, under the place's head.

**The inspector** shows one step at a time: beside the graph at W, and as a `Drawer` at H and N (`design/ui.tsx:940`). It says what the step does, who acts (`reachBy`), and, for a plugin step, the manifest's `title` and `about` (row f).

**What happens next** is a table of outcomes from the palette's `leaves`:
- *Once done*, with *＋ also to…*;
- *If you ask for changes* (on a look, *If you say not yet*);
- *If it fails*;
- every declared outcome, routed or not.

A target is a later step, *Back to* an earlier one, *No further step*, *The run stops*, or *＋ A new step…*. `trigger` and hand-made wires sit under *More ways on*. A review is never drawn as a plugin's decision.

**Inputs are proposed**: routing to a step proposes the `data` wire beside its `when` or `after`.
- The proposal prefers the output of the step it is routed from. Where that step has no fitting output and two others do, the row asks.
- In the release workflow, *Record* takes *Find*'s output, and *Copy* takes *Record*'s (`release.json:27,34,39`).
- The wire shows under *Carries* and in the save review, can be changed, and never merges into the condition.

**Options** read in chrome words (`workflow.edit.option.‹kind›.‹name›`, with the raw name as the fallback), and keys sit under *Details*. `land`'s method is five choices, each bringing its tail. `pattern` is previewed. `target` lists the lines.

***Taken by*** appears only on the work and the second opinion (§2.4).

**No ids.** The workflow's id comes from its name, as role ids do (`agents/roles/Roles.tsx:26-31`). A name with no ASCII letter or digit takes the id it was copied from (`release` → `release-2`), else `workflow`, numbered until unique. Step ids sit under *Details*, and *Goes to* lists titles.

**Refusals, removal and undo**
- **Refusals** stand at their row, gathered under *n to put right ▾*.
- ***Remove…*** is confirmed in place and never bridges the gap.
- **Undo** is ↶, or Ctrl+Z in the drawing. ↑↓, Enter and Escape move between the steps and their inspector.

**The picker** (*＋* or *Add a step*) is searchable and grouped:
- *The work*;
- *You*;
- *Checks*;
- *Landing*;
- *Other repositories*;
- each plugin, by its steps' titles;
- *Other*, which holds the door to Plugins where none is installed.

**Saving and choosing**
- **The judgement** carries its draft's fingerprint, and an older answer is discarded.
- ***Review and save*** needs this draft's holding judgement. It says the changed steps, the involvement, and every place an append reaches, then offers *Save as a new workflow* or *Save vN*.
- **An append's consent** names exactly the scope keys the driver returned (`Workflows/WorkflowOperations.cs:642-646`), and resets when that set changes.
- ***Use here…*** shows only while the draft equals its saved version, and reads the plan of one. *Start from* goes.

*Chosen:* Opus's page, inspector and picker. Codex's one open form, the drawer, *Carries*, a removal that never bridges, and judgement by fingerprint. Opus's rail at 25rem, keeping every branch and return in words.

```
A run's view: the session's side bar · Workflow · 300 px
│ Release for engine v1 · chosen here    │
│ At Your review · waiting on you        │
│ ✓ The work  ✓ Daoris makes sure · 3    │
│ ● Your review  [ Accept ] [ Ask for changes… ]
│   The landing · On the line  not reached│
│   Find a pending release     not reached│
│   ├ release → Record, Copy             │
│   └ none → No further step             │
```

**A run's view** (`workflow/WorkflowRunView.tsx`) uses the module.
- Its head names the bound version and how it was chosen.
- A decision says what it came to.
- Ruled-out routes read *route not taken*, apart from *not reached*, *skipped* and *failed*.
- *Finished* comes from the record.
- The run answers no return count (`WorkflowRoutes.cs:650-661`), so none is drawn.

### 2.4 Staffing

```
1546 · W · engine › Roles
│ ▾ Roles                                                                                      [ All roles ] │
│   Second opinions  Reader · set here         Codex · gpt-5 · high · cooling 12 min                         │
│                                                                    [ Edit ]  [ Back to aurora's… ]         │
│   Building         Builder · this machine    Claude Code · opus · high (+1) · a start is held              │
│                                                                    [ Give engine its own… ]                │
│   Design           Builder · this machine: design falls to building           [ Give engine its own… ]     │
│   ▸ 6 more, as aurora has them: chat, helping, partnering, brainstorms, intake, Ask Daoris                 │
│   Appointed        Supervisor: aurora's covers engine                    [ Appoint for engine… ]           │

680 · H · rows stacked, the editor in place           1546 · W · This machine › All roles
│ ▾ Roles                        [ All roles ] │     │ ▾ All roles [ Everywhere │ Here │ Workspaces │ Repos ]  ⌕  │
│ Second opinions · Reader · set here          │     │   [ List │ Diagram ]                                    │
│ ┌ Edit Reader · engine ────────────────────┐ │     │   Placed on this machine                                 │
│ │ 1 [ Codex ▾ ] [ gpt-5 ▾ ] [ high ▾ ] ↑↓✕ │ │     │   Builder   building, design  Claude Code · opus  [Edit] │
│ │ 2 [ Claude Code ▾ ] [ other… ] [ — ▾ ]   │ │     │   Reader    second opinions   Codex · gpt-5       [Edit] │
│ │ [ ＋ Add a choice ] ⓘ How choices are used│ │     │   Placed elsewhere                                       │
│ │ ▸ Edit role   It takes engine's second   │ │     │   Reader · engine  second opinions  [ Open at engine ]   │
│ │ opinions.  [ Save ] [ Cancel ]  ⋯ More   │ │     │   [ ＋ Add a role ]  [ Restore all role defaults ]       │
│ └──────────────────────────────────────────┘ │
│ ▸ 7 more, as aurora has them                 │
```

**Rows** come from `ROLE_STRUCTURE` (`RoleFor` alone). Their folded line comes from row d.
- **What is a row.** A work set here, differing from the parent's, or with a need is a row; the rest fold.
- **What a row says.** The role, its place, its first choice, and that choice's last reading.
- **Acts.** A parent's role offers *Give ‹place› its own…*. One set here offers *Edit* and *Back to ‹parent›'s…*, confirmed with the fallback named.
- **Under *More role acts ⋯*.** *Off*, *Clear staffing* and *Restore role defaults*, kept as three acts.
- **A shared role** is edited once.

**One `RoleEditor`**, split from `Roles.tsx:320-445`, serves a place, *All roles* and *Taken by*.
- The place is fixed, the id is generated, and the role's scope and revision travel to its save.
- The choices come first, from the agent's offers, keeping the typed *other…* model and effort.
- *Edit role* is folded, with the reader's language first.
- The prose sits behind one ⓘ, and one sentence of consequence stands before *Save*.
- No account is set.

**This machine's page**
- ***All roles*** lists every role once, by scope, as a *List* or a *Diagram*. A role placed elsewhere is read-only there, with *Open at ‹place›*.
- ***Suggested*** is one line opening `SuggestedChoicesCard` (one `SET_ROLES`, judged whole). The found changes are a line beside it.

**A step's role** offers the roles that take the work and cover this place, each named with its place. It also offers *＋ Give engine its own role for ‹work›…*, or, where engine has one, *Edit Reader · engine…*.
- The inline editor says the role then takes all of engine's ‹work›, not only this step's (`RoleCatalog.cs:308-312`).
- `SET_ROLE` saves at once, and the step's choice stays in the draft. Both saves are said.
- A refused save keeps both forms.

*Chosen:* Opus's folded rows, sentence and *design falls to building* row. Codex's single edit for a shared role, opening an existing role, and scope, revision and typed alternatives.

### 2.5 The words

**The rename (settlement 3).** The shipped workflows are renamed. The deliveries keep today's names, which the head and *Set up…* say, and a workflow's name ends in *workflow* (…工作流). Each record's `delivery` gains `names` and `for`, with today's strings, and its top-level `names` become the workflow's.

**No name contains another's.** No workflow, delivery or step name contains another's whole name, in either language. So:
- `pull-request` leaves 拉取请求;
- `integrate` leaves a bare 集成;
- `handed` leaves 落地, which `land` keeps.

The part is *How it lands*: *delivery* is also the land node's record, and 交付 is *submission*. A way back is *Back to…* (退回到…), beside the glossary's `send back`.

| Thing | en | zh | Replaces | Glossary |
|---|---|---|---|---|
| The mode; a place's set-up | Setup; Set up… | 配置; 配置… | 打开设置 | amend `setup` (`glossary.json:701`); `set-up` (搭建) stands |
| A place | place | 位置 | 范围 (`zh/settings.ai.json:79`) | new; avoid 范围 |
| Set up…'s first part; keeping it | How it lands; Keep how it lands | 落地方式; 保持现有落地方式 | Delivery, 交付 | new; avoid 交付 |
| A delivery | Merge after you accept · A branch to push · A pull request · Integrated into the line · Not driven | 你采纳后合并 · 留给你推送的分支 · 拉取请求 · 集成到主线 · 未驱动 | the same names on workflows; `defaultName.*` | the data's `delivery.names`; *Not driven* a chrome key |
| A shipped workflow | preset; Merge, Automatic merge, Documents, Branch, Hosted merge, Integration, No-press integration and Release workflow | 预设; 合并, 自动合并, 文档, 分支, 托管合并, 集成, 免按键集成, 发版工作流 | 默认工作流; "Pull request workflow" | new `preset`; the data's `names`; 发版, since 发布 is a quest's (`:520`) |
| Renamed steps | `pull-request`: Completed on the host; `integrate`: The integration request; `handed`: Handed on | 在托管平台完成; 集成请求; 已交接 | 拉取请求; 集成; 落地 | new |
| The workflow of the rules | Current; *from Current* | 现行; 来自现行 | 当前; "from Merge workflow (Current)" | new; avoid 当前 |
| A saved version | version, v2 | 版本 | a digest shown as a version | amend `version` (`:644`) |
| Choose; compose | Change… · Use here · Use the workspace's; Edit steps… | 更改… · 在此使用 · 沿用工作区的; 编辑步骤… | 在这里使用; 编排工作流, 编排步骤… | `Use here` (`:584`); `compose` (`:581`) keeps 编排 |
| A step; way on; decision; result | step; way on; decision step; outcome | 步骤; 去向; 决策步骤; 结果 | *wire*, a remote's word (`:607`) | new; `outcome` (`:578`); avoid 节点, 连线, 接线 |
| Back to an earlier step | Back to … | 退回到… | 回到前面的步骤, 回退 | beside `send back` (`:576`) |
| A review's rejection | Ask for changes (on a look, Not yet) | 要求修改 (查看: 还没有) | 你拒绝时 (`zh/workflow.edit.json:51,64`) | each kind keeps its own word |
| The person's steps | Your review; Your look | 你的审阅; 你的查看 | *Needs a look*, 需要查看 | new; the filter becomes *Needs attention*, 需要留意 |
| Declared gates | gates | 检查项 | 检查, 关卡, 关口 | new; avoid 关卡, 关口 |
| End; no route; done | Work done here; No further step; Finished | 工作在此完成; 无后续步骤; 已完成 | "ends here" over later steps; *Finished* after `end` | amend `ends here` (`:583`); new; avoid 结束 |
| No landed verification | No verification here | 这里不做验证 | Current on the landed row | stands (`workflow.start.noVerification`) |
| The pre-landing group | Daoris makes sure · n | Daoris 核实 · n | "no words for" | new |
| A place's state | Not driven · adoption running | 未驱动 · 采用进行中 | — | under `adopt` (`:626`) |
| Hold; appointed; off | Hold, Held; Appointed; Off | 暂停, 已暂停; 任命的; 停用 | 搁置; 委任; 关闭 | `hold`, `appoint`, `turn off` |
| Staffing | staffing; Clear staffing | 人选; 清除人选 | 清除顺序 | new |
| Adoption; driving | Ask its session to adopt Daoris; Drive all its work | 请它的会话采用 Daoris; 驱动它的全部工作 | 为智能体配置; 通用驱动 | `adopt` |
| Plan; Apply | plan; Apply | 计划; 应用 | — | new `plan`; amend `apply` (`:679`) |
| Landing | landing | 落地 | a meaning from before LANDNODE (`:554`) | amend: the five methods |
| A plugin's step | its manifest `title` | as written | `publish`, a quest's (`:520`) | amend `publish`; the example copies into its own registry folder (`examples/plugins/local-release/README.md:25-29`), never an outside registry |

Plugin names, outcome ids, authored names, paths and refusal sentences stay as written (`.claude/knowledge/translation-parity.md`).

## 3. The flows

Counts start on the Agents place, and count every press, page and confirmation.

1. **First set-up.**
   - **Presses:** *Setup* → *16 imported, not driven* → api → *Set up…* (how it lands preselected with its why; Adoption ticked; Driving off) → *Review the plan* → tick the acceptances on offer → *Apply 3 changes* (`PLACE_APPLY`).
   - **The count:** six presses on two pages, plus the acceptances. Today it is about fifteen across six screens (`docs/2026-11-01-agents-place-design.md:18`).
   - **After:** a door opens the adoption quest, and the head and tree read *Not driven · adoption running*. With *Drive all its work* ticked, they read *Driven*.
   - **Several repositories:** aurora → *Set up…* → *Not driven* (16) → choose → review → *Apply*.
   - **A seed from Ask Daoris** opens the review first.
2. **A change.**
   - **One row:** Workflow → Landing *Edit* → *Save* (`SET_LANDING`).
   - **Keeping the workflow:** *Set up…* → *Keep how it lands* → tick a second opinion → review → *Apply*.
   - **A workflow:** *Change…* → *Saved here* → *Docs for aurora v1* → the plan of one → *Use here*.
   - **The workspace's:** *Use the workspace's* → its plan says what the kinds keep → *Use here*.
3. **Staffing.** engine → Roles → Building *Give engine its own…* → Claude Code · opus · high, then Codex · gpt-5. It says "It takes engine's building from Builder, this machine." → *Save* (`SET_ROLE`).
4. **An appointment.** aurora → Roles → Supervisor *Appoint…* → the owner's *Appoint*, unchanged.
5. **The workflow.** Workflow → *Edit steps…* → a change → *Review and save* → *Save v3*. The append names the places it reaches, and running work keeps its version. Where the route followed another workflow: *Use here…* → the plan → *Use here*.
6. **A release workflow with a decision.** Both ways start from the shipped Merge workflow, which holds *On the line*.
   - **The preset:** *Change…* → *Presets* → *Release workflow* → *Save a copy…* as *Release for engine* → the plan of one → *Use here*. Six presses.
   - **Composed:**
     1. *Change…* → *Presets* → *Merge workflow* → *Edit steps from this…*.
     2. *On the line* → *＋* → *Local release › Find a pending release*. Both outcomes read *No further step*.
     3. On the release row: *＋ A new step…* → *Record the release*, its input proposed from *Find*.
     4. *＋* → *Copy to its registry*, its input proposed from *Record*.
     5. *Your review* → *If you ask for changes* → *Back to The work*.
     6. Name it → *Review and save* → *Save as a new workflow* → *Use here…* → *Use here*.

     About eighteen presses. Without the plugin, *Other* holds the door to Plugins.
7. **Staffing a step with a role the place adds.**
   1. *＋* above *Your review* → *Second opinion* → *Taken by* → *＋ Give engine its own role for second opinions…*.
   2. Codex · gpt-5 · high. It says "It will then take all of engine's second opinions, not only this step's." → *Save* (`SET_ROLE`).
   3. *Reader · engine* is chosen in the draft → *Review and save* → *Save* → *Use here*.

## 4. What moves, merges and goes

New molecules take props and send presses. Organisms hold the hooks: `PlaceSetUpPanel`, the Workflow organism over `useWorkflowTab`, and the editor's organism over `bridge/repositories/workflowEdit.ts`.

| File | Fate | What |
|---|---|---|
| `agents/setup/SetupPlaces.tsx` | change | The facts (`:284-324`) move into `PageHead`; the slots become three sections; the tree gains its second lines, the chip, the kept selection and the adoption rows. |
| `agents/setup/PlaceSessions.tsx` | split | Driving (`:40`) moves to the head. The Workflow mount (`:41-47`) becomes the Workflow organism, also for a workspace and a not-driven place. Sessions stays, without the heavy gates. |
| `agents/setup/PlaceRoles.tsx` | change | Rows for the works that differ. `RolesCard editorOnly` (`:173-179`) becomes `RoleEditor`. |
| `agents/setup/PlaceSetUp.tsx` | split | `SetUpChoices` and `SetUpReview`. `Declaration` (`:44-90`) merges into `Acceptances`, and the page's own `moved` (`:309`) goes. |
| `agents/setup/PlaceSetUpPanel.tsx`, `placePlan.ts` | keep | `PRESETS` (`:7`) goes. A tick's key gains its line and blob. The keep ask is added, and a seed opens the review. |
| `agents/setup/placePage.ts`; `projects/setup/SetupParts.tsx` | add; change | The page's state for each place; a controlled `SetupSection`. |
| `agents/AgentsView.tsx` | keep | The lit branch (`:179-209`) mounts *All roles*, the diagram, *Suggested* and the found changes. The dark branch (`:210-229`) goes at the cutover. |
| `agents/modes.ts`, `AgentsModes.tsx`, `roles/RoleSections.tsx`, `roles/StructureSections.tsx`'s lists | remove at the cutover | A stored `roles` mode reads as `setup`. |
| `agents/roles/Roles.tsx`, `RolesView.tsx` | split | The editor (`:320-445`) moves to `RoleEditor.tsx`. `RolesCard` stays as *All roles*. |
| `workflow/graphFlow.ts` | add | Absorbs `workflowFlow.ts:158-171` and `WorkflowEditor.tsx:103-114`, with the ◎ node and the 14rem track. `WorkflowChart` and `WorkflowRunView` draw through it. |
| `workflow/WorkflowStartNode.tsx`, `WorkflowTab.tsx` | remove at the cutover | Routes become rows, and the head's words become the chart's label. |
| `workflow/place/PlaceWorkflowView.tsx`, `ChangeWorkflow.tsx` | re-laid | Routes, chart and folds. Change… gains tabs, a preview, the announced plan and *Save a copy…*. |
| `settings/repository/LandedRoute.tsx`, `workflow/place/PlaceGates.tsx` | keep | Mounted in the *Landed route* and *Gates* folds. `PlaceGates` becomes `Acceptances`, with ticks empty (`:90`). |
| `workflow/edit/WorkflowEditor.tsx` | split | `StepGraph`, `StepInspector` (beside, or in a `Drawer`), `StepPicker` and `SaveReview`. *Start from*, the id field, the card list and `WayForm` go. |
| `workflow/edit/StepOptions.tsx`, `graphDraft.ts` | keep | Worded fields, tails, *Taken by*; `insertAfter`, `setExit`, `withTail`, `proposeData`, undo, a fingerprint; lossless round trips. |
| `workflow/edit/answers.ts`, `bridge/repositories/workflowEdit.ts`, `editFixtures.ts` | keep | Rows a–f's fields, a judgement matched to its request, and the consented scope keys. The fixture equals `release.json`. |
| `projects/useWorkflowTab.ts`, `app/opener.ts` | change | The read takes a route. `placeOpening` (`:187-201`) passes a section, a row and the way back. |
| `projects/workspace/WorkspaceView.tsx`, `projects/setup/WorkspaceSetup.tsx` | move (row v), then change at the cutover | Remote (Wire/Unwire, undeclared machines) becomes a row of Repositories' workspace *Details*. The rules move to Sessions, the supervisor to Roles, the line's defaults to Workflow. The tabs go after a census. |
| `design/Flow.tsx`, `flowLayout.ts`; `design/ui.tsx` `Drawer` | unchanged; reused | ◎ is an ordinary node, and sections and tracks exist (`Flow.tsx:89-97`). The `Drawer` holds the inspector at H and N. |
| stories, locales, `glossary.json` | change | The frame's own placement replaces the 280 px split. §2.5. |
| `Defaults/workflows/*.json`, `WorkflowDefaults.cs`, `SetupPresets.cs` | change | `delivery.names` and `delivery.for`. `Table` (`:47`) reads them. |
| `WorkflowOperations.cs`, `Bridge/Routes/WorkflowRoutes.cs` | change | Preset fields (`:67-74`). `route`, `effective` and the cells (`:47`). `use` announced on the list. `plan` and `expected` (`:399-433`). |
| `Bridge/Routes/DriverRoutes.cs` (`PLACES`, `:487`), `PlaceProjection.cs` | change | `detail`, `lands`, role names and scope, the summary values, `adoption` and `members`. |
| `Setup/PlacePlan.cs`, `Setup/PlaceApply.cs` | change | The keep ask, and `why` as a code. Apply writes a subset of what it writes today, and saves no workflow. |
| `examples/plugins/local-release/plugin.json`, the manifest contract, `WorkflowPalette.cs` | change | Each step gains an optional `title` and `about` (`plugin.json:11-44`). |
| `.claude/knowledge/twins.md` | change | A twin row for each new field. |

## 5. Rows, in build order

**Proof for every visible row:**
- page tests;
- stories at 80, 39 and 25rem, in en and zh, both themes, keyboard throughout, with layout measured wherever a `Flow` draws;
- the installed window at 1546 and 680 px where the row changes it ("installed").

Each row writes its line of D221's note and its own translations, checked against row g's glossary rows. Rows a–f add fields only: a driver without them leaves them out, and the page falls back.

| Row (lanes; after) | Contract | Proof |
|---|---|---|
| **SETUPUX1a** presets from the shipped data (driver, modules) | `delivery.names`, `delivery.for`; workflows renamed (§2.5); `Table` reads delivery names; `WORKFLOW_LIST` carries `for`, `order`, `setUp`, `delivery`; the suggestion's `why: {code, said}`; twin rows | `DriverModuleWorkflowTests`; each suggestion fixture's code; no name contains another kind's |
| **1b** the effective graph (modules, driver) | `WORKFLOW_CURRENT` takes `route`, and answers `effective` (workflow, source, version or digest, graph) with each node's cell, as `WORKFLOW_RUN` answers steps (`WorkflowRoutes.cs:612-614`), and the limits in the driver's words. Current's graph is `workflow new --from current:‹repository›`'s, and a workspace's comes from the same handler over `WorkflowCurrent.Derive(config, null, workspace)` (`:52`) | it writes nothing; its identity equals `WorkflowSelection.Resolve`'s for a saved default, an inherited kind, explicit Current, a cleared choice and a workspace; cells equal today's chart's |
| **1c** choosing, reviewed (modules) | `WORKFLOW_LIST` announces `use: ["plan"]`; `WORKFLOW_USE {plan}` writes nothing and answers reach, lowering, places, what a default-clear's kinds keep, and `expected`; a press with the token is refused `moved` where anything moved; `stale` stays | plan and moved write nothing; the census keeps the press the person's; the terminal unchanged |
| **1d** place detail, `lands`, summaries (modules, driver) | `PLACES` takes `detail: {workspace, repository}` and `members: workspace`; rows gain `lands`, each work's role names and scope, the values `look`, `helpers`, `answer`, and `adoption: none / running / adopted`; an adoption-running repository is listed with the driven | detail equals its execution readers and probes nothing; a version's `lands` equals Current's for the same `land`; every folded value present |
| **1e** keep how it lands (driver, modules) | `PLACE_PLAN`'s keep ask writes no landing and no workflow choice, and allows driving, the ticks and adoption; `PLACE_APPLY` applies a subset of what it writes today | no `SET_LANDING` or `WORKFLOW_USE` in a keep plan; a saved workflow survives; Apply stays the person's; a restart settles as today |
| **1f** step titles (plugin contract, driver) | a manifest step may declare `title` and `about` (en, zh); the palette carries them; the example's words say it records a release and copies the package into its own registry folder | schema tests; an old manifest keeps its kind; no *publish* in the example |
| **1g** the glossary rows (web, glossary) | §2.5's new and amended rows, before any visible row's words | `names:check --strict` knows each row, and fails on a name containing another kind's |
| **1h** the page shell (web; d) | §2.1's head and ⋯, the first-screen rule, folded sections, the first section by kind, Roles' need-opening, controlled state above the mounts, doors to section and row, *Back*, focus, editable not-driven sections | each folded value names its source; a folded section reads nothing; a door opens its row; editing a not-driven place never sets driving; installed: the head and three lines on the first screen, every kind of place |
| **1i** the tree (web; h) | second lines, *Adoption running*, the chip, the selection kept | a page turn, a search, the chip's count, an adoption-running row |
| **1j** the choices (web; a, e, h) | §2.2's choices page: *Keep how it lands*, deliveries and `why`, *Not driven*'s key, Driving's caption from the plan's sides, refusal doors, the chooser from `members`, mixed suggestions | refusal → repair → return → plan read again; mixed suggestions preselect nothing; `defaultName.*`, `set.line.*` and `PRESETS` gone; installed |
| **1k** the review and Apply (web; j) | §2.2's review: order, reach and unread lines, hold outcomes, `Acceptances` with read-bound ticks and standings, *updating*, the driver's `moved`, the result's door, a seed opening it | a changed blob clears its tick; a stale plan offers no *Apply*; partial holds per repository; flow 1 ends *Not driven · adoption running*; installed: a mostly undriven workspace, an outside change mid-review, *Apply*, recovery, retry |
| **1l** Roles on a place, one role editor (web; d, h) | §2.4's rows and `RoleEditor`, keeping scope, revision and typed alternatives | folds; *Back to* naming the fallback; the two-roles-one-work refusal in the editor; a shared role edited once; a typed model saved; installed |
| **1m** This machine's page (web; l) | *All roles*, the diagram, *Suggested* and the found changes, reachable lit | a door census and `HelpCoverageTests` re-point every dark Roles door |
| **1n** one drawing (web) | `graphFlow.ts` per §2.3's table: sections, the ◎ node, the 14rem track, the holds group, the end's words, the renamed steps' words | unit tests over every shipped graph; fixtures equal to `Defaults/workflows/*.json`; measured stories: the release graph down at 80 and 39rem, the rail at 25rem, no crossings |
| **1o** the Workflow section (web; b, h, n) | route rows as a selection; the chart column at W, one press at H; label and cells; folds with owners (landed route on `WORKFLOW_LANDED`, heavy gates in More); the Gates and Landing need-openings; a workspace's section | a saved default route draws its own graph; landed Current reads *No verification here*; *Verify…* waits on acceptance; choosing writes nothing; installed |
| **1p** Change… and the plan of one (web; c, o) | the three tabs, the preview, the plan only where announced, *Save a copy…*, *Use here* with its token, *Use the workspace's* | against today's handler, selecting a candidate writes nothing; a stale and a moved *Use*; a default-clear with kinds left says they keep Current |
| **1q** the editor's page (web; n, o) | the page, the inspector beside or in a drawer, keys, picker, *＋*, confirmed removal, the outcome table, ids with their fallback, undo, refusal jumps | inspector and navigation by keyboard; an unrouted outcome shows; undo; installed at 680 px |
| **1r** options, tails, inputs (web; q) | worded options, the method's tail, `pattern`, `target`, inputs from the routed predecessor, asking where two fit | the release workflow composed from the Merge preset by the inspector alone (extending `WorkflowEditor.test.tsx:138`), both data wires proposed; an ambiguous input asks; unknown fields round-trip |
| **1s** save and choose in the editor (web; c, r) | judgement by fingerprint, consent bound to the scope keys, *Review and save*, a name-only save, *Use here* only on an unchanged draft | out-of-order judgements; a changed reach resets consent; no *Use* on a changed draft; a stale base; *Save* passing while *Use* refuses; installed: an append two places follow, with a run bound to the older version |
| **1t** staffing a step (web; l, q) | §2.4's *Taken by* | add a role, return, choose it, save and use; a refused save keeps both forms; a driver regression on the bound role; installed |
| **1u** a run's view (web; n) | §2.3's run view | a release run of each outcome; stories at 300 px, 45% of the window and 25rem; installed: changes sent back |
| **1v** the workspace's remaining rows (web; h, l, o) | Remote as a row of Repositories' workspace *Details*; the rules to Sessions, the supervisor to Roles, the line's defaults to Workflow | a control census of Wire, Unwire, retiring a machine, allow, ask and deny, each with its door; the door census |
| **1w** the rewording (web) | the zh keys reworded and *Needs attention*, over every visible row, last | `names:check --strict` passes and fails on a planted 交付, 搁置, 委任, 当前, 关卡 or 回到前面的步骤; parity both ways |
| **1x** the installed window (the steward; w) | §3's seven flows on several mostly undriven workspaces, with a repaired refusal, a restart mid-review, a moved plan, an interrupted *Apply*, and a release run of each outcome | dated shots in en and zh, both themes, at 1546 and 680 px, with run records |
| **1y** lit (web; x) | `daoris.agents.setup` on by default; the old tabs, the Roles mode's list, strip and sections, `WorkflowTab` and `WorkflowStartNode` go | an opener test per old door; the door census; the installed window again |

## 6. What stays open, and what to measure first

**Open**
- **A role only its naming steps take.** A role added for one step takes the place's whole work, and avoiding that needs a driver design.
- **Whether the graph's end should be derived.** That is the driver's call, and the end's words hold either way.
- **A review's changes with no `back` wire.** The inspector would say the driver's sentence, which needs a palette field.
- **Whether a look's press keeps 已审阅.**
- **Editing a verification workflow's steps** from the landed row.
- **Smaller questions left open:** a repository's own kinds, where running work is listed, and unsaved drafts across a restart.
- **Carried from AGENTSPLACE1:** an *Undo* plan, and the Integration queue beside Gates.

**Measure first**
1. **First set-up, unaided, in en and zh.** Adopt an imported repository while unrelated work stays held. Count as §3 counts, note the wrong turns, and check whether the person can say what *Apply* starts.
2. **The overview.** The head and three section lines within the first 400 px at 1546 and 680 px, and the zh head lines at 680 px.
3. **Large workspaces.** The repository counts, and the time to choose among them.
4. **A long graph.**
   - Finding an outcome, and following its return without opening the whole graph.
   - Reading the release chart cold: who accepts, where changes go, what *none* does, which plugin copies, and what batches the line.
5. **Composing and staffing.**
   - From the Merge workflow to the release workflow: time, refusals and presses.
   - Adding a place role, and saying what else it now takes.
6. **Placement and words.**
   - The release graph is drawn down at 80 and 39rem, never across.
   - The `names:check --strict` count before and after rows g and w.

## 7. The settlements

**Before the synthesis**
1. **The place page.** A head and three sections, folded from `PLACES`. Workflow comes first on a repository and a workspace, Roles on This machine. A section opens only for a need. A workspace has its Workflow section.
2. ***Set up…***
   - Choices, then a review, over one draft and one plan.
   - Refusals sit beside their part and are listed in the review.
   - Mixed suggestions are said per repository.
   - Preset names come from the shipped data.
3. **One name per mechanism.** *Set up…* sets how work lands, and *Change…* chooses a workflow. Where they shared a word, one is renamed.
4. **`WORKFLOW_USE` stays the person's,** reviewed as a plan of one with an `expected` token. Save and Use stay separate presses, and a dirty draft never offers *Use*.
5. **The chart.** It draws what the place follows, labelled, from one drawing module, top to bottom, with truthful endings.
6. **The editor.**
   - It has its own page: the graph, and one step's inspector with an outcome table.
   - A method brings its tail, and inputs are wired from the step that makes them.
   - No ids; undo; the judgement is tied to the draft.
   - Chrome words, with the keys under *Details*.
7. **Staffing.**
   - One row per differing work, and one role editor.
   - The step's picker adds a place role and says its consequence.
   - *All roles*, the diagram, *Suggested* and the found changes stay reachable once lit.
8. **The words.** One glossary row per thing, Opus's zh collisions fixed, Codex's table merged, held by `names:check --strict`.
9. **Rows.**
   - The reads and fields come first, then the shell, *Set up…*, staffing, the drawing, the editor, save and choose, a step's staffing and the run.
   - The words and the cutover come last.
   - The flag is lit only after the installed window shows every flow.

**After the reads** (`2026-11-01-setup-ux-reads.md` §3)
1. **The preview.** It sends `plan` only where a read announces it (1c, 1p).
2. ***Use the workspace's*** clears the default alone, and says what the kinds keep. A kind's clear is its own press.
3. **The landed route** keeps `WORKFLOW_LANDED` and its choices, in Workflow's folds beside the second opinion and your look. The heavy gates move to More.
4. **Flow 1** ends *Not driven · adoption running* unless driving is chosen, and the head and tree show it.
5. **A not-driven place** is edited through its existing presses, never enabling driving. A refusal's door repairs it and returns.
6. **New fields.**
   - `PLACES` gains role names, role scope and the summary values.
   - Row b answers cells and a workspace's graph.
   - *Sent back n times* is dropped.
7. ***Keep how it lands*** (1e).
8. **The release flow** starts from the shipped Merge workflow. Inputs prefer the routed predecessor, and ask where two fit.
9. **No control lost at the cutover:** the remote, the machines and the rules get homes, under a census (1v).
10. **◎** is a drawing-only node, and the 14rem track is the drawing's contract.
11. **A seed opens the review.** The head and three section lines fit the first screen, with the chart one press away at 680 px.
12. **The editor's inspector** is a drawer at 680 px.

Every non-blocking finding and split of both reads is taken, the names are rechecked (§2.5), and the measures are added (§6).
