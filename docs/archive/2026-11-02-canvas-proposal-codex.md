# CANVAS1 — Codex's proposal

Status: sealed proposal, 2026-11-02, from `docs/archive/2026-11-02-canvas-brief.md`; Codex, read-only, kept as it came.

**Status:** proposed, 2026-11-02. Amend D223 and D186’s viewport and rendering choices; retain their workflow semantics, measured geometry and accessible reading.

Adopt **React Flow behind a Daoris-owned canvas interface**, while keeping Daoris’s workflow layout and feature components. Use one viewport, interaction model and accessibility contract for workflows, structure and maps. Layout remains a feature decision. Positions remain derived presentation state.

The first delivery is the workflow editor working on that canvas. The platform grows through complete feature migrations; it does not ship first as an unused framework.

**Evidence and limits**

This proposal follows the repository’s `doc-loader` and `pattern-finder` workflows. The exemplars are `FlowCanvas` and `workflow/chart/FlowChart`: worded props, feature-supplied components, outgoing callbacks, local measurement, stable identities and a rail using the same graph.

The source review establishes four important starting facts:

| Verified in this checkout | Consequence |
|---|---|
| `FlowCanvas` already accepts custom nodes, labels, inserts and pick mode, but chooses CSS placements and measures their resulting boxes. It has no viewport transform. `src/Daoris.Web/src/design/FlowCanvas.tsx:143`, `src/Daoris.Web/src/design/FlowCanvas.tsx:298`, `src/Daoris.Web/src/design/FlowCanvas.tsx:993` | Preserve its feature seams and geometry contracts; replace its placement shell. |
| The workflow editor already uses `graphFlow` and `FlowChart`. Its draft operations are separate from drawing. `src/Daoris.Web/src/workflow/edit/EditCanvas.tsx:16`, `src/Daoris.Web/src/workflow/chart/FlowChart.tsx:45`, `src/Daoris.Web/src/workflow/edit/graphDraft.ts:333` | Moving the editor does not require rewriting its graph semantics. |
| `RoleDiagram` implements its own measurement, navigation and folding around shared layout helpers. `src/Daoris.Web/src/agents/roles/RoleDiagram.tsx:89`, `src/Daoris.Web/src/agents/roles/RoleDiagram.tsx:142`, `src/Daoris.Web/src/agents/roles/RoleDiagram.tsx:232` | This duplicated machinery should leave the feature. |
| The maps use separate SVG renderers. The large repository map **already pans, zooms and searches**. `src/Daoris.Web/src/map/MapView.tsx:175`, `src/Daoris.Web/src/map/LayeredMap.tsx:152`, `src/Daoris.Web/src/map/CodeMap.tsx:114` | Migrate existing behaviour, including its useful conventions; do not treat every map as a `Flow` caller. |

The code-map source is presently bounded at **500 modules, 5,000 dependencies and 1 MiB**. `docs/index/` is a separate generated orientation index with no page consumer. A canvas supporting thousands of nodes does not make either source richer. `src/Daoris.Service/Daoris.Service.Core/Knowledge/Feed/CodeMap.cs:42`, `docs/archive/2026-09-23-map-design.md:357`

No builds, tests, story renders, package experiments or installed-window checks were run for this read-only proposal. External library and product comparisons below come from established knowledge; their selected versions, licences, bundle costs and behaviour in Daoris remain unverified.

**The platform boundary**

Introduce `design/canvas/` with a small public surface. Feature code never imports React Flow directly.

| Component or contract | Responsibility |
|---|---|
| `Canvas` | Bounded viewport; controlled scene, selection and view; pointer arbitration; feature renderers. |
| `CanvasControls` | Zoom percentage and menu, zoom in/out, 100%, Fit, Focus selection, List, optional Follow selection. Controls remain at normal UI size. |
| `CanvasMiniMap` | Overview of groups and visible extent; pointer navigation, with equivalent named controls elsewhere. |
| `CanvasReading` | Semantic reading order, node descriptions and explicit incoming/outgoing relationships. Provides the list placement. |
| `CanvasNodeFrame` | Identity, focus, selection and measurement registration around the feature’s node component. |
| `CanvasEdge` | Feature-supplied visual path and label, plus a separate screen-sized hit area. |
| `CanvasOverlay` | Named run, proposal, refusal and selection decorations that do not affect layout. |
| `LayoutPlan` | World-space node boxes, port anchors, edge routes, groups, bounds and reading order. Independent of the viewport engine. |

The core props are approximately:

```ts
type CanvasProps<N, E> = {
  label: string;
  scene: CanvasScene<N, E>;
  layout: LayoutPlan;
  view: CanvasView;
  selection: CanvasSelection;
  presentation: 'canvas' | 'list';
  followSelection: boolean;

  interaction:
    | { mode: 'display'; selection: 'one' }
    | {
        mode: 'edit';
        selection: 'one' | 'many';
        boxSelection: boolean;
        connection?: ConnectionInteraction;
      };

  renderNode: NodeRenderer<N>;
  renderEdge: EdgeRenderer<E>;
  renderLabel?: LabelRenderer<E>;
  renderOverlay?: OverlayRenderer<N, E>;

  onViewChange: (view: CanvasView, reason: ViewReason) => void;
  onSelectionChange: (selection: CanvasSelection) => void;
  onPresentationChange: (presentation: 'canvas' | 'list') => void;
};
```

`CanvasScene` contains stable IDs, feature data, group membership, accessible words and relationship identities. It contains no bridge, query or service object. `CanvasSelection` distinguishes nodes from edges and retains a primary item for focus.

The edit contract supplies connection eligibility and connection **intent** callbacks. React Flow’s default edge creation, deletion and reconnect mutations are disabled. The workflow adapter interprets an intent using `graphDraft`; the canvas never decides what a wire means.

Display mode permits navigation, selection, expansion and opening existing doors. It offers no graph mutation. A run may still contain its owner’s operational controls, just as it does today; display mode does not remove those controls or grant new ones. `src/Daoris.Web/src/workflow/WorkflowRunView.tsx:422`

Organisms retain data hooks, persistence and domain actions. Canvas molecules receive props and emit callbacks; local measurement and interaction state follow the existing components’ pattern. This preserves the repository’s presentational boundary. `docs/archive/2026-10-20-ui-ux-design.md:1167`

**State and layers**

Keep three independent state families:

- **Domain state:** draft graph and history, run records, proposal transactions, structure readings and map facts. Features own these.
- **View state:** camera, presentation, expanded groups, selection, primary focus and follow preference.
- **Transient interaction:** idle, panning, boxing, connecting, keyboard picking, or interacting with a node’s controls.

Loading, empty, unread and layout-failed states have explicit readings. During a new layout, retain the previous valid picture with an updating notice; discard obsolete layout results by scene revision. If the first layout fails, show the list with the failure’s words. No blank canvas implies an empty graph.

The visual stack is:

1. Optional group boundaries.
2. Edges.
3. Nodes.
4. Labels and insertion controls.
5. Run, proposal and refusal marks.
6. Transient connection and box-selection feedback.
7. Screen-space controls, search and minimap.

Marks reserve space within the existing node frame or sit outside its measured geometry. Selection, hover and run-state changes must not change the layout revision. Proposal previews deliberately produce a different scene; they are not ordinary state decorations.

**Viewport behaviour**

Use a bounded viewport inside the existing page region. Its height follows available space with a sensible minimum; the document retains ordinary scrolling around it. No canvas press resizes a frame pane, opens a wider window or changes authority.

| Input | Behaviour |
|---|---|
| Primary click | Select a node or edge. A background click clears selection after distinguishing it from a pan. Existing feature toggle-selection behaviour remains available. |
| Background drag | Pan. Middle-button drag or Space-drag also pans. Node controls and text fields retain their own gestures. |
| Touchpad two-finger movement / ordinary wheel | Pan while over the canvas ground. Scrollable controls inside nodes and inspectors retain scrolling. Normalize wheel delta modes. |
| Ctrl+wheel / pinch | Zoom continuously about the pointer or gesture centre; preserve the world point underneath it. Consume the event once. |
| Toolbar zoom | Zoom about the viewport centre; use readable steps and expose the current percentage. |
| Fit | Include nodes, return lanes, labels and group boundaries within padded bounds. Never fit on every data refresh. |
| Focus selection | Reveal the selected node at a readable scale; a selected edge reveals both ends and its relationship detail. |
| Double click | No automatic node mutation or zoom. Use the feature’s explicit Open action. |
| Touch | Tap to select; two-finger pan/pinch. Connecting also has tap-source, tap-target and inspector paths. |

Use an initial **80–100% readable view**, following the precedent already implemented by `LayeredMap`. Fit may show a much smaller overview when explicitly requested; it must not pretend that tiny labels are readable. `src/Daoris.Web/src/map/LayeredMap.tsx:119`

Start with a proposed zoom range of 10–250%, subject to the first installed-window measurements. At overview scales, suppress inline editing affordances and show compact marks; selecting a result offers Focus to read or edit it. The toolbar and accessible list never shrink with the camera.

**Follow selection** means that a selection made in the list, inspector, search or problem list is revealed with the smallest necessary camera movement. It does not mean repeatedly centring it. Following a run’s current step is a separate, explicit feature option and starts off. A manual pan suspends automatic run following.

The global zoom contract already leaves a wheel consumed by a self-zooming surface alone. Canvas zoom should use that seam, rather than introduce a second global listener. Ctrl+=, Ctrl+− and Ctrl+0 remain window zoom commands. `src/Daoris.Web/src/design/zoom.ts:121`, `docs/archive/2026-10-20-ui-ux-design.md:1125`

**Keyboard, readers and narrow placement**

Keep the current flow navigation model:

- Up/Down and Home/End traverse feature reading order.
- Right follows a relationship; Left returns by the relationship used, otherwise an incoming relationship.
- Enter selects or opens the feature’s item.
- Escape cancels an interaction first, then returns from inner controls, then clears selection where the feature supports it.
- A named action enters a node’s controls; Escape returns to its roving focus stop. Every branch is also available through explicit relationship controls.
- On the **viewport ground**, arrows pan and `+`, `−`, `0` zoom or fit. On an editor step, `+` retains Add step. Existing undo, copy, paste and semantic move commands remain the editor’s.
- Composition keystrokes belong to the input method.

The existing roving navigation and IME handling are implemented in `FlowCanvas`; they are not delegated wholesale to a library’s defaults. `src/Daoris.Web/src/design/FlowCanvas.tsx:370`, `src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:375`

Screen readers receive a named region, a logical node reading and worded relationships. Decorative paths are hidden. Avoid duplicate library and Daoris announcements. Announce selection, connection eligibility, completed edits and failures; do not announce every pan frame or live status repaint.

For a culled graph, keyboard navigation first mounts and reveals the destination, then focuses it. Focused items remain mounted. A relationship list handles a high-degree node without forcing its entire neighbourhood into one announcement.

**List is a full placement**, offered at every width. Automatically prefer it when the region cannot support readable nodes and controls: the workflow’s 25rem main area and the run’s 300px sidebar are required cases. Structure retains its readable list at widths where its columns cannot serve it. The decision uses the component region, not the window.

The editor’s list supports the same selection, picker, ways table, options, removal, clipboard and proposal actions. It is never a thumbnail beside an unusably small canvas. Changing placement keeps selection, reading anchor and open inspector. Only one drawer or picker owns modal focus at a time.

This carries D223’s settled narrow contract and the shared container-width rule. `docs/2026-11-02-flowchart-design.md:136`, `docs/archive/2026-10-20-ui-ux-design.md:281`

**Geometry and hit-testing**

Use one explicit conversion:

`screen = viewport origin + translation + scale × world`

Layout and routing consume world coordinates. Pointer positions, selection rectangles, ports and measured boxes cross this boundary through shared functions.

Today’s measurement subtracts screen rectangles without undoing a canvas transform, while label sizes use untransformed offsets. Wrapping the existing component in `transform: scale(...)` would mix coordinate spaces. Replace that measurement path as part of the first row. `src/Daoris.Web/src/design/FlowCanvas.tsx:993`

Measure node and port dimensions in layout coordinates. Language, font readiness, root text zoom and content changes invalidate dimensions; camera changes do not. Convert rem-based spacing using the actual root size rather than adding another fixed 16px assumption.

Interaction priority is inner control → port → node → edge hit path → background. Labels and insertion buttons remain independently actionable. Use pointer capture, a drag threshold, `pointercancel` and lost-capture handling; releasing outside the canvas cancels safely. A connection commits one intent on a valid release.

For edges, keep thin visual ink but a wider invisible hit stroke whose effective screen width stays usable across zoom. Do not merge identities merely because paths share ink: selecting an aggregate opens its member relationships, and two outcome ports reaching one step remain individually addressable. The map already records why wide invisible strokes are necessary. `docs/archive/2026-09-23-map-design.md:52`

**Feature canvases**

| Feature | Layout and components | Editing and saved positions | First canvas row |
|---|---|---|---|
| Workflow editor | Automatic top-to-bottom ranks; existing step variants, labelled outcome ports, holds groups, configured-end marks and truthful terminals. | Edit graph semantics through existing draft operations. No drag-to-position and no saved coordinates. | **CANVAS1a:** compose and edit the release workflow on the viewport, including its rail. |
| Place workflow chart | Same workflow layout and components; existing settings and doors supplied by its owner. | Display graph; existing configuration actions retain their destinations and authority. No saved coordinates. | **CANVAS1b:** chosen effective workflow on the shared canvas. |
| Workflow run | Same bound graph, with recorded states and existing controls; selection opens retained step details. | Display graph. No invented traversal history or outcome. No saved coordinates. | **CANVAS1b:** run shown beside the place chart’s migration. |
| Proposal diff | One union scene with New, Changed and To remove marks; structured change list and review bar. | Preview is display mode. Taking changes calls the existing proposal transaction into the draft. No saved coordinates. | **CANVAS1c:** take, partially take, reject stale changes and undo on the canvas. |
| Agents’ structure | Hierarchical place groups plus role/staffing columns; work facets from existing resolved readings. | Select, expand and open the one role editor. No drag-to-reparent, staffing by drawing a line, or saved coordinates. | **CANVAS1d:** This machine’s All roles and scoped place diagrams. |
| Repository reference map | Workspace clusters; deterministic layers for directed relationships, separate undirected knowledge links, disconnected repositories grouped visibly. | Display only, including edges. No saved coordinates. | **CANVAS1f:** existing map, filters and detail on the common viewport. |
| Code map | Folder clusters from supplied module paths; dependency layers, explicit cycle groups and expandable neighbourhoods. | Display only. No position saving or editing generated facts. | **CANVAS1g:** module map, search, clusters and provenance. |

**Workflow layout stays**

Keep `layoutFlow`’s ranking and ordering, then introduce a measured arrangement stage that turns ranks, waypoints and node sizes into world-space boxes. Feed those boxes to the existing routing and label-placement rules.

Retain the traps D186’s measured comparison established:

- Skipping edges receive waypoint space.
- Returns have separate outer lanes, included in bounds.
- Reciprocal and differently styled relationships remain distinguishable.
- Each decision outcome starts at its own port.
- Labels and inserts clear nodes and each other.
- Optional data edges do not change the execution layout.
- Folding retains every boundary relationship.
- Status changes preserve positions; topology changes may relayout, preserving focus by ID.

These are implemented contracts worth carrying forward, not reasons to prohibit a viewport. `src/Daoris.Web/src/design/flowLayout.ts:115`, `src/Daoris.Web/src/design/flowInk.ts:60`, `src/Daoris.Web/src/design/flowInk.ts:251`

D186’s dagre comparison found collisions and poorer fit on its fixtures. That is evidence against replacing the tested workflow arrangement with that tested dagre configuration. It is not evidence against React Flow’s viewport, or proof that every other layout configuration fails. `docs/decisions/D186.md:150`

Keep endings exactly as `graphFlow` represents them: the configured end marks its own step; an unrouted outcome gets No further step; Finished requires the run record. `src/Daoris.Web/src/workflow/graphFlow.ts:272`

Keep the draft’s semantic operations unchanged. In particular, “earlier” remains graph ancestry, not screen height; Remove and join remains restricted to a plain chain. `src/Daoris.Web/src/workflow/edit/graphDraft.ts:316`, `src/Daoris.Web/src/workflow/edit/graphDraft.ts:515`

**Structure layout**

Use machine → workspace → repository containment as labelled grouping, visually distinct from relationship edges. Place role definitions once, at their recorded scope; keep agents in a shared staffing column. Workspace groups expand locally rather than unfolding every workspace together.

Preserve placed, staffed and appointed relationships, including choice order, unavailable readings and dormant appointments. Inheritance remains a highlighted resolved answer with provenance, never a new inferred reporting edge. The current diagram’s edge vocabulary and resolved work readings already support this distinction. `src/Daoris.Web/src/agents/roles/roleStructure.ts:156`, `src/Daoris.Web/src/agents/roles/roleStructure.ts:178`

Work is visible as the role’s labelled work facets and through a selected place’s work reading. Expanding that reading may show work rows and their supplied role relationships; it does not invent autonomous work entities or role-over-role hierarchy. Accounts, policy and workflows retain doors to their existing homes.

The canvas remains a view over the structure reading. Expanding a visual group must not silently widen a scoped read to every workspace. Scoped reading currently asks only the chosen place and its parents. `src/Daoris.Web/src/agents/roles/structureRead.ts:16`

This amends COMPANYVIEW1’s no-pan/no-zoom and column placement, while retaining its one read, one selection, one role editor and inheritance semantics. `docs/archive/2026-10-31-company-view-design.md:165`, `docs/2026-11-01-agents-place-design.md:57`

**Map layouts**

The repository reference map retains every current relationship kind: quests, asks, chains, declared dependencies and shared knowledge. Their directions, counts, time filters, outside counts and details survive. They must not become generic “references” that erase the distinction between a declaration and recorded work. `src/Daoris.Web/src/map/topology.ts:44`, `src/Daoris.Web/src/map/topology.ts:64`, `src/Daoris.Web/src/map/topology.ts:79`

Keep the existing deterministic layered layout initially. Small maps can retain a ring arrangement as a layout option inside the same canvas; crossing twelve nodes should no longer swap the entire renderer and lose the person’s camera. Preserve the percentage menu and readable opening scale already established for the map. `src/Daoris.Web/src/map/layers.ts:27`, `src/Daoris.Web/src/map/LayeredMap.tsx:254`

For the code map, use paths only to group **supplied modules**. Folder grouping is not evidence of file imports or runtime calls. Condense strongly connected components for overview ranking, label them as cycles, and expose their complete internal relationships on expansion. Preserve dependency kinds as authored.

Retain the code map’s source and fed-commit provenance. A view remembered from another revision must never make old coordinates imply current facts. `src/Daoris.Web/src/map/CodeMap.tsx:197`

`docs/index/` may later supply additional navigation through a separately designed adapter. CANVAS1 must not parse its Markdown into invented dependency edges or imply that it already supplies a file-level code graph.

**Hundreds, thousands and keeping place**

Use one renderer with bounded visible detail before considering a second GPU renderer.

| Scale | Reading |
|---|---|
| Up to roughly 100 visible nodes | Full node titles and meaningful marks; selected details; ordinary labels where clear. Minimap offered when the scene extends substantially beyond the viewport. |
| Hundreds of source nodes | Group overview, local expansion, viewport culling and selected-neighbourhood emphasis. Search covers every source item. |
| Thousands of source nodes | Cluster overview by default; expand a bounded region at a time. Aggregate relationships by direction and kind, with exact counts and member lists. Never mount thousands of rich cards merely because they exist. |

Start with a rendering budget of **250 rich nodes and 2,000 visible paths**. These are proposed budgets, to be measured. Above them, retain groups and aggregate edges, saying how much is represented. Selection and focus can pin required items; they cannot silently discard other facts.

Large layout jobs run in a worker, carry scene revisions and are cancellable. Measure rich content only for the bounded expanded scene; overview groups use predictable dimensions. Pan and zoom change the camera, not the graph projection or layout.

Search returns a proper result list. Choosing a result expands its ancestors, selects it, reveals it at readable scale and transfers focus. It does not depend on an offscreen node already being mounted. Provide a way back to the previous camera.

Remember view state locally per feature, scope and graph identity: presentation, expanded groups, selected ID, camera and anchor ID plus screen offset. Unsaved editor state remains session state. Do not serialize positions into workflow JSON, role records, manifests or generated maps.

On an unchanged graph, restore the camera exactly. On a changed graph, preserve the surviving anchor’s screen position; otherwise use its nearest surviving group and say the selected item is gone. A status refresh never refits. A language or text-size change remeasures while preserving the anchor. This improves on the current large map, whose size/node-count effect resets its view. `src/Daoris.Web/src/map/LayeredMap.tsx:138`

The thousand-node acceptance fixture is explicitly synthetic. Real thousand-module ingestion remains a separate source-contract change: its reader, producer, feed, bounds and compatibility must move together. The canvas migration leaves the current whole-file validation intact.

**Build or adopt**

Choose React Flow for the common viewport and graph interaction substrate, with controlled state and Daoris-owned HTML nodes, custom routes, controls and reading. Do not adopt its example appearance, default mutation logic or an assumed layout algorithm.

D223 grouped React Flow with stored positions. The relevant distinction is that coordinates are needed to render; persisting those coordinates is an application choice. Derived coordinates can remain entirely outside saved graph records.

| Candidate | What it would buy | Why it is or is not chosen |
|---|---|---|
| Extend today’s `FlowCanvas` alone | Maximum continuity with existing DOM and routing. | It still requires a new camera, gesture arbitration, selection geometry, culling and minimap. Maintaining those across every feature becomes substantial platform work. Keep its contracts and layout, not all its mechanics. |
| **React Flow / xyflow** | React node components, controlled viewport, handles, selection and minimap facilities. | Best fit for rich workflow controls and existing React stories. Preferred adoption; performance and accessibility integration must pass the common fixtures. Commonly MIT; selected package licence remains to be verified. |
| Cytoscape.js | Mature graph model, network interaction and layout ecosystem. | Strong map candidate, but making rich workflow nodes behave like existing React controls would introduce another rendering integration. Commonly MIT; no repository measurement establishes that trade-off as worthwhile. |
| sigma.js | GPU rendering suited to large network overviews. | Attractive if bounded DOM rendering still fails. Rich forms, measured HTML nodes and accessible editing would need a second surface. Commonly MIT; defer until a measured failure justifies it. |
| d3-zoom with Daoris rendering | A focused transform/gesture foundation. | Credible fallback with less graph machinery, but leaves selection, handles, minimap, culling and lifecycle integration to Daoris. Verify the exact package licence; no size claim is established here. |
| dagre | Layered layout. | It does not solve the viewport problem. The repository’s measured workflow comparison favours the current layout. Reconsider only for another feature on its own fixtures. |
| ELK / elkjs | Port-aware and compound automatic layouts. | Potential value for difficult grouped structure or maps. Worker and bundle cost, determinism and exact distribution licence need measurement. D186 records a historical approximately 467 KB gzip result, not a current measurement. |
| Complete whiteboard SDK | Extensive drawing/editor behaviour. | Too much unrelated document, tool and persistence machinery for governed graphs. Borrow interactions rather than introduce another document model; verify commercial and redistribution terms before any future adoption. |

The React Flow comparison must measure the **integrated result** against a minimal extension of the current renderer on the same editor fixture. It must not compare an elaborate Daoris chart with a library’s tiny demo.

Before locking the dependency:

1. Inspect the pinned package and transitive licences, required notices, optional commercial features and redistribution terms.
2. Measure production raw, gzip and Brotli additions, parse/init time and lazy loading. Proposed ceiling: **150 KiB gzip incremental canvas code**, with no graph engine loaded by CLI doctrine commands.
3. Verify React 19, custom routes and ports, IME, nested controls, pointer capture, wheel arbitration, focus restoration and token styling.
4. Run the geometry and performance matrix below in browser Chromium and the installed host.

If the adoption fails a blocking contract and the current-renderer extension passes it, return that evidence to D224’s settlement and choose the latter. Do not ship two engines chosen independently by features.

**Appearance and language**

Use the existing paper surfaces, ink, accent and status tokens. No library stylesheet may establish a parallel palette or type scale. The actual token source already contains independent light and dark values. `src/Daoris.Web/src/tokens.css:18`, `src/Daoris.Web/src/tokens.css:63`

Workflow edges retain neutral ink and structural styles; run state appears through words and node marks. Proposal changes use words and distinct marks as well as hue. Refusals remain whole at their action or item.

Every toolbar label, instruction, accessible announcement and empty state comes from the shipped catalogues. Authored titles, identifiers, dependency kinds and service sentences keep D222’s rules. Do not add bilingual fields to canvas data. `docs/decisions/D222.md:5`

Pointer targets remain usable at the selected scale, with at least the design language’s 28px target requirement for controls. Reduced motion disables camera animation, and idle graphs schedule no continuing animation. `docs/archive/2026-09-19-platform-ux.md:157`, `docs/archive/2026-09-19-platform-ux.md:735`

**Established patterns taken in**

These are product-pattern judgements from familiarity, not a fresh audit of those products.

| Familiar pattern | Take into Daoris | Refuse, and why |
|---|---|---|
| Figma | Predictable pan/zoom, focus selection, side inspector, explicit selection and undo. | Layers as an alternative configuration hierarchy, free positioning and shared drawing documents: Daoris’s records already own structure and execution. |
| Miro | Overview navigation, grouping and recoverable place. | An unbounded workshop board, sticky-note semantics and collaboration cursors: they obscure the governed graph’s meaning. |
| Excalidraw and tldraw | Space-pan, forgiving gestures, clear selected objects and accessible explicit tools. | Sketch style, arbitrary drawing tools and drawing-file persistence: these are operational records, not illustrations. |
| yEd and draw.io | Automatic directed layouts, labelled branches, grouping and fit. | Manual reroute points and diagram-owned semantics: a line’s execution meaning cannot depend on where it was drawn. |
| React Flow examples | Custom nodes, handles, minimap and controlled interaction. | Default visual styling, default connection acceptance and direct library mutations. |
| n8n and Node-RED | Searchable palette, insertion on a connection, labelled ports and inspector editing. | Free node arrangement, script/expression wires and assistant actions that deploy or run. Daoris’s palette and authority rules remain decisive. |
| Cytoscape and Gephi network views | Neighbourhood inspection, filtering, clustering, degree-aware detail and overview. | Continuous force motion, unlabeled hairballs and arbitrary status colours: they destabilize reading and disguise omitted detail. |
| Code graphs in editors and code hosts | Search-to-focus, breadcrumbs, source provenance, dependency direction and linked detail. | Inferring calls or dependencies from names, folder proximity or a layout. Only supplied facts become edges. |

Ask Daoris remains a proposer. The person takes a transaction into the draft and separately saves; opening, selecting, fitting or reviewing a proposal changes no configuration. D223 already specifies the dependency closure, stale-change refusal and one undo per take. `docs/2026-11-02-flowchart-design.md:123`, `docs/2026-11-02-flowchart-design.md:129`

**What becomes of today’s code**

| Existing part | Disposition |
|---|---|
| `design/Flow` | Keep the public convenience component and generic node. Migrate its internals through a compatibility adapter. Small record flows may remain inline rather than acquire a large viewport. |
| `design/FlowCanvas` | Replace its monolithic placement/measurement/keyboard shell with the shared canvas and reading primitives. Retain a compatibility entry point during migration. |
| `flowLayout` | Keep workflow topology, ordering, waypoint and return-lane decisions. Separate viewport-independent layout from old pane-fit selection. |
| `flowInk` | Keep routing, label and insert clearance rules, adapting measurement to world coordinates. |
| `workflow/chart/` | Keep feature components and semantic variants. Adapt their frame/measurement contract; move gesture capture into the common canvas. |
| `workflow/edit/EditCanvas` | Becomes the editor adapter over the shared canvas and its list. Empty-draft beginnings remain. |
| `graphDraft`, inspector, picker, save review | Keep their domain jobs and operation semantics. Canvas gestures call them. |
| Judgement and proposals | Keep their fingerprint, authority and transaction contracts. Add their marks as layers; do not claim unfinished FLOWCHART2 rows are already complete. |
| `RoleDiagram` | Keep wording and structure adaptation; delete its duplicated camera-independent navigation, measurement and SVG shell after migration. |
| Map renderers | Keep topology, filters, detail and provenance; replace separate interaction/rendering shells with canvas adapters. |

Multi-selection first enables selection reading and copying, which the draft helper already supports. It does not introduce unreviewed batch removal or configuration writes. `src/Daoris.Web/src/workflow/edit/graphDraft.ts:613`

**Measurements and proof**

All numerical targets here are proposed acceptance targets, not observed performance.

| Fixture | Proposed target |
|---|---|
| Shipped workflows, including the 24-step authoring bound | From ready props to stable usable chart within 150ms p95. Selection feedback within 50ms p95. |
| 100 nodes / 500 edges | Initial stable layout within 300ms p95; pan/zoom frame work within 16.7ms p95. |
| Current code-map ceiling: 500 modules / 5,000 dependencies | Usable grouped overview within 750ms p95; pan/zoom within 33ms p95; indexed search reveal within 100ms when no new layout is required. |
| Synthetic 10,000 nodes / 50,000 edges | Usable clustered overview within 1.5s p95; worker work does not block typing; bounded scene meets the same interaction targets. |
| Repeated navigation | After twenty open/close cycles, no monotonic retained-node, observer, listener or worker growth. |
| Idle | No self-sustaining animation frame loop or repeated geometry measurement. |

Measure separately: source normalization, grouping, layout, measurement, route generation, React commit, first usable frame and subsequent camera frames. Record graph shape, visible counts, hardware, viewport, text zoom, language, theme, cold/warm state and host build. Run representative sparse, dense, cyclic, disconnected and high-degree graphs; use repeated runs and report distributions.

Extend the existing measured harness rather than replace it with screenshots alone. It already asserts node/label collisions, port starts, crossing ceilings, stable drawing and insertion clearance. `src/Daoris.Web/measured/flow.spec.ts:160`, `src/Daoris.Web/measured/flow.spec.ts:198`

Add transformations at several scales and offsets, root zoom at 50/100/200%, nested page scrolling, drawer opening and resizing. Assert world-to-screen round trips, port alignment, hit targets and focus visibility. A test must fail if a path or handle is intentionally displaced.

Vitest owns pure geometry, grouping, selection, operation dispatch and stale-result handling. Playwright owns measured geometry and real pointer/keyboard interaction. Neither proves the installed host: Daoris embeds Chromium through `ChromiumView`, whose wheel, focus, DPI and accessibility behaviour must be checked on the shipped application. `src/Daoris.Desktop/Daoris.Desktop.App/MainForm.cs:11`

Every visible row has stories at **80, 39 and 25rem**, en and zh, both themes, using the existing helper; run views also retain their 300px sidebar proof. The helper defines these widths and actual page gutters. `src/Daoris.Web/src/design/storyWidths.tsx:13`

**Rows in build order**

Build as one coordinated canvas wave, with complete feature checkpoints. Do not combine this with unrelated workflow semantics or map-source expansion. Existing unfinished FLOWCHART2 rows retain their contracts and are delivered on the new substrate.

| Row | Contract | Proof |
|---|---|---|
| **CANVAS1a — compose on the canvas** | First vertical slice: compare engines, pin the choice, implement shared viewport/reading/measurement, and migrate `EditCanvas`. Keep inspector, operations, picker, history and save authority. Single/multiple selection, copy, valid connection intent, focus and complete rail. | Compose Merge into Release by pointer and keyboard. Every shipped graph round-trips existing operations and undo. Pan/zoom/select leave draft fingerprint unchanged. Port, hit-test, cancellation, root-zoom and narrow proofs; pinned licence and bundle report. |
| **CANVAS1b — read the workflow and its run** | Move the place chart and run onto the same layout and components. Preserve effective/bound graph identity, endings, controls, records, limits and doors. Add explicit follow-current option without automatic camera jumps. | Recorded outcomes only; no synthetic success or path. Status refresh changes no positions. Selection and camera survive rereads. Place widths and 300px run rail, including existing owner controls. |
| **CANVAS1c — judge and review proposals in place** | Carry FLOWCHART2c and 2f onto refusal and proposal layers; retain options/tails, staffing, save/use and proposal transaction contracts. Complete opener wiring under 2g. | Exact-draft judgement invalidates immediately; failed or older answers cannot restore it. Fresh, partially taken, stale, discarded and undone proposal states. Dependency closure atomic; one undo per take; Apply/open writes no workflow or choice. Measured union scenes. |
| **CANVAS1d — inspect the agents’ structure** | Migrate All roles and scoped diagrams; hierarchical place groups, shared roles and agents, work reading, inherited highlighting, search and existing editor doors. Delete duplicated `RoleDiagram` mechanics. | Same resolved facts and selection in diagram/list. Unknown, unavailable and dormant remain distinct. Expanding groups widens no read or grant. Hundred-node fixture, folded boundary counts, focus restoration and role-editor return. |
| **CANVAS1e — finish the shared flow migration** | Move remaining `Flow` consumers through the common scene, measurement and reading contracts, keeping compact inline placement where appropriate. Retire obsolete implementation after caller migration. | Existing quest, session, parts and landing fixtures preserve their recorded meaning, crossing ceilings, folds and rail controls. No independent viewport/keyboard implementation remains in migrated callers. |
| **CANVAS1f — navigate repository references** | Replace ring/layer renderer switching with one display-only canvas. Preserve topology kinds, filters, details, outside counts and browser/service boundaries. Add workspace clusters, minimap and remembered place. | Existing map behaviour plus twelve-to-thirteen-node transition without camera reset; reciprocal relationships independently selectable; counts conserved through clusters and filters. No mutation callbacks or machine-path disclosure. |
| **CANVAS1g — read the code map at scale** | Migrate modules/dependencies, folder and cycle groups, full-source search, bounded expansion and provenance. Keep current reader limits; exercise the common platform with synthetic thousand-node scenes. | Actual committed map and current 500/5,000 bounds; cycles, long authored IDs, fed/absent/refused states, exact aggregate counts, reveal across collapsed groups and performance table. No file-level edges invented from the orientation index. |
| **CANVAS1h — installed acceptance and retirement** | Run the integrated wave on the installed build; remove transitional engines and adapters that no caller needs; reconcile D186/D223’s superseded clauses. | Dated en/zh, light/dark evidence at 1546 and 680px; pointer, touchpad, keyboard and Narrator passes; DPI/root-zoom checks; editor/refusal/proposal/run/structure/map journeys. Relevant declared gates green, with remaining source-scale limits stated. |

The repository already declares web types, rules, unit, measured and end-to-end gates. Use those reached checks during the wave and the full integration set at its boundary; installed proof remains separate. `daoris.gates.json:13`

D224 should settle one common canvas and retain the records’ ownership: workflows define execution, structure readings define staffing, and maps display supplied relationships. The viewport makes those graphs navigable without becoming a second source of truth.