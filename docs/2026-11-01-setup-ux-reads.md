# SETUPUX1 — the reads of the synthesis, and the steward's settlement

Status: reads of `docs/2026-11-01-setup-ux-design.md` (the synthesis), 2026-11-01; both readers read-only, kept as they came; the steward's settlement in §3. Both said *accept after the named changes*.

## 1. Codex's read

**Blocking findings**

1. **Candidate preview can apply a choice on an older driver.** The design sends `WORKFLOW_USE {plan:true}` when selecting a candidate and claims compatibility with drivers lacking the additions (`docs/2026-11-01-setup-ux-design.md:260`, `:504`). Today’s handler ignores `plan` and `expected`, calls `UseAsync`, and writes the change (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:409`, `:421`, `:429`). Add a capability announced by a safe read, and require it before sending a preview request. Prove that candidate selection against today’s handler writes nothing.

2. **“Use the parent’s” does not necessarily restore inheritance.** The design says this clears the repository’s own choice (`docs/2026-11-01-setup-ux-design.md:258`). A default-clear preserves its kind mappings; any remaining development mapping still replaces the workspace’s whole mapping (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Domain/WorkflowSelection.cs:220`, `:282`, `:290`, `:328`). Specify either a narrower default-clear with its actual consequence, or explicitly reviewed, separate clears for the remaining mappings. Do not widen one `WORKFLOW_USE` press into clearing them all.

3. **The landed route needs a separate control contract.** Route selection includes landed work, and *Edit steps* opens the graph drawn, but Change/Use is described through the development-route mechanism (`docs/2026-11-01-setup-ux-design.md:223`, `:229`, `:260`). `WORKFLOW_USE` expressly excludes landed choice; verification workflows are refused on development routes (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:392`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Domain/WorkflowSelection.cs:277`). Also, landed `current` means **no verification**, not Current’s development graph (`…/WorkflowSelection.cs:238`, `:287`). Define that empty state and preserve the existing verification/no-verification/parent choices and their acceptance consequence (`src/Daoris.Web/src/settings/repository/LandedRoute.tsx:52`, `:99`). Keep them on `WORKFLOW_LANDED`.

4. **The driving checkbox promises the wrong consequence.** “With it off, only the adoption quest runs” and the first flow’s resulting “Driven” head contradict each other and the implementation (`docs/2026-11-01-setup-ux-design.md:177`, `:439`). `Drive=false` leaves existing driving unchanged; it does not disable it. An imported repository remains undriven when adoption is requested without enabling driving (`src/Daoris.Desktop/Daoris.Desktop.Driver/Setup/PlacePlan.cs:187`, `:201`). Describe the actual before/after state, and make the first flow end **Not driven** unless the person explicitly enables general driving.

5. **Read-only imported sections block refusal recovery.** Imported configuration is explicitly read-only, while setup refusals promise doors to repair their causes (`docs/2026-11-01-setup-ux-design.md:95`, `:179`). The driver can require configuring reviewers or a review environment before the requested setup can proceed (`src/Daoris.Desktop/Daoris.Desktop.Driver/Setup/PlacePlan.cs:252`, `:271`). Those doors cannot repair the imported repository under the proposed restriction. Allow the relevant configuration edits through their existing presses while driving remains off, and prove refusal → repair → return → Apply.

6. **Two promised presentations lack their required read fields.**
   - Folded summaries must name role sources using `PLACES` alone (`docs/2026-11-01-setup-ux-design.md:105`). Its work row supplies a role **id**; its `source` belongs to the work policy, not the role (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/PlaceProjection.cs:36`, `:137`). Row 1d adds detail and landing information, but no role name/scope or complete Sessions summary contract (`docs/2026-11-01-setup-ux-design.md:511`). Add the exact summary fields before 1e.
   - “Sent back n times” is assigned to a web-only row (`docs/2026-11-01-setup-ux-design.md:347`, `:524`). The run answer exposes configured back wires and current outcomes, without firing history or a return count (`src/Daoris.Desktop/Daoris.Desktop.Modules/Bridge/Routes/WorkflowRoutes.cs:650`). Add a driver-derived count, with its counting scope and unavailable state, before 1q.

7. **The composed release flow starts from a node Current does not contain.** Row 1b deliberately uses the terminal’s Current-copy graph; §3 then starts with merge Current and selects *On the line* (`docs/2026-11-01-setup-ux-design.md:509`, `:450`). That compiler ends merge Current at `landing`; it adds `line` only for integration (`src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/Domain/WorkflowWords.cs:340`, `:349`). Start this flow from the shipped Merge preset, or include the missing insertion and end-setting steps. Prove it from the real row-1b answer.

8. **The input-proposal rule cannot produce both promised release wires.** It proposes a wire only when exactly one earlier output fits, while row 1n requires both release data wires to be proposed (`docs/2026-11-01-setup-ux-design.md:309`, `:521`). Both Find and Record expose the schema Publish accepts (`src/Daoris.Desktop/Daoris.Desktop.Driver/Defaults/workflows/release.json:27`, `:34`, `:39`). Define a preference for the routed predecessor, or require an explicit choice when ambiguous and amend the flow and press count.

9. **The workspace cutover silently removes controls.** The synthesis removes workspace Setup without assigning remote wiring and rules a destination (`docs/2026-11-01-setup-ux-design.md:488`, `:527`). Today that tab retains the remaining workspace rows (`src/Daoris.Web/src/projects/workspace/WorkspaceView.tsx:157`), including Wire/Unwire, undeclared-machine retirement, and allow/ask/deny rules (`:589`, `:608`, `:621`). Add an explicit destination, implementation row and control/door census before removal.

**Non-blocking findings**

- **Clarify read timing.** Row 1e’s “opening reads no section” conflicts with opening Workflow on arrival. Say that *folded* sections read nothing; the initially open section reads normally (`docs/2026-11-01-setup-ux-design.md:94`, `:107`, `:512`).
- **Bind append consent explicitly to the reviewed scope keys.** A draft fingerprint alone does not describe an externally changed reach. Preserve the exact scope authorization checked by the driver, and reset consent when that set changes (`docs/2026-11-01-setup-ux-design.md:337`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Workflows/WorkflowOperations.cs:642`).
- **Resolve the end-marker wording before its visible row.** “Ends here” remains misleading above downstream release steps; §6 currently leaves its explanatory wording open (`docs/2026-11-01-setup-ux-design.md:211`, `:533`). Keep declared end and recorded finish visibly distinct.
- **Split row 1m’s proof.** Its release-composition acceptance precedes row 1n’s input and tail work. Keep inspector/navigation proof in 1m and complete composition proof after 1n (`docs/2026-11-01-setup-ux-design.md:520`).
- **Correct the measurement baseline.** Adding a separate review page changes the first-flow press count. Record the starting screen and count every navigation and confirmation consistently (`docs/2026-11-01-setup-ux-design.md:439`, `:546`).

**What the proposals dropped and should keep**

- **Opus’s explicit Remote destination:** Remote becomes a Details row, with a workspace replacement row before cutover. Restore this to address finding 9 (`docs/2026-11-01-setup-ux-proposal-opus.md:682`, `:782`).
- **Codex’s editable imported configuration without enabling driving.** This supplies the missing recovery path in finding 5 (`docs/2026-11-01-setup-ux-proposal-codex.md:77`).
- **Codex’s explicit preservation of role scope/revision and typed model/effort alternatives.** Keep these as acceptance criteria for the shared editor and step-role projection (`docs/2026-11-01-setup-ux-proposal-codex.md:472`, `:597`).

accept after the named changes

## 2. The Opus reader's read

Paths: web `src/Daoris.Web/src/`, driver `src/Daoris.Desktop/Daoris.Desktop.Driver/`, modules `src/Daoris.Desktop/Daoris.Desktop.Modules/`.

### Blocking findings

**B1. Flow 1 ends in a state the driver does not produce.** With Driving off the plan never makes the repository drivable: `SET_DRIVABLE` is added only when `ask.Drive` is set (driver `Setup/PlacePlan.cs:187`); adoption is planned apart (`:194`). `PLACES` treats driven as drivable and nothing else (driver `Workflows/PlaceProjection.cs:163,173`). So after *Apply* the head still reads *Not driven*, and the row stays in *17 imported, not driven*, where it reads as untouched. The design needs a state for "set up, adoption running, not driving all its work", in the head's pill and the tree.

**B2. *As it is, n of its own* cannot be planned, and Set up… discards a saved workflow.** `PLACE_PLAN` refuses any preset outside the table (driver `Setup/PlacePlan.cs:113-116`); *Drive all its work* applies only inside a delivery (`:157-191`) and the page disables it for *Not driven* (web `agents/setup/PlaceSetUp.tsx:203`); every delivery resets a chosen saved workflow to Current (`PlacePlan.cs:169-182`). A person who only wants to turn driving on, or tick a second opinion, must throw their release workflow away. Fix: a row giving `PLACE_PLAN` a "keep how it lands" ask, driving allowed under it — a subset of what `PLACE_APPLY` already writes — or say plainly that Set up… always replaces the workflow and drop *As it is*.

**B3. The Roles folded line cannot come from `PLACES` alone.** `PlaceWork` carries the role's id and the source of the work's policy (driver `Workflows/PlaceProjection.cs:36,137`), not the role's names or the place it sits at; the fold would show ids, and could not say *set here*. Fix: row 1d adds each work's role name and scope.

**B4. The chart drawn from `effective.graph` (row 1b) loses what Current's chart says today:** each step's settings, where each was set with a door, runtime and limit (web `workflow/current.ts:26-42`; `workflow/WorkflowChart.tsx:195-235`), the limits in the driver's words (modules `Bridge/Routes/WorkflowRoutes.cs:487-491`), and `WorkflowHead`'s part and unregistered note (web `workflow/WorkflowTab.tsx:39-66`). Row 1b should answer each node's cell beside the graph, as `WORKFLOW_RUN` does (`WorkflowRoutes.cs:612-614`). `--from current:‹repository›` names a repository only (driver `Workflows/WorkflowOperations.cs:389-392`); say how a workspace's Current graph is made.

**B5. ◎ *No further step* contradicts "`Flow` unchanged", and row 1k's proof depends on it.** A `FlowEdge` needs a `to` node; if `none` draws nothing every rank is one node wide, `needs().down` is 15.5rem under N's 23.5rem (web `design/flowLayout.ts:258-267,290`; `design/storyWidths.tsx:18`), and `placementFor` picks *down* at 25rem, not the rail the proof shows. Say ◎ is a drawing-only `FlowNode`, and put the 14rem track into 1k's contract: today `WORKFLOW_TRACK` is 19rem (web `workflow/workflowFlow.ts:34`), and 2×19 + 3 = 41rem does not fit H's 36rem.

**B6. "Sent back *n* times" (row 1q) has no field.** The run graph answers back wires, not counts (driver `Workflows/Domain/WorkflowGraphRun.cs:90-101`; modules `WorkflowRoutes.cs:650-661`). Name the field or drop the line.

**B7. The Workflow section drops the second opinion, your look and the landed route.** Today it mounts Setup's `work` rows (web `agents/setup/PlaceSessions.tsx:45`): line, landing, commits, your look, opinion and the landed route (web `projects/setup/RepositorySetup.tsx:271-285,507-508`). §2.3's folds leave no home for the second opinion, your look, or the landed route's pipeline (`LandedRoute` → `WORKFLOW_LANDED`), which the agents-place design puts in Workflow (`docs/2026-11-01-agents-place-design.md:73,79`); Change… cannot hold the landed route (`WorkflowRoutes.cs:392`). Heavy gates move from Sessions (`RepositorySetup.tsx:214`) into More; §4 should say so.

**B8. Row 1t removes the workspace's Setup tab, and the remote wiring goes with it** (web `projects/workspace/WorkspaceView.tsx:151-157,182`). Opus had "Remote becomes a Details row".

**B9. AGENTSPLACE1k's seeded set-up is not placed** (web `app/opener.ts:188-193`, `agents/AgentsView.tsx:138-146`, `agents/setup/placePlan.ts:97-100`). Say which page a seed opens on — the review, since the person came for *Apply* — and prove it in 1g and 1h.

**B10. The place page with Workflow open on arrival.** Route rows, the full chart and four folds come before Roles and Sessions; at 680 px the other two section lines sit a screen or more below the head — the long unfolded stack the owner found unusable, now the default. Keep the head and all three section lines on the first screen; Workflow can open on its routes and folds with the chart behind one press, at least at 680 px. Measure "the head and three folded lines within the first 400 px".

**B11. The step editor at 680 px.** The inspector sits under ten or more ranks; selecting *The work* sends the person a screen down and back. Use Codex's drawer, or open the step inline under its node.

**B12. The imported, not-driven page.** Its sections are read-only, so a person cannot compose the workflow or staff the work before letting it drive — the natural order, and the state flow 1 leaves them in. The driver does not require drivable for `WORKFLOW_USE`, `SET_ROLE` or `SET_LANDING`. Keep Codex's rule: reading or editing configuration does not enable driving.

### Non-blocking findings

- **N1** citations to correct: `WorkflowEditor.tsx:173` is `stepChoices`, the stale *Use* is `:337-341` after `change` at `:177`; Save's guard is `:333`; the stale check is `WorkflowRoutes.cs:415-419`.
- **N2** write the steward's settlements into D221's note.
- **N3** §2.5's names undo part of the rename: "Pull request workflow", the delivery *A pull request* and the step *Pull request* are one suffix apart and all 拉取请求 in zh; say "from Current", not "from Merge workflow (Current)".
- **N4** keep a chrome key for *Not driven* once `defaultName.*` goes (driver `Workflows/Domain/SetupPresets.cs:46` names it in English only).
- **N5** the suggestion's `why` should be a code the page words, the driver's sentence beneath.
- **N6** draw route rows as a selection, not radios.
- **N7** state the id fallback for a name with no ASCII letters (web `agents/roles/Roles.tsx:26-31`).
- **N8** the holds group is `graphFlow`'s own: `Flow` folds three or more (web `design/flowLayout.ts:61`), `documents` holds two.
- **N9** plugin step `title`/`about` extends the plugin manifest (`examples/plugins/local-release/plugin.json:11-44`); name the contract change; "publish" must not imply an external registry (`examples/plugins/local-release/README.md:25`).
- **N10** *The run stops* means no `failed` wire, not an inserted `stop` node.
- **N11** the head keeps `driving.note` and the steward-set time (web `projects/setup/RepositorySetup.tsx:363-367`).
- **N12** the chooser's *Driven* needs the whole drivable list, not the `PLACES` page in hand (web `agents/setup/SetupPlaces.tsx:336`).
- **N13** keep today's reach sentences and "the line could not be read" in the review (web `agents/setup/PlaceSetUp.tsx:248-255`).
- **N14** the result opens the adoption quest through a door.
- **N15** *Taken by* offers the roles that take the work and cover this place, each named with its place.
- **N16** `PageHead`'s `line` is a string (web `work/frame/ViewMain.tsx:32`); `SetupSection` keeps its own open state (web `projects/setup/SetupParts.tsx:57-61`).
- **N17** *Verify…* appears only once a landed pipeline is accepted (web `workflow/WorkflowStartNode.tsx:78`).
- **N18** say whether `moved` replaces today's `stale` refusal or joins it.
- **N19** flow 1 is six presses with two pages.
- Splits: 1d into place detail and `lands` (driver) and step titles (plugin contract); Change…'s plan of one after rows c and l, apart from the editor's save in 1o; 1r's glossary rows first and the rewording last; 1e's "a declaration opens Workflow at Gates" depends on 1l.
- Measures: Opus's overview-fits; Codex's large-workspace selection and finding an outcome in a long graph; zh head lines at 680 px.

### What a proposal had that the synthesis dropped

Codex: editing a not-driven place's configuration (B12); the drawer at 680 px (B11); focus back to the head after choosing a place at 680 px; the plugin-publishing caution (N9); Acceptances saying changed landed gates stop verification while integration goes on under its last accepted blob; the two measures. Opus: the run view's 300 px wireframe; the *All roles* wireframe; Remote as a Details row (B8); second opinion and your look in Workflow's folds (B7); the overview measure (B10); the workspace head's spread of deliveries; the "design falls to building" row.

**Verdict: accept after the named changes (B1–B12).**

## 3. The steward's settlement

Every blocking finding of both reads is taken; the design is revised in place, and accepted once the revision carries each (D221).

- **Choosing, reviewed, safely** (Codex 1): the page sends `WORKFLOW_USE {plan}` only where a read announces it; against today's handler a preview writes nothing, and a row proves it.
- **The parent's** (Codex 2): *Use the workspace's* clears the default alone and says what its kinds keep; clearing a kind's mapping is its own reviewed press.
- **Landed work** (Codex 3, B7): the landed route keeps `WORKFLOW_LANDED` and today's verification, no-verification and parent choices, in Workflow's folds beside the second opinion and your look; heavy gates move to More.
- **Driving** (Codex 4, B1): flow 1 ends *Not driven · adoption running* unless general driving was chosen; the head and the tree show that state.
- **Not driven is editable** (Codex 5, B12): a not-driven place's configuration is edited through the existing presses; editing never enables driving; a refusal's door repairs and returns.
- **Fields the page needs** (Codex 6, B3, B4): `PLACES` gains each work's role names and scope and the Sessions summary; row 1b answers each node's cell beside the graph and how a workspace's Current graph is made. *Sent back n times* is dropped (B6).
- **Keep how it lands** (B2): `PLACE_PLAN` gains a "keep how it lands" ask, driving and the ticks allowed under it, a subset of what `PLACE_APPLY` writes.
- **The release flow** (Codex 7) starts from the shipped Merge workflow; its inputs prefer the routed predecessor and ask where two fit (Codex 8).
- **No control lost at cutover** (Codex 9, B8): the workspace's remote wiring, undeclared-machine retirement and rules get a home on the workspace's page and a door census before the old tab goes.
- **Drawing** (B5): ◎ is a drawing-only node; the 14rem track is row 1k's contract.
- **The seed opens the review** (B9).
- **The first screen** (B10): the head and the three section lines fit the first screen at both widths; at 680 px the chart is one press away.
- **The editor at 680 px** (B11): a step's inspector opens as a drawer.
- Every non-blocking finding and split is taken, the names rechecked so no two of workflow, delivery and step share a word in either language, and the measures added.