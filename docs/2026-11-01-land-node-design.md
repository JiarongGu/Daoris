# LANDNODE1: landing a performed, configurable node of the workflow (round 1 synthesis, 2026-11-01)

**Standing:** the steward's synthesis of round 1's two sealed proposals:
- the Opus maker's, `2026-11-01-land-node-proposal-opus.md`;
- Codex's, `2026-11-01-land-node-proposal-codex.md`.

Both answer `2026-11-01-land-node-brief.md`. The synthesis waits for both makers' reads (D191).

## 1. What both found

Three actors share one landing, and the workflow is the one that does not act.
- **A door performs the landing.** It happens only in `TreeLands.LandAsync` (`Workplace/TreeLands.cs:401`), called by the person's presses and by the look's `AutoLander` under the rule's `autoAccept` (`AutoLander.cs:29-41`, `:337`).
- **The workflow only observes.** Its `handed`, `pushed`, `pull-request` and `line` nodes are observed (`NodeContract.cs:279-283`), and the runtime starts no landing (`NodeRuntime.cs:2055`).
- **Current redraws the rule.** Current's branch graph is the rule, redrawn (`NodeGraphs.cs:885-894`), and a rule's cycle may neither declare a landing's kind nor wire into one (`:909-917`).
- **Graph versions went halfway.** A version's `handed` carries `form`, `accept`, `pattern` and `plugin`, while `tidy`, `key` and `squash` stay the rule's (`Workflows/Domain/WorkflowGate.cs:139-163`). Even there, a door lands and the runtime judges, held together by a press's bounded wait (the cutover design's `:257-262`, `:283-288`).
- **The precedent is built:** `integrate` is performed behind a fence keyed by unit and tip (`IntegrateNode.cs:147-167`).
- **A defect beside it (Codex):** a candidate takes its batching from its first request's policy (`IntegrationReplays.cs:137`), so a later change to the line's policy can make one request govern members queued under another.

## 2. The node: `land` (落地)

A new built-in kind, reach **performed**, with Daoris as its executor, like `check` and `quest.close`.
- The observed kinds keep their meanings. Making `handed` performed is rejected by both, since saved versions would change meaning and an older build would land beside it.
- `land` is reached when the landing record holds the bound tip (the evidence `handed` reads, `BuiltInNodes.cs:125-151`), and failed by the landing's own refusal.
- It is never skipped, since a skip would read as landed. The person's doors are Accept, stop and *Try again*.

| Option | Values |
|---|---|
| `method` | `merge`, `branch`, `push`, `pull-request`, `integrate` |
| `accept` | `you`, `automatic` |
| `target` | a line the repository declares; for now another than the tree's base only for `integrate` (the Opus open question, taken) |
| `pattern` | `LandingKey`'s placeholders and validation, previewed expanded |
| `plugin` | one declaring `work/land`, or `none`; a pull request needs one; credentials stay machine-local |
| `squash` | for `merge` and `branch`; refused for `integrate`, whose history is the line's |
| `message` | a template over `{title}`, `{quest}`, `{ticket}`, `{session}`, frozen before the landing; it applies only to the commits the landing makes |

**The method brings its tail,** edited in one change:
- `pull-request` adds `pushed` then `pull-request`, whose observer still waits for the platform's completion (D215);
- `push` adds `pushed`;
- `integrate` adds an `integrate` node with its own `request` and `conflicts`;
- `merge` and `branch` add nothing.

**What stays the line's:** history shape (`commit`), `batch`, `order`, `gates`, `unconfirmed`, `overlap` and the promotion fence. One candidate holds units of several flows, so these cannot be the node's. The node chooses *which* line, and asks the line's integration under its policy. Setup keeps `key`, `tidy`, the installed plugins and the defaults.

**What precedes it:** `work.requests`, `opinion`, `look`, `go-ahead`, `human` or `check`, wired in D157's protected order. The quest's holds, the content holds and D88's proof stay the gate's first parts, judged inside the start at the bound tip.
- `accept: automatic` is refused on `merge` (D145).
- On `integrate`, it is refused except under `request: done` with the gates declared and accepted.
- Lowering to `automatic` counts in `InvolvementRoutes.Lowers`.

## 3. How it performs

**One performer, two starters.**
- Under `accept: automatic`, the runtime starts `land` once its wires hold, its subject is due and the gate holds nothing.
- Under `accept: you`, the person's Accept is the last precondition. The press keeps the word on the unit's record (a Person route that no agent, role or standing confirmation writes), asks for a whole pass, and waits within its bound. Past the bound it says *accepted; landing*.
- The press never calls the performer: one writer of the record.

**The fence**, as `IntegrateNode`'s:
1. One locked edit judges the start and writes the attempt. Its **delivery key** is a digest of the unit, the bound tip, the method and the target. An unreconciled attempt blocks a new one, and a new tip or method makes a new delivery only after the old one settles (both).
2. The attempt records each **phase's** outcome as it happens: prepared, branch made, pushed, plugin answered, integration admitted, settled (Codex's phases on the Opus attempt).
3. Off the pass, `TreeLands.LandAsync` lands from the attempt's **resolved intent**: the line, the expanded branch name, the rendered message, the plugin and the integration's policy reference. It is pinned to the tip, under LANDRACE1's hold on the tree, so a press and a pass never land one tree at once. A new landing reads no live rule to choose its method.
   - Branch refs it makes go through the git foundation's `RefTransaction` with the act journal's ownership mark (D204), so a ref and its record cannot part (Codex).
   - The plugin is told the delivery key.

**A restart settles by postconditions, read in order before any new start:**
1. the landing record or the line holds the tip: the node is reached;
2. the branch was made and its push held back: it is handed on, never made again;
3. the plugin's answer was lost: the remote-tracking ref and `work/state` are read, and only then asked again with the same key;
4. nothing was made and the tree is still at the tip: the attempt is withdrawn and judged again;
5. anything else: **unknown**, held and said for the person to settle.

Neither a timeout nor another Accept authorizes repeating an uncertain effect, and a legacy plugin that declares no key is never retried blind. Stopping prevents further phases; it never deletes delivered work or reverses a promotion.

## 4. The landing rule, and the cut-over (decided: Opus's, with Codex's frozen versions)

- **Nothing is migrated.** The rule keeps its shape and is read three ways:
  1. as **Current's `land` options, live**: Current is read live (D157 point 6), and editing them writes the rule through `SET_LANDING`;
  2. as the **defaults a new version is saved from**;
  3. as **the line's policy**.
- **A saved version resolves every option at its save** and keeps no live fallback, so a later rule change never moves a bound version (Codex's objection to runtime inheritance, taken). *Save as a workflow…* writes Current's values into a version.
- **Current's units cut over at the upgrade.**
  - An open `AutoLandings` entry is the start condition under `automatic`.
  - A kept Accept (`CarriedAccepts`) is the person's word already given.
  - A unit already landed is reached by observation.
  - The runtime arms only while a due entry or a kept word is open, and reads only their trees' tips, so an install with nothing due, and a repository that is not driven, costs nothing.
- **Bound runs keep their versions.** Schema-1 runs, and schema-2 runs whose version holds `handed`, land through today's doors until they end.
- **Only new saves hold `land`,** the graph presets included.
- **Older builds** read the rule and land Current by their door. A version holding `land` is an unknown kind there, held whole. A unit an older build landed is found by the newer build's reconcile, so nothing lands twice.
- **Codex's staged per-repository conversion is not taken:** there is nothing to convert. Its proof that an older build and a newer one meet on one unit without a second landing is taken into row 1c.

## 5. The line's policy (from Codex, narrowed)

- **Admission freezes the line's policy as an epoch** with the accepted declaration's blob. A candidate combines only members of one epoch, and a later change of the line's policy starts a new one.
- **A node's `merge` cannot bypass a line whose policy requires gated integration.**
- **Not taken now:** moving the line's policy out of the landing rule into a desktop-owned store keyed by repository and line. It stays the rule's `integrate` keys, shown on AGENTSPLACE1's *Line* row, until a row shows the store is needed.

## 6. Graph rules and what this reverses

- **`landing-kind`:** reworded to "the graph holds one landing; under Current, the rule's `land`". A cycle may not declare `land`.
- **`into-landing`:** stands.
- **New:** `land-once` (one `land` or one `handed`); `land-tail` (the method matches its tail, replacing `integrate-plugin`); `land-automatic` (§2's refusals).
- **Existing:** `before-landing`, `integrate-unlanded`, `work-requests-late`, `protected-order` and `protected-wire` count `land` as a landing.
- **Verification graphs** hold no landing performer.

**Reversed or amended**, each by its row's note:
- **The cutover design's `:21`** and its table's first two rows, for Current: the runtime performs Current's `land`. The schema-1 rows stand.
- **D215's split** stands with its second owner renamed: the workflow owns how this unit lands; **the line**, not "the landing rule", owns batching, order, gates and promotion. Its point 4's "by the person's Accept or the rule's `autoAccept`" becomes "by its `land` node's start".
- **D157 point 4, in part:** `squash` and the message become a step's options.
- **D100:** explicit `push` and `pull-request` methods, now that one performer shares the branch's preparation; the platform's acts stay the plugins'.
- **D87 and D145** keep their meaning; the act moves from the look and the press into the node.

## 7. Routes, data and the UI

- **Data:**
  - `NodeKinds.Land` and its authority;
  - `LandNodes` (options, defaults);
  - `ILandingDoor` over `SessionTrees`;
  - the attempt's phases;
  - the `work/land` frame gains `key` and `mode`, with `push` offered only to a plugin that declares it.
- **Routes:**
  - `LAND_SESSION_TREE`, `LAND_WITHOUT_LOOK`, `LAND_ENTRY`, `TOGETHER_PRESS` and `trees land` write the word and wait;
  - `LANDING` previews the bound intent, with additive fields for *authorized*, *running* and *completed*;
  - `SET_LANDING` gains `message` beside AGENTSPLACE1 row 1d's `integrate`;
  - `WORKFLOW_NODE`'s *Try again* reaches a failed `land`.
- **AGENTSPLACE1:**
  - its Workflow section's *Landing* row **is** `land`'s options editor (`LandOptions`). Under Current, Save is `SET_LANDING`; under a version, *Edit steps* (its row 1i) opens the same editor, and Save makes a new version;
  - the *Line* row shows the line's policy, so D215's two owners stay visible and apart;
  - its delivery presets are `land` options: *Merge after you accept* `merge`/`you`; *A branch to push* `branch`/`you`/`plugin: none`; *A pull request* `pull-request` with the platform's plugin; *Integrated into the line* `integrate`/`you`/`request: owner`, plus the line's policy;
  - `SetupPresets` (its row 1a2) matches a place by its effective `land`.
- **Words:** 落地, 合并, 推送, 拉取请求 and 集成, in English and Chinese chrome; an authored message stays as written.

## 8. Rows

Each row writes its own decision note and translations.

1. **LANDNODE1a, the kind and its rules** (driver): `land`, its authority, options and checks, and §6's rules.
   - Proof: a test per refusal; an older table reads `land` as unknown; stored versions read unchanged.
2. **1b, the performer** (driver): the fence over `ILandingDoor`, the delivery key, the phases, the `RefTransaction` writes, and the reconcile ladder.
   - Proof: a stop at each boundary ends in one branch, push and pull request, counted by a stand-in plugin; a press and a pass at once land once.
   - After a.
3. **1c, Current performs** (driver): Current draws `land` and its tail; the pre-landing revision for due units; bounded arming; `AutoLander` choosing only for `handed` runs; an older build's landing found by the reconcile.
   - Proof: `AutoLandingRulesTests` and `LandingTests` through the node; nothing due, nothing read; due entries and kept Accepts adopted.
   - After b.
4. **1d, the person's doors** (modules): each Accept writes the word and waits; `LANDING`'s added fields; the census marks it Person.
   - Proof: `bridge-presses.md`'s places, and a role's write refused.
   - After c.
5. **1e, the plugin frame** (driver, kit, examples): `key`, `mode` and `push`; the reconcile reads before it asks again; a keyless plugin is never retried blind.
   - Proof: the kit's tests and both example landing plugins.
   - After b.
6. **1f, the message** (driver): the template on the commits the landing makes, the maker's line kept.
   - Proof: message tests.
   - After a.
7. **1g, the line's epoch** (driver): admission freezes the policy and the declaration's blob; candidates of one epoch; `merge` cannot bypass gated integration.
   - Proof: two epochs never batch; a policy change mid-queue.
8. **1h, presets, *Save as a workflow…* and lowering** (driver): presets hold `land`; versions resolve at save; `--from current` writes it out; `Lowers` gains `accept` to `automatic`.
   - Proof: preset fixtures and the lowering rows.
   - After a.
9. **1i, the editor and the chart** (web): `LandOptions` for AGENTSPLACE1's Landing row and *Edit steps*; `land`'s states drawn.
   - Proof: page tests; stories at 80, 39 and 25rem, en and zh, both themes.
   - After d, and before AGENTSPLACE1 1h and 1i.
10. **1j, the installed window** (the steward): one repository per method, a restart mid-landing, a lost plugin answer, and an older build's landing.
    - After i.

## 9. Open

- **One Accept carrying several sessions** (an entry's, LINESTOGETHER1): the first unit performs, and the others are reached as *brought into*, with no second fence.
- **Tree tips read at each pass for due units:** measure under many trees.
- **A plugin taking minutes behind a bounded press:** the person may see *landing* before the pull request's link.
- **Undecided:** whether `tidy` becomes the node's, and whether a terminal with no loop runs the fence itself.
