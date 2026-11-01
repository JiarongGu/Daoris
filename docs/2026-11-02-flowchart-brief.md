# FLOWCHART2 — the brief: a proper flowchart for composing and reading workflows, with Ask Daoris composing beside you

Status: brief, 2026-11-02. Co-designed under D191: sealed proposals from this brief, one synthesis, then reads. Decision number
reserved: D223. It amends D221 (`docs/2026-11-01-setup-ux-design.md`) where they meet: its rows q to t (the editor's page, options,
save and choose, staffing a step) wait on this design.

## 1. Why

Setting work up is mostly a flowchart: a workflow is a graph of steps, decisions route the work, plugin steps publish and release, a
step names the role that takes it. The owner wants a proper flowchart, designed as components, and wants Ask Daoris to help compose
it. Today's pieces:
- `src/Daoris.Web/src/design/Flow.tsx` and `design/flowLayout.ts`: a generic measured flow, read-only, with sections, a list fallback
  and keyboard steps; `workflow/graphFlow.ts` (SETUPUX1n) maps a workflow graph onto it, always top to bottom, endings drawn truthfully.
- `workflow/edit/` (WorkflowEditor, StepOptions, graphDraft, answers): today's editor; D221 §2.3 designs it as the graph beside one
  step's inspector, with an outcome table and no dragging.
- `WORKFLOW_PALETTE`: each step kind, its options, outcomes and the wires it may take; `graphDraft` is the lossless draft.
- Ask Daoris's `workflow_propose` (`src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpWorkflowProposals.cs`,
  `src/Daoris.Service/Daoris.Service.Mcp/HelpWorkflowTool.cs`): a whole version, new or an edit on the newest, judged as the editor
  judges and applied by saving; its card says the diff (`WorkflowDiff`).

## 2. What to design

1. **The component system.** A node component per kind the palette offers (work, review, your look, a decision, a plugin step, the
   landing, the line, the holds group, an ending, *No further step*), its ports for outcomes and wires, an edge component per wire kind
   (`after`, `when` with its outcome, `back`, `data`), labels, selection, the canvas (fit, pan and zoom or none, the minimap or none),
   and how the same components draw the place's chart, the editor, a run's view and a proposal's diff.
2. **Layout and editing.** Automatic top-to-bottom layout or free placement (and whether positions are saved); adding a step (between
   two, at an outcome, from a palette), connecting an outcome to a target (drag, choose, or both), moving, removing with its wires,
   undo; the inspector; what a refusal from the driver's judgement highlights; keyboard and screen readers throughout; the narrow
   window (680 px) and 25rem.
3. **Build or adopt.** Extend `design/Flow`, or adopt a flowchart library (for example xyflow/React Flow with an ELK or dagre layout).
   Weigh the licence, size, theming through the design tokens, accessibility, both languages, testability in vitest and the measured
   stories, and the installed window's web view; say what you would measure before choosing.
4. **Ask Daoris composing beside you.** From the editor (and from a place's Workflow), ask Daoris in words ("add a release after the
   line", "send review changes back to the work"); its proposal shows on the chart as a diff (steps and wires added, removed, changed)
   that the person takes into the draft whole or change by change, or discards; nothing is saved without the person's *Save*. Say
   whether `workflow_propose` gains a draft mode (a proposal against the open draft's fingerprint, not a saved base) or a new door,
   what the room is told of the draft, how a stale proposal is refused, and how Ask Daoris explains a step or a refusal.
5. **What D221's rows q to t become,** and what of `graphFlow`, `Flow` and the editor is kept, moved or removed.

## 3. Constraints

- D221's settlements stand where this does not replace them: a graph reads top to bottom, endings are truthful, a step's ways on are
  all visible, nothing asks for an id, the judgement is tied to the exact draft, saving and choosing are two presses, one role editor.
- D222: authored words need no language; Daoris's words translate through the catalogues.
- No press widens: `WORKFLOW_SAVE`, `WORKFLOW_USE` and the help room's proposals keep their authority; Ask Daoris proposes, the person
  applies and saves.
- The design language and shared rules (`docs/2026-09-19-platform-ux.md`, `docs/2026-10-20-ui-ux-design.md`), the tokens and both
  themes, molecules taking props and organisms holding hooks, stories at 80, 39 and 25rem, en and zh.

## 4. What a proposal returns

- The component inventory: each component, its props and states, as text wireframes; the canvas at 1546 and 680 px and at 25rem.
- The interactions, each with its keyboard path.
- Build or adopt, with the evidence and what to measure first.
- The Ask Daoris flow end to end, with its route or tool changes and their authority.
- What becomes of D221's rows q to t and today's files; rows in build order, each with its contract and proof.

A proposal reads the code it names, never `local/`, and writes nothing into the repository.
