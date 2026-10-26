# A repository's session trees kept and reused, each session's work a branch (TREEPOOL1, 2026-10-26: round 1, proposed)

**Standing (round 1, proposed under D191, D214):** the synthesis of the two sealed proposals, Codex's
(`docs/2026-10-26-tree-pool-proposal-codex.md`, *X*) and the independent Opus reader's (`docs/2026-10-26-tree-pool-proposal-opus.md`, *O*),
each written from the brief (`docs/2026-10-26-tree-pool-brief.md`) and the survey (`docs/2026-10-26-tree-pool-survey.md`) alone and sealed
before its author read the other. It is written by Claude Opus 5.5, the voice the brief's §2 point 2 gives Claude, which wrote neither
proposal. Where the two agree it is said once; where they differ the choice names whose it follows, why, and the case the other gets wrong
(§1.1). **One choice is neither proposal's, and both makers are asked to weigh it first:** both keep a slot at a stable path of its own and
move a line into it; this round moves the slot to the line's own path instead (§2, §1.1 row 1), because at the code a stable slot path either
reaches a shipped build as the line's own tree while it holds another line's work (O's, §0.2 row 5) or needs a second session family in the
service to hide it (X's). The design changes a foundation in the senses D191's amendment of 2026-10-20 names (decided rules other designs rest
on: D51 rule 2, D167 point 4's tree, D189 point 2's tree, D195's kinds, D88's tidy, D178's kept take), and it switches and removes trees, so
**both makers read this round before any of it is accepted** (D191's note of 2026-10-24; the brief's §2 point 4), and no part that deletes a
person's work or a record is accepted on one maker's read. **Nothing is built before FOUNDATION1's row mod-x** (the Workplace move, D212
§14.6), which has not landed: D212's notes record rows mod-i to mod-vi built and none after. The rows marked ⚖ (§17) are built only after
both makers' reads find nothing blocking in them. Nothing here is built, spends an account, signs in, publishes or deletes.

**Read at** main `f8fbb1bb7`. The survey read the code at `6b999d6b1` and both proposals at `a3f5be21a`. Between `6b999d6b1` and `f8fbb1bb7`
the driver's source changed only in files neither cites (FOUNDATION1's mod-vi moved the instance pipe's files under `Instance/` and added the
subdomains' lease files; SESSPROC1 changed `Sessions/SessionProcesses.cs`) and in `Conversations/Chat/LimitMove.cs` and `ChatGoOn.cs`
(PICKLOST1, the second below every line cited); `Trees/`, `Loop/`, `Quests/`, the service and the page are byte for byte as the survey read
them. So every line the survey and the proposals cite holds at `f8fbb1bb7` but those §0.2 names, and every line this round adds was read at
`f8fbb1bb7` with `git show`. Paths follow the survey's aliases: a bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `Tests/`,
`Core/`, `Http/`, `Svc.Tests/`, `Web/` and `Modules/` as the survey sets them. `Dnn` is `docs/decisions/Dnn.md`; *X-n* is Codex's answer to
the brief's question n and *O-n* the Opus reader's; *X §n* and *O §n* a section of each. A home's places are written `<home>/...`. Read for
it: the brief, the survey and both proposals whole; D51, D88, D164, D167, D178, D189 and D195 at the points this cites; FOUNDATION1's design
§13 (the desktop store) and §14 (the module map; `Trees/` is Workplace, row mod-x) and D212's note of 2026-10-26 on module-initializer seams;
`.claude/knowledge/compatible-builds.md`; the working surface's rules (`docs/2026-09-21-working-surface-design.md:70-107`) and SWITCHCTX1
§1.5 (`docs/2026-10-24-switch-context-design.md:184-198`).

## 0. What this rests on

### 0.1 The direction

The owner's direction of 2026-10-25, as the brief §1 carries it: each repository keeps a **pool of trees**. A session takes a free tree and
holds it, locked, while it works; at its start the tree is brought to the line (reset to the line's head, its untracked work cleared, its
ignored dependency and build folders kept); the session works on a branch of its own; at its end the tree goes back to the pool, its work on
that branch ready to land, or with no branch where it was done with no output, *done with output* and *done without output* both ends a
person sees. The pool is a managed number of slots, set by the repository's configuration or Daoris's, at most one per session the repository
may run at once; a slot's tree is made lazily; trees are removed only when that number is lowered or the repository leaves, never by age.
TREEPOOL1's row adds that a go-on or a review that needs the work again has the branch checked out in any free tree (`TASKS.md:312`). In
D193's picture it is the workspace and equipment a staff member is handed (`docs/2026-10-21-company-concept.md` §2, part 14): set up once,
kept, and handed to the next member clean.

**What both proposals share, and this design keeps without argument.** The work's identity is never reused, the equipment is (X's opening;
O §0 item 2). A session's branch is `daoris/s-…`, minted once and never handed to another line. The registered checkout is never a slot. A
slot is a linked worktree of the checkout, made by today's open the first time it is needed. The start window stays `TreeLock` and the
ledger's one session per tree stays the unit of exclusion. The claim on a slot is a desktop-store row by FOUNDATION1 §13's rules, taken in one
`BEGIN IMMEDIATE` transaction and passed on only from a dead process run by `HomeLease`'s test. git's own lock stays the guard against outside
tools, with a new reason that is neither a session's nor an opening's. No `clean -fdx`, no forced checkout, no `worktree add --force`. A slot
holding work no ref holds is never reset. A parked record holds its tree. Nothing of the install's trees is adopted, converted or removed by
this work. Each amended decision takes a dated note. The pool is off by default until its proofs pass, then on for the repository whose owner
opts in.

### 0.2 Corrections to the survey and the proposals, checked

| Claim | At `f8fbb1bb7` | Who is right |
|---|---|---|
| 1. Survey §8.4 (and the brief's *what holds a slot*): seven things hold a tree with no process | Four are holds the code keeps by path: a parked record (the ledger's key, `Core/Sessions/SessionLedger.cs:320-324`, `:1604-1607`, `:1833`), words waiting, a helper wait and a served review (the tidy's in-use set, `Trees/Branches/BranchTidying.cs:140-150`). A D79 wait, a kept lost take and a tree whose branch never moved are kept only by the tidy's judgement of the branch (`Trees/SessionTrees/TreeSetAside.cs:27-30`): an unlanded branch is not removable, and an unmoved one is a conversation's place | O |
| 2. Survey §5's lock row: four reasons coexist, which the look interprets | True, and `SessionLocks.HoldAsync` reads any reason that is not a session's as the tree already held and takes no lock (`Trees/SessionTrees/SessionLocks.cs:60-65`), so a tree locked under a new reason is never session-locked until it learns that reason | O |
| 3. Survey §2 and §6 mark the landing and the auto-lander among the readers that only group by path (A) | They act on what the path holds: a landing lands the branch the tree's HEAD is on (`Trees/SessionTrees/TreeLands.cs:208-218`, `:930-941`, `:1005`, `:1014`); the auto-lander closes a due landing *gone* where its tree is gone (`Trees/Landing/AutoLander.cs:209`) and *superseded* by a newer record on the same path (`:212-222`) | O; §3 keeps every path one line's so neither meets another's work |
| 4. Survey §2's limit-move lines `LimitMove.cs:167`, `:171`, `:330`, `:334` | PICKLOST1 moved them to `:174`, `:178`, `:346`, `:350`; the gates on a tree are `:75` and `:261` | O |
| 5. O §0 item 1 and §4: *a path outside its trees home is one an older build refuses to remove, land, lock or tidy*, and an older build's *resume, carry-on or part grows a fresh `s-` tree* | The refusals hold for removal, landing and locks (`TreeLands.cs:918-921`; `TreeRemovals.cs`, survey §1.5; `SessionLocks.cs:126-134`; `Loop/DriverContinuations.cs:303`). **The start does not refuse.** A resume's tree goes through TREEGONE1's check only where the home holds it (`Loop/Driver.cs:2099`); otherwise `StandsNow` answers `Directory.Exists` (`:2127-2128`), so a resume goes on in the folder whatever branch it then holds (`:2129-2131`), and a further part the same way (`:2100`, `:2138`); words going on run in the record's tree, judged by the disk *as for a tree this home did not open* (`DriverContinuations.cs:171-178`, `:227`); a conversation's go-on and moves ask only whether the folder exists (`Conversations/ConversationTrees.cs:11`; `Conversations/Chat/ChatRunner.cs:630-633`) | X, in kind: a reused stable path reaches a shipped build as the line's own tree |
| 6. X-3: the planner starts a further part alongside *despite D167's original waiting rule* | D167's amendment of 2026-10-14 decided *run alongside* (`D167.md:73-82`); the planner keeps a decided rule (`Quests/Domain/Planner.cs:1105-1157`) | — |
| 7. X-14 and X §4: a pooled record's location is safe from older builds only in a new service session family, since `tree = null` holds every tree and a path would hold another line's work | The first half holds (`Core/Sessions/Sessions.cs:89-93`, `:1147-1161`: a null tree is the repository's main tree and holds every tree). The second holds only of a path a later line reuses; a path that names only its own line keeps today's meaning in `sessions.tree` (§3.1, §14), so no new family is needed | X on `null`; this design on the rest |
| 8. X §4: Review runs through `ReviewGraphReads`, *called at `SessionReviews.cs:55`* | `Trees/GitReads/SessionReviews.cs:56` | X, a line off |
| 9. The brief §3: FOUNDATION1 Part A *building (mod-vi landed; mod-x, Workplace, ahead)* | Holds: D212's notes record mod-i to mod-vi built, none after | — |

The rest holds as the survey and the proposals cite it.

### 0.3 The inputs, folded

| Input (the brief §3) | Folded as |
|---|---|
| FOUNDATION1, D212 | The ground. `Trees/` is Workplace (§14.2 row 10, `docs/2026-10-25-foundation-design.md:2342`), so every new type is Workplace's or lower (§2.5). The claim is a store family by §13.3 (`:1970-2001`), in Workplace's family (§13.3's row, `:1987`), with §13.4's claim by process run (`:2042-2066`). No lower subdomain needs the pool's answer, so no port and no module initializer is added; a row that finds one keeps the three bounds of D212's note of 2026-10-26. Every row lands after mod-x (§14.6) |
| D51 and its notes; the working surface | Rule 2 and rule 4 amended by note, the rejected alternative kept (§13); TREEDEPS1's gate read again for a kept slot (§11); TREELINK1's link rule applied after a move (§5.3); TREELOCK1's reason kept for a held slot and its *not covered* closed for a conversation's (§2.4); TREEGONE1's remake becomes, where a slot is free, a slot brought back, its fallback copying TREEDEPS1's (§3.3, §11) |
| D88, D102, D195 (CLEANUP1, KEEP1, KEEPSIZE1a/b) | The tidy's set-aside of a pooled line becomes *the end in place*, KEEPSIZE1b's own steps with *detach* where it has *move* (§10); the one list of build folders TREEKEEP1's with `wwwroot`, the slim's and the copy's its named subsets (§11); D195's kinds gain `kept-trees` (§10); KEEPSIZE1c-f stand |
| D167, D178, D189 | Their rules stand: the entry's tree, the kept take's tree and the resume's tree are each the line's path, which a free slot is brought back to (§3.3, §4); each takes a clarifying note |
| SWITCHCTX1 §1.5 | Read as the reason the slot moves and the line does not: Claude Code files a conversation and keeps its trust by the working directory's path (`Platform/ToolHomeLeftovers.cs:30-31`, `:55`; `Accounts/SignIns/ClaudeTrust.cs:108-125`), Codex by date (`ToolHomeLeftovers.cs:41-42`). Its carry is not needed here |
| D115, DEV6 | The count's default reads `laneCap` once DEV6 builds it (§8) |
| LINESTOGETHER1, ACCEPTCONFLICT1, REVIEWFLOW1 | Unchanged: a landing's steps are today's; a landing whose line's slot was taken has one brought back first (§3.3) |

## 1. The two proposals, question by question

The question numbers are the brief's §7.

| Q | X | O | Chosen |
|---|---|---|---|
| 1 **The slot** | `s-<n>` under `<home>/work/tree-pools/<pool-id>/`; a durable store lease per occupation with a generation; a permanent pool lock | `t-<n>` under `<home>/work/pool/<ws>/<repo>/`, a new home entry; a store claim per step, the record holding the slot after; the lock a session's while held, `daoris kept tree` while free | **Neither's place: a numbered slot whose folder sits at its line's path and is moved to the path of the next line that takes it (§2.2); O's claim per step and O's lock, OPENCUT1's lock during a take (§2.3, §2.4); the store, as both** |
| 2 **The line and its branch** | create-only `daoris/s-<full id>` per branch work unit; readers by unit; no folder fallback for pooled lines | `daoris/s-<8 hex>`; `branch_parts`; readers by the session's branch; `BranchOfTree` null for slots | **Today's name and derivation: the folder is named for its line's branch, so `BranchOfTree` and its callers stand (§3)** |
| 3 **The line's identity** | the entry, a `BranchWorkId`, a part and a lease; every path reader moved to them | the branch; the path kept only as *the folder the record ran in* | **The path, as today, since no line's path ever holds another line's work (§3.1); the dozen sites that meet a gone tree re-keyed (§3.2), the ways back in §3.3** |
| 4 **The conversation** | native resume only once the exact original directory is reacquired; else a new part with the brief (`workplace-moved`); D189 amended | the record's slot if free; else a new part with the brief (`fromWhy=tree`); D189 amended | **A free slot brought back to the line's own path, so its conversation and trust are found there; D189's rule stands, a clarifying note (§4)** |
| 5 **The start** | journalled; HEAD pinned; loose files moved out; `reset --keep`; a non-forcing switch with `--no-overwrite-ignore`; verified | `reset --hard`, `clean -fd` after a parentless keep commit; then a switch | **X's non-forcing discipline, its pin and its unfinished-operation check; the first build takes only a clean slot, so no reset and no clean; O's keep commit on the owner's yes (§5)** |
| 6 **The end** | output; no output; uncommitted work holds the equipment; loose files kept apart | output; no output; uncommitted work kept aside | **Both ends, said once; uncommitted work holds the slot (X) until O's keep on the owner's yes (§6)** |
| 7 **What holds a slot** | a table; words waiting do not; a terminal and dirty work do | a table; words waiting do for one look; an extra tree so nothing waits | **The union where they agree; words waiting hold (O); a terminal and work no ref holds hold (X); a due landing holds (neither) (§7)** |
| 8 **The count** | a Workplace store policy, `treePools` on export; default the cap at opt-in, stored; a bound conversations wait on | `driver.json`'s `keptTrees`; default cap + 1 read live; an extra tree past it | **O's place and default; past the count a tree as today; what the count means put to the owner (§8, §18)** |
| 9 **Lazy creation, lowering** | rows declared, folders lazily; lowering drains highest first; a departure presents what stays | made lazily; retiring slots moved aside, read `unmarked`, the days | **Both, said once; retirement by the set-aside's own move (O), highest first (X) (§9)** |
| 10 **The finished record** | at equipment release; new pool tables; immutable end refs; an outbox | at the branch's deletion; a new route `POST /api/lines/record`; `branch_ends` | **KEEPSIZE1b's own steps and route at the end in place: the path is still the line's, so its attribution holds (§10)** |
| 11 **Dependencies** | one catalogue with capabilities; lockfile evidence; no silent delete; TREEGONE1's remake seeded | npm's own staleness rule; replaced from the checkout where it matches; `bin/` kept | **O's rule and list with `wwwroot`; X's capabilities as the list's named subsets, its words and its seeding; links made again after a move (§11)** |
| 12 **Git and Windows** | non-forcing; a held preparation; contained jobs empty first | non-forcing; `stuck`; measured | **Both; `git worktree move` and its refusals (§12)** |
| 13 **D51** | the work's identity never reassigned, equipment reused between completed occupations | rule 2 the branch's; rule 4 restated | **Stronger than both: neither the branch nor the path is ever reused; no directory ever holds two lines (§13)** |
| 14 **Records and older builds** | pooled sessions in a new service family with doors of its own; older builds blind to them | the slot's path in `sessions.tree`, an older build reading it as no tree of its own | **Neither: every record keeps today's meaning, since a path stays its line's; X's family not needed, O's reading wrong at the code (§0.2 row 5, §14)** |
| 15 **The words** | old codes kept; new codes with no path; a table in en and zh | new codes; a table in en and zh | **Both's rule; the codes of §15** |
| 16 **The install's trees** | none adopted; any removal an owner-reviewed plan | none adopted; they drain | **Both: none adopted, none removed by this work; they drain by today's rules (§16)** |
| 17 **The order** | P1 to P9, reuse off until P8 | 1a to 1k, on per repository at 1f | **a to j (§17): the first three both's shape (the store and the census, git unwired, the readers), on per repository at d** |
| 18 **The owner's** | seven | four | **Four, and one bounded spend (§18)** |
| 19 **Proof** | per row; the decisive fixture; kills at every boundary | per row; each case seen failing first | **Both, per row (§17)** |

### 1.1 Where they differ, and which way

| # | The difference | Chosen, and why | The case the other gets wrong |
|---|---|---|---|
| 1 | Where a slot's folder sits: a place of its own (both), or the path of the line that holds it | **The line's path, the synthesis's own** (§2.2): a slot is moved to the path of the line that takes it and brought to the line there. Every reader of a path keeps reading one line (§3.1), every record keeps its meaning to builds already shipped (§14), and an agent's conversation is found where it was (§4). The cost, said: a rename per take, and caches that hold absolute paths rebuilt after one (§11), which row b measures | **O's:** a shipped build resumes a line into its slot's folder by `Directory.Exists` (`Loop/Driver.cs:2127-2131`) or goes on with words there (`DriverContinuations.cs:171-178`, `:227`), on whatever branch another line left; and a line whose slot another line holds loses its agent's own memory to the brief. **X's:** a second session family in the service with doors of its own, which every reader of sessions merges (the page, the trace, the channel, asks, history), and a shipped build that can neither show nor continue a pooled line |
| 2 | The claim: a durable lease per occupation with a generation (X), or a step's claim, the record holding the slot after (O) | **O's**: whether a slot is held is read live from the ledger, the in-use set and the claims (§7), so no lease mirrors them | X's: a park ended in another process (an orphan's sweep, the person's *End*) leaves a lease standing beside a ledger that holds nothing, two answers to one question, which TREELOCK1's let-go exists to reconcile for git's lock today (`SessionLocks.cs:96-123`) |
| 3 | Work no ref holds before a reuse: moved out to a kept area (X) or kept in a parentless commit (O) | **Neither in the first build**: a slot holding such work is not taken (§5.2). **O's keep commit** on the owner's yes, row i (§5.5) | X's: a file move cannot keep a tracked edit, so X holds those slots anyway, and its payloads (`<home>/work/tree-pool-kept/`) have no rule that removes them (X §6 item 6), so they grow until the owner disposes of each. O's before the owner's yes: a reset of a tree that holds work, which the brief §2 reserves to the owner |
| 4 | The count: a bound sessions wait on, the person's conversations included (X), or the trees a repository keeps, a start past it given an extra tree (O) | **O's**: past the count a start opens a tree of its own as today, which today's tidy takes once its work is done; the reading is put to the owner (§18 item 1) | X's: the person's second conversation in a repository whose two slots hold parked sessions waits for a tree, where today a conversation opens its own at once (`ChatRunner.cs:635-651`). O's, and this design's: a strict reading of the direction's point 7 removes trees only by the count, and a tree past the count is removed by the tidy |
| 5 | Where the count is declared: a Workplace store table (X) or `driver.json` (O) | **O's** | X's: a setting outside §13.3's `settings` family, which holds one row per key of `driver.json` read through Settings' one parser (`docs/2026-10-25-foundation-design.md:1972-1990`); the export and import would need a case of their own, and z-ii a second home for configuration |
| 6 | The default: the cap at opt-in, stored (X), or cap + 1, read live (O) | **O's** | X's: a person who raises the cap later keeps the old number of kept trees and runs past it every day |
| 7 | The lock: one permanent pool reason (X), or TREELOCK1's while held and a kept reason while free (O) | **O's**, with OPENCUT1's opening lock during a take (§2.4) | X's: the session lock is how every shipped reader tells a tree's holder (`SessionLocks.cs:42-54`); a permanent pool reason hides the holder from them and from a person reading `git worktree list` |
| 8 | Words waiting on an ended record: no hold (X) or a hold (O) | **O's** | X's: the in-use set keeps such a tree today for the next look's go-on there (`BranchTidying.cs:146`; D88's AUTOTIDY2d); freed for that look, a start takes the slot and the go-on must bring another back, a rename and a switch for nothing |
| 9 | The finished record: new tables and refs (X), a new route (O), or KEEPSIZE1b's own | **KEEPSIZE1b's**, at the end in place (§10) | Both: a second shape for one record. Here the path is the line's, so the route's rule by path (`Core/Sessions/WorkRecords.cs:446-463`) names exactly the line's sessions |
| 10 | A stale `node_modules`: kept untouched (X) or replaced from the checkout where its install matches (O) | **O's**, under D195 point 3's reason that npm discards the folder at every clean install | X's: kept stale with the agent told to run `npm ci`, which deletes it first; keeping it gains nothing, and the copy at a matching lockfile saves the install |
| 11 | A line whose slot was taken when it comes back: a new part handed the brief (both) | **The slot is brought back to the line's path** (row 1) | Both: a D79 wait answered after another start took its slot resumes from the brief, not its conversation, and O's own §2.4 expects it often enough to measure (`fromWhy=tree`) |
| 12 | A no-output line's empty branch: grown again when its record goes on (O) or gone (X) | **O's** (§6) | X's: a conversation that ended with nothing, spoken to again, finds its branch gone and is held (`Trees/SessionTrees/TreeReturns.cs:70-77`; `Conversations/Domain/Continuation.cs:231`) |

### 1.2 What each got that the other missed

**Codex's.** The rule this design is built on, that the work's identity is never reused while its equipment is. HEAD pinned and compared
before anything prepares a slot (X-5 step 4), and an unfinished git operation as a hold (§5.2). `--no-overwrite-ignore` on every switch. A
person's terminal as a hold. A kept build output as no proof that a commit builds. TREEGONE1's remake seeded by TREEDEPS1. A crash that never
jumps to *free*. The downgrade question taken seriously, which led to §0.2 row 5 and to §2's choice. D167's parts alongside kept apart from a
line's own continuation. The install's measurement split between what reuse saved and what deleting legacy folders saved, and the plain
statement that a count bounds equipment, not bytes.

**The Opus reader's.** Four corrections of the survey (§0.2 rows 1 to 4). The claim as a step's, the ledger holding the slot afterwards. The
parentless keep commit and `AnyRef` excluding its prefix. The count in `driver.json` at cap + 1, read live. Nothing ever waiting for a tree.
Retirement through the set-aside's own move. A no-output branch grown again. A take preferring a slot whose line is finished. `wwwroot` on
the one list. The census in `ConfigSeamTests`' manner.

## 2. The slot, its place and its claim

### 2.1 In one paragraph

A slot is a linked worktree of the repository's registered checkout that the repository keeps, numbered `n` in its pool and known by
`(workspace, repository, n)` and by git's own name for its registration, which a move does not change. **Its folder sits at the path of the
line that holds it, or last held it**: `<home>/work/trees/<workspace>/<repository>/s-<8 hex>`, today's layout (`Trees/SessionTrees/SessionTrees.cs:354-356`;
`Platform/DaorisHome.cs:405`). When another line takes it, the slot is moved to *that* line's own path and brought to the line there; when a
line comes back and its slot was taken meanwhile, a free slot is moved back to the line's own path, on its branch. So a path and a branch
still name one line of work for its whole life, as they do today, and what is reused is a working copy and its ignored dependency and build
folders. Every reader that reads a path as a line reads right (§3), every record keeps its meaning to builds already shipped (§14), and an
agent's conversation, filed by its working directory, is found where it was (§4).

### 2.2 What a slot is, and where it sits

- **What it is.** A linked worktree of the checkout, made by today's open (`SessionTrees.OpenAsync`, `SessionTrees.cs:273-428`) the first time
  a start needs a slot and the repository has fewer than its count (§9). Its first life is a fresh tree in every respect, said in today's words
  (`started.tree`, `started.tree-copied`).
- **What identifies it.** `(workspace, repository, number)` in the store (§2.3), and git's registration name under `<common dir>/worktrees/`,
  which `git worktree move` keeps (git's documented behaviour; row b proves it). Nothing in the driver keys on that name equalling the folder's:
  the two readers of it read any name (`Service/LinkedWorktree.cs:25-27`; `Trees/RefWatch.cs:238-239`), and the session lock is read through
  the tree's `.git` file (`SessionLocks.cs:42-54`).
- **Its folder's name.** Its line's: `s-<8 hex>`, minted as `OpenAsync` mints it, for a new line; the line's own name for a returning one. The
  slot's number is never spelled in a path or a branch.
- **Not a place of its own.** Both proposals put slots in a place of their own (`<home>/work/pool/…`, O §1.1; `<home>/work/tree-pools/…`,
  X §1); §1.1 row 1 says why not.
- **Its neighbours.** The `o-`, `p-` and `r-` folders stay where they are (`Trees/SessionTrees/TreeStrays.cs:77`); the strays' walk meets a slot
  as a tree git lists and leaves it (`:150-153`).

### 2.3 The claim, and where it is recorded

**The desktop store, as a Workplace family by FOUNDATION1 §13.3's rules**: new tables, every column required, the migration step after the
last at build time, the writer stamp unchanged, so builds of z-i and z-iii never open them, as `bindings` and `binding_picks` were added
(`Store/DesktopStore.cs:107-129`). It is the owner's preferred home for records the product reads and sets (the brief §5), and the only one
that gives one compare-and-set across the shell, a `--share` loop and a verb (O-1). A lease file beside the slot would need the store's
dead-run test anyway; a lease in the service would teach the service a machine path and desktop state (D47 §4; foundation §13.7); git's lock
alone is not a test-and-set across a read, a judgement and a switch (O-1).

| Table | Columns, all required | Key | What it says |
|---|---|---|---|
| `kept_trees` | workspace, repository, number, registration (git's name for it), path (its folder now), line (the branch whose path it is at, `''` for none), state (`kept`, `retiring`, `stuck`), said (`''` or why), since | (workspace, repository, number) | the slots that exist, and where each is now |
| `tree_claims` | workspace, repository, number, kind (`take`, `return`, `end`, `retire`), holder (the quest, session or act it serves), line, from (path), to (path), phase (`judged`, `moving`, `moved`, `switched`), run (a `store_runs` id), at | (workspace, repository, number) | the one claim in flight on a slot, deleted when its step ends |
| `line_ends` | workspace, repository, branch, session, kind (`output`, `no-output`, `uncommitted`), tip, at | (workspace, repository, branch, at) | each let-go of a line: the tip the next judgement compares (§5.2), and the licence to grow a no-output branch again (§6) |

Row i adds `kept_work` (§5.5). **Whether a slot is held is never stored**: it is read live from the ledger, the in-use set and the claims
(§7), so no row mirrors the records and the two cannot drift (O §1.2; §1.1 row 2).

**How a claim is taken.** The start that needs a slot (the driven start, `Loop/Driver.cs:2186-2215`; a conversation's, `ChatRunner.cs:635-651`;
a landing's return, §3.3) holds the repository's start window as today (`TreeLock.TryStarting`, shared among starts and exclusive for a
replay, `Trees/TreeLock.cs:50-63`), and in one `BEGIN IMMEDIATE` transaction chooses a slot (§5.1) and inserts its claim; the primary key
refuses a second claim on the slot from any process. Each git step after it is a mark in §13.4's sense: a transaction checks that the claim is
still this run's and writes the phase, then the step runs. The record opens, the session lock is taken, and one transaction deletes the claim
and writes the slot's `path` and `line`. **The end** claims the slot (`end`), reads it (§5.2's reads), writes `line_ends`, puts the free lock
back and deletes the claim; nothing in the folder changes at an end. D201's launcher holds no claim: a claim belongs to a step, never to an
agent's process (both).

**Across a crash.** A claim whose run is dead by `HomeLease`'s test (its pid gone, or alive under another start time, a start time that does
not read counting as alive: `Platform/HomeLease.cs:66-68`, `:165-173`; foundation §13.4) is taken over by the look, by its phase:
- `judged`: nothing moved; the claim is dropped and the free lock put back.
- `moving`: the slot is wherever git now lists it, its old path or its new; it is recorded there, locked `daoris kept tree`, `stuck`, and judged
  again at the next look.
- `moved` or `switched`, before the record opened: the slot is at the new path; it is recorded there, `stuck`, and judged again at the next look
  (§5.2), freed where nothing in it is anyone's; a branch the take made, still at its start with no commit, is deleted at that commit, as an
  undone open's is (`SessionTrees.cs:411-420`); the new line never opened, so its path names nothing.
- after the record opened: the ledger holds the slot; today's orphan sweep ends the record, and the end follows at the next look.

A killed take also reads to every build's strays as a cut-off opening (§2.4), moved aside whole only where its branch holds nothing another ref
does not (`TreeStrays.cs:181-201`). A slot is taken only where nothing of the previous line's is outside a ref (§5.2), so that costs the pool a
slot and nobody any work.

### 2.4 Its lock

- **While a session holds it:** TREELOCK1's `daoris session <id>`, unchanged, so `HolderOf` and every shipped reader of the lock read the
  holder (`SessionLocks.cs:42-54`). A conversation that takes a slot locks it too, closing TREELOCK1's *not covered* for conversations
  (D51:137-138).
- **While it is free:** `daoris kept tree`, a fifth reason, matching neither a session's prefix (`SessionLocks.cs:28`) nor an opening's
  (`Trees/SessionTrees/TreeOpenings.cs:15-45`). `SessionLocks.HoldAsync` learns to take it over, as it takes over a session's today
  (`SessionLocks.cs:60-73`); the end puts it back; the look's let-go of a session lock nobody holds puts it back on a slot rather than leaving
  the slot unlocked (`:101-123`).
- **During a take or a return:** OPENCUT1's opening reason naming this process (`TreeOpenings.cs:25`), so a take killed halfway reads to every
  build as a cut-off open (`TreeStrays.cs:181-201`) and a live one is left alone (`TreeOpenings.cs:44-45`).
- **Any other reason** (a person's, or an opening's whose process lives): the slot is out of the pool, said, and never touched.
- **What a shipped build does with a slot's lock.** Its tidy refuses a locked tree (`TreeSetAside.cs:84-88`); its let-go lifts only a session's
  lock (`SessionLocks.cs:126-134`); its strays judge only an opening's; its press's `git worktree remove` refuses a locked tree.

### 2.5 The types on FOUNDATION1's map

| Type | Subdomain (rank) | Names only |
|---|---|---|
| the migration step's DDL in `DesktopStore.Steps` | Store (2) | — |
| `DriverConfig.KeptTrees`, `WithKeptTrees`, `KeptTreesOf` | Settings (3) | a map of ints, no rule type (§14.3) |
| `NoteCodes` `StartedTreeKept` and its clauses, `StartedTreeBack`, `StartedTreePastKept`, `TreeReturned`, `EndedOutput`, `EndedNoOutput`, `EndedUncommitted`, `TreeKeptHeld`, `TreeKeptStuck` | Events (7) | — |
| `KeptTree`, `TreeClaim`, `LineEnd`, `TreePool` (take, return, end in place, retire), `SlotChoice` and `SlotJudgement` (pure, in `Domain/`), `TreeHolds` (the holders as a delegate, as `reviewing` is handed to `BranchTidying.InUseAsync` today, `BranchTidying.cs:132`, `:140-150`) | Workplace (10) | Store, Settings, Service, Events and below |
| the holders a standing move (Course 14), a served review (Review 16) and a helper wait (Participants 12) contribute | each answers `TreeHolds` as a delegate Loop composes | — |
| `ConversationTrees.Stands` and `Continuation.Judge`'s `treeStands` answering *stands, or can be brought back* | Course (14), calling `TreePool` downward | Workplace |

`Loop/FreshTree` is Workplace's (§14.2 row 10); its undo becomes the claim's. No pool type names `ChatRunner`, the planner, `HelperWaits` or
the review desk.

## 3. The line's identity, and the readers keyed by path

### 3.1 The rule: a path names one line, always

The folder a line's sessions run in is named for that line's branch (`s-<x>` for `daoris/s-<x>`), and a slot taken for another line is moved
to *that* line's path before anything runs in it. Nothing ever runs in a path but its own line's sessions, so the facts every reader in survey
§6 assumed, *the folder is the branch*, *a path is one line*, *the same path means the same line*, stay true. What the pool adds is that a
line's path may be **empty for a while**: its slot was taken by another line while nothing held it (§7). Today a path empties when the tidy, a
press or a repository's merge tool takes its tree, and every reader already answers that case (TREEGONE1's remake, a gone tree's refusal, the
review's landed branch). The pool changes those answers only where they would refuse or remake a bare tree, so that a slot is brought back
instead (§3.3).

### 3.2 Every reader of survey §6

**Stored.**

| Record | Kept, re-keyed or cut |
|---|---|
| The service's `sessions.tree` (`Core/Sessions/Sessions.cs:89-100`, `:1147-1161`) | **Kept**: the line's path and the ledger's lock key; a pooled line's record names the path its line always runs in |
| `work_records`, `work_record_sessions` (`Core/Sessions/WorkRecords.cs:30-48`, `:446-463`) | **Kept**: posted at a pooled line's end in place, `Tree` its path and `Moved` that moment, so *every session naming the path made before the move* is exactly the line's (§10) |
| The helper, partner and opinion desks (`Core/Sessions/HelperDesk.cs:89-94`; `Core/Sessions/PartnerDesk.cs:69-74`; `Core/Review/OpinionDesk.cs:113-119`) | **Kept**: the asker's or lead's tree is its path, held while it runs (§7) |
| `session-branches.json`, `GrownBranch` (`Trees/Branches/SessionBranches.cs:19-27`; the unit found by folder, `TreeLands.cs:845-848`) | **Kept**: recorded at a take as at an open (`SessionTrees.cs:380-398`), its `At` the take's moment; the folder still names the branch |
| `landings.json` | **Kept** |
| Auto-landings (`AutoLander.cs:209-222`) | **Kept**: a due landing holds its line's slot until it is judged (§7), so `:209`'s *gone* never meets a slot taken under it |
| Set-aside manifests (`Trees/SessionTrees/TreeSweeps.cs:220-230`) | **Kept** for legacy trees and trees past the count; a pooled line's end writes none, its inputs in the store (§10) |
| The heavy slot's holder (`Sessions/Heavy/HeavySlot.cs:9-27`) | **Kept** |
| The desktop store's bindings and picks (`Store/DesktopStore.cs:107-129`) | **Kept** |
| git's registrations and `locked` (`SessionLocks.cs:37-54`) | **Kept**: a slot's registration name is stable and its path follows its line; one new reason (§2.4) |
| The agents' homes (`ToolHomeLeftovers.cs:30-31`; `ClaudeTrust.cs:108-125`) | **Kept**: filed by the line's path, which a returning line has again (§4) |
| The opening note (`SessionTrees.cs:439-468`; `Core/Sessions/SessionNote.cs:9-17`) | **Kept** for every tree opened fresh; a taken or returned slot says new codes (§15) |

**Read in the driver.**

| Reader | Kept, re-keyed or cut |
|---|---|
| `TreeFollows.BranchOfTree` (`Trees/SessionTrees/TreeFollows.cs:148-161`), *what finds a session's branch once its tree is gone*, and its callers and copies (survey §6's list) | **Kept**: the folder is the branch |
| `Planner.SameTree` (`Planner.cs:1120`, `:1141`, `:1358`, `:1380`, `:1390`, `:1586-1591`; `Sessions/RunningSessions.cs:129-145`) | **Kept**: one line per path; a line whose slot was taken holds no path and blocks nobody |
| `EntryLandings.Sequence` (`Trees/SessionTrees/EntryLands.cs:104-121`) | **Kept**: parts sharing a path share a line |
| A chat run's line and its taken quest (`Workflows/ChatRuns.cs:66-91`; `Workflows/WorkflowRun.Read.cs:204-220`) | **Kept** |
| The trace's *same tree* (`Sessions/Domain/Trace.Chain.cs:169-187`) | **Kept**: the same path is the same line |
| The tidy's in-use set and waiting trees (`BranchTidying.cs:140-150`; `Service/ServiceClient.cs:2222-2249`) | **Kept**, joined by the pool's own holders (§7) |
| `ContentHolds.HoldsAsync`'s *opened* (`Trees/SessionTrees/ContentHolds.cs:573-574`) | **Re-keyed** (row c): for a slot, its line's `GrownBranch.At`, since a moved folder keeps the creation time of its first making; a legacy tree keeps its folder's |
| The context brief (`Loop/Driver.cs:2560-2570`) | **Kept** |

**Where a gone tree is met** (survey §2's holders, the sites the pool re-keys).

| Site | Kept, re-keyed or cut |
|---|---|
| TREEGONE1 (`TreeReturns.cs:48-110`) | **Re-keyed** (row e): a pooled line's tree gone while its branch stands gets a free slot brought back (`tree.returned`); with none free, today's remake, copying TREEDEPS1's (§11) |
| Words going on (`Continuation.cs:231`; `DriverContinuations.cs:171-178`) and a conversation's go-on and moves (`ConversationTrees.cs:11`; `ChatRunner.cs:630-633`; `Conversations/Chat/BriefedMove.cs:105-107`, `:188-190`; `LimitMove.cs:75`, `:261`) | **Re-keyed** (row c, on at e): *stands, or can be brought back*, and the go-on brings it back first |
| A landing (`TreeLands.cs:196-204`, `:918-926`) | **Re-keyed** (row e): a line whose slot was taken has one brought back (a `return` claim), then lands as today |
| A session's review (`SessionReviews.cs:125-130`) | **Re-keyed** (row c): a pooled line's tree gone and unlanded reads its branch in the checkout, where today it reads nothing |
| *Show it again* (`Review/ReviewDesk.cs:179-190`) | **Kept**: the served review holds the slot until the verdict (§7); after it, a slot taken shows as a tidied tree does today, since its build outputs went with the slot |
| The tidy's set-aside (`TreeSweeps.cs:158-301`; `TreeSetAside.cs:190-341`) | **Re-keyed** (row d): a slot is ended in place, never set aside (§10) |
| The press's removal (`TreeRemovals.cs:14-200`) | **Re-keyed** (row d): on a slot, the person's press retires it (§9) |
| `SessionLocks.HoldAsync` and the look's let-go (`SessionLocks.cs:60-73`, `:101-123`) | **Re-keyed** (row b): `daoris kept tree` taken over, and put back on a slot |
| The Disk space card (`Platform/SystemSpace.cs:30-41`) | **Re-keyed** (row d): a `kept-trees` kind (§10) |

**Read on the page.**

| Reader | Kept, re-keyed or cut |
|---|---|
| The branch built as `` `daoris/${last segment}` `` (`Web/work/WorkFrame.tsx:475-477`; `Web/work/attention/attention.ts:639-640`) | **Kept** |
| *The last segment is the branch the tree was cut for* (`Web/work/identity.ts:132-144`, `:186-192`) | **Kept** |
| The terminal and the acts on a tree (`WorkFrame.tsx:1309`; `Web/work/acts.ts:102`, `:166`) | **Kept**: a line whose slot was taken shows as a gone tree does today; a terminal open in a slot holds it (§7) |
| The trace's tree and *same/other* (`Web/work/head/HowItCameToBe.tsx:427-438`); earlier parts' trees (`Web/work/conversation/EarlierParts.tsx:56`) | **Kept** |
| The repository's trees switch (`Web/projects/ProjectsView.tsx:313-316`; `Modules/Bridge/Routes/DriverRoutes.cs:71`) | **Kept**; the count's control beside it (§8) |

**Cut:** none. Row a's census (`PoolReadersCensusTests`) lists every re-keyed site above as *not yet moved*, with the row that moves it, and
fails on a new site that refuses, or remakes a bare tree, where a record's tree is gone outside the seams, a new read of a tree folder's
creation time, or a path built from a slot's number.

### 3.3 Every way back

| Start | Its line's slot is at its path and free | Its line's slot was taken |
|---|---|---|
| A resume (D79), an answer (D131), words going on (MSG1b, D137) | goes on there; no git at all (O) | a free slot brought back to its path on its branch (row e), the conversation resuming there (§4); none free: today's remake |
| A carry-on (D80), a driven hand-over (MIXAGENT1), a bring-forward (D178) | there | brought back, as above |
| A further part (D167 point 4 as amended) | into the entry's newest part's path where nothing live holds it (`Planner.cs:1105-1157`), as today | brought back; a part alongside a live part takes a slot for a new line of its own, as today it opens a tree |
| A hand-over, a limit move, a briefed move (D189 point 2) | the same path; the standing move holds the slot (§7) | not reached while the move stands; a move begun after the slot was taken brings one back |
| A chain's next step (D82, CHAIN2) | a slot for a new line from `BranchNowAsync(parent)`, else `daoris/<folder>` (`Driver.cs:2191-2207`), right since the folder is the branch | the same |
| A landing (Accept, `trees land`, the auto-lander) | lands in it, as today | brought back for the landing (a `return` claim), free after |
| TREEGONE1 for a line that began in a legacy `s-` tree | today's | a free slot brought back where one is free; else today's remake |

A line never waits for *its own* slot while another line holds it, and never goes on in another path: another free slot is brought to its
path. Both proposals made that case a new part handed the brief (§1.1 row 11).

## 4. The agent's conversation and resume (D189)

Claude Code files a conversation as `projects/<working directory, flattened>/<id>.jsonl` in the account's home, and its resume reads it there
and refuses a missing one (`docs/2026-10-24-switch-context-design.md:184-188`; `ToolHomeLeftovers.cs:30-31`, `:55`); its trust is the path's
(`ClaudeTrust.cs:108-125`). Codex files a session by date (`ToolHomeLeftovers.cs:41-42`), so its resume does not depend on the folder.

**Because a line's sessions only ever run at its own path, a line coming back runs at that path whichever slot is brought there**, and its
agent's own conversation is found in the same account home under the same folder name, with the trust the path already had. D189 point 2's
*the same agent, account and tree* holds, *tree* being the path the record has always named, and its resume-first rule stands: **D189 takes a
clarifying note, not an amendment** (D214's *This would amend*). The same holds for a hand-over's or limit move's new part: *the same tree
keeps the agent's folder the same on every account home* (survey §2's row for D189 point 2) stays true.

**What it costs, said in `tree.returned`.** A conversation resumed in a slot brought back finds its branch's tracked files and its commits,
and ignored files another line left (build outputs, at its own tip once switched); its uncommitted work is there only where its slot was
never taken (a slot holding work no ref holds is never taken, §5.2) or, after row i, laid back.

**Rejected:** a resume only in the line's own slot while it is free, else a new part handed the brief (O-4; X-4 likewise, reacquiring the
exact directory or briefing): the agent's exact memory gives way whenever another line took the slot, which in a pool sized to sessions at once
befalls every D79 wait that outlasts another start; copying the agent's files between project folders, which writes in an agent's home (D189
point 5) and is SWITCHCTX1's unaccepted carry; a junction per line to a stable slot, since `git worktree remove --force` follows a junction
(D51:74-77) and what the agents and the tree guard read through one is unverified (O-4).

**Unverified:** whether Claude Code reads anything of a directory but its path when it resumes (its git branch, a file it touched). TREEGONE1's
remake already resumes at the same path in a new folder (D51:143-159), and its ticks prove the folder with a stand-in harness only
(`Tests/Loop/SessionMessagesTickTests.cs`); row e's installed proof runs one real resume (§17, §18 item 5).

## 5. The start: brought to the line, and what makes it safe

### 5.1 The choice

`SlotChoice`, pure, inside the start window, takes the first that stands:

1. **The returning line's own slot**, at its path and free: nothing moves and no git runs (O).
2. **A free slot whose line is finished**: its branch ended in place (§10), or it ended without output, or it landed and its work is on the
   line.
3. **A free slot whose line may come back** (a D79 wait, an ended conversation, an unlanded line no one holds), judged clean (§5.2), the one
   let go longest ago first.
4. **A new slot**, where the repository has fewer than its count: today's open, recorded as the next number (§9).
5. **A tree of the line's own, as today** (`SessionTrees.OpenAsync`), off the pool's books, said in `started.tree-past-kept` beside today's
   words; today's tidy takes it once its work is done (§8).

The claim is inserted in the transaction that chooses.

### 5.2 The judgement: what makes it safe

A slot is taken for another line only where every one of these holds; any other answer leaves it its line's, said (`tree.kept-held` with
why), and the choice tries the next:

1. **Nothing holds it** (§7): no record active or waiting names its path, no claim, no standing move, no served review, no due landing, no
   helper wait, no terminal.
2. **git lists it at its folder** (TREEGONE1's test, `TreeReturns.cs:56-58`), and its lock is `daoris kept tree`, or a session's lock whose
   record no longer holds it (`SessionLocks.cs:88-94`).
3. **The look's last guards** (`TreeSetAside.LastLookAsync`, `TreeSetAside.cs:70-135`): no link under the trees home on the way to it, no
   lock of a person's, no `assume-unchanged` or `skip-worktree` path, no repository inside it that git would not ignore, nothing unread.
4. **No git operation half done**: no `MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `BISECT_LOG`, `rebase-merge/` or `rebase-apply/` in its
   git folder (X-5).
5. **Nothing outside a ref.** `status --porcelain -z --untracked-files=all --ignored=matching --ignore-submodules=none`, the read
   `ContentHolds.HoldsAsync` makes (`ContentHolds.cs:565-566`), shows no tracked change and no untracked path, and every ignored path lies
   under a folder of the one list (§11), unless the line is finished and its record kept (§10), whose other ignored scratch is carried into
   the next line's tree, never cleared, until row j.
6. **HEAD is where its line let it go, and a ref holds it**: the commit `line_ends` recorded, held by the line's branch (or its
   `GrownBranch.Now`, TREEFOLLOW1) or, for a finished line, by the recovery ref its end kept (`ContentHolds.cs:102-106`). A HEAD moved since,
   by a person or by a build that does not know the pool, holds the slot: *changed since its line let it go* (X-5 step 4).

Judged before the choice and **judged again inside the claim, right before the move**, as §13.4 marks an effect, so a write between the two
holds the slot (X's point; O's *made again and compared*).

So **the first build resets nothing.** A slot holding a tracked change, an untracked file, a half-done merge or a commit no ref holds is
never taken, and *brought to the line* is a non-forcing switch of a clean working copy (§5.3), which writes tracked files and touches no
untracked one. The brief §5's *a reset that clears untracked work is a deletion* is met by clearing none until the owner says otherwise (row
i), and its *never resets a tree that holds work no ref holds* by steps 5 and 6.

### 5.3 The move and the switch

For a new line B taking a slot at line A's path:

1. **Unlocked for the move.** Phase `judged`, then `moving` with its destination; `daoris kept tree` is lifted, its own reason only, since
   `git worktree move` refuses a locked tree unless forced twice. The repository's start window is held throughout, as an open's unlocked
   seconds are today (D51:137-138).
2. **Moved, and locked for the take.** `git worktree move <A's path> <B's path>`: one rename of the folder within `<home>/work/trees/`, git's
   registration updated and its name kept; then the slot is locked under OPENCUT1's reason naming this process (`TreeOpenings.cs:25`). Refused
   (a handle held open without delete-sharing, the set-aside's `busy`, `TreeSetAside.cs:196-197`, `:313-333`), the free lock is put back and the
   next slot tried; this one is tried at a later look. Phase `moved`.
3. **Its links made again.** npm links a workspace on Windows as a junction to its absolute path (D51:73-77), so a junction in a kept
   `node_modules` that led into A's path now leads into a folder that is gone, or later into A's own tree brought back. Each link in the slot's
   kept ignored folders that leads into A's path is made again leading to the same place in B's path, by TREEDEPS1's link rule (D51:72-73); a
   link leading anywhere else, or one that does not read, holds the slot (`stuck`). The walk is TREELINK1's: it never enters a link and looks
   at no more than a million entries (D51:108-112).
4. **Switched.** For a new line, `git switch --no-overwrite-ignore -c daoris/s-<B> <start>`, the start being what `OpenAsync` grows a branch
   from (the line, a chain's `from`, its `landed`, a part's `partOf`: `SessionTrees.cs:299-352`), and the `GrownBranch` and its unit recorded
   as at an open (`:380-398`). For a returning line, `git switch --no-overwrite-ignore <its branch, or GrownBranch.Now>`. Never forced:
   `--no-overwrite-ignore` refuses where the target tracks a path a kept ignored file stands at (X-5). Phase `switched`.
5. **Dependencies** judged per npm project (§11).
6. **Opened.** The opening's lock lifted, the record opened with `tree` = B's path, the session lock taken (`SessionLocks.HoldAsync`), and one
   transaction deletes the claim and writes the slot's `path` and `line`. A start that ends before its open deletes its new branch, still at
   its start with no commit, at that commit; the slot stays at B's path, free, locked `daoris kept tree`, its `line` none.

A returning line then has its kept work laid back where row i kept it and its tip is still that work's base, and LANDSYNC2 brings it up to
date with the line as today (`Driver.cs:2169-2172`).

### 5.4 A refusal

- **A rename refused**: `busy`, as the set-aside's; the slot stays where it is, free, and another is taken.
- **A switch refused** (`--no-overwrite-ignore`, or a file held open): `stuck`, git's words said (`tree.kept-stuck`), another slot taken. A
  stuck slot is judged again at each look and switched back to detached at its recorded tip where git allows; it held nobody's work before the
  switch (§5.2), so the person's press may retire it whatever it holds. What a refused switch leaves of the tracked files on Windows is
  unmeasured (survey §5); row b measures it.
- **A nested repository, a person's lock, a link not Daoris's**: never reached, since §5.2 holds the slot first.

### 5.5 Work no ref holds, kept aside (row i, only on the owner's yes)

Until the owner agrees (§18 item 3), a slot whose line let go with work no ref holds stays that line's (§6, §7). With the yes, the Opus
reader's keep (O §1.4), with Codex's checks:

1. The last guards (§5.2 step 3) and a bound: past 10,000 paths or 64 MB of untracked bytes the slot stays its line's, said.
2. The tree is written through an index of its own under the slot's git folder (`read-tree HEAD`, `add -A`, `write-tree`), so the real index
   and the folder are never touched; `commit-tree` with **no parent**, its dates the let-go's, its message naming the branch and `HEAD`, stored
   by a create-only `update-ref` at `refs/daoris/kept/<leaf>/<n>`; a `kept_work` row (workspace, repository, branch, ref, base, tree, paths,
   bytes, at; all required).
3. **Parentless on purpose**: `for-each-ref --contains <tip>` never lists it, so no ancestry proof, a shipped build's included, counts it as
   holding a branch (`ContentHolds.cs:379-384`); `ContentHolds.AnyRef` excludes the prefix too (`:432-433`).
4. Made again and compared right before the slot is cleared for another line; then `reset --hard` and `clean -f -d` (never `-x`), then §5.3.
5. Laid back when the line returns at the same base (`restore --source=<commit> --worktree -- :/`, proven in row i); the ref and the row
   dropped once the slot's status equals the kept tree. Where the tip moved, nothing is laid back and the opening names the ref.

**Rejected:** loose files moved to a kept area outside the slot (X-6), since a move cannot keep a tracked edit and its payloads have no rule
that removes them (§1.1 row 3); Daoris committing the leftovers on the line's own branch, which lands what the agent never chose to commit and
turns a no-output end into output (O-6).

## 6. The end: done with output, done without output

At a run's end (`Driver.cs:2886-2894`; the conversation's end in the chat runner), once its processes and their job have ended
(`Platform/ProcessJob.cs:6-32`), the end claims the slot, reads it (§5.2 steps 3 to 6), writes `line_ends`, and the record says one of:

- **Done with output** (`ended.output`): commits on its branch, nothing uncommitted. The slot stays at the line's path on its branch, locked
  `daoris kept tree`, free but for what §7 names, a due landing among them. Ready to land: Accept lands from it, or from a slot brought back.
- **Done without output** (`ended.no-output`): no commit and nothing uncommitted. The slot is free. The empty branch is the tidy's, ended in
  place (§10); `line_ends` saying `no-output` is the one licence to grow it again at the line's tip, under its own name, when its record goes on
  (O §1.3), so a conversation that ended with nothing can still be spoken to. No other line ever takes the name.
- **Done, with work it did not commit** (`ended.uncommitted`): tracked changes or untracked paths. The slot stays the line's, held, until a
  session commits the work, the person discards it, or row i keeps it aside. Daoris commits nothing on the branch (D164 point 1); today's
  landing refuses such a tree the same way (`TreeLands.cs:221-227`).
- **A park, a setup change, a helper wait**: as today, the record holds the tree (`Driver.cs:2894`).

`landing.nothing` (`Web/locales/en/note.json:133`) stays what a due landing with no commits says. A landing's steps are unchanged; only where
its tree comes from changes (§3.3), so LINESTOGETHER1, ACCEPTCONFLICT1 and GITAPP1 change nothing here.

## 7. What holds a slot

| Holder | Holds? | For how long | What frees it | What the person sees |
|---|---|---|---|---|
| A claim in flight | yes | its step | its end, or the look's takeover of a dead run's | — |
| A running record | yes, the ledger's key (`SessionLedger.cs:320-324`) | its run | its end | the rail's tree name (`identity.ts:141-144`) |
| A parked record | yes, the ledger's key (`SessionLedger.cs:1604-1607`, `:1833`) | until the person answers or ends it | the answer goes on there; *End* lets go | on the Projects row, *held: waits on you* |
| Words waiting on an ended record | yes, the in-use set (`BranchTidying.cs:146`) | until the next look's go-on | the go-on | — |
| A helper wait (HELPERWAIT1) | yes (`BranchTidying.cs:147`; `SessionLocks.cs:91`) | while its quest is taken | the answer, or the quest closing | as today |
| A served review build (REVIEWENV1d) | yes (`BranchTidying.cs:148`) | until the verdict | the verdict | as today |
| A standing move (a hand-over, a limit move, a briefed move) | yes, Course's `moves` row | the move | the new part's open | as today |
| A due auto-landing | yes | until the look judges it | its close | — |
| A terminal the person opened in it (`WorkFrame.tsx:1309`) | yes | while it runs | its job ending | — |
| Work no ref holds (§5.2 steps 4 to 6), a dirty lost take among it | yes | until committed, discarded or kept aside (row i) | a session or the person | `ended.uncommitted`, then `tree.kept-held` with why |
| A `stuck` slot | out of the pool | until a look clears it | the look, or the person's press | `tree.kept-stuck` |
| A D79 wait, an ended conversation, a clean kept lost take, a done of either kind | no | — | — | on its return, its own slot if free there, else one brought back (`tree.returned`) |

Where they agree, both proposals' tables are this one: a park holds, a review holds, a helper wait holds, a D79 wait, an ended conversation
and a clean kept lost take do not; kept work is not kept equipment (X-7, which D79's and D178's notes record). Where they differ: words
waiting hold for a look (O's, §1.1 row 8); a terminal and dirty work hold (X's). **A due landing holds** (neither's): the auto-lander closes a
due landing *gone* where its tree is gone (`AutoLander.cs:209`), so a slot taken under it would turn a landing due into a closed one. Survey
§8.4's other holders are O's correction (§0.2 row 1).

**When every kept slot is held**, the next start opens a tree of its own as today (§5.1 step 5): nothing waits, and the Projects row reads
*Trees kept: 3 · in use 3*.

## 8. The count

- **Where it is declared:** `driver.json`'s `keptTrees`, a map from a repository to a number from 1 to 16, beside `trees`, which stays the switch
  (`Settings/DriverConfig.cs:19-22`, `:1383-1391`), read through Settings' one parser as a map of ints with no rule type (§14.3), kept unread by
  an older build as every unknown key is (foundation §13.1), and moved into the store's `settings` by z-ii with every other key (§13.3) (O).
- **Its default:** absent, `cap + 1` read live (`DriverConfig.cs:71-75`: a cap of 2, so 3) for a repository whose pool is on: the driven
  sessions the machine may run, all in one repository at most, and the person's conversation. Once DEV6 builds `laneCap` (D115:21-26), it reads
  `max(cap, laneCap) + 1`.
- **How it is set:** `daoris driver trees <repository> keep <n>`, `SET_TREES` gaining `kept`, the Projects row's control beside the trees switch,
  and Ask Daoris's `setting` door as for every key.
- **Against the cap:** unchanged. The cap counts driven work, never the person's conversations (CAPCOUNT1, `Planner.cs:192-209`); set-ups'
  `atOnce` is unchanged; a conversation takes a slot as a driven start does, locked in its name, and counts under no cap.
- ***At most one slot per session the repository may run at once*:** the count keeps no more trees than can run at once, and lazy making keeps
  fewer where fewer ever ran (§9).
- **Past the count:** a tree of its own as today (§5.1 step 5), which today's tidy takes once its work is done. Whether that is the direction's
  meaning, or whether the count should bound the sessions that run in trees with the rest waiting, is the owner's question (§18 item 1).
- **What it does not bound** (X §3): the trees past it, retired slots waiting D195's days, and caches growing in each kept slot.
- **Rejected:** `daoris.json`, which D115 rejected for lanes as the doctrine tool's inert contract (D115:58-59), a count being a machine's
  besides (its disk and cores); the registry, which is not machine-local; a store table of its own (§1.1 row 5); the cap at opt-in, stored
  (§1.1 row 6).

## 9. Lazy creation, lowering, and a repository leaving

- **Lazy creation.** The first start that finds no free slot and fewer slots than the count opens a tree as today (§2.2) and records it as the
  next number. A slot git no longer lists is dropped from the store; the next start that needs one makes another.
- **Lowering.** The slots numbered above the new count are `retiring`, highest first (X): each takes no new line; when free and judged as a take
  would judge it (§5.2), or its line finished, its own lock is lifted (git keeps a locked tree's record against a removal not forced twice) and
  it is moved aside whole into `.tidied/<ws>/<repo>/<name>-<moment>` by the set-aside's own move (`TreeSetAside.cs:190-235`, `:313-341`), git's
  record of the vanished path dropped as the set-aside drops it (`:252-270`); with no manifest it
  reads `unmarked` and waits the set-aside days, its `node_modules` going at the slim's hour (`Platform/KeepKinds.cs:11-23`). A held one goes at
  its let-go; one holding work no ref holds stays, said. Raising the count again takes back a retiring slot not yet moved.
- **Not `git worktree remove`**, which TREEKEEP1 refuses for ignored paths only the tree holds; the set-aside's move is the path the tidy
  already proves (O-9).
- **A repository leaving** (unregistered, or its trees switched off): every slot retiring. A checkout gone: every slot kept, said, since git
  cannot judge them.
- **The person's press** (the Branches tab, `daoris-driver trees clean`) on a slot retires it now, as above; on a held slot it refuses as today.
- **Never by age.** A slot leaves only by a lowered count, a repository leaving, or the person's press; the days that follow are D195's on the
  folder moved aside, as for every set-aside folder.

## 10. The finished record and removal

A pooled line's tree never ends: its slot stays the pool's. So **KEEPSIZE1b's steps run at the end in place** (D195:511-519;
`TreeSweeps.cs:158-301`). The look's tidy takes a pooled line's branch exactly where it would take today's (`GoesByItself`,
`TreeSetAside.cs:27-30`), reading the line's free slot as no tree of the line's, and then:

1. **judges** it as today (the press's `Removable` and the look's wait for the line);
2. **holds the tip** on `refs/daoris/discarded/<branch>` wherever the line's own branch does not hold it (`TreeSweeps.cs:191-213`);
3. **detaches the slot in place** at the same commit (`git switch --detach`, which writes no file), where it is at the line's path on its
   branch: the step where today's tidy moves the folder;
4. **deletes the branch** at the commit judged in its one `update-ref --stdin` transaction (`ContentHolds.cs:456-475`), which refuses a branch
   checked out anywhere (`:468-471`);
5. **posts the record** to `POST /api/trees/record` (`Http/Program.cs:1439`) with `Tree` the line's path, `Moved` this moment and `Folder`
   `<name>-<moment>`, the name KEEPSIZE1b gives a set-aside folder (`TreeSetAside.cs:35-36`), so `WorkRecords.SessionsOfAsync` names the line's
   sessions and no other (`Core/Sessions/WorkRecords.cs:446-463`); a lost post is retried from the store as today's is from the manifest.

No manifest is written: its inputs (the tip, what it grew from, the unit, the hold and its form, the recovery ref, the read moment) are the
store's. The slot stays at the finished line's path, detached at its tip, free, until a take moves it. **A no-output line** takes steps 1, 3 and
4 with no record, as a no-output tree's press keeps none today (D195:518-519).

**D195's kinds.** `trees`, rule `proof` (`Platform/SystemSpace.cs:37`), keeps the legacy trees and trees past the count; a new kind,
`kept-trees`, rule `count`, the person's number, counted from `kept_trees`; `set-aside`, rule `days`, gains retired slots. **The tidy's
`GoesByItself`:** its exception for a conversation's place (`TreeSetAside.cs:18-20`; D88:51-52) is moot for a pooled line, whose no-output
branch is grown again (§6). **KEEPSIZE1c to 1f stand**; 1d's allowance never counts a kept slot, which is live, and its set-aside part gains
retired slots.

## 11. Dependencies and build outputs kept

- **The one list:** TreeScratch's thirteen (`Trees/SessionTrees/TreeScratch.cs:15-19`) and `wwwroot`, an ignored web root no list holds today
  (survey §4). Each use names its subset of it, which is X's capabilities kept as subsets: what a take keeps (§5.2 step 5: the whole list);
  what the slim drops early (D195 point 3: a `node_modules` beside a tracked `package.json`, `KeepKinds.cs:11-20`); what TREEDEPS1 copies
  (npm's `node_modules`, `Trees/SessionTrees/TreeDependencies.cs:85-136`). A name never authorises a deletion: a path is kept only where it is
  ignored and under the list, and a take deletes nothing on the list. The unification is a dated note on D195 point 3, D102's TREEKEEP1 and
  D51's TREEDEPS1 (row b), as the brief §10 point 4 asks.
- **npm, per project after the switch (O-11):** **fresh** where the slot's `node_modules/.package-lock.json` is not older than the project's
  `package-lock.json`, npm's own staleness rule and TREEDEPS1's (D51:57-60): the agent is told to skip `npm ci` there; **stale, and the
  checkout's install is at the slot's lockfile byte for byte**: replaced by TREEDEPS1's copy, the old folder renamed aside within the slot and
  deleted never following a link (`TreeSetAside.DeleteNeverFollowing`), under D195 point 3's reason; **stale otherwise**: the agent is told the
  lockfile changed since its dependencies were installed and to run `npm ci` there. A switch that changes a lockfile writes it, so its time
  moves past the install's.
- **`bin/`, `obj/`, `dist/`, `.angular`, `wwwroot` and the rest:** kept; the agent is told that earlier builds' outputs are kept so builds are
  incremental, and that a kept output is no proof that this commit builds (X-11).
- **What a move costs (this design's own, §1.1 row 1):** a cache that holds an absolute path may read as stale after a rename and be rebuilt:
  .NET's restore files in `obj/`, which NuGet checks by the project's path, and the web build's caches. How much of the build saving survives a
  move is unmeasured; row b measures a .NET build and the web build in a slot moved to a new path against one built in place and one fresh.
  npm's `node_modules` holds no absolute path but its workspace junctions, which §5.3 step 3 makes again.
- **TREEGONE1's remake where no slot is free** copies TREEDEPS1's where its gate passes (X-11; today it copies nothing, `TreeReturns.cs:105-109`),
  a dated note on D51's TREEGONE1 (row e).
- **Rejected:** copying at every take, 228 MB for nothing (D51:85-88; O-11); telling the agent to skip `npm ci` after a lockfile changed, which
  leaves a broken install; keeping a stale `node_modules` untouched (§1.1 row 10).

## 12. Git's rules and Windows

- **A branch checked out elsewhere.** A line's branch is only ever checked out at its own path, so never in two slots; a returning line whose
  branch the checkout has checked out is held, said, as TREEGONE1 holds it (`TreeReturns.cs:80-85`). Never `--force` or
  `--ignore-other-worktrees` (D51:157-158).
- **The lock reasons:** the four (an opening's, a session's, a pass's, a person's: survey §5) and `daoris kept tree` (§2.4); only Daoris's own
  are lifted, and a person's never.
- **`daoris/`** stays on every line's branch, so every proof's `--exclude=daoris/*` holds (`TreeFollows.cs:15-19`, `:61-68`); row i's kept refs
  sit outside `--branches` and `--remotes`, and are parentless.
- **The start window and the ledger's key:** `TreeLock` unchanged; the claim keeps two takes off one slot; the ledger's key is the line's path.
- **`git worktree move`** refuses a locked tree unless forced twice, and a tree holding a submodule (git's documentation): the pool lifts only its
  own lock first, and a nested repository holds the slot before (§5.2 step 3). It is a rename, so a handle open without delete-sharing refuses
  it: `busy`, as the set-aside's rename (`TreeSetAside.cs:196-197`).
- **A switch refused by a held file:** `stuck` (§5.4); row b measures what it leaves with a file held open without delete-sharing and git's
  standard input closed, so Git for Windows never asks (O-12).
- **Long paths:** a slot's path is today's tree's, and every git call keeps `-c core.longpaths=true` (`Trees/WorkingTree.cs:974-981`).
- **Junctions in a kept `node_modules`:** a rename moves a junction as an entry and follows none; the ones leading into the old path are made
  again (§5.3 step 3); a retired slot leaves by the set-aside's move and the purge's never-following delete (D51:105-118).
- **What may hold a file:** a session's processes end with its job before its end reads the slot (ORPHAN1, `Platform/ProcessJob.cs:6-32`); a
  heavy run's builds run with `MSBUILDDISABLENODEREUSE=1` and end their job before the run lets its slot go (`Sessions/Heavy/HeavyRun.cs:47-51`);
  a served review and a terminal hold the slot (§7); a scanner's hold on a just-written file reads as `busy` or `stuck` and is tried again.

## 13. D51's rejected alternative and rule 2

D51 rejected *one directory switching branches* because it *serialises exactly what needs to run at once and destroys the person's working
state on every switch* (D51:47-48). The pool keeps both reasons. Nothing that runs at once is serialised: N slots serve N lines, and a start
past them opens a tree as today. **And no directory switches between lines at all**: a directory is one line's for good, and a slot changes
branch only once it has been moved into the next line's directory, released and clean, never the person's checkout (rules 1 and 5). Working
surface rule 2's *named for the session, never reused* (`docs/2026-09-21-working-surface-design.md:77-82`) holds of the branch and of the path;
what is reused is a working copy's files. Rule 4's *a fresh tree holds nothing git does not track* (`:86-90`) holds of every tree opened fresh,
and a taken slot's sentence says what it keeps.

The note D51 takes (row b):

> *TREEPOOL1 (on acceptance):* rule 2's *never reused* is the branch's and its path's: a line's tree is named for its branch, and no other line's
> work is ever in it. A repository keeps a number of working copies and moves a free one to the path of the line that takes it, where a
> non-forcing switch brings it to the line. Rule 4 reads: a tree holds nothing of another line's that git tracks; what it holds untracked is the
> kept dependency and build folders, and the sentence that hands it over says which, and whether they match its lockfiles. The tree stays the
> unit of exclusion and the ledger's key. The rejection of one directory switching branches stands: no directory holds two lines.

## 14. Records and older builds

| Record | What the pool writes | How a build that does not know the pool reads it |
|---|---|---|
| The service's `sessions.tree` | the line's path, as today | as today: the line's own tree, or gone and remade by TREEGONE1 on its branch (`TreeReturns.cs:61-109`), without the slot's dependencies, which its note says |
| A slot free at a line's path, locked `daoris kept tree` | — | its tidy refuses a locked tree (`TreeSetAside.cs:84-88`); its let-go lifts only a session's lock (`SessionLocks.cs:126-134`); its strays leave a tree git lists (`TreeStrays.cs:150-153`); its press's removal refuses a locked tree; its resume of that line goes on there, in the line's own tree (`TreeReturns.cs:58`), `HoldAsync` reading the lock as another's and taking none (`SessionLocks.cs:65`), so the session runs under the pool's lock and its end lets nothing go |
| A take in flight | the opening's lock with a live process | its strays leave it while the process runs (`TreeOpenings.cs:44-45`); killed, it is a cut-off opening, moved aside only where its branch holds nothing another ref does not (`TreeStrays.cs:181-201`) |
| `work_records` | today's shape and route | as today |
| `session-branches.json`, `landings.json`, auto-landings, manifests | today's | as today |
| git's registrations | slots, their names stable, their paths following lines | as worktrees |
| The agents' homes | the line's path | as today |
| The opening notes | new codes | an unknown code reads by its English; the path is cut wherever it travels (`Core/Sessions/SessionNote.cs:9-17`) |
| The store's new tables | new | builds of z-i and z-iii never open them (foundation §13.3) |
| `driver.json`'s `keptTrees` | a new key | kept unread; `trees` still opens `s-` trees |
| `refs/daoris/kept/` (row i) | parentless commits | its `AnyRef` would count one as a holder (`ContentHolds.cs:432-433`), but a parentless commit is never listed by `--contains` (`:379-384`), so no proof reads it as holding a branch |

A tree a build without the pool opens or remakes is read by a pool-aware build as a legacy tree: today's rules, never taken into the pool.

**What is left** (compatible-builds' *say what is left*): a build without the pool running beside a pool-aware one on the same home (a second
install) can race a return of a line to its path with its own remake there; git refuses the second (the folder or the registration already
there), and it is said. And such a build's resume of a line whose slot was taken runs without the slot's dependencies and without work row i
kept aside, as its TREEGONE1 note says of the first; the pool-aware build lays the work back at the line's next return.

**Rejected:** a second session family for pooled lines (X-14), and the slot's own path in `sessions.tree` (O-14), both in §1.1 row 1.
compatible-builds asks for a new table for rows an older build must not reach (`.claude/knowledge/compatible-builds.md`); a record whose path
still names only its line is a row it may reach.

**Proof:** today's code paths held over a home the pool wrote: `TreeReturnsTests` and `SessionMessagesTickTests`' TREEGONE1 ticks with the pool
off (row e), and the deployment rehearsal running the build before over a pooled home: a resume, a tidy and a press.

## 15. The words, en and zh

A stored shape is never changed in place (`SessionTrees.cs:443-444`): `started.tree` and `started.tree-copied` keep their words for every tree
opened fresh, a slot's first making and a tree past the count among them. Codes carry the branch, the base, counts and why; no path and no
account (X-15). Each is one line in the head, within its budget (`docs/2026-10-20-ui-ux-design.md:199`).

| Code | en | zh |
|---|---|---|
| `started.tree-kept` | Took a kept tree of `{{repository}}` for {{branch}}, from {{basedOn}}: its files are the line's, and it keeps what earlier work installed and built ({{kept}}). | 已为 {{branch}} 取用 `{{repository}}` 保留的工作树，基于 {{basedOn}}：文件与主线一致，并保留先前安装和构建的内容（{{kept}}）。 |
| `started.tree-kept-fresh` (one per npm project) | Its dependencies in `{{project}}` match its lockfile: `npm ci` is not needed there unless a lockfile changes. | `{{project}}` 中的依赖与锁文件一致：除非锁文件改变，否则无需在其中运行 `npm ci`。 |
| `started.tree-kept-stale` (one per npm project) | `{{project}}`'s lockfile changed since its dependencies were installed: run `npm ci` there. | `{{project}}` 的锁文件在依赖安装之后改变了：请在其中运行 `npm ci`。 |
| `started.tree-kept-built` | Earlier builds' outputs are kept, so builds there are incremental; a kept output is no proof this commit builds. | 先前构建的产物已保留，构建是增量的；保留的产物不能证明当前提交可以构建。 |
| `started.tree-back` | Went back into its tree, where its branch `{{branch}}` still was. | 回到了它的工作树，分支 `{{branch}}` 仍在那里。 |
| `tree.returned` | Its tree had been taken for other work, so Daoris brought a kept tree back to its place, on its branch `{{branch}}`, where its commits are; what earlier work installed and built there is kept. | 它的工作树曾被其他工作取用，所以 Daoris 把一个保留的工作树带回原处，放到它的分支 `{{branch}}` 上，提交都在那里；先前安装和构建的内容已保留。 |
| `started.tree-past-kept` | Every kept tree of `{{repository}}` was in use, so it opened a tree of its own, as before. | `{{repository}}` 保留的工作树都在使用中，所以它像以前一样打开了自己的工作树。 |
| `ended.output` | Done with output: {{count}} commit(s) on `{{branch}}`, ready to land. | 已完成，有产出：`{{branch}}` 上有 {{count}} 个提交，可以落地。 |
| `ended.no-output` | Done without output: no commits and nothing uncommitted. | 已完成，无产出：没有提交，也没有未提交的内容。 |
| `ended.uncommitted` | Done, with work it did not commit ({{count}} path(s)): its tree stays its own until that work is committed or discarded. | 已完成，但有未提交的工作（{{count}} 个路径）：在提交或丢弃之前，它的工作树仍归它所有。 |
| `tree.kept-held` | Kept tree `{{name}}` stays with its work: {{why}}. | 保留的工作树 `{{name}}` 仍归它的工作所有：{{why}}。 |
| `tree.kept-stuck` | Kept tree `{{name}}` could not be brought to the line, and git said: {{said}} | 保留的工作树 `{{name}}` 无法带到主线，git 的说法是：{{said}} |
| The Projects row | Trees kept: {{count}} · in use {{held}} | 保留的工作树：{{count}} · 使用中 {{held}} |

The short ends the session's head shows are *Done with output* (已完成，有产出) and *Done without output* (已完成，无产出), Codex's Chinese, which
matches the catalogue's 已完成 (`note.landing.nothing`). The glossary gains **kept tree** (保留的工作树): *a tree a repository keeps and hands from
one line of work to the next, brought to the line each time*; *tree* (工作树) keeps its entry, its meaning reading *a working tree beside the
checkout that a line of work holds*. Git's refusal text and an author's words are kept verbatim.

## 16. The install's trees today

- **None becomes a slot and none goes by this work** (both). The install's `s-` trees under `<home>/work/trees` and the folders under `.tidied`
  keep today's rules: the tidy's move aside, KEEPSIZE1b's hour and days, the person's press.
- **A line that began in an `s-` tree goes on there** while its tree stands. Once its tree is gone, a pool-aware build brings a free slot to
  its path (§3.3), as TREEGONE1 remakes it today: so a legacy line's path may come to hold a slot, never an `s-` tree adopted in place.
- **The cost while they drain:** legacy trees and kept slots both exist for one repository; row g's measurement says which bytes are which (X).
- **What is the owner's:** nothing here removes or converts them, so nothing is asked about them; opting the install's repository in is asked
  (§18 item 2).
- **Rejected:** adopting idle `s-` trees as slots. In this design it would harm no line (a slot stays at its line's path until taken), but it
  reaches folders the install holds to save one install per slot once.

## 17. The rows, in build order after mod-x

Each row is its own branch, after FOUNDATION1's mod-x has landed and on files no open branch holds (D212 §14.6), every new type written in
`Workplace/`. Each lands with every gate of `daoris.gates.json` green, runs the brief §9's suites whole wherever it touches their part
(`SessionTreesTests`, `TreeDependenciesTests`, `TreeReturnsTests`, `SessionLocksTests`, `SessionTreeLockTickTests`, `StartTreeCloseTests`,
`PlannerTests`, `BranchTidyingTests`, `BranchTidyingFolderTests`, `CleanupLeftoversTests`, `KeepingTests`, `KeepingSlimTests`, `KeepSizeGitTests`,
`KeepSizeStopsGitTests`, `SystemSpaceTests`, `EntryLandingsTests`, `AutoLandingTriesTests`, `AutoLandingRulesTests`, the service's
`WorkRecordStoreTests` and `SessionSyncTests`, the page's `identity.test.ts` and `noteLines.test.ts`, and the tick tests that start real sessions
in fresh trees), with the git half of the real-process suites and the family and deployment rehearsals run serially, and leaves Daoris working
as it did for a person who sets nothing. **A row marked ⚖ resets or removes something, or changes which tree a resume or a landing reads, and
is built only after both makers' reads find nothing blocking in it.** *Row a* is TREEPOOL1a, and so on. The first three prove the shape: the
store and its census (a), the take over git with nothing wired (b), the readers that meet a gone tree (c).

### a. The slots named beside today's trees, and the census. Nothing is pooled.

- **Changes.** `KeptTree`, `TreeClaim`, `LineEnd` and `TreePool`'s store reads and writes; `SlotChoice` and `SlotJudgement`, pure, in `Domain/`;
  the store's migration step (`kept_trees`, `tree_claims`, `line_ends`, every column required, after the last step at build time, the writer
  stamp unchanged, as `bindings` was added, `Store/DesktopStore.cs:107-117`); `PoolReadersCensusTests`, a by-name source scan in
  `ConfigSeamTests`' manner listing §3.2's re-keyed sites as *not yet moved*, each with its row. Nothing calls them. D212 takes a note that
  §13.3's workplace family gains these tables.
- **The person sees.** Nothing.
- **Proof.** The store's cases: a build of z-iii never opens the tables, the stamp unchanged, a migration interrupted at each step leaving the
  store as it was; one claim per slot with the shell, a headless `--share` loop and a verb racing; a dead run's claim taken over and a paused
  live one never, §13.4's three cases with live processes; `SlotChoice` and `SlotJudgement` over every row of §5.1 and §5.2; the census passing,
  and failing on each kind of planted site; `StructureTests` with no new census edge.

### b. ⚖ Taking, returning and ending a slot over git, unwired.

- **Changes.** `TreePool.TakeAsync`, `ReturnAsync`, `EndInPlaceAsync` and `RetireAsync`; the judgement's git reads (§5.2); `git worktree move`,
  the links made again and the non-forcing switch (§5.3); `daoris kept tree`, `SessionLocks.HoldAsync` taking it over and the let-go putting it
  back (§2.4); the one build-folder list and its named subsets, with dated notes on D195 point 3, D102's TREEKEEP1 and D51's TREEDEPS1; D51's
  note (§13).
- **The person sees.** Nothing.
- **Proof** (`TreePoolGitTests`, `TreePoolStopsGitTests` and a Windows `Process` class):
  - **The brief §8's first proof, its clean half.** Line A commits on its branch, installs an ignored `node_modules` and `bin/`, and ends with
    nothing uncommitted; line B takes the slot at the line. B's path holds the line's tracked files and A's ignored folders and none of A's
    tracked work; A's branch holds its commits; A's path is empty; `BranchOfTree(B's path)` is B's branch; A's `GrownBranch` is unchanged; the
    slot's registration name is unchanged.
  - **No work lost**, each case seen failing first with its guard taken out: no take of a slot holding an untracked file, a tracked edit, a
    half-done merge or rebase, an `assume-unchanged` path, a nested repository, a person's lock, a link Daoris did not make, a HEAD moved since
    its let-go, or a commit no ref holds; a write planted between the judgement and the move through a seam (as `BeforeRefDeleted` is,
    `Trees/SessionTrees/TreeContext.cs:40`) holds it.
  - **The end in place.** A landed line's tip held, its slot detached at it, its branch deleted at the commit judged and its record posted with
    the line's path; a branch checked out anywhere never deleted; a no-output end posting no record.
  - **A stop at every phase** (`judged`, `moving`, `moved`, `switched`, before the record) leaves a slot the next judgement reads right; a killed take is
    read by the strays as a cut-off opening and moved aside only where its branch holds nothing another ref does not.
  - **Windows.** `git worktree move` refused by a file held open without delete-sharing (`busy`), the slot unchanged; a switch refused by a held
    file (`stuck`), what it left recorded in the row's note; a workspace junction in a kept `node_modules` leading into the old path made again
    into the new, one leading elsewhere holding the slot; a path of 250 characters.
  - **Measured, written in the row's note:** a .NET build of a test project and the web build in a slot moved to a new path, against one built
    in place and one fresh.

### c. ⚖ The sites that meet a gone tree learn that a slot can come back; the opened moment; a taken line's review. Inert until d.

- **Changes.** The seams §3.2 marks row c: `ContentHolds.HoldsAsync`'s *opened* from the line's `GrownBranch.At` for a slot (a removal's
  judgement, hence ⚖); `Continuation.Judge`'s `treeStands`, `ConversationTrees.Stands`, the chat runner's gone-tree refusal
  (`ChatRunner.cs:630-633`), the briefed move's (`BriefedMove.cs:105-107`, `:188-190`) and the limit move's gates (`LimitMove.cs:75`, `:261`)
  asking the pool *stands, or can be brought back*, which answers by the disk alone while no pool is on; `SessionReviews` reading a pooled line's
  branch in the checkout when its tree is gone and unlanded.
- **The person sees.** Nothing; every legacy answer is the same.
- **Proof.** Per seam, a fixture whose record names a line whose slot was taken answers *can be brought back*, and the legacy fixture answers as
  before; the review's graph held equal to its chain, its oracle (`SessionReviews.ChainReadAsync`, `SessionReviews.cs:67`); the census shrinks by
  these sites.

### d. ⚖ The pool on per repository, off by default.

- **Changes.** `keptTrees`, its verb, `SET_TREES`' `kept` and the Projects control; the driven start (`Driver.cs:2186-2215`) and the
  conversation's (`ChatRunner.cs:635-651`) taking from the pool (§5.1), else opening as today; the end (§6) and `line_ends`; the holders composed
  by Loop (§7), a due landing and a terminal among them; the look's takeover of a dead run's claim (§2.3); the tidy's end in place for a pooled
  line (§10); the press retiring a slot (§9); the words (§15); the Disk space `kept-trees` kind; dated notes on D88 and D195.
- **The person sees.** Only where a count is set, which in this row only tests do.
- **Proof.** The brief §8's first proof as a tick test on a stand-in harness that records its working folder, as `SessionMessagesTickTests`'
  TREEGONE1 ticks do; each holder of §7 holding; a start past the count opening a tree as today; `CapCountTickTests` and `LookParallelTests`
  with the pool on; the brief §9's suites whole; the family rehearsal with one pooled repository; `identity.test.ts` and `noteLines.test.ts`
  with the new codes.

### e. ⚖ Lines coming back through the pool.

- **Changes.** `TreeReturns.EnsureAsync` bringing a free slot back to a pooled line's path on its branch (`tree.returned`), else today's remake,
  now copying TREEDEPS1's where its gate passes; the go-ons and moves bringing a slot back before they go on (c's seams, switched on); a landing's
  `return` claim (`TreeLands.cs:196-204`, `:918-926`); the no-output branch grown again; dated notes on D51 (TREEGONE1), D79, D167, D178 and D189.
- **The person sees.** With a count set, a line whose slot was taken goes on where it was, said.
- **Proof.** A resume whose slot was taken finds its conversation: the stand-in sees the same folder and the same id; words going on; a further
  part on its line, and one alongside; a limit move held in its slot; a bring-forward; an Accept of a line whose slot was taken; a no-output
  conversation's go-on growing its branch again; `HandOverQuestTickTests` and `StartInSyncTickTests` whole; `TreeReturnsTests` and the TREEGONE1
  ticks with the pool off over a home the pool wrote, as a build without the pool reads it; the deployment rehearsal running the build before
  over a pooled home (a resume, a tidy, a press). **Installed, the parent's:** one Claude Code session in a pooled repository, its slot taken by
  a second start, then resumed: the conversation found (one session, one resume, nothing forced to a limit), on the owner's account under §18
  item 5.

### f. ⚖ Lowering, and a repository leaving.

- **Changes.** `retiring`, raising, a repository leaving, a checkout gone (§9).
- **The person sees.** The Projects row's *n more retiring* where a count was lowered with slots held.
- **Proof.** A lowered count retiring a free slot (moved aside, `unmarked`, the days, the slim's hour) and a held one at its let-go; one holding
  work no ref holds staying, said; `KeepSizeGitTests`, `CleanupLeftoversTests`, `KeepingSlimTests` and `SystemSpaceTests` whole.

### g. ⚖ On for the install's repository, measured.

- **When.** After rows a to f and the owner's yes (§18 item 2). The parent's, on the install.
- **Changes.** `keptTrees` set for the install's repository; no code.
- **The person sees.** Sessions in that repository start in a kept tree, said (`started.tree-kept`, `tree.returned`), and the Projects row's
  count.
- **Proof, measured before and after a day of the install's ordinary work:** the home's `work/` bytes split into legacy trees, kept slots, trees past
  the count and set-aside folders, from the Disk space card's kinds; the time from a start's record to its first turn over twenty starts, the
  take, the move, the switch and the dependencies apart; a .NET build in a moved slot; how often a start went past the count; how often a line
  was brought back.

### h. ⚖ The default flips.

- **Changes.** A repository with trees and no count keeps cap + 1 slots. After a week of row g with no finding; the steward's call, told to
  the owner, since it carries out the owner's direction.
- **The person sees.** In every repository with trees, sessions starting in kept trees, and nothing new to set.
- **Proof.** The tick and rehearsal cases of rows d and e with the default rather than a set count; a repository with trees off unchanged
  byte for byte; `DriverConfigKeepsTests` with and without the key.

### i. ⚖ Uncommitted work kept aside off its branch. Only on the owner's yes (§18 item 3).

- **Changes.** §5.5: `kept_work`, the keep, `AnyRef` excluding `refs/daoris/kept/`, the lay-back; D164's note.
- **The person sees.** A tree whose session left uncommitted work goes back to the pool, said in `ended.uncommitted`'s place by a line naming
  the kept ref; the work laid back when the line returns.
- **Proof.** The brief §8's first proof whole: A leaves an untracked file and a changed tracked file; B takes the slot at the line; neither is in
  B's path; both are in `refs/daoris/kept/…`; A coming back at the same base has them laid back. The keep commit is parentless and never listed by
  `for-each-ref --contains <tip>`; past the bounds the slot stays A's; a write after the keep holds the slot; on Windows a file held during the
  reset loses nothing of A's.

### j. ⚖ A finished line's scratch outside the list cleared at a take. Only on the owner's yes (§18 item 4).

- **Changes.** §5.2 step 5's carried scratch cleared, after the finished line's record is kept and named in it; D195's note.
- **The person sees.** A new line's tree without the last one's logs and probes.
- **Proof.** A finished line's `_fixtures/` and `local/` gone from the next line's slot, its record naming them as KEEPSIZE1b's record names a
  set-aside tree's scratch; a line that may come back never cleared.

**How each input folds in.** FOUNDATION1: every row after mod-x; the tables Workplace's family by §13.3; no port added. D51, D79, D88, D102,
D167, D178, D189 and D195: a dated note each, in the row that changes them; D164 only in row i. SWITCHCTX1: nothing taken. D115: the default
reads `laneCap` once DEV6 builds it. LINESTOGETHER1, ACCEPTCONFLICT1 and REVIEWFLOW1: unchanged; a head merging others does so in its own path.

## 18. What is the owner's

D191 point 3: a change of the product's direction, a spend beyond the development's own runs, a sign-in, a publish, a push or a history
rewrite, and anything that loses the owner's data, which the brief §2 reads as removing a tree, a branch or a folder the install holds today,
or resetting a tree that holds work.

1. **What the count means.** *What it is:* the direction says a repository keeps a number of slots, at most one per session it may run at once,
   and removes trees only when that number is lowered or the repository leaves. This design reads the number as **the trees a repository
   keeps**: when every kept tree is held, a start opens a tree of its own as today, which today's tidy removes once its work is done. *What a
   yes does:* nobody ever waits for a tree, and a busy day still makes a few trees past the count. *What a no does:* the number bounds the
   sessions that run at once in trees, the person's conversations included, and a start past it waits until a tree comes free (Codex's
   reading). *Where to look:* §8, §5.1 step 5, §1.1 row 4.
2. **The pool on for the install's repository** (row g). *What it is:* the repository the install works in keeps cap + 1 trees, three today,
   and a session there takes a free one, moved to its own path and switched to its new branch, instead of opening a fresh one; a tree is taken
   only where it holds nothing but what git tracks at a ref and the ignored build folders. *What a yes does:* sets `keptTrees` for that
   repository and starts row g's day of measurements. *Where to look:* §5, §7, row g, and the proofs of rows a to f.
3. **Daoris keeping a let-go line's uncommitted work aside, and clearing it from the tree** (row i). *What it is:* a parentless commit of the
   tree's uncommitted and untracked files, on `refs/daoris/kept/…`, made by Daoris off the line's branch, where D164 point 1 has Daoris commit
   nothing; compared again right before the tree is reset for another line, and laid back when the line returns. *What a yes does:* trees whose
   sessions left uncommitted work go back to the pool. *What a no does:* such a tree stays its line's, said, until a session commits the work or
   the person discards it, and the pool opens another. *Where to look:* §5.5 and row i's proof.
4. **A finished line's ignored scratch cleared when its tree is taken** (row j). *What it is:* KEEPSIZE1b's decision that a finished session's
   own scratch (the ignored files it wrote outside the build folders: logs, probes, notes) goes with its tree (D195:503-505), applied when its
   tree is taken by another line rather than an hour after its record. *What a yes does:* the next line starts without the last one's
   `_fixtures/`, `local/` or logs. *What a no does:* they are carried into the next line's tree, kept, never cleared. *Where to look:* §5.2 step
   5, §10, row j.
5. **One real resume on the install** (row e's installed proof). *What it is:* one Claude Code session and one resume in a pooled repository,
   on one of the owner's accounts, to prove the conversation is found once its tree was taken and brought back. *What a yes does:* runs it once,
   bounded to one session and one resume with nothing forced to a limit, unless an authorization the repository records already covers it.
   *Where to look:* row e.

**Not asked:** the install's trees and set-aside folders, since nothing here removes or converts them (§16); lowering a count, which is the
person's own act and whose retired trees wait D195's days; a publish, a push, a history rewrite or a sign-in, none of which any row needs.

## 19. Rejected

| Rejected | Why |
|---|---|
| A slot at a stable path of its own (both proposals) | §1.1 row 1: a shipped build goes on in it by `Directory.Exists` on another line's branch, or the service needs a second session family to hide it |
| A second session family in the service for pooled lines (X) | §1.1 row 1; compatible-builds asks a new table only for rows an older build must not reach, and a path that stays its line's needs none |
| A durable lease per occupation (X) | §1.1 row 2 |
| git's lock alone, a lease file, a lease in the service | not a test-and-set across a read, a judgement and a switch; a file needs the store's dead-run test anyway; the service would learn a machine path (D47 §4; foundation §13.7) (O-1) |
| A permanent pool lock reason (X) | §1.1 row 7 |
| `reset --hard` and `clean -fd` in the first build (O) | a reset of a tree that may hold work before the owner's yes; a clean slot needs neither |
| `clean -fdx`; a forced switch; `worktree add --force` or `--ignore-other-worktrees` | loses the kept folders; overwrites what was there; two trees on one branch (D51:157-158) |
| Loose files moved to a kept area (X) | §1.1 row 3 |
| Daoris committing the leftovers on the line's branch | lands what the agent never chose to commit, and turns a no-output end into output (O-6) |
| The count in `daoris.json`, or in the registry | D115:58-59's reason, and a count is a machine's; the registry travels |
| The count in a store table of its own (X) | §1.1 row 5 |
| The cap at opt-in as a stored default (X) | §1.1 row 6 |
| A start waiting for a tree (X) | §1.1 row 4; the owner's question (§18 item 1) |
| New record tables, refs or a route for a finished line (both) | §1.1 row 9 |
| Removal by age | the direction |
| `git worktree remove` of a retiring slot | TREEKEEP1 refuses ignored paths only the tree holds; the set-aside's move is the path the tidy proves (O-9) |
| A branch named for a slot (`daoris/t-2`) | landings, recovery refs and traces would run two lines under one name (O-2) |
| A junction per line leading to a stable slot | `git worktree remove --force` follows a junction (D51:74-77); what agents and the tree guard read through one is unverified (O-4) |
| Copying an agent's conversation files between folders | writes in an agent's home (D189 point 5); SWITCHCTX1's carry is not accepted (O-4) |
| A new part handed the brief whenever the line's slot was taken (both, D189 amended) | §1.1 row 11 |
| Copying dependencies at every take | 228 MB for nothing (D51:85-88; O-11) |
| A stale `node_modules` kept untouched (X) | §1.1 row 10 |
| Adopting the install's idle trees as slots | §16 |
| A no-output branch gone for good (X) | §1.1 row 12 |

## 20. What stays unmeasured, and what this design's checks do not cover

- **How much of the build saving survives a move**: .NET's `obj/` restore files and the web build's caches hold absolute paths and may be
  rebuilt after a rename (§11). Row b measures a .NET build and the web build in a moved slot against one in place and one fresh. If most of it
  is lost, the trade of §1.1 row 1 is weighed again by a later round with that number, never silently.
- **`git worktree move` on Windows**: its refusal with a handle held, the registration name it keeps, and what a refused switch leaves: row b.
- **Whether Claude Code reads anything of a directory but its path when it resumes**: row e's installed proof (§18 item 5).
- **How often every kept tree is held** so a start goes past the count, and how often a line is brought back: row g.
- **The cost of making links again** after a move in a large `node_modules`: row b, with TREELINK1's bound of a million entries.
- **An agent other than Claude Code and Codex that keys a conversation by path**: none is read here; a plugin's door is read by its provider's
  contract (D213), which this design does not change.
- **A build without the pool beside a pool-aware one on one home**, racing a return: said (§14), not measured.
- **Whether a folder keeps its creation time across a rename**: assumed, and no longer read for a slot (§3.2).
- **What this round's checks do not cover.** It is a document: `npm run verify` and the documentation checks prove its routing and shape, not
  its claims about the code. Its claims were read at `f8fbb1bb7` for every line §0.2 and §2 to §17 cite; the survey's and the proposals' other
  lines are taken as they cite them, the source between their commits and `f8fbb1bb7` having changed in none of the files they cite but
  `LimitMove.cs` (§0.2 row 4). git's own behaviour (`worktree move`, `switch --no-overwrite-ignore`) is taken from its documentation, not run.
  Nothing was run. Both makers' reads check it next.

## 21. Rounds

| Round | Written | Read | Answered |
|---|---|---|---|
| 1 | 2026-10-26, Claude Opus 5.5, from main at `f8fbb1bb7`: the synthesis of Codex's and the independent Opus reader's sealed proposals; every question of the brief answered (§1); each difference chosen with its reason and the case the other gets wrong (§1.1); one choice neither proposal's, the slot moved to its line's path (§1.1 row 1, §2); the rows in order with their proofs (§17); D214 proposed | owed: Codex's, and the independent Opus reader's as a foundation read | — |
