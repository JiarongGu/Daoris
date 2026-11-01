# FLOWCHART2: a proposal for the workflow flowchart, with Ask Daoris composing beside you

Status: sealed proposal, 2026-11-02, answering `docs/2026-11-02-flowchart-brief.md`. D223 is reserved. Nothing was built, run or measured for this proposal.

**Paths.** Web paths start at `src/Daoris.Web/src/`, driver paths at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, modules paths at `src/Daoris.Desktop/Daoris.Desktop.Modules/`, and service paths at `src/Daoris.Service/`. Documents are under `docs/`.

**Widths**, as in D221:
- **W** is 1546 px with the list open.
- **H** is the 680 px window.
- **N** is 25rem (400 px), the main area's floor.

**What I read:**
- the brief, D221's design (§1, §2.3, §4, §5 and §7), D222, D186 and its notes, and the flowchart design (§3, §4, §7 and §8);
- the workflow design §12.1, the platform language (§2 to §4, §6) and the UI/UX design (R3, R6, R7 and ZOOM1);
- `design/Flow.tsx`, `flowLayout.ts`, `flowInk.ts`, `workflow/graphFlow.ts`, `workflowFlow.ts`, `GraphFlow.stories.tsx` and the whole of `workflow/edit/`;
- `WorkflowPalette.cs`, `Nodes/NodeContract.cs`, `Nodes/NodeWires.cs` and the shipped `Defaults/workflows/*.json`;
- `Help/Proposals/HelpWorkflowProposals.cs`, `Workflows/Domain/WorkflowDiff.cs`, `HelpSetupProposals.cs`, `HelpRoom.cs`, `Course/Domain/ChatMessage.cs`, the service's `HelpWorkflowTool.cs` and `HelpProposalBox.cs`;
- `help/ProposalCard.tsx`, `help/where.ts`, `help/AskConversation.tsx`, `bridge/state/stateHub.ts`, `menus/commands.ts` and the web `package.json` and lockfile.

---

## 0. The proposal in twelve sentences

1. **Build it, don't adopt a library.** Extend `design/Flow` into a measured canvas with an edit mode, and keep Daoris's own layout. React Flow would bring a transformed viewport, coordinates and a second rendering engine. D186 has already measured the half of that it could lose on (dagre, `docs/decisions/D186.md:150-169`), and the other half breaks its traps (§3).
2. **One component set draws all four views:** the place's chart, the editor, a run, and a proposal's diff. Each step kind gets a `StepNode` variant, a decision's outcomes become **ports** on its lower edge, and each wire kind gets its own ink.
3. **The layout stays automatic, top to bottom, and positions are never saved.** There is no free placement, no drag-to-move, no zoom of its own and no minimap. The window's zoom (ZOOM1) already scales the chart, and the chart reflows at every level.
4. **Editing happens on the chart.** A step is added with a *＋* on a way on, with *＋* on a ◎ *No further step*, or from a searchable picker. An outcome is connected by choosing its target in the inspector, by dragging from its port, or by a keyboard *pick* mode that walks only the valid targets. Removing, moving within a chain, duplicating, and undo and redo are all on the chart too.
5. **The person never chooses a wire kind.** The row (*Once done*, *If you ask for changes*, *If it fails*, an outcome) plus where the target stands (later or earlier) decides between `after`, `when`, `failed`, `rejected` and `back`. That mirrors the driver's own rules (`Workflows/Nodes/NodeWires.cs:225-235, 503-518`).
6. **Refusals are marked where they are,** on the step and on the way on. A step's border takes declined's hue and its words take the danger ink. An edge keeps its ink and gains a ⚠ chip, because D186 point 6 says no edge wears a status hue. The head carries *n to put right*.
7. **Ask Daoris gets a new door, `draft`,** on the existing `workflow` kind and its existing `workflow_propose` tool. It is a proposal against the open draft's fingerprint, and its Apply saves nothing: it takes changes into the page's draft. The precedent is the `setup` kind's `place` door, whose Apply also writes nothing (`Help/Proposals/HelpSetupProposals.cs:152`).
8. **The agent is handed the draft in a new message field, `Draft`,** of its own and bounded, sent when the fingerprint changes. The preface, capped at 1200 characters, cannot carry a graph (`Course/Domain/ChatMessage.cs:20-21`). The room's allow-list has no way to read a file (`Help/HelpRoom.cs:57-68`).
9. **The proposal is drawn on the chart as a union drawing:** steps marked *New*, *To remove* or *Changed*, and ways on marked *new* or *to remove*. The person takes it whole or one change at a time, and a change's dependencies come with it.
10. **A stale proposal is refused change by change.** A change is taken only where the draft still holds what the change was made against; anything else is refused by name, with *Ask again*.
11. **D221's rows q to t survive, reshaped.** Row q becomes FLOWCHART2b. Rows r and t stand. Row s loses its fingerprint half to FLOWCHART2c. Two of D221's §2.3 details are amended (§5).
12. **Every pattern from the brief's list is either taken or rejected,** each with its reason (§6). The rejections cluster around one fact: a workflow here is a governed record of at most 24 steps, judged by the driver. It is not a drawing.

---

## 1. The component system (brief §2.1)

### 1.1 What today's pieces give, and what they lack

- **`Flow` is already the right kind of drawing.**
  - Its nodes are HTML placed by CSS rank by rank, and its edges are an SVG hidden from readers, drawn from measured boxes and redrawn on every resize (`design/Flow.tsx:122-151, 324-352`).
  - Its nodes are one list in reading order with a roving Tab stop. ↓ and ↑ move in reading order, → and ← follow an edge, and Enter opens a node's door (`:282-313`).
  - Every edge out of a node is a press in that node's foot (`:608-622`).
  - The layout reads only the graph's shape and holds no positions (`design/flowLayout.ts:1-24`).
  - It is **read-only**: Enter opens a door, and there is no selection, no insertion point and no port.
- **`graphFlow` draws the edges by kind, mostly.** It maps `when` onto the dashed `conditional` style labelled by its outcome (`workflow/graphFlow.ts:97-121`). It drops `data` wires entirely (`:183-184`). It draws a ◎ node for each unrouted outcome (`:232-243`).
- **The editor draws every step twice and asks for internals.**
  - Each step appears once in the `Flow` and again as a form card (`workflow/edit/WorkflowEditor.tsx:252, 255-270`).
  - Steps are added from a select at the foot (`:272-286`), and ways on through a per-step `WayForm` that asks for a wire kind, a target, an outcome or an `on`, and the ports (`:456-527`).
  - It asks for an id (`:303-313`) and shows each step's id (`:403`).
  - These are the "hard to use" surfaces D221 found (`docs/2026-11-01-setup-ux-design.md:66-69`).

### 1.2 The layers

| Layer | Home | What it is |
|---|---|---|
| **`FlowCanvas`** (molecule) | `design/FlowCanvas.tsx` (split from `Flow.tsx`) | The measured pane: placement, ranks, ink, labels, folds, roving focus, keys. It gains edit slots: `renderNode`, `selected` with `onSelect`, `pick` mode, `gap` (one affordance per path), edge `marks`, `ports`, extra `keys`, and `fold={false}`. |
| **`Flow`** (molecule) | `design/Flow.tsx` | `FlowCanvas` plus today's `FlowNodeBox`, with **its props unchanged**. Its other callers (`quests/QuestFlow.tsx:84`, `work/head/PartsFlow.tsx:56`, `work/head/SessionFlow.tsx:64`, `projects/git/LandingFlow.tsx:92`) see no change. |
| **The workflow's chart parts** (molecules) | `workflow/chart/` | `StepNode`, `PortRow`, `Terminal`, `HoldsGroup`, `EdgeChip`, `GapInsert`, `ChartBar`, `ProblemList`, `ProposalBar`, `ChangeList`. |
| **The editor's parts** (molecules) | `workflow/edit/` | `WorkflowCanvas` (the chart in edit mode), `StepInspector`, `WaysOnTable`, `StepPicker`, `SaveReview`, and `StepOptions` (kept). |
| **Organisms** (hold the hooks) | `workflow/edit/WorkflowEditPage.tsx`, `workflow/place/PlaceWorkflowView.tsx`, `workflow/WorkflowRunView.tsx` | Reads, presses, the draft's history, the open-draft registration, and the proposals read from `HELP_PROPOSALS`. |
| **Pure** | `workflow/graphFlow.ts`, `workflow/edit/graphDraft.ts` | `graphFlow` stays the **one drawing**: shapes, ports, `data` ways, diff union and refusal marks. `graphDraft` holds every edit operation. |

### 1.3 `StepNode`: one component, a variant per kind

**Anatomy,** in reading order. The surface stays D186's: `--raised`, `--line`, radius 6 (`docs/2026-10-20-flowchart-design.md:327`).

```
┌──────────────────────────────────────┐
│ ◆ Find a pending release   [ pill ]  │ 1 glyph · title (wraps, never cut) · one pill: run state | New/To remove/Changed | ⚠ n
│ Local release · Daoris performs it   │ 2 who acts: a role (authored, D222), "You", "Daoris", or the plugin's authored title
│ ⚑ Work done here                     │ 3 the end's mark, with its foot "Steps after it still run" where steps follow
│ → On the line, once done             │ 4 the foot: one press per way on that is not a port (Flow.tsx:608-622)
├──────────────┬───────────────────────┤
│  release ●   │   none ○              │ 5 ports: one chip per declared outcome, routed ● or unrouted ○ (→ ◎)
└──────┬───────┴──────────┬────────────┘
       ▼                  ▼             each `when` way leaves its own port's chip
```

**Kinds.** The shape is carried by the glyph, the "who" line and the port row, never by a rotated box: a rotated diamond cannot wrap 中文 (trap 17). Only the terminals change outline.

| Variant | Kinds it draws | Glyph (exists / to add) | Who line | Ports | Notes |
|---|---|---|---|---|---|
| `work` | `claimed` | `stepWork` (exists, `graphFlow.ts:69`) | the role that takes it (*Taken by*), else *its work's role* | — | never the end (`NodeContract.cs:315`) |
| `you` | `review`, `look`, `go-ahead`, `human` | `stepLook` (exists); `stepReview` (to add) | *You* plus the participation word | — | "If you ask for changes" leaves as a `rejected` or `back` way, never as a port (D221: a review is never drawn as a decision) |
| `check` | `check`, `opinion`, `work.requests` alone | `stepOpinion` (exists); `stepCheck` (to add) | *Daoris checks* / *Second opinion* plus its role | — | |
| `holds` | two or more `work.requests`, `quest.holds`, `content.holds` in a chain | `stepHold` (exists) | the members as words | — | `HoldsGroup`: *Daoris makes sure · 3 ▸*; opened by its press, or by a refusal or a proposed change inside it |
| `decision` | a plugin step with `outcomes` | `stepDecision` (to add, ◆) | the plugin's authored title (D222) | **one per outcome** | outcomes are codes, shown as code |
| `plugin` | a plugin step without outcomes | `stepPlugin` (to add, ▣) | the plugin's authored title | — | |
| `landing` | `land`, `handed` | `stepMerge` / `stepBranch` (exist) | *Daoris lands it · merge* | — | the *After it lands* divider stands below it (`graphFlow.ts:260-265`) |
| `line` | `line`, `pushed`, `pull-request` | `stepLine` (to add); `stepPullRequest` (exists) | *Git's proof* / *the platform's answer* | — | usually carries ⚑ |
| `other` | `request`, `wait.unit`, `integrate`, `quest.close`, `all`, `stop` | `tool` | the kind's words | — | |
| `unknown` | a kind this page has no words for | `tool` | *shown as recorded* plus the kind as code | as declared | never dropped (trap 10) |

**The terminals** (`Terminal`) are small rounded pills, the flowchart's terminator shape. They are drawn only where D221 draws them:
- ◎ *No further step*, a drawing-only node for each unrouted outcome (`graphFlow.ts:232-243`). In edit mode it is a **press**: *＋ Add a step on ‹outcome›*.
- *Finished*, only from a run's record.
- *The run stops*, only inside a selected step's port row and in the inspector, never as a node. D221 says it is no wire and never an inserted `stop`.

**The states of a `StepNode`.** Each is said in words; none rests on hue alone.

| State | Drawn | Said to a reader |
|---|---|---|
| rest | 1 px `--line` | title, who |
| focused | the platform's 2 px accent focus ring, offset | — |
| **selected** (edit) | 2 px `--accent` border. The accent marks only what is pressed (flowchart design §3.8). | "selected" in its name (`aria-current="true"`) |
| current (run) | 2 px `--ink-soft` (`Flow.tsx:587`) | `aria-current="step"` |
| not reached / faint | dashed `--line-strong`, words at `--ink-soft`, never opacity (trap 9) | "not reached" |
| **refused** (edit) | 1 px `--st-declined` border (a border is a mark and keeps its hue, `docs/2026-09-19-platform-ux.md:122`), plus a chip *⚠ 1* in `--ink-danger` | "1 to put right: ‹the driver's sentence›" |
| **New** (diff) | 1 px `--st-done` border; pill *New* in done's tone | "proposed by Daoris, not taken" |
| **To remove** (diff) | dashed `--line-strong`, title struck through; pill *To remove* in declined's tone | "Daoris proposes removing it" |
| **Changed** (diff) | pill *Changed · method* (neutral); the fields in the inspector | "Daoris proposes changing its method" |
| pick: can take it | dashed accent outline, *Can go here* on hover | in the walk: "can take ‹row›" |
| pick: cannot | unchanged; the arrows skip it | — |

A diff's words wear their inks (`docs/2026-09-19-platform-ux.md:103-105`): the added count in done's ink, the deletion in the danger ink.

### 1.4 Ports: `PortRow` and `Port`

- **Only a decision has ports.**
  - One chip per declared outcome, in the declared order, on the lower edge inside the node. They are HTML, so they wrap. Each carries `data-flow-port="‹node›:‹outcome›"`.
  - `flowInk` anchors that outcome's `when` path at the chip's box, where today it spreads a node's paths by where each goes (`design/flowInk.ts:17-18`).
  - `layoutFlow`'s first ordering seeds a decision's targets in its outcomes' order, so the four sweeps start from the uncrossed order.
- **A port is a foot line made visible.**
  - Its accessible name is "release: on to Record the release", or "none: no further step".
  - Pressing it moves the focus to its target, exactly as a foot line does (`Flow.tsx:615`). The foot then omits that line, so nothing is said twice.
- **States:**
  - routed ●, or unrouted ○ (drawn to ◎);
  - in edit mode, *pick from here* (the port is the source of a pick or a drag);
  - refused (⚠, the danger ink);
  - diff (*new* / *to remove*).
- **Failure and changes are not ports.**
  - They are rows of the outcome table, drawn as edges only when routed: short dashes for `failed`, and dashes or a return lane for `rejected` and `back`.
  - When the step is selected, its port row also shows ○ *If it fails: the run stops* and ○ *If you ask for changes: …* at its right end, set apart. This follows BPMN's boundary event: what goes wrong sits on the edge, apart from the outcomes.
  - This keeps D221's settlement that "a step's ways on are all visible" without cluttering every step at rest.

### 1.5 Edges: one ink per wire kind

D186 point 6 stands: an edge is ink and never a status hue. The cause is always in the source's foot or port, and in the reader's description.

| Wire kind | Ink | Leaves from | Label chip | In edit mode |
|---|---|---|---|---|
| `after` | solid, `--ink-faint` 1 px, arrow | the lower edge | none (its foot says *once done*) | **`GapInsert` ＋** at the path's middle in the rank gap |
| `when` | **solid** from its port | the port's chip | none (the port names it) | — |
| `back` | dashed, on its own lane at the left (`flowLayout.ts:22-23`) | the far side, then the gap | *↺ back* plus `on`'s word | — |
| `failed` | short dashes | the lower edge, right | *if it fails* | — |
| `rejected` | dashes | the lower edge, right | *if you ask for changes* (on a look: *if you say not yet*) | — |
| `data` | **dotted, 1 px, drawn only while either end is selected** | the source's lower edge | the output's name (code) | listed under *Carries* in the inspector |
| `trigger` | solid | the lower edge | *starts* | under *More ways on* |
| `wait` | not an edge: a chip on the `wait.unit` naming what it waits on | — | — | under *More ways on* |

**`EdgeChip`** is the one label component. It holds a word or two on the page's colour, placed only where it clears every node, label and other chip (as today, `flowInk.ts:19-20`). It also carries an edge's **mark**:
- ⚠ with the problem's short word, in `--ink-danger`;
- *new* in `--ink-done`;
- *to remove* in `--ink-danger`, with the path drawn faint and dashed.

A chip that does not fit is dropped. Nothing is lost, because the foot or port and the description say it all.

**`GapInsert`** is a 28 px press, the platform's floor. It sits in the label's place on an `after` path, which carries no label. It is hidden on a way on that is only proposed. In the drawing it is not a Tab stop: the keyboard reaches it as the source node's foot line *＋ Add a step here* (§2.3).

### 1.6 The canvas

- **What it is:** `FlowCanvas`, a pane that lays out by its own width (R7). It places the drawing across, down or as the rail (`flowLayout.ts:283-293`). The workflow's sections keep it down or as the rail, never across (`Flow.tsx:221`).
- **Fit:** the placement *is* the fit, to the width. The height scrolls natively.
  - There is no scale-to-fit: the flowchart design's trap 15 forbids a `transform` scale over text (`docs/2026-10-20-flowchart-design.md:615`).
  - *Show the selected step* scrolls a selection or a jump into view (`block: 'nearest'`). That is Figma's *zoom to selection* without the zoom.
- **Pan:** the pane's own scroll, by wheel, Shift+wheel, touchpad or scrollbar.
  - A drag on empty ground does nothing. A drag from a port connects.
  - The bar (`ChartBar`) and the proposal strip stick to the pane's top.
- **Zoom: none of its own.**
  - The window's zoom (Ctrl+wheel, Ctrl+= / Ctrl+- / Ctrl+0; `docs/2026-10-20-ui-ux-design.md:1125-1129`) scales the root size. The nodes are rem and the ink is redrawn from measured boxes, so the chart reflows at each level.
  - The Map keeps a zoom of its own only because it is SVG in its own units (`:1140-1141`).
- **Minimap: none** (rejected in §6).
- **Folds:** on in the read views (the chart, a run). **Off in the editor** (`fold={false}`), because a fold hides what is being edited. An authored graph is bounded at 24 steps (D157); past that, the editor scrolls.

### 1.7 The same components in four views

| | The place's chart | The editor | A run's view | A proposal's diff |
|---|---|---|---|---|
| Mounted by | `PlaceWorkflowView` (row o) | `WorkflowEditPage` | `WorkflowRunView` (side bar) | `WorkflowEditPage` overlay; read-only elsewhere |
| `StepNode` | who and one fact from the cell; Enter opens the step's setting door | Enter **selects**; ports pressable; refusal chips | the run's pill (`runTone`), current, not reached, *route not taken* | New / To remove / Changed, over the draft's own state |
| Edges | all kinds; `data` hidden | plus `GapInsert`; `data` on selection | no edge drawn taken (the run keeps no path, D186's FLOWCHART1d note) | *new* / *to remove* chips |
| Canvas | folds on | folds off; pick mode | folds on (a run can pass 24) | the union of draft and proposal, laid out once |
| Bar | *Change…*, *Edit steps…*, *✦ Ask Daoris*, *Show as list* | name, ↶ ↷, *⚠ n*, *✦*, *Close*, *Review and save* | the bound version, how it was chosen | `ProposalBar`: Take all, One by one, Discard |
| At 300 px | — | — | the rail | — |

### 1.8 The canvas at W, H and N (the editor, with a proposal open)

```
1546 · W · the chart and the inspector side by side while the page holds 64rem (D221 §2.3)
┌ engine › Workflow › Edit steps ───────────────────────────────────────────────────────────────────────────────────────────┐
│ Release for engine · from v2 · not saved     ↶ ↷   ⚠ 1 to put right ▾   ✦ Ask Daoris   [ Close ]   [ Review and save ] │
│ ┌ ✦ Daoris proposes 3 steps and 4 ways on — "add a release after the line" · these hold · Claude Code, opus ──────────┐ │
│ │                                                       [ Take all ]  [ One by one ▾ ]  [ Discard ]                    │ │
│ └──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘ │
│ ┌ The steps ──────────────────────────────── [ Show as list ] ┐ ┌ Find a pending release ───────────── Local release ┐ │
│ │ Before it lands                                              │ │ New · proposed by Daoris        [ Take ] [ Leave ] │ │
│ │ ╭────▶ ┌ ◯ The work ─────────────────┐                       │ │ Finds a release the line is waiting to publish.    │ │
│ │ │      │ Builder                     │                       │ │ What happens next          Goes to                 │ │
│ │ │      └──────────────┬──────────────┘                       │ │   release      → [ Record the release   ▾ ]        │ │
│ │ │                     ＋                                     │ │                   carries its release              │ │
│ │ │      ┌ ◇ Daoris makes sure · 3 ▸ ──┐                       │ │   none         → [ No further step      ▾ ]        │ │
│ │ │      └──────────────┬──────────────┘                       │ │   If it fails  → [ The run stops        ▾ ]        │ │
│ │ ↺ back               ＋                                     │ │ ▸ More ways on        ▸ Details                    │ │
│ │ if you ┌ ● Your review ──────────────┐                       │ │ ⋯  Work is done here · Duplicate · Remove…         │ │
│ │ ask for│ You accept it               │                       │ └────────────────────────────────────────────────────┘ │
│ │ changes└──────────────┬──────────────┘                       │                                                        │
│ │ ╰──────┐              ＋                                     │                                                        │
│ │        ┌ ⤓ The landing · merge ──────┐                       │                                                        │
│ │        └──────────────┬──────────────┘                       │                                                        │
│ │ After it lands ───────┼────────────────────────              │                                                        │
│ │        ┌ ═ On the line ──────────────┐                       │                                                        │
│ │        │ ⚑ Work done here            │                       │                                                        │
│ │        │ Steps after it still run    │                       │                                                        │
│ │        └──────────────┬──────────────┘  new                  │                                                        │
│ │        ┏ ◆ Find a pending release  [New] ┓  selected         │                                                        │
│ │        ┃ Local release                   ┃                   │                                                        │
│ │        ┠─── release ● ─────── none ○ ────┨                   │                                                        │
│ │        ┗━━━━━━━│━━━━━━━━━━━━━━━│━━━━━━━━━┛                   │                                                        │
│ │   ┌ ▣ Record the release [New] ┐   ( ◎ No further step )    │                                                        │
│ │   │ Local release              │                             │                                                        │
│ │   └────────────┬───────────────┘ ┄┄ release (carries, shown while selected)                                          │
│ │   ┌ ▣ Copy to its registry [New] ┐                           │                                                        │
│ │   └──────────────────────────────┘                           │                                                        │
│ │ [ ＋ Add a step ]                                            │                                                        │
│ └──────────────────────────────────────────────────────────────┘                                                        │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

680 · H · the drawing whole, top to bottom at the 14rem track; the inspector a Drawer (design/ui.tsx:942); Ask Daoris by Quick Ask
┌ engine › Edit steps ───────────────────────┐┌ Find a pending release ────── ✕ ┐
│ Release for engine · not saved             ││ New · proposed  [ Take ][ Leave ]│
│ ↶ ↷  ⚠ 1 ▾  ✦  ⋯         [ Review and save ]││ release → [ Record the re… ▾ ]   │
│ ✦ 3 steps, 4 ways on proposed ▾            ││    carries its release           │
│                  [ Take all ] [ Discard ]  ││ none    → [ No further step ▾ ]  │
│ Before it lands                            ││ If it fails → [ The run stops ▾ ]│
│ ╭▶ [ ◯ The work · Builder ]                ││ ▸ More ways on   ▸ Details       │
│ │          ＋                              ││ ⋯                       [ Done ] │
│ │  [ ◇ Daoris makes sure · 3 ▸ ]           │└──────────────────────────────────┘
│ ╰─ [ ● Your review · You ]                 │
│    [ ⤓ The landing · merge ]               │
│ After it lands ──────────────────          │
│    [ ═ On the line · ⚑ ]                   │
│    ┏ ◆ Find a pending release · New ┓      │
│    ┠ release ● ──── none ○ ┨               │
│  [ ▣ Record… · New ]  ( ◎ No further step )│
│  [ ▣ Copy… · New ]                         │
│ [ ＋ Add a step ]                          │
└────────────────────────────────────────────┘

400 px · N · the rail: each step a row with its ways on as words, ＋ rows between; the inspector a full-width Drawer
│ Edit steps · not saved      ↶  ⚠1  ✦  ⋯ │
│ ✦ 3 steps, 4 ways on proposed ▾        │
│                 [ Take all ] [ Discard ]│
│ Before it lands                         │
│ 1 ◯ The work · Builder                › │
│   ＋ Add a step here                     │
│ 2 ◇ Daoris makes sure · 3             › │
│ 3 ● Your review · You                 › │
│   ↺ If you ask for changes: back to 1   │
│ 4 ⤓ The landing · merge               › │
│ After it lands                          │
│ 5 ═ On the line · ⚑ Work done here    › │
│   → 6, new                              │
│ 6 ◆ Find a pending release · New      › │
│   ├ release → 7 Record the release      │
│   └ none → No further step   ＋         │
│ 7 ▣ Record the release · New          › │
│   carries its release from 6            │
│ 8 ▣ Copy to its registry · New        › │
│ [ ＋ Add a step ]                       │
│ [ Review and save ]                     │
```

**The read views.**

```
1546 · W · the place's Workflow: routes at the left, the chosen route's chart at the right (D221 §2.1); a cell's one fact on each step
│ ▾ Workflow  [ Change… ] [ Edit steps… ] [ ✦ ]│ Release for engine v2, the saved workflow default work follows        │
│ ▌ Default work  → Release for engine v2     │ Before it lands                                                       │
│   Documentation → Documents workflow v1     │ ╭▶ ┌ ◯ The work · Builder · opus, high ┐                              │
│   Landed work   → No verification here      │ │  … ┌ ◆ Find a pending release ┐                                     │
│                                             │      ├ release ● ─── none ○ ┤ → Record → Copy   ( ◎ No further step ) │

300 px · the side bar's Workflow view (a run): the rail, the same rows with the run's pills
│ Release for engine v2 · chosen here        │
│ At Your review · waiting on you            │
│ ✓ The work                       done      │
│ ✓ Daoris makes sure · 3          done      │
│ ● Your review  [ waiting on you ]          │
│   ↺ If you ask for changes: back to 1      │
│   The landing                 not reached  │
│   Find a pending release      not reached  │
│   ├ release → Record, Copy                 │
│   └ none → No further step                 │
```

---

## 2. Layout and editing (brief §2.2)

### 2.1 The layout: automatic, top to bottom, nothing saved

- `layoutFlow` stays the engine, deterministic and reading only the shape (`flowLayout.ts:110-205`). The editor adds one input: a decision's targets are seeded in its outcomes' order (§1.4).
- **No positions are saved,** in the version or anywhere else.
  - A position kept in the graph would be presentation inside a record the driver reads, digests and judges.
  - Every drag would make a new digest, and so a new version for the same meaning.
  - That is exactly the bpmn.io pattern of storing diagram geometry beside the model, and D157 and D186 rejected it (`docs/decisions/D186.md:63-64`).
- **What moves on an edit, said honestly.** "A graph that grows is laid out again, and its nodes may move" (`docs/2026-10-20-flowchart-design.md:279-282`).
  - In a top-to-bottom chain, an insert pushes everything below it down one rank. People expect that from Zapier and Power Automate.
  - Two things soften it:
    1. the focus and the selection move to the new step, and *Show the selected step* keeps it in view;
    2. placing is never animated (`:425-426`), so nothing slides.
  - **I would measure it first:** across every shipped graph and every insertion point, how many nodes change rank or order per edit (§3.4).

### 2.2 The inspector

D221's inspector stands (`docs/2026-11-01-setup-ux-design.md:365-408`). It sits beside the chart at W, and is the `Drawer` at H and N. It reads in this order:

1. Its title, *by ‹plugin›*, and what it does: the palette's `reachBy` and `failureBy` in words, and the plugin's authored `about`.
2. **Its refusals**, at the top, each with *Ask why* (§4.6).
3. **A proposed change**, where one touches this step: *New · proposed by Daoris [Take] [Leave]*, or *Changed: method `merge` → `squash` [Take] [Leave]*.
4. ***What happens next***, `WaysOnTable`. Its rows come from the palette's `leaves` (`WorkflowPalette.cs:78-90`): *Once done* with *＋ also to…*; *If you ask for changes* (on a look, *If you say not yet*); *If it fails*; then every declared outcome. Each row has *Goes to [▾]* offering a later step, *Back to* an earlier one, *No further step*, *The run stops* (failure only) and *＋ A new step…*. **The wire kind is derived, never chosen:**

   | Row | Later step (or a sibling branch) | Earlier step | Nothing |
   |---|---|---|---|
   | Once done | `after` | refused, with "*Once done* only goes on; a way back is taken on changes, a failure or an outcome" | no wire |
   | If you ask for changes | `rejected` | `back`, `on: rejected` | no wire (the run waits for you) |
   | If it fails | `failed` | `back`, `on: failed` | no wire (*the run stops*) |
   | outcome *x* | `when`, `outcome: x` | `back`, `on: x` | ◎ *No further step* |

   *Earlier* means an ancestor along the ordered wires (`after`, `trigger`, `data`, `when`), as `NodeWires` computes it (`Workflows/Nodes/NodeWires.cs:265-267, 503-518`). The page's check only advises; the driver's judgement decides.
5. ***Carries***, the `data` ways, proposed from the routed predecessor (row r).
6. Options in chrome words (row r), and *Taken by* (row t).
7. *▸ More ways on* (`trigger`, groups, ways the editor does not draw, kept as written by `graphDraft`), then *▸ Details* (the step's id, kind and version).
8. ⋯, the step's menu: *Add a step after*, *Move earlier*, *Move later*, *Duplicate*, *Work is done here*, *Ask Daoris about this step*, *Remove…*.

With nothing selected, it says *Choose a step to see what happens next*, and lists the holding judgement or *n to put right*.

### 2.3 The interactions, each with its keyboard path

The keyboard path assumes the chart's one Tab stop: the selected step, else the current one, else the first (`Flow.tsx:246-249`).

| Interaction | Pointer | Keyboard | Result, and what a reader hears |
|---|---|---|---|
| Select a step | click it | ↓ ↑ → ← to it, then **Enter** or Space | Inspector beside (W) or Drawer (H, N). "Find a pending release, selected." Escape closes the Drawer, and focus returns to the step. |
| Add between two | the ＋ on an `after` way | on the source step: **+** or Insert, or Tab to its foot line *＋ Add a step here* and Enter | Picker titled *After Your review, before The landing*. `insertBetween` turns A→B into A→N→B, keeping the wire's kind. The new step is focused and selected. "Added Check between Your review and The landing." |
| Add at an outcome | the ◎, or an unrouted ○ port | focus ◎, then Enter; or on the decision **+**, which asks *on which way: Once done · release · none* | `addOnOutcome` makes a `when` wire. The ◎ goes. |
| Add from the palette | *＋ Add a step* under the chart | Tab to it, Enter | The picker asks *after: [the selected step ▾]*. With no free way on, the step becomes a new start, and the picker says so. |
| The picker (`StepPicker`) | type to filter | type; ↓ ↑; Enter; Escape | Searchable and grouped as D221 sets out: *The work*, *You*, *Checks*, *Landing*, *Other repositories*, each plugin by its steps' authored titles, and *Other* with the door to Plugins. Kinds that cannot stand here (a second landing; a `review` after `land`) are listed last, with why. |
| Route a way (choose) | *Goes to [▾]* in the inspector | Tab to the row, Space, ↓ ↑, Enter | The derived wire is made or rewired. "release now goes on to Record the release." |
| Route a way (drag) | drag from a port chip, or from a ○ row's handle; the targets that can take it are outlined, the rest say why on hover; drop | — (its twin is *pick*) | Same as choose. **Dropping on empty ground opens the picker** for a new step on that way (Blueprints' drag-off-a-pin, React Flow's add-node-on-edge-drop). |
| Route a way (pick) | the port's ⋯ → *Choose on the chart* | on a port: **Enter** → pick mode. ↓ ↑ walk **only the steps that can take it**; Enter picks; Escape cancels. | Live: "Choose where release goes: 4 steps can take it. Enter picks, Escape cancels." |
| Move within a chain | ⋯ → *Move earlier* / *Move later* | **Alt+↑** / **Alt+↓** on the step | `moveInChain`, only where the step has one `after` in and one `after` out. Elsewhere: "It has more than one way in, so it moves by its ways on." |
| Duplicate | ⋯ → *Duplicate* | the menu key or Shift+F10, then *Duplicate* | A copy with a new id and no ways on, added after the original by `after`. |
| Remove | ⋯ → *Remove…*, or *Remove…* in the inspector | **Delete** on the step | `InlineConfirm` (`work/InlineConfirm.tsx:181`): "Remove *Check*? Its 2 ways on and 1 way in go with it." The moves: **[Remove and join Daoris makes sure to Your review]**, offered only in a straight `after` chain, [Remove only], [Never mind]. Focus goes to the step above. |
| Undo and redo | ↶ ↷ in the bar | **Ctrl+Z**, **Ctrl+Y** or **Ctrl+Shift+Z** outside a field, through the Edit menu, which today is present only in a field (`menus/commands.ts:354-355`) | "Undone: added Check." A change taken from Ask Daoris is one step. |
| Mark the end | ⋯ → *Work is done here* | the menu | ⚑ moves; the foot says *Steps after it still run* where they do. |
| Jump to a problem | *⚠ 1 to put right ▾*, then a row | Tab to it, Enter, ↓, Enter | Focus goes to the step, the inspector opens at the row, and the holds group opens where needed (`WorkflowEditor.tsx:218-221`). |
| Into a step's controls | — | Tab from the step reaches its ports, foot lines and ＋; Escape returns to the step (`Flow.tsx:288-295`) | |
| The step's menu | right-click | the menu key or **Shift+F10** (D138; `docs/2026-09-19-platform-ux.md:737-739`) | a `menu` named for the step |
| Show as list | *Show as list* | Tab | the rail at any width, remembered (`Flow.tsx:113-114`) |
| Ask Daoris | *✦ Ask Daoris* | **F1** / Ctrl+Alt+I; Quick Ask Ctrl+Shift+Alt+L (`menus/commands.ts:515, 520`) | the conversation, with the draft handed to it (§4.2) |

**Every key respects the input method** (`isComposing`, `Flow.tsx:283`). A key aimed at a field is the field's own, so Ctrl+Z in an option's text box undoes the text, not the draft.

**Readers.**
- The chart stays one `role="list"` (`Flow.tsx:439`).
- In edit mode, each step's description gains:
  - its problems: "1 to put right: ‹sentence›";
  - its diff state: "Daoris proposes adding this step; not taken";
  - its ports, as foot lines.
- A polite live region announces each edit, take, undo and judgement: *Judging…*, *These steps hold*, *1 to put right*.
- `ChangeList` (§4.4) is the proposal's equivalent for readers, so no one needs to read the drawing.

### 2.4 What a refusal from the judge highlights

- The judgement is a planned `WORKFLOW_SAVE`.
  - It carries the **draft's fingerprint** (D221 row s). An answer to an older fingerprint is discarded; today's editor keeps any answer (`WorkflowEditor.tsx:143-150`).
  - Each problem names a node or a wire (`answers.ts:36`).
- **On a step:** a declined-hue border, the ⚠ *n* chip in the danger ink, and the sentence at its row in the inspector.
- **On a way on:**
  - the edge keeps its ink, and its `EdgeChip` becomes ⚠ plus the code's short word, in the danger ink;
  - the source's foot line or port carries the sentence;
  - the inspector row for that way shows it.
- **Neither** (a cycle across several steps, a general refusal): the head's *n to put right ▾* lists it. Its door goes to the first step it names.
- A refusal inside a closed holds group opens the group (kept from `WorkflowEditor.tsx:218-221`).
- Never hue alone: every mark has a word, and the sentence is the driver's (`WorkflowEditor.tsx:66-67, 349-358`).

### 2.5 The narrow window and 25rem

- **H, 680 px window, about 544 px of main area.** The release graph draws top to bottom at the 14rem track: a decision's two ways (2 × 14 + 1.5rem) plus one return lane fit (`docs/2026-11-01-setup-ux-design.md:297-299`).
  - The bar wraps to two rows, and the proposal strip folds to one line.
  - The inspector is a Drawer. Ask Daoris is Quick Ask: opening the side bar at 680 px would leave the main area at its 400 px floor, and the chart would become the rail.
- **N, 25rem.** The rail, in which every edit is still possible:
  - *＋ Add a step here* rows sit between steps;
  - a decision's outcomes are sub-rows `├ release → …`, `└ none → No further step ＋`;
  - Enter opens the full-width Drawer;
  - pick mode walks the rail's rows;
  - diff marks are words (*· New*, *· To remove*), never only an outline.

---

## 3. Build or adopt (brief §2.3)

### 3.1 Evidence verified in this repository

- **The web depends on no graph library.**
  - The `dependencies` are Radix, Shenora, TanStack Query, xterm, i18next, lucide and react-markdown (`src/Daoris.Web/package.json:22-44`).
  - The lockfile holds no `@xyflow`, `reactflow`, `dagre`, `@dagrejs`, `elkjs`, `d3-zoom` or `zustand` (searched `src/Daoris.Web/package-lock.json`).
- **D186 has measured a library against the hand layout** in the measured harness (`src/Daoris.Web/playwright.measured.config.ts`; `measured/flow.spec.ts`):
  - dagre ran edges through nodes on the dense graph (7 and 8);
  - it added a crossing to small flows;
  - its coordinates came out wider than the pane;
  - the hand layout drew none through a box (`docs/decisions/D186.md:156-169`).
- **`Flow`'s shape is one CSS pass that reflows by itself** on a resize or a language change. Nothing it measures moves a box (`Flow.tsx:324-352`; trap 14). Unmeasured, it is the rail, so jsdom tests hold its roles, names and keys while the measured harness holds its geometry (`Flow.tsx:129-130`).
- **Seven call sites draw through `Flow` today** (§1.2), with measured stories at 80, 39 and 25rem in both languages (`workflow/GraphFlow.stories.tsx:14-15, 29-42`).
- **D186 rejects a drawing per flow** (`docs/decisions/D186.md:73`). The flowchart design rejects panning, zoom, dragging and a `transform` scale over text (`docs/2026-10-20-flowchart-design.md:259-260, 615, 633`), and leaves the minimap out (`:655`).
- **The window's zoom already scales rem** (ZOOM1, `docs/2026-10-20-ui-ux-design.md:1115-1120`).
- **The installed window is Chromium** (D92, `:1097`).

### 3.2 Evidence about React Flow (`@xyflow/react`), from what I know of it, not verified here

- **Licence:** MIT.
- **Size:** about 50 KB gzipped with its runtime dependencies (`@xyflow/system`, d3-zoom, d3-drag, d3-selection, zustand, classcat) by public bundle trackers. Not measured here. Against today's single chunk of about 905 KB gzipped (`docs/decisions/D186.md:65-66`), that is roughly 5%.
- **Rendering:**
  - Nodes are React components (HTML, so 中文 wraps), positioned absolutely from coordinates the caller supplies.
  - The viewport is a CSS `transform: translate(…) scale(…)` over the nodes. At any zoom but 1, text is scaled: trap 15.
  - There is no layout; its own layout examples feed dagre, ELK or d3-hierarchy coordinates.
  - Built-in edges (bezier, step, smoothstep) do not route around nodes in a skipped rank. Daoris's ink threads waypoint slots and return lanes (`flowInk.ts:11-16`).
- **Theming:** CSS variables (`--xy-*`) and a `colorMode`. The tokens could be mapped.
- **Accessibility:**
  - Nodes and edges are focusable; Enter and Space select; arrows move selected nodes; Delete removes; there is a live region for moves and configurable aria labels.
  - Its focus order is DOM order, not the rank-by-rank reading order D186 requires.
  - It has no node description of every way in and out, and no roving single Tab stop.
- **Testing:** in jsdom it needs `ResizeObserver` and `DOMMatrixReadOnly` stubs, and edges render only once nodes are measured. That is the same limit as `Flow`, with a stub layer added.
- **Recipes:** several of its example recipes (auto layout, undo and redo, copy and paste, helper lines) are, as far as I know, its paid "Pro" examples. Not verified.

### 3.3 The judgement: build

| | Extend `design/Flow` (proposed) | Adopt React Flow for the workflow |
|---|---|---|
| Layout | ours, locked by D186's measured table | coordinates from somewhere. ELK is rejected (size, licence; D186); dagre was measured worse here; ours would need converting from ranks to coordinates, losing the one CSS pass |
| Routing | ours: waypoints, return lanes, ports to add | built-in edges cross skipped-rank nodes; custom edges would re-host `flowInk` |
| Text and languages | HTML in CSS tracks; reflows in one pass | HTML, but sized and then placed: two passes on every language change and resize |
| Zoom | the window's, which reflows | its own transform: trap 15 unless pinned at 1, which turns off what it is for |
| Readers and keys | D186's list, descriptions and roving stop, tested (`Flow.test.tsx`) | rebuilt on top of its DOM order and handles |
| Other flows | one primitive for all seven call sites | a second engine beside `Flow` (D186 rejects a drawing per flow), or moving all seven and re-measuring about 50 stories |
| What it gives | — | handles, drag-to-connect, selection, viewport, minimap. Handles and drag are a few hundred lines on `FlowCanvas`; the viewport and minimap are rejected anyway (§6) |
| Licence and size | none added | MIT, about 50 KB gzipped (unmeasured) |
| Tests | jsdom for structure, measured harness for geometry (exists) | the same split, plus stubs |

**Build.** What React Flow would give is either already rejected for this product (viewport, zoom, minimap, free placement) or cheap on `FlowCanvas` (ports, a drag line, selection). What it would cost (two-pass layout, trap 15, a second engine, rebuilding the readers' model) is exactly what D186 measured or ruled against.

### 3.4 What to measure before locking it, as D186 locked dagre

1. **Edit latency on `FlowCanvas`.** On the dense fixture (32 nodes, the bounds' 96 wires) and the release graph, measure layout plus ink per edit in Chromium. The budget: `layoutFlow` under one frame (16 ms), and `routeInk` plus labels under 50 ms. Every edit re-lays out (`Flow.tsx:218-219`).
2. **Movement per edit.** For each shipped graph and each insertion point, count the nodes whose rank or order changes. This judges "what moves on an edit" (§2.1).
3. **The measured-box check with the editor's parts.** No `GapInsert` overlapping a node, a label or another insert. Each `when` path starting inside its port's box. The crossing ceilings unchanged (`measured/flow.spec.ts`), at 80, 39 and 25rem, in en and zh.
4. **A one-day React Flow spike,** behind the same editor story, with scratch work that is not kept. Measure:
   - bytes added to the built chunk (gzip);
   - render passes on a switch from en to zh;
   - edges through a box using our coordinates and its built-in edges;
   - Narrator's reading of a branch and back;
   - the keyboard presses needed to send the review's changes back to the work.

   Lock the engine on that table.
5. **Dragging in the installed window.** Pointer events with `setPointerCapture` from a port, never HTML5 drag-and-drop, which a host may intercept for file drops. Measure in the installed window at 1546 and 680 px.
6. **The `Draft` field's size** (§4.2): the release graph plus its palette offer, in bytes, against the bound.

---

## 4. Ask Daoris composing beside you (brief §2.4)

### 4.1 A new door, `draft`, on the existing kind and tool

- **Not a new tool.** The room's tools are one per proposal kind (`HelpRoom.cs:67`), and the agent already knows `workflow_propose` (`Knowledge/Room/HelpRoomMayPropose.cs:134-145`).
- **Not a flag on `edit`.** A door names what its Apply does (`HelpWorkflowProposals.cs:47`).
  - `edit` saves a version against a saved base, refused stale by `BaseProblem` (`:378-392`).
  - A draft's Apply **saves nothing**: it takes changes into page state, and its staleness is the draft's fingerprint.
  - Two authorities and two staleness rules make two doors. The `setup` kind's `place` door is the precedent: "A place's Apply plans and presses nothing: it takes the person to the page" (`HelpSetupProposals.cs:152-153`).
- **The tool gains one parameter,** `draft`. It is one of:
  - `"<fingerprint>"`: the open draft this conversation was handed;
  - `"<workflow>@v<n>"`: a saved version;
  - `"preset:<id>"`: a shipped default.
- **A graph is accepted here for either pipeline.** Today a development pipeline's graph is refused, because "the person's part in a development pipeline's graph is not read yet" (`HelpWorkflowProposals.cs:119-123`; `HelpWorkflowTool.cs:43-45`). In the draft door nothing is saved by the proposal. The person's part is read where the save is reviewed: *Review and save* says the changed steps and the involvement (D221 row s). The `edit` and `new` doors keep their refusal unchanged.

### 4.2 What the room is told of the draft

- **The preface line, unchanged in kind.** It is capped at 1200 characters and says where the person is (`help/where.ts:104-149`), and it gains one clause: "editing the steps of workflow `release-for-engine` (from v2, not saved) for repository `engine`, draft `f3a91c…`, 9 steps, 1 to put right, *Find a pending release* selected". The selected step lets "what does this do?" be answered without a press.
- **A new message field, `Draft`,** of its own beside `Briefing` and `Process` (`ChatMessage.cs:87-106`). It is never the preface, which "is a few lines of where the person is, never a document" (`:20-21`). It carries:
  - `fingerprint`, `workflow` (id or null), `name`, `base` (version or null), `place` (repository or workspace);
  - `graph`, the draft as `graphOf` writes it (`graphDraft.ts:105-121`);
  - `judged`: holds, or the problems with their codes;
  - `palette`, the offer in compact form (each kind's `leaves`, outcomes, inputs and outputs, and option names), sent only when its own digest changed, since the room cannot read the palette otherwise.

  It is bounded at 64 KiB until measured (§3.4). It is sent only when the fingerprint changed since this conversation was last told, exactly as the preface is (`help/AskConversation.tsx:340-364`). The record keeps it as what the agent was told, read at `ConversationRoutes.cs:141` beside `preface`. Like everything Ask Daoris holds, it stays on this machine and never syncs.
- **The draft reaches the conversation through an `openDraft` registration.** The editor's organism registers it while mounted (fingerprint, the context above, and `take(changes)`). `AskConversation` reads it to send `Draft`, and `AskPanel` reads it to route a draft card's press (§4.4).

### 4.3 The judge and the diff

The driver's `HelpWorkflowProposals`, for the `draft` door, plans in this order:

1. **Finds the base.**
   - For a fingerprint: the `Draft` this proposal's conversation was told under it, looked up on the conversation's own record by `HelpProposal.Session`. If none, it refuses: "the draft it names was never shown to this conversation; ask again from the editor".
   - For `id@vN`: that version, which must read.
   - For `preset:id`: the shipped graph.
2. **Reads the graph** as the store would (`WorkflowNamed.ReadVersion` under a schema-2 version, as `GraphVersion` does at `HelpWorkflowProposals.cs:466-474`). An unreadable graph is refused with the reader's sentence.
3. **Judges it** with the save's own rules (`NodeGraphs` and `NodeWires.Problems`). Problems do **not** refuse the proposal: they become the card's *To put right*, and the person may take it and fix it. The tool's answer to the agent says them, so the agent can try again.
4. **Diffs base against proposal** with a new `Workflows/Domain/WorkflowGraphDiff.cs`, replacing `GraphLines` (`HelpWorkflowProposals.cs:531-549`). `GraphLines` compares wires by id alone and never says one changed.
   - **Steps match by id.** The agent sees the ids in `Draft`.
   - **Ways on match by structure first** (kind, ends, ports, outcome or `on`), **then by id**, so an agent that renumbers wire ids does not read as removing and re-adding everything.
   - Each entry: `{ id, what: step | way, change: added | removed | changed, step?, kind?, fields[], needs[], said }`. Its `needs` are its dependencies: an added way names its added ends; a removed step names its ways' removals.
   - `said` is the driver's English line, which the page words.
5. **Builds the card.** `HelpWorkflowCard` gains `Draft` (the base it names), `Changes` (the entries), `Problems`, and `Graph` (the proposed graph, already a field, `:657`). Its `Saves` is false.

**Apply** writes no workflow file and no choice. It settles the proposal as `applied`, recording the entry ids taken, which the page sends. **Dismiss** is today's.

### 4.4 On the chart: take it whole, change by change, or discard

- **The overlay.**
  - `WorkflowEditPage` reads `HELP_PROPOSALS`, which the `help` scope already asks again (`bridge/state/stateHub.ts:59`). It keeps the `draft`-door cards in state `proposed` whose base is this draft's fingerprint (or this version or preset).
  - `graphFlow` draws the **union** of draft and proposal once, marked from the driver's entries (§1.3, §1.5). The page draws marks; it never computes the diff itself, so there is one judge.
- **`ProposalBar`** sits above the chart and sticks:
  - "✦ Daoris proposes 3 steps and 4 ways on: *‹the person's words›*";
  - the judgement (*these hold* / *1 to put right*);
  - the agent and model the conversation ran on, because model-dependent work states the tier that ran;
  - [Take all] [One by one ▾] [Discard].
- **`ChangeList`** (*One by one*): a row per entry in the driver's order. Each row's name is a door to its step (focus and select), with [Take] and [Leave]. Taking a row takes its `needs` and says so: "takes *Record the release* with it". Leaving an added step leaves its ways. The same Take and Leave sit in the inspector of the step concerned.
- **What happens on Take:**
  - `applyChanges(draft, proposalGraph, entries)` writes the taken entries into the draft as one undo step ("Undo: Daoris's change").
  - An id the draft now uses (the person added their own `find` meanwhile) is renamed by `idFor` (`graphDraft.ts:124-128`), and the ways that name it follow.
  - The draft's new fingerprint is judged as any edit is.
- **Settling it.** Once every change is taken or left, the page sends `HELP_APPLY` with `taken: [ids]`; *Discard* sends `HELP_DISMISS`. Closing the editor with changes undecided leaves the card standing in Ask Daoris.
- **The card in Ask Daoris** (`help/ProposalCard.tsx:236-285`, a draft variant) shows:
  - the title *A change to your draft*;
  - the changes in the driver's lines, the judgement and the why;
  - **[Take into the draft]** where this window's editor holds that base (the same as Take all), else **[Open in the editor]**, which opens *Edit steps…* on that base with the overlay;
  - [Not now];
  - a closing line: *Nothing is saved until you press Save*.

  The card's `revision` (HELPAPPLY1) covers the proposal and its base.
- **Nothing is saved without the person's *Save*.** `WORKFLOW_SAVE` and `WORKFLOW_USE` are pressed only by the person, in *Review and save* and *Use here…* (D221 row s), and they change in nothing.

### 4.5 A stale proposal is refused change by change, never applied over

A proposal made against fingerprint F0, while the draft is now F1:

- **Each entry carries its precondition:** the step or way as it stood in F0, which the page has from the card's base.
- **Where F1 still holds exactly that, the entry can be taken.** A proposal to add the release after the line stays takeable while the person renames the workflow.
- **Where F1 changed what the entry touches,** the row reads "Changed in your draft since Daoris proposed this", with **[Leave]** and **[Ask again]**. *Ask again* sends the conversation the person's words again, together with the new `Draft`.
- **Whole-proposal refusals are the driver's:** an unknown fingerprint (§4.3), a saved version that has moved (`id@vN` no longer the newest, in `BaseProblem`'s words), or an unreadable graph.

### 4.6 How Ask Daoris explains a step or a refusal

- **Explanations that need no model.**
  - The inspector says what a step does from the palette and the plugin's `about`.
  - A refusal shows the driver's sentence, with the catalogue's wording where the page has the code (`WorkflowEditor.tsx:66-67`).
  - This works with no agent at all.
- **With a model:**
  - *Ask Daoris about this step* (⋯) and *Ask why* (on a refusal row) open the conversation (the side bar at W, Quick Ask at H and N) with the words filled in. For example: "Why is this refused: ‹sentence›?", or "What does *Find a pending release* do here, and what happens on *none*?", in the page's language.
  - The person presses Enter. The words are theirs, and the draft and selection go with them.
  - *Fix it* on a refusal fills in "Put this right: ‹sentence›", which naturally comes back as a `draft` proposal.
  - The answer's agent and model are the conversation's, shown as Ask Daoris shows them today (not re-read for this proposal).

### 4.7 Two requests, end to end

**"Add a release after the line"**, from the Merge preset in the editor (draft F1):
1. *✦ Ask Daoris*. The message carries the preface and `Draft{F1, graph, palette}`.
2. The agent calls `workflow_propose(scope: engine, workflow: "", why: "…", draft: "F1", graph: {…})`, adding `find`, `release` and `publish`, and wires `after` line→find, `when release`, `data` find.release→release.release, `after` release→publish, and `data` release.release→publish.release, as in `release.json:44-57`.
3. The judge: *these hold*; entries are 3 steps added and 5 ways on added, with `needs`.
4. The strip appears; the union is drawn with *New*. The person presses [Take all]: one undo step, F2 judged, *These steps hold*. `HELP_APPLY{taken: all}`.
5. *Review and save* → *Save as a new workflow* (`WORKFLOW_SAVE`, the person's) → *Use here…* → *Use here* (`WORKFLOW_USE`, the person's).

**"Send review changes back to the work"**: one entry, a `back` way from `review`, `on: rejected`, to `work`. It is drawn on the left lane marked *new*. The person takes it from the inspector of *Your review*.

### 4.8 Route and tool changes, and their authority

| Change | Where | Authority, before → after |
|---|---|---|
| `Draft` on a conversation's message | `ChatMessage.cs`, `ConversationRoutes.cs:141`, `help/AskConversation.tsx`, `help/where.ts` | tells the agent; writes only the conversation's own record. Unchanged in kind (like `preface`) |
| `workflow_propose` gains `draft` | `Daoris.Service.Mcp/HelpWorkflowTool.cs`, `Daoris.Service.Core/Help/HelpProposalBox.cs:156-160` and its writer | writes a proposal file, as today |
| door `draft` | `HelpWorkflowProposals.cs`, `HelpProposalKinds` doors | judges; its Apply **writes no workflow and no choice** and records the entries taken. Narrower than `edit` and `new` |
| `WorkflowGraphDiff` | `Workflows/Domain/` | pure |
| `HELP_APPLY` gains `taken` for a draft card | the help route | the same press, with a narrower effect |
| the room's paragraph on the draft door | `Knowledge/Room/HelpRoomMayPropose.cs` | words |
| **unchanged** | `WORKFLOW_SAVE`, `WORKFLOW_USE`, `WORKFLOW_PALETTE`, `WORKFLOW_LIST`, `WORKFLOW_SHOW` | the person's presses alone |

The places a change like this must reach are named by `.claude/knowledge/connector-tools.md` (a tool's parameter: its description test and the README row), `session-words.md` (a message's field), `bridge-presses.md` (a press's field) and `setting-doors.md`'s pattern for a door's coverage row. Each row in §7 names its own.

---

## 5. What D221's rows q to t become, and what of today's code is kept, moved or removed (brief §2.5)

### 5.1 The rows

| D221 row | Becomes | Why |
|---|---|---|
| **q**, the editor's page | **FLOWCHART2b**, which keeps every clause of q (page, inspector beside or in a drawer, keys, picker, ＋, confirmed removal, the outcome table, ids with their fallback, undo, refusal jumps) and adds the canvas's edit mode: ports, `GapInsert`, pick, drag, move within a chain, duplicate | q designed the page; this designs the chart it is drawn on |
| **r**, options, tails, inputs | stands, after 2b. Inputs proposed are drawn as *Carries* (dotted) while either end is selected | unchanged contract |
| **s**, save and choose | **split.** Judgement by fingerprint, and its marks on the chart, move to **FLOWCHART2c**, before Ask Daoris's rows, which need the fingerprint. *Review and save*, consent bound to the scope keys, a name-only save, and *Use here* only on an unchanged draft stay in s | the fingerprint is now shared |
| **t**, staffing a step | stands, after r. The chosen role is also drawn as the step's who line | unchanged contract |

**D221 is amended in two places:**
1. **§2.3's table, the branch row** (`docs/2026-11-01-setup-ux-design.md:304`): a `when` is drawn **solid from its labelled port**, not dashed. A dash now means only that something went wrong (a failure, a rejection) or a way back. This changes the run view too (`graphFlow.ts:106-108`).
2. **§2.3's removal** (`:390`), "never bridges the gap": the gap is never bridged **unasked**. In a straight `after` chain, the confirmation also offers *Remove and join*.

Everything brief §3 lists as settled stands: top to bottom, truthful endings, a step's ways on all visible, no id asked, the judgement tied to the exact draft, saving and choosing as two presses, and one role editor.

### 5.2 The files

| File | Fate | What |
|---|---|---|
| `design/Flow.tsx` | **split** | `FlowCanvas` (the core, gaining `renderNode`, `selected`, `pick`, `gap`, `marks`, `keys`, `fold`) and `Flow` (today's props, unchanged) |
| `design/flowLayout.ts` | keep, plus one input | a decision's targets seeded in their outcomes' order; nothing else changes, and the crossing table holds |
| `design/flowInk.ts` | change | port anchors (`data-flow-port`), the `data` dotted style, an edge's mark on its chip, the `GapInsert` slot cleared like a label |
| `design/flowFolds.ts` | keep | the editor passes `fold={false}` |
| `design/Flow.stories.tsx`, `flowFrames.tsx`, `measured/flow.spec.ts` | extend | edit-mode stories; ports, inserts and diff marks measured |
| `workflow/graphFlow.ts` | **keep as the one drawing**, extended | each node's variant and ports, `data` ways drawn on selection, the diff union and its marks, refusal marks; still pure and worded |
| `workflow/workflowFlow.ts`, `WorkflowChart.tsx`, `WorkflowRunView.tsx`, `place/PlaceWorkflowView.tsx` | change | draw through `StepNode` via `renderNode` |
| `workflow/chart/*` | **add** | `StepNode`, `PortRow`, `Terminal`, `HoldsGroup`, `EdgeChip`, `GapInsert`, `ChartBar`, `ProblemList`, `ProposalBar`, `ChangeList` |
| `workflow/edit/WorkflowEditor.tsx` | **split and remove** | becomes `WorkflowEditPage` (organism), `WorkflowCanvas`, `StepInspector`, `WaysOnTable`, `StepPicker` and `SaveReview`. Removed: `Step`, the card list (`:255-270`, `:381-454`), `WayForm` (`:456-527`), the id field (`:303-313`), the add-select (`:272-286`) and *Start from* (`:232-250`) |
| `workflow/edit/graphDraft.ts` | keep, plus operations | `insertBetween`, `addOnOutcome`, `route` (derives the kind), `moveInChain`, `duplicate`, `removeStep(…, { join })`, `applyChanges`, `fingerprint`, and a history helper; every shipped graph round-trips through each |
| `workflow/edit/StepOptions.tsx` | keep | row r's words |
| `workflow/edit/answers.ts` | keep | problems by node and wire, as today |
| `workflow/edit/openDraft.ts` | add | the registration §4.2 names |
| `help/ProposalCard.tsx` | change | the draft variant of `WorkflowChange`, and the press routed to the open draft |
| `help/where.ts`, `help/AskConversation.tsx`, `help/AskPanel.tsx` | change | the preface clause, `Draft`, the draft card's press |
| `workflow/WorkflowStartNode.tsx` | removed at the cutover | as D221 already decides |
| `Help/Proposals/HelpWorkflowProposals.cs` | change | the `draft` door; `GraphLines` gives way to `WorkflowGraphDiff` |
| `Course/Domain/ChatMessage.cs`, `Bridge/Routes/ConversationRoutes.cs` (modules) | change | `Draft` |
| `HelpWorkflowTool.cs`, `HelpProposalBox.cs` (service) | change | `draft` |
| `Knowledge/Room/HelpRoomMayPropose.cs` | change | the door's paragraph |

---

## 6. The established patterns, taken in (brief §2.6)

**The deciding fact.** A Daoris workflow is a governed record of at most 24 authored steps. The driver judges it, digests it and binds runs to it, and most of its steps are someone's authority (git, the person, a plugin). It is not a drawing or a program. A pattern is taken where it helps a person read or change that record. It is rejected where it would put presentation into the record, make the drawing claim what the record does not hold, or turn the editor into a programming surface (`docs/2026-10-09-workflow-design.md:892-895`).

| Pattern | Seen in | Taken or rejected | The reason for this product |
|---|---|---|---|
| A step palette with quick-add search | n8n's node creator (Tab), Node-RED's quick-add, Blueprints' right-click palette, ComfyUI's double-click search, Power Automate's *Add an action*, Zapier and Make's pickers, bpmn.io's palette | **Taken** as `StepPicker`, searchable, grouped as D221 groups it, and **context-sensitive** like Blueprints: kinds that cannot stand at that point are listed last with why | a person names what they want; the palette already says what each step may do (`WorkflowPalette.cs:47-72`) |
| *＋* between steps | Zapier, Power Automate, Logic Apps, Make, n8n's ＋ on a connection, React Flow's "edge with a button" | **Taken** as `GapInsert` on `after` ways | the commonest edit (put a check before the review) in one press, in place |
| *＋* at a dangling outcome | n8n's ＋ on an unconnected output | **Taken**: the ◎ *No further step* is the press | the truthful ending doubles as the place to continue |
| A context pad on the selected element | bpmn.io (append a task or gateway beside it) | **Taken** as the step's ⋯ and the **+** key | keyboard-first, with no floating pad to tab through |
| Drag from a handle to connect | n8n, Node-RED, React Flow, Blueprints, ComfyUI | **Taken** from labelled ports and ○ rows, as a pointer convenience, with the inspector's choice and **pick mode** as keyboard twins | dragging alone fails the keyboard and readers (`docs/2026-09-19-platform-ux.md:737-741`) |
| Drop a connection on empty ground to add a step | React Flow's "add node on edge drop", Blueprints' drag off a pin | **Taken** | composes "on release, a new step" in one gesture |
| Live connection validation | React Flow's `isValidConnection`, Blueprints' red ✕ on an incompatible pin | **Taken**: only steps that can take the way are outlined or walked, and the rest say why. The driver's judgement still decides | prevents most refusals before they are made |
| Auto-layout with *tidy* | n8n's *Tidy up*, Make's auto-align, Logic Apps and Power Automate (always laid out), GitHub Actions' run graph | **Auto-layout taken; *tidy* rejected** | nothing is ever placed by hand, so there is nothing to tidy |
| Free placement and stored positions | n8n, Node-RED, Make, Blueprints, ComfyUI, Figma, Miro; bpmn.io's DI geometry stored beside the model | **Rejected** | presentation inside a record the driver digests; each drag a new version for the same meaning (D157, D186) |
| Pan by dragging the canvas | every canvas | **Rejected**: the pane scrolls | a drag is kept for connecting; scrolling is what a page does |
| Fit and zoom controls | React Flow's Controls, n8n, Make, Figma (Shift+1), Miro | **Rejected**: the placement fits the width; the window's zoom (ZOOM1) scales and reflows; *Show the selected step* stands in for zoom-to-selection | a transform scale over text breaks trap 15, and a second zoom would fight the window's |
| A minimap | React Flow's MiniMap, Unreal, Miro's navigator | **Rejected** | 24 steps, sections and *n to put right* already locate everything; a pointer-only overview no reader uses (`docs/2026-10-20-flowchart-design.md:655`) |
| A side inspector for the selected step | Zapier, Power Automate's and Logic Apps' action pane, bpmn.io's properties panel, Figma | **Taken**: beside the chart at W, a Drawer at H and N | the step stays in view while it is edited |
| A full-screen node editor | n8n's node details view, Make's module dialog | **Rejected** | it hides where the step sits, which is what the person was reasoning about |
| Inline validation marks on the step and wire | n8n's warning triangle, Power Automate's and Logic Apps' red action, bpmn-js linting, Zapier's *needs attention*, GitHub's annotations | **Taken**, with words and inks; edges keep their ink and carry a chip | refusals said where the action is (R3); no edge wears a status hue (D186) |
| A problems list | bpmn-js linting's panel, an IDE's problems | **Taken** as *n to put right ▾* with doors | one count and a way to each refusal |
| A run's states on the same chart | GitHub Actions' run graph, n8n's executions, Logic Apps' run history, Camunda Operate, Node-RED's status dots | **Taken** (built, SETUPUX1u) | the same steps read the same way while running |
| Counts or tokens flowing on edges | n8n's item counts, Camunda's token overlay | **Rejected** | a run keeps where each step stands, not the path (D186's FLOWCHART1d note) |
| Animated running edges | React Flow's animated edges, n8n | **Rejected** | no animation of a move (D186); nothing at rest moves (FREEZE1) |
| Decision gateways with labelled branches | BPMN's exclusive gateway, n8n's IF and Switch, Power Automate's Condition, Logic Apps' True/False, Make's router filters, Zapier's Paths | **Branches taken, as labelled ports on the decision itself; a separate gateway node rejected** | a decision here is a plugin step reaching an outcome; an extra diamond would draw a node the record does not hold |
| A boundary event for the failure path | BPMN's error boundary event | **Taken** as the separated *If it fails* / *If you ask for changes* chips at the port row's end (on selection) and their own ink | the wrong-way paths look different from the outcomes |
| Failure routing hidden in a menu | Logic Apps' and Power Automate's *Configure run after* | **Rejected** | "a step's ways on are all visible" (D221) |
| Typed data pins in colours | Blueprints, ComfyUI | **Rejected**; the types are checked by the driver (`incompatible-schema`) and said in *Carries* | colour carries no meaning without words, and data ways run beside control ways (`release.json:54-57`), so drawing them always doubles the ink |
| Data lines drawn on demand | — (the reason the above is rejected) | **Taken**: dotted while either end is selected | inputs stay visible without crowding |
| Grouping and collapsing | GitHub Actions' matrix group, Node-RED groups, Blueprints' collapsed graphs and comment boxes, ComfyUI groups, Figma frames | **Derived groups taken**: the holds group, the sections, folds and stacks in read views. **Authored groups, frames, comment boxes and sticky notes rejected** | an authored group is presentation in the record, and `group` already means an eligibility group on wires (`NodeWires.cs:105-106`) |
| Swimlanes or pools by actor | BPMN | **Rejected**: who acts is on the step | lanes fix geometry by actor and fight the rank layout; *Before* and *After it lands* are the only bands |
| Copy and paste | n8n (nodes as JSON), Node-RED export, Figma, Blueprints | **Duplicate a step taken; clipboard and multi-step paste rejected** | at 24 steps, cross-workflow reuse is *Edit steps from this…*; pasted ways would name ids that do not exist |
| Multi-select and marquee | Figma, Miro, React Flow, n8n | **Rejected** | nothing to move as a group, and removing many at once is destructive at this size |
| Undo and redo | every editor | **Taken** through the Edit menu's keys | |
| An assistant proposing a change as a diff on the canvas | today's builders' copilots (n8n, Power Automate, Zapier, Make) | **Taken**: Ask Daoris's draft proposal, taken whole or change by change | the person sees exactly what changes, where |
| An assistant editing the canvas directly ("applied, undo if wrong") | some copilots | **Rejected** | Ask Daoris proposes and the person applies (D89); no press widens |
| Expressions, code or a function node in a step | n8n expressions, Make functions, Power Automate expressions, Node-RED's function node | **Rejected** | a wire carries no expression (`NodeWires.cs:3-6`); no programming canvas (workflow design §12.1) |
| Run a single step, pinned test data | n8n's *Execute step* and pinned data, Make's *Run once* | **Rejected** | a step's run is a real act (a landing, a publish); the judgement is the only dry run |
| Templates | n8n, Zapier, Make galleries | **Taken** as Presets (D221's *Change…*) | already built |
| Version history and a version diff | n8n's history, Power Automate's versions | **Taken later**: the same union drawing can compare *Saved here*'s vN with vN+1, read-only (not a row here) | one diff component |
| Snap grid, alignment guides, reroute nodes, a grid background | Figma, React Flow's helper lines and Background, Blueprints' reroute | **Rejected** | nothing is placed by hand, so routing is the layout's; a dot grid implies placement |
| A shortcuts sheet | n8n's "?", Figma | **Taken** through *Keyboard shortcuts* (D152's one table) | |
| Vertical top-to-bottom flow | Zapier, Power Automate, Logic Apps | **Taken** (D221) | reads like the sequence it is |

---

## 7. Rows, in build order

These follow D187: each row is one feature a person can see working, built end to end and handed back working. **Every visible row's proof includes:**
- page tests;
- stories at 80, 39 and 25rem, in en and zh, in both themes, with layout measured wherever a `Flow` draws;
- keyboard throughout;
- the installed window at 1546 and 680 px where the row changes it, with a Narrator pass along a branch and back.

| Row (lanes; after) | Contract | Proof |
|---|---|---|
| **FLOWCHART2a** a workflow drawn as its steps' kinds, with outcomes as ports (web; SETUPUX1n, u) | `FlowCanvas` split from `Flow` with `renderNode`; `StepNode` and its kind table; `PortRow`; `Terminal`; `HoldsGroup`; `EdgeChip`; `when` solid from its port; `data` dotted while an end is selected; a decision's targets seeded in outcome order; the place's chart and the run view draw through it; `Flow`'s other callers untouched | `graphFlow.test.ts` over every shipped graph (each kind's variant, each outcome a port, each unrouted outcome a ◎); `Flow.test.tsx` (a port in the description, its press moves the focus); measured: each `when` path starts inside its port's box, none through a box, crossing ceilings held; the quests', sessions', parts' and landing's measured stories unchanged; `tokens.test.ts` (no status hue on an edge) |
| **FLOWCHART2b** the editor's canvas (web; a; replaces SETUPUX1q) | §2's edit mode: select, the inspector beside or in a Drawer, `WaysOnTable` deriving the wire, `StepPicker`, `GapInsert`, ◎ as a press, drag and pick, move within a chain, duplicate, remove with *Remove and join*, undo and redo through the Edit menu, `fold={false}`, live announcements; `graphDraft`'s operations; the old card list, `WayForm`, the id field and *Start from* removed | `graphDraft.test.ts`: each operation's graph; every shipped graph round-trips through each operation and its undo; the derived-kind table against `NodeWires`' refusals on fixtures. Page tests: each interaction by keyboard alone and by pointer; the release workflow composed from the Merge preset **by keyboard alone** (extending `WorkflowEditor.test.tsx`); pick mode never focuses a step that cannot take the way. Measured: no insert over a node, label or another insert. Installed |
| **SETUPUX1r** options, tails, inputs (web; 2b), as amended | D221's contract; inputs proposed drawn as *Carries* | D221's proof |
| **FLOWCHART2c** the judgement on the chart (web; 2b; takes s's first clause) | each judgement carries the draft's fingerprint and an older answer is discarded; marks on step and way; *n to put right* with doors; a refusal opens its holds group; *Ask why* and *Fix it* fill in Ask Daoris | out-of-order answers dropped; each refusal code in the fixtures placed at its step or way; no mark without words; en and zh |
| **SETUPUX1s** save and choose (web; 2c), as amended | *Review and save*, consent bound to the scope keys, a name-only save, *Use here* only on an unchanged draft | D221's proof |
| **SETUPUX1t** staffing a step (web; r) | D221's contract; the role also drawn as the step's who | D221's proof |
| **FLOWCHART2d** Ask Daoris told the open draft (web, modules, driver; 2c) | `openDraft`; the preface clause; `ChatMessage.Draft` (bounded, sent on a fingerprint change, recorded as told, palette only on its own change); the route's field; the room's paragraph | driver: the record keeps the draft as told, an oversize draft refused with its sentence, nothing re-sent unchanged; page: sent once per fingerprint per conversation, the preface within 1200 characters; `session-words.md`'s places; the room's goldens |
| **FLOWCHART2e** a proposal against the draft (service, driver; 2d) | `workflow_propose.draft`; the `draft` door; a graph accepted for either pipeline here alone; judged as a save; `WorkflowGraphDiff` (steps by id, ways by structure then id, `needs`); the card's fields; Apply writes no workflow and no choice and records `taken` | `HelpWorkflowProposalsTests`: judged against the told draft; an unknown fingerprint refused; a development graph still refused by `edit` and taken by `draft`; renumbered wires read as kept; **Apply leaves the workflow store and the config byte for byte**; `taken` recorded. `WorkflowGraphDiffTests` over the shipped graphs; the tool's description test; the registration and coverage lists; the room's goldens |
| **FLOWCHART2f** the proposal on the chart (web; 2e, 2b) | the union drawing and its marks; `ProposalBar` and `ChangeList`; take whole or by change with `needs`; renaming on a clash; stale refused per change with *Ask again*; one undo step per take; `HELP_APPLY{taken}` and `HELP_DISMISS`; the card's draft variant (*Take into the draft* / *Open in the editor*) | page tests for each state (fresh, partly taken, a stale change, all taken, discarded, undo after a take); measured diff stories; installed: both of the brief's requests end to end in a real conversation, the agent and model named, and nothing saved until *Save* |
| **FLOWCHART2g** asking from the place's Workflow and from any conversation (web, driver; 2f, SETUPUX1o) | the chart's *✦ Ask Daoris* opens *Edit steps…* on the chart's graph with the conversation told; a `draft` on `id@vN` or `preset:id` opens the editor with it shown | opener tests; a `HelpCoverageTests` row; installed |
| **FLOWCHART2h** the installed window (the steward; all) | the flows: compose the release workflow by pointer, by keyboard and by Ask Daoris; a stale take; a refusal fixed through *Fix it*; a run of each outcome on the same components | dated shots in en and zh, both themes, at 1546 and 680 px; a Narrator pass |

---

## 8. Against the decisions

**This amends:**
- **D186 / the flowchart design.**
  - `Flow` gains an edit mode as `FlowCanvas`, drawing through the same layout. D186 already says "the editor is laid out by this layout" (`docs/decisions/D186.md:84-85`).
  - Ports and the `data` style are added to the ink.
  - New traps, each with a test: an insert over a label; a `when` that leaves another port; pick reaching a step that cannot take the way; a diff mark only by hue; an edit that animates.
- **D221 §2.3,** in its two places (§5.1). Rows q and s are reshaped as §5.1 says.
- **D157's** `workflow_propose` as WORKFLOW1h-b built it: a new door alongside, with the old ones unchanged.

**These stand:**
- D186's rejection of a free canvas: stored positions, panning, zoom and dragging to place.
- No graph library, elkjs rejected, and dagre locked out by the table.
- D41's tokens; D52's molecule rule; D89's "Ask Daoris proposes, the person applies".
- D221's settlements listed in brief §3; D222's authored words; the routes' authority (§4.8).

---

## 9. What this proposal does not cover

- **Not verified in this repository:** every figure about React Flow (§3.2).
- **Not read:**
  - whether `WORKFLOW_SAVE`'s plan answers a development graph's involvement for *Review and save*;
  - how Ask Daoris's head names its agent and model;
  - whether the room's text already lists the palette (it does name `workflow_propose`, `HelpRoomMayPropose.cs:134-145`).
- **Judgements to measure before they are relied on** (§3.4): the edit-latency budget, the movement per edit, and the 64 KiB `Draft` bound.
- **Left open, as D221 left them:** drafts kept across a restart; editing a verification workflow's steps from the landed row; authoring `allOf` and `anyOf` groups (kept as written under *More ways on*).
