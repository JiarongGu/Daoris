# FLOWCHART2: the workflow flowchart, and Ask Daoris composing beside you

Status: accepted by the steward 2026-11-02 (D223), the synthesis of `2026-11-02-flowchart-proposal-opus.md` and
`2026-11-02-flowchart-proposal-codex.md`, which answer `2026-11-02-flowchart-brief.md`. Both makers' reads run beside the first
rows (the owner's direction for pace) and are settled as findings on main. It amends D221 (`2026-11-01-setup-ux-design.md`) §2.3
and its rows q to t, and D186's flowchart. Web paths start at `src/Daoris.Web/src/`, driver paths at
`src/Daoris.Desktop/Daoris.Desktop.Driver/`.

## 1. What both proposals decided alike

- **Build on `design/Flow`, adopt no library.** Both keep Daoris's measured layout: React Flow brings a transformed viewport,
  stored coordinates and a second renderer, and D186 already measured dagre losing on collisions (`docs/decisions/D186.md:150-169`).
  Codex keeps adoption reopenable on evidence; it is reopened only if an editing need cannot be met on `Flow` (§6).
- **One component set draws four views:** the place's chart, the editor, a run and a proposal's diff.
- **The layout is automatic, top to bottom, and positions are never saved.** No free placement, no drag-to-move, no canvas zoom
  of its own (the window's zoom scales it), no minimap, no *Tidy* button: every change lays itself out.
- **Editing happens on the chart, with a keyboard path beside every pointer gesture,** and one inspector for the selected step.
- **Ask Daoris proposes against the open draft through a new `draft` door** on `workflow_propose`; its Apply saves nothing.

## 2. The components

| Component | What it is |
|---|---|
| `FlowCanvas` | `Flow` split into the layout and a canvas taking `renderNode`, ports and an edit mode; `Flow`'s other callers unchanged |
| `StepNode` | one node per palette kind (work, review, your look, a decision, a plugin step, the landing, the line), its title in words, its who (the role or the plugin), its state where a run or a judgement paints one |
| `PortRow` | a decision's outcomes as ports on the node's lower edge, each labelled; an unrouted outcome ends at a ◎ |
| `Terminal` | the configured end (*Work done here*), ◎ *No further step*, *Finished* from a run's record only |
| `HoldsGroup` | `graphFlow`'s holds group, folded to one node, opening on a refusal inside it |
| `EdgeChip` | a wire's label: its outcome, *if you ask for changes*, *if it fails*; `when` solid from its port, `back` around the outside, `data` dotted and drawn only while an end is selected; no status hue on an edge (D186 point 6), a ⚠ chip instead |
| `GapInsert` | the *＋* on a way on and on a ◎, opening the `StepPicker` |
| `StepPicker` | a searchable picker of the palette's kinds and the installed plugins' steps, grouped, worded from the catalogues and D222's authored words |
| `Inspector` | the selected step: its options worded (keys under *Details*), its ways on as `WaysOnTable` (one row per *Once done*, *If you ask for changes*, *If it fails* and each outcome, a target per row), its who through D221's one role editor; beside the chart at 1546 px, a Drawer at 680 px |
| `ProposalBar`, `ChangeList` | a proposal drawn on the chart (§4) and its changes listed |

## 3. Editing

- **Add:** *＋* between two steps or at a ◎, or the picker (`/` or *Add step…*); the new step is selected.
- **Connect:** choose a row's target in `WaysOnTable`; or drag from a port to a step; or the keyboard's *pick* mode, which walks
  only the steps that can take that way. **The person never chooses a wire kind:** the row and where the target stands (later or
  earlier) give `after`, `when`, `failed`, `rejected` or `back`, as `Nodes/NodeWires.cs` rules them.
- **Move within a chain, duplicate, remove:** removing asks *Remove* (its ways go with it, nothing bridged) or *Remove and join* (its
  one way in joined to its one way on, offered only where that is one of each).
- **Undo and redo** for every change, through the Edit menu and its keys; one undo step per change and per proposal take.
- **Refusals where they are:** the driver's judgement, tied to the draft's fingerprint (an older answer is dropped), marks the step
  (its border in declined's hue, its words in danger ink) and the way (a ⚠ chip); the head says *n to put right* with a door to
  each; *Ask why* and *Fix it* fill in Ask Daoris with the refusal.
- **A method brings its tail, an input is proposed from the routed predecessor and asked where two fit, nothing asks for an id**
  (D221 §2.3, kept).
- **Copy and paste** of steps within one draft; across drafts is not offered.

## 4. Ask Daoris composing beside you

1. **The room is told the open draft:** a bounded `Draft` field on the conversation's message (the graph, its fingerprint, the place
   and the palette's digest), sent when the fingerprint changes and recorded as told; the preface says a draft is open. An oversize
   draft is refused with its sentence, never truncated silently.
2. **`workflow_propose` gains `draft`:** a whole graph proposed against the told draft's fingerprint, judged as a save is, its card
   saying a structured diff (`WorkflowGraphDiff`: steps by id, ways by structure then id, each change's `needs`). Its Apply writes no
   workflow and no choice: it opens or returns to the editor with the proposal shown. The existing `new`, `edit` and `use` doors stay.
3. **On the chart:** a union drawing marks steps *New*, *To remove*, *Changed* and ways *new* or *to remove*, in words and marks, not
   by hue alone; the person takes it whole or change by change, a change's `needs` coming with it; a change whose base the draft no
   longer holds is refused by name with *Ask again*; nothing is saved until *Save*.
4. **From anywhere:** a place's Workflow has *Ask Daoris* opening the editor on the chart's graph with the conversation told; a
   conversation's draft card opens the editor with the proposal shown.
5. **Authority:** Ask Daoris proposes; the person takes changes into the draft and saves; `WORKFLOW_SAVE` and `WORKFLOW_USE` keep
   theirs (D89, D221).

## 5. The established patterns

**Taken:** a searchable step palette and an insert *＋* on the way (n8n, Node-RED, Zapier, Make, Logic Apps); handles and
drag-to-connect from a port, with a keyboard path beside it (React Flow's examples, Blueprints, ComfyUI); labelled decision branches
(BPMN tools); a run's states painted on the same chart without moving it (GitHub Actions' run graph); a side inspector, undo and
copy-paste (Figma); grouping and folding (the holds group); a copilot's proposal reviewed on the canvas as a diff, taken by change.
**Rejected:** free placement, saved coordinates and reroute points (a workflow is a governed record of at most 24 steps judged by the
driver, not a drawing); infinite panning, a canvas zoom and a minimap (the window's zoom and a reflowing layout serve 24 steps);
*Tidy* (every change lays out); hover-only controls (keyboard and touch); expression wires and scripts (the palette's rules only);
an assistant that saves, uses or runs anything.

## 6. Kept, changed and gone

- `design/Flow.tsx`, `flowLayout.ts`: kept; `FlowCanvas` split out with port-aware routing; the measured suite extended (each
  `when` leaves its port, no insert over a node or label, no edge in a status hue).
- `workflow/graphFlow.ts`: kept as the workflow adapter; it hands kinds and ports to `StepNode` rather than generic cards.
- `workflow/WorkflowChart.tsx`, `WorkflowRunView.tsx`: kept for their settings, sources, records and doors; their node chrome moves
  into the components.
- `workflow/edit/WorkflowEditor.tsx`: split into the page, the canvas, the inspector, the picker and the save review; the card list,
  `WayForm`, the id field and *Start from* go. `graphDraft.ts` gains insert, connect, move, duplicate, remove and history operations.
- Reopening adoption: only if a row meets an interaction `FlowCanvas` cannot give at 24 steps, measured, with the pinned licence read.

## 7. Rows, in build order

Every visible row: page tests; stories at 80, 39 and 25rem, en and zh, both themes, with layout measured where a `Flow` draws;
keyboard throughout; the installed window where it changes it. Each writes its own line of D223's note and its own translations.

| Row (lanes; after) | Contract | Proof |
|---|---|---|
| **FLOWCHART2a** the components (web; SETUPUX1n, u) | `FlowCanvas`, `StepNode` per kind, `PortRow`, `Terminal`, `HoldsGroup`, `EdgeChip`; the place's chart and the run view draw through them | every shipped graph: each kind's variant, each outcome a port, each unrouted outcome a ◎; measured: each `when` leaves its port's box, none crosses a box, the other `Flow` callers' stories unchanged |
| **2b** the editor's canvas (web; a; replaces SETUPUX1q) | §3's editing, the inspector beside or in a Drawer, `WaysOnTable`, `StepPicker`, `GapInsert`, pick mode, remove and join, undo and redo; `graphDraft`'s operations | each operation round-trips every shipped graph with its undo; the release workflow composed from the Merge workflow by keyboard alone and by pointer; pick never offers a step that cannot take the way |
| **SETUPUX1r** options, tails, inputs (web; 2b) | D221's contract, inputs drawn as *Carries* | D221's proof |
| **2c** the judgement on the chart (web; 2b; takes SETUPUX1s's first clause) | the fingerprint, marks on step and way, *n to put right*, a refusal opening its group, *Ask why* and *Fix it* | out-of-order answers dropped; each refusal code placed; no mark without words |
| **SETUPUX1s** save and choose (web; 2c) | D221's contract less its first clause | D221's proof |
| **SETUPUX1t** staffing a step (web; r) | D221's contract, the role drawn as the step's who | D221's proof |
| **2d** Ask Daoris told the open draft (web, modules, driver) | the `Draft` field, bounded, sent on a fingerprint change, recorded; the preface clause; the room's paragraph | the record keeps the draft as told; an oversize draft refused with its sentence; nothing re-sent unchanged; the room's goldens |
| **2e** a proposal against the draft (service, driver; d) | `workflow_propose.draft`, the `draft` door, `WorkflowGraphDiff`, the card's fields; Apply writes nothing | judged against the told draft; an unknown fingerprint refused; Apply leaves the workflow store and the configuration byte for byte; the diff over the shipped graphs; the tool's description and registration tests |
| **2f** the proposal on the chart (web; b, e) | the union drawing, `ProposalBar`, `ChangeList`, take whole or by change with `needs`, stale refused per change, one undo per take, the card's *Open in the editor* | each state's page test (fresh, partly taken, stale, all taken, discarded, undone); measured diff stories |
| **2g** asking from a place and a conversation (web, driver; f, SETUPUX1o) | the place's *Ask Daoris*, the conversation's draft card opening the editor | opener tests; a `HelpCoverageTests` row |
| **2h** the installed window (the steward; all; with SETUPUX1x) | compose the release workflow by pointer, by keyboard and by Ask Daoris; a stale take; a refusal fixed through *Fix it*; a run of each outcome | dated shots in en and zh, both themes, at 1546 and 680 px, and a screen-reader pass |
