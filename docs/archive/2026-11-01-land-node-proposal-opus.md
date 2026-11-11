# LANDNODE1: landing as a performed node (the Opus maker's sealed proposal, 2026-11-01)

Read at `parent/batch-1031h` (`778f1f5d0`). Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

## 1. Diagnosis

Three actors share one landing, and the workflow is the one that does not act.

1. **A door performs it.** Work lands only in `TreeLands.LandAsync` (`Workplace/TreeLands.cs:401`), called by the person's presses (`Workplace/SessionTrees.cs:655`, `EntryLands.cs:337`, `TogetherAccepts.cs:105`) and by the look's `AutoLander` under the rule's `autoAccept` (`Workplace/AutoLander.cs:29-41`, `:131`, `:337`).
2. **The workflow observes.** `handed`, `pushed`, `pull-request` and `line` are observed (`Workflows/Nodes/NodeContract.cs:279-283`). Of the built-ins the runtime begins only observed ones (`NodeRuntime.cs:2055`); what it starts is plugins' nodes, `check`, `quest.close`, `integrate` and `request`, never a landing. It governs a branch unit only where a plugin's node, `integrate` or a closure arms it (`:456`, `:2062`).
3. **Current's branch graph is the rule, redrawn.** `NodeGraphHomes.Landing(rule)` derives the landing's nodes from the rule's form, plugin and `integrate` (`NodeGraphs.cs:885-894`), and every branch unit takes them (`:942`). A rule's `cycle` may neither declare a landing's kind nor wire into one (`:909-917`, `:68-72`). A branch unit has no revision until its end (`WorkOrders/Inventory/WorkInventory.cs:437`), so nothing binds it before it lands.
4. **Graph versions went halfway.** A version's `handed` carries `form`, `accept`, `pattern` and `plugin`, while `tidy`, `key` and `squash` stay the rule's (`Workflows/Domain/WorkflowGate.cs:139-163`). The runtime still only judges. A door lands where the record holds `handed` active (`docs/archive/2026-10-20-fabric-cutover-design.md:257-262`), and a press waits, within a bound, for a pass it asked for (`:283-288`). Two engines, kept together by a timeout.
5. **The precedent is built.** `integrate` is a waited node whose start runs a fence keyed by the unit and the tip (`Workflows/Nodes/IntegrateNode.cs:147-167`, `:29-30`). Its options are the unit's half of the policy; the line's half stays on the rule (`docs/2026-10-28-integration-design.md:1675-1681`, `Settings/LandingKey.cs:88-90`).

No node performs the flow's most consequential act; its options live in a rule (`LandingKey.cs:48-70`) the graph only reflects.

## 2. The design

### 2.1 The node: `land` (落地)

`land` is a new built-in kind, reach **performed**, with Daoris as its executor, like `check` and `quest.close`.
- Its subject is the session's branch at its tip. It is **reached** when the landing record holds that tip, the evidence `handed` reads (`BuiltInNodes.cs:125-151`), and **failed** by the landing's own refusal (a conflict, a standing branch, uncommitted work, a plugin refused).
- It is **never skipped**, since a skip would read as landed. The person's doors are Accept, stop and *Try again*.

| Option | Values | When unset |
|---|---|---|
| `method` | `merge`, `branch`, `push`, `pull-request`, `integrate` | the rule's form, plugin and `integrate` |
| `accept` | `you`, `automatic` | the rule's `autoAccept` |
| `target` | a line the repository declares | the line the tree grew from |
| `pattern` | the placeholders of `LandingKey.cs:162` | the repository's, with its `key` |
| `plugin` | one declaring `work/land`, or `none` | the repository's |
| `squash` | true or false | the rule's |
| `message` | a template over `{title}`, `{quest}`, `{ticket}`, `{session}` | today's (`SquashMessage.Of`, git's merge message) |

**The method brings its tail.** `pull-request` adds `pushed` then `pull-request`; `push` adds `pushed`; `integrate` adds an `integrate` node, which keeps its own `request` and `conflicts` (the integration design's §17.4, cited below by section); `merge` and `branch` add nothing. The editor changes the tail with the method, in one edit.

**The line keeps** `commit`, `batch`, `order`, `gates`, `unconfirmed`, `overlap` and the promotion fence, beside the repository's `integration` facts: the node chooses *which* line. Setup keeps `key`, `tidy`, the installed plugins and the defaults.

**What precedes it.** Wires bring `work.requests`, `opinion`, `look`, `go-ahead`, `human` or `check` before `land`, in the protected order. The quest's holds, the content holds and D88's proof stay the gate's first parts, judged inside the start at the bound tip. `accept: automatic` is refused on `merge` (D145), and on `integrate` except under `request: done` with gates declared and accepted (§17.4).

### 2.2 How it performs

**One performer, two starters.**
- Under `accept: automatic`, the runtime starts `land` once its wires hold, its subject is due and the gate holds nothing.
- Under `accept: you`, the person's Accept is the last precondition. The press keeps the word on the unit's record, as a held attempt's word is kept (the cutover design's §3, *One writer*), asks for a whole pass and waits within its §4 bound; past it, it says *accepted; landing*.
- The Accept is a Person route that no agent, role or standing confirmation writes. Under `automatic` it may still start first, as today.

**The fence**, as `IntegrateNode`'s:
1. One locked edit judges the start and writes the attempt, its key (an `lk-` digest of unit, bound tip, method and target) and the tip.
2. Off the pass, as `AutoLander.LandAsync` runs today, `TreeLands.LandAsync` lands, pinned to the tip, `AcceptedBy` from the start's cause. It runs under LANDRACE1's hold on the tree (`TreeLands.cs:408-413`), so a press and a pass never land one tree at once. The plugin is told the key.
3. The answer is kept on the attempt, only while it still waits.

**A restart settles by postconditions**, read in order before any new start:
1. The landing record holds or squashed the tip, or the line holds it by a merge: the node is reached.
2. The branch was made and its push held back: it is handed on (FABRIC-CLOSE1h2's path), never made again.
3. The plugin's answer was lost: the remote-tracking ref and `work/state` are read before it is asked again, with the same key.
4. Nothing was made and the tree is still at the tip: the attempt is withdrawn, and the node is judged again.
5. Anything else: unknown, said, for the person to settle.

A new commit invalidates the node, and an unreconciled attempt blocks a new one (§17.4, F41). Records, keys and attempts stay in the home.

### 2.3 What becomes of the landing rule

Nothing is migrated, and the file keeps its shape. The rule is read three ways:
- **As Current's `land` options, live:** `NodeGraphHomes.Landing(rule)` draws `land` and its tail, each option saying its source; editing them writes the rule through `SET_LANDING`.
- **As the defaults** a version's `land` falls back to, as `WorkflowGate.Of` reads them today.
- **As the line's policy:** the line's half of `integrate`.

The only conversion is *Save as a workflow…*, the person's press writing the rule's values into a version's `land`.

### 2.4 The cut-over

- **Current's units cut over at the upgrade**, since Current is read live (D157 point 6). An open `AutoLandings` entry is the start condition under `automatic`; a kept Accept (`CarriedAccepts`) is the person's word already given; a unit already landed is reached by observation. The runtime arms only while a due entry or a kept word is open, and reads only their trees' tips, so an install with nothing due, or a repository that is not driven, costs nothing.
- **Bound runs keep their versions.** Schema-1 runs, and schema-2 runs whose version holds `handed`, land through today's doors until they end.
- **Only new saves hold `land`**, the graph presets included.
- **Older builds** read the rule and land by their door. A version holding `land` reads there as an unknown kind, so its run is held whole (§17.6 point 6). If an older build lands a unit the newer build had attempted, the newer build's reconcile finds the landing record, so nothing lands twice.

### 2.5 Graph rules

- **`landing-kind`** now reads "the graph holds one landing; under Current, the rule's `land`". A cycle may not declare `land`.
- **`into-landing`** stands: composing steps before the landing needs a version.
- **New rules:** `land-once` (one `land` or one `handed`); `land-tail` (the method matches its tail, replacing `integrate-plugin`); `land-automatic` (the refusals of §2.1).
- **Existing rules** `before-landing`, `integrate-unlanded`, `work-requests-late`, `protected-order` and `protected-wire` count `land` as a landing.

### 2.6 What changes, and what it reverses

- **D215's split stands, with its second owner renamed.** The workflow owns how this unit lands: method, line and options. **The line**, no longer "the landing rule", owns batching, order, gates and promotion. Point 4's *by the person's Accept or the rule's `autoAccept`* becomes *by its `land` node's start*. Its refusal beside a plugin moves to `land-tail`.
- **The cutover design's `:21`** and its table's first two rows are reversed for Current: the runtime performs the `land` of Current's graph. The schema-1 rows stand, and §4's door holds only for `handed` versions.
- **D157 point 4, in part:** `squash` and the message become a step's options.
- **D145 and D87** keep their meaning; the act moves from the look and the press into the node.

**Why.** One engine judges and acts: the look's second judgement retires for new work, a restart reads an attempt, and the landing is drawn where the person reads the flow.

### 2.7 Routes, data and the UI

- **Data:** `NodeKinds.Land` and its authority; `LandNodes` (options, defaults); `ILandingDoor` over `SessionTrees`; the `work/land` frame gains `key` and `mode`, `push` offered only to a plugin declaring it.
- **Routes:** `LAND_SESSION_TREE`, `LAND_WITHOUT_LOOK`, `LAND_ENTRY`, `TOGETHER_PRESS` and `trees land` write the word and wait; `SET_LANDING` gains `message` beside row 1d's `integrate`; `WORKFLOW_NODE`'s *Try again* reaches a failed `land`.
- **AGENTSPLACE1:** its §3 *Landing* row **is** `land`'s options editor (`LandOptions`). Under Current, Save is `SET_LANDING`; under a version, *Edit steps* (row 1i) opens the same editor and Save makes a new version. The *Line* row shows the line's policy, so D215's owners stay visible and apart.
- **Its delivery presets (§4)** are `land` options applied to Current, and `SetupPresets` (row 1a2) matches a place by its effective `land`:

  | Preset | `land` options |
  |---|---|
  | *Merge after you accept* | `merge`, `you` |
  | *A branch to push* | `branch`, `you`, `plugin: none` |
  | *A pull request* | `pull-request`, the platform's plugin |
  | *Integrated into the line* | `integrate`, `you`, `request: owner`, plus the line's policy |

- **The chart and the run view** draw `land`'s states, in English and Chinese chrome (落地, 合并, 推送, 拉取请求, 集成).

## 3. What I reject

- **`handed` made performed, or `handed { perform: true }`.** Saved versions would change meaning, and an option is opaque to an older build, which would land by its door beside the runtime (§17.6 point 8).
- **Each rule migrated into a saved version.** It rewrites the person's file, gives undriven repositories versions nobody chose, and older builds read none of it (D157 point 7).
- **The press calling the performer directly:** two writers of one record.
- **Batch, order and gates on the node.** One candidate holds units of several flows (D215 point 5).
- **`integrate` folded into `land`.** They have two authorities, and the person reads two waits: the branch made, then promoted.
- **`AutoLander` kept as the performer, with a node drawn over it.** That is the split the report names.
- **Current's cycle wired into its landing** (§2.5): Current is derived from keys and has no editor for what precedes it.

## 4. Rows

Each row writes its own decision note and translations.

1. **LANDNODE1a, the kind and rules** (driver, pure): `land`, its authority, options, checks and §2.5's rules. *Proof:* a test per refusal; an older table reads `land` as unknown; stored versions read unchanged.
2. **1b, the performer** (driver): the fence over `ILandingDoor`, the key, the reconcile ladder. After a. *Proof:* a stop at each boundary ends in one branch, push and pull request, counted by a stand-in plugin; a press and a pass at once land once.
3. **1c, Current draws `land`** (driver): the derived node and tail; the pre-landing revision for due units; bounded arming; `AutoLander` choosing only for `handed` runs. After b. *Proof:* `AutoLandingRulesTests` and `LandingTests` through the node; nothing due, nothing read; due entries and kept Accepts adopted.
4. **1d, the person's doors** (modules): each Accept writes the word and waits; the census marks it Person. After c. *Proof:* `bridge-presses.md`'s places; a role's write refused.
5. **1e, the plugin frame** (driver, kit, examples): `key`, `mode` and `push`; the reconcile reads before it asks again. After b. *Proof:* kit tests and both example landing plugins.
6. **1f, the message** (driver): the template on the merge and squash commits, the maker's line kept. After a. *Proof:* message tests.
7. **1g, presets, *Save as a workflow…* and lowering** (driver): the presets hold `land`; `--from current` writes it out; `Lowers` gains `accept` to `automatic`. After a. *Proof:* preset fixtures and the lowering rows.
8. **1h, the editor and chart** (web): `LandOptions` for the Landing row and *Edit steps*; `land`'s states drawn. After d, and before AGENTSPLACE1 1h and 1i. *Proof:* page tests; stories at 80, 39 and 25rem, en and zh, both themes.
9. **1i, the installed window** (the steward): one repository per method, a restart mid-landing, a rollback. After h.

## 5. Risks and open questions

- **One Accept carrying several sessions** (an entry's, LINESTOGETHER1): the first unit performs, and the others must be reached as *brought into*, with no second fence.
- **Tree tips read at each pass** for due units: measure it under many trees.
- **A two-minute plugin behind a bounded press:** the person may see *landing* before the pull request's link.
- **A `target` other than the tree's base:** allow it for `integrate` only, at first?
- **Open:** whether `tidy` becomes the node's; whether a terminal with no loop runs the fence itself.
