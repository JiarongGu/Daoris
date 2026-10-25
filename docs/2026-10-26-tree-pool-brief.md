# Session trees kept and reused, each session's work a branch — the brief

> TREEPOOL1, phase 1 of a co-design under D191 (2026-10-26). This brief is the one document every proposal is written from. It holds the
> direction, the process, what the design must settle, the constraints a proposal keeps, what is out of scope, the questions every proposal
> answers, the proof the design names, what must not regress, and how the work lands. The facts of today are in
> `docs/2026-10-26-tree-pool-survey.md`, by `file:line`, read at `6b999d6b1`, and are not repeated here: a proposal cites the survey's section
> and the code's line, never this brief, for a fact. The proposals will be one file per maker, `docs/2026-10-26-tree-pool-proposal-<maker>.md`,
> each written from this brief and the survey alone and sealed before its author reads another; a synthesis follows. `Dnn` is
> `docs/decisions/Dnn.md`. Paths follow the survey's aliases.

## 1. The direction

**Decided (the owner's direction, 2026-10-25, TREEPOOL1's row in `TASKS.md`):** a session tree is a working copy of its repository, and
writing a whole copy for each session and removing it days later wears the disk for nothing. So each repository keeps a **pool of trees**:

1. **A session takes a free tree and holds it while it works**, locked, so no other session writes in it.
2. **At its start the tree is brought to the line**: reset to the line's head, its untracked work cleared, its ignored dependency and build
   folders kept, so the session starts clean and pays no setup it already paid.
3. **The session works on a branch of its own.**
4. **At its end the tree is let go back to the pool**, with the work on that branch, ready to land; or with no branch where it was done with
   no output. *Done with output* and *done without output* are both ends a person sees.
5. **The pool is a managed number of slots**, set by the repository's configuration or Daoris's, at most one slot per session the repository
   may run at once.
6. **A slot's tree is made lazily**, the first time a session needs that slot.
7. **Trees are removed only when the configured number of slots is lowered or the repository leaves**, never by age.

The steward's reading, which the survey tests against the code: today a tree is opened once for one line of work and never reused (D51 rule
2), its folder's name is its branch's name, its path is the line's identity to a dozen readers, and its end is a move aside that KEEPSIZE1b
deletes an hour after the finished record is kept, or fourteen days later otherwise (survey §0, §1.4). The measured price is the setup a fresh
tree pays (`npm ci` 170 to 221 s, .NET `bin/` and `obj/` built again, survey §4) and the bytes a moved-aside tree holds (about 205 MB each, 27
GB a day before KEEPSIZE1b, survey §0).

**The part of the company it makes work** (D193, `docs/2026-10-21-company-concept.md` §2): the workspace and equipment a staff member is
handed (part 14), set up once and kept, handed to the next member clean, rather than built and thrown away for every task.

## 2. The process

D191 governs it, with its amendments and its note of 2026-10-24. The design changes decided rules other designs rest on (D51 rule 2's *never
reused*, D167 point 4's tree of an entry, D189 point 2's resume where *the same agent, account and tree* hold, D195's kinds and their days,
D88's tidy, D178's kept take), so:

1. **Two sealed proposals**, each from this brief and the survey alone: Codex's and an independent Opus reader's. No author reads another's
   before its own is sealed.
2. **One synthesis**, written by Claude Opus as a proposed decision and a design document, comparing the proposals question by question (§7)
   and saying what each choice rejects.
3. **Both makers read it**: Codex, and the independent Opus reader as a foundation read. Each finding names the file and line it rests on, and
   the steward checks each against the code before it is taken.
4. **Rounds until both makers' blocking findings are taken.** No part that deletes a person's work, or a record, is accepted on one maker's read.
5. **Rows**, each its own branch, each behind today's tests (§9), none touching `Trees/` before FOUNDATION1's row mod-x (the Workplace move)
   has landed (D212 §14.6).

**What the owner is still asked** (D191 point 3): a change of the product's direction; anything that spends the owner's accounts or money
beyond the development's own runs; a sign-in; anything that publishes, pushes or rewrites history; **anything that loses the owner's data**,
which here means any step that removes a tree, a branch or a folder the install holds today, or resets a tree that holds work. A proposal that
needs one of these says so, in a list of its own, each question saying what it is, what a yes does and where to look.

## 3. The inputs

| Design | Standing | What it brings |
|---|---|---|
| FOUNDATION1, D212, `docs/2026-10-25-foundation-design.md` | Part A building (mod-vi landed; mod-x, Workplace, ahead); Part B accepted, its rows building | the module map (`Trees/` is Workplace, subdomain 10), the desktop store and its families (§13), the move record and the entry (§3), the module-initializer seams the steward's note of 2026-10-26 reads |
| D51 and its notes, `docs/2026-09-21-working-surface-design.md` | accepted | the working surface's nine rules: a tree per session, named for it and never reused (rule 2), the price stated in the sentence that creates it (rule 4), the clean-tree rule; TREEDEPS1, TREELINK1, TREELOCK1, TREEGONE1 |
| D88, D102, D195 (CLEANUP1, KEEP1, KEEPSIZE1a/b), `docs/2026-10-24-keep-size-design.md` | accepted, built; KEEPSIZE1c-f open | the tidy, the move aside, the manifest and the finished record, the kinds and their days, the slim |
| D167, D178, D189 | accepted | the entry a tree belongs to, the lost take kept, a resume in the same agent, account and tree |
| SWITCHCTX1, `docs/2026-10-24-switch-context-design.md` | proposed | where each agent keeps its conversation, by working directory for Claude Code (§1.5) |
| D115 (lanes, `laneCap`), DEV6 | decided, unbuilt | a per-repository count of lanes, the nearest precedent for a count |
| LINESTOGETHER1, ACCEPTCONFLICT1, REVIEWFLOW1 (backlog) | filed | how a line's parts land together; a proposal says whether a pool changes them, and changes none of them here |

## 4. What the design must settle

The survey's §8 names fourteen tangles; each is a part a proposal places. In short:

| Part | What a proposal answers | Survey |
|---|---|---|
| **The slot and the branch** | a slot's folder name, a session's branch name, and how every reader that derives one from the other reads them | §6, §8.1 |
| **The line's identity** | what identifies a line of work once a path holds many: its branch, its entry, a slot lease; how a resume, a carry-on, a further part, a hand-over, a limit move, a lost take and TREEGONE1 find their tree | §2, §8.2 |
| **The readers keyed by path** | each of the forty-odd readers, kept, re-keyed or cut | §6, §8.3 |
| **What holds a slot** | a parked record, words waiting, a helper wait, a served review build, a kept lost take, a D79 wait, a tree that never moved: which hold a slot, for how long, and what a person sees | §8.4 |
| **The count** | where the number of slots is declared (the repository's `daoris.json`, Daoris's `driver.json`, the registry), its default, how it meets the machine's cap, lanes and the person's conversations | §3, §8.5 |
| **The end and the reset** | what a session's end must leave committed; what *brought to the line* clears and keeps; the one list of build and dependency folders | §1.3, §4, §8.6 |
| **The finished record and removal** | where KEEPSIZE1b's record, the recovery ref and the branch's deletion happen once trees no longer end; D195's kinds | §1.4, §8.7 |
| **Git and Windows** | a branch checked out elsewhere, the four lock reasons, `daoris/`'s prefix, the start window, a reset refused by a held file | §5, §8.8, §8.9 |
| **Dependencies kept** | a kept `node_modules` or `bin/` at another lockfile, and what the agent is told | §1.1, §8.10 |
| **The words** | the opening's sentences and codes, *done with output* and *done without output*, en and zh | §1.1, §8.11 |
| **The neighbours** | the `o-`, `p-` and `r-` folders beside the slots, the strays tidy | §2, §8.12 |
| **The install's trees today** | how the trees the install holds now become slots or go, and who decides | — |

## 5. The constraints a proposal keeps

Each is decided and stands, unless a proposal argues to amend it, saying which and why; the amendment is appended to the decision as a dated
note, never rewritten into it.

**The doctrine** (`AGENTS.md`).
- **A session changes its own repository**, and work for another goes as a quest (D45). A pool is per repository; no slot of one repository
  ever serves another's session.
- **Local operation needs no remote.** A slot is brought to the local line; nothing fetches or pushes to make a slot ready.
- **Machine-local stays on the desktop**: slots, their paths and their leases are the desktop's; a shared host learns no path (`.claude/knowledge/session-record-fields.md`).
- **No machine path** in a tracked file, a record that travels or an event that leaves the machine.

**The person's work is never lost.**
- **Uncommitted work keeps a tree today at every door** (survey §1.3): a landing refuses, an unforced removal refuses, the tidy keeps it. A
  reset that clears untracked work is a deletion; a proposal says what makes it safe (a refusal, a commit Daoris makes, a recovery ref, a
  kept copy) and never resets a tree that holds work no ref holds.
- **A branch's work stays reachable** as today's recovery refs keep it (`refs/daoris/discarded/`, survey §5).
- **Nothing the person made in a tree** (a person's lock, a link, a nested repository, survey §1.4) is cleared by a reset.

**FOUNDATION1's map and Part B** (D212).
- **`Trees/` is Workplace** (§14.2 row 10). A pool names only Workplace's types and those below it; a higher subdomain reads it through a
  port. A new record family goes in the desktop store by Part B's rules (§13), where a proposal keeps one; the owner prefers the store for
  records the product reads and sets (the steward's reading of the owner's word on FOUNDATION1's store, 2026-10-25).
- **No row touching `Trees/` lands while mod-x holds it** (D212 §14.6).

**Agents keep their conversations by working directory** (SWITCHCTX1 §1.5; survey §6's agents' homes row): Claude Code files a conversation
under its working directory's path and keeps its trust by path, so a resume in another directory does not find the conversation. A proposal
says where a resumable line runs once its slot may have been taken, and keeps D189's resume-first rule or amends it by a note.

**Compatible builds** (`.claude/knowledge/compatible-builds.md`): every record that names a tree path (survey §6) is read by builds already
shipped; a path an older build reads keeps meaning what it read, and a new meaning goes where those builds cannot see it.

**The install's live home**: the installed build holds trees and moved-aside folders now. Nothing in the design's work reads, writes or
removes them; development never sets or uses `DAORIS_HOME`; a row's migration of the install's trees is the parent's, on the install, after
the owner's yes where it removes anything (§2).

**Simple** (D193): a person who sets nothing sees sessions start faster and the disk stop filling, and no new setting they must understand.

## 6. Out of scope

- **Rooms** (intake, Ask Daoris, brainstorms, `work/rooms/`, survey §2): not session trees; unchanged.
- **The clones and replays** (`o-` opinions and helpers, `p-` partners, `r-` replays): their own folders, unchanged but for how the pool's
  folders sit beside them (§4's neighbours).
- **How a line's parts land** (LINESTOGETHER1, ACCEPTCONFLICT1, GITAPP1): a proposal says whether a pool changes a landing's steps, and
  designs none of them.
- **KEEPSIZE1's allowance for the other kinds** (rows 1c to 1f): kept; a proposal says what of it a pool makes moot for session trees.
- **A repository without trees** (one session at a time in its checkout, survey §3): unchanged, unless a proposal argues it becomes a pool of
  one.

## 7. The questions every proposal answers

A proposal answers each, citing the survey's section and the code's line for every fact it rests on, and says where it amends a decision.

**The shape.**
1. **The slot.** What a slot is (a linked worktree at a stable folder, `s-<n>` or another name), what identifies it, and what records its
   lease: a file, the desktop store, git's own lock, or several. How a lease is taken and let go across a crash, and by whom (D201's launcher,
   the loop, the start).
2. **The line and its branch.** How a session's branch is named and grown, and how every reader that today derives the branch from the folder
   (`TreeFollows.BranchOfTree` and its twenty-odd callers, the page's two sites, survey §6) learns it; TREEFOLLOW1's `GrownBranch` as the
   truth, or another.
3. **The line's identity.** Which records and readers key a line by its tree's path today (survey §6, §8.2, §8.3), and what each keys by in a
   pool. A resume, a carry-on, the person's words going on, a further part, a hand-over, a limit move, a lost take brought forward, TREEGONE1:
   where each runs when its slot is free, and when another session holds it.
4. **The agent's conversation.** Where a resumable line runs once its slot may be taken, given that Claude Code keeps a conversation by its
   working directory and trust by path: the line keeps its slot while resumable, the conversation follows it, or the resume becomes a brief
   (D189); with its cost to D189's resume-first rule.

**The lifecycle.**
5. **The start.** *Brought to the line*: which git operations, in which order, and what each keeps (ignored dependency and build folders, the
   person's locks and links) and clears (tracked changes, untracked files); the one list of build and dependency folders replacing today's
   three (survey §4); what happens when the reset is refused (a held file on Windows, a nested repository).
6. **The end.** What a session's end must leave: committed work on its branch, nothing uncommitted, or a defined way to keep what is
   uncommitted; who commits, if Daoris ever does (today never, D164); *done with output* (a branch ready to land) and *done without output*
   (no branch, the slot free) as the person sees them.
7. **What holds a slot.** For each holder of survey §8.4, whether it holds a slot, for how long, what frees it, and what a person sees while
   a repository's slots are all held.
8. **The count.** Where the number is declared, its default, and how it meets the machine's cap (CAPCOUNT1), D115's lanes, set-ups'
   `atOnce`, and the person's conversations (today uncounted and their trees unlocked, D51:137); what *a slot per session the repository may
   run at once* means for each.
9. **Lazy creation and lowering.** A slot made the first time it is needed; lowering the count with slots busy or holding work; a repository
   leaving; and the folders today's trees leave, the install's among them.

**Across the design.**
10. **The finished record and removal.** Where KEEPSIZE1b's work record, the manifest, the recovery ref and the branch's deletion happen once a
    tree no longer ends; D195's `trees` and `set-aside` kinds; the tidy's `GoesByItself`.
11. **Dependencies.** A kept `node_modules`, `bin/` or `obj/` from a previous session at another lockfile or another commit: kept, refreshed or
    cleared; what the agent is told (TREEDEPS1's sentence), and what TREEGONE1's remake does.
12. **Git's rules and Windows.** A branch checked out in another slot; the four lock reasons; `daoris/`'s prefix in every proof; the start
    window and the ledger's lock key; a reset or switch refused by a held file; long paths; junctions in a kept `node_modules`.
13. **D51.** D51 rejected one directory switching branches because it serialises what runs at once and destroys the person's working state;
    working-surface rule 2 says a branch is never reused. How the pool stands against both, and the note it appends.
14. **Records and older builds.** Every record of survey §6: kept, re-keyed or moved; how an older build reads a slot's path or a line it did
    not open.
15. **The words.** The opening's sentences and codes (a stored shape is never changed in place), the end's two kinds, a repository whose slots
    are all held, en and zh, within the page's budget.
16. **The install's trees.** How the trees and moved-aside folders the install holds become slots or go, and what is the owner's to decide.
17. **The order.** The rows, after mod-x, each landing alone, the first small enough to prove the shape; how each input of §3 folds in.
18. **What is the owner's.** Every point that needs D191 point 3's answer.
19. **Proof.** Per row: the unit, git and Process cases, the rehearsals, and what is measured on the install.

## 8. The proof the design names

At least:

- **A second session takes the first one's slot at the line**, with the first's ignored dependency and build folders kept and none of its
  work: a driver test on a git fixture, the first session's branch holding its commits, its untracked file gone from the slot and kept where
  the design keeps it.
- **No work lost**: a slot holding uncommitted or unreferenced work is never reset; each of survey §8.4's holders holds as the design says.
- **The readers**: every reader of survey §6 answering a pooled line as it answered a tree of its own, a fixture per reader the design
  re-keys.
- **Older builds**: a record a shipped build writes, and one the pool writes, each read by the other as the design says.
- **The install**: its disk measured before and after a day's work, and the time from a session's start to its first turn, before and after.

## 9. What must not regress

Every row keeps today's suites green, as `daoris.gates.json` declares them, and adds its own. The suites that hold today's trees, which a row
touching that part runs whole, not by sample: `SessionTreesTests`, `TreeDependenciesTests`, `TreeReturnsTests`, `SessionLocksTests`,
`SessionTreeLockTickTests`, `StartTreeCloseTests`, `PlannerTests`, `BranchTidyingTests`, `BranchTidyingFolderTests`, `CleanupLeftoversTests`,
`KeepingTests`, `KeepingSlimTests`, `KeepSizeGitTests`, `KeepSizeStopsGitTests`, `SystemSpaceTests`, `EntryLandingsTests`,
`AutoLandingTriesTests`, `AutoLandingRulesTests`, the service's `WorkRecordStoreTests` and `SessionSyncTests`, the page's `identity.test.ts`
and `noteLines.test.ts`, and the tick tests that start real sessions in fresh trees (survey §7), with the git half of the real-process suites
and the family and deployment rehearsals.

And the behaviours the install relies on, each with a case before the row that could break it: a session's work lands as today; a resume
finds its conversation; a parked session keeps its tree; a lost take is kept; the tidy never removes work a ref does not hold; a person's lock
and a link in a tree are never touched.

## 10. How the work lands

**Never a big-bang rewrite.**

1. **Name the slot and its lease beside today's trees**, with a census of every reader of survey §6 as not yet moved, moving none.
2. **Move one reader family at a time** onto the line's new identity, each behind its own tests.
3. **The pool switched on per repository**, off by default until its proofs pass, then on for the repository whose owner opts in, the
   install's first.
4. **A disagreement found is a decision, not a refactor**: where the survey shows two lists or two rules for one fact (the three build-folder
   lists), the row that unifies them says which stands, as a dated note.
5. **Removal last**: no row removes a tree or folder the install holds until the owner's yes (§2).

A proposal names its first three rows concretely, with their proofs, and the rest in order.
