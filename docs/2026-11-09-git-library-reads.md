# The git library design's reads

Both makers' reads of [the git library design](2026-11-09-git-library-design.md) (GITLIB1), each read-only and independent, kept
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
