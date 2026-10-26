# TREEPOOL1: the reads of rounds 1 and 2 (D191)

Codex's read and the independent Opus 5.5 reader's foundation read of `docs/2026-10-26-tree-pool-design.md` and D214, round 1, both at
`3f55eb1e5`, each run by the steward from outside the install, read-only, neither seeing the other's, verbatim but for their headings set one
level down. Neither carries a link, so none is rewritten; the paths both cite are code spans, in each read's own aliases. Round 2 answers every
finding of both, one row each, and each changed passage of the design and D214 is marked *(C1-n.m)* for Codex's §n item m or *(O1-Fn)* for
the Opus reader's finding n.

Both makers' reads of round 2, at `d595cabd6`, follow in the same way. Round 3 answers every finding of both, one row each, and each changed passage of the design and D214 is marked *(C2-n.m)* for Codex's §n item m or *(O2-Fn)* for the Opus reader's finding n.

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
