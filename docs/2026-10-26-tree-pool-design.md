# A repository's session trees kept and reused, each session's work a branch (TREEPOOL1, 2026-10-26: round 4, proposed 2026-11-07)

**Standing (round 4, proposed under D191, D214):** **The independent Opus reader read round 3** at `23958cb903`
(`docs/2026-10-26-tree-pool-reads.md`, *Opus (independent), round 3*), the only read of it: 14 findings, 1 blocking, no case in which the pool
loses work, and another round asked, scoped to its F1: no take saved the first build, since a slot moved to each line's path cannot carry the
outputs that name their path, and round 3 measured its saving against `npm ci`, which TREEDEPS1's copy already avoids. **The owner chose on
2026-11-07 that a slot keeps a fixed path**, so its build outputs stay valid and a reused tree skips the first build. **Round 4 is written by
Claude Opus 5.5 from main at `75a4b694be`**; every finding was checked against the code there and is answered in §24, one row each, and each
passage of §0 to §20 it changed is marked *(R4-Fn)*, or *(R4)* where the fixed path alone changed it. **The slot**: a kept linked worktree at a
fixed folder of its own, `k-<n>`, beside today's trees, never renamed; a line works in it, so the parking name, the moves, the relinking and
the exclusive hold at a take are gone (§2). **The record**: a session's record names its line's path, `s-<8 hex>`, as today, a path a pooled
line never has as a folder, and no record names a slot; every reader that reads a path as a line reads right, and any reader that opens a
record's tree as a folder, a build that does not know the pool among them, meets a pooled line as a gone tree, never as another line's work
(§3.1, §14). **The builds**: a slot's `node_modules`, `bin/`, `obj/` and the web's build stay in it as the slot's own, valid at its path, and
serve the next line incrementally; a kind a tool runs in place of the line's sources (this repository's CLI `dist/`) never crosses lines, and a
slot past its size bound is renewed (§11). **The saving, measured here**: a fresh worktree of this repository builds its two desktop test
projects cold in 98 s and, with nothing changed, in 12 s; after a switch over three hours of main, in R4_INCR; the web's build runs whole either
way; TREEDEPS1's copy (about 6 s) already saves `npm ci`. So a take saves most of the first .NET build and the checkout, and nothing of the
web's (§11). **What it costs**: a line coming back in another slot finds no Claude Code conversation there and goes on from the brief, D189
amended (§4); every reader that opens a record's tree as a folder asks the pool (§3.2, row a's census); and the next line builds over another
line's outputs (§11). **§18 item 4** is restated for slots whose builds are their own. **Both makers read round 4 before any of it is accepted,
and nothing is built until then.** FOUNDATION1's mod-x and the two Part B rows round 3 waited on, 1d-i's keyed open and f-i's move record, have
landed *(R4-F14)*; the rows marked ⚖ are built only after both makers' reads find nothing blocking in them. Nothing here is built, spends an
account, signs in, publishes or deletes.

**The earlier rounds.** Rounds 1 to 3's standings and *Read at* paragraphs, and §22 and §23, are in the reads file since round 4, under *Moved
from the design at round 4*; §21 says what each round did.

**Read at** main `75a4b694be` for round 4. Since round 3's `3dd134cf3`, FOUNDATION1's mod-x moved `Trees/` into `Workplace/`, flat, and
CODESHAPE1 split the hubs, so a cited `Trees/…/X.cs` is `Workplace/X.cs`, `Loop/Driver.cs`'s start is `Loop/Run/Tree/GrowTree.cs` and the
conversation's `Loop/Chat/ChatStarts.cs` *(R4-F2)*. Every line round 4 writes was read at `75a4b694be`, the lines §24 names among them:
`Workplace/TreeReturns.cs:57-65` (TREEGONE1 answers *stands* for any folder git lists, whatever branch it holds), `:63` (the binding check);
`Workplace/TreeSetAside.cs:29`, `:86`; `Loop/Run/Tree/GrowTree.cs:28`, `:56-58`, `:96`, `:110`, `:130`; `Loop/Run/RunState.cs:200-202`;
`Workplace/TidyHolders.cs:30-37`, `:99-108`; `Workplace/TreeLocalInputs.cs:5-8`; `Workplace/JournaledDeletes.cs:31-33`;
`Tests/Workplace/JournaledDeletesTests.cs:118`; `Workplace/ActRuns.cs:76`, `:163`; `Workplace/ActJournal.cs:98`, `:215`, `:396-411`;
`Workplace/TreeLock.cs:23-41`, `:127-141`; `Workplace/LandPerformer.cs:97`, `:146`; `Workplace/TreeFolders.cs:13-15`;
`Workplace/IntegrationReplays.cs:76-78`; `Workplace/TreeStrays.cs:77`, `:147`; `Tests/Workplace/CheckoutWritersCensusTests.cs:5-11`. A citation
round 4 does not rewrite stands as round 3 read it at `3dd134cf3`, through that map: the round-3 reader found each moved, none changed in
meaning (its §1), and each row re-reads what it touches at its own base. Paths keep round 1's aliases: a bare path is under
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, and `Tests/` is the driver's tests.

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

**Round 2: round 1's own claims the reads found wrong, checked at `f92e95415`.**

| Round 1 said | At `f92e95415` | Found by |
|---|---|---|
| §2.3, §2.4, §14: a take killed halfway reads to every build's strays as a cut-off opening | Only on its own new branch or detached: a tree on any other branch *holds someone's work there* and is kept (`TreeStrays.cs:188-190`), so a take killed after its move and before its switch stood at the new path on the previous line's branch; and a *return* killed under OPENCUT1's lock reads as a cut-off open whose branch the strays delete where another ref holds its tip (`:183-201`, `:227-238`). §5.3 now parks, switches and checks a slot under a name the strays never read as an opening, and OPENCUT1's lock is used only at a slot's first making | C1-1.1, O1-F6 |
| §5.2 step 3: the look's last guards judge links and nested repositories | They let a repository under an ignored path go with its tree (`TreeSetAside.cs:116-124`), and the links they gather serve only the slim's confirmation (`:127-132`). §5.2 step 3 is now a judgement of its own | C1-1.2 |
| §9 and row f: a retired slot's `node_modules` goes at the slim's hour | Only a folder whose manifest reads `moved` is slimmed (`SlimManifest.cs:13-14`; `SetAsideKeeping.cs:87-90`); retirement now writes the slim's own manifest | C1-1.3, O1-F8 |
| §11: npm's `node_modules` holds no absolute path but its workspace junctions, and caches with absolute paths are merely rebuilt | TREEDEPS1 leaves `.cache`, `.vite` and `.vite-temp` behind since each names where it was made (`TreeDependencies.cs:27-28`, `:44-45`); `.venv` is on the list (`TreeScratch.cs:17`) and its launchers and editable installs name their path; NuGet's `obj/*.nuget.g.props` names its assets file absolutely. After a move the old path may hold another line's tree, so these are reads of another line's work, not stale caches | C1-1.4, O1-F3 |
| §5.3 step 2: `git worktree move` is one rename of the folder | Where the destination exists as a directory, git moves the tree *into* it (its `move_worktree` appends the source's name to a destination that is a directory, git's source as the reader cites it, not run here); and it renames before it writes the registration's `gitdir`, so a failure between leaves the folder at the new path and the registration at the old | O1-F1, O1-F5 |
| §10: no manifest is written, its inputs being the store's | None of round 1's tables holds what `FinishedTree` carries (`TreeSweeps.cs:221-230`: the read moment, the tip, grew-from, unit, hold and form, recovery ref, the scratch left, `whole`), and the route keeps the first post of an identity (`Http/Program.cs:1435-1439`), so a retry must keep its first moment | C1-2.3, O1-F7 |
| §7: words waiting hold *until the next look's go-on* | The waiting trees are every record with words waiting (`Service/ServiceClient.cs:2245`), words held for the person's press or for an account's reset among them, which can be long | O1-F11 |
| §7: a standing move holds through Course's `moves` row | No such table stands: the store's steps are 1 to 3 (`Store/DesktopStore.cs:73-129`); a standing move is today's box, `<s>.limit-move.json` or `<s>.hand-over.json` (`Conversations/Chat/LimitBox.cs:24`; `Conversations/HandOver/HandOver.cs:15`), until Part B's `moves` lands | O1-F11 |
| §7: a terminal holds a slot | It is known to the shell's own process alone (`Modules/Windows/TerminalModule.cs:202-218`), so a `--share` loop or a verb cannot read it | C1-2.5, O1-F11 |
| §3.2: *Show it again* after a slot was taken shows as a tidied tree does | It refuses only a tree that is gone (`Review/ReviewDesk.cs:226-229`); where a slot was brought back to the path, it serves that slot's build | O1-F12 |
| §3.3, §5.1 choice 1: the returning line's own slot goes on with no git | A finished line's slot, ended in place detached, answers *stands* (`TreeReturns.cs:56-58`) and would go on detached, in this build and an older one, where today a landed line whose branch the tidy took grows a fresh tree (`Loop/Driver.cs:2119-2127`) | C1-2.7, O1-F9 |
| §2.3: a take's branch is deleted at its start, as an undone open's is | An undone open runs `git branch -D` with no commit check (`TreeOpenings.cs:108`) | O1-F15 |
| §8, §18 item 1: a tree past the count is one today's tidy takes once its work is done | A tree whose branch never moved stays until the person's press (`TreeSetAside.cs:18-20`) | O1-F15 |
| §1.1 row 12: Codex's no-output branch is gone for good | X restores a missing owned branch create-only from its preserved refs (X-3, X-10); the difference is where the licence lives | O1-F13 |
| §11, row b: adding `wwwroot` to the one list changes nothing a person sees | `TreeScratch`'s folders decide what a removal lets go of (`ContentHolds.cs:588-594`), so `wwwroot` there would change today's removals; it now joins only what a take carries | C1-4.2 |

**Round 3: round 2's own claims the reads found wrong, checked at `3dd134cf3`.**

| Round 2 said | At `3dd134cf3` | Found by |
|---|---|---|
| §5.2 step 3: the look's last guards run first | `LastLookAsync` refuses every locked tree, whatever its reason (`TreeSetAside.cs:84-87`), so it would refuse every slot the pool itself locked; §5.2 step 3 now reads the lock by the pool's own rule (step 2) and asks the rest of the last guards apart from it | C2-1.1 |
| §11: `bin`, `dist`, `build`, `out`, `coverage`, `wwwroot` and `__pycache__` are outputs no tool resolves code from | This repository's CLI runs a kept `dist/cli.js` in preference to its sources (`src/Daoris.Cli/bin/daoris.mjs:12-13`), a hazard its publishing already guards against (`tools/desktop-publish.mjs:1260-1262`); a CMake or Ninja `build/` and an `out/build/<preset>` name the source and build folders absolutely; a .NET test assembly under `bin` can carry its content root (this repository's HTTP tests use the testing host, `Daoris.Service.Http.Tests.csproj:16`, as the reader reads that package, not run). Carried outputs run and resolve, so the first build carries none of them | C2-1.2, O2-F1 |
| §0.3, §2.5: no port is added | The cap's verb, which must name the slots a lower cap retires, and the count's door, which must refuse a repository with submodules, are Settings' (`Settings/DriverChoicesCommand.cs:427-438`), rank 3, which cannot read Workplace's tables; a port is needed (§2.5) | O2-F8 |
| §14: D139's rollback cannot reach pooled lines, since they exist only once a pooled build has confirmed | Nothing in round 2 held a take back until the start's confirmation; once row h flips the default, a build's first look would pool before it confirmed. A take now waits for the confirmation to be settled (§5.1) | O2-F15 |
| §2.3: every rename's exclusive hold is honoured by every build's start and tidy | It is, but a landing takes neither it nor any holder §7 names: it takes the tree's landing hold (`TreeLands.cs:420`) and the repository's due-moment hold (`:435`). The landing hold now holds a slot (§7) | O2-F2 |
| *Read at* round 2: every other file unchanged since `f8fbb1bb7` | Row p moved four service and client files since `f92e95415` (*Read at*) | O2-F12 |

### 0.3 The inputs, folded

| Input (the brief §3) | Folded as |
|---|---|
| FOUNDATION1, D212 | The ground. `Trees/` is Workplace (§14.2 row 10, `docs/2026-10-25-foundation-design.md:2342`), so every new type is Workplace's or lower (§2.5). The claim is a store family by §13.3 (`:1970-2001`), in Workplace's family (§13.3's row, `:1987`), with §13.4's claim by process run (`:2042-2066`). Only Settings, below Workplace, needs the pool's answer, by the one port named below *(O2-F8)*; no other port and no other module initializer is added. Every row lands after mod-x (§14.6). Part B's `moves` (the course's family, §13.3, row f-i) is read for a standing move once it lands, today's boxes until then *(O1-F11)*, and both from row d on, since f-i stops writing the boxes *(O2-F14)*; the shell's terminal and a served build reach the pool by a call down into Workplace *(C1-2.5, C2-3.2)*. **One port is added** *(O2-F8)*: Settings' verbs, which must say how many slots a lower cap retires and refuse a repository the pool cannot serve, ask a port Settings declares and Workplace answers by a module initializer installed in Workplace's folder, keeping the three bounds of D212's note of 2026-10-26. **Two Part B rows are dependencies**: row 1d-i's keyed open (`session_opens`, foundation design `:1711`), under which a start's open is made (§2.3) *(C2-2.2)*; and §13.4's dead-run takeover over `store_runs`, which row a builds once in the Store subdomain for this design and row f-i alike, or takes from f-i where f-i lands first *(O2-F14)* |
| D51 and its notes; the working surface | Rule 2 and rule 4 amended by note, the rejected alternative kept (§13); TREEDEPS1's gate read again for a kept slot (§11); TREELINK1's link rule applied after a move (§5.3); TREELOCK1's reason kept for a held slot and its *not covered* closed for a conversation's (§2.4); TREEGONE1's remake becomes, where a slot is free, a slot brought back, its fallback copying TREEDEPS1's (§3.3, §11) |
| D88, D102, D195 (CLEANUP1, KEEP1, KEEPSIZE1a/b) | The tidy's set-aside of a pooled line becomes *the line's finish*: KEEPSIZE1b's own steps and manifest, setting aside everything the slot does not keep, its manifest written before anything moves or the branch goes, then the slot parked *(C1-2.3, O1-F9, C2-2.4)* (§10); the one list of build folders TREEKEEP1's with `wwwroot`, the slim's, the copy's and what crosses a move (`node_modules` alone) its named subsets *(O2-F1)* (§11); D195's kinds gain `kept-trees` (§10); KEEPSIZE1c-f stand |
| D167, D178, D189 | Their rules stand: the entry's tree, the kept take's tree and the resume's tree are each the line's path, which a free slot is brought back to (§3.3, §4); each takes a clarifying note |
| SWITCHCTX1 §1.5 | Read as the reason the slot moves and the line does not: Claude Code files a conversation and keeps its trust by the working directory's path (`Platform/ToolHomeLeftovers.cs:30-31`, `:55`; `Accounts/SignIns/ClaudeTrust.cs:108-125`), Codex by date (`ToolHomeLeftovers.cs:41-42`). Its carry is not needed here |
| D115, DEV6 | The count's default reads `laneCap` once DEV6 builds it (§8) |
| LINESTOGETHER1, ACCEPTCONFLICT1, REVIEWFLOW1 | Unchanged: a landing's steps are today's; a landing whose line's slot was taken has one brought back first (§3.3) |

## 1. The two proposals, question by question

The question numbers are the brief's §7.

| Q | X | O | Chosen |
|---|---|---|---|
| 1 **The slot** | `s-<n>` under `<home>/work/tree-pools/<pool-id>/`; a durable store lease per occupation with a generation; a permanent pool lock | `t-<n>` under `<home>/work/pool/<ws>/<repo>/`, a new home entry; a store claim per step, the record holding the slot after; the lock a session's while held, `daoris kept tree` while free | **Both's place in kind, a numbered slot at a fixed folder of its own, `k-<n>`, beside today's trees, where its lines work; but every record names its line's path, never the slot's (§2.2, §3.1) *(R4-F1)*; O's claim per step, reserving the slot from its choice to its record's open, with its whole intent (§2.3) *(C1-2.2, C1-3.1)*, its open made under FOUNDATION1's keyed open *(C2-2.2)*, its git in a job named for it that the takeover empties *(C2-2.3)*, the takeover fenced by a generation as the act engine's is *(R4-F8)*; the pool's own lock throughout, OPENCUT1's only at a slot's first making (§2.4) *(O1-F6)*; the store, as both** |
| 2 **The line and its branch** | create-only `daoris/s-<full id>` per branch work unit; readers by unit; no folder fallback for pooled lines | `daoris/s-<8 hex>`; `branch_parts`; readers by the session's branch; `BranchOfTree` null for slots | **Today's name and derivation: the folder is named for its line's branch, so `BranchOfTree` and its callers stand (§3)** |
| 3 **The line's identity** | the entry, a `BranchWorkId`, a part and a lease; every path reader moved to them | the branch; the path kept only as *the folder the record ran in* | **The path, as today, since no line's path ever holds another line's work, nor its own work off its branch (§3.1) *(C1-2.1, O1-F2)*; the dozen sites that meet a gone tree re-keyed (§3.2), the ways back in §3.3, a finished line's among them *(C1-2.7, O1-F9)*** |
| 4 **The conversation** | native resume only once the exact original directory is reacquired; else a new part with the brief (`workplace-moved`); D189 amended | the record's slot if free; else a new part with the brief (`fromWhy=tree`); D189 amended | **A free slot brought back to the line's own path, so its conversation and trust are found there; D189's rule stands, a clarifying note (§4)** |
| 5 **The start** | journalled; HEAD pinned; loose files moved out; `reset --keep`; a non-forcing switch with `--no-overwrite-ignore`; verified | `reset --hard`, `clean -fd` after a parentless keep commit; then a switch | **X's non-forcing discipline, its pin, its unfinished-operation check and its check after the switch (X-5 step 9), dropped in round 1 and restored *(C1-2.4, O1-F4)*; parked, switched with hooks off *(O2-F9)*, relinked to where the links will stand *(C2-2.1)* and checked under a parking name, then placed (§5.3) *(O1-F2)*; the first build takes only a clean slot, so no reset and no clean; O's keep commit, of the index and the working tree both, on the owner's yes (§5) *(C1-2.6)*** |
| 6 **The end** | output; no output; uncommitted work holds the equipment; loose files kept apart | output; no output; uncommitted work kept aside | **Both ends, said once; uncommitted work holds the slot (X) until O's keep on the owner's yes (§6)** |
| 7 **What holds a slot** | a table; words waiting do not; a terminal and dirty work do | a table; words waiting do for one look; an extra tree so nothing waits | **The union where they agree; words waiting hold (O), for as long as they wait *(O1-F11)*; a terminal and a served build, each admitted by a row in the store *(C1-2.5, C2-3.2)*, and work no ref holds hold (X); a due landing, a due resume and a landing under way hold (neither) *(O2-F2)*; a visit holds back only a move *(O2-F7)* (§7)** |
| 8 **The count** | a Workplace store policy, `treePools` on export; default the cap at opt-in, stored; a bound conversations wait on | `driver.json`'s `keptTrees`; default cap + 1 read live; an extra tree past it | **O's place and default, clamped to 16, a lowered default retiring as a lowered count does *(C1-3.2, O1-F15)*; past the count a tree as today; what the count means put to the owner (§8, §18)** |
| 9 **Lazy creation, lowering** | rows declared, folders lazily; lowering drains highest first; a departure presents what stays | made lazily; retiring slots moved aside, read `unmarked`, the days | **Both, said once; retirement by the set-aside's own move with the slim's manifest (O) *(C1-1.3, O1-F8)*, highest first (X) (§9)** |
| 10 **The finished record** | at equipment release; new pool tables; immutable end refs; an outbox | at the branch's deletion; a new route `POST /api/lines/record`; `branch_ends` | **KEEPSIZE1b's own set-aside at a line's finish, of everything the slot does not keep, its manifest the record's snapshot written before anything moves or goes, its route and its hour as today: the path is still the line's, so its attribution holds (§10) *(C1-2.3, O1-F7, C2-2.4, O2-F6)*** |
| 11 **Dependencies** | one catalogue with capabilities; lockfile evidence; no silent delete; TREEGONE1's remake seeded | npm's own staleness rule; replaced from the checkout where it matches; `bin/` kept | **O's rule and list, `TreeScratch`'s unchanged for removals *(C1-4.2)*; X's capabilities as the list's named subsets, its words and its seeding; across a move, `node_modules` alone in the first build, its workspace links proved by the repository's declarations and made again; a finished line's other ignored folders set aside with its record by today's rule; a live line's held, since dropping them is the owner's to allow (§11, §18 item 4) *(O1-F3, C1-1.4, C1-2.8, O2-F1, C2-1.2, C2-5.1, C2-2.5)*** |
| 12 **Git and Windows** | non-forcing; a held preparation; contained jobs empty first | non-forcing; `stuck`; measured | **Both; `git worktree move` and its refusals, a destination that must not exist, a repository with submodules never pooled, judged at every take *(C2-4.3)*, git's steps in a named job of the claim's own and the pool offered only where that job holds *(C2-2.3)*, hooks off at the park *(O2-F9)* (§12) *(O1-F1, O1-F15, C1-2.2)*** |
| 13 **D51** | the work's identity never reassigned, equipment reused between completed occupations | rule 2 the branch's; rule 4 restated | **Stronger than both: neither the branch nor the path is ever reused; no directory ever holds two lines (§13)** |
| 14 **Records and older builds** | pooled sessions in a new service family with doors of its own; older builds blind to them | the slot's path in `sessions.tree`, an older build reading it as no tree of its own | **Neither: every record keeps today's meaning, since a path stays its line's; X's family not needed, O's reading wrong at the code (§0.2 row 5, §14); the race between a build without the pool and a take, named and bounded (§14) *(O1-F14)*** |
| 15 **The words** | old codes kept; new codes with no path; a table in en and zh | new codes; a table in en and zh | **Both's rule; the codes of §15** |
| 16 **The install's trees** | none adopted; any removal an owner-reviewed plan | none adopted; they drain | **Both: none adopted, none removed by this work; they drain by today's rules (§16)** |
| 17 **The order** | P1 to P9, reuse off until P8 | 1a to 1k, on per repository at 1f | **a to j (§17): the first three both's shape (the store and the census, git unwired, the readers); d and e wired with no door; the count's doors only at f, once returns and retirement stand *(C1-4.1)*; d waits on b's measurement being read *(O1, §3)* and on FOUNDATION1's keyed open *(C2-2.2)*; the installed resume at the head of g *(C2-4.2)*; j a live line's build folders set aside, on the owner's yes *(C2-5.1)*** |
| 18 **The owner's** | seven | four | **Four, and one bounded spend (§18), the fourth now a live line's build folders set aside at a take *(C2-5.1)*, the finished line's scratch withdrawn as today's rule *(O2-F6)*** |
| 19 **Proof** | per row; the decisive fixture; kills at every boundary | per row; each case seen failing first | **Both, per row (§17)** |

*(R4)* Rows 3, 4, 5, 11, 13 and 14 above are round 3's where they name a parking name, a move, a relink or a line's path holding its slot:
round 4 reads them as §1.1 rows 1 and 11 and §2 to §14 now say, a line's path a name no folder has while it is pooled and its slot a fixed
folder no record names.

### 1.1 Where they differ, and which way

| # | The difference | Chosen, and why | The case the other gets wrong |
|---|---|---|---|
| 1 | Where a slot's folder sits: a place of its own (both), or the path of the line that holds it (rounds 1 to 3) | **A place of its own, as both proposed, by the owner's choice of 2026-11-07** *(R4-F1)*: `k-<n>` beside today's trees, never moved, so what a slot's builds name stays valid and the next line builds over them incrementally (§11). **But every record keeps naming its line's path, which a pooled line never has as a folder** (§3.1): no record names a slot, every reader of a path as a line reads right, and every reader of a record's tree as a folder, a shipped build's included, meets a gone tree, never another line's work (§14). The costs, said: a line coming back in another slot loses Claude Code's conversation to the brief (§4); every reader that opens a record's tree as a folder asks the pool (§3.2, row a's census); and the next line builds over another line's outputs (§11). Rounds 1 to 3's moving slot saved only the checkout and TREEDEPS1's copy, about 6 s (R4-F1) | **O's** (the slot's path in `sessions.tree`): a shipped build resumes a line into its slot because TREEGONE1 answers *stands* for any folder git lists, whatever branch it holds (`Workplace/TreeReturns.cs:57-65`), and goes on there on another line's branch *(R4)*. **X's** (a second session family): every reader of sessions merges two families, and a shipped build can neither show nor continue a pooled line. **Rounds 1 to 3's** (the slot moved to each line's path): no output that names its path survives a move, so no take saves a build *(R4-F1)* |
| 2 | The claim: a durable lease per occupation with a generation (X), or a step's claim, the record holding the slot after (O) | **O's, with X's reservation and X's check** *(C1-3.1)*: the claim reserves the slot from its choice until its record opens, the ledger holding it only after that open (`Core/Sessions/SessionLedger.cs:1625-1639`), and the slot is read again before the open (§5.3); after the open, whether a slot is held is read live from the ledger, the in-use set, the claims and the visits (§7) | X's: a lease kept through the occupation needs the same reconciliation TREELOCK1's let-go does for git's lock (`SessionLocks.cs:96-123`) whenever a record ends in another process; it is a second record of what the ledger already says, kept in step at a cost, not impossible. The choice is economy |
| 3 | Work no ref holds before a reuse: moved out to a kept area (X) or kept in a parentless commit (O) | **Neither in the first build**: a slot holding such work is not taken (§5.2). **O's keep commit** on the owner's yes, row i (§5.5) | X's: a file move cannot keep a tracked edit, so X holds those slots anyway, and its payloads (`<home>/work/tree-pool-kept/`) have no rule that removes them (X §6 item 6), so they grow until the owner disposes of each. O's before the owner's yes: a reset of a tree that holds work, which the brief §2 reserves to the owner |
| 4 | The count: a bound sessions wait on, the person's conversations included (X), or the trees a repository keeps, a start past it given an extra tree (O) | **O's**: past the count a start opens a tree of its own as today, which today's tidy takes once its work is done; the reading is put to the owner (§18 item 1) | X's: the person's second conversation in a repository whose two slots hold parked sessions waits for a tree, where today a conversation opens its own at once (`ChatRunner.cs:635-651`). O's, and this design's: a strict reading of the direction's point 7 removes trees only by the count, and a tree past the count is removed by the tidy |
| 5 | Where the count is declared: a Workplace store table (X) or `driver.json` (O) | **O's** | X's: a setting outside §13.3's `settings` family, which holds one row per key of `driver.json` read through Settings' one parser (`docs/2026-10-25-foundation-design.md:1972-1990`); the export and import would need a case of their own, and z-ii a second home for configuration |
| 6 | The default: the cap at opt-in, stored (X), or cap + 1, read live (O) | **O's, clamped to 16** *(O1-F15)*, a lowered default retiring slots as a lowered count does, said where the cap is set *(C1-3.2)* | X's: a person who raises the cap later keeps the old number of kept trees and runs past it every day. O's unclamped: a cap of 16 or more (`Settings/DriverConfig.cs:552-553` sets no ceiling) asks for more slots than the count allows |
| 7 | The lock: one permanent pool reason (X), or TREELOCK1's while held and a kept reason while free (O) | **O's**, the pool's own reason also through every claim, OPENCUT1's opening lock only at a slot's first making (§2.4) *(O1-F6)* | X's: the session lock is how every shipped reader tells a tree's holder (`SessionLocks.cs:42-54`); a permanent pool reason hides the holder from them and from a person reading `git worktree list` |
| 8 | Words waiting on an ended record: no hold (X) or a hold (O) | **O's** | X's: the in-use set keeps such a tree today for the next look's go-on there (`BranchTidying.cs:146`; D88's AUTOTIDY2d); freed for that look, a start takes the slot and the go-on must bring another back, a rename and a switch for nothing |
| 9 | The finished record: new tables and refs (X), a new route (O), or KEEPSIZE1b's own | **KEEPSIZE1b's** own set-aside, manifest, route and hour, at a line's finish, of everything the slot does not keep, its manifest written before anything moves or goes (§10) *(C1-2.3, O1-F7, C2-2.4)* | Both: a second shape for one record. Here the path is the line's, so the route's rule by path (`Core/Sessions/WorkRecords.cs:446-463`) names exactly the line's sessions |
| 10 | A stale `node_modules`: kept untouched (X) or replaced from the checkout where its install matches (O) | **O's**, under D195 point 3's reason that npm discards the folder at every clean install | X's: kept stale with the agent told to run `npm ci`, which deletes it first; keeping it gains nothing, and the copy at a matching lockfile saves the install |
| 11 | A line whose slot was taken when it comes back: a new part handed the brief (both) | **The line's own last slot where it is free, switched back to the line**, so its agent's conversation is found where it was filed; **else both's**: in another slot a new part handed the brief (`fromWhy=tree`) for an agent that files its conversation by folder (§4) *(R4-F1)* | Rounds 1 to 3's (a slot brought to the line's path) keeps the conversation and loses every build. Under today's holders a live line's slot is rarely taken at all (§5.1 choice 3, §18 item 4), so the brief falls mostly to lines that ended long ago, as a fresh tree's does today |
| 12 | Where the licence to grow a missing branch again lives: in preserved unit and end refs (X-3, X-10) or in the store's record of a no-output end (O) *(O1-F13)* | **O's** (§6): both grow the branch again under its own name; a no-output branch has no commit of its own, so a ref kept for it would only hold a commit of the line's | X's: a ref per no-output end, kept so that a branch with nothing on it can be made again, holding line commits nobody needs held |

### 1.2 What each got that the other missed

**Codex's.** The rule this design is built on, that the work's identity is never reused while its equipment is. HEAD pinned and compared
before anything prepares a slot (X-5 step 4), and an unfinished git operation as a hold (§5.2). `--no-overwrite-ignore` on every switch. A
person's terminal as a hold. A kept build output as no proof that a commit builds. TREEGONE1's remake seeded by TREEDEPS1. A crash that never
jumps to *free*. The downgrade question taken seriously, which led to §0.2 row 5 and to §2's choice. D167's parts alongside kept apart from a
line's own continuation. The install's measurement split between what reuse saved and what deleting legacy folders saved, and the plain
statement that a count bounds equipment, not bytes. And the check after the switch (X-5 step 9), which round 1 dropped without a word and
round 2 restores *(O1-F4, C1-2.4)*.

**The Opus reader's.** Four corrections of the survey (§0.2 rows 1 to 4). The claim as a step's, the ledger holding the slot afterwards. The
parentless keep commit and `AnyRef` excluding its prefix. The count in `driver.json` at cap + 1, read live. Nothing ever waiting for a tree.
Retirement through the set-aside's own move. A no-output branch grown again. A take preferring a slot whose line is finished. `wwwroot` on
the one list. The census in `ConfigSeamTests`' manner.

## 2. The slot, its place and its claim

### 2.1 In one paragraph

*(R4-F1)* A slot is a linked worktree of the repository's registered checkout that the repository keeps, numbered `n` in its pool and known
by `(workspace, repository, n)`. **Its folder is fixed and named for the slot**: `<home>/work/trees/<workspace>/<repository>/k-<n>`, beside
today's `s-` trees (`Platform/DaorisHome.cs:405`), made the first time a start needs it and never renamed. **A line works in it**, on the line's
own branch, for as long as the line holds it; between lines it stays where it is, free, on the branch its last line left or detached at a
finished line's tip. **The record names the line, not the slot**: a pooled line's sessions are recorded with its line's path,
`<…>/s-<8 hex>`, minted as `OpenAsync` mints one, which the pool never makes as a folder while the line is pooled; the store alone says which
slot a line is in (§2.3). So a path still names one line for its whole life, the folder name is still the branch to every reader that derives
one from the other, no record names a folder that holds another line's work, and a build that does not know the pool reads a pooled line as a
line whose tree is gone (§3.1, §14). What is reused is a working copy and what it built: its `node_modules`, its build folders and their caches,
valid at its unchanging path (§11).

### 2.2 What a slot is, and where it sits

- **What it is.** A linked worktree of the checkout, made by today's open at the slot's own folder the first time a start needs a slot and
  the repository has fewer than its count (§9), its first line's branch made with it as an open makes one (`SessionTrees.OpenAsync`). Its first
  life is a fresh tree, said in today's words (`started.tree`, `started.tree-copied`).
- **What identifies it.** `(workspace, repository, number)` in the store (§2.3), its folder `k-<n>`, and git's registration name under
  `<common dir>/worktrees/`, the folder's leaf as `worktree add` names it, unchanged for the slot's life since the folder never moves *(R4)*.
- **No record names its folder** *(R4-F1)*: no session's `tree`, no landing, manifest or work record, no `session-branches.json` entry. It is
  read only through the pool: by the start that runs an agent in it, the shell that opens a terminal there, and the readers §3.2 lists.
  `BranchOfTree` of it would answer `daoris/k-<n>`, a branch nobody makes, and TREEFOLLOW1 learns only a folder whose layout branch has a
  `GrownBranch` entry (`Workplace/TreeFollows.cs`), so no reader that derives a branch from a folder ever learns from a slot. Row a's census
  fails on any write of a slot's folder into a record.
- **Where a line is.** A line holds at most one slot and a slot serves at most one line: `kept_trees.line` names the line whose branch it is
  on (`''` when detached), and a pooled line's path is never a folder. A finished line's slot is detached at its tip at the finish (§10); a
  no-output line's stays on its empty branch until a take switches it (§6).
- **A repository with submodules is never pooled** *(O1-F15, R4)*: a switch between commits that track different submodule commits leaves the
  submodule's checkout where it was, which no judgement of §5.2 can read as clean. The count's door refuses such a repository, saying why, and
  so does **every take** *(C2-4.3)*; a slot whose line adds one leaves the pool, said, and its line is finished by today's set-aside of the
  whole tree *(O2-F15)*.
- **The pool is offered only where the claim's job holds** *(C2-2.3)*: a job that ends a claim's git processes with its process, and that a
  takeover can open by its name and see empty (§2.3), which `ProcessJob` gives on Windows and nowhere else (`Platform/ProcessJob.cs:15-18`).
  Elsewhere the count's door refuses, said, and starts open trees as today.
- **Its neighbours.** The `o-`, `p-`, `r-` and `i-` folders stay where they are (`Workplace/TreeStrays.cs:77`;
  `Workplace/IntegrationReplays.cs:76-78`) *(R4-F11)*; the strays' walk meets a slot as a tree git lists and leaves it, and the newer strays
  pass `k-` over as they pass their others' own (`:147`). A `k-` folder git does not list (a registration lost) is the pool's to reconcile
  (§2.3); a build without the pool moves one aside after its quiet ten minutes, which costs the pool a slot and nobody any work, since no record
  names it (§14). **The integration tree is the nearest precedent** *(R4-F11)*: a warm tree of Daoris's own at a stable path, kept between
  candidates. The two stay two makings, since a slot holds a line's work and is never reset (§5.2), while the integration tree holds none and
  is put back by reset, clean and checkout.

### 2.3 The claim, and where it is recorded

**The desktop store, as a Workplace family by FOUNDATION1 §13.3's rules**: new tables, every column required, the migration step after the
last at build time, the writer stamp unchanged, so builds of z-i and z-iii never open them, as `bindings` and `binding_picks` were added
(`Store/DesktopStore.cs:107-129`). It is the owner's preferred home for records the product reads and sets (the brief §5), and the only one
that gives one compare-and-set across the shell, a `--share` loop and a verb (O-1). A lease file beside the slot would need the store's
dead-run test anyway; a lease in the service would teach the service a machine path and desktop state (D47 §4; foundation §13.7); git's lock
alone is not a test-and-set across a read, a judgement and a switch (O-1).

| Table | Columns, all required | Key | What it says |
|---|---|---|---|
| `kept_trees` *(R4)* | workspace, repository, number, registration (git's name for it), path (its fixed folder), line (the line's path whose branch it is on, `''` when detached), tip (the commit HEAD stands at while free, `''` while a line holds it) *(O2-F4)*, state (`kept`, `retiring`, `stuck`), said (`''` or why), since (the moment its present line took it), bytes (what it holds ignored outside `node_modules`, read at each let-go, §11) | (workspace, repository, number), and one unique index over (workspace, repository, line) where `line` is not empty | the slots that exist, and which line each serves |
| `tree_claims` *(R4)* | workspace, repository, number, kind (`make`, `take`, `return`, `finish`, `end`, `retire`), holder (the quest, session or person's press it serves), line (the line's path it brings the slot to, or ends), branch, start (the commit a new branch grows at, or a returning line's tip), expect (the HEAD the judgement read), opens (the key its record's open is made under, written before that open, `''` where a kind opens none) *(C2-2.2)*, job (the name of the claim's own job) *(C2-2.3)*, record (the opened record's id, `''` until known), phase, claim_run (a `store_runs` id), claim_generation *(R4-F8)*, at | (workspace, repository, number), and one unique index over (workspace, repository, line) | the one claim in flight on a slot, its whole intent written before its first effect *(C1-2.2)* |
| `tree_visits` | workspace, repository, number, kind (`terminal`, `served`) *(C2-3.2)*, visitor (the terminal's or the served build's id), run, at | (workspace, repository, number, visitor) | a terminal the person opened in a slot, or a build served from it to a tab, admitted before its shell starts or its serving begins *(C1-2.5, C2-3.2)* |
| `line_ends` *(R4)* | workspace, repository, line, branch, session, number (the slot it was let go in), kind (`output`, `no-output`, `uncommitted`, `held`), tip, read (the moment the slot was read), left (the names of its own ignored paths, §11, `''` for none), at | (workspace, repository, line, at) | each let-go of a line: the tip the next judgement compares (§5.2), the slot a returning line prefers (§5.1), the facts the finish's manifest needs where the line's slot serves another by then (§10) *(C1-2.3)*, and the licence to grow a no-output branch again (§6) |

Round 3's `from`, `park`, `to` and `before` are withdrawn with the moves *(R4)*, and the unique index over a claim's `to` becomes one over its
line, so two ways back of one line still let one through *(O1-F1)*. Round 2's `line_records` stays withdrawn *(C2-2.4)*: a finished line's
record is KEEPSIZE1b's own manifest, written before anything goes, as today (§10). Row i adds `kept_work` (§5.5). **Whether a slot is held is
never stored**: it is read live from the ledger, `TidyHolders`, the landing holds, the claims and the visits (§7) *(R4-F3)*, a hold on a line's
path holding the slot that line is in, so no row mirrors the records (O §1.2; §1.1 row 2).

**How a claim is taken.** The step that needs a slot (the driven start's take, a stage of the `Grown` phase before `GrowTree`,
`Loop/Run/Tree/GrowTree.cs:11` *(R4-F2)*; a conversation's, where `ChatStarts` chooses its tree, `Loop/Chat/ChatStarts.cs:188-215`; a `land`
node's return, §3.3; the look's finish, end or retirement) chooses it (§5.1) and, in one `BEGIN IMMEDIATE` transaction, reads that the slot
has no claim, and, for a claim that switches it (`take`, `return`, `finish`, `retire`), no visit, and inserts its claim with its whole intent,
its `claim_run` and generation 1; the primary key refuses a second claim on the slot, and the unique index a second claim for the same line,
from any process. A visit holds back only a claim that switches the slot: the line's own claim in place (§5.1 choice 1) and an `end` go on
beside a person's terminal, as a resume does beside it today *(O2-F7)*. **A take waits for the start's confirmation to be settled**
(foundation §13.5's rule for what an older build cannot undo), so D139's rollback never meets a slot a build it rolls back from switched
*(O2-F15)*. **A pooled line's start is held for the look while any claim names its line or its slot**, and the look runs every takeover before
it plans a start *(O2-F11)*. **A start's claim reserves the slot from its choice until its record opens** *(C1-3.1)*, the ledger holding the
line from then (`Core/Sessions/SessionLedger.cs:1625-1639`); the driven start's claim is the run's, on `RunState`, let go with the run as
`Starting` is, so a run that ends before `OpenRecord` gives the slot back through the run's own release *(R4-F2)*. Each effect is a mark in
§13.4's sense: a transaction checks that `claim_run` and `claim_generation` are still this run's and writes the phase the effect begins, then the
effect runs, then a transaction writes the phase it reached. **No rename, so no exclusive hold at a take** *(R4-F7)*: a take and a return run
inside the start's shared hold, where today's open makes a tree and its branch (`TreeLock.TryStarting`, `GrowTree.cs:96`, `:110`); a finish and
a retirement run inside the look's tidy, which holds the repository alone under `Tidying` as today's set-aside does (`Workplace/TreeLock.cs:23-41`).
So round 3's bounded wait for the exclusive hold, and a conversation's tree of its own for want of it, are gone. **git's steps run in a job
named for the claim, made as an act's is** *(C2-2.3, R4-F8)*: `ProcessJob.CreateNamed` under a name derived from the claim as `ActJournal.JobOf`
derives an act's (`Workplace/ActJournal.cs:215`; `Workplace/ActRuns.cs:76`, `:163`), its name written in the claim before the first git
process starts, each git started suspended in it and joined as `ActSteps` starts one, its kill-on-close ending them all when the claim's process
ends. D201's launcher holds no claim: a claim belongs to a step, never to an agent's process (both).

**The phases, kind by kind** *(C1-2.2, O1-F5, O1-F10, R4)*:

| Kind | Phases, in order | What the effect between them is |
|---|---|---|
| `make` *(O2-F15)* | `opening`, `made`, then as `take` from `checked` | today's open at `k-<n>` under OPENCUT1's lock, its first line's branch made with it (`SessionTrees.OpenAsync`); its lock swapped for the claim's; then the check, the keyed open and the record (§5.3 steps 4 and 6) |
| `take`, `return` | `judged`, `switched`, `copied`, `checked`, `opening-record`, `opened` | the switch in place, hooks held (§5.3 step 2); TREELOCAL1's copies made again and the dependencies judged (step 3); the check (step 4), then the repository's `post-checkout` hook (step 5); the keyed open requested, its key already written; the record's id written. A returning line's own slot still on its branch (§5.1 choice 1) has only `judged`, `checked`, `opening-record` and `opened` |
| `finish` | `judged`, `setting-aside`, `set-aside`, `detached` | KEEPSIZE1b's tip held and its manifest written at `judged` before anything moves; the line's own ignored paths (§11) renamed into the set-aside folder, the manifest at `moved`, and the slot read again; `switch --detach` at the line's tip, which writes no file, and `kept_trees.tip` written; then the claim is done, and the branch's journaled delete and the record's post are the manifest's own steps (§10) *(C2-2.4, R4-F6)* |
| `end` | `judged`, `recorded`, `locked` | the slot read; `line_ends` written; the free lock put back |
| `retire` | `judged`, `slim-judged`, `moving`, `moved`, `dropped` | the slim's manifest at `judged`; the set-aside's rename of the whole folder into `.tidied`; the manifest at `moved`; git's record of the vanished path dropped and the row removed (§9) |

**Across a crash.** A claim whose run is dead by `HomeLease`'s test (`Platform/HomeLease.cs:66-68`, `:165-173`; foundation §13.4) is taken
over by the look. **First it proves the dead run's git processes gone** *(C2-2.3, R4-F8)*: it opens the claim's job by the name the claim
recorded, ends it and waits until it holds no process, as `ActReconciler` settles an act's (`ProcessJob.cs:146-149`, `:160-168`, `:196`), before
anything else; where that cannot be done the pool is not offered at all (§2.2). **Then it takes the claim by one guarded update** of
`claim_run` and `claim_generation`, as `ActJournal`'s take (`ActJournal.cs:396-411`) and `MoveStore`'s are, so of two lookers that read one
claim dead (the shell and a `--share` loop) only one acts *(R4-F8)*. Then it reads the disk, never the phase alone *(C1-2.2)*: the slot's HEAD
and status, git's registration of it (`worktree list --porcelain`), and git's own lock files (`index.lock`, `HEAD.lock`), one present holding
the slot `stuck`, said, until the person removes it, since Daoris never deletes git's lock files. Then, by what it found:

- **Before the switch** (`judged`): the claim is dropped and the free lock put back.
- **Switched, no open requested** (`switched` to `checked`): a take's slot standing on its new branch at `start`, with no commit of its own, is
  detached at `start` and that branch deleted through `JournaledDeletes.DeleteAsync` judged at `start` (`Workplace/JournaledDeletes.cs:31-33`),
  which verifies the commit and keeps the Undo, never by `update-ref -d` or `branch -D` *(O1-F15, R4-F6)*; a return's slot stays on the
  returning line's branch, free, as the line let it go; a HEAD at neither holds the slot `stuck`, said. No record names the slot meanwhile.
- **An open requested** (`opening-record` or later) *(C2-2.2)*: the takeover asks the service for the claim's `opens` key in FOUNDATION1's keyed
  open, `session_opens` (row 1d-i, built), which the open wrote in its own exclusive step. A row names the record whatever has become of it
  since: the slot was opened, the claim is deleted, and the end follows that record's end. No row: the open never landed, and the slot is freed
  as above. **No answer is not no row** *(R4-F13)*: the claim is held for the next look and the slot never freed on it. The record's liveness
  is never the test.
- **`finish`** *(C2-2.4)*: at `judged`, the claim is dropped and the free lock put back, the manifest left at `judged`, which KEEPSIZE1b's one
  resume rule finishes or forgets as it does today (D195:511-519); at `setting-aside`, what was already moved is renamed back into the slot
  first, then as at `judged`; from `set-aside`, the detach is done again; once `detached`, the branch's delete and the record's post are the
  manifest's own resumed steps, keyed by its identity, never by a slot.
- **`end`, `retire`**: each resumes at its phase, every step idempotent; the retirement takes KEEPSIZE1b's resume of a stopped set-aside.
- **A refusal in a living process** (a switch refused, a check failed): as *switched, no open requested*, at once, the slot `stuck` where the
  switch left it something to read (§5.4).

A slot is switched only where nothing its last line holds is outside a ref or the line's own (§5.2), and a slot on a line's branch is only
ever that line's, so every takeover loses nobody any work; at worst it costs the pool a slot.

### 2.4 Its lock

- **While a session holds it:** TREELOCK1's `daoris session <id>`, unchanged, so `HolderOf` and every shipped reader of the lock read the
  holder (`SessionLocks.cs:42-54`). A conversation that takes a slot locks it too, closing TREELOCK1's *not covered* for conversations
  (D51:137-138).
- **While it is free:** `daoris kept tree`, a fifth reason, matching neither a session's prefix (`SessionLocks.cs:28`) nor an opening's
  (`Trees/SessionTrees/TreeOpenings.cs:15-45`). `SessionLocks.HoldAsync` learns to take it over, as it takes over a session's today
  (`SessionLocks.cs:60-73`); the end puts it back; the look's let-go of a session lock nobody holds puts it back on a slot rather than leaving
  the slot unlocked (`:101-123`).
- **Through a claim:** the pool's own reason naming the claim's run, `daoris kept tree (claimed by run <id>)`, put on before the claim's first
  effect and kept until its last, since no step renames the slot *(O1-F6, R4)*. No build reads it as an opening's, so no build's strays delete
  a returning line's branch or set a claimed slot aside (`TreeStrays.cs:183-201`).
- **Whose folder is locked** *(R4)*: the slot's, though the record names the line's path: `SessionLocks.HoldAsync` and the look's let-go are
  handed the slot the store says the line is in, never the record's `tree`, which for a pooled line is no folder (§3.2).
- **OPENCUT1's opening lock** only at a slot's first making, which is today's open (`SessionTrees.cs:371-373`) and keeps its undo.
- **No lock at all** (a session's lock an older build's let-go lifted, `SessionLocks.cs:129`): accepted by the judgement and locked again under
  the free reason *(O1-F10)*.
- **Any other reason** (a person's, or an opening's whose process lives): the slot is out of the pool, said, and never touched.
- **The pool's own judgement of the lock** *(C2-1.1)*: the look's last guards refuse any locked tree (`TreeSetAside.cs:84-87`), which would
  refuse every slot the pool locked; the pool reads the lock by this section's rules alone, accepting exactly its own free reason, a session's
  lock whose record no longer holds the slot, or none, and refusing every other, and asks the rest of the last guards apart from their lock
  test (§5.2 step 3).
- **What a shipped build does with a slot's lock.** Its tidy refuses a locked tree (`TreeSetAside.cs:84-88`); its let-go lifts only a session's
  lock (`SessionLocks.cs:126-134`); its strays judge only an opening's, and only on an `s-` folder (`TreeStrays.cs:183`); its press's
  `git worktree remove`, forced at most once, refuses a locked tree (`TreeRemovals.cs:220-222`).

### 2.5 The types on FOUNDATION1's map

| Type | Subdomain (rank) | Names only |
|---|---|---|
| the migration step's DDL in `DesktopStore.Steps` | Store (2) | — |
| `DriverConfig.KeptTrees`, `WithKeptTrees`, `KeptTreesOf` | Settings (3) | a map of ints, no rule type (§14.3) |
| `KeptTreesPort`, the port Settings' verbs ask (how many slots a lower cap or count retires; whether the pool can serve a repository: no submodule, a job that holds) *(O2-F8)* | Settings (3) declares it; Workplace answers it by a module initializer installed in Workplace's folder, D212's note of 2026-10-26 kept: an unset port throws, a test that sets another puts it back, and row mod-xxi lists it in the wiring view | — |
| `NoteCodes` `StartedTreeKept` and its clauses, `StartedTreeBack`, `StartedTreePastKept`, `TreeReturned`, `EndedOutput`, `EndedNoOutput`, `EndedUncommitted`, `TreeKeptHeld`, `TreeKeptStuck` | Events (7) | — |
| `KeptTree`, `TreeClaim`, `TreeVisit`, `LineEnd`, `TreePool` (make, take, return, finish, end, retire, visit, and `WhereIs(line)`, the one answer every folder-reader of §3.2 asks) *(R4)*, `SlotChoice` and `SlotJudgement` (pure, in `Domain/`), `SlotKeeps` and `WorkspaceLinks` (§11, §5.2), the `KeptTreesPort`'s answer, `TreeHolds` (the holders as a delegate over `TidyHolders`, whose rule it reads and never repeats *(R4-F3)*) | Workplace (10) | Store, Settings, Service, Events and below |
| the holders a standing `moves` row (Course 14) *(R4-F9)*, a served review (Review 16), a helper wait (Participants 12) and a run's due or running `land` node *(R4-F10)* contribute | each answers `TreeHolds` as a delegate Loop composes | — |
| the take as a `[RunStage]` of the `Grown` phase, its claim on `RunState`, and `RunState`'s line path beside `WorkTree` *(R4-F2)* | Loop (above Workplace), calling `TreePool` downward | Workplace |
| `ConversationTrees.Stands` and `Continuation.Judge`'s `treeStands` answering *stands, or can be brought back* | Course (14), calling `TreePool` downward | Workplace |
| `ReviewServed.Stands` asking whether the slot at a path was placed after a set-up's showing *(O1-F12)*, and the review's serving taking a `served` visit before it serves and letting it go when serving stops *(C2-3.2)* | Review (16), calling `TreePool` downward | Workplace |
| a landing's hold read as a holder (`Trees/Landing/LandingHolds.cs:61`) *(O2-F2)* | Workplace's own (the landing is Workplace's) | — |
| the terminal's visit, taken before its shell starts and let go when its job is empty *(C1-2.5)* | the shell's `TerminalModule` (outside the map, above the driver), calling `TreePool.VisitAsync` downward | Workplace |

`Loop/FreshTree` is Workplace's (§14.2 row 10); its undo becomes the claim's. No pool type names `ChatRunner`, the planner, `HelperWaits`,
the review desk or the shell's terminal.

## 3. The line's identity, and the readers keyed by path

### 3.1 The rule: a record names its line, and no record names a slot *(R4-F1)*

A pooled line's sessions are recorded with the line's path, `s-<x>` for `daoris/s-<x>`, as today, and the pool never makes that path a folder
while the line is pooled; the line works in a slot whose folder, `k-<n>`, no record names (§2.2). So the facts every reader in survey §6
assumed of a record's tree, *the folder is the branch*, *a path is one line*, *the same path means the same line*, stay true of every record,
and **every reader that reads a record's tree as a name** (the ledger's key, the planner's same tree, the trace, a work record's sessions, the
page's branch from the last segment, `BranchOfTree`, `TreeGoneLines`) **reads it right unchanged**. What changes is the readers that **open** a
record's tree as a folder, since for a pooled line no folder is there: each asks the pool where the line stands (`TreePool.WhereIs`, §2.5),
its slot where it holds one on its branch, or none, when one is switched to it first (§3.3). **A reader the pool missed fails closed**: it
meets a pooled line exactly as it meets today a line whose tree the tidy took (TREEGONE1's remake or hold, a gone tree's refusal, a landing
closed *gone*), never as another line's work; a build that does not know the pool is such a reader everywhere (§14). TREEFOLLOW1, which learns
the branch a line's folder shows at every tidy pass (`TreeFollows.cs`), learns a pooled line's from its slot while the line holds it, and a free
slot shows only its last line's branch or none.

### 3.2 Every reader of survey §6

*(R4)* Every row below marked **Kept** holds as written, a pooled line's record naming its line's path. The readers that open a record's tree
**as a folder** are re-keyed to ask the pool, and round 4 adds those round 3 did not list, since its line's path held its slot: the start's
record, agent and trust over `RunState.WorkTree` (`Loop/Run/Open/OpenRecord.cs`, `Loop/Run/Hand/HandToSession.cs`, `Loop/Run/Trust/TrustAsk.cs`;
`RunState.cs:200-202`) *(R4-F2)*; the conversation's tree (`ChatStarts.cs:188-215`); the landing's two *gone* reads through
`TreeFolders.TreeGone` (`LandPerformer.cs:97`, `:146`; `TreeFolders.cs:13-15`) *(R4-F10)*; a chain's `BranchNowAsync(parent)`; TREEFOLLOW1;
`SessionLocks` (§2.4); the shell's terminal and its acts on a tree; the review's serving; and `ToolHomeLeftovers`, which reads a slot's
project folder in an agent's home as the slot's, gone only with the slot. The rows round 3 re-keyed because a line's path could be empty
(TREEGONE1, the go-ons and moves, a session's review, *Show it again*, the tidy's set-aside, the press) are re-keyed the same way, a pooled
line's path being always empty. Rows whose reading changes: *git's registrations* (a slot's path never moves); *The agents' homes* (filed by
the slot's folder, found where the line returns to that slot, §4); *Auto-landings* (gone: a run's `land` node lands every line) *(R4-F10)*;
*The tidy's in-use set* (`TidyHolders`) *(R4-F3)*; `TreeScratch`'s list (what a slot keeps is `SlotKeeps`, §11, the removal's list unchanged);
and `ContentHolds.HoldsAsync`'s *opened* (for a slot, `kept_trees.since`, the moment its present line took it). Row a's census lists every
folder-reader of a record's tree as *not yet moved*, with the row that moves it, and fails on a new one.

**Stored.**

| Record | Kept, re-keyed or cut |
|---|---|
| The service's `sessions.tree` (`Core/Sessions/Sessions.cs:89-100`, `:1189-1203`) | **Kept**: the line's path and the ledger's lock key; a pooled line's record names the path its line always runs in |
| `work_records`, `work_record_sessions` (`Core/Sessions/WorkRecords.cs:30-48`, `:446-463`) | **Kept**: posted at a pooled line's finish, `Tree` its path and `Moved` that moment, so *every session naming the path made before the move* is exactly the line's (§10) |
| The helper, partner and opinion desks (`Core/Sessions/HelperDesk.cs:96-101`; `Core/Sessions/PartnerDesk.cs:69-74`; `Core/Review/OpinionDesk.cs:113-119`) | **Kept**: the asker's or lead's tree is its path, held while it runs (§7) |
| `session-branches.json`, `GrownBranch` (`Trees/Branches/SessionBranches.cs:19-27`; the unit found by folder, `TreeLands.cs:845-848`) | **Kept**: recorded at a take as at an open (`SessionTrees.cs:380-398`), its `At` the take's moment; the folder still names the branch |
| `landings.json` | **Kept** |
| Auto-landings (`AutoLander.cs:209-222`) | **Kept**: a due landing holds its line's slot until it is judged (§7), so `:209`'s *gone* never meets a slot taken under it |
| Set-aside manifests (`Trees/SessionTrees/TreeSweeps.cs:220-230`) | **Kept** for legacy trees and trees past the count; a pooled line's finish writes the same schema-2 manifest beside its own set-aside folder, before anything moves or its branch goes, and that manifest is the line's record (§10) *(C1-2.3, O1-F7, C2-2.4)*; a retired slot's slim manifest is the slim's own schema (§9) *(C1-1.3, O1-F8)* |
| The heavy slot's holder (`Sessions/Heavy/HeavySlot.cs:9-27`) | **Kept** |
| The desktop store's bindings and picks (`Store/DesktopStore.cs:107-129`) | **Kept** |
| git's registrations and `locked` (`SessionLocks.cs:37-54`) | **Kept**: a slot's registration name is stable and its path follows its line; one new reason (§2.4) |
| The agents' homes (`ToolHomeLeftovers.cs:30-31`; `ClaudeTrust.cs:108-125`) | **Kept**: filed by the line's path, which a returning line has again (§4) |
| The opening note (`SessionTrees.cs:439-468`; `Core/Sessions/SessionNote.cs:9-17`) | **Kept** for every tree opened fresh; a taken or returned slot says new codes (§15) |

**Read in the driver.**

| Reader | Kept, re-keyed or cut |
|---|---|
| `TreeFollows.BranchOfTree` (`Trees/SessionTrees/TreeFollows.cs:148-161`), *what finds a session's branch once its tree is gone*, and its callers and copies (survey §6's list) | **Kept**: the folder is the branch |
| TREEFOLLOW1's `LearnTreesAsync` and `MovedOnTo` (`TreeFollows.cs:91-126`, `:132-139`), run at every tidy pass before its hold (`AutoTidies.cs:190`) | **Kept**: a line's folder only ever shows that line's branch (§3.1), and a parking folder has no `GrownBranch` entry for it to learn *(O1-F2)* |
| `TreeScratch`'s build folders, what a removal lets go of (`ContentHolds.cs:588-594`) | **Kept byte for byte**: the pool's carried subset of the one list is `node_modules` alone (§11) *(O2-F1, C2-1.2)*; whether a removal should read `wwwroot` as a build folder is a correction of its own, not this design's *(C1-4.2)* |
| `Planner.SameTree` (`Planner.cs:1120`, `:1141`, `:1358`, `:1380`, `:1390`, `:1586-1591`; `Sessions/RunningSessions.cs:129-145`) | **Kept**: one line per path; a line whose slot was taken holds no path and blocks nobody |
| `EntryLandings.Sequence` (`Trees/SessionTrees/EntryLands.cs:104-121`) | **Kept**: parts sharing a path share a line |
| A chat run's line and its taken quest (`Workflows/ChatRuns.cs:66-91`; `Workflows/WorkflowRun.Read.cs:204-220`) | **Kept** |
| The trace's *same tree* (`Sessions/Domain/Trace.Chain.cs:169-187`) | **Kept**: the same path is the same line |
| The tidy's in-use set and waiting trees (`BranchTidying.cs:140-150`; `Service/ServiceClient.cs:2242-2269`) | **Kept**, joined by the pool's own holders (§7) |
| `ContentHolds.HoldsAsync`'s *opened* (`Trees/SessionTrees/ContentHolds.cs:573-574`) | **Re-keyed** (row c): for a slot, `kept_trees.since`, the moment it reached its present path, since a moved folder keeps the creation time of its first making and a returning line's `GrownBranch.At` predates the other lines' writes the slot carried *(O2-F15)*; a legacy tree keeps its folder's |
| The context brief (`Loop/Driver.cs:2573-2583`) | **Kept** |

**Where a gone tree is met** (survey §2's holders, the sites the pool re-keys).

| Site | Kept, re-keyed or cut |
|---|---|
| TREEGONE1 (`TreeReturns.cs:48-110`) | **Re-keyed** (row e): a pooled line's tree gone while its branch stands gets a free slot brought back by a return run before the start's tree check (§5.3), which then finds it standing (`tree.returned`); with none free, today's remake, copying TREEDEPS1's (§11) |
| Words going on (`Continuation.cs:231`; `DriverContinuations.cs:171-178`) and a conversation's go-on and moves (`ConversationTrees.cs:11`; `ChatRunner.cs:630-633`; `Conversations/Chat/BriefedMove.cs:105-107`, `:188-190`; `LimitMove.cs:75`, `:261`) | **Re-keyed** (row c, on at e): *stands, or can be brought back*, and the go-on brings it back first |
| A landing (`TreeLands.cs:196-204`, `:918-926`) | **Re-keyed** (row e): a line whose slot was taken has one brought back (a `return` claim), then lands as today. A landing under way holds its slot through its tree's landing hold, which every door takes around the whole landing (`TreeLands.cs:420`; `Trees/Landing/LandingHolds.cs:9-14`), read as a holder (§7) *(O2-F2)* |
| A session's review (`SessionReviews.cs:125-130`) | **Re-keyed** (row c): a pooled line's tree gone and unlanded reads its branch in the checkout, where today it reads nothing |
| *Show it again* (`Review/ReviewDesk.cs:179-190`, its gone-tree refusal `:226-229`) | **Re-keyed** (row e) *(O1-F12)*: the served review holds the slot until the verdict (§7); after it, *Show it again* is refused where the slot now at the set-up's path reached it after the showing (`kept_trees.since` later than the showing's moment), since that slot's build is not the set-up's. And **every serving takes a `served` visit before it serves** (`ReviewDesk.cs:231-236`'s `ServeAsync`, *Show it again* and the set-up's first showing alike), let go when serving stops, so a headless allocator, which reads only waiting set-ups and its own desk's tabs (`Trees/Branches/BranchTidying.cs:734-743`), cannot move a tree another shell serves from *(C2-3.2)* |
| The tidy's set-aside (`TreeSweeps.cs:158-301`; `TreeSetAside.cs:190-341`) | **Re-keyed** (row d): a pooled line is finished by today's set-aside of everything its slot does not keep, and its slot parked (§10). A free slot at its line's path is read as that line's tree, `Holds` and `Worked` as today (`TreeSetAside.cs:27-30`), so a no-output line's slot stays its conversation's place *(O2-F5)* |
| The press's removal (`TreeRemovals.cs:14-200`) | **Re-keyed** (row d): on a slot, the person's press retires it (§9) |
| `SessionLocks.HoldAsync` and the look's let-go (`SessionLocks.cs:60-73`, `:101-123`) | **Re-keyed** (row b): `daoris kept tree` taken over, and put back on a slot |
| The Disk space card (`Platform/SystemSpace.cs:30-41`) | **Re-keyed** (row d): a `kept-trees` kind (§10) |

**Read on the page.**

| Reader | Kept, re-keyed or cut |
|---|---|
| The branch built as `` `daoris/${last segment}` `` (`Web/work/WorkFrame.tsx:475-477`; `Web/work/attention/attention.ts:639-640`) | **Kept** |
| *The last segment is the branch the tree was cut for* (`Web/work/identity.ts:132-144`, `:186-192`) | **Kept** |
| The terminal and the acts on a tree (`WorkFrame.tsx:1309`; `Web/work/acts.ts:102`, `:166`) | **Kept** on the page: a line whose slot was taken shows as a gone tree does today. The shell's terminal takes a visit in the store before its shell starts (`Modules/Windows/TerminalModule.cs:202-218`), refused while a claim is in flight on that slot, and holds the slot until its job is empty (§7) *(C1-2.5)* |
| The trace's tree and *same/other* (`Web/work/head/HowItCameToBe.tsx:427-438`); earlier parts' trees (`Web/work/conversation/EarlierParts.tsx:56`) | **Kept** |
| The repository's trees switch (`Web/projects/ProjectsView.tsx:313-316`; `Modules/Bridge/Routes/DriverRoutes.cs:71`) | **Kept**; the count's control beside it (§8) |

**Cut:** none. Row a's census (`PoolReadersCensusTests`) lists every re-keyed site above as *not yet moved*, with the row that moves it, and
fails on a new site that refuses, or remakes a bare tree, where a record's tree is gone outside the seams, a new read of a tree folder's
creation time, or a path built from a slot's number.

### 3.3 Every way back

| Start *(R4)* | Its line's last slot is free and still on its branch | Its line's last slot serves another line, or the line holds none |
|---|---|---|
| A resume (D79), an answer (D131), words going on (MSG1b, D137) | goes on there once checked, its HEAD on its branch at the tip its let-go recorded; no switch (O) *(C1-2.7)* | its last slot where free, switched back to its branch, where its conversation is (§4); else another free slot switched to it, its conversation from the brief; none free: a tree of its own at its line's path, today's remake |
| The same, for a **finished** line (its slot detached at its finish, its branch gone) *(C1-2.7, O1-F9)* | — | a landed line takes a slot for a new line from its landing, as today it grows a fresh tree; a no-output line has its branch grown again at its tip, licensed by `line_ends` (§6), and a slot switched to it. Never goes on detached |
| A closed quest's line whose tree is gone: TREEGONE2's *Go on with Daoris's brief* *(R4-F3)* | — | a slot switched to its branch, the brief as today |
| A part that left nothing: TREEKEEPRETRY1's fresh tree (`GrowTree.cs:36-50`) *(R4-F3)* | — | a slot taken for a new line, as today a fresh tree |
| A carry-on (D80), a driven hand-over (MIXAGENT1), a bring-forward (D178) | there | as a resume |
| A further part (D167 point 4 as amended) | into the entry's newest part's line where nothing live holds it (`Planner.cs:1105-1157`), in that line's slot | as a resume; a part alongside a live part takes a slot for a new line of its own, as today it opens a tree |
| A hand-over, a limit move, a briefed move (D189 point 2) | the same slot; the standing move holds it (§7) | not reached while the move stands; a move begun after the slot served another line goes as a resume |
| A chain's next step (D82, CHAIN2) | a slot for a new line from the parent's branch now, asked of its slot, else `daoris/<folder>` of the parent's line path (`GrowTree.cs:110-125`) | the same |
| A landing (a run's `land` node, `trees land`) *(R4-F10)* | lands in it, as today | a free slot switched to the line (a `return` claim), free after |
| TREEGONE1 for a line that began in a legacy `s-` tree | today's: its own path, its own tree | where its tree is gone and its branch stands, a free slot switched to it; else today's remake |

A line never waits for its own slot while another line holds it: another free slot is switched to it. **Two ways back of one line at once** (a
resume and a landing, in the shell and a `--share` loop) name one line, so the unique index on a claim's line lets one through and the other
waits for a look *(O1-F1)*. **A take of a line's free slot racing that line's return** reads the other's claim in its own transaction: a return
waits a look while its line's slot is under another claim, and a take passes over a slot whose line has a claim in flight *(O1-F1)*. Nothing is
renamed, so nothing is moved aside and no destination must be absent *(R4)*. **A return is chosen only where the line holds no slot on its
branch** *(O2-F7)*: a line whose own slot still stands on its branch goes on there (choice 1), with a person's terminal open in it or not. **A
pooled line's start is held for the look while a claim names its line or its slot**, and the look runs takeovers before it plans starts (§2.3)
*(O2-F11)*.

## 4. The agent's conversation and resume (D189)

Claude Code files a conversation as `projects/<working directory, flattened>/<id>.jsonl` in the account's home, and its resume reads it there
and refuses a missing one (`docs/2026-10-24-switch-context-design.md:184-188`; `ToolHomeLeftovers.cs:30-31`, `:55`); its trust is the folder's
(`ClaudeTrust.cs:108-125`). Codex files a session by date (`ToolHomeLeftovers.cs:41-42`), so its resume does not depend on the folder.

*(R4-F1)* **A pooled line's agent works in its slot's folder, so its conversation is filed under the slot.** A line that comes back to the same
slot finds it there, with the trust the slot already has, whatever lines worked there between; so a returning line prefers its own last slot
(`line_ends.number`), switched back to its branch where another line used it meanwhile (§5.1). **A line that comes back in another slot does not
find it**, and for an agent that files by folder it goes on as both proposals had it: a new part handed the brief (`fromWhy=tree`), said in
`tree.returned` (§15). **D189 point 2 takes an amendment**, where round 3 needed only a note: *the same agent, account and tree* reads *the same
slot* for a pooled line, its resume-first rule standing where that slot can be had, the brief elsewhere. An agent that files by date resumes in
any slot. A hand-over's or limit move's new part keeps its slot while the move stands (§7), so *the same tree keeps the agent's folder the
same on every account home* stays true of it.

**How often a line meets the brief.** A slot is handed on from a line that may still come back only by §5.1 choice 3, which `TidyHolders`
narrows to closed quests' lines and conversations not spoken to within the hour (§7) *(R4-F3)*, and which §18 item 4 asks of the owner; a line
whose own slot is merely free always gets it back. So the brief falls mostly to lines that ended long ago, which today, their tree tidied,
start in a fresh tree already (`GrowTree.cs:36-50`).

**What a conversation resumed finds.** Its branch's tracked files and its commits; its own uncommitted work wherever its slot was never handed
on (a slot holding work no ref holds is never taken, §5.2) or, after row i, laid back; and the slot's builds, its own or a later line's, said
(§11).

**Rejected:** copying the agent's files between two slots' project folders, which writes in an agent's home (D189 point 5) and is the kind of
carry SWITCHCTX1 proposes between account homes and has not had accepted (a later row may use that carry once it is); a junction from a line's
path to its slot, since `git worktree remove --force` follows a junction (D51:74-77; PRUNELINKS1) and what agents and the tree guard read through
one is unverified (O-4); a returning line waiting for its own slot while another line holds it, which serialises what the pool runs at once.

**Unverified:** whether Claude Code reads anything of a directory but its path when it resumes (its git branch, a file it touched); row g's
installed proof runs one real resume in the same slot after another line worked there (§17, §18 item 5).

## 5. The start: brought to the line, and what makes it safe

### 5.1 The choice

`SlotChoice`, pure, before the start's own window, and only once the start's confirmation is settled (§2.3) *(O2-F15)* and the repository
tracks no submodule at the start's commit *(C2-4.3)*, takes the first that stands:

1. **The returning line's own slot, still on its branch**, free or held only by that line's own work or a person's terminal there: no switch
   (O); it is checked (§5.3 step 4) and goes on *(C1-2.7, O2-F7)*.
2. **The returning line's own last slot, free**, though another line worked there since: switched back to the line's branch, so its agent's
   conversation is found where it was filed (§4) *(R4-F1)*.
3. **A free detached slot**: one a finished line left at its finish (§10), a retirement left unfinished, or a take undone *(O1-F9)*; the one
   whose last line's base is newest first, so the least is built again *(R4)*.
4. **A free slot whose line may come back** (a closed quest's line, a conversation not spoken to within the hour, an unlanded line nobody
   holds), judged clean (§5.2), the one let go longest ago first; never one `TidyHolders` holds *(R4-F3)*, nor one whose line has a start due
   this look (§7) or a claim in flight (§3.3). In a repository whose sessions leave scratch (this one's tests write `_fixtures/`), few such slots
   qualify until the owner answers §18 item 4, and the pool works mostly through choices 3 and 5, which row g counts *(O2-F6)*.
5. **A new slot**, where the repository has fewer than its count: today's open at `k-<n>`, recorded as the next number (§9), a `make` claim
   (§2.3).
6. **A tree of the line's own, as today** (`SessionTrees.OpenAsync`), at its line's path, off the pool's books, said in
   `started.tree-past-kept` beside today's words; today's tidy takes it once its work is landed, and a tree whose branch never moved stays until
   the person's press, as today (§8). Round 3's way here for a conversation that could not have the exclusive hold is gone with the hold
   *(R4-F7)*.

The claim is inserted in the transaction that chooses.

### 5.2 The judgement: what makes it safe

A slot is taken for another line only where every one of these holds; any other answer leaves it its line's, said (`tree.kept-held` with
why), and the choice tries the next:

1. **Nothing holds it** (§7): `TidyHolders` holds no line the slot serves *(R4-F3)*, and there is no claim, no visit (a terminal's or a served
   build's), no landing under way (its landing hold, only tried), no standing `moves` row, no run's `land` node due, no start due and no helper
   wait *(C2-3.2, O2-F2, R4-F9, R4-F10)*.
2. **git lists it at its folder** (TREEGONE1's test, `TreeReturns.cs:57-65`), **binds it to no other checkout** (`TreeBinding.RefusalAsync`,
   `:63`) *(R4-F5)*, and its lock is `daoris kept tree`, a session's lock whose record no longer holds it (`SessionLocks.cs:88-94`), or none,
   which it locks again *(O1-F10)*; every other reason refuses (§2.4).
3. **Its links and repositories, judged whole** *(C1-1.2, C2-1.1, C2-2.5)*. The look's last guards (`TreeSetAside.LastLookAsync`,
   `TreeSetAside.cs:70-135`) **apart from their lock test**, which refuses any lock (`:86`) and is replaced by step 2's: no link under the
   trees home on the way to it, no `assume-unchanged` or `skip-worktree` path, nothing unread. Then a walk of the whole slot, TREELINK1's (it
   never enters a link, skips the slot's own `.git` and looks at no more than a million entries, D51:108-112), which the last guards do not
   make. **A link is the pool's to carry only where npm's own workspace rule accounts for it** *(C2-2.5)*: it stands at
   `node_modules/<name>` or `node_modules/@<scope>/<name>` beside a tracked `package.json` whose `workspaces`, as the slot's tip tracks it,
   declare the folder it leads to; that folder lies inside the slot, whose path never changes *(R4)*; and that folder's
   own tracked `package.json` is named `<name>`. That is exactly the link npm makes for a workspace, and exactly what TREEDEPS1 copies
   (`TreeDependencies.cs:24-28`, `:279-299`). Any other link, where it lies or leads (a person's, a tool's, one npm would not make from the
   tracked declarations), holds the slot, said; it is never made again or moved. **A repository inside the slot** (a `.git` file or folder
   below its top) holds the slot wherever it lies: since a slot is handed on holding nothing ignored but `node_modules` (step 5), a finished
   line's repository under an ignored path has gone aside with its record before (§10), as OVERVIEWNOISE1a lets one go with a tree
   (`TreeSetAside.cs:116-124`).
4. **No git operation half done**: no `MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `BISECT_LOG`, `rebase-merge/` or `rebase-apply/` in its
   git folder (X-5).
5. **Nothing outside a ref, and nothing of the line's own** *(R4)*. `status --porcelain -z --untracked-files=all --ignored=matching
   --ignore-submodules=none`, the read `ContentHolds.HoldsAsync` makes (`ContentHolds.cs:565-566`), shows no tracked change and no untracked
   path, and **every ignored path is the slot's or Daoris's own**: the slot's are §11's `SlotKeeps` (a `node_modules` git ignores whole beside
   a tracked `package.json`, the slim's own judgement, `TreeSlims.cs:19-22`, and the build kinds §11 keeps), and Daoris's own are TREELOCAL1's
   declared `local/` copies (`TreeLocalInputs.cs:5-8`), made again at every take *(R4-F4)*. Any other ignored path is the line's own: its
   scratch (`_fixtures/` among it) or a kind §11 keeps from crossing lines. A detached slot meets this by its line's finish, which set the
   line's own aside (§10); a line that may come back and holds any of its own keeps its slot until the owner answers §18 item 4 *(C2-5.1)*.
6. **HEAD is where its line let it go, and a ref holds it**: for a slot at a line's path, the commit `line_ends` recorded, held by the line's
   branch (or its `GrownBranch.Now`, TREEFOLLOW1); for a parked or stuck slot, `kept_trees.tip`, held by the branch or the recovery ref its
   finish kept (`ContentHolds.cs:102-106`) *(O2-F4)*. A HEAD moved since
   on the line's own branch is the line's work, and the look's end records it before any take (§6) *(O1-F10)*; a HEAD moved anywhere else,
   by a person or by a build that does not know the pool, holds the slot: *changed since its line let it go* (X-5 step 4).

Judged whole before the choice, and **judged again inside the claim, right before the switch**: git's list, the lock, the binding, HEAD and
status, as §13.4 marks an effect, so a write between the two holds the slot (X's point; O's *made again and compared*); a write after the
switch is caught by §5.3 step 4's check *(C1-2.4, O1-F4, R4)*.

So **the first build resets nothing.** A slot holding a tracked change, an untracked file, a half-done merge or a commit no ref holds is
never taken, and *brought to the line* is a non-forcing switch of a clean working copy (§5.3), which writes tracked files and touches no
untracked one. The brief §5's *a reset that clears untracked work is a deletion* is met by clearing none until the owner says otherwise (row
i), and its *never resets a tree that holds work no ref holds* by steps 5 and 6.

### 5.3 The switch *(R4)*

A take brings a slot to a new line B; a return brings one to a returning line R. Both run as the take stage of the `Grown` phase, before
`GrowTree`'s own tree checks and inside the start's shared hold, where today's open runs (`GrowTree.cs:96`, `:110`) *(R4-F2, R4-F7)*; `GrowTree`
then finds the line standing in its slot. No step renames the slot, so round 3's park, placement, relink and two exclusive holds are gone.
Every step below is a phase of the claim (§2.3) *(C1-2.2, C1-2.4, O1-F4)*:

1. **Claimed.** The claim is written with its whole intent (§2.3), its `opens` key and its job's name among it, and the slot's free lock is
   swapped for the claim's reason. Where the slot serves a line that may come back (§5.1 choice 4), that line's `line_ends` is written first,
   with its tip, its read moment, its slot and its own ignored paths (none, by §5.2 step 5), so its record can be composed whenever it
   finishes *(C1-2.3)*. Then what may have gone stale is judged again (§5.2's last paragraph). Phase `judged`.
2. **Switched, in place, hooks held to step 5** *(R4-F14)*: for B, `git -c core.hooksPath=<an empty folder of the home's> switch
   --no-overwrite-ignore -c daoris/s-<B> <start>`, `start` being what `OpenAsync` grows a branch from (the line, a chain's `from`, its `landed`,
   a part's `partOf`), and the `GrownBranch` and its unit recorded as at an open, keyed by B's line path; for R, the same switch to its branch,
   or its `GrownBranch.Now`. Never forced: `--no-overwrite-ignore` refuses where the target tracks a path a kept ignored file stands at (X-5).
   Phase `switched`.
3. **Copied and judged.** TREELOCAL1's declared inputs copied again from the checkout's current files, read-only, by
   `TreeLocalInputs.CopyAsync`, as TREEGONE1's remake copies them *(R4-F4)*; npm judged per project (§11); only on the owner's yes to §18 item
   4, a live line's own ignored paths set aside (row j). Nothing else is removed at a take. Phase `copied`.
4. **The check** *(C1-2.4, O1-F4; X-5 step 9)*: git lists the slot at its folder and binds it to no other checkout
   (`TreeBinding.RefusalAsync`) *(R4-F5)*; HEAD is the target branch, at `start` for B and at R's recorded tip for R; `status --porcelain -z
   --untracked-files=all --ignored=matching --ignore-submodules=none` shows no tracked change and no untracked path; every ignored path is the
   slot's or Daoris's own (§5.2 step 5); every link in a kept `node_modules` is one npm's workspace rule accounts for. A path ignored on the
   previous line's branch and not at the target shows here as untracked, and a write that landed after the judgement as a change; either fails
   the check. A failed check frees the slot as §2.3's takeover would, marks it `stuck`, says why and drops the claim: **no record opens on a
   slot that did not pass**. Phase `checked`.
5. **The repository's `post-checkout` hook run**, once the check has passed, by `git hook run post-checkout` with the switch's own arguments,
   in the claim's job *(R4-F14)*: in the folder the line will work in, as an open runs it, so its output is the new line's to use, and it
   never reads as a write after the judgement. Its exit is said, never the switch's outcome.
6. **Opened, under the keyed open** *(C2-2.2)*: phase `opening-record`, the `opens` key already written; the record opened with `tree` the
   line's path, never the slot's, through FOUNDATION1's keyed open (`session_opens`), which row d extends to the pool's opens, since today only
   a moving open uses it (`ChatStarts.cs:221-227`) *(R4-F13)*; the agent started in the slot, `RunState.WorkTree` *(R4-F2)*; the session lock
   put in the claim's lock's place on the slot (§2.4); and one transaction writes `opened` with the record's id, deletes the claim, and writes
   the slot's `line`, `since` and an empty `tip`. A start that ends before its open frees the slot as the takeover would (§2.3), B's branch
   deleted through `JournaledDeletes.DeleteAsync` judged at `start` *(R4-F6)*.

The returning line's own slot (§5.1 choice 1) takes only steps 1, 4 and 6, in place: its HEAD must be on its branch, at its recorded tip or
moved on that branch by its own sessions; its own uncommitted work and its scratch stay its own *(C1-2.7)*. A returning line then has its kept
work laid back where row i kept it and its tip is still that work's base, and LANDSYNC2 brings it up to date with the line as today.

### 5.4 A refusal

- **A switch refused** (`--no-overwrite-ignore`, or a file held open), or **a check failed**: `stuck`, git's words or the check's said
  (`tree.kept-stuck`), another slot taken. A stuck slot stays where the switch left it, its `tip` recorded *(O2-F4)*, is judged again at each
  look, and is switched back to detached at that tip where git allows; it is freed only by a clean judgement (§5.2). **Nothing retires it but
  §9's proof** *(C1-2.4)*: the person's press on a stuck slot is today's press, which refuses what holds work no ref holds, naming it, and goes
  only when the person says it again meaning it (working-surface rule 7, `docs/2026-09-21-working-surface-design.md:97-99`). What a refused
  switch leaves of the tracked files on Windows is unmeasured (survey §5); row b measures it.
- **A nested repository, a person's lock, a link not the pool's, a binding to another checkout**: never reached, since §5.2 holds the slot
  first *(R4-F5)*.
- **The start's shared hold not had** (a replay or a writer holds the repository alone): the start is held for the next look, as today's
  open is *(R4-F7)*.

### 5.5 Work no ref holds, kept aside (row i, only on the owner's yes)

Until the owner agrees (§18 item 3), a slot whose line let go with work no ref holds stays that line's (§6, §7). With the yes, the Opus
reader's keep (O §1.4), with Codex's checks:

1. The last guards (§5.2 step 3) and a bound: past 10,000 paths or 64 MB of untracked bytes the slot stays its line's, said.
2. **Both states are kept, the index's and the working tree's** *(C1-2.6)*: what is staged can differ from what is on disk (a path staged at
   one version and then edited to another, a deletion staged or not). So two trees are written, neither touching the real index or the folder:
   `I`, the real index as it stands (`write-tree`, which reads the index and writes only objects), and `W`, the working tree through an index of
   its own under the slot's git folder **seeded from `I`, never from `HEAD`** (`read-tree <I>`, `add -A`, `write-tree`) *(C2-2.6)*: a path the
   real index tracks though the ignore rules match it (one added with force) stays tracked in the private index, so `add -A` records its working
   version, where an index seeded from `HEAD` would pass over it as ignored. Each is stored as a commit with **no parent**, its dates the
   let-go's and its message naming the branch, `HEAD` and which state it is, by create-only `update-ref`s at `refs/daoris/kept/<leaf>/<n>/index`
   and `…/worktree`; a `kept_work` row (workspace, repository, branch, index ref, worktree ref, base, index tree, worktree tree, paths, bytes,
   at; all required).
3. **Parentless on purpose**: `for-each-ref --contains <tip>` never lists either, so no ancestry proof, a shipped build's included, counts one as
   holding a branch (`ContentHolds.cs:379-384`); `ContentHolds.AnyRef` excludes the prefix too (`:432-433`).
4. Both made again and compared right before the slot is cleared for another line; then `reset --hard` and `clean -f -d` (never `-x`), then
   §5.3.
5. **Laid back in two steps** when the line returns at the same base: the working tree from `W` (`restore --source=<W> --worktree -- :/`,
   which removes what `W` lacks), then the index from `I` (`read-tree <I>`), each step a phase of the return's claim, so an interrupted lay-back
   is resumed from its phase and never read as done. The refs and the row are dropped once the slot's status and its index both equal the kept
   ones. Where the tip moved, nothing is laid back and the opening names the refs. Row i proves staged and unstaged versions of one path,
   **a force-added ignored path staged at one version and edited to another** *(C2-2.6)*, deletions staged and unstaged, and a lay-back stopped
   between its steps.

**Rejected:** loose files moved to a kept area outside the slot (X-6), since a move cannot keep a tracked edit and its payloads have no rule
that removes them (§1.1 row 3); Daoris committing the leftovers on the line's own branch, which lands what the agent never chose to commit and
turns a no-output end into output (O-6).

## 6. The end: done with output, done without output

At a run's end (the run's conclusion; the conversation's end in the chat runner), once its launcher's own job is empty
(`Agents/SessionProcesses.cs:514-519`; D201) *(R4-F14)*, the end claims the slot (`end`), reads it (§5.2 steps 3 to 6), writes `line_ends`
with the tip, the read moment, the slot and the names of the line's own ignored paths (§11), puts the free lock back, and the record says one
of the three below. Where an agent was not contained (`Platform/AgentJobs.cs`' reasons), that read of the slot is the only guard against a
process still writing, and the end says so.

**Who runs the end** *(O1-F10)*. The run's own end, and, idempotently, **the look**, for every slot that no holder holds (§7), that is under
no claim, and that has no `line_ends` row since its `since`: a park ended in another process, a dead `end` claim, or a session of a build that
does not know the pool, which left the slot under that build's rules. The look's end reads where HEAD stands: on the slot's line's branch (or
its `GrownBranch.Now`), moved or not, it records that tip, the line's own work; anywhere else, or with work no ref holds, it records `held`
and says why, for the person.

- **Done with output** (`ended.output`): commits on its branch, nothing uncommitted. The slot stays on the line's branch, locked
  `daoris kept tree`, free but for what §7 names, a due `land` node among them *(R4-F10)*. Ready to land: its run's `land` node lands from it,
  or from a slot switched to the line.
- **Done without output** (`ended.no-output`): no commit and nothing uncommitted. The slot is free and stays on the line's empty branch, where
  words to the line still find it, as a conversation's place does today, until a take switches it (§5.1 choice 4) *(R4)*; then the
  empty branch, with no tree, is the tidy's as an empty branch is today (`TreeSetAside.cs:27-30`), and `line_ends` saying `no-output` is the one
  licence to grow it again at the line's tip, under its own name, when its record goes on (O §1.3), so a conversation that ended with nothing
  can still be spoken to. No other line ever takes the name.
- **Done, with work it did not commit** (`ended.uncommitted`): tracked changes or untracked paths. The slot stays the line's, held, until a
  session commits the work, the person discards it, or row i keeps it aside. Daoris commits nothing on the branch (D164 point 1); today's
  landing refuses such a tree the same way (`TreeLands.cs:221-227`).
- **A park, a setup change, a helper wait**: as today, the record holds the tree (`Driver.cs:2910`).

`landing.nothing` (`Web/locales/en/note.json:133`) stays what a due landing with no commits says. A landing's steps are unchanged; only where
its tree comes from changes (§3.3), so LINESTOGETHER1, ACCEPTCONFLICT1 and GITAPP1 change nothing here.

## 7. What holds a slot

| Holder | Holds? | For how long | What frees it | What the person sees |
|---|---|---|---|---|
| A claim in flight | yes | its step | its end, or the look's takeover of a dead run's | — |
| A running record | yes, the ledger's key (`SessionLedger.cs:328-332`) | its run | its end | the rail's tree name (`identity.ts:141-144`) |
| A parked record | yes, the ledger's key (`SessionLedger.cs:1627-1630`, `:1856`) | until the person answers or ends it | the answer goes on there; *End* lets go | on the Projects row, *held: waits on you* |
| A visit: a terminal the person opened in it *(C1-2.5)* | against a move only *(O2-F7)*: a `tree_visits` row the shell writes before its shell starts (`Modules/Windows/TerminalModule.cs:202-218`), refused while a claim that moves the slot is in flight there; read by every process that takes a slot; the line's own claim in place and an `end` go on beside it | while the terminal runs, until its job is empty: its shell is joined to a job of its own at its start, as a heavy run's gates are (`ProcessJob.cs:23-26`) | its job empty; a dead shell's visits taken over by the look, its terminal's job having ended with it | — |
| A visit: a build served from it to a tab *(C2-3.2)* | against a move only: a `served` row written before serving begins, by the set-up's first showing and *Show it again* alike (`ReviewDesk.cs:231-236`), refused while a claim that moves the slot is in flight there; read by every process, where today a headless loop reads only waiting set-ups and its own desk's tabs (`BranchTidying.cs:734-743`) | while serving runs | serving stopped; a dead shell's taken over by the look | as today |
| A landing under way *(O2-F2)* | yes: the tree's landing hold, which every landing door takes around the whole landing and which is shared across processes (`TreeLands.cs:420`; `Trees/Landing/LandingHolds.cs:9-14`, `:61`), read by the judgement under the exclusive hold, a take passing over a slot whose landing hold is held | the landing | the landing's end | as today |
| Words waiting on an ended record | yes, the in-use set (`BranchTidying.cs:146`), which reads every record with words waiting (`Service/ServiceClient.cs:2265`) | for as long as they wait, which may be long: words held for the person's press (MSG1f) or for an account's reset (LIMITSWITCH3) among them *(O1-F11)* | the go-on, or the words taken back | — |
| A start due this look: an answered wait the planner will resume, or a line whose record a go-on will start | yes, read from the look's own plan | that look | the start | — |
| A helper wait (HELPERWAIT1) | yes (`BranchTidying.cs:147`; `SessionLocks.cs:91`) | while its quest is taken | the answer, or the quest closing | as today |
| A set-up waiting for its verdict (REVIEWENV1d) | yes (`BranchTidying.cs:148`), read from the service by every process | until the verdict | the verdict | as today |
| A standing move (a hand-over, a limit move, a briefed move) | yes: today's box beside the old part, `<s>.limit-move.json` or `<s>.hand-over.json` (`LimitBox.cs:24`; `HandOver.cs:15`), which every build writes and reads, **and** Part B's `moves` row, both read from row d on, since row f-i imports the boxes and stops writing them *(O1-F11, O2-F14)* | the move | the new part's open | as today |
| A due auto-landing | yes | until the look judges it | its close | — |
| Work no ref holds (§5.2 steps 4 to 6), a dirty lost take among it | yes | until committed, discarded or kept aside (row i) | a session or the person | `ended.uncommitted`, then `tree.kept-held` with why |
| A `stuck` slot | out of the pool | until a look's clean judgement | the look; the person's press only as today's press, which refuses what holds work no ref holds (§5.4) *(C1-2.4)* | `tree.kept-stuck` |
| A D79 wait, an ended conversation, a clean kept lost take, a done of either kind | no | — | — | on its return, its own slot if free there, else one brought back (`tree.returned`) |

Where they agree, both proposals' tables are this one: a park holds, a review holds, a helper wait holds, a D79 wait, an ended conversation
and a clean kept lost take do not; kept work is not kept equipment (X-7, which D79's and D178's notes record). Where they differ: words
waiting hold (O's, §1.1 row 8), for as long as they wait; a terminal and dirty work hold (X's), the terminal by a row every process reads,
since the shell's own table is no other process's *(C1-2.5, O1-F11)*. **A due landing holds** (neither's): the auto-lander closes a due landing
*gone* where its tree is gone (`AutoLander.cs:209`), so a slot taken under it would turn a landing due into a closed one. **A start due this
look holds** (neither's): it keeps a take from moving a line's slot away in the look its own start comes, which also narrows the race with a
build that does not know the pool (§14). **A served build and a landing under way hold** (X-7's served admission, dropped in round 1 and
restored, and neither's for the landing): each is read across processes where today it is not *(C2-3.2, O2-F2)*. **A visit holds back only a
move**, never the line's own claim in place, so a person's terminal open in a line's tree never stops that line going on in it *(O2-F7)*. Survey
§8.4's other holders are O's correction (§0.2 row 1).

**When every kept slot is held**, the next start opens a tree of its own as today (§5.1 step 5): nothing waits, and the Projects row reads
*Trees kept: 3 · in use 3*.

## 8. The count

- **Where it is declared:** `driver.json`'s `keptTrees`, a map from a repository to a number from 1 to 16, beside `trees`, which stays the switch
  (`Settings/DriverConfig.cs:19-22`, `:1383-1391`), read through Settings' one parser as a map of ints with no rule type (§14.3), kept unread by
  an older build as every unknown key is (foundation §13.1), and moved into the store's `settings` by z-ii with every other key (§13.3) (O).
- **Its default:** absent, `min(cap + 1, 16)` read live (`DriverConfig.cs:71-75`: a cap of 2, so 3) for a repository whose pool is on: the
  driven sessions the machine may run, all in one repository at most, and the person's conversation; clamped, since the cap has no ceiling
  (`DriverConfig.cs:552-553`) *(O1-F15, C1-3.2)*. Once DEV6 builds `laneCap` (D115:21-26), it reads `min(max(cap, laneCap) + 1, 16)`.
- **The effective count lowered by the cap** *(C1-3.2)*: a lowered cap lowers an absent count, and the slots above it retire by §9's rule, as
  under a lowered count: nothing held or holding work goes, a free one is moved aside whole and waits D195's days. The cap's own control and
  verb say so where a cap lowers a repository's kept trees, naming how many retire, which they learn through Settings' port (§2.5), since the
  verbs are Settings' and the slots Workplace's (`Settings/DriverChoicesCommand.cs:427-438`) *(O2-F8)*.
- **How it is set:** `daoris driver trees <repository> keep <n>`, `SET_TREES` gaining `kept`, the Projects row's control beside the trees switch,
  and Ask Daoris's `setting` door as for every key; **none of these doors lands before row f** *(C1-4.1)*, so no person can switch a pool on
  before lines come back through it and slots retire safely. A repository with submodules is refused, said, and so is any repository where the
  claim's job does not hold (§2.2), each judged by Workplace through Settings' port *(O1-F15, C2-2.3, O2-F8)*; a pool the default switches on
  (row h) meets the same refusal at every take, which needs no door *(C2-4.3)*.
- **Against the cap:** unchanged. The cap counts driven work, never the person's conversations (CAPCOUNT1, `Planner.cs:192-209`); set-ups'
  `atOnce` is unchanged; a conversation takes a slot as a driven start does, locked in its name, and counts under no cap.
- ***At most one slot per session the repository may run at once*:** the count keeps no more trees than can run at once, and lazy making keeps
  fewer where fewer ever ran (§9).
- **Past the count:** a tree of its own as today (§5.1 step 5), which today's tidy takes once its work is landed, and which, where its branch
  never moved, stays until the person's press as a conversation's place does today (`TreeSetAside.cs:18-20`) *(O1-F15)*. Whether that is the
  direction's meaning, or whether the count should bound the sessions that run in trees with the rest waiting, is the owner's question (§18
  item 1).
- **What it does not bound** (X §3): the trees past it, retired slots and finished lines' set-aside folders waiting D195's hour or days, and the
  `node_modules` growing in each kept slot.
- **Rejected:** `daoris.json`, which D115 rejected for lanes as the doctrine tool's inert contract (D115:58-59), a count being a machine's
  besides (its disk and cores); the registry, which is not machine-local; a store table of its own (§1.1 row 5); the cap at opt-in, stored
  (§1.1 row 6).

## 9. Lazy creation, lowering, and a repository leaving

- **Lazy creation.** The first start that finds no free slot and fewer slots than the count opens a tree as today (§2.2) and records it as the
  next number. A slot git no longer lists is dropped from the store; the next start that needs one makes another.
- **Lowering.** The slots numbered above the new count are `retiring`, highest first (X): each takes no new line; when free and judged as a take
  would judge it (§5.2), or parked, it is retired by a claim of its own (§2.3's `retire` phases) *(O1-F10)*: the slim's package folders judged
  (`TreeSlims.JudgeAsync`, `TreeSlims.cs:24`) and **the slim's own manifest written** at `judged` beside where the folder goes, a schema-1
  manifest, the slim's alone (`SlimManifest.cs:17-20`, `:28-29`) *(C1-1.3, O1-F8)*; its own lock lifted (git keeps a locked tree's record
  against a removal not forced twice); moved aside whole into `.tidied/<ws>/<repo>/<name>-<moment>` by the set-aside's own move
  (`TreeSetAside.cs:190-235`, `:313-341`), under the exclusive hold; the manifest at `moved`; git's record of the vanished path dropped as the
  set-aside drops it (`:252-270`). Its `node_modules` then goes at the slim's hour (`SetAsideKeeping.cs:87-90`), and the rest reads `unmarked`
  and waits the set-aside days (`SlimManifest.cs:19-20`). A held one goes at its let-go; one holding work no ref holds stays, said. Raising the
  count again takes back a retiring slot not yet moved.
- **Not `git worktree remove`**, which TREEKEEP1 refuses for ignored paths only the tree holds; the set-aside's move is the path the tidy
  already proves (O-9).
- **A repository leaving** (unregistered, or its trees switched off): every slot retiring. A checkout gone: every slot kept, said, since git
  cannot judge them.
- **The person's press** (the Branches tab, `daoris-driver trees clean`) on a slot retires it now, as above; on a held slot it refuses as today.
- **A slot that comes to hold a submodule** (its line added one after the pool was on) can be neither moved nor parked: it leaves the pool,
  said, its row removed, and its line is finished by today's set-aside of the whole tree, as a legacy tree's is *(O2-F15)*.
- **Never by age.** A slot leaves only by a lowered count (set, or the default the cap lowers), a repository leaving, or the person's press;
  the days that follow are D195's on the folder moved aside, as for every set-aside folder.

## 10. The finished record and removal

A pooled line's tree never ends: its slot stays the pool's. So **a line's finish is KEEPSIZE1b's own set-aside of everything the slot does not
keep** (D195:503-519; `TreeSweeps.cs:158-301`; `TreeSetAside.cs:190-341`) *(C2-2.4, O2-F6, C1-2.3, O1-F7)*: what today's tidy does to a
finished tree, but for the tracked files at the line's tip and the `node_modules`, which stay in the slot and go on to the pool. Its snapshot
is today's: KEEPSIZE1b's schema-2 manifest, written before anything moves or goes, its identity fixed when it is written, its steps
(`judged`, `moved`, `deleted`, `recorded`) and its one resume rule as today; round 2's `line_records` is withdrawn as a second home for the
same record. The look's tidy takes a pooled line's branch exactly where it takes today's (`GoesByItself`, `TreeSetAside.cs:27-30`), **a free
slot at its line's path read as that line's tree** (`Holds` and `Worked`, as today), so a no-output line's slot stays its conversation's place
*(O2-F5)*; and then:

1. **judges** it as today (the press's `Removable` and the look's wait for the line);
2. **holds the tip** on `refs/daoris/discarded/<branch>` wherever the line's own branch does not hold it (`TreeSweeps.cs:191-213`);
3. **writes the manifest** beside the set-aside folder `.tidied/<ws>/<repo>/<name>-<moment>`, before anything moves (`TreeSweeps.cs:220-230`):
   the read moment, the tip judged, what it grew from, its unit, the hold and its form, the recovery ref, the landing or why none, the scratch it
   leaves and `whole`, all read now while the branch and its `GrownBranch` stand (`Trees/Branches/SessionBranches.cs:180-202` may forget them
   once the branch goes); where the line's slot was taken while the line could still come back, the tree's own facts (its read moment; scratch,
   none by §5.2 step 5) come from the `line_ends` its take wrote. A manifest that cannot be written keeps the branch, as today;
4. **sets aside what the slot does not keep**, where the slot still stands at the line's path, under a `finish` claim on the slot: under the
   exclusive hold, each path the slot holds that git ignores whole and that is not a `node_modules` folder the slot keeps (§5.2 step 5) —
   build folders, the session's scratch, a repository under an ignored path — is renamed into the set-aside folder at its own relative place,
   as today's move takes the whole folder; the manifest at `moved`; and the slot read again, as today's set-aside reads the moved tree again
   (`TreeSetAside.cs:243-249`): anything changed puts it all back and keeps the branch. A refused rename (`busy`) puts back what moved and ends
   the finish for this look. Where the slot was taken earlier there is nothing to move, and the set-aside folder is made empty;
5. **parks the slot**, still under the hold: renamed to its parking name and detached there, `kept_trees.tip` written *(C1-2.7, O1-F9, O2-F4)*,
   so the finished line's path is empty, as after today's tidy, and nothing can go on in it detached; the claim is done;
6. **deletes the branch** at the commit judged in its one `update-ref --stdin` transaction (`ContentHolds.cs:456-475`), which refuses a branch
   checked out anywhere (`:468-471`); the manifest at `deleted`;
7. **posts the record** from the manifest to `POST /api/trees/record` (`Http/Program.cs:1446`), as today, with `Tree` the line's path and `Moved`
   the manifest's moment, so `WorkRecords.SessionsOfAsync` names the line's sessions and no other (`Core/Sessions/WorkRecords.cs:446-463`); the
   route answers a second post of a kept identity as the first (`Http/Program.cs:1442-1445`), and a lost post is retried from the manifest by
   the next pass, the press or a restart, as today; the manifest at `recorded`.

The set-aside folder then goes **by today's rule**: an hour after its record is kept, on a holding check run right before it goes, or its days
where its record waits (`SetAsideEligibility`; D195:520-523). So a finished line's build folders and scratch go exactly as they go today with
its tree, by the owner's decision of 2026-10-24 that a finished session's own scratch goes with its tree (D195:503-505), and **no build folder
of a finished line is ever handed to another line** *(O2-F6, O2-F1)*. **What `Left` and `Whole` mean** are today's: `Left` names the scratch
set aside with the record, `Whole` the person's `whole` choice as today *(O1-F7)*. **A no-output line** whose slot still stands at its path keeps
it as its conversation's place; once a take has moved its slot away, its empty branch, with no tree, goes as an empty branch does today, with no
record, as a no-output tree's press keeps none (D195:518-519).

**D195's kinds.** `trees`, rule `proof` (`Platform/SystemSpace.cs:37`), keeps the legacy trees and trees past the count; a new kind,
`kept-trees`, rule `count`, the person's number, counted from `kept_trees`; `set-aside`, rule `days`, gains retired slots. **The tidy's
`GoesByItself`:** its exception for a conversation's place (`TreeSetAside.cs:18-20`; D88:51-52) holds while a no-output line's slot is at its
path, and is moot once a take moved it away, the empty branch going and being grown again when its record goes on (§6). **KEEPSIZE1c to 1f stand**; 1d's allowance never counts a kept slot, which is live, and its set-aside part gains
retired slots.

## 11. Dependencies and build outputs kept

- **The one list:** TreeScratch's thirteen (`Trees/SessionTrees/TreeScratch.cs:15-19`) and `wwwroot`, an ignored web root no list holds today
  (survey §4). Each use names its subset of it, which is X's capabilities kept as subsets: **what a slot may hold for a take and what crosses a
  move** (`node_modules` alone in the first build, below) *(O2-F1)*; what a finish sets aside with the record (everything else, §10); what a removal lets go of (TREEKEEP1's, which stays
  `TreeScratch`'s thirteen byte for byte, `ContentHolds.cs:588-594`, so a legacy tree's removal reads as today *(C1-4.2)*); what the slim drops
  early (D195 point 3: a `node_modules` beside a tracked `package.json`, `KeepKinds.cs:11-20`); what TREEDEPS1 copies (npm's `node_modules`,
  `Trees/SessionTrees/TreeDependencies.cs:85-136`). A name never authorises a deletion by itself: a path is kept only where it is ignored and
  under the list. The unification is a dated note on D195 point 3, D102's TREEKEEP1 and D51's TREEDEPS1 (row b), as the brief §10 point 4
  asks; whether a removal should read `wwwroot` as a build folder is left to a correction of its own.
- **What crosses a move: `node_modules` alone, in the first build** *(O2-F1, C2-1.2, C2-5.1, O1-F3, C1-1.4, C1-2.8)*. A slot handed to another
  line carries what it holds to that line's path, and two things make anything but dependencies dangerous to carry. **Paths**: a folder that
  names its own absolute path reads whatever stands at its old path after a move, another line's tree within the hour (`.venv`'s launchers;
  NuGet's `obj/*.nuget.g.props`; a CMake or Ninja `build/` and an `out/build/<preset>`; a test assembly under `bin` carrying its content root).
  **Content**: an output another line built can run in place of this line's sources: this repository's CLI runs a kept `dist/cli.js` before its
  sources (`src/Daoris.Cli/bin/daoris.mjs:12-13`), the hazard its publishing already guards (`tools/desktop-publish.mjs:1260-1262`). So the first
  build's `CarriedFolders` is `node_modules` alone, beside a tracked `package.json`, which its producer rewrites from the lockfile and which the
  npm judgement below checks against that lockfile: its own top caches that name where they were made (`.cache`, `.vite`, `.vite-temp`, which
  TREEDEPS1 already leaves behind, `TreeDependencies.cs:27-28`, `:44-45`) dropped at the take (§5.3 step 3), its workspace links accounted for
  by the repository's own declarations and made again into the new path (§5.2 step 3, §5.3 step 6). Every other kind joins only by a dated note
  on row b's proof that, after a move with the old path holding another tree's sentinel files, it reads and writes only its own path, and that
  no output it carries runs in place of the line's sources.
- **Everything else**: **a finished line's** other ignored folders (its `bin`, `obj`, `dist`, `.venv`, `target`, its scratch) are set aside with
  its record at its finish, and go by today's rule an hour after the record (§10): that is what today's tidy does with them when it sets the
  whole tree aside, under the owner's decision of 2026-10-24 (D195:503-505), so it removes nothing today's rule does not already remove. **A line
  that may come back** and holds any of them keeps its slot (§5.2 step 5): handing that slot on would delete a live line's build folders, and
  no decision of the owner's lets Daoris remove those unpressed. D195 point 3 lets the slim take only a `node_modules` beside a tracked
  `package.json`, whose producer discards it whole at every clean install, keeps `bin/` from it for good and leaves `obj/` to later evidence
  (D195:21-29; `TreeSlims.cs:3-12`); the brief §2 reads D191 point 3's *anything that loses the owner's data* as removing a folder the
  install holds or resetting a tree that holds work; and a `.venv`, a `target/` or a `bin/` can hold what no clean install remakes. So **that
  removal is the owner's to allow: §18 item 4 asks it, row j builds it on a yes, and until then nothing but `node_modules`' own caches is ever
  removed at a take** *(C2-5.1)*. A slot that does not move (§5.1 choice 1) keeps everything.
- **What it saves, then**: npm's install (the measured 170 to 221 s) and the disk, on every take of a parked slot or a slot whose line built
  nothing else; a new line's .NET, Rust, Angular and Next builds start cold, as in a fresh tree today. Row b measures it before row d.
- **npm, per project after the switch (O-11):** **fresh** where the slot's `node_modules/.package-lock.json` is not older than the project's
  `package-lock.json`, npm's own staleness rule and TREEDEPS1's (D51:57-60): the agent is told to skip `npm ci` there; **stale, and the
  checkout's install is at the slot's lockfile byte for byte**: replaced by TREEDEPS1's copy, the old folder renamed aside within the slot and
  deleted never following a link (`TreeSetAside.DeleteNeverFollowing`), under D195 point 3's reason; **stale otherwise**: the agent is told the
  lockfile changed since its dependencies were installed and to run `npm ci` there. A switch that changes a lockfile writes it, so its time
  moves past the install's.
- **What the agent is told:** which dependencies were kept and whether they match their lockfiles, that no build output came with the tree,
  and, for a slot that did not move, that its own outputs are kept and a kept output is no proof that this commit builds (X-11).
- **The proof of what crosses** *(C1-2.8, C2-1.2, O2-F1)*: row b moves a slot, then makes a tree at its old path holding another line's
  sentinel files, and runs in the moved slot a node import of a workspace package, which must resolve inside the slot's new path, and **the
  repository's ordinary CLI entry** (`src/Daoris.Cli/bin/daoris.mjs`), which must run the new line's own sources, no `dist/` having come with
  the slot; and a slot whose line built `bin`, `obj`, `dist` or a `.venv` and may come back is never taken. For a kind proposed to join the
  carried set, the same proof with that kind: a run of its outputs **without** a rebuild (`dotnet test --no-build`; a CMake tree's build), a
  sentinel read or written at the old path failing it, and the CLI entry running sources. Then it reports npm's saving against a fresh tree.
- **TREEGONE1's remake where no slot is free** copies TREEDEPS1's where its gate passes (X-11; today it copies nothing, `TreeReturns.cs:105-109`),
  a dated note on D51's TREEGONE1 (row e).
- **Rejected:** copying at every take, 228 MB for nothing (D51:85-88; O-11); telling the agent to skip `npm ci` after a lockfile changed, which
  leaves a broken install; keeping a stale `node_modules` untouched (§1.1 row 10).

## 12. Git's rules and Windows

- **A branch checked out elsewhere.** A line's branch is only ever checked out at its own path, so never in two slots; a returning line whose
  branch the checkout has checked out is held, said, as TREEGONE1 holds it (`TreeReturns.cs:80-85`). Never `--force` or
  `--ignore-other-worktrees` (D51:157-158).
- **The lock reasons:** the four (an opening's, a session's, a pass's, a person's: survey §5) and the pool's, `daoris kept tree` free and
  `daoris kept tree (claimed by run <id>)` through a claim (§2.4); only Daoris's own are lifted, and a person's never.
- **`daoris/`** stays on every line's branch, so every proof's `--exclude=daoris/*` holds (`TreeFollows.cs:15-19`, `:61-68`); row i's kept refs
  sit outside `--branches` and `--remotes`, and are parentless.
- **The start window and the ledger's key:** `TreeLock` unchanged for starts; every rename of a slot under its exclusive form (§2.3); the claim
  keeps two takes off one slot and two claims off one destination; the ledger's key is the line's path.
- **`git worktree move`** refuses a locked tree unless forced twice, and a tree holding a submodule (git's documentation): the pool lifts only its
  own lock first, and **a repository with submodules is never pooled**, judged at every take and not only at the count's door, a slot whose line
  adds one leaving the pool (§2.2, §9) *(O1-F15, C2-4.3)*. **Into a destination that exists, even an empty folder, it moves the tree inside it**
  (its `move_worktree`, as the reader cites git's source) *(O1-F1)*, so a destination must not exist, a folder git does not list there moved
  aside first (§5.3 steps 2 and 9), and git must list the slot at exactly the destination after. **It renames before it
  writes the registration's `gitdir`**, so a failure between leaves the folder at the new path and the registration at the old, which
  `git worktree repair <new>` mends (§2.3) *(O1-F5)*. It is a rename, so a handle open without delete-sharing refuses it: `busy`, as the
  set-aside's rename (`TreeSetAside.cs:196-197`); an editor, an indexer, a terminal's working directory or a git file-system monitor daemon in
  the slot each make it so, which row g counts.
- **git's processes** run in a named job of the claim's own, made empty first, each joined before it runs, ended with the claim's process by
  the job's kill-on-close (`ProcessJob.cs:23-26`, `:77-83`), the job's name written in the claim; the takeover opens it by that name and waits
  for it to be empty before it reads anything (§2.3) *(C1-2.2, C2-2.3)*. That holds on Windows alone (`:15-18`), so **the pool is offered only
  there**: elsewhere the count's door refuses, said, and starts open trees as today (§2.2).
- **git's hooks are off** in every git step of the pool *(O2-F9)*: `-c core.hooksPath=` an empty folder of the home's, so a `post-checkout`
  hook (an install hook among them) never runs at a parking name and writes output tied to it; the opening says where a repository has a
  `post-checkout` hook that did not run (§15). A slot's first making is today's open, which runs the hook at the line's own path as today.
- **A switch refused by a held file:** `stuck` (§5.4); row b measures what it leaves with a file held open without delete-sharing and git's
  standard input closed, so Git for Windows never asks (O-12).
- **Long paths:** a slot's path is today's tree's, and every git call keeps `-c core.longpaths=true` (`Trees/WorkingTree.cs:974-981`).
- **Junctions in a kept `node_modules`:** a rename moves a junction as an entry and follows none; the ones npm's workspace rule accounts for are
  made again to lead into the slot's next path before it is placed there (§5.3 step 6) *(C2-2.1, C2-2.5)*; any other holds the slot (§5.2 step
  3); a retired slot leaves by the set-aside's move and the
  purge's never-following delete (D51:105-118).
- **What may hold a file:** a session's processes end with its job before its end reads the slot (ORPHAN1, `Platform/ProcessJob.cs:6-32`); a
  heavy run's builds run with `MSBUILDDISABLENODEREUSE=1` and end their job before the run lets its slot go (`Sessions/Heavy/HeavyRun.cs:47-51`);
  a served build's and a terminal's visits and a landing's hold hold the slot (§7); a scanner's hold on a just-written file reads as `busy` or
  `stuck` and is tried again.

## 13. D51's rejected alternative and rule 2

D51 rejected *one directory switching branches* because it *serialises exactly what needs to run at once and destroys the person's working
state on every switch* (D51:47-48). The pool keeps both reasons. Nothing that runs at once is serialised: N slots serve N lines, and a start
past them opens a tree as today. **And no line's directory switches between lines at all**: a line's directory is that line's for good, and a
slot changes branch only at its parking name, released, clean and checked, before it is renamed into the next line's directory, never the
person's checkout (rules 1 and 5) *(O1-F2)*. Working
surface rule 2's *named for the session, never reused* (`docs/2026-09-21-working-surface-design.md:77-82`) holds of the branch and of the path;
what is reused is a working copy's files. Rule 4's *a fresh tree holds nothing git does not track* (`:86-90`) holds of every tree opened fresh,
and a taken slot's sentence says what it keeps.

The note D51 takes (row b):

> *TREEPOOL1 (on acceptance):* rule 2's *never reused* is the branch's and its path's: a line's tree is named for its branch, and no other line's
> work is ever in it. A repository keeps a number of working copies; between lines one waits at a parking name no record names, where a
> non-forcing switch brings it to the next line's branch and a check finds nothing of another line's in it, and only then is it renamed into
> that line's path. Rule 4 reads: a tree holds nothing of another line's, tracked or untracked, but the npm dependencies it was handed, and the
> sentence that hands it over says which, and whether they match its lockfiles *(O2-F6, O2-F1)*. The tree stays the
> unit of exclusion and the ledger's key. The rejection of one directory switching branches stands: no directory holds two lines.

## 14. Records and older builds

| Record | What the pool writes | How a build that does not know the pool reads it |
|---|---|---|
| The service's `sessions.tree` | the line's path, as today | as today: the line's own tree, or gone and remade by TREEGONE1 on its branch (`TreeReturns.cs:61-109`), without the slot's dependencies, which its note says |
| A slot free at a line's path, locked `daoris kept tree` | — | its tidy refuses a locked tree (`TreeSetAside.cs:84-88`); its let-go lifts only a session's lock (`SessionLocks.cs:126-134`); its strays leave a tree git lists (`TreeStrays.cs:150-153`); its press's removal refuses a locked tree; its resume of that line goes on there, in the line's own tree (`TreeReturns.cs:58`), `HoldAsync` reading the lock as another's and taking none (`SessionLocks.cs:65`), so the session runs under the pool's lock and its end lets nothing go |
| A parked slot, `k-<n>`, detached, locked `daoris kept tree` *(O1-F2, C1-1.1)* | a folder no record names | its strays leave a tree git lists (`TreeStrays.cs:150-153`) and judge as an opening only an `s-` folder (`:183`); its TREEFOLLOW1 has no entry to learn for it (`TreeFollows.cs:101-104`); its tidy and press meet no branch of it; a parking folder git no longer lists, its strays move aside after ten quiet minutes (`TreeStrays.cs:70`, `:160`), which costs the pool a slot and nobody any work, since a parked slot holds nothing outside a ref |
| A claim in flight, its slot at a parking name or, under the exclusive hold, being renamed *(C1-1.1, C1-2.1, O1-F6)* | the pool's reason naming the claim's run | never read as an opening's, so its strays never delete a returning line's branch nor set the slot aside (`TreeStrays.cs:183-201`); while a rename runs, its starts' tree checks are held for a look (`Loop/Driver.cs:2110-2111`) and its tidy cannot hold the repository (`TreeStrays.cs:115`); between renames the slot is at a parking name no reader keys, on a new branch no record names or on a returning line's branch with its path empty, which its TREEGONE1 holds as *checked out in another tree* (`TreeReturns.cs:80-85`) |
| A finished line's path, empty after its finish *(C1-2.7, O1-F9)* | — | as after today's tidy: a resume of a landed line grows a fresh tree from its landing (`Loop/Driver.cs:2119-2127`); a no-output line's go-on finds its branch gone and is held, said (`TreeReturns.cs:70-77`), which the pool-aware build answers by growing the branch again (§6): a build without the pool is less able here, and loses nothing |
| `work_records` | today's shape and route | as today |
| `session-branches.json`, `landings.json`, auto-landings, manifests | today's | as today |
| git's registrations | slots, their names stable, their paths following lines | as worktrees |
| The agents' homes | the line's path | as today |
| The opening notes | new codes | an unknown code reads by its English; the path is cut wherever it travels (`Core/Sessions/SessionNote.cs:9-17`) |
| The store's new tables | new | builds of z-i and z-iii never open them (foundation §13.3) |
| `driver.json`'s `keptTrees` | a new key | kept unread; `trees` still opens `s-` trees |
| `refs/daoris/kept/` (row i) | parentless commits | its `AnyRef` would count one as a holder (`ContentHolds.cs:432-433`), but a parentless commit is never listed by `--contains` (`:379-384`), so no proof reads it as holding a branch |

A tree a build without the pool opens or remakes is read by a pool-aware build as a legacy tree: today's rules, never taken into the pool.

**What is left** (compatible-builds' *say what is left*) *(O1-F14)*. A build without the pool running beside a pool-aware one on the same home
(a second install, a `--share` loop or a verb beside the instance, or a person running an older build; D139's rollback cannot, since a take
waits for the start's confirmation to be settled, so a build rolled back from never moved a slot, §2.3 *(O2-F15)*):

1. **Its remake racing a return** of a line to that line's path: git refuses the second (the folder or the registration already there), and it
   is said.
2. **Its start racing a take of the same line's free slot.** Its resume checks the line's tree under the shared window and then takes the
   window again to open its record (`Loop/Driver.cs:2108-2117`, `:2184`), and a conversation's new part in its own tree takes no window at all
   (`Conversations/Chat/ChatRunner.cs:630-637`). Every rename the pool makes holds the exclusive window, which holds the first; and a take passes
   over a slot whose line has words waiting, a start due this look or a standing move (§7), which is the reason nearly every such start has, and
   which both builds read from the same service and the same boxes. What remains is a start whose reason the pool-aware build had not yet read
   when it judged the slot again under its hold: the folder is then gone at that start's spawn, so the start fails rather than running
   anywhere else, since its working directory is the line's own path, which no other line's slot ever takes, and what a spawn writes beside a
   session goes to the home, not the tree (`Agents/Permissions/SpawnServers.cs:28`). That failing start is read, not run; row d's pinned-build
   rehearsal runs it.
3. **Its resume of a line whose slot was taken** runs without the slot's dependencies and without work row i kept aside, as its TREEGONE1 note
   says of the first; the pool-aware build lays the work back at the line's next return.
4. **Its session in a line's own free slot** leaves HEAD moved on that line's branch and the slot under the pool's lock (`SessionLocks.cs:65`);
   the pool-aware look's end records it as the line's work (§6) *(O1-F10)*.
5. **A pool-aware process killed between a return's placement and its last check** leaves the slot at the returning line's path, on that
   line's own branch, until the pool-aware look's takeover moves it back to its park (§2.3). Its npm links already lead into that path, made
   again at the park before the placement (§5.3 step 6), so a build without the pool that resumes that line in that window runs on the line's
   own branch and reads nothing at another path *(C2-2.1)*.
6. **A finished line's set-aside folder** is today's: a folder in `.tidied` with KEEPSIZE1b's manifest, which a build that knows KEEPSIZE1b reads
   and purges as its own, and one before it purges by its days (`SlimManifest.cs:17-20`).

**Rejected:** a second session family for pooled lines (X-14), and the slot's own path in `sessions.tree` (O-14), both in §1.1 row 1.
compatible-builds asks for a new table for rows an older build must not reach (`.claude/knowledge/compatible-builds.md`); a record whose path
still names only its line is a row it may reach.

**Proof** *(C1-4.3, O1-F14)*: from row d, the first row that writes what such a build meets, **the deployment rehearsal runs a pinned build
from before the pool** over a pooled home: a resume, words going on, a conversation's new part, a tidy, a press, a parked slot, a finished
line's empty path and its set-aside folder, each beside a pool-aware loop taking and returning slots at once, and each again after a return
stopped at every phase. **Where that build comes from** *(O2-F13)*: the deployment rehearsal publishes this checkout only
(`tools/deployment-rehearsal.mjs`), so row d adds to it the last release's commit before row d, named by its tag in the rehearsal's fixture,
checked out as a detached worktree under `_fixtures/` from the repository's own objects, with no network, and published there by the same
publishing tool; **how real processes switch the pool on before any door** *(O2-F13)*: one environment variable, named in row d, read by the
driver for this alone and set only by the rehearsal and the tick tests, standing for `keptTrees` until row f's doors land. Pool-off runs of
this build's own code (`TreeReturnsTests`, the TREEGONE1 ticks) are kept beside it, never in its place.

## 15. The words, en and zh

A stored shape is never changed in place (`SessionTrees.cs:443-444`): `started.tree` and `started.tree-copied` keep their words for every tree
opened fresh, a slot's first making and a tree past the count among them. Codes carry the branch, the base, counts and why; no path and no
account (X-15). Each is one line in the head, within its budget (`docs/2026-10-20-ui-ux-design.md:199`).

| Code | en | zh |
|---|---|---|
| `started.tree-kept` *(O1-F15, O2-F1)* | Took a kept tree of `{{repository}}` for {{branch}}, from {{basedOn}}: its files are {{basedOn}}'s, and it keeps the npm dependencies earlier work installed ({{kept}}); no build output came with it. | 已为 {{branch}} 取用 `{{repository}}` 保留的工作树，基于 {{basedOn}}：文件与 {{basedOn}} 一致，并保留先前安装的 npm 依赖（{{kept}}）；没有带来任何构建产物。 |
| `started.tree-hook-skipped` *(O2-F9)* | The repository's `post-checkout` hook did not run when this tree was handed over; run the repository's own setup if it needs that hook's work. | 交接这个工作树时，仓库的 `post-checkout` 钩子没有运行；如果需要该钩子完成的工作，请运行仓库自己的设置步骤。 |
| `started.tree-kept-fresh` (one per npm project) | Its dependencies in `{{project}}` match its lockfile: `npm ci` is not needed there unless a lockfile changes. | `{{project}}` 中的依赖与锁文件一致：除非锁文件改变，否则无需在其中运行 `npm ci`。 |
| `started.tree-kept-stale` (one per npm project) | `{{project}}`'s lockfile changed since its dependencies were installed: run `npm ci` there. | `{{project}}` 的锁文件在依赖安装之后改变了：请在其中运行 `npm ci`。 |
| `started.tree-kept-built` (only where a line goes on in its own tree, which never moved) | Its earlier builds' outputs are kept, so builds there are incremental; a kept output is no proof this commit builds. | 它先前构建的产物已保留，构建是增量的；保留的产物不能证明当前提交可以构建。 |
| `started.tree-back` | Went back into its tree, where its branch `{{branch}}` still was. | 回到了它的工作树，分支 `{{branch}}` 仍在那里。 |
| `tree.returned` | Its tree had been taken for other work, so Daoris brought a kept tree back to its place, on its branch `{{branch}}`, where its commits are; its npm dependencies are kept, and its builds start afresh. | 它的工作树曾被其他工作取用，所以 Daoris 把一个保留的工作树带回原处，放到它的分支 `{{branch}}` 上，提交都在那里；npm 依赖已保留，构建需要重新进行。 |
| `started.tree-past-kept` *(C2-2.7)* | No kept tree of `{{repository}}` was free to hand on, so it opened a tree of its own, as before. | `{{repository}}` 没有可交接的保留工作树，所以它像以前一样打开了自己的工作树。 |
| `tree.visit-refused` *(C1-2.5)* | This tree is being handed to other work right now, so the terminal did not open in it; open it again in a moment. | 这个工作树正在交给其他工作，所以终端没有在其中打开；请稍后再打开。 |
| `review.show-again-moved` *(O1-F12)* | Set-up step `#{{quest}}`'s tree has served other work since it showed its build, so there is no build of its own to show again. | 设置步骤 `#{{quest}}` 的工作树在展示构建之后被用于其他工作，所以没有它自己的构建可以再次展示。 |
| `ended.output` | Done with output: {{count}} commit(s) on `{{branch}}`, ready to land. | 已完成，有产出：`{{branch}}` 上有 {{count}} 个提交，可以落地。 |
| `ended.no-output` | Done without output: no commits and nothing uncommitted. | 已完成，无产出：没有提交，也没有未提交的内容。 |
| `ended.uncommitted` | Done, with work it did not commit ({{count}} path(s)): its tree stays its own until that work is committed or discarded. | 已完成，但有未提交的工作（{{count}} 个路径）：在提交或丢弃之前，它的工作树仍归它所有。 |
| `tree.kept-held` | Kept tree `{{name}}` stays with its work: {{why}}. | 保留的工作树 `{{name}}` 仍归它的工作所有：{{why}}。 |
| `tree.kept-stuck` *(O2-F15)* | Kept tree `{{name}}` could not be brought to its branch, and git said: {{said}} | 保留的工作树 `{{name}}` 无法切换到它的分支，git 的说法是：{{said}} |
| `tree.kept-unclean` *(C1-2.4, O1-F4)* | Kept tree `{{name}}` was not handed on: after it was switched, it held {{count}} path(s) that were not its new branch's ({{paths}}). | 保留的工作树 `{{name}}` 没有交给下一项工作：切换后，它有 {{count}} 个路径不属于新分支（{{paths}}）。 |
| The Projects row | Trees kept: {{count}} · in use {{held}} | 保留的工作树：{{count}} · 使用中 {{held}} |

Round 2's `started.tree-kept-dropped` is withdrawn *(O2-F1, C2-5.1)*: a slot is handed on holding nothing ignored but `node_modules`, so no
build folder is ever dropped at a take for the sentence to name, and `started.tree-kept` says that no build output came with the tree; the
caches dropped from inside `node_modules` are TREEDEPS1's, which a copied tree never has either, and are not said.

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
store and its census (a), the take, return, finish and retirement over git with nothing wired (b), the readers that meet a gone tree (c).
**No door that sets a count lands before row f** *(C1-4.1)*: rows d and e read `keptTrees` only through a test's seam, so no person can switch
a pool on before lines come back through it (e) and slots retire and leave safely (f). **Row d starts only once row b's note is read** by both
makers and the steward finds §1.1 row 1's trade holds on its numbers (O1, §3), **and once FOUNDATION1's row 1d-i, the keyed open, has landed**
(§2.3) *(C2-2.2)*.

### a. The slots named beside today's trees, and the census. Nothing is pooled.

- **Changes.** `KeptTree`, `TreeClaim`, `TreeVisit`, `LineEnd` and `TreePool`'s store reads and writes; `SlotChoice` and `SlotJudgement`,
  pure, in `Domain/`; the store's migration step (`kept_trees` with its `tip`, `tree_claims` with its `opens`, its `job` and its unique index on
  `to`, `tree_visits` with its `kind`, `line_ends`, every column required, after the last step at build time, the writer stamp unchanged, as
  `bindings` was added, `Store/DesktopStore.cs:107-117`); **§13.4's dead-run takeover over `store_runs`, built once in the Store subdomain for
  this design and Part B's row f-i alike**, or taken from f-i where f-i has landed first *(O2-F14)*; Settings' `KeptTreesPort`, unset
  *(O2-F8)*; `PoolReadersCensusTests`, a by-name source scan in `ConfigSeamTests`' manner listing §3.2's re-keyed sites as *not yet moved*,
  each with its row. Nothing calls them. D212 takes a note that §13.3's workplace family gains these tables and that the takeover is shared.
- **The person sees.** Nothing.
- **Proof.** The store's cases: a build of z-iii never opens the tables, the stamp unchanged, a migration interrupted at each step leaving the
  store as it was; one claim per slot and one per destination with the shell, a headless `--share` loop and a verb racing; a visit and a claim
  racing for one slot, one refused; a dead run's claim taken over and a paused live one never, §13.4's three cases with live processes;
  `SlotChoice` and `SlotJudgement` over every row of §5.1 and §5.2; the census passing, and failing on each kind of planted site;
  `StructureTests` with no new census edge. Row a settles the shapes; the lifecycle's proof is row b's (C1, §4).

### b. ⚖ Taking, returning, finishing, ending and retiring a slot over git, unwired.

- **Changes.** `TreePool.MakeAsync`, `TakeAsync`, `ReturnAsync`, `FinishAsync`, `EndAsync`, `RetireAsync` and `VisitAsync`, each with its
  phases and its takeover (§2.3); the judgement's git reads, its own reading of the lock, and its whole walk of links, proved against npm's
  workspace declarations, and of repositories (§5.2); the park, the caches' drop, the switch with hooks off, the links made again to where they
  will stand, the two checks and the placement (§5.3); the finish as KEEPSIZE1b's set-aside of what the slot does not keep (§10); git's steps in
  the claim's named job, the takeover emptying it; the pool's lock reasons, `SessionLocks.HoldAsync` taking the free one over and the let-go
  putting it back (§2.4); the one build-folder list and its named subsets, `TreeScratch`'s own byte for byte, `node_modules` alone carried, with
  dated notes on D195 point 3, D102's TREEKEEP1 and D51's TREEDEPS1; D51's note (§13).
- **The person sees.** Nothing: no removal of today's reads differently *(C1-4.2)*.
- **Proof** (`TreePoolGitTests`, `TreePoolStopsGitTests` and a Windows `Process` class), each case seen failing first with its guard taken out:
  - **The brief §8's first proof, its clean half.** Line A commits on its branch, installs an ignored `node_modules`, builds an ignored `bin/`
    and `dist/`, and finishes; its finish sets `bin/` and `dist/` aside with its record and parks the slot; line B takes the parked slot at the
    line. B's path holds the line's tracked files and A's `node_modules` and nothing else of A's; A's branch holds its commits until its landing;
    A's path is empty; `BranchOfTree(B's path)` is B's branch; the slot's registration name is unchanged; A's manifest was written before
    anything moved and its record names A's sessions *(C2-2.4, O2-F1)*.
  - **No work lost:** no take of a slot holding an untracked file, a tracked edit, a half-done merge or rebase, an `assume-unchanged` path, a
    repository anywhere in it, a person's lock, a link npm's workspace rule does not account for (a person's junction inside `node_modules`
    leading into the slot among them), a HEAD moved since its let-go off its line's branch, a commit no ref holds, or, from a line that may come
    back, any ignored folder but `node_modules` *(C1-1.2, C2-2.5, C2-5.1)*.
  - **The lock** *(C2-1.1)*: a slot under the pool's free reason and one under no lock judged takeable; one under a person's lock or an unknown
    reason refused; the last guards' other tests still run.
  - **Holders across processes** *(C2-3.2, O2-F2, O2-F7)*: a *Show it again* serving from a slot in one shell while a headless loop tries to
    take it, refused by the `served` visit; an Accept under way in a line's own free slot while a take tries it, refused by the landing hold;
    a person's terminal open in a line's own slot while that line resumes there, which goes on beside it.
  - **Placement refused** *(O2-F3)*: a handle held open at the park during step 9's rename, and during a rename back; the park path runs at once,
    no branch is left checked out at the park, and the line's next start succeeds.
  - **Hooks** *(O2-F9)*: a repository with a `post-checkout` hook that writes into its working folder; the hook does not run at the park, the
    checks pass, the opening says it did not run.
  - **A parked slot's HEAD moved** *(O2-F4)*: a commit made at `k-<n>` by hand holds the slot, `changed since its line let it go`.
  - **The checks** *(C1-2.4, O1-F4)*: a write planted after the judgement and before the switch, through a seam as `BeforeRefDeleted` is
    (`Trees/SessionTrees/TreeContext.cs:40`), fails the first check; a `.gitignore` that ignores a kept folder on A's branch and not at B's start
    fails it; a write planted after placement fails the last; in each, no record opens, the slot is back at its park, detached and `stuck`.
  - **The path invariant** *(C1-2.1, O1-F2, C2-2.1)*: a look's TREEFOLLOW1 run between every two steps of a take and of a return never learns a
    branch other than the line's own at a line's path; an older resume's tree check run against each step is held for the look while a rename
    runs; a return killed after its placement leaves only links that lead into its own path.
  - **A pooled line's start in each crash state** *(O2-F11)*: held for the look while a claim names its slot or path, the takeover run first.
  - **The destination** *(O1-F1)*: an empty folder and a leftover folder at R's path, each moved aside before the placement, the slot never
    nested; two returns of one line from two processes, one refused by the unique `to`; a take of A's free slot racing A's return.
  - **Every boundary stopped** *(C1-4.3, C1-2.2, O1-F5, C2-2.2, C2-2.3)*: each rename stopped after the folder moved and before git's `gitdir`
    was written, a held `gitdir` file standing for it, and repaired; the caches' drop and the dependencies' replacement stopped halfway, the
    prefix finished by the takeover *(O2-F15)*; the switch done and its phase not written; the relink stopped halfway; **the open's answer lost**,
    the record then ended by the orphan sweep before the takeover runs, the takeover finding the open in `session_opens` and treating the slot as
    opened; the open never landing, the slot sent back to its park; the finish stopped at each of its phases; an `end` and a `retire` stopped at
    each phase; **a git process still running when its claim's process is killed**, the takeover opening the named job and waiting it empty
    before it reads anything, and a git lock file left behind holding the slot until a person removes it. Each leaves a state the next look reads
    right, and nothing at a line's path but that line's branch.
  - **The finish** *(C1-2.3, O1-F7, C1-2.7, C2-2.4, O2-F5, O2-F6)*: a landed line's tip held and its manifest written before anything moves;
    its `bin/`, `obj/`, `dist/` and scratch set aside into its record's folder and its slot parked; its branch deleted at the commit judged; its
    record posted with the line's path, and posted again unchanged after a refused post; the set-aside folder gone an hour after the record as
    today; a line whose slot was taken while it could come back, finished later, its manifest's tree facts from its `line_ends` and its folder
    empty; a no-output line's free slot at its path never finished while it stands; a branch checked out anywhere never deleted.
  - **What crosses a move** (§11) *(O1-F3, C1-1.4, C1-2.8, C2-1.2, O2-F1)*: with the old path holding another tree's sentinel files, a
    workspace import resolves inside the new path; the repository's CLI entry runs the new line's sources, no `dist/` having come; a returnable
    line holding `bin/`, `obj/`, `dist/` or a `.venv` is never taken; `node_modules/.vite` is gone after the move.
  - **Windows.** `git worktree move` refused by a file held open without delete-sharing (`busy`), the slot unchanged; a switch refused by a held
    file (`stuck`), what it left recorded in the row's note; a workspace junction leading into the old path made again into the new; a path of
    250 characters; a repository with a submodule refused.
  - **Measured, written in the row's note, read before row d:** an `npm` install's saving in a slot moved to a new path against a fresh tree;
    how long each exclusive hold lasts, the relink among it; how often a rename is `busy`.

### c. ⚖ The sites that meet a gone tree learn that a slot can come back; the opened moment; a taken line's review. Inert until d.

- **Changes.** The seams §3.2 marks row c: `ContentHolds.HoldsAsync`'s *opened* from `kept_trees.since` for a slot (a removal's judgement,
  hence ⚖) *(O2-F15)*; `Continuation.Judge`'s `treeStands`, `ConversationTrees.Stands`, the chat runner's gone-tree refusal
  (`ChatRunner.cs:630-633`), the briefed move's (`BriefedMove.cs:105-107`, `:188-190`) and the limit move's gates (`LimitMove.cs:75`, `:261`)
  asking the pool *stands, or can be brought back*, which answers by the disk alone while no pool is on; `SessionReviews` reading a pooled line's
  branch in the checkout when its tree is gone and unlanded; `ReviewServed.Stands` asking whether the slot at the path was placed after the
  showing *(O1-F12)*.
- **The person sees.** Nothing; every legacy answer is the same.
- **Proof.** Per seam, a fixture whose record names a line whose slot was taken answers *can be brought back*, and the legacy fixture answers as
  before; the review's graph held equal to its chain, its oracle (`SessionReviews.ChainReadAsync`, `SessionReviews.cs:67`); the census shrinks by
  these sites.

### d. ⚖ The pool wired, read only through a test's seam.

- **When.** After row b's note is read and FOUNDATION1's row 1d-i has landed (above).
- **Changes.** `keptTrees` read only through the one environment variable row d names, set by the rehearsal and the tick tests alone
  *(O2-F13)*; the driven start (`Driver.cs:2192-2221`) and the conversation's (`ChatRunner.cs:635-651`) taking from the pool (§5.1) before
  their tree checks, once the start's confirmation is settled *(O2-F15)*, else opening as today; their open made under the keyed open, which
  row d extends to these opens where 1d-i has not *(C2-2.2)*; the end, and the look's end (§6); the holders composed by Loop (§7): a due
  landing, a start due, the landing hold, today's standing-move boxes and Part B's `moves` both *(O2-F14)*, the terminal's visit taken by the
  shell before its shell starts and let go when its job is empty *(C1-2.5)*, and the served build's visit taken before serving begins
  *(C2-3.2)*; a pooled line's start held while a claim names its slot or path, the look's takeovers run before it plans *(O2-F11)*; the look's
  finish for a pooled line (§10); the press on a slot (§9); the words (§15); the Disk space `kept-trees` kind; dated notes on D88 and D195.
- **The person sees.** Nothing: no door sets a count.
- **Proof.** The brief §8's first proof as a tick test on a stand-in harness that records its working folder, as `SessionMessagesTickTests`'
  TREEGONE1 ticks do; each holder of §7 holding, the terminal's and the served build's visits from a `--share` loop's view and a verb's; a
  terminal opening while a claim that moves the slot is in flight, refused, and one open while its line resumes there, going on beside it; a
  start past the count opening a tree as today; a person's conversation that cannot have the exclusive hold opening a tree of its own; a take
  before the start's confirmation is settled, none; `CapCountTickTests` and `LookParallelTests` with the pool on; the brief §9's suites whole;
  the family rehearsal with one pooled repository; **the deployment rehearsal with the pinned pre-pool build** (§14) *(C1-4.3, O1-F14,
  O2-F13)*; `identity.test.ts` and `noteLines.test.ts` with the new codes.

### e. ⚖ Lines coming back through the pool.

- **Changes.** A return before the start's tree checks, bringing a free slot back to a pooled line's path on its branch (`tree.returned`),
  else today's remake (`TreeReturns.EnsureAsync`), now copying TREEDEPS1's where its gate passes; the go-ons and moves bringing a slot back
  before they go on (c's seams, switched on); a landing's `return` claim (`TreeLands.cs:196-204`, `:918-926`); a finished line coming back as
  §3.3 says *(C1-2.7, O1-F9)*; the no-output branch grown again; *Show it again*'s refusal (§3.2) *(O1-F12)*; dated notes on D51 (TREEGONE1),
  D79, D167, D178 and D189.
- **The person sees.** Nothing yet: still no door sets a count.
- **Proof.** A resume whose slot was taken finds its conversation: the stand-in sees the same folder and the same id; words going on; a further
  part on its line, and one alongside; a limit move held in its slot; a bring-forward; an Accept of a line whose slot was taken; a landed line
  coming back taking a slot for a new path from its landing, and a no-output conversation's go-on growing its branch again, neither ever
  detached; *Show it again* refused after a slot was brought back; `HandOverQuestTickTests` and `StartInSyncTickTests` whole; the pinned-build
  rehearsal again, its returns stopped at every phase. Its proofs stay behind the test's seam; the installed resume moves to the head of row g
  *(C2-4.2)*.

### f. ⚖ Lowering, a repository leaving, and the count's doors.

- **Changes.** `retiring`, raising, a repository leaving, a checkout gone (§9); a cap that lowers an absent count retiring as a lowered count
  does, said where the cap is set through Settings' port *(C1-3.2, O2-F8)*; a slot that comes to hold a submodule leaving the pool *(O2-F15)*;
  then, last, **the count's doors**: `daoris driver trees <repository> keep <n>`, `SET_TREES`' `kept`, the Projects control, Ask Daoris's
  `setting` door, a repository with submodules, or where the claim's job does not hold, refused through the port *(C1-4.1, O1-F15, C2-2.3,
  O2-F8)*; the environment variable of row d retired.
- **The person sees.** The count's control on the Projects row, off by default; *n more retiring* where a count was lowered with slots held.
- **Proof.** A lowered count, and a lowered cap under an absent count, each retiring a free slot (the slim's manifest written, moved aside,
  its `node_modules` gone at the slim's hour, the rest `unmarked` and waiting the days) *(C1-1.3, O1-F8)* and a held one at its let-go; one
  holding work no ref holds staying, said; a retirement stopped at each phase; `KeepSizeGitTests`, `CleanupLeftoversTests`, `KeepingSlimTests`
  and `SystemSpaceTests` whole; each door's case, and the one refusing a repository with a submodule; a slot whose line adds a submodule
  leaving the pool and its line finished by today's set-aside; `StructureTests` with the port's edge downward and the initializer in
  Workplace's folder.

### g. ⚖ On for the install's repository, measured.

- **When.** After rows a to f and the owner's yes (§18 item 2). The parent's, on the install.
- **First, the installed resume** *(C2-4.2)*: one Claude Code session in the pooled repository, its slot taken by a second start, then
  resumed: the conversation found (one session, one resume, nothing forced to a limit), on the owner's account under §18 item 5. A failure
  turns the pool off for that repository and is a finding for both makers.
- **Changes.** `keptTrees` set for the install's repository; no code.
- **The person sees.** Sessions in that repository start in a kept tree, said (`started.tree-kept`, `tree.returned`), and the Projects row's
  count.
- **Proof, measured before and after a day of the install's ordinary work** *(C1-4.4)*: the home's `work/` bytes split into legacy trees, kept
  slots, trees past the count and set-aside folders, from the Disk space card's kinds; **the time from a start's admission** (the planner's
  verdict for a driven start, the person's request for a conversation) **to its first turn**, over twenty starts each way, with the claim, the
  park, the drops, the switch, the checks, the placement, the links, the dependencies and the record's open apart, since today the tree is made
  before the record opens (`ChatRunner.cs:644-664`; `Loop/Driver.cs:2192-2223`); how often a start went past the count, a start was held behind
  another's exclusive hold *(O2-F10)*, a rename was `busy`, a check failed, a line was brought back, and a slot stayed its line's for its build
  folders, so how much choice 3 serves before §18 item 4 is answered *(O2-F6)*.
- **What permits row h** *(C1-4.4)*: over a week of row g, no record opened on a slot a check refused; nothing lost or held for the person by the
  pool's own steps; no slot `stuck` past a look but for a held file or a git lock file left; the median admission-to-first-turn of a take below
  a fresh tree's in the same repository; and the bytes under `work/trees` no longer growing day over day past the kept slots and the trees past
  the count.

### h. ⚖ The default flips.

- **Changes.** A repository with trees and no count keeps `min(cap + 1, 16)` slots. Once row g's criteria hold (above); the steward's call,
  told to the owner, since it carries out the owner's direction.
- **The person sees.** In every repository with trees, sessions starting in kept trees, and nothing new to set.
- **Proof.** The tick and rehearsal cases of rows d and e with the default rather than a set count; a repository with trees off unchanged
  byte for byte; **a repository with a submodule under the default, every take refusing it** *(C2-4.3)*; `DriverConfigKeepsTests` with and
  without the key.

### i. ⚖ Uncommitted work kept aside off its branch. Only on the owner's yes (§18 item 3).

- **Changes.** §5.5: `kept_work`, the keep, `AnyRef` excluding `refs/daoris/kept/`, the lay-back; D164's note.
- **The person sees.** A tree whose session left uncommitted work goes back to the pool, said in `ended.uncommitted`'s place by a line naming
  the kept ref; the work laid back when the line returns.
- **Proof.** The brief §8's first proof whole: A leaves an untracked file and a changed tracked file; B takes the slot at the line; neither is in
  B's path; both are in `refs/daoris/kept/…`; A coming back at the same base has them laid back. **Staged and unstaged apart** *(C1-2.6)*: a path
  staged at one version and edited to another comes back staged at the first and on disk at the second; **a path the ignore rules match, added
  with force at one version and edited to another, comes back the same way** *(C2-2.6)*; a deletion staged and one unstaged come back as each
  was; a lay-back stopped between its two steps resumes from its phase. Each keep commit is parentless and never listed by
  `for-each-ref --contains <tip>`; past the bounds the slot stays A's; a write after the keep holds the slot; on Windows a file held during the
  reset loses nothing of A's.

### j. ⚖ A live line's build folders set aside when its slot is handed on. Only on the owner's yes (§18 item 4) *(C2-5.1)*.

Round 2's row j, a finished line's scratch cleared at a take, is withdrawn: a finished line's scratch now goes with its record at its finish by
today's rule (§10), with nothing for the owner to answer *(O2-F6)*.

- **Changes.** §5.2 step 5 admits, for a line that may come back, ignored folders of the list besides `node_modules`; a take of such a slot
  moves them aside whole into a set-aside folder of their own beside the line's path's name in `.tidied`, with the slim's own manifest, where
  they wait D195's days and the line's person may take them back by hand; never deleted at the take; the line, when it returns, builds them
  again, said; D195's note.
- **The person sees.** A line that comes back after its tree served other work rebuilds its outputs, said in `tree.returned`.
- **Proof.** A returnable line's `bin/`, `obj/`, `dist/` and `.venv` set aside, not deleted, when its slot is taken, its tracked work and
  commits untouched; the set-aside folder waiting its days; the line's return rebuilding them; its `node_modules` carried; a slot holding a
  repository or a person's link still refused.

**How each input folds in.** FOUNDATION1: every row after mod-x; the tables Workplace's family by §13.3; one port, Settings', answered by
Workplace within D212's three bounds *(O2-F8)*; the standing move read from today's boxes and Part B's `moves` both from row d on *(O1-F11,
O2-F14)*; §13.4's dead-run takeover shared with row f-i *(O2-F14)*; row 1d-i's keyed open before row d *(C2-2.2)*. D51, D79, D88, D102,
D167, D178, D189 and D195: a dated note each, in the row that changes them; D164 only in row i. SWITCHCTX1: nothing taken. D115: the default
reads `laneCap` once DEV6 builds it. LINESTOGETHER1, ACCEPTCONFLICT1 and REVIEWFLOW1: unchanged; a head merging others does so in its own path.

## 18. What is the owner's

D191 point 3: a change of the product's direction, a spend beyond the development's own runs, a sign-in, a publish, a push or a history
rewrite, and anything that loses the owner's data, which the brief §2 reads as removing a tree, a branch or a folder the install holds today,
or resetting a tree that holds work.

1. **What the count means.** *What it is:* the direction says a repository keeps a number of slots, at most one per session it may run at once,
   and removes trees only when that number is lowered or the repository leaves. This design reads the number as **the trees a repository
   keeps**: when every kept tree is held, a start opens a tree of its own as today, which today's tidy removes once its work is landed, and
   which, where its session made no commit, stays until the person's press, as such a tree does today *(O1-F15)*. *What a yes does:* nobody
   ever waits for a tree, and a busy day still makes a few trees past the count. *What a no does:* the number bounds the
   sessions that run at once in trees, the person's conversations included, and a start past it waits until a tree comes free (Codex's
   reading), which this design does not yet specify, so a no means another round of it *(O2, §3)*. *Where to look:* §8, §5.1 step 5, §1.1
   row 4.
2. **The pool on for the install's repository** (row g). *What it is:* the repository the install works in keeps cap + 1 trees, three today,
   and a session there takes a free one, switched to its new branch and checked at a parking name and then moved to its own path, instead of
   opening a fresh one; a tree is handed on holding nothing but what git tracks at a ref and its npm dependencies, a finished line's build
   folders and scratch going aside with its record as today. *What a yes does:* sets `keptTrees` for that repository, runs row g's installed
   resume (item 5) and starts its day of measurements. *Where to look:* §5, §7, §11, row g, and the proofs of rows a to f.
3. **Daoris keeping a let-go line's uncommitted work aside, and clearing it from the tree** (row i). *What it is:* two parentless commits, one
   of what the line had staged and one of its working files, untracked ones included, on `refs/daoris/kept/…`, made by Daoris off the line's
   branch, where D164 point 1 has Daoris commit
   nothing; compared again right before the tree is reset for another line, and laid back when the line returns. *What a yes does:* trees whose
   sessions left uncommitted work go back to the pool. *What a no does:* such a tree stays its line's, said, until a session commits the work or
   the person discards it, and the pool opens another. *Where to look:* §5.5 and row i's proof.
4. **A live line's build folders moved out of its tree so the tree can serve other work** (row j) *(C2-5.1)*. *What it is:* a session that
   ended but may still go on (a question waiting on another repository, an ended conversation, unlanded work) leaves its tree holding build
   folders it made: `bin/`, `obj/`, `dist/`, a Python `.venv`, a `target/` and the like. Today nothing removes those from a live line's tree
   unless the person presses; D195 lets the slim take only a `node_modules` folder unasked, and keeps `bin/` from it for good. Handing that
   tree to another line would mean moving those folders out of it. *What a yes does:* such a tree can be handed on: its build folders are moved
   aside whole into a folder of their own under `.tidied`, never deleted at the moment of the take, and are deleted after D195's set-aside days
   (fourteen by default) unless the person takes them back; the line rebuilds them when it comes back, and is told so. *What a no does:* such a
   tree stays its line's until the line finishes (its build folders then go with its record by today's rule, an hour after the record), and a
   busy repository opens trees past the count as today; in this repository, whose sessions build and test, the pool then reuses mostly trees
   whose lines are finished. *Where to look:* §11, §5.2 step 5, row j; row g measures how often a tree stays for this reason.
5. **One real resume on the install** (row g's installed proof) *(C2-4.2)*. *What it is:* one Claude Code session and one resume in a pooled
   repository, on one of the owner's accounts, to prove the conversation is found once its tree was taken and brought back. *What a yes does:*
   runs it once, bounded to one session and one resume with nothing forced to a limit, unless an authorization the repository records already
   covers it. *Where to look:* row g.

Round 2's item 4, a finished line's scratch cleared at a take, is withdrawn: a finished line's scratch now goes with its record at its finish,
which is today's rule under the owner's decision of 2026-10-24 (D195:503-505), and asks nothing new *(O2-F6)*.

**Not asked:** the install's trees and set-aside folders, since nothing here removes or converts them (§16); lowering a count, which is the
person's own act and whose retired trees wait D195's days; **a finished line's build folders and scratch set aside at its finish**, since today's
tidy already sets a finished tree aside whole and deletes it an hour after its record, under the owner's decision of 2026-10-24 (D195:503-505,
:511-519), and the finish removes nothing that rule does not *(C2-5.1)*; dropping `node_modules`' own top caches at a take, which lie inside the
one folder D195 point 3 lets go unasked and which TREEDEPS1 never copies (`TreeDependencies.cs:27-28`); a publish, a push, a history rewrite or
a sign-in, none of which any row needs.

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
| A no-output branch gone for good (round 1's misreading of X) *(O1-F13)* | §1.1 row 12, restated: both grow it again; the licence lives in the store |
| *Round 2:* moving a slot to a line's path before switching it (round 1's §5.3) *(C1-2.1, O1-F2)* | the line's path showed another line's branch for the length of a relink, which TREEFOLLOW1 records and an older resume goes on in; parked, switched and checked first, placed last |
| *Round 2:* a slot's destination keyed by nothing but the slot (round 1's claim key) *(O1-F1)* | two returns of one line, or a take racing a return, both move into one path and the second nests |
| *Round 2:* OPENCUT1's lock through a take or a return (round 1's §2.4) *(O1-F6, C1-1.1)* | any build's strays read a killed return as a cut-off open and delete the returning line's branch; and a take killed between move and switch is no opening to them at all |
| *Round 2:* opening a record without reading the slot after its switch (round 1's §5.3) *(C1-2.4, O1-F4)* | a write after the judgement rides the switch into the new line, and a path ignored only on the old branch is committed by the new agent's `add -A` |
| *Round 2:* retiring a stuck slot *whatever it holds* (round 1's §5.4) *(C1-2.4)* | a partial switch can leave work no ref holds; a stuck slot goes only by today's press, which refuses that and asks again |
| *Round 2:* carrying every listed folder across a move (round 1's §11) *(O1-F3, C1-1.4)* | `.venv`, NuGet's restore files and the build caches read whatever stands at their old path, another line's tree within the hour |
| *Round 2:* the keep made from `HEAD` and the working files alone (round 1's §5.5) *(C1-2.6)* | a path staged at one version and edited to another came back with the second only |
| *Round 2:* a takeover by phase alone (round 1's §2.3) *(C1-2.2, O1-F5)* | a git child may outlive its run, and a rename may land before git writes its registration: the disk is read first |
| *Round 2:* a terminal's hold known to the shell alone (round 1's §7) *(C1-2.5)* | a `--share` loop or a verb would take a slot a terminal stands in |
| *Round 2:* the count's doors at row d (round 1's §17) *(C1-4.1)* | a person could switch a pool on before lines come back through it or slots retire safely |
| *Round 2:* the finished record's inputs left to the store with no table (round 1's §10) *(C1-2.3, O1-F7)* | a line whose slot was taken before it became removable had no tree left to read, and a retried post had no fixed identity |
| *Round 3:* carrying `bin`, `dist`, `build`, `out`, `coverage`, `wwwroot` or `__pycache__` across a move (round 2's §11) *(O2-F1, C2-1.2)* | a kept `dist/cli.js` runs in place of the line's sources; a CMake tree, a test assembly's content root and `out/build` name their old path |
| *Round 3:* dropping `.venv`, `obj`, `target`, `.angular`, `.next` or `.gradle` at a take as already allowed (round 2's §11) *(C2-5.1)* | for a line that may come back, a deletion no decision of the owner's allows; put to the owner (§18 item 4) |
| *Round 3:* carrying a finished line's scratch into the next line's tree (round 2's §5.2 step 5) *(O2-F6)* | it hands one line's notes and settings to another, contradicts rule 4, and grows without bound; it goes with the record by today's rule |
| *Round 3:* `line_records`, a second home for a finished line's record (round 2's §10) *(C2-2.4)* | KEEPSIZE1b's manifest already is that snapshot, written before anything moves or goes, with its resume rule and its hour |
| *Round 3:* a placed slot whose links are made again after it is placed (round 2's §5.3) *(C2-2.1)* | a crash between leaves a line's path whose links lead into another path, which an older build resumes through |
| *Round 3:* a link carried for where it lies (round 2's §5.2 step 3) *(C2-2.5)* | location is no authorship: a person's junction inside `node_modules` would be retargeted |
| *Round 3:* an open known by its record's liveness (round 2's §2.3) *(C2-2.2)* | an open whose answer was lost and whose record the orphan sweep ended reads as never opened |
| *Round 3:* a dead run's git children taken as ended by the run's death, or by git's lock files alone (round 2's §2.3) *(C2-2.3)* | a git process may outlive its run and write after the takeover read the disk; the job is opened and emptied first, and the pool is offered only where it can be |
| *Round 3:* a served build seen only by its own shell (round 2's §7) *(C2-3.2)* | a headless loop would move the tree another shell serves from |
| *Round 3:* a visit that holds back every claim (round 2's §2.3) *(O2-F7)* | a person's terminal would stop its own line going on in its own tree |
| *Round 3:* the pool's git steps running repository hooks at the parking name (round 2's §5.3) *(O2-F9)* | an install hook writes output tied to the park, so every take in that repository would stick its slot |

## 20. What stays unmeasured, and what this design's checks do not cover

- **What the pool saves**, now that only `node_modules` crosses a move (§11): row b measures npm's saving against a fresh tree, its note read
  before row d; and whether any other kind can be carried, by row b's isolation proof with that kind. If the saving left is too small to carry
  the trade of §1.1 row 1, a later round weighs it again with that number, never silently.
- **How often a live line's build folders keep its tree** until the owner answers §18 item 4: row g.
- **Whether a junction can be made to a folder not yet standing**, as §5.3 step 6 makes one at the park for its place in `to`: TREEDEPS1 writes
  a junction's reparse data with no process (`TreeDependencies.cs:326-330`) and the reader reads that as indifferent to whether its target
  stands, not run here; row b proves it, and where it is not so, the links are made right after the placement, inside the same exclusive hold,
  before the last check, and a slot is never left placed with a link into another path.
- **Whether a job object stays open by its name while a process in it runs**, which the takeover's proof of quiet rests on: read from the
  platform's documentation, not run; row b kills a claim's process mid-git and proves the job empty before the takeover reads the disk.
- **`git worktree move` on Windows**: its refusal with a handle held, the registration name it keeps, its nesting into a destination that
  exists and its rename before its `gitdir` write (both as the reader cites git's source, not run here), and what a refused switch leaves: row b.
- **How long the exclusive hold lasts** at each rename and the relink, and how often it is not to be had: row b, and row g on the install.
- **Off Windows**: the pool is not offered, since the claim's job holds nothing there (`ProcessJob.cs:15-18`) *(C2-2.3)*.
- **Whether a start of a build without the pool fails cleanly** when the folder of its line's path went between its check and its spawn
  (§14 item 2): read, not run; row d's pinned-build rehearsal runs it.
- **Whether Claude Code reads anything of a directory but its path when it resumes**: row g's installed proof (§18 item 5).
- **How often every kept tree is held** so a start goes past the count, and how often a line is brought back: row g.
- **The cost of making links again** after a move in a large `node_modules`: row b, with TREELINK1's bound of a million entries.
- **An agent other than Claude Code and Codex that keys a conversation by path**: none is read here; a plugin's door is read by its provider's
  contract (D213), which this design does not change.
- **A build without the pool beside a pool-aware one on one home**, racing a return: said (§14), not measured.
- **Whether a folder keeps its creation time across a rename**: assumed, and no longer read for a slot (§3.2).
- **What this round's checks do not cover.** It is a document: `npm run verify` and the documentation checks prove its routing and shape, not
  its claims about the code. Round 3's claims were read at `3dd134cf3` for every line both reads of round 2 cite and every line round 3 adds or
  re-cites (§23.3); round 2's at `f92e95415` (§22.3); round 1's other lines rest on its reading at `f8fbb1bb7`, re-cited where LIMITSWITCH3 and
  row p moved them (*Read at*), and the survey's and the proposals' other lines are taken as they cite them. git's own behaviour
  (`worktree move` and its nesting and `gitdir` order, `worktree repair`, `switch --no-overwrite-ignore`, `-c core.hooksPath`, `write-tree` from
  the real index, `read-tree` into a private index), npm's workspace links, the testing host's content root and a CMake tree's absolute paths
  are taken from their documentation and the readers' citations, not run. FOUNDATION1's row 1d-i, on which a start's open rests, is designed
  and not built. Nothing was run. Both makers' reads of round 3 check it next.

## 21. Rounds

| Round | Written | Read | Answered |
|---|---|---|---|
| 1 | 2026-10-26, Claude Opus 5.5, from main at `f8fbb1bb7`: the synthesis of Codex's and the independent Opus reader's sealed proposals; every question of the brief answered (§1); each difference chosen with its reason and the case the other gets wrong (§1.1); one choice neither proposal's, the slot moved to its line's path (§1.1 row 1, §2); the rows in order with their proofs (§17); D214 proposed | both at `3f55eb1e5` (`docs/2026-10-26-tree-pool-reads.md`): Codex, another round (18 findings, 12 blocking); the independent Opus reader, another round (15 findings, 4 blocking), both keeping the central choice | §22, every finding of both |
| 2 | 2026-10-26, Claude Opus 5.5, from main at `f92e95415` (FOUNDATION1's mod-vii and LIMITSWITCH3 landed since round 1): both reads answered one row each (§22); every blocking finding taken and met by a contract and the row that proves it; the slot parked between lines, switched and checked under a parking name and placed last under the exclusive hold, its destination unique and absent; the claim's whole intent, every kind's phases and a takeover that reads the disk first, git's steps in a job; a terminal's visit in the store; `line_records`; only what reads its own path carried across a move; the keep of index and working tree both; the count's doors at row f; the install measured from admission; D214's round-2 note | both at `d595cabd6` (`docs/2026-10-26-tree-pool-reads.md`): Codex, another round (15 findings, 8 blocking; its round-1 1.2, 2.2, 2.3 and 2.6 open in the cases they name); the independent Opus reader, accept after named changes (15 findings, 1 blocking; F2, F3, F4, F5, F7 and F11 before row b), finding no case in which the pool loses work | §23, every finding of both |
| 3 | 2026-10-26, Claude Opus 5.5, from main at `3dd134cf3` (FOUNDATION1's row p and LIMITSWITCH3b landed since round 2): both reads answered one row each (§23), every blocking finding taken and met by a contract and the row that proves it; `node_modules` alone carried across a move; a finished line's other ignored folders set aside with its record by today's rule; a live line's build folders the owner's question (§18 item 4) and row j's; the finish KEEPSIZE1b's own set-aside of what the slot does not keep, `line_records` withdrawn; links made again before placement and proved by npm's workspace declarations; the open under FOUNDATION1's keyed open; the claim's named job emptied by the takeover, the pool on Windows alone; a served build's admission, a landing's hold, visits that hold back only a move; the keep's private index seeded from the real index; hooks off at the park; one port for Settings; D214's round-3 note | owed: Codex's, and the independent Opus reader's as a foundation read | — |

## 22. Round 2: both reads of round 1 answered

Moved at round 4 to `docs/2026-10-26-tree-pool-reads.md`, *Moved from the design at round 4*, as round 2 wrote it.

## 23. Round 3: both reads of round 2 answered

Moved at round 4 to the same place, as round 3 wrote it.

## 24. Round 4: the independent read of round 3 answered

The independent Opus 5.5 reader read round 3 at `23958cb903`, verbatim in `docs/2026-10-26-tree-pool-reads.md`: 14 findings, 1 blocking, and
another round asked, scoped to F1; Codex has not read round 3. The owner answered F1's question on 2026-11-07 with a fixed slot path, so round 4
reopens §1.1 row 1, as the read's verdict foresaw, and takes F2 to F14 under it. Where a finding was about the moving slot's mechanics (the
renames, the parking name, the relink), those mechanics are gone and the row says so; where it named something the code added since round 3,
the pool now uses that rather than standing beside it (D228). Every row was checked at `75a4b694be`.

| Finding | Verdict | Where it went, and the row that proves it |
|---|---|---|
| F1 (blocking) No take saves the first build; round 3's saving measured against `npm ci`, which TREEDEPS1's copy already avoids | **Taken**, by the owner's choice | A slot keeps a fixed path, `k-<n>` (§2.1, §2.2), so its outputs that name their path stay valid, and its `node_modules`, `bin/`, `obj/` and the web's build are the slot's own (§11). The saving restated against today's copy and measured in a fresh worktree of this repository (§11 *What it saves*): the two desktop test projects build cold in 98 s, unchanged in 12 s, after a switch over three hours of main in R4_INCR; the web's build runs whole either way. The record's path stays the line's, so the hazard of §0.2 row 5 is bounded another way: no record names a slot (§3.1, §14). What the fixed path gives up is said in §1.1 rows 1 and 11, §4 and §19. The read's §18 item 6 is not added: the owner has answered it. Row b measures a warm build against a cold one at one path and proves them equal; row g measures a take's admission to first turn against a fresh tree's |
| F2 Every `Trees/` and `Loop/Driver.cs` citation moved; the take belongs in the run's stages | **Taken** | *Read at*: the map, and every line round 4 writes read at its base. §5.3: the take is a `[RunStage]` of the `Grown` phase run before `GrowTree` (`Loop/Run/Tree/GrowTree.cs:11`), its claim on `RunState` and let go with the run as `Starting` is, so a run that ends before `OpenRecord` gives the slot back through the run's own release; `RunState` gains the line's path beside `WorkTree` (`RunState.cs:200-202`), the record opened with the first and the agent started in the second. The conversation's take sits where `ChatStarts` chooses its tree (`Loop/Chat/ChatStarts.cs:188-215`). Row d |
| F3 Two holders added since round 3, `TidyHolders.LineHolders` and `SpokenTo` | **Taken** | §5.2 step 1 and §7 read `TidyHolders` as the one rule, a held line's path holding the slot its line is in; §7's *a D79 wait … no* row is withdrawn, since the newest part of an open or taken quest holds its tree whatever its state (`TidyHolders.cs:99-108`) and a conversation spoken to within the hour holds (`:30-37`); so choice 3 narrows to closed quests' lines and conversations not spoken to within the hour (§5.1). §3.3 adds TREEGONE2's *Go on with Daoris's brief* and TREEKEEPRETRY1's fresh tree for a part that left nothing (`GrowTree.cs:36-50`). Row d |
| F4 TREELOCAL1's machine-local inputs | **Taken** | §5.2 step 5 names them Daoris's own copies, beside what the slot keeps and never the line's scratch; §5.3 step 3 copies them again at every take after the switch, from the checkout's current files, by `TreeLocalInputs.CopyAsync` under the claim, as TREEGONE1's remake copies them; the finish and the retirement delete them never following a link, clearing the read-only mark first (§9, §10). Row b (a taken slot's gates read the checkout's private patterns, a pattern the checkout changed since the slot's last line included) |
| F5 The binding check is missing from the take | **Taken** | §5.2 step 2 and §5.3 step 4: `TreeBinding.RefusalAsync` (`TreeReturns.cs:63`) at the judgement and at the check; a bound slot is `stuck`, said in the binding's words (§15). Row b |
| F6 A second way to delete a branch | **Taken** | §2.3, §5.3: a take that ends before its open deletes the branch it made through `JournaledDeletes.DeleteAsync` judged at `start` (`JournaledDeletes.cs:31-33`), which verifies the commit and keeps the Undo; `update-ref -d` is withdrawn, and the census's one exception (`JournaledDeletesTests.cs:118`) stays the opening's. §10's delete is the finish's one journaled act, `ContentHolds.DeleteJudgedAsync`, as today's tidy deletes. The person's Undo of a pooled line's delete brings back the branch alone; the line's path stays empty, as a pooled line's always is, and the line comes back by §3.3 into a free slot switched to it. Row b (a take ended before its open; an Undo after a finish) |
| F7 GITLOCK1's census, holder name and lock order | **Taken** | With no rename a take holds no exclusive form: it runs inside the start's shared hold, where today's open runs (`GrowTree.cs:96`, `:110`), so it needs no holder name; the finish and the retirement run inside the look's tidy under `Tidying` (`TreeLock.cs:23-41`). The pool's git writes join `CheckoutWritersCensusTests` as an own-tree writer (`switch@slot`, `worktree@root` for its lock and unlock); the journal's delete is named already. The landing hold is only tried, never waited for, under any hold (§7). Row b (the census failing on an unnamed call) |
| F8 The act engine's job and the store's claim, not new ones | **Taken** | §2.3, §12: a claim's job is made as an act's is, `ProcessJob.CreateNamed` under a name derived from the claim as `ActJournal.JobOf` derives an act's (`ActJournal.cs:215`; `ActRuns.cs:76`, `:163`), each git started suspended in it and joined as `ActSteps` starts one, and the takeover opens it by that name, ends it and waits for it empty as `ActReconciler` does; `tree_claims` gains `claim_generation` beside `claim_run`, and a takeover is one guarded update of both, as `ActJournal`'s take is (`:396-411`) and `MoveStore`'s. The pool's steps are not the journal's acts, which are the person's acts on a checkout and refuse any write into a session's tree (git area rule 39). Row a (two lookers reading one claim dead, one fenced) |
| F9 The standing move is `moves` alone | **Taken** | §7: a standing `moves` row, and an older build's box only while it stands unimported; *both from row d on* withdrawn. Row d |
| F10 Landing is a run's `land` node | **Taken** | §3.2, §3.3, §7, row e: the re-keyed sites are `LandPerformer`'s two `TreeFolders.TreeGone` reads (`LandPerformer.cs:97`, `:146`), which for a pooled line ask the pool where its line stands; the holder is a run whose `land` node is due or under way; the auto-lander's rows and the Accept-in-place door withdrawn, and the two auto-landing suites dropped from §17. Row e |
| F11 The integration tree is a neighbour and a precedent | **Taken** | §2.2 names `i-` (`IntegrationReplays.cs:76-78`). Both are now warm trees kept at a stable path; they stay two makings, not one, because a slot holds a line's work and is never reset, while the integration tree holds none and is put back by reset, clean and checkout. Whether one kept-tree type serves both is left open (§20). Row a's census names `i-` beside `k-` |
| F12 Set-aside folders also go by size | **Taken** | §9, §10, §11, §18: wherever a set-aside folder is promised to wait, it waits D195's days or its kind's size limit, whichever comes first (KEEPSIZE1). Row f |
| F13 The takeover's read of the keyed open when the service does not answer | **Taken** | §2.3: an unanswered read is not *no row*; it holds the claim for the next look and never frees the slot. Row d extends the keyed open to the pool's opens, which today only a moving open uses (`ChatStarts.cs:221-227`). Row b |
| F14 Nits | **Taken**, each | (a) §8: `keptTrees` per repository, its doors following the per-repository `trees` (`Settings/DriverConfig.cs:557`), the machine settings list unchanged; (b) §5.3 step 5, §12: the repository's `post-checkout` hook now **runs**, since the switch is made in the folder the line works in, where its output is valid, as the act engine's steps and today's open run hooks; it runs once the check has passed, so its output never reads as a write after the judgement; `started.tree-hook-skipped` withdrawn; (c) §6: the end's proof that a session's processes are gone is its launcher's own job (`Agents/SessionProcesses.cs:514-519`), and where an agent was not contained the end's read of the slot is the only guard, said; (d) §0.3, §17 and D214 say 1d-i is built |

**The code, checked.** Every line the read cites was read at `75a4b694be` and holds as it cites it, but where *Read at* re-cites it. Round 4's
claims that rest on other programs, not run here: that a .NET build made incrementally after a switch equals a clean one at the same path,
which deterministic compilation leads one to expect and row b proves byte for byte; that Claude Code files a conversation by its working folder
(SWITCHCTX1 §1.5); and that MSBuild's incremental clean removes a project's outputs its new build no longer makes. What was measured is §11's.
