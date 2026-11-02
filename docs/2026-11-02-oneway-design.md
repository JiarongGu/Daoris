# ONEWAY1: the design. Each job done one way, every old record converted or cleared on one press, every kept read with its end

Status: proposed 2026-11-02 under D228, from the census (`docs/2026-11-02-oneway-census.md`), read on main at `403e267e1` and on
`wave/workflowclean1` at `b022cc80c`. Revised 2026-11-03 on main at `f3ebcb986` after an independent Opus read found eight blocking
points and the steward checked three against the code (§14 lists each and where it is settled), and after the owner chose one name for
every command (§11). The reading was read-only and no tests were run. It widens a foundation (the conversion becomes every area's, and a
register holds every kept read), so under D191 it takes both makers' reads before the steward accepts it. **Accepted 2026-11-03** by the
steward after two Opus reads (the second read the revision; §14 holds what each found and where it is settled), the other maker's read
owed while its workspace's spend cap holds and folded in as a revision if it finds anything; the rows marked *now* in §10 start, each
within the files §10.1 names. WORKFLOWCLEAN1's areas (the workflows, adoption A1 to A3, page P1) are
its design's (`docs/2026-11-02-workflow-cleanup-design.md`) and are cited here, never re-designed.

Paths as the census spells them: `Drv/` `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Mod/` `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
`Svc/` `src/Daoris.Service/Daoris.Service.Core/`, `Http/` `…Service.Http/`, `Web/` `src/Daoris.Web/src/`, `Cli/` `src/Daoris.Cli/src/`.
*The wave* is `wave/workflowclean1`. Every row id is prefixed `ONEWAY1` (`ONEWAY1a`), since bare letters collide with D213's and
FOUNDATION1's rows; *row v* alone, as in *FOUNDATION1 row v*, is never this design's.

## 0. In short

- **One conversion door** (§1.1). WORKFLOWCLEAN1's conversion becomes the home's, made of parts, one per area's old shape: one
  *What needs you* row, one review, one Apply, one receipt holding each part's outcome and the build that applied it, one Undo, and
  `daoris convert` at the terminal. A failed part is retried inside its receipt. The home part is pressed alone and moves the files at
  the restart its press makes.
- **Converted where in use, cleared where finished** (§1.2). Whatever an open line, run, quest, park or wait reads is converted.
  Finished history is cleared by its store's own remover, said and sized, and is not undone.
- **One register of kept reads** (§1.3). Every transitional read is one entry naming its code, its condition and its deleting row, shown
  on This machine's page. *For an older build* ends at the home's floor, read from the build stamped in the receipt. A gate holds every
  marker to an entry and every entry to an open row, and lands green.
- **No silent rewrite** (§1.4). The three silent rewrites become parts of the conversion.
- **Staffing** (§2). Kept in the store alone, set through the role door alone. The five roles with an order of their own get it written
  out; Second opinion, Partner and Helper keep their caller's rule as a rule. The named-file case ends before the old door does.
- **Sessions** (§3). AGENTPROVIDER1m-i first, then D213's host rows, the parity rows after the moves, and the adapter classes' end on
  Daoris's own transports, with AGENTLYNTAI1's parity oracle re-cut onto `TransportRun` in the same amendment.
- **Messages** (§4). A park answered through the say door alone, `level` alone, `Said` alone (no conversion), one confirmation queue,
  and an end for each of COMMS1's kept paths.
- **Git** (§5). The ambient path goes, and moving off it is the git part. Push is git access's setting alone. Every branch delete goes
  through the journal.
- **One name, one tree** (§11). A person and a session type `daoris …` for everything. The hand-off families and `daoris-driver` as a
  typed name retire in the tree's wave, with no alias.
- **Waves** (§10): now on main, within named files; the conversion widened once the wave lands; the parts and doors; the command tree on
  its own wave branch; the messages with COMMS1; the measured parts; the sessions track; the deletions.

## 1. What every area shares

### 1.1 One conversion door (recommendation 1: accepted)

**Why.** Converting the person's old records is one job, and D228 counts a second way to do one job as a defect. A press per area
would mean nine doors, nine receipts, nine Undo rules and nine *What needs you* rows, which the person would have to tell apart. Every
area also needs the guarantees WORKFLOWCLEAN1 §4.4 and §4.5 built: a plan that writes nothing; a digest the press names; a receipt
written before the first effect, and a restart settled from it; Undo while nothing has moved. The rollback refusal an applied
conversion raises (that design's §1.5) is what ends every read kept for an older build (§1.3), and one door raises it once. *Rejected:*
a press per area. An act that is not a conversion keeps its own door, so a git identity is still set up in Settings → Git.

**Parts.** The conversion is a list of parts in `Drv/Conversion/`, moved from `Drv/Workflows/Legacy/`. The workflow part is
WORKFLOWCLEAN1's code, moved and not changed. Each part answers:
- a pure census: its items, and `Ended`;
- its plan: its operations, each said once, its row choices, and its own digest over its inputs, operations and choices;
- its effects, each idempotent;
- its *before* for Undo, and the checks Undo makes that each write still reads as written; or *not undone* where it clears;
- what it never touches (the review's section 5);
- the register entries (§1.3) it ends.

The parts, in apply order: `settings` (§5 G2, §6 X1), `accounts` (§1.4), `staffing` (§2), `git` (§5 G1), `workflows` (built),
`quests` (§6 X6), `notes` (§8 P4). Within one receipt every part's converting operations apply first, in that order, and every
part's clears last: a failed converting operation holds every clear of the receipt, so nothing irreversible happens while a
reversible part is unsettled. The `home` part (§6 X5) is never in a receipt with another part (below).

**One plan, one Apply.**
- `CONVERT_PLAN` composes every part that has something to do. A part whose source does not answer (the service, for `quests`) is
  left out of the plan and said: *not read: the service did not answer*. The plan's digest covers each part's digest.
- `CONVERT_APPLY {digest, choices}` is the person's press, confirmed in the window. The apply plans every part again:
  - a part that reads and whose digest differs refuses the whole press as `moved`, with nothing written, as WORKFLOWCLEAN1's does: the
    person read something no longer true;
  - a part whose source does not answer at Apply cannot be compared, so it is written into the receipt as `failed`, with its pressed
    digest, no operations and its sentence; the other parts go on.
- **Outcomes per part.** Each part in the receipt holds `done`, `failed`, `moved`, `left` or `uncertain`, with its sentence. A part
  that fails during its effects (a file held) is `failed`, its operations kept in the receipt.
- **A failed part is retried inside its receipt.** Pressing the same digest again settles the receipt in place, as the wave already
  does (`Drv/Workflows/Legacy/ConversionApplies.cs:74-77` on the wave). A failed part with operations runs them again. A failed part
  with none plans itself alone: the same digest writes its operations and applies them; a different one closes the part as `moved`,
  and the next plan offers it again under a new receipt. *Leave it* closes a failed part as `left`, offered again by the next plan.
  The receipt is settled when no part is `failed` or `uncertain`.

**The receipt** gains `build`, the version of the build that applied it (`InstallRollback.This`; the wave's receipt holds none,
`ConversionReceipts.cs:36-38`), and `parts`, each with its digest, operations, outcome and *before*. A receipt written before parts
existed reads as one workflow part with no build, since a stored shape is never changed in place (a register entry that `ONEWAY1ag`
ends). `Stands`, which the rollback refusal reads, becomes: some receipt not undone has a part `done` or `uncertain`, or a receipt does
not read; the wave's reads the workflow part's `configuration` operation alone (`ConversionReceipts.cs:129`).

**Undo** reverts the home's latest receipt whole, once it is settled (a newer receipt that is not settled refuses Undo, never reaching back past it), its done parts in reverse order, under WORKFLOWCLEAN1 §4.5's conditions widened
to every part. It is offered only where:
- every write the receipt made still reads as written. Each part checks its own: the staffing part each `role_staffing` row and policy
  it wrote, and `driver.json`'s four keys it removed; the settings part each key; the git part each `own` it wrote; the workflow part as
  built. A write edited since refuses Undo as `moved`, naming it;
- this build has not deleted a part's own reads (its `ONEWAY1af` step), since Undo would put back a shape it cannot read;
- this build has deleted no read that the floor ended. Undo lifts the rollback refusal (`InstallRollback.cs:40-42` on the wave), which
  is what holds the floor; once any `ONEWAY1af` step or FOUNDATION1 row v has deleted a floor-ended read, an older build staged after
  an Undo would write what this build no longer reads. The register states the first such build (`Register.FloorNeeded`), and Undo
  is refused from it on with that sentence.

Undo restores the staffing part's *before*: the rows it wrote are removed or put back as they were, and the four keys go back into
`driver.json`. Clears are listed as *not undone*.

**The home part.** Its files cannot move while Daoris holds the home open, and `DaorisHome.Entry` writes an entry at its old place
until it moves (`Drv/Platform/DaorisHome.cs:105-127`). So it is offered alone, once no other part's receipt is unsettled, and its
*Apply and restart* writes its receipt and restarts Daoris. The launcher carries out the moves from the receipt at that start before
anything opens the home, as it lays a staged update, and settles each move's outcome there. From the press until the moves are settled
the plan reads that receipt and offers nothing for the home, and the row reads *your files move at the restart*. Its Undo is the same:
a press, and the moves back at the restart. No part writes unpressed.

**The terminal.** `daoris convert [--plan | --apply <digest> | --undo <digest>]` runs through the running Daoris (RPC1e). The wave's
`workflow convert` folds into it with the command tree (§11).

**One row.** *What needs you*'s row for the conversion keeps its stored kind, `workflow-convert` (`Drv/Events/NoticesRead.cs:66` on the
wave), since a stored code is never renamed in place; its words widen: one row per home while any part holds something, reading
*`n` old records wait for your conversion · Review…*, with the count by part in its detail. Each surface that shows a part's old shape
opens the same review at that part: a place's head (as the wave built), a role's card, a held remote, an agent's account list.

**The review** keeps WORKFLOWCLEAN1 §4.3's order within each part: what changes; the row choices; what lowers the person's part; what
stays; what it never touches; a section **removed for good** (each clear with its count, dates and size); the terminal fold; one
*Apply*. Ask Daoris opens the review and never proposes the apply.

**Never touched.** WORKFLOWCLEAN1 §4.6 is the workflow part's list. Each other part declares its own, since the staffing, settings and
git parts write what §4.6 keeps the workflow part from. The door as a whole writes into no repository's own files, signs in, moves no
credential, and touches nothing no part names.

### 1.2 Converted where in use, cleared where finished

D228's note leaves this to the design. The rule for every part:

- **In use**: an open line, run, quest, park or wait reads it, or a live surface draws it (a head, a waits row, a role's card, a
  setting). In use is converted to the new shape with its meaning kept. A setting is always in use. A retired key that has no meaning
  any more is removed by its part and kept in the receipt for Undo: `timeoutMinutes`, `prefer`, `parallel`, and `gitMoved` once the
  floor passes `ONEWAY1p`'s build (§5).
- **Finished history**: nothing open reads it. It is cleared by the plan under the reason `older-shape`, through its store's one remover:
  the retention pass for home entries (D195, KEEPSIZE1; `Drv/Workplace/KeepKinds.cs:18-72`), the service's quest remover for quests
  (§6 X6), and the history clear for finished sessions. The review counts, dates and sizes it, and it is not undone. The `Pooled`
  receipts in `harnesses.json` are such history: cleared, never kept for Undo.
- **Measured first**: where a converter costs more than its use and the census may find nothing in use (`quests`, `notes`), the part's
  census is read on the owner's install first, by the parent and read-only. Its in-use converter is built only if something is found,
  as WORKFLOWCLEAN1 built its row 1j.
- **After a part's deletion step**, an old-shape record nobody converted is drawn by the one rule for any record this build cannot read
  (*a record this build does not read*), never by a reader of its own. A home whose census has not ended holds that part's new work
  with the conversion as its door, as WORKFLOWCLEAN1 §4.7 holds an old-shape place.

### 1.3 The register, the floor and the gate

- **The register.** Every transitional read kept is one entry in `Drv/Transitional/Register.cs`, with twins only where the reader lives
  outside the driver. An entry names its id; the code it keeps (file:line); what reaches it; the condition that ends it, a pure check
  over the home; and the row that deletes it. Each kept site carries the marker `// transitional: <id>`.
- **Shown.** This machine's page lists the entries with each condition's state. WORKFLOWCLEAN1's *Workflows: nothing old here* becomes
  the first line of that list. An ended entry reads *nothing old here*, and Ask Daoris's room reads the same list.
- **The floor**: the oldest build that can still write this home. It is read from two places:
  - the receipts: while an applied conversion stands, no build below the running one is staged (WORKFLOWCLEAN1 §1.5), so the build
    stamped in the oldest standing receipt is a floor. A receipt with no build gives none: every entry waiting on the floor stays;
  - the desktop store's writer stamp, for what that store holds: an older build reads it and writes nothing (D226's first note;
    `Drv/Store/DesktopStore.cs:23`). The service's store has no such stamp.

  Every entry kept *for an older build* names the build whose writes it waits out, and ends when the floor passes that build. This is
  FOUNDATION1 row v's condition made visible, one entry per pairing of its §19.12 matrix. A write that slips past the floor (a second
  install on the home) is caught by the store's own check, as a found row for staffing, or by the part's census, and is said.
- **The gate** (`ONEWAY1ad-i`): `tools/transitional-markers.mjs`, run by `verify`. It reads markers and register entries only. It fails
  on a marker that names no entry, an entry with no marker, and an entry whose deleting row is not open in `TASKS.md` (the rows are
  listed by id in ONEWAY1's entry). It lands green: `ONEWAY1ad-i` registers and marks the kept reads already on main. Each later row
  that keeps a read adds its entry and marker in the same commit, each deleting row removes both, and a row is archived only once its
  entries are gone, so `verify` stays green between rows. A row named in prose is not a marker and is not read: the owed actor door
  that names MODELROLE1h (`Drv/Knowledge/Room/HelpRoomOwed.cs:93-102`) goes with `ONEWAY1a` (§2 S1), not through the gate.

### 1.4 The three silent rewrites (recommendation 2)

| Rewrite | Today | Becomes | Kept as a pure read, until |
|---|---|---|---|
| `harnesses.json`'s old names (`early`, `near`), retired fields (`prefer`, `parallel`) and workspace lists, rewritten at the next write (`Drv/Agents/HarnessSettings.cs:753`, `:1146-1156`) | any edit rewrites every entry in today's names | the `accounts` part, on the press; an edit writes only the entry it edits | the old names read as `switch`/`curve`, the retired fields as nothing, and the workspace lists into the set, until the part's census ends (`ONEWAY1af`) |
| The account-pool fold (`Drv/Agents/AccountSet.cs:16-39`) | `HarnessSettings.Change` folds before every edit and appends `Pooled` receipts | the same part, whose plan is POOL1d's dry run; `Change` folds nothing; no `Pooled` receipt is written, and the kept ones are cleared as finished | `AccountSet.Of`, the fold every reader takes in memory: it writes nothing and is D197's meaning, so it stays until the file holds that meaning (`ONEWAY1af`) |
| The git move's mark at the first start (`Drv/Workplace/GitMove.cs:15-31`, `Drv/Loop/DriverWatch.cs:404-405`) | a start writes `gitMoved`, and from then on an unserved remote is held | the `git` part (§5); the mark's write and the ambient path removed | the key itself, read by nothing, kept until the floor passes `ONEWAY1p`'s build (§5 G1) |

None of the three stays a write. The two reads stay because they change nothing on disk and give the person's file the meaning D197
already decided. Each has its end.

## 2. Staffing (S1 to S5, K1, K5; recommendation 3)

**The end state.**
- Staffing is kept in the desktop store alone: `role_staffing` with `role_policy` (D212 w-s, D226). `driver.json`'s `actors`,
  `adapter`, `intakeAdapter` and `helperAdapter` are neither written nor read.
- Staffing is set through the role door alone: `SET_ROLE` and `SET_ROLES` in the window, `daoris role …` at the terminal (§11), and
  `role_propose` in Ask Daoris.
- **The five roles with an order of their own** (Build, Chat, Brainstorm, Intake and Ask Daoris) are never unset, and *as before* is
  gone: on a converted home each holds the order it ran, written out; a fresh home's first start writes each one's seed into the store;
  the role door's `default` writes the seed, and that is what *Clear order* becomes. A **fresh home** is one whose store does not exist
  yet and whose `driver.json` holds none of the four staffing keys.
- **Second opinion, Partner and Helper** have no order of their own: their caller's rule decides who runs (`HelperChoice.Candidates`,
  `Drv/Participants/Helpers/HelperChoice.cs:24-33`: another maker than the asker's, from the agents set up here), which
  `ActorChoices.Effective` marks by reading empty (`Drv/Roles/ActorChoices.cs:55-56`, `:95`). That is a rule, not *as before*, and it
  stays one: with no stored order each runs by it, the window and the room name it (*Another maker than the asker's*), and `default`
  clears a stored order back to it. A stored order the person set for one of them stays as set.
- The only default left is D226's `order` rule (*First that can run*).
- **FOUNDATION1 row v's condition, shown.** The copy's register entry reads the floor (§1.3). The copy stops on a home past the floor,
  and row v, with `ONEWAY1af`, deletes the code.

**S1. Setting a role's agents** (`ONEWAY1a`, after `ONEWAY1c-i`)
- *One way:* the role door.
- *Retires:*
  - `SET_ACTOR` (`Mod/Bridge/Routes/DriverRoutes.cs:374-386`) and both of its page callers, which move onto `SET_ROLE`: Roles' *Clear
    order* (`Web/agents/roles/Roles.tsx:173-183`, becoming `default`) and, on the wave, the seeded roles' staffing editor
    (`RoleEditor.tsx`'s `staffing` mode, becoming `set`; `Web/agents/roles/Roles.test.tsx:318`);
  - `driver adapter|intake|helper|actor` (`Drv/Settings/DriverChoicesCommand.cs:105-131`, `:166-216`);
  - Ask Daoris's `setting_propose` `adapter`, `intake` and `helper` (`Drv/Help/Proposals/HelpSettingProposals.cs:34-38`, `:225-308`)
    and its service twin;
  - the room's `driver actor` lines and the owed actor door (`HelpRoomOwed.cs:93-102`);
  - their usage, catalogue keys and Help coverage rows.
- *Why after `ONEWAY1c-i`:* on a home `DAORIS_DRIVER_CONFIG` names, `SET_ROLE` and `SET_ROLES` refuse (`DriverRoutes.cs:566-567`,
  `:578-579`; `Drv/Roles/RoleStaffing.cs:104-105`), so `SET_ACTOR` is that home's only editor until the named file ends.
- *Records and transitional reads:* none.
- *Seen:* every role is edited by the one editor. *Clear order* reads *Use the default* and shows what it writes. At the terminal,
  `driver actor …` is refused naming `role` (§11).

**S2. Where staffing is kept** (`ONEWAY1c-i`, `ONEWAY1b`, `ONEWAY1c-ii`)
- *One way:* the store.
- *Retires, at `ONEWAY1c-i`, before `ONEWAY1a`:* the named-file path (K5). The file `DAORIS_DRIVER_CONFIG` names is read and written
  with no store (`RoleStaffing.cs:105`); it and the variable's readers go (`Drv/Settings/DriverConfig.cs`,
  `Drv/Settings/RoleStaffingPort.cs`, `DriverChoicesCommand.cs`, `Drv/Instance/VerbLeases.cs`, and the `Program.cs` of the host and of
  the app). In the tools that set the variable (`tools/family-rehearsal.mjs`, 18 uses; `tools/desktop.mjs`,
  `tools/deployment-rehearsal.mjs`, `tools/proof-kit.mjs`) each run is given a home of its own, and the rehearsal's `driver.json`
  fixtures that write `adapter` (`tools/family-rehearsal.mjs:987`, `:2449` and the others like them) set their staffing through the role
  door instead, so every rehearsal home is fresh.
- *Retires, at `ONEWAY1c-ii`:* the copy onto `driver.json` (`RoleStaffing.cs:569-600`, `Drv/Roles/StaffingFile.cs`,
  `StaffingProjection`), and the found rows and their door (`STAFFING_FOUND_ANSWER`, `driver actor found|export|import`), which only the
  copy feeds.
- *Records (the `staffing` part, `ONEWAY1b`, converted):*
  - The four keys are removed from `driver.json` and kept in the receipt for Undo.
  - Found rows still open are listed in the review, and *Take it* stays offered until Apply. Apply clears them: each was a change an
    older build or a hand made to the copy.
- *Transitional:* the copy's write, on a home no conversion has reached. It ends when the floor passes the w-s build on that home,
  which the first Apply does; `ONEWAY1c-ii` makes the writer skip such a home, and `ONEWAY1af` deletes it. The found check ends with it.
- *Seen:* This machine's list reads *Roles' staffing, copied for older builds: ends when no older build can open this home*, with its
  state.

**S3. *As before*** (`ONEWAY1b`)
- *One way:* each of the five roles' written order; the other three's rule.
- *Retires:*
  - `ActorKey.Before` and `FirstAgent`'s fallback to the old keys (`Drv/Settings/ActorKey.cs:28-50`);
  - the readers that skip a role for the old key (`Drv/Roles/StartPlan.cs:463`, `:467`; `HelpSettingProposals.cs:775`;
    `Drv/Loop/Driver.cs:755`);
  - `DriverContext.DoorOf` (`Mod/Bridge/DriverContext.cs:184-194`), which reads `adapter` for the door's wire: it reads Build's first
    agent from the store instead (`ActorChoices.FirstAgent("build", …)`, the role `adapter` stood for), with the same fallback to the
    pipe;
  - the window's and the room's *as before* words.
- *Records (converted):*
  - Each of the five that reads *as before* is written as `ActorChoices.Effective` reads it at the plan: an agent at its account
    default, from `adapter` for Build, Chat and Brainstorm, from `intakeAdapter` for Intake and from `helperAdapter` for Ask Daoris.
  - An Intake or Ask Daoris whose key is absent is written off.
  - Second opinion, Partner and Helper are not written. Writing `Effective`'s empty read for them would turn them off, since an empty
    order is off (`DriverRoutes.cs:377`).
  - A stored entry that does not read (`ActorErrors`) is kept and said, with its sentence and the role door, never forced.
  - A role written by the plan has no policy, since D226 refuses a policy on a role with no choices of its own, so nothing is re-keyed.
- *Transitional:* `ActorKey.Before` on an unconverted home. It ends when the staffing part's census reads `Ended`, and `ONEWAY1af`
  deletes it. A build past that step that opens an unconverted home holds each such role's new starts, with the conversion as their door.
- *Seen:* the review's staffing section reads *Build: claude-code (as before) → claude-code, written*, and says nothing lowers and
  nothing spends differently. After it, no role says *as before*.

**S4. The words** (`ONEWAY1d`)
- *One way:* *role* in every window word, verb, route and catalogue key.
- *Kept:* *actor* stays only in code names and stored shapes, since a stored shape is never renamed in place: the event codes
  `actor.chose` and `actor.moved`, the record field `chosen`, and `ActorChoice`.
- *Retires:* the verbs, with S1; the route `ACTORS`, folded into `ROLES` (its `arranged` answer becomes a field of each role's row, which
  the page's `nextChoices` reads); the catalogue keys under `actor`, renamed under `role` in every shipped catalogue (D222); the
  glossary's *actor* row, its words moved to the *role* rows; the room's sections and the terminal's usage.
- *Seen:* no *actor* in the window or the room in either language. `names:check --strict` holds it.

**S5. The old Roles mode**: WORKFLOWCLEAN1's (SETUPUX1y's removal, its §6.2, and the stored `roles` mode's end in its §4.7). Nothing is
added here.

## 3. Sessions (C1 to C6; recommendation 5)

**The order.**
1. **AGENTPROVIDER1m-i, parts 2 to 4** (D213's note of 2026-10-30): part 2, the driven executor and the continuations; part 3, the
   helpers', intakes', opinions', partners' and brainstorms' starts; part 4, the conversation's runner. Nothing changes for the person.
   A half-moved seam is itself a second way, so these go first: parts 2 and 3 on main now (`ONEWAY1h-i`, `ONEWAY1h-ii`), part 4
   (`ONEWAY1h-iii`) after the wave, which changes the conversation's runner (`Drv/Loop/Chat/ChatRunner.cs:1523-1629`).
2. **D213's host rows in its §11 order**: d, e's host half, f, g, h, l's sink, m-ii, n, o, p, q, r, the corrections s-i to s-ix (each
   after its move), and u. AGENTLYNTAI1's design §9.1 item 4 leaves these rows to Daoris. Each is its own sitting, on files no open
   branch holds. They are not re-designed here.
3. **The parity rows** (`ONEWAY1g`), each right after the host row that makes its behaviour one place (the table below).
4. **`ONEWAY1ah`, the adapter classes' end on Daoris's own transports.** Every built-in door is registered in `Providers.Built()`, and
   `ISessionAdapter`, `HarnessToolchain`, `SessionWire`, `AdapterSet` and `LegacyDoor` (`Drv/Agents/Registry/LegacyDoor.cs:5-19`) go
   once the host rows leave none of them a reader. D213 row t's proof is run on today's transports.
   - *Why before Lyntai's moves.* AGENTLYNTAI1 §9.1 item 12 puts the end after every door has moved onto Lyntai's layer, which ties
     the end of a second way in this repository to another repository's release labels. The moves themselves (1d, 1e, 1f) only
     replace a provider's transport behind one contract, which is no second way. The layer's own seam's end stays AGENTLYNTAI1's last
     row.
   - *The parity oracle, re-cut in the same amendment.* AGENTLYNTAI1 §6.3 builds a move's old path as a look over `AdapterSet.Built()`
     (`docs/2026-10-30-agent-lyntai-design.md:1003-1007`), and D213's stand-in classes stay until row t as the comparison's old side
     (`docs/decisions/D213.md:354`). From `ONEWAY1ah` the watch is handed a provider set instead of an adapter set, and the oracle builds
     its look over a provider set whose door runs either path: the old path is the door's `TransportRun` (m-i), built from its
     registration and its retained spawn, dialect and tables; the new is `LayerRun`. The stand-ins' old side is their own
     `TransportRun`, so `StubAdapter` and `AcpStubAdapter` go at `ONEWAY1ah` with the rest.
   - *Amends* D213's note of 2026-10-29 (row b part 1) and its note of 2026-10-30 accepting AGENTLYNTAI1 (§6.3 item 1, §9.1 item 12),
     by D213's note of 2026-11-03, taken with the steward's acceptance of the principle.
5. **The rows gated on Lyntai** (AGENTLYNTAI1 1a to 1f, and j) stay on Lyntai's releases, outside ONEWAY1's waves.

**C1. One agent, two doors** (`ONEWAY1g`). Both doors stay, as two providers of one contract (D228's note). Each behaviour that differs
between them is a defect, removed by its row:

| What differs (census C1) | Removed by (the parked row brought back) | After | Needs the owner |
|---|---|---|---|
| A limit cools no account on the native door | TOOL4n's native limit read into the one failure channel and cooled as on the protocol door, D210's row 1c on row l's channel | D213 l | TOOL4n's recording spends an account |
| The transient retry inside a run is on the protocol door only (`Drv/Loop/Driver.cs:4530-4537`) | AGENTERRORS1 1c and 1e: one error policy, the same retry on each door through `AgentRun` | D213 m-ii, which moves the native retry once | no |
| *Try again* is briefed on the native door and resumed on the protocol door (D210) | LIMITRESUME1f with AGENTERRORS1e: one *Try again* on both doors | D213 m-ii | no |
| A word sent during a turn is taken between turns on the protocol door and held to the turn's end on the native door | MSG1h and MSG1i through m-ii's `DoorInput`; STEER3 measures other steering; BGWAIT2b says words into a woken turn | D213 m-ii | BGWAIT2b's probe and STEER3's measure need the go-ahead |
| Skills and settings are handed on the native door only | D213 row o (`ISkillLayout`, `IRuleLanguage`) for both doors; HELPWIRED1b says what each door loaded | D213 o | no |
| Parity not yet measured | SESSCTX1a with HELPMODE1a3 (resume and a fresh part on each door; full access on the installed pins); AGENTLYNTAI1s-ii (the three headless tools); FABRIC-C2b (the two-harness canary) | their rows | each spends: the owner's yes |

- *Retires:* each difference's branch, and the catalogue words that said the doors differ.
- *Records:* none, since each door's records are already read by provider id (D213 §5).
- *Seen:* the window says the same on both doors.

**C2. Adapter classes beside providers**: `ONEWAY1ah`. `LegacyDoor` is a register entry until then; it ends when D213 row a's census
finds no reader of `ISessionAdapter`.

**C3. Seven executors**: `ONEWAY1h-i` to `h-iii`. The half-moved seam is a register entry that ends at part 4, which deletes it.

**C4. A conversation's course beside a driven line's**: STAFFPOLICY1e3, as filed (`ONEWAY1i`), with SESSWATCH1a and 1b. Its interim
refusal of a conversation's moves is a register entry, which e3 ends.

**C5. Four presses to go on** (`ONEWAY1j`, after the wave)
- *One way:* hand over (`SESSION_HAND_OVER`: D212's move, D189's brief). A line goes on in a new part. Where the press names no agent,
  the role's walk picks the successor (`RoleOrders.Arranged`); where it names one, the named agent, model and effort take it (D226's
  row d note). Three cases, one press each, one route:
  - *Go on with Daoris's brief*: the words could not be taken (a cooling account, or an ended or stood-down part). The walk picks, and
    waits for an account that can run, as a start waits.
  - *Hand over to…*: the person picks another agent.
  - *Go on fresh*: a fresh context while the part can still take words. This is SESSCTX1e, re-cut to the same agent and account, after
    SESSCTX1a's measure.
- *Kept:* `SESSION_START_FROM` (*Start a conversation with these words*). A new conversation is a new line, which is another job.
- *Retires:* `SESSION_GO_ON_BRIEF` and `SESSION_GO_ON_NEW`, with their driver acts, folded into the hand-over; their hooks
  (`useGoOnBrief`, `useGoOnNew`) and both callers of each, the work page's (`Web/work/WorkFrame.tsx:925`, `:946`) and Ask Daoris's
  (`Web/help/AskConversation.tsx:13`, `:733`), which move onto the hand-over; their terminal twins, refusal keys and Help coverage
  rows.
- *Records:* a go-on-new choice kept and not yet acted on is taken by the hand-over at the next look. That read is a register entry: it
  ends when no such choice is kept, and `ONEWAY1af` deletes it.
- *Seen:* the card for words not taken shows its case's one press and *Hand over to…*, on the work page and in Ask Daoris alike. The
  context meter on the head offers *Go on fresh*. Each press says that the brief goes with the words, and who takes them.

**C6. The course's paths for an older build** (the unkeyed open, "an older host moved nothing", `held-words.json`, the import backups):
FOUNDATION1 row v's. Each is a register entry, one per §19.12 pairing, ended by the floor. Row v deletes them once each reads ended on
the owner's install. Nothing is converted, since each reads a shape only an older build writes.

## 4. Messages (M1 to M5; recommendation 4)

`ONEWAY1k`, `l`, `m` and `o` are built inside COMMS1's wave 1 (D225; `docs/2026-11-02-comms-design.md` §10) as that design's retirement
rows: COMMS1 was accepted before D228 and carries none. `k` needs COMMS1e (a press names its question), `m` needs COMMS1a and e, and
`o` needs COMMS1b and g. The chunks keep their order: ONEWAY1 closes when that wave lands. `ONEWAY1n` needs none of them.

**M1. Answering a park** (`ONEWAY1k`)
- *One way:* the say door (`SESSION_INPUT`, `daoris session say`, the service's say door) carrying the question's `id` (COMMS1 §5.2).
- *Retires:* `POST /api/sessions/{id}/answer` (`Http/Program.cs:2207-2213`) and its handler; the terminal's `answer` (`daoris session
  answer` after the tree, §11); the park card's call through the route; its usage, its room line, its catalogue keys and its Help
  coverage row.
- *Records and transitional reads:* none.
- *Seen:* the park card looks the same. Its press goes through the say door, and a stale press is refused beside its control.

**M2. The say's level** (`ONEWAY1l`)
- *One way:* `level` (`ordinary`, `urgent`, or `now`, which is the person's alone, D188).
- *Retires:* the `now` field every writer still writes beside `level`; the terminal's `--now`, a second spelling of `--level now`; the
  request file written for an older build. The headless `drive` loop's own request file stays: it runs with no pipe and no service.
- *Records:* a word waiting with `now: true` and no `level` is in use, and is read as `level: now`.
- *Transitional:* that read. It ends when the floor passes the last build that wrote `now` alone and no waiting word carries it, and
  `ONEWAY1af` deletes it.

**M3. `Session.Answer`** (`ONEWAY1m`)
- *One way:* the keyed `Said` words, with the question id.
- *Today:* `Session.Answer` is no stored value. It is computed from `Said`, joining the person's words
  (`Svc/Sessions/Sessions.cs:282-283`). The old `answer` column is already read as a said word with id `answer` where a record has no
  `said` (`:1736-1745`), and the next word written empties it (`:576-581`, `:1629`, `:1867-1873`).
- *Retires:* the computed property. Its readers (the course, the planner, attention and Ask Daoris) read the keyed `Said` words, with
  the question id where one answers a question.
- *Records:* none to convert.
- *Transitional:* MSG1a's read of the old column, for a record with no `said`. It ends when no session row holds an `answer` and no
  `said` (a census over the service's store; finished ones go with their history's clear), and `ONEWAY1af` deletes the read. The
  column stays in the schema, unread: the service's store has no writer stamp to raise, and an older build would add it again.

**M4. One confirmation queue** (`ONEWAY1n-i`, `ONEWAY1n-ii`, with PERSONDOOR1j)
- *One way:* CONFIRMWAIT1's (D156's notes; `docs/2026-10-24-confirm-wait-design.md`). A request waits as a row in *Notifications* until
  the person confirms or declines it or its asker withdraws it. The press is made at the Confirm, where its grounds are judged and a
  stale one is refused with its reason, and the asker is told.
- *Retires:*
  - the service's two-minute expiry (`Http/PersonConfirmations.cs:16-23`, `:127`). The host already holds the request as asked (the
    path, the pattern and the body to the byte, `:29-37`), so it makes the request at the Confirm instead of handing out a grant to be
    used within two minutes;
  - `Expired` as a state of a wait. A request whose grounds moved reads *refused: stale*;
  - the separate drawing of the host's cards, which become the same request row (`ONEWAY1n-ii`).
- *PERSONDOOR1j, folded:* without an expiry, agents' asks could hold all five of the host's slots. The person's terminal keeps a
  reserved slot, and an asker past its bound is refused with its waiting requests named.
- *Records:* none. The host keeps its confirmations in memory (`PersonConfirmations.cs:179-181`), so a restart or an update ends those
  waiting, as today, and each asker is told *not confirmed: Daoris restarted*.
- *Seen:* a terminal's request through the service is the same row as the pipe's, waits with no expiry until the person answers or its
  asker withdraws it, and is made at the Confirm.

**M5. Ends for COMMS1's and D226's kept paths** (`ONEWAY1o`). Each is a register entry:

| Kept | Why | Ends when | Deleted by |
|---|---|---|---|
| The helper state's and the partner receipt's `Attempted` and `Handed`, still written (COMMS1 §13 point 3) | a rolled-back driver hands nothing twice | the floor passes COMMS1b's build | `ONEWAY1af` |
| The driver's own hand against a host with no `answers: 1` (§4.2) | an older host | the floor passes COMMS1b's build, since the host and the driver ship together | `ONEWAY1af` |
| `HandedBefore`, the helper's line walk and the partner's receipt file, still read | old files | no open helper or partner ask has an old file, and the floor holds | `ONEWAY1af` |
| The legacy reader COMMS1g keeps to seed admissions (COMMS1 §13 point 8; COMMS1g builds it, so its file is named there: `AnswersAlone.Solicited` is not it) | a line with no admission yet, or after a rollback | every line with a count has its admission, and the floor passes COMMS1g's build | `ONEWAY1af` |
| `opinion.Handed` guarding an unkeyed opinion word; a question word with no `question` id; old `answered` receipts | older records | none waiting (census); finished ones cleared by the `notes` part | `ONEWAY1af` |
| D226: a seeded role's staffing copied onto `driver.json` | §2, S2 | §2's condition | `ONEWAY1af` |
| D226: the doors refusing a conversation's moves for now | STAFFPOLICY1e3 not yet built | e3 built | `ONEWAY1i` |

## 5. Git (G1 to G3, K3)

**G1. Credentials** (`ONEWAY1p`)
- *One way:* a remote is reached by a git identity, or by the person's own sign-ins where they chose `own` for its host (D198). Nothing
  is reached by ambient credentials.
- *Retires:*
  - the ambient path (`Drv/Workplace/GitDoors.cs:100`'s branch for a home not yet moved, and its HTTPS twin);
  - `GitMove.Ensure` at the first start;
  - `GitMoved` as a state, which `GitMove.Reclass` and the door's check at `GitDoors.cs:94` read: every unserved remote is held;
  - `git.own.moved` as a kind of its own (the git-auth design §7.4), whose two presses become the git part's row choices.
- *Records (the `git` part, converted):*
  - Each host whose remotes no identity serves and no `own` names is listed with its repositories.
  - The repositories whose own configuration names their own authentication, or whose ssh is not OpenSSH, are listed apart (that
    design's §6.8).
  - Per host the review offers **Set up a git identity…** (Settings → Git, host filled; the host stays held until an identity serves
    it) or **Keep using my own sign-ins** (writes `own` for the host at this machine).
  - Nothing is preselected: credentials are the person's (D228's note). Apply converts the hosts chosen, and the rest stay held, said.
- *Transitional:* the `gitMoved` key, read by nothing from `ONEWAY1p`. An older build that finds no mark calls `GitMove.Ensure`, which
  swallows its failures (`GitMove.cs:15-31`), and takes the ambient path wherever the mark is missing (`GitDoors.cs:93-100`). So the
  mark stays until the floor passes `ONEWAY1p`'s build; from then the git part's plan lists its removal, and `ONEWAY1af` deletes the
  register entry.
- *Seen:* the conversion row counts the hosts, and a held remote's `git.identity.missing` row opens the review's git section.

**G2. May a session push** (`ONEWAY1q`)
- *One way:* git access's Write (`machineGitAccess.write`, the same per repository and workspace, `SET_GIT_ACCESS`). `no-push` stays the
  harness rule composed from git access (D198 §6.8) and can no longer be switched.
- *Retires:* the reading of `no-push`'s switch as an allow (`Drv/Workplace/GitAccessRules.cs:22-23`, `:37-47`, `:75-90`;
  `GitDoors.cs:511-513`'s `|| PushesHere`), and the switch in Settings → Permissions, with its words. The publishing and release
  sentence that `no-push`'s switch also turned off becomes its own default rule, `no-publish`, switched alone, so publishing has one
  setting too.
- *Records (the `settings` part, converted):* a home whose `permissions.json` lists `no-push` in `defaultsOff` gets `no-publish` in
  `defaultsOff`, with `no-push` taken out. `machineGitAccess.write: allow` is written only where the machine's Write is unset, since
  `PushesHere` counts only where Write resolves to no value (`GitAccessRules.cs:62-71`); where Write is set, the switch changed nothing
  and nothing is written. The review says *keeps what you chose*.
- *Transitional:* `PushesHere` on an unconverted home. It ends when the settings part reads `Ended`, and `ONEWAY1af` deletes it.
- *Seen:* Settings → Git's machine card shows Write *allow* where the switch was off and Write was unset. Permissions offers
  `no-publish`'s switch and not `no-push`'s.

**G3. Deleting a branch** (`ONEWAY1r`; after the wave, whose 1c changed the look's clean-up)
- *One way:* the journal's delete (`GIT_REF_DELETE`; GITAPP2d, D204): one performer, one record, one Undo.
- *Retires:* the deletes that `DISCARD_SESSION_BRANCH`, `WITHDRAW_LANDING_BRANCH`, `SWEEP`, the look's clean-up (WORKFLOWCLEAN1
  §2.5) and `housekeeping_discard_branch` each make their own way. Each keeps its judgement of whether to delete, and deletes through the
  journal's act. Their own undo records give way to the journal's (`Drv/Workplace/RecoveryRetention.cs`, `RecoveryDropping.cs`).
- *Records:* recovery refs and records of past deletes stay in use as undo until their retention ends. They are not converted.
- *Transitional:* their reader and their undo. They end when the last recovery record from before `ONEWAY1r` passes its retention (a
  dated condition, shown), and `ONEWAY1af` deletes them.
- *Seen:* every deleted branch is in the git journal with *Undo*, whatever deleted it.

## 6. The service and the CLI (X1 to X6, K2, K4)

**X1 and K4. The retired timeout** (`ONEWAY1s`)
- *One way:* none. D200 point 9 retired the timeout. The quiet setting, `quietMinutes`, is its own job: SESSWATCH1c builds its doors
  after SESSWATCH1a, and is re-cut so that no timeout door names it. `timeoutMinutes` is not converted into it, since a wall clock and
  quiet are different settings.
- *Retires:* `driver timeout`, Ask Daoris's `timeout` door in both twins, `settings_timeout`, the reading of `timeoutMinutes` only to
  say it is retired, and their words.
- *Records (the `settings` part, cleared):* `timeoutMinutes` is removed and kept in the receipt.

**X2. A brainstorm from a terminal** (`ONEWAY1t`, with BRAINSTORM1b2 and AGENTROOM1c re-cut)
- *One way:* the service's `BrainstormDesk` is the one writer of a brainstorm's request (`brainstorms/<id>/`, twinned on
  `fixtures/brainstorm-files.json`). Sessions' tools and the terminal both reach it.
- The terminal's one verb is `daoris brainstorm` (§11), the driver's, through the desk, with `stop <id>` and `--id` (BRAINSTORM1b2). A
  meeting is a kind of it, `--kind meeting` (AGENTROOM1c, re-cut: no `daoris meeting`).
- *Retires:* the driver verb's own file writer (`ONEWAY1t`), and the CLI's wrapper (`Cli/service/brainstorm.ts`), its usage and its twin
  with the tree (`ONEWAY1ai-i`).

**X3. Remotes, browser favorites and the registry's verbs** (`ONEWAY1u`)
- *One way:* the driver library's typed operations, in D194's shape, called by the window's modules and by the terminal. The
  registry's `connect`, `retire`, `wire` and `import` join them. `connect` is its own typed operation, not `register`: it makes a
  repository's first registration from its working tree, takes `--workspace`, and targets any service URL, a remote one included
  (`Cli/service/connect.ts:38-44`, `:80`, `:185-192`), where `register` only re-follows the rows this machine's host already holds
  (`Drv/Instance/RegisterCommand.cs:3-5`). The family rehearsal's `connect --workspace` against two hosts is its proof.
- *Retires:* `Cli/service/remotes.ts`, `Cli/service/remotemap.ts`, `Cli/browser/browser.ts`, `Cli/service/connect.ts` and
  `Cli/service/manage.ts`, with their tests and twins, at `ONEWAY1ai-i`, when `daoris remote`, `daoris browser` and `daoris repository`
  become the driver program's groups (§11).

**X4 and K2. One name per flag and verb**: the command tree (§11, `ONEWAY1ai`). `harnesses.json`'s old names, retired fields,
workspace lists and fold are the `accounts` part (§1.4, `ONEWAY1v`). POOL1d reads its plan on the owner's install rule by rule before
the owner presses.

**X5. Where a home's file lives** (`ONEWAY1w`; HOMELAYOUT5 measures first)
- *One way:* D172's places.
- *Retires:* `DaorisHome.Entry`'s old place (`Drv/Platform/DaorisHome.cs:105-127`), `Places`' second place (`:152-164`), `FolderOf`'s
  old home (`:166-183`) and their twins; the trees under two roots; the old `driver.lock`; D63's move-in; the flat service host
  (LEGACYHOST1).
- *Records (the `home` part, converted, pressed alone, §1.1):*
  - Each entry at its old place is moved to its new one at the restart the press makes, from the receipt, before anything opens the
    home.
  - An entry at both places (`HomeEntryTwiceException`) is listed with both places' dates and sizes, and the person chooses which
    stays.
  - Trees, rooms, kept files and account homes are moved only where HOMELAYOUT5 measured the move safe, and `bin/` never.
- *Transitional:* the reads of the old places. They end when the home part reads `Ended` and the floor passes D172's build, and
  `ONEWAY1af` deletes them.
- *Seen:* the review's home section, offered once the other parts are settled, with *Apply and restart*.

**X6. Six-character quest ids** (`ONEWAY1x`; measured first)
- *One way:* twelve characters.
- *Retires:* `LegacyIdLength` and its two uses, the same ask found under either width at a publish and the forgotten check
  (`Svc/Quests/Quests.cs:725`, `:1322-1326`, `:1366`).
- *Records (the `quests` part):*
  - Closed six-character quests and their forgotten entries are finished history. The service has no remover of a closed quest today:
    `DeleteAsync` refuses one (`Quests.cs:2931`), and the driver's retention pass covers home entries only. So `ONEWAY1x` adds the
    service's one remover of a closed quest, tombstoned where it may have travelled as `DeleteAsync` tombstones, which the plan's clear
    calls. The review says that the same words published later make a new quest.
  - Open ones are expected to be none. If the census finds any, the part holds until each closes. A converter that renames a quest
    through its peer is not built unless the owner's install shows one that will not close.
- *Transitional:* none after `ONEWAY1x`.

## 7. This repository's integration (I1 to I3; `ONEWAY1y`)

- **I1 and I2.** INTEGRATE1 row k, the owner's sitting, is widened to name every twin it retires: `tools/merge-branch.mjs` and
  `tools/merge-queue.mjs`; merge-branch's backlog merge, beside `Drv/Workplace/BacklogMerge.cs`, which stays; `tools/merge-rows.mjs`'s
  git driver beside the declared rows merge, becoming one implementation that both git and the fabric call. FABRIC-PIPELINE1e, f, g, h
  and j stay under it as filed. The tools are a register entry that ends at row k.
- **I3.** WORKFLOWCLEAN1's: the merge rule's press ends with the landing rules, at its row 1z.

## 8. The page (P1 to P4)

**P1.** Nothing is left. On the wave the only file that names *Set up for agents* is `Web/projects/ProjectsView.test.tsx`, whose four
lines assert its absence and stay. The census read main before WORKFLOWCLEAN1 1h. The stored `roles` mode is WORKFLOWCLEAN1 §4.7's.

**P2. The life-flows on the canvas** (`ONEWAY1aa`, after the wave, CANVAS1e and f, and SETUPUX1y)
- *One way:* CANVAS1's `PositionedCanvas` (D224, built at CANVAS1d), in display mode, for every graph Daoris draws.
- *Retires:* `Web/design/Flow.tsx` and its drawing of the landing, quest, parts and session flows (`LandingFlow`, `QuestFlow`,
  `PartsFlow`, `SessionFlow` with their `…Life.ts`), whose pieces move onto the canvas's nodes and edges beside CANVAS1e and f's maps.
  `Flow.tsx` is also imported by the workflow chart (`Web/workflow/chart/FlowChart.tsx:7`, `chart/stepKinds.ts:1`), `graphFlow.ts:2`,
  `workflowFlow.ts:2`, `WorkflowChart.tsx:4`, `WorkflowStartNode.tsx:3` and the canvas's own story (`Web/design/canvas/Canvas.stories.tsx:4`):
  neither map imports it, so CANVAS1e and f do not retire it. What `ONEWAY1aa` needs from them is CANVAS1e's positioned world (a
  supplied layout, a feature's own node and edge drawing, pressable typed lines), onto which aa moves the life-flows and the workflow
  chart's imports; `WorkflowStartNode` goes with SETUPUX1y. CANVAS1's §6 kept `Flow.tsx` for the inline flows; this row supersedes that
  keep (D228). So the row waits for CANVAS1e's seam and SETUPUX1y, not for CANVAS1f.
- *Seen:* the same flows, with fit and focus. For the page builders: stories at 80, 39 and 25 rem, in en and zh.

**P3. Branches for an older shell** (`ONEWAY1ab-i` now, `ONEWAY1ab-ii` after the wave)
- *One way:* the page and the shell are one build (D139).
- *Retires:* every field marked *absent on an older shell*, made required, with its fallback (`Web/data/signals.ts:21-80`,
  `Web/agents/tools.ts:29-168`, `Web/agents/SignInSteps.tsx:40`, `:254` and its story); the pulls kept for an older shell
  (`Web/data/queries.ts:270`, `:368`, `:606`), the plain browser's path staying only as the harness of the stories and e2e, named so;
  the modules' routes kept for an older page. `ONEWAY1ab-ii` takes those in `Web/bridge/repositories/lines.ts`,
  `Web/bridge/sessions/trees.ts` and `Web/bridge/shell.ts`, which the wave changes. The named files are examples: P3 covers every
  older-shell branch in the page (about three hundred sites in ninety files, `bridge/driver.ts` the largest) and every pull kept beside
  a slice, `USAGE` and `ACCOUNTS_READ` among them where no terminal or Ask Daoris reads them. `ONEWAY1ab-i` takes those in files the
  wave left alone, in two parts that each typecheck: the pulls and the named files, then the rest of the fields.
- *Kept:* a fallback for a record that travels between machines, such as a quest or a channel message, which another machine's build
  writes (D178). A fallback for the local service host, which a second install may run, is under the floor.

**P4. Note shapes from before codes and parts** (`ONEWAY1ac`; measured first)
- *One way:* a coded note (D142, LANG1a and 1b).
- *Retires:* the readers of a note from before parts, and of one with no code from a shell before codes (`Web/work/noteLines.ts:783`,
  `:943`; `Web/work/Note.tsx:22`; `Web/work/attention/attention.ts:1003`), their stories (`Web/work/Note.stories.tsx:84-93`,
  `Web/work/attention/attention.stories.tsx:120`, `:747`, `Web/work/head/SessionHead.stories.tsx:451`) and their driver twins.
- *Not old shapes:* a code this page does not know (FABRIC-UNKNOWN1) and a current driver sentence with no code. Both stay.
- *Records (the `notes` part):* finished records are cleared through retention. In-use ones are expected to be none. If the census finds
  any, `ONEWAY1ac` rewrites each as the coded note `recorded`, carrying its English as a value.

## 9. Across (`ONEWAY1ad` and WIRING1d)

- **`ONEWAY1ad`** is §1.3: the register and the gate (`ad-i`, now), and the floor with This machine's list (`ad-ii`, after `ae`).
- **WIRING1d** puts every setting in one list (`SYSTEM_SETTINGS` and the page's catalogue). Its census holds the route, the catalogue and
  every `SettingRow` together, so a setting with two keys fails it. It is built after `ONEWAY1a`, `s` and `q`, so it holds the final keys.
- **Outside the 37 jobs**, the triage brought DOCSHOME1, LAYOUT5, LAYOUT6 and RPC1g back as new areas. Each keeps its own row and
  design, and none is settled here.

## 10. Waves and rows

### 10.1 The order, and what may be built on main now

1. **Wave 0, on main now, beside the wave.** Each row touches only the files named, which the wave leaves alone:
   - `ONEWAY1ad-i`: new files (`Drv/Transitional/Register.cs`, `tools/transitional-markers.mjs` and its test), the root `package.json`'s
     `verify` line, and marker comments at kept sites in files the wave does not change; a site in a file it changes is marked at
     `ad-ii`. No floor: it reads the wave's receipt and `InstallRollback.cs`, so it is `ad-ii`'s.
   - `ONEWAY1n-i`: `Http/PersonConfirmations.cs`, the host's confirmation routes and PERSONDOOR1j's slots, and the modules' bridge to
     them. Not `Web/locales/*/person.json` or `Web/bridge/person.test.tsx`, which the wave changes: the page is `n-ii`.
   - `ONEWAY1t`: `Drv/WorkOrders/BrainstormCommand.cs`, `MeetingCommand.cs`, `Drv/Loop/DriverBrainstorms.cs`, `DriverMeetings.cs`, the
     service's `BrainstormDesk`, and their tests. Not the host's `Program.cs` or the CLI, which the tree takes.
   - `ONEWAY1u`: new typed operations in the driver library and the window modules' calls onto them. Not the CLI, which the tree takes.
   - `ONEWAY1ab-i`: the web files P3 names, apart from the three `ab-ii` takes, and no module route the wave changed: its modules part
     waits for `ab-ii` where a pull route it retires sits in a route file the wave touched.
   - `ONEWAY1h-i`, `ONEWAY1h-ii`: the executors' files, `Drv/Loop/Driver.cs` outside the nine lines the wave adds (`:738-746`). Not
     `DriverWatch.cs`, `DriverCommand.cs`, `Loop/Chat/`, `Participants/Chat/` or `Participants/Domain/AccessRules.cs`, which the wave
     changes.
2. **Wave 1**, once the wave reaches main: the conversion widened (`ae`) and the floor with This machine's list (`ad-ii`).
3. **Wave 2**: the parts and the doors, after the wave: `c-i`, then `a`; `b`, `c-ii`, `d`, `s`, `q`, `p`, `r`, `v`, `j`, `h-iii`, `n-ii`,
   `ab-ii`, WIRING1d; and `aa` once CANVAS1e and f and SETUPUX1y are built.
4. **Wave 3**: the command tree (`ai-i` to `ai-iv`), built on a wave branch of its own that reaches main whole, so no shipped build
   carries two names. After `a`, `d`, `j`, `s`, `t` and `u`, so it maps only the verbs that remain.
5. **Wave 4**, inside COMMS1's wave 1: the messages rows (`k`, `l`, `m`, `o`).
6. **Wave 5**: the parts measured on the install first, and the integration sitting.
7. **The sessions track**, serial, from now: AGENTPROVIDER1's and D213's rows, each its own sitting.
8. **Wave 6**: the deletions, part by part, once the install's census of that part has ended.

Each row is sized for one builder's sitting.

### 10.2 The rows

| Row (lanes; after) | Contract | Proof | When |
|---|---|---|---|
| **Wave 0** | | | |
| **ONEWAY1ad-i** the register and the gate (driver, tools) | §1.3 | the gate green on main with the kept reads registered; red on a planted marker that names no entry, an entry with no marker, and an entry whose row is closed | now |
| **ONEWAY1n-i** one confirmation queue at the service, with PERSONDOOR1j (service, modules) | §4 M4 | a host request left an hour then confirmed makes its press and tells its asker; a declined one does nothing; a stale one is refused with its reason; six agents' asks leave the person's slot | now |
| **ONEWAY1t** one brainstorm writer (service, driver) | §6 X2 | `BrainstormDeskTests` and the verb's tests (start, stop, one id asked twice, a meeting as a kind); the twin fixture; the driver's own writer gone | now |
| **ONEWAY1u** remotes, favorites and the registry's verbs as typed operations (driver, modules) | §6 X3 | the typed operations' tests; the window's edit on the fixture files equal to today's; `connect`'s, `retire`'s, `wire`'s and `import`'s registry answers equal to the CLI's on the same fixture | now |
| **ONEWAY1ab-i** no branch for an older shell (web, modules) | §8 P3 | the typecheck with the fields required; page tests; the routes' census | now |
| **ONEWAY1h-i**, **h-ii** m-i parts 2 and 3 (driver) | D213's m-i note; the AGENTLYNTAI1 design §9.2 | that design's §12 line; every moved executor's suite whole | now |
| **Wave 1** | | | |
| **ONEWAY1ae** one conversion door (driver, modules, web; the wave) | §1.1, §1.2 | the workflow part's tests pass unchanged through parts; a planted second part in each outcome; an unreachable part at Apply written `failed` and retried in its receipt, then once moved; *Leave it*; a receipt without `parts` or `build` read; `Stands` from a non-workflow part; Undo of a two-part receipt, refused when a write moved and when `FloorNeeded` is set; a restart mid-apply; `daoris convert`; the row; the review's sections at 1546 and 680 px, en and zh | after the wave |
| **ONEWAY1ad-ii** the floor and This machine's list (driver, modules, web; ae, ad-i) | §1.3 | the floor from fixture homes: no receipt, a receipt with no build, one with a build, a store stamp; the list from a fixture register, one entry ended and one not, en and zh; the wave's §4.7 entries registered | after the wave |
| **Wave 2** | | | |
| **ONEWAY1c-i** the named file's end (driver, tools) | §2 S2, K5 | `DAORIS_DRIVER_CONFIG` read nowhere; each tool's run on a home of its own; the rehearsal's homes fresh; rehearsals run by the parent | after the wave |
| **ONEWAY1a** one staffing door (driver, modules, web, service; ae, c-i) | §2 S1 | each retired door gone; both page editors on `SET_ROLE`; `HelpCoverageTests`; the room's goldens | after the wave |
| **ONEWAY1b** the staffing part (driver; a) | §2 S2's records, S3 | a fixture home with Build *as before*, Intake off, Helper unset, a stored order and an entry that does not read: plan, Apply (Helper unwritten and still by its rule), a start running the same choice, Undo restoring the rows and keys, Undo refused after a role is edited; `DoorOf` from the store; a fresh home seeded with written orders | after the wave |
| **ONEWAY1c-ii** the copy's end (driver; b) | §2 S2 | a converted home's write leaves `driver.json` alone; an unconverted home is copied as today | after the wave |
| **ONEWAY1d** the words (help, web locales, driver; a) | §2 S4 | `names:check --strict`; parity; no *actor* in the window or the room; `ACTORS` folded into `ROLES` | after the wave |
| **ONEWAY1s** the timeout doors (driver, service; ae) | §6 X1 | each door gone; the settings part clears the key | after the wave |
| **ONEWAY1q** push in git access (driver, modules, web; ae) | §5 G2 | a home with `no-push` off and Write unset, converted: a session pushes as before and `no-publish` is off; one with Write set: Write untouched; the judge reads git access alone | after the wave |
| **ONEWAY1p** the git part (driver, modules, web; ae) | §5 G1 | an unmarked home whose remotes were reached ambiently: held after the update, the row counting hosts; `own` chosen for one host and an identity for another; `gitMoved` kept, then offered for removal past the floor; `GitHttpsProcessTests` | after the wave |
| **ONEWAY1v** the accounts part (driver; ae) | §1.4 | a fixture `harnesses.json` holding `early`/`near`, `prefer`/`parallel`, workspace lists and `Pooled`: an edit writes only its entry; the plan folds; `Pooled` cleared; Undo | after the wave |
| **ONEWAY1r** every delete through the journal (driver, modules; the wave) | §5 G3 | each delete door through `GIT_REF_DELETE`, its journal entry and its Undo | after the wave |
| **ONEWAY1j** one press per go-on case (driver, modules, web) | §3 C5 | each case through `SESSION_HAND_OVER` (the walk's choice, a named agent, fresh), on the work page and in Ask Daoris; the two routes gone; `HelpCoverageTests`' rows; stories | after the wave; *Go on fresh* after SESSCTX1a |
| **ONEWAY1h-iii** m-i part 4, the conversation's runner (driver) | as h-i | as h-i | after the wave |
| **ONEWAY1n-ii** the host's cards as request rows (web) | §4 M4 | the page draws one row for both; en and zh | after the wave |
| **ONEWAY1ab-ii** the three bridge files (web) | §8 P3 | as ab-i | after the wave |
| **WIRING1d** every setting in one list (web, modules; a, s, q) | §9 | its census, as filed | after the wave |
| **ONEWAY1aa** the life-flows on the canvas (web; CANVAS1e, f, SETUPUX1y) | §8 P2 | each flow's tests on the canvas; stories at 80, 39 and 25 rem, en and zh; `Flow.tsx` gone | after those |
| **Wave 3** (its own wave branch) | | | |
| **ONEWAY1ai-i** the tree's routing (driver, cli, tools; a, d, j, s, t, u) | §11 | `fixtures/command-tree.json` held equal to both programs' routers and usage; each old form refused naming its new; the CLI's TypeScript editors and wrappers gone; doctrine reaching no spawn; the install's `daoris` launcher with no Node for a desktop group | the tree's wave |
| **ONEWAY1ai-ii** the driver's words (driver, modules, service for the room's twin) | §11 | usage, briefs, continuation lines, refusals and the room's goldens name `daoris …` alone; an appointed role's terminal allowlist (`Mod/Platform/HolderTerminal.cs:29`, `:45-47`) rewritten to the new forms, as narrow as today's; the root README's `daoris connect` held; `HelpCoverageTests` | the tree's wave |
| **ONEWAY1ai-iii** the page's words (web) | §11 | catalogues en and zh, the glossary, stories, tests and e2e specs; `i18n:check`, `names:check --strict` | the tree's wave |
| **ONEWAY1ai-iv** tools, rehearsals and documents (tools, docs) | §11 | the rehearsals and the user guides name `daoris …` alone; the `daoris-driver` launcher a refusal; D194's note; rehearsals run by the parent | the tree's wave |
| **Wave 4** (inside COMMS1's wave 1) | | | |
| **ONEWAY1k** a park answered by the say door alone (service, driver, web; COMMS1e) | §4 M1 | the route and the verb gone; a park answered through the say door with its question id | with COMMS1 |
| **ONEWAY1l** `level` alone (service, driver; COMMS1a) | §4 M2 | no writer writes `now`; `--now` refused naming `--level now`; a waiting `now: true` word read as `now` | with COMMS1 |
| **ONEWAY1m** `Said` alone (service, driver, modules, web; COMMS1e) | §4 M3 | each reader on `Said`; `Session.Answer` gone; an old-column record read as its said word | with COMMS1 |
| **ONEWAY1o** the ends of COMMS1's and D226's kept paths (driver, service; COMMS1b, g) | §4 M5 | each entry registered with its condition; the gate green | with COMMS1 |
| **Wave 5** (measured on the install first) | | | |
| **POOL1d** the accounts part read on the install (the parent; v on the install) | §1.4 | the plan rule by rule in the private notes, then the owner's press | the install |
| **HOMELAYOUT5** what D172 never moves, measured (the parent) | §6 X5 | its measurements in D172's note | the install; a sign-in per harness, the owner's yes |
| **ONEWAY1w** the home part (driver, the launcher project `Daoris.Desktop.Launcher`, which links `DaorisHome.cs` and gains the receipt's reader; HOMELAYOUT5, ae) | §6 X5, §1.1 | a fixture home with an entry at each place, and one at both; offered alone once the rest is settled; moved at the restart its press makes; not offered again before; Undo at a restart; a home started without the install's launcher (a development run) is moved by the driver's own start before anything else opens, through the same reader | after its measure |
| **ONEWAY1x** the quests part (service, driver; the install's census) | §6 X6 | the service's remover of a closed quest, tombstoned where it travelled; closed six-character quests cleared; the same words published again make a new quest; the legacy lookups gone | after its census |
| **ONEWAY1ac** the notes part (driver, web; the install's census) | §8 P4 | finished notes cleared; the readers gone; an unreadable record drawn by the one rule | after its census |
| **ONEWAY1y** INTEGRATE1 row k, widened (the owner's sitting) | §7 | as row k names it | the owner |
| **The sessions track** | | | |
| **ONEWAY1h** D213's host rows, d to u (driver, and service, web and tools where named) | §3, step 2 | each row's own proof in D213 §11 | serial; D213 i, j, l, m, s-v spend (the owner's yes) |
| **ONEWAY1g** the parity rows (driver; their host rows) | §3 C1 | each difference's old and new cases paired on both doors | per the table; the measures are the owner's |
| **ONEWAY1i** STAFFPOLICY1e3 | §3 C4 | as filed | as filed |
| **ONEWAY1ah** the adapter classes' end (driver; the host rows) | §3, step 4 | D213 row t's proof on today's transports; the oracle's look over a provider set, old path on `TransportRun`; the census's exceptions only the permanent ones | after ONEWAY1h; the installed proof spends |
| **Wave 6** (the deletions) | | | |
| **ONEWAY1af** the transitional reads deleted, part by part (driver, modules, service, web; each part's census ended on the owner's install, in a dated note) | §1.3's entries | the build with that code gone; an old-shape fixture held, with the conversion as its door; `FloorNeeded` raised where a floor-ended read goes | after each census ends |
| **FOUNDATION1 row v** (C6, and the copy's reader) | §3 C6, §2 S2 | as filed, each pairing shown ended; `FloorNeeded` raised | after the floor |
| **ONEWAY1ag** the conversion's own readers | WORKFLOWCLEAN1 §4.7 | the conversion gone from the build | when the owner says no older home remains |

### 10.3 What needs the install or the owner

- **Spends an account**: TOOL4n's recording; SESSCTX1a with HELPMODE1a3; AGENTLYNTAI1s-ii's measure; FABRIC-C2b; D213's installed
  proofs (its rows i, j, l, m, s-v, and `ONEWAY1ah`); BGWAIT2b's probe; STEER3's measure.
- **A sign-in, or account homes moved**: HOMELAYOUT5.
- **Read-only on the install, by the parent**: POOL1d's plan; the `quests` and `notes` censuses; each part's `Ended` before
  `ONEWAY1af`.
- **The owner's own press**: each conversion's Apply on the install, in the window, and the home part's *Apply and restart*;
  `ONEWAY1y`'s sitting.

## 11. The command tree (the owner's choice)

**The choice.** One name, `daoris`. A person and a session type `daoris …` for everything: the doctrine, the service and the desktop's
verbs, in one tree of groups (`daoris role set …`), never a hand-off family nested inside it (`daoris driver role set …`).
`daoris-driver` stops being a name anyone types or is taught. Under D228 the hand-off families and `daoris-driver` as a taught name are
second ways, so they retire in the tree's own wave (`ONEWAY1ai`).

**Two programs, one tree.**
- **The CLI** (the npm package, TypeScript) owns the doctrine commands, at the root as today: `analyze`, `check`, `doctor`, `index`,
  `init`, `status`, `sync`, `upstream`. They stay offline, reach no spawn and have no runtime dependencies; a machine with the CLI alone
  keeps them whole, and `AGENTS.md` keeps its constraint (doctrine works without a service).
- **The desktop's program** (the driver's host) owns every other group. The verbs the CLI still carries in TypeScript move onto the
  driver library's typed operations, as D194 moved the four families: `brainstorm` (`Cli/service/brainstorm.ts`), `remote` and
  `browser` (§6 X3), and the registry's `connect`, `retire`, `wire` and `import` (`Cli/service/connect.ts`, `Cli/service/manage.ts`),
  `connect` as a typed operation of its own beside `register` (§6 X3).
- **The hand-off.** The CLI hands every word outside its own commands, whole, to the desktop's program under D194 point 1's program
  rule (`DAORIS_DRIVER_PROGRAM`, else the install's own; else refused, exit 2, saying where the program is). An install's `daoris`
  launcher in `app/bin/` runs the desktop's program, which needs no Node; that program hands the doctrine commands to the CLI the
  install carries in `app/cli/`, on the `node` the PATH finds, as the launcher does today. A word handed once is never handed back: a
  word neither program owns is its usage, exit 2.
- **One declaration.** The tree is declared once, in `fixtures/command-tree.json`: each group, its owner and its sub-verbs. Both
  routers, both usage texts and the twin tests are held to it. `daoris help [<group>]` and `--help` print it; the CLI alone prints the
  doctrine commands and says the rest needs the desktop's program.

**The groups, and where today's verbs land.** `daoris-driver <v>` is written *d:v*; `daoris <family> <v>` is written *family v*.

| Group | Sub-verbs | From |
|---|---|---|
| `agent` | list, install, update, login, key, pin, unpin, which, trust, settings, rules; `agent account` list, add, remove, order, use, ready, rename | *agent …*; *agent profile …* |
| `ask` | `"…"` (a new ask); publish, close, delete, pause, resume, abandon, clear, go-ahead, review, workflow, channel, at-work | *d:ask*, its act flags (`--publish`, `--close`, `--delete`, `--pause`, `--resume`, `--abandon`, `--clear`, `--go-ahead`, `--set-review`, `--set-workflow`) becoming sub-verbs |
| `ask-daoris` | list, resume, rename, pin, unpin, delete, mode | *d:help* (so `help` is the tree's usage) |
| `brainstorm` | `"<question>"`, show, send, stop, clear, list, measure; `--kind meeting` | *d:brainstorm*, *d:brainstorm measure*, *d:meeting*, the CLI's `brainstorm` |
| `browser` | favorite, extensions, use, links | the CLI's `browser` |
| `chat` | (one verb) | *d:chat* |
| `convert` | `--plan`, `--apply`, `--undo` | the wave's *d:workflow convert* |
| `drive` | (one verb, the headless loop) | *d:drive* |
| `gate` | run, verify | *d:run-heavy*, *d:verify* |
| `git` | branches, branch, tag, fetch, pick, revert, rebase, review, identity, use, access, asks, answer | *d:git*, *d:git identity*, *d:git use*, *d:git access*, *d:git asks*, *d:git answer* |
| `history` | (read), clear | *d:history* |
| `integrate` | (one verb) | *d:integrate* |
| `logs`, `notices`, `trace`, `wiring`, `update` | as today | *d:logs*, *d:notices*, *d:trace*, *d:wiring*, *d:update* |
| `place` | list, `<repository>…` (the quick set-up's plan), setup, drive, undrive, hold, resume, trees, line, landing, work-cycle, across, standing, language, review, opinion, helpers, commits, access, heavy, checks | *d:place*, *d:setup*, *driver list*, *driver drive…commits*, *d:access*, *d:heavy*, *d:checks* |
| `plugin` | list, add, update, remove, enable, disable, new, try, install, show, activity, approve, withdraw | *plugin …*, *d:plugins …* |
| `press` | result, withdraw | *d:result*, *d:withdraw* |
| `quest` | pause, resume, abandon, delete, accept, done, reopen, go-on, check, review, workflow, clear, retry | *d:quest …*, *driver retry* |
| `remote` | list, add, remove; sync (status, dismiss, forward) | the CLI's `remote`; *d:sync*, whose name the doctrine's `sync` holds |
| `repository` | connect, register, retire, wire, import | the CLI's `connect`, `retire`, `wire`, `import`; *d:register* |
| `role` | `[<id>]`, set, remove, default, policy, brief | *driver role*, *d:roles brief*; *driver actor*, *adapter*, *intake* and *helper* retired at `ONEWAY1a` |
| `session` | list, stop, finish, decline, archive, unarchive, delete, say, take-back, reword, start-from, hand-over, helper, partner, handback, opinion, answer, supervise | *d:sessions …*, *d:sessions supervise*, *d:opinion …*, *d:answer* (until `ONEWAY1k`); *go-on-new* retired at `ONEWAY1j` |
| `settings` | notify, strikes, cooloff, keep, cap, handover, brainstorms | *driver notify*, *strikes*, *cooloff*, *keep*, *cap*, *handover*, *brainstorms*; *driver timeout* retired at `ONEWAY1s` |
| `space` | (read), free | *d:space*, *d:space free* |
| `steward` | as today | *d:steward* |
| `tool` | as today | *tool …* |
| `tree` | list, remove, clean, land, hand, state, sync | *d:trees* |
| `work` | state, held, recovery | *d:work …* |
| `workflow` | show, list, new, edit, apply, export, import, use, kind, run, keep, skip, stop, changes | *driver workflow …*, *d:workflow …* |

A group holding acts on one object is named by that object in the singular (`session`, `tree`, `plugin`), which ends the two plugin
families and the plural `sessions` and `trees`.

**One name per flag** (row v of the census, folded here):
- `--profile` → `--account` (`chat`, `agent login`), with `agent profile` → `agent account`;
- `--adapter` → `--agent` (`chat`), `--reviewer` → `--agent` (`session opinion ask`), `--reviewers` → `--agents` (`place opinion`);
- `--early` and `--near` → `--switch` and `--curve` (the accounts' curve);
- `--now` → `--level now` (`session say`; `ONEWAY1l` retires it with the field);
- a flag that names an act becomes a sub-verb (`ask`'s, above).

**How a session is handed `daoris`.** A session already finds the install's `app/bin/` (This machine's wiring reports which launcher a
terminal finds first). From the tree's wave that folder holds `daoris`, which runs the desktop's program, and every brief, continuation
line, refusal, room line and usage a session reads names `daoris …`. The wiring's check of the first `daoris-driver` becomes the first
`daoris`, said where one found earlier on the PATH (a global npm install) is not this install's and cannot reach its program.

**How every install carries `daoris`.** The publish lays `daoris` and `daoris.cmd` in `app/bin/` as the desktop's program's launchers,
and the CLI in `app/cli/` as today, reached through that program for the doctrine commands. A desktop group needs no Node; the
doctrine commands need Node 22 or later, as today.

**What D194 point 1 becomes.** The CLI hands every word outside its doctrine commands to the desktop's program, and an install's
`daoris` runs that program, which hands the doctrine commands back to the CLI it carries. The families' nesting (`daoris
driver|agent|plugin|tool …`) ends: each is a group of the tree. Point 1's program rule, its spawn site, its streams and its exit codes
stand. D194's note records it.

**The old names' end. No alias.** Every old form is refused, exit 2, naming its new form (`daoris driver role set` → `daoris role set`),
and does nothing. `app/bin/daoris-driver` and its `.cmd` stay one release as a launcher that runs nothing and prints that refusal, for a
session an older build briefed and carried on over the update. Both refusals are one register entry: it ends when no open session was
started before this install took the tree's build, and `ONEWAY1af` deletes them. The tree is built on its own wave branch, so no shipped
build answers to both names.

**The program underneath.** The desktop's program keeps its folder and file name, `app/daoris-driver/daoris-driver`, for good. It is no
name a person types or a session is taught: nothing in `app/bin/` runs it by that name after the old launcher's release, and
`DAORIS_DRIVER_PROGRAM` is a developer's variable. A rename would touch every tool and test that starts it, for nothing a person sees;
the condition that would end it is the two programs becoming one, which no row plans.

## 12. The census, corrected

- **A1 and P1.** *Set up for agents* is gone from the page on the wave (1h). The only file that names it there asserts its absence. The
  census read main before 1h.
- **S1.** `SET_ACTOR` is not only *Clear order*. On the wave, the seeded roles' staffing editor writes through it too
  (`Web/agents/roles/Roles.test.tsx:318`), and on a home `DAORIS_DRIVER_CONFIG` names it is the only editor.
- **S3.** Second opinion, Partner and Helper have no *as before*: their caller's rule decides, and an empty order is off.
- **K2.** The account-pool fold is two things: a pure read in memory that every reader takes (`AccountSet.Of`), and a write at every
  edit (`HarnessSettings.Change`). Only the write is silent.
- **G1.** The git-auth design's §6.8 planned the move with its own *What needs you* row and two presses, which were never built
  (GITAUTH1e's *Left*). What is silent is the mark at the first start.
- **G2.** D198 §6.8 says the switch is read once and written as `machineGitAccess.write`. The code reads it at every act and writes
  nothing (`GitDoors.cs:513`), and only where Write is unset, so the conversion is new work, not a leftover.
- **M3.** `Session.Answer` is computed from `Said`, and the old column is already read as a said word. Nothing is converted.
- **M4.** CONFIRMWAIT1's queue on the pipe is built. Only the service's person door keeps the expiry, and its waits live in memory.
- **X6.** A six-character id is resolved only at a publish and in the forgotten check (`Quests.cs:1322-1326`, `:1366`), not by every
  lookup. No remover of a closed quest exists yet.
- **P3.** The page's fallbacks for an older shell are dead under D139. Those for an older host may serve a record from another machine
  (D178), so not all of them are second ways.
- **P4.** A code the page does not know, and a current driver sentence with no code, are not old shapes.
- **I2.** The twins are two implementations of one merge. Their end is one implementation with two callers, not the loss of git's
  driver.
- **Outside the census, and not a second way.** The CLI's `connect` (a first registration from a working tree, any service) and the
  driver's `register` (re-following the rows this host holds) are two acts; the tree keeps both under `repository`.

## 13. Rejected

- **A press per area**, which would be a second conversion door (§1.1).
- **Keeping finished history readable by its own old reader** (D228's note).
- **A preselected choice in the git part**: credentials are the person's.
- **`timeoutMinutes` converted into `quietMinutes`**: they are different settings.
- **Renaming stored *actor* codes and fields, or the stored `workflow-convert` kind, in place.**
- **Writing Second opinion's, Partner's and Helper's empty read as their order**: it would turn them off.
- **The home part applied beside the others, or at an unpressed start.**
- **Removing `gitMoved` at the git part's Apply**: an older build would take the ambient path.
- **The adapter classes' end only after Lyntai's moves** (§3).
- **`no-push` kept switchable beside git access.**
- **`SESSION_START_FROM` folded into the hand-over**: a new line is another job.
- **`daoris-driver` as the taught name** (the first proposal of §11), **a transitional alias that still runs the old forms**, and
  **renaming the program's file in the tree's wave**.

## 14. Read and settled (2026-11-03)

An independent Opus read found eight blocking points and several smaller ones; the steward checked three against the code. Each is
settled here:

| Finding | Settled in |
|---|---|
| 1. Staffing would switch Second opinion, Partner and Helper off | §2's end state and S3: they keep their caller's rule as a rule; written orders and *never unset* hold for the other five |
| 2. `SET_ACTOR` is the only editor of a named-file home | §2 S1, S2: `ONEWAY1c-i` ends the named file before `ONEWAY1a`; the wave's seeded-roles editor named |
| 3. M3 wrong about the code | §4 M3 rewritten: readers move to `Said`, the computed property goes, nothing converted; the stamp is the desktop store's alone (§1.3) |
| 4. The conversion door in parts | §1.1: per-part outcomes and retry in the receipt; `moved` against `failed` (an unreachable part); the home part alone, at its press's restart; Undo over `role_staffing` with its checks; Undo refused once `FloorNeeded` is set; `build` in the receipt; per-part *never touches*; `Stands` widened; `workflow-convert` kept; clears last across parts; `Pooled` cleared (§1.2); removers by store (§1.2, §6 X6) |
| 5. Git | §5 G2: `allow` written only where Write is unset; G1: `gitMoved` kept until the floor passes `ONEWAY1p`'s build |
| 6. Callers not named | §3 C5: Ask Daoris's `useGoOnBrief`; §8 P2: every importer of `Flow.tsx`, so `ONEWAY1aa` waits on CANVAS1e, f and SETUPUX1y |
| 7. *Now* rows that touch the wave | §10.1: each now row's files; `j`, `h-iii`, `n-ii`, `ab-ii` and the floor moved after the wave |
| 8. The gate | §1.3: it reads markers and entries only, lands green, and `verify` stays green between rows; MODELROLE1h's door goes with `ONEWAY1a` |
| *Fresh home* undefined | §2's end state; the rehearsal's homes made fresh at `ONEWAY1c-i` |
| M4's records and *waits an hour* | §4 M4 |
| P1 overstated | §8 P1, §12 |
| `AnswersAlone.cs:28` is `Solicited` | §4 M5 |
| `DriverContext.DoorOf` needs a source | §2 S3 |
| Row letters collide | every row id prefixed `ONEWAY1` |
| The parity oracle over `AdapterSet.Built()`, and D213's stand-ins kept as the old side | §3 step 4, by D213's note of 2026-11-03 |
| The owner's question | §11, the command tree |

A second read of the revision found seven of the eight settled and four more points, each settled in place: Undo reaches only the latest
receipt once settled (§1.1); `connect` is a typed operation of its own beside `register`, the two acts kept apart (§6 X3, §11); the tree
maps `git identity|use|access|asks|answer`, `session supervise`, and `settings handover|brainstorms`, so no shipped verb is dropped (§11);
an appointed role's terminal allowlist is rewritten with the words (`ONEWAY1ai-ii`); the home part's mover is the launcher project's, and
a home started without it is moved by the driver's start (`ONEWAY1w`); `ONEWAY1ab-i` takes no module route the wave changed (§10.1).
