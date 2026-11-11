# The git library design's reads

Both makers' reads of [the git library design](../2026-11-09-git-library-design.md) (GITLIB1), each read-only and independent, kept
as history; the design's next round answers them.

## Codex, the design

Codex's read of the design at `aa230e4408`, run by the steward from outside the install, read-only; verbatim.

**The measures support the direction, but do not prove the entire split as drawn.** Layout, ordinary merge-bases, two-tip divergence and name-only tree diffs favour the library. Keeping walks, patches and working-tree reads on git is well supported: the large fixture’s excluded-tip walk takes 20.4 s versus 1.08 s, patches 3.67 s versus 1.44 s, and status fails. Ref enumeration barely wins there: 1.02 versus 1.09 s. Commit fields and stored-byte performance are unmeasured; counts and ancestry remain graph walks despite their small answers. The actual Branches query, including distances across many tips, needs measurement.

I would block acceptance on these gaps:

- **Worktree parity omits the safety-critical answer.** `tools/git-library-bench/Parity.cs:26` deliberately skips git’s main checkout and compares only linked-tree paths, omitting branches and locks. If that implementation supplies `TreeWorktrees`, put the main checkout on `topic`, then delete `topic` through Daoris: both `GitRefActs` occupancy checks could miss it. **Smallest change:** include the main checkout and compare complete occupancy records, including detached, locked and missing trees; prove the delete remains refused.

- **Admission misses replacement semantics.** §2.4 checks formats and extensions, but neither admission nor parity covers replacement refs. After `git replace --graft` changes a commit’s ancestry, git’s parents, counts and merge-bases change; ordinary library object lookup reads the original object, so moved facts can contradict binary patches and change Review’s base or cleanup judgement. This follows from [git’s replacement semantics](https://www.kernel.org/pub/software/scm/git/docs/git-replace.html) and [libgit2’s object lookup](https://github.com/libgit2/libgit2/blob/main/src/libgit2/object.c). **Smallest change:** admit replacement-bearing repositories to the conditioned binary path until parity passes; recheck when relevant repository state changes.

- **The pool can hold packs indefinitely.** Under §2.2, repeated reads less than a minute apart prevent expiry. Read a packed commit, keep polling, then run the person’s `git gc`: held handles/mappings can obstruct pack removal. The [documented Windows default](https://libgit2.org/docs/reference/v1.8.7/common/git_libgit2_opts.html) lacks delete sharing. **Smallest change:** release repositories and every native descendant after each read batch; prove external repack/gc succeeds during repeated reads. Exclusive leases must survive asynchronous thread switches and prevent disposal during use.

- **Partial clones deliberately lose an existing answer.** §2.4 admits them but returns unavailable for absent objects. Open a historical landed-file preview whose blob was filtered out: today git fetches and displays it; the proposed reader cannot. `FilePreview.cs:201` currently translates failed content reads into `NotOnBranch`. **Smallest change:** distinguish missing objects from absent paths, expose an explicit door-controlled fetch-and-reread, and prohibit absence-based decisions while unread. Merely opening a partial clone does not answer D147’s blank-page objection.

**GITLIB1a–f have sensible dependency order, but insufficient proofs.** Admission and fallback must precede migration; f is an ongoing retirement obligation. Add complete worktree/ref parity to b; multi-tip union counts, `--independent`, criss-cross and shallow-boundary cases to c; actual byte comparisons to d—the bench decodes blobs as UTF-8—and behavioural status parity to e. Count reductions alone prove no answer. Add native-load proof from the published install to a.

**D147 §4.3:** startup cost is answered; credentials/SSH and hooks remain correctly with git; repository coverage, worktrees and “no surprise” remain incomplete above. Size is answered for the shipped `win-x64` install; I found no missing shipped-platform runtime. D228’s format fallback names both its ending condition and deletion row, provided expanded parity—not successful opening—controls retirement.

## Opus (independent), the design

The independent Opus 5.5 reader's read of the design at `aa230e4408`, read-only; as it answered, set down by the steward.

**The split** holds for each kind of read taken alone: a bounded read takes 0.1 to 55 ms in process against 0.3 to 1.2 s a start; walks and patches lose on the large fixture, and working-tree reads fail. The whole-flow figures measure neither today's product nor the split: the bench's Review runs sixteen reads one after another, while the product's Review is a graph (`ReviewGraphReads`) whose ref reads run beside the chain W→S→U→M→D→offer (`ReviewGitCountTests` counts 17 starts); the bench's library columns read status, patches and the walk in process, which the design keeps on git; the bench's look reads every tree's status, while the product's tidy reads none (`AutoTidies`); and the start table was traced, so it includes the 52 to 92 ms ancestry walk the product never pays.

**Blocking.**

1. **Packs held open.** §2.2 keeps a pool closed after a minute idle, and §3 omits the hazard the git-app proposals §13 and the area name (a process holding packs makes the person's own `git gc` ask to retry). Sequence: the reader maps the common directory's packs, commit-graph and midx; a commit act runs git's auto maintenance (`CommitActs.cs:95`), and sessions commit all the time; a repack cannot unlink mapped packs and the commit-graph rewrite is refused; while sessions run, reads come more often than once a minute, so the pool never idles. Change: open per flow and dispose at its end (0.12 s more per Review here), with a Windows Process test that `repack -ad`, `gc --prune=now` and `commit-graph write` under a reader mid-flow leave no old pack and no prompt.
2. **A second path (D228).** §2.1 deletes each moved read's binary path in its row; §2.4 keeps it for refused repositories, with an ending condition that waits on libgit2 reading reftable, which nothing plans, while git 3.0 is to make reftable and SHA-256 its default for new repositories; "the steward weighs" is not a condition, and a log line is not a register entry. Change: a register entry with two ends: deleted when the opens census passes (1f), and the library retired for §2.7's fallback at the first Tools git that defaults to reftable or SHA-256. Also admit split and sparse indexes for refs, merge-bases and counts, none of which reads the index.
3. **The worktree list guards deletes, but parity checked only its paths.** `TreeWorktrees` saying no tree holds a branch lets it go, and a delete is an `update-ref` transaction that does not refuse a branch checked out elsewhere; the census compares only lower-cased paths (`Parity.cs:25`). Sequence: a linked tree whose folder is missing or locked drops out of the library's list, or loses its branch, so its branch reads as unheld and the tidy deletes it. Change: in 1b compare the parsed `WorktreeEntry` (path as given, branch, locked, reason) for a prunable and a locked tree, or keep the list on git.

**Not blocking.**

- Merge-base and count parity for 1c: a criss-cross with two merge bases committed in the same second (libgit2's tie order can pick another base and move Review's range); N↑ counts two refs (`ReviewGraphReads.cs:304`), not one divergence; `--independent`, `--is-ancestor` and replace refs are not compared; the layout's paths are compared only after normalising.
- A partial clone: a blob, or a diff that detects renames, reads blank where git fetches the missing object, and stored bytes feed decisions (`RepositorySetup`, `InstallScripts`, `IntegrationAcceptance`); admit a partial clone for refs, counts and name-only diffs only.
- Large blobs: an in-process blob is read whole into the desktop process; keep a blob over the preview's budget on git, or leave blobs to §2.7's `cat-file`.
- A native fault in libgit2 ends the window, since the driver runs in the desktop app.
- Configuration: libgit2 finds its own system configuration, not Tools' git's, and the global file is handed only where a key is carried (`GitGlobalFile.Hand`).
- Threads: lease a repository per flow; no LibGit2Sharp object may leave the reader (`Commit.Tree` loads lazily).
- The parity class sits in the git Process gate, which the release workflow runs on ubuntu: it needs a Windows run.
- Deployment: no gap; the win-x64 publish lays the native files beside SQLite's.

**Do the wins survive?** Review, after the change, makes W, S, one start per untracked file, D, and the offer's `log` walk, all on the serial chain: with ten untracked files about 14 starts, roughly 6.3 s here, not 0.8 s. The gain is real, about half (the roughly ten ref starts that queue behind .NET's one process-creation lock); the untracked tail is now the largest share, and one start through a temporary index (`GIT_INDEX_FILE`, `add -N`, as the piece keeper already does) would cut it on git. The look's tidy falls from 20 starts on six trees to about 1, the walk.

**Rows.** 1e needs no library: land it first, with the untracked cut, and measure it as the baseline the library must beat. 1a's proof lacks the pack-hold test, the register entry, the deployment rehearsal laying the native library, and a Windows parity run. 1b to 1d are proved by start counts only: add `review.read`'s `ms` and `createdMs` on the install, before and after. 1c must name the cases above. 1f needs the second ending condition.

**D147 §4.3.** The start's cost: answered, measured, though overstated (traced starts, a serial Review). Credentials and SSH: answered. Hooks: answered. Repositories libgit2 does not open: only by a second path, a partial clone half answered. Worktrees: not as claimed, only paths compared. No surprise: yes for refs and objects, apart from configuration and replace refs. Size: yes, for win-x64. Not answered at all: the held reader's lifetime and the repack hazard.


## Codex, round 2

Codex's read of round 2 at `91223b91bb`, run by the steward from outside the install, read-only; verbatim.

**Round 2 needs another revision.** It answers both worktree blockers by keeping occupancy on git, answers the partial-clone blocker by preserving fetch-and-reread and distinguishing unread from absent, and supplies D228’s two retirement conditions. Replacement semantics and Windows pack holding remain incomplete. The earlier measurement, deployment and parity requests now have proposed checks; those checks are not yet evidence.

Remaining blockers:

- **Windows mappings — §§5.5–5.6.** Read a packed object, retain its mapping, then run external `gc` on Windows 10: delete sharing alone does not permit deletion while that mapping exists. Ten seconds bounds the obstruction. The [upstream unlink implementation](https://github.com/git-for-windows/git/blob/main/compat/mingw.c) explicitly distinguishes this legacy behavior. **Smallest change:** prohibit retaining mappings between questions on affected Windows; require the hold proof to pin an actual mapped pack through attempted deletion under legacy semantics.

- **Replacement-sensitive answers — §5.6, `RefSnapshot.Format`, `ContentHolds`.** The ref listing includes `%(tree)`, not merely stored ids. Replace a branch tip with a commit having another tree: library-derived original trees can change the content-holding judgement, whereas [git reads replacement objects for these fields](https://github.com/git/git/blob/v2.53.0/ref-filter.c). Also, adding a replacement after lease admission leaves later graph questions incorrectly admitted. **Smallest change:** send object-derived ref fields through the object engine and recheck replacement admission before each affected question.

- **Held-reader state — §5.5.** Start the repository’s reader from worktree A, then ask worktree B’s implicit-HEAD commit fields (`WorkingTree.ProvenanceAsync`): unresolved HEAD answers for A. Separately, add a replacement between batch requests: [git initializes its replacement map once](https://github.com/git/git/blob/v2.53.0/replace-object.c), so the held process can answer differently from a fresh git. **Smallest change:** resolve worktree-relative names in the caller’s context and restart the reader when replacement/configuration state changes.

- **Merged reads cross writes — §5.4.** W begins and advances the epoch; R starts during W and reads the old ref; W completes; a post-write caller joins still-running R because no further write began. It receives the pre-write state. A session’s own git can similarly move state without advancing any scheduler epoch. **Smallest change:** invalidate joining at write admission and completion, preserve write barriers, and restrict cross-flow joining to questions whose complete immutable inputs and context establish equivalence.

- **Retirement detection — §5.9.** Probe once, then change global `init.defaultRefFormat` without changing the executable: `GitInUse` remains identical, so the promised retirement condition goes undetected. **Smallest change:** include effective initialization configuration in probe invalidation and specify what immediately disables library admission when the condition fires.

The dependency order is sensible: a first; b before c/d; d before e/f; g’s obligation established with d. The proofs need these sequences added. Row b also cannot establish its bound solely from the Git job census: today Whole runs join `ProcessPurpose.Read`, while Held runs use separate jobs. Count scheduler-owned parent runs; prove same-repository hook re-entry and write cancellation through postconditions. Row a needs literal/NUL pathspec cases. Rows c–f need behavioral equality before count reductions.

I would avoid the 2.36 refusal: one [`--batch` protocol](https://git-scm.com/docs/git-cat-file) already supplies type, size and contents, allowing bounded consumption without introducing a second implementation.

Keeping the service host’s two reads outside the desktop service is reasonable: retain explicit census exceptions and scope “one door” to the driver process.

## Opus (independent), round 2

The independent Opus 5.5 reader's read of round 2 at `91223b91bb`, read-only; as it answered, set down by the steward.

**Round 1's blocking findings.** Most are answered: packs held open, for the library lease (one flow, at most ten seconds, delete shared, a Windows hold test), and only half for the object reader (B2); the second path (two ends, each held by a failing test and said in the area's rule); the worktree list (kept on git, the deletes proved still refused); replace refs; and partial clones, for missing against absent, the reader's own fetch opening B3.

**New blocking findings.**

- **B1. A merged read can answer an older state.** §5.4 joins a read already running, and its epoch moves only on writes made through the scheduler. Sequence: a tick's `rev-parse --verify HEAD^{commit}` and `--no-optional-locks status --porcelain` of tree X start; the session commits or fixes the tree with its own git; the landing's `ReadTreeAsync` (`LandPerformer.cs:458`) asks the same reads, joins them, and gets the old tip and status fingerprint, so a failed landing is not tried again. The area says a landing reads afresh before each act because another process can move either; `RouteFlights.Forget` exists for exactly this. Change: join only a read still queued, never one running.
- **B2. The held reader holds a tree's folder and its packs.** (a) Its working folder is whichever tree asked first; while another flow's asks keep it alive for up to 10 s, Windows will not remove that folder, so the pool's retire or `worktree remove` of X right after X's Review fails; names like `HEAD:path` and `:path` also resolve in the reader's tree, not the asker's. Change: start the reader in the common directory and take only ids or full ref names. (b) Git for Windows opens packs without delete sharing, and the next ask restarts the reader on the very packs gc is deleting; under the look's steady asks the ten-second end leaves no gap, so the person's `git gc` asks to retry over and over, and the hold test's "reader mid-batch, never asked" cannot pass. Change: start no reader, and end a running one, while `gc.pid` or `objects/maintenance.lock` exists, each object meanwhile one start; prove it with `gc` itself.
- **B3. A fetch inside the reader blocks every object read.** In a partial clone `cat-file` fetches a missing object itself, through the doors, and that fetch may wait up to 15 minutes for the person's go-ahead (`GitAsks.Bound`); it runs inside a read's place, outside the two places allowed for remote gits, and the reader answers in order, so every object ask of that repository waits behind it, a person's preview included. Change: run the reader with `GIT_NO_LAZY_FETCH=1`, re-ask a `missing` answer as one start counted as a remote git, and answer a person's asks first in the reader's queue.
- **B4. What may be ended is not defined.** Today's runner ends any git on its caller's token (`ChildProcess.RunAsync(info, bound, ct…)`), and round 2 hands tokens to work that ran without one. A plain working-tree `diff` takes `index.lock` whatever `--no-optional-locks` says (TreeDiffs' remark), and `WorkOverlaps.cs:44` lacks `diff.autoRefreshIndex=false`; ended mid-write it leaves the lock behind and the session's next commit fails. Change: end only a git that `RefSnapshot.MayWrite` calls a read and that takes no lock (extend the optional-locks census to cover this); every other git runs untokened once started.
- **B5. Leases can wait on themselves.** §5.6 allows eight per process, each held for its whole flow; a page's read is a flow, and housekeeping's inspect sweeps every repository in one flow (`HelpHousekeeping.cs:133`), so at the ninth repository it waits on its own eight. Change: when eight are open, close the least recently used lease not in a call.
- **B6. Git starts missing from §5.2.** `ToolsRoutes.cs:621` (`git config --system --list`) and `:606`/`ToolsCommand` (a git file's version) start git through `ToolQuestions`. Change: list them, route them through the service, and make the census recognise git started by file through `ToolQuestions`.

**Not blocking.** Row b's proof counts processes in the Git job, which also holds hooks and transports, so "never more than four gits" holds only for plain reads: count places instead. When an over-limit blob ends the shared reader, the other asks in flight are asked again, never answered `missing`. A `HeldChild` past its bound keeps running until its job ends it, so its place is held until it exits. The census names the programs Daoris starts that start git themselves (gates, generators, plugin hooks), as it names the agents'.

**Rows a to g.** The order is right (a alone; b before c and d; e and f after d; g after d). Proofs to add: b, a join never answers an ask made after a write outside the service; c, the reader's tree removed, a fetch beside a person's preview, gc run with the reader yielding; d, one flow over nine repositories.

**The author's two flagged calls.** `git-too-old` below 2.36 is not needed: `cat-file --batch`, in every supported git, prints `<oid> <type> <size>` before the bytes, which is all the limit needs, so there is no floor, and git 2.31 to 2.35 keeps previews and the landings' stored-byte reads (`BacklogMerge`, `IntegrationAcceptance`). The service host's two gits outside the service: right; the host is its own program that must run with no desktop, and both reads take no lock (`ls-files`, `--no-optional-locks status`), so the census names them with that condition, not as an exception to the one door.


## Codex, round 3

Codex's read of round 3 at `78aa979e6d`, run by the steward from outside the install, read-only; verbatim.

**Round 3 still needs revision.** It explicitly addresses all eleven findings, but does not close every sequence.

For **Codex’s five blockers**, replacement-derived ref fields, queued-read merging and retirement detection are answered for the named cases. Windows holding and held-reader state remain partly open. For **Opus’s six**, B1, B4, B5 and B6 have adequate design answers; B2(a) is answered, B2(b) remains, and B3 is only partly answered. These are proposed constructions and checks, not executed proof.

The remaining or new blockers are:

- **§10.4: pack lifetime is not protected by notifications.** Read a packed object through an alternate object store, then run that store’s `gc`: its markers and pack changes occur outside the watched common directory. The reader retains its mapping. Even locally, a watcher callback has no ordering guarantee before deletion. **Smallest change:** release held mappings between questions on affected Windows unless every backing store’s lifetime can be protected; test alternate-store gc and delayed notification delivery. Git explicitly supports [borrowed object stores](https://git-scm.com/docs/gitrepository-layout).

- **§§10.3–10.5: caller configuration is incomplete.** Enable `worktreeConfig`; worktree B sets `core.useReplaceRefs=false`, while A uses replacements. Resolving B’s HEAD to an id does not prevent the common-directory reader from applying A’s replacement. The proposed stamp also omits B’s `config.worktree`. **Smallest change:** carry and stamp the caller’s effective configuration, including conditional-include context, and partition/restart reader state accordingly. [Git’s configuration rules](https://git-scm.com/docs/git-config) distinguish these contexts.

- **§§5.5/10.4: raw parents differ from effective parents.** Open a shallow-boundary commit. Raw `cat-file` bytes retain its parent; `log`’s `%P` reports none. `GitReads.CommitAsync` then selects an unavailable parent instead of the empty tree, making the commit page unreadable. Grafts present the same distinction. **Smallest change:** apply effective shallow/graft parent semantics before publishing commit fields; prove the page and commit-plan judgements. See [shallow handling](https://github.com/git/git/blob/v2.53.0/shallow.c) and [parent formatting](https://github.com/git/git/blob/v2.53.0/pretty.c).

- **§§10.4/10.6: the lazy-fetch version predicate is wrong.** Git 2.44.0 is admitted, but ignores the variable; a filtered-object request can fetch inside the held reader and block previews again. **Smallest change:** use a verified capability predicate, with unsupported builds taking the remote one-shot path. Compare upstream [2.44.0](https://github.com/git/git/blob/v2.44.0/promisor-remote.c) and [2.44.1](https://github.com/git/git/blob/v2.44.1/promisor-remote.c).

- **§§5.4/10.6: remote re-entry can deadlock.** Two hook-running acts in different repositories occupy both outside-wait places. Each hook requests a missing blob; both remote retries wait for places held by their waiting parents, until a bound kills an act. **Smallest change:** reserve re-entrant remote capacity and test this combined sequence.

- **§10.4: path framing loses valid objects.** Sending `<id>:<newline-containing-path>` through plain `--batch` creates multiple requests, producing missing or misaligned answers. **Smallest change:** resolve paths with NUL-safe plumbing and send only object ids, or use a capability-gated NUL protocol. [Batch protocol](https://git-scm.com/docs/git-cat-file).

**The class censuses do not establish missed-member failure:** an omitted dependency inside an already-listed holder passes a field/type census. Require negative mutation proofs for those omissions.

**Rows a–g are ordered correctly:** a→b→c→d, then independent e/f; g’s obligation begins with d. Their proof specifications improve substantially, but need the sequences above and passing results before acceptance or count reductions. D228’s two retirement ends remain explicit.

## Opus (independent), round 3

The independent Opus 5.5 reader's read of round 3 at `78aa979e6d`, read-only; as it answered, set down by the steward.

**Round 2's blocking findings.** Fully answered: Codex's derived ref fields, the held reader's state, merged reads across writes, and retirement; Opus B1, B2a, B4 and B5. Partly: Codex's mappings and B2b (gc and maintenance covered; a writer that replaces a file when it finishes, and alternates, not: N3, N4); B3 for a read that fails, not for one that degrades and exits 0 (N2); B6's named doors routed, but the census missed another door, and the gate checks only two purposes (N1).

**New blocking.**

- **N1. A git door the census missed, which the gate then breaks.** For a plugin declaring `{"id":"git"}` (the `acme.tooled` fixture does), `PluginToolChecks.CheckAsync` → `PluginToolRuns.RunAsync` → `ChildProcess` (purpose `Read`) asks Tools' git `--version` and runs the manifest's `ready` checks; §10.7 does not list it, and once the gate refuses a git with no scheduler place, the check reads "it could not be started" and the plugin shows git as missing. Change: route a declared tool whose id is git through `GitService.AskFileAsync`, list it in §10.7, and refuse a git image for every purpose, not only `Git`/`Read` (`GitIdentityActs.RunWithAsync` starts git as `Tool` today).
- **N2. A partial clone's landed preview reads blank.** `FilePreview.ReadLandedAsync` takes the blob's size from `ls-tree -l`; under `GIT_NO_LAZY_FETCH=1` git prints a missing blob's size as `BAD` and exits 0, so nothing is asked again as a remote start, `Listed` rejects the size, and the answer is `NotOnBranch`, which §5.5 forbids. Change: take the size from the `--batch` header; the census lists reads that exit 0 with a degraded answer when lazy fetch is off; the case in the proofs.
- **N3. The reader restarts on the file a writer is replacing.** `git multi-pack-index write` (or `repack --write-midx`) creates its `.lock`, which ends reader R1; the next question starts R2 at once, mapping the old `multi-pack-index`; the writer's final rename over it is refused, no event ends R2, and Git for Windows retries for about 1.3 s, then asks the person. Change: after a change in `objects/pack/`, start no reader until the folder has been still for a settle interval with no `*.lock` or `.tmp-*`, each object meanwhile one start; this write in the hold test.
- **N4. Alternates.** The reader maps an alternate's packs but checks `gc.pid` and watches `objects/pack/` only in its own repository, so the person's `git gc` in the source repository hits B2b's sequence. Change: the markers and the watcher on every object directory `objects/info/alternates` names, or no reader in such a repository.

**Would each census fail on a missed member?** Class 1: `GitReadOnce`, `RefSnapshot` and `GitFacts` let parallel stages of one flow join a running read under the first asker's token; once speculative reads have their own tokens a stage using the answer inherits a speculative asker's cancellation (no such pair in Review today, nothing guarantees it): the memos ask under the flow's token, and the census names them as joins of a running read. Class 2 misses N3 and N4. Class 3 leaves out the runners' own stop once their reader has enough (`GitRuns.cs:272`, `:390`), safe only because the streamed reads are endable, so list it. Class 4's source half missed N1 because the program's path is a variable, and its runtime half would only catch it by breaking production, since the plugin checks' fast tests use a stand-in.

**Not blocking.** D228: "each object is one start meanwhile" keeps today's `cat-file blob`, `show c:p` and `log`-format parsers beside the reader's, and they never end; make that one start a `--batch` asked one question through the same parser. A reader started in a common directory not named `.git` is refused under `safe.bareRepository=explicit`: hand it `GIT_DIR`. Row a's diff against its own index is a working-tree diff: write it with `diff.autoRefreshIndex=false`, or row b's extended optional-locks census goes red on it.

**Row order and row b.** a → b → c → d → e and f, g after d, is right (d needs c's derived fields). The scheduler has three gaps: (i) the person's reserved place is protected only from the background: two person-class gits waiting outside Daoris (a push running its hook, a fetch waiting up to 15 minutes on a grant) plus two background reads fill all four places, so reserve one place against every long-running class; (ii) a reader started by a person keeps the person's place for up to 10 s while background asks keep it alive, so a reader's place takes the class of the asks in it; (iii) an unended git keeps its place until it exits, so four on a hung network share stall every git: bound them, or count their places apart and say so in the machine log. Proofs to add to b: a person's read still placed when every place is held by long-running gits, and each class's longest wait; a removal while an unendable git is queued in its folder; a used read that joined a memo surviving its speculative starter's cancellation; the gate refusing a git started as `Tool`; N1 to N4.


## Codex, round 4

Codex's read of round 4 at `b997f4474c`, run by the steward from outside the install, read-only; verbatim. The findings for row b were handed to its builder.

Round 4 answers most surviving round 3 findings: effective parents, newline framing, preview sizing, the named plugin route, memo cancellation, runner stops and census mutations have explicit fixes. The original two-hook re-entry sequence is answered. Held-mapping findings dissolve between questions, subject to row d’s Windows proof. Caller configuration and the version predicate remain incomplete. Proposed tests are not passing evidence.

- **Wrong-state sharing — blocking (§§10.3, 14.5).** Stamping `GitReadCache` does not cover flow memos. `GitFacts.SameRepository` merges worktrees’ by-id answers; `RefSnapshot.TreeHeld` searches without caller context. In one flow, A reads a replaced commit, then B, with `core.useReplaceRefs=false`, receives A’s tree or graph answer before admission runs. That can corrupt `ContentHolds`’ deletion judgement. Smallest change: partition every memo and derived-tree lookup by repository and caller configuration context; test both worktrees within one flow.

- **Lazy-fetch floor — blocking (§14.5).** “At least 2.44.1” is wrong: [2.45.0 lacks the guard](https://raw.githubusercontent.com/git/git/v2.45.0/promisor-remote.c), while [2.39.4 already contains it](https://raw.githubusercontent.com/git/git/v2.39.4/promisor-remote.c). A 2.45.0 filtered-object read therefore fetches inside an ordinary place. Use a verified capability probe, or an accurate maintenance-release predicate; test unsupported intermediate releases, not only current binaries.

- **Deadlock remains possible (§14.2; `GitLaunch`’s diff kinds).** Three background diffs and one person’s diff can fill four places; their textconv/filter programs synchronously request service reads. None has an outside-waiting parent eligible to lend capacity. They wait until bounds kill the parents. Smallest change: classify helper-running reads as outside-waiting, or extend bounded lending to every scheduled parent capable of re-entry. Add this combined test.

- **Person’s read starvation (§§5.4, 14.2).** The revised reservation protects an eligible local read from background/remote occupancy. Two occupied remote places can still delay a missing-blob preview’s retry. Specify selection of the oldest *eligible* ask, so a blocked remote queue head cannot obstruct a later local read. This is a latency issue, not independently blocking.

- **Write order (§10.2).** W1 awaited before W2 remains ordered; unrelated flows are explicitly unordered and journaled acts retain the checkout lock. No demonstrated new write-order blocker; test those guarantees.

- **Mid-write termination (§10.6).** Bounds and shutdown explicitly may kill writes; `ActRuns` preserves unknown outcomes through postconditions. Cancellation and runner stops must also exclude fetch-enabled retries and unsupported-version reads: `cat-file` arguments alone cannot establish `Endable`. Test cancellation during the nested fetch.

- **Escaping starts (§§14.3–14.4).** Recognised git images are refused without places across the named purposes. No bypass is demonstrated; retain real-runner tests for variable-file routes and `HeldChild`.

The dependency graph is otherwise sound: a→b→{c,d}, d→{e,f,g}. **But b enables no-lazy-fetch before c fixes `ls-tree -l`’s `BAD` preview regression.** Move that handling into b or land them atomically. Add the sequences above; require parity, deployment/Windows checks and install measurements before lowering counts.
