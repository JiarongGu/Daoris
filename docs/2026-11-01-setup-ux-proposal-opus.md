# SETUPUX1: the Opus maker's sealed proposal

**Standing:** a sealed proposal under D191 that answers `docs/2026-11-01-setup-ux-brief.md` (D221 is reserved for it). I wrote it from the code, stories, locales and records listed below. I changed nothing in the repository, read no `local/` folder and read no other proposal.

**What I read.** The brief; `docs/2026-11-01-agents-place-design.md`, `-fabric2-plan.md` and `-land-node-design.md`; the platform language §2–§4 (`docs/2026-09-19-platform-ux.md`); the UI/UX design's R1–R7 and §4 (`docs/2026-10-20-ui-ux-design.md`); UX7 §2 (`docs/2026-10-05-ux7-design.md`); `.claude/knowledge/translation-parity.md`; and the glossary.

Web code:
- `agents/setup/*`, `agents/AgentsView.tsx`, `modes.ts`, `AgentsModes.tsx`
- `agents/roles/{Roles,RolesView,RoleSections,SuggestedChoicesCard,AppointedRoles,RoleDiagram}.tsx`
- `workflow/{WorkflowChart,WorkflowTab,WorkflowStartNode,WorkflowRunView}.tsx`, `workflowFlow.ts`, `current.ts`
- `workflow/place/*`, `workflow/edit/*` with their stories and fixtures
- `design/Flow.tsx`, `design/flowLayout.ts`, `app/opener.ts`
- `projects/{ProjectsView,workspace/WorkspaceView,setup/RepositorySetup,setup/SetupParts}.tsx`
- the en and zh locales for agents, workflow, workflow.place, workflow.edit, workflow.run, settings.ai and nav

Driver and modules:
- `Workflows/WorkflowPalette.cs`, `Nodes/NodeContract.cs`, `Nodes/NodeOptionFields.cs`, `Nodes/NodeWires.cs`
- `WorkflowDefaults.cs`, `WorkflowOperations.cs`, `Defaults/workflows/*.json`
- `Setup/PlacePlan.cs`, `Setup/PlaceApply.cs`, `Domain/SetupPresets.cs`, `Roles/RoleCatalog.cs`
- Modules' `Bridge/Routes/WorkflowRoutes.cs`

Paths are repository-relative. Web paths start at `src/Daoris.Web/src/` and driver paths at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, unless written in full.

---

## 0. What the built parts do today

### The place

1. **The page is a stack with nothing folded, in the wrong order.** It reads: a facts list (`agents/setup/SetupPlaces.tsx:284-324`), then Roles (`:325`). On a repository it continues with Driving's rows, then Workflow, then Sessions (`agents/setup/PlaceSessions.tsx:40-50`). The design put Driving in the head (`docs/2026-11-01-agents-place-design.md:63`). The code still draws it as a section and says so (`PlaceSessions.tsx:30-31`).
2. **Lighting the flag today loses *All roles*, the diagram, the *Suggested* line and the found staffing changes.** The lit branch mounts only `PlaceRoles` (`agents/AgentsView.tsx:164-194`). `RolesCard`, with its catalogue, suggestion and found changes, is drawn only by the dark Roles mode (`AgentsView.tsx:195-214`, `agents/roles/RolesView.tsx:221-239`).
3. **A workspace's place has no Workflow section** (`PlaceSessions.tsx:33`). Its Workflow tab and the rest of its Setup tab stay on Repositories (`projects/workspace/WorkspaceView.tsx:141-161`).
4. **A tree row says only its name and its needs** (`SetupPlaces.tsx:146-152`). What a repository follows is read only after it is opened, although `PLACES` already answers each value with its source (`data/wiring.ts:352`).

### Set up…

5. **Only the first repository's suggestion is read** (`agents/setup/PlaceSetUp.tsx:111`, `PlaceSetUpPanel.tsx:33`). A workspace's set-up ticks only its driven repositories (`AgentsView.tsx:187`), so setting up 17 imported ones takes 17 ticks.
6. **Refusals and changes are one block under the parts.** Each change is a `said: before → after` sentence (`PlaceSetUp.tsx:216-238`). Nothing is said beside the part it is about.
7. **The presets' names come from two sources.**
   - The page reads its own strings, `agents.setup.defaultName.*` and `agents.setup.set.line.*` (`PlaceSetUp.tsx:165-168`, `SetupPlaces.tsx:310`). These copy the shipped data's `names` and `for` (`Defaults/workflows/merge-after-accept.json:4-8`), which `ChangeWorkflow` reads instead (`workflow/place/ChangeWorkflow.tsx:16`).
   - The preset ids are a literal (`agents/setup/placePlan.ts:7`) beside `SetupPresets.Table` (`Workflows/Domain/SetupPresets.cs:44-48`).
   - `WORKFLOW_LIST` does not carry a preset's `for` (`Workflows/WorkflowOperations.cs:67-74`).
8. **Two components draw the same declaration and its two acceptances** (`PlaceSetUp.tsx:44-90` and `workflow/place/PlaceGates.tsx:79-196`).

### The workflow

9. **One name covers two mechanisms.**
   - *Merge after you accept* in *Set up…* is a set of `land` options written to the rule. It also sets a chosen version back to Current (`SetupPresets.cs:25-31`, `Setup/PlacePlan.cs:169-182`).
   - The same name under *Change…* is a graph. You open it in the editor, save it, then choose it (`ChangeWorkflow.tsx:140-149`; zh `workflow.place.change.body`, `locales/zh/workflow.place.json:28`).
   - No plan ever saves a workflow (`Setup/PlaceApply.cs:47-48`).
10. **The chart shows Current even where the place follows a saved version.** `WORKFLOW_CURRENT` derives Current from the rules (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:47`). The section draws it under "Current workflow" (`workflow/place/PlaceWorkflowView.tsx:61,71`; `workflow/WorkflowTab.tsx:54`), while the start node above says the route names the version.
11. **Two flowcharts are stacked:** the start node is a `Flow` of its own (`workflow/WorkflowStartNode.tsx:178`) drawn above the chart.
12. **The editor sits at the foot of the section and draws each step twice.** It comes under the chart, Gates and the landing rows (`PlaceWorkflowView.tsx:94-98`). It draws its graph, then lists every step again as a card with a form of selects (`workflow/edit/WorkflowEditor.tsx:261-279`, `:466-536`).
13. **The editor's drawing can read sideways.** It has no sections (`WorkflowEditor.tsx:261`), so `Flow` lays it left to right whenever the ranks fit (`design/Flow.tsx:216`, `design/flowLayout.ts:283-293`). A short graph reads sideways at 1546 px.
14. **The editor asks for internals.** A person types a workflow id (`WorkflowEditor.tsx:316-317`), sees each step's id as code (`:412`), and sets options by their field names: `method`, `target`, `pattern`, `squash`, `request`, `conflicts`, `to`, `until` (`workflow/edit/StepOptions.tsx:69,82,86,98`).
15. **Changing `land`'s method does not bring its tail** (`docs/2026-11-01-land-node-design.md:35-39`). `setOption` writes one option (`workflow/edit/graphDraft.ts:187-199`), and the save refuses `land-tail`.
16. **An input needs a hand-made `data` wire**, with two port selects (`WorkflowEditor.tsx:512-523`), beside the `when` or `after` it rides on (`release.json` wires `h`/`i` and `j`/`k`).
17. **The editor has no words for two steps every preset holds.** Every shipped preset holds `quest.holds` and `content.holds` (LANDNODE1k; `merge-after-accept.json:17-18`). The editor has words for `kind.quest.close` and `kind.work.requests` only (`locales/en/workflow.edit.json:27,33`; `keySeparator: false`, `locales/i18n.ts:23`). So each preset's holds read "A step this page has no words for" (`WorkflowEditor.tsx:65-70`). The stories cannot show it: their release graph has no holds (`workflow/edit/editFixtures.ts:53-76`).
18. **Endings are drawn wrongly.** An outcome with no route draws nothing (`release.json` has no `when none`). The end of the line hangs off the graph's `end` (`workflow/workflowFlow.ts:166-168`). In the release preset that `end` is `line`, with three steps after it, so the drawing forks at the line into *Finished* and *Find*.
19. **Choosing has no plan.**
   - The editor's *Start from* select (`WorkflowEditor.tsx:241-259`) repeats Change…'s presets.
   - Both *Use here* presses go straight to `WORKFLOW_USE` with no plan read first (`ChangeWorkflow.tsx:47-55`, `WorkflowEditor.tsx:220-228`). The design asked for "a plan of one" (`docs/2026-11-01-agents-place-design.md:131`).
20. **Current's "version" is its 12-hex digest,** drawn as `v3f2a…` (`ChangeWorkflow.tsx:71`, `workflow/current.ts:125-126`).

### Staffing

21. **The Roles section is long and its editor is wordy.** A place's Roles lists all nine works, each with its own act (`agents/setup/PlaceRoles.tsx:111-187`). The editor opens with five paragraphs of prose every time (`agents/roles/Roles.tsx:422-426`, `:441`). The design said that prose goes (`docs/2026-11-01-agents-place-design.md:146`).
22. **A role added at a place takes that work for everything there:** at most one role takes a work in one scope (`Roles/RoleCatalog.cs:311`). The step's role picker (`StepOptions.tsx:43-62`) neither says so nor offers a way to add a role.

### The words

23. The zh catalogue gives one thing several words, and borrows words that already name other things. The full list is §2.5's table:
   - 交付 is *submission*.
   - 搁置 is *sitting*.
   - 委任 should be 任命 (*appoint*).
   - 关闭 should be 停用 for a role turned off.
   - 当前 sits beside 现行 for Current.
   - "Open Setup" reads 打开设置, which is "Open Settings".
   - 检查, 关卡 and 关口 all stand for gates.
   - A way back has three names; the editor has three names.

---

## 1. The design in seven decisions

1. **One page per place, in one order:** the head, then Workflow, Roles and Sessions.
   - Each section is folded to one line that names its values and where each comes from. Every folded line is drawn from `PLACES` alone, so opening a page asks for nothing more. A section opens itself only where something in it needs attention.
   - Driving, holding and trees belong to the head.
   - Workflow comes first because it is what the head's preset names, what *Set up…* changes, and most often what differs from the parent. Roles mostly come from the parent and fold to one line.
2. **One page says how a place works: *Set up…*.**
   - *Change…* is a door into its first part, *How it lands*.
   - Every change of what a place follows is read as one plan and applied by one *Apply*: a setUp preset, any other preset, a saved version or Current. For this, `PLACE_PLAN` gains a `workflow` field. `PLACE_APPLY`'s step 4 is the slot the receipt left open for saving a workflow (`PlaceApply.cs:47-48`).
3. **Presets come first, from one source:** the shipped data's names and what each preset is for, in the data's order. The suggested preset comes first, with the reason. Saved versions and Current fold beneath.
4. **The chart is what the place follows:** Current, or the chosen version's graph.
   - One module draws it, and the editor and a run's view use the same module.
   - It reads top to bottom under *Before it lands* and *After it lands*, with the landing as the hinge.
5. **The step editor is a page of its own: the graph and an inspector.**
   - You select a step on the drawing and set it beside the drawing.
   - Each step's ways on are a short table of what can happen to it: once done, changes asked for, it fails, each outcome. Each row has a target.
   - A method brings its tail, an input is wired from the step that makes it, nothing asks for an id, and every change can be undone.
6. **Staffing is one row per work that differs, one editor, and one sentence of consequence.**
   - The editor's prose folds behind one ⓘ.
   - The step's role picker can give the place its own role, and says that the role then takes all of that work there.
7. **One name per thing** (§2.5). Each name is a glossary row, held by `names:check --strict`.

---

## 2. Page structure

**Widths.**
- **W** is the main area at 80rem: a 1546 px window with the list open.
- **H** is 39rem: a 680 px window with the list closed to its strip, or 1546 px with the side bar open.
- **N** is 25rem, the 400 px floor: a 680 px window with the list open.

"1546" draws W. "680" draws H, and N where N differs. Each two-column layout switches by a container query on its own page, never the window's width (R7).

### 2.1 The Setup mode as one place

```
1546 · W
┌ Agents ──────────────────────┬───────────────────────────────────────────────────────────────────────────────────────┐
│ [ Agents │ Setup ]            │ aurora ›                                                                              │
│ ⌕ Search places               │ engine   ● Driven                                     [ Set up… ]  [ Hold ]   ⋯       │
│ [ Needs attention · 2 ]       │ Merge after you accept, 2 options of its own · its own tree per session               │
│───────────────────────────────│───────────────────────────────────────────────────────────────────────────────────────│
│ This machine                  │ ▸ Workflow   Merge after you accept · Current · merges into main · gates accepted     │
│ aurora              3 of 20   │ ▸ Roles      Building: Builder, this machine · Second opinions: Reader, from aurora   │
│   engine                      │              · 7 more from this machine                                               │
│   Merge after you accept      │ ▸ Sessions   Restricted · English · helpers ask you · no standing answer              │
│   site  ⚠                     │                                                                                       │
│   Its landing can't be read   │                                                                                       │
│   docs                        │                                                                                       │
│   Documents                   │                                                                                       │
│   ▸ 17 imported, not driven   │                                                                                       │
│ beta                0 of 4    │                                                                                       │
│   ▸ 4 imported, not driven    │                                                                                       │
└───────────────────────────────┴───────────────────────────────────────────────────────────────────────────────────────┘
 ⋯  Stop driving…  ·  ✓ Its own tree per session  ·  Supervisor: Appoint…  ·  Show in Repositories  ·  Copy the terminal's line
```

```
680 · H (list at its strip)                      N: the same column at 400 px, with the list's rows beside it
┌──┬───────────────────────────────────────────────────────┐
│◧ │ aurora ›                                              │
│⚙ │ engine   ● Driven                                     │
│  │ Merge after you accept, 2 options of its own          │
│  │                         [ Set up… ]  [ Hold ]  ⋯      │
│  │───────────────────────────────────────────────────────│
│  │ ▸ Workflow                                            │
│  │   Merge after you accept · Current · merges into main │
│  │ ▸ Roles                                               │
│  │   Building: Builder, this machine · 8 more            │
│  │ ▸ Sessions                                            │
│  │   Restricted · English · helpers ask you              │
└──┴───────────────────────────────────────────────────────┘
```

**The tree.**
- *This machine* comes first, then each workspace with its driven repositories. The rest of a workspace's repositories fold into one row, *n imported, not driven*, which opens a paged read (as `SetupList` does today).
- Each repository row gains a quiet second line: the preset it follows from `values.follows`, or *n options of its own*, or *held*. A row with a need wears ⚠ and its first reason.
- A workspace row says *3 of 20* driven.
- *Needs attention* becomes a chip with its count (from `PlacesRead.matching`), not a checkbox (`SetupPlaces.tsx:120`).
- ↑ and ↓ move through the rows, as in every list pane.

**The head** (`PageHead`):
- **Title:** the place's name.
- **Line:** its parent, as a door (*aurora ›*, or *This machine ›*).
- **Pill:** *Driven*, *Held* or *Not driven*.
- **Facts:** the preset it follows and *n options of its own*, and its trees.
- **Acts:** *Set up…* (the primary press on a place not driven) and *Hold* or *Resume* (`SET_HOLD`).
- **⋯:**
  - *Stop driving…*, asked once in an `InlineConfirm` over `SET_DRIVABLE`;
  - *Its own tree per session*, a tick over `SET_TREES`;
  - the supervisor's *Appoint…* door;
  - *Show in Repositories*;
  - the terminal's line.

At 640 px of the main area or less, the acts take their own row under the facts (`work/frame/ViewMain.tsx:34-37`).

**The sections.**
- They are three `SetupSection` folds (`projects/setup/SetupParts.tsx:57-90`), in the order Workflow, Roles, Sessions.
- Each folded line names values with their sources: *from aurora*, *from this machine*, *default*; a value set here is unmarked.
- A section opens itself where it holds a need:
  - a declaration owed acceptance opens Workflow at Gates;
  - an unreadable landing opens Workflow at Landing;
  - a held start opens Roles at that work.
- A door may name a section and a row: `placeOpening` passes `placeSection` and `placeRow` (`app/opener.ts:187-201`).
- Which sections are open is page state per place, held beside the list as the set-up's drafts are (`placePlan.ts:139-147`). It is never remembered across launches.
- A section's own reads are asked only while it is open: the `ROLE_STRUCTURE` chain, `WORKFLOW_CURRENT` or `WORKFLOW_SHOW`, and the setup reads.

**The other places.**

```
1546 · W · an imported repository                       1546 · W · a workspace
│ aurora ›                                          │   │ This machine ›                                          │
│ api   ○ Not driven · checkout here   [Set up…] ⋯ │   │ aurora   3 of 20 driven                    [Set up…]  ⋯ │
│──────────────────────────────────────────────────│   │ Its repositories follow: Merge after you accept 2 · own 1│
│ Daoris drives nothing here yet. Set up… chooses  │   │─────────────────────────────────────────────────────────│
│ how its work lands, can ask its own session to   │   │ ▸ Workflow      By default: Current · Documentation →   │
│ adopt Daoris, and reads you the plan before       │   │                 Documents v1 · lands by merge          │
│ anything is written.                              │   │ ▸ Roles         Second opinions: Reader · the supervisor │
                                                        │                 off · 7 from this machine               │
                                                        │ ▸ Sessions      Restricted · English · 3 rules          │
                                                        │ ▸ Repositories  engine, site, docs driven · 17 not      │

1546 · W · This machine
│ This machine                                                                                    ⋯             │
│ 9 works · 14 roles · the steward off                                                                         │
│ ◆ Suggested choices for 4 roles, from Daoris's reference of 2026-10-28                 [ Review… ] [ Not now ]│
│──────────────────────────────────────────────────────────────────────────────────────────────────────────────│
│ ▸ Roles       Building: Builder · Chat: Chat · Second opinions: Reader · 6 more · the steward: off            │
│ ▸ All roles   14: 9 placed here, 5 elsewhere                                              [ Show as diagram ]│
```

This machine has no *Set up…* and no Workflow section.

**Moving between places and back.**
- The parent line in the head is the door up.
- Every source mark (*from aurora*) is a door to that place's page, with the same section and row open.
- A page opened by a door from another place shows *← Back to engine · Roles* once.
- The tree keeps its selection under the list's memory (`AgentsView.tsx:58-63`).

### 2.2 The quick set-up

`Set up…` replaces the sections in the main area and keeps the head, as it does today (`SetupPlaces.tsx:271-273`).

```
1546 · W · Set up engine                                                                       (two columns ≥ 60rem)
│ aurora ›  engine  ● Driven                                                                         [ Close ]  │
│ Set up engine                                                                                                 │
│ ┌ Choose ─────────────────────────────────────────────┐ ┌ The plan ─────────────────────────────────────────┐ │
│ │ How it lands                                         │ │ 3 changes · nothing refused                       │ │
│ │ (•) A pull request                    [ Suggested ]  │ │ How it lands                                      │ │
│ │     Pushed, and a plugin opens a pull request for    │ │   Landing         merge into main → pull request  │ │
│ │     you to merge.                                    │ │   A tree each     off → on                        │ │
│ │     Suggested: its remote is github.com, and a       │ │ Adoption                                          │ │
│ │     landing plugin lands there.                      │ │   one quest asks engine's session to adopt Daoris │ │
│ │ ( ) Merge after you accept                           │ │ Driving                                           │ │
│ │ ( ) A branch to push                                 │ │   unchanged: only the adoption quest runs         │ │
│ │ ( ) Integrated into the line                         │ │ ▸ The declaration at main a1b2c3d, blob 9f3c1e0   │ │
│ │ ( ) Not driven                                       │ │   ☐ Accept for landed verification                │ │
│ │ ▸ More presets 4   ▸ Saved here 1                    │ │   ☐ Accept for integration                        │ │
│ │ Before it lands  ☐ a second opinion  ☐ your look     │ │ In a terminal: daoris-driver place engine         │ │
│ │                                                      │ │   --preset pull-request --adopt --plan            │ │
│ │ Adoption                                             │ │                             [ Apply 3 changes ]   │ │
│ │ ☑ Ask its session to adopt Daoris                    │ └───────────────────────────────────────────────────┘ │
│ │   What it builds  (•) not said  ( ) a library        │                                                       │
│ │   ( ) a web app or service  ( ) a desktop app or game│                                                       │
│ │   ( ) documentation or tooling                       │                                                       │
│ │ Driving                                              │                                                       │
│ │ ☐ Drive all its work                                 │                                                       │
│ │   Off: only the adoption quest runs; 4 queued quests │                                                       │
│ │   wait.                                              │                                                       │
│ └──────────────────────────────────────────────────────┘                                                       │
```

```
680 · H · one column; the plan follows the parts; its head line counts what stops it
│ engine › Set up engine                     [ Close ] │
│ How it lands                                          │
│ (•) A pull request                     [ Suggested ]  │
│     ⚠ No landing plugin lands on github.com here.     │   ← a refusal said under its part,
│       [ Open Plugins ]                                │     in the driver's sentence
│ ( ) Merge after you accept                            │
│ …                                                     │
│ Adoption  ☑ Ask its session to adopt Daoris           │
│ Driving   ☐ Drive all its work                        │
│ ───────────────────────────────────────────────────── │
│ The plan · 2 changes · 1 stops it  ↑ How it lands     │
│ Landing      merge into main → pull request           │
│ A tree each  off → on                                 │
│ In a terminal: daoris-driver place engine …           │
│ [ Apply ]  (not offered: put right what stops it)     │
```

**The parts.**
- ***How it lands*** lists the setUp presets in the data's order, the suggested one first, with its `why` sentence. Each preset reads its name and its `for` from the data.
- *More presets* (merge automatically, documents, release, integrated with no press) and *Saved here* fold beneath.
- *Current with options of its own* appears where the place matches no preset.
- *A second opinion* and *your look* are ticks under the presets.
- ***Adoption*** is one tick, with *What it builds* under it while ticked.
- ***Driving*** is one tick, *Drive all its work*. Off, only the adoption quest runs.

**The plan.**
- Its head counts the changes and what stops the plan, with a jump to each.
- Each refusal is said under the part it belongs to (`PlaceRefusal.part`, `PlacePlan.cs:47-48`), whole and in the driver's sentence. Where the refusal's code names a door, the door is there: no landing plugin opens Plugins.
- Changes are rows grouped by part, *what · now · after*. A fourth column, *where*, appears only when the plan covers several repositories.
- What lowers your part stands on the warning rail.
- The work that driving would release is listed, with *Hold it after driving*.
- The declaration is shown once, with its two acceptances, by the one *Acceptances* molecule (`PlaceGates`'s, which `PlaceSetUp` then draws too).
- The terminal's line closes the plan.
- *Apply n changes* is the person's own press (`PLACE_APPLY`). A plan that moved is refused `moved` and read again, and the page says so.
- **The result:** each operation with its outcome, and the adoption quest opened from its operation's `quest`. The head then names the preset.

**Several repositories** (a workspace's page).
- The plan starts with *Repositories*: four presses, *All · Driven · Not driven · None*, a count (*12 of 20 chosen*), and a ticked list that scrolls at 12rem.
- Changes made at several repositories are said once.
- A repository whose own suggestion differs from the chosen preset is named, with a door to set it up on its own page.

**Change…** on the Workflow section opens this same page at *How it lands*. Adoption and Driving are then folded to what stands (*Adopted ✓ · Drives all its work*).

### 2.3 Composing a workflow

**The Workflow section, open.**

```
1546 · W · engine › Workflow
│ ▾ Workflow                                                                     [ Change… ]  [ Edit steps… ]   │
│   Follows          Merge after you accept · Current, read from the rules as they stand                        │
│                    2 options of its own: squash, message                                                       │
│   Where work goes  By default                  → Current                      chosen at aurora                 │
│                    Documentation  docs/**      → Documents v1                 chosen at aurora                 │
│                    The work that lands         → no verification                                               │
│   ┌──────────────────────────────────────────────────────────────────────────────────────────── Show as list ┐│
│   │ Before it lands                                                                                          ││
│   │                         ┌──────────────────────────────────────┐                                         ││
│   │                         │ ◯ The work · Builder, this machine   │                                         ││
│   │                         └──────────────────┬───────────────────┘                                         ││
│   │                         ┌──────────────────▼───────────────────┐                                         ││
│   │                         │ ◇ Daoris makes sure · 3            ▸ │                                         ││
│   │                         └──────────────────┬───────────────────┘                                         ││
│   │                         ┌──────────────────▼───────────────────┐                                         ││
│   │                         │ ● Your review · you accept           │                                         ││
│   │                         └──────────────────┬───────────────────┘                                         ││
│   │                         ┌──────────────────▼───────────────────┐                                         ││
│   │                         │ ◇ The landing · merge into main      │                                         ││
│   │ After it lands ─────────└──────────────────┬───────────────────┘─────────────────────────────────────────││
│   │                         ┌──────────────────▼───────────────────┐                                         ││
│   │                         │ ◇ On the line · ✓ work done here     │                                         ││
│   │                         └──────────────────────────────────────┘                                         ││
│   └──────────────────────────────────────────────────────────────────────────────────────────────────────────┘│
│   ▸ Gates      Landed verification: accepted, blob 9f3c1e0 at main a1b2c3d · Integration: none declared       │
│   ▸ Landing    merge into main · squash · "{title}"                                              [ Edit ]     │
│   ▸ The line   main · batch 1 · in order · declared gates — the line's own, not the workflow's                │
│   ▸ More       commits: one per change · second opinion: none · your look: off · heavy gates: 2               │
│   In a terminal: daoris driver workflow show engine                                                           │
```

At H the same column lays out narrower: the 19rem track still draws one-wide ranks top to bottom, and the folds' values go under their titles.

- **The chart is what the default route follows.** For Current it is `WORKFLOW_CURRENT`. For a saved version it is `WORKFLOW_SHOW` of that workflow's newest version, drawn by the shared module (§2.3, *Drawing*). *Follows* then says *Release for engine v2 · saved here · from Release after you accept*, the last from the version's `from`.
- **The routes are rows,** not a second `Flow`. *Verify…* moves onto the landed route's row.
- **The two policy owners stay apart** (D215). *Landing* is the workflow's: `land`'s options, saved by `SET_LANDING` under Current, or by *Edit steps* under a version. *The line* is the line's: batching, order, gates and promotion. Both are said and kept apart.

**The step editor** is a page of its own in the main area, with the place's head kept. It opens from:
- *Edit steps…*, on what the place follows (Current via `WORKFLOW_SHOW current:<repository>`, row c);
- a preset row's *Edit steps from this…*;
- a saved version's *Edit*.

```
1546 · W · Edit steps                                     (graph and inspector side by side ≥ 64rem of its own width)
│ aurora › engine › Workflow › Edit steps · from Merge after you accept      ↶ Undo   1 to put right ▾  [ Close ] │
│ ┌ The steps ───────────────────────────────────────────┐ ┌ Find a release ──────────────── by Local release ─┐  │
│ │ Before it lands                                      │ │ Looks for a pending release. Daoris runs it, and   │  │
│ │        ┌──────────────────────────────┐              │ │ the plugin answers release or none.                │  │
│ │  ╭────▶│ ◯ The work                   │              │ │                                                    │  │
│ │  │     │   Builder, this machine      │              │ │ What happens next                                  │  │
│ │  │     └──────────────┬───────────────┘              │ │  When it comes to release → [ Release          ▾ ] │  │
│ │  │                    ＋                              │ │  When it comes to none    → [ Nothing follows  ▾ ] │  │
│ │  │     ┌──────────────▼───────────────┐              │ │  If it fails              → [ The run stops    ▾ ] │  │
│ │  │     │ ◇ Daoris makes sure · 3    ▸ │              │ │ Hands on  its release to Release ✓                 │  │
│ │  │     └──────────────┬───────────────┘              │ │ ▸ More ways on                                     │  │
│ │  │     ┌──────────────▼───────────────┐              │ │                                                    │  │
│ │  ╰back─┤ ● Your review                │              │ │ ⋯  The work is done here · Remove this step         │  │
│ │        └──────────────┬───────────────┘              │ └────────────────────────────────────────────────────┘  │
│ │        ┌──────────────▼───────────────┐              │                                                         │
│ │        │ ◇ The landing · merge        │              │                                                         │
│ │ After it lands ───────┼──────────────────────────────│                                                         │
│ │        ┌──────────────▼───────────────┐              │                                                         │
│ │        │ ◇ On the line · ✓ work done  │              │                                                         │
│ │        └──────────────┬───────────────┘              │                                                         │
│ │        ┏━━━━━━━━━━━━━━▼━━━━━━━━━━━━━━━┓  chosen      │                                                         │
│ │        ┃ ◇ Find a release             ┃              │                                                         │
│ │        ┗━━━━━┯━━━━━━━━━━━━━━━━━━━┯━━━━┛              │                                                         │
│ │      release ┆                   ┆ none              │                                                         │
│ │   ┌──────────▼────────┐   ┌──────▼────────────┐      │                                                         │
│ │   │ ◇ Release         │   │ ◎ nothing follows │      │                                                         │
│ │   └──────────┬────────┘   └───────────────────┘      │                                                         │
│ │   ┌──────────▼────────┐                              │                                                         │
│ │   │ ◇ Publish         │                              │                                                         │
│ │   └──────────┬────────┘                              │                                                         │
│ │      ◎ nothing follows                               │                                                         │
│ │ [ ＋ Add a step ]                                     │                                                         │
│ └──────────────────────────────────────────────────────┘                                                         │
│ Name [ Release for engine                ]  saved as release-for-engine     These steps hold.       [ Save ]      │
```

```
680 · H · the drawing above, the inspector below           400 px · N · the rail, the inspector inline
│ engine › Edit steps            ↶   1 ▾   [ Close ]  │    │ Before it lands                          │
│ Before it lands                                     │    │ 1 ◯ The work                           › │
│ ╭─▶ [ ◯ The work · Builder ]                        │    │     Builder, this machine                │
│ │          ＋                                        │    │ 2 ◇ Daoris makes sure · 3              › │
│ │   [ ◇ Daoris makes sure · 3 ▸ ]                   │    │ 3 ● Your review                        › │
│ ╰── [ ● Your review ]                               │    │     ↩ If you ask for changes: back to 1  │
│     [ ◇ The landing · merge ]                       │    │ 4 ◇ The landing · merge                › │
│ After it lands ──────────────────────────────────── │    │ After it lands                           │
│     [ ◇ On the line · ✓ ]                           │    │ 5 ◇ On the line · ✓ work done here     › │
│     [ ◇ Find a release ]                            │    │ 6 ◇ Find a release                     ⌄ │
│   release ┆              ┆ none                     │    │   When release → [ 7 Release        ▾ ]  │
│ [ ◇ Release ]    [ ◎ nothing follows ]              │    │   When none    → [ Nothing follows  ▾ ]  │
│ [ ◇ Publish ]                                       │    │   If it fails  → [ The run stops    ▾ ]  │
│ ◎ nothing follows                                   │    │   ├ release → 7 Release → 8 Publish      │
│ ─────────────────────────────────────────────────── │    │   └ none → nothing follows               │
│ Find a release · Local release   ↑ To the drawing   │    │ 7 ◇ Release                            › │
│ When release → [ Release ▾ ]                        │    │ 8 ◇ Publish                            › │
│ When none    → [ Nothing follows ▾ ]                │    │ [ ＋ Add a step ]                         │
│ If it fails  → [ The run stops ▾ ]                  │    │ Name [ Release for engine ]   [ Save ]   │
│ Name [ Release for engine ]            [ Save ]     │
```

At H the editor uses a compact node track (at least 14rem). A two-way branch then needs 2×14 + 1.5 + 1.5 for the return lane = 31rem, which fits inside 39rem less its gutters, so the drawing still reads top to bottom. At N it does not fit, and the rail stands in its place. At W the drawing is never laid left to right, because it is in sections.

**The inspector**, one step at a time:

```
The work                                        The landing
│ The agent's work on the quest · agent alone │  │ Daoris lands the work · automatic              │
│ Taken by [ Its work's own role:           ▾]│  │ How it lands                                    │
│            Builder, this machine            │  │ (•) Merge into the line   ( ) A branch of its own│
│   Builder · this machine                    │  │ ( ) Push its branch       ( ) A pull request     │
│   Release builder · aurora                  │  │ ( ) Integrated into the line                     │
│   ＋ Give engine its own role for building… │  │ Into            [ main ▾ ]                       │
│ Once done → [ Daoris makes sure ▾ ] ＋ also │  │ ☑ Squash its commits                             │
│ ▸ More ways on                              │  │ Commit message  [ {title} ] → "Fix the login"    │
                                                 │ ▸ Branch name and plugin                         │
Your review                                      │ Once landed → [ On the line ▾ ]                  │
│ You read the work and accept it, or ask for │  │ A pull request adds The push and Pull request    │
│ changes with your words · you               │  │ after it, in the same change.                    │
│ Once you accept      → [ The landing ▾ ]    │
│ If you ask for changes → [ Back to The work▾]│ Release                                by Local release
│   offered: earlier steps, or nothing        │  │ Records the pending release. Daoris runs it; the │
                                                 │ plugin answers.                                  │
Second opinion                                   │ Takes   its release, from Find a release ✓       │
│ Another agent reads the work before it lands│  │ Once done → [ Publish ▾ ]   If it fails → [ … ▾ ]│
│ Taken by [ Reader, from aurora ▾ ]          │
│ Once done → [ Your review ▾ ]               │
```

- **What a step does** is one sentence per kind (`workflow.edit.about.<kind>`), and who acts in it comes from its kind's `reachBy` (`WorkflowPalette.cs:6-8`). A kind the page has no words for keeps its spelled kind, so a new driver kind still shows.
- **Options are worded:** `workflow.edit.option.<kind>.<name>` and each choice's words, with the raw name as the fallback.
  - `land`'s method is five choices that bring and take their tail in one change (`docs/2026-11-01-land-node-design.md:35-39`).
  - `pattern` is previewed expanded.
  - `target` lists the repository's lines.
- **What happens next** is drawn from the palette's `leaves` for the kind (`WorkflowPalette.cs:75-87`):
  - *Once done* (`after`), with *＋ also to…* for a fork;
  - *If you ask for changes* on a review, or *If you say not yet* on a look (`rejected` or `back on: rejected`);
  - *If it fails* (`failed` or `back on: failed`);
  - *When it comes to <outcome>*, one row per declared outcome (`when`).
  - Each row's target is a later step, *Back to* an earlier one (only steps it comes after are offered), *Nothing follows*, *The run stops*, or *＋ A new step…*. The driver still judges every choice.
  - `trigger` and the hand-made ports fold under *More ways on*.
- **Inputs are wired for you.** When a step is added, an input fed by exactly one upstream output whose schema fits is wired, and *Hands on* or *Takes* says so.
- **Taken by** appears on the work and the second opinion (`WorkflowPalette.cs:33-38`). It lists the roles that take the step's work and cover the place.
  - *＋ Give engine its own role for <work>…* appears only where engine has none for that work.
  - It says that the new role takes all of that work at engine (`RoleCatalog.cs:311`), not only this step.
- **Refusals** stand at their step and row, in the page's words with the driver's beneath, and the head's *n to put right ▾* jumps to each (judged as today, `WORKFLOW_SAVE plan:true`).

**The step picker** (*＋* between steps, or *Add a step*) is grouped by what a step is for, each kind with its line of what it does:
- *The work*;
- *You*: your review, your look, your go-ahead, your task;
- *Checks*: the checks, a second opinion, Daoris makes sure;
- *Landing*: the landing, integration, on the line;
- *Other repositories*: a quest to another repository, other work;
- each plugin, by its name (*Local release*: find a release, release, publish);
- *Other*: `handed`, `pushed`, `pull-request`, `all`, `stop`, `quest.close`, and any kind the page has no group for.

*＋* inserts after its step and rewires that step's way on (`insertAfter`).

**Drawing** (one module, `workflow/graphFlow.ts`, used by the chart, the editor and a run's view):

| Thing | W and H, drawn | N, the rail | Edited by |
|---|---|---|---|
| A way on (`after`) | A solid edge to the step below; *＋* between them | Numbered rows, one under another | *Once done →*; *＋* inserts |
| A branch (`when`) | Dashed edges, each labelled with its outcome; the routes side by side under their step | `├ release → 7 Release → 8 Publish` / `└ none → nothing follows` under the step | *When it comes to <outcome> →* |
| A loop back (`back`) | A return on its own lane at the drawing's left, labelled *back*, with its words in the source node's foot | `↩ If you ask for changes: back to 1 The work` under the step | *If you ask for changes → Back to <step>* |
| A failure path (`failed`, `rejected` forward) | Short dashes to its target | `✕ If it fails → …` | *If it fails →* |
| An ending | ◎ *nothing follows* under each step with no way on and each unrouted outcome; the graph's end is a pill, *✓ work done here*, on its step and never a fork into *Finished* | The same words | *Nothing follows* as a target; *The work is done here* in the step's ⋯ |
| Daoris's pre-landing holds (`work.requests`, `quest.holds`, `content.holds`, two or more in a row) | One node, *Daoris makes sure · n ▸*, that opens to its members | One row | Its members' switches; *Remove* under *More* |

**Saving and choosing.**
- *Save* asks only for a name. The id is made from the name, unique against `WORKFLOW_LIST`, and shown faint (*saved as release-for-engine*), as role ids are made (`Roles.tsx:26-31`).
- An append says the places it reaches, with *Let them follow it*, as today (`WorkflowEditor.tsx:323-331`).
- Once saved, *Use it here…* opens *Set up…* at *How it lands* with that version chosen. That is the plan of one; then *Apply*.
- *Undo* is a draft stack: a press, and Ctrl+Z while the focus is in the drawing (never in a field).
- Keys: ↓ and ↑ move through the steps; Enter goes to the inspector; Escape goes back to the step.

**A run's view** stays the session's side-bar *Workflow* (`workflow/WorkflowRunView.tsx`), drawn by `graphFlow`:

```
Side bar · Workflow · 300 px
│ Release for engine v1 · bound to it                │
│ At Your review · waiting on you                    │
│ Before it lands                                    │
│ ✓ The work                     done                │
│ ✓ Daoris makes sure · 3        done                │
│ ✓ The checks                   done                │
│ ● Your review                  waiting on you      │
│   [ Accept ]  [ Ask for changes… ]                 │
│   ↩ sent back once                                 │
│   The landing                  not reached         │
│ After it lands                                     │
│   On the line                  not reached         │
│   Find a release               not reached         │
│   ├ release → Release, Publish                     │
│   └ none → nothing follows                         │
```

After *Find* comes to *none*: *Release* and *Publish* read *route not taken*, faint, and *Find* reads *came to none*.

### 2.4 Staffing

```
1546 · W · engine › Roles  (columns ≥ 48rem of the section)
│ ▾ Roles                                                                                                          │
│   Building         Builder · this machine    Claude Code · opus · high (+1) · ready 3 min ago  [ Give engine its own… ]│
│   Second opinions  Reader · engine  set here Codex · gpt-5 · high · cooling 12 min      [ Edit ]  [ Back to aurora's ] │
│   Design           Builder · this machine — design falls to building                    [ Give engine its own… ]    │
│   ▸ 6 more, all from this machine: chat, helping, partnering, brainstorms, intake, Ask Daoris                      │
│   Supervisor       aurora's covers engine · none of engine's own                         [ Appoint for engine… ]   │
```

```
680 · H · rows stacked; the editor in place
│ ▾ Roles                                           │
│ Building                                          │
│ Builder · this machine                            │
│ Claude Code · opus · high (+1) · ready 3 min ago  │
│ [ Give engine its own… ]                          │
│ ┌ Give engine its own role for building ────────┐ │
│ │ 1 [ Claude Code ▾ ] [ opus ▾ ] [ high ▾ ] ↑↓✕ │ │
│ │ 2 [ Codex ▾ ]       [ gpt-5 ▾ ] [ medium ▾ ]   │ │
│ │ [ ＋ Add a choice ]   ⓘ How choices are used   │ │
│ │ ▸ Its name and work: Builder · engine          │ │
│ │ It takes engine's building from Builder,       │ │
│ │ this machine.                                  │ │
│ │ [ Save ]  [ Cancel ]                           │ │
│ └────────────────────────────────────────────────┘ │
│ ▸ 7 more, all from this machine                   │
```

```
1546 · W · This machine › All roles
│ ▾ All roles  [ Everywhere │ This machine │ Workspaces │ Repositories ]  ⌕ Search roles     [ Show as diagram ]   │
│   Placed on this machine                                                                                         │
│   Builder          building and design   Claude Code · opus · high (+1)   12 starts this week    [ Edit ]  ⋯     │
│   Reader           second opinions       Codex · gpt-5 · medium                                  [ Edit ]  ⋯     │
│   Placed elsewhere                                                                                               │
│   Reader · engine  second opinions       Codex · gpt-5 · high                         [ Open at engine ]         │
│   [ ＋ Add a role ]   [ Restore all role defaults ]                                                              │
```

- **Roles on a place.**
  - The works set here, or differing from the parent, come first. The rest are one line, *n more from <parent>*, which opens.
  - Appointed roles are rows; a dark one is one quiet line.
  - The acts are unchanged: *Give <place> its own…*, *Edit*, and *Back to <parent>'s* confirmed with the fallback named. *Off*, *Clear staffing* and *Restore role defaults* stay three acts.
- **The role editor** is split from `RolesCard` as its own molecule (`Roles.tsx:320-445`).
  - Choices come first, with *Its name and work* folded.
  - The five paragraphs go behind one *How choices are used* ⓘ (`Roles.tsx:422-426`, `:441`).
  - One sentence of consequence stands before *Save*.
- ***All roles*** is on This machine's page. A role placed elsewhere is read-only there, with *Open at <place>*. *Show as diagram* draws `DiagramSection` for the scope.
- ***Suggested*** is one line at the top of This machine's page. It opens into `SuggestedChoicesCard`, whose *Use these choices* is `SET_ROLES`, judged whole. Found changes are one line beside it.
- **A step's role** is the editor's *Taken by* (§2.3) and uses the same role editor inline.

### 2.5 The words

| Thing | en | zh | Today, where it differs | Glossary |
|---|---|---|---|---|
| The Agents place's mode | Setup | 配置 | "Open Setup" is 打开设置, which is Settings (`locales/zh/agents.json:202`) | `setup` row (`locales/glossary.json:701`): amend "means", since there is no Setup tab once lit |
| A place's page of how it works, and its act | Set up… / Set up engine | 配置… / 配置 engine | — | under `setup` |
| A place | place | 位置 | 范围 in the roles' prose (`zh/settings.ai.json:79`) | new row; avoid 范围 |
| How a place's work lands (the part) | How it lands | 落地方式 | Delivery / 交付 (`zh/agents.json:373`); 交付 is *submission* (`glossary.json:522`) | new row; avoid 交付 |
| A shipped workflow | preset | 预设 | 默认工作流 (`zh/workflow.edit.json:8`), "a default is a preset" (`zh/workflow.place.json:28`), 工作流沿用 (`zh/agents.json:335`) | new row; avoid 默认工作流 |
| The workflow derived from the rules | Current | 现行 | 当前 (`zh/workflow.place.json:29`) beside 现行工作流 (`zh/workflow.json:4,150`) | new row; avoid 当前 |
| A saved version | version, v2 | 版本 | Current's digest drawn as a version (`ChangeWorkflow.tsx:71`) | amend `version` (`glossary.json:644`): a workflow's saved version; a digest is never one |
| A step | step | 步骤 | no row | new row; never 节点 on the window |
| A step's way on | way on | 去向 | "by its `kind` wire" (`workflow.edit.how.recorded`); *wire* is a remote's (`glossary.json:607`) | new row; avoid 连线, 接线 |
| A way back to an earlier step | Back to … | 退回到… | 回到前面的步骤 (`zh/workflow.edit.json:66`), 回退 (`:126`), 退回 (`zh/workflow.run.json:279`) | new row, beside `send back` |
| The review's rejection | Ask for changes / changes | 要求修改 / 修改 | "If you refuse it", 你拒绝时 (`zh/workflow.edit.json:51,64`); 拒绝 is *deny*'s | each kind's own word: review says changes, look says not yet |
| The review step | Your review | 你的审阅 | — | new row |
| The look | Your look | 你的查看 | "Needs a look", 需要查看 (`zh/agents.json:310`) | new row; the filter becomes *Needs attention* / 需要留意 |
| A repository's declared gates | gates | 检查项 | 检查 (`zh/workflow.place.json:2`), 关卡 (`zh/workflow.edit.json:144`), 关口 (`zh/workflow.json:5`) | new row; avoid 关卡, 关口 |
| Edit a workflow's steps | Edit steps… | 编辑步骤… | 编排步骤… (`zh/workflow.json:181`), 编排工作流 (`zh/workflow.edit.json:2`) | `compose` row (`glossary.json:581`): `use: edit steps` |
| Choose for a place | Use here / Use it here… | 在此使用 / 在此使用… | 在这里使用 (`zh/workflow.place.json:37`) | `Use here` row (`glossary.json:584`) |
| Saved here | Saved here | 此处保存的 | 保存在这里的 (`zh/workflow.place.json:31`) | — |
| Hold | Hold / Held | 暂停 / 已暂停 | 搁置, which is *sitting* (`zh/agents.json:324,405`) | `hold` (`glossary.json:668`) |
| Appointed | Appointed | 任命的 | 委任 (`zh/agents.json:363`) | `appoint` (`glossary.json:572`) |
| A role off | Off | 停用 | 关闭 (`zh/settings.ai.json:37`) | `turn off` (`glossary.json:652`) |
| Staffing | staffing / Clear staffing | 人选 / 清除人选 | "Clear order", 清除顺序 (`zh/settings.ai.json:62`) | new row |
| Adoption | Ask its session to adopt Daoris | 请它的会话采用 Daoris | 为智能体配置 (`zh/agents.json:383,352`) | `adopt` (`glossary.json:626`) |
| Driving all work | Drive all its work | 驱动它的全部工作 | 通用驱动 (`zh/agents.json:392-393`) | — |
| The plan, Apply | plan / Apply | 计划 / 应用 | — | new `plan` row; amend `apply` (`glossary.json:679`) to cover a place's plan |
| Landing | landing | 落地 | its "means" predates LANDNODE (`glossary.json:554`) | amend to the five methods |
| Where the work is done | work done here | 工作止于此 | — | `ends here` stands |
| Where a route stops | nothing follows | 此后没有步骤 | a *Finished* fork after `end` (`workflowFlow.ts:166-168`) | new row; avoid 结束 |

---

## 3. The flows

1. **First set-up** (design §5): Setup → aurora's *17 imported, not driven* → api → *Set up…* → read the plan (*How it lands* is preselected with its why; Adoption ticked; Driving off) → tick the acceptances the declaration offers → *Apply 3 changes*.
   - That is five presses and the acceptances. Today it is about fifteen across six screens (`docs/2026-11-01-agents-place-design.md:18`).
   - Afterwards the head reads *Driven · A pull request*, the result opens the adoption quest, and the tree row joins the driven rows with its preset line.
   - Several repositories: aurora → *Set up…* → *Not driven* (17 chosen) → one plan, its changes said once and the two repositories whose suggestions differ named → *Apply*.
2. **A change.** Either engine → Workflow → *Landing · Edit* → message `{title} ({quest})` → *Save* (`SET_LANDING`: one row, one press). Or engine → Workflow → *Change…* (that is, *Set up…* at *How it lands*) → *A branch to push* → read the plan → *Apply*.
3. **Staffing** (design §5): engine → Roles → Building · *Give engine its own…* → choice 1 Claude Code · opus · high, choice 2 Codex · gpt-5 → *Save* (`SET_ROLE`). The row then reads *Builder · engine*, with *Back to aurora's* beside it.
4. **An appointment:** aurora → Roles → Supervisor · *Appoint…* → its ask, the share and the window → the owner's *Appoint* (`SET_APPOINTMENT`; unchanged).
5. **The workflow** (design §5): engine → Workflow opens on *Follows*, *Where work goes*, the chart and the gates' standing → *Edit steps…* → a change → *Save* (a new version; running work keeps its own) → *Use it here…* → the plan of one → *Apply*.
6. **A release workflow with a decision.**
   - As a preset: engine → Workflow → *Change…* → *More presets* → *Release after you accept* → read the plan: it saves a copy as `release-for-engine` and engine's work follows it; it lowers nothing; it adds the checks → *Apply*. That is four presses.
   - Composed:
     1. engine → Workflow → *Edit steps…*. The editor opens on what engine follows: Merge after you accept, written out from Current.
     2. Select *On the line* → *＋* → in the picker, *Local release › Find a release*. It is inserted after the line, with *release* and *none* both reading *Nothing follows*.
     3. *When release → ＋ A new step… → Release*. Its input is wired from *Find*.
     4. *＋* under *Release* → *Publish*, wired the same way.
     5. Select *Your review* → *If you ask for changes → Back to The work*.
     6. The head says *These steps hold*. Name it *Release for engine* → *Save* → *Use it here…* → the plan → *Apply*. About sixteen presses.
   - Where Local release is not installed, the picker has no *Local release* group. *Other* says that plugins add their steps here, with a door to Plugins, and the plan's reach refuses a version naming it.
7. **Staffing a step with a role the place adds:**
   1. In the release workflow's editor, *＋* above *Your review* → *Second opinion*.
   2. In its inspector, *Taken by: Reader, from aurora ▾* → *＋ Give engine its own role for second opinions…*.
   3. The role editor opens inline, placed at engine and fixed, named *Reader · engine*. Choose Codex · gpt-5 · high.
   4. Before *Save* it says: "It takes engine's second opinions from Reader, aurora." → *Save* (`SET_ROLE`).
   5. The picker now shows *Reader · engine* chosen (the option `role`), and the judge checks it with `RoleCatalog.Named`.
   6. *Save* → *Use it here…* → *Apply*.
   - Afterwards engine's Roles reads *Second opinions: Reader · engine · set here*.

---

## 4. What it keeps, moves, merges and removes

**`agents/setup/`**
- `SetupPlaces.tsx` — **keep, change.** `SetupList` keeps its tree and gains the second line and the *Needs attention* chip. `SetupMain`'s facts list (`:284-324`) moves into `PageHead`. The `roles` and `after` slots become three ordered sections. *Why:* one head, folded sections, and what a place follows read from the list.
- `PlaceRoles.tsx` — **keep, change:** columns, the inherited works folded, and the split-out `RoleEditor`. *Why:* §2.4.
- `PlaceSessions.tsx` — **split.**
  - The Driving rows (`:40`) move to the head's acts and ⋯.
  - The Workflow mounting (`:41-47`) moves to a Workflow organism beside `PlaceWorkflowView`.
  - Sessions stays.
  - *Why:* each section has one owner, and Driving is the head's (`docs/2026-11-01-agents-place-design.md:63`).
- `PlaceSetUp.tsx` — **keep, re-laid:** the parts, the grouped plan and the refusals in place. Its `Declaration` (`:44-90`) **merges** into `PlaceGates`' Acceptances. *Why:* one component for one declaration.
- `PlaceSetUpPanel.tsx` — **keep.** It also opens at *How it lands* and sends `workflow`.
- `placePlan.ts` — **keep.**
  - `PRESETS` (`:7`) goes, read from `WORKFLOW_LIST`'s `setUp`.
  - `PlaceAsk` gains `workflow`.
  - A sibling holds the folds' page state.
- `setupFixtures.ts`, `placeFixtures.ts` — **keep, extend.**

**`agents/`**
- `AgentsView.tsx` — **keep.** The lit branch mounts This machine's *All roles*, diagram and *Suggested*. The dark Roles-mode branch (`:195-214`) goes at row m.
- `modes.ts` — `ROLE_SECTIONS`, `isRoleSection` and `setupLit` go at row m. The place ids stay.
- `AgentsModes.tsx` — `RoleSectionStrip` goes at row m.

**`agents/roles/`**
- `Roles.tsx` — **keep** `RolesCard` as *All roles*. The editor (`:320-445`) **moves** to `RoleEditor.tsx`. `roleIdFor`, `entryText` and `roleName` stay.
- `RolesView.tsx` — **keep** `useRoleEditing`. The catalogue and diagram half of `useRolesMode` becomes This machine's organism. `RolesMode`, `SharedMain` and `OWN_MAINS` go at row m.
- `RoleSections.tsx` — **remove** at row m: the section list goes (`docs/2026-11-01-agents-place-design.md:146`).
- `StructureSections.tsx` — **remove** `WorkSection`, `AccountsSection`, `RepositorySection` and `StructureFilterBar` at row m. Their facts become the place pages' and Accounts' *Used by*. `StructureShell`, `DiagramDetail`, `stateText`, `positionOf` and `UsedLine` stay.
- `RoleDiagram.tsx`, `diagram.ts`, `SuggestedChoicesCard.tsx`, `AppointedRoles.tsx`, `AppointedList.tsx`, `roleStructure.ts`, `structureRead.ts`, `callTable.ts`, `OfferedField.tsx`, `RowMore.tsx` — **keep.**

**`workflow/`**
- `WorkflowChart.tsx` — **keep.** It draws what the place follows; graph mode goes through `graphFlow`.
- `workflowFlow.ts` — **merge.** Its graph branch (`:158-171`) moves into `graphFlow.ts`; Current's steps branch stays.
- `WorkflowStartNode.tsx` — **keep** for a run's route and for `WorkflowTab` until row m. It leaves the place page: its routes become rows and *Verify…* moves to the landed route's row.
- `WorkflowTab.tsx` — **remove** at row m, with Repositories' Workflow tab. `WorkflowHead`'s words move into *Follows*.
- `WorkflowRunView.tsx` — **keep**, drawn through `graphFlow`, with outcomes, routes not taken, and the count of times sent back.

**`workflow/place/`**
- `PlaceWorkflowView.tsx` — **keep, re-laid:** *Follows*, the routes, the chart, then the folds. The editor leaves it.
- `ChangeWorkflow.tsx` — **merge** into *Set up…*'s *How it lands* (its presets, *Saved here* and Current), then **remove**. *Why:* one name, one mechanism, one plan.
- `PlaceGates.tsx` — **keep.** It becomes the one Acceptances molecule.

**`workflow/edit/`**
- `WorkflowEditor.tsx` — **keep, split** into `StepGraph`, `StepInspector`, `StepPicker` and `SaveBar`.
  - *Start from* (`:241-259`) goes.
  - `WayForm` (`:466-536`) becomes the inspector's *What happens next*.
- `StepOptions.tsx` — **keep:** worded fields, and `land`'s method as five choices.
- `graphDraft.ts` — **keep.** It gains `insertAfter`, `setExit`, `withTail`, `feedInputs` and an undo stack.
- `answers.ts` — **keep.** `PresetWorkflow` gains `for`, `order` and `setUp`.
- `editFixtures.ts` — **keep.** Its release graph is held equal to the shipped `release.json` by a test.

**Elsewhere**
- `projects/setup/RepositorySetup.tsx` — **keep** as the row molecule. Its `driving` (`:352-396`) moves to the head; its `work` rows are the Workflow folds.
- `projects/workspace/WorkspaceView.tsx` — the Workflow and Setup tabs go at row m; Remote becomes a Details row.
- `projects/ProjectsView.tsx` — the `setupLives` branches (`:141-156`) go at row m.
- `app/opener.ts` — `placeOpening` (`:187-201`) passes a section and a row.
- `design/Flow.tsx`, `design/flowLayout.ts` — **keep unchanged.** Sections, track bounds and `current` already exist (`Flow.tsx:89-97`, `:41-48`).
- Locales: `agents.json`, `workflow.json`, `workflow.place.json`, `workflow.edit.json`, `settings.ai.json`, `glossary.json`.
- Driver and modules:
  - `WorkflowOperations.cs`: the preset fields;
  - `Setup/PlacePlan.cs` and `Setup/PlaceApply.cs`: `workflow`;
  - `WORKFLOW_SHOW`: `current:<repository>`;
  - `SetupRoutes.cs`: the ask.

---

## 5. Rows, in build order

**Proof for every visible row:** page tests; stories at 80, 39 and 25rem in en and zh, in both themes, keyboard throughout, with measured layout where a `Flow` draws; the installed window wherever the row changes it. Each row writes its own line in D221's note and its own translations.

1. **SETUPUX1a — the words** (web, glossary; waits on nothing).
   - *Contract:*
     - §2.5's new rows and amended rows.
     - The zh keys it names are reworded; *Needs a look* becomes *Needs attention*.
     - The hold kinds gain `workflow.edit.kind.quest.holds` and `workflow.edit.kind.content.holds`.
   - *Proof:*
     - `names:check --strict` passes, and fails on a planted 交付, 搁置, 委任 or 当前.
     - Parity holds both ways.
     - The touched components' tests read the new words.
     - The release preset's holds read their titles in a story drawn from the shipped graph.
2. **SETUPUX1b — presets from the shipped data** (driver, modules, web; waits on nothing).
   - *Contract:*
     - `WORKFLOW_LIST`'s presets carry `for` (en, zh), `order` and `setUp` from `WorkflowDefaults`, as additive fields.
     - `PLACE_PLAN`'s `suggested` carries `why`, the driver's sentence of the facts it read.
     - The page reads names and lines from the list, and `agents.setup.defaultName.*`, `agents.setup.set.line.*` and `PRESETS` go.
     - A driver before these fields shows the id and no line. The record twin gains its row in `twins.md`.
   - *Proof:*
     - `DriverModuleWorkflowTests` answers the three fields from a fixture home.
     - `PlacePlan` tests say a `why` for each suggestion fixture.
     - A page test renames a default and reads the new name in *Set up…*, the tree and the head.
3. **SETUPUX1c — one plan for what a place follows** (driver, modules; after b).
   - *Contract:*
     - `PlaceAsk` (`PlacePlan.cs:7-14`) gains `workflow`: `{preset}` for any shipped default, `{id, version}` for a saved version, or `current`.
     - A setUp preset plans exactly as today.
     - Another preset plans "saves a copy as <fresh id> and chooses it for <place>'s work".
     - A saved version plans `WORKFLOW_USE`, with its reach, what lowers your part (`InvolvementRoutes.LowersRoute`), and that running work keeps its version.
     - `PLACE_APPLY`'s step 4 saves the copy under the receipt, settled by its postcondition: the id holds a version whose `from` digest is the plan's. The one Change then chooses it.
     - `daoris-driver place … --workflow …` is the terminal twin.
     - `WORKFLOW_SHOW` answers `current:<repository>` as the graph `--from current` writes (`WorkflowOperations.cs:392`).
   - *Proof:*
     - A setUp preset's plan is byte-identical to today's.
     - The release preset plans a copy and a use; a saved version plans a use.
     - A stale version is refused `moved`.
     - A stop between the save and the change leaves *configuration pending* with the copy found, and a retry makes no second copy.
     - The census keeps `PLACE_APPLY` the person's own, with no standing cover.
     - `WORKFLOW_SHOW current:` equals `new --from current`'s body.
4. **SETUPUX1d — the place page** (web; after a).
   - *Contract:* §2.1's head and ⋯, Driving off the page, the three ordered folds drawn from `PLACES` with their sources and needs, the tree's second lines, the *Needs attention* chip, section and row in a door, and *Back to*.
   - *Proof:*
     - Each folded line names each value and its source.
     - A need opens its section.
     - The ⋯ presses its routes.
     - A door opens the named section.
     - Opening the page asks no section's read.
     - The installed window.
5. **SETUPUX1e — *Set up…* as the page of how a place works** (web; after b, c, d).
   - *Contract:* §2.2: two columns; the parts; each refusal under its part, with its door; the grouped plan; one Acceptances molecule; the workspace's chooser; differing suggestions said; *Change…* opening at *How it lands*; the result's doors.
   - *Proof:*
     - A refusal is said under its part and counted at the plan's head.
     - Changes group by part.
     - The chooser's four presses.
     - A preset beyond setUp sends `workflow`.
     - *Apply* sends the digest and only the chosen acceptances.
     - The installed window's first set-up of a workspace of mostly undriven repositories.
6. **SETUPUX1f — one graph drawing** (web; after a).
   - *Contract:* `workflow/graphFlow.ts` per §2.3's *Drawing*, replacing `WorkflowEditor.tsx:103-114` and `workflowFlow.ts:158-171`. A compact 14rem track for the editor and the side bar.
   - *Proof:*
     - Unit tests over the shipped release, merge-after-accept, documents and pull-request graphs.
     - The fixtures are held equal to `Defaults/workflows/*.json` by a test.
     - Measured stories: the release graph placed top to bottom at 80 and 39rem and as the rail at 25rem, never left to right; a crossing ceiling of 0.
7. **SETUPUX1g — the Workflow section** (web; after c, d, f).
   - *Contract:* §2.3's section: *Follows*, the routes as rows, the chart of what the place follows, *Gates*, *Landing*, *The line* and *More* folded, *Change…* and *Edit steps…*, and no second `Flow`.
   - *Proof:*
     - A place whose default route names `release-for-engine` draws that graph and says so.
     - *Change…* opens *Set up…*.
     - A step's door opens *Landing*.
8. **SETUPUX1h — the step editor's page** (web; after c, f, g).
   - *Contract:* §2.3's editor: the page; graph and inspector; the picker; *＋* insertion; *What happens next*; method and tail; inputs wired; worded options; *Taken by*; name-only save; undo; refusals with their jumps; *Use it here…*.
   - *Proof:*
     - Compose documents, and the release workflow from Merge after you accept, using only the inspector's rows, through a stubbed bridge.
     - Choosing *A pull request* adds *The push* and *Pull request*.
     - A refusal is said at its row.
     - Undo works.
     - The keys work.
     - Measured stories; the installed window.
9. **SETUPUX1i — Roles on a place, and one role editor** (web; after d).
   - *Contract:* §2.4's rows, the folded inherited works, `RoleEditor` with its ⓘ and its sentence of consequence, and the step picker's *Give engine its own…*.
   - *Proof:*
     - `PlaceRoles` and `RoleEditor` tests.
     - The add flow goes through a stubbed `SET_ROLE`, then a judge that names the new id.
10. **SETUPUX1j — This machine's page** (web; after i).
    - *Contract:* Roles, *All roles*, the diagram, the *Suggested* line and the found changes.
    - *Proof:* every door of the dark Roles sections is re-pointed, held by a door census and `HelpCoverageTests`.
11. **SETUPUX1k — a workspace's page** (web; after d, g, i).
    - *Contract:* Workflow (its routes, kinds of task through `WORKFLOW_KIND`, the defaults rows, the *Repositories* list), Roles (its works and supervisor) and Sessions. Remote becomes a Details row.
    - *Proof:* the door census, `HelpCoverageTests`, and page tests.
12. **SETUPUX1l — a run's view** (web; after f).
    - *Contract:* §2.3's run view.
    - *Proof:* a release run that found none and one that found a release; stories at 300 px, at 45% of the window, and at 25rem.
13. **SETUPUX1m — lit** (web; after e, g, h, j, k).
    - *Contract:*
      - `daoris.agents.setup` is on by default.
      - Repositories' Setup and Workflow tabs go, and so do the Roles mode's list, strip and three sections.
      - A stored `roles` mode still reads as `setup`.
    - *Proof:* the opener tests for each old door, the door census, and the installed window.
14. **SETUPUX1n — the installed window** (the steward; last).
    - *Contract:* §3's seven flows on a home of several workspaces of mostly undriven repositories, with a refusal, a restart mid-review, a moved plan, and a release run of each outcome.
    - *Proof:* dated shots in en and zh, both themes, at 1546 and 680 px, with the run records.

---

## 6. What it leaves open, and what to measure first

**Open**

- **A role taken only by the steps that name it.** At most one role takes a work in a scope (`RoleCatalog.cs:311`), so a role a place adds for one step also takes all of that work there. A role that `RoleFor` never takes, only a step naming it, needs a driver design of its own.
- **The graph's `end` when steps follow it.** In the release preset the end is `line`, with three steps after it. This design draws *✓ work done here* on that step. Whether the end should instead be derived, and so leave the editor, is the driver's call.
- **What a review's changes do with no `back` wire** (DECIDE1c's recommendation). The inspector's row should say the driver's own sentence for it, and that wants a palette field.
- **Two person steps with one zh word, 查看.** The look's press reads 已审阅 while the review step's press is Accept. That needs a naming pass of its own.
- **Where a repository's own kinds of task are chosen,** where a workspace declares them.
- **Where a place's running work is listed.** No read lists a repository's runs, so no door from the Workflow section to *running now* is drawn.
- **Carried open from AGENTSPLACE1:** an *Undo* plan built from the last plan's *before* values, and whether Integration's queue sits beside Gates.

**Measure first**

1. **First set-up of an imported repository:** presses and minutes to *Apply*, on the installed window, in en and zh. Today's baseline is about fifteen presses across six screens; the target is five plus the acceptances.
2. **How much of a folded place page fits on screen:** the head and three folded lines within the first 400 px at 1546 and at 680 px. In *Set up…* at 680 px, how far down the page *Apply* sits.
3. **The editor task:** from Merge after you accept to the release workflow — time, refusals met and presses. Then the owner reads the release chart cold and answers "what happens when no release is pending?".
4. **Placement,** in measured stories: the release graph is drawn top to bottom at 80rem and at 39rem with the 14rem track, and is never laid left to right.
5. **Word conflicts:** `names:check --strict`'s count before and after row a.
