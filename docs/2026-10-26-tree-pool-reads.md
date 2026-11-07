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
