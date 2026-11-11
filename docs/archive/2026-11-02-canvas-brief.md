# CANVAS1 — the brief: one proper canvas for every graph Daoris draws

Status: brief, 2026-11-02. Co-designed under D191: sealed proposals from this brief, one synthesis, then a read. Decision number
reserved: D224. It amends D223 (`docs/2026-11-02-flowchart-design.md`) and D186 where they meet.

## 1. Why

Several features are graphs a person reads and some a person edits, and today each draws on `design/Flow`, a measured layout with
no viewport: the workflow (its chart, its editor, a run, a proposal's diff; FLOWCHART2), the agents' structure (the roles' diagram,
`agents/roles/RoleDiagram.tsx`, COMPANYVIEW1 and AGENTSPLACE1), and, for display only, the repository reference map and the code map
(`docs/archive/2026-09-23-map-design.md`; `docs/code-map.json` and `docs/index/` hold the code's facts). The owner wants a proper canvas
these all stand on. D223 rejected a canvas zoom, panning and a minimap for a workflow of at most 24 steps; a structure across a
machine's workspaces and a code map are far larger, and one canvas serving all of them is the decision now.

## 2. What to design

1. **The canvas platform:** a viewport (pan, zoom, fit, focus a node, follow the selection), layers (nodes, edges, labels, overlays
   such as a run's states, a proposal's diff, a refusal's marks), nodes and edges as components a feature supplies, selection
   (one, many, by box where a feature allows it), hit-testing, and the canvas's own controls (zoom in and out, fit, a minimap where
   the graph is large). An edit mode where a feature allows editing and a display mode where it does not.
2. **Layouts per feature:** automatic top-to-bottom for a workflow (D186's measured layout kept or replaced, with its traps held);
   hierarchical or grouped for the structure (machine, workspaces, repositories, roles, works); map layouts for the reference map and
   the code map (clusters by folder or repository, edges by reference); whether a person may move nodes, and whether positions are
   saved, per feature.
3. **Scale and reading:** what the canvas does at a hundred nodes and at thousands (the code map): level of detail, clustering and
   expanding, search that focuses, keeping the person's place across reads, performance targets and how they are measured.
4. **Build or adopt:** extend `design/Flow`/`FlowCanvas`, or adopt a library (for example xyflow/React Flow, Cytoscape.js,
   sigma.js for large graphs, d3-zoom over Daoris's own rendering, an ELK or dagre layout). Weigh licence, size, theming through
   the design tokens, accessibility (keyboard, screen readers, the list fallback the narrow window needs), both languages,
   testability (vitest, the measured stories), and the installed window's Chromium host; say what to measure before choosing.
5. **What becomes of today's code:** `design/Flow`, `FlowCanvas`, `flowLayout`, `workflow/chart/`, `workflow/edit/`'s canvas,
   `RoleDiagram`, and FLOWCHART2's rows (its editing logic — the draft's operations, the inspector, the judgement, the proposals —
   moves onto the canvas unchanged); the order to move each feature, and what each feature's first canvas row is.
6. **The established patterns, taken in:** graph and diagram tools people know (Figma, Miro, Excalidraw, tldraw, yEd, draw.io,
   React Flow's examples, n8n and Node-RED's canvases, Cytoscape's and Gephi's network views, code-graph views in editors and code
   hosts): name what is taken and what is refused for this product, and why.

## 3. Constraints

- D221's and D223's settlements stand where this does not replace them: a workflow reads top to bottom by default, endings are
  truthful, the person names no wire kind, Ask Daoris proposes and the person takes and saves.
- D222's words; the design language and shared rules (`docs/archive/2026-09-19-platform-ux.md`, `docs/archive/2026-10-20-ui-ux-design.md`), the
  tokens and both themes; molecules take props, organisms hold hooks; stories at 80, 39 and 25rem, en and zh; the narrow window
  keeps a reading that is not a canvas (a list or a rail) where a canvas cannot serve it.
- No press widens; the maps are display only.

## 4. What a proposal returns

- The platform's components, props and states; the viewport's behaviour by pointer, keyboard and touchpad; the narrow fallback.
- Each feature's canvas: its layout, its nodes and edges, edit or display, and its first row.
- Build or adopt, with the evidence and the measurements to run first.
- Rows in build order, each with its contract and proof, ordered so the workflow editor moves first and the maps last.

A proposal reads the code it names, never `local/`, and writes nothing into the repository.
