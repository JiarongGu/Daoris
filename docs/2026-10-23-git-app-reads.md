# GITAPP1: the two reads of round 1 (D191)

Codex's read and Fable's foundation read of `docs/2026-10-23-git-app-design.md`, D202, the brief and the three proposals at
`92c7b8ed5`, verbatim but for their headings set one level down; the parent ran both from outside the install, Codex read-only.
Neither opened the other's read. Round 2 answers each finding in the design's reads table.

## Codex
### 1. Every claim about today’s code

Locations below use **Design** for `docs/2026-10-23-git-app-design.md`, **Brief** for `docs/2026-10-23-git-app-brief.md`, **Driver** for `src/Daoris.Desktop/Daoris.Desktop.Driver/`, **Modules** for `src/Daoris.Desktop/Daoris.Desktop.Modules/`, and **Web** for `src/Daoris.Web/src/`.

The central code observations hold: Review lacks optional-lock suppression; the counts are unions; HANDOFF_PLAN is eager while LANDING depends on the diff; committed patches already come from one streamed range read; the detail/history/blame/compare backends exist without their screens; and Review shares neither the screens’ cache nor their reader. The substantive corrections and qualifications follow.

1. **Non-blocking — “13–19 + U” is neither the minimum nor a ceiling.**  
   Design:78,139 and D202’s introductory measurement paragraph repeat this range. Counting `SESSION_DIFF` itself, for the configured-line, valid-recorded-base, two-readable-line-refs case:

   | Part | Starts |
   |---|---:|
   | Uncommitted | `3 + Uᵣ` |
   | Range | `6 + I + M` |
   | Committed | `2` |
   | Branch | `1` |
   | Total before landing standing/offer | **`12 + I + M + Uᵣ`** |

   `I` is one independence read when candidates differ; `M` is one incomparable-base merge. Thus the ordinary minimum is **12**, commonly **13**, and **14** with the merge. `Uᵣ` counts additions actually sent to git before the patch budget stops further reads; it can be smaller than the untracked-file count.

   Evidence: `Driver/Trees/WorkingTree.cs:326,329,335,351,358`, `Driver/Trees/SessionTrees/ReviewRanges.cs:40,42,55,72,77,125`, and `Modules/Bridge/Routes/TreesRoutes.cs:80,88`. The offer adds six preliminary starts (`TreeWorkReads.cs:40`, `TreeFollows.cs:31,32,64`), **then** squash/content proofs. `ContentHolds.cs:163,166,177` adds a comparison per eligible branch, so “21 more” is not a bounded runtime count.

   Warm driver calls repeat that work; there is no Review-result cache. A fresh live-page answer, or an ended-page answer read after its last record move, makes **zero SESSION_DIFF starts**; a stale live answer reruns the chain, including bounded untracked reads (`Web/bridge/sessions/trees.ts:168,182`). A gone-tree, standing landed branch with a valid recorded `From` costs **seven** starts cold and on another backend invocation (`LandedReviews.cs:35,48,53,77,80,63`; `WorkingTree.cs:420`). A landing standing read beside an existing tree adds three.

   **Smallest fix:** publish this conditional count, distinguish page hits from driver hits, and label the brief’s eighteen-call probe as a replay rather than a complete endpoint trace.

2. **Non-blocking — the brief’s authentication inventory is already stale at the design’s stated base.**  
   Brief:155 says the identities store is unbuilt. `Driver/Platform/GitIdentities.cs:51,165`, `GitIdentityCommand.cs:30`, and `GitAuthor.cs:18` implement the store, verbs and author rule; D198:367 records GITAUTH1c part one. Conversely, compiled doors do not mean ordinary installs route through them: `tools/desktop-publish.mjs:1326,1329,1340` still makes their installation opt-in.

   Design:40 claims its relied-on inventory was checked at `fe4809b75`; these are not subsequent code changes.

   **Smallest fix:** describe GITAUTH1c as partly built, retain its remaining proofs, and state the installation prerequisite beside the remote-act rows.

3. **Non-blocking — “the ref watch hears every ref” is false; its selection is narrower than “repositories with work.”**  
   Brief §6 point 3 overstates what Design:250 correctly starts qualifying. `RefWatch.cs:235` hears heads/remotes and HEAD, packed refs and reftable paths, but not loose tags, replace refs or stash reflog changes. `RefWatch.cs:260` selects registered **main checkouts with a `.git` directory**; a registered linked checkout is omitted. Rebuild also groups by repository name (`RefWatch.cs:291`), rather than common-directory identity.

   **Smallest fix:** correct the brief and explicitly include common-directory resolution and repository identity in the widened watch’s work.

4. **Non-blocking — the measurements establish expensive process latency, not its complete causal attribution.**  
   Design:13,68 and D202 say the scanner causes the measured start cost. PROC1a (`docs/FIX-LOG.md:5226`) separates `Process.Start` time from whole-process time; UIFREEZE1 (`:3374`) establishes the runtime lock and a slow `CreateProcess`. Those support the proposed diagnosis, but do not attribute every measured 0.2–1.6 seconds to scanning. The proposals’ probes also use different hosts and loads.

   **Smallest fix:** call scanner/load attribution an inference supported by the stack and probes. Keep gate wait, process creation and child runtime separate in the new measurements.

### 2. The shape

One reader per common directory, shared immutable results, bounded jobs, independent action plans and git’s existing binary are a sound direction. Keeping session trees exclusively theirs is the strongest synthesis choice. The checkout switch serves the person’s actual edits better than a separate empty manual tree, provided its exclusion and recovery contracts are completed. Preserving union counts and REVIEWBASE3 over the four-call shortcut is also correct.

5. **Blocking — the specified cold dependency graph is deeper than three rounds.**  
   Design:119–132 resolves an uncached refs snapshot **before** round 1. Within round 2, `merge-tree` cannot start until `--independent` determines incomparability. Today that dependency is explicit at `ReviewRanges.cs:72,75,77`. Therefore the ordinary cold path has snapshot → initial questions → independence → diff; the incomparable path adds another dependent stage. Untracked disk reads also depend on status supplying their names.

   **Smallest fix:** distinguish logical scheduling groups from dependent depth. Either amend the three-round promise, or provide a different algorithm and count any speculative work it starts. Move the union counts alongside other reads once their pinned tips are known; they do not depend on `from`.

6. **Blocking — the latency targets do not follow from the stated arithmetic or gate.**  
   Design:180–185 treats three rounds plus a 1.0–2.6-second diff as approximately three seconds. A permitted trace of 0.8-second snapshot, merge-base and independence reads followed by a 2.6-second diff is already five seconds, before queueing, status, disk additions or paint. The incomparable path is longer. `ProcessStarts.cs:29,136` gates **creation**, whereas Design:210 also appears to bound whole reader jobs; those are different schedulers.

   The 1,771-file case is feasible as a **complete file list with bounded patches**, not a complete 16.6 MB patch: `WorkingTree.cs:271,274,451`. Neither ≤0.1-second reopen nor ≤3 seconds after a commit follows without reliable retained observations and queue measurements.

   **Smallest fix:** retain the numbers as provisional acceptance targets, remove the claim that arithmetic proves them, define reader-slot lifetime, and benchmark the actual dependency graph through the shared gate. Codex’s more conservative targets were better justified by the available evidence.

7. **Blocking — a per-reader bound alone does not reserve capacity for Review.**  
   Design:210–212 says a long read holding one of three places prevents starvation. Three simultaneous long reads can hold all three; several repositories can also saturate the shared background queue. The existing queue gives priority to `Person`, not to pressed Review over other background work (`ProcessStarts.cs:192,223`).

   **Smallest fix:** specify an aggregate walk limit and foreground priority within the reader scheduler, with at least one same-repository place available to short attended reads. Keep the existing person reservation.

8. **Non-blocking — “every read” and “as comprehensive” exceed the enumerated product contract.**  
   Design:326 and D202 point 6 promise every management-app read, while Design:333 explicitly excludes image comparison, and §7–§9 do not provide the brief’s interactive reorder/squash/reword/drop workflow. Some omissions are reasonable, but “all proposals offer every read” also hides their actual differences.

   **Smallest fix:** make §7.1 the authoritative capability boundary, explicitly list deferred capabilities, and describe object rebasing separately from interactive history editing.

### 3. What is missing or unsafe

9. **Blocking — the lock-suppression commands put a global option after the subcommand.**  
   Design:130,292 writes `status --no-optional-locks` and `diff --no-optional-locks`. The option belongs before `status` or `diff`. Existing correct examples are `HelperCapture.cs:104` and `HelpCheckouts.cs:104`.

   **Smallest fix:** spell every command as `git --no-optional-locks status …` or `git --no-optional-locks diff …`; make the census verify placement, not mere presence.

10. **Blocking — REVIEWLOCK1’s described census proves less than the claimed absence of writes.**  
    Design:61,452–463 needs to cover both streaming and ordinary runners, including `WorkingTree.CleanAsync`, `UncommittedAsync`, `UnmodifiedAsync`, `TreeWorkReads.UncommittedOfAsync`, `ContentHolds.HoldsAsync`, `TreeSlims` and landing/judgement callers. Relevant unsuppressed sites include `WorkingTree.cs:166,187,205,335`, `TreeWorkReads.cs:77`, `ContentHolds.cs:497`, and `TreeSlims.cs:27`.

    Checking `status`/`diff` arguments alone does not control configured programs. The range command disables external diff but does **not** disable textconv (`WorkingTree.cs:472`); a cached textconv can write refs, and configured filters, fsmonitor hooks or merge drivers can have effects. `merge-tree --write-tree` is consequently not an unconditional “objects only” guarantee under arbitrary driver configuration.

    Conversely, `rev-parse --show-toplevel`, `worktree list`, ordinary config getters and two-object name diffs should not be falsely classified as index writers.

    **Smallest fix:** define a read-command effect policy, explicitly address textconv/filter/driver execution, and census every launch seam with narrowly justified write exceptions. Add real fixtures for the configured-program paths, not just stat-stale status/diff.

11. **Blocking — write time plus size cannot make the refs snapshot authoritative.**  
    Design:244–255 claims stamps catch moves missed by the watch. Same-length ref contents with indistinguishable timestamps defeat that claim; an in-place `packed-refs` rewrite can do likewise. There is also a race between stamping and enumeration, and another between validation and use. Linked-tree membership and changes to `commondir` are not covered by stamping the checkout’s `.git` entry alone.

    A stash list additionally depends on `logs/refs/stash`: dropping an older stash need not change its tip.

    **Smallest fix:** use watch generations plus content validation of mutable ref-store inputs, validate before and after snapshot construction, and fail closed on unread layouts. Include linked-tree membership, per-tree HEAD and stash-log state. Reftable must remain an explicitly counted fallback. Describe the result as an identified observation, not an atomic view of external git.

12. **Blocking — the cache context remains underspecified, especially across linked trees and frontend caches.**  
    Design:261–273 shares results by common directory, but two linked trees can have different `.gitattributes`, worktree config, ignore-revision files and mailmaps. Other dependencies include included/global/system config, `info/attributes`, attribute files, rename thresholds/limits, diff algorithm, EOL/filter settings and the selected git implementation.

    The claim that `blame.ignoreRevsFile` is already supplied on the call is wrong: `GitReads.Formats.cs:73` supplies only quote-path and encoding options. Current keys omit context (`GitReads.cs:492`), while frontend detail/history/blame/compare answers remain infinitely fresh (`Web/bridge/repositories/git.ts:262`). Fixing the driver alone leaves those stale.

    **Smallest fix:** define a context per read category, freeze supported options and disable unsupported transformations, include worktree identity where necessary, and validate context before cache **lookup**, joining and completion. Propagate a context revision or invalidation to frontend caches. Test two linked trees with identical object IDs and different attributes/configuration.

13. **Blocking — the partial-clone fallback still permits an implicit network call.**  
    Design:275–277 disables caching when git does not honour `GIT_NO_LAZY_FETCH`; that does not stop git fetching the missing object. Saying “objects may be missing” afterwards cannot restore the local-read contract.

    **Smallest fix:** prove suppression for the selected git, and otherwise refuse object-dependent local reads before starting them. Fetching missing objects must be an explicit remote act through D198.

14. **Blocking — the snapshot, dirty diff and joining contract can describe different HEADs.**  
    Design:130 pins range questions to resolved IDs but leaves the dirty diff reading the live name `HEAD`. A commit between those reads can produce a patch against one HEAD and a committed range/counts against another. Merely returning `head`, or refusing an older completion, does not detect that mixture.

    The page currently joins by session (`trees.ts:81–108`); its key cannot become only `session + head`, because dirty files and line tips can change without HEAD changing (`queries.ts:146`).

    **Smallest fix:** pin the dirty comparison too, capture status’s branch/OID information, and validate the observation generation after the reads. Define joining by IDs **and context**, with separate waiter cancellation. Keep mutable session observations distinct from immutable patch keys; bound retries and expose “changed during read.”

15. **Blocking — zero-start tree freshness lacks a safe fallback for several ordinary cases.**  
    Design:142 promises ended sessions cost zero, while Design:316 watches only live sessions. Ended session trees can retain uncommitted files and can still be edited; today `TreesRoutes.cs:70` reads their dirty state whenever their own tree remains.

    Filtering ignored folders also misses changes to tracked files inside them unless exclusions are proven safe. Ignore-rule changes require rebuilding exclusions. Index stamps do not detect unstaged edits, and a capped, failed or idle-released watch cannot validate zero-start reopening.

    **Smallest fix:** watch every attended mutable tree regardless of session state; install the watch before taking the retained observation. Define safe exclusions and invalidation for ignore/config changes. On watch loss, cap, release or resume, perform an explicitly counted validation read before claiming freshness.

16. **Blocking — invalidation follows too few dependencies and no complete publication contract.**  
    Design:160–163,526–529 wakes sessions “on the moved branch.” A line or origin-line move affects reviews on **other** branches; a tree can also switch to a branch the original layout did not name, or detach. `TreeFollows.cs:152` derives the original branch from the folder, while D164’s TREEFOLLOW1 explicitly follows later movement.

    The design names the `diff` scope but does not complete invalidation for the branch list, holding-ref decorations, stash list or other mutable screen reads. `Web/bridge/state/stateHub.ts:31` supplies invalidation keys, not new IDs.

    **Smallest fix:** define dependencies by common repository plus actual per-tree HEAD and configured line; invalidate every dependent review on line movement. Name the mutable screen families, publisher wiring and missed-event reconciliation. Pre-read and refetch must join the same generation rather than race to publish different answers.

17. **Blocking — the hand-built untracked patch contract is not byte-equal to git as specified.**  
    Design:304–312 hardcodes `100644` and an ordinary-file hunk. Git has special output for empty files, executable files and symlinks, and quotes special path characters. Text/binary classification and content can depend on attributes and conversions. A symlink target must not become file content merely because Windows represents it differently.

    Today’s oracle also fixes quote-path, prefixes, colour and external-diff options (`WorkingTree.cs:386`); Design:459’s abbreviated oracle does not. Budget accounting must stop allocations as well as truncate the eventual string.

    **Smallest fix:** adopt Codex’s bounded **display addition** contract, exact only for a documented ordinary-text subset, and never use it as the staging patch. Otherwise specify all headers, modes, quoting, conversion rules and exceptional forms. Add empty/exec/symlink/attribute/filter/EOL fixtures and enforce the existing **200 KiB**, per-file and aggregate limits during reading.

18. **Blocking — the held-process protocol and lifetime are incomplete.**  
    Design:217–224 gives idle time and an OID echo, but not byte framing, binary payload lengths, stderr draining, per-question deadlines, cancellation, EOF, context replacement, shutdown ownership or restarting after partial consumption. Matching an OID cannot recover framing after an abandoned blob response.

    The advertised four readers “across the machine” also cannot be enforced by registries that exist independently per process (Design:193–195,236).

    **Smallest fix:** specify a framed, bounded request protocol and an owner that drains or retires on cancellation and kills/waits on exit. Either make the cap explicitly per process or provide cross-process coordination. Test page close, window exit, cancelled creation and a cancelled middle response, plus concurrent repack on Windows.

19. **Non-blocking — cached synthetic tree IDs need an object-lifetime rule.**  
    Design:262 keeps `merge-tree` answers until eviction, but their generated trees may have no retaining ref. `ReviewRanges.cs:92` creates such objects. A later diff can therefore find its cached synthetic base pruned, even though the input commits still exist.

    This is distinct from pack handles: ordinary object writing does not itself hold packs open indefinitely. Aggressive concurrent pruning is also a general git-writer hazard, not evidence that every concurrent `gc` is unsafe.

    **Smallest fix:** treat a missing generated object as a cache miss and recompute it; never cache that failure as absence. State the supported maintenance assumptions and exercise recomputation during the GC/repack proof.

20. **Blocking — checkout permission and conversation exclusion are not checked atomically at effect time.**  
    Design:351–368 says the switch is shown on the plan and conversations are found in records. A conversation can start after that read, or the switch can be turned off before the press. Existing conversation startup takes the tree-start exclusion only for isolated trees (`ChatRunner.cs:439`, `ChatGoOn.cs:442`), so reusing that mechanism unchanged does not protect checkout acts.

    **Smallest fix:** add checkout acts and checkout-conversation starts to the same narrow exclusion protocol. Under it, re-read the switch, actual checkout identity, active conversations, operation state, HEAD/index and shown bytes immediately before effects. Refuse session-tree targets independently of the switch.

21. **Blocking — allowed side effects of writes are not bounded by the claimed `.git` rule.**  
    Design:399 explicitly permits moving a branch’s configuration section, while Design:413 and559 broadly prohibit configuration writes. Fetch can also invoke automatic maintenance or configured commit-graph writing; checkout/commit/stash acts execute hooks and filters with effects beyond “index” or “branch” in the table.

    **Smallest fix:** state the narrow rename-config exception and preserve its previous contents for recovery. Disable automatic performance maintenance on Daoris fetches where D147 requires it. Enumerate hooks, filters, reflogs, operation files and index/file effects in typed plans and proofs; preserve required hooks rather than silently bypassing them.

22. **Blocking — old-value CAS does not preserve holder proofs or prevent late checkout races.**  
    Design:389–403 specifies rejudgement and the target ref’s old ID, but does not carry holder refs into the deletion transaction. The repository’s existing contract requires exactly those rests to be verified together (`ContentHolds.cs:373` and `.claude/knowledge/ref-removal-transactions.md`). Its existing removal also addresses checkout after the first worktree read (`ContentHolds.cs:378`).

    Snapshot omission is likewise not proof of absence: `for-each-ref` can skip a broken ref, as `.claude/knowledge/git-ref-absence.md` records.

    **Smallest fix:** explicitly reuse the judged-delete transaction and its late-checkout recovery protocol. Require confirmed missing versus unread, verify every proof ref, and retain recovery refs whenever restoration or verification is uncertain. Extend occupancy protection to object rewrites and renames, not only deletes.

23. **Blocking — object-based history editing lacks enough semantics to implement safely.**  
    Design:372–377 names `merge-tree` plus `commit-tree`, but does not define the replay cut, picked/reverted merge mainline, root commits, empty commits, merge preservation, author/message handling or signatures. A plain merge of two tips is not a cherry-pick. Rebase also lacks the published-history refusal that D109 retains; preventing force push later does not make rewriting a published local branch satisfy that contract.

    **Smallest fix:** initially support a precisely defined linear, unpublished commit range; refuse unsupported merge/root cases. Specify each three-way base, preserved metadata and empty-commit policy, pin all source/target IDs, and establish recovery before the first destructive ref effect.

24. **Blocking — stash conflict handling promises a transaction git does not supply.**  
    Design:408 says a conflicted apply aborts whole. A normal stash apply can change files and index before reporting conflicts; it has no general “abort apply” operation. A recovery ref preserves committed objects, not the checkout’s pre-existing uncommitted bytes or exact index.

    **Smallest fix:** either present retained conflicts as the supported outcome, or preserve and restore exact affected file/index state with interruption recovery. Define tracked/untracked inclusion, dirty-start rules, shared stash ownership and drop selection by identity rather than a moving ordinal.

25. **Blocking — the two-call Review representation cannot support the proposed staging controls.**  
    Design:292–294 correctly notes that staged and unstaged changes can cancel in `diff HEAD`, but §8 still leaves hunk/line staging without a separate index comparison. A file with staged additions and matching unstaged reversals can disappear from that patch while requiring both staging and unstaging controls.

    **Smallest fix:** give Changes separate HEAD→index and index→worktree observations, plus untracked/conflict states. Compose applicable patches from the appropriate comparison and revalidate exact index entries and source bytes at the press.

26. **Blocking — remote-act dependencies point to the wrong D198 milestone.**  
    Design:404,565 waits only for GITAUTH1f before offering tag push. D198’s update-by-update Dangerous classification is **GITAUTH1g**, not 1f (`docs/2026-10-22-git-auth-design.md:497–500`; `TASKS.md:275`). Today `GitAccessRules.cs:40,75` judges the remote command as Read/Write; it cannot inspect a tag update or prove the branch update is the exact permitted fast-forward.

    Ordinary person pushes also need an explicit grant path; today the default denies nongranted pushes, and current grants are address/connection scoped (`GitAccessRules.cs:40,144`).

    **Smallest fix:** gate tag pushes on the proved transport judge, gate every remote act on installed/proved doors, and define exact person-push authorization without borrowing a landing grant. Name these dependencies in row 1e and its install proof.

27. **Blocking — the terminal write contract omits the existing person-confirmation seam.**  
    Design:433–435 supplies `--plan|--yes` and rejects `DAORIS_SESSION_ID`, but does not say how these verbs preserve the running window’s `Person` pipe classification. An absent session marker is not affirmative proof of a person’s press.

    **Smallest fix:** route writing verbs through the existing person-press/confirmation mechanism, with their census and wording entries, and retain the established standalone-terminal rules. Keep the environment refusal as an additional ordinary-session guard.

28. **Blocking — the first-row census and dependency order cannot prove the stated claims unchanged.**  
    Design:445 uses `GitRead` plus `WorkingTree.Started`, but `Started` fires only in `RunGitAsync` (`WorkingTree.cs:1020`); `ReadGitAsync` and `GitBytesAsync` launch independently (`:1106,1199`). Existing `TreeDiffTests` inject a streaming reader; that alone does not instrument the complete route and background work.

    Counting “rounds” as overlapping starts is also timing-dependent and conflicts with the gate’s limited creation slots. Finally, row 1a promises landed-path counts while migration of `LandedReviews` is assigned to 1c (Design:531); §7.1 promises kept file-history IDs in 1d while their implementation is assigned to 1h.

    **Smallest fix:** put one counting/scheduling seam beneath all reader launches in 1a, test dependency depth with held completions rather than incidental overlap, and count fallback/retry paths separately. Move every migration needed for 1a’s ceiling into 1a; distinguish the initial history screen from the later whole-history optimization.

29. **Non-blocking — rows 1d and 1g are too broad for useful acceptance boundaries.**  
    Design:534–543 bundles several screens, graph filters, search, diff rendering, LFS parsing, a process protocol and conflict preview into 1d; 1g combines permission storage, exclusion, staging, commits, stash and file replacement. A story and counter do not prove their parser, lifecycle or recovery claims.

    **Smallest fix:** split each into independently reviewable slices, keeping terminal, bridge, Ask Daoris and translations with each slice. Prove the held reader before integrating it, and prove checkout exclusion/recovery before adding destructive file acts.

**29 findings; 22 blocking.**

The direction is sound, but this round should remain proposed. Keep the shared reader, preserved range semantics, separate action readiness, exclusive session ownership and explicitly enabled person’s checkout. Before acceptance, replace the three-round arithmetic with a buildable dependency graph, make freshness an identified and validated observation, complete process ownership and write recovery, and correct the D198 prerequisites. Those changes preserve the synthesis’s intent while making its speed and safety claims testable.

## Fable

## Fable's foundation read of GITAPP1's design (D202, round 1) — 2026-10-23

Read at the `gitapp1-reads` worktree (`92c7b8ed5`): `docs/2026-10-23-git-app-design.md`, `docs/decisions/D202.md`, the brief and
the three proposals, whole. Every claim about today's code was checked against the main checkout at `fe4809b75`, and against
REVIEWLOCK1's branch `daoris/s-e276dfc9` (`445720582`..`f80814215`), which is **not merged into `main`**. D147, D150, D198,
D113, D191, D102's SQUASHTIDY1c note and the built-in git design were read. Nothing of Daoris was started; no agent, no sign-in;
nothing written but this file. Codex's read was not opened (D191 point 1). Paths are repository-relative; `Driver/` is
`src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Web/` is `src/Daoris.Web/`.

### 0. Facts the design states, checked

| Claim (design §) | Where | Holds? |
|---|---|---|
| Review's `status` and `diff` pass no `--no-optional-locks`; `HelperCapture.cs:90,104`, `HelpCheckouts.cs:104` do (§1) | `Driver/Trees/WorkingTree.cs:335` (`status --porcelain -z --untracked-files=all`), `:330` (`RangeAsync(root, "HEAD", "HEAD")`), `TreeWorkReads.cs:77`; `GitWiring.cs:28-34` sets no `GIT_OPTIONAL_LOCKS` | Holds on `main`. **REVIEWLOCK1 is built on its branch** (`445720582`: all 24 statuses and diffs; `75b8ed62b`: `tools/optional-locks.mjs` in `verify`; `f80814215`: records), not merged. See B1 for what it found that the design does not say |
| Union counts, not pairwise (§1, §3.1 round 3) | `ReviewRanges.BehindAsync`, `ReviewRanges.cs:122-132`: `rev-list --count <refs…> --not HEAD` and its converse | Holds |
| `StartClass.Person` is "a start a person is waiting on"; git is named under Background (§1, §4.4) | `Driver/Platform/ProcessStarts.cs:10-17`; every git run is Background: `ChildProcess.cs:36-42,60`, `WorkingTree.cs:1199` | Holds |
| D51 has no rule 7; the recovery ref is D102's SQUASHTIDY1c note (§1, §13) | D51 has no numbered rules at all; `built-in-git-design.md:134` cites "D51 rule 7"; `D102.md:220-272` (`ContentHold.KeptAt`) | Holds |
| `merge-tree --write-tree` writes objects only (§1) | `ReviewRanges.cs:90-95` | Holds |
| `plannable` derives from the tree's answer; `useHandOff(session)` is always asked (§1, §3.2) | `Web/src/work/review/DiffPane.tsx:148-163`, `:188` | Holds |
| The page re-reads on every `updated` of a live session (§3.3) | `Web/src/bridge/sessions/trees.ts:182-184` | Holds |
| The ref watch hears heads, remotes, reftable, trees' `HEAD`, not tags; roots are repositories holding Daoris's work (§5.1) | `Driver/Trees/RefWatch.cs:229-242`, `:249-265` | Holds |
| `ChangeWatch.MostRoots` is 32 (§5.1, §6.3) | `Driver/Platform/ChangeWatch.cs:189`; **per `ChangeWatch` instance** (`:201`, `:255`), and `RefWatch` owns its own (`RefWatch.cs:141`) | Holds, with N4's qualification |
| The gate is four, three the background's (§4.2) | `ProcessStarts.cs:51-54` | Holds |
| PROC1a measured no gain past four (§1.2, §4.2) | `docs/FIX-LOG.md:5291-5295` | Holds |
| `GitReadCache` 64 MiB LRU, memory only, keyed by common dir; nothing from a shallow repository (§4.1, §5.2) | `GitReadCache.cs:19`, `GitReads.cs:517-545` | Holds |
| The layout from disk: `.git` folder or `gitdir:` file, `commondir` (§3.1, §4.1) | `GitBranches.cs:284-302`, `GitReads.cs:519-545`, `LineGuesses.cs:42-54` | Holds |
| `GitFacts`/`RefSnapshot` consulted at `WorkingTree.cs:1009-1011`, dropped by a write (§4.1) | `WorkingTree.cs:1005-1012`, `:1019`, `:1068-1070`; `GitFacts.cs:11-21`; `RefSnapshot.cs:13-17` | Holds |
| `RefMoves.Mine` on Daoris's own move (§5.1) | `RefWatch.cs:20-52` | Holds |
| The state hub's `diff` scope (§3.3) | `Web/src/bridge/state/stateHub.ts:31` | Holds |
| The service's `IgnoredPaths` runs `PATH`'s git, untouched (§4.5) | `src/Daoris.Service/Daoris.Service.Core/Registry/IgnoredPaths.cs:92-114` | Holds; but see N13 |
| `ContentHolds` holds 21 git calls (§3.2) | 21 `GitAsync` calls in `ContentHolds.cs` | Holds |
| The commit-graph here carries changed-path Bloom filters ("relayed, not re-read", §1) | `.git/objects/info/commit-graph` chunk table: `OIDF OIDL CDAT GDA2 BIDX BDAT` (read 2026-10-23); a `multi-pack-index` is in `objects/pack/` | Holds; the design may say it was read |
| `DAORIS_SESSION_ID` named by 13 sources (§1) | 33 files under `src/`, tests included | Count differs; immaterial |
| `cat-file --batch-command` from 2.36; `git replay` experimental (§4.3, §7.3) | git's own documentation | Holds |
| The landed review is three more starts (§3.1, brief) | `LandedReviews.cs:35-66`: `--show-toplevel`, `rev-parse --verify`, `--is-ancestor`, then `LandedFromAsync` (1-3) and the diff | Holds |

### 1. Blocking

#### B1. `--no-optional-locks` is not the no-lock rule for a `diff` against the working tree — §1.1, §3.1 round 1, §6.1, §10.1, §12 row 1; D202 point 3

**What is wrong.** The design (§1.1: "`git diff` refreshes quietly the same way"; §6.1 "both `--no-optional-locks`"; D202 point 3) rests the
no-lock promise on the one flag. REVIEWLOCK1, building that row, measured on git 2.53 that **`git diff` against the working tree
refreshes and writes the index whatever `--no-optional-locks` says**; only `status`, `diff --cached` and a diff of two commits honour
it. Its fix (`445720582`, commit message and `docs/FIX-LOG.md` on that branch, lines 13-22) passes `-c diff.autoRefreshIndex=false` on
Review's range read (`WorkingTree.RangeAsync`, whose uncommitted range is `HEAD`) and then has `RangeSplit` drop the rows git lists
unrefreshed (`M` in `--raw` with no `--numstat` count and no patch: stat data alone moved). This matches git's source
(`builtin/diff.c`'s `refresh_index_quietly` is reached through `diff.autoRefreshIndex`, and takes the lock without consulting
`use_optional_locks()`). The census REVIEWLOCK1 added (`tools/optional-locks.mjs`) checks the flag alone; `diff.autoRefreshIndex` is
held only by a unit test on `RangeAsync`. So a reader built from §3.1 as written (a new call site, `diff --no-optional-locks … -p
HEAD`) takes `index.lock` in a running session's tree again, and the design's own census passes it.

**Smallest change.** Write round 1's uncommitted diff as REVIEWLOCK1 built it (`--no-optional-locks -c diff.autoRefreshIndex=false`,
stat-only rows dropped as `RangeSplit` drops them) and say so in §3.1, §6.1 and D202 point 3 ("no optional lock and no index refresh").
Widen the census (`tools/optional-locks.mjs`) to require `diff.autoRefreshIndex=false` on any `diff` whose revisions are not two
objects, so 1a's reader is held by it. Read §12 row 1 and §1's first table row as "built on its branch, merged before 1a".

#### B2. The refs snapshot's stamp has no ordering or racy rule, so a ref written during the `for-each-ref` is kept under the newer stamp — §5.1; D202 points 4-5; §10.2's stamp case

**What is wrong.** §5.1 says the snapshot is "kept with a stamp" and "the stamp is read again before every use". It does not say when
the stamp is first taken. Taken *after* `for-each-ref` returns (the natural reading of "kept with"), a ref moved while git was
printing (a session's commit, the thing 1b is for) is absent from the snapshot and present in the stamp, so the snapshot is served as
current until the next move, and the answer names the old `head` as what it read at — exactly what D202 point 5 promises never happens.
Every loose ref file is 41 bytes, so size never tells; the write time is the whole stamp. The repository already has the rule:
`LineGuesses.GuessAsync` (`Driver/Platform/LineGuesses.cs:33-37`) takes the stamp **before** asking git and keeps the answer only if
the stamp read **after** is equal. §10.2's case ("the snapshot drops on a loose ref write … each by real git") writes the ref before or
after the read, never during, so it cannot catch this.

**Smallest change.** State in §5.1: the stamp is taken before the read and compared after it; a snapshot whose stamps differ is not
kept (asked again on the next use), as `LineGuesses` does; and on a filesystem whose write times are coarse (a network share, exFAT)
the snapshot is not kept at all. Add to §10.2 a case where the ref moves while a held stand-in `for-each-ref` is still answering, with
the answer naming the new id or reading again. The same rule applies to the layout's `.git` stamp and the tree watch's `index` stamp.

#### B3. Round 1 reads the uncommitted diff at the symbolic `HEAD` while round 3 reads the committed range at the snapshot's id — §3.1; D202 points 1 and 5

**What is wrong.** §3.1 round 1 starts `diff … HEAD` (*tree*), as `WorkingTree.cs:330` does today, while round 3 reads
`<from>..<head>` at the id the snapshot resolved. A session's commit between the snapshot's resolve and git's open of the index (a
window of one start's latency here, 0.3-0.8 s) makes the two halves disagree: the new commit's changes are in neither (the uncommitted
diff is against the new `HEAD`, the committed range ends at the old one), and the answer says "read at `<head>`" for a working-tree
read that was not. Today's serial chain has the same hole and nobody keys by `head`; the design introduces the key, so the hole
becomes a wrong answer the page trusts.

**Smallest change.** Round 1's working-tree calls take the id: `diff … <head>` (identical semantics to `HEAD` for a working-tree diff).
After round 1, re-read the tree's `HEAD` stamp; where it moved, the answer says so (the ids it was read at, and that `HEAD` is past
them) and 1b's hint reads again. One line in §3.1 and one case in §10.1's stand-in ("a ref moves between resolve and read").

### 2. Not blocking, in order of weight

#### N1. The held `cat-file`'s hazard is any `gc --auto` in the shared object store, not only the person's `git gc`; its bound is per process — §4.3; D202 point 4

Every linked session tree shares the common directory, so an agent's `git commit`, `fetch` or `rebase` in its own tree runs git's auto
gc there (detached, `gc.autoDetach` default; `gc.auto` is unset in this repository, so the default 6,700 loose objects applies), and that
repack unlinks packs the held process has mapped. On Windows a mapped pack cannot be unlinked whatever the share mode, so the
hazard is real and its trigger is whenever a screen is open, not the person's terminal alone; the person's own `gc` in a terminal is the
case that *prompts*, a detached auto gc the case that warns and leaves the old pack for the next repack. "At most four across the
machine" cannot be held: `GitReaders` is one registry per process (§4.1), the window's and each `daoris-driver` verb's.

Smallest change: 1d measures the hazard with an auto gc fired from a linked tree (`git -c gc.auto=1 commit` in a fixture tree) while the
held process answers, beside the person's `gc`; the held process ends on a repack sign (`<commondir>/gc.pid` appearing, or the
`objects/pack/` listing changing) and never starts while `gc.pid` exists; the bound reads "four per process". The 30 s idle bound is right
for a burst.

#### N2. Round 1's long pole is `status`, which the design does not shorten, and a round of five is two waves under a bound of three — §3.1, §3.4

The brief measured `status` here at 0.6-3.8 s (clean, ignored `node_modules`); the committed diff is 1.0-2.6 s. Round 1 ≈ max(status,
diff) + one start's wait where `status` lands in the second wave; round 3 ≈ the diff. Three rounds' medians sit at about 3 s, the target's
edge; fine as a reported bench, but two cheap changes move it: (a) hand a round over longest-first (`status`, the diff, then the three
merge-bases), so `status` is never in the second wave; (b) in round 1, also start the committed diff `<recorded>..<head>` speculatively
where the recorded base is an id: it is the chosen base whenever the line has not moved since the session began
(`ReviewRanges.cs:63-69`, one candidate → `from` = it), it is keyed by ids so a wrong guess is one wasted background start and never a
wrong answer, and the ordinary case becomes two rounds. Optional; (a) at least should be said.

#### N3. The round census needs held answers — §10.1

A stand-in that answers at once (`WorkingTree.Answered`, `Runs`) makes any chain look like one round: the second start is issued only
after the first returns, whether the code awaits them together or in turn. Say that the seam holds every answer until the test releases
the round and counts the starts issued before the first release; "a round is a set of starts that overlap" is then defined. The counts
themselves are load-independent, as the design says.

#### N4. The widened ref watch must not feed today's consumers, and `MostRoots` is per watch — §5.1, §6.3, row 1b

Every hint of the ref watch runs `LandingWakes.Moved` and the machine facts' per-repository topics (`DriverWatch.cs:432-438`;
`HelpFacts.Woken`, `HelpFacts.cs:141-142`: `help.lines`, `help.trees`, `help.work`, `help.sweep`, each a judgement with git starts), and
`RefWatch.RepositoriesAsync` (`:249-265`) is the set `RebuildAsync` (`:288-308`) reconciles. Widening the roots to every registered
repository with a checkout (29 on the install) makes the person's every commit anywhere wake those judgements for repositories holding
no Daoris work. Smallest change: the reader's roots are a second topic (or a second `ChangeWatch`) whose hint only drops the snapshot
and publishes the `diff` and `lines` scopes; the work-holding set keeps its consumers. And since `MostRoots` is per `ChangeWatch` instance
(`ChangeWatch.cs:201, 255`), say which watch the tree roots of §6.3 and 1g live in and what their own cap is.

#### N5. 1b's "sessions whose tree is on that branch (`session-branches.json`)" — §3.3, row 1b

`session-branches.json` holds `GrownBranch(Repository, Workspace, Branch, Line, From, GrewFrom, At, Now, Unit)`
(`SessionBranches.cs:19-27`): no session. D147's GIT1a note says the same: it records where a branch grew from, not its session; a
session's branch is named by the newest session record whose tree's folder is the branch's name. The mapping 1b needs is the session
records' tree folder ↔ branch (`TreeFollows.BranchOfTree`), with `GrownBranch.Now` for a tree its agent moved. Say that; the parenthetical
is wrong as a source.

#### N6. The merge landing writes the person's checkout; the Changes tab's acts and it need one lock — §7.2, §8

`TreeLands.MergeAsync` is "the one place Daoris writes into a checkout it did not create" (`TreeLands.cs:162-168`), running `merge --no-ff`
in the registered checkout (`:287-298`) after checking it is clean and on the line "immediately before" (`:264-281`). §7.2's refusals for
the checkout's acts name a conversation in the checkout but not a landing in flight; §8's "serialised per tree" does not say the landing is
among the writers. One direction is covered by accident (a staged change makes the checkout dirty, so the landing refuses); the other is
not (a stage or a switch pressed while the merge runs). Smallest change: one lock per checkout taken by `MergeAsync` and by every
Changes-tab act, and "a landing holds the checkout" in the refusal list (`LAND_SESSION_TREE` under way for a merge rule).

#### N7. The person's push needs a grant at the door — §8; D202 point 6

`GitAccessRules.Judge` (`Driver/Platform/GitAccessRules.cs:40-46`) allows Write only where the act carried a grant (`GitGrants`,
`DAORIS_GIT_GRANT`) or `no-push` is off; a process that is neither a landing's plugin nor a session is "denied" once the doors are laid out
(D198's GITAUTH1b note). §8's "the person's press being its grant as the landing's is" is right in intent; say the mechanism: the press
writes a `GitGrants` grant for origin's push address, handed to the push's own process, as the landing does.

#### N8. Commit and amend run the checkout's hooks, which may write files, and trigger auto gc — §7.2, §8

"The acts that write only the index and refs (stage, unstage, commit, amend) never touch a file the editor holds" overstates: a
`pre-commit` hook that formats in place writes the person's files, and `commit` runs `gc --auto`. The plan should say the hooks run
(rightly, no `--no-verify`) and name them where the repository has them; the editor's files may move.

#### N9. History edits by objects are two serial starts per commit — §7.3, row 1f

A rebase of n commits is n `merge-tree --write-tree` and n `commit-tree`, each waiting on the one before: 2n starts at 0.3-0.8 s here, so a
30-commit branch is about 30 s with no progress said. 1f should state the count, stream progress with the time, and cap or warn past a
size. A merge commit in the range cannot be replayed by `merge-tree` one parent at a time; say it is refused (or dropped as `rebase`
drops merges, said). Cherry-pick of a merge needs a mainline; refuse it.

#### N10. "Nothing written into `.git`" is not whole: objects are — §1, §13; D202 point 8

§1's table says `merge-tree --write-tree` writes objects and that this is said wherever "reads only" is said; §13 then says "§4.2 stands
whole: nothing written into `.git`", and 1f's `commit-tree` writes objects by design. Say "no ref, no configuration, no file on disk of
Daoris's own; objects where git's own commands write them". And a cached `merge-tree` answer (a tree id) can be pruned by the person's
`gc` (unreachable, two weeks): on a failed read of a kept id, drop the entry and ask again rather than serve "git could not read".

#### N11. The amendments' targets — §13; D202 "This amends"

- D113 §5 rejects reading the person's checkout **for a landed session's review**; that stands (the landed review still reads objects).
  What the Changes tab changes is D147's rejection "Checking out in the person's checkout" (`D147.md:58`) and the built-in git design's
  §3.1 "Checking out is not offered" (`built-in-git-design.md:136-139`), which §13 does not name. Name them; say D113 §5 stands.
- D150 point 3 names Setup's four sections; say which holds the switch (Reach, the section about what writes into the repository).
- D147 point 2 and §2.8: the design keeps "no conflict editor" by offering a side per hunk or the person's editor; say that §2.8's
  "merge-conflict editor" bullet is amended, not kept, since *Take ours/theirs/both* and *Mark resolved* write a file.

#### N12. The stamp's walk is unbounded by the ref count — §5.1

"Each file under those ref folders" is every loose ref under `refs/remotes/*` and `refs/tags/*`; a repository with thousands of tags
stats thousands of files before every use. `LineGuesses.Stamp` reads four. Bound it: past N loose files, stamp the ref folders' own write
times (a rename into a folder changes it on NTFS and ReFS) plus `packed-refs`, `HEAD` and the trees' `HEAD`s, or leave the watch and
the deadline to guard, said in the answer.

#### N13. `IgnoredPaths` is the service's — §6.3, row 1g

The tree watch filters "as `IgnoredPaths` names them", but `IgnoredPaths.Start` is in the service (`IgnoredPaths.cs:92-114`, `PATH`'s git,
`GIT_CEILING_DIRECTORIES`), another process. The driver's watch needs its own list: one `ls-files --others --ignored --directory` through
`GitStart` per watched root (one more start, said in `review.read`), or the service's answer where a service is up. Say which; a filter
that misses `node_modules` under an agent's install is the storm §6.3 names.

#### N14. Rows — §12

1a is the largest row (driver, modules, web: the reader, the rounds, the untracked patches, the page's key and *Read at*, the offer
after the diff, `review.painted`, the verb and the bench). Split it so the driver's reader, rounds and `daoris-driver git review --time`
land and are benched first, the page's half second: the bench door should exist before the page changes, so before and after are
measured on one axis. 1e and 1f write refs on proofs that read other refs: cite `.claude/knowledge/ref-removal-transactions.md` (the
removal and its proof as one `update-ref --stdin` transaction) and `git-ref-absence.md` (an unread ref is not a missing one) for rename,
delete and the history edits' `update-ref` at the judged old id.

#### N15. Small corrections of fact — §1, §12

REVIEWLOCK1 is built and recorded on `daoris/s-e276dfc9`, not filed; 24 statuses and diffs changed, a census in `verify`, `ReviewLockTests`
(the index's write time unchanged; Review opening while a stand-in holds `index.lock`). The commit-graph's Bloom filters were readable
without a process (the chunk table) and are present. `DAORIS_SESSION_ID`: 33 files, not 13.

#### N16. The byte-equality fixture set lacks a non-UTF-8 text file — §6.2, §10.2

`GitStart` decodes git's output as UTF-8 (`WorkingTree.cs:911-912`); a Latin-1 file's bytes become U+FFFD both ways today. The fixture
set should hold one, with the comparison stated at the decoded text (as git's answer is decoded) or at the bytes, so the two readers
cannot drift on exactly the files that differ.

### 3. Answers to the read's questions

- **The reader.** Can it serve a stale answer: yes in two places the design can close cheaply (B2, B3); the keying by ids, the stamp
  at use and the watch as a wake are the right shape. Status and the working tree are never kept in 1a, and 1b's kept answer is
  stamped by the `index` and dropped by the watch: right, given N13.
- **The kept-open `cat-file`.** It blocks a repack on Windows (a mapped pack cannot be unlinked); the 30 s idle bound is right for a
  burst, the "measure before accept" is right, the trigger and the bound's scope are wrong (N1). Keeping it out of Review is right.
- **Review's targets.** Reached in arithmetic at the edge (N2); the count gate is load-independent as counts, once a round is defined
  by held answers (N3).
- **Locks.** No read of the design's takes a lock once B1 is taken; `merge-tree` and `commit-tree` write objects, never a lock.
- **Bounds.** The acts stay within D147/D150/D198 once N6, N7 and N11 are said; a session's tree is never written by the person
  (right, and the reason for rejecting my idle-tree acts holds: the trace and the transcript would not explain the commits).
- **The amendments.** D147 points 2, 3, 6 and D150 point 3 are the right targets; D113 §5 is not the thing amended (N11).
- **The rows.** Safe order; 1a makes Review fast; split 1a (N14).

### 4. Verdict

**Accept after the blocking findings are answered** (B1 the no-lock rule as REVIEWLOCK1 measured it, in the chain and the census; B2
the stamp's order; B3 one `head` id for both halves of a review). The shape is right: one reader per common directory, rounds through
the unchanged gate as Background, invalidation by git's own files at use with the watch as a wake, no held process for Review, the
person's working-tree acts in their own checkout behind a switch and never in a session's tree.

## Codex, round 2

Codex's read of round 2 at `a8b741993`, read-only, with REVIEWLOCK1's branch (`daoris/s-e276dfc9`) beside it. Round 3 answers it.

### 1. Each round-1 finding

References use **Design** for `docs/2026-10-23-git-app-design.md`, **Driver/** for `src/Daoris.Desktop/Daoris.Desktop.Driver/`, and **Web/** for `src/Daoris.Web/src/`. Verdicts assess the actual answer, including its implementation and proof requirements; “taken” does not mean already built.

| Finding | Verdict | Where in round 2 | Why |
|---|---|---|---|
| Codex 1 — existing counts, offer cost, page versus driver hits | taken | §1:60–62; §3.2:212–216 | The conditional `12 + I + M + Uᵣ` count and unbounded eligible-branch comparisons match the existing chain. |
| Codex 2 — identities partly built; doors opt-in | taken | §1:63; §8.3:638–640 | The identities implementation and `desktop-publish.mjs:1326–1340` support the correction. |
| Codex 3 — watch coverage and repository identity | taken | §1:64; §5.4:440–445 | Today’s omissions are acknowledged; the new reader watch resolves main and linked checkouts by common directory. |
| Codex 4 — scanner attribution | taken | §1.2:108–124 | Attribution is now an inference; wait, creation and runtime measurements are separated, consistent with PROC1a. |
| Codex 5 — dependency depth | open | §3.1:154–205 | A graph replaces rounds, but its displaced-base and landed paths remain incomplete; finding 1 below. |
| Codex 6 — unsupported latency arithmetic | answered | §3.4:232–247; §4.2 | Keeping the requested numbers as provisional acceptance targets is reasonable; arithmetic no longer establishes them. |
| Codex 7 — reservation and aggregate scheduling | answered | §4.2:283–296 | Aggregate walk limits address the original long-read saturation case; cross-reader priority is explicitly deferred to measurement. The remaining wording overclaim is finding 10. |
| Codex 8 — capability boundary | taken | §7.1:504–524 | The table is authoritative and explicitly defers interactive history editing and other omitted capabilities. |
| Codex 9 — global option placement | taken | §1.1:86–94; §3.1:172–175 | Commands place the option before the subcommand; REVIEWLOCK1’s census already checks that placement. |
| Codex 10 — read effects and census coverage | open | §1.1:96–104; §10.2:698–699 | Configured worktree programs are acknowledged, but the proposed suppression of merge drivers does not hold; finding 6. |
| Codex 11 — authoritative timestamp snapshot | open | §5.1:344–365 | Ordering and content checks improve it, but the bounded fallback still permits undetected changes; finding 4. |
| Codex 12 — context and frontend caches | open | §5.3:394–407 | Context travels through keys, but effective attribute inputs and the mechanism waking cached pages remain incomplete; findings 5 and 17. |
| Codex 13 — partial-clone fallback fetches | wrong | §5.3:413–417 | Refusing unsupported reads is right, but the newly asserted Git 2.44 floor is wrong; finding 7. |
| Codex 14 — mixed HEADs and joining | open | §4.1:265–268; §5.2 | Both diffs are pinned, but final validation and mutable-generation joining are not fully specified; finding 3. |
| Codex 15 — mutable ended trees and watch fallback | open | §6.3:486–498 | Ended trees and watch loss are covered; newly tracked paths can invalidate exclusions, and fallback counts omit attributes; findings 2 and 9. |
| Codex 16 — invalidation dependencies and publication | open | §5.4:421–445 | Line and actual-HEAD dependencies are corrected; context-change detection and delivery to indefinitely cached pages remain unspecified; finding 5. |
| Codex 17 — untracked patch equality | taken | §6.2:459–482 | Display additions replace general patch equality, are never staged, and have bounded reads and explicit exceptional cases. |
| Codex 18 — held-process protocol and ownership | wrong | §4.3:304–315; §4.4 | Ownership is supplied, but the literal protocol reads a body after `info` and permits withdrawing an already-sent request; finding 8. |
| Codex 19 — generated-object lifetime | taken | §5.3:409–411 | Missing generated objects cause recomputation, and the planned proof exercises that path. |
| Codex 20 — atomic checkout/conversation exclusion | taken | §7.2:536–549 | Conversation startup joins the same exclusion and effect-time revalidation. Another existing checkout writer remains outside it; finding 11. |
| Codex 21 — side effects and `.git` rule | taken | §8.1:595–606; §13:838–841 | Hooks and maintenance are acknowledged, fetch maintenance is disabled, and the rename-config exception is named. |
| Codex 22 — proof refs and late checkout | wrong | §8.2:610–626 | The transaction principle is taken, but duplicate ref operations and create-only rollback of rewrites are unbuildable as stated; findings 12–13. |
| Codex 23 — object history-edit semantics | taken | §7.3:565–583 | Explicit three-way bases, linearity, publication, metadata, empty-result and signature rules answer the finding. |
| Codex 24 — stash outcomes and ownership | open | §8.2:630–632 | Retained apply conflicts are honest; rereading an ordinal under a checkout lock does not make shared-stash deletion identity-safe; finding 14. |
| Codex 25 — separate staging observations | taken | §7.2:555–561 | HEAD→index and index→worktree are separate, with index IDs and source bytes revalidated. |
| Codex 26 — D198 prerequisites and push grant | taken | §8.3:638–648; §12:802–806 | Installed doors, GITAUTH1f, a press-specific grant and GITAUTH1g for tags now match D198 and `GitAccessRules`. |
| Codex 27 — terminal person-confirmation seam | taken | §8.3:649–653 | `PersonPresses`, `RouteClass.Person` and standalone behavior match `InstanceRoutes.cs:13–17,156–170`. |
| Codex 28 — complete census and migration order | open | §4.1:259–263; §10.1; §12:776–784 | Counting beneath all runners and moving `LandedReviews` are correct; the counted graph and several expectations are still incomplete. |
| Codex 29 — oversized implementation rows | taken | §12:792–819 | Screens, held process, checkout reads and writes are split. Findings 16–17 identify remaining acceptance-boundary defects. |
| Fable B1 — worktree diff ignores optional-lock suppression | taken | §1.1:86–94 | Matches REVIEWLOCK1’s branch: `diff.autoRefreshIndex=false`, stat-only-row removal and a proposed census extension. |
| Fable B2 — stamp ordering and racy reads | taken | §5.1:356–363; §10.2:700–702 | Before/after ordering and the held-enumeration fixture directly answer B2; finding 4 concerns the separate bounded fallback. |
| Fable B3 — one HEAD ID for both halves | taken | §3.1:175; §5.2:369–374 | W now uses `h`, and status’s `branch.oid` is checked. Finding 3 concerns the acceptance barrier around the complete observation. |
| Fable N1 — automatic GC and per-process cap | taken | §4.3:312–322 | Linked-tree automatic GC, repack signs, a per-process cap and a measured fallback are specified. |
| Fable N2 — status first; speculative committed diff | taken | §3.1:179,207–208 | Both scheduling suggestions are adopted; speculation is explicitly counted. |
| Fable N3 — held answers in the census | taken | §10.1:684–687 | Dependency release is controlled by the test rather than incidental process overlap. |
| Fable N4 — separate watch consumers and caps | taken | §5.4:440–445 | Existing judgement consumers remain on their watch; reader refs and attended trees have separate instances and caps. |
| Fable N5 — branch records contain no session | taken | §1:73; §5.4:423–425 | Session records supply the session association; `GrownBranch.Now` supplies movement information. |
| Fable N6 — merge landing and checkout acts share exclusion | taken | §7.2:540–543 | `TreeLands.MergeAsync` explicitly takes the new lock. D109’s other checkout writer is finding 11. |
| Fable N7 — person-push grant | taken | §8.3:642–648 | The press writes its own address-scoped grant; tag authorization waits for the transport judge. |
| Fable N8 — hooks and automatic maintenance | taken | §8.1:599–604 | File-changing hooks and maintenance are acknowledged instead of promising index/ref effects alone. |
| Fable N9 — history-edit cost and merge commits | taken | §7.3:569–582 | The design states 2n core starts, progress, a cap and explicit merge refusal. |
| Fable N10 — object writes and pruned generated trees | taken | §5.3:409–411; §13:838–841 | Object writes are admitted and missing generated trees are recomputed. |
| Fable N11 — amendment targets | taken | §7.2:528–530; §13 | D113 §5 stands; D147’s checkout/conflict exclusions and the switch’s Reach location are named correctly. |
| Fable N12 — unbounded stamp walk | wrong | §5.1:348–355 | The walk is bounded, but its replacement loses validation strength; finding 4. |
| Fable N13 — service-owned ignored-path reader | taken | §4.5:336–338; §6.3:491–494 | The driver computes and counts its own exclusions; maintaining their safety needs finding 9. |
| Fable N14 — split driver/page work; transaction knowledge | taken | §8.2; §12:776–784 | The bench door precedes page changes and both ref contracts are cited; their unsafe extension is findings 12–13. |
| Fable N15 — branch status and small factual corrections | taken | §1:57,79–80 | REVIEWLOCK1 is built but unmerged; its diff contains the stated changes. The source-marker count is 33. |
| Fable N16 — non-UTF-8 fixture | taken | §6.2:468,480–482; §10.2:706–708 | Latin-1 is included, comparison is at decoded text, and replacement decoding matches `WorkingTree.cs:911`. |

### 2. What round 2 introduced

1. **Blocking — the graph still cannot generate all its claimed paths.**  
   **Design:** 154–165, 177–195, 682–687. **Code:** `Driver/Trees/SessionTrees/ReviewRanges.cs:101–108`; `Driver/Platform/LineGuesses.cs:31–37`; `Driver/Trees/WorkingTree.cs:105–123`; `Driver/Trees/SessionTrees/LandedReviews.cs:53–88`.

   A displaced recorded base is discovered by an ancestry read; the design instead declares that path has “no K.” Today it then looks up the grown base and may perform another ancestry read, which has no node. An unconfigured line can make `LineGuesses` start up to three processes, despite appearing under “no process.” The landed row names dependent ancestry/base-resolution/diff operations without supplying a graph that attains depth two. Gone-branch content-proof paths are also missing.

   **Smallest fix:** add explicit selection, grown-base, line-resolution and landed/gone subgraphs, including failure paths. Recompute their ceilings and depths. Keep depth two only for the precisely qualified ordinary case, and update D202 accordingly.

2. **Blocking — warm and watched counts omit necessary starts.**  
   **Design:** 173–174, 190, 198–200, 241–243, 491–498; §12:785–788.

   After watch loss, S can discover new untracked paths whose attributes have never been read. Rebuilding the answer therefore needs A as well as S and W: three starts, not two. With R already valid, S→A also has depth two, contradicting the unconditional depth-one row. Installing a fresh watch additionally runs two exclusion queries that the cold Review ceilings do not include.

   **Smallest fix:** state separate budgets for graph reads, watch establishment/re-establishment and retries, while reporting their total for an open. Include A whenever its inputs are not independently validated. Change the lost-watch proof to cover newly appearing untracked files.

3. **Blocking — V is not a final acceptance barrier for the complete observation.**  
   **Design:** 180–183, 265–268, 369–379, 490. **Code:** `Web/bridge/sessions/trees.ts:81–104` already distinguishes replaced reads before publishing.

   V depends on S, W and D or the speculative D₀. It does not require base selection, counts, A or U to finish. D₀ can finish before the graph establishes whether it is the selected answer. The stated validation checks refs and status’s HEAD, but does not explicitly validate the working-tree generation after all dirty-file reads. Joining specifies IDs and context without explicitly including that mutable generation.

   **Smallest fix:** add a selected-result node and a final barrier over every returned component. Validate both ref and tree generations there; include the mutable generation in mutable-read joining and retained-result identity. Preserve the page’s existing protection against an older read publishing over its replacement.

4. **Blocking — the bounded refs fallback can retain an observation it cannot validate.**  
   **Design:** 348–363, 440–445, 700–702.

   Beyond 256 loose refs, folder timestamps replace file checks; beyond 32 watched repositories, those checks must stand alone. Updating `refs/heads/team/topic` changes its containing directory, not necessarily the top-level `refs/heads` directory. The contract does not require a complete recursive containing-directory census, and directory timestamps cannot detect in-place ref-content writes. Large `packed-refs` files similarly lose content validation. Saying the stamp was bounded does not make a retained answer validated.

   **Smallest fix:** where complete validation is unavailable, do not retain the mutable refs observation: enumerate again on use. Alternatively specify and prove the complete directory/content validation algorithm. Add a nested-ref move in an unwatched, over-cap repository to the fixture set.

5. **Blocking — the context still omits inputs, and its publication lacks a detection mechanism.**  
   **Design:** 394–407, 427–445. **Code:** `Web/bridge/repositories/git.ts:262–294`; `Web/bridge/state/stateHub.ts:25–68`.

   `--attr-source` replaces worktree `.gitattributes`; it does not eliminate global/system attribute files or `info/attributes`. The context includes configuration files and `info/attributes`, but omits the contents of the effective `core.attributesFile` and system attributes. Editing such a file leaves the listed context unchanged. Moreover, the specified ref watch does not detect global configuration/include/attribute changes. An infinitely fresh frontend query cannot discover a new context merely because the backend would check it on another request. Today the state hub also has no `git` scope handler.

   **Smallest fix:** enumerate effective attribute and configuration dependencies per category, including defaults and worktree configuration, and name how their changes trigger reconciliation. Add the `git` scope mapping and an observable repository-context revision before context-dependent queries use it. Test a context change while an already-open page makes no requests.

6. **Blocking — an empty attribute tree does not suppress all merge drivers or preserve the claimed merge semantics.**  
   **Design:** 81, 102, 181, 518, 698–699, 797–798. **Code:** `Driver/Trees/SessionTrees/ReviewRanges.cs:90–95` currently runs the merge with repository semantics.

   `--attr-source=<empty tree>` leaves higher-priority `info/attributes`, global/system attributes and the configured default merge driver relevant. A custom driver can still execute. Conversely, removing committed merge attributes changes results: a union or binary rule can produce a different merged base or conflict answer. A preview computed under those altered rules cannot unconditionally say *Lands without conflict* about a landing using the repository’s rules. A fixture configuring only committed `.gitattributes` misses the remaining execution paths.

   **Smallest fix:** prove suppression across every attribute/default-driver source, or refuse/fall back when unsupported merge semantics apply. State the semantic change explicitly, qualify the preview, and test committed, info, global and default-driver configurations.

7. **Blocking — the newly specified compatibility floors are incorrect.**  
   **Design:** 398–399, 413–417.

   The installed Git release notes introduce `--attr-source` in **2.41**, not 2.40 (`RelNotes/2.41.0.adoc:116`), and record a `merge-tree --attr-source` crash correction in **2.43** (`2.43.0.adoc:257`). They introduce the no-lazy-fetch facility in **2.45** (`2.45.0.adoc:120`), rather than supporting the asserted 2.44 floor. A wrong suppression floor can turn an ostensibly local partial-clone read into a fetch.

   **Smallest fix:** gate these capabilities on proved support for the selected binary, using conservative supported floors and refusing object reads when suppression is unproved. Exercise the lowest supported versions; the fixture must include a command that would actually demand a missing object.

8. **Blocking — the held-reader protocol would deadlock or desynchronize as written.**  
   **Design:** 101, 300–310.

   `info <oid>` returns a header only. Reading `size` bytes after that header waits for a body Git will never send; only `contents` has that body and trailing newline. A request already written cannot be withdrawn merely because its answer has not started—the response remains in the stream. The below-2.36 `--batch` fallback also does not accept `info`/`contents` commands. Finally, `cat-file` does not accept the blanket `--no-ext-diff` flags assigned to every object-read kind.

   These distinctions are documented in the installed `git-cat-file.html:631–672,843–854,910–912`.

   **Smallest fix:** specify framing separately for `info`, `contents` and `missing`; permit withdrawal only before transmission and otherwise drain or retire. Define the older-Git protocol separately or disable the held optimization there. Give each command its supported effect controls rather than appending diff flags indiscriminately.

9. **Blocking — safe watch exclusions become unsafe when the index changes.**  
   **Design:** 490–498.

   An ignored directory can initially contain no tracked files and therefore be excluded. An agent can then force-add a file there. The index stamp invalidates the answer, but the exclusion list is rebuilt only for ignore/configuration changes. After the next read, edits to that now-tracked file remain invisible and a zero-start reopening can return stale content. Index-only changes also need a wake; merely checking an index stamp on a future request does not refresh an idle open page.

   **Smallest fix:** revoke and re-prove exclusions whenever tracked membership changes, and explicitly watch the actual per-tree index location. Count rebuilding costs. Add force-add-then-edit and index-only-change fixtures.

10. **Non-blocking — queue priority is described as a reservation.**  
    **Design:** 286–296. **Code:** `Driver/Platform/ProcessStarts.cs:37–42` explicitly explains why priority alone cannot reserve capacity.

    Eight unattended short jobs can occupy all eight reader run places before an attended read arrives. Giving it priority for the next free place does not mean it never waits behind unattended work. Deferring cross-reader priority to measurements is reasonable, but this stronger same-reader statement is not established.

    **Smallest fix:** say “first among queued reads,” or reserve an attended run place. Keep the cross-reader measurement decision separate.

### 3. Findings on writes and delivery

11. **Blocking — the checkout lock omits D109’s line pull.**  
    **Design:** 536–549, 810–815. **Code:** `Driver/Trees/SessionTrees/LinePulls.cs:51–78`; `Driver/Trees/SessionTrees/TreeSync.cs:209`.

    `LinePulls.PullAsync` checks the registered checkout and runs `merge --ff-only` there. It is a second existing checkout writer, besides `TreeLands.MergeAsync`, and is absent from the lock’s takers. A Changes-tab switch or index write can therefore race the line pull between its checks and merge.

    **Smallest fix:** put the in-checkout line-pull path under `CheckoutLock`, from rejudgement through effect and recovery. Include it in the writer census and mutual-exclusion proof before checkout acts land.

12. **Blocking — the proposed ref transactions contain duplicate operations on a ref.**  
    **Design:** 610–613, 623, 625–626. **Code:** `Driver/Trees/SessionTrees/ContentHolds.cs:283–287` deduplicates proof rests; its existing delete target is separate from those rests.

    `verify <name>` followed by `create <name>` submits the same ref twice; `create` already checks absence. Likewise, “verify every ref the judgement read” followed by updating the judged target can submit both a verify and update for that target. Git rejects multiple operations for the same ref in one transaction. The existing judged-delete helper does not establish that this generalized construction works.

    **Smallest fix:** normalize the transaction to one operation per ref. Encode target expectations in `create`, `update … <old>` or `delete … <old>`; emit separate verifies only for untouched proof refs. Reject contradictory expectations. Test successful creation and source/target alias cases, not only refusal cases.

13. **Blocking — create-only restoration cannot undo a rewrite.**  
    **Design:** 616–618, 625–626, 807–809. **Code:** `Driver/Trees/SessionTrees/ContentHolds.cs:467–475`.

    Existing `PutBackAsync` is create-only because deletion left the branch absent. After pick/revert/rebase, the branch still exists at the new tip, so the proposed create-only restoration necessarily fails. Rename recovery also needs to account for both names and its configuration phase. Extending the deletion wording does not extend the protocol.

    **Smallest fix:** define operation-specific conditional rollback: rewritten tip→old tip only while the branch still equals this act’s result; a rename’s reverse transaction only while both names meet its expectations. Preserve recovery refs and report an uncertain/partial outcome if anything moved or occupancy cannot be read. Test those outcomes.

14. **Blocking — stash deletion is still ordinal-based under the wrong lock.**  
    **Design:** 536–543, 632, 713–716, 816–817.

    A checkout lock protects one checkout, while `refs/stash` and its reflog are shared by linked trees. Another tree can push or drop a stash after the selected `stash@{n}` is reread and before deletion. Preserving the intended commit in a recovery ref does not prevent deleting a different ordinal. Git’s drop operation selects a reflog entry; the design supplies no atomic delete-by-OID mechanism. A message containing `daoris/` is also not sufficient ownership evidence for detached or switched session work.

    **Smallest fix:** specify and prove identity-checked reflog removal under concurrent linked-tree mutations, or defer Drop. Permit deletion only for positively identified person-owned entries; keep unknown ownership readable and retained. Hold a mutation between final selection and effect in the proof.

15. **Blocking — process ownership does not yet define bounded, interruption-safe write outcomes.**  
    **Design:** 331–332, 595–604, 625, 629–634. **Code:** `Driver/Trees/WorkingTree.cs:1022–1059`; `Driver/Trees/Domain/GitBounds.cs`; `Driver/Platform/ChildProcess.cs:76–87`.

    The table promises that effects run to exit and are never killed, while today’s runner terminates Git at its bound—even with caller cancellation disabled. Removing that bound would conflict with D181 and could make window close wait indefinitely on a hook or transport. Exit failure also does not prove no effect: a commit can have moved its ref before a later failure. Rename additionally has a crash window between its ref transaction and configuration update; no durable phase/reconciliation contract is supplied.

    **Smallest fix:** distinguish caller cancellation from the process backstop. Specify postcondition reconciliation and an explicit unknown outcome after timeout or lost ownership, with no blind retry. Define a durable act journal and restart reconciliation for multistep effects, including rename configuration and saved conflict bytes. Prove interruption after each effect boundary.

16. **Non-blocking — stash conflicts are shipped before their named recovery control.**  
    **Design:** 631, 816–819.

    Row 1g-iii supports an apply that leaves conflicts “for the Changes tab’s resolve,” but Resolve arrives only in 1g-iv. Since each row lands alone, the earlier row’s described recovery surface does not exist yet.

    **Smallest fix:** move conflicting apply support after Resolve, or explicitly provide the interim external-editor/manual-Git completion path and test its wording in 1g-iii.

17. **Non-blocking — two acceptance expectations contradict the chosen implementation.**  
    **Design:** 398–407, 703–704, 792–798. **Code:** `Driver/Trees/GitReads/GitReads.cs:290–313`.

    The object-read policy freezes attributes to the same destination commit, yet its fixture requires linked trees at those same IDs with different worktree attributes to answer differently. Those object answers should agree under the frozen policy. Separately, §12 requires each read’s “one call,” while the existing commit page uses two dependent calls and the held reader is deliberately deferred.

    **Smallest fix:** distinguish frozen object-context equality from genuine worktree-context differences in the fixture. Give each view its actual expected start graph—two for the current commit reader—and test that expectation before introducing the held optimization.

**17 findings; 14 blocking.**

**Verdict: another round.** Keep the shared reader, ordinary-case speculation, separate action readiness, exclusive session ownership and explicitly enabled checkout acts. The remaining changes affect the foundation: the complete graph and census, validation and context delivery, configured-program suppression, checkout-writer exclusion, and transaction/recovery protocols. D202 should remain proposed until those contracts and their acceptance proofs agree.

## Codex, round 3

Codex's read of round 3 at `f9e9f6dd4`, read-only, the read half only (the writes moved to GITAPP2, not read). Round 4 answers it.

### 1. Previous findings on the read half

References below use **Design** for `docs/2026-10-23-git-app-design.md`, **Driver/** for `src/Daoris.Desktop/Daoris.Desktop.Driver/`, **Modules/** for `src/Daoris.Desktop/Daoris.Desktop.Modules/`, and **Web/** for `src/Daoris.Web/src/`. “Taken” describes the proposed contract, not an implementation already built.

| Finding | Verdict | Where in round 3 | Why |
|---|---|---|---|
| R2-1 — complete graph, grown base, line guesses, landed/gone paths | open | Design:203–274; §17 | The added subgraphs help, but the line derivation is not equivalent to today’s reader, D omits `Against`, and some ceilings remain wrong; findings 1–3. |
| R2-2 — A and watch establishment omitted from counts | open | Design:243–286, 654–664 | A and the separate budgets are taken; the new worktree-dependent M introduces further starts absent from the warm ceiling; finding 3. |
| R2-3 — final barrier and mutable-generation joining | taken | Design:231–233, 352–357, 475–484 | Selection is explicit, V waits for every returned component, and mutable joining includes the tree generation. Today’s replaced-read guard in `Web/bridge/sessions/trees.ts:81–104` is preserved. |
| R2-4 — unsafe bounded refs fallback | taken | Design:445–469, 596–601 | Unwatched repositories now enumerate on use; the loose-ref folder-time substitute is withdrawn. A separate packed-ref exception remains; finding 4. |
| R2-5 — context inputs, detection and delivery | open | Design:515–539 | Effective attribute paths and the page revision are supplied, but context construction and binary replacement still have gaps; findings 5, 6 and 11. |
| R2-6 — empty attributes neither suppress drivers nor preserve semantics | taken | Design:118–137 | Refusing configured drivers and renormalization with configured filters is a conservative, buildable policy. Keeping ordinary merge attributes preserves today’s `ReviewRanges.MergedAsync` semantics. |
| R2-7 — incorrect version floors | taken | Design:550–570 | The installed release notes support 2.41, 2.42 and 2.45. The explicit no-lazy-fetch option and pre-start refusal answer the network issue; frontend cache fallback is a separate remaining defect. |
| R2-8 — broken held-reader protocol | deferred | Design:393–417 | Deferral holds: today’s commit reader already works with two dependent starts (`Driver/Trees/GitReads/GitReads.cs:290–313`). The protocol corrections are recorded without requiring that reader here. |
| R2-9 — index changes invalidate exclusions | taken | Design:650–664 | The actual per-tree index is watched, index moves publish `diff`, and exclusions are revoked before reuse. The force-add-then-edit proof addresses the original hole. |
| R2-10 — priority described as reservation | taken | Design:377–386 | Unattended reads are explicitly limited to seven of eight places, matching `ProcessStarts.cs:37–42`’s distinction between priority and reservation. |
| R2-17 — contradictory linked-tree and start expectations | taken | Design:541–544, 674, 871–877 | Frozen object reads now agree across equal contexts, and the commit page is correctly assigned two dependent starts. |
| R1 Codex 5 — dependency depth | open | Design:218–286 | Depth is now a graph property, but some path qualifications and warm counts still fail; findings 2–3. |
| R1 Codex 10 — read effects and census | taken | Design:100–137 | The policy distinguishes commands and configured programs. REVIEWLOCK1 on `main` supplies the measured index-refresh rule; custom merge programs are now refused rather than supposedly suppressed. |
| R1 Codex 11 — timestamps cannot make refs authoritative | open | Design:451–461 | Review’s claimed whole-content validation still excludes a sufficiently large `packed-refs`; finding 4. |
| R1 Codex 12 — context and frontend caches | open | Design:491–570 | The revision mechanism is useful, but unsupported-context answers can still survive in the frontend, and C lacks a stable-construction rule; findings 5–6. |
| R1 Codex 13 — partial-clone fallback fetches | taken | Design:550–559 | Unsupported partial-clone object reads are refused before starting; supported reads pass the explicit global option and child environment variable. |
| R1 Codex 14 — mixed HEADs and joining | taken | Design:352–357, 475–489 | Both halves use `h`, status checks it, V covers the complete answer, and joining distinguishes mutable generations. |
| R1 Codex 15 — ended trees and watch fallback | taken | Design:640–664 | Attended ended trees are included; watch loss prevents retention and requires validation, including A. Finding 3 corrects the remaining count consequence of M. |
| R1 Codex 16 — invalidation and publication | open | Design:574–601 | Git dependencies and context publication are named, but changes to Daoris’s own line choices and grown-base records lack an invalidation contract; finding 7. |
| R1 Codex 18 — held-process protocol and ownership | deferred | Design:393–429 | The held process is genuinely removed from these rows. Page-close cancellation for the remaining reads still needs its own bridge mechanism; finding 8. |
| R1 Codex 28 — complete census and build order | open | Design:840–858 | One seam beneath all three runners is correct, but the first and graph rows have contradictory acceptance requirements; findings 9–10. |
| R1 Fable N12 — unbounded stamp walk | taken | Design:445–469 | The full loose-ref walk and folder-time fallback are removed; an unwatched repository pays R instead. |

**R2-11–16:** no read-side dependency on their checkout lock, ref transactions, rollback, stash deletion or write-outcome protocols remains. Moving those mechanisms away does not block building the reads; this is not an assessment of GITAPP2.

### 2. Findings on the read mechanisms

1. **Blocking — D discards REVIEWBASE3’s merged comparison tree.**

   **Rests on:** Design:230–232; `Driver/Trees/Domain/ReviewBase.cs:9–12`; `Modules/Bridge/Routes/TreesRoutes.cs:82–84`.

   Σ computes both `from` and `against`, but D explicitly reads `<from>..h`. Today the route reads `range.Reads`, meaning `Against ?? From`, while returning `From` as the displayed base. These are observably different: `ReviewBaseTests.cs:179–183` expects only `own.txt` from the merged tree and also gets `parent.txt` from `From`.

   **Smallest fix:** define D’s comparison endpoint as `against ?? from`, include that endpoint in its key, and exercise the graph executor through the existing chain-step fixture. Keeping the old range tests unchanged does not prove that the new executor uses their result correctly.

2. **Blocking — R cannot reproduce the promised line guess for dangling symbolic refs.**

   **Rests on:** Design:207–211, 744–745; `Driver/Trees/WorkingTree.cs:105–123`; `.claude/knowledge/git-ref-absence.md:24–29`.

   Today the first question reads the symbolic ref itself. A dangling `refs/remotes/origin/HEAD` can still name its target, while `for-each-ref` omits broken or dangling entries. Its omitted `%(symref)` therefore cannot distinguish “no symbolic ref” from “symbolic ref naming a missing branch.” Falling back to local `main` changes today’s answer. The proposed dangling-HEAD equality fixture exposes this distinction.

   **Smallest fix:** read that symbolic ref, including its chain, independently of enumeration—through validated disk contents for a supported layout or a counted `symbolic-ref` fallback. Preserve today’s shortening rule and amend the affected ceilings.

3. **Blocking — several advertised ceilings do not follow from the new graph.**

   **Rests on:** Design:251–258, 284–286, 497; `Driver/Trees/SessionTrees/ReviewRanges.cs:63–79,101–108`.

   M is now correctly keyed by the tree generation and retained only under its watch. Consequently, after watch loss or an attribute change, unchanged commit IDs do not eliminate M. A changed merged tree can also require D again. The warm promise of only S, W and optional A therefore omits necessary starts.

   Other rows also need qualification: no line plus a displaced recorded base and a distinct valid grown base requires K′ and a replacement D, exceeding the stated five-start case. “After a commit” is not universally depth two: distinct or incomparable candidates still require I and possibly M before D.

   **Smallest fix:** generate counts from both semantic conditions and cache-validity conditions, including M’s generation. Qualify the ordinary-only rows, publish the resulting warm ceilings, and include a lost-watch or changed-attributes REVIEWBASE3 case.

4. **Blocking — large packed refs retain the validation hole that Review claims to exclude.**

   **Rests on:** Design:451–461, 468; `Driver/Platform/ChangeWatch.cs:174–175,594–598,747–773`.

   Only loose review refs are read whole. Above 1 MiB, `packed-refs` falls back to its stamp and watch generation. A same-size, timestamp-preserving change to a packed line ref can therefore pass validation before the debounced watch judgement arrives. Status’s HEAD check does not detect a changed line ref. The allowance for stale branch decorations does not justify the stronger “never in a review” sentence.

   **Smallest fix:** validate the actual values of every packed ref a review uses, or stop retaining its observation when that validation is unavailable. Specify immediate dirty-generation invalidation separately from debounced reconciliation. Add a packed-line-ref case whose stamp remains equal while notification delivery is held.

5. **Blocking — C has no stable-construction barrier.**

   **Rests on:** Design:218, 522–532; `Driver/Platform/LineGuesses.cs:33–37`.

   C combines separate `var`, configuration and attribute-file reads, but unlike R it has no specified before/after construction rule. For example, `var -l` can discover attribute file A, configuration can then change to name B, and `config --list` can return the new setting. Stamping the files afterward can validate that mixed capture indefinitely while B’s contents and watch root were never captured.

   Validating an already-constructed context before lookup does not establish that its constituent reads belonged to that context.

   **Smallest fix:** give C an explicit capture-and-validation protocol covering configuration discovery, discovered dependencies and their contents. Reject or retry a capture changed during construction, count those starts, and test a configuration change between the two git answers.

6. **Blocking — “not kept” does not reach the frontend fallback.**

   **Rests on:** Design:529–539, 567–568; `Web/bridge/repositories/git.ts:262–310`; `Driver/Trees/GitReads/GitReads.cs:465–478`.

   Below the attribute/context floors, the design stops keeping backend object answers. The page still keeps commit, history, blame and compare answers indefinitely, now under a context revision that cannot describe the missing inputs. Below `--attr-source`, an object diff can depend on worktree attributes that are absent from C; below `git var`’s floor, the effective global attribute file is unknown. Neither event necessarily changes the page’s key.

   The existing backend already avoids caching shallow answers while the frontend independently uses `Infinity`, demonstrating why backend retention alone is insufficient.

   **Smallest fix:** carry cacheability/context capability to the page and apply it to retention, joining and refetch policy, or refuse these unsupported reads. Cover the older-Git fallback through the page cache, not only the launch stand-in.

7. **Blocking — retained reviews omit non-Git inputs from invalidation.**

   **Rests on:** Design:203–204, 238–241, 574–601; `Driver/Trees/SessionTrees/TreeContext.cs:67–75`; `Driver/Trees/Branches/SessionBranches.cs:97–105`; `Modules/Bridge/Routes/LinesRoutes.cs:28–50`.

   The selected line comes from Daoris’s choices, and K′ comes from its grown-branch record. Neither is a Git configuration input in C. The invalidation table handles line *refs* moving, but does not say what changing the configured line or grown base does to a held review.

   Today `SET_LINE` publishes `StateScopes.Driver`; `Web/bridge/state/stateHub.ts:40–41` maps that to the driver key, not the separately keyed diff. A line choice can thus change while Git and tree generations stay unchanged. A grown-base record written after its associated ref notification has the same missing wake.

   **Smallest fix:** include the resolved line choice, recorded/grown bases and their revisions in whole-review identity and validation. Wire their existing record/configuration wakes to dependent reviews, including terminal edits. Test each change with HEAD and all refs held fixed.

8. **Blocking — page-close cancellation has no route to the process owner.**

   **Rests on:** Design:352–357, 423–429, 686; `Web/bridge/call.ts:10–15`; `Web/bridge/sessions/trees.ts:32–35,67–75`.

   The ownership table promises that closing a long-read page ends its Git process. The bridge helper carries only payload and timeout, and the existing Review code explicitly distinguishes cancelling the page’s wait from cancelling host work. A reader job token does not connect those two sides. Without a cancellation door, abandoned searches can continue occupying the process-wide long-read places until their backstop.

   **Smallest fix:** specify a request/waiter cancellation mechanism across the bridge, with one waiter leaving independently and the last waiter cancelling the job. Assign it to the relevant row and prove page closure through the real route boundary. Alternatively, explicitly defer page-close process cancellation and remove the stronger lifecycle promise.

### 3. Findings on rows and proof

9. **Blocking — the first row cannot both apply the new effect controls and preserve today’s answers.**

   **Rests on:** Design:840–847; `Driver/Trees/WorkingTree.cs:459–476`, including REVIEWLOCK1’s version on `main`.

   GITAPP1a-i promises “No answer changes” while asserting §1.1’s per-command controls. Today the range reader disables external diff but does not disable textconv. Adding `--no-textconv` changes configured repositories’ displayed answers. Merge-driver refusal additionally depends on C, which does not arrive until 1a-ii.

   **Smallest fix:** make 1a-i instrumentation plus REVIEWLOCK1’s existing flags only. Assign the new effect-policy behavior and its semantic fixtures to the later rows that have the required context, preserving a genuine before-bench.

10. **Blocking — the graph row requires the later untracked implementation’s proof.**

    **Rests on:** Design:734–743, 853–858; `Driver/Trees/WorkingTree.cs:341–360`.

    GITAPP1a-iii explicitly retains today’s per-file untracked starts, but requires §10.1’s generated census, which covers untracked combinations under the final graph ceilings. Fifty eligible untracked files cannot satisfy those ceilings until 1a-iv replaces those starts with A and U.

    **Smallest fix:** give 1a-iii an explicit interim untracked subgraph and ceiling, then replace both in 1a-iv; alternatively move A/U into 1a-iii. Its acceptance must count the implementation that actually lands.

11. **Non-blocking — binary identity detects selection changes but omits replacement in place.**

    **Rests on:** Design:520; `Driver/Trees/WorkingTree.cs:933–946`.

    A Git executable can be updated at the same path without a Tools selection event. “Version once per binary per process” needs a rule identifying that replacement; otherwise cached answers and capability decisions remain associated with the old binary. Today `GitInUse` already includes the file’s write time specifically for this case.

    **Smallest fix:** define the binary fingerprint from the resolved executable and its replacement stamp, re-probe version when it changes, and invalidate dependent contexts. Include replacement without a Tools press in 1a-ii’s lifecycle proof.

**11 findings; 10 blocking.**

**Verdict: another round.** The read/write split holds, the corrected version floors hold, and deferring the held reader is justified. The remaining changes affect the accepted foundation: which tree D compares, equivalence of line resolution, generated ceilings, validation of retained observations and contexts, invalidation of Daoris-owned inputs, cancellation across the bridge, and independently satisfiable row proofs. D202 should remain proposed until those contracts agree.
