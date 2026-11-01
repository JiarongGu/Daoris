# CANVAS1 — the Opus maker's proposal

Status: sealed proposal, 2026-11-02, from `docs/2026-11-02-canvas-brief.md`; kept as it came.

# CANVAS1: one canvas for the workflow, the structure and the maps (sealed proposal)

*A sealed maker's proposal answering `docs/2026-11-02-canvas-brief.md` (D191; D224 reserved). It amends D223 and D186 where they meet. Web paths start at `src/Daoris.Web/src/`. I read the code and records the brief names, plus `map/` and `design/groupFolds.ts`, and changed nothing.*

## 0. The answer in short

1. **Build it ourselves. Adopt no library for the platform.** The two hard parts are already in the repository and have been proven in the window.
   - D186's measured layout: CSS places the nodes, and the ink is routed from their measured boxes (`design/FlowCanvas.tsx:468-494`, `design/flowInk.ts:60-187`).
   - A working viewport that zooms and pans: `map/LayeredMap.tsx:119-227`.

   The proposal joins them and generalises the result.
2. **The key fact: a CSS transform changes no layout.** If the measured flow is put inside a world element that wears `translate(...) scale(k)`, its ranks, text wrapping in both languages, folds, keys and reader all stay exactly as they are today. Only two things must change:
   - **Measurement must convert to world units.** Today it mixes `getBoundingClientRect` (screen space, `FlowCanvas.tsx:997`, `:1001`) with `offsetWidth` (layout space, `:1015`, `:1024`).
   - **Focus must stop scrolling the hidden overflow.** Today it calls `.focus()` with no `preventScroll` (`FlowCanvas.tsx:333-336`, `WorkflowEditor.tsx:178`, `agents/roles/RoleDiagram.tsx:146-148`).

   This is why the workflow editor can move first, with its editing logic unchanged.
3. **Two kinds of world on one platform.**
   - **Placed worlds**: HTML laid out by CSS, then measured. These are the workflow and the structure, whose words wrap.
   - **Positioned worlds**: coordinates computed in advance, with fixed-size cards. These are the reference map and the code map, whose names are one-line identifiers. A positioned world can be culled and drawn at levels of detail when it reaches thousands of nodes.
4. **No feature moves nodes, and no feature saves positions.** D223's governed record keeps that rule, and so do the structure (a projection of configuration) and the display-only maps.
5. **Order of the rows:** the workflow's viewport, then selection, marks and the connect gesture, then the structure, then scale, then the reference map, then the code map, then the installed window.

## 1. The canvas platform

### 1.1 Components (in `design/canvas/`, all molecules)

None of these imports a data hook. They hold view state only.

| Component | What it is |
|---|---|
| `Canvas` | The viewport region, the world it transforms, the layers, the toolbar, selection, keys, focus-follow, find, the minimap and the list fallback |
| `viewMath.ts` | Pure functions: zoom about a point, clamp, fit, minimal reveal, wheel → pan or zoom, keeping an anchor through a relayout, world ↔ screen |
| `worldBoxes.ts` | One `ResizeObserver` per world. Reads `[data-canvas-node]` boxes in world units (rect ÷ k, relative to the world's origin, rounded to 0.5 px). It is shared by the ink, hit-testing, the minimap and reveal |
| `CanvasToolbar` | Sits above the viewport and never floats over the drawing (the Map's pattern, `map/LayeredMap.tsx:234-289`). In order: find (where shown), the feature's own tools (the map's *Lines*), the flow's *Show whole* / *Fold again*, *Show as list*, then the percentage button that opens the sizing menu (the owner's sizing-menu direction, map design §1a `:109-112`) |
| `Minimap` | An overlay in the viewport's lower right. It is `aria-hidden`, and its view rectangle can be dragged. Rules in §3 |
| `CanvasList` | The reading that is not a canvas. It generalises the rail of `FlowCanvas` (`:592-609`) and of `RoleDiagram` (`:294-299`): nodes in reading order, every edge as a foot press, groups as headings |
| `RankedFlow` | `FlowCanvas` without its pane: stacks, folds, `layoutFlow`, sections, rank placement, `routeInk`, labels, ＋ inserts and the rail. `FlowCanvas` stays the exported composition, `<Canvas><RankedFlow/></Canvas>`, with today's props plus `viewport` |

### 1.2 Props (sketch)

```ts
type View = { x: number; y: number; k: number };          // the world point at the top left, and the scale
type Tier = 'full' | 'chip' | 'dot';
type CanvasProps = {
  label: string;                 // names the drawing for a reader (the feature's words)
  roledescription: string;       // e.g. map.panLabel's sentence: how to move it
  mode: 'display' | 'edit';
  world: { kind: 'placed'; width: number | 'pane'; render: (c: WorldContext) => ReactNode }
       | { kind: 'positioned'; frame: Box; boxes: ReadonlyMap<string, Box>;
           renderNode: (id: string, tier: Tier) => ReactNode;
           renderEdges: (visible: ReadonlySet<string>, tier: Tier) => ReactNode;
           groups?: readonly { id: string; box: Box; title: string; count: number; open: boolean }[] };
  order: readonly string[];      // reading order: the keys walk it, the list reads it
  walk: { out(id: string): string | undefined; into(id: string): string | undefined }; // → and ←, as today
  selection: { kind: 'none' | 'one' | 'many'; ids: readonly string[]; onChange(ids: string[]): void; releases: boolean };
  zoom?: { min: number; max: number };       // within [0.1, 2.5]; edit mode is floored at 0.8, at 1.0 on a coarse pointer
  opening?: 'actual' | 'fit' | { at: string };
  follow?: { focus: boolean; selection: boolean };
  find?: { text(id: string): string; placeholder: string } | null;
  minimap?: 'auto' | 'never';
  marks?: { nodes?: ReadonlyMap<string, Mark[]>; edges?: ReadonlyMap<string, Mark[]> };
  pick?: FlowPick | null;        // today's type, unchanged (FlowCanvas.tsx:138)
  connect?: { from(target: Element): { id: string; port: string } | null; onConnect(to: string): void; onGround(): void } | null;
  fitLabel: string;              // the feature's own: "Show the whole workflow", the map's "显示整张地图"
  list: ReactNode;
  place?: { key: string; keep: 'page' | 'viewer' };
};
type Mark = { word: string; kind: 'added' | 'removed' | 'changed' | 'refused' | 'note' };
// Through a ref: reveal(id, 'minimal' | 'centre'), fit(ids?), actual().
```

### 1.3 Layers, bottom to top

1. **The ground.** It has no grid: there is no placing by hand, and a grid promises snapping.
2. **Groups.** Bands, workspace and folder boxes, the holds group, section dividers.
3. **Edges.** One SVG of ink, `aria-hidden`, as today.
4. **Nodes.** HTML, supplied by the feature.
5. **Labels and ＋.** Placed by `placeLabels` / `placeInserts`, which move into `design/canvas/ink.ts`.
6. **Overlays.** Marks, pick outlines, the connection line, the box-selection rectangle.
7. **Screen space, untransformed.** The minimap, and the size readout in the toolbar.

**Overlays never change layout.** A mark is one of two things:
- an outline, which takes no room (as `NodeFrame` already draws selection, `workflow/chart/NodeFrame.tsx:57-58`);
- a chip placed where it clears every box, and dropped where it clears none. Its words are kept in the node's description and in the feature's own list (*n to put right*).

A mark never adds a line to a node. A judgement that arrives 250 ms after an edit (`WorkflowEditor.tsx:66`) would otherwise make every node jump.

### 1.4 The viewport by input

**Mouse**
- **Plain wheel pans** (Shift+wheel pans across). It is taken only while the world overflows in that direction and is not already at that edge. Otherwise the wheel scrolls the page. The editor sits in a scrolling page above *Save*, so a canvas that swallowed every wheel would trap the page.
- **Ctrl+wheel zooms about the pointer.** The step is continuous, `k × 1.25^(−Δ/100)`, so a mouse notch is one step and a touchpad's small travel is a small change, as the window's zoom gathers it (`design/zoom.ts:106-114`). Its default is prevented, so the window's zoom leaves it alone (`design/zoom.ts:117-124`; UI/UX design §3.11 `:1140-1141`).
- **Dragging pans.** Drag the ground, or drag anywhere with the middle button. A press on a node, button or `[data-flow-port]` never starts a pan.
- **Shift+drag on the ground selects by box**, where the feature allows many.
- **Pressing a node selects it.** Ctrl+press toggles and Shift+press adds, where many are allowed. A double press opens the node's door, as Enter does.
- **Pressing the ground** releases the selection in display mode (the Map's toggle, UX5 U47). In edit mode it does nothing, except in pick mode, where it is `onGround`.

**Touchpad** (Chromium on Windows)
- A two-finger scroll arrives as wheel deltas and pans both ways.
- A pinch arrives as Ctrl+wheel and zooms continuously.
- No inertia is added.

**Touch**
- One finger on the ground pans, and a pinch zooms about its midpoint. The viewport is `touch-action: none`.
- A long press opens the feature's menu where it has one (D138).

**Keyboard**
- **Tab order:** the toolbar (its controls), then the node list as one roving stop (D186 point 8, unchanged), then out.
- **Walking** (`FlowCanvas.tsx:370-428`) is kept as is: ↓ ↑ Home End walk the reading order; → and ← follow edges; Enter opens; Escape returns from a node's controls; any key with Ctrl, Alt or the system key is the page's (`:373`).
- **New, from anywhere in the canvas:**
  - Shift+1 fits (Figma's key);
  - Shift+2 reveals and zooms to the selection;
  - Shift+0 returns to 100%.

  They are read by `event.code`, so they work in every layout, and are guarded by `isComposing`.
- **On a display map's ground, as today:** bare `+` `-` `0` and the arrows (`LayeredMap.tsx:214-227`).
- **In the workflow,** `+` on a step stays the editor's *add after* (`WorkflowEditor.tsx:388`), and Ctrl+= / Ctrl+- / Ctrl+0 stay the window's zoom.
- **Focus follows.** A node that takes the focus is revealed by the smallest pan that brings it inside a 2rem margin, clear of the minimap. Reveal never changes the zoom and is never animated (D186 rejected animated moves).

**Zoom bounds**
- Each feature sets its own bounds.
- **Fit** never goes above 100% (the map's `fitted(true)`, `LayeredMap.tsx:119-130`).
- **Edit mode is floored at 80%.** That is the map's *never smaller than four fifths* (`LayeredMap.tsx:20-21`). It keeps `text-meta`, the ＋ and the ports usable. On `pointer: coarse` the floor is 100%.

**The region's height**
- The viewport is the world's height at k, plus padding, up to the window's room below it (`useTall`, as the map does). It has a floor of 20rem.
- A small workflow is therefore never a tall empty box, and a large one pans.

**Opening and moving**
- The view opens where `opening` says.
- It follows the drawing (folds, data arriving) until the person moves it. This is the map's lesson (`LayeredMap.tsx:132-150`), made general.

### 1.5 Edit and display

| | Display | Edit |
|---|---|---|
| Pressing a node | Selects or opens it | Selects it (the inspector) |
| ＋ on a way, ports, pick, connect | None | The feature's, as FLOWCHART2b built them |
| Box selection | Where `many` | Where `many` |
| Ground press | Releases the selection | Pick mode: `onGround` |
| Zoom floor | The feature's | 80% (100% on touch) |

**The canvas adds no act.** Every press is the feature's existing operation (the brief's *no press widens*; map design §1 `:67-69`).

**The connect gesture** (new in row b) takes over `FlowChart`'s pointer handling (`workflow/chart/FlowChart.tsx:51-73`) with today's drop rules:
- It draws a rubber line from the port's box to the pointer.
- The node under the pointer is outlined where `pick.can` allows it.
- Dragging near the viewport's edge pans automatically.
- Escape cancels.

### 1.6 Hit-testing

- **Placed worlds:** the DOM, through `closest('[data-canvas-node]')`, as today.
- **Positioned worlds:** a uniform spatial grid over world boxes (own code, about 80 lines), used for the dot tier and the box selection.
- **Pressable edges** (the maps) keep the map's wide invisible strokes (`map/MapCanvas.tsx:545-548`).
- **The workflow's edges stay inert** apart from their ＋.

### 1.7 States

Each state has a story:
- unmeasured (jsdom: the list, as today);
- measuring;
- actual size; zoomed (the percent shown); fitted; moved by the person;
- large (find and minimap shown);
- folded; *Show whole*;
- picking; connecting;
- the list chosen; narrow;
- the feature's empty state (EditCanvas's *start from*).

### 1.8 The narrow fallback and the reader

**The canvas floor**
- The viewport is offered from **36rem of inner width**, which is the 39rem story's 576 px (`design/storyWidths.tsx:13`).
- Below that floor, a feature keeps today's placement: drawn if it fits the width at 100%, otherwise its rail or list. At 25rem nothing changes from today.
- *Show as list* is offered at every width and remembered (`flow.list`, `FlowCanvas.tsx:170`).

**The reader**
- The nodes stay one `role="list"` in reading order inside the world, so a screen reader reads a viewport drawing as it reads a flow today.
- Placed worlds are never culled.
- Positioned worlds cull only past 400 nodes. Then:
  - each rendered item carries `aria-setsize` and `aria-posinset`;
  - the focused and selected nodes are always rendered;
  - `CanvasList` (grouped, its folds closed by default) is the whole reading, offered at the toolbar's head.

## 2. Each feature's canvas

### 2.1 The workflow: chart, editor, run and proposal diff

**Layout**
- D186's hand layout is kept whole: `layoutFlow`, the stacks, the folds past 24, sections, `routeInk`, ports, data wires and ＋ inserts. The engine stays locked on its measured table (D186 `:150-169`).
- It is always top to bottom (D223). `trackFor` (`FlowCanvas.tsx:974-990`) still sizes the track from the pane's width at k = 1, so zooming never lays anything out again.

**What the viewport changes**
- **The drawing is no longer the rail just because it is wider than the pane.**
  - A three-wide rank needs 3×14 + 2×1.5 = 45rem (`workflow/graphFlow.ts:14`; `design/flowLayout.ts:105`), so today it is the rail at 39rem. In the canvas it is drawn and panned.
  - The dense graph, which D186 drew as the rail at every frame width before folds (`:182`), is drawn and panned once *Show whole* is pressed.
- **The view opens at 100% on the entries.**
- **It follows both the focus and the selection.** That covers:
  - the head's door to a refusal (`WorkflowEditor.tsx:561`);
  - pick mode moving to the first step that can take the way (`FlowCanvas.tsx:340-344`);
  - a step just added, whose focus the editor asks for (`WorkflowEditor.tsx:169-182`).
- **Zoom is 80–200% while editing and 50–200% when reading** a place's chart or a run.
- **The minimap stays off.** D223's rejection holds for at most 24 steps. It appears only where §3's rule says so, in practice a 96-node run shown whole.

**Nodes and edges**
- `workflow/chart/` is unchanged.
- The judgement (FLOWCHART2c) and the proposal (2f) draw through **marks**:
  - a refused step: an outline in declined's hue, with its words in the description and the head's list;
  - a step in a proposal: *New*, *To remove* or *Changed*, as a chip and an outline style, never hue alone (flowchart design 2026-11-02 §4.3).

**Mode:** edit in the editor, display on the place's chart, the run and the diff's reading.

**Positions** are never saved. The view is page state.

**First row:** CANVAS1a.

### 2.2 The agents' structure: bands, not three columns

Today it is three columns (places, roles, agents) joined by *placed*, *staffed* and *appointed* edges, with no pan, zoom or drag (`agents/roles/diagram.ts:5-10`; company-view design `:153-156`, `:175`). The *placed* edges cross everything, and the hierarchy of machine → workspace → repository is flattened into one column.

**I propose bands, a swimlane layout** (pure, `agents/roles/structureBands.ts`):

**Rows.** One band per place, in tree order:
- This machine;
- each workspace;
- that workspace's repositories that set something of their own, indented under it;
- a place that sets nothing of its own stays a counted line in its workspace's band, as today (`:175`).

**Columns:**

| Band label (the tree) | The roles placed there | Gutter | Agents |
|---|---|---|---|
| The place, indented by depth | Stacked, with appointed roles among them (dashed while dormant) | | One column, each agent once, ordered by the barycentre of its roles' bands, ties in declared order |

**Edges**
- **Only *staffed* edges are drawn**, role → agent across the one gutter, numbered by position and dashed where the last reading says the choice cannot run.
- **A band means *placed* and *appointed***, so those edges disappear.
- **Inheritance is lit, never drawn** (`:165`), as today.
- **Works are lines and a filter, never nodes.** A work node would draw the resolution as edges, which §4 never draws (`:167-173`).

**Routing.** Because the roles align in one column and the agents in another, the staffed edges are a two-rank flow drawn across. `routeInk(layout, 'across', boxes)` and `placeLabels` can route them as they stand. To verify: `routeInk`'s rank extents (`flowInk.ts:72-75`) assume aligned columns, which the bands give.

**By width**
- **Up to 60rem of inner width:** the label sits beside the roles (12 + 12 + 4 + 12 = 40rem).
- **Below 60rem:** a container query moves the label above the roles (28rem), so the canvas serves the 39rem story.
- **At 25rem:** the section's list, as today (`:192-195`).

**Size.** Past 24 nodes each workspace's group of bands folds, using `groupFolds`, unchanged. *Show whole* opens them all.

**Mode:** display. Choosing a node is the selection (one) and its detail; *Edit* stays the row's editor (`:190`). Nothing moves and nothing is saved.

**First row:** CANVAS1c. It replaces `RoleDiagram` in `DiagramSection` and AGENTSPLACE1's scoped Roles.

### 2.3 The reference map (repositories and the references between them)

I read *the repository reference map* as MAP2/MAP4's workspace map: the quests, asks, chains, declared uses and shared findings between repositories.

**Layout**
- `layoutLayers` is kept whole as a positioned world (`map/layers.ts:19-25`). Its lines, lanes and bows are the map's window lessons.
- The ring (`map/MapCanvas.tsx`, up to 12 repositories) opens at fit with zoom off. It is a picture sized to its card, and panning it would add nothing.
- **Clusters by workspace.** Scoped to several workspaces, each workspace is a block with its own layers. Blocks are stacked by name, and lines between workspaces run through the gaps between blocks. A single workspace's map is unchanged.

**Nodes and edges.** The map's SVG cards and lines are kept, as its feature layers. The lines stay pressable through their wide strokes. Search, sizing and the view's place become the platform's.

**Mode:** display only. It adds no acts (map design `:67-69`). Nothing moves. The view and the folds are remembered per viewer, as *Lines* already is (`:129-133`).

**First row:** CANVAS1e. It also replaces `LayeredMap`'s fixed 1.25× step for each Ctrl+wheel event (`LayeredMap.tsx:171`). A touchpad pinch sends many such events, so one pinch probably compounds to a large jump; this is to measure.

### 2.4 The code map

**Today:** 20 modules and 20 dependencies (`docs/code-map.json`). The contract is bounded at 500 modules and 5,000 dependencies (map design `:250-251`). A map of thousands would need a finer feed (files); that is a decision for MAP3's contract and outside CANVAS1. The canvas proves that scale on synthetic fixtures, so such a feed is never blocked by the drawing.

**Layout** (pure, `map/codeClusters.ts`):
- folders taken from each module's `path`;
- nested boxes packed by name;
- inside a folder, `layerModules` as today (`map/codeLayout.ts`);
- a family-wide view clusters by repository first, then by folder.

**Edges**
- Between folders, edges are aggregated by folder pair and counted (×n) until a folder is opened.
- A selected module shows its own neighbourhood. The rest steps back, as the map's focus already does.

**At size**
- Past 60 modules, folders open closed.
- Find opens the path to its match.
- The list is grouped by folder, with `CodeMapDetail` (`map/CodeMap.tsx:197-267`) kept as the detail.

**Cards.** Fixed size, with an id cut with an ellipsis (`FITS`, `CodeMap.tsx:16-17`), so the layout needs no measuring.

**Mode:** display only. Nothing moves. The view is remembered per viewer.

**First row:** CANVAS1f.

## 3. Scale and reading

**At about 100 nodes** (the structure, the reference map, a run shown whole)
- Every node is HTML at full tier, and nothing is culled.
- Find is shown past 40 nodes.
- The minimap appears where the world exceeds the viewport by 2× in either direction and the inner width is at least 36rem. It can be switched in the sizing menu, and the choice is remembered per viewer.

**At thousands** (positioned worlds only)
- Only what is visible, plus a margin, is rendered.
- The tier follows the **effective size of `text-meta`**: 11 px × the window's zoom × k.

  | Effective size | Tier | What is drawn |
  |---|---|---|
  | ≥ 9 px | full | The node's own component |
  | Below 9 px | chip | A box, with its one line drawn at the type scale's size (counter-scaled) where it clears its neighbours, otherwise hidden |
  | Below 4 px | dot | A canvas2D layer, `aria-hidden`; group titles stay HTML at the type scale |

- **Nothing is ever drawn smaller to fit:** the person's zoom is the only scale.
- **Strokes do not scale below 100%** (`vector-effect: non-scaling-stroke`), so the ink never thins under 1 px.
- **Arrowheads stay in world units** (the map's lesson, map design `:59-60`).

**Clusters and expanding.** One mechanism serves every feature: a fold is a node with a press, and every edge across its boundary is drawn to it and counted. It covers:
- the flow's stacks, its settled stretches and its focus (`design/flowFolds.ts`);
- the structure's workspace groups and the maps' workspace and folder boxes (`design/groupFolds.ts`, or the positioned groups);
- *Show whole* and *Fold again* in the toolbar.

**Find that focuses**
- **Enter** reveals the next match and moves the focus to it; **Shift+Enter** goes back.
- It says *n of m*, and dims what does not match (`LayeredMap.tsx:479-495`).
- A match inside a fold opens that fold.
- Its placeholder is the feature's own (*Find a repository*, `map.find`).

**Keeping the person's place**
1. **The focus is kept by node id** (`FlowCanvas.tsx:346-353`).
2. **Relayout keeps the anchor.** Before a new layout, the canvas records where the focused node sits on screen (else the selected node, else the node nearest the centre). Afterwards it pans so that node is at the same place. An added step therefore never throws the view (yEd's *keep the mental map*).
3. **The view follows the drawing until the person moves it.**
4. **Memory.** The view, opened folds and selection are page state for the workflow and the structure. For the maps they are stored per viewer under `daoris.canvas.<feature>.<key>` (try/catch, a convenience only).

**Performance targets**

Measured as ceilings in `measured/canvas.spec.ts` (Chromium), then in the installed window:

| Case | Drawn | p95 frame while panning and zooming | First draw | Else |
|---|---|---|---|---|
| Workflow, the dense graph shown whole | ≤ 96 | ≤ 16.7 ms | ≤ 100 ms | An edit's ink drawn ≤ 50 ms; no long task over 50 ms |
| Structure | ≤ 400 | ≤ 16.7 ms | ≤ 200 ms | |
| Reference map | ≤ 200 cards | ≤ 16.7 ms | ≤ 150 ms | |
| Code map, at the bound / synthetic | 500 / 5,000 | ≤ 16.7 / ≤ 33 ms | ≤ 300 / ≤ 800 ms (layout in a worker if over 50 ms) | Find to reveal ≤ 100 ms |
| Bundle | | | | The platform adds ≤ 12 KB gzipped and no dependency |

**How they are measured**
- The spec scripts `page.mouse.wheel` (with Ctrl for zoom) over the stories.
- Frames are collected in the page with requestAnimationFrame deltas and the Long Animation Frames observer.
- The ceilings are recorded the way the crossing table records its ceilings (`measured/flow.spec.ts:160-176`).
- The pure layouts get vitest benches: `layoutFlow` at 96, bands at 400, clusters at 5,000.

## 4. Build or adopt

| | Ours (extend `FlowCanvas` + `LayeredMap`) | @xyflow/react 12 | Cytoscape.js | sigma.js 3 + graphology | d3-zoom over our rendering | dagre / elkjs layout |
|---|---|---|---|---|---|---|
| Licence | The repository's | MIT; shows an attribution by default | MIT | MIT, MIT | ISC | MIT / EPL-2.0 or GPL-3.0 |
| Size, gzipped | ~a few KB of new code | ~50–60 KB with zustand and d3 (unverified) | ~110–130 KB core (unverified) | ~50–70 KB (unverified) | ~12–15 KB with its d3 parts (unverified) | 16,897 B / 466,978 B (**measured**, flowchart design `:487`) |
| Layout model | CSS-placed and measured, or positioned | Stored x,y per node; two passes to auto-lay HTML nodes | Its own layouts over canvas nodes | Coordinates; force-directed by default | Ours | Coordinates over sizes given up front |
| Theming through the tokens | Native Tailwind tokens | CSS variables, mappable | A JS stylesheet; restyled on a theme change | JS colours to WebGL | Native | – |
| Keyboard and reader | D186's one stop, roving, descriptions, kept | Every node a Tab stop by default; arrow keys move nodes; ours would override most | None (canvas) | None (WebGL) | Ours | – |
| Both languages | HTML wraps 中文 (D186 `:71-72`) | HTML nodes | Canvas text, no wrap | Canvas text | Ours | – |
| Testability | vitest + the measured stories, as today | jsdom needs ResizeObserver and DOMMatrix stubs | A headless mode for layout | Needs WebGL; no jsdom | As ours | – |
| At scale | DOM to ~1k, then cull and LOD with canvas2D | DOM, comfortable to ~1k with virtualisation | Thousands; layout is the cost | 10k+, with a GPU | As ours | – |
| Chromium host | Plain DOM, SVG and canvas2D | DOM | canvas2D | **WebGL in CEF 154 unverified** (D92 `:4`) | DOM | – |

**Verdict: build.**
- **React Flow** would replace exactly what D186 measured as better: CSS placement that reflows by itself, against coordinates and a second pass. Its gains are a viewport, a minimap, a box selection and a connection line, and each is a few hundred lines we either already have (`LayeredMap.tsx:119-227`) or need anyway. D223's rejection (`docs/decisions/D223.md:16-17`) holds on the merits, and row a re-measures it rather than presuming it.
- **Cytoscape** is refused: canvas nodes cannot be read or keyboarded (D186 `:71-72`), its stylesheet sits beside the tokens, and it overlaps sigma's niche at lower performance.
- **elkjs** stays rejected on size and licence (D186 `:65-66`).
- **dagre** lost the measured table (D186 `:156-169`).
- **d3-zoom** is refused: the gesture arithmetic is small, and d3-selection's listeners, transitions and inertia fight React and the window zoom's *a wheel already taken* contract.
- **sigma.js + graphology is the one named exit**, for the code map only, if:
  - a file-level feed lands and positioned worlds past 5,000 miss 33 ms at the dot tier, **and**
  - WebGL is verified as GPU-backed in the installed window.

**Verified in this repository** (by reading)
- No graph or viewport dependency (`src/Daoris.Web/package.json:22-61`).
- D186's dagre table and the elkjs and dagre sizes (D186 `:150-169`; flowchart design `:482-491`).
- A viewport proven in the window, with its notes (`LayeredMap.tsx`; map design `:102-112`).
- The window zoom leaves a taken wheel alone (`zoom.ts:117-124`).
- The measured ink mixes screen and layout units (`FlowCanvas.tsx:997-1024`).
- Focus calls lack `preventScroll` (as cited in §0).
- Trap 15 forbids a transform over text (`docs/2026-10-20-flowchart-design.md:613`).
- The code map's size and bound.
- The host is CEF 154.

**Not verified** (from my knowledge; to measure)
- Every library size marked unverified above.
- React Flow's accessibility defaults.
- Cytoscape and sigma performance.
- WebGL and GPU compositing in the installed window.
- Frame times of an HTML world at 1–5k nodes in CEF.
- The cadence of touchpad pinch events in CEF on Windows.
- Whether Shift+1 / 2 / 0 collide with anything in D152's table (`menus/shortcuts.test.ts` shows only Ctrl+digit in use).

**What to measure first** (in row a, as FLOWCHART1a1 measured dagre before the engine was locked):
1. **World-unit ink under scale.** The editor's and the dense graph's stories at k = 0.5, 0.8, 1, 1.5 and 2. Paths and label boxes equal to k = 1 within 0.5 px. No edge through a box. Crossings at or under the flow table's ceilings.
2. **Focus without scroll.** Walk the focus to a node off screen: the viewport's `scrollTop` and `scrollLeft` stay 0, and the node is revealed.
3. **Frame times.** Synthetic placed worlds of 100, 500 and 2,000 `StepNode`s while panning and zooming, with and without `will-change: transform`. Set it only during a gesture: kept on, Chromium tends to keep the raster scale and blurs text when zoomed (to measure).
4. **A React Flow spike, kept nowhere.** The same fixtures with `StepNode` as custom nodes and our placement converted to x,y. Record: the bundle difference in a `vite build`; the count of Tab stops and roles in the accessibility tree; key conflicts; the measured-box table. Lock on the table.
5. **The window's zoom times the canvas's** at 50, 100 and 200%: the effective text sizes where the tiers switch.
6. **In the installed window (steward):** WebGL2 context creation and `WEBGL_debug_renderer_info` (SwiftShader or a GPU), and how touchpad pinch events arrive.

## 5. What becomes of today's code

| Today | Becomes |
|---|---|
| `design/FlowCanvas.tsx` | Split into `design/canvas/*` and `design/RankedFlow.tsx`. The `FlowCanvas` export and its props stay, plus `viewport?: 'none' \| CanvasOptions` (default `'none'`) |
| `design/Flow.tsx`, and its inline callers: `quests/QuestFlow.tsx`, `work/head/SessionFlow.tsx`, `work/head/PartsFlow.tsx`, `projects/git/LandingFlow.tsx`, `workflow/WorkflowStartNode.tsx` | **Unchanged, with no viewport.** They are small reads in a scrolling page, where a viewport would trap the wheel and add chrome. They can opt in later on a measured need |
| `flowLayout.ts`, `flowFolds.ts`, `groupFolds.ts` | Kept whole. `placementFor` gains the canvas floor |
| `flowInk.ts` | Kept. Its boxes are world units by contract. `placeLabels` and `placeInserts` move to `design/canvas/ink.ts` |
| `workflow/chart/*` | Unchanged. `FlowChart`'s port drag (`:51-73`) becomes the canvas's `connect`; `ChartEdit` is unchanged |
| `workflow/edit/*` | **Logic unchanged**: the draft's operations, inspector, judgement and proposals. `EditCanvas` passes `viewport` (opens at actual size, follows focus and selection). The editor's focus call takes `{ preventScroll: true }` (`WorkflowEditor.tsx:178`) |
| `agents/roles/RoleDiagram.tsx`, `diagram.ts` | Replaced by `StructureCanvas` and `structureBands.ts`. `NodeBox` becomes `StructureNode`. `diagramShape`'s folds and `DiagramSection`'s API are kept |
| `map/LayeredMap.tsx` | Its viewport, wheel, keys, search and sizing (`:111-289`) go to the platform. `layers.ts` and the SVG lines stay as a positioned world |
| `map/MapCanvas.tsx` | The ring is kept, as a fitted positioned world |
| `map/CodeMap.tsx`, `codeLayout.ts` | `CodeMapCanvas` is replaced by `codeClusters.ts`. `layerModules` and `CodeMapDetail` are kept |
| FLOWCHART2's remaining rows | **2c** (the judgement) and **2f** (the proposal) draw through CANVAS1b's marks. **2g** is unchanged. **2h**'s window proof gains the canvas's checks. SETUPUX1r, s and t are inspector-side and unordered against the canvas |

**Words** (through D222's catalogues, with glossary rows):
- `map.zoomIn`, `zoomOut`, `size`, `sizing` and `sizes` move to `canvas.*`.
- New: `canvas.actual`, `canvas.toSelection`, `canvas.minimap` (缩略图, VS Code's word) and `canvas.matches` (*{{at}} of {{count}}*).
- Each feature words its own *fit* (*Show the whole workflow* / 显示整个工作流, within the button budget) and its own region's sentence.

## 6. The established patterns

| Tool | Taken | Refused, and why |
|---|---|---|
| **Figma** | Shift+1, Shift+2 and Shift+0; Ctrl+wheel and pinch zooming about the pointer; a percentage menu; the selection outline with an inspector beside | Free placement, frames and auto-layout authoring: a workflow is a governed record. Drag on the ground selecting by default: here pan is the common act, so box selection needs Shift. Space+drag: Space already chooses a node (`RoleDiagram.tsx:190`) |
| **Miro** | *Fit*, and a navigator (minimap) that can be switched off | An infinite whiteboard, sticky notes, ink, live cursors: shared hosts serve no UI, and none of these is a record |
| **Excalidraw** | A view mode distinct from edit; the keys said in one place | The sketchy look, freehand, a free canvas |
| **tldraw** | A camera kept separate from what is drawn (screen ↔ page); culling off screen; auto-pan at the edge while dragging | Free shapes, and arrows bound by hand |
| **yEd** | A layout per kind of diagram (hierarchic, grouped, tree); group folding with edges redirected and counted; incremental layout that keeps the mental map (our anchor) | A *Layout* command the person must run: every change lays itself out (D223's *Tidy* refused) |
| **draw.io** | The outline panel; swimlanes (the structure's bands); routing round nodes | Connectors and waypoints by hand, the style editor, saved coordinates |
| **React Flow's examples** | Handles and drag-to-connect with a connection line; `isValidConnection`, which is our `pick.can`; `fitView` with padding; controls and minimap; Shift for box selection; rendering only what is visible; sub-flows as groups | Stored node coordinates, dragging nodes, every node a Tab stop, floating node toolbars, a library as the renderer |
| **n8n** | ＋ on a way and at an end, already built; the node creator, which is our `StepPicker`; zoom to fit on open | Free placement, *Tidy up*, sticky notes, hover-only buttons on edges (D223) |
| **Node-RED** | A search across nodes that jumps to one and marks it; wiring from a port; a switchable navigator | Flows as tabs on one canvas, link nodes, grouping by hand |
| **Cytoscape** | Compound nodes, as our bands and folders; the neighbourhood lit and the rest stepped back; marks from data | Canvas-drawn nodes for anything a person edits or a reader reads |
| **Gephi** | Labels by level of detail; filtering by kind (the map's *Lines*); a selected node's neighbourhood | Force-directed layouts by default: they are not deterministic, and D186 point 2 wants *same graph, same picture*. Colour by community and size by metric: meaning by hue or size without words |
| **Code-graph views** (VS Code's call and type hierarchy and outline; JetBrains' package diagrams; a code host's dependents list and references panel; Nx's project graph; Sourcetrail) | A list or tree beside every graph, and the list as the authority (`CodeMapDetail`); expanding and collapsing a package or folder; focusing one node with its neighbours; grouping by folder; *show all* as an explicit press | Opening on the hairball; a graph as the only way to read dependencies |

## 7. What stands and what is amended

**D223 stands** in each of these:
- top to bottom by default;
- truthful endings;
- the person names no wire kind;
- Ask Daoris proposes, and the person takes and saves;
- one component set;
- positions never saved;
- no free placement, no *Tidy*, no hover-only control;
- React Flow not adopted (re-measured in row a).

**D223 is amended in one place.** Its rejection of *a canvas zoom, panning and a minimap* is replaced by §2.1: a viewport that opens at 100%, follows the focus and selection, zooms 80–200% while editing, and shows a minimap only past 2×.

**D186 stands** in each of these:
- one primitive per process;
- the hand layout and its locked table;
- the rail as the narrow placement;
- an edge is ink, never a status hue;
- the list in reading order;
- no animated moves;
- the Map draws no process.

**D186 is amended** in three places:
- **Point 2** (`:26`): *no panning, no zoom* becomes the viewport. Stored positions and dragging stay rejected (`:64`).
- **Trap 15** becomes: *the canvas never shrinks a drawing on its own; only the person's zoom scales it, and below a readable size a node draws its chip*.
- **The rejection of drawing on a canvas** (`:71-72`) keeps one exception: the dot tier of display-only maps, `aria-hidden`, with the list carrying the reading.

**The map design.** *No graph library* stands (`:48-50`). Its open question *MAP3 may need one* is answered: not yet, with sigma as the named exit.

**The company-view design.** *No pan, zoom or drag* (`:175`) becomes pan and zoom, with no drag. Its three columns become bands.

## 8. Rows in build order

Every visible row carries:
- page tests;
- stories at 80, 39 and 25rem, in en and zh, in both themes;
- keyboard throughout;
- its own line in D224's note and its own translations.

| Row (lanes; after) | Contract | Proof |
|---|---|---|
| **CANVAS1a**: the workflow's chart stands in a viewport (web; first) | `design/canvas/` (`viewMath`, `Canvas`, the toolbar with its sizing menu and *Show as list*, `worldBoxes`). The region's height rule, wheel chaining, continuous Ctrl+wheel and pinch, panning by drag, middle button and touch, Shift+1/2/0, following by the smallest pan, focus with `preventScroll`. `FlowCanvas` on it with `viewport`. The editor, the place's chart (`workflow/place/EffectiveChart.tsx:83`), the tab's chart and the run (`workflow/WorkflowChart.tsx:426`) opt in from 36rem; inline flows `'none'`. Carries §4's measurements and locks on them | Every existing Flow, FlowCanvas, FlowChart and WorkflowEditor test unchanged. `viewMath.test.ts`. Key tests: `+` on a step still adds, Ctrl+= is still the window's, a Ctrl+wheel taken leaves the window's level unchanged. Measured: ink identical in world units at five scales, no edge through a box, no label or ＋ over a box, crossings at or under the ceilings, scroll 0 after a focus walk, a resize draws once (trap 14). The dense graph drawn and panned at 80rem. p95 ≤ 16.7 ms |
| **CANVAS1b**: selection, marks and the connection line (web; a) | Selection none, one or many (press, Ctrl and Shift presses, Shift+drag box; release in display mode). Marks as outlines and placed chips that take no room. `FlowPick` kept. `connect` with its rubber line, the target outlined, auto-pan, Escape. The workflow keeps one selection; FLOWCHART2c and 2f build on the marks | A page test per gesture and key. Measured: every node's box equal with and without marks, the connection line starting inside its port's box at every k. FLOWCHART2c's refused-step story |
| **CANVAS1c**: the agents' structure as bands (web; b) | `structureBands.ts`, `StructureCanvas` and `StructureNode`. Only staffed edges drawn. Lit as today. Workspace folds past 24. The label above the roles below 60rem. The list below the floor. Replaces `RoleDiagram` in COMPANYVIEW1 and AGENTSPLACE1 | `structureBands.test.ts` over `structureFixtures` (tree order, counted places, the agents' barycentre, folds). The reader assertions of `RoleDiagram.test.tsx` and `RolesDiagram.test.tsx` kept. Measured `roles.spec.ts`: every staffed edge clear of every card at 80 and 39rem, with a crossings table |
| **CANVAS1d**: large graphs (web; c) | Positioned worlds, the spatial grid, culling past 400, the full, chip and dot tiers by effective size, find, the minimap rule, keeping the anchor, the view kept per feature key, `aria-setsize` and `aria-posinset`, `CanvasList` grouped | Unit tests for culling, tiers and anchoring. Synthetic stories at 100, 1,000 and 5,000. Measured performance ceilings (§3) written into D224's note. The tier switches at window zoom 50, 100 and 200%. The minimap's rectangle equal to the view |
| **CANVAS1e**: the reference map (web; d) | `LayeredMap`'s viewport, toolbar and search become the platform's. `layers.ts` and the line kinds unchanged. Workspace blocks when scoped to several. The ring at fit. *Lines* in the toolbar's slot. The view remembered per viewer. A pinch zooms continuously | The reader assertions of the `MapView` and `LayeredMap` tests kept. The map's browser gate (lines pressable through their strokes, names outward). The 29-repository fixture at 80 and 39rem with no card overlap. Find centres its match |
| **CANVAS1f**: the code map clustered (web; e) | `codeClusters.ts` (folders, packing, layers inside a folder, aggregated edges, the neighbourhood). Folders closed past 60. Find opens the path. A list by folder. `CodeMapDetail` kept | Tests over this repository's map, a fixture at the 500/5,000 bound, and a synthetic 5,000. Measured: no card overlap when opened whole; 1d's ceilings hold |
| **CANVAS1g**: the installed window (the steward; all, with FLOWCHART2h) | In CEF at 1546 and 680 px, in en and zh, in both themes: compose the release workflow by pointer, keyboard and touchpad; canvas and window zoom together; the structure; both maps | Dated shots. The performance spec run in the window, with the WebGL and GPU state recorded. One Narrator pass per feature, along a branch and back |

## 9. The traps, each a test

1. **Ink measured in screen space under a transform**: the paths would be k× wrong. Test: world-unit equality at five scales.
2. **Focusing a node inside the hidden overflow scrolls the viewport behind its transform.** Test: scroll 0.
3. **A canvas that takes every wheel traps the page.** Test: wheel chaining at both edges.
4. **Ctrl+wheel zooming the window as well as the canvas, or Ctrl+= taken from the window.** Test: `zoom.ts` level unchanged after a canvas zoom.
5. **One fixed step per pinch event** (`LayeredMap.tsx:171`). Test: a scripted pinch of 40 small deltas zooms by under 2×.
6. **Zooming lays the drawing out again.** Test: a box snapshot unchanged across zoom.
7. **A relayout jumps the view.** Test: the anchor's screen position kept after an insert.
8. **A mark moves a node.** Test: boxes equal with and without marks.
9. **`will-change` blurs text after zooming.** A measured shot comparison.
10. **A popover inside a node not portalled**: `position: fixed` inside a transformed ancestor is mispositioned. Test: every node control that opens one uses the kit's portal.
11. **A culled node unreachable by a reader.** Test: `aria-setsize` and `aria-posinset`, the focused node always rendered, and the list's count equal to the graph's.

## 10. What this proposal did not verify

- No build, test or window was run.
- Every library figure marked unverified is from memory.
- The reuse of `routeInk` for the bands, and Chromium's raster scale with `will-change`, are reasoned, not measured. Row a and row c each measure their claim before building on it.
