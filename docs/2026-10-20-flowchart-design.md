# The processes drawn as flowcharts: one auto-laid diagram, the flows it draws, and the rail it keeps (proposed, 2026-10-20)

**Standing:** proposed under D186 (FLOWCHART1). A second maker read it on 2026-10-20 (the workflow design's §11 weighed one
the same way), and it was revised the same day, each finding checked against main at `872520fa5` (DESIGNREAD1, D186's note
says what changed and what did not). Pending the owner's review. No row builds before the owner's yes. The design
builds nothing; its rows (§10) do, each one feature end to end as D187 shapes a row. UIDESIGN1's design (D185) refers here for every diagram: the component catalogue there
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
| A quest's life | state machine, with its path | the quest's operation log, replayed by `QuestTransitions` (`QuestLog.cs:4-102`, `:324-349`) | **a new read** of one quest's moves (FLOWCHART1a2) | the quest's page |
| A session's states | state machine, current state only | the session record; `SessionLedger.Allowed` (`SessionLedger.cs:1521-1537`) | the record as the page holds it | the session side bar's *Timeline* view |
| A line of work's parts | instance (lineage) | the entry's parts (`SessionEntries.cs:3-50`): how each began and what it follows, no times | `SESSION_GROUPS`' entries | the session's page, in the parts strip's place |
| A workflow | process (not lit) | Current, or the named version; after WORKFLOW2, its decoded graph | `WORKFLOW_CURRENT` | the *Workflow* tab |
| A workflow's run, and the fabric's running view | process, each node lit | `WORKFLOW_RUN` (`run.ts:8-70`): each step's state now, no path; after GRAPHVIEW1, each node's state and each wire's condition | `WORKFLOW_RUN`, then GRAPHVIEW1's answer | the session side bar's *Workflow* view |
| A node's lifecycle | state machine, current state only | `NodeLifecycle.Table` (`NodeLifecycle.cs:3-4`, `:239`); the node's counters (`:16-20`) and its retained entries (`NodeRecord.cs:134-138`), not its path | GRAPHVIEW1's answer | under the node, in the running view |
| Landing and clean-up | instance | the landing record (several units' work, its advances), the due list's tries and the unit's milestone | `TRACE` {session}'s landing (`Trace.Chain.cs:606-681`, `:720-749`), its branch gaining the advances and the units (FLOWCHART1c), and `WORK_STATE` (`milestone.ts:85-104`) | a landed branch's page; the work's milestone; the run's landing node |

Nothing is inferred from a conversation (D157 §5.1). **Each drawing promises only what its record holds**: a path only
where the record keeps the moves in their order, a time only where it keeps one, a join or a concurrency only where it
records one, an event only where it keeps when it happened. Where a record keeps no path, no path is drawn, and the drawing
says so. A snapshot (where a run stands now, a pull request's latest answer) is drawn as the state it is, never as a
sequence.

### 2.1 A quest's life

- **The graph** is the four statuses and the moves the table allows (`QuestTransitions.Allows`). Taken comes only from
  Open. Done and Declined come from Open or Taken. Nothing leaves a close, and nothing returns to Open (D46 §3). The flow is
  entered by the publish. A deleted quest replays to no quest, so it has no page and no flow.
- **The path** is the quest's operations in the order they replay (`QuestStore.HistoryAsync`, `Quests.cs:1503-1508`): what
  a remote accepted, by its number, then this machine's pending ones, in the order made. Never sorted by time: the times are
  each machine's own.
  - **A recorded operation is not always a move.** The replay passes over one that does not apply (`QuestLog.cs:364-372`,
    `:384`), so each move says whether it applied, as the service's replay decides, and one that did not is drawn inside
    its state, *recorded, not applied*, never as an edge.
  - **The path is what the log holds, never a complete history.** A quest from before the log was given a history of its
    publication and its status alone (`Quests.cs:1114-1128`): its Open → Done is drawn as recorded, and no take is filled in.
  - *Published* is the entry edge. Who asked comes from the quest the page holds (its sender, its ask), never from the
    publish's payload, which the read does not answer.
  - *Taken* names the machine. A take carries no session (`QuestLog.cs:159-162`: only a submission names one); the session
    holding the quest now, where the quest answers its holder, is said on the Taken state as its holder now, never as who
    took it.
  - *Done* or *Declined*: the standing close's note and answers are the quest's own, from the record the page holds; a close
    that lost to another machine's is a conflict, with its attempted status.
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
  remote's sync feed: every quest's operations since a cursor. FLOWCHART1a2 adds the read: `GET /api/quests/{id}/moves` on
  both hosts.
  - **Each move answers, field by field and by allow-list:** its kind; its time on the machine that made it; its machine;
    its reference, machine and sequence (`QuestOperationRef`, `QuestLog.cs:104-109`), by which a dismissal names its
    conflict; the remote's number, or none while it is pending; whether the replay applied it; for a conflict its attempted
    status, for a dismissal the reference it dismisses, for a submission its session; for a hold, a verdict, an evidence
    read or a set-up, its code.
  - It never answers the published payload (`QuestLog.cs:175-182`), a note's body or the answers beyond what the quest
    already answers, or a path. A field a move does not keep is absent, and the drawing says *not recorded*.
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
    `SessionLedger.cs:65-69`). It is not in `Allowed`, and has guards of its own: the person's words waiting, and the tree
    free (`:1251-1266`). It is drawn as a guarded edge saying both, and faint, with its reason, where this record cannot
    make it at all: a stand-down, an intake, a reviewer's pass, a helper, or another machine's record (`:1240-1258`).
- **The ended outcomes are one node** whose members are words (*completed · declined · stood down · failed · stopped*),
  with the reached one lit. The edge into it from every live state is said once on that node (*stopped: by you, from any
  live state*) rather than drawn from each state. The go-on edge leaves only the members it may leave, and the group says
  which may not (a stand-down never goes on, `:1245`), so the one node never implies every outcome can resume.
- **What is lit** is the record's state. Its shown word (*idle*, *parked*, *awaiting reply*, *answered*, *going on*:
  `shownState`) appears on the current node, in `SESSION_TONE`'s tone (`design/ui.tsx:234-264`). No new tone table is added.
- **No path.** A session's record keeps no event log (`work/timeline.ts:15-17`). So the drawing says *this record keeps
  where it stands, not how it got there*, and draws the moves out of the current state with who makes each (the person's
  answer or stop, the agent's done or decline, the driver's stand-down). A state history on the record is left out (§8).
- **No entry time.** The record's `updated` is its last write, not when it entered its state (`work/timeline.ts:65-70`
  reads it so), so the current node says its state and no *since*.
- **Mounted** at the head of the side bar's *Timeline* view, above the observed events, which stay as they are.

### 2.3 A line of work's parts

- **Nodes** are the entry's parts, oldest first. Each shows its word (`shown`) and its quest; its agent and when it began
  only where the page holds that part's session record, since a part keeps neither (`SessionEntries.cs:25-38`).
- **Edges** follow `EntryPart.After`, labelled by how the part began (*retried*, *resumed*, *carried on*, *handed over*):
  - an *asked* part hangs from a faint node outside the entry, *asked by `<repository>`*;
  - an *opened* part with no `after` hangs from the entry's start, and so does a part carried on from a chain's parent,
    which keeps no `after` (`SessionEntries.cs:139-140`), labelled by how it began.
- **Lineage, not concurrency.** Parts with no `after` between them branch from the entry's start: they began apart, which
  says nothing of whether they ran at once, since a part records no start, end or overlap. Neither the branches nor the
  oldest-first order are drawn as a cause.
- **No join is drawn as done.** Where `ENTRY_LANDING` answers (`bridge/entries.ts:38-48`), its plan, where the parts *would*
  land with what holds or leaves out each (`EntryAccept.tsx:9-29`), is a prospective node, dashed, *Accept would land these
  on {target}*. What did land is the landing flow's (§2.7).
- **What is lit**: the part shown, and the part that leads the entry's group.
- **Mounted** on the session's page, in the parts strip's place. The strip becomes the flow's rail, and a line of work
  with one part draws nothing, as today.

### 2.4 A workflow

- **Nodes** are the version's steps. Each keeps today's glyph, the person's part by its shape (`PartMark`) and word, and
  its executor.
- **Today, its steps in order.** The web's answer carries each step's kind, part, executor and settings, and no members
  and no wires (`workflow/current.ts:27-42`): before WORKFLOW2 the drawing is the steps in their version's order, as the
  rail draws them, with no fork and no group the answer does not hold.
- **Edges and the protected order, once the decoded graph is read:**
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
  who skipped it as the record names it: the person, or the record's own pass where nothing was asked of it (an opinion's
  gate, `NodeLifecycle.cs:270-272`), never *by you* on every skip.
- **No path.** A run answers where each step stands now and its facts (`run.ts:18-64`), not the order the work went
  through: no taken edges are drawn between done steps beyond the version's own order, which concurrent or repeated work
  would contradict.
- **After WORKFLOW2 and GRAPHVIEW1** the same view draws the bound graph:
  - each node in one of §E2's states;
  - each wire's condition: satisfied, not yet, or satisfying nothing, since unknown, invalidated and stopped satisfy no wire
    (§E3);
  - the revision, any authority missing, and a commitment waited on;
  - the unit's milestone as the graph's end: end reached, result kept, cleanup, recovery (§D0);
  - several nodes current at once, or none in a settled or unstarted run, each lit by its own state.
  This is **one running view**, D157 §7's and §E8's together.
- **Mounted** in the side bar's *Workflow* view. A quest's or an ask's `WorkflowLine` opens it, as today.

### 2.6 A node's lifecycle

- **The graph** is `NodeLifecycle.Table`, §E2's rows: blocked, ready, active, reached, failed, rejected, unknown, skipped,
  stopped and invalidated. Each edge says who causes it: Daoris's evaluation, the reach authority, the failure authority or
  the person.
- **The hardest graph this design draws.** The table is a multigraph (`NodeLifecycle.cs:241-284`): reciprocal moves
  (blocked ⇄ ready, active ⇄ unknown), several returns to ready (from failed, rejected and stopped), and parallel moves of
  one source and target with different causes (unknown → reached by its authority or by the person; failed → ready by
  Daoris or by the person; active → skipped by the person or by the record). §3.4 and §3.5 route it, and it is a fixture of
  §7's trap 3.
- **No path.** The record keeps counters, not moves: `Attempt`, the newest attempt's number, and `Tries`, the attempts since
  the binding or the person's word (`NodeLifecycle.cs:16-20`); and a history of the statuses a move cleared (`NodeHistory`,
  `NodeRecord.cs:134-138`), written only when a move clears one (`WorkPerformer.cs:855-860`), so blocked ⇄ ready and
  active ⇄ unknown leave no entry. The drawing lights the current state, says the counters (*attempt 3 · 2 tries since your
  word*), and lists the retained entries beside the table (*attempt 2: failed at 14:02, cleared by a declared retry*), never
  as lit edges. A traversed path is drawn only once a row records each move.
- **Mounted** under the node in the running view, unfolded by its press. It is a record, and the drawer keeps forms
  (D118 §3d).
- **The record's gap.** GRAPHVIEW1 answers each node's history from the unit's record. Nothing answers it today.

### 2.7 Landing and clean-up

- **Nodes**, from the session's trace (its landing) and the unit's milestone:
  1. **Due since** (`TraceDue`). Its tries are nodes, and consecutive tries with one code are stacked: *held ×3:
     unreviewed* (codes `AutoLanding.cs:40-119`).
  2. **Landed.** The branch or the merge, who accepted it (the person, automatically, or the access setup), the rule, and
     the review, opinion and workflow that let it land (`TraceBranch`). Its advances, each later done that moved it on
     (LAND2c, `LandedBranches.cs:103-107`), are stacked on it, each naming its session: one landing carries several units'
     work (`:131-135`), so the flow is the branch's, never one session's. A landing into the line keeps no branch, and the
     trace says so by a gap (`merge-accepted`, `Trace.Chain.cs:1144-1158`), drawn as its words: *merged into the line,
     accepted; no branch kept*.
  3. **Pushed**, by its plugin: a fact with no time, since the record keeps whether it was pushed and not when
     (`Trace.Chain.cs:627-644`). Then **the pull request**, with its latest answer and when that was asked
     (`PullRequestState`), or the ask that failed: a snapshot, never a sequence of states.
  4. **End reached**, with its proof: merged, on the line, carried, or the pull request (`MilestoneEnd`).
  5. **Clean-up.** *Pending*, with each entry that waits and why. Or *completed*, with what went and the proof each was
     removed by (`LandedKind`, `LandedCleanups.cs:6-60`).
  6. **Recovery kept** until its date.
- **Edges** say the cause: *accepted by you*, *accepted automatically*, *pushed by `<plugin>`*, *merged on the
  platform*, *removed by the clean-up*.
- **Read by its branch, with one read added.** `WORK_STATE` {unit: the branch} picks the unit by its branch and names its
  session (`bridge/lines.ts:141-172`, `milestone.ts:85-97`), whose `TRACE` carries the landing and the due list. The trace's
  branch carries neither the advances nor the units (`Trace.Chain.cs:1187-1204`), so FLOWCHART1c adds both, fields beside
  the old: an older driver answers none, and the drawing says the advances were not read.
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
(`map/layers.ts:4-25`, `map/codeLayout.ts:1-11`). The Map is a precedent for the steps, not a proof of the geometry: its
cards are one height (`map/layers.ts:33-34`) at coordinates it computes (`:213-229`), where a flow's nodes are HTML boxes
of any height, so only a measured-box test proves a flow's edges clear its nodes (§7, trap 3).

1. **Back edges.** A depth-first walk from the entries, in declared order, marks each edge that reaches a node still on
   the walk as a back edge. Nodes no entry reaches are walked after them. A back edge is ranked as if reversed and drawn as
   a return. Only a state machine has back edges: a process graph is acyclic (§E7), and an instance is made in time order.
2. **Ranks.** Each node's rank is the longest path to it from an entry.
3. **Waypoints.** An edge that skips ranks takes a narrow slot in every rank it crosses, so it threads between nodes,
   never through one. The Map learned this from a loop of three.
4. **Order.** Each rank starts in declared order. Four barycentre sweeps (down, up, down, up) order each rank by its
   neighbours' places, and ties keep the earlier order (the Map's `SWEEPS = 4`). **It is a heuristic**: it lowers crossings
   and guarantees no minimum (two entries each wired to both of two targets cross once in any order), and the graphs the
   fabric permits are denser than a quest's (`allOf` and `anyOf` groups, the protected order's `after` wires from each hold
   to every later step, up to 96 wires; the fabric design §E7). The crossing table (§7, trap 3) holds a ceiling per
   fixture, never a minimum.
5. **Same graph, same picture.** The layout reads only the graph's shape, never the lit state, the path or a time, so a
   state that changes moves no node on an unfolded flow, and only what is lit changes; folds are the one exception (§3.6).
   **A graph that grows is laid out again, and its nodes may move.** A node appended to a rank is ordered by its
   neighbours with the rest (ranks [A, B] → [X, Y] with A→X and B→Y, then A→Z added: the sweep orders the second rank X, Z,
   Y, and Y moves), and a centred rank that widens moves its nodes. No positions are kept to prevent it (§8); the focus is
   kept by node id (§3.7), so the person's place survives a move.

### 3.2 Placed by the pane's width

The same nodes, in the same reading order, are placed one of three ways. Each node track is at least 10rem, a waypoint
track is 1rem, and tracks are 1.5rem apart. Ranks are 2.5rem apart top to bottom, and 3rem left to right, room for one
label line.

| Placement | When | What it is |
|---|---|---|
| **Left to right** | the pane holds every rank side by side, and no rank holds more than three nodes | each rank a column, centred; the shortest drawing on a wide pane |
| **Top to bottom** | otherwise, where the pane holds the widest rank (its nodes, waypoints and gaps) and the return lanes | each rank a row, centred; the reading order of the rail |
| **The rail** | otherwise, wherever nothing measured the pane, and wherever the person chose *Show as list* | the ordered list, each node's edges said under it as words; today's `WorkflowChart` generalised |

**Chosen by the measured inner width against what the flow needs**, never by the window: the widest rank's node tracks at
their floor, the waypoint slots in that rank, the gaps between them, and a lane for each return (§3.5), each lane 1rem
with a 0.5rem gap. The widths below are worked at those floors on the frame's (frame model §3a-§3b, lines 80-86 and
104-108; a rem is 16 px), before gutters:
- **The side bar's 300 px floor:** two node tracks need 344 px, so a fork is the rail; a flow one node wide holds top to
  bottom, its return lanes counted (one node and three lanes, 232 px).
- **The main area's 400 px floor:** a rank of two (344 px) with one return lane or one waypoint (368 or 384 px), not both.
- **A 680 px window's main area** (about 544 px beside the activity bar, the list's strip and the side bar's closed
  strip): a rank of three nodes and nothing else (528 px). A waypoint in that rank needs 568 px, so such a flow is the rail
  there.
- **Left to right at 1,200 px of pane:** six ranks at their floor (six tracks and five 3rem gaps, 1,200 px) and no more,
  before gutters and lanes. A quest's life, three ranks, fits at the install's 1546 px window; a longer workflow is top to
  bottom or the rail.

The thresholds are judgements, held by one table in the layout's tests over those budgets.

**Show as list, at every width.** The flow's head offers *Show as list*, and back *Show as flowchart*: the width decides
what fits, not whether a person can follow branching ink. The list is the rail, the same nodes in the same order with
their edges as words, so nothing is lost by it. The choice is remembered on this machine for every flow.

### 3.3 A node

- **Content, in reading order:**
  1. its glyph and title;
  2. its state as a pill, with its word;
  3. who acts: the participation mark where a step has one, and the actor's name;
  4. when it was entered, and how long it has stood where it is current, only where its record keeps the time (a quest's
     move does; a session's state, a part and a run's step say none they do not hold);
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
- **Parallel edges**, one source and one target with several causes (the node lifecycle's, §2.6): those of one style are
  one path marked ×n, each cause apart in the source's foot and the description, since the ink never carries a cause and
  two like inks side by side add width and nothing else; those of different styles are separate paths, offset by a fixed
  spacing so neither runs on the other.

### 3.5 Loops and retries

- **In a state machine**, a back edge (working ⇄ awaiting-person; a node's failed → ready) leaves its source through an
  **escape slot** at the outer end of the source's rank, reserved as a waypoint's is, so it never crosses a sibling in that
  rank; runs on **a lane of its own** outside every node (left of the drawing top to bottom, below it left to right, so it
  crosses no earlier column); and enters its target through the escape slot at the outer end of the target's rank, marked
  ↺ and counted ×n on the path.
- **No two returns share a lane**, since a shared lane hides which return is which, and the lifecycle's returns differ by
  cause. Every lane counts in the width (§3.2): where the lanes do not fit, the flow is the rail.
- **A reciprocal pair** (blocked ⇄ ready, active ⇄ unknown) is two paths: the forward one in the rank gap, the return on
  its lane. In the rail a return is a foot line: *↺ back to Working when you answer*.
- **In an instance**, a retry is a new node (a part retried, a try, an attempt), so an instance has no loop. Consecutive
  like nodes become a stack (§3.6).
- **A process has no backward edge.** A send-back is the kind's bounded return (§2.4).

### 3.6 Too large to show whole

A flow is drawn whole up to 24 nodes, D157's authoring bound. A decoded graph may reach 96 with its synthetic nodes
(§E7). Past 24, the rules apply in this order:

1. **Stacks.** Three or more consecutive like nodes become one fold: same kind and state, or the same try code, each in a
   chain with one edge in and one out. It shows the count, the first and last time, and their shared word. Two stay apart,
   so a single retry stays visible.
2. **Settled stretches.** Four or more consecutive done nodes, ending two ranks or more before every current node, fold
   into *n steps done*.
3. **Focus.** If it is still past 24, the ranks from two before to three after each current node are drawn, their union
   where several nodes are current; with none current, the ranks after the entries where nothing has started, or before
   the end where all has settled. *Earlier* and *later* folds hold the rest.

**A fold keeps its boundary.** A fold's members are whole nodes. Every edge that crosses into or out of it is drawn to the
fold, merged by cause and by the node outside and counted, and the fold's description names each with its cause, so no
cause is lost to a fold; an edge between two members is said in the description.

A fold is a node with a press. It has `aria-expanded` and says what it holds. *Show whole* opens every fold, and the pane
scrolls. Nothing is ever drawn smaller to fit: a drawing keeps the type scale at every width (platform-ux lines 502-505).
Opened folds are page state, never remembered.

**The stability exception.** Settled stretches and focus follow the state, so a state that changes may open or close a
fold, and so change the drawing's shape: it is the one way a change of state moves anything (§3.1). The focus stays on its
node by id (§3.7), and a fold the person opened stays open while they are on the page.

### 3.7 Keyboard and reader

- **One list in reading order.** Whatever the placement, the nodes are one `role="list"` in rank order, then rank by
  rank in each rank's order, named for the flow (*How #q1 moved*). The edges are one SVG, `aria-hidden`: the commit
  graph's rule that the drawing sits beside rows a reader reads (`CommitGraph.tsx:28-35`). Each node is a list item that
  takes the focus, one at a time (a roving `tabindex`), named by its title and its state's word and described by its state,
  who acts, when, and every edge in and out in words (*Reached from Open: taken by this machine at 14:02. Next: Done, when
  its agent finishes; Declined, when it declines.*).
- **The keys are said.** A list does not tell a reader its arrows move, so the flow's label is followed by a line a reader
  hears once and the eye does not see: *arrow keys move between steps, Enter opens one, Tab reaches its controls*.
- **Keys.** The flow is one Tab stop: the current node, else the first. Then:
  - ↓ and ↑ move in reading order, and Home and End go to either end;
  - → goes to the first edge's target, and ← back along the edge the node was reached by, else its first edge in;
  - the arrows act only on a key aimed at the node itself, never from a button or a field inside it (the guard
    `WorkflowChart.tsx:82-92` already keeps);
  - Enter opens the node's door or a fold.
  A key an input method is composing with is the input method's (`isComposing`, platform-ux §6).
- **Every branch has a door.** Each line of a node's foot is a press that moves the focus to that edge's target, so a
  second or a third branch is reached directly, never only by walking the list.
- **Into a node and back.** Tab from a node reaches its own controls in order (its foot's lines, its door, a fold's press);
  Escape from any of them gives the focus back to the node; a drawer the node opened gives it back to the node as it
  closes (the workflow design §6.4, `docs/2026-10-09-workflow-design.md:682-685`).
- **The focus is kept by node id.** A change of placement (a resize), a fold opened or closed, *Show as list* and a new
  layout keep the focus on the same node id; a node folded away gives it to its fold, and an opened fold to the node that
  had it.
- **Tested twice.** `Flow.test.tsx` asserts the roles, names and descriptions, the roving focus, each key, Escape back and
  the focus kept across a fold. Attribute and order assertions do not prove a reader follows it, so each row's window
  proof includes one pass with a screen reader (Narrator) along a branch and back, written into its hand-back.
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
nothing measured can loop back into the layout. Where nothing measures (a unit test's DOM: jsdom lays nothing out,
`vite.config.ts:29`, with `css: false`), the placement is the rail and no SVG is drawn, so the geometry is proved in a
rendered browser (§7, trap 3) and never by a unit test.

### 4.3 A layout library, weighed

Measured 2026-10-20 from the packages' published files (gzip at level 9). Today's page is one chunk: a build of main that
day was 3,209,506 bytes, 904,638 gzipped.

| | elkjs 0.12.0 | @dagrejs/dagre 3.1.1 | By hand |
|---|---|---|---|
| Added to the page | 1,609,707 bytes, 466,978 gzipped (`lib/elk.bundled.js`; the worker build is the same size): about half again the page | 48,559 bytes, 16,897 gzipped (`dist/dagre.esm.js`, graphlib inlined): about 2% | a few hundred lines, beside `layers.ts` (268) and `codeLayout.ts` (42) |
| Licence | EPL-2.0 OR GPL-3.0-or-later: weak copyleft, with its notice to carry | MIT | the repository's |
| Upkeep | maintained (the registry's last change 2026-07-17), a Java layout compiled to JavaScript | maintained by the dagrejs organisation (last change 2026-08-08), after the original `dagre` stopped at 0.8.5 | ours, tested by table |
| How it lays out | asynchronous (a promise, or a worker); every node's size given up front; ports, orthogonal routing and clusters this design does not need | synchronous; every node's size given up front; coordinates out | ranks and order only, with CSS placing the nodes |
| What it costs here | a bundle half again larger and a licence notice to carry; a render to measure, then one to place | a render to measure, then one to place, again on each resize and each change of language | the crossing quality and the routing are ours to hold by test |

**Measuring is no bar to a library.** The drawing measures its boxes anyway, and those sizes could feed either library.
The choice is between two shapes of layout: ranks and order from the layout with CSS placing the nodes in one pass, so text
reflows by itself on a resize or a change of language; or coordinates from a library over measured sizes, a render to
measure and a second to place, again on every reflow.

**Chosen, provisionally: by hand.** A flow is bounded (24 authored nodes, 96 decoded at most), the Map and the code map
already lay layers by hand and say why (map design lines 46-50; `codeLayout.ts:9-10`), and one pass with CSS placing the
nodes is the simpler page while it holds its fixtures.

**dagre is evaluated, not presumed.** FLOWCHART1a1 runs the hardest fixtures (§7, trap 3) through both the hand layout and
@dagrejs/dagre (MIT, about 17 KB gzipped) over the same measured boxes in a rendered browser, and the engine is locked on
that table, not before. dagre is weighed whole, with its coordinates: its ranks and order alone, fed to this design's
placement, could not repair what the routing does with them. elkjs stays rejected on its size and its licence (D186),
not on measuring.

### 4.4 Its stories

The stories are the design review (D42 §5), each written by the feature that first draws its case (§10): each one in
both themes, en and 中文, at 300, 400, 544 and 1200 px by a decorator:
- **A quest's life:** Open → Taken → Done with a wait and the evidence read; declined while open; a conflict refused; Done
  held for your yes; a move recorded and not applied; a history given from before the log (Open → Done, no take).
- **A session's states:** working ⇄ waiting on you, with the outcomes as one group and the go-on edge's guards.
- **Parts:** two begun apart, a retry, a part carried on with no `after`, parts asked by another repository, and the
  landing plan as a prospective node.
- **A landing:** three tries held for one reason, then landed with an advance from another session, pushed, merged and
  cleaned up; a merge into the line with no branch kept.
- **A workflow:** today's steps in order, and its run lit; after WORKFLOW2, a group and a failure path.
- **A node's lifecycle:** the whole table, its reciprocal and parallel moves, the counters and the retained entries.
- **A dense graph:** `allOf` and `anyOf` groups at the bounds (24 authored nodes, their synthetic ones, 96 wires).
- **A run too large:** 24 steps and their synthetic nodes, folded around one current node, around three, and with none.
- **A grown instance:** before and after a node is appended, the focus kept on its node.
- **Edge cases:** an unknown state shown as recorded, long Chinese titles beside short English ones, *Show as list* at
  1200 px, and nothing measured (the rail).

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
  *Show whole* (显示全部), *n more tries*, *n steps done*, *this record keeps where it stands, not how it got there*; the
  choice *Show as list* (以列表显示) and *Show as flowchart* (以流程图显示); *recorded, not applied* (已记录，未生效) and
  *not recorded* (未记录); and the keys' line a reader hears.
  Every node and edge word is a state or a cause the catalogues already hold (a quest's statuses, a session's states, a
  run's states, how a part began, a try's codes). The new ones are a quest's moves (FLOWCHART1a2) and a node's states
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
2. **The picture jumps between two looks.** The same graph lays out the same way twice, and on an unfolded flow a change of
   the lit state, the path or the times moves no node; a fold's change is §3.6's exception, and a grown graph is laid out
   anew with the focus kept. *Test:* the layout of each fixture equal with and without them; the focus after an append.
3. **An edge runs through a node or a label, or crosses past its ceiling.** In a rendered browser (Playwright over the
   stories), every node's box and every label's box are measured and each edge's path is sampled along its length: no
   edge enters a box but its own ends', no label overlaps a node or another label, and each fixture's crossings stay at or
   under its table's ceiling. The fixtures are the hardest named graphs: the node lifecycle's multigraph, a dense `allOf`
   and `anyOf` graph at the bounds, unequal boxes in both languages, an instance before and after an append, several
   current nodes and none. *Test:* the measured-box check and the crossing table, run for the hand layout and for dagre,
   which decide the engine (§4.3); the unit test's slot crossings are only its first half, since jsdom measures nothing.
4. **The placement is wrong at a floor.** The placement table at 300, 400, 544 and 1200 px for one-, two- and three-wide
   ranks, with waypoints and return lanes counted (§3.2); unmeasured is the rail. *Test:* placement rows; *story:* each
   width.
5. **The drawing is read instead of the list.** The list is in reading order, and the SVG is `aria-hidden`. *Test:* the
   list items' order; the SVG's attribute.
6. **A reader cannot follow an edge.** Each node's description names every edge in and out. *Test:* the description's
   words, en and zh.
7. **Keys.** One Tab stop; ↓ ↑ → ← Home End Enter on the node alone; a foot line's press to its target; Escape back from
   a control; the focus kept by id across a fold and a resize; nothing acts while an input method composes. *Test:*
   `Flow.test.tsx` and `composingKeys.test.ts`; a screen reader's pass on the window (§3.7).
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
13. **A path or a payload reaches the page.** The moves answer §2.1's fields and no others, never the published body or a
    machine path (D47 §4). *Test:* the answer's fields by allow-list.
14. **Measuring loops.** Nothing measured changes a box (§4.2). *Test:* a resize redraws the SVG once and leaves each
    node's box unchanged.
15. **Shrunk to fit.** No `transform` scale and no `viewBox` over text. *Test:* the rendered markup.
16. **Too large.** A 96-node run renders folded with every current node in view (one, several, or none), each fold
    naming the edges across its boundary with their causes, and *Show whole* opens it. *Story* and *test*.
17. **Words cut or clashing.** Chinese titles wrap with no fixed width, and the glossary's collisions are caught.
    *Story:* long 中文; `i18n:check`, `names:check --strict`.
18. **An older host, or a teammate's record.** No moves read: the state machine is drawn with its current state lit and
    says the path is not kept. *Test:* the quest's flow with no moves.
19. **A drawing claims what its record does not keep.** A path where the record keeps none, a time it does not hold, a
    join or a concurrency for parts, a dated push, a traversed path for a node, a sequence for a snapshot. *Test:* each
    flow's test over its record's own shape: a session's node with no *since*, a part with no time, a landing's push with
    no date, a run with no taken edges between done steps, a node's retained entries listed and never lit as edges.
20. **A move's order or its application is guessed.** The quest's moves drawn in replay order, never by time; one not
    applied drawn inside its state; a take naming no session. *Test:* fixtures with a pending move after accepted ones,
    a move that did not apply, and a dismissal matched by its reference.
21. **Show as list missing where the drawing fits.** *Test:* the flow's test presses it at 1200 px and reads the rail, the
    choice kept.

## 8. Not built

**Rejected (D186):**
- A free canvas, stored positions, panning, zoom and dragging. They were D157's, and they stand.
- elkjs, by its size (half again the page) and its licence; measuring is no reason against it. dagre is not rejected: it
  is evaluated beside the hand layout on the same fixtures (§4.3).
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
- Positions kept from one layout to the next, so a grown instance kept its old places: they would be stored positions,
  which D157 rejected; the focus is kept by node id instead (§3.1).
- A node's traversed path, until a row records each move it makes (§2.6).
- A part's times and its agent, where the page holds no session record for it, and a landing's dated push: no record keeps
  them (§2.3, §2.7).
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
- The map design §1's *no graph library*, which this extends to flows unless FLOWCHART1a1's table chooses dagre (§4.3).
- D116's glossary, D142's content as written, D143's trace, and D173's names, whose collisions §6 hands to GRAPHEDIT1.

## 10. The rows

**The rows follow D187.** Each is one feature a person can see working, built end to end across every lane it needs by
one builder, and handed back working. The primitive is never a row of its own: the first feature builds the layout and
the component as far as it needs them, and each later feature adds the parts it needs (a back edge, a stack, a fold), each
with its own tests and stories. Only the first feature is too large for one builder, so it comes in two parts, and each
part works and is seen working. Each row names its Ask Daoris door or its reason (D110). These flows read and add no
press, so they need no `HelpCoverageTests` row. **Each row names its trap**: the mistake its builder is most likely to
make, which its proof must catch.

- [ ] **FLOWCHART1a1 — a quest's statuses drawn as a flowchart on its page** (web-shell; D186, D187; §2.1, §3, §4). The layout, the `Flow` component and its stories, *Show as list*, the keyboard and reader model (§3.7), the glossary's *flowchart*; the hardest fixtures (§7, trap 3: the node lifecycle's table, a dense `allOf`/`anyOf` graph, unequal boxes in both languages, an append, several current nodes and none) laid out by hand and by dagre and measured in a rendered browser, and the engine locked on that table, before any later row builds on it; on the quest's page a folded section draws the four statuses from `QuestTransitions` (held by a source test), the current one lit, the moves out of it said, the rail at the side bar's floor; a browser too. Opus. Proof: `flowLayout.test.ts` and `Flow.test.tsx` (placement at 300/400/544/1200 with lanes and waypoints counted, keys, reader), the measured-box check and the crossing table in a rendered browser, the quest's test en and zh, stories, `i18n:check`, `names:check --strict`; seen working on the window at 1546 and 680 px, both themes, with a screen reader's pass. *Trap:* taking slot crossings and jsdom's unmeasured rail as proof of the routing, and shipping no measured-box collision test (§4.2).
- [ ] **FLOWCHART1a2 — how a quest moved, drawn on its flowchart** (service, driver, web-shell; after 1a1; §2.1, traps 11-13, 18, 20). `GET /api/quests/{id}/moves` on both hosts from `HistoryAsync`, in its replay order, each move's fields by §2.1's allow-list with whether it applied and its reference; `TRACE`'s quest link and `daoris-driver trace quest` carry them; the census and `quest-operations.md` gain the place; the page draws the path as recorded, each move inside its status, one not applied inside its state, a conflict refused, a held Done in open's hue. Opus. Proof: the door's tests per kind, `QuestOperationKindsTests`, `TraceTests`, the page's tests en and zh; seen on the window. *Trap:* sorting the moves by time, drawing every logged operation as applied, guessing the taking session, or leaving out the references a dismissal matches its conflict by (`QuestLog.cs:104-109`).
- [ ] **FLOWCHART1b — a session's line of work and its states drawn as flowcharts** (web-shell; after 1a1; §2.2-§2.3, §3.5). The parts flow in the parts strip's place, the strip its rail, parts drawn as lineage and the landing plan as a prospective node; the *Timeline* view's head draws `SessionLedger.Allowed` (held by a source test), the current state lit with no *since*, the outcomes as one group node, the go-on edge with its guards, no path; the layout gains back edges on their own lanes through escape slots. Opus. Proof: tests en and zh, `PartsStrip`'s kept, stories; seen on the window. *Trap:* turning parts listed oldest first into a causal chain or a concurrency, drawing the landing plan as done, or letting the outcomes' group imply every outcome can go on (a stand-down cannot, `SessionLedger.cs:1245`).
- [ ] **FLOWCHART1c — a landed branch's landing and clean-up drawn as a flowchart** (driver, web-shell; after 1a1; §2.7, §3.6). The trace's branch gains its advances and its units (fields added beside the old); on the branch's page, opened from the work's milestone and the run's landing node: `WORK_STATE` by branch and its session's `TRACE`, tries stacked by code, landed with its advances and their sessions, pushed as a fact, the pull request's latest answer, a merge into the line from its gap, the end, the clean-up, the recovery; the layout gains stacks. Opus. Proof: `TraceTests` for the added fields; tests en and zh over each try code, stories; seen on the window. *Trap:* picking one unit or session for a shared landing branch, losing its advances, or drawing the push and the pull request's answers as dated events (`Trace.Chain.cs:627-644`).
- [ ] **FLOWCHART1d — a workflow and its run drawn as flowcharts** (web-shell; after 1a1; §2.4-§2.5, §3.6, §5.1). `WorkflowChart` on `Flow`: the Workflow tab and the side bar's Workflow view top to bottom where wide, the rail at the floor; today's steps in their order, failure paths, the landing's divider, the run lit with no path, each skip by whom the record names; groups as fork and join once WORKFLOW2's decoded graph is read; the layout gains settled folds, focus around every current node or none, folds that keep their boundary, and *Show whole*. Opus. Proof: the workflow tests' words unchanged, a 96-node run folded, stories; seen on the window. *Trap:* inventing groups today's answer does not hold (`workflow/current.ts:27-42`), saying *by you* on every skip (a record's skip exists, `NodeLifecycle.cs:270-272`), or folding around one current node when several are current or none is.
- [ ] **MAPUX1 — the Map's UI/UX, its own design** (design; the map design §1, D186 §5.5). The Map stays the map of requests and was judged poor: shots of the ring and the layers at 1546 and 680 px in both themes first, then what it answers that no page does, and its rows. Opus, with another maker's read. Proof: the document and its rows.

**Two rows change** (the parent's to write into TASKS):
- **FABRIC-GRAPHVIEW1** is drawn on `Flow` after FLOWCHART1d, in the side bar's Workflow view, with a node's lifecycle
  (§2.6) unfolding under it: its current state, its counters and its retained entries, and no traversed path unless the
  row records each move first. *Trap:* reading the retained history as the path the node took.
- **FABRIC-GRAPHEDIT1** lays its editor out by the flow's layout (FLOWCHART1a1), and settles §6's three name collisions.
