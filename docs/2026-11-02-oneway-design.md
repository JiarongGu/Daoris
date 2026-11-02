# ONEWAY1: the design. Each job done one way, every old record converted or cleared on one press, every kept read with its end

Status: proposed 2026-11-02 under D228, from the census (`docs/2026-11-02-oneway-census.md`), read on main at `403e267e1` and on
`wave/workflowclean1` at `b022cc80c`. The reading was read-only and no tests were run. It widens a foundation (the conversion becomes
every area's, and a register holds every kept read), so under D191 it takes both makers' reads before the steward accepts it; the rows
marked *now* in §10 may start beside those reads, as WORKFLOWCLEAN1's first rows did. WORKFLOWCLEAN1's areas (the workflows, adoption
A1 to A3, page P1) are its design's (`docs/2026-11-02-workflow-cleanup-design.md`) and are cited here, never re-designed.

Paths as the census spells them: `Drv/` `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Mod/` `src/Daoris.Desktop/Daoris.Desktop.Modules/`,
`Svc/` `src/Daoris.Service/Daoris.Service.Core/`, `Http/` `…Service.Http/`, `Web/` `src/Daoris.Web/src/`, `Cli/` `src/Daoris.Cli/src/`.
*The wave* is `wave/workflowclean1`.

## 0. In short

- **One conversion door** (§1.1). WORKFLOWCLEAN1's conversion becomes the home's, made of parts, one per area's old shape: one
  *What needs you* row, one review, one Apply, one receipt, one Undo, and `daoris-driver convert` at the terminal.
- **Converted where in use, cleared where finished** (§1.2). Whatever an open line, run, quest, park or wait reads is converted.
  Finished history is cleared through the retention pass, said and sized, and is not undone. A costly converter is built only where the
  install's census finds something in use.
- **One register of kept reads** (§1.3). Every transitional read is one entry naming its code, its condition and its deleting row. This
  machine's page lists them. *For an older build* ends at the home's floor, the oldest build that can still write the home, and a gate
  refuses a marker that names no live row.
- **No silent rewrite** (§1.4). The three silent rewrites become parts of the conversion. The two pure reads that give old files their
  present meaning stay until their part ends.
- **Staffing** (§2). Staffing lives in the store alone and is set through the role door alone. *As before* becomes each role's written
  order. The copy in `driver.json` ends on a home past the floor. *Actor* leaves every window word.
- **Sessions** (§3). AGENTPROVIDER1m-i comes first, then D213's host rows. Each behaviour that differs between Claude Code's two doors
  is removed by a row after the move that makes it one place. The adapter classes end on Daoris's own transports, not after Lyntai's
  releases.
- **Messages** (§4). A park is answered through the say door alone, and `level` replaces `now`. The service's confirmations wait as the
  pipe's do. COMMS1's kept guards each get an end. These rows build inside COMMS1's wave 1.
- **Git** (§5). The ambient path goes, and moving off it is the git part of the conversion. Push is git access's setting, and every
  branch delete goes through the journal.
- **The service, the CLI, integration and the page** (§6 to §8). One brainstorm writer, one verb per job (the name is the owner's
  question), one place per home entry, twelve-character quest ids, every drawn flow on the canvas, and no branch for an older shell.
- **Waves** (§10): now on main; the conversion widened once the wave lands; the parts; messages with COMMS1; the measured parts; the
  sessions track; the deletions.

## 1. What every area shares

### 1.1 One conversion door (recommendation 1: accepted)

**Why.** Converting the person's old records is one job, and D228 counts a second way to do one job as a defect. A press per area
would mean nine doors, nine receipts, nine Undo rules and nine *What needs you* rows, which the person would have to tell apart. Every
area also needs the same guarantees WORKFLOWCLEAN1 §4.4 and §4.5 built:
- a plan that writes nothing;
- a digest the press names;
- a receipt written before the first effect, and a restart settled from it;
- Undo while nothing has moved.

Finally, the rollback refusal an applied conversion raises (that design's §1.5) is what ends every read kept for an older build (§1.3),
and one door raises it once. *Rejected:* a press per area. An act that is not a conversion keeps its own door, so a git identity is still
set up in Settings → Git.

**The shape.**
- **Parts.** The conversion is a list of parts in `Drv/Conversion/`, moved from `Drv/Workflows/Legacy/`. The workflow part is
  WORKFLOWCLEAN1's code, moved and not changed. Each part answers:
  - a pure census: its items, and `Ended`;
  - its operations, each said once, and its row choices;
  - its effects, each idempotent;
  - its *before* for Undo, or *not undone* where it clears;
  - the register entries (§1.3) it ends.

  The parts, in apply order:
  - `home` (§6, X5);
  - `settings` (`timeoutMinutes`, `no-push`'s switch: §5, §6);
  - `accounts` (`harnesses.json`: §1.4, §6);
  - `staffing` (§2);
  - `git` (§5);
  - `workflows` (built);
  - `quests` (§6);
  - `notes` (§8).

  The home moves first, so every later write lands in the new place. The clears come last, since a clear is never undone.
- **One plan, one Apply.**
  - `CONVERT_PLAN` composes every part that has something to do, and its digest covers every part's inputs, operations and choices.
  - `CONVERT_APPLY {digest, choices}` is the person's press, confirmed in the window. The apply plans again and refuses `moved` as
    WORKFLOWCLEAN1's does.
  - A part that cannot apply (a file held, the service not answering) is `failed` in the receipt with its sentence. The other parts
    stand, and the next plan offers the failed one again.
- **The receipt** gains `parts`, each holding its operations and its *before*. A receipt written before parts existed reads as the
  workflow part, since a stored shape is never changed in place (a register entry that row ag ends).
- **Undo** reverts the home's latest receipt whole, under WORKFLOWCLEAN1 §4.5's conditions widened to every part: each key and file the
  receipt wrote must still read as written, and the build must be before that part's deletion (row af). Clears are listed as *not
  undone*.
- **The terminal.** `daoris-driver convert [--plan | --apply <digest> | --undo <digest>]` runs through the running Daoris (RPC1e).
  `workflow convert` goes in the same row. If the parent folds this rename into the wave before the wave reaches main, no shipped verb
  has to retire.
- **One row.** *What needs you*'s `workflow-convert` becomes `convert`: one row per home while any part holds something, reading
  *`n` old records wait for your conversion · Review…*, with the count by part in its detail. Each surface that shows a part's old
  shape opens the same review at that part: a place's head (as the wave built), a role's card, a held remote, an agent's account list.
- **The review** keeps WORKFLOWCLEAN1 §4.3's order within each part:
  1. what changes;
  2. the row choices;
  3. what lowers the person's part;
  4. what stays;
  5. what it never touches;
  6. a section **removed for good** (each clear with its count, dates and size);
  7. the terminal fold;
  8. one *Apply*.

  Ask Daoris opens the review and never proposes the apply.
- **Never touched.** WORKFLOWCLEAN1 §4.6 holds for every part. No part writes into a repository's own files, signs in, or moves a
  credential.

### 1.2 Converted where in use, cleared where finished

D228's note leaves this to the design. The rule for every part:

- **In use**: an open line, run, quest, park or wait reads it, or a live surface draws it (a head, a waits row, a role's card, a
  setting). In use is converted to the new shape with its meaning kept. A setting is always in use. A retired key that has no meaning
  any more is removed by its part and kept in the receipt: `timeoutMinutes`, `prefer`, `parallel`, the `Pooled` receipts, and
  `gitMoved` once §5 lands.
- **Finished history**: nothing open reads it. It is cleared by the plan through the one remover of old records, the retention pass
  (D195, KEEPSIZE1), under one more reason, `older-shape`. The review counts, dates and sizes it, and it is not undone.
- **Measured first**: where a converter costs more than its use and the census may find nothing in use (`quests`, `notes`), the part's
  census is read on the owner's install first, by the parent and read-only. Its in-use converter is built only if something is found,
  as WORKFLOWCLEAN1 built its row 1j.
- **After a part's deletion row**, an old-shape record nobody converted is drawn by the one rule for any record this build cannot read
  (*a record this build does not read*), never by a reader of its own. A home whose census has not ended holds that part's new work
  with the conversion as its door, as WORKFLOWCLEAN1 §4.7 holds an old-shape place.

### 1.3 The register, the floor and the gate

- **The register.** Every transitional read kept is one entry in `Drv/Transitional/Register.cs`, with twins only where the reader lives
  outside the driver. An entry names:
  - its id;
  - the code it keeps (file:line);
  - what reaches it;
  - the condition that ends it, a pure check over the home;
  - the row that deletes it.

  Each kept site carries the marker `// transitional: <id>`.
- **Shown.** This machine's page lists the entries with each condition's state. WORKFLOWCLEAN1's *Workflows: nothing old here* becomes
  the first line of that list. An ended entry reads *nothing old here*, and Ask Daoris's room reads the same list.
- **The floor**: the oldest build that can still write this home. It is read from two places:
  - the install's rollback refusal: while an applied conversion stands, no build below it is staged (WORKFLOWCLEAN1 §1.5);
  - the store's writer stamp: an older build reads the store and writes nothing (D226's first note).

  Every entry kept *for an older build* names the build whose writes it waits out, and ends when the floor passes that build. This is
  FOUNDATION1 row v's condition made visible, one entry per pairing of its §19.12 matrix. A write that slips past the floor (a second
  install on the home) is caught by the store's own check, as a found row for staffing, or by the part's census, and is said.
- **The gate** (row ad): `tools/transitional-markers.mjs`, run by `verify`. Every marker names an entry, and every entry names a row
  that is open in `TASKS.md`. A marker or entry that names neither fails. Its first finding is the room's owed actor door, which names
  MODELROLE1h, a row that exists nowhere (`Drv/Knowledge/Room/HelpRoomOwed.cs:93-109`). Row a removes it.

### 1.4 The three silent rewrites (recommendation 2)

| Rewrite | Today | Becomes | Kept as a pure read, until |
|---|---|---|---|
| `harnesses.json`'s old names (`early`, `near`), retired fields (`prefer`, `parallel`) and workspace lists, rewritten at the next write (`Drv/Agents/HarnessSettings.cs:753`, `:1146-1156`) | any edit rewrites every entry in today's names | the `accounts` part, on the press; an edit writes only the entry it edits | the old names read as `switch`/`curve`, the retired fields as nothing, and the workspace lists into the set, until the part's census ends (row af) |
| The account-pool fold (`Drv/Agents/AccountSet.cs:16-39`) | `HarnessSettings.Change` folds before every edit and appends `Pooled` receipts | the same part, whose plan is POOL1d's dry run; `Change` folds nothing; no `Pooled` receipt is written, and the kept ones are cleared | `AccountSet.Of`, the fold every reader takes in memory: it writes nothing and is D197's meaning, so it stays until the file holds that meaning (row af) |
| The git move's mark at the first start (`Drv/Workplace/GitMove.cs:15-31`, `Drv/Loop/DriverWatch.cs:404-405`) | a start writes `gitMoved`, and from then on an unserved remote is held | the `git` part (§5); the mark and the ambient path removed | nothing kept |

None of the three stays a write. The two reads stay because they change nothing on disk and give the person's file the meaning D197
already decided. Each has its end.

## 2. Staffing (S1 to S5, K1, K5; recommendation 3)

**The end state.**
- Staffing is kept in the desktop store alone: `role_staffing` with `role_policy` (D212 w-s, D226). `driver.json`'s `actors`,
  `adapter`, `intakeAdapter` and `helperAdapter` are neither written nor read.
- Staffing is set through the role door alone:
  - `SET_ROLE` and `SET_ROLES` in the window;
  - `daoris-driver role …`, with `role policy`, at the terminal;
  - `role_propose` in Ask Daoris.
- No role is ever unset, and *as before* is gone:
  - on a converted home, every role holds the order it ran, written out;
  - a fresh home's first start writes each seeded role's seed into the store;
  - the role door's `default` writes the seed explicitly, and that is what *Clear order* becomes.

  The only default left is D226's `order` rule (*First that can run*).
- **FOUNDATION1 row v's condition, shown.** The copy's register entry reads the floor (§1.3). The copy stops on a home past the floor,
  and row v, with row af, deletes the code.

**S1. Setting a role's agents** (row a)
- *One way:* the role door.
- *Retires:*
  - `SET_ACTOR` (`Mod/Bridge/Routes/DriverRoutes.cs:374-386`) and both of its page callers, which move onto `SET_ROLE`: Roles' *Clear
    order* (`Web/agents/roles/Roles.tsx:173-183`, becoming `default`) and, on the wave, the seeded role's staffing editor (`RoleEditor.tsx`'s
    `staffing` mode, becoming `set`);
  - `driver adapter|intake|helper|actor` (`Drv/Settings/DriverChoicesCommand.cs:105-131`, `:166-216`);
  - Ask Daoris's `setting_propose` `adapter`, `intake` and `helper` (`Drv/Help/Proposals/HelpSettingProposals.cs:34-38`, `:225-308`)
    and its service twin;
  - the room's `driver actor` lines and the owed actor door (`HelpRoomOwed.cs:93-109`);
  - their usage, catalogue keys and Help coverage rows.
- *Records and transitional reads:* none.
- *Seen:* every role is edited by the one editor. *Clear order* reads *Use the default* and shows the seed it writes. At the terminal,
  `driver actor …` is an unknown verb whose error names `role`.

**S2. Where staffing is kept** (rows b and c)
- *One way:* the store.
- *Retires:*
  - the copy onto `driver.json` (`Drv/Roles/RoleStaffing.cs:569-600`, `Drv/Roles/StaffingFile.cs`, `StaffingProjection`);
  - the found rows and their door (`STAFFING_FOUND_ANSWER`, `driver actor found|export|import`), which only the copy feeds;
  - the named-file path (K5): the file `DAORIS_DRIVER_CONFIG` names is read and written with no store (`RoleStaffing.cs:105`), and the
    variable's readers go too (`Drv/Settings/DriverConfig.cs`, `Drv/Settings/RoleStaffingPort.cs`, `DriverChoicesCommand.cs`,
    `Drv/Instance/VerbLeases.cs`, and the `Program.cs` of the host and of the app);
  - in the tools that set the variable (`tools/family-rehearsal.mjs`, 18 uses; `tools/desktop.mjs`, `tools/deployment-rehearsal.mjs`,
    `tools/proof-kit.mjs`), each run is given a home of its own instead, so every run has a store.
- *Records (the `staffing` part, converted):*
  - The four keys are removed from `driver.json` and kept in the receipt for Undo.
  - Found rows still open are listed in the review, and *Take it* stays offered until Apply. Apply clears them: each was a change an
    older build or a hand made to the copy.
- *Transitional:* the copy's write, on a home no conversion has reached. It ends when the floor passes the w-s build on that home,
  which the first Apply does; row c makes the writer skip such a home, and row af deletes it. The found check ends with it.
- *Seen:* This machine's list reads *Roles' staffing, copied for older builds: ends when no older build can open this home*, with its
  state.

**S3. *As before*** (row b)
- *One way:* each role's written order.
- *Retires:*
  - `ActorKey.Before` and `FirstAgent`'s fallback to the old keys (`Drv/Settings/ActorKey.cs:28-50`);
  - the readers that skip a role for the old key (`Drv/Roles/StartPlan.cs:463`, `:467`; `Mod/Bridge/DriverContext.cs:184-194`;
    `HelpSettingProposals.cs:775`; `Drv/Loop/Driver.cs:755`);
  - the window's and the room's *as before* words.
- *Records (converted):*
  - Each role that reads *as before* is written as `ActorChoices.Effective` reads it at the plan: an agent at its account default, from
    `adapter` for Build, Chat and Brainstorm, from `intakeAdapter` for Intake and from `helperAdapter` for Ask Daoris.
  - An Intake or Ask Daoris whose key is absent is written off.
  - A stored entry that does not read (`ActorErrors`) is kept and said, with its sentence and the role door, never forced.
  - A role written by the plan has no policy, since D226 refuses a policy on a role with no choices of its own, so nothing is re-keyed.
- *Transitional:* `ActorKey.Before` on an unconverted home. It ends when the staffing part's census reads `Ended`, and row af deletes
  it. A build at row af that opens an unconverted home holds each such role's new starts, with the conversion as their door.
- *Seen:* the review's staffing section reads *Build: claude-code (as before) → claude-code, written*, and says nothing lowers and
  nothing spends differently. After it, no role says *as before*.

**S4. The words** (row d)
- *One way:* *role* in every window word, verb, route and catalogue key.
- *Kept:* *actor* stays only in code names and stored shapes, since a stored shape is never renamed in place: the event codes
  `actor.chose` and `actor.moved`, the record field `chosen`, and `ActorChoice`.
- *Retires:*
  - the verbs, with S1;
  - the route `ACTORS`, folded into `ROLES` (its `arranged` answer becomes a field of each role's row, which the page's `nextChoices`
    reads);
  - the catalogue keys under `actor`, renamed under `role` in every shipped catalogue (D222);
  - the glossary's *actor* row, its words moved to the *role* rows;
  - the room's sections and the terminal's usage.
- *Seen:* no *actor* in the window or the room in either language. `names:check --strict` holds it.

**S5. The old Roles mode**: WORKFLOWCLEAN1's (SETUPUX1y's removal, its §6.2, and the stored `roles` mode's end in its §4.7). Nothing is
added here.

## 3. Sessions (C1 to C6; recommendation 5)

**The order.**
1. **AGENTPROVIDER1m-i, parts 2 to 4** (D213's note of 2026-10-30):
   - part 2, the driven executor and the continuations;
   - part 3, the helpers', intakes', opinions', partners' and brainstorms' starts;
   - part 4, the conversation's runner.

   Nothing changes for the person. A half-moved seam is itself a second way, so these go first, on main now.
2. **D213's host rows in its §11 order**: d, e's host half, f, g, h, l's sink, m-ii, n, o, p, q, r, the corrections s-i to s-ix (each
   after its move), and u. AGENTLYNTAI1's design §9.1 item 4 leaves these rows to Daoris. Each is its own sitting, on files no open
   branch holds. They are not re-designed here.
3. **Row g's parity rows**, each right after the host row that makes its behaviour one place (the table below).
4. **t-i, the adapter classes' end on Daoris's own transports.** Every built-in door is registered in `Providers.Built()`, and
   `ISessionAdapter`, `HarnessToolchain`, `SessionWire`, `AdapterSet` and `LegacyDoor` (`Drv/Agents/Registry/LegacyDoor.cs:5-19`) go
   once the host rows leave none of them a reader. D213 row t's proof is run on today's transports.

   This is recommended against AGENTLYNTAI1 §9.1 item 12, which puts t after every door has moved onto Lyntai's layer. That order ties
   the end of a second way in this repository to another repository's release labels, an end this repository cannot make hold. The
   moves themselves (1d, 1e, 1f) only replace a provider's transport behind one contract, which is no second way. The layer's own
   seam's end stays AGENTLYNTAI1's last row. The change amends D213's and AGENTLYNTAI1's order, and is taken with the steward's
   acceptance of this design.
5. **The rows gated on Lyntai** (AGENTLYNTAI1 1a to 1f, and j) stay on Lyntai's releases, outside ONEWAY1's waves.

**C1. One agent, two doors** (row g). Both doors stay, as two providers of one contract (D228's note). Each behaviour that differs
between them is a defect, removed by its row:

| What differs (census C1) | Removed by (the parked row brought back) | After | Needs the owner |
|---|---|---|---|
| A limit cools no account on the native door | TOOL4n's native limit read into the one failure channel and cooled as on the protocol door, D210's row 1c on row l's channel | l | TOOL4n's recording spends an account |
| The transient retry inside a run is on the protocol door only (`Drv/Loop/Driver.cs:4530-4537`) | AGENTERRORS1 1c and 1e: one error policy, the same retry on each door through `AgentRun` | m-ii, which moves the native retry once | no |
| *Try again* is briefed on the native door and resumed on the protocol door (D210) | LIMITRESUME1f with AGENTERRORS1e: one *Try again* on both doors | m-ii | no |
| A word sent during a turn is taken between turns on the protocol door and held to the turn's end on the native door | MSG1h and MSG1i through m-ii's `DoorInput`; STEER3 measures other steering; BGWAIT2b says words into a woken turn | m-ii | BGWAIT2b's probe and STEER3's measure need the go-ahead |
| Skills and settings are handed on the native door only | D213 row o (`ISkillLayout`, `IRuleLanguage`) for both doors; HELPWIRED1b says what each door loaded | o | no |
| Parity not yet measured | SESSCTX1a with HELPMODE1a3 (resume and a fresh part on each door; full access on the installed pins); AGENTLYNTAI1s-ii (the three headless tools); FABRIC-C2b (the two-harness canary) | their rows | each spends: the owner's yes |

- *Retires:* each difference's branch, and the catalogue words that said the doors differ.
- *Records:* none, since each door's records are already read by provider id (D213 §5).
- *Transitional:* none.
- *Seen:* the window says the same on both doors.

**C2. Adapter classes beside providers**: row t-i. `LegacyDoor` is a register entry until then; it ends when D213 row a's census finds
no reader of `ISessionAdapter`.

**C3. Seven executors**: row h's m-i. The half-moved seam is a register entry that ends at part 4, which deletes it.

**C4. A conversation's course beside a driven line's**: STAFFPOLICY1e3, as filed (row i), with SESSWATCH1a and 1b. Its interim refusal
of a conversation's moves is a register entry, which e3 ends.

**C5. Four presses to go on** (row j)
- *One way:* hand over (`SESSION_HAND_OVER`: D212's move, D189's brief). A line goes on in a new part. Where the press names no agent,
  the role's walk picks the successor (`RoleOrders.Arranged`); where it names one, the named agent, model and effort take it (D226's
  row d note). Three cases, one press each, one route:
  - *Go on with Daoris's brief*: the words could not be taken (a cooling account, or an ended or stood-down part). The walk picks, and
    waits for an account that can run, as a start waits.
  - *Hand over to…*: the person picks another agent.
  - *Go on fresh*: a fresh context while the part can still take words. This is SESSCTX1e, re-cut to the same agent and account, after
    SESSCTX1a's measure.
- *Kept:* `SESSION_START_FROM` (*Start a conversation with these words*). A new conversation is a new line, which is another job.
- *Retires:*
  - `SESSION_GO_ON_BRIEF` and `SESSION_GO_ON_NEW`, with their driver acts, folded into the hand-over;
  - their hooks (`useGoOnBrief`, `useGoOnNew`, `Web/work/WorkFrame.tsx:923-954`), terminal twins, refusal keys and Help coverage rows.
- *Records:* a go-on-new choice kept and not yet acted on is taken by the hand-over at the next look. That read is a register entry: it
  ends when no such choice is kept, and row af deletes it.
- *Seen:* the card for words not taken shows its case's one press and *Hand over to…*. The context meter on the head offers *Go on
  fresh*. Each press says that the brief goes with the words, and who takes them.

**C6. The course's paths for an older build** (the unkeyed open, "an older host moved nothing", `held-words.json`, the import backups):
FOUNDATION1 row v's. Each is a register entry, one per §19.12 pairing, ended by the floor. Row v deletes them once each reads ended on
the owner's install. Nothing is converted, since each reads a shape only an older build writes.

## 4. Messages (M1 to M5; recommendation 4)

Rows k, l, m and o are built inside COMMS1's wave 1 (D225; `docs/2026-11-02-comms-design.md` §10) as that design's retirement rows:
COMMS1 was accepted before D228 and carries none. Row k needs COMMS1e (a press names its question), row m needs COMMS1a and e, and row o
needs COMMS1b and g. The chunks keep their order: ONEWAY1 closes when that wave lands. Row n needs none of them and is built now.

**M1. Answering a park** (row k)
- *One way:* the say door (`SESSION_INPUT`, `sessions say`, the service's say door) carrying the question's `id` (COMMS1 §5.2).
- *Retires:*
  - `POST /api/sessions/{id}/answer` (`Http/Program.cs:2207-2213`) and its handler;
  - `daoris-driver answer`;
  - the park card's call through the route;
  - its usage, its room line, its catalogue keys and its Help coverage row.
- *Records and transitional reads:* none (what the route wrote is M3's).
- *Seen:* the park card looks the same. Its press goes through the say door, and a stale press is refused beside its control.

**M2. The say's level** (row l)
- *One way:* `level` (`ordinary`, `urgent`, or `now`, which is the person's alone, D188).
- *Retires:*
  - the `now` field every writer still writes beside `level`;
  - the request file written for an older build. The headless `drive` loop's own request file stays: it runs with no pipe and no
    service.
- *Records:* a word waiting with `now: true` and no `level` is in use, and is read as `level: now`.
- *Transitional:* that read. It ends when the floor passes the last build that wrote `now` alone and no waiting word carries it, and row
  af deletes it.

**M3. `Session.Answer`** (row m)
- *One way:* the keyed `Said` words, with the question id.
- *Retires:* the joined `Session.Answer` as a source for the course, the planner, attention and Ask Daoris, then its writer.
- *Records:*
  - An open session whose answer already sits in `said` (the person's and the supervisor's answers write there,
    `Svc/Sessions/Sessions.cs:391-395`) needs nothing.
  - An open session whose answer has no `said` word (an older build's) is converted: its text is kept as a `said` word keyed
    `answer:<session>`.
  - Ended sessions' values are finished history. Row af clears them by dropping the column, a store step that raises the writer stamp.
- *Transitional:* the column, read by nothing after row m. It ends when no open session's answer lacks its word.

**M4. One confirmation queue** (row n, with PERSONDOOR1j)
- *One way:* CONFIRMWAIT1's (D156's notes; `docs/2026-10-24-confirm-wait-design.md`). A request waits as a row in *Notifications* until
  the person confirms or declines it or its asker withdraws it. The press is made at the Confirm, where its grounds are judged and a
  stale one is refused with its reason, and the asker is told.
- *Retires:*
  - the service's two-minute expiry (`Http/PersonConfirmations.cs:16-23`, `:127`). The host already holds the request as asked (the
    path, the pattern and the body to the byte, `:29-37`), so it makes the request at the Confirm instead of handing out a grant to be
    used within two minutes;
  - `Expired` as a state of a wait. A request whose grounds moved reads *refused: stale*;
  - the separate drawing of the host's cards, which become the same request row.
- *PERSONDOOR1j, folded:* without an expiry, agents' asks could hold all five of the host's slots. The person's terminal keeps a
  reserved slot, and an asker past its bound is refused with its waiting requests named.
- *Records:* a confirmation waiting at the update keeps waiting under the new rule.
- *Seen:* a terminal's request through the service is the same row as the pipe's, waits an hour, and is made at the Confirm.

**M5. Ends for COMMS1's and D226's kept paths** (row o). Each is a register entry:

| Kept | Why | Ends when | Deleted by |
|---|---|---|---|
| The helper state's and the partner receipt's `Attempted` and `Handed`, still written (COMMS1 §13 point 3) | a rolled-back driver hands nothing twice | the floor passes COMMS1b's build | af |
| The driver's own hand against a host with no `answers: 1` (§4.2) | an older host | the floor passes COMMS1b's build, since the host and the driver ship together | af |
| `HandedBefore`, the helper's line walk and the partner's receipt file, still read | old files | no open helper or partner ask has an old file, and the floor holds | af |
| The legacy answers-alone reader seeding admissions (`Drv/Course/Domain/AnswersAlone.cs:28`; §13 point 8) | a line with no admission yet, or after a rollback | every line with a count has its admission, and the floor passes COMMS1g's build | af |
| `opinion.Handed` guarding an unkeyed opinion word; a question word with no `question` id; old `answered` receipts | older records | none waiting (census); finished ones cleared by the `notes` part | af |
| D226: a seeded role's staffing copied onto `driver.json` | §2, S2 | §2's condition | af |
| D226: the doors refusing a conversation's moves for now | STAFFPOLICY1e3 not yet built | e3 built | row i |

## 5. Git (G1 to G3, K3)

**G1. Credentials** (row p)
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
  - `gitMoved` is removed as finished.
- *Transitional:* none. From row p an unserved remote is held, and the conversion row holds work wherever a landing or a session waits on
  one, as D198's release note says.
- *Seen:* the conversion row counts the hosts, and a held remote's `git.identity.missing` row opens the review's git section.

**G2. May a session push** (row q)
- *One way:* git access's Write (`machineGitAccess.write`, the same per repository and workspace, `SET_GIT_ACCESS`, `git access`).
  `no-push` stays the harness rule composed from git access (D198 §6.8) and can no longer be switched.
- *Retires:*
  - the reading of `no-push`'s switch as an allow (`Drv/Workplace/GitAccessRules.cs:22-23`, `:37-47`, `:75-90`; `GitDoors.cs:511-513`'s
    `|| PushesHere`);
  - the switch in Settings → Permissions, and its words.

  The publishing and release sentence that `no-push`'s switch also turned off becomes its own default rule, `no-publish`, switched
  alone, so publishing has one setting too.
- *Records (the `settings` part, converted):* a home whose `permissions.json` lists `no-push` in `defaultsOff` gets
  `machineGitAccess.write: allow` and `no-publish` in `defaultsOff`, with `no-push` taken out. The review says *keeps what you chose*.
- *Transitional:* `PushesHere` on an unconverted home. It ends when the settings part reads `Ended`, and row af deletes it.
- *Seen:* Settings → Git's machine card shows Write *allow* where the switch was off. Permissions offers `no-publish`'s switch and not
  `no-push`'s.

**G3. Deleting a branch** (row r; after the wave, whose 1c changed the look's clean-up)
- *One way:* the journal's delete (`GIT_REF_DELETE`; GITAPP2d, D204): one performer, one record, one Undo.
- *Retires:* the deletes that `DISCARD_SESSION_BRANCH`, `WITHDRAW_LANDING_BRANCH`, `SWEEP`, the look's clean-up (WORKFLOWCLEAN1
  §2.5) and `housekeeping_discard_branch` each make their own way. Each keeps its judgement of whether to delete, and deletes through the
  journal's act. Their own undo records give way to the journal's (`Drv/Workplace/RecoveryRetention.cs`, `RecoveryDropping.cs`).
- *Records:* recovery refs and records of past deletes stay in use as undo until their retention ends. They are not converted.
- *Transitional:* their reader and their undo. They end when the last recovery record from before row r passes its retention (a dated
  condition, shown), and row af deletes them.
- *Seen:* every deleted branch is in the git journal with *Undo*, whatever deleted it.

## 6. The service and the CLI (X1 to X6, K2, K4)

**X1 and K4. The retired timeout** (row s)
- *One way:* none. D200 point 9 retired the timeout. The quiet setting, `quietMinutes`, is its own job: SESSWATCH1c builds its doors
  after SESSWATCH1a, and is re-cut so that no timeout door names it. `timeoutMinutes` is not converted into it, since a wall clock and
  quiet are different settings.
- *Retires:* `driver timeout`, Ask Daoris's `timeout` door in both twins, `settings_timeout`, the reading of `timeoutMinutes` only to
  say it is retired, and their words.
- *Records (the `settings` part, cleared):* `timeoutMinutes` is removed and kept in the receipt.
- *Transitional:* none.

**X2. A brainstorm from a terminal** (row t, with BRAINSTORM1b2 and AGENTROOM1c re-cut)
- *One way:* the service's `BrainstormDesk` is the one writer of a brainstorm's request (`brainstorms/<id>/`, twinned on
  `fixtures/brainstorm-files.json`). Sessions' tools and the terminal both reach it.
- The terminal's one verb (its name is question 1, §11) goes through the desk, with `stop <id>` and `--id` (BRAINSTORM1b2). A meeting is
  a kind of the same verb (AGENTROOM1c, re-cut: no `daoris meeting`).
- *Retires:* the other path's own writer (`daoris-driver brainstorm`'s file writer, or the CLI's wrapper, by question 1's answer), its
  usage and its twin.
- *Records:* none.

**X3. Remotes and browser favorites** (row u)
- *One way:* the driver library's typed operations, in D194's shape, called by the window's modules and by the terminal.
- *Retires:* `Cli/service/remotes.ts`, `Cli/service/remotemap.ts` and `Cli/browser/browser.ts`, with their tests and twins. The CLI's
  `remote` and `browser` families hand their words to the driver program as `agent|driver|plugin|tool` do, or retire with question 1's
  yes.
- *Records:* none: the files are the same.

**X4 and K2. One word per flag and verb** (row v)
- `--profile` gives way to `--account`, and `--early` and `--near` to `--switch` and `--curve`, with their usage and the parser's old
  names.
- The verb's name is question 1.
- `harnesses.json`'s old names, retired fields, workspace lists and fold: the `accounts` part (§1.4). POOL1d reads its plan on the
  owner's install rule by rule before the owner presses.

**X5. Where a home's file lives** (row w; HOMELAYOUT5 measures first)
- *One way:* D172's places.
- *Retires:*
  - `DaorisHome.Entry`'s old place (`Drv/Platform/DaorisHome.cs:105-128`), `Places`' second place (`:152-164`), `FolderOf`'s old home
    (`:166-183`) and their twins;
  - the trees under two roots;
  - the old `driver.lock`;
  - D63's move-in;
  - the flat service host (LEGACYHOST1).
- *Records (the `home` part, converted):*
  - Each entry at its old place is moved to its new one. The move is made at the next start, from the receipt, before anything opens
    the home.
  - An entry at both places (`HomeEntryTwiceException`) is listed with both places' dates and sizes, and the person chooses which
    stays.
  - Trees, rooms, kept files and account homes are moved only where HOMELAYOUT5 measured the move safe, and `bin/` never.
- *Transitional:* the reads of the old places. They end when the home part reads `Ended` and the floor passes D172's build, and row af
  deletes them.
- *Seen:* the review's home section, which says the files move at the next start.

**X6. Six-character quest ids** (row x; measured first)
- *One way:* twelve characters.
- *Retires:* `LegacyIdLength` and its two uses, the same ask found under either width at a publish and the forgotten check
  (`Svc/Quests/Quests.cs:725`, `:1322-1326`, `:1366`).
- *Records (the `quests` part):*
  - Closed six-character quests and their forgotten entries are finished history, cleared through the service's retention on the plan.
    The review says that the same words published later make a new quest.
  - Open ones are expected to be none. If the census finds any, the part holds until each closes. A converter that renames a quest
    through its peer is not built unless the owner's install shows one that will not close.
- *Transitional:* none after row x.

## 7. This repository's integration (I1 to I3; row y)

- **I1 and I2.** INTEGRATE1 row k, the owner's sitting, is widened to name every twin it retires:
  - `tools/merge-branch.mjs` and `tools/merge-queue.mjs`;
  - merge-branch's backlog merge, beside `Drv/Workplace/BacklogMerge.cs`, which stays;
  - `tools/merge-rows.mjs`'s git driver beside the declared rows merge, becoming one implementation that both git and the fabric call.

  FABRIC-PIPELINE1e, f, g, h and j stay under it as filed. The tools are a register entry that ends at row k.
- **I3.** WORKFLOWCLEAN1's: the merge rule's press ends with the landing rules, at its row 1z.

## 8. The page (P1 to P4)

**P1** (row z). WORKFLOWCLEAN1's row 1h already removed *Set up for agents* on the wave. SETUPUX1y's list (its §6.2) gains the four
files that still name `SetUpForAgents`: `Web/agents/setup/placeFixtures.ts`, `Web/help/PlaceCard.stories.tsx`,
`Web/help/ProposalCard.test.tsx` and `Web/projects/ProjectsView.test.tsx`. A test that asserts its absence stays, and a fixture or story
that draws it goes. The stored `roles` mode is WORKFLOWCLEAN1 §4.7's.

**P2. The life-flows on the canvas** (row aa)
- *One way:* CANVAS1's `PositionedCanvas` (D224, built at CANVAS1d), in display mode, for every graph Daoris draws.
- *Retires:* `Web/design/Flow.tsx` and its drawing of the landing, quest, parts and session flows, whose pieces move onto the canvas's
  nodes and edges beside CANVAS1e and f's maps. `WorkflowStartNode` goes with 1y.
- *Seen:* the same flows, with fit and focus. For the page builders: stories at 80, 39 and 25 rem, in en and zh.

**P3. Branches for an older shell** (row ab)
- *One way:* the page and the shell are one build (D139).
- *Retires:*
  - every field marked *absent on an older shell*, made required, with its fallback (`Web/data/signals.ts:21-80`, `Web/agents/tools.ts:29-168`,
    `Web/agents/SignInSteps.tsx:40`, `:254` and its story);
  - the pulls kept for an older shell (`Web/data/queries.ts:270`, `:368`, `:606`). The plain browser's path stays only as the harness of
    the stories and e2e, and is named so;
  - the modules' routes kept for an older page.
- *Kept:* a fallback for a record that travels between machines, such as a quest or a channel message, which another machine's build
  writes (D178). A fallback for the local service host, which a second install may run, is under the floor.
- *Records and transitional reads:* none.

**P4. Note shapes from before codes and parts** (row ac; measured first)
- *One way:* a coded note (D142, LANG1a and 1b).
- *Retires:* the readers of a note from before parts, and of one with no code from a shell before codes (`Web/work/noteLines.ts:783`,
  `:943`; `Web/work/Note.tsx:22`; `Web/work/attention/attention.ts:1003`), their stories (`Web/work/Note.stories.tsx:84-93`,
  `Web/work/attention/attention.stories.tsx:120`, `:747`, `Web/work/head/SessionHead.stories.tsx:451`) and their driver twins.
- *Not old shapes:* a code this page does not know (FABRIC-UNKNOWN1) and a current driver sentence with no code. Both stay.
- *Records (the `notes` part):* finished records are cleared through retention. In-use ones are expected to be none. If the census finds
  any, row ac rewrites each as the coded note `recorded`, carrying its English as a value.
- *Transitional:* none after row ac.

## 9. Across (row ad and WIRING1d)

- **Row ad** is §1.3: the register, the floor, the gate and This machine's list.
- **WIRING1d** puts every setting in one list (`SYSTEM_SETTINGS` and the page's catalogue). Its census holds the route, the catalogue and
  every `SettingRow` together, so a setting with two keys fails it. It is built after wave 2, so it holds the final keys.
- **Outside the 37 jobs**, the triage brought DOCSHOME1, LAYOUT5, LAYOUT6 and RPC1g back as new areas. Each keeps its own row and
  design, and none is settled here.

## 10. Waves and rows

### 10.1 The order

1. **Wave 0, on main now, beside the wave.** These rows touch none of the code the wave changed. One shared file meets it in a few
   lines: `Drv/Loop/Driver.cs`, where the wave adds nine.
2. **Wave 1**, once the wave reaches main: the conversion widened, which is the foundation every part sits on.
3. **Wave 2**: the parts that need no measuring.
4. **Wave 3**, inside COMMS1's wave 1: the messages rows.
5. **Wave 4**: the parts measured on the install first, and the integration sitting.
6. **The sessions track**, serial, from now: AGENTPROVIDER1's and D213's rows, each its own sitting.
7. **Wave 5**: the deletions, part by part, once the install's census of that part has ended.

Each row is sized for one builder's sitting.

### 10.2 The rows

| Row (lanes; after) | Contract | Proof | When |
|---|---|---|---|
| **Wave 0** | | | |
| **ad-i** the register, the floor and the gate (driver, tools) | §1.3 | the gate fails on a planted marker that names no entry and on an entry whose row is closed; the floor read from fixture homes with and without a receipt and a stamp | now |
| **n** one confirmation queue, with PERSONDOOR1j (service, modules, web) | §4 M4 | a host request left an hour then confirmed makes its press and tells its asker; a declined one does nothing; a stale one is refused with its reason; six agents' asks leave the person's slot | now |
| **t** one brainstorm verb (service, driver or cli) | §6 X2 | `BrainstormDeskTests` and the verb's tests (start, stop, one id asked twice); the twin fixture; the other writer gone | now; the name waits on question 1 |
| **u** remotes and favorites (driver, cli) | §6 X3 | the typed operations' tests; the window's edit and the terminal's equal on the fixture files; the TS editors gone | now |
| **aa** the life-flows on the canvas (web) | §8 P2 | each flow's tests on the canvas; stories at 80, 39 and 25 rem, en and zh; `Flow.tsx` gone | now |
| **ab** no branch for an older shell (web, modules) | §8 P3 | the typecheck with the fields required; page tests; the routes' census | now, leaving `Web/bridge/shell.ts` to after the wave |
| **j** one press per go-on case (driver, modules, web) | §3 C5 | each case through `SESSION_HAND_OVER` (the walk's choice, a named agent, fresh); the two routes gone; `HelpCoverageTests`' rows; stories | now; *Go on fresh* after SESSCTX1a |
| **h-i, h-ii, h-iii** m-i parts 2 to 4 (driver) | D213's m-i note; the AGENTLYNTAI1 design §9.2 | that design's §12 line; every executor's suite whole | now |
| **Wave 1** | | | |
| **ae** one conversion door (driver, modules, web; the wave) | §1.1, §1.2 | the workflow part's tests pass unchanged through parts; a planted second part in each outcome; a receipt without `parts` read; Undo of a two-part receipt; a restart mid-apply; `convert`; the `convert` row; the review's sections at 1546 and 680 px, en and zh | after the wave |
| **ad-ii** This machine's list (modules, web; ae, ad-i) | §1.3 | the list from a fixture register, one entry ended and one not, en and zh | after the wave |
| **Wave 2** | | | |
| **a** one staffing door (driver, modules, web, service; ae) | §2 S1 | each retired door gone; both page editors on `SET_ROLE`; `HelpCoverageTests`; the room's goldens | after the wave |
| **b** the staffing part (driver; a) | §2 S2's records, S3 | a fixture home with Build *as before*, Intake off, a stored order and an entry that does not read: plan, Apply, a start running the same choice, Undo; a fresh home seeded with written orders | after the wave |
| **c** the copy's end and `DAORIS_DRIVER_CONFIG` (driver, tools; b) | §2 S2, K5 | a converted home's write leaves `driver.json` alone; an unconverted home is copied as today; the variable gone; rehearsals on homes, run by the parent | after the wave |
| **d** the words (help, web locales, driver; a) | §2 S4 | `names:check --strict`; parity; no *actor* in the window or the room; `ACTORS` folded into `ROLES` | after the wave |
| **s** the timeout doors (driver, service; ae) | §6 X1 | each door gone; the settings part clears the key | after the wave |
| **q** push in git access (driver, modules, web; ae) | §5 G2 | a home with `no-push` off, converted: a session pushes as before and `no-publish` is off; the judge reads git access alone | after the wave |
| **p** the git part (driver, modules, web; ae) | §5 G1 | an unmarked home whose remotes were reached ambiently: held after the update, the row counting hosts; `own` chosen for one host and an identity for another; `GitMove` gone; `GitHttpsProcessTests` | after the wave |
| **v** the accounts part and one word per flag (driver, cli; ae) | §1.4, §6 X4 | a fixture `harnesses.json` holding `early`/`near`, `prefer`/`parallel`, workspace lists and `Pooled`: an edit writes only its entry; the plan folds; Undo; the old flags refused naming the new | after the wave |
| **r** every delete through the journal (driver, modules; the wave) | §5 G3 | each delete door through `GIT_REF_DELETE`, its journal entry and its Undo | after the wave |
| **WIRING1d** every setting in one list (web, modules; a, s, q) | §9 | its census, as filed | after the wave |
| **Wave 3** (inside COMMS1's wave 1) | | | |
| **k** a park answered by the say door alone (service, driver, web; COMMS1e) | §4 M1 | the route and the verb gone; a park answered through the say door with its question id | with COMMS1 |
| **l** `level` alone (service, driver; COMMS1a) | §4 M2 | no writer writes `now`; a waiting `now: true` word read as `now` | with COMMS1 |
| **m** `Said` alone (service, driver, modules, web; COMMS1e) | §4 M3 | each reader on `Said`; an older answer converted; the column read by nothing | with COMMS1 |
| **o** the ends of COMMS1's and D226's kept paths (driver, service; COMMS1b, g) | §4 M5 | each entry registered with its condition; the gate green | with COMMS1 |
| **Wave 4** (measured on the install first) | | | |
| **POOL1d** the accounts part read on the install (the parent; v on the install) | §1.4 | the plan rule by rule in the private notes, then the owner's press | the install |
| **HOMELAYOUT5** what D172 never moves, measured (the parent) | §6 X5 | its measurements in D172's note | the install; a sign-in per harness, the owner's yes |
| **w** the home part (driver; HOMELAYOUT5, ae) | §6 X5 | a fixture home with an entry at each place, and one at both; moved at the next start; Undo | after its measure |
| **x** the quests part (service, driver; the install's census) | §6 X6 | closed six-character quests cleared; the same words published again make a new quest; the legacy lookups gone | after its census |
| **ac** the notes part (driver, web; the install's census) | §8 P4 | finished notes cleared; the readers gone; an unreadable record drawn by the one rule | after its census |
| **y** INTEGRATE1 row k, widened (the owner's sitting) | §7 | as row k names it | the owner |
| **z** *Set up for agents*' residue (web; SETUPUX1y) | §8 P1 | 1y's door census | with 1y |
| **The sessions track** | | | |
| **h** D213's host rows, d to u (driver, and service, web and tools where named) | §3, step 2 | each row's own proof in D213 §11 | serial; i, j, l, m, s-v spend (the owner's yes) |
| **g** the parity rows (driver; their host rows) | §3 C1 | each difference's old and new cases paired on both doors | per the table; the measures are the owner's |
| **i** STAFFPOLICY1e3 | §3 C4 | as filed | as filed |
| **t-i** the adapter classes' end (driver; the host rows) | §3, step 4 | D213 row t's proof on today's transports; the census's exceptions only the permanent ones | after h; the installed proof spends |
| **Wave 5** (the deletions) | | | |
| **af** the transitional reads deleted, part by part (driver, modules, service, web; each part's census ended on the owner's install, in a dated note) | §1.3's entries | the build with that code gone; an old-shape fixture held, with the conversion as its door | after each census ends |
| **FOUNDATION1 row v** (C6, and the copy's reader) | §3 C6, §2 S2 | as filed, each pairing shown ended | after the floor |
| **ag** the conversion's own readers | WORKFLOWCLEAN1 §4.7 | the conversion gone from the build | when the owner says no older home remains |

### 10.3 What needs the install or the owner

- **Spends an account**: TOOL4n's recording; SESSCTX1a with HELPMODE1a3; AGENTLYNTAI1s-ii's measure; FABRIC-C2b; D213's installed
  proofs (rows i, j, l, m, s-v and t-i); BGWAIT2b's probe; STEER3's measure.
- **A sign-in, or account homes moved**: HOMELAYOUT5.
- **Read-only on the install, by the parent**: POOL1d's plan; the `quests` and `notes` censuses; each part's `Ended` before row af.
- **The owner's own press**: each conversion's Apply on the install, in the window; row y's sitting; question 1.

## 11. Question for the owner

1. **Which name does a person type for the desktop's verbs?** Recommended: `daoris-driver`. Every install carries it, every session is
   handed it, and the room already teaches it (93 lines, against 56 for `daoris driver`).
   - *Yes* retires the CLI's hand-off families (`daoris driver|agent|plugin|tool`, and `remote` and `browser` with row u). This narrows
     D194 point 1: the CLI keeps the doctrine and the service's management.
   - *No* keeps D194's hand-off as the name taught, and moves the room and the usage to `daoris …`.

## 12. The census, corrected

- **A1 and P1.** *Set up for agents* is already gone from the page on the wave (1h). Only four test, fixture and story files still name
  it. The census read main before 1h.
- **S1.** `SET_ACTOR` is not only *Clear order*. On the wave, the seeded roles' staffing editor writes through it too
  (`Web/agents/roles/Roles.test.tsx:318`).
- **K2.** The account-pool fold is two things: a pure read in memory that every reader takes (`AccountSet.Of`), and a write at every
  edit (`HarnessSettings.Change`). Only the write is silent.
- **G1.** The git-auth design's §6.8 planned the move with its own *What needs you* row and two presses, which were never built
  (GITAUTH1e's *Left*). What is silent is the mark at the first start.
- **G2.** D198 §6.8 says the switch is read once and written as `machineGitAccess.write`. The code reads it at every act and writes
  nothing (`GitDoors.cs:513`), so the conversion is new work, not a leftover.
- **X6.** A six-character id is resolved only at a publish and in the forgotten check (`Quests.cs:1322-1326`, `:1366`), not by every
  lookup.
- **M4.** CONFIRMWAIT1's queue on the pipe is built. Only the service's person door keeps the expiry.
- **P3.** The page's fallbacks for an older shell are dead under D139. Those for an older host may serve a record from another machine
  (D178), so not all of them are second ways.
- **P4.** A code the page does not know, and a current driver sentence with no code, are not old shapes.
- **I2.** The twins are two implementations of one merge. Their end is one implementation with two callers, not the loss of git's
  driver.

## 13. Rejected

- **A press per area**, which would be a second conversion door (§1.1).
- **Keeping finished history readable by its own old reader** (D228's note).
- **A preselected choice in the git part**: credentials are the person's.
- **`timeoutMinutes` converted into `quietMinutes`**: they are different settings.
- **Renaming stored *actor* codes and fields in place.**
- **t only after Lyntai's moves** (§3).
- **`no-push` kept switchable beside git access.**
- **`SESSION_START_FROM` folded into the hand-over**: a new line is another job.
