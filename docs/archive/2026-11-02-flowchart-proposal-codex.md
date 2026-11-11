# FLOWCHART2 — Codex's proposal

Status: sealed proposal, 2026-11-02, from `docs/archive/2026-11-02-flowchart-brief.md`; Codex, read-only, kept as it came.

*2026-11-02. Proposed amendment to D221, rows q–t.*

Build the workflow editor around a proper, automatically laid out flowchart: distinct step components, named connection ports, selectable wires, quick insertion, one inspector, and undo. Extend `design/Flow` behind an optional interaction contract. Keep positions derived, preserve the rail, and give Ask Daoris a draft-bound proposal door whose only result is a proposed change to the open draft.

Taking a proposal changes the draft. **Save** writes a workflow version. **Use here** chooses that saved version. Each remains a separate act.

This proposal rests on read-only source inspection. Tests, stories and recorded measurements below were read, not executed. Product comparisons are established patterns recalled from those products; their current versions, licences, sizes and accessibility have not been independently verified.

**The existing foundation is useful, but it is not yet this editor.**

| Verified here | Consequence |
|---|---|
| `Flow` exposes generic nodes and edges, sections and a rail, but no ports, selected wire or editing callbacks. `src/Daoris.Web/src/design/Flow.tsx:19`, `src/Daoris.Web/src/design/Flow.tsx:63`, `src/Daoris.Web/src/design/Flow.tsx:79` | Extend its rendering and interaction seams; keep workflow semantics outside it. |
| Layout reads graph shape, preserves declared-order ties and merges parallel edges with the same ends and style. `src/Daoris.Web/src/design/flowLayout.ts:110`, `src/Daoris.Web/src/design/flowLayout.ts:116`, `src/Daoris.Web/src/design/flowLayout.ts:193` | Editable routes need individual identities even when their ink is bundled. |
| `graphFlow` already draws sections, holds, the configured end and drawing-only *No further step*. It excludes data wires. `src/Daoris.Web/src/workflow/graphFlow.ts:181`, `src/Daoris.Web/src/workflow/graphFlow.ts:225`, `src/Daoris.Web/src/workflow/graphFlow.ts:232` | Retain these semantics; add typed ports and a separate data-wire presentation. |
| The editor draws the graph followed by every step’s form. Save is enabled independently of a matching successful judgement. `src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:252`, `src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:255`, `src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:324` | Replace the duplicated forms with one selected inspector and implement D221’s exact-draft save contract. |
| The bridge ignores answers older than the latest **requested** judgement. That does not itself invalidate the previous answer immediately when the draft changes during the debounce. `src/Daoris.Web/src/bridge/repositories/workflowEdit.ts:106`, `src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:142` | Match judgement to the current draft, not merely to request order. |
| `workflow_propose` accepts development workflows as steps; its graph path is for verification pipelines. The driver explicitly refuses development graphs. Its Apply path saves directly. `src/Daoris.Service/Daoris.Service.Mcp/HelpWorkflowTool.cs:14`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpWorkflowProposals.cs:109`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpWorkflowProposals.cs:329` | Draft composition requires a new, explicitly weaker door. It cannot reuse today’s Apply path. |

**One component system serves four views.**

The proposed presentation model retains the original graph and adds display information beside it. It never reconstructs a saved graph from rendered nodes.

Every step component takes a common `StepNodeProps`:

```text
identity: node id, kind, accessible title
content: title, about, actor, one summary fact
ports: incoming dependencies, outgoing routes, inputs, outputs
appearance: selected, focused, connection candidate, problems[]
run?: recorded state, current, reached, outcome, route not taken
diff?: added | removed | changed, before/after fields
callbacks: select, inspect, choose connection, quick-add
```

These are independent states. Selection uses the accent and focus ring; it does not borrow the current run step’s mark. A diff’s “Added” label does not mean that a run completed anything.

The frame is a token-styled HTML component. Wires are SVG behind it. Text, port labels and actions remain HTML, with the same semantic representation in chart and rail.

| Component and mapped kind | Text wireframe and specific props |
|---|---|
| `WorkNode` — `claimed` | `[Work] The work / Taken by Builder / ○ Once reached`. Takes the resolved role summary and its source. |
| `OpinionNode` — `opinion` | `[Second opinion] / Taken by Reader / ○ Once reached`. Its available routes come from the palette, not from resemblance to a review. |
| `ReviewNode` — `review` | `[Your review] / You / ○ Accepted / ○ Changes requested`. Shows the configured changes route, including its absence. |
| `LookNode` — `look` | `[Your look] / You / ○ Reviewed / ○ Not yet`. Remains distinct from acceptance before landing. |
| `DecisionNode` — a plugin step declaring outcomes | `[Plugin mark] Find a pending release / ◇ Decision / ○ release / ○ none`. Takes every declared outcome and `outputsOn`. This adds no built-in decision kind. |
| `PluginStepNode` — other plugin kinds | `[Plugin mark] Record the release / Local release / ○ Once reached`. Takes authored title/about, plugin identity, version and declared ports. |
| `LandingNode` — `land` | `[Landing] Merge into the line / target: main / ○ Once reached / ○ Failure`. Takes the selected method and tail summary. |
| `ObservedLandingNode` — `handed` | `[Landing recorded] / Observed from the landing record`. Does not make an older observed node look performed. |
| `LineNode` — `line` | `[On the line] / Git’s recorded proof / ○ Once reached`. May wear the separate configured-end marker. |
| `CheckNode` — `check` | `[Checks] / declared gates / ○ Once reached / ○ Failure`. |
| `HumanTaskNode`, `GoAheadNode` — `human`, `go-ahead` | `[Task for you]` or `[Your go-ahead] / authored request / declared routes`. |
| `RequestNode`, `WaitNode` — `request`, `wait.unit` | `[Request other work] / destination, until…` or `[Wait for work] / agreed revision`. An external dependency is a labelled reference, never a wire into another repository’s chart. |
| `WorkRequestsNode`, `QuestHoldsNode`, `ContentHoldsNode` | Individual nodes for `work.requests`, `quest.holds`, `content.holds`, with their own summaries and options. |
| `HoldsGroupNode` — presentation only | `[Daoris makes sure · 3] [Expand] / member names`. Takes member ids, aggregate recorded state and problem count. It is not removable as one authored step. |
| `JoinNode` — `all` | `[Join] / incoming groups: all / any`. Takes the actual group expressions; converging lines alone do not imply a join policy. |
| `QuestCloseNode` — `quest.close` | `[Close the quest] / Daoris`. A performed step, with its configured-end marker where applicable. |
| `StopNode` — an actual `stop` declaration | `[Run stops here]`. Never inserted merely because a failure route is absent. |
| `RunEndingNode` — record only | `(( Finished · recorded result ))`. Created only from a run’s recorded finish. |
| `NoFurtherNode` — presentation only | `⊣ No further step`. Takes its source outcome; in the editor it offers quick-add. It is neither success nor a saved step. |
| `UnknownStepNode` / diagnostic reference | `[kind as recorded] / unavailable here`, or `[Missing target]`. Preserves the underlying declaration and exposes the driver’s problem without pretending it is *No further step*. |

The palette contains more than work, review and plugins: all built-ins must retain a component and an accessible name. Its actual table includes the observed landing, requests, joins, stops and holds. `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeContract.cs:106`

A decision’s diamond is an outline glyph beside its title, with its branches below. Rotating the entire text container into a large diamond would spend space on long authored names and Chinese wrapping. The chart still uses the familiar gateway vocabulary without sacrificing readable text.

`ConfiguredEndMarker` is attached to the named step:

```text
On the line
Work done here
Steps after it still run
```

It is not a second ending node. The shipped release graph ends the work at `line` while release steps follow it. `src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/release.json:15`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/release.json:53`

Only `claimed` and `opinion` offer *Taken by*. The palette’s role contract already makes that distinction. `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/WorkflowPalette.cs:35`

**Ports and wires carry their meaning in words.**

A `RoutePort` takes `{source, kind, outcome?, on?, targets[], available, problem}`. An `InputPort` or `OutputPort` additionally takes its name, schema and outcome availability. Port names are stable identities internally; people choose readable titles.

| Edge component | Drawing and selection |
|---|---|
| `AfterEdge` | Solid forward arrow. Source label states the actual cause: *Accepted*, *Once reached*, or the caller’s recorded words. |
| `WhenEdge` | Conditional arrow from a named outcome port. The outcome stays visible at the port even when no edge label fits. |
| `BackEdge` | Return on its own outer lane, labelled *Changes requested → The work*, or the relevant failure/outcome. |
| `DataEdge` | Separate fine dotted path, square port marks, label such as *release → release*. It does not masquerade as a route. |
| `FailedEdge`, `RejectedEdge` | Distinct exception paths with their words. |
| `TriggerEdge`, `WaitEdge` | Explicitly named advanced connections. A wait source is a dependency reference rather than a local step. |

Each takes `{wireId, endpoints, label, selected, problems, diff?, runEvidence?}`. Each has a generous invisible hit path and an equivalent row in the inspector. Selecting a bundled path opens a list of its constituent wires; deleting it never deletes every bundled wire implicitly.

The eight wire kinds and their eligibility semantics already exist. In particular, data is mandatory input, and `back` makes no node eligible. The editor must not flatten them into interchangeable arrows. `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeWires.cs:9`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeWires.cs:36`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeWires.cs:50`

Every step shows all its declared outcomes and all configured ways on. Multiple targets under one outcome remain individually visible. Data ports are a separate band; selecting a step opens their complete *Carries* table. A target with grouped prerequisites says *All required* or *Any of these* according to its stored groups. No automatic regrouping accompanies a new connection.

**The surrounding components have narrow responsibilities.**

| Component | Props and visible states |
|---|---|
| `WorkflowDiagram` | Graph presentation, mode `place/editor/run/proposal`, selection, expanded groups, callbacks. Reading, empty, unread, ready. |
| `WorkflowCanvas` | Nodes, wires, layout, selected identity, keyboard navigation. Chart or rail; connection idle/choosing/dragging. No bridge imports. |
| `StepPicker` | Search, palette offers, insertion anchor, compatible choices, callbacks. Empty search, results, no result, missing plugin door. |
| `StepInspector` | Selected step, palette fields, outgoing routes, incoming groups, carries, roles and problems. Exactly one open inspector. |
| `WireInspector` | Selected wire, editable ends/condition/ports, problem and removal action. |
| `ProblemList` | Matching judgement and ordered problems. Reading, current, unavailable; each problem is a focus door. |
| `ProposalReview` | Immutable before/after graph, structured changes, taken changes, dependencies, judgement, stale reason. |
| `SaveReview` | Exact draft identity, changed steps/wires, involvement, outward acts, append scopes and consent. Pending, refused, ready, saving, saved. |
| Editor controller | Owns draft history, selection, proposal lineage and bridge hooks. Passes answers and actions to the molecules. |

UI state hooks remain permissible in molecules; service and bridge hooks belong to their holder. This follows the existing props-driven editor seam and the repository’s presentational boundary. `src/Daoris.Web/src/workflow/edit/WorkflowEditor.tsx:20`, `src/Daoris.Web/src/design/presentational.test.ts:36`

**The canvas is a document that lays itself out.**

Keep automatic top-to-bottom layout, the 14rem minimum track, normal scrolling and the rail. Save no positions. Changing run state, selection or a diff badge must not reorder the chart.

There is no independent pan, canvas zoom or minimap in the first implementation. The chart fits its region through measured tracks and uses the rail when a rank cannot fit. *Find step*, *Show selected step*, group expansion and *Show whole* provide navigation. The existing application zoom continues to enlarge readable content; the editor introduces no competing Ctrl+wheel behavior.

This is a deliberate choice for workflows bounded at 24 authored nodes and 96 authored wires, while measuring larger run and proposal presentations too. `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Domain/WorkflowNamed.cs:53`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeGraphs.cs:190`

The existing layout already separates shape from lit state, and the current view measures boxes before routing ink. Preserve that separation when adding ports. `src/Daoris.Web/src/design/Flow.tsx:216`, `src/Daoris.Web/src/design/Flow.tsx:324`

```text
1546 px window — Ask closed; editor region wide enough for two columns

Place › Workflow › Edit steps                    Undo  Redo  Ask Daoris
┌─ Steps ───────────────────────────────┬─ Selected step ────────────────┐
│ Before it lands                      │ Find a pending release        │
│ [The work · Builder]                 │ Local release                 │
│ [Daoris makes sure · 3 ▸]            │                               │
│ [Checks]                            │ What happens next             │
│ [Your review] ── changes ──↩ Work    │ release → Record the release   │
│ [Landing · merge]                    │ none    → No further step     │
│ After it lands                       │ Failure → The run stops       │
│ [On the line] · Work done here        │                               │
│             [+]                      │ Carries                       │
│ [◇ Find a pending release]           │ release → Record.release      │
│    release ○          none ○         │                               │
│       ↓                  ⊣           │ [Explain this step]           │
│ [Record release]  No further step    │ [Remove…]                     │
│ [Copy to registry]                   │                               │
└──────────────────────────────────────┴───────────────────────────────┘
Name [Release workflow                     ]          [Review and save]
```

Opening Ask uses the existing frame’s Ask Daoris surface. It reduces the editor’s available width. When the remaining editor region cannot hold both chart and inspector, the inspector becomes a drawer—even in a 1546 px window.

```text
1546 px window — Ask open

┌─ Workflow chart ──────────────────────────────┬─ Ask Daoris ───────────┐
│ Same nodes, ports and branches                │ Editing this draft    │
│ Inspector opens as a drawer where needed      │                       │
│                                              │ “Add a release after  │
│ + Added step     ~ Changed wire               │  the line”            │
│ - Removed wire                               │                       │
│                                              │ Proposed: 3 changes   │
│ [Changes · 3]                                │ [Show on chart]       │
└──────────────────────────────────────────────┴───────────────────────┘
                         [Take all into draft] [Discard]
```

```text
680 px window — chart owns the main area

Edit steps                       Undo  More…  [Ask Daoris]
Before it lands
[The work]
[Daoris makes sure · 3 ▸]
[Checks]
[Your review] ↩ The work
[Landing]
After it lands
[On the line] · Work done here
[◇ Find a pending release]
 release ○                 none ○
    ↓                         ⊣
[Record release]       No further step
[Copy to registry]

Name [                         ] [Review and save]
```

A selected step opens the existing drawer pattern. Ask occupies the frame’s panel or selected Ask surface; the inspector and Ask do not stack competing modal drawers. Returning to the chart restores selection and scroll.

```text
25rem main area — rail, with the same editing actions

Edit steps                         Undo  [Ask Daoris]
1  The work · Builder
   Once reached → 2
2  Daoris makes sure · 3 [Expand]
…
6  Find a pending release [Inspect]
   release → 7 Record the release [+]
   none    → No further step      [+]
7  Record the release
   Once reached → 8
   Carries release → Copy.release
8  Copy to registry
[Add a step]
[Review and save]
```

The transition depends on the actual region width, including open docks and application zoom. A two-branch release chart already has repository tests for chart placement at 80/39rem and rail placement at 25rem. Those are the starting fixtures, not proof of the proposed editor. `src/Daoris.Web/src/workflow/graphFlow.test.ts:80`

**Editing is direct, with a complete chooser path beside every pointer gesture.**

| Interaction | Pointer behavior | Keyboard path |
|---|---|---|
| Select and inspect | Click a step; double-click is unnecessary. | Tab enters the chart at its remembered step. Up/Down follow reading order; Home/End reach its ends. Enter opens the inspector; Escape returns to the step. |
| Follow a branch | Press its visible target or outcome row. | Right opens the selected step’s route choices when several exist; choose with arrows and Enter. Left returns along the remembered incoming route. |
| Add between steps | Press `+` on a selected forward wire; search the picker. | Select the source, open its route row, choose *Insert a step…*, search, Enter. |
| Add at an outcome | Press `+` beside that outcome, including *No further step*. | Inspector → outcome → *A new step…* → picker. |
| Add from palette | *Add a step* opens grouped search and asks where to attach it. *Add unconnected* is an explicit advanced choice. | Tab to Add; search; choose an attachment or the explicit unconnected option. |
| Connect | Drag a named route handle to a compatible target, or press it and choose a target. | Inspector → route → target chooser → Enter. Touch uses the same chooser. |
| Reconnect | Drag a wire end, or change its target in the inspector. | Select the wire’s row → *Change target*. |
| Connect data | Choose or drag an output to a compatible input. | *Carries* → input → source/output chooser. |
| Move a step in the workflow | Change its connections; the layout follows. A simple-chain *Move…* action previews every rewire before applying. | Inspector → *Move…* → destination slot → review rewires → Enter. |
| Remove | Inline confirmation names the step and incident wires. Nothing bridges the gap. | Inspector → *Remove…* → confirmation. Delete on a selected step opens that confirmation. |
| Remove a wire | Remove the selected wire alone; show the resulting unrouted outcome or missing input. | Wire inspector → Remove; undo remains available. |
| Undo/redo | Toolbar actions. One gesture is one history transaction. | Ctrl+Z / Ctrl+Shift+Z while chart editing has focus; text fields retain native text undo. |
| Copy/paste | Copy selected steps and their internal wires into the editor’s local clipboard; paste opens attachment review. | Chart Copy/Paste actions and Ctrl+C/V outside fields. New identities are allocated; external connections are listed as omitted. |
| Explain | *Explain this step* or *Explain this refusal*. | The same labelled action in the inspector or problem row. |
| Review proposal | Choose a change, see it on the chart, take it or take all. | Changes list → arrows → Enter to reveal; Tab reaches *Take this change* and *Discard*. |
| Save and choose | Review → Save; afterwards a separate Use review. | Ordinary labelled buttons; neither action requires a canvas shortcut. |

Insertion is an explicit graph transaction. On an `after` edge A→B, it previews A→new→B. On a decision route, it retains the outcome on A→new; the new step’s onward route is selected according to its kind. Data wires, grouped prerequisites and exception routes are never transferred by guesswork. If the selected step cannot supply a single onward route, the picker asks.

Moving is distinct from removing. The simple-chain convenience may reconnect the old gap only because its preview explicitly includes that rewire. Complex moves use the connection controls.

Each history entry retains the full lossless draft, relevant selection and proposal acceptance state. Undoing a proposal take reverses that take. Undoing local edits never reverses a saved workflow version or a separately saved role.

`graphDraft` already preserves graph, node and wire fields it does not edit, allocates identities and removes incident wires without bridging. Keep that model and extend it with transactional helpers. `src/Daoris.Web/src/workflow/edit/graphDraft.ts:71`, `src/Daoris.Web/src/workflow/edit/graphDraft.ts:104`, `src/Daoris.Web/src/workflow/edit/graphDraft.ts:162`

No person types an id. Preserve D221’s generated workflow-id fallback for names without ASCII letters or digits. Existing step and wire identities survive edits and reconnects; newly copied elements receive new identities. Identity is not a display label.

**The inspector remains the exact account of the selected step.**

Its order is: title/about and actor; *Taken by* where applicable; options; *What happens next*; *Carries*; incoming prerequisites; advanced details and removal.

The outcome table always includes every declared outcome, configured exception route and default behavior the driver can state. “No route configured” must not invent an unrecorded runtime result.

Changing a landing method previews the whole required tail. Merge and branch bring no tail; push brings `pushed`; pull request brings `pushed` and `pull-request`; integrate brings `integrate`. These are existing graph rules. `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeGraphs.cs:155`

A tail change and its rewires form one undoable transaction. An edited tail with independent connections requires an explicit choice about those connections.

Input suggestions prefer the routed predecessor’s compatible output and respect `outputsOn`. Ambiguity asks. The release fixture requires Find→Record and Record→Copy data wires; Find’s release output exists only on its `release` outcome. `src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/release.json:27`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/release.json:55`

A driver problem highlights the exact named node, wire or port row. A problem inside the holds group expands it. A problem about a missing endpoint exposes a diagnostic reference. General problems remain above the chart. Selecting a problem scrolls to and focuses its inspector row; the driver’s full sentence remains available.

The judgement is invalidated synchronously on every relevant draft change. Until a matching answer arrives, the editor says *Checking these changes…* and offers no Save. A failed judgement request says the draft could not be checked; the last successful answer cannot stand in for it.

**Accessibility is part of the chart model.**

Retain `Flow`’s roving focus and focus restoration by identity, but extend its current first-outgoing-edge navigation into an explicit choice where routes branch. The existing behavior and descriptive edge text provide the starting implementation. `src/Daoris.Web/src/design/Flow.tsx:246`, `src/Daoris.Web/src/design/Flow.tsx:282`, `src/Daoris.Web/src/design/Flow.tsx:652`

The semantic DOM exposes a labelled list of steps, each with actor, state, incoming requirements and every outgoing route. SVG is decorative to screen readers. Ports and wire actions have names such as “Connect release from Find a pending release.” A live region announces completed edits and current validation summaries without announcing every pointer movement.

Dragging has a choose-source/choose-target alternative. No editing action depends on hover, color, spatial precision or a context menu. IME composition suspends chart shortcuts. Drawer focus returns to its originating step.

Use existing surface, ink, accent and status tokens in both themes. Structural wires use ink; diff additions/removals have both words and marks; recorded run states retain their own pills. Authored titles/about/outcomes stay as written, while Daoris labels translate through catalogues. `src/Daoris.Web/src/tokens.css:18`, `src/Daoris.Web/src/tokens.css:65`, `src/Daoris.Web/src/locales/authored.ts:20`

**Extend Flow; keep adoption as a measured alternative.**

| Choice | Benefit | Cost and uncertainty |
|---|---|---|
| Extend `Flow` | Preserves the measured routing, narrow rail, focus behavior, tokens and existing readers. No new runtime dependency. | Daoris owns port geometry, connection gestures, hit testing and editable-wire behavior. These need real-browser proof. |
| Adopt React Flow | A mature vocabulary for custom nodes, handles, connection gestures, selection and viewport interaction. Attractive if the editor grows into a spatial canvas. | It supplies neither Daoris’s graph semantics nor its rail. Existing rendering, focus and measured-test integration would need adapters. Accessibility still needs product-level verification. |
| Add dagre | Familiar layered automatic layout; potentially small compared with a canvas library. | Coordinates and routing must be evaluated together. The repository’s earlier comparison found regressions on several fixtures. |
| Add ELK | Richer port, orthogonal-routing and compound-graph facilities. | Larger integration and layout lifecycle; licence notices, worker packaging and benefit must be checked for the exact package. |

My understanding is that React Flow’s core is MIT-licensed, with separately offered Pro material, dagre is MIT, and ELK carries Eclipse licensing obligations. **Those current package terms are not verified by this review.** Before adoption, inspect the pinned distribution and all transitive notices. Do not infer the core library’s licence from examples or a paid offering.

There is relevant repository evidence: D186 records a whole-layout dagre comparison and retained the hand layout because of collisions and width behavior, despite fewer crossings on one fixture. That is historical evidence for those versions and fixtures, not a benchmark of React Flow or today’s ELK. `docs/decisions/D186.md:150`

The web package currently declares none of these graph dependencies. `src/Daoris.Web/package.json:22`

Before locking D223’s renderer, measure a small connection-editing spike against an equivalent React Flow implementation:

- The shipped release graph, multiple routes to one target, several returns, data-only dependencies, long multilingual labels, invalid endpoints and a proposal’s before/after union.
- The maximum authored graph, plus the existing larger generic-flow fixtures.
- At 80, 39 and 25rem, both languages and themes: node/port collisions, label collisions, crossings, focus retention and rail equivalence.
- Incremental production JavaScript/CSS, compressed transfer size, parse/start time, layout time and connection responsiveness on the actual installed machine.
- Pointer capture outside the chart, touchpad scrolling, drag cancellation, IME, focus, screen-reader navigation and application zoom in the installed Chromium host.
- Whether the implementation remains props-driven and testable without mocking layout into an assumed success.

The app actually registers a Chromium engine; browser compatibility alone is insufficient installed-window proof. `src/Daoris.Desktop/Daoris.Desktop.App/Program.cs:139`

Retain the extension if it meets these requirements without constructing an independent viewport framework. Reopen adoption if connection interaction or required graph sizes demand that framework. Do not switch layout engines merely to obtain handles.

The current measured suite checks browser geometry, crossings and redraw stability. Extend that check rather than replacing it with snapshots. `src/Daoris.Web/measured/flow.spec.ts:125`

**Ask Daoris composes against a named snapshot of the open draft.**

From the editor, *Ask Daoris* opens the existing conversation with an “Editing this workflow draft” attachment. From a place’s Workflow, *Ask Daoris to change this…* first opens an unsaved editor copy of the selected route’s effective graph, then attaches it. Current is copied from the effective graph, never guessed from visible cards.

The attachment tells the room:

```text
Editor identity and revision
Place and selected route
Source: Current digest, preset, or saved workflow/version/digest
Complete lossless graph
Selected step or wire, when relevant
Palette snapshot/version and applicable role summaries
Current judgement and its problems
Intent: propose draft changes; Save and Use belong to the person
```

The attachment is frozen for that request. Typing in the editor does not continuously rewrite what an in-flight turn was told. It changes the current draft identity and makes the old proposal stale.

Today `HelpWhere` carries the view and selected item, not this draft context. Extend it with a reference to the attachment, not the entire graph embedded in its prose preface. `src/Daoris.Web/src/help/where.ts:16`, `src/Daoris.Web/src/help/where.ts:112`

The desktop owns the draft context. Its machine-local storage, tokens and room attachment do not become shared-host state. The service’s proposal writer continues to validate and write proposal shape; the desktop judges graph meaning.

**Extend `workflow_propose` with a distinct `draft` door.** Keep its existing doors explicitly separate.

| Proposed seam | Contract and authority |
|---|---|
| `WORKFLOW_DRAFT_CONTEXT` | Window-only registration/update/close of an editor snapshot. Returns an opaque token, revision and fingerprint. Changes ephemeral editor context; saves no workflow and changes no choice. Bound to the editor and help conversation. |
| `workflow_propose(..., draft: token, graph: wholeGraph)` | With a draft token, proposes a schema-2 development or verification graph according to that context. Infers door `draft`. Rejects `choose`, route-only use, steps, imports and saved-base substitutions in this mode. |
| Draft proposal judgement | Uses the editor’s graph/save planning rules without executing the save. Returns structured changes, involvement, outward acts and problems. It does not mark a development graph as a verification pipeline because it arrived as `graph`. |
| `WORKFLOW_DRAFT_REVIEW` | Read-only review of a proposal and selected change groups against the registered context. Returns the exact candidate graph, its fingerprint, problems and required dependent changes. |
| *Take into draft* | Local editor compare-and-set of that reviewed candidate. One undo transaction. No `WORKFLOW_SAVE`, `WORKFLOW_USE` or legacy `HELP_APPLY`. |
| Legacy `HELP_APPLY` | Explicitly refuses door `draft`. Existing saved-version and use proposals retain their own authority and checks. |
| Save / Use | Continue through their existing routes, with the exact draft, append authorization and reviewed choice. |

An older driver that does not advertise draft composition gets a clear unavailable state. The page must not silently fall back from draft composition to a legacy proposal that saves.

The new mode changes the tool description, writer, driver reader/judge, card shape and coverage together. Its draft token is checked against the proposing conversation’s scope; possession of a token does not grant another conversation access. The existing service writer’s shape-only responsibility is explicit. `src/Daoris.Service/Daoris.Service.Core/Help/HelpWorkflowProposalWriter.cs:28`

**The chart diff is structured and computed, not narrated into existence.**

Keep the assistant’s whole candidate graph. The desktop derives changes against the frozen draft:

```text
entity: node | wire | graph field
identity
added | removed | changed
before / after
changed fields
dependencies on other changes
```

Compare every wire field, including endpoints, kind, outcome, `on`, ports and group. The existing graph-card helper lists wire additions/removals by id; it does not report a changed wire retaining the same id. It is insufficient for this chart. `src/Daoris.Desktop/Daoris.Desktop.Driver/Help/Proposals/HelpWorkflowProposals.cs:543`

Retain `WorkflowDiff` for schema-1 workflows. Its current input is `NamedStep`, not a schema-2 graph diff. Add a graph-specific structural comparator with the same review priorities. `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Domain/WorkflowDiff.cs:47`

In proposal mode, added elements have `+ Added`, removed elements remain visible as `− Removed` ghosts, and changed elements have `~ Changed` with before/after details. Offer *Before*, *Proposed* and *Changes*. A union drawing is labelled as a comparison; it is never passed off as an executable graph containing both old and new wires.

Layout stays fixed while reviewing one proposal. Selecting a change reveals its affected elements. At narrow widths, the same changes appear on the rail.

**Partial acceptance works by dependent change groups.**

“Add release after the line” may comprise new steps, route wires and two data wires. The person may take independent groups individually. A wire addition includes any newly required endpoints; removing a step includes its incident-wire removals; changing a landing method includes its tail; replacing the configured end includes the new end reference.

The interface names included dependencies before taking them. It never quietly takes an unrelated change.

Let the immutable proposal base be B and the accepted change set be S. The editor’s proposal-review head is the deterministic result of applying S to B. Each further take verifies that the current draft still equals that head, reconstructs the next candidate and applies it atomically. Thus taking one change does not stale the remainder of the same proposal.

A manual edit, another proposal’s take, a changed source, a closed editor or a mismatching head makes the proposal stale. It remains readable, with *Ask again using this draft*. There is no automatic rebase. Undo restores the corresponding accepted set along with the graph.

The fingerprint covers the lossless graph, intended save target/source, place and route; object keys are canonicalized while meaningful array order is retained. Editor identity and monotonically increasing revision prevent accidental reuse after closing/reopening or an edit-and-undo sequence. Locale, focus, layout and selection are excluded.

A take can leave a draft needing repair, as a manual edit can. Such problems are shown immediately and prevent Save. It cannot create hidden dependent operations or bypass the driver’s judgement.

**Explanation uses the same evidence the person can inspect.**

*Explain this step* attaches its declaration, palette authority, options, routes and current context. *Explain this refusal* attaches the exact problem code, words, affected identities and draft revision. Deterministic facts appear immediately; Ask Daoris supplies an explanation and may propose a repair through the draft door.

The assistant’s explanation is labelled as its answer. Driver judgement remains visibly distinct. Without an available helper, inspection, manual editing, structural diff and driver validation still work. The answer identifies the agent/tier that actually ran rather than promising a particular model.

**Saving remains D221’s review, with stronger draft identity.**

`Review and save` uses the complete current save request and a matching judgement. It shows changed nodes and wires, involvement, outward acts and every scope an append reaches. Consent is bound to the exact returned scope keys and resets when they change.

Save submits that same graph and base; the driver rejudges them. During the write, the submitted snapshot is fixed. A stale base keeps the draft and offers comparison/copy paths; it never replaces the draft with a newer saved version automatically.

The existing save implementation already refuses a moved base and requires exact append scope authorization. Preserve those checks. `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/WorkflowOperations.cs:728`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/WorkflowOperations.cs:740`

After Save, *Use here* appears only while the draft matches that saved result. It reads the announced choice plan and submits its expected token. A Use refusal leaves the saved version intact. `src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:428`

Staffing continues through D221’s single role editor. It says that creating a place role changes staffing for that work across the place. **Save role** performs that separate save; choosing the role in the workflow remains a draft edit. Closing or undoing the workflow does not undo the role save. D221 explicitly distinguishes those two saves. `docs/2026-11-01-setup-ux-design.md:462`, `docs/2026-11-01-setup-ux-design.md:473`

**The familiar flow-editor vocabulary is taken selectively.**

These are design references, not claims that current releases all implement identical behavior.

| Established pattern | Adopted here, and why |
|---|---|
| n8n, Node-RED, Zapier, Make, Logic Apps and Power Automate: palette/search, insertion `+`, named branches and configuration beside the flow | Makes composition discoverable at the point where work continues. |
| React Flow examples, Blueprints and ComfyUI: handles, wire selection and typed data ports | Makes connections tangible; chooser parity keeps precision optional. Execution routes and data remain visually distinct. |
| BPMN tools such as bpmn.io/Camunda Modeler: gateways and labelled alternatives | Gives decisions recognizable structure. No BPMN import, execution semantics or separate invented gateway step. |
| GitHub Actions’ run graph: familiar structure with recorded states on it | Reuses the workflow’s components for reading a run. A status update does not move nodes. |
| Figma/Miro: selection, inspector, undo, local copy/paste and focus on the selected object | Supports familiar editing without requiring a spatial document model. |
| Workflow copilots: proposed changes reviewed on the canvas | The person sees exactly which steps and wires change before taking them. |

Reject free placement, saved coordinates, arbitrary reroute points, infinite panning and a minimap for this scope. Reject a redundant *Tidy* button while every topology change is already laid out automatically. Reject hidden hover-only branches, expression/script wires, unrestricted connection handles and assistant execution. These would either obscure graph meaning or create capabilities absent from the driver.

Do not paint an imagined run traversal. Today the run view distinguishes an outcome and route not taken, but its chart documentation explicitly says the record does not retain the order traversed. Preserve that limit. `src/Daoris.Web/src/workflow/WorkflowRunView.tsx:65`, `src/Daoris.Web/src/workflow/workflowFlow.ts:78`

**D221’s replacement rows and the implementation sequence are concrete.**

Every visible row requires props-driven stories at 80, 39 and 25rem, en/zh, light/dark, plus keyboard proof. Geometry requires the measured browser suite. Installed behavior requires the installed window at 1546 and 680 px; a mocked bridge or story does not substitute.

| Build row | Contract | Proof |
|---|---|---|
| **FLOWCHART2a — renderer decision** | Run the bounded Flow-extension/React-Flow comparison above. Retain the measured engine unless the evidence justifies an amendment. | Recorded geometry, bundle, interaction and installed-host measurements; pinned licence review if adopting. No dependency selected by an unmeasured size claim. |
| **2b — one component drawing** | Introduce typed nodes, ports and wire identity on the shared chart/rail. Preserve holds and truthful endings. Keep non-workflow Flow callers compatible. | Every palette kind; all eight wire kinds; release fixture; same-target outcomes; data ports; missing endpoints; existing generic-flow regressions and measured ceilings. |
| **2c — editor page and transactions; replaces 1q** | One chart, one inspector, picker, insertion, connect/reconnect, move preview, confirmed removal, undo/redo and focus doors. | Compose by pointer and entirely by keyboard; cancelled drags; removal never bridges; undo restores unknown fields; measured 680 px drawer and 25rem rail. |
| **2d — options, tails and carries; replaces 1r** | Worded options, authored plugin metadata, method-tail transaction, routed-predecessor input suggestions and explicit ambiguity. | Compose the shipped release graph from Merge through the inspector; both data wires; output unavailable on `none`; every landing tail; unknown-field round trip. Extend the existing composition regression at `src/Daoris.Web/src/workflow/edit/WorkflowEditor.test.tsx:138`. |
| **2e — save and choose; replaces 1s** | Exact-draft judgement, name-only creation, scope-bound consent, Save and Use separately reviewed. | Edit during debounce; out-of-order and failed judgements; changed scopes; stale base; dirty draft has no Use; Save succeeds while Use refuses; older running version stays bound. |
| **2f — staffing; replaces 1t** | One role editor, scope/revision preserved, consequence shown, separate role and workflow saves. | Create/edit/return/select; refused role save retains forms; workflow discard keeps an explicitly saved role; driver role-binding regression; authored names in one language and arbitrary tags. |
| **2g — Ask composes a whole draft** | Desktop context registration, frozen attachment, `workflow_propose` draft door, structured graph diff and take-all. Legacy Apply refuses draft proposals. | New draft and saved-base edit; development graph stays development; wrong conversation, stale token, source movement and editor closure; taking invokes no save/use; unavailable helper; installed request→diff→take→Save flow. |
| **2h — take change by change** | Dependency groups, exact review head, acceptance history and explicit stale refusal. | Endpoint dependencies; tail bundle; end replacement; same-id wire modification; first take followed by second; manual edit stales remainder; undo/redo; competing proposals. |
| **2i — place/run integration and cutover** | Place chart, editor, run and proposal use the same components and semantic model. Retain each surface’s own authority. | Both release outcomes, changes sent back, route not taken versus not reached/skipped/failed, configured end before trailing steps, record-only finish, unchanged geometry on state update; installed bilingual/theme evidence. |

These rows replace D221 q–t’s implementation plan, not its independent settlements. D221’s effective-graph read, place shell, reviewed choice and role-editor prerequisites remain dependencies. Its run row adopts these components rather than introducing another drawing.

The file disposition is:

| Existing file | Disposition |
|---|---|
| `design/Flow.tsx` | Keep the generic primitive. Extract node/edge rendering parts and add optional rendering, port, selection and interaction props. It imports no workflow domain. |
| `design/flowLayout.ts` | Keep the measured engine. Add port-aware routing support and editable parallel-wire identity without changing unrelated readers by default. |
| `workflow/graphFlow.ts` | Keep as the workflow presentation adapter. Retain kinds, all wire semantics, data endpoints, synthetic identities, sections, holds and end markers. Stop reducing the editable model to generic cards too early. |
| `workflow/workflowFlow.ts` | Keep compatibility conversion for Current/schema-1 and run answers; delegate the common presentation. It must not invent missing graph facts. |
| `workflow/WorkflowChart.tsx` | Keep settings, source, limit and run-content adapters. Move node chrome into the shared components; remove the duplicated rail rendering after parity is proved. |
| `workflow/WorkflowRunView.tsx` | Keep record wording, doors and existing run actions. Replace presentation only; add no editing or inferred traversal history. |
| `workflow/edit/WorkflowEditor.tsx` | Split into controller-facing page, diagram, selected inspector, picker and save review. Remove the graph-plus-all-open-cards structure, typed-id form and low-level `WayForm`. |
| `workflow/edit/graphDraft.ts` | Keep the lossless model; add atomic insertion/reconnect/tail/history operations and exact fingerprints. |
| `workflow/edit/StepOptions.tsx` | Keep field controls; compose them into the inspector with worded labels and the shared role editor. |
| `workflow/edit/answers.ts`, `bridge/repositories/workflowEdit.ts` | Keep the prop/bridge seam. Add request identity, matching judgement and typed draft-review answers. |
| Help tool, writer, workflow proposal judge and `ProposalCard.tsx` | Add the explicit draft door and graph-diff payload together. Preserve legacy doors; prevent draft fall-through to Apply. |
| Existing fixtures, stories and measured checks | Extend on the actual components. Retire superseded stories only after their behavior has replacement proof. |

D223 should amend D186’s read-only rendering contract only as far as selectable ports, wires and editing callbacks require. Its automatic layout, absence of stored positions and narrow rail remain. D221 keeps top-to-bottom reading, visible ways on, exact-draft judgement, separate Save/Use and one role editor. D222 governs all new wording: authored content is preserved; Daoris’s own words come from the shipped catalogues.