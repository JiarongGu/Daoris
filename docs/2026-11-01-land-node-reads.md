# LANDNODE1: the makers' reads of the synthesis (2026-11-01)

**Standing:** the makers' reads under D191 of `2026-11-01-land-node-design.md` as round 1 synthesized it; the steward's settlement follows them, and the design was rewritten to it.

## Codex's read

**Accept with named changes.** I accept Opus’s live-Current approach together with the synthesis’s save-time resolution. That can avoid a configuration conversion while preserving one execution authority. However, the present design does not yet establish its recovery and downgrade guarantees.

1. **§§3–4: specify what authorization freezes.**

   Keep Current live until admission; freeze the admitted intent thereafter. Record the person’s authorization against the unit, revision, graph digest, resolved options and prerequisite receipts. Revalidate standing automatic authority immediately before the first effect. Today, `TreeLands` explicitly rereads the automatic switch to prevent execution after revocation (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/TreeLands.cs:565`).

   “Every option” saved into a version must include the ticket-key expression used by `{ticket}`, even though setup owns its default. Otherwise branch naming still inherits runtime behavior (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Domain/WorkflowGate.cs:159`).

   Preserve carried Accepts’ bounded repair semantics explicitly: they authorize a session’s cleanup continuation and wait while that continuation runs; they are not already revision-bound approvals (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/CarriedAccepts.cs:7`).

2. **§4 and row 1c: preserve existing cycles without rewriting configuration.**

   Add a compatibility projection for existing cycle references to `handed`, including evidence bindings and pending attempts. Retaining its identifier while deriving a performed `land` is one possible implementation; specify and test the chosen mapping.

   Today, custom cycles wire from the landing’s existing node identifiers, and composition preserves those wires (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeGraphs.cs:915`). Replacing the derived `handed` with an unrelated `land` identifier can strand valid configurations. “Nothing is migrated” needs this explicit compatibility contract.

3. **§3: replace the restart ladder with phase-aware reconciliation.**

   A landing record must settle **branch preparation**, not the whole delivery. Today that record is written before the plugin runs (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/TreeLands.cs:672`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/TreeLands.cs:759`). Consequently, ladder step 1 currently short-circuits the lost-plugin-answer case in step 3.

   Define separate durable start and outcome records for each effect, especially plugin dispatch. Reconcile the exact intent’s repository, source revision, target, branch ownership and plugin operation. A remote-tracking ref cannot prove that a pull request exists.

   Keep `integrate` admission and promotion owned by the following `integrate` node; remove their duplication from `land`’s settlement condition. State whether `land` waits for plugin delivery or exposes a preparation receipt while remaining active. Existing `handed` evidence proves local handoff only (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/BuiltInNodes.cs:125`).

4. **§§4, 7: add a concrete storage and downgrade boundary.**

   Attempts, phases and keys are sufficient *logical records*; separate tables are not intrinsically required. Their storage must nevertheless survive older writers. Today’s node-record serializer reconstructs known fields and does not preserve arbitrary added attempt payload (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeRecordFiles.cs:345`). Put delivery intents and phases in a new table or directory outside those writers’ reach.

   For changed integration semantics, explicitly raise the desktop writer stamp; existing writes check it inside their transaction (`src/Daoris.Desktop/Daoris.Desktop.Driver/Store/DesktopStore.cs:653`).

   Replace “an older landing is found, so nothing lands twice” with a supported downgrade rule: block older execution while newer effects remain unresolved, and require compatible Current configuration before allowing downgrade. An older handoff can invoke the plugin without the new key (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/TreeHandOffs.cs:107`).

   Also specify persisted Current method/mode fields. Today’s rule stores only merge/branch plus plugin (`src/Daoris.Desktop/Daoris.Desktop.Driver/Settings/LandingKey.cs:48`); that cannot round-trip push-only intent, and an older reader may interpret it as pull-request delivery.

5. **§5 and row 1g: make epochs enforceable and line-specific.**

   Keeping policy in the rule is acceptable initially, provided each selectable destination resolves to exactly one policy; reject targets lacking that mapping. Editing workflow delivery options must preserve the line policy.

   Define the epoch from the resolved shared policy and accepted declaration blob, excluding unit-owned `request` and `conflicts`. Persist it on requests and candidates, enforce equality transactionally in candidate formation, and run gates from that frozen declaration. Today formation checks repository and line but no epoch (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/IntegrationStore.cs:307`), while promotion obtains acceptance from current configuration (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/IntegrationPromotes.cs:274`). Admission tagging alone is insufficient.

6. **§8: split and reorder the rows.**

   Split 1b into durable intent/journal, explicit-intent git adapter, and reconciliation. Make plugin negotiation/recovery, epochs, authorization/lowering and save-time resolution prerequisites of Current activation. Cut over Current and its person-facing doors together.

   Make installed-window proof depend on **all** functional rows. Move combined Accept ownership and terminal execution without a loop from “Open” into the doors row’s required contract.

   Add older-reader/writer and restart proofs covering branch-before-record, plugin-success-before-reply, live-rule edits, custom cycles and mixed epochs. The current 1b is oversized, and 1c activates execution before several protections it relies on.
## The Opus maker's read


Read at `parent/batch-1031h` (`219a55176`): `docs/2026-11-01-land-node-design.md`, beside both proposals.

### Verdict: accept with nine named changes

The spine stands: one performed `land`, one performer with two starters, the fence, settling by postconditions. Five of Codex's six points fit it, some tightened. Point 1 as written breaks workspace-wide versions; change 1 repairs it and keeps Codex's aim.

### The six points

**1. Resolving every option at save: change it.** The aim is right: a later rule change must not move a bound run. Freezing at save goes too far.
- The accepted presets leave `pattern` and `plugin` to the rule (`docs/2026-10-28-integration-design.md:1645-1650`, `WorkflowPresets.cs:56-63`).
- A workflow chosen for a whole workspace "cannot hold each repository's address" (`docs/2026-10-09-workflow-design.md:103-107`, `:116-122`; D157 point 4).
- So frozen at save, *A pull request* could not be saved once for a workspace whose repositories use two platforms.

Freeze at the bind instead.
- **Change 1 (§4, second bullet):** "A run's binding resolves its version's `land` over the repository's declarations at the bind, and keeps the result. No later rule change reaches it. A version may leave `pattern`, `plugin` and `squash` to the repository. *Save as a workflow…* writes Current's values explicitly."
- **Same change, row 1h:** "versions resolve at save" becomes "the binding resolves at the bind".
- Older builds hold any run bound to a `land` version whole, so the binding's new field is read only by builds that know it.

**2. The phases: accept, with two edits.**
- **Change 2 (§3, step 2):** the phases are *prepared*, *ref written*, *recorded*, *plugin dispatched* and *plugin answered*.
  - Drop *integration admitted* and *settled*: they belong to the `integrate` node's attempt and the integration record. §2 keeps `integrate` as the tail's own node, and folding it in was rejected.
  - Rename *pushed* to *plugin dispatched*. Daoris never pushes; a push is read from the remote-tracking ref.
- **Change 3 (§3 and §7, Data):** the phases live in a delivery record of their own, `<home>/workflows/deliveries/<key>.json`, every field required. The node record's attempt names only the key.
  - Why: an older build armed by an integrating rule (`NodeRuntime.cs:351-354`) governs that repository's Current branch units and rewrites their records whole (`NodeRecordFiles.cs:9-14`). Phases kept on the attempt would be dropped there.
  - Rows an older build must not reach need a shape it cannot see (`compatible-builds.md:39-41`). Codex said this; the synthesis left it out.

**3. `RefTransaction`: accept. The act journal's mark: change it.**
- `refs/daoris/acts/<act>` holds only index snapshots and Resolve's stages. It is excluded from content holds (`docs/2026-10-23-git-writes-design.md:62`, `ContentHolds.cs:378-379`), so a landing's mark there would contradict D204's namespaces.
- A landing's ownership is its landing record (`RecoveryRetention.cs:32`).
- **Change 4 (§3, step 3, first sub-bullet):** "Before a branch ref moves, a write-ahead phase names the ref, its expectation and the commit. The ref moves in one `RefTransaction`: created where absent (today `git branch`, `TreeLands.cs:1144`), advanced from its old value (`:1249`). This runs under GITLOCK's checkout lock and LANDRACE1's tree hold. On a restart, the reconcile reads the ref against that entry and only then writes the landing record."

**4. A keyless plugin never retried blind: accept, with wording.**
- **Change 5 (§3, under the ladder):** "For a plugin that declares no key, the reconcile still reads the remote-tracking ref and `work/state`. It never sends the frame again. Only the person's hand-off (`HANDOFF`, `TreesRoutes.cs:1064`) does."
- Row 1e gives both example plugins the key, so the held path is not the default.

**5. The epoch and `merge`: accept both, re-scoped.**
- Each request already carries its policy as admitted (`IntegrationRequests.cs:155-156`). The defect is the candidate's `queued[0].Policy.Batch` (`IntegrationReplays.cs:137`).
- **Change 6 (row 1g):** "A candidate takes only the queued prefix whose admitted policy and accepted blob equal the first's. The row waits on nothing, and is recorded in the fix log." It is a defect beside this design and should not wait for it.
- **Change 7 (§5, second bullet):** "`method: merge` is refused, at the choice's reach (§17.7) and again inside the start, for a line whose effective `integrate` says `gates: declared` with an accepted declaration naming a gate (D215 point 3: the line moves only to a gated commit). Under Current it cannot arise, since a rule has one form."

**6. D100: accept, with one addition.**
- **Change 8 (§6, D100's bullet):** add "Current derives `pull-request` from a branch rule naming a plugin, never `push`, since no plugin today is told a mode. D87's *Daoris itself never pushes* is quoted unchanged."

### Rows

Ten is close, but two rows each hold two builders' work, and two orderings are missing.

**Change 9:**
- **1b: split it.** 1b keeps the delivery record, the fence, the write-ahead and `RefTransaction`, and the git half of the ladder for `merge` and `branch`. The plugin phases and their reconcile move into 1e, the plugin row.
- **1c: split it.**
  - 1c: Current draws `land`, the pre-landing revision, bounded arming.
  - 1c2, after 1c: `AutoLander`'s choosing retired, due entries and kept Accepts adopted, an older build's landing met by the reconcile.
- **1g:** waits on nothing.
- **1i:** after h as well, since the editor needs the binding's resolution and the presets.
- **AGENTSPLACE1 1a2:** after LANDNODE1a, since `SetupPresets` reads `LandNodes`. Say it in both rows.

That makes eleven rows: a, b, c, c2, d, e, f, g, h, i, j.

§9 stands. One addition: prove one Accept carrying several sessions in 1b, not 1j.

## The steward's settlement (2026-11-01)

Both makers accept with named changes. The steward takes them as follows, and the design is rewritten to them.

- **When a version's options are fixed: at the bind** (Opus).
  - A version's `land` may leave `pattern`, `plugin` and the ticket key to the repository, so a workflow chosen for a whole workspace still serves each repository (D157 §2.3; the integration design's `:1645-1650`).
  - The run's binding resolves them over the repository's settings once, at the bind, and keeps them. The ticket-key expression is among them (Codex), so no later rule change reaches a bound run.
  - Current stays live until its landing is authorized.
- **What authorization freezes** (Codex).
  - The person's Accept is recorded against the unit, revision, graph digest, resolved options and prerequisite receipts.
  - Standing automatic authority is judged again just before the first effect, as `TreeLands` reads the switch again today (`TreeLands.cs:565`).
  - Carried Accepts keep their bounded repair meaning: they authorize a session's clean-up continuation and wait while it runs, and are not revision-bound approvals (`CarriedAccepts.cs:7`).
- **Phases, each effect written ahead and its outcome after** (both).
  - The phases are: prepared, ref written, recorded, plugin dispatched, plugin answered. The admission and settlement of integration stay the `integrate` node's own attempt (Opus); nothing of Daoris "pushes".
  - The landing record proves the branch's preparation only, since it is written before the plugin runs (`TreeLands.cs:672`, `:759`; Codex).
  - `land` is reached when its method's own effects are recorded: for `merge` and `branch`, the landing record; for `push` and `pull-request`, the plugin's answer recorded. The `pull-request` observer then waits for the platform.
  - A lost plugin answer is settled by asking `work/state` with the key. A remote-tracking ref proves a push and never a pull request.
- **Where the phases live.** A delivery record, kept apart from the node record that an older writer rewrites whole (`NodeRecordFiles.cs:345`, `compatible-builds.md:39-41`).
  - It is a new table of the desktop store, `deliveries`, written under a raised writer stamp, which older writers check inside their transaction (`DesktopStore.cs:653`).
  - A rollback to an older build is refused while a delivery is unresolved (the store's rollback window, the foundation design §13.5), so an older handoff never calls a plugin without the key behind a newer attempt.
- **Refs:** `RefTransaction` under GITLOCK's checkout lock and LANDRACE1's tree hold, preceded by the write-ahead phase entry. There is no act-journal mark: `refs/daoris/acts/` stays D204's (Opus).
- **Keyless plugins** are reconciled by reading, and sent again only by the person's `HANDOFF` (Opus).
- **Old cycles keep wiring** (Codex). Current's `land` keeps the derived landing node's identifier, so a custom cycle's wires and evidence bindings to it still resolve. The mapping is specified and tested.
- **Current has no `push`.** It derives `pull-request` from a branch rule naming a plugin and never derives `push`, so today's rule round-trips unchanged. `push` exists only in versions. D100's amendment quotes D87's "Daoris itself never pushes" unchanged (Opus).
- **`merge` is refused** at the choice and again at the start, for a line whose `integrate` says `gates: declared` with an accepted declaration naming a gate (Opus).
- **The line's epoch is a fix of its own, waiting on nothing** (both).
  - The epoch is the resolved shared policy plus the accepted declaration's blob, excluding the unit's own `request` and `conflicts`.
  - It is kept on requests and candidates, enforced as equality inside candidate formation (`IntegrationStore.cs:307`), and promotion runs gates from that frozen declaration, not the current configuration (`IntegrationPromotes.cs:274`).
  - Each selectable target resolves to exactly one policy; a target with none is refused (Codex).
- **Rows** (both).
  - The performer splits into the record and intent, the git adapter, and the reconciliation. The plugin's half of reconciliation goes to the plugin row.
  - Current is drawn first and inert; its activation and the person's doors move together, after every protection it relies on.
  - The installed proof waits on all of them.
  - An Accept carrying several sessions, and a terminal with no loop, are contracts of the doors row.
  - The proofs of older readers and writers and of restarts are named per row.
  - AGENTSPLACE1's row 1a2 comes after LANDNODE1a.

**Accepted 2026-11-01, as rewritten.**
