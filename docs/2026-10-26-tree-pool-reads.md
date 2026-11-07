# TREEPOOL1: the reads of rounds 1 to 3 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-26-tree-pool-design.md` and D214, round 1, both at
`3f55eb1e5`, each run by the steward from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one
level down. Neither carries a link, so none is rewritten; the paths both cite are code spans, in each read's own aliases. Round 2 answers every
finding of both, one row each, and each changed passage of the design and D214 is marked *(C1-n.m)* for Codex's §n item m or *(O1-Fn)* for
the Opus reader's finding n.

Both makers' reads of round 2, at `d595cabd6`, follow in the same way. Round 3 answers every finding of both, one row each, and each changed passage of the design and D214 is marked *(C2-n.m)* for Codex's §n item m or *(O2-Fn)* for the Opus reader's finding n.

**Round 4 is out for both makers' reads** (2026-11-07). On the owner's choice of that day, a slot keeps a fixed path, so its builds stay valid
and a reused tree skips the first build; round 4 is written from main at `75a4b694be`, answers the independent read of round 3 below in the
design's §24, one row each, and marks each changed passage *(R4-Fn)*, or *(R4)* where the fixed path alone changed it. Codex's read and the
independent Opus reader's are owed before any of it is accepted, and nothing is built until then. Round 4 also moved the design's own superseded
round text to the end of this file, under *Moved from the design at round 4*.

**Round 5 waits on both makers' reads** (2026-11-07). The independent Opus reader's read of round 4 is below; Codex has not read round 4. On
the owner's reframing and frame of that day, round 5 puts rooms first, with an optimised fallback, and is written from main at `bfdb82acd6`.
It answers the read of round 4 in the design's §25, one row each, and writes what it proposes in the design's section R5, with this
repository's churn measured by class (R5.2). Codex's read and the independent Opus reader's read of round 5 are both owed before any of it
is accepted, and nothing is built until then.

**Round 6, for row 2 alone** (2026-11-07), is written from main at `5643df4196` and answers the independent read of round 5 in the
design's §26. The independent Opus reader's read of round 6 is below, under *Opus (independent), round 6*: another round for row 2, on its
F1 to F3. Codex's read of round 6 is owed.

**Round 7, for rows 3 and 4** (2026-11-08), is written from main at `a94a922745` and answers, in the design's §27, Codex's read of round 6
for rows 3 and 4 and the round-6 independent read's F1 and F9 where they meet row 4. The independent Opus reader's read of round 7 is
below, under *Opus (independent), round 7*: another round, narrowly, for row 3 on its F3 and for row 4 on its F1 and F2. Codex's read of
round 7 is below too, under *Codex, round 7*: another round for row 3 and, narrowly, for row 4.

**Round 9, the rule file** (2026-11-08), is written from main at `5651fdda5a`. Round 8 was written before either read of round 7 was
seen; on the owner's choice of that day, round 9 replaces its inventory with a rule file and answers both reads of round 7 in the
design's §29, one row each. Both makers' reads of round 9 are owed before rows 6a, 3 and 4 are built.

**Rounds 9 and 10, read** (2026-11-08). Round 10, written from main at `50055ee6d6`, cuts round 9's restrictions to a floor of four
and the person's own lists (the design's §30), and both makers read the two rounds together for rows 6a, 3, 4 and 6b. Codex's read is
below, under *Codex, rounds 9 and 10*: rows 6a and 3 another round, row 4 inheriting them, row 6b ready. The independent Opus reader's
follows it, under *Opus (independent), rounds 9 and 10*: rows 6a and 6b with nothing blocking, row 3 another round narrowly on its F1
and row 4 narrowly on its F2.

## Codex, round 1

Codex's read of round 1 at `3f55eb1e5`: another round. It finds four wrong claims (two blocking: an interrupted move that is no interrupted
opening, and the last guards that do not judge every link or nested repository), eight findings on the design (six blocking: the path
invariant during a return, the takeover's durable intent, the finished record's inputs, the destination's check after the switch, a
terminal's admission, row i's keep of staged work, and a detached no-output slot), two on the choices and four on the rows (two blocking:
row d before e, and the crash and compatibility proofs).

Reviewed HEAD `3f55eb1e5`, read-only. No files changed or tests run. `Design` below means `docs/2026-10-26-tree-pool-design.md`; code paths use the inputs’ `Driver/`, `Modules/` and `Core/` aliases.

### 1. Wrong claims about the existing code

1. **Blocking — an interrupted move is not always an interrupted opening.** Design:206 and :219 say shipped strays recognize a killed take, but after A moves to B’s path and before switching, its branch is still A’s. `Driver/Trees/SessionTrees/TreeStrays.cs:188–190` explicitly rejects that combination; correct the compatibility table and cover this intermediate state.

2. **Blocking — the cited last guards do not reject every unknown link or nested repository.** Design:378 and :433 rely on `LastLookAsync`, but `TreeSetAside.cs:118–124` permits ignored nested repositories, and :127–132 uses collected links only to confirm slimming candidates. Specify a separate, complete pre-move link judgement, including how Daoris distinguishes its junctions from a person’s.

3. **Non-blocking — manifest-free retirement does not get the slim’s hour.** Design:527–528 promises both no manifest and early package removal. `Driver/Trees/SessionTrees/SetAsideKeeping.cs:88–90` requires a manifest with authorized paths; retain a slim-only manifest or withdraw that promise.

4. **Non-blocking — workspace junctions are not the only path-dependent contents of `node_modules`.** Design:583 contradicts `Driver/Trees/SessionTrees/TreeDependencies.cs:24–28` and :44–45, which deliberately exclude caches naming their original location. Correct the claim and include those caches in relocation handling.

### 2. The design

1. **Blocking — the central path invariant fails during a return.** Design:405 moves A’s working copy into historical B’s path before :414 switches it to B; an older resume accepts any listed tree there (`Driver/Trees/SessionTrees/TreeReturns.cs:58`), while starts share `TreeLock` (`Driver/Trees/TreeLock.cs:50`). Neither the opening lock nor a new SQLite claim stops that older resume from running on A’s branch; specify an exclusion protocol older writers actually honor, or revise the compatibility choice.

2. **Blocking — takeover lacks a complete durable intent and outcome protocol.** Design:181 stores no selected start commit, expected previous tip, opened-record identity or distinct completion states for end/retirement, yet :198–204 derives recovery from four coarse phases. Moreover, the git runner starts an ordinary child (`Driver/Platform/ChildProcess.cs:62`), so executor death alone does not establish that a marked git effect has stopped; recovery must reconcile surviving children, both folders and registration files before acting.

3. **Blocking — the finished-record replacement is underspecified.** Design:554 says the manifest’s inputs are “the store’s,” but none of :180–182’s tables holds the complete inputs, immutable post identity or retry outcome. Capture them before equipment moves—including when A’s slot is reused before A later becomes removable—because today `Driver/Trees/SessionTrees/TreeSweeps.cs:218–230` captures those facts only while it still has A’s tree.

4. **Blocking — a clean source does not establish a clean destination.** Design:382–385 judges ignored files under A’s rules, then :414–418 switches without a final isolation check: B can stop ignoring a retained directory, exposing A’s files to B’s `add -A`. Restore the proposal’s pre-launch verification and handle partial switches conservatively; today’s set-aside deliberately checks again after moving (`Driver/Trees/SessionTrees/TreeSetAside.cs:243–248`), and Design:431’s permission to retire a stuck slot “whatever it holds” must go.

5. **Blocking — terminal holds need admission, not just observation.** Design:485 names a terminal hold without specifying its cross-process reservation or release. `Modules/Windows/TerminalModule.cs:209–216` queues the shell after inserting only an in-process entry; reserve the tree before that queue, expose the hold to every allocator, and retain it until the terminal’s job is empty, using a downward Workplace seam.

6. **Blocking, row i — the keep loses distinct staged work.** Design:441–448 builds its temporary index from HEAD and the working files, then resets the real index. If a person stages version X and subsequently edits the file to Y, only Y is preserved; keep and restore both index and working-tree state, and prove staged/unstaged differences, deletions and interrupted restoration.

7. **Blocking — “own slot, no git” includes a detached no-output slot.** Design:358 says no git runs, while :546–556 detaches the slot and removes its empty branch. The next continuation must recreate and check out that branch even when the slot never moved; otherwise it runs detached, which today’s `TreeReturns.cs:58` also accepts without repairing.

8. **Non-blocking — Windows measurement needs a correctness condition as well as a timing.** Design:580–583 allows path-dependent outputs to rebuild, but supplies no rule ensuring they cannot resolve back into A once A’s path exists again. Row b should prove relocated tools read and write only B’s intended locations, then report surviving build savings; a successful build alone does not establish that isolation.

### 3. Choices between the proposals

1. **Non-blocking — the step claim is reasonable, but its rejection of an occupation lease is overstated.** Design:114 treats an externally ended record as necessarily leaving two conflicting authorities, although reconciliation is precisely what existing `Driver/Trees/SessionTrees/SessionLocks.cs:101–123` does. Keep the simpler claim if desired, but retain X’s explicit reservation-before-use and final-verification requirements; the ledger becomes authoritative only after its open (`Core/Sessions/SessionLedger.cs:1602–1616`).

2. **Non-blocking — the live default’s removal consequence is missing from its rationale.** Design:118 discusses raising the cap, but lowering it also lowers an absent count and can retire equipment automatically. Define that behavior and the effective bound: `Driver/Settings/DriverConfig.cs:552–553` permits cap 16 and above, while Design:501 permits counts only through 16 and :504 computes cap + 1.

### 4. Rows a to j

1. **Blocking — d enables reuse before e makes reuse safe for continuations.** Design:764–778 exposes the setting, reuses slots and ends branches in d, but postpones return, go-on and no-output regrowth until e. That landing can strand conversations or make them run detached; keep production allocation disabled until e and retirement/departure handling in f are complete.

2. **Non-blocking — b is not wholly inert for existing trees.** Design:728 adds `wwwroot` to the shared classification, which changes existing removal judgements through `Driver/Trees/SessionTrees/ContentHolds.cs:588–594`, even with pooling unwired. Preserve legacy classification or identify and test that policy change explicitly; “the person sees nothing” is inaccurate.

3. **Blocking — the crash and compatibility proofs omit the dangerous boundaries.** Design:742 tests stops at named phases, but must also test the folder moved before registration repair, switched before outcome recording, and record opened before claim completion. Design:783’s pool-off tests and “build before” rehearsal must additionally run a specifically pinned pre-pool implementation against concurrent take/return and interrupted return, rather than merely exercising newer code with pooling disabled.

4. **Blocking — g’s clock cannot measure the claimed startup saving.** Design:802 measures record-open to first turn, but today tree preparation precedes record opening (`Driver/Conversations/Chat/ChatRunner.cs:644–664`). Measure request/admission to first turn, including move, junction repair, switch and dependency work, and define the result that permits h’s default flip.

The ⚖ markings on b–j are appropriate. Row a can establish unused tables and the census, but it cannot settle the missing lifecycle protocol above merely through store races and pure judgements.

### 5. What is the owner’s

None. Design:841–864 correctly separates the count interpretation, installed opt-in, destructive optional rows and bounded account use. The preservation, compatibility and measurement defects above are design obligations, not additional permission questions.

**Verdict: another round.**

## Opus (foundation), round 1

The independent Opus 5.5 reader's foundation read of round 1 at `3f55eb1e5`: 15 findings, 4 blocking (F1, a return's destination neither
checked nor exclusive; F2, a move before the switch puts another line's branch at a line's path, which TREEFOLLOW1 records; F3, kept folders
naming the old absolute path, `.venv` and NuGet's props, read another line's working copy after a move; F4, nothing re-reads the slot after
the move and the switch); another round, scoped to §2.3 to §2.4, §5.2 to §5.3, §11, §14 and row b. It would keep the slot moved to its line's
path.

I read everything at `3f55eb1e5` with `git show` and `git grep`, and ran nothing. Repository paths use the survey's aliases. The design cites lines as of `f8fbb1bb7`. At `3f55eb1e5`, LIMITSWITCH3 (`17685fec8`) has shifted lines in `Loop/Driver.cs` (+5 after :979, +6 after :1994, up to +16 near the end) and in `LimitMove.cs` (:346/:350 are now :347/:351). Where this read cites Driver.cs it uses the `3f55eb1e5` numbers. For example, the design's `:2099` and `:2127-2131` are `:2105` and `:2133-2137` here.

### 0. The synthesis against both proposals

**What it took.**
- **From Codex (X):**
  - the rule that the work's identity is never reused, while the equipment is;
  - a non-forcing switch with `--no-overwrite-ignore`;
  - HEAD pinned and compared before a take, and an unfinished git operation counted as a hold;
  - a person's terminal and work no ref holds counted as holds;
  - "a kept output is no proof that this commit builds";
  - TREEGONE1's remake seeded by TREEDEPS1;
  - "a crash never jumps to free";
  - lowering retires the highest-numbered slots first;
  - the downgrade question taken seriously.
- **From the Opus reader (O):**
  - the claim held per step, after which the ledger holds the slot, and `daoris kept tree` as the lock while a slot is free;
  - the count in `driver.json` at cap + 1, read live;
  - a start past the count opens a tree as today, so nothing waits;
  - retirement by the set-aside's own move;
  - a no-output branch grown again;
  - the keep commit, deferred to row i;
  - `wwwroot` on the one list;
  - the census;
  - four corrections of the survey. §0.2 rows 1–4 all hold at the code: `SessionLedger.cs:320-324`, `:1604-1607`, `:1833`; `BranchTidying.cs:140-150`; `SessionLocks.cs:65`; `TreeLands.cs:208-218`, `:930-941`; `AutoLander.cs:209-222`.

**What it chose where they differ.** §1.1 rows 2–11 are argued fairly. Row 12 misreads X (finding 13).

**What it dropped.**
- X's service session family, durable lease, kept area for loose files, and waiting for a tree.
- O's `t-<n>` place, `branch_parts`, the `BranchOf(session)` reader, the new `/api/lines/record` route, and `reset --hard`/`clean -fd` before the owner's yes.
- **Silently: X's post-preparation verification (X-5 step 9).** That verify is the one guard against a write or an ignore-rule change riding the switch into the new line (finding 4).

**The one choice neither made: the slot moves to the path of the line that takes it.** Its motivation holds at the code:
- A shipped build resumes into a record's folder by `Directory.Exists` alone, for any path its home does not hold (`Driver.cs:2105`, `:2133-2137`). Words going on judge by the disk the same way (`DriverContinuations.cs:171-178`, `:210`), and so does a conversation's go-on (`ConversationTrees.cs:11`; `ChatRunner.cs:630`). So O's stable slot path (§0.2 row 5) really would put an older build's session on another line's branch.
- X's second session family really is the heavy alternative.
- Keeping each path to one line keeps `sessions.tree`, `WorkRecords.SessionsOfAsync` (`WorkRecords.cs:446-463`), `BranchOfTree` (`TreeFollows.cs:153-161`) and Claude Code's per-path conversation and trust correct without rewriting them.

I would keep the choice. But it carries costs that neither proposal had, because their slots never move, and the design states them too narrowly:
1. **Absolute paths in kept folders become live reads of another line's working copy, not stale caches.** After a move, the old path is soon occupied by another slot that the pool brings back (finding 3).
2. **Each take or return has a window in which a line's path shows another line's branch.** TREEFOLLOW1 records that branch (finding 2).
3. **Destinations are contended.** `git worktree move` into an existing directory nests the slot inside it (finding 1).
4. **Each take is a directory rename on Windows**, where an in-place switch needed none. Any handle in the tree (an editor, an indexer, a terminal's working directory, a git fsmonitor daemon) makes it `busy`.

Points 2–4 are mechanics that can be fixed. Point 1 needs a rule. With those fixes the choice stands.

### 1. Claims about the code

These hold:
- §0.2 rows 1–8.
- The §2–§5 citations I spot-checked:
  - `SessionLocks.cs:42-73`, `:101-134`
  - `TreeOpenings.cs:15-45`
  - `TreeReturns.cs:48-110`
  - `TreeStrays.cs:150-153`, `:181-201`
  - `TreeSetAside.cs:27-30`, `:70-135`, `:190-341`
  - `ContentHolds.cs:102-106`, `:379-384`, `:432-433`, `:456-475`, `:565-574`
  - `TreeLock.cs:50-63`; `HomeLease.cs:66-68`, `:165-173`; `DesktopStore.cs:107-129`
  - `Sessions.cs:89-93`, `:1147-1161`
  - `LinkedWorktree.cs:25-27`; `RefWatch.cs:238-239` (no other reader keys on a worktree's registration name)
  - `DriverConfig.cs:19-22`, `:71-75`, `:1383-1391`; `Planner.cs:192-209`, `:1105-1157`
  - `ReviewDesk.cs:179-190`; `Http/Program.cs:1439`; `note.json:133`
  - the page sites
- `SystemSpace` and `KeepKinds` are Workplace's, by FOUNDATION1 §14.2's round-4 change, so the `kept-trees` kind adds no upward edge.
- D212 records mod-i to mod-vi built and none after.

These are wrong:
- **§5.3 step 2 / §12: "`git worktree move` … one rename of the folder".** Where the destination exists as a directory, even an empty one, git moves the tree *into* it. git's `move_worktree` appends the source's basename when `is_directory(dst)` is true. Finding 1.
- **§2.3, crash at `moving`: "the slot is wherever git now lists it".** git renames first and writes `worktrees/<name>/gitdir` afterwards (`update_worktree_location`). A failed write leaves the folder at the new path while git lists the old one. `TreeReturns.cs:94-98` then force-removes that registration twice over. Finding 5.
- **§9 and row f: a retired slot's "`node_modules` going at the slim's hour" (`KeepKinds.cs:11-23`).** Only a folder with a manifest at `moved` is ever slimmed (`SlimManifest.cs:11-13`; `TreeSlims.cs:11`). A retired slot has no manifest, so its `node_modules` waits the full days. Finding 8.
- **§10: "No manifest is written: its inputs (…) are the store's".** None of `kept_trees`, `tree_claims` or `line_ends` holds grew-from, unit, hold and form, recovery ref, read moment, left names or whole. Those are composed at `TreeSweeps.cs:221-230`. The route is idempotent on `(workspace, repository, folder)` (`Http/Program.cs:1436-1439`), so a retry must also keep the first moment. Finding 7.
- **§11: "npm's `node_modules` holds no absolute path but its workspace junctions", and caches with absolute paths are merely "rebuilt".**
  - `.venv` is on the one list (`TreeScratch.cs:17`). Its console-script launchers and editable-install `.pth` files name the old absolute path.
  - NuGet's `obj/*.nuget.g.props` names `ProjectAssetsFile` absolutely, and `--no-restore` reads it as written.
  - Both read whatever now stands at the old path. Finding 3.
- **§7: words waiting hold "until the next look's go-on".** `ServiceClient.cs:2245` counts any record with `WordsWaiting`, including words held for the person's press (MSG1f) or an account's reset (LIMITSWITCH3). That can be indefinite. Finding 11.
- **§7: a standing move holds through "Course's `moves` row".** There is no `moves` table at `3f55eb1e5`: the store's steps 0–3 are at `DesktopStore.cs:72-130`. Standing moves are the boxes `.limit-move.json` and `.hand-over.json` (`LimitBox.cs:24`; `HandOver.cs:15`). Finding 11.
- **§3.2, *Show it again*: "a slot taken shows as a tidied tree does today".** `ReviewDesk.cs:226-231` refuses only a tree that is gone. Where another slot was brought back to the path, it serves that slot's build folder. Finding 12.
- **§3.3 and §5.1 choice 1: the returning line's own slot "goes on there; no git at all".** For a finished line ended in place (detached, branch deleted), `TreeReturns.cs:56-58` answers `Stands` whatever HEAD is. So the line goes on detached, in this build and in an older one. Today a landed line whose branch the tidy took grows a fresh tree (`Driver.cs:2119-2127`). Finding 9.
- **§2.3: a take's branch "is deleted at that commit, as an undone open's is (`SessionTrees.cs:411-420`)".** An undone open runs `git branch -D` with no commit check (`TreeOpenings.cs:108`). Finding 15.
- **§8 and §18 item 1: a tree past the count is one "today's tidy takes … once its work is done".** A no-output tree stays until the person's press (`TreeSetAside.cs:18-20`). Finding 15.
- **§1.1 row 12, on X.** X restores a missing owned branch create-only from its preserved unit and end refs (X §3, §10). It does not simply hold. Finding 13.

### 2. The design as stated

**The slot's move against every path-keyed reader (survey §6).** Stored records and the page hold, because a path never names two lines. They fail inside the take or return window:
- `TreeFollows.LearnTreesAsync` runs at the start of every tidy pass, before any hold (`AutoTidies.cs:188-190`), in every build. It records *any* non-layout branch, `daoris/` ones included, as the line's `GrownBranch.Now` (`TreeFollows.cs:117-119`).
- A return moves the slot to A's path while it is still on C's branch, then re-makes up to a million links, then switches (§5.3 steps 2–4). A concurrent look in that window writes A's `Now = daoris/s-C`.
- Then all of these follow it:
  - TREEGONE1 (`TreeReturns.cs:67`);
  - the return's own "its branch, or `GrownBranch.Now`";
  - the tidy's `MovedOnTo` (`TreeFollows.cs:132-139`; "its tree stays, on …", `TreeSweeps.cs:296`).
- A crash at `moved` keeps it so, and leaves C's branch checked out at A's path, so C's own return is refused.

This is O's hazard reintroduced (finding 2).

**An older build on the same home.**
- **Holds:** its tidy refuses a locked slot (`TreeSetAside.cs:84-88`); its press refuses one (`TreeRemovals.cs:221` forces only once); its let-go lifts session locks only (`SessionLocks.cs:129`); its strays leave a listed tree (`TreeStrays.cs:150-153`); its landing lands from the line's own slot; its resume of a taken line remakes the tree at its path by TREEGONE1.
- **Fails:**
  - It goes on detached in a finished line's slot (finding 9).
  - Its strays read a *return* killed under OPENCUT1's lock as a cut-off open. They delete the returning line's pre-existing branch where another ref holds its tip, and set the slot aside (`TreeStrays.cs:183-201`, `:227-238`). TREELOCK1 rejected keeping OPENCUT1's lock through a session for exactly this reason (D51:138-139). Finding 6.
  - Run beside a pool-aware build, its resume checks `Directory.Exists` outside any hold (`Driver.cs:2133-2137`, before the hold at `:2184`), and a conversation's go-on in its own tree takes no hold at all (`ChatRunner.cs:630-637`). Either can race a take that holds the window shared. §14's "what is left" names only the remake race (finding 14).
  - It ends a session in a slot leaving HEAD moved and the lock unchanged (`SessionLocks.cs:65`), so §5.2 step 6 holds that slot until the line returns (finding 10).

**Claude Code's conversation and trust by path.** These hold: a line only ever runs at its own path, `ToolHomeLeftovers.cs:30-31`/`:55` and `ClaudeTrust.cs:108-125` key by path, and a new line's path is new, as today. What is unverified (whether Claude Code reads more of a directory than its path) is correctly given to row e.

**A crash at every phase.**
- `judged` holds.
- `moving` is mis-stated (finding 5).
- `moved` leaves the previous line's branch at the new path and lets TREEFOLLOW1 record it (finding 2).
- `switched` holds for a take (OPENCUT1's undo of a fresh branch), but not for a return (finding 6).
- The link re-make has no phase of its own. A slot stopped halfway carries links into an older path, which a later take reads as "leading anywhere else", so the slot sticks for good (finding 5).
- `end` and `retire` claims have no phases or takeover (finding 10).

**Two processes racing a take.** The per-slot key keeps two claims off one slot. Nothing keeps two claims off one *destination*:
- two returns of one line (say a resume and an Accept, in the shell and a `--share` loop);
- or a take of line A's slot racing A's return.

Both choose different slots and both move into A's path. The second nests (finding 1).

**A held file or a junction on Windows during a move or a switch.**
- A refused rename reads `busy`, as the set-aside's does (`TreeSetAside.cs:196-197`, `:313-333`), and git's stdin must stay closed.
- Junctions move as entries and are re-made.
- A write landing between the in-claim re-judgement and the switch rides `git switch`, which carries non-conflicting local edits, into the new line. The set-aside re-reads the tree after its move for exactly this (`TreeSetAside.cs:243-249`); §5.3 does not (finding 4).

**Absolute paths in `obj/` and the web build's caches.** As §11 frames it, "rebuilt" is a cost. `.venv` and `--no-restore` are correctness failures, unique to moving slots (finding 3).

**A slot judged free that holds work no ref holds.**
- §5.2's checks are sound at the source path, against the source's ignore rules.
- After the switch, a path ignored on A's branch but not at B's start is untracked in B, and B's `git add -A` commits it. `--no-overwrite-ignore` covers only the reverse case (finding 4).
- Stash and branch refs are shared, so nothing is lost there.

**What holds a slot, and what frees it.** The union is right. But:
- the terminal is known only to the shell's process (`Modules/Windows/TerminalModule.cs`; there is no record), so a `--share` loop or a verb cannot read it;
- the standing move names a table that does not exist yet;
- the words-waiting duration is understated (finding 11).

**The count past which a start opens a tree as today.** It holds and nothing waits. The default `cap + 1` can exceed the stated range of 1–16 (finding 15).

**The end in place and KEEPSIZE1b's record.** The steps are right. The pending post has no home in the store (finding 7).

**A line coming back whose slot another line holds.** Bringing a free slot back is the right move. It needs the destination guarded (finding 1), the switch staged so it happens before the folder takes the line's name (finding 2), and a finished line handled separately (finding 9).

**FOUNDATION1's map.**
- There is no upward edge:
  - Course's `ConversationTrees`, `Continuation.Judge` and the moves call down into Workplace;
  - the chat runner is Loop's;
  - the holders come in as delegates that Loop composes;
  - `SystemSpace` is Workplace's;
  - Settings holds a map of ints.
- The unnamed dependency is Part B's `moves`, and every holder must be readable from every process that takes a slot (finding 11).

### 3. The rows a to j, the ⚖ rows, the owner's questions and the order

**Row a** (not ⚖) is right as a shape row.

**Row b** (⚖) proves the take, but lacks these cases:
- a destination that exists, and two returns of one line racing (finding 1);
- a look running between move and switch, and a crash at `moved` for a *return* (finding 2);
- a `.venv` with an editable install, and `--no-restore`, both in a moved slot (finding 3);
- a write between the re-judgement and the switch, and a `.gitignore` that differs between source and target (finding 4);
- a failed `gitdir` write, and a stop mid-link-walk (finding 5);
- a return killed and read by each build's strays (finding 6).

Row b's measurement decides whether §1.1 row 1's trade stands (§20). So rows d and later should wait on its note being read, not merely on rows a–c being built.

**Row c** (⚖, inert until d) is fine.

**Row d** (⚖) is the first row that writes what an older build meets: locked slots and detached, ended-in-place slots. The deployment rehearsal belongs here too, not only in row e (finding 14).

**Row e** (⚖) is fine, and its installed proof is bounded under §18 item 5.

**Row f** (⚖): its proof asserts the slim's hour, which will not happen (finding 8).

**Rows g to j** are correctly gated on the owner's word.

The ⚖ marking is right: every row that switches, removes or re-keys a read is marked.

**The owner's questions (§18).** All five are well formed, each saying what it is, what a yes does and where to look. Item 1 misstates what today's tidy does with a no-output tree (finding 15). Nothing else needs the owner.

**The order.** Every row comes after mod-x, which has not landed (D212). Two more dependencies need naming:
- Part B's `moves` for the standing-move holder, or today's boxes until then;
- DEV6 for `laneCap`, which the design already names.

### 4. Findings

1. **Blocking. A return's destination is neither checked nor exclusive.**
   - **Where:** design §2.3 (claim key `(workspace, repository, number)`), §5.3 step 2; git's `move_worktree` (`is_directory(dst)` appends the source's basename); `TreeReturns.cs:87-92` (TREEGONE1 moves a leftover folder aside for this very case).
   - **What goes wrong:** a leftover folder, or a second return of the same line (or a take of its slot) in another process, nests the slot inside the line's path. The session then runs in a directory no reader knows.
   - **Smallest change:**
     - A claim carries `to`, unique among the repository's in-flight claims, and a return refuses while the line's own slot is under another claim.
     - Inside the claim, the destination must not exist. A folder left there is moved aside first, as TREEGONE1 does.
     - After the move, git must list the slot at exactly `to`.
     - Optionally, run the move and switch under the exclusive hold (`TreeLock.TryReplaying`, as `SessionLocks.cs:111` and `TreeStrays.cs:115` take it).
     - Row b proves both cases.

2. **Blocking. Moving before switching puts another line's branch at a line's path, and TREEFOLLOW1 records it.**
   - **Where:** design §5.3 steps 2–4, §2.3 `moved`; `TreeFollows.cs:91-126` (learns any branch, `:117-119`; skips only a detached HEAD, `:116`); `AutoTidies.cs:188-190`; `TreeReturns.cs:56-58`, `:67`; `TreeFollows.cs:132-139`.
   - **What goes wrong:** this contradicts §3.1, the rule the design rests on.
   - **Smallest change:**
     - Detach in place before any move (`git switch --detach` writes no file).
     - A return reads its branch before the move.
     - A return is staged under a folder name no reader keys, switched and re-linked there, and renamed into the line's path last.
     - The takeover detaches at every phase.
     - Restate the crash table in §2.3.

3. **Blocking. Kept folders that name the old absolute path read another line's working copy after a move.**
   - **Where:** design §11; `TreeScratch.cs:17` (`.venv` is on the list).
   - **What goes wrong:**
     - `.venv` launchers and editable-install `.pth` files point to the old path, which the pool hands to other slots: a line's tests silently run another line's code.
     - `obj/*.nuget.g.props` does the same under `--no-restore`.
   - **Smallest change:**
     - Name which list folders do not survive a move. `.venv` at least is never carried across one: it is set aside within the slot, deleted never following a link, and the agent is told to rebuild it.
     - Restate §11's "no absolute path" claim.
     - Row b measures a venv with an editable install and a `--no-restore` build before row d.

4. **Blocking. Nothing re-reads the slot after the move and the switch.**
   - **Where:** design §5.2 step 5, §5.3; the precedent `TreeSetAside.cs:243-249`; X-5 step 9, dropped without a word.
   - **What goes wrong:**
     - A write after the in-claim judgement rides `git switch`, which carries local edits, into the new line.
     - A path ignored at the source but not at the target becomes untracked, and the new agent's `git add -A` commits it.
   - **Smallest change:** after the switch, `status --porcelain -z --untracked-files=all` must be empty, and the HEAD must be the target. Otherwise switch back detached to the recorded tip and hold the slot `stuck`, never opening the record. Row b proves both cases.

5. **Not blocking. The `moving` crash recovery is mis-stated, and the link re-make has no phase.**
   - **Where:** design §2.3; `TreeReturns.cs:94-98`.
   - **What goes wrong:** where the folder is at `to` and git lists `from`, the slot is recorded at a path with no folder, and TREEGONE1 force-removes its registration. A slot stopped mid-walk sticks for good.
   - **Smallest change:** in that state, run `git worktree repair <to>`. Add a `relinking` phase, or re-point links into any path the slot held before. Prove it with a held `gitdir` file.

6. **Not blocking. OPENCUT1's lock during a return lets any build's strays delete the returning line's own branch.**
   - **Where:** design §2.4 ("during a take or a return"); `TreeStrays.cs:183-201`, `:227-238`; D51:138-139.
   - **What goes wrong:** where another ref holds the line's tip, the strays delete its branch and set the slot aside.
   - **Smallest change:** a return holds the pool's own reason, which its claim's run names, and is never read as an opening's. OPENCUT1's lock stays for a take's new branch only.

7. **Not blocking. The end in place has no store home for its pending record.**
   - **Where:** design §10; `TreeSweeps.cs:221-230`; `Http/Program.cs:1436-1439`.
   - **Smallest change:** add a table, e.g. `line_records`: the post's fields, including `Folder` with its first moment, deleted on the service's acknowledgement. Say what `Left` and `Whole` mean when the scratch stays in the slot.

8. **Not blocking. A retired slot is not slimmed.**
   - **Where:** design §9, row f; `SlimManifest.cs:11-13`.
   - **Smallest change:** write a slim manifest at retirement (`TreeSlims.JudgeAsync`, as the set-aside does), or drop the clause and row f's proof of the slim's hour.

9. **Not blocking. A finished line that comes back goes on detached.**
   - **Where:** design §3.3, §5.1 choice 1, §14; `TreeReturns.cs:58`; `Driver.cs:2119-2127`.
   - **Smallest change:** add a row to §3.3. A finished line's own slot (ended in place) is read as today's gone or landed branch: a no-output line regrows its branch, and a landed one takes today's fresh tree from its landing. Never go on detached. §14 says what an older build does.

10. **Not blocking. Who runs "the end" is underspecified.**
    - **Where:** design §2.3, §6; `SessionLocks.cs:65`.
    - **What goes wrong:**
      - a park ended in another process, or a dead `end` claim, leaves no `line_ends` row, so step 6 holds the slot;
      - an older build's session leaves HEAD moved and the slot held;
      - `end` and `retire` claims have no phases;
      - a slot an older build's let-go left unlocked matches none of step 2's lock cases.
    - **Smallest change:** the look runs the end, idempotently, for any slot with no holder and no `line_ends` row since its last take. Define the phases of `end` and `retire`. Step 2 accepts an unlocked slot and locks it again.

11. **Not blocking. Three holders cannot be read the way §7 says.**
    - **Where:** design §7, §2.5; `TerminalModule.cs` (in the shell's process only); `DesktopStore.cs:72-130` with `LimitBox.cs:24` and `HandOver.cs:15`; `ServiceClient.cs:2245`.
    - **Smallest change:**
      - Say that the terminal is the shell's alone, with `busy` as the cross-process backstop, or have the shell record a terminal's working folder in the store.
      - Read the standing move from today's boxes until Part B's row f, and name that dependency.
      - Correct how long waiting words hold a slot.

12. **Not blocking. *Show it again* after a bring-back serves another line's build.**
    - **Where:** design §3.2; `ReviewDesk.cs:193-197`, `:226-231`.
    - **Smallest change:** refuse where the slot at the path was taken or brought back since the set-up showed its build. Compare against a take count recorded with the served record.

13. **Not blocking. §1.1 row 12 misreads X.**
    - **Where:** Codex's proposal §3 and §10.
    - **Smallest change:** say that X restores the branch from its preserved refs; the difference is where the licence to regrow lives.

14. **Not blocking. The race between two builds is unnamed, and the rehearsal comes a row late.**
    - **Where:** design §14 "what is left", rows d and e; `Driver.cs:2133-2137`, `:2184`; `ChatRunner.cs:630-637`.
    - **Smallest change:** name the race between an older build's resume or go-on and a pool take of that line's free slot. Run the deployment rehearsal (an older build over a pooled home: a resume, a tidy, a press, and a finished slot) from row d.

15. **Not blocking. Nits.**
    - Cite `3f55eb1e5`, or note LIMITSWITCH3's line shifts.
    - Clamp the default to `min(cap + 1, 16)`.
    - `started.tree-kept`'s "its files are the line's" is wrong for a chain step's `from` or a part's `partOf`.
    - An undone open deletes with `git branch -D` (`TreeOpenings.cs:108`); the take must check the commit itself.
    - Trees past the count with no output stay until the press (`TreeSetAside.cs:18-20`), in §8 and §18 item 1.
    - A repository that tracks a submodule can never move a slot, since git refuses, so it never pools; say so in §12.

**15 findings, 4 of them blocking.**

**Verdict: another round.** The central choice of moving the slot to the line's path holds at the code, and I would keep it. But all four blocking findings sit in its own mechanics: the claim's key, the order of move and switch, what may cross a move, and the re-read after the switch. They reshape §2.3's crash table and row b's proofs, which both makers are asked to weigh first.

Scope the round to §2.3–§2.4, §5.2–§5.3, §11, §14 and row b. The rest can carry over with the non-blocking changes above.

## Codex, round 2

Codex's read of round 2 at `d595cabd6`: another round. Its §0 checks its round-1 findings; it then finds two wrong claims, not blocking (the last guard's lock reading, and carried outputs that run), six on the design blocking (a placed slot's links across a crash, the record's open identified before it opens, a dead executor taken for quiet git children, a finish's facts captured before its branch goes, a link's owner, and row i's working snapshot), the served build's admission dropped (blocking), the rows sound with e's installed proof too early, and the build folders a move drops put to the owner as a deletion (blocking).

Reviewed HEAD `d595cabd6`, read-only. No files changed or tests run; no checkout-relative `local/` content or files outside this checkout inspected. `Design` below is `docs/2026-10-26-tree-pool-design.md`; code paths use the inputs’ aliases.

### 0. Your round-1 findings

| Finding | Assessment | Basis in round 2 |
|---|---|---|
| 1.1 Interrupted move is not an interrupted opening | **Taken** | Parking and the claim’s distinct lock replace that assumption: Design:273, :312. |
| 1.2 Last guards do not establish link ownership | **Open** | The whole walk is added, but location is still treated as provenance: Design:496. |
| 1.3 Manifest-free retirement gets no slim | **Taken** | Retirement writes the slim’s manifest: Design:705. |
| 1.4 Other path-dependent dependency contents | **Taken** | The named caches are dropped: Design:769. Broader relocation proof remains necessary. |
| 2.1 Another branch temporarily occupies the returning path | **Taken** | Switching now precedes placement: Design:543, :556. The junction crash window below remains. |
| 2.2 Durable intent, outcomes and surviving git children | **Open** | More intent is stored, but open reconciliation and positive child quiescence remain incomplete: Design:246, :283, :295. |
| 2.3 Finished-record inputs and retry identity | **Open** | The outbox exists, but complete finish inputs are still composed after deletion: Design:733–739. |
| 2.4 Check the destination after switching | **Taken** | Both checks and conservative failure handling are specified: Design:550, :564. |
| 2.5 Terminal admission | **Taken** | Cross-process admission precedes launch and lasts through the terminal job: Design:247, :650. |
| 2.6 Preserve distinct staged and working states | **Open** | Two snapshots help, but the working snapshot still misses force-added ignored files: Design:597. |
| 2.7 Detached no-output continuation | **Taken** | No-output slots retain their branch; finished slots leave the path: Design:630, :729. |
| 2.8 Relocation needs correctness proof | **Taken** | Old-path sentinels and read/write isolation are now required: Design:790. |
| 3.1 Occupation-lease rejection overstated | **Taken** | Economy replaces impossibility; reservation and final verification return: Design:165. |
| 3.2 Live default’s lowering consequence | **Taken** | Clamp, retirement and disclosure are explicit: Design:677–682. |
| 4.1 Production reuse enabled too early | **Taken** | Count doors wait until f: Design:959. |
| 4.2 `wwwroot` changes legacy removal policy | **Taken** | Legacy removal classification remains unchanged: Design:381. |
| 4.3 Dangerous crash boundaries and older-build proof | **Taken** | Boundary tests and a pinned pre-pool build are named: Design:898, :1000. |
| 4.4 Startup clock and default-flip criteria | **Taken** | Admission-to-first-turn and explicit criteria replace the earlier measurement: Design:1079–1087. |

### 1. Wrong claims about the existing code

1. **Non-blocking — the existing last guard rejects every lock.** Design:493 describes calling `LastLookAsync` first, but `Driver/Trees/SessionTrees/TreeSetAside.cs:84–87` rejects a pool’s free or claimed lock too. Specify a pool-aware guard accepting only the exact owned reason, with unknown locks still refusing; otherwise every ordinary take fails its own judgement.

2. **Non-blocking — carried outputs are executable.** Design:771 calls `dist` and its companions outputs “no tool resolves code from,” but `src/Daoris.Cli/bin/daoris.mjs:12–15` prefers retained `dist/cli.js`, and `tools/desktop-publish.mjs:1261` explicitly records this stale-output hazard. Withdraw that claim and include the normal CLI entry in row b’s retained-output proof.

### 2. The design

1. **Blocking — placement still exposes unsafe junctions across a crash.** Design:556 places the slot before :560 repairs its links, and :889–892 explicitly permits an older build to resume through those old targets; `Driver/Trees/SessionTrees/TreeReturns.cs:58` accepts the listed tree without checking them. Repair links to their final targets before publishing the directory, or provide exclusion already honored by shipped builds; a later takeover cannot undo another line’s reads or writes.

2. **Blocking — the record-open outcome is not durably identifiable before opening.** `tree_claims.record` stays empty until the answer arrives (Design:246, :567), while takeover recognizes an unrecorded open only through an **active** record (Design:295). An open whose answer is lost, followed by orphan settlement before takeover, falls into the rollback case; persist a keyed open identity beforehand and reconcile its historical outcome independently of current liveness, following FOUNDATION1:2013–2021.

3. **Blocking — executor death is still substituted for child quiescence.** Design:283 assumes the Windows job has ended its git processes and accepts lock-file absence elsewhere, but `Driver/Platform/ProcessJob.cs:18` supplies no containment elsewhere; its strict recovery primitive explicitly terminates and waits for emptiness. Record recoverable containment identity and establish emptiness before takeover, or disable pooling wherever that proof is unavailable; an instant without `index.lock` or `HEAD.lock` does not prove a surviving child has finished.

4. **Blocking — finish facts must survive before branch deletion.** Design:733 deletes first and :735 captures provenance, landing and hold facts afterwards; `line_ends` contains only part of those inputs. Today `Driver/Trees/SessionTrees/TreeSweeps.cs:221–230` snapshots them **before** removal, and `Driver/Trees/Branches/SessionBranches.cs:180–202` can subsequently forget their source. Persist a complete finish snapshot and fixed identity before deletion, and specify a branch-keyed finish claim for a line whose former slot now serves somebody else.

5. **Blocking — link location does not establish ownership.** Design:496–499 admits any internal link under an eligible `node_modules`, including one the person created; `Driver/Trees/SessionTrees/TreeDependencies.cs:279–285` establishes target containment, not authorship. Record the links Daoris creates or prove them against the package’s workspace declarations, and hold unexplained links rather than retargeting them.

6. **Blocking, row i — the working snapshot still loses a valid working version.** At Design:597–598, force-add an ignored file with content X, then edit it to Y: `I` preserves X, but a private index seeded from HEAD makes `add -A` ignore Y. The subsequent reset can remove Y with no snapshot holding it; seed the working snapshot from the real index’s tracked set and prove this case alongside staged deletions and interrupted restoration.

7. **Non-blocking — the count and module placement are sound, but one opening sentence overclaims.** The count interpretation is explicitly the owner’s, overflow preserves today’s admission, and Design:325–337 introduces no upward edge. However, `started.tree-past-kept` at Design:919 also occurs when slots are stuck or retiring, so say that no reusable kept tree was available rather than that every tree was in use.

### 3. Where it chose between the proposals

1. **Non-blocking — the central choice remains reasonable, conditionally.** Preserving each line’s path retains the existing path groupers and Claude trust lookup (`Driver/Accounts/SignIns/ClaudeTrust.cs:114–120`) while avoiding the second session family. The step claim, existing record route, Settings-owned count and conservative first release have sound reasons; row b’s measurement gate correctly leaves the Windows rename and cold-build cost unresolved before activation.

2. **Blocking — X’s served-build admission was dropped.** X explicitly required registration before serving, but Design:654 supplies only a hold until verdict and :398 adds a historical timestamp check. `Driver/Review/ReviewDesk.cs:226–236` permits *Show it again* without acquiring equipment, while a headless allocator’s `Driver/Trees/Branches/BranchTidying.cs:736–741` sees neither another shell’s serving table nor an answered setup as waiting; it can move that served tree. Restore cross-process admission lasting until serving stops, through a downward Workplace seam.

### 4. The rows a to j

1. **Non-blocking — a–c have appropriate boundaries; b’s proof needs the counterexamples above.** Row a establishes unused shapes, b implements the unwired lifecycle, and c moves the identified readers after mod-x, with legacy behavior explicitly preserved (Design:963–1029). Add the force-added ignored file, ordinary CLI entry, served-build admission race, and complete pre-delete snapshot to their respective proofs.

2. **Non-blocking — d–f fix activation order, but e’s installed proof comes too early.** Design:959 keeps production activation unavailable until f, yet Design:1058 requires a real installed pooled session as e’s proof. Move that installed proof after f and before g’s measurement; keep e’s landing proof confined to its test seam.

3. **Non-blocking — g–j have suitable gates, with one default-path omission.** The admission clock and h’s criteria are now explicit, i and j remain separate owner-gated changes, and the ⚖ markings on b–j are appropriate (Design:1073–1115). But h enables repositories without passing the count-setting door, so submodule refusal must also run on effective default activation and subsequent takes, with a default-on submodule fixture—not only row f’s door test.

### 5. What is the owner’s

1. **Blocking — the newly destructive relocation policy needs an explicit direction decision.** Design:779 treats deleting `.venv`, `obj`, `target` and other named folders as already authorized, but `docs/decisions/D195.md:23–32` authorizes only narrowly judged package folders early and expressly leaves `obj` for later evidence. The brief promises ignored dependency/build folders retained; either preserve these folders without exposing them to the next line, or put their expanded deletion policy to the owner with its concrete scope. The existing count, install opt-in, optional preservation, scratch and bounded-account questions are otherwise correctly assigned.

**Verdict: another round.**

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `d595cabd6`: 15 findings, 1 blocking (F1, `build`, `out` and `bin` carried across a move without proof, each able to read the old path); accept after named changes the steward can check without another read, F2, F3, F4, F5, F7 and F11 before row b. It finds no case in which the pool loses work, and every round-1 finding taken, O1-F3 with C1-2.8 open in part.

Read at `d595cabd6` with `git show` and `git grep`. Nothing was run. Paths use the survey's aliases. FOUNDATION1 row p merged at `600a03056`, after the `f92e95415` that round 2 cites, so some service lines have moved (finding 12).

### 0. Both reads of round 1

**Codex, round 1**

| Finding | Round 2 | Verdict |
|---|---|---|
| 1.1 (blocking) an interrupted move is no interrupted opening | §5.3 park first, then switch; the pool's own lock reason; OPENCUT1 only at first making | Taken. It holds: `TreeStrays.cs:183`, `:188-190` never read `k-` or the pool's reason as an opening |
| 1.2 (blocking) the last guards do not judge every link or repository | §5.2 step 3, a whole walk | Taken. But a finished line's nested repository is *carried*, not let go (finding 6) |
| 1.3 a retirement without a manifest is never slimmed | §9 writes a schema-1 manifest | Taken. It holds (`SlimManifest.cs:13-14`, `:17-20`) |
| 1.4 caches in `node_modules` | §11 drops `.cache`, `.vite`, `.vite-temp` | Taken (`TreeDependencies.cs:45`) |
| 2.1 (blocking) the path invariant during a return | Park, switch, check, place, all under the exclusive hold | Taken. It holds for starts and the tidy (`Driver.cs:2110-2111`; `TreeStrays.cs:115`). A landing does not take that hold (finding 2) |
| 2.2 (blocking) durable intent; git child processes | Phases, a takeover that reads the disk, a job | Taken. A refusal at placement is not specified (finding 3). A parked slot has no recorded tip (finding 4) |
| 2.3 (blocking) the finished record | `line_ends` and `line_records` | Taken. `Left` is attributed wrongly (finding 6) |
| 2.4 (blocking) a clean source is no clean destination | Checks at §5.3 steps 7 and 11 | Taken. It holds |
| 2.5 (blocking) a terminal needs admission | `tree_visits` | Taken. It now also holds back the line's own claim in place (finding 7) |
| 2.6 (blocking, row i) the index and the working tree | Separate `I` and `W` trees | Taken. It holds |
| 2.7 (blocking) a detached no-output slot | The no-output slot stays on its branch; a finished slot is parked | Taken. It holds. §10's opening contradicts it (finding 5) |
| 2.8 isolation as well as timing | The sentinel proof in row b | Taken in part. The proof covers only builds that rewrite their outputs (finding 1) |
| 3.1 the lease rejection is overstated | Restated as economy; the reservation kept | Taken |
| 3.2 the live default's consequences | The clamp; a lowered cap retires slots | Taken. The cap verb's sentence needs an upward edge (finding 8) |
| 4.1 (blocking) row d before row e | No door before row f | Taken. Row d's rehearsal needs a seam that real processes read (finding 13) |
| 4.2 `wwwroot` changes removals | `TreeScratch` kept byte for byte | Taken |
| 4.3 (blocking) the crash and compatibility proofs | Every boundary stopped; a pinned pre-pool build | Taken. How the pinned build is obtained is not said (finding 13) |
| 4.4 (blocking) row g's clock | Measured from admission; what permits row h | Taken |
| Closing note on row a | Row a settles shapes only | Taken |
| §5 nothing more for the owner | — | Agreed |

**Opus (foundation), round 1**

| Finding | Round 2 | Verdict |
|---|---|---|
| F1 (blocking) the destination | A unique `to`; the destination must not exist; git must list the slot at exactly `to` | Taken. It holds |
| F2 (blocking) move before switch | Rename to the park first, detach there, place last | Taken. It holds. While the slot is parked on A's branch, a TREEGONE1 for A is refused (`TreeReturns.cs:81-85`) |
| F3 (blocking) folders that name the old path | `.venv`, `obj` and caches dropped | Taken in part. It is **open** for `build`, `out` and `bin` (finding 1) |
| F4 (blocking) no re-read after the switch | Steps 7 and 11 | Taken |
| F5 the crash at `moving`; the relink's phase | `worktree repair`; the `relinked` phase; `before` | Taken |
| F6 OPENCUT1 during a return | The pool's own reason | Taken |
| F7 the pending record | `line_records`; `Left` and `Whole` said | Taken (see finding 6) |
| F8 the slim at retirement | The slim's manifest | Taken |
| F9 a finished line goes on detached | Parked at the finish | Taken |
| F10 who runs the end | The look, idempotently; phases | Taken |
| F11 three holders | A visit; the boxes; how long words wait | Taken |
| F12 *Show it again* | `kept_trees.since` | Taken |
| F13 row 12 misreads X | Restated | Taken |
| F14 the race between builds | Named and bounded; rehearsal from row d | Taken (see findings 13 and 15e) |
| F15 nits | Each taken | Taken |
| §3 rows d and later wait on row b's note | Yes | Taken |

None was wrong, and none was answered rather than taken. One is still open in part: O1-F3 with C1-2.8.

### 1. Claims about the code

- **Stale lines at `d595cabd6`.** Row p shifted these:
  - `Core/Sessions/SessionLedger.cs`: `:320-324` is now `:328-332`, `:1602-1616` is `:1625-1639`, `:1604-1607` is `:1627-1630`, and `:1833` is `:1856`.
  - `Core/Sessions/Sessions.cs`: `:1147-1161` is now `:1189-1203`.
  - `Service/ServiceClient.cs`: `:2245` is now `:2265`, and `:2222-2249` is `:2242-2269`.
  - `Http/Program.cs`: `:1435-1439` is now `:1442-1446`, and the route at `:1439` is `:1446`.

  *Read at* says every other file is unchanged since `f8fbb1bb7`. That is no longer true (finding 12).
- **§11 says `bin`, `dist`, `build`, `out`, `coverage`, `wwwroot` and `__pycache__` are "outputs no tool resolves code from".** This is wrong for `build` and `out`: CMake's build trees, including VS's `out/build/<preset>`, name the source and build directories absolutely. It is also wrong for .NET `bin` in this repository. `Daoris.Service.Http` is a Web SDK project, and its tests use `Microsoft.AspNetCore.Mvc.Testing` (`Daoris.Service.Http.Tests.csproj:16`), whose content-root attribute is compiled into the test assembly in `bin`. That is how the reader understands these SDKs; it was not run here (finding 1).
- **§0.3 and §2.5 say no port is added.** But the cap's verb, which must name "how many retire" (§8), and the count's door, which must refuse "a repository with submodules", are both in `Settings/DriverChoicesCommand.cs:427-436`. That is Settings, rank 3, and it cannot read Workplace's `kept_trees` (finding 8).
- **§14 says "D139's rollback cannot" reach pooled lines, "since pooled lines exist only once a build that knows them has confirmed".** Nothing gates a take on the start's confirmation. Once row h flips the default, a build's first look pools before it confirms (finding 15e).
- **§2.3 says every rename's exclusive hold is honoured "by every build's start and tidy".** That is true. But a landing honours neither the rename's hold nor any holder in §7: it takes `LandingHolds` (`TreeLands.cs:420`) and the in-process `RepositoryHolds` (`:435`), not `TreeLock` (finding 2).

Everything else that round 2 adds or re-cites holds at `d595cabd6`. I checked these:
- `TreeLock.cs:50-63`
- `Driver.cs:2105-2137`, `:2184-2185`, `:2192-2221`, `:2902-2910`
- `ChatRunner.cs:630-651`
- `TreeStrays.cs:70`, `:77`, `:115`, `:147-160`, `:181-201`, `:227-245`
- `TreeFollows.cs:91-126`, `:132-161`
- `SessionLocks.cs:28`, `:42-73`, `:88-134`
- `TreeOpenings.cs:15-45`, `:108`
- `TreeReturns.cs:48-110`
- `TreeSetAside.cs:18-36`, `:70-135`, `:190-270`, `:313-341`
- `SlimManifest.cs:11-29`; `SetAsideKeeping.cs:87-90`; `TreeSlims.cs:3-24`
- `TreeScratch.cs:15-19`; `TreeDependencies.cs:24-45`
- `ContentHolds.cs:102-106`, `:379-384`, `:432-433`, `:456-475`, `:565-574`, `:588-594`
- `TreeSweeps.cs:191-230`
- `HomeLease.cs:66-68`, `:165-173`; `ProcessJob.cs:15-26`
- `TerminalModule.cs:202-218`; `LimitBox.cs:24`; `HandOver.cs:15`
- `DesktopStore.cs:73-129`; `DriverConfig.cs:71-75`, `:549-553`
- `ReviewDesk.cs:226-229`; `TreeRemovals.cs:220-222`; `AutoTidies.cs:190`; `TreeContext.cs:40`
- the foundation design's `:1970-2001`, `:1987`, `:2042-2066`, `:2342`
- every suite §17 names exists.

### 2. The design as round 2 states it

**A claim's destination; two processes racing a take or a return.**
- These hold:
  - The primary key keeps two claims off one slot, and the unique index on `to` keeps two claims off one destination.
  - A return waits while its line's own slot is under another claim, and a take passes over a slot whose line has a claim naming its path.
  - A dead `placed` claim still occupies `to`, so a second return of that line is refused by the index.
- Two gaps:
  - **An Accept in the line's own free slot takes no claim (§3.3) and is no holder (§7, §5.2 step 1).** So a choice-3 take, whose candidates are exactly unlanded lines, can rename the slot away between the landing's git steps (finding 2).
  - **A visit refuses every claim (§2.3), including choice 1's in-place claim.** So a terminal open in A's own tree holds A's own resume, which today goes on beside it. The choice then falls to a parked slot, whose switch to A's branch is refused (A's branch is checked out in A's slot). Each free slot is marked `stuck` in turn before today's remake runs (finding 7).

**The order of detach, move, switch and rename.**
- This holds. Rename-then-detach leaves A's path empty while A's branch is at `k-<n>`, and both builds' TREEGONE1 refuse then (`TreeReturns.cs:81-85`).
- Detaching while the hold is still held would close even that window; it costs nothing.
- `git switch` runs `post-checkout` at the park. A hook that installs or records its directory writes output tied to the park's path. The checks at steps 7 and 11 turn that into `stuck`, so such a repository sticks every slot it takes (finding 9).

**A refusal mid-protocol.** Step 2's refused rename is specified. Step 8's is not, nor is step 11's rename back. In that state B's new branch, or R's branch, is checked out at the park:
- B's retry on another slot fails `switch -c`, because the branch exists.
- R's retry fails, because the branch is checked out at `k-<n>`.
- TREEGONE1 refuses R's remake (`:81-85`).

R cannot come back until a person acts (finding 3).

**A crash at every phase.**
- `judged` and `parking`: the claim is dropped. This holds.
- A rename that outran its `gitdir` write: repaired. If an older build's TREEGONE1 reaches it first, it force-removes the registration (`TreeReturns.cs:94-98`), and the folder becomes the pool's stray. Nothing is lost.
- `parked` to `checked-park`: detach, then `update-ref -d <start>`. This holds. Two gaps:
  - **The drop and the npm replacement leave an untracked folder if a crash interrupts the delete**, and that folder holds the slot until the person presses twice (finding 15a).
  - **Step 6 and §5.4 compare a parked slot's HEAD with "its recorded tip", but no column records it.** `kept_trees` has no tip, `line` is `''` when parked, `line_ends` has no slot number, and the claim's `expect` dies with the claim (finding 4).
- `placing` to `checked`: the takeover moves the slot back to the park. But between the crash and the takeover, nothing says a pool-aware start of that line is *held* rather than falling through to `EnsureAsync`. `EnsureAsync` answers *stands* (`TreeReturns.cs:58`) for an unchecked slot whose links may still lead into the former path, which another line's tree may now occupy (finding 11).
- `opened`: the ledger holds the slot. This holds.
- `finish`, `end` and `retire`: each resumes at its phase. This holds.
- Choice 4's first making (today's open under OPENCUT1's lock, which `worktree move` refuses) has no row in the phase table (finding 15b).

**What may cross a move.**
- `.venv`, `obj` (NuGet's props), `target`, `.angular`, `.next`, `.gradle` and the top-level `node_modules` caches are dropped. This holds.
- **`build` and `out` are carried without proof.** A CMake or Ninja tree moved with the slot compiles the sources at the old path, which is another line's tree within the hour, and regenerates into it.
- **`bin` is carried.** With `obj` dropped, every build rebuilds `bin` anyway, so it saves little. But a run without a rebuild reads the old content root, e.g. `dotnet test --no-build` through Mvc.Testing's attribute.
- Row b proves only builds that rewrite their outputs (finding 1).

**The check after the switch.** This holds. It catches a late write, a path ignored only on the old branch, hook output and a placed slot whose links lead outside `to`. A failed check opens no record.

**A terminal's hold across processes.**
- This holds, through the transaction on `tree_visits`.
- It holds too much (choice 1, finding 7).
- Off Windows the job is a no-op (`ProcessJob.cs:15-18`), so a dead shell's terminal child can outlive the visit the look takes over. This is minor and goes with §20's off-Windows item.

**An older build on the same home racing a take.**
- This needs `--share` or a verb beside the instance (`HomeLease.cs:57-60`, `:181-191`).
- These hold:
  - Its TREEGONE1 check runs under the shared hold (`Driver.cs:2110-2116`), which the rename's exclusive hold refuses.
  - The window from `:2117` to `:2184` is bounded by holders it can read: words waiting (both builds read the service), boxes, and the ledger after the open.
  - A start that loses the race spawns into a missing directory. Spawn files go to the home (`SpawnServers.cs:28`), not the tree, so it fails rather than running elsewhere.
- The "rollback cannot" claim does not hold after row h (finding 15e).

**The finished record in the store.**
- `line_ends` captures the tree's facts at each let-go and each take, and `line_records` keeps the first identity. This holds.
- Two problems:
  - **§10's opening reads "the line's free slot as no tree of the line's".** Under `GoesByItself` (`TreeSetAside.cs:27-30`: with no tree, it goes), that finishes a no-output line while its slot is still the conversation's place (finding 5).
  - **A finished line's carried scratch is named again in the next line's `left`, grows without bound until row j, and contradicts the D51 note's rule 4 (§13)** (finding 6).

**A finished line coming back.**
- A landed line takes a slot from its landing (`Driver.cs:2119-2127`), and a no-output line regrows its branch under `line_ends`' licence. Neither goes on detached. This holds.
- An older build is held or grows a tree fresh, and loses nothing. This holds.

**FOUNDATION1's map.**
- The holders arrive as delegates Loop composes. Course and Review call Workplace downward, and the terminal (Modules) calls Workplace downward. `SystemSpace` and `BranchTidying` are Workplace's. This holds.
- One upward edge: the Settings verb's sentences (finding 8).

**Work lost.** I found no case in which the pool loses committed or uncommitted work. Every gap above ends in `stuck`, held, or a refused Accept.

### 3. The rows, their proofs, the ⚖ rows, the owner's questions and the order

- **Row a.** Right as a shapes row. Its claim takeover over `store_runs` is the one D212's note on z-iii assigns first to Part B's row f-i ("row f-i's claims are the first to read it"). The two should share one implementation (finding 14).
- **Row b.** It lacks these proofs:
  - placement refused at the park (finding 3)
  - an Accept racing a take (finding 2)
  - a run of the carried outputs without a rebuild, and a CMake tree (finding 1)
  - a `post-checkout` hook (finding 9)
  - a pool-aware start of the line in each crash state (finding 11)
  - an interrupted drop (finding 15a)
  - a parked slot's HEAD moved (finding 4)
- **Row c.** Right. The opened moment should be `kept_trees.since` (finding 15d).
- **Row d.** Its pinned-build rehearsal has no named source: `tools/deployment-rehearsal.mjs` publishes this checkout only. It also has no named seam that real processes read, while §17 allows only a test's seam (finding 13).
- **Row e.** Right. Its installed proof is bounded by §18 item 5.
- **Row f.** Right, apart from the cap verb's edge (finding 8).
- **Row g.** It should count starts held behind another start's exclusive hold (finding 10). In this repository, tests write `_fixtures/` (e.g. `LandingHoldsTests.cs:28`), so most slots cannot be taken while their line may return, and choice 3 will rarely run (finding 6). Row g's numbers will mostly measure choice 2.
- **Row h.** It turns the pool on for stacks never measured, so it rests on finding 1.
- **Rows i and j.** Correctly the owner's. Row j's question narrows if finding 6's first option is taken.
- **The ⚖ marks** are right: rows b to j each reset, remove, or change which tree a read reaches, and row a does none of these.
- **The owner's questions.** All five are well formed: what it is, what a yes does, where to look. Item 1's *no* implies a waiting design that this round does not specify, so a *no* means another round; it should say so. Nothing that needs the owner is missing.
- **The order.** Every row comes after mod-x, which has not landed: D212 records mod-i to mod-vii built, plus row p. Two Part B dependencies are not named:
  - the dead-run takeover over `store_runs` (row f-i)
  - the boxes, which row f-i stops being written (foundation §13.3's course row says "imported and no longer written"). So the standing-move holder must read both the boxes and `moves` from row d on, not switch over at f-i (finding 14).

  DEV6 is named.

### 4. Findings

1. **Blocking. The carried kinds are not proved, and `build`, `out` and `bin` read the old path.**
   - **Where:** §11 *What crosses a move*; §0's standing; D214 point 11; row b's proof; `TreeScratch.cs:17`.
   - **What goes wrong:**
     - A CMake or Ninja `build/` (and VS's `out/build/<preset>`) names the source and build directories absolutely. Moved, it compiles the former path's sources, which another line's tree may now hold, and regenerates into it.
     - .NET `bin` holds assemblies and manifests that name content roots absolutely, which `--no-build` runs read. This repository's own `Daoris.Service.Http.Tests` uses Mvc.Testing.
     - With `obj` dropped, carrying `bin` saves almost nothing.
     - This is O1-F3's hazard, and the design's own rule, "only folders proved to read and write their own path are carried", is not applied to the carried subset.
   - **Smallest change:** `build` and `out` join the dropped kinds. Every carried kind other than `node_modules` is carried only once row b's isolation proof names it, and that proof adds a run of the carried outputs without a rebuild (`dotnet test --no-build` against the sentinel tree) and a CMake tree. Until then a kind is dropped, as `obj` is. D195's "`bin/` never" concerns the slim of a set-aside folder, not a move.
2. **Not blocking. A landing in progress holds nothing.**
   - **Where:** §3.3's landing row; §7; §5.2 step 1; `TreeLands.cs:420`, `:435`; `Trees/Landing/LandingHolds.cs:61` (LANDRACE1's per-tree hold, a file shared across processes).
   - **What goes wrong:** a choice-3 take can rename the slot between an Accept's git steps. Nothing wrong lands, because of the path invariant, but the Accept fails after landing: LANDSYNC1's step and the Tidy rule run against a gone path.
   - **Smallest change:** read `LandingHolds` as a holder, in §7 and in §5.2 step 1, under the exclusive hold. Row b adds an Accept racing a take.
3. **Not blocking. A refused placement, or a refused rename back, is unspecified.**
   - **Where:** §5.3 steps 8 and 11; §5.4's "the slot stays where it is, free".
   - **What goes wrong:** B's new branch, or R's branch, is left checked out at the park. B's retry and R's retry both fail, and TREEGONE1 refuses R (`TreeReturns.cs:81-85`).
   - **Smallest change:** an in-process refusal at step 8 or step 11 runs the takeover's park path at once: detach at the park, `update-ref -d` on B's branch at its `start`, record the slot parked, try again at the next look. Row b plants a held handle at the park.
4. **Not blocking. A parked or stuck slot has no recorded tip.**
   - **Where:** §2.3's tables (`kept_trees` has no tip; `line` is `''` when parked; `line_ends` has no slot number); §5.2 step 6; §5.4's "its recorded tip".
   - **What goes wrong:** X's HEAD pin cannot be judged for choice 2.
   - **Smallest change:** `kept_trees` gains `tip`, written at `parked` and at a failed check, and step 6 compares a parked slot's HEAD with it.
5. **Not blocking. §10's opening contradicts §6.**
   - **Where:** §10's first paragraph versus §6, §10's own *GoesByItself* paragraph and D214 point 6; `TreeSetAside.cs:27-30`.
   - **What goes wrong:** reading "the line's free slot as no tree of the line's" lets the tidy finish a no-output line (an empty branch is removable) while its slot still stands at its path.
   - **Smallest change:** read a free slot at its line's path as that line's tree (`Holds` and `Worked`, as today).
6. **Not blocking. A finished line's carried scratch.**
   - **Where:** §5.2 step 5; §10 *What `Left` and `Whole` mean*; §13's D51 note; §15's `started.tree-kept`; §6's `line_ends.left`; `.gitignore:3`, `:8`, `:14`, `:16` (`local/`, `_fixtures/`, `publish/`, `*.log`).
   - **What goes wrong:**
     - It contradicts rule 4 as the D51 note restates it: "what it holds untracked is the kept dependency and build folders".
     - It is not said when the tree is handed over.
     - The next line's `left` names it again.
     - It grows without bound until row j.
     - It hands the next agent another line's `local/`, and any agent-local settings the repository ignores.
     - Any line that wrote scratch cannot have its slot taken while it may come back.
   - **Smallest change:** at the finish, move the line's scratch outside the list aside with its record, as KEEPSIZE1b moves a finished tree's (it goes an hour after the record; today's decided rule), so row j narrows to clearing at a take. At the least: the D51 note and `started.tree-kept` name carried scratch, `left` excludes names carried in, and §8's *what it does not bound* names it.
7. **Not blocking. A visit refuses the line's own claim in place.**
   - **Where:** §2.3 ("no claim and no visit", for every kind); §7's visit row; §5.1 choice 1; §5.3 step 8 ("a folder left at R's path is moved aside").
   - **What goes wrong:** a person's terminal holds the line's own resume, words and Accept, which today go on beside it. The choice falls to other slots and marks each `stuck` before today's remake runs.
   - **Smallest change:** a visit refuses only claims that move the slot. A return is chosen only where the line's path holds no tree git lists, and step 8 moves aside only a folder git does not list.
8. **Not blocking. An upward edge from Settings to Workplace.**
   - **Where:** §8 (the cap verb names "how many retire"; the count's door refuses submodules); `Settings/DriverChoicesCommand.cs:427-436`; foundation §14.2 row 3; §0.3's "no port".
   - **Smallest change:** a port that Settings declares and Workplace answers by module initializer, within D212's three bounds, or compose those sentences above Settings. §0.3 and §2.5 say so.
9. **Not blocking. Git hooks at the park.**
   - **Where:** §5.3 step 4; §12; §5.4 (a stuck slot is freed only by a clean judgement).
   - **What goes wrong:** `git switch` runs `post-checkout` in `k-<n>`. An install hook (husky, lefthook) writes junctions or a venv tied to the park's path, the checks fail, and every take in that repository sticks a slot for good.
   - **Smallest change:** say how hooks run in the pool's git steps: either the park's switch runs with hooks off and the opening says the hook did not run, or such a repository is refused at the count's door as submodules are. Row b proves it.
10. **Not blocking. Contention on the exclusive hold.**
    - **Where:** §2.3 (every rename, the whole walk re-judged, the relink and the last status, all under `TreeLock` alone); `Driver.cs:2110-2111`, `:2184-2185`; `ChatRunner.cs:636-637`; `TreeLock.cs:22-25`.
    - **What goes wrong:** two starts in one pooled repository now take turns, one per look, and the person's new conversation is refused while any take holds the lock.
    - **Smallest change:** a conversation that cannot get the lock in row b's bounded moment opens a tree of its own (choice 5). Re-judge under the lock only what a rename makes stale (status, HEAD, lock, git's list). Row g counts starts held this way.
11. **Not blocking. A pooled line's start while a claim names its path.**
    - **Where:** §3.2's TREEGONE1 row; §3.3; §2.3's takeover; `TreeReturns.cs:56-58`, `:94-98`.
    - **What goes wrong:** nothing says the start is held rather than falling to `EnsureAsync`. `EnsureAsync` answers *stands* for an unchecked `placed` slot, and it force-removes a registration that a rename outran.
    - **Smallest change:** a pooled line's start is held while any claim names its path or its slot, and the look runs takeovers before it plans starts. Row b runs a pool-aware start of the line in each crash state.
12. **Not blocking. Lines stale at `d595cabd6`, from row p.**
    - **Where:** *Read at*; the four files listed in §1 above.
    - **Smallest change:** re-cite them, or add row p to *Read at*.
13. **Not blocking. Row d's rehearsal cannot be built as written.**
    - **Where:** §14 *Proof*; rows d and e; `tools/deployment-rehearsal.mjs` (it publishes this checkout only).
    - **What is missing:** where the pinned pre-pool artefact comes from, and how real processes switch the pool on when §17 allows only a test's seam.
    - **Smallest change:** publish the last release's commit into `_fixtures/` with no network, and name the seam (e.g. an environment variable read nowhere else).
14. **Not blocking. Part B dependencies the order does not name.**
    - **Where:** §17; §2.3's dead-run takeover; D212's note on z-iii; foundation §13.3's course row.
    - **Smallest change:** row a builds §13.4's takeover over `store_runs` once, for both TREEPOOL and f-i, or waits on f-i. §7's standing-move holder reads the boxes *and* `moves` from row d on.
15. **Not blocking. Nits.**
    - (a) The drop and the npm replacement should use a named Daoris prefix, which the takeover finishes deleting. They should drop only folders git reports ignored whole, as `TreeSlims.cs:19-22` reads them; this repository *tracks* `src/Daoris.Cli/bin` (`.gitignore`, the .NET block).
    - (b) Choice 4's making needs its own phases in §2.3, including how the takeover lifts OPENCUT1's lock before a move.
    - (c) A repository that gains a submodule after it opts in has slots holding a gitlink that can be neither moved nor parked, so their finish retries every look. Say that such a slot leaves the pool and its line finishes by today's set-aside.
    - (d) `ContentHolds`' *opened* for a slot should be `kept_trees.since`, not `GrownBranch.At`. Otherwise another line's writes under a shared ignored folder read as the returning line's own (`ContentHolds.cs:588-594`).
    - (e) Gate a take on the start's confirmation, as §13.5 gates migrations that raise the stamp, or drop §14's "D139's rollback cannot".
    - (f) `tree.kept-stuck`'s Chinese "无法带到主线" says *main line* where a return goes to a line's own branch.

**15 findings, 1 of them blocking.**

**Verdict: accept after named changes the steward can check without another read.** The blocking change is finding 1's list and proof. Findings 2, 3, 4, 5, 7 and 11 are mechanics contracts that should land in the design before row b. Every other finding can be taken in the row it names. The central mechanics hold at the code: the park, switch, check and place order, the unique and absent destination, the disk-first takeover and the finish into the store. I found no case in which they lose work.

## Opus (independent), round 3

The independent Opus 5.5 reader's read of round 3 at main `23958cb903`, eleven days after round 3's `3dd134cf3`: 14 findings, 1 blocking
(F1: no take saves the first build, and the saving round 3 claims is measured against `npm ci` where today's TREEDEPS1 copy already avoids
it, so the direction's point 2 is not met and the owner has not been told). The prerequisite has landed: FOUNDATION1's mod-x, and the two Part B
rows round 3 waits on (1d-i's keyed open, f-i's move record). Every finding of round 2 is answered, two in part (C2-2.2, O2-F14). The mechanics
hold at today's code, and I found no case in which they lose work. But eleven days of main have moved most of what they cite, and they have
added two holders, a kind of ignored file every tree now holds, one way to delete a branch, and one engine for claimed, journaled git steps. The
pool must use all of these, not stand beside them. **Verdict: another round, scoped to F1.** The trade goes to the owner first. If the owner
keeps the moving slot, F2 to F14 are named changes the steward can check without another read.

Read at main `23958cb903`: its code is byte for byte `ed75cc0eba`'s, since everything after touched only `docs/`. I read with the file tools
and `git log`, and ran nothing. A bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`. `Tests/` is the driver's tests, `Svc/`
is `src/Daoris.Service/Daoris.Service.Core/`, and `Design` is `docs/2026-10-26-tree-pool-design.md`.

### 0. The prerequisite

**FOUNDATION1's row mod-x has landed**, and the pool needs it, since its every type is Workplace's.
- The move is `faae42db97` (2026-10-27, *move Workplace sources and tests to their owner*), integrated by `9884178fd2`.
- Its last upward family was cut by `7bdbc028b8` (2026-10-29, *Workplace names nothing above it*). D212's note of that cut says the census
  goal is met (`docs/decisions/D212.md:1335`).
- Part A closed with `4e3960ff37`, whose archive entry says mod-i to mod-xxi are built (`docs/task-archive.md:21882`).
- `Trees/` is gone. Its files sit flat in `Workplace/` (with `Workplace/Domain/`).

**The two Part B rows round 3 waits on are built too.**
- **Row 1d-i, the keyed open**, was archived 2026-10-27 (`docs/task-archive.md:496`; `Svc/Sessions/SessionOpens.cs`). Design:1615 and
  `docs/decisions/D214.md:21-22` still call it unbuilt.
- **Row f-i, the move record**, was archived 2026-10-30 (`docs/task-archive.md:21942`), and f-ii to f-vi after it.

So nothing in the order still waits on the foundation. What waits is this design.

### 1. Round 3 against main today

| § | Holds? | What moved under it |
|---|---|---|
| 0, standing, *Read at* | In part | mod-x, 1d-i and f-i have landed (§0 above). Every `Trees/…` path is now `Workplace/…`. CODESHAPE1 split the hubs under 40 KB, so `TreeSetAside`'s, `BranchTidying`'s and `TreeReturns`' lines moved: the lock test cited as `TreeSetAside.cs:84-87` is `:86`, `GoesByItself` is `:29`, and TREEGONE1's `Stands` is `TreeReturns.cs:57-66` (F2) |
| 1, 1.1 | The mechanics hold | Row 1's trade is the open question (F1). The integration tree is a warm kept tree at a stable path (F11) |
| 2.1, 2.2 | Yes | The neighbours gain `i-`, the integration tree (`Workplace/IntegrationReplays.cs:77-79`, `:96-99`). The strays' passed-over list is `OthersOwn` (`Workplace/TreeStrays.cs:72-77`, `:147`), which `k-` joins. The pool on Windows alone still holds: off Windows there is no job (`Platform/OwnedProcesses.cs:52`) |
| 2.3 | In shape | The store is at step 21 with its writer stamp raised twice (`Store/DesktopStoreSteps.cs:453`, `:503`, `:633`), so the claim "builds of z-i and z-iii never open them" needs its reason restated. Four families now claim by `claim_run` and `claim_generation`, and `tree_claims` has no generation (F8). The named job and its takeover are the act engine's, already built (F8). The keyed open serves only a moving open today (F13) |
| 2.4 | Yes | `SessionLocks` and the opening's reasons are unchanged (`Workplace/SessionLocks.cs:28-65`; `Workplace/TreeOpenings.cs:16-25`) |
| 2.5 | In part | The driven start's tree is a run stage, `[RunStage("tree", RunPhase.Grown)]` (`Loop/Run/Tree/GrowTree.cs:11-134`). The conversation's tree is `Loop/Chat/ChatStarts.cs:188-215`. A new setting's doors follow the one machine-settings list (F14) |
| 3.1, 3.2 | Yes, the rule | Sites have moved or gone. The auto-lander is gone, and a run's `land` node lands every line, closing *gone* by the disk (`Workplace/LandPerformer.cs:97`, `:146`; `Workplace/TreeFolders.cs:13-15`) (F10). The tidy's in-use set is `TidyHolders` and holds more than §3.2 says (F3) |
| 3.3 | In part | The landing row's doors are gone: no Accept lands in place (CHATFLOW1g, `8aa18eb0a3`) (F10). Two ways back are new: TREEGONE2's *Go on with Daoris's brief* for a closed quest whose tree is gone, and TREEKEEPRETRY1's fresh tree for a part that left nothing (`Loop/Run/Tree/GrowTree.cs:41-45`). Each is a site that would bring a slot back (F3) |
| 4 | Yes | The provider contract changes nothing here. A returning line runs at its own path whichever door it uses, so no door's resume rule needs reading |
| 5.1, 5.2 | In part | Step 1's holders miss two (F3). Step 3 misses the binding check (F5). Step 5's "nothing ignored but `node_modules`" is false of every tree this repository opens (F4) |
| 5.3 | In mechanics | Steps 1, 9 and 11 cite `Loop/Driver.cs` lines that are now `GrowTree.cs:23-35`, `:96-98` and `:104-128`. Step 4's hooks off is right at a parking name, but the act engine runs hooks on every step (`Tests/Workplace/CheckoutWritersCensusTests.cs:31-33`), so say why the two differ (F14). Step 11's and the park path's branch delete is a second way to delete a branch (F6) |
| 5.5 | Yes | — |
| 6 | Yes | A session's processes are its launcher's own job now (`Agents/SessionProcesses.cs:514-519`; D201), no longer ORPHAN1's job: the end's proof cites it (F14) |
| 7 | In part | Two holders are missing (F3). The standing move's holder is `moves` alone, since no box is written (F9). The due auto-landing is a run's due `land` node (F10). The terminal still has no record across processes (`Daoris.Desktop.Modules/Windows/TerminalModule.cs:171-217`), so the visit is still new work and holds as designed |
| 8 | Yes | The cap still has no most (`Settings/List/MachineSettings.cs:42`), so the clamp stands |
| 9 | Yes | PRUNELINKS1 (`docs/task-archive.md:20`) confirms that `git worktree remove --force` follows junctions. §9's set-aside move and never-following purge already avoid it |
| 10 | In shape | Steps 2 and 6 are now one journaled `ref.delete` act, its recovery ref made in its own transaction, with an Undo on the Branches tab (`Workplace/ContentHolds.cs:286-329`; `Workplace/TreeSweeps.cs:270`) (F6). The set-aside's horizon now includes KEEPSIZE1's size limit (F12) |
| 11 | No, in its claim of saving | F1 and F4 |
| 12 | Yes | The pool's git writes join GITLOCK1's census (F7) |
| 13 | Yes | — |
| 14 | Yes, and better | Item 2's "a conversation's new part takes no window at all" no longer holds: the conversation's start takes the shared window before it opens (`Loop/Chat/ChatStarts.cs:198-199`). That narrows the race, and the pool's renames refuse it now |
| 15 | Yes | — |
| 17 | In part | `AutoLandingTriesTests` and `AutoLandingRulesTests` no longer exist (Design:1107). Row a's takeover is now taken from f-i, which has landed. Rows d and e cite the moved sites (F2, F9, F10) |
| 18 | In part | Item 4's horizon (F12). A sixth item is needed (F1) |

### 2. Round 3's answers to round 2 (§23)

**Codex, round 2**

| Finding | Round 3 | Answered? |
|---|---|---|
| §0: 1.2, 2.2, 2.3 and 2.6 still open | Re-taken as 2.5, 2.2 with 2.3, 2.4 and 2.6 | Answered, each below |
| 1.1 the last guard refuses every lock | §2.4, §5.2 steps 2 and 3 | Answered. Its test is now `TreeSetAside.cs:86`, inside `LastLookAsync` (`:72`), which must take the pool's reading as a parameter rather than be skipped |
| 1.2 carried outputs run | `node_modules` alone (§11) | Answered |
| 2.1 links across a crash | Relinked at the park before placement (§5.3 step 6) | Answered. It rests on a junction made to a folder not yet standing (`Workplace/TreeDependencies.cs:312-330` writes reparse data and checks no target), which row b proves, with §20's fallback |
| 2.2 the open's identity | `opens` written first, read from `session_opens` | **Partly.** The keyed open is built, but today only a moving open uses it ("every other start opens as before", `Loop/Chat/ChatStarts.cs:221-227`). And a takeover that cannot reach the service is not specified (F13) |
| 2.3 child quiescence | A named job per claim, emptied by the takeover | Answered. Build it on the act engine's own primitives (F8) |
| 2.4 the finish's facts before the delete | KEEPSIZE1b's manifest at `judged` | Answered. The delete it then makes is the journal's act (F6) |
| 2.5 a link's owner | npm's workspace rule | Answered |
| 2.6 row i's working snapshot | Seeded from `I` | Answered |
| 2.7 `started.tree-past-kept` | Reworded | Answered |
| 3.1 the central choice, conditionally | Row b's note before row d | Agreed. But the condition is measured against the wrong baseline (F1) |
| 3.2 served-build admission | A `served` visit | Answered. `ReviewDesk.cs:226-236` is still where serving starts |
| 4.1, 4.2, 4.3 | Proofs added; the installed resume at g; the submodule judged at every take | Answered |
| 5.1 the build folders a move drops | §18 item 4; nothing removed at a take | Answered as a removal. But it leaves the direction's point 2 unmet for every take, and that is not put to the owner (F1) |

**Opus (foundation), round 2**

| Finding | Round 3 | Answered? |
|---|---|---|
| F1 (blocking) `build`, `out`, `bin` | `node_modules` alone | Answered |
| F2 a landing under way | The landing hold read under the exclusive hold | Answered. Its sites moved: a run's `land` node performs every landing (F10), and a delivery's landing holds the repository alone around its ref transaction (`Workplace/TreeLock.cs:29-31`). Under that hold the judgement may only *try* the landing hold, never wait for it (`:35-41`) (F7) |
| F3 a refused placement | The park path at once | Answered |
| F4 a parked slot's tip | `kept_trees.tip` | Answered |
| F5 §10 against §6 | A free slot is its line's tree | Answered |
| F6 a finished line's scratch | Set aside with its record | Answered for scratch. TREELOCAL1's machine-local copies are a kind it does not name (F4) |
| F7 a visit holds back the line's own claim | A visit holds back only a move | Answered |
| F8 Settings to Workplace | `KeptTreesPort` | Answered. Its door follows today's per-repository settings (F14) |
| F9 hooks at the park | `core.hooksPath` to an empty folder | Answered (F14's note) |
| F10 contention on the hold | A conversation opens its own tree | Answered |
| F11 a start while a claim names its path | Held for the look | Answered. Its site is now `GrowTree.cs:23-35` (F2) |
| F12 stale lines | Re-cited for row p | Answered then. Stale again since mod-x and CODESHAPE1 (F2) |
| F13 the pinned build's source | The last release from the repository's own objects | Answered (`tools/deployment-rehearsal.mjs` stands) |
| F14 Part B dependencies | Build the takeover once, or take it from f-i; read boxes and `moves` both | **Partly**, overtaken. f-i has landed and no box is written (F8, F9) |
| F15 (a) to (f) | Each taken | Answered |
| §3: item 1's *no* | A no means another round | Answered |
| §3: row g mostly measures choice 2 | Said | Answered, and narrower still (F3) |

### 3. Findings

1. **Blocking. No take saves the first build, and the saving round 3 claims is measured against the wrong baseline.**
   - **Where:** Design:246-247 (a slot handed on holds nothing ignored but `node_modules`); :892-915 (*what it saves*: "npm's install (the
     measured 170 to 221 s) … a new line's .NET, Rust, Angular and Next builds start cold"); §1.1 row 1; §18. Against it:
     - the direction's point 2: "its ignored dependency and build folders kept, so the session starts clean and pays no setup it already paid"
       (`docs/2026-10-26-tree-pool-brief.md:17-18`);
     - TREEDEPS1's copy at every open (`Workplace/TreeDependencies.cs:1-19`; driving area rule 21);
     - D51's note of 2026-10-19: the web's `node_modules`, 228 MB, copied in 5.9 s, against its `npm ci` at 70 s warm
       (`docs/decisions/D51.md:85-88`).
   - **What goes wrong:**
     - Today a fresh tree whose lockfile matches the checkout's already skips `npm ci`. So against today, a take saves TREEDEPS1's copy
       (seconds a project) and the `worktree add` checkout, less two renames, a switch, two checks and the exclusive hold.
     - It saves no build, ever. A finished line's `bin/`, `obj/`, `dist/` and `wwwroot` go aside with its record. A live line holding any of
       them keeps its slot. Only choice 1, today's resume in place, keeps a build.
     - The owner's reason for pursuing this, a fresh checkout *and its first build* saved, is half unmet. The standing and §18 do not say so.
     - The reason is structural. Moving the slot to the line's path is what makes absolute-path outputs (NuGet's props, `.venv`, CMake) unsafe
       to carry. A stable slot path would keep them valid, and the integration tree (F11) is one already. So whether the first build matters
       is a question for the central choice, not for row b's timing.
   - **Smallest change:**
     - State the trade in the standing, §1.1 row 1 and §11, against today's TREEDEPS1 copy rather than `npm ci`.
     - Row b measures both on this repository: the take's admission to first turn against a fresh tree's, and a cold `dotnet build` with
       `npm run build` against a warm one at the same path.
     - Add §18 item 6 for the owner: *the pool as designed* (the checkout and the copy saved, builds cold, nothing removed), or *another round
       that weighs a stable slot path for build reuse*, with its older-build hazard (§0.2 row 5, still `GrowTree.cs:23-35` with
       `TreeReturns.cs:57-66`) bounded another way.
     - Accept nothing until the owner answers.

2. **Not blocking. Every `Trees/` and `Loop/Driver.cs` citation has moved, and the take belongs in the run's stages.**
   - **Where:** Design:62-69, §2.3, §5.3, §17 rows d and g.
     - The driven start's tree is `Loop/Run/Tree/GrowTree.cs`: TREEGONE1 under the shared window at `:23-35`, `StandsNow` at `:56-57`, the
       start's hold at `:96-98`, the open at `:104-128`.
     - The conversation's is `Loop/Chat/ChatStarts.cs:188-215`.
     - The record opens in `Loop/Run/Open/OpenRecord.cs`.
     - The driving area says a part of a start is a `[RunStage]` writing `RunState`, which lets go of the held trees with the run
       (`docs/areas/driving.md:22-23`, `:291`).
   - **Smallest change:** re-cite every line at the row's own base. Place the take as a stage of the `Grown` phase, its claim on `RunState`
     (let go with the run, as `Starting` is), so a run that ends before `OpenRecord` sends the slot back through the run's own release.

3. **Not blocking. Two holders the code has added since round 3.**
   - **Where:** §7's table and its *D79 wait … no* row (Design:765); §3.2's in-use row (:463); §5.1 choice 3.
     - `TidyHolders.LineHolders`: the newest part of every open or taken quest holds its tree, whatever its state, a D79 wait among them
       (TREEKEEPRETRY1; `Workplace/TidyHolders.cs:39-46`, `:81`, `:99-108`).
     - `SpokenTo`: a tree whose conversation the person spoke to within the hour (TREEGONE2; `:30-37`, `:76`).
   - **What goes wrong:** §3.2 keeps the in-use set and joins the pool's holders to it, while §7 says a D79 wait and an ended conversation hold
     nothing. The two contradict each other.
   - **Smallest change:** say which rule the pool reads. Today's `InUseAsync` holds both, which narrows choice 3 to closed quests' lines and to
     conversations not spoken to within the hour. Add `CannotGoOn`'s *Go on with Daoris's brief* and `LeftNothing`'s fresh tree to §3.3's ways
     back.

4. **Not blocking, but before row b. TREELOCAL1's machine-local inputs.**
   - **Where:** `Workplace/TreeLocalInputs.cs:6-8`, `:18-24`: read-only copies under `local/`, ignored, copied into every tree as it opens.
     `TreeReturns.cs:117-122` copies them at a remake too. Design:246-247, :594-598, :892-903.
   - **What goes wrong:**
     - Every tree this repository opens holds an ignored `local/` file. So by §5.2 step 5 no live line's slot is ever taken.
     - A parked slot, its copies set aside at the finish, is handed on with none. Its gates then run without them, and the sensitive scan
       "falls back to its built-in patterns and passes" (`TreeLocalInputs.cs:8`).
   - **Smallest change:** name the declared inputs as Daoris's own copies beside `node_modules` in §5.2 step 5. Re-copy them at the park after
     the switch, from the checkout's current files, as the remake does, under the claim's lock and undone by its park path. Their read-only
     mark meets the never-following delete. Row b proves that a taken slot's gates read the checkout's private patterns.

5. **Not blocking. The binding check is missing from the take's checks.**
   - **Where:** GITWORKTREECONFIG1's `TreeBinding.RefusalAsync`, which refuses a tree git binds to another checkout, at every resume and landing
     (`Workplace/TreeReturns.cs:61-65`, `:138-143`; `Workplace/LandingPress.cs:101-106`). §5.2 step 3 and §5.3 steps 8 and 10 omit it.
   - **Smallest change:** add it to both checks. A bound slot is `stuck`, said in the binding's words.

6. **Not blocking. A second way to delete a branch.**
   - **Where:** the park path's and step 11's `git update-ref -d refs/heads/<branch> <start>` (Design:348-352, :669-670); §10 steps 2 and 6.
     Against them:
     - ONEWAY1r: every branch delete is the journal's `ref.delete` act (`docs/areas/git-and-remotes.md:46`, rule 38);
     - the one-way census allows only `TreeOpenings.cs` (`Tests/Workplace/JournaledDeletesTests.cs:104-118`);
     - the finish's delete already goes through the journal (`Workplace/TreeSweeps.cs:270`; `Workplace/ContentHolds.cs:286-329`).
   - **Smallest change:** a take's empty branch goes through `JournaledDeletes.DeleteAsync` judged at `start`, which verifies the commit and keeps
     the Undo, or through the opening's undo, the census's one exception, with the census naming why. §10 steps 2 and 6 become the one
     journaled act. Say what the person's Undo of a pooled line's delete brings back: the branch, its path empty, so the line comes back by
     §3.3.

7. **Not blocking. GITLOCK1's census, holder name and lock order.**
   - **Where:**
     - `TreeLock` is one lock per checkout's common folder, its holders named (`Workplace/TreeLock.cs:26-33`, `:48-60`, `:135`, `:141`).
     - Every git write is named in `CheckoutWritersCensusTests`, and "a new call is a failing line until it is named"
       (`Tests/Workplace/CheckoutWritersCensusTests.cs:5-11`).
     - No flow waits for a landing hold under the lock (`TreeLock.cs:35-41`).
   - **Smallest change:**
     - Give the pool a holder name, so a start it holds says *being handed on*.
     - Add the pool's `worktree move`, `switch` and ref writes to the census as an own-tree writer.
     - Say that the landing hold is only tried under the exclusive hold (O2-F2's read).

8. **Not blocking. Use the act engine's job and the store's claim, not new ones (D228).**
   - **Where:** §2.3 and §12's named job per claim and its takeover. Against them:
     - `Workplace/ActRuns.cs:76-77`, `:163-166`: a named job per act, `ProcessJob.CreateNamed(ActJournal.JobOf(id))`.
     - `Workplace/ActSteps.cs:370-405`: each git started suspended in it.
     - `Workplace/ActReconciler.cs:16-17`, `:196-208`: opened by name, ended, awaited empty.
     - §2.3's claim table (:294) has `run` and no generation, while `moves`, the act journal, deliveries and the integration store each claim
       by `claim_run` and `claim_generation` and take over by one guarded update (`Course/MoveStore.cs:19-24`).
   - **What goes wrong:** with no generation, two lookers (the shell and a `--share` loop) that both read one claim dead are not fenced from
     each other.
   - **Smallest change:** cite and reuse those primitives. Add `generation`, and take over by its guarded update. Row a's takeover is f-i's,
     already built.

9. **Not blocking. The standing move is `moves` alone.**
   - **Where:** Design:168, :761, row d. Since FOUNDATION1f-vi part a no build writes a box; an older build's box is imported once and is its
     record's *never* while it stands (`Course/Chat/LimitBox.cs:6-8`, `:25-30`). `moves_standing` holds one standing move per record
     (`Course/MoveStore.cs:14-18`).
   - **Smallest change:** the holder reads a standing `moves` row, plus an unimported box only while one stands. "Both from row d on" goes.

10. **Not blocking. Landing is a run's `land` node.**
    - **Where:** §3.2's auto-lander row, §3.3's landing row, §7's *due auto-landing*, row e's `return` claim at `TreeLands.cs:196-204`,
      `:918-926`. `AutoLander.cs` is gone, and no Accept door lands in place (CHATFLOW1g). `LandPerformer` closes a due land *gone* wherever
      `TreeFolders.TreeGone` says so (`Workplace/LandPerformer.cs:97`, `:146`).
    - **Smallest change:** the re-keyed sites are `LandPerformer`'s two `Gone`s. The holder is a run whose `land` node is due or under way. Drop
      the two gone suites from §17.

11. **Not blocking. The integration tree is a neighbour and a precedent.**
    - **Where:** `Workplace/IntegrationReplays.cs:72-79`: `i-<line>`, "a warm tree of Daoris's own", kept between candidates, put back at a
      recorded commit by reset, clean and checkout. §2.2's neighbours omit it. §1.1 row 1 does not weigh it.
    - **Smallest change:** name `i-` in §2.2. Say why a kept session slot and a kept integration tree are not one way: the second holds no line's
      work and no record names it. That difference is also exactly what a stable slot path would need (F1).

12. **Not blocking. Set-aside folders also go by size.**
    - **Where:** §10's *by today's rule* and §18 item 4's "deleted after D195's set-aside days (fourteen by default)". KEEPSIZE1 is built: a
      set-aside kind takes "at most N GB beside its days, whichever comes first, oldest first" (`docs/task-archive.md:22217`).
    - **Smallest change:** say the horizon is the days or the kind's size limit, wherever a set-aside folder is promised to wait.

13. **Not blocking. The takeover's read of the keyed open when the service does not answer.**
    - **Where:** §2.3's *an open requested* (Design:357-361): "No row: the open never landed".
    - **What goes wrong:** an unanswered read is not *no row*. Read as one, it would park a slot whose record opened.
    - **Smallest change:** an unanswered read holds the claim for the next look and is never parked. Row d extends the keyed open to the pool's
      opens, since today only moving opens use it (`Loop/Chat/ChatStarts.cs:221-227`).

14. **Not blocking. Nits.**
    - (a) `keptTrees` is per repository: its doors follow the per-repository `trees` set (`Settings/DriverConfig.cs:557`), and the machine
      settings list stays as it is (`Settings/List/MachineSettings.cs:26-45`).
    - (b) Hooks: say why the pool's switch runs none where every act's step runs them (`Tests/Workplace/CheckoutWritersCensusTests.cs:31-33`).
      The pool switches at a name no hook should key.
    - (c) The end's proof that a session's processes are gone is its launcher's own job (`Agents/SessionProcesses.cs:514-519`). Where an agent
      was not contained (`Platform/AgentJobs.cs`' reasons), the rename's `busy` is the only guard: say so.
    - (d) D214:21-22 and Design:1615 call 1d-i unbuilt.

**14 findings, 1 of them blocking.**

### 4. §18 item 4, for the owner

*Whether taking a slot may move a live line's ignored build folders out of it.* A live line is one that may still go on. Its build folders are
`bin/`, `obj/`, `dist/`, `.venv`, `target/` and the like.

- **(a) No**, round 3's default. A live line keeps its slot while it holds any of them.
  - The pool hands on finished lines' slots and new ones, and trees past the count open as today. Nothing is removed.
  - In this repository choice 3 then almost never runs: sessions build and test, every tree holds TREELOCAL1's copies (F4), and today's
    holders keep every open quest's newest tree anyway (F3).
- **(b) Yes, as row j writes it.** They are moved aside whole into `.tidied`, never deleted at the take, and the line rebuilds at its return.
  - They go by the set-aside kind's days or its size limit, whichever comes first (F12). On a busy day that may be sooner than fourteen days.
- **(c) Yes, and laid back.** The same move aside, but the line's own folders are put back when it returns.
  - A line always comes back to its own path, so its own build folders are valid there again, `.venv` and NuGet's props included.
  - They are laid back before the last check, only where the line's tip is the one their manifest names.
  - They go by days or size only once the line has finished, or if it never returns.
  - Only the list's build kinds move. Any other ignored path still keeps the slot.
- **(d) Yes, deleted at the take.** Not recommended: it loses what a clean install cannot remake.

**Recommendation:** answer **(a)** for the first build. If the owner later wants live lines' slots reused, choose **(c)** over **(b)**: under
(c) a move aside is a delay, not a loss, and it is the only way the moving slot can ever save a build (F1). Weigh it after F1's answer and row
g's count of how often a slot stayed its line's for this reason.

**Verdict: another round, scoped to F1.** The owner answers F1's question first. If the owner keeps the moving slot, round 4 restates the
saving and takes F2 to F14 as named changes the steward can check without another read. If the owner wants the first build saved, round 4
reopens §1.1 row 1. In both cases the central mechanics stand as round 2's read found them, and I found no case in which they lose work.

## Opus (independent), round 4

The independent Opus 5.5 reader's read of round 4 at main `b8ef097e29`: 8 findings, 2 of them blocking. The slot's mechanics hold at today's code:
- a fixed folder that no record names;
- every record naming its line's path, so a reader the pool missed, and an older build, meet a gone tree;
- the switch in place under the start's shared hold;
- the claim on the act engine's job and generation.

I found no case in which they lose work. But the saving the owner chose the fixed path for does not hold in this repository (F1). The .NET SDK
stamps the commit into every assembly, so a take compiles every project again. Measured here, the best case saves about 18 s of a 69 s cold
build, not 86 s of 98 s. And two of the kinds `SlotKeeps` carries, `bin/` and `wwwroot`, are outputs this repository's runners use without
building, the class §11 keeps from crossing lines (F2). **Verdict: another round, scoped to F1 and F2.** F3 to F8 are named changes the steward
can check without another read.

Read at main `b8ef097e29`, round 4's `75a4b694be` with eleven hours of main merged. I read with the file tools and git and changed no file but
this one. I ran builds in my own worktree, which wrote only its ignored `bin/` and `obj/` and scratch under `_fixtures/`. To give a build
another commit, I switched that worktree detached and back. A bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and `Design` is
`docs/2026-10-26-tree-pool-design.md`.

### 0. What was measured

The SDK is 10.0.402, on Windows, with other sessions running on the machine.

- **One small project, its inputs equal at two commits.** `Daoris.Desktop.PieceStart` compiles two driver files. Its project, those files,
  `src/Daoris.Desktop/Directory.*.props` and `canon/canon.json` are byte for byte the same at `e7e96cfaa6` and `b8ef097e29`.
  - Rebuilt with nothing changed: `Skipping target "CoreCompile"`, 1.4 s.
  - After `switch --detach e7e96cfaa6`, with no input changed: `csc` ran. The generated `obj/.../Daoris.Desktop.PieceStart.AssemblyInfo.cs`
    went from `AssemblyInformationalVersionAttribute("0.0.1+b8ef097e29…")` to `"0.0.1+e7e96cfaa6…"`, and the project's
    `obj/.../*.sourcelink.json` names the commit too.
- **The two desktop test projects** (the driver's and the modules'), the pair round 4 measured:
  - cold, restore included: 42 s and 27 s, 69 s together;
  - unchanged: 11 s and 7 s, and 5 s and 7 s in a later sample;
  - after a switch to `6e715f8942`, whose tree is identical to `b8ef097e29`'s, so the switch wrote no file: 67 s and 21 s under the
    machine's load, and 37 s and 14 s, 51 s together, after switching back.
- **What compiled after each switch.** Every project of the driver tests' build compiled (8 `csc` runs, 0 skipped). The modules tests' build
  compiled its 4 others, the 2 it shares with the driver tests' build already being current at that commit.

So the round's 12 s is a build at an unchanged `HEAD`, which no take meets. A take moves `HEAD` to another commit, and that alone compiles
every project.

### 1. Round 4 against main today

| § | Holds? | What I found |
|---|---|---|
| Standing, 2.1, 11, 24's F1 | **No**, in the saving | A commit change alone compiles every .NET project (F1); `bin/` and `wwwroot` are run without a build (F2) |
| 2.2, 2.3 | Yes | `TreeReturns.cs:57-65`, `:63` and `GrowTree.cs:11` as cited. §2.3's "writer stamp unchanged" is stale (F8) |
| 2.4 | Yes | `SessionLocks` unchanged |
| 3.1, 3.2 | The rule, yes | The census misses the readers that name a branch from a listed folder (F7) |
| 3.3, 4 | Yes | `TidyHolders.cs:30-37`, `:99-108` as cited; Claude Code files by folder, so a same-slot return keeps the conversation |
| 5.1 | In part | Choice 4 is empty in this repository, not few (F5) |
| 5.2 | Yes | Steps 2 and 6 as cited. The npm judgement trusts another line's install by a file's time (F6) |
| 5.3 | In part | Step 3's `CopyAsync` declines a copy already there (F3). Step 6's journaled delete is refused under the start's shared hold (F4). Step 5's hook writes an ignored path of the line's own (F5) |
| 6, 7, 8, 9, 10 | Yes | `LandPerformer.cs:97`, `:146` and `TreeFolders.cs:13-15` as cited |
| 11 | **No** | F1, F2, F6 |
| 12, 13, 14 | Yes | An older build's TREEGONE1 holds a line whose branch a slot has checked out (`TreeReturns.cs:88-93`), as §14 says |
| 15 to 20 | Yes | Row b's proof of builds cannot pass as written (F1) |

### 2. Round 4's answers to round 3 (§24)

| Finding | Round 4 | Answered? |
|---|---|---|
| F1 (blocking) no take saves the first build | A fixed path, the owner's choice | **Partly.** The outputs stay valid at the slot's path, but in this repository a commit change compiles every project again. The 86 s is the unchanged-`HEAD` case (F1 here) |
| F2 moved citations; the take a run stage | The take a `[RunStage]` of `Grown`, its claim on `RunState` | Answered (`GrowTree.cs:11`, `RunState.cs:199-204`) |
| F3 two new holders | `TidyHolders` read as the one rule | Answered |
| F4 TREELOCAL1's inputs | Daoris's own; copied again at a take | **Partly.** `CopyAsync` refreshes nothing in a slot that holds them (F3 here) |
| F5 the binding check | At the judgement and the check | Answered |
| F6 a second way to delete a branch | `JournaledDeletes.DeleteAsync` judged at `start` | **Partly.** Refused where the design runs it, inside the start's shared hold (F4 here) |
| F7 GITLOCK1's census and lock order | The take in the start's shared hold, as an open; the pool's writes named | Answered |
| F8 the act engine's job and generation | Reused | Answered |
| F9 `moves` alone | Taken | Answered |
| F10 the `land` node | `LandPerformer`'s two `TreeGone` reads re-keyed | Answered |
| F11 the integration tree | Named a neighbour; two makings kept | Answered |
| F12 the size limit | Days or size, wherever a folder waits | Answered |
| F13 an unanswered read | Holds the claim | Answered |
| F14 nits | Each taken | Answered. (b)'s hook now runs, and here it writes `docs/index/` (F5 here) |

### 3. Findings

1. **Blocking. A take compiles every .NET project again, since the SDK stamps the commit into every assembly.**
   - **Where:**
     - Design:16-19, the standing's saving;
     - Design:867-873: "compiles only what its branch changed since the slot's last build";
     - Design:881-892, the 86 s;
     - Design:1112-1113, row b's proof that B's build "compiles that project and its dependants only";
     - Design:1458, §24's F1;
     - `docs/decisions/D214.md:4-5`: "a reused tree skips the first build".

     Against them:
     - `src/Daoris.Desktop/Directory.Build.props` and `src/Daoris.Service/Directory.Build.props` set neither
       `IncludeSourceRevisionInInformationalVersion` nor `EnableSourceLink`. So the SDK's defaults put `+<commit>` into every assembly's
       informational version and the commit into its source link file, and §0 measured the effect.
     - This repository reads that version at run time (`Store/DesktopStore.cs:88`; `Workplace/InstallRollback.cs:14`;
       `Instance/InstanceClient.cs:227`; `Loop/SystemWiring.cs:96`; `src/Daoris.Service/Daoris.Service.Http/HostVersion.cs:9-10`). So neither
       Daoris nor a session can turn the stamp off for a line's builds without changing what those builds test.
   - **What goes wrong:**
     - Every take moves `HEAD`, so every take recompiles every project. Even a switch that writes no file does.
     - What a take saves of the first .NET build is the restore and the copy of package files: about 18 s of 69 s here. A switch between lines
       that differ a lot saves less: a changed package reference restores again, and the switch writes more files.
     - Against today, a take saves the checkout, TREEDEPS1's copy (about 6 s) and that restore. It pays for them with what §1.1 row 1 lists:
       D189 amended, every folder-reader asking the pool, another line's outputs underfoot.
     - The owner chose the fixed path on 2026-11-07 on round 3's F1 and this round's 86 s. Neither holds for this repository.
     - Row b's proof as written cannot pass.
   - **Smallest change:**
     - Restate the saving in the standing, §11, §24 and D214, from a build after a switch, never an unchanged one. Say it by toolchain: a .NET
       repository on the SDK's defaults compiles every project per commit, and other toolchains are unmeasured.
     - Turning the stamp off is a repository's own build choice, asked of it as any change in its code is. It is never the pool's.
     - Row b's proof says every project compiles, the restore and unchanged copies are skipped, and the outputs equal a clean build.
     - Put the corrected trade to the owner before any row is built (§4 below).

2. **Blocking. `bin/` and `wwwroot` are outputs this repository's runners use without building, the class §11 keeps from crossing lines.**
   - **Where:** Design:855-866. `SlotKeeps` keeps `bin/`, `obj/` and `wwwroot`, while "what never crosses lines" is "an output a tool runs in
     place of the line's sources". Design:888-890: "a kept `wwwroot` serves only a line that does not build the web again".

     Against them:
     - `src/Daoris.Web/scripts/e2e-host.mjs:14-24` runs the host's kept `bin/Debug/net10.0/daoris-knowledge-http.dll` and refuses only where it
       is missing. It serves the project's `wwwroot` (`:36-42`).
     - `tools/desktop/commands.mjs:106` and `:160` run the kept shell and host, and refuse only where none is built.
     - `Daoris.Desktop.Modules/Windows/DesktopPage.cs:49-62` serves the first `wwwroot` that holds a page.
     - This repository's agents are told to build once and then `dotnet test --no-build` (`.claude/agents/branch-worker.md:51-52`).
   - **What goes wrong:**
     - In a fresh tree each of these stops: "not built". In a taken slot each runs the previous line's build silently, and reports its result as
       this line's. That is a wrong test result with no tool saying so.
     - `started.tree-kept-built` says a kept output is no proof that the commit builds. It does not say that a run without a build runs another
       line's code.
     - After F1, keeping `bin/` saves only the copy of package files, and §11's own measurement says keeping `wwwroot` saves nothing.
   - **Smallest change:**
     - `SlotKeeps` is `node_modules`, and `obj/` beside a tracked project file. `bin/` and `wwwroot` are the line's own, as `dist/` is, and go
       aside at its finish. The next build copies `obj/`'s outputs into `bin/` again.
     - `obj/` holds no output that runs unbuilt: the driver tests' own host build under `obj/service-host` is built before every use
       (`Tests/Service/RealServiceHost.cs:41-60`).
     - Row b proves that `--no-build`, `e2e-host.mjs` and `desktop.mjs run` refuse in a taken slot as they do in a fresh tree, and measures what
       copying `bin/` again costs.

3. **Not blocking, before row b. `CopyAsync` refreshes no TREELOCAL1 copy that a slot already holds.**
   - **Where:** Design:612-614 (§5.3 step 3). `Workplace/TreeLocalInputs.cs:141` declines an input whose place is taken (`LinkOnTheWay`,
     `:204`, `Path.Exists(final)`). `CopyOne` renames without overwriting (`:180`).
   - **What goes wrong:**
     - At a take every input is declined, so the slot keeps the previous line's copies. A pattern the checkout changed is never seen, and row b's
       own case of that change fails.
     - A copy the checkout no longer declares is, by §5.2 step 5, the line's own, so it holds the slot.
   - **Smallest change:** at a take, delete the slot's earlier copies under `local/` first: the read-only mark cleared, never following a link,
     the undeclared ones among them. Then run `CopyAsync`. Say that all of them are Daoris's.

4. **Not blocking. A take's undo cannot run the journaled delete inside the start's shared hold.**
   - **Where:** Design:323-324 and :629-630: a refusal or a start ended before its open deletes B's branch through `JournaledDeletes.DeleteAsync`,
     in the take's stage. Against them:
     - an act takes the checkout lock alone (`Workplace/ActRuns.cs:113`);
     - it runs under an outer hold only where that hold is alone (`:121-124`; `Workplace/TreeLock.cs:81-82`);
     - a start holds the lock shared (`TreeLock.cs:20-23`).
   - **What goes wrong:** the delete is refused, "nothing was changed", so B's empty branch stands. Nothing is lost, since the look's tidy takes an
     empty branch and the takeover runs outside any hold. But the design names a step that cannot run where it puts it.
   - **Smallest change:** in a living process, detach and free the slot under the shared hold, and leave the delete to the look's takeover once
     the hold is let go. Or use the opening's own undo, the census's one exception, with the census naming the take. Row b's "a take ended
     before its open" proves which.

5. **Not blocking. Every pooled line in this repository holds ignored paths of its own from its first moment.**
   - **Where:** Design:535-538 (choice 4, "few such slots qualify"), :622-624 (the hook after the check), :1313-1329 (§18 item 4). Against them,
     three writers run in every line:
     - this repository's `post-checkout` and `post-merge` hooks write the ignored `docs/index/` after a switch or a merge on a branch
       (`tools/index-hook.mjs:7-11`, `:27-35`);
     - `npm run quick`, which every session runs before it hands back (`package.json:16`), writes the contracts' ignored types
       (`.gitignore`'s `src/Daoris.Web/src/data/generated/` and `src/Daoris.Web/src/bridge/generated/`);
     - the web's pre-scripts write the same types.
   - **What goes wrong:**
     - None of these is in `SlotKeeps` or Daoris's copies, so each is the line's own. Choice 4 is empty here, whatever a line writes.
     - Every finish sets them aside, and the next take's hook and the next `--if-stale` write them again.
     - Carrying them is safe: the hook writes the index after every switch, and each generated file stamps a hash of its sources
       (`tools/contracts.mjs:12-15`).
   - **Smallest change:** name these two as the slot's in a dated note on row b's proof, or say that choice 4 is empty in this repository until
     §18 item 4 is answered. Row g counts it.

6. **Not blocking. npm's freshness, read by a file's time, trusts another line's install.**
   - **Where:** Design:893-897, after TREEDEPS1's rule (`Workplace/TreeDependencies.cs:123`; `docs/decisions/D51.md:60`): fresh where
     `node_modules/.package-lock.json` is not older than `package-lock.json`.
   - **What goes wrong:**
     - In the checkout, only the person installs. In a slot, an earlier line's agent did.
     - An `npm install --no-save <name>` rewrites the hidden lockfile and leaves the tracked one, so the next line is told its dependencies match,
       with a package installed that its lockfile does not name.
     - A hand edit inside `node_modules` changes neither file.
   - **Smallest change:** read fresh only where the hidden lockfile's `packages` equal the tracked lockfile's. Otherwise treat it as stale.

7. **Not blocking. The readers that name a branch from a listed folder meet a slot as `daoris/k-<n>`.**
   - **Where:** Design:224-226 argues that only TREEFOLLOW1 learns from a folder. Others act on the name:
     - a detached tree's discard keeps its commits under the layout's name (`Workplace/TreeWorkReads.cs:166-167`);
     - a removal of a detached tree names it (`Workplace/TreeRemovals.cs:88`);
     - a landing's replay reads it (`Workplace/TreeBranches.cs:151`, `:168`);
     - an update reads its grown entry (`Workplace/TreeUpdates.cs:383`).
   - **What goes wrong:** a person's commit made in a detached slot and then discarded would be kept as `refs/daoris/discarded/daoris/k-1`, a
     recovery ref named for a slot, which §19 rejects (O-2).
   - **Smallest change:** row a's census also lists every reader that derives a branch from a folder git lists, each asking the pool or refusing
     a `k-` folder.

8. **Not blocking. Nits.**
   - (a) Design:247-249 still says "the writer stamp unchanged". Round 3's read found the stamp raised twice
     (`Store/DesktopStoreSteps.cs:453`, `:503`, `:633`): restate why older builds never open the tables.
   - (b) `git hook run` needs git 2.36, by git's documentation; the doors' floor is 2.31 (git area rule 14). Say what a take does below 2.36.
   - (c) Design:877-878's "MSBuild reads the SDK among its inputs" is a time comparison, and an SDK installed with older file times is not newer
     than the slot's outputs. After F1 a take compiles anyway, so say only that row b measures a toolchain change at an unchanged commit.

**8 findings, 2 of them blocking.**

### 4. §18 item 4, for the owner

*Whether a kept tree may be handed on while its last line may still come back.* Under round 4, the tree's dependencies and builds stay in it.
What would move is the line's own: its scratch, and any output a tool runs in place of the next line's sources.

- **(a) No**, round 4's default: such a tree stays its line's until the line finishes.
  - In this repository, no tree is ever handed on this way, not few. Every line holds `docs/index/`, which the take's own hook writes, and
    the contracts' generated types, which `npm run quick` writes (F5).
  - So the pool here hands on finished lines' trees and new ones only.
- **(b) Yes, moved aside and laid back.** As row j writes it. The line also loses its conversation unless it returns to the same tree (§4).
  - After F1 the tree's builds save the restore and little more, so this buys little: a checkout, TREEDEPS1's copy and the restore.
- **(c) Yes, moved aside and not laid back.** As (b), but the line comes back without its scratch.
- **(d) Yes, deleted at the take.** Not recommended.

**Recommendation:** answer **(a)** for the first build, as round 4 does, and record that in this repository it hands on finished lines' trees
alone until F5's content-stamped kinds are named the tree's. Choose (b) only if row g shows that starts went past the count because live
lines held trees, and only after F1's corrected saving shows the move is worth a conversation lost to the brief. Prefer (b) to (c), since
laying back makes the move a delay, not a loss.

**Verdict: another round, scoped to F1 and F2.**
- F1: the owner is told the saving the fixed path buys in this repository, measured after a switch, and answers whether its costs are still
  worth it.
- F2: `SlotKeeps` loses `bin/` and `wwwroot`.
- F3 to F8 are named changes the steward can check without another read.

The round's mechanics hold at main's code, and I found no case in which they lose work.

## Opus (independent), round 5

The independent Opus 5.5 reader's read of round 5 at main `353a67a334`: 11 findings, 4 of them blocking, all four in row 2. I read the owner's
three choices (D214's note of 2026-11-07) as given: rooms in place, kept slots as the fallback, no Dev Drive. BUILDSTAMP1 is built as R5.8
says (`Directory.Build.props:12-16`, imported by `src/Daoris.Desktop/Directory.Build.props:3` and `src/Daoris.Service/Directory.Build.props:3`,
`-p:DaorisStamp=true` at `tools/service-publish.mjs:51`).
- **The fallback holds.** §25's answers to round 4's F1 to F8 hold at main, with three named changes (F9). I found no case in which it
  loses work.
- **The classes and the store** are stack-agnostic in their frame, and safe on Windows as R5.6 places them, by copy. F10 names what the
  copy, the manifest and the key still need.
- **Rooms in place, as R5.3 writes them, lose work or give a wrong build in four cases**, each from code the design does not cite:
  - every way a line moves in a checkout refuses one that is dirty or has a session in it, and the design's replacement leaves stale files
    that a later line commits as a revert (F1);
  - a room's files hold its line's work, and after any restart the claim hands them to another line (F2);
  - a harness's own git reaches every room: for Claude Code it is held by rules and a hook that fail open, for Codex by nothing (F3);
  - a build or a gate in one room reads and writes another room's in-flight work, and no lock can hold it (F4).

  Each has a smallest change inside the owner's frame, and none reopens a choice.

**Verdict: another round, scoped to row 2 (F1 to F4).** Rows 1, 3 and 4 may go ahead (§4).

Read at main `353a67a334`, round 5's `bfdb82acd6` with a day of main merged, BUILDSTAMP1 and D214's note among it. Every line cited here was
read there. I read with git and read-only commands and changed no file but this one. I built and ran nothing, so every figure here is the
round's own. A bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and `Design` is `docs/2026-10-26-tree-pool-design.md`.

### 1. Round 5 against main today

| § | Holds? | What I found |
|---|---|---|
| R5.1, R5.2 | Not re-measured | The 93% ignored share and the kept tree's 526 MB are the round's figures |
| R5.3, the claim | **No** | The claim is a run's, but a room's files are its line's (F2). It is per home, not per checkout (F7) |
| R5.3, working in place | In part | The private index has precedents (`Workplace/ActCommits.cs:131`, `Workplace/PieceKeeper.cs:289`). Its commit skips the hooks and stages a person's skip-worktree files (F5). Today's in-root mode is a second way (F6) |
| R5.3, landing | **No** | The merge tool merges in the main checkout, not in GATEWT1's gate tree. Daoris's merge, promotion and pull each refuse a dirty checkout or one a session works in (F1) |
| R5.3, break 1 | **No** | Codex takes no rules. Claude Code's are prefix rules beside a guard that fails open (F3) |
| R5.3, breaks 2 and 3 | **No** | Nothing can lock an agent's own build. Runnable outputs and repository-wide gates cross rooms (F4) |
| R5.3, breaks 4 to 8 | In part | The generators hold their own locks (`tools/contracts.mjs:139-147`). Laneless build inputs and the central package file are missed (F4, F8). The person's acts are refused while any room is held (F7) |
| R5.4 | In part | The check misses build inputs outside a unit's folder. This repository's eight rooms meet in most of their closures (F4, F8) |
| R5.5 | Yes, as a frame | Mixed folders and path-bound installs need a word (F10) |
| R5.6 | Yes, by copy | The copy keeps the read-only mark, a removal must not follow links, nothing fills the store without a template, and under rooms it serves nothing (F10) |
| R5.7 | Yes, under the owner's no | C, the Dev Drive and §18 item 6 are answered by the owner's no (F11) |
| R5.8 | Yes, built | As above |
| §17.0 | In part | Row 2's proofs cannot pass as written (F1 to F4). Rows 1, 3 and 4 stand apart from them |
| §18 items 7, 8 | In part | Item 7 must say what the person loses in their own checkout (F7) |

### 2. Round 5's answers to round 4 (§25)

| Finding | Round 5 | Answered? |
|---|---|---|
| F1 (blocking) the commit stamp | Row 0 | **Yes**, built (`Directory.Build.props:12-16`) |
| F2 (blocking) `bin/` and `wwwroot` run without a build | The runnable-outputs class, cleared at every take | **For the fallback, yes.** Under rooms, "A room's session has outputs of its own making only" (Design:1888) does not hold (F4) |
| F3 `CopyAsync` refreshes nothing | The slot's copies deleted first | Yes |
| F4 the take's undo under the shared hold | Detach and free; the takeover deletes | Yes |
| F5 every line holds ignored paths | Declared outputs | Yes, by this repository's declaration (Design:240-242) |
| F6 npm's freshness by a file's time | The store keyed by bytes | **Partly.** A kept slot's dependencies are placed again only when their key moves (Design:1892). So an agent's `npm install --no-save` at an unchanged lockfile is still carried, unless the manifest is read at every take (F9) |
| F7 branches derived from a listed folder | Row 4's census | Yes |
| F8 nits | Each taken | Yes |

### 3. Findings

1. **Blocking. A room's branch cannot land, and the design's way to land it leaves files that a later line commits as a revert.**
   - **Where:** Design:165-169: "The merge tool already lands in a gate tree of its own (GATEWT1), so a room's branch lands as any branch
     does". Then `git reset -q <new tip> -- <landed paths>` moves the index, "only where each still equals the old tip's". Against it:
     - the merge tool merges in the main checkout and refuses unless it is clean (`tools/merge-branch.mjs:23`, `:3164-3174`, the merge at
       `:5237`). GATEWT1's tree runs the full set beside the merge (`:111-128`);
     - Daoris's own `merge` refuses a path that is not a tree it opened (`Workplace/TreeMerges.cs:61-65`) and a root that is not clean
       (`:139-145`). It merges in the line's integration tree, then fast-forwards the checkout (`:157-160`);
     - the promotion waits for any session the ledger holds in that checkout (`Workplace/IntegrationPromotes.cs:402`; `Workplace/CheckoutBeside.cs:37`,
       a session whose tree is the checkout). It refuses a checkout that is not clean (`:408-409`) and moves it only by `merge --ff-only`
       in it (`:410`): "never by a ref moved under it" (`:407`; `:23-25`);
     - a line's pull waits the same way (`Workplace/TreeSync.cs:225`).
   - **What goes wrong:**
     - Under rooms the checkout is dirty, and a session works in it, whenever any room is held. So no branch of that repository lands or
       is pulled while one is: neither a room's nor a fallback tree's.
     - The replacement moves the ref under the checkout and moves the index alone. That is right only for a path whose working file already
       holds the new content. A path the move changes that the room's session did not write keeps its old working file.
     - An example. Room A is held in place. A fallback session's branch changes `a/p` and lands: R5.4 lets a repository declare rooms for
       part of itself, and nothing keeps a fallback session out of a room's paths. The index takes the new `a/p`, and the working file
       keeps the old. A's builds read the old file. The next line in room A grows from the new tip, and its first commit (`add -A -- <room>`)
       stages the old file. Its branch reverts the landed change, and its landing lands the revert. A pull request merged on the remote
       and pulled in does the same.
   - **Smallest change:**
     - One room-aware fast-forward: a journaled act under the checkout's lock, used for every move of a line that a checkout has out
       (the merge's, the promotion's and the pull's). For each path the move changes:
       - a working file and index at the old content take the new in both, as a fast-forward writes them;
       - a path of the landing room's own, whose working file holds that session's later work, takes the new index alone;
       - any other path, above all a path in another held room, holds the whole move, said, with nothing written, as `merge --ff-only`
         refuses today.

       Then one `update-ref` that verifies the old value.
     - `CheckoutBeside` waits for a session whose room's paths the move changes, not for any session in the checkout. `TreeMerges`' tree
       and clean-root refusals are read for a branch that has no tree.
     - Row 2's proofs: a fallback branch landing into a held room's path is held, said; a landing beside a held room writes the free paths
       and moves the index; nothing reverts.

2. **Blocking. A room's files are its line's work, and the claim hands them to another line.**
   - **Where:** Design:146-150: the claim is "passed on only from a dead process by `HomeLease`'s test", and "a room whose paths hold
     changes no session made (the person's) is refused to a session". Against it:
     - in place, a line's commits go to its branch, not to the checkout's `HEAD` (Design:156-161). So the room's working files hold both the
       line's uncommitted work and every commit of it not yet landed;
     - a shutdown, an update's included, kills every session and carries it on (driving rule 41, `docs/areas/driving.md:257-258`). A parked
       session holds its tree (`:245`), and uncommitted work keeps one (`:282-283`);
     - round 4 kept a slot's holder out of the claim: "Whether a slot is held is never stored". It is read live from `TidyHolders`
       (Design:608-610; `Workplace/TidyHolders.cs:39-51`).
   - **What goes wrong:**
     - After any restart, every claim names a dead run. If the look plans another quest to that lane before the line's carry-on, the room
       goes to the new line, and that line's first commit stages the old line's work onto its own branch.
     - The old line's carry-on finds the room held and takes the fallback, where its uncommitted work is not.
     - A parked line's room is taken the same way.
     - "Changes no session made" cannot be read: the working files say nothing of who wrote them.
   - **Smallest change:**
     - The claim names the line. A dead run's claim is taken over only by that line's carry-on or resume, the look binding it to the new
       run.
     - A room is held by its line under `TidyHolders`' rules, read live, as round 4 reads a slot's holder.
     - A room is free for another line only when its paths equal the line's tip (`status --porcelain -- <room>` empty, `HEAD` at the tip).
       F1's fast-forward makes that true once the line's work lands.
     - A line that is discarded or withdrawn puts its room's files back at the tip, in the same journaled act that keeps its branch on a
       recovery ref.
     - Row 2's proofs: a restart with two quests to one lane gives the room back to the line that was carried on; a parked line's room is
       never taken.

3. **Blocking. A harness's own git and writes reach every room: Claude Code is held by rules and a hook that fail open, and Codex by
   nothing.**
   - **Where:** Design:176-178: "The session's permissions deny git's writing commands (Claude Code's deny rules, Codex's rules)". Against
     it:
     - Codex takes no rules file (`docs/areas/git-and-remotes.md:159-160`). A harness that a Claude Code rule means nothing to is handed
       nothing (`Loop/Session/SessionRules.cs:39-42`). Where Daoris needed Codex held, as a reviewer, Codex was held "by its copy alone"
       (`Participants/Domain/OpinionPosture.cs:10-17`);
     - the git area never rests what an act may do on "the harness's rules alone" (`docs/areas/git-and-remotes.md:293-294`);
     - the tree guard is "defence beside the working-directory boundary, never instead of it", and a hook that fails or times out blocks
       nothing (`Workplace/TreeGuard.cs:14-25`). In place, that boundary is the whole checkout;
     - this repository's own builder runs in the harness's own worktree (`.claude/agents/branch-worker.md:6`), which the harness makes with
       git from the checkout's `HEAD`.
   - **What goes wrong:**
     - A Codex session's `git checkout -- .`, `git restore .`, `git stash -u`, `git reset --hard` or `git clean -fd`, or a harness's own undo
       from a snapshot, removes every room's uncommitted work. It puts each room's working files back at the checkout's `HEAD`, without
       that room's commits. The next private-index commit of every other room then stages those files, and each branch reverts its own
       commits.
     - A Claude Code session reaches the same through a spelling its prefix rules miss (`git -C . stash`, a script, an npm script that runs
       git), or through a guard that timed out.
     - A habitual `git commit` lands on the checkout's own branch, which is the line, with no gate.
     - A lead in a room that dispatches a builder into the harness's own worktree grows that builder from the line, without the lead's
       in-place work.
     - The harness's own `git status` takes the index lock, so the landing's index move can fail on `index.lock`.
   - **Smallest change:**
     - In place only for an adapter that takes Daoris's rules and guard (`adapter.Can.TakesSettings`, as `OpinionPosture.Of` reads it).
       Every other harness takes the fallback until it can be held.
     - For that adapter:
       - every writing form of git is denied in each spelling, as `no-push` denies `git -* push` (`Agents/Permissions.cs:181`);
       - the classifier's sentence names Daoris's `commit`, since this repository's canon tells every agent to commit as it goes;
       - the guard is narrowed to the room's paths and refuses git's working-tree writers by their structure;
       - `GIT_OPTIONAL_LOCKS=0` is in its environment, as `HelperCapture` hands it (`Participants/Helpers/HelperCapture.cs:12`, `:90`);
       - the harness's own worktree isolation is refused in place.
     - What protects the branch, whatever ran:
       - Daoris's `commit` refuses when the room's files no longer hold its branch's own changes (a path its branch changed is back at the
         base's content, or a file it added is gone). It stops the session for the person rather than committing the revert;
       - the turn-end read of `HEAD` and the index compares entries, never bytes, since the person's editor rewrites the index's stat data.
     - Routing the harness's git through an environment `GIT_DIR` over the shared working tree is no way out. It reaches every git the
       session's tools start, a test's fixture repositories among them, where `git init` or `git commit` would write the session's own git
       directory.
     - Row 2's proofs: a Codex start for a lane takes the fallback; a Claude Code session's `git -C . stash` is refused; a room's files reset
       by hand stop its session before any commit.

4. **Blocking. A build or a gate in one room reads and writes another room's in-flight work, and no lock can hold it.**
   - **Where:** breaks 2 and 3 build a shared unit "under one lock per unit, Daoris's own" (Design:179-183); also Design:207-215, row 2's
     proof "two builds of a shared unit serialised" (:1419), and :1888. Against them:
     - the agent runs its own builds and tests in its shell. Daoris sees a build only through a tool, which is model B, later, and "never a
       shim on the path, which a script or an absolute path steps around" (Design:313-314). The lock has nothing to take;
     - the web's build writes `src/Daoris.Service/Daoris.Service.Http/wwwroot/`, a path of the service room, which the modules' `DesktopPage`
       and the HTTP host serve without a build (round 4's F2). A test project's `bin/` is written by whichever room built it last;
     - `npm run quick`, which every session runs, reads every room: the generator over the service's contracts, the type check, the
       doctrine check, the size ratchet (`package.json:16`);
     - `tools/contracts` compiles the contracts and the modules (`tools/contracts/ContractsGen.csproj:19-20`);
     - some build inputs lie outside any unit's folder:
       - the root `Directory.Build.props`, laneless;
       - `canon/canon.json`, laneless, which `src/Daoris.Desktop/Directory.Build.props:29` reads;
       - `src/Daoris.Desktop/Directory.Packages.props`, in the modules lane (`daoris.lanes.json:49`), which sets every desktop project's
         package versions (`src/Daoris.Desktop/Directory.Build.props:17`).
   - **What goes wrong:**
     - Two rooms that build one unit at once race on its `obj/`. On Windows, a test host or the window holding a unit's `bin/` open fails
       the other room's copy.
     - A room's `--no-build`, `e2e-host.mjs` or `desktop.mjs run` runs another session's in-flight build as its own. That is round 4's F2
       again, inside one checkout.
     - A gate turned red or green by another room's half-done edit is not the branch's result.
     - The check as written passes every one of these, since it reads only the sources of declared build units.
     - The landing's gates in the integration tree stay the proof, so nothing wrong lands. But the session's own results are wrong, and
       row 2's proof cannot pass.
   - **Smallest change:**
     - The check reads each room's build closure: its units, what they reference, the inputs outside their folders that `builds` lists
       (props, central package files, a version file), the sources its generators read, and the outputs its builds write into another
       room's paths.
     - Rooms whose closures meet are one claim group: one session in place per group, a second start in it taking the fallback, until
       model B builds the rooms apart. Or a repository's own build gives each session outputs of its own (for .NET, an `ArtifactsPath`
       from the session's environment), which ends the race and the crossing outputs but builds each session cold once.
     - In this repository, the contracts, `wwwroot`, the generated types, `tools/contracts`, the root props and `canon/canon.json` join the
       web, driver, modules, service, tools and docs rooms, and the CLI reads `canon/`. So nearly the whole repository is one group. R5.4
       should say what that leaves in place (one session at a time beside the records, the rest on the fallback, until B), and Design:1888
       should be corrected.
     - A session's instruction says that its repository-wide gates read other rooms' working files, and that the landing's gates are its
       proof.

5. **Not blocking, before row 2. The private-index commit.**
   - **Where:** Design:156-163; precedents at `Workplace/ActCommits.cs:131` and `Workplace/PieceKeeper.cs:289`.
   - **What goes wrong, and the change, for each:**
     - (a) `read-tree` writes no skip-worktree bit. So a file the person keeps out of their own commits with `update-index
       --skip-worktree`, a local configuration and sometimes a secret, is staged by `add -A` into a session's branch, which a `push` step
       may publish. And in a sparse checkout, every room path not on disk is staged as a delete. Refuse in place where the real index holds
       a skip-worktree or assume-unchanged entry in the room, or the checkout is sparse.
     - (b) `commit-tree` runs no `pre-commit` or `commit-msg` hook and signs nothing, where a session's `git commit` in a tree runs both.
       Run them with the private index named (`git hook run`, git 2.36, the floor §25's F8(b) already sets), or refuse in place where they
       are set.
     - (c) The lanes' paths are globs with `!` exclusions (`daoris.lanes.json`'s `web-shell`), and git's pathspecs need `:(glob)` and
       `:(exclude,glob)`. Prove the translation against `git ls-files`.
     - (d) As an act, the commit takes the checkout lock alone (`Workplace/ActRuns.cs:113`) and the one open act per checkout
       (`Workplace/ActJournal.cs:173`). So it is refused while any start holds the shared hold, while another room commits, and while the
       person acts. It writes only its own branch and its private index, so it can be one ref transaction that verifies the old value,
       journaled as a keep is, outside the checkout's exclusive hold.
     - (e) Staging the whole room ends the agent's selective commit. Let `commit` take paths within the room.

6. **Not blocking. Today's in-root mode is a second way to work in the checkout.**
   - **Where:** driving rule 12 (`docs/areas/driving.md:139-140`); `Loop/Run/Choose/CleanCheckout.cs:13-15`;
     `Participants/Domain/RoutingStanding.cs:27`. A repository whose sessions open no tree already runs one session in the root, and that
     session runs git itself and commits on the checkout's own branch.
   - **What goes wrong:** rooms put a second way beside it (D228), with the opposite rule for the agent's git. And `CleanCheckout` holds
     every in-place start whenever another room has left the checkout dirty, which under rooms is always.
   - **Smallest change:** say which way stands. The in-root mode becomes a repository with one room covering every path, under row 2's rules,
     and the old way goes in the same chunk. `CleanCheckout` reads the room's paths only.

7. **Not blocking. The person's checkout, and another home.**
   - **Where:** Design:191-192 (break 8); §18 item 7 (Design:1712-1717). `Workplace/StageActs.cs:147` and git rule 39
     (`docs/areas/git-and-remotes.md:211-212`) refuse the person's acts while a session works in the checkout. `Workplace/TreeLock.cs:43`,
     `:88-89` keep the checkout lock under the home, and the store keeps every claim there too.
   - **What goes wrong:**
     - While any room is held, the person's Stage, Commit, Switch and Stash in Daoris are refused. Their own editor's *Discard all* or
       `git switch` reaches every room's working files.
     - A second home that registers the same checkout, a development run from source among them, sees no claim and no lock.
   - **Smallest change:** §18 item 7 says the first plainly, and the Projects row says it while a room is held. A marker in the checkout's
     common git directory names the home that holds rooms there, and every other home refuses to work in place.

8. **Not blocking. The room check, this repository's rooms, and the word.**
   - The laneless paths are not documents alone: `canon/`, `.claude/`, `examples/`, `daoris.json`, `daoris.lock` and two
     `Directory.Build.props` (F4). R5.4's "docs" room holds them, and every desktop build reads some of them.
   - Joining `web-shell` and `web-settings` renames lanes that the backlog and the dispatch skill name. A room can name its lanes instead.
   - "Room" already names the intake's place (`<home>/work/rooms/intake`, `docs/areas/driving.md:266`; `Workplace/IntakeRoomPaths.cs`),
     meeting rooms (AGENTROOM1) and the doctrine's rooms. The Projects row's word needs a glossary term of its own, in both catalogues.

9. **Not blocking, before row 4. The fallback against round 4's F1 to F8.** §25's answers hold, with three changes and one consequence.
   - The manifest is read at every take, not only when the key moves (Design:1892). Otherwise round 4's F6 stands for an agent's install
     at an unchanged lockfile.
   - The order at a take: switch, check, clear the runnable outputs and the local copies, then run the hook, which writes `docs/index/`,
     an output, again.
   - Every removal at a take or a finish removes each link as a link first and never follows one (F10).
   - Row 4's landings are today's and land as today's do, but only while no room is held in that checkout (F1).

10. **Not blocking, before row 3. The classes and the store, on Windows and across stacks.**
    - **Hard links and junctions stay rejected,** for Windows' own reasons beside D51's.
      - An NTFS hard link shares one file record, its read-only mark included. The sealed mark makes every tree's file read-only, and
        clearing it in one tree clears it in the store. After that, a tool that writes in place (a postinstall patch, `patch-package`, a
        rebuild of a native module) writes every tree and the store. One file takes at most 1,024 names.
      - A junction is the store itself to every tool. An install or a cache under it writes the store, Node and TypeScript resolve modules
        by the store's real path, and a removal that follows it empties the store.
      - That last is no theory here: `git worktree remove --force` followed a builder's junction to the main checkout's `node_modules` and,
        through npm's workspace link, deleted the CLI's tracked sources (PRUNELINKS1, `docs/task-archive.md:30`;
        `tools/worktree-remove.mjs:2-5`).
    - **The copy** (R5.6) is safe, with four changes.
      - `File.Copy` copies the read-only mark. So a placement clears it in the tree, and every delete of a placed folder clears it first,
        as TREELOCAL1's deletes do.
      - Every removal of a placed folder (a re-placement, a clearing, the finish's set-aside, an eviction) first removes, as links, the
        workspace links it made again, by `tools/worktree-remove.mjs`'s rule.
      - The manifest fails on an entry file changed or gone, not on files added beside them (`node_modules/.cache`, `.vite`). Otherwise
        every run places the folder again.
      - With the owner's no to a Dev Drive there is no template, so the store fills only when a slot is made or renewed. After any lockfile
        change, every take misses and the agent installs: 72 to 93 s, against TREEDEPS1's copy of 6 to 17 s. Instead, a miss runs `prepare`
        in the taken slot before the session starts, at the line's tip with its dependency folder cleared first, and seals the result.
    - **Keys.** The lockfile and the toolchain miss the package manager's own tracked configuration (`.npmrc` and its kin) and any
      environment that changes an install (`NODE_ENV`). The proposal's conventions name them in `key`, and `prepare` runs in the gates'
      environment.
    - **Two shapes that no class holds:**
      - a path-bound install keyed by a lockfile (a pip `.venv`, a conda environment) can be neither placed nor trusted to check itself.
        Make it an intermediate with a `key`, cleared when its key moves;
      - a folder that holds intermediates and runnable outputs together (a CMake build tree, Maven's `target/`) is an output unless every
        runner builds first. The class belongs to the runner, not the folder, and the canon's conventions say so per stack.
    - **Under rooms, the store serves nothing.** The checkout's own `node_modules` belongs to every room, and a room that owns a lockfile
      reinstalls under every other room's `npm run quick`. So R5.6's "a person's install never reaches a session" (Design:282) holds on the
      fallback only, and the "under both" of D214's note needs that said.

11. **Not blocking. Nits.**
    - (a) The owner's no to a Dev Drive (D214:324-326) answers R5.7's C, its Dev Drive steps, the clone inside a slot and §18 item 6. R5.6's
      "template" and R5.7's choice should say so.
    - (b) Design:171-173 sets "eight rooms" against "the five workers the dispatch skill runs today". Those workers are the harness's own
      worktree builders (`.claude/agents/branch-worker.md:6`), which rooms do not change, and after F4 here there is about one room in
      place.
    - (c) Design:183 calls the build lock "Daoris's own", while Design:313 has builds reach Daoris only by its MCP tool. Say which rows each
      holds for.

**11 findings, 4 of them blocking.**

### 4. Verdict, and the rows that may be built first

**Verdict: another round, scoped to row 2.**
- F1: the room-aware fast-forward, for every move of a line.
- F2: the line's hold of its room.
- F3: in place only for a harness Daoris holds, and a commit that refuses a reverted room.
- F4: claim groups by build closure, and this repository's count said.

F5 to F11 are named changes that the steward can check without another read. None of the four reopens the owner's choices. Each is a
condition under which rooms in place keep their work and give a session its own build.

**The rows:**
- **Row 0** is built.
- **Row 1 first,** with F4's closure and F8's laneless inputs in its check. Nothing acts on it, and it lets the steward read this
  repository's claim groups before row 2 is cut again.
- **Rows 3 and 4 next, in either order,** once Codex's read finds nothing blocking in them, with F10's and F9's changes. Neither depends on
  rooms: row 4's landings are today's, and row 3 serves today's fresh trees too.
- **Row 2 after another round** on F1 to F4, and **row 5 after row 2's week**, as §17.0 says.

## Opus (independent), round 6

The independent Opus 5.5 reader's read of round 6, row 2 alone, at main `b347f3ab20`: 9 findings, 3 of them blocking, all in row 2. I read
the owner's three choices (D214's note of 2026-11-07) as given, and round 6's scope as §26 sets it: row 2, and rows 1, 4 and 5 where round 6
changed them.
- **The fast-forward holds.** It never writes a held group's working file, nothing it moves comes back as a revert, and both crash cases
  reconcile, with precisions (F6).
- **The claim holds.** It names the line, `TidyHolders` holds it live, only the line's carry-on takes a dead run's claim, and the release
  writes over only what a ref holds. A line that comes back after its release needs a sentence (F4).
- **Claude Code alone in place, and claim groups by build closure, hold in their frame**, leaving this repository one session in place
  beside the records, as R5.4 says.
- **Three cases still lose work or stop a line**, each from code or a tool the design does not cite:
  - a room's branch has no tree, and every tidy and delete keeps a session branch only through its tree (F1);
  - the turn's end reads `HEAD` and the index, never a working file, and the keep is one ref rewritten each turn, so a reset passes unseen
    and the next keep overwrites the last good one (F2);
  - the repository's own commit and merge hooks run over the shared working tree, where common ones rewrite every file (F3).

  Each has a small change inside the owner's frame, and none reopens a choice.

**Verdict: another round for row 2, on F1 to F3.** Rows 1, 3 and 4 may go ahead (§4).

Read at main `b347f3ab20`, which holds round 6's `70ee53cf7c`; no file round 6 cites changed after its `5643df4196`. I read with git and
read-only commands and changed no file but this one. I built and ran nothing; what I say of other tools is from their documentation. A bare
path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Tests/` is the driver's tests, and `Design` is
`docs/2026-10-26-tree-pool-design.md`.

### 1. Round 6 against main today

| § | Holds? | What I found |
|---|---|---|
| R5.3, the claim | Yes | *Spoken to* is read only by the look's `InUseAsync` (`Workplace/TidyHolders.cs:16-37`), not `:39-51` (F9). A released line's return is unsaid (F4) |
| R5.3, working in place | In part | The hooks rewrite the tree (F3); the check misses a reset to the index's content (F2) |
| R5.3, the line's move | Yes | Against `Workplace/IntegrationPromotes.cs:366-419`, `Workplace/LinePulls.cs:57-78`, `Workplace/ActSteps.cs:149-205` (F6). Its `post-merge` is F3's |
| R5.3, the record and census | In part | It misses the judgements that keep a branch because a tree has it out (F1) |
| R5.3, the harness | In part | The fallback for Codex and plugins holds. The PowerShell tool and `git config` are missed; the guard reaches `local/` (F5). A reset is unbounded (F2) |
| R5.3 builds; R5.4 groups | Yes | For builds, and this repository's count is right. A program's write outside its room is F2's |
| Break 8; §18 items 7, 9 | In part | Item 7 misses the revert a `commit -a` lands and a session's scratch (F7); item 9, the records room's writers (F8) |
| §17.0 rows 1, 4, 5; §19, §20 | Yes | Row 4 now extends a census row 2 builds, though it was to go first (F9) |

### 2. Round 6's answers to round 5 (§26)

| Finding | Round 6 | Answered? |
|---|---|---|
| F1 (blocking) a room's branch cannot land | The room-aware fast-forward | **Yes** (F6); the branch it lands is not kept from the tidy (F1) |
| F2 (blocking) the claim hands a line's files on | Claimed by line; a lossless release | **Yes** (F4) |
| F3 (blocking) a harness's own git | Claude Code alone, and protections | **In part**: a reset is unbounded (F2); spellings (F5) |
| F4 (blocking) builds cross rooms | Claim groups by closure | **Yes** for builds |
| F5 the private-index commit | Each taken | **Yes**, but running the hooks is F3's |
| F6, F10's last point, F11 (b), (c) | As §26 says | Yes |
| F7 the person, another home | Item 7; a marker | **In part** (F7; the marker leaves another home's replays, F1) |

### 3. Findings

1. **Blocking. A room's branch has no tree, and every tidy and delete keeps a session branch only through its tree.**
   - **Where:** Design:188 makes the branch "without a checkout"; Design:183-185 shields it from `TreeLandSyncs`, `TreeUpdates` and a pull's
     replays only. Against it:
     - the look's tidy takes, with no press, a branch the clean-up would clear, empty or landed: "With no tree it goes"
       (`Workplace/TreeSetAside.cs:29-32`; driving rule 48, `docs/areas/driving.md:280-281`);
     - the sweep's in-use test reads a branch's tree, so a branch with none is never in use (`Workplace/TreeSweeps.cs:338`), and the press
       judges every `daoris/` branch (`:46-54`);
     - the landed clean-up keeps a landed branch only where `git worktree list` has it out (`Workplace/LandedCleanups.cs:339-341`);
     - every delete door (`DISCARD_SESSION_BRANCH`, `SWEEP`, the look's clean-up, a run's `tidy`) passes `ContentHolds.DeleteJudgedAsync`,
       which refuses "a branch a working tree has checked out" and nothing else (`Workplace/ContentHolds.cs:313-323`; git rule 38);
     - the replays judge a branch with no tree with no in-use or dirty test (`Workplace/BranchJudging.cs:117-124`), over every `daoris/`
       branch, grown here or not (`:47-52`).
   - **What goes wrong:**
     - A take makes the branch at the group's base, which the line holds, so until the agent's first commit it is *empty*. The look's tidy
       deletes it once the end keeper marks it held, and the person's clean-up press lists it as going.
     - A room's line that lands and goes on (a quest's next part, a conversation spoken to again) has a landed branch with no tree. The next
       pull's clean-up (`Workplace/TreeSync.cs:277-279`) or the look's deletes it while its group is held.
     - Either way the next `commit` has no tip for its private index, and the hold by its files (Design:162) and the release's test
       (Design:170-172) have nothing to compare. Nothing committed is lost, but the line stops; and a `commit` that makes the branch again at
       `HEAD` commits the held paths at their base, reverting every landing into the group.
     - A second home reads no claim, so its pull replays the room's branch: round 5's F1 through another door.
   - **Smallest change:**
     - One predicate, *a branch is out*: a tree has it checked out, or a standing `room_claims` row names it, or another home's marker stands
       in the checkout, which makes every tree-less `daoris/` branch out. It is read wherever a judgement now asks the worktree list: the
       sweep's in-use test, `GoesByItself`, the landed clean-up, `DeleteJudgedAsync` before and after its delete, and the replay and update
       judgements.
     - Row 2's census adds those readers by a source scan of `Worktrees.ReadAsync`, `WorktreesAsync` and `Tree is null`.
     - Row 2's proofs: a fresh room branch survives the look's tidy and the press; a landed one survives the pull's clean-up while its group is
       held; a second home's pull leaves it.

2. **Blocking. The turn's end never reads a working file, and the keep is one ref rewritten each turn.**
   - **Where:** Design:277-284; break 8's "the keep bounds the first" (Design:324-325); §18 item 7 (Design:1876). The keep writes
     `refs/daoris/rooms/<line>`, and git keeps no reflog outside `refs/heads/`, `refs/remotes/`, `refs/notes/` and `HEAD` by default
     (`core.logAllRefUpdates`).
   - **What goes wrong:**
     - After a take, and after every fast-forward, the held paths' index equals `HEAD`. So `checkout -- .`, `restore .`, `stash -u`,
       `reset --hard`, `clean -fd` and an editor's *Discard all* change working files alone. `HEAD`, the line's moves and the held index
       entries are as Daoris left them, and the turn's end passes.
     - The session goes on, and its turn's keep overwrites the last good keep, which no ref or reflog then holds. Every uncommitted change
       made before the reset is gone. A parked group the person's *Discard all* reached loses it at its next turn the same way.
     - The commit's check catches a committed change put back at the base's content. But a reset writes the index's content, which the
       fast-forward moved to `N` for held paths (Design:228). Where the line also changed a path the branch changed, the reset puts `N`'s
       content there, the check passes, and the commit reverts the session's own committed change; the landing then takes the line's side.
     - The same blindness lets a program's write outside its room through. A formatter, a fixer or a snapshot update run over the repository
       writes another group's files, which that group's next `add -A` commits as its own, or dirties a free path, which holds every move of the
       line (Design:224-225). The guard cannot see a program's own write (`Workplace/TreeGuard.cs:16-21`).
   - **Smallest change:**
     - Each keep commit's parent is the previous keep, or the ref is written with a reflog, so every turn's state stays reachable until the
       release ends the chain.
     - Before writing the keep, the turn's end compares the room's files with the last keep: a path it held changed that is now at the index's
       or the base's content stops the session for the person. The keep is still written, as a new link.
     - The commit's check also refuses a path the branch changed that now holds the index's content.
     - The turn's end also reads the free paths against `HEAD`, and each group whose session is between turns against its last keep; a change
       there is said and holds that group's next commit for the person. R5.3 says two sessions in turns at once can still reach each other
       through a program.
     - Row 2's proof: a room reset by a script, then a turn: the session stopped at that turn's end, its earlier work on the keep's chain.

3. **Blocking. The repository's own hooks run over the shared working tree.**
   - **Where:** Design:194-196 runs `pre-commit`, `prepare-commit-msg`, `commit-msg` and `post-commit` with the private index named;
     Design:232 runs `post-merge` after the move; break 8 allows the person's Commit of free paths (Design:320-322), an act whose hooks run
     and are never skipped (`Tests/Workplace/CheckoutWritersCensusTests.cs:33`; git rule 37).
   - **What goes wrong:** a hook runs in the checkout, so it meets every room, and common ones write the whole tree:
     - the `pre-commit` framework saves every working change against the index it reads as a patch, runs `git checkout -- .`, runs its hooks,
       and applies the patch back. For the hooks' length every other room's work is off disk, under its session's build. A write made
       meanwhile conflicts with the patch, and the framework checks out again and loses it. A commit act ended past its bound never applies
       the patch, which stays in the person's cache, where Daoris does not look;
     - `lint-staged` restores a failed run by `git reset --hard HEAD` and a stash apply.
     - `post-merge` hooks commonly install dependencies, which writes the folder every group reads (break 4) under running sessions.
     - This repository's hooks are `post-checkout` and `post-merge`, writing the ignored `docs/index/` (`tools/index-hook.mjs:7-11`), so its
       own proofs would not show it.
   - **Smallest change:** in place only where the hooks path holds no `pre-commit` or `post-merge` hook, or where the gates file declares each
     set hook as writing only its own outputs, as this repository would its index hook; otherwise the start takes the fallback, said.
     `prepare-commit-msg` and `commit-msg` write the message alone and may run. The person's Commit, while a group is held, follows the same
     rule. Row 2's proof: a `pre-commit` hook that rewrites the tree refusing in place.

4. **Not blocking, before row 2. A line that comes back after its room's release.**
   - **Where:** Design:170-172 releases a room once neither hold stands, and row 2 places "a start whose work lies in one free group" in the
     checkout. The writer's holders stop counting a completed part (`Workplace/TidyHolders.cs:54-59`), and *spoken to* lapses in an hour.
   - **What goes wrong:** a quest's next part, or a conversation spoken to again, goes on in a line whose branch holds commits `HEAD` does
     not. Taken again at `HEAD`, its room lacks its own commits. Its first commit is stopped where its changes read back at the base, and
     reverts its own change where the line also moved that path.
   - **Smallest change:** say it. Such a line goes on as a gone tree does, from its branch in a fresh tree or a kept slot (from the brief, its
     conversation being filed under the checkout), or its take first writes its branch's room paths in a journaled act. Row 2's proof: a
     quest's second part after its room's release.

5. **Not blocking, before row 2. The harness's denials and the guard.**
   - The deny forms are `Bash(…)` alone. A driven session on Windows may use the PowerShell tool, which the guard judges
     (`Agents/Domain/TreeGuardHook.cs:10`) and no rule names (`Setup/SetupPress.cs:17-18`). Name its forms, or deny it in place.
   - An alias, a path to git, a quoted subcommand and a script are the classifier's, as `no-push` says of itself
     (`Agents/Permissions.cs:176-178`). "In every spelling" should say so; F2's turn's end then catches them.
   - `git config` is not on the list, but a `core.hooksPath`, `core.fsmonitor` or alias the agent sets runs its program from Daoris's own
     `git hook run`, every `status` and the person's git. Deny its writing forms, and run the acts' hooks from the path read at the take.
   - The guard allows "what git ignores" anywhere (Design:268-269): the person's `local/`, whose `sensitive-patterns.txt` the sensitive gate
     reads, and an ignored `.claude/settings.local.json` that every later session in place reads. Allow ignored writes only under the room's
     paths and the declared output and scratch classes.
   - `TakesSettings` is a door's declaration (`Agents/Contract/DoorAbilities.cs:47`), which a plugin may make with no Claude Code rule
     language or hook. Admit in place only where `SessionRules.Hand` wrote the rules and the guard.
   - A lead may send a builder into the harness's worktree (Design:272-274), but has no door to bring its commits into the room: `merge`
     and `cherry-pick` are denied. Name one.

6. **Not blocking. The fast-forward's precisions.**
   - The reconciliation writes a free path back from `O` only where it still holds `N`'s bytes, and an index entry only where it still holds
     the act's, as the act engine's steps compare first (`Workplace/ActJournal.cs:61-62`; `Workplace/ActSteps.cs:162-164`).
   - Free paths are written through git's checkout (`checkout-index`, `restore --worktree --source`), so smudge filters and line endings
     apply.
   - The pull is judged dirty before it is tried (`Workplace/LinePulls.cs:40-43`); that read leaves the held paths out too.
   - With every path free the room-aware move is a fast-forward: keep it as the one way, not beside `merge --ff-only` (D228).
   - The line-keyed commit act needs its own unique key and reconciler: the journal's one open act and a writer's reconciler are per checkout
     (`Workplace/ActJournal.cs:173-174`; `Workplace/IntegrationPromotes.cs:373`).

7. **Not blocking, before the owner answers §18 item 7. The person's checkout.**
   - With the held paths' index at `N` and their files at the base, the person's `commit -a`, or an editor's *stage all* on an empty stage,
     lands on the line, ungated, the reverse of every landing into a held group. The turn's end stops the sessions, and nothing undoes the
     line. Item 7 should say so beside "commits the rooms' work".
   - A session's scratch stays in the person's checkout (`_fixtures/` here), where nothing can tell it from theirs or remove it. R5.5's
     scratch row (Design:376) and the owner's decision that a finished session's scratch goes with its tree (D195) do not hold in place.
     Item 7 should ask it.

8. **Not blocking. This repository.**
   - A change here moves its task to the archive with its code (the task-lifecycle rule), which a code session in place cannot do:
     `TASKS.md` and `docs/task-archive.md` are the records room's. Either the records room joins the code group, or each change sends it a
     quest. §18 item 9 should say which.
   - The rooms are read from `daoris.lanes.json`, a records path, which a session in place may edit while groups are held. Read the
     declaration at `HEAD`, never from the working file.

9. **Not blocking. Rows and nits.**
   - Row 4 now "extends row 2's census" (Design:1573, :2115), but rows 3 and 4 were to go before row 2. Whichever of rows 2 and 4 lands
     first builds the census and `WhereIs(line)`.
   - The claim's holder read takes `InUseAsync`'s *spoken to* beside `HoldersAsync`.
   - A keep ref per line, never dropped, grows by one a line. The release ends it, and the recovery retention takes it.

**9 findings, 3 of them blocking.**

### 4. Verdict, and the rows that may be built first

**Verdict: another round for row 2, on F1 to F3.**
- F1: one *out* predicate for a branch, read by every tidy, delete and replay, and their census.
- F2: a chained keep, a turn's end that reads the room's files, and a commit's check against the index's content.
- F3: in place only where the repository's commit and merge hooks are absent or declared safe.

F4 to F9 are named changes the steward can check without another read. Round 6's own mechanics hold: the fast-forward, the claim by line,
Claude Code alone and the claim groups. None of the three reopens the owner's choices.

**The rows:**
- **Row 0** is built.
- **Row 1 may go ahead** as round 6 writes it. Nothing acts on it.
- **Rows 3 and 4 may go ahead, in either order,** once Codex's read finds nothing blocking in them, row 4 building the census if it lands
  before row 2 (F9).
- **Row 2 after another round** on F1 to F3, and **row 5 after row 2's week**.

## Moved from the design at round 4

Round 4 moved the design's own superseded round text here, so the design does not grow: rounds 1 to 3's standings and *Read at* paragraphs,
and its §22 and §23, each as its round wrote it, headings set one level down. A section number in them is the design's as that round numbered
it; round 3's body text, which round 4 replaces in place, is the design at round 3's commit.

### The standings of rounds 1 to 3

**Round 3's standing (proposed under D191, D214):** **Both makers read round 2** at `d595cabd6` (`docs/2026-10-26-tree-pool-reads.md`,
*Codex, round 2* and *Opus (foundation), round 2*). Codex asked another round: 15 findings, 8 blocking (its 2.1 to 2.6, 3.2 and 5.1), its
round-1 1.2, 2.2, 2.3 and 2.6 open in the cases they name. The independent Opus reader asked accept after named changes: 15 findings, 1
blocking (F1), F2, F3, F4, F5, F7 and F11 to land before row b, and it found no case in which the pool loses work. **Round 3 is written by
Claude Opus 5.5 from main at `3dd134cf3`**, which carries FOUNDATION1's row p and LIMITSWITCH3b; every finding of both was checked against the
code there (§23.3) and is answered in §23, one row each, and each changed passage of §0 to §20 is marked *(C2-n.m)* or *(O2-Fn)*. Where the reads
meet, the stricter is taken and one change meets both. **What crosses a move** (O2-F1 with C2-1.2 and C2-5.1): the first build carries
`node_modules` alone, its workspace links proved against the repository's own workspace declarations *(C2-2.5)*; a finished line's other
ignored folders, its scratch among them, go aside with its record by KEEPSIZE1b's decided rule, which today already sets a finished tree aside
whole; and a line that may come back keeps its slot while it holds any other ignored folder, since **taking that slot would delete a live
line's build folders, which no decision of the owner's allows: that is put to the owner (§18 item 4), and until the owner answers nothing but
`node_modules`' own caches is ever removed at a take**. **The finish** (C2-2.4 with O2-F5, O2-F6): KEEPSIZE1b's own set-aside of everything the
slot does not keep, its manifest the record's snapshot written before anything moves or goes, `line_records` withdrawn as a second home for
it. **The links** (C2-2.1): made again at the parking name to where they will stand, before the slot is placed, so a placed slot never holds
a link into another path. **The open** (C2-2.2): made under FOUNDATION1's keyed open, so its outcome is known whether or not its record still
runs. **The git processes** (C2-2.3): each claim's own named job, opened and emptied by the takeover, and the pool offered only where that job
holds. **The holders** (C2-3.2, O2-F2, O2-F7, O2-F11): a served build's admission, a landing's hold, visits that hold back only a move, and a
line's start held while a claim names its path. **Both makers read round 3 before any of it is accepted, and nothing is built until then**,
nor in any case before FOUNDATION1's row mod-x (D212 §14.6), which has not landed (D212's notes record mod-i to mod-vii built, with Part B's
z-i, z-iii, a-i, a-ii, a-iii, b, c, p and u); the rows marked ⚖ are built only after both makers' reads find nothing blocking in them. Nothing
here is built, spends an account, signs in, publishes or deletes.

**Round 2's standing, as it was written (proposed under D191, D214):** **Both makers read round 1** at `3f55eb1e5` (`docs/2026-10-26-tree-pool-reads.md`,
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

### The *Read at* paragraphs of rounds 1 to 3

**Read at** main `3dd134cf3` for round 3. Since round 2's `f92e95415`, FOUNDATION1's row p moved `Core/Sessions/SessionLedger.cs` (8 lines on
past its line 203, 17 past 940, 23 past 975), `Core/Sessions/Sessions.cs` (42 on past its line 685), `Core/Sessions/HelperDesk.cs` (7 on past
its line 35), `Service/ServiceClient.cs` (20 on past its line 456) and `Http/Program.cs` (7 on past its line 467); LIMITSWITCH3b moved
`Loop/Driver.cs`, `Loop/DriverContinuations.cs` and `Conversations/Chat/ChatRunner.cs` only below every line cited. So every line of §1 to §20
that moved is re-cited in place (`SessionLedger.cs:320-324` is `:328-332`, `:1602-1616` `:1625-1639`, `:1604-1607` `:1627-1630`, `:1833`
`:1856`; `Sessions.cs:1147-1161` `:1189-1203`; `HelperDesk.cs:89-94` `:96-101`; `ServiceClient.cs:2222-2249` `:2242-2269`, `:2245` `:2265`;
`Http/Program.cs:1435-1438` `:1442-1445`, `:1439` `:1446`), §0.2 and §22 keeping the lines as their rounds read them; every other file cited is
unchanged since `f92e95415`, and every line round 3 adds was read at `3dd134cf3` (§23.3). Round 2 read as follows.

**Round 2 read at** main `f92e95415`. Since round 1's `f8fbb1bb7`, LIMITSWITCH3 shifted `Loop/Driver.cs` (5 lines on past its line 981,
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

### 22. Round 2: both reads of round 1 answered

Both makers read round 1 at `3f55eb1e5`, verbatim in `docs/2026-10-26-tree-pool-reads.md`. Codex asked another round: 18 findings, 12 blocking
(1.1, 1.2, 2.1 to 2.7, 4.1, 4.3, 4.4), and nothing for the owner beyond §18. The independent Opus reader asked another round: 15 findings, 4
blocking (F1 to F4), keeping the central choice and scoping the round to §2.3 to §2.4, §5.2 to §5.3, §11, §14 and row b. **Every blocking
finding of both is taken**, each met by a contract a reader can check line by line and a row that proves it; every non-blocking finding is taken
too. Where the reads meet, one change meets both, the stricter taken: the path invariant during a move (C1-2.1 with O1-F1, O1-F2, C1-1.1 and
O1-F6); the check after the switch (C1-2.4 with O1-F4); the crash protocol (C1-2.2 with O1-F5 and O1-F10); the finished record (C1-2.3 with
O1-F7); what crosses a move (O1-F3 with C1-1.4 and C1-2.8); the retired slot's slim (C1-1.3 with O1-F8); a finished line coming back (C1-2.7
with O1-F9); the terminal's hold (C1-2.5 with O1-F11, Codex's admission being the stricter); the default's clamp and the cap's lowering (C1-3.2
with O1-F15). No finding is answered rather than taken.

#### 22.1 Codex's read (18 findings, 12 blocking)

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

#### 22.2 The independent Opus reader's read (15 findings, 4 blocking)

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

#### 22.3 The code, checked

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

### 23. Round 3: both reads of round 2 answered

Both makers read round 2 at `d595cabd6`, verbatim in `docs/2026-10-26-tree-pool-reads.md`. Codex asked another round: 15 findings, 8 blocking
(2.1 to 2.6, 3.2 and 5.1), and four of its round-1 findings open in the cases its §0 names (1.2 as 2.5, 2.2 as 2.2 and 2.3, 2.3 as 2.4, 2.6 as
2.6). The independent Opus reader asked accept after named changes: 15 findings, 1 blocking (F1), F2, F3, F4, F5, F7 and F11 to land before
row b, and it found no case in which the pool loses work. **Every finding of both is taken**, each blocking one met by a contract a reader can
check line by line and the row that proves it. Where the reads meet, one change meets both: **what crosses a move** (O2-F1 with C2-1.2 and
C2-5.1), `node_modules` alone in the first build, a finished line's other ignored folders set aside with its record by today's rule, and a live
line's build folders the owner's to release (§18 item 4); **the finish** (C2-2.4 with O2-F5, O2-F6 and O1-F7's store home), KEEPSIZE1b's own
set-aside of what the slot does not keep, its manifest written before anything moves or goes; **the links** (C2-2.1 with C2-2.5), proved by
npm's workspace declarations and made again before placement; **the holders** (C2-3.2 with O2-F2, O2-F7 and O2-F11). **On C2-5.1's question,
the decision is that dropping a live line's build folders at a take is a removal D191 point 3 gives the owner**, since D195 point 3 lets the
slim take unasked only a `node_modules` beside a tracked `package.json` and keeps `bin/` from it for good, and the brief §2 reads *anything that
loses the owner's data* as removing a folder or resetting a tree that holds work; while **setting aside a finished line's build folders with
its record is not a new removal**, since today's tidy sets a finished tree aside whole and deletes it an hour after its record under the owner's
decision of 2026-10-24 (D195:503-505, :511-519), and the finish removes nothing that rule does not. So the first build carries `node_modules`
alone and removes nothing else at a take until the owner answers.

#### 23.1 Codex's read (15 findings, 8 blocking)

| Finding | Verdict | Where it went, and the row that proves it |
|---|---|---|
| §0: round-1 1.2 open (location is not provenance) | **Taken** as 2.5 below | — |
| §0: round-1 2.2 open (open reconciliation, positive child quiescence) | **Taken** as 2.2 and 2.3 below | — |
| §0: round-1 2.3 open (complete finish inputs composed after deletion) | **Taken** as 2.4 below | — |
| §0: round-1 2.6 open (the working snapshot misses force-added ignored files) | **Taken** as 2.6 below | — |
| 1.1 The last guards refuse every lock, the pool's own included (`TreeSetAside.cs:84-87`) | **Taken** | §2.4 *The pool's own judgement of the lock*, §5.2 steps 2 and 3: the pool reads the lock by its own rules, exactly its free reason, a released session lock or none, every other refusing, and asks the last guards apart from their lock test. Row b (*the lock*) |
| 1.2 Carried outputs run: a kept `dist/cli.js` runs before the sources (`src/Daoris.Cli/bin/daoris.mjs:12-13`; `tools/desktop-publish.mjs:1260-1262`) | **Taken**, with O2-F1 | §11: no output is carried in the first build; §0.2's round-3 table withdraws round 2's claim. Row b (*what crosses a move*: the CLI entry runs the new line's sources) |
| 2.1 (blocking) A placed slot's links across a crash | **Taken** | §5.3 step 6: the links are made again at the park to lead into `to`, by TREEDEPS1's own maker for where a link will stand (`TreeDependencies.cs:303-322`, `:326-330`), **before** the placement, and the last check confirms every link leads into `to`; §2.3's takeover and §14 item 5 say a slot standing at `to` after a crash holds no link into another path. Row b (*the path invariant*: a return killed after placement); §20 names the one platform fact it rests on, with the fallback |
| 2.2 (blocking) The record's open is not identifiable before it opens | **Taken** | §2.3: the claim's `opens` key is written at step 1, before the open; §5.3 step 11: the open is made under FOUNDATION1's keyed open (row 1d-i, `session_opens`, foundation design `:846`, `:1711`), which keeps the open's outcome in its own exclusive step; the takeover reads that row, never the record's liveness. Rows d and later wait on 1d-i (§17), row d extending the keyed open to a pool's opens where 1d-i has not. Row b (*the open's answer lost*, the record ended by the orphan sweep before the takeover) |
| 2.3 (blocking) Executor death is substituted for child quiescence | **Taken** | §2.3, §12: the claim's job is named and its name written before its first git process; the takeover opens it by that name (`ProcessJob.cs:146-149`) and ends it and waits until it holds no process (`:160-168`, `:196`) before reading anything; where that cannot be proved (off Windows, `:15-18`) the pool is not offered (§2.2). Row b (a git process outliving its claim's process) |
| 2.4 (blocking) Finish facts must survive before the branch goes; a branch-keyed finish for a line whose slot serves another | **Taken**, with O2-F5, O2-F6 | §10: the finish is KEEPSIZE1b's own set-aside of what the slot does not keep; its schema-2 manifest, the record's whole snapshot (`TreeSweeps.cs:221-230`), is written at step 3, before anything moves and before the branch goes, while `SessionBranches` still holds its facts (`Trees/Branches/SessionBranches.cs:180-202`); a line whose slot serves another is finished by the manifest's own steps, keyed by its identity, with no slot claim, its tree facts from `line_ends`; `line_records` withdrawn. Row b (*the finish*) |
| 2.5 (blocking) A link's location does not establish its owner | **Taken** | §5.2 step 3: a link is carried only where npm's workspace rule accounts for it: at `node_modules/<name>` beside a tracked `package.json` whose `workspaces` declare the folder it leads to, inside the slot, that folder's `package.json` named `<name>`; any other link holds the slot and is never retargeted. Row b (*no work lost*: a person's junction inside `node_modules` leading into the slot) |
| 2.6 (blocking, row i) The working snapshot misses a force-added ignored file's working version | **Taken** | §5.5 step 2: the private index is seeded from the real index's tree `I`, never from `HEAD`, so a path tracked though ignored stays tracked and `add -A` records its working version. Row i (a force-added ignored path staged at one version and edited to another) |
| 2.7 `started.tree-past-kept` overclaims | **Taken** | §15: *No kept tree was free to hand on* |
| 3.1 The central choice holds, conditionally | **Agreed** | Row b's note read before row d (§17) |
| 3.2 (blocking) X's served-build admission was dropped | **Taken** | §2.3, §7, §3.2: a `served` visit in `tree_visits`, taken through a call down into Workplace before any serving begins (the set-up's first showing and *Show it again*, `ReviewDesk.cs:231-236`), refused while a claim that moves the slot is in flight, held until serving stops, read by every process; a dead shell's taken over. Row b (*holders across processes*), row d |
| 4.1 Rows a to c sound; b's proofs need the counterexamples | **Taken** | Row b adds the force-added file (row i), the CLI entry, the served-build race and the pre-delete snapshot; row a settles shapes only |
| 4.2 Row e's installed proof comes too early | **Taken** | The installed resume moves to the head of row g, after row f's doors and under §18 items 2 and 5; row e's proofs stay behind the test's seam |
| 4.3 The default path skips the submodule refusal | **Taken** | §2.2, §5.1, §8: every take reads whether the start it switches to tracks a submodule, a pool switched on by the default included. Row h (a repository with a submodule under the default) |
| 5.1 (blocking) The build folders a move drops are a deletion the owner has not allowed | **Taken** | Decided as §23's opening says: a live line's build folders are the owner's (§18 item 4, row j), a finished line's go with its record by today's rule (§10, §11); the first build carries `node_modules` alone and removes nothing else at a take. Row b (*no work lost*: a returnable line holding `bin/`, `obj/`, `dist/` or a `.venv` never taken), row j on the owner's yes |

#### 23.2 The independent Opus reader's read (15 findings, 1 blocking)

| Finding | Verdict | Where it went, and the row that proves it |
|---|---|---|
| F1 (blocking) `build`, `out` and `bin` carried without proof | **Taken**, with C2-1.2 and C2-5.1 | §11: `node_modules` alone carried in the first build; any other kind only by a dated note on row b's proof, which runs its outputs without a rebuild (`dotnet test --no-build`, a CMake tree) against another tree at the old path. Row b |
| F2 A landing under way holds nothing | **Taken** | §7, §5.2 step 1: the tree's landing hold (`TreeLands.cs:420`; `LandingHolds.cs:9-14`, `:61`) read under the exclusive hold, a take passing over a slot whose landing hold is held. Row b (an Accept racing a take) |
| F3 A refused placement, or a refused rename back, is unspecified | **Taken** | §2.3 *A refusal in a living process*, §5.3 step 9, §5.4: the park path runs at once, no branch is left checked out at the park. Row b (*placement refused*) |
| F4 A parked or stuck slot has no recorded tip | **Taken** | §2.3: `kept_trees.tip`, written at `parked` and at a failed check; §5.2 step 6 compares a parked or stuck slot's HEAD with it. Row b (*a parked slot's HEAD moved*) |
| F5 §10's opening contradicts §6 | **Taken** | §10, §3.2: a free slot at its line's path is read as that line's tree, `Holds` and `Worked` as today, so a no-output line's slot stays its conversation's place. Row b (*the finish*) |
| F6 A finished line's carried scratch | **Taken**, its first option | §10, §11: the finish sets the line's scratch aside with its record and it goes an hour after the record by today's rule; nothing of it is carried; the D51 note and `started.tree-kept` say a tree holds only its npm dependencies; round 2's row j and §18 item 4 withdrawn for it; row g counts how often a returnable line's scratch keeps its slot |
| F7 A visit refuses the line's own claim in place | **Taken** | §2.3, §7: a visit holds back only a claim that moves the slot; §3.3: a return only where the line's path holds no tree git lists, and step 9 moves aside only a folder git does not list. Row b, row d |
| F8 An upward edge from Settings to Workplace | **Taken** | §0.3, §2.5, §8: `KeptTreesPort`, declared by Settings and answered by Workplace by a module initializer in Workplace's folder, D212's three bounds kept. Row a (unset), row f (`StructureTests`) |
| F9 Git hooks at the park | **Taken** | §5.3 step 4, §12: every git step of the pool runs with `-c core.hooksPath=` an empty folder of the home's; `started.tree-hook-skipped` says where a repository's `post-checkout` hook did not run. Row b (*hooks*) |
| F10 Contention on the exclusive hold | **Taken** | §2.3: under the hold only what a rename makes stale is read again; §5.1 choice 5: a person's conversation that cannot have the hold opens a tree of its own. Row d, row g (starts held behind another's hold counted) |
| F11 A pooled line's start while a claim names its path | **Taken** | §2.3, §3.3: such a start is held for the look, and the look runs takeovers before it plans starts. Row b (*a pooled line's start in each crash state*) |
| F12 Lines stale at `d595cabd6` from row p | **Taken** | *Read at* re-cites them in place |
| F13 Row d's rehearsal cannot be built as written | **Taken** | §14 *Proof*: the last release before row d checked out from the repository's own objects into `_fixtures/` with no network and published there; one environment variable, named in row d, the only way real processes switch the pool on before row f. Row d |
| F14 Part B dependencies unnamed | **Taken** | §0.3, row a: §13.4's dead-run takeover over `store_runs` built once for this design and row f-i; §7: the standing move read from the boxes and `moves` both from row d on |
| F15 Nits: (a) a named prefix for drops, only folders ignored whole; (b) choice 4's making phases; (c) a submodule gained after opt-in; (d) the opened moment `kept_trees.since`; (e) gate a take on the start's confirmation; (f) `tree.kept-stuck`'s Chinese | **Taken**, each | (a) §5.3 steps 3 and 5, §2.3's park path: `.daoris-dropping-<n>`, finished by the takeover; §5.2 step 5 reads ignored-whole folders as the slim does (`TreeSlims.cs:19-22`); (b) §2.3's `make` phases; (c) §2.2, §9: such a slot leaves the pool and its line is finished by today's set-aside; (d) §3.2, row c; (e) §2.3, §5.1, §14; (f) §15. Rows b, c, d, f |
| §3: item 1's *no* implies an unspecified waiting design | **Taken** | §18 item 1 says a no means another round |
| §3: row g mostly measures choice 2 in this repository | **Taken** | §5.1 choice 3, row g |

#### 23.3 The code, checked

Every line both reads of round 2 cite was read at `3dd134cf3`, and holds as cited: `TreeSetAside.cs:18-20`, `:27-30`, `:84-87`, `:116-124`,
`:127-132`, `:243-249`; `src/Daoris.Cli/bin/daoris.mjs:12-13`; `tools/desktop-publish.mjs:1260-1262`; `TreeReturns.cs:56-58`, `:81-85`,
`:94-98`; `ProcessJob.cs:15-18`, `:23-31`, `:77-83`, `:146-149`, `:160-168`, `:196`; `TreeSweeps.cs:221-230`; `SessionBranches.cs:180-202`;
`TreeDependencies.cs:24-28`, `:44-45`, `:279-299`, `:303-322`, `:326-330`; `ReviewDesk.cs:226-236`; `BranchTidying.cs:734-743`;
`TreeLands.cs:420`, `:435`; `Trees/Landing/LandingHolds.cs:9-14`, `:61`; `Settings/DriverChoicesCommand.cs:427-438`;
`Agents/Permissions/SpawnServers.cs:28`; `TreeSlims.cs:3-12`, `:19-22`; `SlimManifest.cs:13-14`, `:17-20`; `Daoris.Service.Http.Tests.csproj:16`
(the testing host); `.gitignore:3-21` (this repository tracks `src/Daoris.Cli/bin/daoris.mjs` and ignores `dist/`); D195:21-29, :503-505,
:511-523; the foundation design's `:846` and `:1711` (row 1d-i, `session_opens`, not built: D212's notes record no row 1d-i); the row-p lines
*Read at* re-cites (`SessionLedger.cs:328-332`, `:1625-1639`, `:1856`; `Sessions.cs:1189-1203`; `HelperDesk.cs:96-101`;
`ServiceClient.cs:2242-2269`, `:2265`; `Http/Program.cs:1442-1446`). The lines round 3 adds were read there too. Claims that rest on other
programs and were not run: that a junction can be made to a folder not yet standing; that a job object stays open by its name while a process
in it runs; npm's workspace links' names; that the testing host compiles a content root into a test assembly; that a CMake tree names its
paths absolutely; and git's nesting, `gitdir` order and `core.hooksPath` behaviour. Round 3 takes each as the reads and the documentation
state it, keeps a fallback where a design rests on one (§20), and row b proves each before row d.


## Codex, round 6

Codex's read of rows 2 to 4 as round 6 left them, at `b347f3ab20`, run by the steward from outside the install, read-only, without seeing the independent Opus reader's round-6 read; verbatim but for its headings set down.

**Verdict**

- **Row 2: another round.** Round 6 fixes the original takeover and stale-room landing cases, but recovery, hooks, start bases and cross-home writers still admit work loss or incorrect session branches.
- **Row 3: another round.** The proposed key does not identify everything that can change a prepared install.
- **Row 4: another round, narrowly on class validity.** The slot machinery looks sound; carrying path-bound installs without keyed invalidation can give the next line the wrong environment.

Reviewed at `b347f3ab20bc`. No files changed, builds run or nested `local/` folders read. Measurements were not reproduced.

Below, **Design** means `docs/2026-10-26-tree-pool-design.md`; code paths beginning `Workplace/`, `Loop/` or `Participants/` are relative to `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

1. **Blocking — the fast-forward’s crash rollback can erase subsequent work.**

   **Where:** R5.3, Design:227–235. Existing safeguards: `Workplace/ActFiles.cs:50`, `:77–83`; `Workplace/ActSteps.cs:162–164`.

   **Sequence:** The act writes a free file from `N`, then crashes before moving the ref. While Daoris is down, the person edits that file. The specified reconciliation writes it back from `O`, destroying the new edit. Restoring the entire saved index similarly risks removing staging performed after the crash. The checkout lock cannot prevent these external actions.

   Today’s file and index steps compare their expected state before replacing anything; simply calling the new operation journaled does not supply that protection.

   **Smallest change:** Journal each affected path’s before and after state, including absence, mode and link identity. Roll back only an unchanged after-state; leave anything else held for the person. Restore only affected index entries, with the same comparison under `index.lock`. Test edits and staging made **between crash and reconciliation**, not only crashes during an otherwise untouched act.

2. **Blocking — a private index does not confine commit hooks.**

   **Where:** R5.3, Design:191–206; §26 F5, Design:2105. Code: `Workplace/ActCommits.cs:14`, `:71–80`.

   **Sequence:** Rooms A and B contain changes. A’s private index starts from A’s branch and stages A’s selected files. Its `pre-commit` hook runs `git add -A`, inheriting that private index but seeing the shared working directory. B’s changes enter A’s index. `write-tree`, `commit-tree` and the branch CAS all succeed: B’s work is now on A’s line.

   Existing commit code explicitly accommodates hooks changing the staged tree; the proposed operation has no final ownership check after those hooks.

   **Smallest change:** Immediately before creating the commit, require the final tree’s differences from the captured branch tip to lie entirely within the explicitly selected, owned paths. Repeat the revert check against that final tree. Refuse a hook-expanded commit instead of silently trimming it. Use per-line message files as well, so concurrent hooks share neither index nor message state.

3. **Blocking — working-tree-only restores evade the checks, and the recovery ref lacks a retention rule.**

   **Where:** R5.3, Design:204–206, :274–283; §18 item 7, Design:1870–1876. Code: `Workplace/TreeGuard.cs:19–25`; recovery reflog handling at `Workplace/ActSteps.cs:95–99`.

   **Sequence:** A claims at `O`, commits `x=A`, and the checkout subsequently advances to `N`, where `x=N`. An external `git restore --worktree x` restores the real index’s `N` content without changing refs or index entries. The turn-end checks pass. The commit check also passes because `N` is neither the original base content nor a missing added file. A’s next commit replaces its own work with `N`.

   Separately, several turns of **uncommitted** work can exist only in the room recovery snapshot. A working-tree-only restore leaves the checked metadata unchanged; the next keep can replace that snapshot with the restored state. The design requires neither retained generations nor parentage preserving previous snapshots. Existing automatic reflog handling covers discarded refs, not this new prefix.

   **Smallest change:** Require explicit acknowledgment when branch-changed paths revert to a checkout/index version introduced by a line move, not merely the original base. Keep immutable, addressable turn checkpoints with a stated retention rule; a new keep must not make the previous checkpoint unreachable. Otherwise the claimed one-turn loss bound does not hold.

4. **Blocking — room eligibility ignores the start’s required source commit.**

   **Where:** R5.3, Design:158–159, :167–188; row 2, Design:1571. Code: `Loop/Run/Tree/GrowTree.cs:110–120`; `Workplace/TreeOpening.cs:65`, `:70–84`.

   **Sequence:** A chain’s next step must verify its predecessor’s unmerged branch. Its requested room is free, so row 2 creates its branch at checkout `HEAD`. It verifies the canonical checkout instead of the predecessor’s result. The same problem affects further parts whose work landed on another branch. Today’s opening explicitly preserves these source choices.

   A clean registered checkout can also be on a branch other than the configured line; cleanliness alone does not make its `HEAD` the correct start.

   **Smallest change:** Resolve the existing opening’s exact source commit first. Offer a room only when that source equals the checkout’s eligible line tip and the required closure is available there. Otherwise take the kept-slot fallback. Preserve the source and `GrownBranch` facts.

5. **Blocking — the other-home marker excludes starts, but not writers.**

   **Where:** R5.3, Design:325–327; §26 F7, Design:2107. Code: `Workplace/TreeLock.cs:43`, `:88–89`; `Workplace/LinePulls.cs:60–77`; `Workplace/IntegrationPromotes.cs:402–410`.

   **Sequence:** Home A holds a room whose files are still clean. Home B already registers the checkout. B starts no session, so it violates no proposed marker rule. Its pull or promotion sees a clean checkout, takes its independent home-local lock and fast-forwards normally. That writes A’s held files instead of leaving them at A’s base. A can edit, build or commit before its turn-end detection runs.

   **Smallest change:** Every Daoris writer touching the checkout’s files, index or checked-out line must consult the common-directory marker. A foreign owner means refuse or route through that owner’s room-aware operation. Make marker acquisition atomic with admission, and account for already-running foreign in-root sessions before granting the first room.

6. **Blocking — a held claim needs an immutable ownership footprint.**

   **Where:** R5.3, Design:158, :223–225; R5.4, Design:346–350; row 1, Design:1570. Code: `daoris.lanes.json:88`; the present disk reader at `tools/merge-branch.mjs:3006–3020`.

   **Sequence:** A holds group G. Another session changes the lanes declaration, which here belongs to the separate records room. Alternatively, a fallback change lands a revised declaration. A subsequent move reads the new groups and classifies one of A’s formerly held paths as free. Step 3 can overwrite A’s working file. Merging or renaming groups can also make admission overlook an existing claim.

   The claim records a base, but the design never requires ownership to be resolved from the declaration at that base rather than the current declaration.

   **Smallest change:** Bind each claim to a committed declaration identity and its immutable ownership/closure patterns. Moves, commits and release use that footprint. Admit starts against the union of outstanding footprints; activate an incompatible partition only once affected claims are released. This is the necessary row-1 consequence of row 2.

7. **Not blocking — extend the reader census to capture semantics, not just location.**

   **Where:** R5.3, Design:178–185; §3.2, Design:889–901. Code: `Participants/Helpers/HelperCapture.cs:99–104`; `Loop/Helpers/HelperSettling.cs:37–44`; `Loop/Helpers/HelperTurn.cs:38–47`.

   **Sequence:** Merely translating a room line’s path into the checkout makes today’s helper capture use checkout `HEAD` and whole-checkout status. A helper then receives other rooms’ changes and lagging-base files as the asker’s work.

   **Smallest change:** Add this reader explicitly to the census. Capture from the asker’s branch tip, overlay only its owned working changes, and preserve branch versions elsewhere. Row 2 already requires readers not to mistake checkout identity for session identity, so this can be implemented under that requirement without another architectural choice.

8. **Blocking — the dependency key identifies a lockfile, not necessarily the prepared install.**

   **Where:** R5.5, Design:380–395; R5.6, Design:422–431; §25 F6, Design:2083. Code: `package.json:14`; `Workplace/IntegrationDeclaration.cs:27`, `:209–216`.

   **Sequence:** An entry is sealed under the example key: lockfile bytes, Node version, system and architecture. A later line changes a lifecycle script that patches installed files, or changes install configuration or an install-relevant environment value. The lockfile and Node version remain unchanged. The old entry hits, and its manifest correctly verifies the **wrong install**.

   Running preparation in the gates’ environment, as the independent read requests, does not put that environment’s values into the key.

   **Smallest change:** Key the preparation identity too: commands, package-manager/toolchain identity, declared install inputs and relevant environment values. Lifecycle-script inputs must be covered. When their inputs cannot be bounded, miss conservatively rather than share. A conservative tracked-tree identity can be the initial default; narrower keys require an explicit completeness contract. Add same-lockfile/different-script and different-environment proofs.

9. **Not blocking — manifest tolerance must distinguish caches from added dependencies.**

   **Where:** R5.6, Design:425–429; independent round-5 F9/F10 at `docs/2026-10-26-tree-pool-reads.md:1460`, `:1482`. Code: `Workplace/TreeDependencies.cs:44–45`, `:248`.

   **Sequence:** A session adds an undeclared package without changing the lockfile. All sealed files remain unchanged. If “files added beside them” are tolerated generally, the next line inherits that package and can pass a test that a clean install would fail.

   **Smallest change:** Validate the dependency namespace at every take. Permit additions only beneath explicitly declared disposable cache paths; otherwise replace the placement. Today’s copy already distinguishes named top-level caches rather than treating arbitrary additions as caches. Carry forward the independent read’s writable-copy and link-safe-removal requirements too.

10. **Blocking — path stability alone cannot validate a carried environment.**

    **Where:** R5.5, Design:373, :398–402; §11’s replacement, Design:1346–1348. Independent round-5 F10 already identifies this at `docs/2026-10-26-tree-pool-reads.md:1490–1494`. Code: today’s fresh opening at `Workplace/TreeOpening.cs:133`; existing recognition of `.venv` at `Workplace/TreeScratch.cs:20`.

    **Sequence:** Slot A contains an environment installed for lockfile A. The slot switches to line B, whose dependency lock or interpreter requirement differs. Its path is unchanged, so the proposed intermediate class carries the environment. B invokes that environment directly and tests against A’s packages. Unlike the class definition, the environment does not itself check the new repository’s lockfile.

    **Smallest change:** Give path-bound installs a key and clear/recreate them when it changes, with explicit failure handling before admission. Put this requirement in row 4’s take and proofs, not only among row 3’s follow-ups. For mixed intermediate/runnable folders, use the independent read’s conservative output classification unless every supported runner rebuilds first.

I found **no additional defect** in the revised same-line-only takeover rule or the ref-backed release predicate. Claim groups also address the identified shared-build races when their closures are complete and stable. The sparse/index-bit refusals, explicit signing, pathspec translation and selective commit interface answer those private-index points; hook confinement remains finding 2.

For row 4, I found **no new work-loss case in the described claim fencing, named-job takeover, holder checks or non-forcing switch**. Its remaining blocker above concerns what the next line is allowed to reuse.


## Codex, round 7

Codex's read of rows 3 and 4 as round 7 left them, at `687dc968a8`, run by the steward from outside the install, read-only, without seeing the independent Opus reader's round-7 read; verbatim but for its headings set down.

**Row 3: another round.** Round 7 fixes the tracked-input key, install provenance and placement-record problems, but the fill can still consume unkeyed state, miss writes outside its dependency folders, and produce effects a store hit never recreates.

**Row 4: another round, narrowly.** The revised classes and running-session mappings address the previous findings. Releasing the start’s shared hold for installation introduces a pre-record replay gap that needs closing.

Reviewed at `687dc968a8`. No files changed, programs built or `local/` contents read. Measurements were not reproduced. Below, **Design** means `docs/2026-10-26-tree-pool-design.md`; abbreviated code paths are relative to `src/Daoris.Desktop/Daoris.Desktop.Driver/`.

1. **Blocking — selecting environment variables does not bound the install’s inputs.**

   **Where:** R5.6, Design:465–479 and :502–504; §5.3, Design:1179–1186. Code: `Review/Heavy/HeavyStep.cs:126–142`; `Workplace/TreeOpening.cs:175`; `Workplace/TreeLocalInputs.cs:81–97`; `Workplace/TreeClassesReader.cs:91`.

   **Sequence:** Daoris seals an install using the selected environment. The package manager also reads configuration from the user’s profile. That configuration changes while its pathname, selected variables, toolchain versions and repository commit remain unchanged. The old entry hits and its manifest verifies an install made under the previous configuration. npm explicitly reads project, user, global and built-in configuration files; an environment allow-list does not suppress those reads. [npm configuration files](https://docs.npmjs.com/cli/v11/configuring-npm/npmrc/)

   The same gap admits **session-derived state**. A slot retains intermediates from line A. After switching to B, an install miss runs with those intermediates accessible, local inputs copied back, and the checkout hook already run. An install script reads an ignored settings file into its generated dependency configuration. It changes no tracked file, writes only inside its dependency folder, leaves no external link and embeds no tree pathname. Every stated sealing check passes. Nevertheless, the entry contains A’s state—or private data copied into the tree.

   “Daoris ran the install” establishes who launched it, not what it consumed. The whole-tree fallback also cannot distinguish changing ignored files or profile configuration at the same commit. Explicitly passing a secret through `env` likewise does not prevent an install from embedding it; the proposed scan searches for paths, not secrets.

   **Smallest change:** Extend the reuse contract beyond tracked inputs. Use controlled package-manager configuration, key declared non-secret filesystem inputs, and keep local copies and previous-session artifacts unavailable during a sealable fill. Where an install requires private or otherwise unbounded inputs, explicitly disable sealing **and cross-line reuse**, running it before each admission instead. Keep secret values out of persisted key descriptions and diagnostics. This retains one dependency mechanism and requires neither cloning nor another filesystem.

2. **Blocking — the proposed fill judgement cannot detect writes inside existing ignored folders.**

   **Where:** R5.6, Design:477–484; §27’s stronger “writes beside its folder” claim at Design:2273. Code: `Workplace/ContentHolds.cs:402` and :421–424; `Workplace/TreeDependencies.cs:221–264`.

   **Sequence:** Dependency A is placed and checked. Dependency B then misses, so Daoris runs B’s install. Its lifecycle script patches a file inside A’s existing ignored folder.

   Before and after, git can report the same ignored directory entry. The walk specified for the judgement covers B’s folder, not the changed bytes inside A. Consequently, B can be sealed and A’s placement record can remain stale. The session starts using modified A; its record is not checked again until another take.

   The cited `ContentHolds` code explicitly describes a wholly ignored folder being named once. A comparison of these status listings proves neither that existing ignored files were untouched nor that another dependency remains valid. Today’s dependency copy launches no install scripts; introducing them creates this additional case.

   **Smallest change:** Give the write check a content-aware baseline outside the install’s declared path set, excluding permitted outputs. Revalidate every affected placement after all installs, before opening the record. A forbidden write must prevent sealing and leave affected dependencies restored or absent, with the failure reported. Add a regression in which the install changes an existing file under another ignored dependency without adding any pathname.

3. **Blocking — a store hit does not reproduce permitted install effects outside the stored paths.**

   **Where:** R5.5, Design:400–407 and :430–431; R5.6, Design:477–488; §5.3, Design:1179–1186. Code: `package.json:14`; `tools/merge-rows.mjs:264–275`; `tools/index-hook.mjs:57–86`.

   **Sequence:** An accepted install writes its dependency folder and generates a required client under a declared output directory. R5.6 permits that output and seals only the dependency folder.

   A later take clears runnable outputs, finds the dependency entry, places it and skips installation. The generated client is absent. The manifest still passes because it describes only the stored dependency paths. The same problem occurs when the dependency stays in place while its companion output is cleared.

   This repository demonstrates the distinction: its root `prepare` changes common git configuration, installs hooks and may write the orientation index. Its checkout hook supplies an independent regeneration route for that index. A generic repository has no such guarantee.

   **Smallest change:** Require a reusable dependency declaration to cover every artifact needed from its install. Required generated artifacts can join the dependency’s multi-path set, with the existing prohibition on overlapping classes. If required effects remain outside that set, specify how they are recreated on **both hits and kept placements**, or make the install non-reusable and run it after those effects are cleared. Merely allowing a declared output during sealing is insufficient.

4. **Blocking — installation releases the protection that currently prevents a replay before record opening.**

   **Where:** §2.3, Design:835–842; §3.2, Design:971–975; §5.3, Design:1185–1186 and :1218–1222; row 3, Design:2293–2295. Code: `Workplace/TreeLock.cs:15–23`; `Workplace/TreeSync.cs:211–245` and :270; `Workplace/BranchJudging.cs:117–124`; `Workplace/BranchReplays.cs:67–81`.

   **Sequence:** A returning line is switched into a slot and passes the check. Its dependencies miss. The start releases its shared hold to wait for admission and run installation; its new session record has not opened.

   Another process acquires the checkout’s exclusive hold and runs a replay. The returning line has no active record or other ledger holder. Expanding ledger-held paths through `WhereIs` adds nothing for this start. The replay sees a clean worktree and proceeds.

   Its judgement does not inspect worktree locks or `tree_claims`; the replay then detaches and rewrites the tree while installation reads it. The install can consequently consume changing sources under a previously computed key. The specified sequence has no repeat of the branch/base check after installation and before record opening.

   §7 correctly says a claim holds a slot, but round 7 specifies claim exclusion for pool operations and record-based expansion for existing in-use readers. It does not connect the pre-record claim to those replay readers. Row 3 introduces the same interval for fresh trees before row 4 supplies pool claims.

   **Smallest change:** Before releasing the shared hold, establish a reservation that every removal and replay judgement reads, including fresh-tree opens. Include claimed slots directly in those readers’ busy sets; do not require an opened record or completed `kept_trees.line` mapping. Reacquire the shared hold and verify branch, commit, declaration, key and placements before opening the record. Test a second process attempting a replay during a paused install.

5. **Not blocking — the proposed repository example still omits a specifically promised key input.**

   **Where:** R5.5, Design:416–420; §27’s proof at Design:2308–2310. Code: `src/Daoris.Web/package.json:52–67`; the present key reader at `Workplace/TreeClassesReader.cs:85–92`.

   **Sequence:** Seal the web install using the example key. Add a project `.npmrc` containing `omit=dev`, without changing either package file or the accepted declaration. None of the example’s key inputs changes. The existing entry hits, although a fresh install would omit development dependencies, including this project’s build tools. npm documents that omission changes the installed tree while retaining dependency resolution in the lockfile. [npm ci’s omit setting](https://docs.npmjs.com/cli/v11/commands/npm-ci/#omit)

   **Smallest change:** Include optional project configuration paths, recording absence as round 7 already requires, or omit the narrow `key` until its completeness is established. Make the `.npmrc` regression exercise the actual proposed declaration. The reader’s planned removal of the “must currently be tracked” restriction is necessary for this absent-file case.

I found **no additional defect** in the revised treatment of path-bound dependencies, toolchain-triggered intermediate renewal, mixed runnable folders, writable copies, or link-safe removal. The cited holder code supports round 7’s distinction between `InUseAsync` and `HoldersAsync`; mapping running records to their slots addresses the identified running-session deletion and replay case.

I also found no further work-loss case in the specified non-forcing switch, claim-generation fencing, named-job takeover or ref-backed finish. Evicting a sealed entry leaves independent placed copies intact as designed. Those conclusions concern the proposed rules; their crash and process proofs remain to be built.

## Opus (independent), round 7

The independent Opus 5.5 reader's read of round 7, rows 3 and 4 alone, at main `8c8e0f86bd`: 11 findings, 3 of them blocking, one in
row 3 and two in row 4. I read the owner's choices as given (D214's notes of 2026-11-07 and 2026-11-08): the kept slot first, the store
the one way a tree gets its dependencies, no Dev Drive, and this repository's sessions on kept slots with its pool on after rows 3 and 4.
- **The record holds.** A placement record read at every take, a declared cache the only tolerance, writable copies and a link-safe
  removal answer Codex 9 as it asked.
- **The fill holds where it judges.** Only Daoris's own install fills, in a tree no session worked in, and the judgement refuses a
  tracked change, a stray ignored path, a link out of the tree and the tree's path. What it cannot see should be said (F8, F9).
- **Path-bound installs and keyed intermediates hold as classes**, with precisions (F6, F10).
- **Three cases still give a session a wrong install or move a running slot:**
  - the configuration each manager reads from the user's folders is in no key (F3);
  - the take's install runs before the record opens and outside the start's hold, where a pull's replay reads the slot as free (F1);
  - the landing's follow tests the slot against an in-use set the census does not name (F2).

  Each has a small change inside the owner's frame, and none reopens a choice.

**Verdict: row 3, another round narrowly on F3; row 4, another round narrowly on F1 and F2.** The rest are named changes (§3).

Read at main `8c8e0f86bd`, which holds round 7's `4cef4d2ba0` and D214's note of 2026-11-08. Of the files round 7 cites, only
`tools/merge-rows.mjs` changed after its `a94a922745`, in its comment alone. I read with git and read-only commands, ran
`npm config list --json` once to see what it prints, built nothing and changed no file but this one; what I say of pnpm, yarn, pip, uv,
cargo and NuGet is from their documentation. A bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and `Design` is
`docs/2026-10-26-tree-pool-design.md`.

### 1. Round 7's answers (§27)

| Finding | Round 7 | Holds? |
|---|---|---|
| Codex 8 (blocking) the key names a lockfile | The key names the install; only Daoris's own install fills | **In part.** The tracked inputs, the command, the toolchain and an environment by selection hold (`Review/Domain/GateEnvironment.cs:23-28`, `:66-75`; `Workplace/IntegrationDeclaration.cs:27`, `:209-216`). The managers' configuration outside the tree is unkeyed (F3), and `prepare` also writes git's common hooks folder (F9) |
| Codex 9 caches against added packages | A placement record; declared `caches` | **Yes**: TREEDEPS1 skips its three caches rather than tolerating additions (`Workplace/TreeDependencies.cs:44-45`, `:248`). A failing record is still the slot's at the judgement and the finish (F7) |
| Codex 10 (blocking) a carried environment | `bound` kept in place by key and record; intermediates keyed | **Yes** as classes (`Workplace/TreeOpening.cs:133`; `Workplace/TreeScratch.cs:20`). The toolchain key is read at the wrong moment (F6); the uv example reinstalls at every take (F10) |
| Codex 7 the helper's capture | Closed for a pooled asker; in the census | **Yes**: the capture is handed the record's path (`Loop/Helpers/HelperSettling.cs:37`) and reads `HEAD` and status there (`Participants/Helpers/HelperCapture.cs:99-104`) |
| Opus F9 the census; *spoken to* | Row 4 builds both; holders through `InUseAsync` | **Yes** (`Workplace/TidyHolders.cs:16-24`, `:30-37`, `:52`) |
| Opus F1 for row 4 | Each in-use set adds the held line's slot | **In part.** The five tests stand as cited. The landing's set and the follow's test are missing (F2); a slot under a take is in no set before its record (F1) |
| Round 6's F10 | The store serves trees alone | **Yes** (`Workplace/TreeOpening.cs:172`; `Workplace/TreeReturns.cs:121`) |
| Round 5's F9, F10, F11 (a) | The take's phases; copies; no template; no Dev Drive | **In part**: the install's place in the take (F1) |
| Round 7's own: the accepted declaration | `trees` only from the accepted blob at the line's tip | **Yes** for what acts (`Workplace/TreeScratch.cs:46-51`; `Workplace/IntegrationAcceptance.cs:91-97`); the whole-file test costs the saving here (F5) |
| Round 7's own: TREEDEPS1, §18 item 10 | The owner's | Answered by D214's note of 2026-11-08, which §18 does not yet say (F11) |

### 2. Findings

1. **Blocking (row 4). The take's install runs before its record opens and outside the start's hold, where a pull's replay reads the
   slot as free.**
   - **Where:** Design:854 orders a take's phases `checked`, `installed`, `opening-record`; Design:1185 runs the install "outside the
     start's shared hold", Design:1191-1192 the take inside it. That hold is the run's from `GrowTree` until the record opens, "so bringing
     the repository up to date cannot rebase the tree a session is starting in" (`Loop/Run/Tree/GrowTree.cs:3-9`, `:96-98`;
     `Loop/Run/Open/OpenRecord.cs:119-120`).
   - **What goes wrong:**
     - B's take switches `k-1` to `daoris/s-B` at the line's tip `L1`. The root key has moved (the root `package.json` changed in 26
       commits in two weeks), so `npm ci` runs, 72 to 93 s, the hold let go.
     - The line moves to `L2`, a median 4.7 minutes apart here. The look's pull takes the repository alone (`Workplace/TreeSync.cs:211`)
       and judges `daoris/s-B`: its tree is `k-1` by git's list (`Workplace/BranchJudging.cs:45`, `:51`); not busy, since no record names B and
       the census adds a slot only for a held line; clean, `npm ci` writing an ignored folder (`:117-124`); behind (`:130`). So it is
       replayed in `k-1` (`:161`) while the install runs there.
     - The record then opens at `L2` over a dependency installed and keyed at `L1`, which nothing reads again: where `L2` changed a key
       file, B's whole session tests against the old install. The fill may also seal an install whose scripts read files the replay
       was rewriting.
   - **Smallest change:** run the take's install where row 3 runs a fresh tree's, a stage after `open` and before `hand`, once
     `kept_trees.line` is written, so the in-use sets hold the slot; the claim then ends at `opened`. Or count a slot under a claim in
     every in-use set, from `tree_claims`. Row 3's "after the open" (Design:2294) should say *after the record's open*. Row 4's proof: the
     line moved during a take's install, the slot's branch not replayed.

2. **Blocking (row 4). The landing's follow tests the slot against an in-use set the census does not name.**
   - **Where:** Design:971-975 names `TidyHolders`' sets, `HelpHousekeeping`'s and the inventory's, and five tests. The landing reads
     none of them: `LandPerformer` hands `TreeLands.LandAsync` its world's set (`Workplace/LandPerformer.cs:111`, `:251`), the active
     sessions' trees alone (`Review/ReviewLandingWorld.cs:43-46`), and LANDSYNC1's follow tests the landed tree against it
     (`Workplace/LandingPress.cs:339-342`; `Workplace/TreeLandSyncs.cs:206`).
   - **What goes wrong:** row e lands a pooled line from its slot, so the follow's tree is `k-1` while the set names `s-B`. A landing
     that replayed (the line held some of B's commits) reaches the follow while B's next part runs or waits there. The test passes, and
     with `k-1` clean on its branch the follow detaches it, moves `daoris/s-B` and switches back (`TreeLandSyncs.cs:216-222`), under the
     running session. At `s-B` today it answers "a session still running or waiting holds it", and the tree stays.
   - **Smallest change:** the census's third list adds `ServiceReviewLandingWorld.InUseAsync` and the follow's test, and the slot is added
     where each set is built, this one among them. Row 4's proof: a replaying landing with its line's session running in the slot leaves
     the slot as it was.

3. **Blocking (row 3). The key leaves out the configuration each manager reads from outside the tree.**
   - **Where:** R5.6's key and environment (Design:465-470), which passes "the user's … folders"; the proof's "a changed `.npmrc`"
     (Design:2308) is a tracked one in a `key`.
   - **What goes wrong:** npm and pnpm read the user's and the global `npmrc`, yarn the home folder's `.yarnrc.yml`, pip its `pip.ini`, uv
     its `uv.toml`. The person sets pnpm's `node-linker=hoisted`, yarn's `nodeLinker`, or npm's `ignore-scripts=true` or `omit=dev`, for
     another project. Every fill after it installs that layout; every entry sealed before still hits at its key and is placed, its record
     holding, and an entry placed every day ages out only by size (Design:497). Sessions test against a layout no fresh install makes:
     Codex 8's case, through a file outside the tree. A `bound` `.venv` kept at its key meets it too.
   - **Smallest change:** key each manager's effective configuration by a `toolchain` command that prints it in the install's
     environment, the conventions naming each (`npm config list --json`, which names no tree path here; `pnpm config list --json`;
     `pip config list`), and this repository's declaration adding npm's. A command whose output names the tree only makes every key miss.
     Row 3's proof: a user-level setting changed at one lockfile, missing.

4. **Not blocking, before row 3. An install killed after the record opened is met by its carry-on, which clears nothing.**
   - **Where:** "a folder with no record … is cleared before anything reads it" (Design:489) is done only by an open, a remake and a take
     (Design:1182-1183, :2293-2295).
   - **What goes wrong:** a shutdown kills each session and carries it on (driving rule 41). Carried on in its tree by `GrowTree`'s resume
     (`:23-34`), or in its own slot by §5.1 choice 1's steps 1, 4 and 6, the session starts over a half-written folder, unsaid. Today's
     copy runs under the opening lock, whose undo takes the tree (`Workplace/TreeOpening.cs:170-172`).
   - **Smallest change:** every start into a tree clears a declared dependency with no placement record and installs it before its
     agent, as a take does. Proof: an install killed by a restart, then the carry-on.

5. **Not blocking, before row g. Acceptance of the whole gates file darkens the classes at every gate edit.**
   - **Where:** Design:394-398 and §27's own row; `Workplace/IntegrationAcceptance.cs:91-97` reads the accepted text only where its blob is
     the tip's.
   - **What goes wrong:** this repository's gates file changed in 10 commits from 2026-10-25 to 2026-11-08, three on 2026-11-07, nine
     outside `trees`. From each until the owner accepts again, TREEDEPS1 gone, a fresh tree is placed nothing and its session runs `npm
     ci`, and every take renews its slot: intermediates and dependencies moved aside, then built and installed again, the churn the owner
     chose to end.
   - **Smallest change:** act where the tip's parsed `trees` equals the accepted blob's, read from the accepted text, so only a change to
     `trees` itself waits for the owner.

6. **Not blocking, before row 4b. The toolchain key is read at the take, not where the intermediates were built.**
   - **Where:** `kept_trees.toolchain`, "at its last take" (Design:2332-2333); the renewal (Design:1179-1181).
   - **What goes wrong:** A takes `k-1` at main, `dotnet --version` T1. A's session commits a `global.json` selecting T2 and builds, so
     `obj/` is T2's. A is let go unlanded; B takes `k-1` at main, reads T1, equal to the column, and builds over T2's `obj/`. The same with
     `rust-toolchain.toml`, `.nvmrc` or `.python-version`.
   - **Smallest change:** the end's read of the slot (§6) writes the toolchain key into `line_ends` and the column, and a take renews
     where its key differs from the last let-go's.

7. **Not blocking, before row 4b. A dependency whose record fails is still the slot's at the judgement and the finish.**
   - **Where:** Design:1125-1126 counts a dependency "with its placement record" as the slot's; the finish keeps the slot's
     (Design:1411-1414); the take replaces a failing one by removal (Design:492-494), never aside.
   - **What goes wrong:** A's session edits a file under `node_modules/x/` for a patch it has not yet written, or runs `pip install` into
     its `.venv`, and is let go unlanded or finishes. Its slot is handed on and B's take deletes the folder. Today those writes go aside
     with the whole tree and wait D195's hour or days.
   - **Smallest change:** the judgement and the finish read each namespace against its record, as the take does. A failing one is the
     line's own: it holds a live line's slot under §18 item 4 (a), and goes aside with a finished line's record.

8. **Not blocking. A secret in `env`, and the inputs a fill reads unkeyed.**
   - `env` is keyed by "every name and value" (Design:468-469), and the record holds "the key" (Design:488). A private registry's token
     is the common `env`. Keep the key as its hash alone; let `env` mark a secret name, keyed by name and never written to a record, a note
     or a log; and have the fill's scan look for each secret value as it looks for the tree's path, sealing nothing where an install wrote
     one (a generated client that inlines its connection string).
   - The fill runs after `placed` makes TREELOCAL1's copies again (Design:1182-1183), and in a slot beside another line's intermediates,
     so an install script reading a `local/` file or an ignored generated file is keyed by nothing. Run installs before the local copies,
     and let R5.5's `key` statement read "no other file, tracked or ignored".

9. **Not blocking. What the fill cannot see, and two fills of one key.**
   - This repository's `prepare` also writes git's common hooks folder (`tools/index-hook.mjs:49-69`), beside `.git/config` (`:76-82`;
     `tools/merge-rows.mjs:270-276`); Design:430-431 names the configuration and `docs/index/` alone. The judgement reads the tree only:
     say that a write to git's common folder or the user's home is the accepted declaration's to answer for.
   - Both `--install`s exit 2 where git cannot take `config.lock`, which fails `npm ci`. Two starts after one `package.json` change fill
     the root key at once (Design:480 lets a second sealer discard its own), and the loser starts without its dependency. One fill per key
     at a time, the second waiting for the seal and placing it, closes the race and saves an install.

10. **Not blocking. The uv example reinstalls at every take.**
    - Python writes `__pycache__/` into the `.venv`'s packages at import, outside any `caches`, so the record fails after any test run.
      The example's intermediate `**/__pycache__/` reaches inside the dependency, a meeting the reader passes, since `RoomPaths.Meet`
      probes literal heads (`Workplace/RoomPaths.cs:31-42`), and nothing says which class wins.
    - Declare `"caches": ["**/__pycache__/"]` on the `.venv`; say a dependency's folder is its own against every other class's glob; and
      key the interpreter (`uv python find`), since `pyvenv.cfg` leads outside the tree to one the person may upgrade or remove.

11. **Not blocking. Nits.**
    - The take's clear removes only what git ignores at the slot's new `HEAD`, §11's rule (Design:1452), since a returning line's commit
      may track a path the accepted declaration names.
    - Installs in one tree run one at a time, each judged against the status the last left.
    - A placement copies by its entry's manifest; a source missing or of another size (a scanner's quarantine) evicts the entry and
      falls to the install.
    - npm's hidden lockfile gets its time after the folders, as TREEDEPS1 gives it (`Workplace/TreeDependencies.cs:268-273`), or npm
      reads the whole tree.
    - A conversation's open (`Loop/Chat/ChatStarts.cs:207`) has no run stage after its record: name where its install runs.
    - A runner that skips its tool's own check (`dotnet build --no-restore` over `obj/project.assets.json`) reads another line's restore
      where a fresh tree refuses: R5.5's output rule should read "uses without its tool's own check".
    - D214's note of 2026-11-08 answers §18 items 2 and 10, which still ask (Design:1931-1933, :2002-2010).

**11 findings, 3 of them blocking.**

### 3. Verdict, and where I found nothing

**Verdict: row 3, another round narrowly on F3; row 4, another round narrowly on F1 and F2, after row 3.**
- F3: the managers' configuration keyed through a printed `toolchain` output.
- F1: the take's install after the record's open, or a claimed slot in every in-use set.
- F2: the landing's in-use set in the census, and the slot added there.

F4, F8, F9 and F11 land with row 3, and F5 before row g; F6, F7 and F10 before row 4b. The steward can check each without another read.

**Where I found nothing:**
- the claim, its generation, the named job and the takeover (§2.3);
- the switch and the order `cleared`, `placed`, `checked`, hook; submodules refused; the first two census lists;
- npm's workspace junctions made again inside the tree;
- pnpm's hard links, sealed by copy;
- NuGet's packages outside the tree, with restore's own check;
- cargo's fingerprints over `target/`;
- yarn's PnP paths, relative to a cache every tree of one repository reaches from the same depth.

Nothing I found loses committed work, and none of the three reopens the owner's choices.


## Codex, rounds 9 and 10

Codex's read of rows 6a, 3, 4 and 6b as rounds 9 and 10 left them, at `9f2a228a48`, run by the steward from outside the install, read-only, without seeing the independent Opus reader's read of the same rounds; verbatim but for its headings set down.

| Row | Verdict | Why |
|---|---|---|
| **6a — rules and analysis** | **Another round** | A script merely naming a folder can make its contents disposable; the repository floor misses bare repositories. |
| **3 — dependency store** | **Another round** | Dirty resumes lack a lossless rollback contract; secret values can change without changing the key; cross-install effects lack complete invalidation. |
| **4 — kept slots** | **Another round** | It inherits those classification and dependency problems. I found no additional running-slot gap in the revised census. |
| **6b — page** | **Ready to build**, after its prerequisites | I found no independent blocking issue in showing rules, setting restrictions, or recording allows. |

Read at **`9f2a228a48`**, including the specified rounds, their revised sections, D214’s owner notes, and the independent round-5 read. No files changed; no `local/` folder read; no builds or tests run.

Below, **Design** means `docs/2026-10-26-tree-pool-design.md`. **Driver/** means `src/Daoris.Desktop/Daoris.Desktop.Driver/`. The proposed types are unbuilt; code references identify the existing behavior or integration seam being revised.

1. **Blocking — merely naming an ignored folder does not establish that its contents are reproducible.**

   **Where:** Design §28.4:2866–2872; §29.4:3209–3224; §5.3:1291–1295; §30.1:3359–3361. Code: `Driver/Workplace/ContentHolds.cs:439–456`, `Driver/Workplace/SetAsideEligibility.cs:64–82`, `Driver/Workplace/TreeSetAside.cs:418–427`.

   **Sequence:** A tracked script reads an ignored `experiments/` folder containing manually prepared inputs and unfinished results. Source 5 treats a script naming an ignored path as its producer and assigns `output`. Nothing identifies these files as a local signature or establishes a competing scratch classification.

   A conversation ends, remains unfinished, and eventually has no holder. Its guessed output does not hold its slot. The next take moves the folder aside. Floor 4 then subjects that folder to D195 and the size limit. Today, an ordinary set-aside folder without a finished manifest becomes `unmarked` and expires by days; that route does not require the originating line to have finished.

   Moving the files delays their loss. It does not establish the claimed “wrong guess loses nothing.” The existing content reader explicitly distinguishes unique session files from declared reproducible output; the automatic analysis would erase that distinction on weaker evidence.

   **Smallest change:** A script’s mention supplies evidence of a reader, not automatically a producer. Write `output` only where a signature or an identified generation step establishes recreation; otherwise retain `scratch` or `unknown`. Also define the lifetime of files moved aside from an unfinished line: they must not become ordinary expiring leftovers merely because an automatic classification released the slot. This requires no acceptance prompt and keeps established runnable outputs cleared at every take.

2. **Blocking — the repository floor recognizes working repositories, but misses bare repositories.**

   **Where:** Design §28.4:2827–2828; §29.2:3130–3132; §30.1:3354–3356; §5.2:1234–1246. Code: `Driver/Workplace/SetAsideReads.cs:29–56`, `Driver/Workplace/TreeSetAside.cs:118–126`. An existing bare-repository recognition precedent is `tools/rehearsal-kit.mjs:383–390`.

   **Sequence:** A repository’s tool maintains an ignored, versioned cache containing a bare Git repository. The analysis finds the cache from its script, and a session creates an unpushed ref in that bare repository.

   A bare repository has its Git directory contents directly under its root; it need not contain a child named `.git`. It is also absent from the enclosing repository’s worktree list. Both specified detection methods therefore miss it, as does the cited existing nested-repository walker.

   The folder remains classified as cache. Another line can inherit it, or a toolchain change can move it aside during a take. Its private ref and objects then follow ordinary cache retention instead of holding the slot under floor 2.

   **Smallest change:** Extend the floor’s repository recognition to bare Git directories. Use structural candidates followed by a read-only Git validation, with unreadable candidates retaining the slot. Apply that check before copying, carrying, renewing, or removing a containing unit. Add a bare-repository fixture alongside the `.git`-directory and gitfile fixtures.

3. **Blocking — the new files stage can apply a fresh-tree rollback to a dirty resume.**

   **Where:** Design R5.6:555–568; §29.7:3286–3301; §5.3:1334–1336. Code: `Driver/Workplace/TreeReturns.cs:59–65`, `Driver/Loop/Run/Tree/GrowTree.cs:58–64`, `Driver/Loop/Chat/ChatStarts.cs:215–219`.

   **Sequence:** A session leaves an uncommitted edit in a tracked source file and ends with a dependency folder absent. Its carry-on uses the same tree. Existing return handling accepts a standing tree without restoring its tracked files; conversations expressly permit a dirty tree.

   Round 9 now runs the files stage on every resume and carry-on. The missing dependency triggers an install. Its lifecycle script modifies that already-edited tracked source. The retained R5.6 failure rule restores a tracked file the install changed **from the commit** before clearing the install.

   That restores neither the session’s working version nor its staged version. The uncommitted edit is lost. Refusing to seal the install does not preserve the pre-install work.

   **Smallest change:** Give installs in existing trees a separate rollback contract. Preserve the exact pre-install working and index states of affected tracked paths, and restore those states on rejection. A dirty/session-used tree must remain ineligible to seal. Alternatively, leave an absent dependency for the session where Daoris cannot execute and roll back its install losslessly. Test a carry-on whose lifecycle script rewrites an already-dirty tracked file.

4. **Blocking — marking an environment variable secret removes its value from cache identity.**

   **Where:** Design R5.6:529–530; §29.6:3264; §30.3:3398–3404. Code: `Driver/Review/Heavy/HeavyStep.cs:126–145` establishes that selected values reach the child unchanged; `Driver/Review/Integration/DirectGateRuns.cs:74–80` is the execution seam the design adopts.

   **Sequence:** A dependency’s install receives a variable marked `secret`. Its value selects a licensed feature set or account-specific package variant. The install writes the selected files without writing the literal secret anywhere, so the value scan passes.

   The secret changes while its name, tracked inputs, toolchain and other environment values remain unchanged. The proposed key remains unchanged because this variable contributes only its name. A store hit places the old variant without running the new install. The same failure affects a path-bound install retained in place.

   A manifest proves that the old files remain intact; it cannot prove they are the files the new secret would produce.

   **Smallest change:** Include a non-plaintext digest of every passed value in cache identity, including values marked secret. Keep secret marking’s separate effect on sealing and disclosure. Test two secret values that produce different folders while neither literal value appears in either folder. The second must miss.

5. **Blocking — keys of earlier dependencies do not capture a later install’s effects on them.**

   **Where:** Design R5.6:528–539; §29.6:3253; §29.7:3289–3296; §29.3:3191–3193. Code: `Driver/Workplace/ContentHolds.cs:421–438` confirms why Git’s ignored-folder listing cannot describe these internal changes; the replacement is inserted at the current dependency-copy seam, `Driver/Workplace/TreeOpening.cs:172`.

   **Sequence:** Install A creates a dependency folder. Install B modifies a file inside A and creates its own folder. Round 9 detects B’s write, seals nothing for B, and rewrites A’s record as the tree’s own.

   The slot now contains A-with-B’s-modification. Later, the effective rules remove B. A’s declared inputs, key and rewritten namespace record still hold. Nothing in A’s key names B, so the next session can inherit B’s modification even though a fresh execution of the remaining rules would produce pristine A.

   There is a complementary hole if the rewritten A is instead treated as line-owned and set aside: placing pristine A again at the **same key** does not invalidate a retained B. The assertion that including earlier keys necessarily reruns B after replacing A is false when replacement preserves A’s key.

   **Smallest change:** Record cross-install writes as a dependency between the affected placements. Invalidate the affected group when a participating rule changes or disappears, or when an earlier placement is rebuilt—even at the same key. Restore the earlier folders and execute the affected installs in order. A simpler first implementation can make such a group non-carriable between lines and reinstall it together.

   Add both proofs: removing B removes its effect on A; rebuilding A at the same key repeats B’s required effect.

I found nothing further in the stated layer precedence, empty default permission lists, or `deny → allow → ask` order. The tracked-path guard has a concrete existing basis in `TreeClassesReader.cs:60–63`. Round 9’s record-before-install correction matches `OpenRecord.cs:119–120`; its landing census addition covers the actual set and comparison at `ReviewLandingWorld.cs:43–46` and `TreeLandSyncs.cs:206`. I found no additional case requiring a running slot to be deleted or rebased once those specified census changes are implemented.

I also found no additional blocker in copy-based sealing, writable placements, link-safe removal, or eviction leaving already-placed copies intact. The page’s proposed responsibilities introduce no separate file-handling mechanism.

## Opus (independent), rounds 9 and 10

The independent Opus 5.5 reader's read of rounds 9 and 10, rows 6a, 3, 4 and 6b, at main `7b9f129e3b`: 9 findings, 2 of them
blocking, one in row 3 and one in row 4. I read the owner's choices as given (D214's notes of 2026-11-07 and 2026-11-08, D233's of
2026-11-08): the rule file restricts nothing by default, so a restriction the owner chose not to build is no finding here. I looked for
what loses work, or a session's results, with nothing pressed.

**Verdict, row by row:**
- **Row 6a: nothing blocking.** F3, F4 and F7 are changes inside it.
- **Row 3: another round, narrowly, on F1.** The files stage installs at a resume and a carry-on, over the line's own work, where round
  7 never ran an install.
- **Row 4: another round, narrowly, on F2, after row 3.** A placement a slot keeps is kept without what its install wrote beside it.
- **Row 6b: nothing blocking**, after row 4.

**What holds:** the store, for what it seals and places (Codex 1 and 2, Opus F3, F8 and F9), and the record before any install (Codex
4, Opus F1 and F2). **Two answers hold for half their case:** Codex 3's for a hit, not a placement a slot keeps (F2); Opus F4's for a
first start, not a later one (F1, F5).

Read at main `7b9f129e3b`, which holds round 10's `1f50cb1ba9`; no file rounds 9 and 10 cite changed after `50055ee6d6`, and each
cited line stands, `ContentHolds.cs`'s within three lines. I read with git and read-only commands, built and ran nothing, and changed
no file but this one; what I say of the package managers, Prisma and husky is from their documentation. I saw Codex's read of these
rounds only as far as its verdict table. *Codex n* is Codex's round-7 finding n, *Opus Fn* my round-7 finding n, and *Fn* this read's.
A bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and `Design` is `docs/2026-10-26-tree-pool-design.md`.

### 1. Rounds 9 and 10's answers to round 7 (§29.6)

| Finding | Rounds 9 and 10 | Holds? |
|---|---|---|
| Codex 1 (blocking) the install's inputs unbounded | effective configuration keyed; a fill sealed only in a tree holding nothing else ignored, before the hook and the copies; secret names marked | **Yes** for the store: no slot's state and no copy is sealed, and each manager's user files are keyed through what it prints. The install that is no fill, at a resume, is F1's |
| Codex 2 (blocking) writes inside existing ignored folders | every earlier placement read after each install, under that isolation | **Yes**: the isolation leaves no other ignored folder for a write to hide in (`Workplace/ContentHolds.cs:399`, `:421-422`) |
| Codex 3 (blocking) effects a hit does not make again | an install writing outside its `paths` never sealed, installed in each tree, kept in a slot by its key and record | **In part**: for a hit, yes; for a kept placement whose companions went aside, no (F2) |
| Codex 4 and Opus F1 (blocking) the install outside the start's hold | the files stage after the record's open | **Yes**: the hold goes once the record opens (`Loop/Run/Open/OpenRecord.cs:119-120`); a replay asks in-use again inside its hold (`Workplace/TreeSync.cs:242-245`; `Workplace/BranchJudging.cs:121`), an opened record active there (`Workplace/TidyHolders.cs:67-72`); the opener's mark holds off the orphan sweep (`Workplace/Orphans.cs:72-74`) |
| Codex 5 the project `.npmrc` | keyed, absent as absent; the untracked-key refusal goes | **Yes** (`Workplace/TreeClassesReader.cs:91`) |
| Opus F2 (blocking) the landing's follow | the census names the set and the test | **Yes**, at the lines cited (`Review/ReviewLandingWorld.cs:43-46`; `Workplace/LandPerformer.cs:112`, `:252-254`; `Workplace/LandingPress.cs:339-342`; `Workplace/TreeLandSyncs.cs:206`) |
| Opus F3 (blocking) the managers' configuration | printed by a signature's command, the tree's path a token | **Yes**: yarn's printed settings name the tree, which the token takes |
| Opus F4 a cut-off install met by a carry-on | `installing` before the clear; the stage at every start | **Yes** for a first start. A failed or held install's mark is F5's, and the stage at a resume F1's |
| Opus F5 the gates file accepted whole | nothing accepted under rules | **Gone** |
| Opus F6 the toolchain key's moment | written at each let-go | **Yes** |
| Opus F7 a failing placement | the line's own at the judgement and the finish | **Yes**; set aside, its `node_modules` is slimmed an hour on, as D195 point 3 does today |
| Opus F8 a secret in `env` | values hashed; a marked value scanned | **Yes**, narrowed as I asked |
| Opus F9 git's common folder; two fills of one key | listed and said; one install per repository | **Yes**; the lock is wider than the race (F9) |
| Opus F10 uv | `**/__pycache__/` a cache; `uv python find` keyed | **In part**: the cache holds, the interpreter's key does not (F8) |
| Opus F11 nits | each taken | **Yes** |

### 2. Findings

1. **Blocking (row 3). The files stage installs at a resume and a carry-on, over the line's own work.**
   - **Where:** round 7 ran the fill "in the tree about to be used: a fresh tree after its checkout, or a slot just switched and
     cleared, its tracked files at the commit and no session working there" (Design:555-557). Round 9 runs the stage "at every start
     into one of Daoris's trees (... a resume, a carry-on)" (Design:3286-3288, :3260) and keeps R5.6's *Not sealed*: "A tracked file
     the install changed is written back from the commit first and the folder cleared, since a session starts at its commit"
     (Design:565-567). Floor 4 says Daoris *deletes* only what it made (Design:3359-3361), and names no overwrite.
   - **What goes wrong:**
     - The key moves at a resume wherever the line committed a key input, and at every commit where the commit's tree keys it: a
       found rule for a root whose `prepare` runs, husky's the common case (Design:3204-3206).
     - Session S commits part of its work, leaves the rest uncommitted, and parks on a question. At the resume the record holds, the
       key moved, and the stage installs in S's tree before its agent is handed.
     - Where the install rewrites a tracked file S had edited (a repository's own `install` of `npm install` or `yarn`, rewriting the
       lockfile S merged by hand), the write-back puts the commit's version over S's edit. Read the write-back as a fill's alone, and
       the install's rewrite joins S's uncommitted work instead, unsaid, for S to commit as its own.
     - A repository's own command (a bootstrap that empties `generated/` first) runs there by default, and the floor cannot bound what
       a command does. Even `npm ci` deletes S's `node_modules/` between its turns, and leaves it absent where S's working `package.json`
       no longer matches the lockfile (Design:3301).
     - The same stage runs in a returning line's own slot (Design:1334-1335) and at a conversation's go-on in its tree; and a
       conversation may open in the checkout (`Loop/Chat/ChatStarts.cs:215`), where no rule acts (Design:3105).
   - **Smallest change:** install only in a tree no agent has run in since Daoris made, remade or took it: a fresh tree, a remake, a
     take, a return, and a start cut off before `hand`. Elsewhere keep what its record holds, place a hit only into an absent folder,
     and say a dependency whose key moved, or absent at a miss, as the line's to install (`started.deps-left` with a why), as today's
     resume leaves it. Floor 4 reads *deletes or overwrites*. The conversation's step runs only in a tree of Daoris's. Row 3's proof: a
     resume after its line's commit moved a commit-tree key, its uncommitted tracked edit and untracked file as they were.

2. **Blocking (row 4). A placement a slot keeps is kept without what its install wrote beside it.**
   - **Where:** Codex 3's answer, an install writing outside its `paths` "is installed in each tree and kept in a slot by its key and
     record" (Design:3254); a top no rule names is `unknown`, set aside at its line's finish (Design:2917); the npm and pnpm rows name no
     workspace member's folder (Design:2852-2854). Codex 3 asked for a hit and a kept placement both.
   - **What goes wrong:**
     - husky's `prepare` writes `.husky/_/`, ignored by its own `*`, and sets `core.hooksPath` to it in git's common configuration.
       Line A's stage installs in `k-1` at main's tip X, and `.husky/_/` is `unknown`.
     - A finishes, and `.husky/_/` goes aside with its record. B takes `k-1` at X: the key and the record hold, so nothing runs. git
       finds no hooks in a missing hooks folder and runs none, so B's commits skip the repository's `pre-commit`, unsaid.
     - npm or pnpm workspaces the same way: the root's install writes `packages/*/node_modules/`, outside its `paths`; they go aside at
       A's finish, and B gets the root kept without them.
     - This repository is spared: the post-checkout hook remakes `docs/index/` after a take, and its `prepare` writes nothing else in
       the tree (`tools/index-hook.mjs:43-88`; `tools/merge-rows.mjs:264-276`).
   - **Smallest change:** an unsealed install's record lists each path it wrote outside its `paths`, as the judgement already names
     them for `started.deps-unstored` (Design:1782); the placement is kept only while each stands, else installed again, and those paths
     are the dependency's at the judgement and the finish, never `unknown`. The npm and pnpm rows put each workspace member's
     `node_modules/` in the root's `paths`. Row 4's proof: a `prepare` writing a folder beside its own, a finish, and a take at the same
     commit installing again.

3. **Not blocking (row 6a). The found layer states a key's completeness only the repository could.**
   - **Where:** §29.4 keys a found dependency by its signature's inputs unless "its install runs a lifecycle script (source 5)", which
     reads the repository's own manifests (Design:3203-3206, :2866-2868). R5.6 left that completeness to the repository's word: "a
     repository that cannot bound them declares no `key`" (Design:584-586).
   - **What goes wrong:** a dependency's own install script that reads a tracked file is unseen. Prisma's client is generated by
     `@prisma/client`'s `postinstall` from the tracked `prisma/schema.prisma`, inside `node_modules/`. Two commits with one lockfile and
     two schemas share the key: the first seals, the second places it, and its session's code meets the old schema's client. Only the
     path scan keeps it out, where the generated files happen to name the tree.
   - **Smallest change:** read a lockfile's own mark of a package's install script (npm's `hasInstallScript`) as source 5's evidence,
     keying by the commit's tree unless the repository's layer states a `key` or the signature row lists that package as reading nothing
     of the tree (esbuild's binary check). Proof: a dependency whose `postinstall` reads a tracked file, two commits at one lockfile, a
     miss.

4. **Not blocking (row 6a). A folder a script names is an output, so a let-go line's evidence leaves its tree.**
   - **Where:** source 5 makes an ignored path a script names an output (Design:2869-2870); §29.4 ranks output over intermediate, and
     scratch over a kind that keeps, never scratch against output (Design:3215-3216); §28.2 classes sibling A's two media-evidence
     folders as outputs on that evidence (Design:2700).
   - **What goes wrong:** A's run writes its screenshots into such a folder for the person's review, and A is let go waiting for it.
     Before round 9 the folder was A's own and held its slot (§5.2 step 5); now B's take moves it aside (Design:3279-3281). When A comes
     back to another slot, nothing lays it back or says where it went, and the set-aside waits only its days (D195 point 3).
   - **Smallest change:** a folder a script writes that no build tool's output setting and no runner's evidence names is `scratch`; and
     a returning line's start names the set-aside folder its outputs went to.

5. **Not blocking (row 3). An `installing` mark outlives a failed or held install.**
   - **Where:** step 2 writes the record `installing` first, step 6 leaves a failure's folder absent (Design:3290-3295, :3301), and
     floor 4 deletes an `installing` install "that was cut off" (Design:3359-3360), which the mark alone cannot tell.
   - **What goes wrong:** the web's fill fails (the registry down), or an `ask` holds it after the mark is written. S runs `npm ci`
     itself, as told, and edits a file under `node_modules/x/` for a patch it has not yet written (Opus F7's case). At the next start into
     the tree the stage reads the mark and removes S's install as Daoris's own.
   - **Smallest change:** the mark names the start that wrote it, and a failed or held install takes its mark and half folder away
     before `hand`; only a mark whose start ended before `hand` is a cut-off install.

6. **Not blocking (row 3). The copy loses TREELOCAL1's two gates.**
   - **Where:** step 5 copies "the files the effective `copy` rules name" at every start (Design:3299-3300). Today's copy refuses a path
     git does not ignore in the tree and a path already standing (`Workplace/TreeLocalInputs.cs:141`, `:204`), and row 3 removes it.
   - **What goes wrong:** the analysis writes `copy` for a local file a script names (Design:3211-3213), `.env` the common one. At a
     carry-on the checkout's `.env` replaces the one S set for its test database, and S's next run reaches the person's. Into a tree
     whose `HEAD` does not ignore it (an older branch), the copy is untracked, and `git add -A` commits it.
   - **Smallest change:** keep both gates: the target ignored at the tree's `HEAD`, and absent or Daoris's own by its recorded hash,
     else left and said (`started.local-refused`).

7. **Not blocking (rows 6a and 6b). The person's layer fails open.**
   - **Where:** one `data/files.json` holds the found layer, the person's rules and their `permissions`, and one that does not read "is
     empty" (Design:3111-3113, :3125-3127, :3415-3418).
   - **What goes wrong:** a hand edit with a stray comma drops every `deny` and `ask` the person set, and the rule by which they settled
     a runner's folder as an output (Design:3241-3242): the next start runs a command they denied, and a take keeps another line's
     build. `permissions.json`'s like rule (`Agents/Permissions.cs:281-319`) leaves the harness's own asks standing; this file's
     defaults restrict nothing. The analysis then writes the file at its next reading or finish, and a write over what it could not read
     loses the person's rules. And `daoris driver files set` writes that layer, where the settings verbs refuse a session's environment
     (`Settings/DriverChoicesCommand.cs:79`; `Workplace/GitIdentityCommand.cs:33`); row 6a does not say it does.
   - **Smallest change:** while the file does not read, hold every `run`, `copy` and `share` as an `ask`, said; keep the found layer in
     a file of its own, or write nothing over a file that does not read; refuse the verb's `set` and `remove` from a session's
     environment.

8. **Not blocking (row 4b). uv's interpreter key reads the `.venv` itself.**
   - **Where:** Opus F10's answer keys the interpreter by `uv python find` (Design:3266, :441).
   - **What goes wrong:** uv documents that `uv python find` shows a virtual environment's interpreter first where a `.venv` is found,
     and ignores it only with `--system`. Once the `.venv` stands, the output is the tree's path, a token: an upgrade or removal of the
     base interpreter `pyvenv.cfg` leads to never moves the key, and a key read before the install, with no `.venv` yet, differs from
     every later one, so the first take installs again.
   - **Smallest change:** `uv python find --system`, with that interpreter's `--version`.

9. **Not blocking. Nits.**
   - The signatures' installs in their frozen forms (`npm ci`, `pnpm install --frozen-lockfile`, `yarn install --immutable`, `uv sync
     --locked`), so a found install never writes a tracked file; §28.4 names npm's alone.
   - A rule is matched against the tops git lists (Design:3102-3105), which hold no dependency folder in a fresh tree: say a dependency's
     folders come from its rule and its manifest at `HEAD`, a glob expanded by the manifests.
   - Row 6a refuses "a path in two kinds" and proves "a later rule winning within one" (Design:3041): say which holds inside one layer.
   - One install per repository holds the web's sealable fill behind the root's unsealable one at each concurrent start here: a lock
     per key, and per repository only for an install that wrote git's common folder, closes Opus F9 as well.
   - Floor 3 names each provider's project configuration where git does not track it: Claude Code's `.claude/settings.json` and
     `.mcp.json` beside `settings.local.json`.
   - The take deletes Daoris's own copies before its switch, so `--no-overwrite-ignore` never refuses a slot for a file Daoris made.

**9 findings, 2 of them blocking.**

### 3. Verdict, and where I found nothing

**Verdict: row 6a, nothing blocking; row 3, another round narrowly on F1; row 4, another round narrowly on F2, after row 3; row 6b,
nothing blocking, after row 4.**
- F1: an install only in a tree no agent has run in since Daoris made or took it, and floor 4 naming an overwrite.
- F2: a kept placement's record listing what its install wrote beside its `paths`.

F3, F4 and F7 land with row 6a, F5 and F6 with row 3, F8 before row 4b, and F9's nits with the rows they name. The steward can check
each without another read.

**Where I found nothing:**
- the files stage under the record, for the replays, the tidy, the landing's follow and the orphan sweep;
- npm's, pnpm's and yarn's printed configuration as key inputs; NuGet's restore and cargo's fingerprints over intermediates kept per
  slot, no tracked script here building with `--no-restore`;
- the fill's isolation; this repository's `prepare`, which writes no tracked file;
- the floor's first two items, `_fixtures/`'s fixture repositories among them;
- secrets marked by name and scanned; the take's phases inside the hold;
- row 6b's Window-class verbs, an allow with its maker, and no name below a local top in its payload.

Nothing I found loses committed work. F1, F5 and F6 lose a session's uncommitted work through Daoris's own acts, the first by the
design's own text, and none of the nine reopens the owner's choices.

## Opus (independent), round 11

The independent Opus 5.5 reader's narrow read of round 11 for rows 3 and 4, at main `107063dfa2`, which holds round 11's `e0803c7ca3`:
the sections §31.4 names, checked against the code they cite. No cited file changed after `b36ab3e8a0` but by comments
(`Review/Heavy/HeavyStep.cs`), and each cited line stands. I built and ran nothing and changed no file but this one; the owner's choices
bound the read (D214's notes of 2026-11-07 and 2026-11-08). *R11-Fn* is my rounds-9-and-10 finding n, as §31 names it, and *Fn* this
read's. A bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `Design` is `docs/2026-10-26-tree-pool-design.md`.

**Verdict: 6 findings, 2 blocking, both in row 3 and both in how a tree is known to be unworked.**
- **Row 3: another round, narrowly, on F1 and F2.** The test the floor rests on reads a conversation's tree, and every tree standing when
  row 3 lands, as unworked, so Daoris installs over a line's uncommitted work there.
- **Row 4: nothing blocking of its own, after row 3.** F1 reaches a conversation's own slot; F6 is a sentence.

### 1. My rounds 9 and 10, answered?

| Finding | Round 11 |
|---|---|
| R11-F1 installs at a resume and a carry-on | **For a run, yes**: `hand` writes `handed` before its agent. Not for a conversation (F1), nor a tree with no files record (F2) |
| R11-F2 a kept placement without its install's other writes | **Yes**: companions recorded and read at the take, the judgement and the finish. Row 4's proof should read *kept with `.husky/_/` at the same commit, installed again where it was removed* |
| R11-F3 the found layer's key completeness | **Yes**, as §31.3 takes it into row 6a, outside this read |
| R11-F4 a let-go line's evidence leaves its tree | **Yes**, with C1; F6 for a line with no record |
| R11-F5 an `installing` mark outliving its install | **Yes** for a run: `permissions` before the mark, the mark gone before `hand`, Daoris's only in an unworked tree. A conversation has no `hand` (F1) |
| R11-F6 the copy's two gates | **Yes**, `started.copy-refused`; a TREELOCAL1 copy with no recorded hash is left, said |
| R11-F7 the person's layer fails open | **Yes**, as §31.3 takes it, outside this read |
| R11-F8 uv's interpreter key | **Yes**, as §31.3 takes it, outside this read |
| R11-F9 nits | **Yes**: a lock per key; Daoris's copies gone before the switch; floor 3 |

### 2. Findings

1. **Blocking (row 3; row 4 through it). A conversation's open writes no `handed`.**
   - **Where:** `handed` is "written in `hand`'s own step, and by the shell in the step that writes a terminal's visit row"
     (Design:3404-3406). `hand` is a run's stage (`Loop/Run/Hand/HandToSession.cs:11`); a conversation opens through `ChatStarts`, which
     has none, with "the same step" as its files stage (Design:3401), and one handed over runs "in the tree the old one ran in", work
     uncommitted and all (`Loop/Chat/ChatStarts.cs:27`, `:35-37`, `:217`). §31.3 counts "a conversation's go-on" as worked; nothing
     writes it so.
   - **What goes wrong:** conversation C opens in a tree of its own, and the stage installs there, rightly. C commits a key input, then
     edits the lockfile, uncommitted. C's account reaches its limit, and the move opens a new part in C's tree: `handed` absent, so
     unworked. Its key moved and missed, the dependency is installed; a repository's `npm install` rewrites the lockfile, and the
     write-back puts the commit's version over C's edit. Row 4 the same at C's return to its own slot. And a failed install's mark goes
     "before `hand`" (Design:3417-3418), which C never reaches.
   - **Smallest change:** the conversation's open writes `handed` in its own step after its files step and before its first turn, and a
     failed install's mark goes before that step. Row 3's proof: a conversation's go-on after a key moved, its uncommitted edit as it was.

2. **Blocking (row 3). An absent `handed` reads as unworked, so every tree standing when row 3 lands is installed in.**
   - **Where:** "A tree whose `handed` is absent is **unworked**" (Design:3406-3407). Row 3 runs the stage "at every start into a tree,
     today's open and TREEGONE1's remake among them" and removes TREEDEPS1 (Design:1908); today's open writes no files record
     (`Workplace/TreeOpening.cs:172`, `:175`), so no tree standing at the landing has one.
   - **What goes wrong:** line L parks on a question in today's tree, an uncommitted lockfile edit in it and a patch under
     `node_modules/x/`. Row 3 lands. L's resume finds no files record: unworked. Its `node_modules/` has no record, so step 2 installs:
     the folder cleared first, L's patch with it, and a lockfile the install rewrites written back from the commit over L's edit. The
     same for a files record that does not read; and wherever a take empties `handed` before its last phase, one refused at `judged`
     (§5.4) leaves the line's own slot unworked.
   - **Smallest change:** hold the mark the other way: `unworked`, written by the making, remaking, take or return in the step that ends
     it, and taken away by `hand`, the conversation's step (F1) and the shell's visit. A tree with no files record, or one that does not
     read, is worked. Row 3's proof: a tree opened before row 3, dirty, resumed after it, left as it was.

3. **Not blocking (row 3). A cut-off install's tracked change is written back only after an install.**
   - **Where:** the write-back follows "an install that changed a tracked file", read from the status before and after (Design:568-570,
     :598-599); a cut-off start's marked folder is "removed and installed again" (Design:3416-3417).
   - **What goes wrong:** a rule's `npm install` rewrites the lockfile, and the desktop restarts before the after-read. At the carry-on
     the tree is unworked, the lockfile changed, and an `ask` the person has since set holds the install: its folder is removed, nothing
     writes back, and the session commits Daoris's lockfile as its own. Where it does run, its before already holds the change.
   - **Smallest change:** in an unworked tree a tracked change, or a path git does not ignore, is Daoris's by the test itself: written
     back from the commit and removed before the first placement, said.

4. **Not blocking (row 3). A group's member is placed alone in a worked tree.**
   - **Where:** a worked tree places "from a hit into an absent folder" (Design:3432); a group "is never placed from the store"
     (Design:579), but its first member's own fill sealed before the later install changed it.
   - **What goes wrong:** A seals, then B's install changes A: one group. S removes A's folder to install it again, and parks. At the
     resume A's key hits, and pristine A is placed beside B's folder without B's change.
   - **Smallest change:** a group's member is placed only by the group's own install in an unworked tree; in a worked tree its absent
     folder is the line's, said.

5. **Not blocking (rows 3 and 4). Floor 4 runs no rule's command in a worked tree, and the key needs one there.**
   - **Where:** "It installs, and runs a command a rule names, only in an unworked tree" (Design:3503-3504); a worked tree "runs no
     command" yet keeps and places by "its key" (Design:3431-3432), whose `toolchain` commands run "at the tree's root" (Design:587-588).
   - **What goes wrong:** either a person's `toolchain` command runs over a line's work against the floor's word, or the key cannot be
     read there and no hit is ever placed. Nothing standard is lost: the signatures' commands print.
   - **Smallest change:** floor 4 says a `toolchain` command runs in a worked tree as a read, only where a folder is absent, its writes
     its rule's to answer for.

6. **Not blocking (row 4). A take's set-aside names no test for its line's finish.**
   - **Where:** §10's round-11 note holds it "until the line's finish", ended by a record naming it (Design:1556-1558); a no-output
     line's empty branch goes once a take switched it, "with no record", and is grown again when its record goes on (Design:1595-1596,
     :1600-1601).
   - **What goes wrong:** read by its branch, a live conversation's set-aside is released at the tidy after the take and goes by days,
     though the line may come back; read by a record, a no-output line's folder is held with no end and under no limit.
   - **Smallest change:** the class holds while the line's record can go on, and ends with it, a no-output line's end included.

### 3. Where I found nothing

- C1: the `Unmarked` folder by days (`Workplace/SetAsideEligibility.cs:69-71`) gives way to a class of the line's.
- C3: nothing installed, written back or run in a run's worked tree; a standing tree still goes on unrestored
  (`Workplace/TreeReturns.cs:59-65`; `Loop/Run/Tree/GrowTree.cs:58-63`), which is why F1's and F2's test matters.
- C4: each value's keyed digest moves the key for a hit and a kept placement alike (`Review/Heavy/HeavyStep.cs:126-142`).
- C5: both of Codex's proofs follow, and a rule added between members moves the later key.
- R11-F2's companions: one another line changed fails its placement's record, so is never handed on; `docs/index/` stays an output
  (`tools/index-hook.mjs:84-88`).

Nothing I found loses committed work. F1 and F2 lose a line's uncommitted work through Daoris's own install, each where the unworked test
misreads a worked tree; the other four are sentences. None reopens an owner's choice.


## Codex, round 11

Codex's narrow read of rows 3 and 4 as round 11 left them, at `107063dfa2`, run by the steward from outside the install, read-only, without seeing the independent Opus reader's; verbatim but for its headings set down.

**Rows 3 and 4: another round, narrowly.** Round 11 answers my five round-10 blockers. One remaining case in F2 can still retain an incomplete install: an install-generated companion is excluded from dependency validation because another rule classifies it as output, then that output is cleared.

Read at `107063dfa2`. This was read-only; no files changed, no `local/` folder read, and no builds or tests run.

1. **Round-10 finding 1 — answered.** C1 distinguishes a script’s reader from its producer. More decisively, floor 4 and §10’s round-11 note preserve a returning line’s set-aside files until its finish, including against size limits. This closes the expiry route visible in `SetAsideEligibility.cs:69–71`.

2. **Round-10 finding 2 — answered.** Floor 2 and C2 add bare-repository recognition, retain unreadable candidates, and require the check before handling a containing unit. That addresses the existing walker’s reliance on `.git` in `SetAsideReads.cs:32–36`.

3. **Round-10 finding 3 — answered.** C3/F1 prohibit installs and their write-back in a worked tree. `handed` is written before agent use, terminal access marks it too, and a returning line’s standing slot explicitly remains worked. This respects existing return handling, which accepts a standing tree without restoring its tracked files (`TreeReturns.cs:59–65`), and dirty conversations (`ChatStarts.cs:215–219`).

4. **Round-10 finding 4 — answered.** Every passed `env` value, including a marked secret, contributes a home-keyed digest. Changing the value changes identity without retaining the plaintext. Secret marking separately controls the sealing scan.

5. **Round-10 finding 5 — answered.** C5’s group binds the affected placements, companions, participating rules and ordered keys. Removing B, changing either member, or rebuilding A at the same key invalidates the group and repeats the installs in order. Keeping groups out of the store avoids independently placing an incomplete member.

**Remaining finding — blocking, rows 3 and 4: a named output can still disappear from a retained install.**

**Where:** R5.6’s round-11 companion bullet, especially its exclusion of paths named by another effective rule; §29.7’s take step 3 and files-stage steps 2–4; §31 F2.

**Concrete sequence:**

- Dependency A’s lifecycle script installs its dependency folder and generates an ignored `generated/client/` folder outside A’s `paths`.
- An effective rule classifies `generated/client/` as `output`. This satisfies C1’s producer test: the lifecycle script actually generates it.
- A’s install is unsealed because it writes outside its namespace. However, `generated/client/` is excluded from its companions because another rule names it.
- The line finishes, or another line takes the slot. The output goes aside; A’s dependency folder stays.
- At the same commit, A’s key, namespace record and recorded companions all hold. The files stage keeps A. It neither repeats A’s lifecycle script nor necessarily runs another producer: step 4 runs only `post-checkout`.
- The next session receives the retained install without its generated client. Its normal compiler command fails against an incomplete installation.

The cited `tools/index-hook.mjs:84–88` explains why this repository’s index can be recreated. It does not establish recreation for an output whose sole producer is the skipped install.

**Smallest change:** Separate output handling from dependency validity. Record install-written paths needed by that install even when another rule controls their kind. Clearing such an output must invalidate the retained install, causing its producer to run before handoff in an unworked tree. Keep the owner’s requirement that outputs are cleared at every take. Prove this with a lifecycle-generated, separately classified output and a same-key take.

I found nothing further blocking in the specified set-aside lifetime, worked-tree preservation, copy hash gates, secret-value identity, group invalidation, or F5’s cleanup before handoff. I found no additional case of a session receiving another line’s files within this narrow scope.


## Opus (independent), round 12

The independent Opus 5.5 reader's last confirming read of rows 3 and 4, at main `557594b07a` (round 12 is `c491406c01`): §29.7,
floor 4, R5.6's and §10's round-11 notes against §32.1, with the code they cite. Main has since changed only the chat start's limit
carry: `Loop/Chat/ChatStarts.cs`'s first turn is now `:328-333`, `ChatLimitCarry.RunAsync` beside it, after the conversation's step.
I built and ran nothing and changed no file but this one; D214's notes of 2026-11-07 and 2026-11-08 bound the read. *R12-Fn* is my
round-11 finding n, *Fn* this read's; a bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and `Design` is
`docs/2026-10-26-tree-pool-design.md`.

**Verdict: 4 findings, 2 blocking, each where the `unworked` mark outlives the hand-off it stands for.**
- **Row 3: another round, narrowly, on F2**: a terminal opened during the files stage takes the mark, and the stage still writes back.
- **Row 4: another round, narrowly, on F1**, and F2 through row 3: a go-on back by a return keeps the mark under its agent.

### 1. My round 11, answered?

| Finding | Round 12 |
|---|---|
| R12-F1 a conversation's open | **Yes for the open**, the limit carry after its step; not a go-on by a return (F1) |
| R12-F2 an absent mark | **Yes**: positive, no record or an unreadable one worked; a running stage misses its removal (F2) |
| R12-F3 a cut-off install's change | **Yes**, step 1 first; F4 where it meets row i |
| R12-F4 a group's member alone | **Yes**, in both kinds of tree |
| R12-F5 `toolchain` in a worked tree | **Yes**, as reads; my narrowing rightly refused |
| R12-F6 a take's set-aside | **Yes**, read from the record (`Workplace/TreeSetAside.cs:17-32`) |
| R11-F2's proof wording | **Yes** in §28.9; §17.0's row 4 still reads *made again*, which a hook's output also meets |
| Codex's C | **Yes** where the stage runs its hook; F3 for a carry-on |

### 2. Findings

1. **Blocking (row 4). A go-on back by a return keeps `unworked` while its agent works.**
   - **Where:** words going on come back to "its last slot where free, switched back to its branch, where its conversation is"
     (Design:1186); a return marks the slot once its check passes (Design:1362-1363); a go-on "removes nothing" (Design:3441-3442),
     resuming at `Course/Chat/GoingOnRun.cs:225`. The open's step it follows came before the return marked the slot again.
   - **What goes wrong:** conversation C is not spoken to for an hour; line B takes its slot and finishes. The person writes to C: the
     return switches the slot back and marks it, the stage installs, C resumes by its kept id, the mark standing. C edits the lockfile,
     uncommitted, and reaches its limit; the new part's stage reads `unworked`, and step 1 writes C's edit back and removes C's new files.
   - **Smallest change:** a go-on into a tree holding `unworked` removes it in its own step before its resumed turn, a failed removal
     holding its words. Proof: that sequence, the edit as it was.

2. **Blocking (row 3). A terminal opened during the files stage takes the mark, and the running install still writes back.**
   - **Where:** the shell removes `unworked` as it writes a visit row (Design:3440-3441), refused only while a claim that moves the slot
     is in flight (Design:1486), so not during the stage, after the claim and outside the hold (Design:3482-3483). The Terminal tab
     opens in the attended session's tree (`src/Daoris.Web/src/work/WorkFrame.tsx:279`), which the record names before the stage. The
     stage reads the mark once (Design:3446-3448); an install's after-read writes back each tracked change, removes each unignored path
     and makes each unnamed ignored one its companion (Design:578-585).
   - **What goes wrong:** during a fresh tree's install (72 to 93 s) the person presses Terminal, edits `src/app.ts` and writes
     `notes.md` and `.env.local`. All three count as the install's: the edit written back, `notes.md` removed, `.env.local` the
     dependency's, kept with it into the next line at a take.
   - **Smallest change:** the files record written under one lock, lest two writers put the mark back, the stage reading the mark
     under it before each after-read, write-back, hook and copy; once a visit has taken it the stage stops, said, writing nothing back
     and listing nothing, the folder left with no record. Proof: a terminal's edit and new file during an install, as they were at
     `hand`.

3. **Not blocking (row 3; row 4's C proof). A carry-on into a still-unworked tree runs no hook.**
   - **Where:** step 4 runs it "where this start switched the tree or made it" (Design:3463); a cut-off start's carry-on installs in a
     tree it did neither to (Design:3479-3480).
   - **What goes wrong:** a take clears `generated/client/` and the desktop restarts before step 4. The carry-on runs no hook; if the
     other-writes read goes with it, C's incomplete install is handed, and if not, only the hook's output is lost.
   - **Smallest change:** the record holds the hook owed from the making or switch until it runs, step 1's write-back owing it again;
     the other-writes read runs at every unworked stage.

4. **Not blocking (row i, not 3 or 4). A lay-back meets step 1.** Row i lays kept work back as phases of the return (Design:1436-1438),
   after a check allowing no tracked change (Design:1385-1388), so after the mark, and step 1 writes it back (Design:3448-3450). A return
   that lays work back writes no `unworked`, or lays it back after the stage: a sentence in §5.5 for the owner's yes.

### 3. Where I found nothing

- A fresh tree and a remake mark at their end; a slot whose start was cut off before `hand` holds a true mark, no agent having run.
- A take across differing ignore rules fails the check (Design:1386-1388), so step 1's *only Daoris's* holds after one.
- A conversation's replacement after a refused set-at-open opens in a tree its first part's step left worked.
- C here: nothing sets `DAORIS_INDEX_HOOK`, a branch's checkout passes the hook (`tools/index-hook.mjs:27`, `:34`), and it writes
  `docs/index/` again, so the root stays kept.

Nothing I found loses committed work. F1 and F2 lose a line's uncommitted work through Daoris's own write-back; F3 costs a hook's
output; F4 is row i's sentence. None reopens an owner's choice.


## Codex, round 12

Codex's confirming read of rows 3 and 4 as round 12 left them, at `f22f391b94`, run by the steward from outside the install, read-only, without seeing the independent Opus reader's; verbatim but for its headings set down.

**Rows 3 and 4: another round, narrowly.** One remaining blocking case can place the wrong install in a worked tree. Read at `f22f391b94`; no files changed, no `local/` folder read, and no builds or tests run.

**My round-11 companion case is answered.** R5.6’s revised note, §29.7 step 4 and §32.1 C record install-written paths classified by another rule as “other writes.” After the hook, an absent other write invalidates the retained placement and repeats its install in the unworked tree. That closes the earlier lifecycle-generated client sequence while preserving output clearing.

1. **Blocking — a worked-tree hit can use a preceding dependency’s expected key despite that dependency’s changed files.**

   **Where:** §29.7’s worked-tree paragraph, particularly lines 3476–3478; R5.6’s round-11 group bullet, whose last sentence retains preceding dependency keys for installs that read earlier folders without changing them.

   **Concrete sequence:**

   - Dependency A installs headers. Dependency B’s install reads A’s headers and compiles files entirely inside B’s namespace. B changes nothing in A, so they form no group. B seals successfully; its key includes A’s key.
   - A session changes A’s ignored headers, then removes B’s folder to rebuild against them. Tracked key inputs remain unchanged.
   - The session resumes. A’s placement record fails, so the worked-tree stage correctly leaves A’s changed files alone.
   - B’s folder is absent, its tracked inputs have no working change, and no group names it. Its calculated key still contains A’s original **expected key**, which has not changed with the ignored header edit.
   - The stage places sealed B, compiled against A’s original headers, beside the session’s changed A. It supplies the wrong install before the session can rebuild B.

   The cited code confirms that standing trees preserve this working state: `Workplace/TreeReturns.cs:59–65` accepts the standing tree, and `Loop/Run/Tree/GrowTree.cs:58–63` selects it for resume or carry-on. §32.1 F4 prevents independent placement of a **group member**, but this read-only dependency relationship creates no group.

   **Smallest change:** Extend the worked-tree hit condition to the preceding dependency state represented in the key. Before placing B, require those dependencies’ actual placements or groups to validate at their current keys, including their recorded companions and other writes. If one is changed, absent or unrecorded, leave B absent and report `started.deps-left`. Add this sequence to row 3’s proof; row 4 inherits it through worked-slot resumes.

I found no further work-loss case in the positive `unworked` mark, its removal before run/conversation/terminal access, or the treatment of missing and unreadable files records. The early write-back now covers a cut-off install even when its retry is held. I found nothing further in the keyed environment values, `toolchain` read allowance, failed-install mark cleanup, or §10’s record-based protection of a returning line’s set-aside files, including a no-output line.

I found no committed-work loss or additional case of a session receiving another line’s files within the specified scope.


## Opus (independent), round 13

The independent Opus 5.5 reader's final confirming read of rows 3 and 4, at main `540de34a85` (round 13 is `d8652720eb`): only the
passages §33.2 names, against §33.1, with the code they cite. I built and ran nothing and changed no file but this one; D214's and
D233's notes bound the read. *R13-Fn* is my round-12 finding n; a bare path is under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and
`Design` is `docs/2026-10-26-tree-pool-design.md`.

**Verdict: rows 3 and 4 ready to build.** Nothing I found loses committed work, writes a line's work back, or places a wrong install.
Two findings, neither blocking.

### 1. My round 12, answered?

| Finding | Round 13 |
|---|---|
| R13-F1 a go-on back by a return | **Yes**: its own removal after the place (`Course/Chat/GoingOnRun.cs:221`) and before the resumed turn (`:225`), a failed one holding its words. Read from the mark, so it also covers a slot a landing's return left marked and free (Design:1207) |
| R13-F2 a terminal during the stage | **Yes** for every write of Daoris's: one lock over the whole record, the shell's removal one writer under it, the mark read again before each write; once a visit has it, nothing is written back or listed |
| R13-F3 the hook on a carry-on | **Yes**, owed in the record until it runs |
| R13-F4 row i's lay-back | **Yes**, no mark written |
| Codex's C | **Yes**: a hit only beside earlier dependencies kept or placed by the stage, so the earlier keys it holds say what stands |

### 2. Findings

1. **Not blocking (row 3). An install or hook already running when a visit takes the mark runs on beside the person.**
   - **Where:** the lock is never held "while an install or the hook runs" (Design:3515), and a visit stops the stage at its next
     write (Design:3516-3519); the command itself runs on, though floor 4 runs one "only in an unworked tree" (Design:3602).
   - **What goes wrong:** the person opens Terminal during a fresh tree's install and edits `package-lock.json`; an `npm install`
     finishing after it writes the file over the edit. Daoris wrote nothing back, but its command overwrote the line's work. It needs
     the edit and the command's own write to meet on one path within the command's remaining run.
   - **Smallest change:** the visit that takes the mark ends the running install's or hook's process tree, its folder left as the
     stop leaves it, said: a build is lost, never an edit. Proof: a terminal's lockfile edit during an install that rewrites it, as it
     was at `hand`.

2. **Not blocking (row 4's proof). Step 4's `git hook run` names no arguments.** This repository's `post-checkout` exits unless its
   third argument is `1` and `HEAD` is a branch (`tools/index-hook.mjs:27`, `:34`). Run bare, it writes no `docs/index/`, so the root's
   other write is absent after every take and its install runs again; row 4's *this repository's root kept* would meet it. Say
   `-- <the old HEAD, or the null id for a fresh tree> <HEAD> 1`.

### 3. Where I found nothing

- The one lock: the `installing` mark taken away after a visit cannot put `unworked` back.
- A placement or copy into an absent target in a worked tree, where a terminal may stand open, stays create-only as today's are
  (`Workplace/TreeLocalInputs.cs:180`; `Workplace/TreeDependencies.cs:186`), so a target the person made after the check is left.
- A worked tree's hit beside an earlier dependency kept with a working change to its key input: that dependency is at its record.
- Today's open runs `post-checkout` inside `worktree add` (`Workplace/TreeOpening.cs:133`); step 4's *made with hooks held* is row 3's
  change there.

None of it reopens an owner's choice.
