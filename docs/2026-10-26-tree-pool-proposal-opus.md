# TREEPOOL1 — the independent Opus reader's sealed proposal

> Written from `docs/2026-10-26-tree-pool-brief.md` and `docs/2026-10-26-tree-pool-survey.md`, with the decisions and FOUNDATION1's design they cite. Every fact below was checked against the code at `a3f5be21a` with `git show` and `git grep`, and nothing was run. Paths follow the survey's aliases: no prefix means `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and `Tests/`, `Core/`, `Web/` and `Svc.Tests/` mean what they mean there. Line numbers are `a3f5be21a`'s. Row mod-x has not landed, so `Trees/` is still `Trees/`. Every new file named below is written under `Workplace/` once mod-x has landed (foundation design §14.6, last paragraph). `<home>` is the Daoris folder.

## 0. The proposal in one page

1. **A slot is a numbered linked worktree in a place of its own**: `<home>/work/pool/<workspace>/<repository>/t-<n>`. That place is a new row of D172's table, `HomeEntry.Pool`, and it sits outside `HomeEntry.Trees` (`Platform/DaorisHome.cs:405`).
   - Every build already shipped reads a path under `work/trees` as a tree it opened. It derives a branch from that path's last segment (`Trees/SessionTrees/TreeFollows.cs:153-161`) and would resume into whatever line the folder holds at the time (`TreeReturns.cs:56-58`).
   - A path outside its trees home is one an older build refuses to remove, land, lock or tidy (`TreeContext.cs:66-78`; `SessionLocks.cs:126-134`).
2. **A line of work is identified by its branch.**
   - Each line still gets `daoris/s-<8 hex>`, minted once and never handed to another line, so D51 rule 2 stands for branches. What is reused is the folder.
   - A session record keeps the slot it ran in as its `tree`. That stays the ledger's lock key and the agent's working directory.
   - A desktop-store row says which branch the record worked on.
   - A reader that asks *which line* reads the branch. A reader that asks *which folder is busy* reads the path.
3. **The lease lives in the desktop store**, as a Workplace family.
   - There is one in-flight claim per slot, enforced by the primary key, and the claim names its process run.
   - The look takes over a claim whose run is dead, by the same test §13.4 uses for a move's claim.
   - Git's own lock stays the guard against outside tools: `daoris session <id>` while a session holds the slot, and a new reason, `daoris kept tree`, while it is free.
4. **Bringing a slot to the line** means `reset --hard`, then `clean -fd` (never `-x`), then `switch -c` for a new line or `switch` for a returning one.
   - Tracked changes and untracked files are cleared. Every ignored path is kept.
   - This happens only after a ref holds everything that was there.
5. **No reset touches work that no ref holds.**
   - When a line lets go, its uncommitted and untracked work is written to a parentless commit on `refs/daoris/kept/<leaf>/<n>`, off its branch.
   - That commit is made again and compared right before any reset.
   - When the line comes back, the work is laid back.
   - Until the owner agrees that Daoris may write that commit, a slot left with uncommitted work stays held by its line, and the page says so.
6. **What holds a slot** is exactly what holds a tree today, by the ledger or by the look's in-use set, plus a standing move and an in-flight claim:
   - today's holders: a running or parked record, words waiting, a helper wait, a served review;
   - nothing else holds one. A D79 wait, an ended conversation, a kept lost take and a no-output end hold no slot, because their branch and their kept ref hold their work.
7. **A line coming back**:
   - goes into the slot its record ran in when that slot is free, and its conversation resumes there, with the same working directory and the same trust;
   - when another line holds that slot, it goes on as a new part handed D189's brief, in a free slot.
8. **The count** is how many trees a repository keeps.
   - It is set per repository by `driver.json`'s new `keptTrees`. The default is the machine's cap + 1.
   - No session ever waits for a tree. A start past the count gets an *extra* tree, numbered above it, which is moved aside when let go.
   - Lowering the count retires the slots above it in the same way.
9. **The end** is one of two, and the tree goes back to the pool either way:
   - *done with output*: commits on the branch, ready to land, or uncommitted work kept aside, which the page states separately;
   - *done without output*: its empty branch goes at the next tidy.
10. **Nothing the install holds today changes.**
   - Its `s-` trees and moved-aside folders keep today's rules until they drain.
   - A line that began in an `s-` tree stays there.

Each amendment is a dated note on its decision: D51 rules 2 and 4, and its rejected alternative; D164 point 1 (the off-branch keep); D167 point 4; D178 point 4; D189 point 2; D88's no-output exception; D195's kinds; and KEEPSIZE1b's record for a pooled line.

### 0.1 Where the survey is wrong or stale at `a3f5be21a`

1. **Survey §8.4 mixes holds with keeps.** Only four of its seven "holds" are holds:
   - a parked record (`Core/Sessions/SessionLedger.cs:320-324`);
   - words waiting (`Service/ServiceClient.cs:2226-2250`);
   - a helper wait and a served review (`Trees/Branches/BranchTidying.cs:140-150`).
   A D79 wait, a kept lost take and a tree whose branch never moved are kept by nothing but the tidy's judgement of the branch (`TreeSetAside.cs:27-30`): an unlanded branch is not removable, and an unmoved one is "a conversation's place". No in-use set names them.
2. **A park's slot is not a choice.** The ledger keys on the record's `tree` and counts a parked session as holding it (`SessionLedger.cs:320-324`; holder read at `:1604-1607` and `:1833`). Another open in that path is refused, whatever a pool records.
3. **`SessionLocks.HoldAsync` takes over only a session's lock** (`SessionLocks.cs:63-65`). A slot locked under any other reason would never be session-locked, and survey §5's lock row does not say so.
4. **The landing and the auto-lander act on the path's current HEAD**; they do not only group by it.
   - `TreeLands` lands whatever branch the tree is on (`TreeLands.cs:1005`, `:1014`).
   - The auto-lander lands an entry's `Tree` after judging the records on that path (`Trees/Landing/AutoLander.cs:209-222`).
   - `EntryLandings.Sequence` lands a second part in the same folder "with" the first (`Trees/SessionTrees/EntryLands.cs:108-121`).
   In a reused folder the first two land another line's work and the third skips a line silently. They are the pool's sharpest hazards, not "A" groupers.
5. **Stale lines.** PICKLOST1 (`098d6813b`) moved `Conversations/Chat/LimitMove.cs`'s tree sites from `:167`, `:171`, `:330`, `:334` to `:174`, `:178`, `:346`, `:350`. Its gates on the tree are at `:75` and `:261`.
6. **A clarification, not an error.** A second opinion's copy is cloned from the registered checkout at the tip its ask kept (`Loop/DriverOpinions.cs:50`, `:62`; `Review/Domain/OpinionGate.cs:96`). A slot reused after the done does not change what the pass reads.

## 1. The pool's shape

### 1.1 The slot

- **What it is.** A slot is a linked worktree of the registered checkout at `<home>/work/pool/<workspace>/<repository>/t-<n>`, where `n` counts from 1.
  - `HomeEntry.Pool = new("pool", HomeCategory.Work, "work/pool/*/*/*", "work/pool/*/*/*")`. It is new since the layout, so its old place is its new one, as `desktop-db`'s is (`DaorisHome.cs:365`).
  - It goes in the three twins, the service's `HomeEntry` and the CLI's `home.ts`, and is held cell for cell.
  - `SystemWiring.Stays` lists it as `resumes` beside `trees` (`Platform/SystemWiring.cs:245`), since a slot's registration names its path.
- **What identifies it.** `(workspace, repository, number)`. Its path is written once, when it is made.
- **Its name is never a branch.** `t-` is no layout's branch prefix. `BranchOfTree` answers null for any path under the pool's place, so no reader can ever spell `daoris/t-2`.
- **How it is made.** The first take that needs a number makes it, as `SessionTrees.OpenAsync` makes a tree today (`:354-427`) but at the slot's path:
  - `worktree add --lock --reason <opening> -b daoris/s-<hex> <slot> <start>`;
  - the `GrownBranch` record;
  - TREEDEPS1's copy;
  - unlock. OPENCUT1's undo covers a cut-off making (`TreeOpenings.UndoAsync`).
- **How it is locked.** A slot is always locked:
  - `daoris session <id>` while a session holds it, which is TREELOCK1's reason, unchanged, so `HolderOf` and the look's let-go keep reading it;
  - `daoris kept tree` while it is free. `SessionLocks.HoldAsync` learns to take over that reason, which today it would refuse (§0.1.3).
  - The look's let-go of an unheld session lock on a slot locks it again under `daoris kept tree`; it never leaves the slot unlocked.
  - A lock under any other reason (a person's, or an opening's whose process lives) takes the slot out of the pool, said, and the slot is never touched.
- **Its neighbours.** The `o-`, `p-` and `r-` folders stay under `trees/<workspace>/<repository>/`, unchanged, and none of their judges meets a slot.
  - The newer build's strays tidy covers the pool's place with today's rule: a folder git lists no tree in and the store names no slot for is moved aside after `Quiet` (`TreeStrays.cs:70`).
  - A slot row whose folder git no longer lists is remade at its next take.

### 1.2 The lease

**A Workplace family in the desktop store.** It follows Part B's rules (foundation §13.3, `docs/2026-10-25-foundation-design.md:1970-1990`):

- every column is required;
- the tables are new, so the writer stamp is unchanged, and builds of z-i and z-iii never open them (as `bindings` and `binding_picks` were added, `Store/DesktopStore.cs:107-129`);
- the migration step goes in `DesktopStore.Steps` after the last one at build time.

| Table | Columns, all required | Key | What it says |
|---|---|---|---|
| `tree_slots` | workspace, repository, number, path, made, state (`ready`, `stuck`, `remade`), said (empty or why) | (workspace, repository, number) | the slots that exist |
| `slot_leases` | workspace, repository, number, kind (`taking`, `letting-go`, `visit`), holder (a quest, request, session or visit id), branch, run (`store_runs` id), taken | (workspace, repository, number) | the one in-flight claim on a slot; deleted when the step ends |
| `branch_parts` | session, workspace, repository, branch, number, at | session | which branch each record worked on, and in which slot; written at its open |
| `branch_ends` | workspace, repository, branch, session, kind (`output`, `no-output`, `kept`, `held`), tip, at | (workspace, repository, branch, at) | each let-go of a line |
| `kept_work` | workspace, repository, branch, ref, base, tree, paths, bytes, at | (workspace, repository, ref) | each kept set of uncommitted work |

**How a lease is taken.** The start takes it: the driven start (`Loop/Driver.cs:2186-2215`), the conversation's start (`ChatRunner.cs:635-651`), or a landing's or review's visit.

1. The repository's start window is held as today: `TreeLock.TryStarting` is shared among starts and exclusive for a replay (`Trees/TreeLock.cs:50-63`).
2. In one `BEGIN IMMEDIATE` transaction the start chooses a slot and inserts its `slot_leases` row. The primary key refuses a second claim on the same slot from any process: the shell, a `daoris-driver --share` loop or a verb.
3. The git work runs (§3.1).
4. The record opens with `tree` = the slot's path.
5. `SessionLocks.HoldAsync` takes the slot.
6. One transaction writes `branch_parts` and deletes the lease.
7. The window is let go.

From then on the slot is held by the record, which the ledger enforces. No long lease mirrors the records, so the two cannot drift.

**How it is let go.** The run's end (`Driver.cs:2886-2894`; the chat runner's end) does the following:

1. It claims `letting-go` and reads the slot's holders.
2. If anything still holds the slot (a park or the others in §3.3), it stops there.
3. Otherwise it judges and keeps (§1.4), writes `branch_ends`, relocks the slot `daoris kept tree`, and deletes the lease.

Nothing in the folder changes at let-go. The reset is the next take's, and that take verifies the keep again.

**Across a crash.** A lease whose `run` is dead (pid gone, or alive under another start time; a start time that cannot be read counts as alive; `Platform/HomeLease.cs:66-68`, `:165-173`) is taken over by the look:

- **A dead `taking` lease.** The look switches the slot's new branch to detached at the same commit and deletes that branch if it holds nothing past its start. The slot is relocked and freed.
  - A partial reset is anyone's only if a ref did not hold it, and the take verified the keep before it started (§3.1 step 3).
- **A dead `letting-go` lease.** The keep is re-run. It is idempotent: an existing `kept_work` row whose tree equals a fresh write of the slot's state is reused.
- **A dead `visit` lease.** The visit made no commit of its own, so the look frees the slot.
- **A crash after the record's open.** Today's orphan sweep ends the record, and the let-go follows at the next look.

The agent's launcher (D201's containment) holds no lease. A lease belongs to a step, never to an agent process.

### 1.3 The branch

- **Name and growth.** A new line's branch is `daoris/s-<8 hex>`, minted at its first take. It grows from what `OpenAsync` grows from today:
  - the line;
  - a chain step's branch (`from`);
  - the chain's landed branch (`landed`);
  - a part's landed branch (`partOf`).
  The `GrownBranch` and its unit are written as at `SessionTrees.cs:380-398`.
- **How a reader learns it.** `TreePool.BranchOf(session)` = `GrownBranch(branch_parts.branch).Now ?? branch_parts.branch`. That is TREEFOLLOW1's truth for *where the work is now*, over the store's truth for *which branch the record took*.
  - For a record in a legacy `s-` tree the folder rule stands.
  - `BranchOfTree` answers null for a pool path.
- **Why not on `GrownBranch`.** `session-branches.json` is rewritten by `RecordList` with its own reader and writer (`SessionBranches.cs:90`). A field an older build does not know would be dropped at its next write, so the new meaning goes where those builds cannot see it (`compatible-builds.md`).
- **Regrowth.** A no-output line whose empty branch the tidy took grows it again from the line's tip, under its own name, when its record goes on. `branch_ends` saying `no-output` is the only licence for this, and no other line ever takes the name.
  - A branch gone with work holds the start, as TREEGONE1 does today (`TreeReturns.cs:70-77`).

### 1.4 The kept work

When a line lets go, `TreePool.KeepAsync`:

1. **Runs the last guards** of `TreeSetAside.LastLookAsync` (`TreeSetAside.cs:70-130`): a link below the trees root, a person's lock, an assume-unchanged or skip-worktree path, a nested repository git would not ignore, or anything unreadable. Any one of them holds the slot for the person, said, and nothing more runs.
2. **Reads** `status --porcelain -z --untracked-files=all --ignore-submodules=none`.
   - Clean means there is nothing to keep.
   - Past 10,000 paths or 64 MB of untracked bytes, the slot is held for the person, said.
3. **Writes the tree through an index of its own** under the slot's git folder: `read-tree HEAD`, `add -A`, `write-tree`. That gives the tree `T`. The real index and the folder are never touched.
4. **Makes `commit-tree T` with no parent**, its dates fixed at the let-go moment and its message naming the branch and `HEAD`. It is stored by a create-only `update-ref` at `refs/daoris/kept/<leaf>/<n>`.
   - **Parentless on purpose**: `for-each-ref --contains <tip>` never lists it. An older build's ancestry proof would otherwise count it as holding the branch (`ContentHolds.cs:379-384`, `AnyRef` at `:432-433` excludes only `refs/daoris/discarded/` and piece refs).
   - `AnyRef` excludes the new prefix too, as a belt.
5. **Writes the `kept_work` row**, with `base` = `HEAD`.

When the line returns to a slot whose tip still equals `base`, the kept tree is laid back as uncommitted changes. The intended command is `restore --source=<commit> --worktree -- :/`, to be proven in row 1b. The ref and the row are dropped once the slot's status equals `T`.

Where the tip moved, nothing is laid back, and the session's opening names the ref.

### 1.5 The types on FOUNDATION1's map

| Type | Subdomain (rank) | Names only |
|---|---|---|
| `HomeEntry.Pool` | Platform (1) | — |
| the migration step's SQL in `DesktopStore.Steps` | Store (2) | — |
| `DriverConfig.KeptTrees`, `WithKeptTrees`, `KeptTreesOf` | Settings (3) | a map of ints; no rule type (§14.3) |
| `ServiceClient.PostLineRecordAsync` | Service (4) | the route's body |
| `NoteCodes` `StartedSlot`, `StartedSlotBack`, `StartedSlotExtra`, `EndedOutput`, `EndedNoOutput`, `EndedKept`, `TreeSlotHeld`, `TreeSlotStuck` | Events (7) | — |
| `TraceLines.BranchOf`, a port (`Func<string, string?>`) | Knowledge (8), declared; Workplace answers it by module initializer, keeping the steward's three bounds (D212's note of 2026-10-26) | — |
| `TreeSlot`, `SlotLease`, `BranchPart`, `BranchEnd`, `KeptWork`, `PoolStore` (through `DesktopStore`), `TreePool`, `SlotChoice` (pure, in `Domain/`), `SlotHolds` (the holders delegate, as `inUse` is today, `BranchTidying.cs:132`) | Workplace (10) | Store, Settings, Service, Events, Agents and below |
| the planner's affinity and the extra tree's word | Work orders (15) | Workplace's `TreeSlot` view |
| the holders: a standing move (Course 14), a served review (Review 16), a helper wait (Participants 12) | each answers `SlotHolds` as a delegate composed by Loop, as `reviewing` is handed to `BranchTidying.InUseAsync` today | — |

`Loop/FreshTree` is Workplace's (§14.2, row 10). Its `remove` becomes the lease's undo.

No pool type names `HelperWaits` (Participants), `ChatRunner`, the planner or the review desk. `BranchTidying`'s own upward edge to `HelperWaits` stays the census exception it is, and the pool's holders arrive through the delegate.

## 2. The nineteen questions

**1. The slot.**
- **Answer:** a slot is a linked worktree at `work/pool/<ws>/<repo>/t-<n>`, identified by (ws, repo, n). Its lease is a store row naming its process run, with git's lock as the outside guard. The start takes the lease, the run's end lets it go, and the look recovers a dead run's claim (§1.1, §1.2).
- **Why:** only the store gives one compare-and-set across the shell and a `--share` loop. The run test is already the claim's (foundation §13.4).
- **Rules out:**
  - *Git's lock alone.* Two starts read "unlocked" together and both switch one slot, because `worktree lock` is not a test-and-set across read, judge and switch.
  - *A lease file beside the slot.* After a crash it names a dead process, and telling dead from paused needs the store's test anyway.
  - *A lease in the service.* The service would learn a machine path and desktop state (D47 §4; foundation §13.7, "The service reading the desktop store").
  - *Slots under `work/trees`.* An older build resumes into a reused folder by path (`TreeReturns.cs:56-58`).

**2. The line and its branch.**
- **Answer:** `daoris/s-<8 hex>` per line, grown as `OpenAsync` grows today. Readers learn it from `TreePool.BranchOf(session)` (§1.3).
  - `BranchOfTree` answers null for slots.
  - Its callers (`TreeRemovals.cs:42`, `:61`; `TreeReturns.cs:61`; `TreeLands.cs:1005`, `:1014`; `TreeUpdates.cs:369`; `TreeWorkReads.cs:157`; `SessionBranchDiscard.cs:69-102`; `WorkUnits.cs:97`; `WorkInventory.cs:409`; `WorkInventories.cs:125`; `SessionGroups.cs:70`, `:118`; `AskWork.cs:195-201`; `Driver.cs:2200`, `:3445`; `WorkflowGraphRun.cs:343-345`; `TreeStrays.cs:183-190`; `TreeFollows.cs:102`, `:132-139`) move to the session's branch wherever they hold a session.
  - `TreeLands.UnitOf`'s folder match (`TreeLands.cs:845-848`) moves too.
  - The page's two sites (`Web/work/WorkFrame.tsx:475-477`; `Web/work/attention/attention.ts:638-641`) read the bridge's `where.branch` first and the folder rule second.
- **Why:** the branch is the only name that survives the folder's reuse and already keys landings, recovery refs and units.
- **Rules out:** *A branch named for the slot* (`daoris/t-2`). Landings by branch (`landings.json`), `refs/daoris/discarded/<branch>`, `GrownBranch` traces and the page's `daoris/<folder>` would merge two lines' histories under one name.

**3. The line's identity.**
- **Answer:** the branch, as above. The path keeps one meaning only, *the folder this record ran in*. Survey §6's readers split in two:
  - by path (kept): the ledger's key, the tidy's in-use set, the heavy slot, the helper and partner desks, the agents' homes, the terminal and acts while the line occupies its slot;
  - by branch (re-keyed): the groupers, the actors in a tree, the review, the record.
- **Where each start runs:**

| Start | Its slot free | Its slot held by another line |
|---|---|---|
| Answer resume or words going on (the same record goes on; ANSWER1a, MSG1b) | its record's slot, switched back to its branch, kept work laid back; the conversation resumes | a new part with the brief, in a free slot (D189 point 3's path) |
| Carry-on (D80), driven hand-over (MIXAGENT1), bring-forward (D178) | a new conversation anyway: its own slot first, else any | any free slot |
| Further part (D167 point 4) | onto the newest part's branch where no live part is on it: its own slot first | otherwise *alongside*: a new branch in a free slot |
| Hand-over, limit move, briefed move (D189 point 2) | the same slot, held through the move by its standing move | not reached: the move holds it |
| Chain step (D82) | a free slot, a new branch from `BranchOf(parent)` | — |
| TREEGONE1 | a slot git no longer lists is remade at its path (§3.5), then the line is taken in | — |

"Any free slot" includes an extra tree.
- **Rules out:** keeping identity by path.
  - `EntryLandings.Sequence` lands only the first of two lines' parts in `t-2`, skipping the second as "lands with it" (`EntryLands.cs:114-117`).
  - The auto-lander closes a due landing as superseded by an unrelated record on `t-2` (`AutoLander.cs:218-221`).
  - A resume goes on in `t-2` while it holds another line's branch.

**4. The agent's conversation.**
- **Answer:** the line goes back into the slot its record ran in when that slot is free. Claude Code then finds `projects/<flattened slot>/<id>.jsonl` in the same account home, and the trust is the slot path's (`Accounts/SignIns/ClaudeTrust.cs:108-125`). Stable paths make an exact resume possible whatever ran in the slot meanwhile.
  - When another line holds the slot, the same record cannot go on: the ledger keys its tree (`SessionLedger.cs:1833`). It goes on as D189 point 3's new part, handed the brief, in a free slot. Its branch, kept work and `from` link come with it, with a new why, `tree`.
  - To keep that rare, the choice (§3.1 step 2) takes a slot whose occupant is finished before one whose occupant may resume, and among the latter the one let go longest ago.
- **The cost to D189:** resume-first holds whenever the line's own slot is free at its return. Otherwise the agent's exact memory gives way to the brief. Row 1i measures how often, by `fromWhy=tree`.
  - D189 point 2 is amended: *"the same agent and account, and its tree free to take it back"*.
  - A side benefit: a slot's trust is granted once per account home and holds for every later session there, where every fresh tree is a new path today (`Driver.cs:2291-2308`).
- **Rules out:**
  - *The line keeps its slot while resumable.* Every D79 wait and every ended conversation would hold a tree for days, and three of them stop every start or force extra trees, which is today's churn.
  - *Copying the agent's files between project folders.* That writes in an agent's home (D189 point 5) and is SWITCHCTX1's unaccepted carry.
  - *A junction per line.* `git worktree remove --force` follows a junction (D51:74-77), and whether the agents and the tree guard read the real path is unverified.

**5. The start.**
- **Answer:** §3.1.
  - Cleared: tracked changes (`reset --hard`) and untracked files that are not ignored (`clean -fd`).
  - Kept: every ignored path, in the first build.
  - The one list of build and dependency folders is TreeScratch's thirteen (`TreeScratch.cs:15-19`) plus `wwwroot`, since an ignored web root is a build's output and today sits in no list. The slim's subset (D195 point 3) and the copy's (TREEDEPS1) become named subsets of it, each with its own proof. A dated note on D195 and D102's TREEKEEP1 says this.
  - The reset clears only the list's complement of ignored paths, and only after the owner's yes (§6.3).
  - A refused reset is a held file (Windows) or a nested repository that the guards would already have refused. It marks the slot `stuck`, with git's words; the take picks another slot, and a later look retries it.
- **Rules out:**
  - *`clean -fdx`.* It loses `node_modules` and `bin/`, which is the point of the pool.
  - *`switch -f` without `clean`.* It leaves the last line's untracked files, which the next agent's `add -A` would commit to the wrong branch.

**6. The end.**
- **Answer:** the end leaves committed work on the branch, as D164 already asks of the agent (D164:9-12). Uncommitted work is kept aside off the branch by Daoris (§1.4); Daoris never commits on the branch.
  - *Done with output*: commits, ready to land.
  - *Done without output*: no commit and nothing uncommitted, so the branch goes at the next tidy.
  - *Kept*: uncommitted work only, said apart.
  Each is a note on the record (§2.15).
- **Rules out:**
  - *Committing leftovers on the branch.* It lands what the agent never chose to commit and turns a no-output end into "output".
  - *Resetting without a keep.* That is a deletion.

**7. What holds a slot.** See §3.3. While every kept tree is in use, nothing waits: the next start gets an extra tree, said in its opening, and the Projects row reads "3 kept · 3 in use · 1 extra".
- **Rules out:** *Releasing a park's slot.* The ledger refuses the next open there anyway (§0.1.2), so the pool's books would lie.

**8. The count.**
- **Answer:** `driver.json`'s `keptTrees: {"<repository>": n}`, from 1 to 16, beside `trees`, which stays the switch (`Settings/DriverConfig.cs:19-22`, `:1384-1391`).
  - Default: the cap + 1 (`DriverConfig.cs:72`; 3 by default). It covers every driven session the machine may run in one repository, plus the person's conversation.
  - Set by `daoris driver trees <repository> keep <n>` (`DriverChoicesCommand.cs:384-400`), `SET_TREES` gaining `kept`, and the Projects row.
  - It moves into the store's `settings` with z-ii, like every key.
- **How it meets the rest:**
  - The cap is unchanged and still counts driven work, never conversations (CAPCOUNT1, `Quests/Domain/Planner.cs:207-209`).
  - A conversation takes a kept tree locked in its name, which closes D51:137's "not covered".
  - A set-up's `atOnce` is unchanged, since set-ups are driven sessions.
  - Once DEV6 builds `laneCap`, the default reads `max(cap, laneCap) + 1`.
  - *A slot per session the repository may run at once* reads as "never keep more than ever ran at once", which lazy making gives for free.
- **Rules out:**
  - *The count in `daoris.json`.* D115 rejected lanes there as the doctrine tool's inert manifest (D115:58-59), and a count is a machine's (disk, cores), as `laneCap` went to `driver.json` (`docs/2026-10-01-self-development-design.md:259`).
  - *The registry.* Slots are machine-local.
  - *The count as a concurrency bound.* The person's conversation would wait for a tree, where today it never does (`ChatRunner.cs:635-651`); see §6.1.

**9. Lazy creation and lowering.** See §3.5 and §3.6.
- **Rules out:**
  - *Removal by age*, which the direction forbids.
  - *`git worktree remove` of a retiring slot.* TREEKEEP1 refuses ignored paths only the slot holds, and moving aside whole is the path the tidy already proves.

**10. The finished record and removal.**
- **Answer:**
  - A pooled line has no tree to set aside. Its end is its branch's deletion by the tidy, as `GoesByItself`'s `Tree is null` (`TreeSetAside.cs:27-30`), after the free slot holding it is detached in place (`switch --detach` at the same commit, which changes no file).
  - The recovery ref is KEEPSIZE1b's, held in the delete's own transaction as today (`ContentHolds.cs:456-492`).
  - The record is posted to a new loopback route, `POST /api/lines/record`, keyed `(workspace, repository, <branch leaf>)` and naming its sessions from `branch_parts`. A 404 keeps it awaiting, as KEEPSIZE1b's 404 does.
  - The manifest's role moves to `branch_ends`.
- **D195's kinds:**
  - `trees` (proof) keeps the legacy `s-` trees;
  - a new `pool` kind, with rule `count`, the person's;
  - `set-aside` (days) gains the retired slots, which carry no manifest and so read `unmarked` and wait the days.
- **The tidy's `GoesByItself`:** the "conversation's place" exception is moot for a pooled line, since a no-output end needs no tree.
- **KEEPSIZE1's 1c–1f:** they stand. 1d's set-aside part shrinks to retired and legacy folders; a kept tree is live and never the allowance's.
- **Rules out:** *Keeping the record per folder.* It would name every session ever run in `t-2` (`Core/Sessions/WorkRecords.cs:446-463`).

**11. Dependencies.** For each npm project after the switch:
- **Answer:**
  - **Fresh**: the slot's `node_modules/.package-lock.json` is not older than `package-lock.json`, which is npm's own staleness rule (D51:58-60); a switch that changed the lockfile rewrote it. The agent is told to skip `npm ci` there.
  - **Stale**, and the checkout's install is at the new lockfile byte for byte: the slot's folder is replaced by TREEDEPS1's copy, with the old one renamed aside and deleted never following.
  - **Stale otherwise**: the agent is told to run `npm ci` there because the lockfile changed.
  - `bin/` and `obj/` are kept, and the agent is told that earlier builds' outputs are kept and builds are incremental.
  - TREEGONE1's remake of a slot is a new slot, so it is copied into.
- **Rules out:**
  - *Copying at every take.* That is 228 MB per take for nothing (D51:85-88).
  - *Saying "skip `npm ci`" after a lockfile change.* That leaves a broken install.

**12. Git's rules and Windows.**
- **A branch checked out in another slot.** A line's branch is checked out in at most one slot, and a free slot still on it is that line's natural way back. A branch checked out in the checkout holds the start, said, as TREEGONE1 does (`TreeReturns.cs:80-85`).
- **The lock reasons:** the four kept, plus `daoris kept tree`.
- **`daoris/` stays on every line.** The kept refs sit outside `--branches` and `--remotes`, and are parentless.
- **The start window** is `TreeLock`, unchanged. The ledger's key is the slot's path.
- **A held file** leaves the slot `stuck`, said and retried. Row 1b measures `reset` and `clean` with a file held open without delete-sharing, as the move aside's `busy` was measured, with git's stdin closed so Git for Windows never asks.
- **Long paths:** `work/pool/<ws>/<repo>/t-2` is shorter than `work/trees/<ws>/<repo>/s-1a2b3c4d`, and every git call keeps `-c core.longpaths=true` (`Trees/WorkingTree.cs:974-981`).
- **Junctions in a kept `node_modules`:** neither `reset` nor `clean -fd` enters an ignored folder. A retired slot leaves by a rename, then the purge's `DeleteNeverFollowing`.
- **Rules out:** *`worktree add --force` or `--ignore-other-worktrees`.* That puts two trees on one branch, which TREEGONE1 rejected (D51:157-158).

**13. D51.** The pool keeps both reasons D51 rejected *one directory switching branches* for (D51:47-48):
- **Nothing that runs at once is serialised.** N kept trees serve N lines at once, and an extra tree serves the next.
- **No person's working state is destroyed.** The registered checkout is never a slot (rules 1 and 5), a slot is switched only between lines and never within one, and only after a ref holds what it held.

The note appended to D51:

> *2026-10-xx, TREEPOOL1:* rule 2's *never reused* is the branch's. The folder is a numbered tree of the repository's kept trees, reused across lines. Rule 4 reads: a tree holds nothing of another line's that git tracks. What it may hold untracked is the kept dependency and build folders, and the sentence that hands it over says which are at its lockfile. The tree is the unit of exclusion still, and the ledger's key.

**14. Records and older builds.** See §4.

**15. The words.** New codes; stored shapes are never changed (`SessionTrees.cs:443-444`).

| Code | Sentence |
|---|---|
| `started.slot` | en *"took tree {slot} of `{repository}` and brought it to `{line}` at {commit}, on a new branch `{branch}`: its files are the line's, and it keeps what earlier work installed and built ({kept}). {stale}"*; zh *"取用了 `{repository}` 的工作树 {slot}，已带到 `{line}`（{commit}），在新分支 `{branch}` 上：文件与主线一致，并保留先前安装和构建的内容（{kept}）。{stale}"* |
| `started.slot-back` | *"went back into tree {slot}, where its branch `{branch}` still was"*, or *"brought `{branch}` back into tree {slot} and laid back the {n} uncommitted path(s) it left"* |
| `started.slot-extra` | *"every kept tree of `{repository}` is in use, so it opened an extra tree {slot}; that one goes when this session lets it go"* |
| `ended.output` | en *"Done: {n} commit(s) on `{branch}`, ready to land. Its tree went back to `{repository}`'s kept trees."*; zh 完成，有产出 |
| `ended.no-output` | en *"Done without output: no commit and nothing uncommitted."*; zh 完成，无产出 |
| `ended.kept` | *"It left {n} path(s) uncommitted, kept aside off its branch and not landed; going on with it lays them back."* |
| `tree.slot-held` | *"tree {slot} stays held: {why}"* |
| `tree.slot-stuck` | *"tree {slot} could not be brought to the line: {git's words}"* |

The glossary's *tree* reads: *"A working tree beside the checkout that a session takes and holds while it works; a repository keeps a number of them."* New terms: *kept trees* (保留的工作树) and *extra tree*. Each sentence is one clause long, within the catalogue's budget.

**16. The install's trees.** See §3.7. None become slots and none go by this work. They drain by today's rules, and only the owner's own press goes faster.

**17. The order.** See §5.

**18. The owner's.** See §6.

**19. Proof.** Each row's proof is listed in §5.

## 3. The lifecycle

### 3.1 The start: brought to the line

Inside `TreeLock.TryStarting` (the conversation's start holds it too, `ChatRunner.cs:636`):

1. **Holders.** `SlotHolds` gives the paths held: `BranchTidying.InUseAsync`'s set, plus standing moves (Course), served reviews (Review) and the helper waits it already reads.
2. **Choice** (`SlotChoice`, pure):
   1. a returning line's own slot, if not held;
   2. otherwise a free slot numbered up to the count whose occupant is detached or finished (landed, no-output, or its branch gone);
   3. then a free slot whose occupant may resume, the one let go longest ago first;
   4. then the lowest unmade number up to the count, made lazily;
   5. then an extra: a free retiring slot first, else a new number above the count.

   The lease is inserted in one transaction.
3. **Judge.**
   - Git must list the slot at its folder; otherwise it is remade (§3.5).
   - Its lock must be `daoris kept tree`, or a session lock of a record that no longer holds it; otherwise the slot is skipped.
   - The last guards must pass (§1.4 step 1).
   - The status must be clean, or must equal the occupant's `kept_work` tree written again now. If the occupant's let-go never kept, the keep is made now. Anything else holds the slot, "changed since its line let it go", and the take tries the next one.
4. **Bring.**
   - `reset --hard --quiet`, then `clean -f -d --quiet`.
   - A new line: `switch --quiet -c <branch> <start>`, and the `GrownBranch` is recorded.
   - A returning line: `switch --quiet <branch or Now>`, its kept work laid back where `base` is the tip, then LANDSYNC2's `StartInSyncAsync` as today (`Driver.cs:2169-2172`).
   - A returning line whose branch is already in the slot runs no git at all.
5. **Dependencies**, as in §2.11.
6. **Open, lock, record** (§1.2).

A start that ends before its open undoes the lease (`FreshTree`). The new branch, still at its start, is deleted; the slot is detached in place and locked `daoris kept tree`.

### 3.2 The end

Let go as in §1.2. The note on the record is one of `ended.output`, `ended.no-output` or `ended.kept` (`ended.kept` alongside `ended.output` where both hold).

- A due auto-landing is judged against the *branch* (§5, row 1d).
- The landing acts in the line's slot if it still occupies one; otherwise it takes a `visit` lease on a free slot, brings the branch in, lands, and lets go.
- A landing's steps are unchanged. Only where its tree comes from changes. LINESTOGETHER1, ACCEPTCONFLICT1 and GITAPP1 change nothing here.

### 3.3 What holds a slot

| Holder | Holds? | For how long | What frees it | What the person sees |
|---|---|---|---|---|
| a start in flight (`taking`) | yes | until its record opens | the open, or its undo; a dead run's claim taken by the look | — |
| a running record | yes, the ledger's key | its run | its end | the rail's tree name (`Web/work/identity.ts:141-144`) |
| a parked record | yes, the ledger's key | until the person answers or ends it | the answer goes on there; *End* lets go | "t-2 held: waits on you" on the Projects row |
| words waiting on an ended record | yes, the in-use set | one look | the go-on | — |
| a helper wait (HELPERWAIT1) | yes | the helper's run, while its quest is taken | the answer, or the quest closing | as today |
| a standing move (hand-over, limit move, briefed move) | yes | the move | the new part's open | as today |
| a served review build (REVIEWENV1d) | yes | until the verdict | the verdict | "t-2 held for your review of …" |
| a visit (a landing, or a review that needs the work) | yes | the act | its end | — |
| uncommitted work that could not be kept aside (a guard, the bounds, or no owner's yes yet) | yes | until the person acts | a session commits it, or the person discards it | `tree.slot-held` with why |
| a `stuck` slot | out of the pool | until a later look clears it | retried each look | `tree.slot-stuck` |
| a D79 wait, an ended conversation, a kept lost take, done (either kind) | no | — | — | when it comes back: its own tree if free, else a new part with the brief, said |

### 3.4 The count

See §2.8. The person sees one control on the Projects row beside the trees switch ("Trees kept: 3"), the Disk space card's `pool` row, and nothing else to set.

### 3.5 Lazy creation

The first take that needs number n makes it (§1.1). A slot git no longer lists is remade at its path the same way (`tree_slots.state = remade`, said `tree.remade`). A folder left there is first moved aside whole, never deleted, as `TreeReturns` does (`:87-92`).

### 3.6 Lowering, and a repository leaving

Lowering the count makes every slot above it *retiring*. A retiring slot:

- takes no new line, except as an extra tree;
- when free, and once its occupant's work is held by a ref (§1.4), is detached, unlocked, moved aside whole into `.tidied/<ws>/<repo>/t-<n>-<moment>`, and dropped from git's record, as `TreeSetAside` moves a tree (`:190-341`). It has no manifest, so it reads `unmarked` and waits the set-aside days, with the slim's hour for its `node_modules`;
- when held, goes once let go;
- when holding work that no ref holds, stays, said.

Raising the count again un-retires a free slot not yet moved. A repository that leaves (unregistered, or trees switched off) retires every slot. A checkout gone keeps every slot, said, because git cannot judge them.

### 3.7 The install's trees today

- **The `s-` trees** under `work/trees` and the folders in `.tidied` keep today's rules: the tidy's move aside, KEEPSIZE1b's hour and days, the press. The pool's readers read them by the folder rule.
- **Lines that began in an `s-` tree** go on there: resumes, parts and words alike. Nothing converts them.
- **The extra cost** is that, while those lines drain, an `s-` tree and a slot can both exist for one repository.
- **Rejected:** adopting idle `s-` trees as slots by `git worktree move`. It breaks the conversation folder of any line still resumable there, and it moves a tree the install holds.

## 4. Records and older builds

| Record | Names a tree path | What the pool writes | How an older build reads it |
|---|---|---|---|
| The service's `sessions.tree` (`Core/Sessions/Sessions.cs:89-116`) | yes | the slot's path, still meaning the tree the record ran in | as no tree of its home (`TreeContext.cs:66-78`). Its removal, tidy, strays and let-go never touch it; its landing refuses. Its resume, carry-on or part grows a fresh `s-` tree from the line or the part's landing, without the line's commits: TREEGONE1's rejected path, here bounded to an older build, nothing lost, said in the note. Its `SESSION_DIFF` reads the slot's HEAD against the record's base (`SessionReviews.cs:132-140`), which can show another line's commits: display only, said. The window is D139's rollback minute, or a person running an older build. |
| `work_records` and `work_record_sessions` | by set-aside folder; sessions by tree path | none for pooled lines; their records go through the new route | 404 from an older service, so the newer driver keeps the record awaiting |
| `session-branches.json` | no | as today; no new field | as today |
| `landings.json` | no | as today | as today |
| auto-landings | the entry's `Tree` | the slot's path; judged by branch from row 1d on | an older lander either refuses the landing (not its tree) or closes it "superseded" by another record in the slot: not landed, which is the safe direction |
| set-aside manifests | yes | none for retired slots | `unmarked`, so the days |
| git's registrations and locks | yes | slots registered; `daoris kept tree` or `daoris session <id>` | its let-go and strays filter by `context.Holds` (`SessionLocks.cs:126-134`); its branch delete refuses a branch checked out in a slot (`ContentHolds.cs:463-471`) |
| the agents' homes | by working directory | the stable slot path | — |
| the opening note | the English sentence only | new codes | the path is cut where it travels (`Core/Sessions/SessionNote.cs:9-17`) |
| the desktop store's new tables | `tree_slots.path` | new tables | builds of z-i and z-iii never open them (foundation §13.3) |
| `driver.json` `keptTrees` | no | new key | kept unread (foundation §13.1: an older build keeps unknown keys); `trees` still opens `s-` trees |

A record an older build writes (an `s-` tree) is read by the pool's build as legacy, by the folder rule.

## 5. The rows, in build order after FOUNDATION1's mod-x

Each row is its own branch. Each runs survey §9's suites whole wherever it touches their part, plus the git half of the real-process suites and the family and deployment rehearsals.

**TREEPOOL1a — the slot named beside today's trees; the census. Nothing is pooled.**
- **Changes:**
  - `HomeEntry.Pool` and its two twins, and `SystemWiring.Stays`;
  - the store migration with the five tables;
  - `TreeSlot`, `SlotLease`, `BranchPart`, `BranchEnd`, `KeptWork` and `PoolStore` in `Workplace/`;
  - `PoolReadersCensusTests`, a by-name source scan in `ConfigSeamTests`' manner. It lists every reader of survey §6, each marked *not yet moved* with the row that moves it, and fails on a new caller of `BranchOfTree`, a new `daoris/{…}` built from a folder, or a new path-equality grouping over records.
- **The person sees:** nothing.
- **Proof:**
  - the store's cases: a build of z-iii never opens the tables, the stamp is unchanged, and a migration interrupted at each step leaves the store as it was;
  - one claim per slot with the shell, a headless loop and a verb racing;
  - a dead run's claim taken over, while a paused live one never is;
  - `DaorisHomeTests` and `home.test.ts` cell for cell;
  - the census passes, and fails on each kind of planted reader;
  - `StructureTests` passes with no new census edge.

**TREEPOOL1b — brought to the line, let go and kept aside, over git, unwired.**
- **Changes:**
  - `TreePool.TakeAsync`, `LetGoAsync`, `KeepAsync` and `ReturnAsync`, and `SlotChoice`;
  - the one build-folder list, with a dated note on D195 and TREEKEEP1;
  - `AnyRef` excluding `refs/daoris/kept/`;
  - `SessionLocks.HoldAsync` taking over `daoris kept tree`.
- **The person sees:** nothing.
- **Proof** (`TreePoolGitTests`, `TreePoolStopsGitTests`):
  - **The brief §8's first proof:** line A commits on its branch, leaves an untracked file and a changed tracked file, and installs an ignored `node_modules` and `bin/`. Line B takes the slot at the line. B's tree holds the line's files and A's ignored folders, and none of A's tracked or untracked work. A's branch holds its commits; A's untracked file and change are in `refs/daoris/kept/…`. A coming back gets them laid back.
  - **No work lost:** a slot is never reset under each of
    - a person's lock;
    - a link (a junction on Windows);
    - an assume-unchanged path;
    - a nested repository;
    - a dirty submodule;
    - untracked bytes past the bound;
    - a write after the keep, between the take's judgement and its reset (planted through the seam, as `BeforeRefDeleted` is).
  - **The keep commit:** it is parentless, and `for-each-ref --contains <tip>` does not list it.
  - **A stop** at every step leaves a slot the next take judges correctly.
  - **On Windows**, a file held open without delete-sharing leaves the slot `stuck`, measured, with nothing of A's lost.
  - **Each case is seen failing first**, with its guard taken out.

**TREEPOOL1c — a session's branch, read by session (reader family 1).**
- **Changes:**
  - `branch_parts` written at every record's open in a tree, legacy included;
  - `TreePool.BranchOf`;
  - `BranchOfTree` null for the pool's place;
  - its callers in §2.2 and `TreeLands.UnitOf`;
  - the bridge's `where.branch`, and the page's two sites reading it.
- **The person sees:** nothing; every legacy answer is identical.
- **Proof:**
  - for each reader, a fixture whose record names a slot now holding another branch answers that record's own branch;
  - the legacy fixture answers as before;
  - `identity.test.ts` and `noteLines.test.ts` pass;
  - the census shrinks by those readers.

**TREEPOOL1d — the groupers, by line (family 2).** `EntryLandings.Sequence`; the auto-lander's supersede and its landing in the line's slot only; `ChatRuns` and `WorkflowRun.Read`; the trace's same and other, through Knowledge's port; `ContentHolds`' opened moment taken from the lease for a slot (`ContentHolds.cs:573-574`); the line record's route and client.
- **Proof:** survey §7's path-keyed tests retold with two lines in one slot, each a new case beside the old:
  - `EntryLandingsTests`: both lines land;
  - `AutoLandingRulesTests` and `AutoLandingTriesTests`: no supersede across lines, and a landing refused when the slot's HEAD is another branch;
  - `WorkRecordStoreTests`: explicit sessions;
  - `TraceTests` and `WorkflowRunTests`.

**TREEPOOL1e — the readers that act in a tree (family 3).**
- **Changes:**
  - `SESSION_DIFF` reads the slot only while the line occupies it; otherwise it reads the branch in the checkout plus the kept ref.
  - The landing's tree comes from the line's slot, else from a visit.
  - The terminal, the acts and the folder are offered only while the line occupies its slot (`WorkFrame.tsx:1309`; `acts.ts:102`, `:166`).
  - *Show it again* brings the line back.
  - TREEGONE1 for slots.
  - The strays and the opening cut-off for the pool's place.
  - `RemoveAsync` refuses a slot.
- **Proof:** a fixture per reader. `SessionReviewsTests`, the landing tests and `TreeReturnsTests` each gain a slot case.

**TREEPOOL1f — the pool switched on per repository, off by default.**
- **Changes:**
  - `keptTrees`, its verb, its route and its control;
  - the driven start and the chat runner take from the pool;
  - the end lets go;
  - `SlotHolds` composed by Loop;
  - the look's recovery;
  - the words in en and zh;
  - extra trees;
  - the Disk space `pool` kind;
  - the planner: a held `into` tree is not `RepositoryBusy` for a pooled repository (`Planner.cs:1355-1398`).
- **The person sees:** only where a count is set, which is only in tests in this row: the opening's and the end's sentences, and the Projects control.
- **Proof:**
  - the brief §8's first proof as a tick test on a stand-in harness that records its working folder (as TREEGONE1's ticks in `SessionMessagesTickTests` do);
  - each holder in §3.3 holding as the table says;
  - an extra tree made and moved aside;
  - Process cases in `CapCountTickTests` and `LookParallelTests` with the pool on;
  - the family rehearsal with one pooled repository.

**TREEPOOL1g — lines coming back through the pool.**
- **Changes:** every start in §2.3's table goes through the pool; D189's, D167's and D178's notes.
- **Proof:**
  - a resume in its free slot finds its conversation: the stand-in sees the same folder and id;
  - a resume whose slot is held goes on as a new part with the brief and `fromWhy=tree`;
  - words going on;
  - a further part on its branch and alongside;
  - a limit move kept in its slot;
  - a bring-forward;
  - a no-output conversation's go-on regrowing its branch;
  - `HandOverQuestTickTests` and `StartInSyncTickTests` pass.

**TREEPOOL1h — lowering and leaving.**
- **Proof:**
  - a lowered count retiring a free slot (moved aside, `unmarked`, days) and a busy one (it goes at let-go);
  - one holding unkept work stays;
  - `KeepSizeGitTests`, `CleanupLeftoversTests` and `SystemSpaceTests` pass.

**TREEPOOL1i — on for the install's repository, measured.**
- **When:** after the owner's yes (§6.2, §6.4).
- **Measured on the install:**
  - the home's `work/` and `.tidied` bytes before and after a day, from the Disk space card's kinds;
  - the time from a start's record to its first turn over twenty starts before and after;
  - the count of `fromWhy=tree`.

**TREEPOOL1j — the default flips.**
- **What:** a repository with trees and no count keeps the cap + 1 trees.
- **When:** after a week of 1i with no finding.

**TREEPOOL1k — scratch cleared at a take.**
- **When:** only on the owner's yes (§6.3).

**How each input folds in:**
- **FOUNDATION1:** after mod-x; a store family by §13.3; ports by delegate, or by module initializer with the three bounds.
- **D51, D88, D102, D164, D167, D178, D189 and D195:** a dated note each, in the row that changes them.
- **SWITCHCTX1:** once its carry is accepted, it could carry a conversation across slots too, a row of its own. Until then the brief does.
- **D115:** the default reads `laneCap` once DEV6 builds it.
- **LINESTOGETHER1, ACCEPTCONFLICT1 and REVIEWFLOW1:** unchanged. A head merging others does so in its own slot.

## 6. What is the owner's

1. **The count's meaning** (a reading of the direction).
   - **What it is:** the count bounds the trees a repository *keeps*, not the sessions it runs. A start beyond it gets an extra tree that is moved aside when let go.
   - **What yes does:** sessions never wait for a tree.
   - **What no does:** the count bounds sessions at once, and the person's conversations wait too.
   - **Where to look:** §2.8 and §3.4.
2. **Daoris keeping a let-go line's uncommitted work off its branch**, then resetting the tree for another line. This amends D164 and resets a tree that held work.
   - **What it is:** a parentless commit on `refs/daoris/kept/…`, made again and compared before any reset, and laid back when the line returns.
   - **What yes does:** such trees go back to the pool.
   - **What no does:** a tree left with uncommitted work stays held by its line, said, until a session commits it or the person discards it.
   - **Where to look:** §1.4 and row 1b's proof.
3. **Ignored files outside the build list cleared at a take** (`_fixtures/`, `local/`, logs).
   - **What it is:** KEEPSIZE1b's decision that a finished session's scratch goes with its tree (D195:503-505), read for a line that let go of a tree.
   - **What yes does:** row 1k clears them.
   - **What no does:** every ignored path is kept across lines, as the first build already does.
   - **Where to look:** §2.5.
4. **The pool on for the install's repository** (row 1i).
   - **What it is:** the first reset of the install's own trees, after item 2's yes.
   - **What yes does:** sets `keptTrees` for it.
   - **Where to look:** §5, row 1i.

Nothing in this design removes, converts or resets a tree, branch or folder the install holds today (§3.7), so no question is asked about them. Lowering a count is the person's own act, and its retired trees wait D195's days. The default's flip (row 1j) carries out the owner's direction and is the steward's call, told to the owner.
