# LANDNODE1: landing a performed, configurable node of the workflow (accepted 2026-11-01)

**Standing:** accepted under D191 on 2026-11-01. Round 1's two sealed proposals were:
- the Opus maker's, `2026-11-01-land-node-proposal-opus.md`;
- Codex's, `2026-11-01-land-node-proposal-codex.md`.

Both answer `2026-11-01-land-node-brief.md`. The steward synthesized them, both makers read the synthesis and accepted it with named changes, and the steward settled those changes in `2026-11-01-land-node-reads.md`. This text is the design as settled.

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
- **The workflow decides** (the owner's direction, 2026-11-01, D145's note). `land` has no `accept` option. Review is a step of its own, a review node before `land`, whose completion is the person's Accept. `land` performs once its incoming wires hold and completes at its end, a merge into the line included. No rule outside the workflow refuses what it is configured to do.
- Removing a review step counts in `InvolvementRoutes.Lowers`, so the plan that removes it says so.
- What still judges a landing outside the graph (the quest's holds, the content holds, D88's proof) becomes configured fabric steps (LANDNODE1k).

## 3. How it performs

**One performer, one starter.**
- The runtime starts `land` once its incoming wires hold, its subject is due and the gate holds nothing.
- Review, where a workflow wants it, is a review step before `land`. The person's Accept completes that step: the press keeps the word on the unit's record (a Person route that no agent, role or standing confirmation writes), asks for a whole pass, and waits within its bound. Past the bound it says *accepted; landing*.
- The press never calls the performer: one writer of the record.

**What authorization freezes.**
- The person's Accept is recorded against the unit, its revision, the graph's digest, the resolved options and the prerequisites' receipts.
- Standing automatic authority is judged again just before the first effect, as `TreeLands` reads the switch again today (`TreeLands.cs:565`).
- A carried Accept keeps its bounded repair meaning. It authorizes a session's clean-up continuation and waits while that runs; it is not a revision-bound approval (`CarriedAccepts.cs:7`).

**The fence**, as `IntegrateNode`'s:
1. One locked edit judges the start and writes the attempt.
   - Its **delivery key** is a digest of the unit, the bound tip, the method and the target.
   - An unreconciled attempt blocks a new one, and a new tip or method makes a new delivery only after the old one settles.
2. The attempt's **delivery record** holds the resolved intent and its phases.
   - The intent is the line, the expanded branch name with its ticket key, the rendered message, the plugin, and the policy epoch.
   - The record lives in a desktop-store table of its own, `deliveries`, written under a raised writer stamp, outside the node record that an older writer rewrites whole (`NodeRecordFiles.cs:345`).
3. **Each effect is written ahead, and its outcome after.** The phases are: prepared, ref written, recorded, plugin dispatched, plugin answered. Integration's admission and settlement stay the following `integrate` node's own attempt.
4. **Off the pass, `TreeLands.LandAsync` lands from that intent,** pinned to the tip, under GITLOCK's checkout lock and LANDRACE1's hold on the tree, so a press and a pass never land one tree at once.
   - Its branch refs go through `RefTransaction`; the landing's ownership is its landing record.
   - A new landing reads no live rule to choose its method.
   - The plugin is told the delivery key.

**When `land` is reached:** when its method's own effects are recorded.
- For `merge` and `branch`, the landing record. That record proves the branch's preparation only, since it is written before any plugin runs (`TreeLands.cs:672`, `:759`).
- For `push` and `pull-request`, the plugin's answer recorded. The `pull-request` observer then waits for the platform's completion.

**A restart settles by phase, before any new start:**
1. **The phase's outcome is recorded:** go on from the next phase.
2. **A ref's write-ahead entry stands without its outcome:** the ref and the landing record are read. Where they hold the intended tip, the phase is recorded done; where they hold nothing, the phase is withdrawn.
3. **A plugin dispatch stands without its answer:** `work/state` is asked with the key. A remote-tracking ref proves a push and never a pull request. A plugin that declares no key is reconciled by reading alone, and is sent again only by the person's `HANDOFF`.
4. **Anything else:** **unknown**, held and said for the person to settle.

Neither a timeout nor another Accept authorizes repeating an uncertain effect. Stopping prevents further phases; it never deletes delivered work or reverses a promotion.

## 4. The landing rule, and the cut-over

- **Nothing is migrated.** The rule keeps its shape and is read three ways:
  1. as **Current's `land` options, live until authorized**: Current is read live (D157 point 6), and editing them writes the rule through `SET_LANDING`;
  2. as the **repository's settings a version's open options resolve over**;
  3. as **the line's policy**.
  - Codex's staged per-repository conversion is not taken: there is nothing to convert.
- **A version's options are fixed at the bind.**
  - A version may leave `pattern`, `plugin` and the ticket key to the repository, so a workflow chosen for a whole workspace still serves each repository (D157 §2.3; the integration design's `:1645-1650`).
  - The run's binding resolves them over the repository's settings once, at the bind, and keeps them, so no later rule change reaches a bound run.
  - *Save as a workflow…* writes Current's values into a version.
- **Current has no `push`.** It derives `pull-request` from a branch rule naming a plugin and never derives `push`, so today's rule round-trips unchanged; `push` exists only in versions.
- **Old cycles keep wiring.** Current's `land` keeps the derived landing node's identifier, so a custom cycle's wires and evidence bindings to it still resolve. The mapping is specified and tested.
- **Current's units cut over in one row,** with the person's doors, after every protection it relies on: the records, the git adapter, the reconciliation, the plugin's key, the epoch, and the bind-time resolution.
  - An open `AutoLandings` entry is the start condition under `automatic`.
  - A kept Accept (`CarriedAccepts`) is the person's word already given, with its repair meaning.
  - A unit already landed is reached by observation.
  - The runtime arms only while a due entry or a kept word is open, and reads only their trees' tips, so an install with nothing due, and a repository that is not driven, costs nothing.
- **Bound runs keep their versions.** Schema-1 runs, and schema-2 runs whose version holds `handed`, land through today's doors until they end.
- **Only new saves hold `land`,** the graph presets included.
- **Older builds:**
  - a version holding `land` is an unknown kind there, held whole;
  - the raised writer stamp keeps their writes out of the delivery records' store;
  - a rollback is refused while any delivery is unresolved (the store's rollback window, the foundation design §13.5), so an older door never calls a plugin without the key behind a newer attempt.

## 5. The line's policy

- **The epoch.** It is the resolved shared policy plus the accepted declaration's blob, excluding the unit's own `request` and `conflicts`.
  - It is kept on requests and candidates.
  - Candidate formation enforces it as equality, inside the formation's transaction (`IntegrationStore.cs:307`).
  - Promotion runs its gates from that frozen declaration, not the current configuration (`IntegrationPromotes.cs:274`).
  - This is a fix of its own, since today a candidate takes its batching from its first request alone (`IntegrationReplays.cs:137`). It waits on nothing.
- **Each selectable target resolves to exactly one policy;** a target with none is refused.
- **`merge` is refused** at the choice and again at the start, for a line whose `integrate` says `gates: declared` with an accepted declaration naming a gate.
- **Not taken now:** moving the line's policy out of the landing rule into a desktop-owned store keyed by repository and line. It stays the rule's `integrate` keys, shown on AGENTSPLACE1's *Line* row, until a row shows the store is needed.

## 6. Graph rules and what this reverses

- **`landing-kind`:** reworded to "the graph holds one landing; under Current, the rule's `land`". A cycle may not declare `land`.
- **`into-landing`:** stands.
- **New:** `land-once` (one `land` or one `handed`); `land-tail` (the method matches its tail, replacing `integrate-plugin`). The `land-automatic` refusals were withdrawn on 2026-11-01: the workflow decides.
- **Existing:** `before-landing`, `integrate-unlanded`, `work-requests-late`, `protected-order` and `protected-wire` count `land` as a landing.
- **Verification graphs** hold no landing performer.

**Reversed or amended**, each by its row's note:
- **The cutover design's `:21`** and its table's first two rows, for Current: the runtime performs Current's `land`. The schema-1 rows stand.
- **D215's split** stands with its second owner renamed: the workflow owns how this unit lands; **the line**, not "the landing rule", owns batching, order, gates and promotion. Its point 4's "by the person's Accept or the rule's `autoAccept`" becomes "by its `land` node's start".
- **D157 point 4, in part:** `squash` and the message become a step's options.
- **D100:** explicit `push` and `pull-request` methods in versions, now that one performer shares the branch's preparation. The platform's acts stay the plugins', and D87's "Daoris itself never pushes" stands unchanged.
- **D87 and D145** keep their meaning; the act moves from the look and the press into the node.

## 7. Routes, data and the UI

- **Data:**
  - `NodeKinds.Land` and its authority;
  - `LandNodes` (options, defaults);
  - `ILandingDoor` over `SessionTrees`;
  - the `deliveries` table and its phases;
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

Each row writes its own decision note and translations, and names its proofs of older readers and writers and of restarts.

1. **LANDNODE1a, the kind and its rules** (driver): `land`, its authority, options and checks, and §6's rules.
   - Proof: a test per refusal; an older table reads `land` as unknown; stored versions read unchanged.
2. **1g, the line's epoch** (driver; a fix, waiting on nothing): §5's epoch kept on requests and candidates, enforced in formation, gates run from the frozen declaration; one policy per target. A FIX-LOG entry.
   - Proof: mixed epochs never batch; a policy change mid-queue; promotion's gates from the frozen blob.
3. **1b, the delivery record and its intent** (driver, store): the `deliveries` table under the raised writer stamp, the delivery key, the frozen intent with its authorization, and the write-ahead phase entries; the rollback refused while one is unresolved.
   - Proof: concurrent starts make one delivery; an older writer's store write refused; a rollback refused with one open.
   - After a.
4. **1b2, the git adapter** (driver): `TreeLands` from the explicit intent; `RefTransaction` under GITLOCK and LANDRACE1.
   - Proof: moved tips, collisions, squash, containment, and a ref written before its record across a crash.
   - After b.
5. **1b3, the reconciliation** (driver): §3's ladder for the git phases; an Accept carrying several sessions, the first performing and the others reached as *brought into*.
   - Proof: a stop at each git boundary ends in one branch; a press and a pass at once land once.
   - After b2.
6. **1e, the plugin frame and its reconciliation** (driver, kit, examples): `key` and `mode`; `work/state` asked with the key; a keyless plugin sent again only by `HANDOFF`.
   - Proof: duplicate keys, a lost reply, and success before the reply make no second effect; the kit's tests and both example landing plugins.
   - After b.
7. **1f, the message** (driver): the template on the commits the landing makes, the maker's line kept.
   - Proof: message tests.
   - After a.
8. **1h, presets, the bind, *Save as a workflow…* and lowering** (driver): presets hold `land`; a version's open options and its ticket key resolved at the bind; `--from current` writes Current out; `Lowers` gains `accept` to `automatic`; automatic authority judged again before the first effect.
   - Proof: preset fixtures, a live rule edited after the bind, and the lowering rows.
   - After a.
9. **1c, Current drawn** (driver): Current's `land` and its tail, keeping the derived landing node's identifier for old cycles, still inert.
   - Proof: custom cycles' wires and bindings resolve; `AutoLandingRulesTests` read unchanged.
   - After a.
10. **1c2, Current performs, with the person's doors** (driver, modules):
    - `AutoLander`'s choosing retired for Current; due entries and kept Accepts adopted with their meanings;
    - every Accept path writes the word and waits, `LANDING`'s added fields, and the census marking it Person;
    - a terminal with no loop.
    - Proof: `AutoLandingRulesTests` and `LandingTests` through the node; nothing due, nothing read; `bridge-presses.md`'s places; a role's write refused; a downgrade refused with a delivery open.
    - After b3, c, e, g and h.
11. **1i, the editor and the chart** (web): `LandOptions` for AGENTSPLACE1's Landing row and *Edit steps*; `land`'s states drawn.
    - Proof: page tests; stories at 80, 39 and 25rem, en and zh, both themes.
    - After c2 and h, and before AGENTSPLACE1 1h and 1i.
12. **1j, the installed window** (the steward): one repository per method, a restart mid-landing, a lost plugin answer, a live rule edit, a custom cycle and mixed epochs.
    - After all of them.

AGENTSPLACE1's row 1a2 (presets and the plan) comes after LANDNODE1a.

## 9. Open

- **One Accept carrying several sessions** (an entry's, LINESTOGETHER1): the first unit performs, and the others are reached as *brought into*, with no second fence. Decided by row 1b3 (D215's note of 2026-11-01): one delivery, the head's unit's; each member brought into it, and bound while its operation is open.
- **Tree tips read at each pass for due units:** measure under many trees.
- **A plugin taking minutes behind a bounded press:** the person may see *landing* before the pull request's link.
- **Undecided:** whether `tidy` becomes the node's, and whether a terminal with no loop runs the fence itself.
