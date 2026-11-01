# LANDNODE1: Codex's sealed proposal (round 1, 2026-11-01)

Written by Codex, read-only at `parent/batch-1031h`, from the brief beside it; kept as written.


## 1. Diagnosis

The workflow describes delivery without owning its execution.

- `handed`, `pushed` and `pull-request` have observed reach; they consume outcomes produced elsewhere (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeContract.cs:279`). `TreeLands` still chooses between the named process’s composed landing and the effective rule (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/TreeLands.cs:562`).
- Integration already offers the right boundary: a durable attempt precedes admission, and reconciliation finds the existing request before another start (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/IntegrateNode.cs:147`). However, admission still obtains most policy from the landing rule (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/IntegrationRequests.cs:147`).
- Current constructs a branch graph from the rule, then attaches its cycle; that cycle cannot declare landing kinds or wire into them (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeGraphs.cs:885`, `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeGraphs.cs:909`).
- The brief’s cutover line 21 is historical. Today, attribution already prevents Current from additionally governing branches owned by graph-bound runs (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeRuntime.cs:449`). The remaining problem is who performs delivery and supplies its configuration.

I propose one new built-in, performed kind: **`land`**, displayed as **Landing**. Existing observed kinds retain their meanings.

## 2. The design

### The node and its options

A `land` node delivers one unit’s accepted revision. It exposes a durable delivery receipt and references to the branch, plugin result or integration request.

| Option | Meaning and ownership |
|---|---|
| `method` | Node: `merge`, `branch`, `push`, `pull-request`, or `integrate`. |
| `targetLine` | Node: a configured line, or the repository’s canonical line. Resolve its identity into the execution intent. |
| `pattern`, `ticketKey` | Node: branch naming, using `LandingKey`’s existing validation and placeholders. Preview the expanded name. |
| `plugin` | Node: installed plugin id for push or pull request. Absence authorizes neither. Credentials and platform connection details remain machine-local. |
| `commitMessage` | Node: literal text or a bounded token template, previewed and frozen before execution. Applies only to commits delivery creates; existing commits remain unchanged. |
| `squash` | Node for branch delivery; refused for integration, whose history shape belongs to the line. |
| `accept` | Node: person’s Accept, or explicitly granted standing automatic delivery where permitted today. |
| `integration` | Node: `request: owner\|done\|steward`, `conflicts: session\|hold`, and a reference to the target line’s policy. |
| Prerequisites | Explicit preceding nodes: work holds, requests, opinion, look and any additional checks. |
| Cleanup | Graph cleanup policy, preserving today’s tidy setting during conversion. |

The methods have distinct completion conditions:

- **Merge:** the line contains the delivered work.
- **Branch:** the named local branch exists at the recorded result.
- **Push:** branch delivery plus proof that the requested revision was pushed.
- **Pull request:** push plus a recorded platform request for the intended source and target.
- **Integrate:** branch delivery, admission through the integration door, then the member’s promotion and settlement.

Opening a pull request does not mean it merged. A following `pull-request` observer can still wait for platform completion, as D215 requires (`docs/2026-10-28-integration-design.md:1889`).

### One line, many units

Move shared integration policy out of `LandingRule.Integrate` into a desktop-owned policy keyed by repository and line:

- history shape: merge, rebase or squash;
- batching and order;
- integration gates and unconfirmed-failure treatment;
- overlap handling;
- promotion conditions.

The node chooses the destination and requests integration under that policy. Its editor shows the effective policy and offers **Edit line policy…**, explicitly naming every affected workflow. Saving node options cannot silently change other units’ policy.

Admission freezes a policy epoch and accepted declaration reference. Candidates combine compatible epochs; a later policy change cannot make the first queued request govern incompatible members. Today replay takes batching from that first request (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/IntegrationReplays.cs:137`).

An ordinary merge cannot bypass a line configured to require gated integration.

### Performing, acceptance and recovery

Register a built-in landing adapter beside the existing built-in executors. Keep the node lifecycle, binding validation and attempt fence; run git, plugins and integration off the runtime pass.

1. **Authorize.** The person’s Accept records the unit, revision, graph digest, resolved delivery options and prerequisite receipts. Standing authorization records its scope separately. An agent cannot produce either.
2. **Persist.** Before effects, write an execution intent containing the resolved line, expanded branch, rendered message, plugin, integration policy reference and acceptance provenance.
3. **Perform.** Pass that intent explicitly into `TreeLands`; new executions must not reread a live landing rule to choose their method. Preserve its containment, replay, branch ownership, dirty-tree, hook and pinned-tip checks.
4. **Continue.** Push and pull request use the landing plugin seam. Integration uses `IntegrationRequests`, `LandingIntegrate` validation, `IntegrateNode`’s request/reconcile mechanism and the existing line transport, replay and promotion algorithms.
5. **Settle.** Canonical writers record outcomes. The runtime reaches or fails the node from those records, with immutable evidence references.

The person’s Accept becomes authorization for the node to execute. It remains separate from **Reviewed**, declaration acceptance and a steward’s integration request. Automatic branch preparation under `request: owner` still waits for the person before integration.

Use a unique delivery identity based on repository identity, owning unit and source revision, independent of attempt number. Repeated Accepts and restarts find the same delivery. Its phases have separate durable outcomes: prepared, branch recorded, plugin dispatched, integration admitted, settled. A later revision creates a new delivery only after unresolved effects of the previous one settle.

Reconciliation precedes retries:

- A branch/ref effect is proven through its journaled result and ownership marker. Ref creation or advancement and its marker use one git transaction; an existing unrelated branch is refused.
- Integration finds the request by its stable key. Promotion recovery uses postconditions, as today’s resume checks whether the line already holds the candidate (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/IntegrationSteps.cs:68`).
- Plugin delivery requires negotiated operation selection, durable idempotency and reconciliation. Today’s `LandingFrame` carries no invocation or idempotency key (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workplace/Landing.cs:271`). Extend the protocol; never infer that a legacy plugin can safely replay, or that a pull-request plugin supports push-only delivery.
- Unknown stays held. Neither a timeout nor another Accept authorizes repeating an uncertain effect.

Stopping prevents further phases where possible; it never deletes delivered work or reverses promotion.

### Landing rules and cutover

Keep existing rules as **legacy execution configuration** until conversion. Thereafter they supply migration input and preset defaults, never a second execution authority.

Conversion is a reviewed place plan:

1. Resolve existing workspace/repository inheritance and named workflow composition.
2. Produce fresh graph versions with explicit landing options, preserving patterns, plugins, acceptance modes, messages, integration settings, cycle nodes and cleanup.
3. Refuse unmappable custom cycles with their exact obstruction; retain legacy operation until explicitly resolved.
4. Stage activation per repository. Existing runs and branch units remain under their original engines and configuration. Activate after their legacy delivery and cycle obligations drain; queue new starts during the short switch.
5. Preserve the conversion receipt and original configuration for recovery. Imported, undriven repositories are neither activated nor probed.

New work binds the saved version. Editing options creates another version; it changes no running unit.

Use the distinct `land` kind in schema-2 graphs. Older builds already reject unknown kinds (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Nodes/NodeContract.cs:353`). Keep ordinary named selections so they hold converted runs instead of falling back to Current.

After the drain, leave a passive branch-only legacy fallback: no plugin, automatic acceptance, integration or active cycle. Store new execution journals—and integration rows requiring new semantics—in new tables outside older executors’ reach. A nullable flag cannot protect them (`.claude/knowledge/compatible-builds.md:46`). Downgrade reads legacy history and holds converted execution; it does not approximate delivery. Prove this with older readers and statements.

### Graph rules and decision amendments

New development graphs permit at most one performing landing, require it on successful delivery paths, and forbid a second actor for its effects. Required gates dominate it on every path. Verification graphs contain no landing performer.

For new graphs, replace “every landing observer follows `handed`” with explicit receipt provenance from the performing landing. Preserve schema-1 and existing schema-2 validation and execution.

Amend:

- **D87/D100:** method, pattern and plugin become node options. Explicit push and pull-request methods reverse D100’s rejection of separate forms because one executor now shares branch preparation. Platform actions remain plugins’ responsibility.
- **D157:** move delivery choices previously classified as declarations onto the node; preserve holds → opinion → look → landing.
- **D215 §17.7:** unit delivery belongs wholly to the workflow; shared integration policy belongs to the line independently of a landing rule.
- **Cutover line 21 and its ownership table:** retain the historical finding, then record that converted units follow their bound workflow, never a live landing cycle.

### Routes, data and AGENTSPLACE1

`LANDING` previews the bound intent. Existing Accept routes authorize that node and wake execution; additive response fields distinguish authorized, running and completed. Entry/ask Accept and automatic landing use the same door. `LAND_WITHOUT_LOOK` records the person’s explicit skip before that door.

`SET_LANDING` remains legacy-only. Workflow saves and `PLACE_PLAN`/`PLACE_APPLY` own converted configuration. All mutation routes retain Person classification, grounding, publishing declarations, terminal twins and Ask Daoris coverage.

AGENTSPLACE1’s Landing row and **Edit steps** open one options editor:

- **1d** becomes landing-option and line-policy validation.
- **1h** shows the node, effective options, source and gates.
- **1i** edits those options into a fresh version.

Quick setup delivery presets generate these options; **Not driven** generates no execution. Preserve separate adoption and driving choices. Gates retain two purpose-specific acceptances, as accepted (`docs/2026-11-01-agents-place-design.md:75`). English and Chinese chrome, narrow ordered lists, keyboard access and authored message text follow the existing contracts.

## 3. What I reject

- **Relabelling observers:** leaves execution outside the workflow.
- **One integration engine per unit:** cannot safely batch or promote a shared line.
- **Runtime inheritance from landing rules:** makes saved options change underneath a bound version.
- **Automatic retry of legacy plugins:** cannot establish whether an external effect already happened.
- **Automatic bulk conversion:** risks dropping custom cycles or changing in-flight authority.

## 4. Builder rows

| Row | One builder’s scope | Proof |
|---|---|---|
| 1 | `land` declaration, options and involvement comparison | Invalid combinations, gate bypasses and automatic-delivery lowering refused. |
| 2 | Durable delivery intent and phase journal | Concurrent starts and interruption at each boundary produce one delivery. |
| 3 | Explicit-intent `TreeLands` adapter | Git cases cover moved tips, collisions, squash, containment and ref-before-record crashes. |
| 4 | Landing plugin protocol and example adapters | Duplicate keys, lost replies and restart after external success create no second effect. |
| 5 | Independent line policies and admission | Conflicting epochs never batch; node delivery cannot bypass line requirements. |
| 6 | Integration completion and recovery adapter | Restart before/after admission and promotion settles the same request. |
| 7 | Migration planner and activation | Mixed inheritance, custom cycles, old flights and older-build fixtures lose no configuration or authority. |
| 8 | Routes and terminal integration | Every Accept path reaches one executor; person-only acts and stale plans are refused. |
| 9 | AGENTSPLACE1 editor and presets | Driver/UI parity; en/zh, both themes, three widths, keyboard and installed-window proof. |

Each row includes its decision amendment and relevant regression gates.

## 5. Risks and open questions

The largest risk is plugin recovery: platform lookup may remain inconclusive, requiring a visible hold.

Custom cycles may delay conversion. The plan must identify those repositories without blocking unrelated ones.

Shared-policy edits need clear scope and epoch wording. Prototype that explanation before finalizing the editor.

These findings are source inspection; the proposed recovery guarantees require the proofs above.