# TREEPOOL1: the reads of round 1 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-26-tree-pool-design.md` and D214, round 1, both at
`3f55eb1e5`, each run by the steward from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one
level down. Neither carries a link, so none is rewritten; the paths both cite are code spans, in each read's own aliases. Round 2 answers every
finding of both, one row each, and each changed passage of the design and D214 is marked *(C1-n.m)* for Codex's §n item m or *(O1-Fn)* for
the Opus reader's finding n.

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
