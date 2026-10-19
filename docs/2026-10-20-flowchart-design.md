# The processes drawn as flowcharts: one auto-laid diagram, the flows it draws, and the rail it keeps (proposed, 2026-10-20)

**Standing:** proposed under D186 (FLOWCHART1). Pending a second maker's read, which this design has not had yet (the
workflow design's §11 weighed one the same way), and the owner's review. No row builds before the owner's yes. The design
builds nothing; its rows (§10) do. UIDESIGN1's design (D185) refers here for every diagram: the component catalogue there
names this primitive, and its layout, words and traps are this document's.

Read for it: platform-ux §2-§4 and §6; the workflow design §3.3, §5.2, §6.2, §6.4, §7, §11 and §12; D157; the map design §1
and §2; the fabric events design §D0, §D2, §D6, §E2, §E3, §E7 and §E8, with D173's note on the names (D173.md:508-514); the
landing and proof design §8; the frame model §3a-§3b; the co-working design §1.3-§1.6. Every file reference below was read
at `02632075f`.

## 0. The case

**What is drawn today.** No process is drawn as a flow. Five drawings come close, and each answers a different question:

| Drawing | Where | What it draws | Its shape |
|---|---|---|---|
| The workflow's rail | `workflow/WorkflowChart.tsx:210-301`, mounted by `WorkflowTab` (a repository's and a workspace's page), `WorkflowRunView` (the session side bar) and `WorkflowLine` (quest and ask pages) | a workflow's steps, and a run's states on the same steps | a vertical list with a rail of marks, ↑/↓ between steps |
| The Map | `map/MapView.tsx:20-28`, `MapCanvas`'s ring, `LayeredMap.tsx:84-99` past `RING_MAX` | repositories and the quests, asks, chains and declared uses between them | a ring, or hand-laid layers that pan and zoom |
| The chain strip | `map/ChainStrip.tsx:20-33` | an ask, its quests in order and each quest's sessions | a list on a rail |
| The parts strip | `work/PartsStrip.tsx:39-49` | a line of work's records, how each began | a row of buttons |
| The commit graph | `projects/CommitGraph.tsx:28-35` | commits and their lanes | lanes beside rows, the lanes hidden from a reader |

**Why the rail was chosen, and what has changed.** WORKFLOW1 asked for how work moves to be shown as a flowchart. The
workflow design drew it as a vertical list instead (§6.2, workflow design lines 608-643), and rejected a free canvas (§12.1,
lines 892-893): the question a workflow answers is *what happens next*, and a list answers it at every width. That held while
a workflow was a sequence. Three things have moved since:

1. **Workflows became graphs.** The fabric's part E, accepted 2026-10-17, decodes every workflow version into nodes and wires:
   `all` groups become join nodes, `failed: stop` and `failed: wait` become wires to synthetic sinks and tasks, and an
   authored graph has `allOf` and `anyOf` groups (fabric §E3, §E7, lines 1495-1518 and 1767-1839). A list can show a sequence.
   It cannot show a fork, a join or a failure path without indenting them into a different shape.
2. **The fabric already chose an auto-laid editor.** §E8 says Setup's *Work* pages become one graph editor, laid out
   automatically, and that the running view shows each node's state and each wire's condition (lines 1841-1854). No drawing
   was designed for either one.
3. **The owner's report of 2026-10-20** (TASKS' priority list, UIDESIGN1) asks for the processes to be drawn with
   flowchart logic. It names the Map as a map of requests, not a process view, and judges its UI/UX poor.

**Every process is in a record already.** A quest's life is its operation log, replayed through one transition table. A
session's states are a ledger table. A line of work's parts, a landing's tries and a unit's milestone are kept, and so is
a node's lifecycle. Section 2 names each record. The drawing is the missing part.

## 1. Words

- **A flow** is one process drawn: its nodes, its edges, and what is lit. **The flowchart** (流程图) is the drawing a person
  sees, laid out by Daoris.
- **A node** is one place in the flow: a state, a step, a record, or a try. **An edge** is one move between two nodes,
  with its cause.
- **Three kinds of flow:**
  - **A state machine** is one thing that stands in one state at a time. Its graph is a table in the code. The current
    state is lit, and **the path** is the moves the record says it made, in order.
  - **A process** is a fixed graph of steps (a workflow version, a decoded graph). Each node has a state of its own.
  - **An instance** is the nodes the records made (parts, tries, attempts). It has no fixed graph. Every node in it is
    either past or current.
- **The placement** is how the same nodes are put on the page: **left to right**, **top to bottom**, or **the rail**,
  a list whose edges are words. The pane's width chooses it (§3.2).
- **A fold** is several nodes drawn as one node that says how many it holds, opened by its press (§3.6).

## 2. The flows, each from its record

| Flow | Kind | Its record | Read through | Mounted |
|---|---|---|---|---|
| A quest's life | state machine, with its path | the quest's operation log, replayed by `QuestTransitions` (`QuestLog.cs:4-102`, `:324-349`) | **a new read** of one quest's moves (FLOWCHART1c) | the quest's page |
| A session's states | state machine, current state only | the session record; `SessionLedger.Allowed` (`SessionLedger.cs:1521-1537`) | the record as the page holds it | the session side bar's *Timeline* view |
| A line of work's parts | instance | the entry's parts (`SessionEntries.cs:3-50`) | `SESSION_GROUPS`' entries | the session's page, in the parts strip's place |
| A workflow | process (not lit) | Current, or the named version; after WORKFLOW2, its decoded graph | `WORKFLOW_CURRENT` | the *Workflow* tab |
| A workflow's run, and the fabric's running view | process, each node lit | `WORKFLOW_RUN` (`run.ts:8-70`); after GRAPHVIEW1, each node's state and each wire's condition | `WORKFLOW_RUN`, then GRAPHVIEW1's answer | the session side bar's *Workflow* view |
| A node's lifecycle | state machine, with its attempts | `NodeLifecycle.Table` (`NodeLifecycle.cs:3-4`, `:239`), the node's kept history (`:103-114`) | GRAPHVIEW1's answer | under the node, in the running view |
| Landing and clean-up | instance | the landing record, the due list's tries and the unit's milestone | `TRACE` {session}'s landing (`Trace.Chain.cs:606-681`, `:720-749`) and `WORK_STATE` (`milestone.ts:85-104`) | a landed branch's page; the work's milestone; the run's landing node |

Nothing is inferred from a conversation (D157 §5.1). Where a record keeps no path, no path is drawn, and the drawing
says so.

### 2.1 A quest's life

- **The graph** is the four statuses and the moves the table allows (`QuestTransitions.Allows`). Taken comes only from
  Open. Done and Declined come from Open or Taken. Nothing leaves a close, and nothing returns to Open (D46 §3). The flow is
  entered by the publish. A deleted quest replays to no quest, so it has no page and no flow.
- **The path** is the quest's operations in replay order (`QuestStore.HistoryAsync`, `Quests.cs:1503-1508`):
  - *Published* is the entry edge. Its words say who asked: an ask, a session, the person or a sender.
  - *Taken* names the machine and the session.
  - *Done* or *Declined* carries the note, and the answers to the requirements.
- **Moves that change no status are drawn inside the state they happened in**, oldest first, three at most before a fold:
  - *waited on #q2* (its close resumes the quest), *accepted your departure*, *evidence read*, *set up in `<environment>`*;
  - *your verdict*, *bound to a graph*, *submitted*, *reopened by you*, *grew again from*;
  - *a conflict*: another machine's move that reached the remote second, kept rather than applied (D68 §5). It is drawn
    as a refused edge, faint and ended in ✕, from the state toward the status it attempted (*machine B's Done, kept as a
    conflict*). A dismissal settles it.
- **What is lit.** The current status is lit. A Done held by a departure, by missing or unread evidence, by an unreviewed
  set-up or by a reopened source says *held for your yes* in open's hue. It is the one place that hue appears in this flow,
  since only that waits on the person (platform-ux §3, lines 91-94, 118-120).
- **The record's gap.** No door answers one quest's history today. `/api/quests/operations` (`Program.cs:2174-2178`) is the
  remote's sync feed: every quest's operations since a cursor. FLOWCHART1c adds the read: `GET /api/quests/{id}/moves` on
  both hosts.
  - Each move answers its kind, its time, its machine and the session it names, as the quest already answers `holder`.
  - It answers its codes (a hold, a verdict, a conflict's attempted status), and never the published payload or a path.
  - Quests are service-visible (D47 §4), so a browser draws the flow too. `TRACE`'s quest link carries the same moves, and
    `daoris-driver trace quest <id>` says them (D50).
- **Mounted** on the quest's page, as a section after the chain strip and before *How it came to be*. It is folded to its
  line, the quest's status and *n moves*, and reads the moves only when opened, as the trace does (D143).

### 2.2 A session's states

- **The graph** is `SessionLedger.Allowed`:
  - queued → starting → working, with working ⇄ awaiting-person;
  - working or awaiting-person → completed or declined, and working → stood-down or failed;
  - queued and starting → stood-down or failed;
  - stopped from every live state, since the person's hand is never refused.
  - **Going on with the person's words** is the one move out of an ended state (`SessionAdvanceRefusal.Terminal`,
    `SessionLedger.cs:65-69`). It is drawn faint, with its reason, where this record cannot make it: a stand-down, an
    intake, a reviewer's pass, a helper, or another machine's record (`:1240-1258`).
- **The ended outcomes are one node** whose members are words (*completed · declined · stood down · failed · stopped*),
  with the reached one lit. The edge into it from every live state is said once on that node (*stopped: by you, from any
  live state*) rather than drawn from each state.
- **What is lit** is the record's state. Its shown word (*idle*, *parked*, *awaiting reply*, *answered*, *going on*:
  `shownState`) appears on the current node, in `SESSION_TONE`'s tone (`design/ui.tsx:234-264`). No new tone table is added.
- **No path.** A session's record keeps no event log (`work/timeline.ts:15-17`). So the drawing says *this record keeps
  where it stands, not how it got there*, and draws the moves out of the current state with who makes each (the person's
  answer or stop, the agent's done or decline, the driver's stand-down). A state history on the record is left out (§8).
- **Mounted** at the head of the side bar's *Timeline* view, above the observed events, which stay as they are.

### 2.3 A line of work's parts

- **Nodes** are the entry's parts, oldest first. Each shows its word (`shown`), its quest, its agent and when it began.
- **Edges** follow `EntryPart.After`, labelled by how the part began (*retried*, *resumed*, *carried on*, *handed over*):
  - an *asked* part hangs from a faint node outside the entry, *asked by `<repository>`*;
  - an *opened* part with no `after` hangs from the entry's start.
- **Parts that run alongside** (the co-working design §1.5's amendment, lines 116-120) are a fork. The one Accept that lands
  them on one branch is a join, drawn only where `ENTRY_LANDING` answers a plan (`bridge/entries.ts:38-69`).
- **What is lit**: the part shown, and the part that leads the entry's group.
- **Mounted** on the session's page, in the parts strip's place. The strip becomes the flow's rail, and a line of work
  with one part draws nothing, as today.

### 2.4 A workflow

- **Nodes** are the version's steps. Each keeps today's glyph, the person's part by its shape (`PartMark`) and word, and
  its executor.
- **Edges and the protected order:**
  - A group is a fork into its members and a join after them.
  - A failure path is a forward edge from a check or a stage to its outcome: *wait for you*, *stop*, or *sent back once*.
    That last is the kind's bounded return, never a backward edge (workflow §3.3; fabric §E7's table).
  - The landing's rank carries a divider labelled *Before it lands* and *After it lands*, where the rail has its section
    titles.
- **After WORKFLOW2**, the same drawing reads the decoded graph: wires `after`, `data`, `failed` and `rejected`; join
  nodes; and the synthetic sinks and tasks, drawn as small outcome nodes marked as made by decoding.
- **Mounted** on the *Workflow* tab of a repository's or a workspace's page. Its rail is today's `WorkflowChart`.

### 2.5 A workflow's run, and the fabric's running view

- **Today, a run** is §2.4's drawing with each step's state on its node, in `runTone`'s tones (`run.ts:78-90`). A step not
  reached is faint, the run's `at` is current, and *this session* marks the attended session's step. A skipped step says
  *skipped by you*. The steps done, in order, are the path.
- **After WORKFLOW2 and GRAPHVIEW1** the same view draws the bound graph:
  - each node in one of §E2's states;
  - each wire's condition: satisfied, not yet, or satisfying nothing, since unknown, invalidated and stopped satisfy no wire
    (§E3);
  - the revision, any authority missing, and a commitment waited on;
  - the unit's milestone as the graph's end: end reached, result kept, cleanup, recovery (§D0).
  This is **one running view**, D157 §7's and §E8's together.
- **Mounted** in the side bar's *Workflow* view. A quest's or an ask's `WorkflowLine` opens it, as today.

### 2.6 A node's lifecycle

- **The graph** is `NodeLifecycle.Table`, §E2's rows: blocked, ready, active, reached, failed, rejected, unknown, skipped,
  stopped and invalidated. Each edge says who causes it: Daoris's evaluation, the reach authority, the failure authority or
  the person.
- **The path** is the node's attempts (`NodeStatus.Attempt`, `Tries` and its kept history, `NodeMoved.Cleared`). Each path
  edge carries its attempt's number. A declared or person-asked retry (failed → ready) is the back edge, counted ×n.
- **Mounted** under the node in the running view, unfolded by its press. It is a record, and the drawer keeps forms
  (D118 §3d).
- **The record's gap.** GRAPHVIEW1 answers each node's history from the unit's record. Nothing answers it today.

### 2.7 Landing and clean-up

- **Nodes**, from the session's trace (its landing) and the unit's milestone:
  1. **Due since** (`TraceDue`). Its tries are nodes, and consecutive tries with one code are stacked: *held ×3:
     unreviewed* (codes `AutoLanding.cs:40-119`).
  2. **Landed.** The branch or the merge, who accepted it (the person, automatically, or the access setup), the rule, and
     the review, opinion and workflow that let it land (`TraceBranch`). A verify step's advances are stacked on it.
  3. **Pushed**, by its plugin. Then **the pull request**, with its kept state (`PullRequestState`) or the ask that failed.
  4. **End reached**, with its proof: merged, on the line, carried, or the pull request (`MilestoneEnd`).
  5. **Clean-up.** *Pending*, with each entry that waits and why. Or *completed*, with what went and the proof each was
     removed by (`LandedKind`, `LandedCleanups.cs:6-60`).
  6. **Recovery kept** until its date.
- **Edges** say the cause: *accepted by you*, *accepted automatically*, *pushed by `<plugin>`*, *merged on the
  platform*, *removed by the clean-up*.
- **Read today, with no new driver read.** `WORK_STATE` {unit: the branch} picks the unit by its branch and names its
  session (`bridge/lines.ts:141-172`, `milestone.ts:85-97`), whose `TRACE` carries the landing and the due list.
- **Mounted** on a landed branch's page in a repository's *Branches* (platform-ux lines 447-450). The work's milestone
  (`WorkMilestone`) and the run's landing node each open it.

### 2.8 Not drawn as flows

- **The chain**, an ask's quests in order: `ChainStrip` stays as it is, since a line needs no layout.
- **An ask's work across repositories** is left out (§8).
- **The commit graph** keeps its lanes.
- **The Map** is §5.5's.

## 3. The diagram

### 3.1 Laid out in layers, never by hand

A pure, deterministic function lays every flow out (`design/flowLayout.ts`). It has no positions to store, no panning,
no zoom and no dragging. It follows the steps that the Map's layers and the code map already take by hand
(`map/layers.ts:4-25`, `map/codeLayout.ts:1-11`):

1. **Back edges.** A depth-first walk from the entries, in declared order, marks each edge that reaches a node still on
   the walk as a back edge. Nodes no entry reaches are walked after them. A back edge is ranked as if reversed and drawn as
   a return. Only a state machine has back edges: a process graph is acyclic (§E7), and an instance is made in time order.
2. **Ranks.** Each node's rank is the longest path to it from an entry.
3. **Waypoints.** An edge that skips ranks takes a narrow slot in every rank it crosses, so it threads between nodes,
   never through one. The Map learned this from a loop of three.
4. **Order.** Each rank starts in declared order. Four barycentre sweeps (down, up, down, up) order each rank by its
   neighbours' places, and ties keep the earlier order (the Map's `SWEEPS = 4`).
5. **Stable between two looks.** The layout reads only the graph's shape, never the lit state, the path or a time. A
   state that changes moves no node, and only what is lit changes. An instance that grows adds its new nodes after the
   others.

### 3.2 Placed by the pane's width

The same nodes, in the same reading order, are placed one of three ways. Each node track is at least 10rem, a waypoint
track is 1rem, and tracks are 1.5rem apart. Ranks are 2.5rem apart top to bottom, and 3rem left to right, room for one
label line.

| Placement | When | What it is |
|---|---|---|
| **Left to right** | the pane holds every rank side by side, and no rank holds more than three nodes | each rank a column, centred; the shortest drawing on a wide pane |
| **Top to bottom** | otherwise, where the pane holds the widest rank (its nodes, waypoints and gaps) | each rank a row, centred; the reading order of the rail |
| **The rail** | otherwise, and wherever nothing measured the pane | the ordered list, each node's edges said under it as words; today's `WorkflowChart` generalised |

Measured on the frame's floors (frame model §3a-§3b, lines 80-86 and 104-108; a rem is 16 px):
- **The side bar's 300 px floor** holds top to bottom for a flow one node wide. A fork there is the rail.
- **The main area's 400 px floor** holds a rank of two.
- **A 680 px window's main area** (about 544 px beside the activity bar, the list's strip and the side bar's closed
  strip) holds a rank of three.
- **A 1546 px window** holds a quest's life left to right.

The thresholds are judgements, held by one table in the layout's tests.

### 3.3 A node

- **Content, in reading order:**
  1. its glyph and title;
  2. its state as a pill, with its word;
  3. who acts: the participation mark where a step has one, and the actor's name;
  4. when it was entered, and how long it has stood where it is current;
  5. the caller's own lines (a quest's moves, a door);
  6. its **foot**: one line for each outgoing edge, naming its target and cause (*→ Done, when its agent finishes*).
  The foot is always shown, in every placement.
- **Surface.** A node is a container: `--raised`, outlined in `--line`, radius 6.
  - **The current node** has a 2 px `--ink-soft` border and `aria-current`.
  - **A node not reached** has a dashed `--line-strong` border and words in `--ink-soft`. It is never made faint by
    opacity, which takes text below the floor unmeasured.
  - **A group node** (§2.2's outcomes) lists its members as words, with the reached one lit.
- **Words wrap** in either language and nothing is cut. A path breaks at its separators (`PathText`), a command between
  its words (`CodeText`), and authored words are shown as written (D142). A state or a kind the page has no words for is
  drawn by its code, *shown as recorded*, never dropped.

### 3.4 An edge

- **Shape.** A curve from the source's out-side to the target's in-side, through its waypoints. Each stretch leaves and
  arrives along the flow's axis (the Map's `link`, `LayeredMap.tsx:54-82`). Arrowheads are sized in the drawing's units,
  never the line's (map design lines 59-60).
- **Ink** (platform-ux §3: a mark that is content wears an ink, and the Map's arrows at `--line-strong` vanished in dark):
  - **taken** (on the path, or a process's settled step to the next): `--ink-soft` at 1.5 px;
  - **possible**: `--ink-faint` at 1 px;
  - **a failure or a rejection**: short dashes;
  - **refused** (a conflict): faint, ended in ✕.
  No edge wears a status hue.
- **Its label** is the cause in a word or two from the catalogue (*taken*, *on failure*, *retried*). It sits at the edge's
  middle in the rank gap, on a page-coloured backing, and only where its box clears every node and every other label. The
  source's foot always holds the whole cause, and a reader always hears it. So a dropped label loses nothing.
- **A path edge** carries the time and who moved it in the foot. Taken more than once, it is marked ×n.

### 3.5 Loops and retries

- **In a state machine**, a back edge (working ⇄ awaiting-person; a node's failed → ready) leaves the source's left side,
  runs along a lane outside the drawing's left edge, and enters the target's left side, marked ↺ and counted ×n on the
  path. Each back edge has a lane of its own, up to three, and then they share one. In the rail it is a foot line: *↺ back
  to Working when you answer*.
- **In an instance**, a retry is a new node (a part retried, a try, an attempt), so an instance has no loop. Consecutive
  like nodes become a stack (§3.6).
- **A process has no backward edge.** A send-back is the kind's bounded return (§2.4).

### 3.6 Too large to show whole

A flow is drawn whole up to 24 nodes, D157's authoring bound. A decoded graph may reach 96 with its synthetic nodes
(§E7). Past 24, the rules apply in this order:

1. **Stacks.** Three or more consecutive like nodes become one fold: same kind and state, or the same try code, each in a
   chain with one edge in and one out. It shows the count, the first and last time, and their shared word. Two stay apart,
   so a single retry stays visible.
2. **Settled stretches.** Four or more consecutive done nodes, ending two ranks or more before the current node, fold
   into *n steps done*.
3. **Focus.** If it is still past 24, the ranks from two before the current node to three after it are drawn, and
   *earlier* and *later* folds hold the rest.

A fold is a node with a press. It has `aria-expanded` and says what it holds. *Show whole* opens every fold, and the pane
scrolls. Nothing is ever drawn smaller to fit: a drawing keeps the type scale at every width (platform-ux lines 502-505).
Opened folds are page state, never remembered.

### 3.7 Keyboard and reader

- **One list in reading order.** Whatever the placement, the nodes are one `role="list"` in rank order, then rank by
  rank in each rank's order, named for the flow (*How #q1 moved*). The edges are one SVG, `aria-hidden`: the commit
  graph's rule that the drawing sits beside rows a reader reads (`CommitGraph.tsx:28-35`). Each node's accessible
  description says its state, who acts, when, and every edge in and out in words (*Reached from Open: taken by this
  machine at 14:02. Next: Done, when its agent finishes; Declined, when it declines.*).
- **Keys.** The flow is one Tab stop: the current node, else the first. Then:
  - ↓ and ↑ move in reading order, and Home and End go to either end;
  - → follows the first edge out, and ← goes back along the edge the node was reached by, else the first edge in;
  - Enter opens the node's door or a fold, and Tab reaches the node's own controls.
  A key an input method is composing with is the input method's (`isComposing`, platform-ux §6).
- **Motion.** Placing is never animated. Only the platform's 140 ms hover moves, and reduced motion takes even that
  away.

### 3.8 Tokens, themes and languages

The flow wears the platform's tokens and nothing new: seven type sizes (a title `text-body` semibold, a pill and a foot
`text-meta`, who and when `text-small`), the status palette's pills and inks, `--raised` nodes, and ink edges. The accent
marks only what is pressed and the focus ring. Both themes come from the tokens. The chrome speaks English and Chinese
through the catalogues, and a node's title is never a fixed English width.

## 4. The primitive

### 4.1 Its contract

```ts
type FlowNode = {
  id: string;
  title: string;                      // worded by the caller
  glyph?: IconName;
  state?: { word: string; tone: PillTone };  // the caller's existing tone table
  part?: WorkflowParticipation;      // the person's part, where a step has one
  actor?: string | null;
  at?: string | null;                // entered; `current` adds how long
  members?: { word: string; lit?: boolean }[];  // a group node's outcomes
  lines?: ReactNode;                 // the caller's own: moves, a door
  reached?: boolean;                 // false: not reached (dashed)
  current?: boolean;
  open?: () => void;                 // Enter and a press: its record's door
};
type FlowEdge = {
  id: string; from: string; to: string;
  says: string;                      // the whole cause: the foot and the reader
  label?: string;                    // a word or two, drawn where it fits
  style?: 'next' | 'failed' | 'rejected' | 'refused';
  path?: { order: number; at?: string | null; by?: string | null; count?: number };
};
type FlowProps = { label: string; nodes: FlowNode[]; edges: FlowEdge[]; entries: string[]; end?: ReactNode };
```

It is a molecule: the flow arrives worded and its doors go out. It imports no data hook and no bridge
(`presentational.test.ts`), so every state is a story's props. The back edges, the ranks and the placement are the
layout's to find, never the caller's to say.

### 4.2 Where it lives

`design/Flow.tsx` and `design/flowLayout.ts` sit beside `ui.tsx` and `Mark.tsx`, with `Flow.stories.tsx` and their tests.
`ui.tsx` already holds 1,678 lines, and this is one component with a pure layout of its own. The width is measured as the
Map measures its card (`map/useWidth.ts`, moved to `design/` or `lib/` so a primitive imports nothing from a view).

**The edges are drawn from measured boxes, and nothing measured feeds back.** The nodes and waypoint slots are placed by
CSS, and a layout effect reads their boxes to draw the SVG, again on each resize. The edges and labels change no box, so
nothing measured can loop back into the layout. Where nothing measures (a unit test's DOM), the placement is the rail and
no SVG is drawn.

### 4.3 A layout library, weighed

Measured 2026-10-20 from the packages' published files (gzip at level 9). Today's page is one chunk: a build of main that
day was 3,209,506 bytes, 904,638 gzipped.

| | elkjs 0.12.0 | @dagrejs/dagre 3.1.1 | By hand |
|---|---|---|---|
| Added to the page | 1,609,707 bytes, 466,978 gzipped (`lib/elk.bundled.js`; the worker build is the same size): about half again the page | 48,559 bytes, 16,897 gzipped (`dist/dagre.esm.js`, graphlib inlined): about 2% | a few hundred lines, beside `layers.ts` (268) and `codeLayout.ts` (42) |
| Licence | EPL-2.0 OR GPL-3.0-or-later: weak copyleft, with its notice to carry | MIT | the repository's |
| Upkeep | maintained (the registry's last change 2026-07-17), a Java layout compiled to JavaScript | maintained by the dagrejs organisation (last change 2026-08-08), after the original `dagre` stopped at 0.8.5 | ours, tested by table |
| How it lays out | asynchronous (a promise, or a worker); every node's size given up front; ports, orthogonal routing and clusters this design does not need | synchronous; every node's size given up front; coordinates out | ranks and order only, with CSS placing the nodes |
| What it costs here | measuring every node before layout, a second render, a bundle half again larger | measuring every node before layout; coordinates the page then ignores | the crossing quality is ours to hold by test |

**Chosen: by hand.** A flow is bounded (24 authored nodes, 96 decoded at most), the Map and the code map already lay
layers by hand and say why (map design lines 46-50; `codeLayout.ts:9-10`), and the design wants ranks and order rather
than coordinates. HTML nodes that wrap in either language cannot be sized before they render. Both libraries need those
sizes up front, which forces a two-pass layout.

**The named exit.** If the layout cannot hold its crossing table on FLOWCHART1a's fixtures (§7, trap 3), it takes
@dagrejs/dagre for ranks and order alone. It is MIT and about 17 KB gzipped, and choosing it is the decision of that row.
elkjs is rejected (D186).

### 4.4 Its stories

The stories are FLOWCHART1b's design review (D42 §5): each one in both themes, en and 中文, at 300, 400, 544 and 1200 px
by a decorator:
- **A quest's life:** Open → Taken → Done with a wait and the evidence read; declined while open; a conflict refused; Done
  held for your yes.
- **A session's states:** working ⇄ waiting on you, with the outcomes as one group.
- **Parts:** two alongside, a retry, and parts asked by another repository.
- **A landing:** three tries held for one reason, then landed, pushed, merged and cleaned up.
- **A workflow:** with a group and a failure path, and its run lit.
- **A run too large:** 24 steps and their synthetic nodes, folded around the current node.
- **Edge cases:** an unknown state shown as recorded, long Chinese titles, and nothing measured (the rail).

## 5. What happens to what is drawn today

### 5.1 The workflow rail: kept, as the narrow placement

`WorkflowChart`'s rail is the flow's rail: the same marks, words, edge line, ↑/↓ and section titles, now rendered by
`Flow` wherever the pane is too narrow for top to bottom. The Workflow tab and the run view draw top to bottom where they
have room, and the rail at the side bar's floor. D157's free canvas stays rejected: no positions, no panning, no zoom, no
dragging. What changes is only *a vertical list drawn as a graph*, which becomes *a layered graph laid out by Daoris,
with the list as its narrow placement*. The editor (WORKFLOW1g, folded into FABRIC-GRAPHEDIT1) keeps D157 §6.3's acts in
each node's ⋯ and the drawer, on this drawing.

### 5.2 The chain strip: stays

It draws one chain, a line, on a quest's page and under a session's head. A flow would add a layout to a line.

### 5.3 The parts strip: becomes the parts flow's rail

As §2.3 says.

### 5.4 The commit graph: stays

Its lanes are git's history, which has its own layout (`graphLanes`).

### 5.5 The Map: stays the map of requests, and needs its own row

The Map stays the workspace's view of repositories and the requests between them (MAP2, MAP4). It is not a process view,
draws no flow and gains none, and its layers stay its own: they pan and zoom across dozens of repositories, which a flow
never does. Its UI/UX was judged poor, and that is a design of its own, not this one: MAPUX1 (§10). UIDESIGN1's audit
covers what was built since UX7, and the Map was built before it.

## 6. Words in both languages

- **One new glossary term**, *flowchart* / 流程图, for the drawing. Its chrome is a section's name and a fold's words:
  *Show whole* (显示全部), *n more tries*, *n steps done*, *this record keeps where it stands, not how it got there*.
  Every node and edge word is a state or a cause the catalogues already hold (a quest's statuses, a session's states, a
  run's states, how a part began, a try's codes). The new ones are a quest's moves (FLOWCHART1c) and a node's states
  (GRAPHVIEW1).
- **The names collide** (D173.md:508-514 chose Graph / 图, Node / 节点, Wire / 连线):
  - the glossary's `graph` is the commit graph, 提交图 (`glossary.json:535`);
  - `wire` is a workspace connected to a remote, 接线 (`:540`);
  - 连线 is the Map's *connection* (`:550`).
  The flowchart's chrome uses neither *graph* nor *wire*. FABRIC-GRAPHEDIT1, which must name nodes and wires on screen,
  settles the three with the glossary's `match` patterns or new words, held by `names:check --strict`.

## 7. The traps, each a test or a story

1. **A cycle hangs or reorders the layout.** A state machine's back edges (working ⇄ awaiting-person; failed → ready) are
   found by the walk, and the layout ends. *Test:* `flowLayout.test.ts`.
2. **The picture jumps between two looks.** The same graph lays out the same way twice, and a change of the lit state, the
   path or the times moves no node. *Test:* the layout of each fixture equal with and without them.
3. **An edge runs through a node, or crosses for nothing.** No edge passes through a node's slot, and each fixture's
   crossings stay at or under its table's number. *Test:* the crossing table (trap 3 decides the exit, §4.3).
4. **The placement is wrong at a floor.** The placement table at 300, 400, 544 and 1200 px for one-, two- and three-wide
   ranks; unmeasured is the rail. *Test:* placement rows; *story:* each width.
5. **The drawing is read instead of the list.** The list is in reading order, and the SVG is `aria-hidden`. *Test:* the
   list items' order; the SVG's attribute.
6. **A reader cannot follow an edge.** Each node's description names every edge in and out. *Test:* the description's
   words, en and zh.
7. **Keys.** One Tab stop; ↓ ↑ → ← Home End Enter; nothing acts while an input method composes. *Test:* `Flow.test.tsx`
   and `composingKeys.test.ts`.
8. **Status by colour alone, or open's hue on what does not wait on the person.** Every lit node carries its word, tones
   come from the existing tables, and open's hue appears only on what waits on the person. *Test:* tones read from
   `SESSION_TONE`, `QUEST_TONE` and `runTone`; `tokens.test.ts` for the SVG's inks.
9. **Faintness by opacity.** A node not reached keeps its words at a token ink. *Test:* no `opacity-` class on a node's
   text.
10. **A code the page does not know is dropped.** It is drawn *shown as recorded*. *Test:* an unknown state, kind and
    try code.
11. **A second copy of a code's table drifts.** A fixed graph comes from its code: answered where the code holds it as
    data (`NodeLifecycle.Table`), or else a page constant held by a test that reads the C# source, as `SessionEventCodes`
    is held. *Test:* `QuestTransitions.Allows` and `SessionLedger.Allowed`, each read against the page's table.
12. **A new quest operation is never drawn.** The census over `QuestOperationKind` covers the moves' words, and
    `quest-operations.md` gains the place. *Test:* the census row.
13. **A path or a payload reaches the page.** The moves answer kinds, times, machine ids, session ids and codes, never
    the published body or a machine path (D47 §4). *Test:* the answer's fields by allow-list.
14. **Measuring loops.** Nothing measured changes a box (§4.2). *Test:* a resize redraws the SVG once and leaves each
    node's box unchanged.
15. **Shrunk to fit.** No `transform` scale and no `viewBox` over text. *Test:* the rendered markup.
16. **Too large.** A 96-node run renders folded with the current node in view, and *Show whole* opens it. *Story* and
    *test*.
17. **Words cut or clashing.** Chinese titles wrap with no fixed width, and the glossary's collisions are caught.
    *Story:* long 中文; `i18n:check`, `names:check --strict`.
18. **An older host, or a teammate's record.** No moves read: the state machine is drawn with its current state lit and
    says the path is not kept. *Test:* the quest's flow with no moves.

## 8. Not built

**Rejected (D186):**
- A free canvas, stored positions, panning, zoom and dragging. They were D157's, and they stand.
- A layout library: elkjs, by size, licence and its asynchronous sizes up front; dagre is the named exit only.
- Mermaid or another text-to-diagram renderer: its text is SVG, its diagrams are not the page's controls, and it lays out
  with one of the two libraries above.
- A canvas drawing, which no reader or keyboard reaches.
- SVG text for a node, which neither wraps nor measures Chinese. A node is HTML.
- Five drawings for five flows instead of one primitive.
- A process drawn on the Map.
- A second running view beside the Workflow view.
- A path inferred from a conversation, or a state the record does not keep.
- A copy of a code's table that no test holds.
- Animation of a move.

**Left out, not rejected:**
- A choice between the flowchart and the list where both fit (the width decides).
- A session's state history on its record, a stored shape of its own (`session-record-fields.md`), until a row asks.
- An ask's work across repositories as one flow, on the ask's page.
- A minimap, and exporting a flow as a picture.
- The Map's layers rebuilt on the flow's layout.

## 9. Against the decisions

**This amends:**
- **D157 point 12** and the workflow design **§6.2** (the drawing), **§6.4** (keys and narrow windows: the list is now
  the narrow placement, and the keys gain → and ←) and **§7** (the run on the same drawing).
- **§12.1's reason**, *a list answers it at every width*: a list answers a sequence, and a graph needs its forks drawn.
  The free canvas stays rejected.
- **The fabric design's §E8.** The running view is drawn on this flow, as one view with D157 §7's. The editor, laid out
  automatically, is laid out by this layout, and a node's lifecycle unfolds under it.
- **The co-working design §1.6.** The parts strip becomes the parts flow's rail.
- **platform-ux §4** gains the flow component. UIDESIGN1's catalogue points here.

**These stand:**
- D41's tokens and the status palette, D66's one navigation, and D118's frame, side bar and drawer rule.
- D47 §4: the moves answer no path, and the landing flow is desktop-only, as the landing record is.
- D50: the moves' terminal twin. D52: a molecule imports no hook.
- The map design §1's *no graph library*, which this extends to flows.
- D116's glossary, D142's content as written, D143's trace, and D173's names, whose collisions §6 hands to GRAPHEDIT1.

## 10. The rows

In build order. Each is sized to about a hundred turns, and each names its Ask Daoris door or its reason (D110). The
reads here add no press, so they need no `HelpCoverageTests` row.

- [ ] **FLOWCHART1a — the flow's layout** (web-shell; D186; this design §3.1-§3.2, §3.6). `design/flowLayout.ts`, pure: back edges by a walk from the entries, longest-path ranks, waypoint slots, four barycentre sweeps, the placement by width, the folds. Opus. Proof: `flowLayout.test.ts`'s tables (cycles, stability, no edge through a node, crossings, placement at 300/400/544/1200, folds), seen to fail first.
- [ ] **FLOWCHART1b — the flow primitive and its stories** (web-shell; after 1a; §3.3-§3.8, §4). `design/Flow.tsx`: one list in reading order, placed by the layout, edges in an `aria-hidden` SVG from measured boxes, the keys, the folds; the glossary's *flowchart*. Opus. Proof: `Flow.test.tsx`, §4.4's stories in both themes and languages, `tokens.test.ts`, `i18n:check`, `names:check --strict`.
- [ ] **FLOWCHART1c — a quest's moves answered** (service, driver; §2.1, traps 11-13). `GET /api/quests/{id}/moves` on both hosts from `HistoryAsync`, page-safe by allow-list; `TRACE`'s quest link carries them and `daoris-driver trace quest` says them; the census and `quest-operations.md` gain the place. Opus. Proof: the door's tests per kind, `QuestOperationKindsTests`' census, `TraceTests` (the link and the terminal's words).
- [ ] **FLOWCHART1d — a quest's life drawn** (web-shell; after 1b, 1c; §2.1). On the quest's page, folded to its status and moves: the four statuses from `QuestTransitions` (held by a source test), the path, each move's words, a conflict refused, a held Done in open's hue; a browser too. Opus. Proof: tests en and zh, stories, the rail at 300 px.
- [ ] **FLOWCHART1e — a session's parts and states drawn** (web-shell; after 1b; §2.2-§2.3). The parts flow in the parts strip's place, the strip as its rail; the *Timeline* view's head draws `SessionLedger.Allowed` (held by a source test) with the current state lit, the outcomes as one node, and no path. Sonnet. Proof: tests en and zh, stories, `PartsStrip`'s tests kept.
- [ ] **FLOWCHART1f — landing and clean-up drawn** (web-shell; after 1b; §2.7). On a landed branch's page, and from the work's milestone and the run's landing node: `WORK_STATE` by branch and its session's `TRACE`, tries stacked by code, landed, pushed, the pull request, the end, the clean-up, the recovery. Opus. Proof: tests en and zh over fixtures of each try code, stories.
- [ ] **FLOWCHART1g — a workflow and its run drawn** (web-shell; after 1b; §2.4-§2.5, §5.1). `WorkflowChart` on `Flow`: the Workflow tab and the side bar's Workflow view top to bottom where wide, the rail at the floor; groups as fork and join, failure paths, the landing's divider, the run lit. Opus. Proof: the workflow tests' words unchanged, placement tests, stories.
- [ ] **FLOWCHART1h — the flows on the install** (parent; after a republish carrying 1d-1g). A quest, a session's parts and states, a landed branch and a workflow with its run, at 1546 and 680 px, both themes, en and 中文. Proof: dated shots and a ledger under D186.
- [ ] **MAPUX1 — the Map's UI/UX, its own design** (design; the map design §1, D186 §5.5). The Map stays the map of requests and was judged poor: shots of the ring and the layers at 1546 and 680 px in both themes first, then what it answers that no page does, and its rows. Opus, with another maker's read. Proof: the document and its rows.

**Two rows change** (the parent's to write into TASKS):
- **FABRIC-GRAPHVIEW1** is drawn on `Flow` after FLOWCHART1b and 1g, in the side bar's Workflow view, with a node's
  lifecycle (§2.6) unfolding under it.
- **FABRIC-GRAPHEDIT1** lays its editor out by FLOWCHART1a's layout, and settles §6's three name collisions.
