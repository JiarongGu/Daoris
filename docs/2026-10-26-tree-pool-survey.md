# Session trees today: how a tree is opened, held, kept and removed — a survey

> TREEPOOL1, phase 1 of a design under D191 (2026-10-26). This is the factual ground for the design's proposals. The direction is the
> row TREEPOOL1 in `TASKS.md:312`. Everything was read from the code at `6b999d6b1` (`main` when the reading began), and every line number
> is that commit's. Nothing was run. One reader read it all. A fact inferred rather than seen says *unverified*. `Dnn` is
> `docs/decisions/Dnn.md`. **Paths:** a path with no prefix is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`. `Tests/` is
> `src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/`. `Modules/` is `src/Daoris.Desktop/Daoris.Desktop.Modules/`. `Core/`, `Http/` and
> `Svc.Tests/` are `src/Daoris.Service/Daoris.Service.{Core,Http,Tests}/`. `Web/` is `src/Daoris.Web/src/`. A home's places are written as
> placeholders (`<home>/work/trees/<workspace>/<repository>/<name>`).
>
> **E and A in the tables.** **E** marks a difference that is essential: the holder or reader needs what it has, such as a clone's
> private refs, the ledger's one-session-per-tree lock, or an agent's own state kept by working directory. **A** marks a difference that
> is an accident of the fact that a tree is opened once, for one line of work, and never reused. Examples: a branch read off a folder's
> name, or a record found by its tree's path. The survey proposes nothing. §8 names the tangles the design must settle.

## 0. In one page

**What a session tree is.** It is a linked git worktree of a repository's registered checkout, made by `git worktree add --lock -b`. It
sits at `<home>/work/trees/<workspace>/<repository>/s-<8 hex>` on its own branch `daoris/s-<8 hex>`, grown from the repository's line
(`Trees/SessionTrees/SessionTrees.cs:273-428`, the name minted at `:354-356`). Trees are opt-in per repository: `driver.json`'s `trees`
list is empty by default (`Settings/DriverConfig.cs:19-22`, `:72-75`, `:1383-1391`). A repository without trees runs one driven session
at a time in its checkout (`Quests/Domain/Planner.cs:1399-1429`).

**Its life today.**

1. A first start opens a fresh tree. It copies in the checkout's npm `node_modules` where the lockfile matches byte for byte
   (TREEDEPS1). It records where the branch started, then opens the session record with the tree's path as the ledger's lock key.
2. While the session works, the tree is held four ways:
   - git's own lock, `daoris session <id>` (TREELOCK1);
   - the ledger's one-session-per-tree rule (D51);
   - the start window's `TreeLock`;
   - the tree guard on file writes.
3. **At the end Daoris commits nothing.** The tree stays whole: *nothing merges itself and nothing deletes itself*
   (`Loop/Driver.cs:2886-2888`).
4. The tree is taken away in one of two ways:
   - **The look's own tidy** runs at most every five minutes. It takes a tree once its branch moved and the line holds its work. It
     renames the folder into `.tidied/`, keeps a recovery ref and a manifest, and then deletes the branch.
   - **The person's press** deletes it at once with `git worktree remove`.
5. **What happens to a moved-aside folder** (KEEPSIZE1b, built):
   - A finished tree whose record the service kept is purged an hour after its record.
   - Every other moved-aside folder waits its days: fourteen by default, 1 to 90 settable, or `whole` (`Platform/KeepKinds.cs:17-20`).
6. **A tree whose branch never moved stays until the person's press.** This is the *done with no output* case
   (`Trees/SessionTrees/TreeSetAside.cs:27-30`).

**Who goes back into an existing tree, by its path.** These starts all reopen a tree that already exists:

- a resume (D79, D131);
- a carry-on (D80);
- the person's words going on (D137);
- a further part of an entry (D167 point 4);
- a hand-over or limit move of a conversation (D189 point 2);
- a lost take brought forward (D178);
- TREEGONE1, which remakes the tree at the same path on the same branch.

**Who makes a tree of its own beside the session trees.** All of these live under the same `trees/<workspace>/<repository>/` folder:

- a chain's next step: a fresh tree from the step before's branch (D82, CHAIN2);
- a reviewer or helper: an `o-` clone;
- a partner: a `p-` clone;
- a replay: an `r-` detached worktree.

**Rooms, not trees.** Intake, Ask Daoris and brainstorms run in rooms under `<home>/work/rooms/...`.

**The cap.** One number for the whole machine: `driver.json`'s `cap`, default 2 (`DriverConfig.cs:15`, `:72`). It is spent by driven
sessions, intakes, helpers, reviewers and partners' pieces, never by the person's conversations (CAPCOUNT1, `Planner.cs:195-220`,
`:669-670`).

- No per-repository count exists anywhere.
- D115's `laneCap` is decided but not built (DEV6 is open, `TASKS.md:531`).
- A repository with trees has no bound of its own (PAR1, `Planner.cs:1353-1398`).
- A repository's builds queue on its one heavy-run slot (`Sessions/Heavy/HeavySlot.cs:111-131`).

**The price TREEPOOL1 is about**, as measured by this repository's records:

| Cost | Measured |
|---|---|
| `npm ci` in a fresh tree | 170–221 s (D51:52-53) |
| Copying a 228 MB `node_modules` instead | 5.9 s (D51:85-88) |
| A moved-aside tree of this repository | about 205 MB (`docs/2026-10-24-keep-size-design.md:78`) |
| Moved-aside trees, before KEEPSIZE1b | grew about 27 GB a day (`:74`), mostly `bin/` (`:57`) |
| Six live trees | 4.1 GB (`:59`) |

**The sites, by what they are about:**

| § | Group | Its sharpest fact |
|---|---|---|
| 1 | A session tree's life | the folder's name *is* the branch's name, and its creation time *is* the session's start |
| 2 | Every other holder | eight kinds of start go back into a tree by its path, and the agent's own conversation is filed by that path |
| 3 | The cap | one machine-wide number; no per-repository count; conversations are uncounted and their trees unlocked |
| 4 | Setup cost | only npm dependencies are copied; build outputs are paid per tree; the tidy has three lists of "build folders" |
| 5 | Git | no `reset` or `clean` anywhere; a checked-out branch is never deleted or added elsewhere; `daoris/` is excluded from every proof |
| 6 | Records naming a tree path | about forty readers key on a path or derive a branch from a folder name |
| 7 | Tests | about 60 test files turn trees on; a handful pin *never reused* and *fourteen days* outright |

**Prior maps this extends rather than repeats:**

- the working-surface design's nine rules (`docs/2026-09-21-working-surface-design.md:70-107`, D51's mechanism);
- the keep-size design's §1, today's move-aside and its holds (`docs/2026-10-24-keep-size-design.md:82-140`);
- the switch-context design §1.5, where each agent keeps its conversation (`docs/2026-10-24-switch-context-design.md:184-198`);
- FOUNDATION1's map, which places all of `Trees/` in the Workplace subdomain (`docs/2026-10-25-foundation-design.md:2342`, row mod-x;
  D212:173).

## 1. A session tree's life today

### 1.1 Opened

**Where.** A driven start opens one in `Loop/Driver.cs:2186-2215` and calls `SessionTrees.OpenAsync` (`Trees/SessionTrees/SessionTrees.cs:273-428`).

- **Who opens one.** Only where `config.OpensOwnTree(quest.To)` holds (`Driver.cs:1916`), and only when the start is not going back into
  an existing tree (`:2187`).
- **Checks before the add.** The root must be the top of its own working tree (`SessionTrees.cs:277-297`). The line comes from
  `CanonicalLine` (`:299-306`), and `StartOfAsync` resolves it to the local branch, else `origin/<line>`, else a refusal (`:585-601`).
- **Where the branch grows from:**
  - the line;
  - a chain step's branch (`from`, CHAIN2, `:308-333`);
  - the branch the chain landed on (`landed`, LAND2c, `:315-328`);
  - a further part's landed branch (`partOf`, SESSONE1d, `:334-352`).
- **The add.** `git worktree add --lock --reason "Daoris is opening this tree (process <pid>)" -b daoris/s-<8 hex> <path> <start>`
  (`:371-373`, the reason at `Trees/SessionTrees/TreeOpenings.cs:15-25`). The opening lock is OPENCUT1's.
- **Recorded at once.** Where the branch started goes to `session-branches.json` as a `GrownBranch`, with a new branch work unit
  (FABRIC-END2, `:380-398`; `Trees/Branches/SessionBranches.cs:8-20`).
- **Dependencies copied.** `TreeDependencies.CopyAsync` runs while the tree is still opening-locked (`:400-402`), then the tree is
  unlocked (`:404-409`).
- **A failure or cancel** undoes everything. `git worktree remove --force --force`, a delete of the folder the open made, and
  `git branch -D` all run on no token (`:411-420`, `TreeOpenings.cs:76-109`).
- **Then:** `RefMoves.Mine` (`:422-423`).

**The note.** The "opened a session tree at {path} on `{branch}`, from {basedOn}" sentence is written at `SessionTrees.cs:439-468`:

- `started.tree` says *a fresh tree holds nothing git does not track: no installed dependencies, no build outputs* (`:453-459`).
- `started.tree-copied` names the copied `node_modules` and says *the rest of the repository's own setup cost, build outputs included,
  is paid here* (`:445-452`).
- A stored shape is never changed in place (`:443-444`).
- The page's words are at `Web/locales/en/note.json:63`, `:65`.
- The note goes on the record while the session runs (`Driver.cs:2704-2711`). It is cut of its path wherever it leaves the machine
  (`Core/Sessions/SessionNote.cs:9-17`).

**What is copied in.** Only npm projects, judged one at a time (`Trees/SessionTrees/TreeDependencies.cs:11-31`, `:85-136`):

- The lockfile must be tracked, and no `pnpm-lock.yaml` or `yarn.lock` may sit beside it.
- The tree's `package-lock.json` must equal the checkout's byte for byte.
- The checkout's hidden `.package-lock.json` must not be older than the lockfile.
- The folder must be ignored in both the tree and the checkout.

The copy is a real one, never a link. It goes under `.daoris-copying-<rand>` and is renamed into place once whole (`:160-199`). Top-level
caches (`.cache`, `.vite`) are left behind (`:44-45`). The agent is told to skip `npm ci` unless a lockfile changes
(`TreeOpened.Dependencies`, `SessionTrees.cs:19-23`; D51:81-83).

Nothing else is copied: no `bin/`, `obj/`, `dist/`, `wwwroot/`, and no untracked local configuration (working-surface rule 4,
`docs/2026-09-21-working-surface-design.md:86-90`).

**Where it lives.**

- The trees root is `HomeEntry.Trees`: `work/trees/*/*/*` now, `trees/*/*/*` at the layout before D172 (`Platform/DaorisHome.cs:405`).
- Both places are read, and new trees open at the new one (TREEPLACE1, `Trees/SessionTrees/TreeContext.cs:54-86`).
- A tree's workspace and repository are read back from its path's segments (`TreeContext.cs:157-162`). A path under neither place is
  "not a tree this home opened" (`:66-78`).

**The name.** It is minted, not the session id, because the order is tree → record → spawn (`SessionTrees.cs:87-89`). The branch is
"named for the session, never reused" (working-surface rule 2, `docs/2026-09-21-working-surface-design.md:77-82`).

### 1.2 Held

| Hold | What it holds | Where | Kind |
|---|---|---|---|
| `FreshTree` | the opened tree, from opening until the record's open is asked; let go by the clean removal on every refused or held start | `Loop/FreshTree.cs:3-48`; `Driver.cs:1297-1301`, `:2209`, `:2225`, `:2248`, `:2261`, `:2269`, `:2345-2446`, `:2475`, `:2487-2489` | E |
| `TreeLock` | a file per repository under `<home>/run/locks/trees/`: starts share it, a replay holds it alone, by Windows file sharing | `Trees/TreeLock.cs:3-80`; `Driver.cs:2104`, `:2174-2179`, `:2454-2455`, `:2507-2508` | E |
| The ledger | the record's `tree` is the lock key, one active (parked included) session per tree, and a go-on refused while another holds it | `Core/Sessions/SessionLedger.cs:320-328`, `:1597-1607`, `:1831-1837`; `Core/Sessions/Sessions.cs:89-100`, `:1129-1158` | E |
| git's lock (TREELOCK1) | `git worktree lock --reason "daoris session <id>"`, taken after the record opens and let go at the run's end unless the record still holds the tree (parked, setup change, helper wait) | `Trees/SessionTrees/SessionLocks.cs:6-94`; `Driver.cs:2498-2504`, `:2894`, `:2988-2990`; `Loop/DriverContinuations.cs:301-307` | E |
| The look's let-go | a session lock whose tree no running or waiting session names is unlocked before the tidy | `SessionLocks.cs:96-123` | E |
| The tree guard | a PreToolUse hook refusing file writes outside the session's tree | `Trees/TreeGuard.cs:3-50` | E |
| The trust check | Claude Code's trust is keyed by the tree's path in the account's `.claude.json` `projects`; a start in an untrusted path is held where the connector's allowance would not reach | `Accounts/SignIns/ClaudeTrust.cs:108-120`; `Driver.cs:2287-2308` | A |

A conversation's own tree is **not** git-locked (D51:137-138). The record names the tree, and the page shows its last segment
(`Web/work/identity.ts:132-144`).

### 1.3 Ended

A run ends with the tree whole (`Driver.cs:2886-2894`). Daoris makes no commit; a session commits on its branch as it goes (D164).

- **Uncommitted work at the end stays in the tree.** Every door refuses to destroy it:
  - landing refuses (`Trees/SessionTrees/TreeLands.cs:221-227`, `:943-948`);
  - unforced removal refuses (`Trees/SessionTrees/TreeRemovals.cs:88-100`);
  - the tidy keeps the tree (`Trees/SessionTrees/TreeSweeps.cs:353`).
- **A done under an auto-accept rule is due**, and a later look lands it (`Driver.cs:2881-2884`; `Settings/LandingKey.cs:30-36`).
  - A done with no commits is `AutoLandingCode.Nothing` (`Trees/Landing/AutoLander.cs:356-363`; `Trees/Landing/AutoLanding.cs:49`).
  - It reads *"Its quest was done and its work made no commits, so nothing was landed."* (`Web/locales/en/note.json:133`).

### 1.4 Moved aside, and deleted (the look's own tidy)

**When.** A pass runs at most every five minutes (`Trees/Branches/BranchTidying.cs:30`). `GoesByItself` takes a session branch only
when all of these hold (`TreeSetAside.cs:15-30`):

- the branch is removable: empty, landed or carried;
- it is read;
- if landed, its work is on the line;
- if a tree is present, the tree is this home's and its branch moved since it grew (`Worked`, `TreeSweeps.cs:371`).

**A tree whose branch never moved is "a conversation's place"** (D137). It is the press's, whatever its kind (`TreeSetAside.cs:18-20`).
So a session that ended with no commits leaves its tree until the person presses.

**What keeps a tree:**

- "In use": active sessions, records with words waiting, helper waits, and a review step's served folder (`BranchTidying.cs:135-150`).
- Uncommitted or untracked paths.
- The last guards: a person's lock, a link, assume-unchanged, a nested repository (`TreeSetAside.cs:52-70`).

**What does not keep a tree.** Ignored paths only the tree holds do not keep it from the look. By the owner's decision, a finished
session's scratch goes with its tree (KEEPSIZE1b, `TreeSweeps.cs:354-357`).

**The steps**, from `TreeSweeps.cs:158-301` and `TreeSetAside.cs:190-341`:

1. The exact tip is held on `refs/daoris/discarded/<branch>` wherever the line's own branch does not hold it
   (`TreeSweeps.cs:191-213`; `ContentHolds.cs:102-106`).
2. A schema-2 manifest is written beside the destination (`FinishedTree`, `TreeSweeps.cs:220-230`). A manifest that cannot be written
   keeps the tree.
3. The folder is renamed to `<trees root>/.tidied/<workspace>/<repository>/<name>-<UTC moment>` in one same-volume rename
   (`TreeSetAside.cs:32-39`, `:313-341`). A refused rename is `busy`.
4. What the tree holds is read again in the moved folder, and anything changed puts the folder back (`:243-249`).
5. `git worktree remove <path>`, whose folder is now gone, drops git's record alone (`:252-270`).
6. The branch is deleted at the commit judged (`TreeSweeps.cs:274`).

**Deleted later** (`SetAsideEligibility.cs:3-115`; `TreeSetAside.cs:452-512`):

- `recorded` (branch deleted, record posted to the service's `POST /api/trees/record`, `Http/Program.cs:1439`): an hour after the record,
  on a holding check run right before its rename into `.tidied/.going`.
- `awaiting` its record: never.
- `stopped`, `refused` or `unmarked` (no manifest: a stray, a pass's leftover, an older build's folder): the set-aside days, fourteen by
  default (`KeepKinds.cs:17-20`; `KeepPolicy`, `:96-134`).
- The **slim** drops a moved folder's package folders (`node_modules` beside a tracked `package.json`) an hour after the move
  (`KeepKinds.cs:11-23`). `bin/` and `obj/` wait the folder's days (`:14`).

The row's "deleted fourteen days later" is today's rule for every folder *not* marked `recorded`. A finished tree whose record was kept
goes an hour after it.

### 1.5 Removed by a press or a landing (no move aside)

**The person's clean-up** (D88), from the Branches tab or `daoris-driver trees clean`, deletes the tree:

- links first (TREELINK1), then `git worktree remove`;
- where something holds the folder open, git's record is let go of all the same (`TreeSweeps.cs:245-270`).

**A landing under a rule with `Tidy`** removes the tree and its branch right after landing, behind the same proof, never forced
(`TreeLands.cs:800-838`). Otherwise the landing leaves the tree: *"The tree is still there; discard it when you are done with it."*
(`:315`).

**`RemoveAsync`** (`TreeRemovals.cs:14-200`) serves the press, the landing's tidy and `FreshTree`. Its refusals:

- anything outside the trees home;
- a tree a live session names;
- uncommitted work;
- ignored paths only the tree holds;
- unlanded commits.

A content-held branch gets its recovery ref first.

**Decisions cited by this code:**

| Decision | Subject |
|---|---|
| D51 and its notes | TREEDEPS1 (D51:50-103), TREELINK1 (:105-122), TREELOCK1 (:124-142), TREEGONE1 (:143-159) |
| D88 | AUTOTIDY1/2a/2b/2c, TREEFOLLOW1, TIDIEDLINK1, STRAYOTHERS1 |
| D102 | landing branches, SQUASHTIDY1c |
| D195 | CLEANUP1, KEEP1a–g, KEEPSIZE1a/b |
| D164 | commits as it goes |
| D167 | the entry is what a tree belongs to |
| D178 | a lost take's tree kept |
| D79, D80, D82, D131, D137, D189 | going back into a tree |

## 2. Every other holder of a tree

| Holder | Opens its own, reuses, or reads another's | What it assumes of the tree | Where | Kind |
|---|---|---|---|---|
| A resume (D79), an answer (D131), a carry-on (D80) | **reuses** the record's `Tree` where it is this home's; ensured first under `TreeLock`; brought in sync with the line (LANDSYNC2) | the tree holds the session's work; same path, same branch; the agent's conversation resumes there | `Driver.cs:2086-2131`, `:2161-2172`; `Trees/SessionTrees/TreeReturns.cs:26-114` | A (path as the line's identity) |
| Words going on in an ended record (MSG1b, D137) | **reuses** the record's tree; the ledger refuses while another session holds that path | one record per harness conversation | `Loop/DriverContinuations.cs:240-310`; `SessionLedger.cs:1795-1837`; `Conversations/Chat/ChatGoOn.cs:662` | A |
| A further part of an entry (SESSONE1d, D167 point 4) | **reuses** the entry's newest part's tree when nothing live holds it, else opens its own; a tree gone grows from the entry's landed branch (`partOf`) | the tree belongs to the entry | `Planner.cs:1105-1157`; `Driver.cs:2100`, `:2136-2138`, `:2202-2207`; D167:26-30, :73-82 | A |
| A conversation's hand-over, limit move or briefed move (D189 point 2) | **reuses** `prior.Tree` (a new part, same tree); refused when the tree is gone | the same tree keeps the agent's folder the same on every account home | `Conversations/Chat/ChatHandOver.cs:158-160`; `Conversations/Chat/LimitMove.cs:167`, `:171`, `:330`, `:334`; `Conversations/Chat/BriefedMove.cs:137`, `:235`; `ChatRunner.cs:630-633`; D189:18-22 | E (the conversation's folder) |
| A driven hand-over (MIXAGENT1) | **reuses**: a carry-on from the handed record's tree | as a carry-on | `Driver.cs:1919-1925`, `:2129-2135` | A |
| A new conversation | **opens its own** where asked per conversation or by the repository's choice; not git-locked; counts under no cap | a fresh tree, removed cleanly if the record is refused | `Conversations/Chat/ChatRunner.cs:402-404`, `:626-671`; D51:137 | A |
| A chain's next step (D82, CHAIN2) | **opens its own** from the step before's branch, read from that step's tree (`BranchNowAsync`, else `daoris/<folder>`) | the parent tree's folder name is its branch, or TREEFOLLOW1 learned it | `Driver.cs:2191-2207`; `SessionTrees.cs:308-333` | A |
| A lost take kept, then brought forward (D178) | **keeps** the stood-down session's tree, branch and transcript; *Bring mine forward* carries on in it, synced to the step's source | the tree is kept indefinitely until the person drops or brings it forward | D178:8, :25-28, :36-39, :222-225; `Driver.cs:2165-2172` | E |
| A landing (Accept, `LAND_SESSION_TREE`, `trees land`, the look's auto-landing) | **acts in** the session's tree: reads HEAD, refuses uncommitted work, replays aside, moves the tree to what landed (LANDSYNC1), makes the branch in the checkout or merges into the line in the checkout | the tree is on its session branch (or one its agent made, TREEFOLLOW1) | `TreeLands.cs:408-839`, `:913-1142`; `AutoLander.cs:200-222`, `:350-380` | E |
| The auto-lander's judgement | **reads** every record on the entry's tree path: a live one waits, a newer one supersedes the due landing | one line of work per path | `AutoLander.cs:209-222` | A |
| A review environment (REVIEWENV1) | **reads** a set-up step's tree: checks its procedure before the record, then serves a folder of it to the tab until the verdict; *Show it again* finds the newest set-up's session's tree; the tidy keeps a served tree | the build folder stays as the session left it (that it is an ignored output is *unverified*) | `Driver.cs:2256-2271`; `Review/ReviewDesk.cs:97-130`, `:140-173`, `:176-190`, `:226-228`; `Review/ReviewServed.cs:17-40`; `BranchTidying.cs:148` | E |
| A session's review (`SESSION_DIFF`) | **reads** the tree its record names, else the landed branch (REVIEW2) | the path still holds that session's work | `Trees/GitReads/SessionReviews.cs:3-60` | A |
| A second opinion (D155) | **its own clone**, `o-<8 hex>`, `--no-hardlinks`, no remote, detached at the candidate's tip read from the session's tree; removed at the pass's end | a clone, never a worktree, so no ref is shared | `Review/OpinionTree.cs:6-112`; `Loop/DriverOpinions.cs:62` | E |
| A helper (MIXAGENT2a, COAGENT1a) | **reads** the asker's tree (head and captured work in progress, no index writes); its own `o-` clone with the work laid over | the asker's tree is the asker's | `Loop/DriverHelpers.cs:860-900`, `:502-513`; `Conversations/Helpers/HelperCapture.cs:6-46` | E |
| A partner (COAGENT1b) | **its own clone**, `p-<piece>`, at the ask's base; files read back by the keeper, never git; reads the lead's tree's checkout | the lead's tree is the lead's | `Trees/Pieces/PieceKeeper.cs:216-248`; `Conversations/Domain/PartnerInstruction.cs:81-88`; `Loop/DriverPartners.cs:227`, `:299` | E |
| A replay (WSR6, LANDSYNC1) | **its own** detached locked worktree `r-<8 hex>`, removed after; switches the session tree's branch in place where it replays a tree's own branch | the session tree may be switched and rebased by Daoris | `Trees/SessionTrees/BranchReplays.cs:13-14`, `:281-299`, `:105`, `:187`, `:224`; `Trees/SessionTrees/TreeLandSyncs.cs:192-196`; `Trees/SessionTrees/TreeUpdates.cs:294-352` | E |
| Intake | **a room** `<home>/work/rooms/intake/<workspace>`, which the record keeps as its tree | — | `DaorisHome.cs:408`; `Loop/DriverIntakes.cs:345` | E |
| Ask Daoris / help | **a room** `<home>/work/rooms/help(-*)`, one run per workspace | — | `DaorisHome.cs:406-407`; `ChatRunner.cs:909`; `SessionLedger.cs:1830-1833` | E |
| A brainstorm or meeting | **a room** `<home>/work/brainstorm-rooms/*` | — | `DaorisHome.cs:415`; `Loop/DriverBrainstorms.cs:731` | E |
| A quest from another repository | a driven start like any other; the asking session ends and is resumed in its own tree (D79) | as a resume | `Driver.cs:2086-2093`, `:2140-2152` | A |
| Another machine's records | none: a mirrored record's `tree` is null, and the wire has no tree field | — | `Core/Sessions/Sessions.cs:89-93`; `SessionNote.cs:10-13` | E |
| A steward's start | runs only in a tree this home opened, else it is held for the owner | — | `Driver.cs:2219-2228`; `Modules/Bridge/StewardActRows.cs:231-232` | E |
| A heavy run | runs in whichever checkout (tree) asks; the slot is the repository's (its common git folder) | — | `Sessions/Heavy/HeavySlot.cs:9-27`, `:99-131`; `Sessions/Heavy/HeavyRun.cs:33-60` | E |

**TREEGONE1.** A start never runs with its tree gone. A tree git no longer lists, whose branch stands, is remade at the same path with
`git worktree add <path> <branch>`. A folder left there is moved aside first. The remade tree gets no dependency copy: *"its
dependencies are installed again there"* (`TreeReturns.cs:44-110`; D51:143-159).

**TREEFOLLOW1.** An agent may move its tree onto a branch of its own. Each look learns it onto the `GrownBranch`'s `Now`
(`Trees/SessionTrees/TreeFollows.cs:5-12`, `:86-126`). So a tree's branch can already differ from its folder's name, and the code
reads `Now` before falling back to `daoris/<folder>`.

## 3. The cap

| Fact | Where |
|---|---|
| `Cap`: concurrent sessions across all repositories, default 2, at least 1; `driver.json`'s `cap` | `DriverConfig.cs:15`, `:32`, `:72`, `:546-553`, `:961`, `:1415` |
| Set by `daoris driver cap <n>`, the service's settings tool, and the bridge's driver choices | `Settings/DriverChoicesCommand.cs:436`, `:644`; `src/Daoris.Service/Daoris.Service.Mcp/ChosenSettingsTools.cs:61`; `Modules/Bridge/Routes/DriverRoutes.cs:518` |
| What spends it: a driven session, an intake, a helper, a reviewer, a partner's reserved piece; **never the person's conversation** (CAPCOUNT1) | `Planner.cs:195-220`, `:252-258`, `:669-670` |
| Spent at: the planner, the look's passes, helpers, brainstorms, meetings, partners, intakes | `Planner.cs:670`, `:1431-1445`; `Driver.cs:1041`, `:1071`; `Loop/DriverHelpers.cs:130`; `Loop/DriverBrainstorms.cs:110`, `:512`; `Loop/DriverMeetings.cs:75`; `Loop/DriverPartners.cs:195`; `Loop/DriverIntakes.cs:104` |
| A workspace's set-ups: `atOnce`, default 1, held to `cap − 1` | `Setup/Domain/WorkspaceSetupRules.cs:10`, `:21-22`; `Setup/WorkspaceSetup.cs:10-15`, `:68-75` |
| A repository with trees: no per-repository bound; only the tree a resume, carry-on or part goes back into is one at a time (PAR1) | `Planner.cs:1353-1398` |
| A repository without trees: one session at a time, its root its one tree | `Planner.cs:1399-1429`; D51:15-21 |
| Lanes: `laneCap` (default 3) per repository, decided in D115 point 4 | D115:21-26; **not built**: no code reads it (search), DEV6 open (`TASKS.md:531`) |
| A repository's heavy-run slot: one per common git folder, builds serialised | `HeavySlot.cs:111-131`; `HeavyRun.cs:40-42` |
| **A slot count** per repository: none, in `daoris.json` (the repository's), `driver.json` (Daoris's) or the registry | `daoris.json`; `DriverConfig.cs:29-39` |

*Slot* already names three things in the code: the cap's places (`Planner.cs:670`), the heavy-run slot (`HeavySlot`), and the set-ups'
*never the last slot* (`WorkspaceSetup.cs:14`).

## 4. Setup cost, and how ignored folders are treated

**What a fresh tree pays.**

- Everything its repository's setup makes, except npm `node_modules` copied at a matching lockfile (§1.1).
- .NET `bin/` and `obj/`, the web build's `wwwroot/`, the CLI's `dist/`, and other build caches are made again per tree.
- In this repository, the ignored outputs are those `.gitignore` names: `node_modules/`, `src/Daoris.Cli/dist/`, the .NET trees'
  `bin/` and `obj/`, `src/Daoris.Service/Daoris.Service.Http/wwwroot/`, `publish/`, `_fixtures/`, `local/`, `*.log` (`.gitignore:3-54`).

**Measured.**

| Measurement | Value | Where |
|---|---|---|
| `npm ci` per tree | 170–221 s | D51:52-53 |
| Warm `npm ci` | 70 s | D51:85-88 |
| The copy | 5.9 s | D51:85-88 |
| Six live trees | 4.1 GB: `node_modules` 55%, an Angular build cache 26%, `bin/` 5%, `obj/` 3% | keep-size design `:59` |
| Moved-aside bytes | `bin/` 58% | keep-size design `:57` |

**Three lists decide what a "build folder" is, and they differ.**

| List | What it says | Where | Kind |
|---|---|---|---|
| `TreeScratch.BuildFolders` | `node_modules`, `bin`, `obj`, `dist`, `build`, `out`, `.angular`, `.next`, `coverage`, `target`, `__pycache__`, `.venv`, `.gradle`. An ignored path under one goes with its tree. Under any other shared ignored folder (`wwwroot` among them), files written since the tree opened are the tree's own (TREEKEEP1). | `Trees/SessionTrees/TreeScratch.cs:9-58` | A |
| KEEP1a's slim | only `node_modules` beside a tracked `package.json` goes early | `KeepKinds.cs:11-20` | A |
| TREEDEPS1 | only npm projects' `node_modules` are copied in | `TreeDependencies.cs:85-136` | A |

**What a removal would take** (`ContentHolds.HoldsAsync`, `Trees/SessionTrees/ContentHolds.cs:550-610`). It runs `git status` with
`--untracked-files=all --ignored=matching`, then sorts what it finds:

- An ignored path the checkout also holds is "shared" and goes with the tree.
- One only the tree holds keeps the tree.
- The tree's "opened" moment is **the folder's creation time** (`:573-574`).

**The move aside and the census.**

- The look moves a tree's folder whole, ignored folders and all (`TreeSetAside.cs:190-235`).
- The **Disk space** card counts live trees as the kind `trees`, rule `proof` (removed only by the clean-up's proof), and moved-aside
  folders as `set-aside`, rule `days` (`Platform/SystemSpace.cs:27-41`, `:64-97`).
- A home move counts trees among what "resumes" (`Platform/SystemWiring.cs:245`).
- KEEPSIZE1's allowance is not built (rows 1c to 1f open, `TASKS.md:383`).

## 5. Git facts the pool must meet

| Fact | Where | Kind |
|---|---|---|
| Trees are linked worktrees (`worktree add --lock -b`); opinions and partners are clones; replays are detached locked worktrees | §1.1, §2 | E |
| Every git Daoris runs says `-c core.longpaths=true` on the command line, never in the repository's configuration; a tree's prefix is longer than its root's | `Trees/WorkingTree.cs:974-981`; `SessionTrees.cs:365-368`; `OpinionTree.cs:88-92` | E |
| Branch names: `daoris/s-<8 hex>`, the folder `s-<8 hex>`; the landing's branch by the rule's pattern (`quest`, `session`, `ticket`, `slug`, `repository`) | `SessionTrees.cs:354-356`; `LandingKey.cs:77-84` | — |
| `daoris/*` is excluded from every "branch of the person's" proof (`--exclude=daoris/*`) | `TreeFollows.cs:15-19`, `:61-68` | E |
| **No `git reset` and no `git clean`** in the driver's tree code (search of the driver's git arguments); bringing a tree up to date or following a landing uses `switch --detach`, `switch <branch>` and `rebase --onto` in place | `BranchReplays.cs:105`, `:163-224`; `TreeLandSyncs.cs:192-196`; `TreeUpdates.cs:294-352`, `:460-462` | — |
| A branch is deleted only in an `update-ref --stdin` transaction at the judged commit, and **never while a worktree has it checked out**; a late checkout puts it back | `ContentHolds.cs:440-492` | E |
| `worktree add <path> <branch>` refuses a branch checked out in another standing tree; TREEGONE1 rejected `--force` for that reason | `TreeReturns.cs:80-85`; D51:157-158 | E |
| `git branch -D` only in an undone open and the PAUSE1d discard | `TreeOpenings.cs:108`; `Trees/SessionTrees/OnlyHereProof.cs:126` | — |
| **Locks.** Four reasons coexist: an opening's (`Daoris is opening this tree (process n)`), a session's (`daoris session <id>`), a pass's (`Daoris is using this for one pass (process n)`), and a person's (never touched). The look's let-go and the strays read them. | `TreeOpenings.cs:15-63`; `SessionLocks.cs:22-35`; `PassLeftovers.cs:29-43`; `Trees/SessionTrees/TreeStrays.cs:177-200` | E |
| `git worktree prune` is never run over every stale registration; only one tree's record is removed (`worktree remove --force --force <path>`); the one `prune` is PAUSE1d's, for a stale registration that holds a branch | `TreeOpenings.cs:102-106`; `TreeReturns.cs:94-98`; `OnlyHereProof.cs:119-124` | — |
| After landing: the landing's branch is recorded (D102) and goes once its work reads on the line; the session branch goes with a `Tidy` rule at once, else by the look once the line holds it, after its tree is moved aside | `TreeLands.cs:800-838`; `TreeSetAside.cs:23-30`; `TreeSweeps.cs:274` | — |
| Recovery refs `refs/daoris/discarded/<branch>`: kept 30 days where a branch of the person's holds the commit, else until dropped. Piece refs `refs/daoris/pieces/<id>` are never moved. | `ContentHolds.cs:102-106`; `Trees/SessionTrees/RecoveryRetention.cs:22`; `PieceKeeper.cs:6-19` | E |
| The branch's start is recorded at open (`GrownBranch.From`); "worked" means the tip moved from it | `SessionTrees.cs:380-398`; `TreeSweeps.cs:371` | A |

**Windows.**

| Fact | Where |
|---|---|
| A rename is refused while a handle without delete-sharing is open: `busy`, tried at a later look (a writer that allows deletion lets it through) | `TreeSetAside.cs:196-197`, `:313-333`; keep-size design §1.1 |
| `git worktree remove --force` follows a junction out of a tree, so links are taken off first (TREELINK1); npm links a workspace as a junction | D51:73-79, :105-118 |
| Read-only object files are cleared before a recursive delete | `TreeOpenings.cs:85-99`; `OpinionTree.cs:22-25` |
| A virus scanner holding a just-written file: renames are retried | `TreeDependencies.cs:182-186` |
| A session's processes run in a job that ends with it (ORPHAN1, after a session's dev servers outlived it and held its tree) | `Platform/ProcessJob.cs:6-32` |
| A heavy run ends its job and runs builds with `MSBUILDDISABLENODEREUSE=1` before letting the slot go | `HeavyRun.cs:47-51` |

What a reset or clean of a reused tree meets while a review's serving, a lingering build node or a scanner holds a file is *unverified*:
no such operation exists to have been measured.

## 6. Records that name a tree path, or derive a branch from one

**Stored.**

| Record | Keyed how | Where | Kind |
|---|---|---|---|
| The service's `sessions.tree` | the path, normalised; the lock key; parked sessions hold it | `Core/Sessions/Sessions.cs:89-100`, `:519`, `:1129-1197` | E |
| `work_records`, `work_record_sessions` (KEEPSIZE1b) | by the set-aside folder `<tree>-<moment>`; its sessions are **every session row naming the tree's path, made before the move**; commits attributed by bases | `Core/Sessions/WorkRecords.cs:30-48`, `:446-463`, `:465-480` | A |
| Helper, partner and opinion desks | refuse their clone being the asker's or lead's tree, compared by path | `Core/Sessions/HelperDesk.cs:89-94`; `Core/Sessions/PartnerDesk.cs:69-74`; `Core/Review/OpinionDesk.cs:113-119`, `:336` | E |
| `session-branches.json` (`GrownBranch`) | by repository and branch; the unit found by matching a folder to `daoris/<folder>` | `SessionBranches.cs:8-20`; `TreeLands.cs:841-848`; `TreeContext.cs:80-86` | A |
| `landings.json` (`LandedBranch`) | by session; no tree path found (search, *unverified* beyond it) | `Trees/LandedBranches.cs` | — |
| Auto-landings | the entry's `Tree`; a newer record on the same path supersedes | `AutoLander.cs:209-222` | A |
| Set-aside manifests | the tree's path, branch and tip, beside the moved folder | `TreeSweeps.cs:220-230`; `TreeSetAside.cs:343-367` | A |
| The heavy slot's holder | the checkout (tree) it runs in | `HeavySlot.cs:9-27` | E |
| The desktop store | bindings and picks by session; no path | `Store/DesktopStore.cs:107-129` | — |
| git's own registrations | `<common dir>/worktrees/<name>` and its `locked` file | `SessionLocks.cs:37-54` | E |
| The agents' homes | Claude Code files a conversation as `projects/<working directory, flattened>/<id>.jsonl`, and its resume reads it there; its trust is in `.claude.json` `projects` by path; Codex files by date | `Platform/ToolHomeLeftovers.cs:24-55`; `docs/2026-10-24-switch-context-design.md:184-198`; `ClaudeTrust.cs:108-120` | E |
| The opening note | the English sentence holds the path; codes hold branch and base | `SessionTrees.cs:439-468`; `SessionNote.cs:9-40` | — |

**Read in the driver, by path or by folder name.**

| Reader | What it assumes | Where | Kind |
|---|---|---|---|
| `TreeFollows.BranchOfTree`: the branch is `daoris/` + the folder's name | the folder *is* the branch | `TreeFollows.cs:148-161` | A |
| Its callers, and copies that build `daoris/<folder>` themselves | as above | `TreeRemovals.cs:42`, `:61`; `TreeReturns.cs:61`; `TreeLands.cs:1005`, `:1014`; `TreeUpdates.cs:369`; `Trees/SessionTrees/TreeWorkReads.cs:157`; `Trees/Branches/SessionBranchDiscard.cs:69-102`; `Trees/Inventory/WorkUnits.cs:97`; `Trees/Inventory/WorkInventory.cs:409`; `Trees/Inventory/WorkInventories.cs:125`; `Trees/SessionGroups.cs:70`, `:118`; `Sessions/Domain/AskWork.cs:195-201`; `Driver.cs:2200`, `:3445`; `Workflows/Domain/WorkflowGraphRun.cs:343-345`; `TreeStrays.cs:183-190`; `TreeFollows.cs:102`, `:132-139` | A |
| `Planner.SameTree`: one session per tree, parts and resumes claim trees | a path is one line's place | `Planner.cs:1120`, `:1141`, `:1358`, `:1380`, `:1390`, `:1586-1591`; `Sessions/RunningSessions.cs:129-145` | E |
| `EntryLandings.Sequence`: parts sharing a tree land once, with the first | sharing a path means sharing work | `Trees/SessionTrees/EntryLands.cs:104-121` | A |
| A chat run's line and its taken quest: records of one entry on one path | a path is one line | `Workflows/ChatRuns.cs:66-91`; `Workflows/WorkflowRun.Read.cs:204-220` | A |
| The trace: whether a session ran in the *same* tree as the record before it; a tree on the wire is its folder's name | path equality means continuity | `Sessions/Domain/Trace.Chain.cs:169-187` | A |
| The tidy's in-use set and waiting trees | a path named by a live or waiting record is busy | `BranchTidying.cs:135-150`; `Service/ServiceClient.cs:2222-2249` | E |
| `ContentHolds.HoldsAsync`: the tree "opened" at its folder's creation | the folder was made for this session | `ContentHolds.cs:573-574` | A |
| The context brief reads the tree a carry-on works in | — | `Driver.cs:2560-2570` | E |

**Read on the page.**

| Reader | Where | Kind |
|---|---|---|
| The branch built as `` `daoris/${last segment}` `` | `Web/work/WorkFrame.tsx:475-477`; `Web/work/attention/attention.ts:639-640` | A |
| *"the last segment … is the branch the tree was cut for"* | `Web/work/identity.ts:132-144`, `:186-192` | A |
| The terminal opens in the session's tree | `Web/work/WorkFrame.tsx:1309` | E |
| Acts offered on a tree | `Web/work/acts.ts:102`, `:166` | E |
| The trace's tree and "same/other" | `Web/work/head/HowItCameToBe.tsx:427-438` | A |
| Earlier parts' trees | `Web/work/conversation/EarlierParts.tsx:56` | A |
| The repository's trees toggle | `Web/projects/ProjectsView.tsx:313-316`; `Modules/Bridge/Routes/DriverRoutes.cs:71` (`SET_TREES`) | E |

## 7. Tests that pin today's behaviour

| What they pin | Tests |
|---|---|
| A fresh tree per session, never sharing a path or branch (*"a name is never reused (D51 rule 2)"*); a fresh tree holds nothing git does not track; dependencies copied at a matching lockfile; an open cut off leaves nothing | `Tests/Trees/SessionTreesTests.cs:51-87`, `:289-430`, `:451-470`; `Tests/Trees/TreeDependenciesTests.cs:79-181` |
| The opening's sentences and codes | `Tests/NoteSitesTests.cs:245`, `:270`, `:290`; `Svc.Tests/Remote/SessionSyncTests.cs:185-229`; `Svc.Tests/Sessions/NotePartsTests.cs:142`; `Web/work/noteLines.test.ts:1121-1127` |
| Locks and holds | `Tests/Trees/SessionLocksTests.cs:42-201`; `Tests/Loop/SessionTreeLockTickTests.cs:40`; `Tests/Loop/StartTreeCloseTests.cs:58` |
| The same tree remade at the same path | `Tests/Trees/TreeReturnsTests.cs:16-80` |
| The planner's tree rules and the cap | `Tests/Quests/PlannerTests.cs:306-388`, `:794-866`, `:1215`, `:1572`, `:2024` |
| Fourteen days, an hour, moved aside (the sentence *"goes once it is left untouched for 14 days"*) | `Tests/Platform/KeepingTests.cs:29-40`; `Tests/Trees/BranchTidyingFolderTests.cs:73`, `:162`, `:230`, `:380`, `:468-470`; `Tests/Trees/BranchTidyingTests.cs:48`, `:208`, `:298`, `:349`, `:378`; `Tests/Trees/CleanupLeftoversTests.cs:186`, `:243-250`; `Tests/Platform/SystemSpaceTests.cs:346`; `Tests/Help/Proposals/HelpSettingProposalsTests.cs:33`; `Tests/Trees/KeepSizeGitTests.cs`, `KeepSizeStopsGitTests.cs`, `KeepingSlimTests.cs:130-133` |
| Path-keyed readers | `Svc.Tests/Sessions/WorkRecordStoreTests.cs:93`, `:113` (two sessions in one tree attributed by bases); `Tests/Trees/EntryLandingsTests.cs:20`, `:42` (parts sharing a tree); `Tests/Trees/AutoLandingTriesTests.cs:344` and `AutoLandingRulesTests.cs:426`, `:506`, `:644` (superseded by a newer session on the tree); `Web/work/identity.test.ts:286-302` |

**Tests that run with trees on.** About sixty test files turn trees on: 49 in code (`Trees = [...]`) and 16 through `driver.json`'s
`trees` (search). Among them are the tick tests that start real sessions in fresh trees: `Tests/Loop/LookParallelTests.cs`,
`CapCountTickTests.cs`, `StartInSyncTickTests.cs`, `SessionMessagesTickTests.cs` (TREEGONE1's three ticks) and `HandOverQuestTickTests.cs`.

## 8. The tangles a design must settle

1. **The folder's name is the branch's name.**
   - D51 rule 2 named the branch for the session and never reused it.
   - The layout then made the folder `s-<x>` and the branch `daoris/s-<x>`.
   - About twenty driver sites, two page sites and the trace derive one from the other (§6).
   - TREEFOLLOW1 already lets a tree's branch differ, learned per look onto `GrownBranch.Now`. But every fallback is still
     `daoris/<folder>`, and `TreeStrays` treats an `s-` folder on another branch as someone's work.
   - A slot reused across sessions breaks that identity everywhere at once.

2. **A path is today one line of work's identity.** Several records and decisions assume it:
   - D167 point 4 makes the entry what a tree belongs to.
   - D189 point 2 resumes the agent's conversation where *"the same agent, account and tree"* hold.
   - Claude Code files the conversation by working directory, and keeps trust by path.
   - Resumes, carry-ons, words going on, further parts, hand-overs, limit moves, brought-forward takes and TREEGONE1 all go back by
     path (§2).

   The row's *"the branch checked out in any free tree"* moves a line to a new directory. What that does to a resumable conversation,
   and to D189's rule, is unsettled.

3. **A path will hold many unrelated sessions over time.** Many readers group or judge by path:
   - KEEPSIZE1b's record names every session that ran on the path;
   - the auto-lander supersedes on the newest record on the path;
   - an entry's Accept lands parts sharing a tree once;
   - chat runs and taken quests;
   - the trace's *same tree*;
   - a session's review reads the tree its record names;
   - the waiting and in-use sets;
   - TREEKEEP1's "opened" moment is the folder's creation.

   Each assumed a path used by one line.

4. **What holds a tree with no process running.** These keep a tree today, some for days:
   - a parked record (the ledger counts it active);
   - a record with words waiting;
   - a helper wait;
   - a set-up step's served build until the person's verdict;
   - D178's kept lost take;
   - a D79 wait;
   - a tree whose branch never moved (*done with no output*, kept for the press).

   A pool sized to *sessions at once* must say whether each holds a slot.

5. **The count.**
   - Only a machine-wide cap exists, and it is not per repository.
   - The person's conversations are uncounted and their trees unlocked (D51:137).
   - Repositories without trees run one at a time.
   - D115's `laneCap` is unbuilt.
   - *Slot* already names the cap's places, the heavy-run slot and the set-ups' rule.
   - Where the count is declared (the repository's `daoris.json`, `driver.json`, the registry) and its default have no precedent.

6. **What a session's end must leave, and what "brought to the line" clears.**
   - Daoris never commits. Uncommitted work keeps a tree at every door today.
   - The row's reset keeps "ignored dependency and build folders" and clears untracked work. Three lists of build folders disagree
     (§4): `wwwroot` is in none, and scratch like `_fixtures/` or `publish/` is ignored too.
   - KEEPSIZE1b's owner decision lets a finished session's scratch go with its tree. Nothing in the code resets or cleans a tree
     today (§5).

7. **Removal and the finished record.**
   - Today's end of a tree is the move aside, which is also where KEEPSIZE1b writes the manifest, holds the tip and later composes the
     record per tree.
   - The pool removes trees only when slots are lowered or the repository leaves. The record, the recovery ref and the branch's
     deletion would need a moment other than the move.
   - D195's kinds (`trees` by proof, `set-aside` by days) and KEEPSIZE1's unbuilt allowance assume trees end.

8. **Git's own rules.**
   - A branch checked out in a tree cannot be deleted by Daoris's transaction, nor added in another tree.
   - The landing, the tidy and LANDSYNC1 expect the tree on its branch.
   - Four lock reasons coexist and the look interprets them.
   - `daoris/` must keep its prefix for the proofs.
   - The start window (`TreeLock`) and the ledger's lock key are per repository and per path.
   - `GrownBranch` and its unit are written inside `OpenAsync`, so a branch made without opening a tree records nothing.

9. **Windows.** A reused tree is reset or switched while things may hold its files:
   - a served review folder;
   - a lingering build node;
   - a scanner;
   - a junction inside a copied `node_modules` (TREELINK1);
   - long paths.

   A rename refused is `busy` today. What a refused reset does is unmeasured.

10. **Dependencies kept across sessions.**
    - TREEDEPS1 copies only at open, gated on the lockfile matching the checkout's, and tells the agent to skip `npm ci`.
    - TREEGONE1's remake copies nothing.
    - A kept `node_modules` or `bin/` from a previous session at another lockfile, and what the agent is told, have no rule today.

11. **The words.** `started.tree` and `started.tree-copied` say *a fresh tree*, and a stored shape is never changed in place. The
    working-surface rule 4 says the price is *"stated in the sentence that creates the tree"*. *Done without output* shows only the
    landing's `nothing` note today, while its tree waits for the person's press.

12. **Neighbours under the same folder.**
    - `o-`, `p-` and `r-` folders live beside `s-` trees under `trees/<workspace>/<repository>/`.
    - The strays tidy moves aside any folder git lists no tree in.
    - A slot's folder name must be one these judges recognise.

13. **D51's rejected alternative.** D51 rejected *"one directory switching branches"* because *"it serialises exactly what needs to run
    at once and destroys the person's working state on every switch"* (D51:47-48). It also stated the clean-tree rule's point: the
    person's work in flight must never hold the driver. A pool switches branches between sessions, not within one. The design must say
    how it stands against that rejection, and against working-surface rule 2's *never reused*.

14. **Proof.** The tests in §7 pin *never share a path*, *nothing git does not track*, *fourteen days* and the path-keyed readers. The
    row's own proof (a second session takes the first's tree at the line with its dependencies kept and none of its work, and the
    install's disk measured before and after) has no fixture yet.
