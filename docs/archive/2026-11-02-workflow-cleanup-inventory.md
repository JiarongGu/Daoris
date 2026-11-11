# The old workflow logic still live — the inventory WORKFLOWCLEAN1 starts from

Status: inventory, 2026-11-02, read on main at `66fe86eac` (read-only, no tests run); the owner reported the old workflow logic still
showing and asked for it updated and cleaned up, the default setup included. Paths: `Driver/` is
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` `src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` `src/Daoris.Web/src/`.

## In short

- **The shipped window shows the old surfaces.** The place page is behind `daoris.agents.setup`, which only the browser's own
  storage turns on (`Web/agents/modes.ts:35-39`): every shipped build shows the old tabs and the old Roles mode. SETUPUX1x and 1y
  (the installed sitting, then lit) are open.
- **`land` is declared performed and nothing performs it.** The runtime observes it from the landing record
  (`Driver/Workflows/Nodes/BuiltInNodes.cs:79-80`, `NodeRuntime.cs:976-993`); the doors land — the Accept press, `trees land`, the
  look's `AutoLander` (`Driver/Workplace/AutoLander.cs:28-56`, `:118-137`), which still chooses by `rule.AutoAccept`; a bound graph's
  `land` is read back as a `LandingRule` (`Driver/Workflows/Domain/WorkflowGate.cs:162-200`). The land node design's §3, §7 and §8
  row 10 retire this; D215's notes (`docs/decisions/D215.md:657`, `:664-665`) record it left undone.
- **Set up… writes the old shape.** Each delivery preset writes a landing rule (`SET_LANDING`, a branch pattern) and sets the place
  back to Current (`Driver/Setup/PlacePlan.cs:182-213`, `:229-263`; `Driver/Workflows/Domain/SetupPresets.cs:43-48`, `:121-141`);
  it never saves or chooses a shipped graph. A fresh place follows Current, drawn from `driver.json`'s rules
  (`Driver/Workflows/Domain/WorkflowSelection.cs:85`), as the fabric2 plan chose (`docs/2026-11-01-fabric2-plan.md:79`).
- **Schema-1 step workflows can still be made and run**: two step presets (`Driver/Workflows/Domain/WorkflowPresets.cs:30-40`),
  `workflow new --from`, `workflow edit`, `workflow apply` (`Driver/Workflows/DriverWorkflowsCommand.cs:34-40`, `:796-845`), Ask
  Daoris's save door (`Driver/Help/Proposals/HelpWorkflowProposals.cs:116-129`), and a new start binds them
  (`Driver/Workflows/WorkflowRunBindings.cs:126-147`). D190 point 1 keeps such runs on D157's engine to their end.
- **Nothing is migrated, by decision** (D190 point 1; the land node design §4).

## Two paths for one job

- **Landing**: the doors above; the access setup's `landing: rule` switching auto-accept on outside the workflow
  (`Driver/Workplace/Landing.cs:894-912`, against the D145 note "no landing logic stays outside the graph"); `AutoConcluded.Merges`
  saying a merge lands nothing by itself (`Driver/Workplace/AutoLanding.cs:22-26`) against D215's 1c3 note; the tidy inside the
  door (`Driver/Workplace/TreeLands.cs:953-967`) and the look's own tidy pass (`Driver/Workplace/BranchTidying.cs`,
  `AutoTidies.cs`), no node for either (land node design §9 leaves it undecided).
- **The pre-graph engine**: a steps version governs through `WorkflowProcess.Of` (`Driver/Workflows/WorkflowProcesses.cs:62-107`);
  a graph version is turned back into gate steps (`:119-148`, D190 point 4 keeps it); chats bind only to Current or steps
  (`Driver/Workflows/ChatRuns.cs:13-25`, CHATFLOW1f parked); a graph holding `handed` still saves (`NodeGraphs.cs:328`, `:410-419`)
  and the editor still offers `handed`, `pushed` and `pull-request` alone (`Web/workflow/edit/StepPicker.tsx:19`).
- **Rules beside the graph**: Current drawn from the landing rule (`NodeGraphs.cs:1057-1104`); a rule's `cycle` and
  `workCycles`/`workspaceWorkCycles` as graph homes outside named workflows (`:1114-1176`); `quest.close` refused in a version
  (`WorkflowProcesses.cs:133-140`, FABRIC-CUTOVER1h parked); the terminal's `landing` and `work-cycle` verbs with stale help
  (`Driver/Settings/DriverChoicesCommand.cs:169-170`, `src/Daoris.Cli/src/cli/driver.ts:19-44`); second opinion and the look written
  as rules (`Driver/Setup/PlacePlan.cs:265-314`); Ask Daoris's landing door proposing rule shapes
  (`Driver/Help/Proposals/HelpSettingProposals.cs:384-453`).
- **Integration**: a branch rule carrying `integrate` is Current's integrate graph (`NodeGraphs.cs:1084-1089`); this repository's own
  line is still integrated by `tools/merge-branch.mjs` until INTEGRATE1 row k's flip (the owner's sitting, D215 §6.3).
- **Staffing**: `SET_ACTOR` beside `SET_ROLE` (`Modules/Bridge/Routes/DriverRoutes.cs:375-386`); with no `actors` entry, roles start
  *as before* on the old adapter keys (`Driver/Settings/ActorKey.cs:27-48`).

## The default setup a new place gets

`daoris import` writes registry rows, not driven. The place follows Current (the default merge rule, a review before `land`).
Get Started points at rules and adapter keys (`Web/help/setup.ts:62-92`), not at *Set up…* and Roles. *Set up…* writes trees,
Current, a branch landing rule (`feature/{quest}-{slug}`, plugin, `autoAccept`, `integrate`, keeping the old tidy and cycle), the
opinion and look rules, driving and the set-up quest; no named workflow is saved. The shipped presets' files each carry a
`delivery` block restating their graph's review and land in the old rule's words (`Driver/Defaults/workflows/merge-after-accept.json:6-9`).
The seeded roles start *as before*. A chat's first start binds to Current. The older adoption doors (`daoris-driver setup`,
`SETUP_PLAN`/`SETUP_PRESS`, the workspace plans on every look, `Driver/Loop/Driver.cs:1127`, `:1877-1890`) stand beside `PLACE_APPLY`.

## Migration

Read into the new shapes: staffing (imported once, written back for older builds until FOUNDATION1 row v); old rules drawn as
Current's graph at read time only; open landings adopted by Current's delivery; the alias `merge-no-press`; integration requests with
no epoch. Not migrated: steps workflows (only a person's `apply --as-graph`); graph versions holding `handed`; `land` saved with
`accept` (refused and never landing, `Driver/Workflows/Nodes/LandNodes.cs:85`); rules never turned into a saved workflow; `cycle` and
`workCycles`. A home set up long ago runs on Current from its rules, the look's `AutoLander`, the rule's tidy and the tidy pass, the
rule cycles and `workCycles`, any steps workflow it chose, and its imported actor orders.

## Dead or flag-dark

The Roles mode's dark branch and its components (`Web/agents/AgentsView.tsx:321-340`, `RolesView.tsx:329`, `RoleSections.tsx`);
`WorkflowTab` and `WorkflowStartNode` but `LandedLines`; the repository's and workspace's Workflow and Setup tabs (the workspace's
drawn even lit, D221's SETUPUX1v note); the plain `/bind` (FABRIC-CLOSEBIND1, parked); the stale rule words (`AutoConcluded.Merges`,
the merge exclusion in `Landing.Effective`, the CLI's `--auto-accept` help).
