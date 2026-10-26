# A repository's session trees kept and reused, each session's work a branch (TREEPOOL1, 2026-10-26: round 2, proposed)

**Standing (round 2, proposed under D191, D214):** **Both makers read round 1** at `3f55eb1e5` (`docs/2026-10-26-tree-pool-reads.md`,
*Codex, round 1* and *Opus (foundation), round 1*), and each asked another round: Codex 18 findings, 12 blocking (its 1.1, 1.2, 2.1 to 2.7,
4.1, 4.3 and 4.4), the independent Opus reader 15, 4 blocking (F1 to F4). **Both keep round 1's central choice**, the slot moved to the path
of the line that takes it, and both block on its mechanics. **Round 2 is written by Claude Opus 5.5 from main at `f92e95415`**; every finding
of both was checked against the code there (§22.3) and is answered in §22, one row each, and each changed passage of §0 to §20 is marked
*(C1-n.m)* for Codex's §n item m or *(O1-Fn)* for the Opus reader's finding n. Where the reads meet, the stricter is taken and one change meets
both. **The path invariant during a move** (C1-2.1, O1-F1, O1-F2, C1-1.1, O1-F6): a slot is never at a line's path but on that line's branch;
between lines it waits at a parking name no reader keys (`k-<n>`), is switched and checked there, and is renamed into a line's path last, its
destination one no other claim may name and one that must not exist, every rename under the repository's exclusive hold, which every build's
start and tidy honours (§5.3). **The check after the switch** (C1-2.4, O1-F4): the slot is read again at its parking name and again at its
destination, and a record opens only on a clean read (§5.3 steps 7 and 11). **The crash protocol** (C1-2.2, O1-F5, O1-F10): every claim
records its whole intent, every kind (take, return, finish, end, retire) has its phases and its takeover, git's steps run in a job that ends
with the claim's process, and the takeover reads both folders, git's registration and git's own lock files before it acts (§2.3). **The
finished record** (C1-2.3, O1-F7): what needs a line's tree is captured at every let-go and every take, and the composed record waits in a
table of its own until the service acknowledges it (§10). **What crosses a move** (O1-F3, C1-1.4, C1-2.8): only folders proved to read and
write their own path are carried; `.venv`, `obj/` and the build caches that name their path are dropped at a move (§11). **The rows**
(C1-4.1, C1-4.3, C1-4.4): no door sets the count until returns and retirement have landed, the crash and pinned-build proofs are named, and the
install is measured from a start's admission (§17). **Both makers read round 2 before any of it is accepted, and nothing is built until
then**, nor in any case before FOUNDATION1's row mod-x (D212 §14.6), which has not landed (D212's notes record mod-i to mod-vii built); the
rows marked ⚖ are built only after both makers' reads find nothing blocking in them. Nothing here is built, spends an account, signs in,
publishes or deletes.

**Round 1's standing, as it was written (proposed under D191, D214):** the synthesis of the two sealed proposals, Codex's
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

**Read at** main `f92e95415` for round 2. Since round 1's `f8fbb1bb7`, LIMITSWITCH3 shifted `Loop/Driver.cs` (5 lines on past its line 981,
6 past 1992, 8 past 2335, 13 past 2405 and 16 past 2683) and `Conversations/Chat/LimitMove.cs` (one line on past its line 336, so round 1's
`:346` and `:350` are `:347` and `:351`), and touched `Loop/DriverContinuations.cs` and `Trees/SessionTrees/SessionTrees.cs` only below every
line cited; FOUNDATION1's mod-vii moved the Events files (`NoteCodes`, `HomeWatch`, `Notices`, the session's event vocabulary) into `Events/`,
none of them cited by line. So every line of §1 to §20 that moved is re-cited in place (`Loop/Driver.cs:2099` is `:2105`, `:2100` `:2106`,
`:2127-2131` `:2133-2137`, `:2138` `:2144`, `:2169-2172` `:2175-2178`, `:2186-2215` `:2192-2221`, `:2191-2207` `:2197-2213`, `:2560-2570`
`:2573-2583`, `:2886-2894` `:2902-2910`), §0.2 keeping round 1's lines as round 1 read them; every other file cited is unchanged since
`f8fbb1bb7`, and every line round 2 adds was read at `f92e95415` (§22.3). Round 1 read as follows.

**Round 1 read at** main `f8fbb1bb7`. The survey read the code at `6b999d6b1` and both proposals at `a3f5be21a`. Between `6b999d6b1` and `f8fbb1bb7`
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

### 0.3 The inputs, folded

| Input (the brief §3) | Folded as |
|---|---|
| FOUNDATION1, D212 | The ground. `Trees/` is Workplace (§14.2 row 10, `docs/2026-10-25-foundation-design.md:2342`), so every new type is Workplace's or lower (§2.5). The claim is a store family by §13.3 (`:1970-2001`), in Workplace's family (§13.3's row, `:1987`), with §13.4's claim by process run (`:2042-2066`). No lower subdomain needs the pool's answer, so no port and no module initializer is added; a row that finds one keeps the three bounds of D212's note of 2026-10-26. Every row lands after mod-x (§14.6). Part B's `moves` (the course's family, §13.3, row f-i) is read for a standing move once it lands, today's boxes until then *(O1-F11)*; the shell's terminal reaches the pool by a call down into Workplace *(C1-2.5)* |
| D51 and its notes; the working surface | Rule 2 and rule 4 amended by note, the rejected alternative kept (§13); TREEDEPS1's gate read again for a kept slot (§11); TREELINK1's link rule applied after a move (§5.3); TREELOCK1's reason kept for a held slot and its *not covered* closed for a conversation's (§2.4); TREEGONE1's remake becomes, where a slot is free, a slot brought back, its fallback copying TREEDEPS1's (§3.3, §11) |
| D88, D102, D195 (CLEANUP1, KEEP1, KEEPSIZE1a/b) | The tidy's set-aside of a pooled line becomes *the line's finish*, KEEPSIZE1b's own steps with *park* where it has *move aside*, its inputs captured while the tree stands *(C1-2.3, O1-F9)* (§10); the one list of build folders TREEKEEP1's with `wwwroot`, the slim's and the copy's its named subsets (§11); D195's kinds gain `kept-trees` (§10); KEEPSIZE1c-f stand |
| D167, D178, D189 | Their rules stand: the entry's tree, the kept take's tree and the resume's tree are each the line's path, which a free slot is brought back to (§3.3, §4); each takes a clarifying note |
| SWITCHCTX1 §1.5 | Read as the reason the slot moves and the line does not: Claude Code files a conversation and keeps its trust by the working directory's path (`Platform/ToolHomeLeftovers.cs:30-31`, `:55`; `Accounts/SignIns/ClaudeTrust.cs:108-125`), Codex by date (`ToolHomeLeftovers.cs:41-42`). Its carry is not needed here |
| D115, DEV6 | The count's default reads `laneCap` once DEV6 builds it (§8) |
| LINESTOGETHER1, ACCEPTCONFLICT1, REVIEWFLOW1 | Unchanged: a landing's steps are today's; a landing whose line's slot was taken has one brought back first (§3.3) |

## 1. The two proposals, question by question

The question numbers are the brief's §7.

| Q | X | O | Chosen |
|---|---|---|---|
| 1 **The slot** | `s-<n>` under `<home>/work/tree-pools/<pool-id>/`; a durable store lease per occupation with a generation; a permanent pool lock | `t-<n>` under `<home>/work/pool/<ws>/<repo>/`, a new home entry; a store claim per step, the record holding the slot after; the lock a session's while held, `daoris kept tree` while free | **Neither's place: a numbered slot at its line's path while that line may use it, else at a parking name no reader keys, and moved to the path of the next line that takes it (§2.2) *(O1-F2)*; O's claim per step, reserving the slot from its choice to its record's open, with its whole intent and a destination no other claim may name (§2.3) *(C1-2.2, C1-3.1, O1-F1)*; the pool's own lock throughout, OPENCUT1's only at a slot's first making (§2.4) *(O1-F6)*; the store, as both** |
| 2 **The line and its branch** | create-only `daoris/s-<full id>` per branch work unit; readers by unit; no folder fallback for pooled lines | `daoris/s-<8 hex>`; `branch_parts`; readers by the session's branch; `BranchOfTree` null for slots | **Today's name and derivation: the folder is named for its line's branch, so `BranchOfTree` and its callers stand (§3)** |
| 3 **The line's identity** | the entry, a `BranchWorkId`, a part and a lease; every path reader moved to them | the branch; the path kept only as *the folder the record ran in* | **The path, as today, since no line's path ever holds another line's work, nor its own work off its branch (§3.1) *(C1-2.1, O1-F2)*; the dozen sites that meet a gone tree re-keyed (§3.2), the ways back in §3.3, a finished line's among them *(C1-2.7, O1-F9)*** |
| 4 **The conversation** | native resume only once the exact original directory is reacquired; else a new part with the brief (`workplace-moved`); D189 amended | the record's slot if free; else a new part with the brief (`fromWhy=tree`); D189 amended | **A free slot brought back to the line's own path, so its conversation and trust are found there; D189's rule stands, a clarifying note (§4)** |
| 5 **The start** | journalled; HEAD pinned; loose files moved out; `reset --keep`; a non-forcing switch with `--no-overwrite-ignore`; verified | `reset --hard`, `clean -fd` after a parentless keep commit; then a switch | **X's non-forcing discipline, its pin, its unfinished-operation check and its check after the switch (X-5 step 9), dropped in round 1 and restored *(C1-2.4, O1-F4)*; parked, switched and checked under a parking name, then placed (§5.3) *(O1-F2)*; the first build takes only a clean slot, so no reset and no clean; O's keep commit, of the index and the working tree both, on the owner's yes (§5) *(C1-2.6)*** |
| 6 **The end** | output; no output; uncommitted work holds the equipment; loose files kept apart | output; no output; uncommitted work kept aside | **Both ends, said once; uncommitted work holds the slot (X) until O's keep on the owner's yes (§6)** |
| 7 **What holds a slot** | a table; words waiting do not; a terminal and dirty work do | a table; words waiting do for one look; an extra tree so nothing waits | **The union where they agree; words waiting hold (O), for as long as they wait *(O1-F11)*; a terminal, admitted by a row in the store *(C1-2.5)*, and work no ref holds hold (X); a due landing and a due resume hold (neither) (§7)** |
| 8 **The count** | a Workplace store policy, `treePools` on export; default the cap at opt-in, stored; a bound conversations wait on | `driver.json`'s `keptTrees`; default cap + 1 read live; an extra tree past it | **O's place and default, clamped to 16, a lowered default retiring as a lowered count does *(C1-3.2, O1-F15)*; past the count a tree as today; what the count means put to the owner (§8, §18)** |
| 9 **Lazy creation, lowering** | rows declared, folders lazily; lowering drains highest first; a departure presents what stays | made lazily; retiring slots moved aside, read `unmarked`, the days | **Both, said once; retirement by the set-aside's own move with the slim's manifest (O) *(C1-1.3, O1-F8)*, highest first (X) (§9)** |
| 10 **The finished record** | at equipment release; new pool tables; immutable end refs; an outbox | at the branch's deletion; a new route `POST /api/lines/record`; `branch_ends` | **KEEPSIZE1b's own steps and route at a line's finish: the path is still the line's, so its attribution holds; what needs the tree captured at every let-go and take, the composed record kept in `line_records` until acknowledged (§10) *(C1-2.3, O1-F7)*** |
| 11 **Dependencies** | one catalogue with capabilities; lockfile evidence; no silent delete; TREEGONE1's remake seeded | npm's own staleness rule; replaced from the checkout where it matches; `bin/` kept | **O's rule and list, `wwwroot` only in what a take carries *(C1-4.2)*; X's capabilities as the list's named subsets, its words and its seeding; links made again after a move; only what reads and writes its own path carried across one, the rest dropped (§11) *(O1-F3, C1-1.4, C1-2.8)*** |
| 12 **Git and Windows** | non-forcing; a held preparation; contained jobs empty first | non-forcing; `stuck`; measured | **Both; `git worktree move` and its refusals, a destination that must not exist, a repository with submodules never pooled, git's steps in a job of the claim's own (§12) *(O1-F1, O1-F15, C1-2.2)*** |
| 13 **D51** | the work's identity never reassigned, equipment reused between completed occupations | rule 2 the branch's; rule 4 restated | **Stronger than both: neither the branch nor the path is ever reused; no directory ever holds two lines (§13)** |
| 14 **Records and older builds** | pooled sessions in a new service family with doors of its own; older builds blind to them | the slot's path in `sessions.tree`, an older build reading it as no tree of its own | **Neither: every record keeps today's meaning, since a path stays its line's; X's family not needed, O's reading wrong at the code (§0.2 row 5, §14); the race between a build without the pool and a take, named and bounded (§14) *(O1-F14)*** |
| 15 **The words** | old codes kept; new codes with no path; a table in en and zh | new codes; a table in en and zh | **Both's rule; the codes of §15** |
| 16 **The install's trees** | none adopted; any removal an owner-reviewed plan | none adopted; they drain | **Both: none adopted, none removed by this work; they drain by today's rules (§16)** |
| 17 **The order** | P1 to P9, reuse off until P8 | 1a to 1k, on per repository at 1f | **a to j (§17): the first three both's shape (the store and the census, git unwired, the readers); d and e wired with no door; the count's doors only at f, once returns and retirement stand *(C1-4.1)*; d waits on b's measurement being read *(O1, §3)*** |
| 18 **The owner's** | seven | four | **Four, and one bounded spend (§18)** |
| 19 **Proof** | per row; the decisive fixture; kills at every boundary | per row; each case seen failing first | **Both, per row (§17)** |

### 1.1 Where they differ, and which way

| # | The difference | Chosen, and why | The case the other gets wrong |
|---|---|---|---|
| 1 | Where a slot's folder sits: a place of its own (both), or the path of the line that holds it | **The line's path, the synthesis's own** (§2.2): a slot is moved to the path of the line that takes it and brought to the line there. Every reader of a path keeps reading one line (§3.1), every record keeps its meaning to builds already shipped (§14), and an agent's conversation is found where it was (§4). The costs, said *(O1-F3, O1-F2, O1-F1)*: two renames per take, each under the repository's exclusive hold and each `busy` where any handle holds the tree; a slot waiting at a parking name between lines, never at a line's path off that line's branch; a destination guarded against a second claim and a leftover folder; and every folder that names its own path dropped at a move rather than carried (§11), so the build saving a move keeps is what row b measures before row d | **O's:** a shipped build resumes a line into its slot's folder by `Directory.Exists` (`Loop/Driver.cs:2133-2137`) or goes on with words there (`DriverContinuations.cs:171-178`, `:227`), on whatever branch another line left; and a line whose slot another line holds loses its agent's own memory to the brief. **X's:** a second session family in the service with doors of its own, which every reader of sessions merges (the page, the trace, the channel, asks, history), and a shipped build that can neither show nor continue a pooled line |
| 2 | The claim: a durable lease per occupation with a generation (X), or a step's claim, the record holding the slot after (O) | **O's, with X's reservation and X's check** *(C1-3.1)*: the claim reserves the slot from its choice until its record opens, the ledger holding it only after that open (`Core/Sessions/SessionLedger.cs:1602-1616`), and the slot is read again before the open (§5.3); after the open, whether a slot is held is read live from the ledger, the in-use set, the claims and the visits (§7) | X's: a lease kept through the occupation needs the same reconciliation TREELOCK1's let-go does for git's lock (`SessionLocks.cs:96-123`) whenever a record ends in another process; it is a second record of what the ledger already says, kept in step at a cost, not impossible. The choice is economy |
| 3 | Work no ref holds before a reuse: moved out to a kept area (X) or kept in a parentless commit (O) | **Neither in the first build**: a slot holding such work is not taken (§5.2). **O's keep commit** on the owner's yes, row i (§5.5) | X's: a file move cannot keep a tracked edit, so X holds those slots anyway, and its payloads (`<home>/work/tree-pool-kept/`) have no rule that removes them (X §6 item 6), so they grow until the owner disposes of each. O's before the owner's yes: a reset of a tree that holds work, which the brief §2 reserves to the owner |
| 4 | The count: a bound sessions wait on, the person's conversations included (X), or the trees a repository keeps, a start past it given an extra tree (O) | **O's**: past the count a start opens a tree of its own as today, which today's tidy takes once its work is done; the reading is put to the owner (§18 item 1) | X's: the person's second conversation in a repository whose two slots hold parked sessions waits for a tree, where today a conversation opens its own at once (`ChatRunner.cs:635-651`). O's, and this design's: a strict reading of the direction's point 7 removes trees only by the count, and a tree past the count is removed by the tidy |
| 5 | Where the count is declared: a Workplace store table (X) or `driver.json` (O) | **O's** | X's: a setting outside §13.3's `settings` family, which holds one row per key of `driver.json` read through Settings' one parser (`docs/2026-10-25-foundation-design.md:1972-1990`); the export and import would need a case of their own, and z-ii a second home for configuration |
| 6 | The default: the cap at opt-in, stored (X), or cap + 1, read live (O) | **O's, clamped to 16** *(O1-F15)*, a lowered default retiring slots as a lowered count does, said where the cap is set *(C1-3.2)* | X's: a person who raises the cap later keeps the old number of kept trees and runs past it every day. O's unclamped: a cap of 16 or more (`Settings/DriverConfig.cs:552-553` sets no ceiling) asks for more slots than the count allows |
| 7 | The lock: one permanent pool reason (X), or TREELOCK1's while held and a kept reason while free (O) | **O's**, the pool's own reason also through every claim, OPENCUT1's opening lock only at a slot's first making (§2.4) *(O1-F6)* | X's: the session lock is how every shipped reader tells a tree's holder (`SessionLocks.cs:42-54`); a permanent pool reason hides the holder from them and from a person reading `git worktree list` |
| 8 | Words waiting on an ended record: no hold (X) or a hold (O) | **O's** | X's: the in-use set keeps such a tree today for the next look's go-on there (`BranchTidying.cs:146`; D88's AUTOTIDY2d); freed for that look, a start takes the slot and the go-on must bring another back, a rename and a switch for nothing |
| 9 | The finished record: new tables and refs (X), a new route (O), or KEEPSIZE1b's own | **KEEPSIZE1b's** route and shape, at a line's finish, its inputs captured while the line's tree is there and the composed post kept until acknowledged (§10) *(C1-2.3, O1-F7)* | Both: a second shape for one record. Here the path is the line's, so the route's rule by path (`Core/Sessions/WorkRecords.cs:446-463`) names exactly the line's sessions |
| 10 | A stale `node_modules`: kept untouched (X) or replaced from the checkout where its install matches (O) | **O's**, under D195 point 3's reason that npm discards the folder at every clean install | X's: kept stale with the agent told to run `npm ci`, which deletes it first; keeping it gains nothing, and the copy at a matching lockfile saves the install |
| 11 | A line whose slot was taken when it comes back: a new part handed the brief (both) | **The slot is brought back to the line's path** (row 1) | Both: a D79 wait answered after another start took its slot resumes from the brief, not its conversation, and O's own §2.4 expects it often enough to measure (`fromWhy=tree`) |
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

A slot is a linked worktree of the repository's registered checkout that the repository keeps, numbered `n` in its pool and known by
`(workspace, repository, n)` and by git's own name for its registration, which a move does not change. **Its folder sits at the path of a
line only while that line may use it, and only on that line's branch**: `<home>/work/trees/<workspace>/<repository>/s-<8 hex>`, today's
layout (`Trees/SessionTrees/SessionTrees.cs:354-356`; `Platform/DaorisHome.cs:405`). **Between lines it waits at a parking name**,
`<home>/work/trees/<workspace>/<repository>/k-<n>`, which no record, branch or reader keys *(O1-F2)*. When another line takes it, the slot is
parked, switched to that line's branch and checked there, and renamed into that line's own path last; when a line comes back and its slot
was taken meanwhile, a free slot is parked, switched to the line's branch, checked, and renamed into the line's own path (§5.3). So a path and a
branch still name one line of work for its whole life, as they do today, a line's path never shows another line's branch or a detached HEAD
*(C1-2.1, C1-2.7)*, and what is reused is a working copy and those of its ignored dependency and build folders that do not name their own
path (§11) *(O1-F3)*. Every reader that reads a path as a line reads right (§3), every record keeps its meaning to builds already shipped (§14), and an
agent's conversation, filed by its working directory, is found where it was (§4).

### 2.2 What a slot is, and where it sits

- **What it is.** A linked worktree of the checkout, made by today's open (`SessionTrees.OpenAsync`, `SessionTrees.cs:273-428`) the first time
  a start needs a slot and the repository has fewer than its count (§9). Its first life is a fresh tree in every respect, said in today's words
  (`started.tree`, `started.tree-copied`).
- **What identifies it.** `(workspace, repository, number)` in the store (§2.3), and git's registration name under `<common dir>/worktrees/`,
  which `git worktree move` keeps (git's documented behaviour; row b proves it). Nothing in the driver keys on that name equalling the folder's:
  the two readers of it read any name (`Service/LinkedWorktree.cs:25-27`; `Trees/RefWatch.cs:238-239`), and the session lock is read through
  the tree's `.git` file (`SessionLocks.cs:42-54`).
- **Its folder's name.** At a line's path, its line's: `s-<8 hex>`, minted as `OpenAsync` mints it, for a new line; the line's own name for a
  returning one. Between lines, **its parking name `k-<n>`**, the only place the slot's number is spelled *(O1-F2)*. No reader keys a parking
  name: `BranchOfTree` would answer `daoris/k-<n>`, a branch nobody makes (`TreeFollows.cs:153-161`); TREEFOLLOW1 learns only a folder whose
  layout branch has a `GrownBranch` entry, and skips a detached one (`TreeFollows.cs:101-116`); the strays judge as an opening only an `s-`
  folder (`TreeStrays.cs:183`); no record names a parking path.
- **When it is where.** At a line's path only while the line holds it, or is free there and may come back (§5.1 choice 3), always on that
  line's branch at the tip `line_ends` recorded. A finished line's slot is parked at its finish (§10), so a finished line's path is empty, as
  today after the tidy *(C1-2.7, O1-F9)*. A slot waiting for a take is parked, detached at the tip its last line left.
- **A repository with submodules is never pooled** *(O1-F15)*: `git worktree move` refuses a tree holding one (git's documentation), so the
  count's door refuses such a repository, saying why, and its starts open trees as today.
- **Not a place of its own.** Both proposals put slots in a place of their own (`<home>/work/pool/…`, O §1.1; `<home>/work/tree-pools/…`,
  X §1); §1.1 row 1 says why not.
- **Its neighbours.** The `o-`, `p-` and `r-` folders stay where they are (`Trees/SessionTrees/TreeStrays.cs:77`); the strays' walk meets a slot
  as a tree git lists and leaves it (`:150-153`), at a line's path or a parking name. A parking folder git does not list (a registration lost in
  a crash) is the pool's to reconcile (§2.3); the newer strays pass `k-` folders over, as they pass `o-` and `r-` ones (`:147`); a build without
  the pool moves one aside after its quiet ten minutes (`:70`, `:160`), which loses the pool a slot and nobody any work (§14).

### 2.3 The claim, and where it is recorded

**The desktop store, as a Workplace family by FOUNDATION1 §13.3's rules**: new tables, every column required, the migration step after the
last at build time, the writer stamp unchanged, so builds of z-i and z-iii never open them, as `bindings` and `binding_picks` were added
(`Store/DesktopStore.cs:107-129`). It is the owner's preferred home for records the product reads and sets (the brief §5), and the only one
that gives one compare-and-set across the shell, a `--share` loop and a verb (O-1). A lease file beside the slot would need the store's
dead-run test anyway; a lease in the service would teach the service a machine path and desktop state (D47 §4; foundation §13.7); git's lock
alone is not a test-and-set across a read, a judgement and a switch (O-1).

| Table | Columns, all required | Key | What it says |
|---|---|---|---|
| `kept_trees` | workspace, repository, number, registration (git's name for it), path (its folder now), line (the branch whose path it is at, `''` when parked), state (`kept`, `retiring`, `stuck`), said (`''` or why), since (the moment its current place was reached), before (the paths it stood at since its links were last made again, `''` for none) *(O1-F5)* | (workspace, repository, number) | the slots that exist, and where each is now |
| `tree_claims` | workspace, repository, number, kind (`take`, `return`, `finish`, `end`, `retire`), holder (the quest, session, act or person's press it serves), line (the branch it brings the slot to, or the line it ends), start (the commit a new branch grows at, or a returning line's tip), expect (the HEAD the judgement read), from, park and to (paths, `''` where a kind has none), record (the opened record's id, `''` until it opens), phase, run (a `store_runs` id), at | (workspace, repository, number), and one unique index over (workspace, repository, to) where `to` is not empty *(O1-F1)* | the one claim in flight on a slot, its whole intent written before its first effect *(C1-2.2)* |
| `tree_visits` | workspace, repository, number, visitor (the terminal's id), run, at | (workspace, repository, number, visitor) | a terminal the person opened in a slot, admitted before its shell starts *(C1-2.5)* |
| `line_ends` | workspace, repository, branch, session, kind (`output`, `no-output`, `uncommitted`, `held`), tip, read (the moment the tree was read), left (the names of its ignored scratch outside the one list, `''` for none), at | (workspace, repository, branch, at) | each let-go of a line and each take that moves its slot away: the tip the next judgement compares (§5.2), what the record needs of the tree (§10) *(C1-2.3)*, and the licence to grow a no-output branch again (§6) |
| `line_records` | workspace, repository, folder (`<name>-<moment>`, its first moment), the finished tree's facts as KEEPSIZE1b's manifest carries them (`FinishedTree`, `TreeSweeps.cs:221-230`), state (`composed`, `posted`, `awaiting`), tries, at | (workspace, repository, folder) | a finished line's record from its composing until the service acknowledges it, then deleted *(C1-2.3, O1-F7)* |

Row i adds `kept_work` (§5.5). **Whether a slot is held is never stored**: it is read live from the ledger, the in-use set, the claims and
the visits (§7), so no row mirrors the records (O §1.2; §1.1 row 2).

**How a claim is taken.** The step that needs a slot (the driven start, `Loop/Driver.cs:2192-2221`; a conversation's, `ChatRunner.cs:635-651`;
a landing's return, §3.3; the look's finish, end or retirement) chooses it (§5.1) and, in one `BEGIN IMMEDIATE` transaction, reads that the
slot has no claim and no visit and inserts its claim with its whole intent; the primary key refuses a second claim on the slot, and the unique
index a second claim on the same destination, from any process *(O1-F1)*. **A start's claim reserves the slot from its choice until its
record opens** *(C1-3.1)*, the ledger holding it only after that open (`Core/Sessions/SessionLedger.cs:1602-1616`). Each effect is a mark in
§13.4's sense: a transaction checks that the claim is still this run's and writes the phase the effect begins, then the effect runs, then a
transaction writes the phase it reached. **Every rename runs under the repository's exclusive hold** (`TreeLock.TryReplaying`,
`Trees/TreeLock.cs:58-63`, as the let-go and the strays take it, `SessionLocks.cs:111`, `TreeStrays.cs:115`), which every build's start and
tidy honours: a start's own tree check holds the window shared and is held for a look while a replay holds it (`Loop/Driver.cs:2110-2111`)
*(C1-2.1)*. The hold is tried for a bounded moment, which row b sets, and otherwise the step is held for the next look, as a start held by a
replay is today. The switch, the drops and the first check run at the parking name under the claim alone (§5.3). **git's steps run in a job of
the claim's own**, made empty first and each git process joined before it runs, as a heavy run's gates are (`Platform/ProcessJob.cs:23-26`),
so no git process outlives the run that marked it *(C1-2.2)*. D201's launcher holds no claim: a claim belongs to a step, never to an agent's
process (both).

**The phases, kind by kind** *(C1-2.2, O1-F5, O1-F10)*:

| Kind | Phases, in order | What the effect between them is |
|---|---|---|
| `take`, `return` | `judged`, `parking`, `parked`, `switched`, `checked-park`, `placing`, `placed`, `relinked`, `checked`, `opened` | the rename from a line's path to `park`; the detach and the drops at `park`, then the switch; the first check (§5.3 step 7); the rename from `park` to `to`; the links made again; the last check (step 11); the record's open, whose id is written. A slot already parked begins at `parked`; a returning line's own slot at its path (§5.1 choice 1) has only `judged`, `checked` and `opened` |
| `finish` | `judged`, `held`, `parking`, `parked`, `deleted`, `composed` | the tip held on its recovery ref; the rename to `park` and the detach; the branch's delete at the commit judged; the record composed into `line_records` (§10) |
| `end` | `judged`, `recorded`, `locked` | the slot read; `line_ends` written; the free lock put back |
| `retire` | `judged`, `slim-judged`, `moving`, `moved`, `dropped` | the slim's manifest at `judged`; the set-aside's rename into `.tidied`; the manifest at `moved`; git's record of the vanished path dropped and the row removed (§9) |

**Across a crash.** A claim whose run is dead by `HomeLease`'s test (its pid gone, or alive under another start time, a start time that does
not read counting as alive: `Platform/HomeLease.cs:66-68`, `:165-173`; foundation §13.4) is taken over by the look. Before it acts, the
takeover reads the disk, never the phase alone *(C1-2.2)*: the folders at `from`, `park` and `to`; where git's registration of the slot points
(`worktree list --porcelain`); and git's own lock files in the slot's git folder and the common folder (`index.lock`, `HEAD.lock`). A git lock
file present holds the slot `stuck`, said, until a later look finds it gone or the person removes it: Daoris never deletes git's lock files.
On Windows the dead run's git processes ended with its job; elsewhere, where the job is no-op (`ProcessJob.cs:15-18`), the lock files are the
only sign, so the takeover waits for them. Then, by what it found:

- **Before any rename** (`judged`, or `parking` with the folder still at `from`): the claim is dropped and the free lock put back.
- **A folder at one path and git's registration at another** (a rename done, the `gitdir` write not: O1-F5): `git worktree repair <folder>`,
  then as below. Where the registration is gone, the folder is a stray of the pool's, moved aside whole by the set-aside's move (§9).
- **The folder at `park`** (`parked` to `checked-park`, or `placing` with the rename not done): the slot is detached at `park` at its HEAD; a
  branch the take made, still at `start` with no commit, is deleted by `git update-ref -d refs/heads/<branch> <start>`, which refuses where the
  branch moved, never by `branch -D` *(O1-F15)*; a returning line's branch stays, holding its commits. The slot is recorded parked, `stuck`, and
  freed by the next clean judgement (§5.2).
- **The folder at `to` with no record open** (`placed` to `checked`): the slot goes back to `park` under the exclusive hold, detached, as
  above, so a line's path never keeps an unchecked slot; for a return, the line's path is empty again, as it was before the return began.
- **A record open** (`opened`, or a later phase with an active record naming `to` in the ledger, the open having landed before its phase was
  written): the ledger holds the slot; the claim is deleted; today's orphan sweep ends the record if its process is gone, and the end follows.
- **`finish`, `end`, `retire`**: each resumes at its phase, every step idempotent (a held tip held again is the same ref; a deleted branch is
  no branch; a composed record is the same row; the retirement takes KEEPSIZE1b's resume of a stopped set-aside, D195:511-519).

A slot is taken only where nothing of the previous line's is outside a ref (§5.2), and a slot at a line's path is only ever on that line's
branch, so every takeover loses nobody any work; at worst it costs the pool a slot.

### 2.4 Its lock

- **While a session holds it:** TREELOCK1's `daoris session <id>`, unchanged, so `HolderOf` and every shipped reader of the lock read the
  holder (`SessionLocks.cs:42-54`). A conversation that takes a slot locks it too, closing TREELOCK1's *not covered* for conversations
  (D51:137-138).
- **While it is free:** `daoris kept tree`, a fifth reason, matching neither a session's prefix (`SessionLocks.cs:28`) nor an opening's
  (`Trees/SessionTrees/TreeOpenings.cs:15-45`). `SessionLocks.HoldAsync` learns to take it over, as it takes over a session's today
  (`SessionLocks.cs:60-73`); the end puts it back; the look's let-go of a session lock nobody holds puts it back on a slot rather than leaving
  the slot unlocked (`:101-123`).
- **Through a claim:** the pool's own reason naming the claim's run, `daoris kept tree (claimed by run <id>)`, put on before the claim's first
  effect and lifted only for a rename, which git refuses on a locked tree unless forced twice, and put back after it *(O1-F6)*. No build reads it
  as an opening's, so no build's strays delete a returning line's branch or set a claimed slot aside (`TreeStrays.cs:183-201`).
- **OPENCUT1's opening lock** only at a slot's first making, which is today's open (`SessionTrees.cs:371-373`) and keeps its undo.
- **No lock at all** (a session's lock an older build's let-go lifted, `SessionLocks.cs:129`): accepted by the judgement and locked again under
  the free reason *(O1-F10)*.
- **Any other reason** (a person's, or an opening's whose process lives): the slot is out of the pool, said, and never touched.
- **What a shipped build does with a slot's lock.** Its tidy refuses a locked tree (`TreeSetAside.cs:84-88`); its let-go lifts only a session's
  lock (`SessionLocks.cs:126-134`); its strays judge only an opening's, and only on an `s-` folder (`TreeStrays.cs:183`); its press's
  `git worktree remove`, forced at most once, refuses a locked tree (`TreeRemovals.cs:220-222`).

### 2.5 The types on FOUNDATION1's map

| Type | Subdomain (rank) | Names only |
|---|---|---|
| the migration step's DDL in `DesktopStore.Steps` | Store (2) | — |
| `DriverConfig.KeptTrees`, `WithKeptTrees`, `KeptTreesOf` | Settings (3) | a map of ints, no rule type (§14.3) |
| `NoteCodes` `StartedTreeKept` and its clauses, `StartedTreeBack`, `StartedTreePastKept`, `TreeReturned`, `EndedOutput`, `EndedNoOutput`, `EndedUncommitted`, `TreeKeptHeld`, `TreeKeptStuck` | Events (7) | — |
| `KeptTree`, `TreeClaim`, `TreeVisit`, `LineEnd`, `LineRecord`, `TreePool` (take, return, finish, end, retire, visit), `SlotChoice` and `SlotJudgement` (pure, in `Domain/`), `CarriedFolders` (§11), `TreeHolds` (the holders as a delegate, as `reviewing` is handed to `BranchTidying.InUseAsync` today, `BranchTidying.cs:132`, `:140-150`) | Workplace (10) | Store, Settings, Service, Events and below |
| the holders a standing move (Course 14, read from today's boxes until Part B's `moves` lands *(O1-F11)*), a served review (Review 16) and a helper wait (Participants 12) contribute | each answers `TreeHolds` as a delegate Loop composes | — |
| `ConversationTrees.Stands` and `Continuation.Judge`'s `treeStands` answering *stands, or can be brought back* | Course (14), calling `TreePool` downward | Workplace |
| `ReviewServed.Stands` asking whether the slot at a path was placed after a set-up's showing *(O1-F12)* | Review (16), calling `TreePool` downward | Workplace |
| the terminal's visit, taken before its shell starts and let go when its job is empty *(C1-2.5)* | the shell's `TerminalModule` (outside the map, above the driver), calling `TreePool.VisitAsync` downward | Workplace |

`Loop/FreshTree` is Workplace's (§14.2 row 10); its undo becomes the claim's. No pool type names `ChatRunner`, the planner, `HelperWaits`,
the review desk or the shell's terminal.

## 3. The line's identity, and the readers keyed by path

### 3.1 The rule: a path names one line, always

The folder a line's sessions run in is named for that line's branch (`s-<x>` for `daoris/s-<x>`), and a slot taken for another line is moved
to *that* line's path before anything runs in it. Nothing ever runs in a path but its own line's sessions, so the facts every reader in survey
§6 assumed, *the folder is the branch*, *a path is one line*, *the same path means the same line*, stay true. **And a line's path never shows,
even for a moment, another line's branch or a detached HEAD** *(C1-2.1, O1-F2, C1-2.7, O1-F9)*: a slot leaves a line's path and arrives at one
only by a rename under the repository's exclusive hold, it is switched to the arriving line's branch and checked before that rename, at its
parking name, and the takeover never leaves it at a line's path unchecked (§2.3, §5.3). So TREEFOLLOW1, which learns whatever branch a line's
folder shows at every tidy pass before any hold (`Trees/SessionTrees/AutoTidies.cs:190`; `TreeFollows.cs:91-126`), only ever learns the line's
own, and TREEGONE1 (`TreeReturns.cs:56-58`, `:67`) and the tidy's *moved onto* (`TreeFollows.cs:132-139`) read the line's own. What the pool adds is that a
line's path may be **empty for a while**: its slot was taken by another line while nothing held it (§7). Today a path empties when the tidy, a
press or a repository's merge tool takes its tree, and every reader already answers that case (TREEGONE1's remake, a gone tree's refusal, the
review's landed branch). The pool changes those answers only where they would refuse or remake a bare tree, so that a slot is brought back
instead (§3.3).

### 3.2 Every reader of survey §6

**Stored.**

| Record | Kept, re-keyed or cut |
|---|---|
| The service's `sessions.tree` (`Core/Sessions/Sessions.cs:89-100`, `:1147-1161`) | **Kept**: the line's path and the ledger's lock key; a pooled line's record names the path its line always runs in |
| `work_records`, `work_record_sessions` (`Core/Sessions/WorkRecords.cs:30-48`, `:446-463`) | **Kept**: posted at a pooled line's finish, `Tree` its path and `Moved` that moment, so *every session naming the path made before the move* is exactly the line's (§10) |
| The helper, partner and opinion desks (`Core/Sessions/HelperDesk.cs:89-94`; `Core/Sessions/PartnerDesk.cs:69-74`; `Core/Review/OpinionDesk.cs:113-119`) | **Kept**: the asker's or lead's tree is its path, held while it runs (§7) |
| `session-branches.json`, `GrownBranch` (`Trees/Branches/SessionBranches.cs:19-27`; the unit found by folder, `TreeLands.cs:845-848`) | **Kept**: recorded at a take as at an open (`SessionTrees.cs:380-398`), its `At` the take's moment; the folder still names the branch |
| `landings.json` | **Kept** |
| Auto-landings (`AutoLander.cs:209-222`) | **Kept**: a due landing holds its line's slot until it is judged (§7), so `:209`'s *gone* never meets a slot taken under it |
| Set-aside manifests (`Trees/SessionTrees/TreeSweeps.cs:220-230`) | **Kept** for legacy trees and trees past the count; a pooled line's finish writes none, its inputs captured in `line_ends` and composed in `line_records` (§10) *(C1-2.3, O1-F7)*; a retired slot's slim manifest is the slim's own schema (§9) *(C1-1.3, O1-F8)* |
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
| `TreeScratch`'s build folders, what a removal lets go of (`ContentHolds.cs:588-594`) | **Kept byte for byte**: the one list's carried subset adds `wwwroot` for a take alone; whether a removal should read `wwwroot` as a build folder is a correction of its own, not this design's *(C1-4.2)* |
| `Planner.SameTree` (`Planner.cs:1120`, `:1141`, `:1358`, `:1380`, `:1390`, `:1586-1591`; `Sessions/RunningSessions.cs:129-145`) | **Kept**: one line per path; a line whose slot was taken holds no path and blocks nobody |
| `EntryLandings.Sequence` (`Trees/SessionTrees/EntryLands.cs:104-121`) | **Kept**: parts sharing a path share a line |
| A chat run's line and its taken quest (`Workflows/ChatRuns.cs:66-91`; `Workflows/WorkflowRun.Read.cs:204-220`) | **Kept** |
| The trace's *same tree* (`Sessions/Domain/Trace.Chain.cs:169-187`) | **Kept**: the same path is the same line |
| The tidy's in-use set and waiting trees (`BranchTidying.cs:140-150`; `Service/ServiceClient.cs:2222-2249`) | **Kept**, joined by the pool's own holders (§7) |
| `ContentHolds.HoldsAsync`'s *opened* (`Trees/SessionTrees/ContentHolds.cs:573-574`) | **Re-keyed** (row c): for a slot, its line's `GrownBranch.At`, since a moved folder keeps the creation time of its first making; a legacy tree keeps its folder's |
| The context brief (`Loop/Driver.cs:2573-2583`) | **Kept** |

**Where a gone tree is met** (survey §2's holders, the sites the pool re-keys).

| Site | Kept, re-keyed or cut |
|---|---|
| TREEGONE1 (`TreeReturns.cs:48-110`) | **Re-keyed** (row e): a pooled line's tree gone while its branch stands gets a free slot brought back by a return run before the start's tree check (§5.3), which then finds it standing (`tree.returned`); with none free, today's remake, copying TREEDEPS1's (§11) |
| Words going on (`Continuation.cs:231`; `DriverContinuations.cs:171-178`) and a conversation's go-on and moves (`ConversationTrees.cs:11`; `ChatRunner.cs:630-633`; `Conversations/Chat/BriefedMove.cs:105-107`, `:188-190`; `LimitMove.cs:75`, `:261`) | **Re-keyed** (row c, on at e): *stands, or can be brought back*, and the go-on brings it back first |
| A landing (`TreeLands.cs:196-204`, `:918-926`) | **Re-keyed** (row e): a line whose slot was taken has one brought back (a `return` claim), then lands as today |
| A session's review (`SessionReviews.cs:125-130`) | **Re-keyed** (row c): a pooled line's tree gone and unlanded reads its branch in the checkout, where today it reads nothing |
| *Show it again* (`Review/ReviewDesk.cs:179-190`, its gone-tree refusal `:226-229`) | **Re-keyed** (row e) *(O1-F12)*: the served review holds the slot until the verdict (§7); after it, *Show it again* is refused where the slot now at the set-up's path reached it after the showing (`kept_trees.since` later than the showing's moment), since that slot's build is not the set-up's |
| The tidy's set-aside (`TreeSweeps.cs:158-301`; `TreeSetAside.cs:190-341`) | **Re-keyed** (row d): a pooled line is finished and its slot parked, never set aside (§10) |
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

| Start | Its line's slot is at its path and free | Its line's slot was taken |
|---|---|---|
| A resume (D79), an answer (D131), words going on (MSG1b, D137) | goes on there once checked, its HEAD on its branch at the tip its let-go recorded; no rename and no switch (O) *(C1-2.7)* | a free slot brought back to its path on its branch (row e), the conversation resuming there (§4); none free: today's remake |
| The same, for a **finished** line (its slot parked at its finish, its branch gone) *(C1-2.7, O1-F9)* | — | a landed line takes a slot for a new path from its landing, as today it grows a fresh tree (`Loop/Driver.cs:2119-2127`); a no-output line has its branch grown again at the line's tip, licensed by `line_ends` (§6), and a slot brought to its own path. Never goes on detached |
| A carry-on (D80), a driven hand-over (MIXAGENT1), a bring-forward (D178) | there | brought back, as above |
| A further part (D167 point 4 as amended) | into the entry's newest part's path where nothing live holds it (`Planner.cs:1105-1157`), as today | brought back; a part alongside a live part takes a slot for a new line of its own, as today it opens a tree |
| A hand-over, a limit move, a briefed move (D189 point 2) | the same path; the standing move holds the slot (§7) | not reached while the move stands; a move begun after the slot was taken brings one back |
| A chain's next step (D82, CHAIN2) | a slot for a new line from `BranchNowAsync(parent)`, else `daoris/<folder>` (`Driver.cs:2197-2213`), right since the folder is the branch | the same |
| A landing (Accept, `trees land`, the auto-lander) | lands in it, as today | brought back for the landing (a `return` claim), free after |
| TREEGONE1 for a line that began in a legacy `s-` tree | today's | a free slot brought back where one is free; else today's remake |

A line never waits for *its own* slot while another line holds it, and never goes on in another path: another free slot is brought to its
path. Both proposals made that case a new part handed the brief (§1.1 row 11). **Two ways back of one line at once** (a resume and an Accept,
in the shell and a `--share` loop) name one destination, so the unique index on a claim's `to` lets one through and the other waits for a look.
**A take of a line's free slot racing that line's return**: a return waits a look while its line's own slot is under another claim, and a take
passes over a slot whose line has a claim in flight naming its path, each read in the claim's own transaction *(O1-F1)*. And inside the claim,
under the exclusive hold, the destination must not exist at all, since `git worktree move` would nest a slot inside a folder there: a folder
left at a line's path is moved aside first, never deleted, as TREEGONE1 moves one (`TreeReturns.cs:87-92`), and after the rename git must list
the slot at exactly `to` (§5.3).

## 4. The agent's conversation and resume (D189)

Claude Code files a conversation as `projects/<working directory, flattened>/<id>.jsonl` in the account's home, and its resume reads it there
and refuses a missing one (`docs/2026-10-24-switch-context-design.md:184-188`; `ToolHomeLeftovers.cs:30-31`, `:55`); its trust is the path's
(`ClaudeTrust.cs:108-125`). Codex files a session by date (`ToolHomeLeftovers.cs:41-42`), so its resume does not depend on the folder.

**Because a line's sessions only ever run at its own path, a line coming back runs at that path whichever slot is brought there**, on its
own branch and never detached *(C1-2.7)*, and its agent's own conversation is found in the same account home under the same folder name, with
the trust the path already had. D189 point 2's
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

`SlotChoice`, pure, before the start's own window, takes the first that stands:

1. **The returning line's own slot**, at its path, free, and on its branch at the tip its let-go recorded: no rename and no switch (O); it is
   checked (§5.3 step 11) and goes on *(C1-2.7)*.
2. **A parked slot**: one a finished line left at its finish (§10), a retirement left unfinished, or a take put back *(O1-F9)*.
3. **A free slot whose line may come back** (a D79 wait not yet answered, an ended conversation, an unlanded line no one holds), judged clean
   (§5.2), the one let go longest ago first; never one whose line has a start due this look (§7) or a claim in flight naming its path (§3.3).
4. **A new slot**, where the repository has fewer than its count: today's open, recorded as the next number (§9).
5. **A tree of the line's own, as today** (`SessionTrees.OpenAsync`), off the pool's books, said in `started.tree-past-kept` beside today's
   words; today's tidy takes it once its work is done (§8).

The claim is inserted in the transaction that chooses.

### 5.2 The judgement: what makes it safe

A slot is taken for another line only where every one of these holds; any other answer leaves it its line's, said (`tree.kept-held` with
why), and the choice tries the next:

1. **Nothing holds it** (§7): no record active or waiting names its path, no claim, no visit, no standing move, no served review, no due
   landing or resume, no helper wait.
2. **git lists it at its folder** (TREEGONE1's test, `TreeReturns.cs:56-58`), and its lock is `daoris kept tree`, a session's lock whose
   record no longer holds it (`SessionLocks.cs:88-94`), or none, which it locks again *(O1-F10)*.
3. **Its links and repositories, judged whole** *(C1-1.2)*. The look's last guards (`TreeSetAside.LastLookAsync`, `TreeSetAside.cs:70-135`)
   first: no link under the trees home on the way to it, no lock of a person's, no `assume-unchanged` or `skip-worktree` path, nothing unread.
   Then a walk of the whole slot, TREELINK1's (it never enters a link, skips the slot's own `.git` and looks at no more than a million entries,
   D51:108-112), which the last guards do not make: **a link is the pool's to carry only where it lies inside a `node_modules` beside a tracked
   `package.json` and leads into the slot's own present path, or a path `kept_trees.before` names**, which is npm's workspace link as TREEDEPS1
   copies it (D51:72-77); any other link,
   one leading outside the slot or lying outside a package folder, is a person's or a tool's, and holds the slot. **A repository inside the slot**
   (a `.git` file or folder below its top) holds the slot wherever git would not ignore it, as the last guards say, and for a line that may come
   back wherever it lies; one under an ignored path of a finished line goes with that line's other scratch (step 5), as OVERVIEWNOISE1a lets
   it go with a tree (`TreeSetAside.cs:116-124`).
4. **No git operation half done**: no `MERGE_HEAD`, `CHERRY_PICK_HEAD`, `REVERT_HEAD`, `BISECT_LOG`, `rebase-merge/` or `rebase-apply/` in its
   git folder (X-5).
5. **Nothing outside a ref.** `status --porcelain -z --untracked-files=all --ignored=matching --ignore-submodules=none`, the read
   `ContentHolds.HoldsAsync` makes (`ContentHolds.cs:565-566`), shows no tracked change and no untracked path, and every ignored path lies
   under a folder of the one list (§11), unless the line is finished and its record kept (§10), whose other ignored scratch is carried into
   the next line's tree, never cleared, until row j.
6. **HEAD is where its line let it go, and a ref holds it**: the commit `line_ends` recorded, held by the line's branch (or its
   `GrownBranch.Now`, TREEFOLLOW1) or, for a finished line, by the recovery ref its finish kept (`ContentHolds.cs:102-106`). A HEAD moved since
   on the line's own branch is the line's work, and the look's end records it before any take (§6) *(O1-F10)*; a HEAD moved anywhere else,
   by a person or by a build that does not know the pool, holds the slot: *changed since its line let it go* (X-5 step 4).

Judged before the choice and **judged again inside the claim, under the exclusive hold, right before the first rename**, as §13.4 marks an
effect, so a write between the two holds the slot (X's point; O's *made again and compared*). A write after that rename is caught by the checks
of §5.3 steps 7 and 11 *(C1-2.4, O1-F4)*.

So **the first build resets nothing.** A slot holding a tracked change, an untracked file, a half-done merge or a commit no ref holds is
never taken, and *brought to the line* is a non-forcing switch of a clean working copy (§5.3), which writes tracked files and touches no
untracked one. The brief §5's *a reset that clears untracked work is a deletion* is met by clearing none until the owner says otherwise (row
i), and its *never resets a tree that holds work no ref holds* by steps 5 and 6.

### 5.3 The move and the switch

A take brings a slot to a new line B's path; a return brings one to a returning line R's own path. Both run **before the start's own tree
checks and window** (`Loop/Driver.cs:2108-2117`, `:2184`), never inside a shared hold, since their renames take the exclusive one; the checks
then find the line's tree standing, on its branch. Every step below is a phase of the claim (§2.3) *(C1-2.1, O1-F1, O1-F2, C1-2.4, O1-F4,
C1-2.2, O1-F5, O1-F6)*:

1. **Claimed.** The claim is written with its whole intent (§2.3), and the slot's free lock is swapped for the claim's reason. Phase `judged`.
2. **Parked**, where the slot sits at a line's path (choice 3; a parked slot begins at step 3). Under the exclusive hold: the judgement made
   again (§5.2); the claim's lock lifted; `git worktree move <from> <park>`, `park` being `k-<n>`, which no folder may hold (one there is
   moved aside first); git must list the slot at exactly `park`, and a registration left at `from` is repaired (`git worktree repair <park>`);
   the claim's lock put back; the hold let go. The line whose path the slot left has its `line_ends` written first, with its tip, the read
   moment and its scratch outside the list (none, by §5.2 step 5), so its record can be composed whenever it finishes *(C1-2.3)*. Then, at the
   park, `git switch --detach`, which writes no file, so the previous line's branch is
   checked out nowhere. The rename comes first so the line's path never shows a detached HEAD; the park is keyed by nobody. A refused rename
   (a handle open without delete-sharing, the set-aside's `busy`, `TreeSetAside.cs:196-197`, `:313-333`) drops the claim, puts the free lock
   back and takes the next slot. Phase `parked`.
3. **What does not cross a move dropped** (§11) *(O1-F3, C1-1.4)*: at the park, each folder of the one list that the carried subset does not
   name, and each cache at the top of a `node_modules` (`TreeDependencies.cs:44-45`), is set aside within the slot under a name of its own and
   deleted never following a link (`TreeSetAside.DeleteNeverFollowing`). A finished line's carried scratch stays, named in its `line_ends`.
4. **Switched**, at the park. For B, `git switch --no-overwrite-ignore -c daoris/s-<B> <start>`, `start` being what `OpenAsync` grows a branch
   from (the line, a chain's `from`, its `landed`, a part's `partOf`: `SessionTrees.cs:299-352`), and the `GrownBranch` and its unit recorded as
   at an open (`:380-398`). For R, `git switch --no-overwrite-ignore <its branch, or GrownBranch.Now>`. Never forced: `--no-overwrite-ignore`
   refuses where the target tracks a path a kept ignored file stands at (X-5). Phase `switched`.
5. **Dependencies** judged per npm project (§11).
6. **Its links made ready.** The links step 3 of §5.2 admitted, npm's workspace links leading into the slot's former path, are listed with
   their targets' places relative to the slot.
7. **The first check, at the park** *(C1-2.4, O1-F4; X-5 step 9)*: HEAD is the target branch, at `start` for B and at R's recorded tip for R;
   `status --porcelain -z --untracked-files=all --ignored=matching --ignore-submodules=none` shows no tracked change and no untracked path; and
   every ignored path lies under a carried folder, or among the finished line's carried names. A path ignored on the previous line's branch and
   not at the target shows here as untracked, and a write that landed after the judgement shows as a change; either fails the check. A failed
   check switches the slot back to detached at `expect`, without force (left as it stands where git refuses), marks it `stuck`, says why and
   drops the claim: **no record opens on a slot that did not pass**. Phase `checked-park`.
8. **Placed.** Under the exclusive hold: `to` must not exist at all, since `git worktree move` would nest the slot inside a folder there (O1-F1);
   a folder left at R's path is moved aside first, never deleted, as TREEGONE1 moves one (`TreeReturns.cs:87-92`), and B's path is new. The
   claim's lock lifted; `git worktree move <park> <to>`; git must list the slot at exactly `to`, a registration left at `park` repaired; the
   claim's lock put back. Phase `placed`.
9. **Relinked**, still under the hold: each listed link is made again leading to the same place inside `to`, by TREEDEPS1's rule (D51:72-73).
   Phase `relinked`. Interrupted, the slot keeps its former paths in `kept_trees.before`, and the next judgement admits a package folder's link
   into any of them as into its present path, to be made again at its next move *(O1-F5)*.
10. **Dependencies' sentence** composed (§11, §15).
11. **The last check, at `to`**, still under the hold *(C1-2.4, O1-F4)*: git lists the slot at exactly `to`; HEAD is unchanged since step 7;
    status reads as at step 7; and no link in a carried folder leads outside `to`. A failed check renames the slot back to the park, detached,
    `stuck`, said, and drops the claim. Phase `checked`; the hold let go.
12. **Opened.** The start's own window taken (`Loop/Driver.cs:2184`), the record opened with `tree` = `to`, the session lock put in the claim's
    lock's place (`SessionLocks.HoldAsync`), and one transaction writes `opened` with the record's id, deletes the claim, and writes the slot's
    `path`, `line` and `since`. A start that ends before its open sends the slot back to the park as the takeover would (§2.3), and deletes B's
    branch by `git update-ref -d refs/heads/daoris/s-<B> <start>`, which refuses where it moved *(O1-F15)*.

The returning line's own slot (§5.1 choice 1) takes only steps 1, 11 and 12, in place and under the start's shared window: its HEAD must be on
its branch, at its recorded tip or moved on that branch by its own sessions; its own uncommitted work stays its own *(C1-2.7)*. A returning
line then has its kept work laid back where row i kept it and its tip is still that work's base, and LANDSYNC2 brings it up to date with the line
as today (`Driver.cs:2175-2178`).

### 5.4 A refusal

- **A rename refused**: `busy`, as the set-aside's; the slot stays where it is, free, and another is taken.
- **A switch refused** (`--no-overwrite-ignore`, or a file held open), or **a check failed**: `stuck`, git's words or the check's said
  (`tree.kept-stuck`), another slot taken. A stuck slot stays at its parking name, is judged again at each look, and is switched back to detached
  at its recorded tip where git allows; it is freed only by a clean judgement (§5.2). **Nothing retires it but §9's proof** *(C1-2.4)*: the
  person's press on a stuck slot is today's press, which refuses what holds work no ref holds, naming it, and goes only when the person says it
  again meaning it (working-surface rule 7, `docs/2026-09-21-working-surface-design.md:97-99`). What a refused switch leaves of the tracked
  files on Windows is unmeasured (survey §5); row b measures it.
- **A nested repository, a person's lock, a link not the pool's**: never reached, since §5.2 holds the slot first.
- **A rename the exclusive hold cannot get**: the step is held for the next look, the slot unmoved.

### 5.5 Work no ref holds, kept aside (row i, only on the owner's yes)

Until the owner agrees (§18 item 3), a slot whose line let go with work no ref holds stays that line's (§6, §7). With the yes, the Opus
reader's keep (O §1.4), with Codex's checks:

1. The last guards (§5.2 step 3) and a bound: past 10,000 paths or 64 MB of untracked bytes the slot stays its line's, said.
2. **Both states are kept, the index's and the working tree's** *(C1-2.6)*: what is staged can differ from what is on disk (a path staged at
   one version and then edited to another, a deletion staged or not). So two trees are written, neither touching the real index or the folder:
   `I`, the real index as it stands (`write-tree`, which reads the index and writes only objects), and `W`, the working tree through an index of
   its own under the slot's git folder (`read-tree HEAD`, `add -A`, `write-tree`). Each is stored as a commit with **no parent**, its dates the
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
   deletions staged and unstaged, and a lay-back stopped between its steps.

**Rejected:** loose files moved to a kept area outside the slot (X-6), since a move cannot keep a tracked edit and its payloads have no rule
that removes them (§1.1 row 3); Daoris committing the leftovers on the line's own branch, which lands what the agent never chose to commit and
turns a no-output end into output (O-6).

## 6. The end: done with output, done without output

At a run's end (`Driver.cs:2902-2910`; the conversation's end in the chat runner), once its processes and their job have ended
(`Platform/ProcessJob.cs:6-32`), the end claims the slot (`end`), reads it (§5.2 steps 3 to 6), writes `line_ends` with the tip, the read
moment and the names of its scratch outside the list, puts the free lock back, and the record says one of the three below.

**Who runs the end** *(O1-F10)*. The run's own end, and, idempotently, **the look**, for every slot that no holder holds (§7), that is under
no claim, and that has no `line_ends` row since its `since`: a park ended in another process, a dead `end` claim, or a session of a build that
does not know the pool, which left the slot under that build's rules. The look's end reads where HEAD stands: on the slot's line's branch (or
its `GrownBranch.Now`), moved or not, it records that tip, the line's own work; anywhere else, or with work no ref holds, it records `held`
and says why, for the person.

- **Done with output** (`ended.output`): commits on its branch, nothing uncommitted. The slot stays at the line's path on its branch, locked
  `daoris kept tree`, free but for what §7 names, a due landing among them. Ready to land: Accept lands from it, or from a slot brought back.
- **Done without output** (`ended.no-output`): no commit and nothing uncommitted. The slot is free and stays at the line's path on its empty
  branch, where words to the line still find it, as a conversation's place does today, until a take moves it away (§5.1 choice 3); then the
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
| A running record | yes, the ledger's key (`SessionLedger.cs:320-324`) | its run | its end | the rail's tree name (`identity.ts:141-144`) |
| A parked record | yes, the ledger's key (`SessionLedger.cs:1604-1607`, `:1833`) | until the person answers or ends it | the answer goes on there; *End* lets go | on the Projects row, *held: waits on you* |
| A visit: a terminal the person opened in it *(C1-2.5)* | yes, a `tree_visits` row the shell writes before its shell starts (`Modules/Windows/TerminalModule.cs:202-218`), refused while a claim is in flight there; read by every process that takes a slot | while the terminal runs, until its job is empty: its shell is joined to a job of its own at its start, as a heavy run's gates are (`ProcessJob.cs:23-26`) | its job empty; a dead shell's visits taken over by the look, its terminal's job having ended with it | — |
| Words waiting on an ended record | yes, the in-use set (`BranchTidying.cs:146`), which reads every record with words waiting (`Service/ServiceClient.cs:2245`) | for as long as they wait, which may be long: words held for the person's press (MSG1f) or for an account's reset (LIMITSWITCH3) among them *(O1-F11)* | the go-on, or the words taken back | — |
| A start due this look: an answered wait the planner will resume, or a line whose record a go-on will start | yes, read from the look's own plan | that look | the start | — |
| A helper wait (HELPERWAIT1) | yes (`BranchTidying.cs:147`; `SessionLocks.cs:91`) | while its quest is taken | the answer, or the quest closing | as today |
| A served review build (REVIEWENV1d) | yes (`BranchTidying.cs:148`) | until the verdict | the verdict | as today |
| A standing move (a hand-over, a limit move, a briefed move) | yes: today's box beside the old part, `<s>.limit-move.json` or `<s>.hand-over.json` (`LimitBox.cs:24`; `HandOver.cs:15`), which every build writes and reads, and Part B's `moves` row once its row f-i lands *(O1-F11)* | the move | the new part's open | as today |
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
build that does not know the pool (§14). Survey §8.4's other holders are O's correction (§0.2 row 1).

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
  verb say so where a cap lowers a repository's kept trees, naming how many retire.
- **How it is set:** `daoris driver trees <repository> keep <n>`, `SET_TREES` gaining `kept`, the Projects row's control beside the trees switch,
  and Ask Daoris's `setting` door as for every key; **none of these doors lands before row f** *(C1-4.1)*, so no person can switch a pool on
  before lines come back through it and slots retire safely. A repository with submodules is refused, said (§2.2) *(O1-F15)*.
- **Against the cap:** unchanged. The cap counts driven work, never the person's conversations (CAPCOUNT1, `Planner.cs:192-209`); set-ups'
  `atOnce` is unchanged; a conversation takes a slot as a driven start does, locked in its name, and counts under no cap.
- ***At most one slot per session the repository may run at once*:** the count keeps no more trees than can run at once, and lazy making keeps
  fewer where fewer ever ran (§9).
- **Past the count:** a tree of its own as today (§5.1 step 5), which today's tidy takes once its work is landed, and which, where its branch
  never moved, stays until the person's press as a conversation's place does today (`TreeSetAside.cs:18-20`) *(O1-F15)*. Whether that is the
  direction's meaning, or whether the count should bound the sessions that run in trees with the rest waiting, is the owner's question (§18
  item 1).
- **What it does not bound** (X §3): the trees past it, retired slots waiting D195's days, and caches growing in each kept slot.
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
- **Never by age.** A slot leaves only by a lowered count (set, or the default the cap lowers), a repository leaving, or the person's press;
  the days that follow are D195's on the folder moved aside, as for every set-aside folder.

## 10. The finished record and removal

A pooled line's tree never ends: its slot stays the pool's. So **KEEPSIZE1b's steps run at the line's finish** (D195:511-519;
`TreeSweeps.cs:158-301`), a `finish` claim whose phases §2.3 names. The look's tidy takes a pooled line's branch exactly where it would take
today's (`GoesByItself`, `TreeSetAside.cs:27-30`), reading the line's free slot as no tree of the line's, and then:

1. **judges** it as today (the press's `Removable` and the look's wait for the line);
2. **holds the tip** on `refs/daoris/discarded/<branch>` wherever the line's own branch does not hold it (`TreeSweeps.cs:191-213`); phase
   `held`;
3. **parks the slot**, where it is still at the line's path *(C1-2.7, O1-F9)*: under the exclusive hold, renamed to its parking name and then
   detached there (`git switch --detach`, which writes no file): the step where today's tidy moves the folder aside. So the finished line's path
   is empty, as after today's tidy, and nothing can go on in it detached. A refused rename (`busy`) ends the finish for this look with nothing
   deleted; it is tried at the next. Phase `parked`;
4. **deletes the branch** at the commit judged in its one `update-ref --stdin` transaction (`ContentHolds.cs:456-475`), which refuses a branch
   checked out anywhere (`:468-471`); phase `deleted`;
5. **composes the record** into `line_records` *(C1-2.3, O1-F7)*: the branch's facts read now, as the set-aside reads them (`TreeSweeps.cs:221-230`:
   the tip judged, what it grew from, its unit, the line, the hold and its form, the recovery ref, the landing or why none), and **the tree's
   facts as the line's last `line_ends` captured them while its tree stood** (the read moment, the scratch it left), since a slot may have been
   taken from a line that may come back long before that line became removable; `Folder` is `<name>-<moment>`, the name KEEPSIZE1b gives a
   set-aside folder (`TreeSetAside.cs:35-36`), its moment fixed here and never again; phase `composed`;
6. **posts the record** to `POST /api/trees/record` (`Http/Program.cs:1439`) with `Tree` the line's path and `Moved` the finish's moment, so
   `WorkRecords.SessionsOfAsync` names the line's sessions and no other (`Core/Sessions/WorkRecords.cs:446-463`). The row's body is posted
   unchanged until the service acknowledges it, the route answering a second post of a kept identity as the first (`Http/Program.cs:1435-1438`);
   a refusal or a newer store keeps it `awaiting`, as KEEPSIZE1b keeps a folder awaiting its record; on the acknowledgement the row is deleted.

**What `Left` and `Whole` mean here** *(O1-F7)*: `Left` names the line's ignored scratch outside the one list, which stays in the parked slot
and is carried into the next line's tree, never cleared until row j, and the record says it was left in a kept tree, not gone; `Whole` is false,
since nothing of a slot waits days or is purged. No manifest is written. **A no-output line** is finished by today's tidy once a take has moved
its slot away (§6): its empty branch, with no tree, goes as an empty branch does today, with no record, as a no-output tree's press keeps none
(D195:518-519).

**D195's kinds.** `trees`, rule `proof` (`Platform/SystemSpace.cs:37`), keeps the legacy trees and trees past the count; a new kind,
`kept-trees`, rule `count`, the person's number, counted from `kept_trees`; `set-aside`, rule `days`, gains retired slots. **The tidy's
`GoesByItself`:** its exception for a conversation's place (`TreeSetAside.cs:18-20`; D88:51-52) holds while a no-output line's slot is at its
path, and is moot once a take moved it away, the empty branch going and being grown again when its record goes on (§6). **KEEPSIZE1c to 1f stand**; 1d's allowance never counts a kept slot, which is live, and its set-aside part gains
retired slots.

## 11. Dependencies and build outputs kept

- **The one list:** TreeScratch's thirteen (`Trees/SessionTrees/TreeScratch.cs:15-19`) and `wwwroot`, an ignored web root no list holds today
  (survey §4). Each use names its subset of it, which is X's capabilities kept as subsets: what a slot may hold for a take to judge it free
  (§5.2 step 5: the whole list); **what crosses a move** (the carried subset, below); what a removal lets go of (TREEKEEP1's, which stays
  `TreeScratch`'s thirteen byte for byte, `ContentHolds.cs:588-594`, so a legacy tree's removal reads as today *(C1-4.2)*); what the slim drops
  early (D195 point 3: a `node_modules` beside a tracked `package.json`, `KeepKinds.cs:11-20`); what TREEDEPS1 copies (npm's `node_modules`,
  `Trees/SessionTrees/TreeDependencies.cs:85-136`). A name never authorises a deletion by itself: a path is kept only where it is ignored and
  under the list. The unification is a dated note on D195 point 3, D102's TREEKEEP1 and D51's TREEDEPS1 (row b), as the brief §10 point 4
  asks; whether a removal should read `wwwroot` as a build folder is left to a correction of its own.
- **What crosses a move** *(O1-F3, C1-1.4, C1-2.8)*. A slot that moves to a new path keeps what it carries at that new path, and its old path
  may hold another line's tree within the hour. So a folder that names its own absolute path is no stale cache after a move: it is a live read
  of whatever stands at the old path. **Carried in the first build**, as `CarriedFolders` names them: `node_modules`, less the caches at its top
  that name where they were made (`.cache`, `.vite`, `.vite-temp`, which TREEDEPS1 already leaves behind, `TreeDependencies.cs:27-28`,
  `:44-45`), its workspace links made again into the new path (§5.3 step 9); and the outputs no tool resolves code from, `bin`, `dist`, `build`,
  `out`, `coverage`, `wwwroot`, and `__pycache__`, whose files the interpreter checks against the source beside them. **Dropped at every move**,
  set aside within the slot and deleted never following a link (§5.3 step 3): `.venv`, whose launchers and editable installs name their path;
  `obj`, whose NuGet restore files name the assets file absolutely and are read as written under `--no-restore`; `target`, `.angular`, `.next`
  and `.gradle`, build caches keyed by or resolving through absolute paths. A dropped kind joins the carried subset only by a dated note on row
  b's proof that, after a move with the old path holding another tree, it reads and writes only its own path; `obj` without NuGet's restore files
  is the first candidate. A slot that does not move (§5.1 choice 1) keeps everything. **This is the move's real price**: in the first build a new
  line's .NET, Rust, Angular and Next builds start cold, and npm's install is what a take saves, with the disk, which row b measures before row d.
- **Dropping is not deleting the owner's data**: each dropped folder is a build's or an install's own output, which its producer makes again,
  D195 point 3's reason for the slim (`TreeSlims.cs:3-7`); `bin` is carried, which D195 point 3 keeps from the slim for good, and `obj` is dropped
  on the evidence above, which point 3 left open.
- **npm, per project after the switch (O-11):** **fresh** where the slot's `node_modules/.package-lock.json` is not older than the project's
  `package-lock.json`, npm's own staleness rule and TREEDEPS1's (D51:57-60): the agent is told to skip `npm ci` there; **stale, and the
  checkout's install is at the slot's lockfile byte for byte**: replaced by TREEDEPS1's copy, the old folder renamed aside within the slot and
  deleted never following a link (`TreeSetAside.DeleteNeverFollowing`), under D195 point 3's reason; **stale otherwise**: the agent is told the
  lockfile changed since its dependencies were installed and to run `npm ci` there. A switch that changes a lockfile writes it, so its time
  moves past the install's.
- **The carried outputs:** kept; the agent is told which were kept and which were dropped at the move, that builds are incremental where their
  outputs were kept, and that a kept output is no proof that this commit builds (X-11).
- **The proof of what crosses** *(C1-2.8)*: row b moves a slot, then makes a tree at its old path holding sentinel files of another line, and
  runs in the moved slot a .NET build with `--no-restore` and without, a node import of a workspace package and the web build: each must read
  and write only the slot's new path, a sentinel read or written failing the case; a `.venv` with an editable install made before the move is
  gone after it, and the opening says so. Then it reports what of the build saving survives, against a build in place and a fresh one.
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
  own lock first, and **a repository with submodules is never pooled** (§2.2) *(O1-F15)*. **Into a destination that exists, even an empty
  folder, it moves the tree inside it** (its `move_worktree`, as the reader cites git's source) *(O1-F1)*, so a destination must not exist, a
  folder there moved aside first (§5.3 steps 2 and 8), and git must list the slot at exactly the destination after. **It renames before it
  writes the registration's `gitdir`**, so a failure between leaves the folder at the new path and the registration at the old, which
  `git worktree repair <new>` mends (§2.3) *(O1-F5)*. It is a rename, so a handle open without delete-sharing refuses it: `busy`, as the
  set-aside's rename (`TreeSetAside.cs:196-197`); an editor, an indexer, a terminal's working directory or a git file-system monitor daemon in
  the slot each make it so, which row g counts.
- **git's processes** run in a job of the claim's own, made empty first, each joined before it runs, ended with the claim's process
  (`ProcessJob.cs:23-26`; on Windows, where the job holds; `:15-18`) *(C1-2.2)*; the takeover reads git's own lock files before it acts (§2.3).
- **A switch refused by a held file:** `stuck` (§5.4); row b measures what it leaves with a file held open without delete-sharing and git's
  standard input closed, so Git for Windows never asks (O-12).
- **Long paths:** a slot's path is today's tree's, and every git call keeps `-c core.longpaths=true` (`Trees/WorkingTree.cs:974-981`).
- **Junctions in a kept `node_modules`:** a rename moves a junction as an entry and follows none; the ones npm made, leading into the slot's
  former path, are made again (§5.3 step 9); any other holds the slot (§5.2 step 3); a retired slot leaves by the set-aside's move and the
  purge's never-following delete (D51:105-118).
- **What may hold a file:** a session's processes end with its job before its end reads the slot (ORPHAN1, `Platform/ProcessJob.cs:6-32`); a
  heavy run's builds run with `MSBUILDDISABLENODEREUSE=1` and end their job before the run lets its slot go (`Sessions/Heavy/HeavyRun.cs:47-51`);
  a served review and a terminal's visit hold the slot (§7); a scanner's hold on a just-written file reads as `busy` or `stuck` and is tried
  again.

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
> that line's path. Rule 4 reads: a tree holds nothing of another line's that git tracks; what it holds untracked is the
> kept dependency and build folders, and the sentence that hands it over says which, and whether they match its lockfiles. The tree stays the
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
(a second install, or a person running an older build; D139's rollback cannot, since pooled lines exist only once a build that knows them has
confirmed):

1. **Its remake racing a return** of a line to that line's path: git refuses the second (the folder or the registration already there), and it
   is said.
2. **Its start racing a take of the same line's free slot.** Its resume checks the line's tree under the shared window and then takes the
   window again to open its record (`Loop/Driver.cs:2108-2117`, `:2184`), and a conversation's new part in its own tree takes no window at all
   (`Conversations/Chat/ChatRunner.cs:630-637`). Every rename the pool makes holds the exclusive window, which holds the first; and a take passes
   over a slot whose line has words waiting, a start due this look or a standing move (§7), which is the reason nearly every such start has, and
   which both builds read from the same service and the same boxes. What remains is a start whose reason the pool-aware build had not yet read
   when it judged the slot again under its hold: the folder is then gone at that start's spawn, so the start fails rather than running
   anywhere else, since its working directory is the line's own path, which no other line's slot ever takes. That failing start is read, not
   run; row d's pinned-build rehearsal runs it.
3. **Its resume of a line whose slot was taken** runs without the slot's dependencies and without work row i kept aside, as its TREEGONE1 note
   says of the first; the pool-aware build lays the work back at the line's next return.
4. **Its session in a line's own free slot** leaves HEAD moved on that line's branch and the slot under the pool's lock (`SessionLocks.cs:65`);
   the pool-aware look's end records it as the line's work (§6) *(O1-F10)*.
5. **A pool-aware process killed between a return's placement and its last check** leaves the slot at the returning line's path, on that
   line's own branch, its npm links possibly still leading into the path the slot left, until the pool-aware look's takeover moves it back to
   its park (§2.3). A build without the pool that resumes that line in that window runs on the line's own branch with those links; the window
   is a crash's and closes at the next pool-aware look.

**Rejected:** a second session family for pooled lines (X-14), and the slot's own path in `sessions.tree` (O-14), both in §1.1 row 1.
compatible-builds asks for a new table for rows an older build must not reach (`.claude/knowledge/compatible-builds.md`); a record whose path
still names only its line is a row it may reach.

**Proof** *(C1-4.3, O1-F14)*: from row d, the first row that writes what such a build meets, **the deployment rehearsal runs a pinned build
from before the pool** (the last release before row d, named by its version in the rehearsal's fixture) over a pooled home: a resume, words
going on, a conversation's new part, a tidy, a press, a parked slot and a finished line's empty path, each beside a pool-aware loop taking and
returning slots at once, and each again after a return stopped at every phase. Pool-off runs of this build's own code (`TreeReturnsTests`, the
TREEGONE1 ticks) are kept beside it, never in its place.

## 15. The words, en and zh

A stored shape is never changed in place (`SessionTrees.cs:443-444`): `started.tree` and `started.tree-copied` keep their words for every tree
opened fresh, a slot's first making and a tree past the count among them. Codes carry the branch, the base, counts and why; no path and no
account (X-15). Each is one line in the head, within its budget (`docs/2026-10-20-ui-ux-design.md:199`).

| Code | en | zh |
|---|---|---|
| `started.tree-kept` *(O1-F15)* | Took a kept tree of `{{repository}}` for {{branch}}, from {{basedOn}}: its files are {{basedOn}}'s, and it keeps what earlier work installed and built ({{kept}}). | 已为 {{branch}} 取用 `{{repository}}` 保留的工作树，基于 {{basedOn}}：文件与 {{basedOn}} 一致，并保留先前安装和构建的内容（{{kept}}）。 |
| `started.tree-kept-dropped` *(O1-F3)* | {{dropped}} were not carried to this tree's place, since each names where it was built: they are made again here. | {{dropped}} 没有带到这个工作树的位置，因为它们记着自己构建时的路径：会在这里重新生成。 |
| `started.tree-kept-fresh` (one per npm project) | Its dependencies in `{{project}}` match its lockfile: `npm ci` is not needed there unless a lockfile changes. | `{{project}}` 中的依赖与锁文件一致：除非锁文件改变，否则无需在其中运行 `npm ci`。 |
| `started.tree-kept-stale` (one per npm project) | `{{project}}`'s lockfile changed since its dependencies were installed: run `npm ci` there. | `{{project}}` 的锁文件在依赖安装之后改变了：请在其中运行 `npm ci`。 |
| `started.tree-kept-built` | Earlier builds' outputs are kept, so builds there are incremental; a kept output is no proof this commit builds. | 先前构建的产物已保留，构建是增量的；保留的产物不能证明当前提交可以构建。 |
| `started.tree-back` | Went back into its tree, where its branch `{{branch}}` still was. | 回到了它的工作树，分支 `{{branch}}` 仍在那里。 |
| `tree.returned` | Its tree had been taken for other work, so Daoris brought a kept tree back to its place, on its branch `{{branch}}`, where its commits are; what earlier work installed and built there is kept. | 它的工作树曾被其他工作取用，所以 Daoris 把一个保留的工作树带回原处，放到它的分支 `{{branch}}` 上，提交都在那里；先前安装和构建的内容已保留。 |
| `started.tree-past-kept` | Every kept tree of `{{repository}}` was in use, so it opened a tree of its own, as before. | `{{repository}}` 保留的工作树都在使用中，所以它像以前一样打开了自己的工作树。 |
| `tree.visit-refused` *(C1-2.5)* | This tree is being handed to other work right now, so the terminal did not open in it; open it again in a moment. | 这个工作树正在交给其他工作，所以终端没有在其中打开；请稍后再打开。 |
| `review.show-again-moved` *(O1-F12)* | Set-up step `#{{quest}}`'s tree has served other work since it showed its build, so there is no build of its own to show again. | 设置步骤 `#{{quest}}` 的工作树在展示构建之后被用于其他工作，所以没有它自己的构建可以再次展示。 |
| `ended.output` | Done with output: {{count}} commit(s) on `{{branch}}`, ready to land. | 已完成，有产出：`{{branch}}` 上有 {{count}} 个提交，可以落地。 |
| `ended.no-output` | Done without output: no commits and nothing uncommitted. | 已完成，无产出：没有提交，也没有未提交的内容。 |
| `ended.uncommitted` | Done, with work it did not commit ({{count}} path(s)): its tree stays its own until that work is committed or discarded. | 已完成，但有未提交的工作（{{count}} 个路径）：在提交或丢弃之前，它的工作树仍归它所有。 |
| `tree.kept-held` | Kept tree `{{name}}` stays with its work: {{why}}. | 保留的工作树 `{{name}}` 仍归它的工作所有：{{why}}。 |
| `tree.kept-stuck` | Kept tree `{{name}}` could not be brought to the line, and git said: {{said}} | 保留的工作树 `{{name}}` 无法带到主线，git 的说法是：{{said}} |
| `tree.kept-unclean` *(C1-2.4, O1-F4)* | Kept tree `{{name}}` was not handed on: after it was switched, it held {{count}} path(s) that were not its new branch's ({{paths}}). | 保留的工作树 `{{name}}` 没有交给下一项工作：切换后，它有 {{count}} 个路径不属于新分支（{{paths}}）。 |
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
store and its census (a), the take, return, finish and retirement over git with nothing wired (b), the readers that meet a gone tree (c).
**No door that sets a count lands before row f** *(C1-4.1)*: rows d and e read `keptTrees` only through a test's seam, so no person can switch
a pool on before lines come back through it (e) and slots retire and leave safely (f). **Row d starts only once row b's note is read** by both
makers and the steward finds §1.1 row 1's trade holds on its numbers (O1, §3).

### a. The slots named beside today's trees, and the census. Nothing is pooled.

- **Changes.** `KeptTree`, `TreeClaim`, `TreeVisit`, `LineEnd`, `LineRecord` and `TreePool`'s store reads and writes; `SlotChoice` and
  `SlotJudgement`, pure, in `Domain/`; the store's migration step (`kept_trees`, `tree_claims` with its unique index on `to`, `tree_visits`,
  `line_ends`, `line_records`, every column required, after the last step at build time, the writer stamp unchanged, as `bindings` was added,
  `Store/DesktopStore.cs:107-117`); `PoolReadersCensusTests`, a by-name source scan in `ConfigSeamTests`' manner listing §3.2's re-keyed sites
  as *not yet moved*, each with its row. Nothing calls them. D212 takes a note that §13.3's workplace family gains these tables.
- **The person sees.** Nothing.
- **Proof.** The store's cases: a build of z-iii never opens the tables, the stamp unchanged, a migration interrupted at each step leaving the
  store as it was; one claim per slot and one per destination with the shell, a headless `--share` loop and a verb racing; a visit and a claim
  racing for one slot, one refused; a dead run's claim taken over and a paused live one never, §13.4's three cases with live processes;
  `SlotChoice` and `SlotJudgement` over every row of §5.1 and §5.2; the census passing, and failing on each kind of planted site;
  `StructureTests` with no new census edge. Row a settles the shapes; the lifecycle's proof is row b's (C1, §4).

### b. ⚖ Taking, returning, finishing, ending and retiring a slot over git, unwired.

- **Changes.** `TreePool.TakeAsync`, `ReturnAsync`, `FinishAsync`, `EndAsync`, `RetireAsync` and `VisitAsync`, each with its phases and its
  takeover (§2.3); the judgement's git reads and its whole walk of links and repositories (§5.2); the park, the drops, the switch, the two checks,
  the placement and the links made again (§5.3); git's steps in the claim's own job; the pool's lock reasons, `SessionLocks.HoldAsync` taking the
  free one over and the let-go putting it back (§2.4); the one build-folder list and its named subsets, `TreeScratch`'s own byte for byte, with
  dated notes on D195 point 3, D102's TREEKEEP1 and D51's TREEDEPS1; D51's note (§13).
- **The person sees.** Nothing: no removal of today's reads differently *(C1-4.2)*.
- **Proof** (`TreePoolGitTests`, `TreePoolStopsGitTests` and a Windows `Process` class), each case seen failing first with its guard taken out:
  - **The brief §8's first proof, its clean half.** Line A commits on its branch, installs an ignored `node_modules` and `bin/`, and ends with
    nothing uncommitted; line B takes the slot at the line. B's path holds the line's tracked files and A's carried ignored folders and none of
    A's tracked work; A's branch holds its commits; A's path is empty; `BranchOfTree(B's path)` is B's branch; A's `GrownBranch` is unchanged;
    the slot's registration name is unchanged; A's `line_ends` holds its tip, its read moment and its scratch.
  - **No work lost:** no take of a slot holding an untracked file, a tracked edit, a half-done merge or rebase, an `assume-unchanged` path, a
    repository git would not ignore, a person's lock, a link outside a package folder or leading outside the slot, a HEAD moved since its let-go
    off its line's branch, or a commit no ref holds *(C1-1.2)*.
  - **The checks** *(C1-2.4, O1-F4)*: a write planted after the judgement and before the switch, through a seam as `BeforeRefDeleted` is
    (`Trees/SessionTrees/TreeContext.cs:40`), fails the first check; a `.gitignore` that ignores a kept folder on A's branch and not at B's start
    fails it; a write planted after placement fails the last; in each, no record opens, the slot is back at its park, detached and `stuck`.
  - **The path invariant** *(C1-2.1, O1-F2)*: a look's TREEFOLLOW1 run between every two steps of a take and of a return never learns a branch
    other than the line's own at a line's path; an older resume's tree check run against each step is held for the look while a rename runs.
  - **The destination** *(O1-F1)*: an empty folder and a leftover folder at R's path, each moved aside before the placement, the slot never
    nested; two returns of one line from two processes, one refused by the unique `to`; a take of A's free slot racing A's return.
  - **Every boundary stopped** *(C1-4.3, C1-2.2, O1-F5)*: each rename stopped after the folder moved and before git's `gitdir` was written, a
    held `gitdir` file standing for it, and repaired; the switch done and its phase not written; the relink stopped halfway, the next judgement
    admitting the former path; the record opened and the claim not completed; the finish stopped after its branch's delete and before the record
    was composed; an `end` and a `retire` stopped at each phase; a git process still running when its claim's process is killed, ended by the
    job, and a git lock file left behind holding the slot until it is gone. Each leaves a state the next look reads right, and nothing at a
    line's path but that line's branch.
  - **The finish** *(C1-2.3, O1-F7, C1-2.7)*: a landed line's tip held, its slot parked, its branch deleted at the commit judged, its record
    composed and posted with the line's path, and posted again unchanged after a refused post; a line whose slot was taken while it could come
    back, finished later, composed from its `line_ends`; a branch checked out anywhere never deleted; a no-output line posting none.
  - **What crosses a move** (§11) *(O1-F3, C1-1.4, C1-2.8)*: with the old path holding another tree's sentinel files, a .NET build with
    `--no-restore` and without, a workspace import and the web build read and write only the new path; a `.venv` with an editable install and
    `node_modules/.vite` are gone after the move.
  - **Windows.** `git worktree move` refused by a file held open without delete-sharing (`busy`), the slot unchanged; a switch refused by a held
    file (`stuck`), what it left recorded in the row's note; a workspace junction leading into the old path made again into the new; a path of
    250 characters; a repository with a submodule refused.
  - **Measured, written in the row's note, read before row d:** a .NET build, the web build and an `npm` install's saving in a slot moved to a
    new path, against one built in place and one fresh; how long each exclusive hold lasts; how often a rename is `busy`.

### c. ⚖ The sites that meet a gone tree learn that a slot can come back; the opened moment; a taken line's review. Inert until d.

- **Changes.** The seams §3.2 marks row c: `ContentHolds.HoldsAsync`'s *opened* from the line's `GrownBranch.At` for a slot (a removal's
  judgement, hence ⚖); `Continuation.Judge`'s `treeStands`, `ConversationTrees.Stands`, the chat runner's gone-tree refusal
  (`ChatRunner.cs:630-633`), the briefed move's (`BriefedMove.cs:105-107`, `:188-190`) and the limit move's gates (`LimitMove.cs:75`, `:261`)
  asking the pool *stands, or can be brought back*, which answers by the disk alone while no pool is on; `SessionReviews` reading a pooled line's
  branch in the checkout when its tree is gone and unlanded; `ReviewServed.Stands` asking whether the slot at the path was placed after the
  showing *(O1-F12)*.
- **The person sees.** Nothing; every legacy answer is the same.
- **Proof.** Per seam, a fixture whose record names a line whose slot was taken answers *can be brought back*, and the legacy fixture answers as
  before; the review's graph held equal to its chain, its oracle (`SessionReviews.ChainReadAsync`, `SessionReviews.cs:67`); the census shrinks by
  these sites.

### d. ⚖ The pool wired, read only through a test's seam.

- **When.** After row b's note is read (above).
- **Changes.** `keptTrees` read through a test's seam alone; the driven start (`Driver.cs:2192-2221`) and the conversation's
  (`ChatRunner.cs:635-651`) taking from the pool (§5.1) before their tree checks, else opening as today; the end, and the look's end (§6); the
  holders composed by Loop (§7): a due landing, a start due, today's standing-move boxes, and the terminal's visit taken by the shell before its
  shell starts and let go when its job is empty *(C1-2.5)*; the look's takeover of a dead run's claim and visit (§2.3); the look's finish for a
  pooled line (§10); the press on a slot (§9); the words (§15); the Disk space `kept-trees` kind; dated notes on D88 and D195.
- **The person sees.** Nothing: no door sets a count.
- **Proof.** The brief §8's first proof as a tick test on a stand-in harness that records its working folder, as `SessionMessagesTickTests`'
  TREEGONE1 ticks do; each holder of §7 holding, the terminal's visit from a `--share` loop's view and a verb's; a terminal opening while a claim
  is in flight, refused; a start past the count opening a tree as today; `CapCountTickTests` and `LookParallelTests` with the pool on; the
  brief §9's suites whole; the family rehearsal with one pooled repository; **the deployment rehearsal with the pinned pre-pool build** (§14)
  *(C1-4.3, O1-F14)*; `identity.test.ts` and `noteLines.test.ts` with the new codes.

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
  rehearsal again, its returns stopped at every phase. **Installed, the parent's:** one Claude Code session in a pooled repository, its slot
  taken by a second start, then resumed: the conversation found (one session, one resume, nothing forced to a limit), on the owner's account
  under §18 item 5.

### f. ⚖ Lowering, a repository leaving, and the count's doors.

- **Changes.** `retiring`, raising, a repository leaving, a checkout gone (§9); a cap that lowers an absent count retiring as a lowered count
  does, said where the cap is set *(C1-3.2)*; then, last, **the count's doors**: `daoris driver trees <repository> keep <n>`, `SET_TREES`'
  `kept`, the Projects control, Ask Daoris's `setting` door, a repository with submodules refused *(C1-4.1, O1-F15)*.
- **The person sees.** The count's control on the Projects row, off by default; *n more retiring* where a count was lowered with slots held.
- **Proof.** A lowered count, and a lowered cap under an absent count, each retiring a free slot (the slim's manifest written, moved aside,
  its `node_modules` gone at the slim's hour, the rest `unmarked` and waiting the days) *(C1-1.3, O1-F8)* and a held one at its let-go; one
  holding work no ref holds staying, said; a retirement stopped at each phase; `KeepSizeGitTests`, `CleanupLeftoversTests`, `KeepingSlimTests`
  and `SystemSpaceTests` whole; each door's case, and the one refusing a repository with a submodule.

### g. ⚖ On for the install's repository, measured.

- **When.** After rows a to f and the owner's yes (§18 item 2). The parent's, on the install.
- **Changes.** `keptTrees` set for the install's repository; no code.
- **The person sees.** Sessions in that repository start in a kept tree, said (`started.tree-kept`, `tree.returned`), and the Projects row's
  count.
- **Proof, measured before and after a day of the install's ordinary work** *(C1-4.4)*: the home's `work/` bytes split into legacy trees, kept
  slots, trees past the count and set-aside folders, from the Disk space card's kinds; **the time from a start's admission** (the planner's
  verdict for a driven start, the person's request for a conversation) **to its first turn**, over twenty starts each way, with the claim, the
  park, the drops, the switch, the checks, the placement, the links, the dependencies and the record's open apart, since today the tree is made
  before the record opens (`ChatRunner.cs:644-664`; `Loop/Driver.cs:2192-2223`); a .NET build in a moved slot; how often a start went past the
  count, a rename was `busy`, a check failed and a line was brought back.
- **What permits row h** *(C1-4.4)*: over a week of row g, no record opened on a slot a check refused; nothing lost or held for the person by the
  pool's own steps; no slot `stuck` past a look but for a held file or a git lock file left; the median admission-to-first-turn of a take below
  a fresh tree's in the same repository; and the bytes under `work/trees` no longer growing day over day past the kept slots and the trees past
  the count.

### h. ⚖ The default flips.

- **Changes.** A repository with trees and no count keeps `min(cap + 1, 16)` slots. Once row g's criteria hold (above); the steward's call,
  told to the owner, since it carries out the owner's direction.
- **The person sees.** In every repository with trees, sessions starting in kept trees, and nothing new to set.
- **Proof.** The tick and rehearsal cases of rows d and e with the default rather than a set count; a repository with trees off unchanged
  byte for byte; `DriverConfigKeepsTests` with and without the key.

### i. ⚖ Uncommitted work kept aside off its branch. Only on the owner's yes (§18 item 3).

- **Changes.** §5.5: `kept_work`, the keep, `AnyRef` excluding `refs/daoris/kept/`, the lay-back; D164's note.
- **The person sees.** A tree whose session left uncommitted work goes back to the pool, said in `ended.uncommitted`'s place by a line naming
  the kept ref; the work laid back when the line returns.
- **Proof.** The brief §8's first proof whole: A leaves an untracked file and a changed tracked file; B takes the slot at the line; neither is in
  B's path; both are in `refs/daoris/kept/…`; A coming back at the same base has them laid back. **Staged and unstaged apart** *(C1-2.6)*: a path
  staged at one version and edited to another comes back staged at the first and on disk at the second; a deletion staged and one unstaged come
  back as each was; a lay-back stopped between its two steps resumes from its phase. Each keep commit is parentless and never listed by
  `for-each-ref --contains <tip>`; past the bounds the slot stays A's; a write after the keep holds the slot; on Windows a file held during the
  reset loses nothing of A's.

### j. ⚖ A finished line's scratch outside the list cleared at a take. Only on the owner's yes (§18 item 4).

- **Changes.** §5.2 step 5's carried scratch cleared, after the finished line's record is kept and named in it; D195's note.
- **The person sees.** A new line's tree without the last one's logs and probes.
- **Proof.** A finished line's `_fixtures/` and `local/` gone from the next line's slot, its record naming them as KEEPSIZE1b's record names a
  set-aside tree's scratch; a line that may come back never cleared.

**How each input folds in.** FOUNDATION1: every row after mod-x; the tables Workplace's family by §13.3; no port added; the standing move read
from today's boxes until Part B's row f-i lands its `moves`, then from it *(O1-F11)*. D51, D79, D88, D102,
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
   reading). *Where to look:* §8, §5.1 step 5, §1.1 row 4.
2. **The pool on for the install's repository** (row g). *What it is:* the repository the install works in keeps cap + 1 trees, three today,
   and a session there takes a free one, switched to its new branch and checked at a parking name and then moved to its own path, instead of
   opening a fresh one; a tree is taken only where it holds nothing but what git tracks at a ref and the ignored build folders, and the build
   folders that name their own path are made again rather than carried. *What a yes does:* sets `keptTrees` for that
   repository and starts row g's day of measurements. *Where to look:* §5, §7, row g, and the proofs of rows a to f.
3. **Daoris keeping a let-go line's uncommitted work aside, and clearing it from the tree** (row i). *What it is:* two parentless commits, one
   of what the line had staged and one of its working files, untracked ones included, on `refs/daoris/kept/…`, made by Daoris off the line's
   branch, where D164 point 1 has Daoris commit
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

## 20. What stays unmeasured, and what this design's checks do not cover

- **How much of the build saving survives a move**, now that every folder naming its own path is dropped at a move (§11): row b measures npm's
  saving and a .NET build and the web build in a moved slot against one in place and one fresh, its note read before row d; and whether `obj/`
  less NuGet's restore files can be carried, by row b's isolation proof. If the saving left is too small to carry the trade of §1.1 row 1, a
  later round weighs it again with that number, never silently.
- **`git worktree move` on Windows**: its refusal with a handle held, the registration name it keeps, its nesting into a destination that
  exists and its rename before its `gitdir` write (both as the reader cites git's source, not run here), and what a refused switch leaves: row b.
- **How long the exclusive hold lasts** at each rename and the relink, and how often it is not to be had: row b, and row g on the install.
- **Off Windows, whether a dead run's git process may still be writing** when the takeover reads the disk: the job holds there no process
  (`ProcessJob.cs:15-18`), so the takeover reads git's lock files, which a git killed mid-write leaves and a git still running holds; a
  process that holds neither is read as done.
- **Whether a start of a build without the pool fails cleanly** when the folder of its line's path went between its check and its spawn
  (§14 item 2): read, not run; row d's pinned-build rehearsal runs it.
- **Whether Claude Code reads anything of a directory but its path when it resumes**: row e's installed proof (§18 item 5).
- **How often every kept tree is held** so a start goes past the count, and how often a line is brought back: row g.
- **The cost of making links again** after a move in a large `node_modules`: row b, with TREELINK1's bound of a million entries.
- **An agent other than Claude Code and Codex that keys a conversation by path**: none is read here; a plugin's door is read by its provider's
  contract (D213), which this design does not change.
- **A build without the pool beside a pool-aware one on one home**, racing a return: said (§14), not measured.
- **Whether a folder keeps its creation time across a rename**: assumed, and no longer read for a slot (§3.2).
- **What this round's checks do not cover.** It is a document: `npm run verify` and the documentation checks prove its routing and shape, not
  its claims about the code. Round 2's claims were read at `f92e95415` for every line both reads of round 1 cite and every line round 2 adds or
  re-cites (§22.3); round 1's other lines rest on its reading at `f8fbb1bb7`, re-cited where LIMITSWITCH3 moved them (*Read at*), and the
  survey's and the proposals' other lines are taken as they cite them. git's own behaviour (`worktree move` and its nesting and `gitdir` order,
  `worktree repair`, `switch --no-overwrite-ignore`, `write-tree` from the real index) is taken from its documentation and, where the Opus
  reader cites it, from git's source as cited, not run. Nothing was run. Both makers' reads of round 2 check it next.

## 21. Rounds

| Round | Written | Read | Answered |
|---|---|---|---|
| 1 | 2026-10-26, Claude Opus 5.5, from main at `f8fbb1bb7`: the synthesis of Codex's and the independent Opus reader's sealed proposals; every question of the brief answered (§1); each difference chosen with its reason and the case the other gets wrong (§1.1); one choice neither proposal's, the slot moved to its line's path (§1.1 row 1, §2); the rows in order with their proofs (§17); D214 proposed | both at `3f55eb1e5` (`docs/2026-10-26-tree-pool-reads.md`): Codex, another round (18 findings, 12 blocking); the independent Opus reader, another round (15 findings, 4 blocking), both keeping the central choice | §22, every finding of both |
| 2 | 2026-10-26, Claude Opus 5.5, from main at `f92e95415` (FOUNDATION1's mod-vii and LIMITSWITCH3 landed since round 1): both reads answered one row each (§22); every blocking finding taken and met by a contract and the row that proves it; the slot parked between lines, switched and checked under a parking name and placed last under the exclusive hold, its destination unique and absent; the claim's whole intent, every kind's phases and a takeover that reads the disk first, git's steps in a job; a terminal's visit in the store; `line_records`; only what reads its own path carried across a move; the keep of index and working tree both; the count's doors at row f; the install measured from admission; D214's round-2 note | owed: Codex's, and the independent Opus reader's as a foundation read | — |

## 22. Round 2: both reads of round 1 answered

Both makers read round 1 at `3f55eb1e5`, verbatim in `docs/2026-10-26-tree-pool-reads.md`. Codex asked another round: 18 findings, 12 blocking
(1.1, 1.2, 2.1 to 2.7, 4.1, 4.3, 4.4), and nothing for the owner beyond §18. The independent Opus reader asked another round: 15 findings, 4
blocking (F1 to F4), keeping the central choice and scoping the round to §2.3 to §2.4, §5.2 to §5.3, §11, §14 and row b. **Every blocking
finding of both is taken**, each met by a contract a reader can check line by line and a row that proves it; every non-blocking finding is taken
too. Where the reads meet, one change meets both, the stricter taken: the path invariant during a move (C1-2.1 with O1-F1, O1-F2, C1-1.1 and
O1-F6); the check after the switch (C1-2.4 with O1-F4); the crash protocol (C1-2.2 with O1-F5 and O1-F10); the finished record (C1-2.3 with
O1-F7); what crosses a move (O1-F3 with C1-1.4 and C1-2.8); the retired slot's slim (C1-1.3 with O1-F8); a finished line coming back (C1-2.7
with O1-F9); the terminal's hold (C1-2.5 with O1-F11, Codex's admission being the stricter); the default's clamp and the cap's lowering (C1-3.2
with O1-F15). No finding is answered rather than taken.

### 22.1 Codex's read (18 findings, 12 blocking)

| Finding | Verdict | Where it went, and the row that proves it |
|---|---|---|
| 1.1 (blocking) An interrupted move is not an interrupted opening: after a move and before a switch the slot is on the previous line's branch, which the strays keep (`TreeStrays.cs:188-190`) | **Taken** | No slot is ever at a new path before its switch: it is parked under `k-<n>`, which the strays never read as an opening (`:183`), switched and checked there, and placed last (§5.3); OPENCUT1's lock is used only at a slot's first making, a claim holding the pool's own reason (§2.4); §14's table states each intermediate state as a build without the pool reads it, and §0.2's round-2 table withdraws round 1's claim. Row b (every boundary stopped), row d (the pinned build's strays over each) |
| 1.2 (blocking) The last guards do not judge every link or nested repository (`TreeSetAside.cs:116-124`, `:127-132`) | **Taken** | §5.2 step 3: a whole walk of the slot, TREELINK1's; a link carried only inside a package folder beside a tracked `package.json` and leading into the slot's own present or former path (npm's workspace link), any other holding the slot; a repository holding it wherever git would not ignore it, and for a line that may come back wherever it lies. Row b (*no work lost*: a link outside a package folder, one leading outside the slot, each kind of repository) |
| 1.3 Manifest-free retirement gets no slim (`SetAsideKeeping.cs:87-90`) | **Taken** | §9: retirement writes the slim's own schema-1 manifest at `judged` and `moved` (`TreeSlims.JudgeAsync`; `SlimManifest.cs:17-20`), so its packages go at the hour and the rest waits the days as `unmarked`. Row f |
| 1.4 Workspace junctions are not the only path-dependent contents of `node_modules` (`TreeDependencies.cs:24-28`, `:44-45`) | **Taken** | §11 *What crosses a move*: `.cache`, `.vite` and `.vite-temp` dropped at every move, as TREEDEPS1 leaves them; round 1's sentence withdrawn. Row b (*what crosses a move*) |
| 2.1 (blocking) The path invariant fails during a return: A's copy at R's path on A's branch before the switch; an older resume accepts any listed tree there, starts sharing `TreeLock` | **Taken**, with O1-F2 and O1-F1 | §3.1, §5.3: a slot is renamed into a line's path only after it is switched to that line's branch and checked at its parking name, and every rename runs under the repository's exclusive hold, which an older build's tree check honours, being held for a look while a replay holds it (`Loop/Driver.cs:2110-2111`); its tidy honours it too (`TreeStrays.cs:115`; the tidy holds the repository alone). The residue between an older start's two holds is named and bounded in §14 item 2 (a start due, words waiting and a standing move hold the slot, §7). Row b (*the path invariant*), row d and e (the pinned build beside takes and returns) |
| 2.2 (blocking) The takeover lacks a durable intent and outcome; a git child may outlive its run (`Platform/ChildProcess.cs:62`) | **Taken**, with O1-F5 and O1-F10 | §2.3: `tree_claims` holds the whole intent (`start`, `expect`, `from`, `park`, `to`, the opened `record`), every kind has its phases, the takeover reads both folders, git's registration and git's lock files before acting, and git's steps run in a job of the claim's own, made empty first, as a heavy run's gates are (`ProcessJob.cs:23-26`). Row b (*every boundary stopped*, a git process outliving its claim's process) |
| 2.3 (blocking) The finished record's replacement is underspecified; capture before the slot moves, including a slot reused before its line is removable (`TreeSweeps.cs:218-230`) | **Taken**, with O1-F7 | §10, §2.3: `line_ends` captures the tree's facts (tip, read moment, scratch left) at every let-go and at every take that moves a slot away (§5.3 step 2); the finish composes `FinishedTree`'s fields into `line_records`, its `Folder` and moment fixed once, posted unchanged until acknowledged, then deleted. Row b (*the finish*) |
| 2.4 (blocking) A clean source is no clean destination; restore the check after the switch; withdraw *retire whatever it holds* | **Taken**, with O1-F4 | §5.3 steps 7 and 11: status and HEAD read at the parking name after the switch and again at the destination after the placement and the links; a failed check never opens a record and returns the slot to its park, detached, `stuck`; §5.4: a stuck slot goes only by today's press, which refuses work no ref holds and asks again. Row b (*the checks*) |
| 2.5 (blocking) A terminal's hold needs admission, not observation (`Modules/Windows/TerminalModule.cs:209-216`) | **Taken** | §2.3, §7: a `tree_visits` row the shell writes through `TreePool.VisitAsync`, a call down into Workplace, before its shell is queued, refused while a claim is in flight there, read by every process that takes a slot, held until the terminal's job is empty, its shell joined to a job of its own; a dead shell's visits taken over by the look. Row d |
| 2.6 (blocking, row i) The keep loses distinct staged work | **Taken** | §5.5: two parentless commits, the real index's tree (`write-tree`) and the working tree's through a private index, laid back in two steps, each a phase. Row i (staged and unstaged versions of one path, deletions staged and unstaged, a lay-back stopped between its steps) |
| 2.7 (blocking) *Own slot, no git* includes a detached no-output slot | **Taken**, with O1-F9 | A finished line's slot is parked at its finish, so its path is empty, as after today's tidy (§10 step 3); a no-output line's slot stays on its own empty branch until a take moves it, and its branch is then grown again before a slot is brought back (§6); choice 1 requires the line's own branch at its path (§5.1, §5.3). Row b (*the finish*), row e (a landed line and a no-output conversation coming back, neither detached) |
| 2.8 Windows measurement needs a correctness condition | **Taken** | §11 *The proof of what crosses*: with the old path holding another tree's sentinel files, the moved slot's builds read and write only its new path, before the saving is reported. Row b |
| 3.1 The step claim's rejection of an occupation lease is overstated; keep reservation-before-use and the final check | **Taken** | §1.1 row 2 restated as economy, not impossibility; the claim reserves the slot from its choice to its record's open (§2.3), the ledger holding it only after (`SessionLedger.cs:1602-1616`); the final check restored (§5.3 step 11) |
| 3.2 The live default's removal consequence; cap 16 and above against a count through 16 | **Taken**, with O1-F15 | §8: the default `min(cap + 1, 16)`; a cap that lowers an absent count retires as a lowered count does, said where the cap is set; §9's *never by age* names it. Row f |
| 4.1 (blocking) Row d enables reuse before row e makes it safe | **Taken** | §17: rows d and e read `keptTrees` through a test's seam alone; the count's doors land last in row f, after returns (e) and retirement and departure (f) |
| 4.2 Row b is not inert for existing trees (`wwwroot`, `ContentHolds.cs:588-594`) | **Taken** | §11, §3.2: `TreeScratch`'s list stays byte for byte for removals; `wwwroot` joins only what a take carries; a removal's reading of `wwwroot` left to a correction of its own. Row b (*the person sees nothing*, today's removal suites whole) |
| 4.3 (blocking) The crash and compatibility proofs omit the dangerous boundaries; a pinned pre-pool build must run against concurrent takes and returns | **Taken**, with O1-F14 | Row b: each rename stopped between folder and `gitdir`, the switch done and unrecorded, the relink halfway, the record opened and the claim unfinished, the finish, end and retirement at each phase; §14 *Proof* and rows d and e: the last release before row d, pinned by its version, run over a pooled home beside a pool-aware loop taking and returning, and after a return stopped at each phase |
| 4.4 (blocking) Row g's clock cannot measure the saving; define what permits row h | **Taken** | Row g: from a start's admission to its first turn, every step apart, since the tree is made before the record opens (`ChatRunner.cs:644-664`); row g's *What permits row h*: no record on a refused slot, nothing lost or held by the pool's own steps, no slot stuck past a look but for a held file or a git lock file, a median take faster than a fresh tree, and `work/trees` no longer growing past the kept slots and trees past the count |
| §4, closing: row a cannot settle the lifecycle protocol | **Taken** | Row a now says it settles shapes only; the lifecycle's proof is row b's |
| §5: nothing more for the owner | **Agreed** | §18 unchanged in its questions, item 1's and item 3's words corrected (O1-F15, C1-2.6) |

### 22.2 The independent Opus reader's read (15 findings, 4 blocking)

| Finding | Verdict | Where it went, and the row that proves it |
|---|---|---|
| F1 (blocking) A return's destination is neither checked nor exclusive; `git worktree move` nests into an existing folder | **Taken**, with C1-2.1 | §2.3: a unique index over a claim's `to`; §3.3: a return waits while its line's own slot is under another claim, and a take passes over a slot whose line has a claim naming its path; §5.3 step 8: the destination must not exist, a folder there moved aside first as TREEGONE1 does (`TreeReturns.cs:87-92`), git listing the slot at exactly `to` after; every rename under the exclusive hold. Row b (*the destination*) |
| F2 (blocking) Moving before switching puts another line's branch at a line's path, which TREEFOLLOW1 records (`TreeFollows.cs:91-126`; `AutoTidies.cs:190`) | **Taken**, with C1-2.1 | §2.2, §3.1, §5.3: a slot leaves a line's path by a rename first and is detached at its parking name, where no `GrownBranch` entry exists for TREEFOLLOW1 to learn (`TreeFollows.cs:101-104`); it is switched and checked there, and renamed into a line's path last, on that line's branch; the takeover never leaves a slot at a line's path unchecked (§2.3). The reader's order (detach in place first) is kept in substance with the rename first, so a crash between leaves the line's path empty rather than detached, which TREEGONE1 would accept (`TreeReturns.cs:58`). Row b (*the path invariant*) |
| F3 (blocking) Kept folders naming the old absolute path read another line's working copy (`.venv`, NuGet's props under `--no-restore`) | **Taken**, with C1-1.4 and C1-2.8 | §11 *What crosses a move*: carried only `node_modules` (its top caches dropped, its links made again), `bin`, `dist`, `build`, `out`, `coverage`, `wwwroot`, `__pycache__`; dropped `.venv`, `obj`, `target`, `.angular`, `.next`, `.gradle`, each to join the carried subset only by a dated note on row b's isolation proof; `started.tree-kept-dropped` tells the agent. Row b (*what crosses a move*), its note read before row d |
| F4 (blocking) Nothing re-reads the slot after the move and the switch (X-5 step 9 dropped) | **Taken**, with C1-2.4 | §5.3 steps 7 and 11; §1.2 says round 1 dropped it. Row b (*the checks*) |
| F5 The `moving` crash recovery is mis-stated; the link re-make has no phase | **Taken** | §2.3: a folder at one path and a registration at another repaired by `git worktree repair <folder>`, a registration gone making the folder the pool's stray; §5.3 step 9: `relinked` a phase, `kept_trees.before` keeping a slot's former paths so the next judgement admits links into them. Row b (a held `gitdir` file; a relink stopped halfway) |
| F6 OPENCUT1's lock during a return lets the strays delete the returning line's branch (`TreeStrays.cs:183-201`, `:227-238`) | **Taken** | §2.4: the pool's own reason naming the claim's run through every claim; OPENCUT1's only at a slot's first making. Row b, row d (the pinned build's strays over a killed return) |
| F7 The end in place has no store home for its pending record; say what `Left` and `Whole` mean | **Taken**, with C1-2.3 | §10: `line_records`, its first moment kept, deleted on acknowledgement; `Left` the scratch left in the parked slot and carried, `Whole` false. Row b (*the finish*) |
| F8 A retired slot is not slimmed (`SlimManifest.cs:11-13`) | **Taken**, with C1-1.3 | §9: the slim's own manifest at retirement. Row f |
| F9 A finished line that comes back goes on detached (`TreeReturns.cs:58`) | **Taken**, with C1-2.7 | §3.3's finished-line row, §10 step 3: the slot parked at the finish, the line's path empty; a landed line takes a slot for a new path from its landing, as today it grows a fresh tree (`Loop/Driver.cs:2119-2127`); a no-output line's branch grown again; §14 says what a build without the pool does. Row e |
| F10 Who runs the end is underspecified; `end` and `retire` have no phases; an unlocked slot matches no lock case | **Taken**, with C1-2.2 | §6 *Who runs the end*: the look, idempotently, for any slot with no holder, no claim and no `line_ends` since its `since`, recording a HEAD moved on the line's branch as the line's work and anything else as `held`; §2.3: `end` and `retire` phases; §2.4, §5.2 step 2: an unlocked slot accepted and locked again. Row b (each phase stopped), row d (a park ended in another process; an older build's session in a line's own slot) |
| F11 Three holders cannot be read as §7 says: the terminal, the standing move, the words waiting | **Taken**, the terminal by Codex's stricter admission | §7: the terminal's visit in the store (C1-2.5); the standing move read from today's boxes (`LimitBox.cs:24`; `HandOver.cs:15`) until Part B's row f-i lands `moves`, the dependency named in §0.3 and §17; words waiting held for as long as they wait (`ServiceClient.cs:2245`). Row d |
| F12 *Show it again* after a bring-back serves another line's build (`ReviewDesk.cs:226-231`) | **Taken** | §3.2: `ReviewServed.Stands` asks Workplace whether the slot at the path reached it after the showing (`kept_trees.since`), and refuses, said (`review.show-again-moved`). Row c (the seam), row e (switched on) |
| F13 §1.1 row 12 misreads X | **Taken** | §1.1 row 12 restated: both grow the branch again; the difference is where the licence lives; §0.2's round-2 table and §19 say round 1 misread X |
| F14 The race between two builds is unnamed; the rehearsal comes a row late | **Taken** | §14 *What is left* item 2 names the race (an older resume's two holds, `Loop/Driver.cs:2108-2117`, `:2184`; a conversation's new part taking none, `ChatRunner.cs:630-637`) and bounds it (the exclusive hold; a start due, words waiting and a standing move hold the slot; the start's working directory never another line's); §14 *Proof*: the pinned pre-pool rehearsal from row d |
| F15 Nits: line shifts; the default's clamp; `started.tree-kept`'s wording; `branch -D` in an undone open; a no-output tree past the count stays until the press; submodules never pool | **Taken**, each | *Read at* re-cites every moved line; §8's `min(cap + 1, 16)`; §15's `started.tree-kept` says the files are `{{basedOn}}`'s; §2.3 and §5.3 step 12 delete a take's branch by `update-ref -d` at its start, never `branch -D` (`TreeOpenings.cs:108`); §8 and §18 item 1 say a tree past the count with no commits stays until the press (`TreeSetAside.cs:18-20`); §2.2 and §12 refuse a repository with submodules. Rows b and f |
| §3 (the rows): row b's measurement decides §1.1 row 1's trade, so rows d and later wait on its note being read; Part B's `moves` and DEV6 named as dependencies | **Taken** | §17's opening and row d's *When*; §0.3 and §17 name `moves`; §8 names DEV6 |

### 22.3 The code, checked

Every line both reads cite was read at `f92e95415`, and holds as cited but where said: `TreeStrays.cs:70`, `:77`, `:115`, `:147`, `:150-153`,
`:160`, `:181-201` (`:183` the `s-` test, `:188-190` *a tree on any other branch*), `:227-238`; `TreeSetAside.cs:18-20`, `:27-30`, `:70-135`
(`:84-88` a locked tree, `:116-124` the ignored repository, `:127-132` the slim's links), `:196-197`, `:243-249` (the re-read after a move,
`:246`), `:252-270`; `SetAsideKeeping.cs:87-90`; `SlimManifest.cs:11-14`, `:17-20`, `:28-29`; `TreeSlims.cs:3-7`, `:24`;
`TreeDependencies.cs:24-28`, `:44-45`; `ContentHolds.cs:588-594`; `TreeOpenings.cs:108`; `TreeReturns.cs:56-58`, `:67`, `:70-77`, `:80-85`,
`:87-92`, `:94-98`; `TreeFollows.cs:91-126` (`:101-104` only a folder with a `GrownBranch` entry, `:116` a detached one skipped,
`:117-119`), `:132-139`; `Trees/SessionTrees/AutoTidies.cs:190` (the reader's `AutoTidies.cs:188-190`); `TreeSweeps.cs:218-230`, `:296`;
`TreeRemovals.cs:220-222` (one `--force`, so a locked tree is refused); `SessionLocks.cs:65`, `:111`, `:129`; `Trees/TreeLock.cs:50-63`;
`Loop/Driver.cs:2108-2117` (`:2110-2111` the tree check under the shared window, held for a look while a replay holds it), `:2119-2127`,
`:2133-2137`, `:2184`; `Conversations/Chat/ChatRunner.cs:630-637`, `:644-664`; `Conversations/Chat/LimitBox.cs:24`;
`Conversations/HandOver/HandOver.cs:15`; `Store/DesktopStore.cs:73-129` (steps 1 to 3; no `moves` table); `Service/ServiceClient.cs:2245`;
`Review/ReviewDesk.cs:193-197`, `:226-229` (the reader's `:226-231`); `Settings/DriverConfig.cs:552-553`; `Core/Sessions/SessionLedger.cs:1602-1616`;
`Http/Program.cs:1435-1439`; `Platform/ChildProcess.cs:56-62` (git started as an ordinary child); `Platform/ProcessJob.cs:15-18`, `:23-26`;
`Modules/Windows/TerminalModule.cs:202-218` (the reader's `:209-216`). The lines round 2 adds were read there too. Two claims of the reads rest on
git's own source, not on this repository, and were not run: that `git worktree move` nests a tree into a destination that is a directory, and
that it renames before it writes the registration's `gitdir` (O1-F1, O1-F5); round 2 takes both as true, since a design safe under them is safe
if either is false, and row b proves each.
