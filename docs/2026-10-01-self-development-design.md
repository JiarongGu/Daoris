# Daoris develops Daoris — lanes, a merge queue, and a steward

> The owner, 2026-10-01 (*One product, not a set of screens*): *"we should be able to run sub-agents
> cross darois development … daoris itself need to have a proper develpment cycle too, this also need
> to be designed properly"*. Other repositories will rely on Daoris to make their development smooth,
> so Daoris's own cycle is the first thing it has to run. This is the contract for the DEV rows in
> `TASKS.md`, and its decision is **D115**. Status: **designed; DEV2 built §2.1's file.** It starts from
> `docs/2026-09-30-parallel-development-design.md` (MOD1–MOD9, D106).

## 0. What exists today, read from the code

The parallel cycle that exists is run by an assistant session acting as the parent. It dispatches
subagents into git worktrees and merges them with a tool. Daoris's driver runs none of it.

| Piece | Where | What it does now |
|---|---|---|
| The lane map | `tools/lanes.json` | Seven lanes of path globs, a `parent` list (`TASKS.md`, `docs/task-archive.md`) and a `laneless` list. Only `tools/merge-branch.mjs` reads it, and only to print a report: *"it informs; it never refuses on a lane"*. |
| The merge tool | `tools/merge-branch.mjs` | From the parent's own checkout, clean and on `main`: the lane report, a commit check (each commit carries its `Co-Authored-By:` line, the worktree holds nothing uncommitted), `git merge --no-ff --no-commit`, then every gate `daoris.gates.json` declares plus the release workflow's rehearsals, ordered check, suite, rehearsal. A failed `Process` gate re-runs its named tests alone once, and a rehearsal that died re-runs whole once (FLAKE). A `--batch` runs the rehearsals once after the last branch. It never commits. |
| The brief | `.claude/skills/dispatch-subagent` | The subagent's half (read, stay in your lane, run only the fast halves, take exactly the reserved number) and the parent's half (reserve the number, name the lane, at most three at once, merge, write the records). |
| The gates | `daoris.gates.json` | Eleven gates, the two `Process` halves among them (MOD8). Kept apart from the manifest on purpose, because it names commands that execute (D26). |
| The planner | `Planner.cs` | Oldest open quest first. A repository without trees runs one session at a time in its root. **A repository with trees on already runs several side by side, up to the cap** (PAR1); only a resume or a carry-on waits for its own tree. The cap is machine-wide, default 2 (`DriverConfig.Empty`), and it counts every active record, a parked one included (`slots = Cap − Active.Count`; `AwaitingPerson` is one of the ledger's active states). |
| The tick | `Driver.TickAsync`, `DriverWatch` | **A tick waits for every session it started.** `RunAsync` holds the process until it exits (`HoldAsync` → `WaitAsync`), `TickAsync` awaits them all, and `DriverWatch` ticks again only after that. So sessions started in one tick run together, and nothing new starts until the last of them ends, up to `timeoutMinutes` (default 30). |
| Trees | `SessionTrees.cs` | `<home>/trees/<workspace>/<repository>/s-<8>` on a branch `daoris/s-<8>`, grown from the repository's line (D86). The merge door refuses unless the root checkout is clean and on the line, read immediately before it merges. The clean-up judges `refs/heads/daoris/*`, and counts a commit landed once any branch outside `daoris/` holds it (D88). |
| The lock between starts and replays | `TreeLock.cs` | `<home>/locks/trees/<workspace>/<repository>.lock`: a start takes it shared, bringing up to date takes it alone (LEFT2). |
| Landing | `Landing.cs` | Two forms, `merge` and `branch`, chosen repository, then workspace, then merge (`LandingRules.Choose`). A branch rule may name a plugin that pushes (D100). |
| The ledger | `SessionLedger.cs` | Opens a session per **tree** (`ActiveForAsync(repository, tree)`). `AnswerAsync` ends a parked record `completed` with the answer, and the quest is carried on in its tree at the next tick (D83). |
| The exchange | `QuestExchange.cs` | Refuses a self-addressed quest: *"That is the repository you are in … Use its own backlog."* Takes, closes and declines carry no identity. Quest ids are content-derived, and D65 widened them with the parent only when there is one (`Quests.MakeId`). |
| One loop | D104 | `<home>/driver.lock`: one live driver loop per home. |

Two premises in the ask need correcting before anything is designed on them:

1. **"One session per repository" is only the rule for a repository without trees.** With trees on,
   PAR1 already starts several at once. What no repository has yet is a rule saying which sessions
   may run beside which. Two sessions on the same files are free to start, and they collide at merge.
2. **Sessions do not yet run side by side over time.** Because a tick waits for its sessions, a quest
   published while one runs waits for it to finish. Lanes that run continuously need a session to
   outlive the tick that started it (§3.1). This is the first thing to build.

## 1. The shape

D46 §9 already gave the answer for parallel work inside one domain: *"wanting parallelism within a
domain is a reason to split the domain, not the tree."* **A lane is that split: a domain inside a
repository.** The repository declares its lanes in a file of its own, and a quest may address one
(`daoris:web-shell`).

- **One session per lane at a time**, each in its own tree on its own branch.
- **A cap per repository** on how many run at once, with the queue counted.
- **A lane session readies its branch rather than landing it.** The **queue**, a third landing form
  beside merge and branch, gates it serially in a tree of its own. On green the queue lands it with a
  fast-forward of the line. On a conflict or a failed gate it sends the work back to the same
  session with the log.
- **A steward lane keeps the records.** It turns a repository-wide quest into lane quests with their
  decision numbers reserved, and it records each landing.
- **The person sets the target, reads the landed history and looks at the window.** Push, publish,
  release and history stay theirs.

What is judgement stays with a session: splitting the work, doing it, the words of an outcome, the
records. What is mechanical goes to the driver: which session may start, the gates, the lane check,
the merge, the send-back.

## 2. Lanes as domains within a repository

### 2.1 The file: `daoris.lanes.json`, the repository's own

`tools/lanes.json` moves to the repository root as **`daoris.lanes.json`**, beside `daoris.json` and
`daoris.gates.json`. It stops being one tool's private map and becomes a declaration the driver, the
queue and `connect` read. A repository with no such file has no lanes.

```json
{
  "_why": "…",
  "lanes": [
    {
      "id": "web-shell",
      "title": "Web shell",
      "summary": "The page but its Settings: the frame, the bridge, every other screen, the page's build.",
      "paths": ["src/Daoris.Web/**", "!src/Daoris.Web/src/SettingsView*.tsx", "!src/Daoris.Web/src/settings/**"]
    },
    {
      "id": "records",
      "title": "Records",
      "steward": true,
      "summary": "The backlog, its archive, and this map.",
      "paths": ["TASKS.md", "docs/task-archive.md", "daoris.lanes.json"],
      "gates": ["universal", "cli"]
    }
  ],
  "laneless": [
    { "what": "The docs, read by every lane and written by the change that makes them stale.", "paths": ["docs/**", "README.md", "CLAUDE.md"] }
  ]
}
```

- **`id`** is how a quest addresses the lane: lower-case letters, digits and dashes, starting with a
  letter, unique in the file. **`title`** and **`summary`** are for people and for the agents that
  address the lane. **`paths`** keep today's glob grammar exactly (`*`, `**`, `{a,b}`, `!` carving a
  narrower lane's files out of a wider one). A path belongs to the first lane that owns it, as
  `classify` reads it today.
- **`steward`** marks at most one lane: the one that owns the parent's records, and the one a quest
  to the whole repository goes to (§5). It replaces today's `parent` list, whose paths it owns.
- **`gates`** (optional) names the declared gates this lane's landing needs, when it is landed alone.
  Absent means every gate. Only a lane whose files nothing outside the named gates reads may narrow.
  For Daoris that is the steward's lane alone: the universal gates scan and link-check its files, and
  `verify` reads them (the budgets, the duplicate check, the lanes test). A code lane never narrows:
  that was MOD9's incident, where a "web only" batch skipped the .NET suites and two C# tests that
  read the web's catalogues broke on main.
- **`laneless`** stays what it is: paths that belong to no lane on purpose, each group with its reason.
  LEFT1's test still refuses a tracked file that is in no lane and not listed here.
- **Absent is a rule.** No file means no lanes. A file whose `lanes` is empty means no lanes. A lane
  with no paths, a repeated id, two stewards, and a `gates` entry naming no declared gate each make
  the file unreadable, and the driver says which. An editor keeps any field it has no use for.
- **The union records are not declared here.** They are git's own attribute (`merge=union`,
  `.gitattributes`). The driver asks git (`git check-attr merge`), rather than parsing the attribute
  file a second time.

For Daoris the file carries today's seven lanes under ids (`web-shell`, `web-settings`, `driver`,
`modules`, `service`, `cli`, `tools`) and an eighth, the steward's `records`. *Tools and records*
becomes *Tools*, since the records now have their own lane.

*Built by DEV2: the file is at the root with those eight lanes, and `tools/merge-branch.mjs` reads it,
by §2.1's rules, for its lane report. The docs' laneless group carves the steward's archive out
(`!docs/task-archive.md`), so no tracked file has two places. Nothing else reads it yet: the registry
is DEV4's, the driver's reader DEV6's, and `gates` waits for the queue (DEV5).*

### 2.2 A quest addresses a lane

- **Spelled `repository:lane`, or `repository:lane+lane`** for work that must cross lanes, at every
  door: `quest_publish`'s `to`, the composer, `daoris-driver ask --to`, the intake. The exchange splits
  the address once. **The quest keeps `to` as the repository and gains `lanes`**, a list. Every place
  that keys on the repository (the registry lookup, the planner, the permission scopes, the ledger,
  the sync) goes on reading a repository name.
- **The id widens only when there are lanes**, as it did for a chain's parent: `MakeId` appends
  `@<lanes, sorted, joined by +>` when the list is not empty. Every existing quest keeps its id.
- **The registry carries the words, never the globs.** `connect` reads `daoris.lanes.json` beside the
  manifest and sends each lane's `id`, `title`, `summary` and `steward`. The registration keeps them the
  way it keeps `uses` (D91), and the registry's HTTP and MCP answers list them, so an asker can see what
  it may address. The globs stay in the file. The driver and the queue read them from the repository's
  line, never through the service.
- **The exchange judges the address.** It refuses a lane the registration does not declare, naming
  the lanes it does. It refuses any lane on a repository that declares none, saying to address the
  repository. **A quest from a repository to one of its own lanes is allowed.** Work for another lane
  is work for another session, which is what the self-address refusal exists to protect. A quest from
  a repository to itself with no lane is still refused, with today's sentence.
- **The line's file is the authority for what runs**, and the registration is what askers read.
  `connect` refreshes the registration, as it does the domain. A quest naming a lane the line no
  longer declares sits, and says which lanes there are now.
- **An older build reads a quest with lanes as a quest to the whole repository**, since the field is
  additive. It does not check lanes at landing, so the lane rule holds on machines that run this build,
  which is every machine one person drives.
- **The intake's room lists each repository's lanes** (D65 §1b), and says that a quest to the
  repository alone goes to its steward to split.

### 2.3 What a lane owns, and staying in it

A lane owns its paths and the tests beside them. **Laneless paths**, such as design documents, the
canon synced into the example family, and the harness settings, are writable by any lane, as they are
today. **The union records** (the decision log, the changelog, the fix log, the documents index, the
twins table) are writable by any lane for its own entries, and the duplicate check guards them (D106).
**The steward's paths** are the steward's alone.

"Stay in your lane" means three things to a driven session:

1. **The room it is told.** `TargetPrompt` gains a lane section, in the canon's words, never in
   decision numbers. It says which lane or lanes the quest is for, what they own, and that the
   repository's other lanes are other sessions' work running beside it. It says to change only those
   files, the tests beside them and the shared documents the change makes stale. It says to publish a
   quest to `repository:lane` and wait on it (D79) when the work needs another lane, and never to
   edit the steward's records. The target already carries the landing, reading and boundary
   sections, and this is one more.
2. **The files it may write.** Nothing is enforced at spawn. A harness's permission rules cannot
   hold it: a deny beats an allow, so "deny everything, allow the lane" cannot be written (D72). A
   shell writes wherever it likes anyway. And over the protocol door a permission request is refused
   by construction (D52), so a rule the work needed would stall the session rather than ask. The
   session is told, and the queue checks.
3. **A check at landing.** The queue lists the paths the branch changed (`git diff --name-only`
   from the merge base with the line) and classifies each one against the **line's** copy of the lanes
   file, never the branch's, so a branch cannot widen its own lane. The order is the one
   `classify` uses today: the steward's paths, the union records, the first lane that owns the path,
   laneless, and outside. The branch passes when every path is in one of its quest's lanes, a union
   record, or laneless. Anything else sends it back naming each path, the lane that owns it and the
   address to ask it by:
   - a path in a lane the quest does not name;
   - a steward path in a quest that is not the steward's;
   - a path outside every lane.

   **It refuses, where the merge tool only reports.** In the assistant cycle a parent reads the
   report and judges each crossing. In the driven cycle no parent reads the middle, so the judgement
   moves to dispatch: the steward names every lane a piece of work needs, and the check is a fact.

### 2.4 A repository that declares no lanes

Nothing changes for it beyond §3.1, which lets any repository's sessions outlive their tick. Its quests
address the repository. The planner runs one session at a time in its root, or several in trees of
their own where trees are on (PAR1). It lands by merge or branch as
its rule says. The queue form is open to it too: the lane check has no lanes to apply, and every
other step is the same.

A repository that declares lanes, but whose quest names none and which has no steward, runs that quest
**holding every lane**. It runs alone, which is today's one-session-at-a-time rule, now stated for
lanes.

## 3. Concurrent sessions in one repository

### 3.1 A session outlives its tick

The watch keeps the running sessions as tasks of its own. A tick plans over the ledger's active list,
starts what may start, and returns. Each session concludes when its process ends, on its exit code and
its quest, as it always has (D46 §4), and its ending joins the next tick's report. The pieces that run
beside sessions today still run: the sync beside them (D68 §6), the stop for a lost claim, the orphan
sweep, and the shutdown that marks a record interrupted (D104). `RunUntilIdleAsync`, the gates'
deterministic mode, becomes *until nothing runs and a tick starts nothing*.

This changes every repository's behaviour, not only a laned one's: a quest published while a session
runs starts at the next look rather than after it. It is still bounded by the caps, the holds and the
tree rule. That is what the cap always said it meant (*"concurrent sessions across all repositories"*).

### 3.2 The lane lock

**A lane is held while work on it is in flight.** Three things hold it:

- any active record whose quest names it, whether working, parked for the person, or waiting in the
  queue;
- any quest the planner would carry on in its tree, because its last session failed, was interrupted,
  or was answered. This covers the gap between the queue's answer and the carry-on that closes the
  quest;
- any entry in the queue.

The lane frees when its quest closes, or when the person's own stop ends the work. It is held across
the wait in the queue on purpose. A second session in that lane would grow from a line that lacks the
first one's work, and would collide with it at the queue. For the steward, this is what stops two
stewards reserving the same decision number (§5).

**The lane lock is the planner's, never the ledger's.** D51 separated two jobs: the tree lock prevents
two agents corrupting one git state, and the ledger holds that lock per tree. Pacing a domain is a
different job. Two lane sessions on the same file corrupt nothing; they conflict at merge, and the
queue catches it. So the ledger keeps its per-tree rule unchanged, and the lane lock lives where D51
put the old one-session-per-repository rule.

### 3.3 The cap

- **`laneCap`**, in `driver.json` (machine-local, with a CLI twin in `driverconfig.ts`, a verb
  `daoris driver lanes cap <n>`, and a control in Settings), **default 3**. It counts the sessions of
  one repository that have a process (starting or working), plus one while the queue gates for that
  repository.
- **The machine's `cap` still bounds every repository together.** The smaller of the two decides.
  **A record waiting in the queue counts against neither cap**: it has no process and builds nothing. A
  record parked for the person counts against the machine's cap as it does today. Changing that
  reading for existing parks is not DEV1's to make.
- **Quiet.** While the queue runs a gate declared `quiet` (§4.3), no new session starts in that
  repository. Running sessions carry on. A new session's first act is the heaviest load there is,
  because a fresh tree holds no dependencies and no build outputs (D51's own sentence). Holding back
  exactly that is what keeps the `Process` half honest without stopping anyone's work.
- **Why 3.** FLAKE1's sightings came with three worktrees building beside the parent's serial run.
  Three with the queue counted leaves two sessions building beside it, which is below that edge. The
  quiet rule then keeps new trees from starting under the `Process` half. The dispatch skill's *"at
  most three at once"* is the same number from the same evidence. Only a real run can say whether the
  default holds on the owner's machine (§9). The queue counts its FLAKE lines, so that run can measure it.

### 3.4 How the driver picks what runs next

In the service's oldest-first order, for each open quest, every check that stands today comes first:
registered and addressable, the door, drivable, held, a root, the strikes. Then, for a repository that
declares lanes:

1. **Trees must be on.** A laned repository without them runs one session at a time in its root, and
   the reason says to turn trees on, as PAR1's does.
2. **Resolve the lanes.** They are the quest's own, else the steward's for a quest to the whole
   repository, else every lane. A lane the line's file does not declare means the quest **sits**, and
   the reason names the lanes there are.
3. **Held lanes wait.** If any of its lanes is held, the quest waits as `RepositoryBusy`, saying which
   lane and which session.
4. **The older quest reserves.** If an older quest this tick could not start and names one of its
   lanes, this one waits: *"queued behind `#q` for lane `x` — oldest first"*. This is today's
   `startedThisTick`, extended to lanes. It costs some packing, and it means a quest that crosses two
   lanes is never starved by younger single-lane ones.
5. **Quiet.** If the queue is running a quiet gate in this repository, the quest waits and says so.
6. **The caps.** `laneCap`, then the machine's cap, both as `AtCapacity`.
7. **Otherwise, start.** A resume or a carry-on goes back into its own tree, as today, and holds its
   lanes again.

Everything stays in the pure planner. Plan and apply are separate, and every reason is a sentence a
person reads (D46 §3).

## 4. The merge queue: Daoris's own landing

### 4.1 A third form, and what readies a branch

**`queue`** joins `merge` and `branch` (`LandingForm`). It is set per repository or per workspace like
the others (`daoris driver landing <repository> queue`, `SET_LANDING`). It needs trees on and a
`daoris.gates.json`, and setting it without either is refused, naming which. `tidy` applies as it does
for the other forms (D88), since the line then holds the work.

A driven session under the queue **does not close its quest when its work is committed.** It calls
**`session_ready`** on its connector, naming its tree's head. The ready is written on its own record,
only for its own quest while it runs, as the take is (D83). Then it ends its turn still holding the
quest.

When it ends, the driver checks four facts: it took the quest, its record says ready, the head it named
is its tree's `HEAD`, and its tree is clean. If all four hold, the record parks with a new field,
**`queued`**, and the branch becomes a queue entry. Otherwise the record parks for the person as today
(D83). Nothing is queued that the session did not say was ready.

`queued` is a field and not a state, for D104's reason: a new state reaches every surface, and an older
build parses states strictly. The "needs you" band skips a queued record. Sessions shows it as *in the
queue*.

### 4.2 Where it gates: a tree of its own

The queue gates in a **queue tree**, never in the person's checkout. `tools/merge-branch.mjs` merges
in the parent's checkout because the parent owned it. In the driven cycle the root checkout is the
person's (D51). A gate run takes an hour, and the person's work in flight would block every landing.

- **Where.** `<home>/queue/trees/<workspace>/<repository>/q-<8>`: a linked worktree at the line's
  tip, made for a batch and removed after it. It sits **outside the trees home**, so `SessionTrees`
  never lists it, lands it or sweeps it.
- **A detached `HEAD`, no branch.** The clean-up's proof counts a commit landed once any branch outside
  `daoris/` holds it (D88). A queue branch holding a session's commits would make that session branch
  read as landed, and the sweep would delete work that is only in a scratch tree.
- **One queue gates at a time per machine.** The load is the machine's, and two repositories' gate
  runs at once would be FLAKE1's load again.
- **The queue runs beside the tick**, as a task the loop owns, not inside a tick. A queue entry file
  per repository (`<home>/queue/entries/<workspace>/<repository>.json`, written beside and renamed)
  holds the entries, their state and their verdicts. It is machine-local, like a transcript (D47 §4).

### 4.3 The steps, for each entry, in order

1. **The universal half of the commit check.** The entry's branch head is the head that was said to
   be ready, and its tree is clean. This is the merge door's own rule.
2. **Merge** with `--no-ff --no-edit` into the queue tree. The append-only records resolve themselves
   through their `merge=union` attribute (D106). What union can leave behind, a number or a row twice,
   is refused by the duplicate check, which runs inside the repository's own declared gates (`verify`,
   for Daoris). A conflict is aborted and **sent back**, naming the files (§4.5).
3. **The lane check** against the line's copy of the lanes file (§2.3).
4. **The declared gates**, one at a time, in declared order within each kind: **checks, then suites,
   then rehearsals.** Two optional fields join a gate in `daoris.gates.json`:
   - **`kind`** (`check`, `suite` or `rehearsal`; absent is `suite`) orders the gates and decides what
     a batch runs once.
   - **`quiet`** (absent is `false`) marks a gate that needs the machine quiet and may flake under
     load.

   The two `Process` halves and the rehearsals are declared quiet. The release and family rehearsals,
   which today are only the workflow's, become declared gates, so the declaration is the one list and
   nothing reads the workflow file. Declared rather than inferred: `merge-branch.mjs` infers the kinds
   from Daoris's own command strings, and a family feature cannot.
5. **The repository's own commit rules are a declared check.** Every commit carrying its
   `Co-Authored-By:` line is Daoris's convention, not the family's. It moves out of the merge tool into
   `tools/commit-check.mjs`, declared as a check gate that reads the range of the merge it runs on.
6. **Logs.** Each gate's whole output goes to `<home>/queue/logs/<entry>/<gate>.log`, written beside
   and renamed when the gate ends. Each verdict is a line in the machine log (D94) without anyone's
   words, so queue time and flakes can be counted.

### 4.4 Batches

A batch is every entry ready when it starts, up to `laneCap`. The entries merge one after another into
one queue tree. The checks and suites run after each merge, and the rehearsals once, on the batch's
last merge, as `--batch` does today. A batch's gate set is the union of its entries' sets, so a steward
entry riding with code runs everything.

If a rehearsal fails for the batch, and the failure is not a flake, nothing lands. The batch is re-gated
as singles, in order, each with its own rehearsals. Bisecting would find the culprit faster; it is held
until a real queue shows batches failing often enough to need it.

### 4.5 A conflict or a failed gate: sent back, never forced

- **Sent back means answered.** The queue answers the entry's parked record through the answer door
  (`SessionLedger.AnswerAsync`, D83), in the queue's words. The answer names the step, the gate and its
  command, and gives a bounded tail of the gate's own output. It says the whole log stays on the
  machine and names where. The planner then carries the quest on in the same tree, handing the session
  those words. The session fixes the work, commits, and readies it again. No new quest is published
  and no closed quest reopens.
- **Never forced.** The queue never resolves a conflict, never forces a merge, never rebases a
  session's commits (D87), and never skips a failed gate. A conflict leaves the queue tree as it was
  before that entry's merge, and the batch goes on without it.
- **The flake rule, declared.** A failed `quiet` gate runs once more, whole, with no session starting.
  If it passes, the gate reads FLAKE, is recorded on the entry and counted in the machine log. If it
  fails again, the gate has failed. A gate not declared quiet is never re-run, because a failure
  outside the `Process` half is real. This is coarser than the merge tool's per-test re-run, which
  reads `dotnet test` lines, and it is project-agnostic. The merge tool keeps its finer rule while it
  lives.
- **Three strikes.** An entry that fails for the third time is not sent back. Its record stays parked
  for the person with the verdict, in "what needs you". The count is derived from the queue's own
  record, never tallied (DRV6), the way D58 parks a quest after three failed sessions.

### 4.6 Landing: a fast-forward of the line

Once the batch is green, the queue lands it in four steps:

1. It takes the repository's `TreeLock` alone, as bringing up to date does.
2. In the root checkout it checks, immediately before acting, that the checkout is clean, is on the
   line, and that the line is still at the batch's base. These are the merge door's own guards.
3. It runs `git merge --ff-only` to the queue tree's head.
4. It lets the lock go.

What lands is what was gated:

- **The line has moved** (the person committed, or bringing it up to date pulled a merged pull
  request): the batch is re-gated on the new tip.
- **The checkout is dirty or off the line:** the gated batch waits. It says why, is retried at each
  look, and lands the moment the checkout is clean.
- **The queue never writes into the person's work in flight.**

Then each entry's record is answered *"landed as `<merge commit>` on `<line>`"*. The carry-on session
closes the quest `done` with its outcome. Its chain's next step, such as the steward's record step,
publishes then (D65 §4). The tree goes if the rule says tidy. The lanes free when the quest closes.

**The close costs one short carry-on per landing.** The queue closing the quest itself was considered
and rejected (§10): D46 keeps quest state for sessions and people.

**A chain inside a queue repository grows from the line.** Its next step publishes only once the step
before has landed, so the line already holds that work. D82's rule, growing from the step before's
branch, is for work that has not landed, and here none is waiting.

### 4.7 What a gate sees

Each gate runs with **every `DAORIS_*` variable of the driver's own removed**, and `DAORIS_HOME` set to
a scratch home under `<home>/queue/homes/<entry>/`, deleted afterwards. No gate reads the live home, its
registry, its sessions or its `driver.json`. The gates build and run the queue tree's own code.

**The install running the queue is never a gate's subject.** The tools that stop processes pick them by
path, never by name (`tools/processes.mjs`), which is what lets a rehearsal run beside a running
install. The dispatch skill forbids a subagent to touch the owner's running install, its folder, its
home or its processes; the queue holds the same line by structure rather than by instruction.

### 4.8 A branch from outside: the second door

Driving is additive (D46 §2). **`daoris-driver queue add <branch> [--repository <r>] [--lanes a+b]`**
puts any branch of a registered repository in the queue: a person's, an interactive session's, a
subagent's. The same steps run. There is no session to answer, so the verdict is the terminal's, the
screen's and the entry's. `queue list`, `hold`, `release` and `drop` complete the door. The screen's
twin is the repository's queue in Sessions (D50). Each new verb and control gets an Ask Daoris door, or
is listed as owed, under D110's coverage test.

### 4.9 Where the person looks

- **The landed history.** Each landing is one merge commit whose message names the quest, its lanes
  and each gate's verdict, and can be reverted as a unit.
- **The queue itself.** What waits, what is gating now, and what landed since the person last looked.
  Each gate's log opens as the console's raw view.
- **The window, after the person republishes** (§6).

## 5. Who keeps the records

| Record | Who writes it | When |
|---|---|---|
| The backlog: rows added, decision numbers reserved | the steward session | when it plans |
| The backlog: a row moved out, and its State counts | the steward session | at its record step |
| The archive entry, with the outcome paragraph | the steward session, from the lane's closing note | at its record step |
| Its decision, under its reserved number | the lane session | with its work |
| Its changelog line, its fix log entry, its documents index row, its twins row, the design documents it amends | the lane session | with its work |
| The quest's close and its outcome | the lane session, carrying on after the landing | once landed |
| The lane check, the gates, the verdicts, the logs, the merge commit, the send-back | the queue | mechanically |

**The steward is a session in the steward's lane.** It is not the intake and not the queue.

- **The intake stays the workspace's router.** It never writes into a repository (D65 §1b), and it does
  not know a repository's backlog, contracts or decision log. It addresses the repository, and the
  repository's steward splits the work.
- **The queue cannot keep the records.** Moving a row is mechanical only in Daoris's own format. The
  `task-lifecycle` rule names the backlog and the archive generically, and each adopter keeps its own.
  A family tool that edited them would carry one repository's conventions to every other. The words of
  an outcome are judgement too.

**Planning.** A quest to a laned repository with no lane goes to the steward. The steward's instruction
is the repository's own doctrine; for Daoris that is the dispatch skill's parent half, rewritten as the
steward's. It:

- reads the backlog and the contract the work cites;
- splits the work into lane quests, each naming its lanes, its contract section, its start and its
  reserved decision number;
- publishes each lane quest with a `then` step to `repository:<steward>` titled for the record;
- lands its own rows through the queue.

It **reserves a decision number** by the parent's rule: the next after both the highest in the decision
log and every number named in an open row. The steward's lane lock spans its time in the queue (§3.2),
so a later steward always reads a line that already holds the earlier rows. The duplicate check
(D106) still refuses a number twice as a fact.

**Recording.** The record step for a quest moves the row to the archive with the lane's closing note
as its outcome, and updates the counts from the verdicts the queue answered with. Any FLAKE line goes
under FLAKE1. The steward's branch lands through the queue under its lane's narrow gates when it lands
alone.

**The lane sessions write their own shared entries.** Today the parent writes the changelog line. The
changelog is a union record, so a lane can write its own line without colliding, and the session that
did the work knows best what a person will notice. That leaves the steward the one record union cannot
serve, the backlog: a row moves out of it, and union would bring the row back (D106). The archive stays
with the steward because moving a row is one act across both files.

**What it costs.** A landing costs the lane's working session, a short carry-on to close it, and a
steward step to record it. Entries batched together share one gate run. Only a real run says what that
costs in time and accounts (§9).

## 6. Where the person stands

- **The target.** An ask, or a row they write, is theirs. The steward's split lands visibly as backlog
  rows. It is not held for approval, because D37 puts no person in the middle of a run: the target is
  the person's, and the split is judgement the steward records.
- **The final diff.** The landed history since they last looked: one merge commit per landing, with
  its verdicts, and the review of a landed session reading its landed work (D113).
- **The look on the window.** **Republishing the install is the person's press, and the queue never
  does it.** The driver running the queue is the install, so republishing stops the loop, and D104
  marks every running session as interrupted. The lane session's closing note names what to look at
  on the window, as a hand-back does today. The steward carries that into the archive. A defect the
  look finds becomes a quest, as WSR7, REVIEW2 and TABS1 did.
- **Everything irreversible stays theirs.** The queue never pushes (the line is local), never
  publishes, never releases, never rewrites history, and never deletes anything the D88 proof has not
  cleared. D37's boundary stands whole.
- **Their controls, each with two doors:** hold a repository's starts; hold its queue's landings; drop
  an entry; set `laneCap`; set the landing rule.

## 7. The phased build

Each row is dispatchable as written, in the order listed. Rows in the same lane go one after another.
The driver's lane is the longest chain: DEV3, DEV5, DEV6, DEV7. DEV2 and DEV4 can run beside DEV3.
D115 decides all of it, so no row needs a decision number of its own unless building it finds
something D115 did not decide. Every row with a rehearsal check is proven by the parent's serial run
at merge, never in the worktree.

| Row | What lands | Lanes | What proves it |
|---|---|---|---|
| **DEV2** | `tools/lanes.json` → `daoris.lanes.json`: ids, summaries, the steward's `records` lane replacing `parent`, *Tools and records* → *Tools*. `merge-branch.mjs` reads the new file. The lanes test and the design's §5 table move with it. The dispatch skill names lanes by id. | Tools; laneless (docs, the skill) | `merge-branch.test.ts`: every tracked file classified as before, the steward's paths where `parent`'s were; `--plan` on a real branch |
| **DEV3** | Sessions outlive their tick (§3.1). The watch keeps running sessions; a tick starts and returns; endings join the next report; `RunUntilIdleAsync` waits until nothing runs; the sync, the lost-claim stop, the sweep and the interrupted shutdown unchanged. | Driver library; Desktop modules (the loop's reports) | Driver tests: a second quest published during a long stub session starts at the next look; a shutdown still marks both interrupted. The family rehearsal's driver phases, unchanged |
| **DEV4** | Lanes in the registry and on the quest (§2.2): `connect` sends them; the registration keeps them; the registry answers list them; the exchange parses `repository:lane[+lane]`, widens the id only with lanes, refuses unknown lanes and a lane on a repository that has none, and allows a self-addressed quest only with a lane. The wire and the remote sync carry `lanes`. The intake's room lists them. | CLI; Service; Driver library (the room, `QuestView`); Web shell (a quest's lanes shown and composed) | Service and CLI tests. Family rehearsal: an example declares two lanes; `engine:core` publishes; `engine:nope` is refused naming both; `engine` to itself is refused, `engine:core` from `engine` is allowed |
| **DEV5** | The queue for a branch from outside (§4.2–§4.4, §4.6–§4.8): the `queue` form, entries under the home, the detached queue tree, the commit check, merge, the lane check, gates by declared `kind` with the `quiet` re-run, batches, logs, the scrubbed environment, the fast-forward under `TreeLock`, one queue gating per machine, `daoris-driver queue add\|list\|hold\|release\|drop`, machine-log lines. The devkit's `GateDeclaration` accepts `kind` and `quiet`. `daoris.gates.json` declares them and the two rehearsals; the workflow runs the declared set. `tools/commit-check.mjs` replaces the trailer check. | Driver library; Tools (gates, workflow, devkit, commit check); CLI (the form in `driverconfig.ts`) | Driver `Process` tests over real git: a scratch repository with two lanes and stub gates that pass, fail and flake; a conflict and a crossing each leave the line unmoved; a dirty root holds. Family rehearsal: an example branch queued from the terminal lands on green; a failing gate leaves the line where it was |
| **DEV6** | Lanes side by side (§3.2–§3.4): lanes read from the line as git objects, never the checkout (D113); lane locks; the oldest-first reservation; `laneCap` in `driver.json` with its CLI twin, verb and Settings control; the whole-repository quest to the steward or to every lane; `lanes` on the session record; the prompt's lane section. | Driver library; CLI; Service (the record field); Desktop modules (the route, the help door); Web settings | Planner unit tests (pure). Family rehearsal: two quests to two lanes start side by side; a third to a held lane sits with its reason; a whole-repository quest waits for every lane; the cap holds under a long stub |
| **DEV7** | A driven session readies, and the queue answers it (§4.1, §4.5, §4.6): `session_ready` on the connector and in the connector's allowance (D72); the `queued` record field; the park as queued; the answer with the verdict; the carry-on that closes or fixes; three strikes to the person; a queue repository's chain growing from the line; the prompt's queue section. | Service; Driver library; Desktop modules; Web shell (*in the queue*; "needs you" skips it) | Family rehearsal: a stub lane session readies, lands, and its stub carry-on closes it done; a failing stub gate sends it back, the stub fixes it and readies again, and it lands; three failures park it for the person |
| **DEV8** | The queue on the window (§4.9): the repository's queue in Sessions (entries, the gate lines, a gate's log as the raw view, landed since you looked, hold and drop), and Ask Daoris's doors for every new verb and control (D110). | Web shell; Desktop modules; Driver library (the doors) | Vitest over a mocked bridge; the look on the window in both themes and both languages |
| **DEV9** | The steward (§5): routing to it, its narrow gates, the `then` record steps, and Daoris's steward instruction (the dispatch skill's parent half rewritten; its subagent half becomes the lane session's). | Driver library; laneless (the skill); Records (`daoris.lanes.json`) | Family rehearsal: a stub steward splits a whole-repository quest into two lane quests with record steps; both land; both record steps land |
| **DEV10** | **The first user.** A small real row (LOOK1's size) runs steward → lane → queue → record on the owner's install with real logins, then a row that crosses two lanes. Evidence document: time and spend per landing, FLAKE count at the default cap, lane crossings refused, how the steward split. The cap's default and the three strikes are revisited from it. | none (a run) | The evidence; the owner present |
| **DEV11** | **The second user, and the tools retire.** A canon knowledge document on lanes (project-agnostic: what a lane is, staying in it, the steward, the queue) and a second lane in the example family. Then, on DEV10's evidence, `tools/merge-branch.mjs` retires with its tests, and the dispatch skill and `CLAUDE.md`'s dev loop say *queue*. | laneless (canon, examples, the skill, `CLAUDE.md`); Tools | `verify`; the family rehearsal over the laned example; the canon budget reported |

## 8. Meanwhile: the assistant-driven cycle

**What keeps working unchanged until the queue replaces it:** `tools/merge-branch.mjs`, the dispatch
skill, the parent's reserved numbers, the union records and the duplicate check, the `Process`
category and FLAKE1's record, and *at most three at once*.

- **From DEV2**, the merge tool and the skill read `daoris.lanes.json`. It moves in the same commit as
  its reader and its test, so nothing is ever read from two places.
- **From DEV5**, with the install republished, the parent may hand a subagent's branch to
  `daoris-driver queue add` instead of the merge tool. The queue gates it in its own tree, so the
  parent's checkout is free during the run. The fast-forward still needs that checkout clean, so the
  parent writes the records **after** the landing, in a commit of their own. The merge commit no longer
  carries them. The merge tool stays the fallback.
- **From DEV7 and DEV9**, a subagent becomes a driven lane session and the parent's judgement becomes
  the steward's. The owner asks Daoris, and nobody has to act as the parent. The skill's two halves
  stay, as the lane session's doctrine and the steward's.
- **At DEV11**, the merge tool goes, on the evidence of DEV10, not before it.

## 9. What a rehearsal can prove, and what only a real run can

**The family rehearsal can prove the mechanism**, with stub agents and no model: lanes addressed and
refused, locks and reservations, the caps, the queue over real git in the example repositories, the
send-back loop, the steward's split and its record steps, the terminal door. The planner's decisions
are pure and are held by unit tests. The queue's git work is held by `Process` tests over scratch
repositories.

**Only a real run can prove the rest:**

1. **Whether a real harness stays in its lane from the room alone.** The check catches every crossing.
   The run measures how often one happens.
2. **Whether `laneCap` 3 with the quiet rule keeps Daoris's own `Process` half clean** on the owner's
   machine under real builds. It is measured by the queue's FLAKE count over DEV10's landings.
3. **Whether Daoris's real gates run in a queue tree under the install's home, beside the running
   install.** Three risks here:
   - Path length: a tree under `data/queue/…` is deep, and the deployment rehearsal publishes a whole
     Chromium beneath it.
   - The environment scrub is complete.
   - `test:web` and `rehearse:deploy` build and stop only their own.
4. **The self-hosting loop.** The install's driver lands changes to its own code, and those changes run
   only after the person republishes.
5. **What a landing costs** in time and in accounts, once the close's carry-on and the steward's record
   step are counted.
6. **Whether a real steward's split is good.** That is judgement, and only use shows it.

## 10. Considered and rejected

- **Splitting Daoris into several repositories, one per lane.** Twins must change in one commit
  (`.claude/knowledge/twins.md`), the five artefacts share one set of gates and one release, and the
  collisions MOD1 measured were inside files, not between assemblies (§7 of the parallel design).
- **Lanes in `daoris.json`'s `domain`.** The manifest is the doctrine tool's inert contract, parsed on
  every command, and its domain travels to the registry and a remote. Lane globs are development
  mechanics that change with every new folder (LEFT1 places each one). Keeping them there would make
  each such change a manifest change and a re-`connect`. `daoris.gates.json` is the precedent for a
  separate root file (D26).
- **Keeping `tools/lanes.json`.** A tool's private path in one repository cannot be what a family
  feature reads.
- **The lane inside `to`** (`to: "daoris:web-shell"` as a string). Every repository-keyed lookup, scope,
  lock and sync would read a repository that does not exist, and an older build would sit the quest as
  unregistered.
- **Enforcing lanes by permission rules at spawn.** See §2.3: a deny cannot be carved back, a shell
  writes anywhere, and the protocol door refuses the ask by construction.
- **A lane report without a refusal**, as the merge tool does. It needs a parent reading the middle,
  which the driven cycle does not have.
- **The lane lock in the ledger.** Pacing is not corruption (D51). The ledger stays per tree.
- **Waiting for every session to stop before quiet gates.** It starves the queue behind long sessions.
  Holding new starts removes the heaviest load and stops nobody's work.
- **Gating in the person's checkout**, as the merge tool does. See §4.2.
- **Rebasing a lane branch onto the line before gating.** It rewrites the session's commits (D87). A
  merge carries them as they were made.
- **A queue branch instead of a detached `HEAD`.** It would make unlanded session branches read as
  landed, and the clean-up would sweep them.
- **The queue closing the quest on landing.** The driver never writes quest state (D46). Keeping that
  costs one short carry-on per landing, and the carry-on is also where the outcome is best written.
- **The session closing `done` when ready, and failures sent back as new quests.** Done would say
  landed before it was. A chain's next step would start on unlanded work. And only a session or a
  person may publish the send-back.
- **Queue logs read by the session from the home.** A session is not promised the home's files. The
  answer carries the failing tail, and the session re-runs the gate in its own tree.
- **Inferring gate kinds from command strings**, as the merge tool does. That is Daoris's own spelling.
  A family queue reads declarations.
- **Choosing gates by what a branch touched.** MOD9's incident. A lane may narrow only by the
  repository's own declaration, and a code lane never does.
- **The queue or the driver writing the records.** Their format is each repository's, and their words
  are judgement.
- **The intake as the steward.** It never writes into a repository, and it is the workspace's, not the
  repository's.
- **Bisecting a failed batch.** Held; singles on failure come first.
- **A queue per machine across repositories.** Each line is one repository's, and so are its gates.
  Load is bounded instead by one gating queue at a time.

## 11. The twins this creates

Each is added to `.claude/knowledge/twins.md` by the row that builds it, with its test tables:

- **`daoris.lanes.json`**: `merge-branch.mjs` (until it retires) and the driver's lanes reader agree on
  the globs, the classification order and what absence means; the CLI's `connect` reads the words.
- **`daoris.gates.json`'s `kind` and `quiet`**: the devkit's `GateDeclaration`, the driver's queue, and
  `merge-branch.mjs` while it lives.
- **`driver.json`'s `laneCap`, the queue's holds and the `queue` form**: `driverconfig.ts`,
  `DriverConfig.cs` and `Landing.cs`.
- **A quest's `lanes`, and a registration's**: the service's wire and sync, `connect.ts`, `Registry.cs`
  and its import, and the remote payloads, which preserve what they do not read.
