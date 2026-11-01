# CANVAS1: one canvas for the workflow, the structure and the maps

Status: accepted by the steward 2026-11-02 (D224), the synthesis of both sealed proposals; the read runs beside the first row.
It answers `2026-11-02-canvas-brief.md` from `2026-11-02-canvas-proposal-opus.md` and `2026-11-02-canvas-proposal-codex.md`, and
amends D223 (`2026-11-02-flowchart-design.md`), D186, the map design (`2026-09-23-map-design.md`) and the company-view design
(`2026-10-31-company-view-design.md`) where §8 says. Each proposal's claims about today's code were read against it; a line cited
here is the checkout's. Web paths start at `src/Daoris.Web/src/`, measured specs at `src/Daoris.Web/measured/` (written `measured/…`).

## 1. What both proposals decided alike

- **One platform and one engine for every graph** (`design/canvas/`); feature code never imports an engine, and no feature picks one.
- **The layouts stay Daoris's:** D186's measured workflow layout, its traps and its locked table (`docs/decisions/D186.md:150-169`);
  every other feature's layout is its own.
- **Coordinates are derived, never stored:** no feature moves a node, reparents, staffs by drawing a line or saves a position.
- **Nodes are the feature's HTML; the canvas decides no meaning.** A connection is an intent the workflow reads through `graphDraft`;
  display adds no act (the brief's *no press widens*). Overlays (a run's states, a refusal, a selection) never change layout.
- **The viewport:** drag pans, Ctrl+wheel and pinch zoom about the pointer, fit, focus and the selection followed by the smallest pan,
  never animated; Ctrl+=, Ctrl+- and Ctrl+0 stay the window's, whose zoom leaves a wheel the canvas took (`design/zoom.ts:117-124`).
- **The keys and the reader stay `FlowCanvas`'s** (`design/FlowCanvas.tsx:370-428`), with a list at every width; **its measurement
  changes first**, since `measure` mixes screen rectangles with layout offsets (`:993-1027`) and any transform makes its ink wrong.
- **Scale by folding, never by shrinking.** The code map's source stays bounded (500 modules, 5,000 dependencies, 1 MiB;
  `src/Daoris.Service/Daoris.Service.Core/Knowledge/Feed/CodeMap.cs:48-53`); thousands are proven on synthetic fixtures; `docs/index/`
  is never parsed into edges.
- **The order:** the workflow editor first, the maps last, FLOWCHART2's logic unchanged; build or adopt measured first, integrated
  against integrated; Cytoscape.js not chosen, sigma.js deferred to a measured failure, elkjs not now.

Where they differed, the steward settled: the choice by measurement (§5), Opus's platform shape (§2), bands for the structure (§3.2),
the inline flows kept without a viewport (§6), the rows (§9).

## 2. The platform

### 2.1 Components (`design/canvas/`, molecules; none imports a data hook)

| Component | What it is |
|---|---|
| `Canvas` | the region, the world it transforms, the seven layers, selection, keys, following, find, the minimap and the list fallback |
| `viewMath.ts` | pure: zoom about a point, clamp, fit, minimal reveal, wheel to pan or zoom, the anchor across a relayout, `screen = origin + translation + k × world` and back |
| `worldBoxes.ts` | one `ResizeObserver` per world; every node, port, label and ＋ in world units (rect less the world's origin, ÷ k, to 0.5 px), shared by the ink, hit-testing, the minimap and reveal |
| `CanvasToolbar` | above the viewport, never over the drawing (`map/LayeredMap.tsx:234-289`): find, the feature's tools (the map's *Lines*), *Show whole* and *Fold again*, *Show as list*, then the percentage opening the sizing menu (zoom in and out, the feature's fit, 100%, to the selection, 50/100/200%, the minimap where offered), each row with its key |
| `Minimap` | the viewport's lower right, `aria-hidden`, its view rectangle draggable; everything it does is also in the menu |
| `CanvasList` | the reading that is not a canvas: `FlowCanvas`'s rail (`:592-609`) and `RoleDiagram`'s (`agents/roles/RoleDiagram.tsx:294-299`) made one; nodes in reading order, each edge a foot press, groups as headings |
| `ink.ts` | `placeLabels` and `placeInserts`, moved from `flowInk.ts` |
| `RankedFlow` | `FlowCanvas` without its pane: stacks, folds, `layoutFlow`, sections, placement, `routeInk`, labels, ＋ and the rail. `FlowCanvas` stays the export, `<Canvas><RankedFlow/></Canvas>`, with today's props and `viewport?: 'none' \| CanvasOptions` (default `'none'`) |

**Two kinds of world.** A *placed* world is HTML laid out by CSS, then measured: the workflow and the structure, whose words wrap in
both languages. A *positioned* world has coordinates computed ahead over fixed-size cards: the maps, whose names are one-line ids.
Only a positioned world is culled or drawn by tier.

### 2.2 Props, sketched

```ts
type View = { x: number; y: number; k: number };            // the world point at the top left, and the scale
type CanvasProps = {
  label: string; roledescription: string;                   // the region's name and how to move it, the feature's words
  mode: 'display' | 'edit';
  world: { kind: 'placed'; render: (context: WorldContext) => ReactNode }
       | { kind: 'positioned'; frame: Box; boxes: ReadonlyMap<string, Box>; groups?: readonly Group[];
           renderNode: (id: string, tier: 'full' | 'chip' | 'dot') => ReactNode;
           renderEdges: (visible: ReadonlySet<string>, tier: 'full' | 'chip' | 'dot') => ReactNode };
  order: readonly string[];                                 // reading order: the keys walk it, the list reads it
  walk: { out(id: string): string | undefined; into(id: string): string | undefined };
  selection: { allows: 'one' | 'many'; value: { nodes: string[]; edges: string[]; primary: string | null };
               onChange(next: CanvasProps['selection']['value']): void; releases: boolean };
  zoom: { min: number; max: number }; opening: 'actual' | 'fit' | { at: string };   // zoom within 0.1-2.5 (§2.4)
  follow: { focus: boolean; selection: boolean; current?: boolean }; minimap?: 'auto' | 'never';
  find?: { text(id: string): string; placeholder: string } | null;
  marks?: { nodes?: ReadonlyMap<string, Mark[]>; edges?: ReadonlyMap<string, Mark[]> };  // Mark: { word, kind }
  pick?: FlowPick | null;                                   // today's type (FlowCanvas.tsx:138)
  connect?: { from(target: Element): { id: string; port: string } | null; onConnect(to: string): void; onGround(): void } | null;
  fitLabel: string; list: ReactNode; place?: { key: string; keep: 'page' | 'viewer' };
};  // through a ref: reveal(id, 'minimal' | 'centre'), fit(ids?), actual()
```

State comes in three families, kept apart (Codex): the **domain**'s is the feature's (a draft and its history, run records, a
proposal's transaction, a structure reading, map facts); the **view**'s is the canvas's (the view, the placement, folds opened,
selection, focus, following); the **interaction** is idle, panning, boxing, connecting, picking, or inside a node's control.

### 2.3 Layers, bottom to top

1. the ground, with no grid (nothing is placed by hand, and a grid promises snapping); 2. groups: bands, workspace and folder boxes,
the holds group, section dividers; 3. edges, one SVG of ink, `aria-hidden`; 4. nodes, the feature's HTML; 5. labels and ＋;
6. overlays: marks, pick outlines, the connection line, the box being drawn; 7. screen space, untransformed: the minimap.

**A mark is an outline, which takes no room (as `NodeFrame` draws selection, `workflow/chart/NodeFrame.tsx:61-62`), or a chip placed
where it clears every box and dropped where it clears none**, its words kept in the node's description and the feature's list. A mark
never adds a line or a border's width to a node: a judgement lands 250 ms after an edit (`workflow/edit/WorkflowEditor.tsx:74`) and
would move every rank below. A proposal's union drawing is a different scene, laid out as one.

### 2.4 The viewport by input

- **Wheel:** a plain wheel pans (Shift+wheel across) only while the world overflows that way and is not at that edge; otherwise the
  page scrolls. Ctrl+wheel zooms about the pointer continuously, `k × 1.25^(−Δ/100)` with Δ in pixels as `zoom.ts` counts it
  (`:106-110`), its default prevented and consumed once.
- **Pointer:** dragging the ground or with the middle button pans; a press on a node, a control or a `[data-flow-port]` never starts a
  pan; Space+drag is refused, Space choosing a node (`RoleDiagram.tsx:190`). Shift+drag on the ground selects by box where many are
  allowed. A press selects; Ctrl+press toggles and Shift+press adds where many are allowed; a double press does what Enter does (the
  node's door), never a zoom or an edit. A ground press releases the selection in display (UX5 U47), does nothing in edit, and is
  `onGround` in pick mode.
- **Touchpad and touch:** a two-finger scroll arrives as wheel deltas and pans; a pinch arrives as Ctrl+wheel and zooms; no inertia is
  added. On touch one finger on the ground pans and a pinch zooms about its midpoint (`touch-action: none`); a long press opens the
  feature's menu where it has one (D138).
- **Keyboard:** Tab reaches the toolbar, then the world's one roving stop (D186 point 8); a positioned world's region is a stop before
  it, where bare `+` `-` `0` and the arrows act as on the map today (`LayeredMap.tsx:214-227`). The walk is `FlowCanvas`'s; a key with
  Ctrl, Alt or the system key is the page's (`:373`). On a map, whose lines are pressable, → from a card reaches its first line out and
  then the card beyond, ← the line it came by, and Enter chooses a line. Shift+1 fits, Shift+2 zooms to the selection, Shift+0 returns
  to 100% (Figma's), read by `event.code`, from any focus in the canvas but a text field, under `isComposing`, and checked against
  D152's table (`menus/shortcuts.ts`). In the editor, `+` and Insert on a step stay *add after* (`WorkflowEditor.tsx:381`).
- **Following:** a node that takes the focus is revealed by the smallest pan bringing it inside a 2rem margin clear of the minimap,
  never zooming and never animated. Every focus call takes `{ preventScroll: true }`, which none does today (`FlowCanvas.tsx:335`,
  `RoleDiagram.tsx:147`, `WorkflowEditor.tsx:187`), so the viewport never scrolls. A selection made in the list, the inspector, find or
  the head's problems is revealed the same way. Following a run's current step is an option, off until chosen, that a pan suspends.
- **Bounds:** each feature sets its own within 10–250% (§3); fit never goes above 100% (`LayeredMap.tsx:119-130`); edit is floored at
  80%, the map's four fifths (`:20-21`), 100% on `pointer: coarse`, so `text-meta`, the ＋ and the ports stay usable; an overview scale
  draws compact marks and no inline affordance. The toolbar, list and menu never scale, keeping the 28 px target
  (`2026-09-19-platform-ux.md:741`).
- **Height:** the world's height at k plus padding, up to the window's room below (`useTall`, as the map measures it), never under
  20rem: a small workflow is never a tall empty box, a large one pans, and the page scrolls around it.
- **Opening and keeping:** the view opens where `opening` says and follows the drawing (folds, data arriving) until the person moves
  it (`LayeredMap.tsx:132-150`, made general); a status refresh never refits. Before a relayout the canvas records where the focused
  node stands on screen (else the selected, else the one nearest the centre) and pans it back there after: an added step never throws
  the view (yEd's mental map).

### 2.5 Edit, display and hit-testing

| | Display | Edit |
|---|---|---|
| A node pressed | selected, or opened where the feature says | selected: the inspector |
| ＋, ports, pick, connect | none | the feature's, as FLOWCHART2b built them |
| The ground pressed | releases the selection | nothing; `onGround` in pick mode |
| Zoom floor | the feature's | 80%, 100% on a coarse pointer |

- **The canvas adds no act**, and display removes none of an owner's controls: a run keeps its own.
- **The connection line** (row b) takes over `FlowChart`'s port handling (`workflow/chart/FlowChart.tsx:51-73`) with today's drop
  rules: a rubber line from the port's box to the pointer, the node under it outlined where `pick.can` allows, a pan at the viewport's
  edge, Escape to cancel; capture, a drag threshold, `pointercancel` and a lost capture cancel safely; a valid release commits one intent.
- **Hit priority:** a node's own control, a port, a node, an edge's hit stroke, the ground. Placed worlds use the DOM
  (`closest('[data-flow-node]')`, the attribute kept so every test and the editor's focus stand); positioned worlds a uniform spatial
  grid over world boxes (about 80 lines) for the dot tier and box selection. A pressable edge keeps its wide invisible stroke
  (`map/MapCanvas.tsx:545-548`), usable at every k; the workflow's edges are inert but for their ＋. An aggregate never merges
  identities: pressing it opens its members, and two outcome ports reaching one step stay addressable alone.

### 2.6 States, the narrow fallback and the reader

- **States, each a story:** unmeasured (jsdom: the list); measuring; 100%; zoomed, its percent said; fitted; moved; large, with find and
  the minimap; folded and *Show whole*; picking; connecting; the list chosen; narrow; the feature's empty state (`EditCanvas`'s
  beginnings); loading, unread, the layout failed. A new layout keeps the last valid picture with an updating notice and drops a result
  older than the scene's revision; a first layout that fails shows the list with the failure's words; a blank canvas never stands for an empty graph.
- **The floor:** the viewport is offered from **36rem of the region's inner width**, the 39rem story's 576 px
  (`design/storyWidths.tsx:13`, `:19`). Below it a feature keeps today's placement, drawn where it fits at 100% and else its rail or
  list: at 25rem nothing changes, and a run in the side bar's 300 px stays its rail. *Show as list* is offered at every width and
  remembered (`daoris.flow.list`, `FlowCanvas.tsx:170`); a change of placement keeps the selection, the anchor and the open inspector;
  one drawer or picker holds the focus at a time (D223 §8.8).
- **The reader:** a named `role="region"` with its `roledescription`; the nodes one `role="list"` in reading order inside the world; the
  ink hidden. A selection, a connection's eligibility, an edit done and a failure are announced once each; a pan's frame or a status
  repaint never. **Placed worlds are never culled.** A positioned world culls past 400 nodes: each item drawn carries `aria-setsize` and
  `aria-posinset`, the focused and selected are always mounted, a key aimed at a culled node mounts and reveals it before the focus
  moves, and `CanvasList` (grouped, its folds closed) is the whole reading, offered at the toolbar's head.

## 3. Each feature's canvas

### 3.1 The workflow: the place's chart, the editor, a run, a proposal's diff

- **Layout:** D186's whole (`layoutFlow`, stacks, folds past 24, sections, `routeInk`, ports, data wires, ＋), always top to bottom
  (D223); `trackFor` (`FlowCanvas.tsx:974-990`) sizes the track from the region's width at k = 1, so a zoom lays nothing out again.
- **From 36rem a drawing wider than its region pans rather than turning to the rail.** A three-wide rank needs 3 × 14 + 2 × 1.5 = 45rem
  (`WORKFLOW_TRACK`, `workflow/graphFlow.ts:14`; `FLOW_REM`, `design/flowLayout.ts:105`), the rail at 39rem today; the dense graph, the
  rail at every width before folds (`D186.md:182`), is drawn once *Show whole* is pressed.
- **It opens at 100% on the entries** and follows the focus and the selection: the head's door to a refusal, pick mode's first step
  that can take the way (`FlowCanvas.tsx:340-344`), a step just added (`WorkflowEditor.tsx:176-190`). Zoom 80–200% editing, 50–200%
  reading. **No minimap** (D223's rejection holds for 24 steps) unless §4's rule shows one, in practice a 96-node run shown whole.
- **Marks** (row b): a refusal is declined's outline on its step and a placed *To put right* chip, its sentence in the description and
  the head's `ToPutRight`, where today it is a `border-2` (`NodeFrame.tsx:59`) and a sentence inside the step
  (`workflow/chart/StepNode.tsx:44-45`). A proposal's *New*, *To remove* and *Changed* are a chip and an outline's style, never hue
  alone (D223 §4.3).
- **A run** (`workflow/WorkflowChart.tsx:426`) keeps its records, controls, limits and doors and paints recorded outcomes only.
- **Mode:** edit in the editor; display on the place's chart (`workflow/place/EffectiveChart.tsx:83`), the tab's chart, a run and a
  diff's reading. In the editor *many* is for Copy alone (`copySteps`, `workflow/edit/graphDraft.ts:613`); every other act takes the
  primary. Positions are never saved; the view, folds and selection are page state. First row: **CANVAS1a**.

### 3.2 The agents' structure: bands

Today's three columns (`agents/roles/diagram.ts:5-10`) draw *placed* edges across everything and flatten the place tree into one
column. Bands replace them, laid out by a pure `agents/roles/structureBands.ts`:

- **Rows:** one band per place in tree order: this machine, each workspace, and that workspace's repositories that set something of
  their own, indented under it; a place setting nothing of its own stays a counted line in its workspace's band (company-view `:175`).
- **Columns:** the label (the place, indented by depth) | its roles, stacked, appointed roles among them (dashed while dormant) | a
  gutter | the agents, each once, ordered by the barycentre of its roles' bands, ties in declared order.
- **Edges:** only *staffed*, role to agent across the gutter, numbered by position, dashed where the last reading says the choice cannot
  run; the band says *placed* and *appointed*, which stay in each node's description and the list. They are routed as a two-rank flow
  across by `routeInk` and `placeLabels` once row c has measured that its rank extents (`design/flowInk.ts:72-75`) hold for bands.
- **Kept from COMPANYVIEW1:** one read, one selection, one role editor; inheritance lit, never drawn, with its provenance; choice order,
  unavailable and dormant distinct; the *never drawn* list (`:167-173`), so works are a role's lines and a filter, never nodes.
- **Opening a fold widens no read or grant:** a scoped read asks only the chosen place and its parents
  (`agents/roles/structureRead.ts:10-16`).
- **By width:** the label beside the roles where the region holds 40rem (12 + 12 + 4 + 12), above them (28rem) from the 36rem floor to
  that, the section's list below the floor. Past 24 nodes each workspace's bands fold (`design/groupFolds.ts`, unchanged).
- **Mode:** display, one selection, its detail beside; *Edit* stays the row's editor; zoom 50–200%; the view is page state. First row:
  **CANVAS1c**, in `DiagramSection` and AGENTSPLACE1's scoped Roles.

### 3.3 The reference map

- MAP2 and MAP4's workspace map. `layoutLayers` (`map/layers.ts`) is kept whole as a positioned world, its lines, lanes and bows the
  window's lessons. Every relationship keeps its kind (quests, asks, chains, declared uses, shared findings) with its direction, counts,
  filters, outside counts and detail (`map/topology.ts`), never a generic *reference*.
- The ring (to `RING_MAX`, 12, `map/layers.ts:31`) is a fitted positioned world with zoom off on the same canvas: crossing twelve
  changes the layout, not the renderer, and keeps the selection and the focus. Scoped to several workspaces, each is a block with its
  own layers, stacked by name, the lines between them running through the gaps; one workspace's map is unchanged.
- Its SVG cards and lines are the world's layers: lines pressed through their strokes, arrowheads in the map's units (map design
  `:56-60`); the cards become the roving stop where each is a Tab stop today (`map/LayeredMap.test.tsx:30`). Search, sizing and the
  view become the platform's; Ctrl+wheel and pinch zoom continuously, where each event is a fixed 1.25× today (`LayeredMap.tsx:171`);
  a view kept per viewer ends its reset on each size or count change (`:138-143`).
- **Mode:** display only, no act of its own (map design `:67-69`); zoom 25–250%, as today; it opens fitted, never below 80%; the view,
  folds and *Lines* are the viewer's. First row: **CANVAS1e**.

### 3.4 The code map

- It holds 20 modules and 20 dependencies today (`docs/code-map.json`). A map of thousands needs a finer feed, MAP3's contract and
  outside CANVAS1; the canvas proves that scale on synthetic fixtures, so the drawing never blocks one.
- **Layout** (pure, `map/codeClusters.ts`): folders from each module's `path`, nested boxes packed by name, `layerModules` inside a
  folder (`map/codeLayout.ts`); a family-wide view by repository, then folder. A folder is no evidence of an import: only supplied
  dependencies are edges, each kind as its producer wrote it. A cycle is condensed for ranking and labelled a cycle, whole when opened.
- Edges between folders are aggregated by pair and direction and counted (×n) until a folder opens; a selected module shows its
  neighbourhood and the rest steps back. Past 60 modules the folders open closed; find opens the path to its match; the list is by
  folder with `CodeMapDetail` (`map/CodeMap.tsx:197-267`) the detail; cards are fixed size, an id cut by `FITS` (`:16-17`) and whole
  in the detail and its name. Its file and fed commit stay said; a view kept under another commit restores no coordinates.
- **Mode:** display only; zoom 10–250%; opens fitted on its closed folders, never below 80%; the view is the viewer's. First row:
  **CANVAS1f**. **sigma.js with graphology is the one named exit**, for the code map alone: when a file-level feed has landed and a
  positioned world past 5,000 misses 33 ms at the dot tier, **and** WebGL is verified GPU-backed in the installed window.

## 4. Scale and reading

| Scale | Reading |
|---|---|
| About 100 nodes | every node at the full tier, nothing culled; find from 40 nodes; the minimap by its rule |
| Hundreds | groups opened locally, the selected neighbourhood stood forward; a positioned world culls past 400; find reaches every item |
| Thousands (positioned worlds) | a cluster overview by default, one bounded region opened at a time; edges aggregated by direction and kind, with exact counts and member lists |

- **Tiers** follow the effective size of `text-meta`, 11 px × the window's zoom × k: from 9 px the node's own component; below it a
  chip, a box with its one line at the type scale's size (counter-scaled) where it clears its neighbours, else hidden; below 4 px a dot
  on a canvas2D layer, `aria-hidden`, group titles staying HTML at the type scale. **The budget** starts at 250 full-tier nodes and
  2,000 visible paths in view, past which the tier drops before the frame does; row d writes what holds into D224's note.
- **Nothing is drawn smaller to fit:** the person's zoom is the only scale; strokes never thin below 1 px
  (`vector-effect: non-scaling-stroke` under 100%), and arrowheads stay in world units. A large positioned layout runs in a worker past
  50 ms, carries its scene's revision and can be cancelled; overview groups take predictable sizes, and only the scene opened is measured.
- **One mechanism folds:** a fold is a node with a press, every edge across its boundary drawn to it and counted: the flow's stacks,
  settled stretches and focus (`design/flowFolds.ts`), the structure's workspaces (`design/groupFolds.ts`), the maps' workspace and
  folder boxes, with *Show whole* and *Fold again* in the toolbar. Selection and focus pin what they need and drop no fact silently.
- **Find** stands in the toolbar from 40 nodes, worded by the feature (`map.find`), and searches every item, folded or culled. It says
  *n of m* and dims what does not match (`LayeredMap.tsx:479-495`). Enter and Shift+Enter step the matches, opening each one's folds,
  selecting it and revealing it at a readable scale, the focus staying in the field; ↓ hands the focus to the match shown; Escape
  clears the find and puts the view back. Opus's Enter moving the focus is refused: the next Enter would open the node.
- **The person's place:** the focus is kept by node id (`FlowCanvas.tsx:346-353`) and a relayout keeps the anchor (§2.4). The view,
  folds and selection are page state for the workflow and the structure; for the maps they are the viewer's, under
  `daoris.canvas.<feature>.<key>` (in try/catch, a convenience) by the graph's identity. An unchanged graph restores its view exactly; a
  changed one keeps the surviving anchor's screen place, else its nearest surviving group, and says the selected item is gone; a
  language or text-size change measures again and keeps the anchor.

### 4.1 Performance targets

| Case | First usable | p95 frame, panning and zooming | Else |
|---|---|---|---|
| The shipped workflows, to the 24-step bound (Codex) | ready props to a stable chart ≤ 150 ms | — | selection's feedback ≤ 50 ms |
| The dense graph shown whole, ≤ 96 (Opus) | ≤ 100 ms | ≤ 16.7 ms | an edit's ink ≤ 50 ms; no long task over 50 ms |
| 100 nodes, 500 edges (Codex) | a stable layout ≤ 300 ms | ≤ 16.7 ms | |
| The structure, ≤ 400; the reference map, ≤ 200 cards (Opus) | ≤ 200 ms; ≤ 150 ms | ≤ 16.7 ms | |
| The code map at its bound, 500 and 5,000 | ≤ 300 ms (Opus); the grouped overview ≤ 750 ms (Codex) | ≤ 16.7 ms (Opus); ≤ 33 ms (Codex) | find to reveal ≤ 100 ms with no new layout |
| Synthetic: 5,000 (Opus); 10,000 nodes and 50,000 edges (Codex) | ≤ 800 ms (Opus); the clustered overview ≤ 1.5 s (Codex) | ≤ 33 ms | the worker never blocks typing |
| Twenty opens and closes; idle (Codex) | | | no growth in retained nodes, observers, listeners or workers; at rest, no frame loop and no measuring (FREEZE1) |
| The bundle | | | the extension ≤ 12 KB gzip and no dependency (Opus); an adoption ≤ 150 KiB gzip incremental (Codex) |

Where the two differ, the stricter is the target and the looser the line no row crosses; between them a row records its figure in
D224's note. **How they are measured:**
- `measured/canvas.spec.ts`, in Chromium and then the installed window, scripts `page.mouse.wheel` (Ctrl to zoom) over the stories and
  takes frames from `requestAnimationFrame` deltas and the Long Animation Frames observer, its ceilings kept as the crossing table's
  are (`measured/flow.spec.ts:160-182`); vitest benches `layoutFlow` at 96, bands at 400 and clusters at 5,000.
- Each stage is timed apart (normalising, grouping, layout, measurement, routing, React's commit, first usable and later frames) with
  the graph's shape, counts drawn, hardware, viewport, text zoom, language, theme, cold or warm and the host's build, over sparse, dense,
  cyclic, disconnected and high-degree graphs, repeated and reported as distributions.
- The harness gains transforms at several scales and offsets, root zoom at 50, 100 and 200%, a scrolling page around the region, a
  drawer opening and a resize; it asserts world-to-screen round trips, port alignment, hit targets and a visible focus, and fails on a
  path or handle displaced on purpose. Vitest owns pure geometry, selection, dispatch and stale results, Playwright measured geometry
  and real pointer and keys; neither proves the host's wheel, focus, DPI or accessibility (`ChromiumView`,
  `src/Daoris.Desktop/Daoris.Desktop.App/MainForm.cs`).

## 5. Build or adopt

**Row a measures both on the same editor fixtures before anything is built on either.**
- **The extension** (Opus): the measured flow inside a CSS-transformed world (`translate(x, y) scale(k)`, origin 0 0), `LayeredMap`'s
  proven viewport made general. A transform changes no layout, so ranks, wrapping, folds, keys and the reader stay as they are; the
  measurement moves into world units and the focus stops scrolling.
- **The React Flow spike** (Codex): `@xyflow/react` with a controlled viewport, `StepNode` as Daoris-owned HTML nodes, Daoris's
  placement handed over as derived coordinates never stored, and its default mutations, connection acceptance and styling off. It stays
  outside the product, in gitignored scratch (`_fixtures/`), never imported by `src/` or added to the web package.
- **The fixtures:** the editor's measured stories (`measured/flow.spec.ts:189-196`), the dense graph, and synthetic placed worlds of
  100, 500 and 2,000 `StepNode`s.

| Blocking contract | Passes when |
|---|---|
| The reader's one roving stop and reading order | one Tab stop in the world; the accessibility tree's list in `layout.order`; nothing announced twice |
| Wheel arbitration with the page and the window's zoom | a plain wheel at either edge scrolls the page; a canvas Ctrl+wheel leaves `zoomLevel()` unchanged; Ctrl+= and Ctrl+0 stay the window's |
| World-unit ink at five scales | at k = 0.5, 0.8, 1, 1.5 and 2, paths and label boxes within 0.5 px of k = 1; no edge through a box; crossings at or under the ceilings |
| IME and nested controls | a composing key reaches its field; a press in a node's control neither pans nor selects; Escape returns to the node |
| Focus without scroll | a focus walked off screen leaves every ancestor's scroll at 0 and the node revealed |
| Frame times | p95 ≤ 16.7 ms at 100 and 500 nodes, ≤ 33 ms at 2,000, `will-change: transform` on only during a gesture |
| The bundle | a `vite build`'s raw, gzip and Brotli difference: ≤ 12 KB gzip for the extension, ≤ 150 KiB gzip for an adoption |
| Licence and notices | read from the pinned package and each transitive one: the licence, required notices, the attribution drawn by default, any commercial part |

Recorded beside them, not blocking: React 19, ports and custom routes, pointer capture, styling through the tokens. **The rule:** the
extension is the default. React Flow replaces it only where the extension fails a blocking contract, and only if React Flow passes
every contract the extension fails and fails none it passes. The figures, the licence reading and the choice go into D224's note
before row a builds further; one engine then serves every feature. Every library figure in the proposals but D186's measured ones is
from memory until this reading replaces it.

| Candidate | Settled |
|---|---|
| Cytoscape.js | refused: canvas-drawn nodes no reader or keyboard reaches (`D186.md:71-72`), a JS stylesheet beside the tokens, sigma's niche at lower speed |
| sigma.js with graphology | the one named exit, for the code map only (§3.4) |
| d3-zoom | refused: the gesture arithmetic is small (`viewMath.ts`), and its listeners, transitions and inertia fight React and the window zoom's taken-wheel contract |
| dagre; elkjs | refused: dagre on D186's table, the bands and clusters laid by hand on their own fixtures; elkjs at 466,978 bytes gzipped under EPL-2.0 or GPL-3.0 (`2026-10-20-flowchart-design.md:487-488`) |
| A whiteboard SDK | refused: a document model, tools and persistence a governed graph has no use for |

## 6. Kept, changed and gone

| Today | Becomes |
|---|---|
| `design/Flow.tsx` and its inline callers: `quests/QuestFlow.tsx`, `work/head/SessionFlow.tsx`, `work/head/PartsFlow.tsx`, `projects/git/LandingFlow.tsx`, `workflow/WorkflowStartNode.tsx` | **kept, with no viewport**: small reads in a scrolling page, where a viewport would trap the wheel and add chrome; one opts in only on a measured need. Codex's row e, moving every `Flow` caller, is not taken |
| `design/FlowCanvas.tsx` | split into `design/canvas/*` and `design/RankedFlow.tsx`, the export and its props kept with `viewport`; `measure` (`:993-1027`) reads world units through `worldBoxes`; its 16 px rem (`:173`, `:298`) becomes the root's measured size; `focusNode` (`:333-336`) takes `preventScroll` |
| `design/flowLayout.ts`, `flowFolds.ts`, `groupFolds.ts`, `flowInk.ts` | kept whole; `placementFor` gains the canvas floor; `flowInk`'s boxes are world units by contract, its label and ＋ placing moved to `design/canvas/ink.ts` |
| `design/zoom.ts` | unchanged: the canvas takes its wheel before the window hears it |
| `workflow/chart/*` | kept; `FlowChart`'s port drag becomes `connect`, `ChartEdit` unchanged; `NodeFrame` and `StepNode` draw refusals as marks (§3.1) |
| `workflow/graphFlow.ts`, `workflow/edit/*` | logic unchanged (the draft's operations, inspector, picker, judgement, proposals, save review); `EditCanvas` passes `viewport`; the editor's focus takes `preventScroll` |
| `workflow/WorkflowChart.tsx`, `workflow/place/EffectiveChart.tsx` | take the viewport from 36rem; a run in the side bar stays its rail |
| `agents/roles/RoleDiagram.tsx`, `diagram.ts` | gone: `StructureCanvas`, `structureBands.ts` and `StructureNode` (from `NodeBox`) replace them; `diagramShape`'s folds and `DiagramSection`'s API kept; `roleStructure.ts` and `structureRead.ts` unchanged |
| `map/LayeredMap.tsx` | its viewport, wheel, keys, search and sizing (`:111-289`) go to the platform; its cards and lines stay as the world's layers over `layers.ts` |
| `map/MapCanvas.tsx`, `map/MapView.tsx` | the ring kept as a fitted positioned world; the switch at `RING_MAX` chooses a layout on one canvas |
| `map/CodeMap.tsx`, `codeLayout.ts`, `topology.ts`, `layers.ts` | `CodeMapCanvas` replaced by the `codeClusters.ts` world; `layerModules`, `CodeMapDetail`, the topology and the layers kept |
| `measured/flow.spec.ts`, `measured/roles.spec.ts` | extended (§4.1); `measured/canvas.spec.ts` is new |
| FLOWCHART2's open rows | **2f** (the proposal on the chart) draws through CANVAS1b's marks; **2g** unchanged; **2h** runs with CANVAS1g; **2c**'s built refusals move onto the marks in row b; SETUPUX1r, s and t are inspector-side, unordered against the canvas |
| Words | through D222's catalogues, with glossary rows: `map.zoomIn`, `zoomOut`, `size`, `sizing` and `sizes` move to `canvas.*`; new `canvas.actual`, `canvas.toSelection`, `canvas.minimap` (缩略图, VS Code's word) and `canvas.matches` (*{{at}} of {{count}}*); each feature words its own fit (*Show the whole workflow*, 显示整个工作流) and its region's sentence |

## 7. The established patterns

| Tool | Taken | Refused, and why |
|---|---|---|
| Figma | Shift+1, 2 and 0; Ctrl+wheel and pinch about the pointer; a percentage menu; a selection outline with an inspector beside; undo | free placement, frames, auto-layout authoring and layers as a second hierarchy (a workflow is a governed record); a ground drag selecting by default (pan is the common act); Space+drag |
| Miro | fit; a navigator that can be switched off; a place recovered | an infinite board, sticky notes, ink, live cursors: none is a record, and shared hosts serve no UI |
| Excalidraw, tldraw | a view mode apart from edit; the camera apart from what is drawn; culling off screen; a pan at the edge while dragging; the keys said in one place | sketch style, free shapes, arrows bound by hand, drawing files: these are operational records |
| yEd, draw.io | a layout per kind of diagram; folding with edges redirected and counted; incremental layout keeping the mental map (the anchor); swimlanes (the bands); an outline panel | a *Layout* command to run (every change lays itself out); connectors and waypoints by hand; saved coordinates |
| React Flow's examples | handles and a connection line; `isValidConnection` (our `pick.can`); fit with padding; controls and a minimap; Shift for a box; drawing what is visible; sub-flows as groups | stored coordinates, dragged nodes, every node a Tab stop, floating node toolbars, its default styling and mutations |
| n8n, Node-RED | ＋ on a way and at an end; the node creator (our `StepPicker`); a search that jumps to a node; wiring from a port; a switchable navigator | free placement, *Tidy up*, sticky notes, hover-only buttons, flows as tabs, expression wires, an assistant that deploys or runs |
| Cytoscape, Gephi | compound nodes (bands, folders); a neighbourhood lit and the rest stepped back; labels by level of detail; filtering by kind (*Lines*) | canvas-drawn nodes for anything read or edited; force layouts, which break *same graph, same picture*; meaning by hue or size without words |
| Code-graph views (editors' hierarchies and outlines, package diagrams, a code host's dependents, a monorepo's project graph) | a list beside every graph, the list the authority (`CodeMapDetail`); folding a folder; one node with its neighbours; search to focus; provenance | opening on the hairball; an edge inferred from a name, a folder or a layout; the graph as the only reading |

## 8. What stands and what is amended

- **D223 stands:** top to bottom; truthful endings; the person names no wire kind; Ask Daoris proposes and the person takes and saves;
  one component set; positions never saved; no free placement, *Tidy* or hover-only control. **Amended:** its rejection of a canvas
  zoom, panning and a minimap gives way to §3.1's viewport; its rejection of React Flow becomes row a's measurement, the extension the
  default; a refused step's declined border and danger-ink words (its §3) become §3.1's outline and placed chip.
- **D186 stands:** one primitive per process; the hand layout and its locked table; the rail as the narrow placement; an edge is ink,
  never a status hue; the list in reading order; no animated moves; the Map draws no process. **Amended:** point 2's *no panning, no
  zoom* (`:26`) becomes the viewport, stored positions and dragging still rejected (`:64`); point 3's placement by the pane's width holds
  below 36rem, and above it the viewport pans what the region cannot hold; trap 15, *no transform scale over text*
  (`2026-10-20-flowchart-design.md:613`, held by `design/Flow.test.tsx:103-105` for a flow with no viewport), becomes *the canvas never
  shrinks a drawing on its own: only the person's zoom scales it, and below a readable size a node draws its chip*; the rejection of a
  canvas drawing (`:71-72`) keeps one exception, the dot tier of a display-only positioned world, `aria-hidden`, the list its reading.
- **The map design:** *no graph library* stands (`:48-50`), and MAP3's open question is answered: not yet, sigma.js the named exit. Its
  moving about (`:105-112`) becomes the platform's, the sizing menu kept; its bound (`:250-251`) stands; every card and line stays a
  keyboard's, through the roving stop and the walk.
- **The company-view design (§4):** three columns (`:153-161`) become bands, *placed* and *appointed* said by the band; *no pan, zoom or
  drag* (`:175`) becomes pan and zoom without drag; its placement and widths (`:177`, `:192-195`) become §3.2's. One read, one function,
  one owner, inheritance lit and the *never drawn* list stand; AGENTSPLACE1's scoped Roles takes the same canvas.

## 9. Rows, in build order

Every visible row: page tests; stories at 80, 39 and 25rem, en and zh, both themes, measured where a world draws; keyboard throughout;
the installed window where it changes it; its own line of D224's note and its own translations.

| Row (lanes; after) | Contract | Proof |
|---|---|---|
| **CANVAS1a** the measured choice, and the workflow's chart and editor in the viewport (web; first; the read beside it) | §5's comparison, its figures, licence reading and choice in D224's note; then `design/canvas/` on the engine chosen: `viewMath`, `Canvas`, `worldBoxes`, the toolbar with its sizing menu and *Show as list*; §2.4's height, wheel, continuous Ctrl+wheel and pinch, drag, middle-button and touch pan, Shift+1, 2 and 0, following by the smallest pan, `preventScroll`, the anchor, the edit floor; the 36rem floor; the root's measured rem; `FlowCanvas`'s `viewport`, taken by the editor, the place's chart, the tab's chart and the run from 36rem, the inline flows `'none'` | §5's table passed or its failure recorded; every existing Flow, FlowCanvas, FlowChart and WorkflowEditor test unchanged; `viewMath.test.ts`; `+` on a step still adds, Ctrl+= still the window's, a canvas Ctrl+wheel leaves the window's level; Merge composed into Release by pointer and by keyboard in the viewport; every shipped graph round-trips its operations and undo; pan, zoom and selection leave the draft's fingerprint; measured: world-unit ink at five scales, no edge through a box, no label or ＋ over one, crossings under the ceilings, scroll 0 after a focus walk, one draw per resize (trap 14), root zoom at 50, 100 and 200%, the dense graph drawn and panned at 80rem |
| **CANVAS1b** selection, marks and the connection line (web; a) | selection none, one or many (a press, Ctrl- and Shift-press, a Shift+drag box; released in display); the editor's *many* for Copy alone; marks as outlines and placed chips, FLOWCHART2c's refusals moved onto them; `connect` with its rubber line, the target outlined, the edge pan, Escape, capture and cancel; the hit stroke of pressable edges; `FlowPick` kept | a page test per gesture and key; measured: every node's box equal with and without marks and across plain, current and refused; the connection line starting inside its port's box at every k; 2c's refused stories on the new marks; FLOWCHART2f then builds on them |
| **CANVAS1c** the structure as bands (web; b) | §3.2: `structureBands.ts`, `StructureCanvas`, `StructureNode`; only staffed edges drawn; lit as today; workspace folds past 24; the label above the roles below 40rem; the list below the floor; `RoleDiagram` replaced in COMPANYVIEW1 and AGENTSPLACE1 | `structureBands.test.ts` over `structureFixtures` (tree order, counted places, the agents' barycentre, folds); the reader assertions of `RoleDiagram.test.tsx` and `RolesDiagram.test.tsx` kept; the same facts and selection in diagram and list; unknown, unavailable and dormant distinct; a fold opened sends no wider read; `routeInk` measured on bands before it is built on; measured `roles.spec.ts`: every staffed edge clear of every card at 80 and 39rem, with a crossings table; a hundred-node fixture; the role editor's return to its node |
| **CANVAS1d** large graphs (web; c) | positioned worlds, the spatial grid, culling past 400, the tiers, the budget, the worker layout with revisions, find, the minimap's rule (the world past 2× the viewport either way, from 36rem, switched in the sizing menu and remembered by the viewer), the view by feature key and graph identity, `aria-setsize` and `aria-posinset`, `CanvasList` grouped | unit tests for culling, tiers, anchoring and stale layouts; synthetic stories at 100, 1,000 and 5,000 and the 10,000 and 50,000 scene; §4.1's table measured into D224's note; the tiers switching at window zoom 50, 100 and 200%; the minimap's rectangle equal to the view; a key reaching a culled node; twenty opens and closes; an idle world |
| **CANVAS1e** the reference map (web; d) | §3.3: `LayeredMap`'s shell to the platform, `layers.ts` and the line kinds unchanged; workspace blocks; the ring at fit; *Lines* in the toolbar; the roving stop and the walk to lines; the view by the viewer; continuous zoom | the `MapView` and `LayeredMap` reader assertions kept but the Tab-stop one, which the walk replaces; the map's browser gate (lines pressed through their strokes); the layered map's largest fixture at 80 and 39rem with no card overlap; find centres its match; twelve to thirteen keeps the selection and the focus; each direction of a pair asked both ways chosen alone; counts conserved through blocks and filters; a pinch of 40 small deltas zooms under 2×; no act and no machine path added |
| **CANVAS1f** the code map clustered (web; e) | §3.4: `codeClusters.ts`, folders and cycles, aggregated edges with their members, the neighbourhood, folders closed past 60, find opening the path, the list by folder, `CodeMapDetail`, the provenance; the reader's bound unchanged | this repository's map, a fixture at 500 and 5,000, the synthetic 5,000; cycles, long ids, and fed, absent and refused maps; exact aggregate counts; reveal across closed folders; no card overlap opened whole; 1d's ceilings held; no edge from `docs/index/` |
| **CANVAS1g** the installed window (the steward; all; with FLOWCHART2h) | at 1546 and 680 px, en and zh, both themes: the release workflow composed by pointer, keyboard and touchpad; the canvas's and the window's zoom together; a refusal and a proposal on the chart; a run; the structure; both maps | dated shots; §4.1's spec run in the window, WebGL's renderer (SwiftShader or a GPU) and the touchpad's pinch cadence recorded; one Narrator pass per feature, along a branch and back; DPI and root-zoom checks; the declared gates green, the code map's source bound stated |

## 10. The traps, each a test

1. **Ink measured in screen space under a transform** is k times wrong. *Test:* world-unit equality at five scales.
2. **Focusing a node in the hidden overflow scrolls the viewport behind its transform.** *Test:* every ancestor's scroll 0 after a walk.
3. **A canvas that takes every wheel traps the page**, the editor's sitting above *Save*. *Test:* the wheel chained at both edges.
4. **Ctrl+wheel zooming the window too, or Ctrl+= taken from it.** *Test:* `zoomLevel()` unchanged by a canvas zoom; Ctrl+= steps it.
5. **One fixed step per pinch event** (`LayeredMap.tsx:171`). *Test:* a scripted pinch of 40 small deltas zooms under 2×.
6. **Zooming lays the drawing out again.** *Test:* a snapshot of every box equal across zoom.
7. **A relayout throws the view.** *Test:* the anchor's screen place kept after an insert, a fold and a change of language.
8. **A mark or a state moves a node:** today a refused or current `border-2` (`NodeFrame.tsx:59`, `design/Flow.tsx:36`) and the
   refusal's lines (`StepNode.tsx:44-45`) grow a box and move the ranks below. *Test:* every box equal across marks and states.
9. **`will-change` kept on blurs zoomed text.** *Test:* a measured shot comparison, the property on only during a gesture.
10. **A popover in a node, not portalled, is misplaced under the transform.** *Test:* every node control that opens one uses the portal.
11. **A culled node unreachable by a reader.** *Test:* `aria-setsize`, `aria-posinset`, the focused always mounted, the list whole.
12. **A rem taken as 16 px under the window's zoom** (`FlowCanvas.tsx:173`, `:298`; `zoom.ts:57`). *Test:* track and placement at root
    zoom 50, 100 and 200% equal the layout's needs.
13. **A canvas key typed into a field** (Shift+1 is `!`). *Test:* Shift+1, 2 and 0 in a node's field and in find type their characters.
14. **Enter in find opening the node.** *Test:* two Enters step two matches with the focus in the field.
15. **A Tab stop per card.** *Test:* one stop in the world; every line reached by → and ← from its ends.
16. **An aggregate hiding an identity.** *Test:* two ports to one step and each way of a reciprocal pair chosen alone; counts exact.
17. **A refit on a refresh.** *Test:* a status refresh and a run's state change leave the view and every box as they were.
18. **A remembered view claiming current facts.** *Test:* a view kept under one fed commit restores nothing under another; a changed
    graph keeps the surviving anchor and says the selection is gone.
19. **A position saved.** *Test:* no workflow, role record, manifest or generated map carries a coordinate after a pan, a zoom, a fold
    and a selection; the draft's fingerprint unchanged.
20. **A fold widening a read.** *Test:* opening a workspace's fold in a scoped Roles sends no read beyond the place and its parents.
21. **The spike leaking into the product.** *Test:* no `@xyflow` import under `src/` and no web dependency unless D224's note chose it.
22. **Growth across opens, and work at rest.** *Test:* twenty opens and closes retain no node, observer, listener or worker; at rest,
    no frame.

## 11. Settled after Codex's read

Codex's read (`docs/2026-11-02-canvas-reads.md`) is taken whole; these amend §2 to §10, and each row carries its part.

- **Row a's gate carries the editor and is fair.** React 19, ports, custom routes, insertion controls, pick mode and pointer capture
  are blocking contracts; if both candidates fail one, the row stops and hands back its evidence. The 12 KB and 150 KiB figures are
  each candidate's own target, not a common gate; common requirements score both alike; equivalent fully adapted scenes are measured,
  startup, parse and measurement costs and lazy loading included. A small installed-host check of wheel, focus and the reader comes
  before the lock; row g keeps the integrated journeys.
- **A state change keeps the boxes only while the scene holds.** The folds that follow current, settled and reached states and a
  refusal opening the holds group are explicit scene changes, with the anchor kept; stability is tested with the scene fixed.
- **The narrow readings stay as built.** Below 36rem a drawing that fits at 100% is drawn (release and review runs at 300 px), else
  the feature's rail.
- **The geometry inventory** includes waypoint boxes and section dividers; **the camera** keeps the map's convention (the world point
  at the top left; the transform's translation is −k × origin); **controls at the 80% floor** are measured as effective targets and
  given enlarged hit areas or a readable-scale editing step; **the interaction precedence** keeps a scrollable node's own scroll, Escape's
  order and a release outside cancelling.
- **Row c carries the appointment model:** an appointment is drawn per place it applies to, each occurrence mapped to the one
  selection and detail identity, its dormancy per place; offered roles without an appointment are named where they appear; the
  workspace fold takes a band-aware projection (today's `diagramShape` assigns workspaces to place nodes only).
- **Row e defines edge navigation before the shared API is fixed:** every incident line is a stop (cycling, order and the way back
  stated, asks and knowledge links included), replacing today's focusable line buttons.
- **Find keeps a selectable list of named results** beside stepping with the focus kept in the field.
