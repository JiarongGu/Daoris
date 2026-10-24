using System.Globalization;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <param name="Path">Where the tree is — what the session record carries and the lock keys on.</param>
/// <param name="Branch">The session's own branch, never reused; git itself refuses a second use.</param>
/// <param name="BasedOn">Which point the branch grew from, spelled for a person — the canonical line, or HEAD with the reason.</param>
/// <param name="Sentence">The whole act in one sentence, price included — for whoever is watching.</param>
/// <param name="GrewFrom">The chain step's branch it grew from (CHAIN2), when it did — null for the canonical line.</param>
public sealed record TreeOpened(string Path, string Branch, string BasedOn, string Sentence, string? GrewFrom = null)
{
    /// <summary>
    /// <see cref="Sentence"/> with its codes (LANG1a, the language design §4 rows 25–26): the tree's branch and what it grew
    /// from as facts, never its path; and where its start went unrecorded, that line with git's words beside it.
    /// </summary>
    public Noted? Opening { get; init; }

    /// <summary>
    /// What its session's agent is told of the dependencies copied into it from its checkout (TREEDEPS1): what was copied, and to
    /// skip the <c>npm ci</c> that would delete it. Null where nothing was.
    /// </summary>
    public string? Dependencies { get; init; }
}

/// <param name="Removed">Whether the tree is gone. False is a refusal, not a failure.</param>
/// <param name="Message">What happened, or what would have been lost and how to mean it.</param>
public sealed record TreeRemoval(bool Removed, string Message);

/// <summary>
/// What came of asking to merge a session tree into the canonical line (D51 rule 6). A refusal is an
/// ANSWER — the checkout is busy, there is nothing to merge, the branches conflict — and each names
/// what the person would do about it.
/// </summary>
public sealed record TreeMerge(bool Merged, string Message);

/// <param name="Path">Where it is.</param>
/// <param name="Workspace">Whose circle, read from the layout Daoris itself chose.</param>
/// <param name="Repository">Whose repository, same.</param>
/// <param name="Branch">
/// The session branch checked out in it; <see cref="NotATree"/> for a folder with no link to a repository, and
/// <see cref="SetAside"/> for a folder the look moved aside.
/// </param>
public sealed record SessionTree(string Path, string Workspace, string Repository, string Branch)
{
    /// <summary>What a folder with no <c>.git</c> is listed as (AUTOTIDY2c): no tree, which the look moves aside once it is left quiet.</summary>
    public const string NotATree = "(not a tree)";

    /// <summary>What a folder the look moved aside is listed as (AUTOTIDY1, AUTOTIDY2c), until the purge deletes it.</summary>
    public const string SetAside = "(moved aside)";

    /// <summary>When the look moved it aside, read from its name; null for anything not moved aside.</summary>
    public DateTimeOffset? MovedAside { get; init; }

    /// <summary>
    /// A folder moved aside past its date that the look's purge keeps (CLEANUP1, after review): why, in its own clause (a later write, a
    /// link, a folder that cannot be read). Null before its date, where the purge takes it, and for anything not moved aside.
    /// </summary>
    public string? PurgeKept { get; init; }

    /// <summary>
    /// A folder moved aside whose package folders the slim dropped (KEEP1a, the cleanup design §5): which, in git's spelling, from the
    /// manifest beside it. None for anything else.
    /// </summary>
    public IReadOnlyList<string> Slimmed { get; init; } = [];
}

/// <summary>
/// The worktree half of D51: a repository may have more than one working tree, and the extra ones are
/// Daoris's to open and account for — <c>&lt;home&gt;/trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>,
/// each a real linked worktree of the registered root on a branch of its own.
/// </summary>
/// <remarks>
/// <para><b>Daoris owns the location; git owns the contents</b> (D51 rule 2) — the same arrangement as
/// a credential profile (D49 §4), and for the same reason: a directory Daoris created is one it may
/// reason about, and one scattered beside somebody's checkout is not. That is also why
/// <see cref="RemoveAsync"/> refuses anything outside the trees home outright, whatever git would say.</para>
///
/// <para><b>Nothing merges itself and nothing deletes itself</b> (rules 6–7). Opening never touches
/// the root's own state; removal refuses while the tree holds uncommitted changes or commits the
/// canonical line has not taken, naming what would be lost — and the person may say it again with
/// force, meaning it. Commits whose work a squash merge or a cherry-pick holds by content lose nothing,
/// and go unforced saying where that work is (SQUASHTIDY1), the commits themselves kept on a recovery
/// ref the sentence names. A branch goes only while it is the commit judged, and a tree only while
/// nothing in it is uncommitted or ignored and its alone (SQUASHTIDY1c).</para>
///
/// <para><b>The name is minted here, not borrowed from the session id</b>, because the order is
/// tree → open → spawn: the ledger's open needs the tree path for the lock, so the id it would borrow
/// does not exist yet. Short and random, like the id itself; the record ties the two together.</para>
///
/// <para><b>One type, a front over classes</b> (D166's PART10i–l notes): the opening, the listing and what a tree is stay here;
/// every other part is a class over the shared <see cref="TreeContext"/>, built once below with the classes it takes in its
/// constructor, and this keeps its callers' one-line calls.</para>
/// </remarks>
/// <param name="plugins">
/// The plugin wire a branch rule's landing speaks (WSR4, D100) — the home's own plugins by default; the
/// shell and the terminal hand one that says the plugin's lines where each says things.
/// </param>
/// <param name="variable">The environment the trees read (ENVSEAM1): this process's by default, a test's own where it hands one.</param>
public sealed class SessionTrees(string home, LandingPlugins? plugins = null, Func<string, string?>? variable = null) : IWorkTrees
{
    /// <summary>What the parts share (PART10i): the home, the plugin wire, the seams.</summary>
    private readonly TreeContext _context = new(home, plugins ?? new LandingPlugins(home), variable);

    /// <summary>The shared context, for Review's graph (GITAPP1a-ii), which reads what <see cref="ReviewRanges"/> and <see cref="LandedReviews"/> read.</summary>
    internal TreeContext Context => _context;

    // The parts, each built once at first use (a field initializer cannot read `_context`), leaves first; a class takes the
    // others it reads in its constructor, and none takes this front.
    private TreeFollows? _follows;
    private ContentHolds? _content;
    private TreeSquashes? _squashes;
    private PullRequestAsks? _asks;
    private TreeHandOffs? _handOffs;
    private BranchReplays? _replays;
    private ReviewRanges? _ranges;
    private LandedReviews? _reviews;
    private TreeGates? _gates;
    private TreeSetAside? _setAside;
    private SetAsideKeeping? _setAsideKeeping;
    private TreeRemovals? _removals;
    private TreeSweeps? _sweeps;
    private AutoTidies? _autoTidy;
    private TreeStrays? _strays;
    private PassLeftovers? _leftovers;
    private LandedCleanups? _cleanups;
    private BranchJudging? _judging;
    private TreeSync? _sync;
    private TreeUpdates? _updates;
    private TreeWorkReads? _work;
    private OnlyHereProof? _onlyHere;
    private TreeTidies? _tidies;
    private TreeLandSyncs? _syncs;
    private TreeLands? _lands;
    private EntryLands? _entries;
    private EndKeeping? _keeping;
    private TreeReturns? _returns;

    /// <summary>The end judgement's one writer (FABRIC-END2): where the look's tidy and <i>Accept</i>'s tidy keep what the line holds.</summary>
    internal EndKeeping Ends => _keeping ??= new(_context, Asks);

    /// <summary>Where each session tree's work is, and the branch the layout names for it (TREEFOLLOW1).</summary>
    private TreeFollows Follows => _follows ??= new(_context);

    /// <summary>Where a branch's work is held by content, and the delete at the commit judged (SQUASHTIDY1, SQUASHTIDY1c).</summary>
    private ContentHolds Content => _content ??= new(_context);

    /// <summary>The squash's commit and the record's word of what it squashed (PROCESS1b-ii).</summary>
    private TreeSquashes Squashes => _squashes ??= new(_context);

    /// <summary>The occasions' asks, <i>Ask again</i> and what a kept answer proves (PLUGHOOK1a, PLUGHOOK1c).</summary>
    private PullRequestAsks Asks => _asks ??= new(_context);

    /// <summary>The hand-off's look and its press (WSR5b).</summary>
    private TreeHandOffs HandOffs => _handOffs ??= new(_context);

    /// <summary>A judged branch replayed, and the grown record's tidying (WSR6).</summary>
    private BranchReplays Replays => _replays ??= new(_context, Leftovers);

    /// <summary>Where a session tree's review is measured from (REVIEWBASE1).</summary>
    private ReviewRanges Ranges => _ranges ??= new(_context);

    /// <summary>A landed session's review (REVIEW2, D113).</summary>
    private LandedReviews Reviews => _reviews ??= new(_context);

    /// <summary>The landing gate's parts for a tree (XAGENT1f, WORKFLOW1f).</summary>
    private TreeGates Gates => _gates ??= new(_context);

    /// <summary>What the look's own tidy takes by itself, its last guards, and its set-aside (AUTOTIDY1).</summary>
    private TreeSetAside SetAsides => _setAside ??= new(_context);

    /// <summary>The set-aside kind's removal, the purge and the slim, which the Keeping pass runs (KEEP1a).</summary>
    private SetAsideKeeping Keeping => _setAsideKeeping ??= new(_context, SetAsides);

    /// <summary>A session tree's removal (D51 rule 7, D88, SQUASHTIDY1c).</summary>
    private TreeRemovals Removals => _removals ??= new(_context, Follows, Content, Squashes);

    /// <summary>The session branches' half of the clean-up, for the person's press and the look's own (D88, AUTOTIDY1).</summary>
    private TreeSweeps Sweeps => _sweeps ??= new(_context, Asks, Content, Follows, Squashes, SetAsides);

    /// <summary>The look's own tidy of one repository (AUTOTIDY1), the landings' branches with it (AUTOTIDY2b).</summary>
    private AutoTidies AutoTidy => _autoTidy ??= new(_context, Follows, Replays, Sweeps, SetAsides, Cleanups, Ends);

    /// <summary>The look's own tidy of the folders under a repository's trees that are no tree of its own (AUTOTIDY2c, OPENCUT1).</summary>
    private TreeStrays Strays => _strays ??= new(_context, SetAsides, Content);

    /// <summary>What a killed opinion's or replay's pass left under a repository's trees, moved aside by the next (STRAYOTHERS1).</summary>
    private PassLeftovers Leftovers => _leftovers ??= new(_context, SetAsides);

    /// <summary>The clean-up's list and press, and each landed branch judged (D88, WSR5).</summary>
    private LandedCleanups Cleanups => _cleanups ??= new(_context, Asks, Content, Replays, Sweeps, Ends);

    /// <summary>Each branch judged for the replay (WSR6).</summary>
    private BranchJudging Judging => _judging ??= new(_context, Cleanups);

    /// <summary>The look, the press and the scope of bringing up to date (WSR6, WSR7).</summary>
    private TreeSync Sync => _sync ??= new(_context, Asks, Cleanups, Judging, Replays);

    /// <summary>The press that brings one tree up to date (REVIEWBASE2-i).</summary>
    private TreeUpdates Updates => _updates ??= new(_context, Follows, Judging, Replays, Ranges);

    /// <summary>What a tree holds that nobody has accepted (SESSUX1a, D126 §2.1).</summary>
    private TreeWorkReads WorkReads => _work ??= new(_context, Squashes, Content, Follows);

    /// <summary>Whether a tree's work is anywhere but Daoris's own branches, and its discard (PAUSE1d).</summary>
    private OnlyHereProof OnlyHere => _onlyHere ??= new(_context, Removals);

    /// <summary>A landing's tidy of the other session branches its work holds, and the person's door for one (LAND3).</summary>
    private TreeTidies Tidies => _tidies ??= new(_context, Asks, Content, Replays, Squashes, Removals);

    /// <summary>A landing in sync with its line: the fetch, the replay of what the line lacks, the tree's follow (LANDSYNC1).</summary>
    private TreeLandSyncs Syncs => _syncs ??= new(_context, Replays);

    /// <summary>The landing: its plan, the merge, the branch form and its advance, and the rule's tidy (WSR1, D87, D100).</summary>
    private TreeLands Lands => _lands ??= new(_context, Follows, WorkReads, Removals, Tidies, Syncs, Ends);

    /// <summary>An entry's Accept: its parts' work on one branch, in ledger order (ACCEPTALL1, D167).</summary>
    private EntryLands Entries => _entries ??= new(_context, Lands, Syncs);

    /// <summary>A session's tree asked for before it goes on, and made again where its folder is gone (TREEGONE1).</summary>
    private TreeReturns Returns => _returns ??= new(_context, Follows, SetAsides);

    /// <summary>
    /// The test seam (SQUASHTIDY1c): run with the branch's name between a judgement and the delete it cleared, where a test
    /// moves the branch as a session or a person could. Production sets none.
    /// </summary>
    internal Func<string, Task>? BeforeDeleting { get => _context.BeforeDeleting; init => _context.BeforeDeleting = value; }

    /// <inheritdoc cref="TreeContext.BeforeRefDeleted"/>
    internal Func<string, Task>? BeforeRefDeleted { get => _context.BeforeRefDeleted; init => _context.BeforeRefDeleted = value; }

    /// <summary>The test seam (AUTOTIDY1): true for a checkout whose working trees git could not list. Production sets none.</summary>
    internal Func<string, bool>? ListFails { get => _context.ListFails; init => _context.ListFails = value; }

    /// <inheritdoc cref="TreeContext.Clock"/>
    internal Func<DateTimeOffset> Clock { get => _context.Clock; init => _context.Clock = value; }

    /// <summary>The test seam (UIFREEZE1): git's guess at a checkout's line, which a test counts. Production asks git.</summary>
    internal Func<string, CancellationToken, Task<string?>> CheckoutLine { get => _context.CheckoutLine; init => _context.CheckoutLine = value; }

    /// <summary>The branches this machine's landings made (WSR5) — the only ones the clean-up and the hand-off act on.</summary>
    public LandedBranches Recorded => _context.Recorded;

    /// <summary>Where this machine's session branches started (WSR6) — what bringing one up to date cuts at.</summary>
    public SessionBranches Grown => _context.Grown;

    /// <summary>Every tree this machine's Daoris has opened lives under here, and only here.</summary>
    public string TreesRoot => _context.TreesRoot;

    /// <inheritdoc cref="TreeContext.OneLook"/>
    internal IDisposable OneLook() => _context.OneLook();

    /// <summary>
    /// Open a fresh tree for a session in <paramref name="root"/>. Throws <see cref="DriverException"/>
    /// with git's own words when it cannot — before any record exists, which is the caller's contract.
    /// </summary>
    /// <param name="from">
    /// A branch to grow from instead of the canonical line — the one a chain's previous step landed on
    /// in this repository (CHAIN2), so the next step's tree holds the unmerged work it builds on or
    /// checks. One that no longer exists falls back to <paramref name="landed"/>, then to the canonical line, and the
    /// answer says so.
    /// </param>
    /// <param name="landed">
    /// The branch the chain's step before landed on (LAND2c, D82 as D145 amends it), tried when <paramref name="from"/> is gone:
    /// a rule's tidy removes that session branch at its landing, and a step grown from the line could not move the chain's
    /// branch on. With <paramref name="partOf"/>, the branch that part's work landed on.
    /// </param>
    /// <param name="partOf">
    /// For a further part of a session (SESSONE1d, D167 point 4, D82): the earlier part whose tree it would have gone into, gone
    /// since its landing. The tree grows from <paramref name="landed"/> where that branch stands, else from the line, and the
    /// opening says which and whose tree is gone. Without <paramref name="from"/>: a part grows from where its work landed.
    /// </param>
    public async Task<TreeOpened> OpenAsync(
        string root, string repository, string workspace, CancellationToken ct = default, string? from = null, string? landed = null,
        string? partOf = null)
    {
        // The root must BE the top of its own working tree — proven, not assumed. Git resolves a
        // repository by walking UP from wherever it is asked, so a root that is not one (a
        // mis-registered path, a folder inside some other checkout) would silently grow worktrees and
        // branches on whatever repository CONTAINS it. The first run of this class's own test suite
        // did exactly that, to this repository — which is why this check exists and refuses loudly.
        var (topCode, toplevel, topErr) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (topCode != 0)
        {
            throw new DriverException(
                $"`{repository}`'s registered root is not a git repository ({FirstLine(topErr)}) — "
                + "a session's tree is one git links to its checkout, and there is nothing here to link it to.");
        }

        if (!SamePath(toplevel.Trim(), root))
        {
            throw new DriverException(
                $"`{repository}`'s registered root {root} is INSIDE the repository at {toplevel.Trim()} "
                + "rather than being one — refusing to grow a tree on a repository the registration "
                + "does not name. Re-run `daoris connect` from the actual root.");
        }

        // The repository's line (WSR2): what the person set for it or its workspace, else the
        // checkout's guess, else the root's HEAD — and the answer SAYS which, because a branch grown
        // from the wrong point is invisible until merge time.
        var line = await _context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false);
        var basedOn = line.Branch is not null
            ? CanonicalLine.Describe(line)
            : "the root's HEAD (no canonical line is declared)";
        var start = line.Branch is { } set ? await StartOfAsync(root, set, line, repository, ct).ConfigureAwait(false) : "HEAD";

        // A chain's previous step's branch, where it still stands (CHAIN2). Session branches are refs
        // of the root's own repository, so a linked worktree grows from one like any other branch.
        string? grewFrom = null;
        if (from is { Length: > 0 })
        {
            var (known, _, _) = await WorkingTree.GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{from}"], ct).ConfigureAwait(false);
            var (chain, _, _) = known != 0 && landed is { Length: > 0 } && BranchName.IsValid(landed)
                ? await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{landed}"], ct).ConfigureAwait(false)
                : (1, "", "");
            if (known == 0)
            {
                start = grewFrom = from;
                basedOn = $"`{from}`, the branch the step before it landed on (not merged yet)";
            }
            else if (chain == 0 && landed is not null)
            {
                start = grewFrom = landed;
                basedOn = $"`{landed}`, the branch its chain landed on (not merged yet) — `{from}`, the branch the step before it "
                    + "worked on, is gone";
            }
            else
            {
                basedOn += $" — `{from}`, the branch the step before it landed on, is gone";
            }
        }
        else if (partOf is not null)
        {
            // A further part whose session's tree went at its landing (SESSONE1d): the branch that work landed on, where it stands.
            var gone = $"the tree session `{partOf}` worked in is gone";
            var (stands, _, _) = landed is { Length: > 0 } && BranchName.IsValid(landed)
                ? await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{landed}"], ct).ConfigureAwait(false)
                : (1, "", "");
            if (stands == 0 && landed is { } standing)
            {
                start = grewFrom = standing;
                basedOn = $"`{standing}`, the branch its session's work landed on (not merged yet) — {gone}";
            }
            else
            {
                basedOn += landed is { Length: > 0 }
                    ? $" — {gone}, and `{landed}`, the branch its work landed on, is gone too"
                    : $" — {gone}, and no branch holds its work here";
            }
        }

        var name = $"s-{Guid.NewGuid().ToString("N")[..8]}";
        var branch = $"daoris/{name}";
        var path = Path.Combine(TreesRoot, workspace, repository, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // OPENCUT1: a folder that was there before the add is never this open's to delete.
        var ours = !Directory.Exists(path);

        string? unrecorded = null;
        var dependencies = DependencyCopy.None;
        try
        {
            // 🔴 Long paths (2026-09-27): a tree's prefix is longer than the root's, so a repository whose
            // deepest file fits under its root can cross Windows' 260 characters here. The first real
            // workspace did, and every tick's `worktree add` failed. `WorkingTree.GitAsync` says
            // `core.longpaths` on every call since, removal and status included (2026-09-28).
            // OPENCUT1: locked while it opens, the lock's reason naming this process, so a later look tells an open a kill cut
            // off from a person's lock (TreeStrays); let go of below, once where its branch started is recorded.
            var (code, _, stderr) = await WorkingTree.GitAsync(
                root, ["worktree", "add", "--lock", "--reason", TreeOpenings.LockReason(Environment.ProcessId), "-b", branch, path, start], ct)
                .ConfigureAwait(false);
            if (code != 0)
            {
                throw new DriverException(
                    $"could not open a session tree for `{repository}` — git said: {Failure(stderr)}");
            }

            // Where the branch started (WSR6), the moment it exists: bringing it up to date after the line moves cuts
            // here, so a chain's step replays only its own commits once the step before's are on the line.
            var (headCode, head, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
                .ConfigureAwait(false);
            if (headCode == 0)
            {
                try
                {
                    // The branch work unit's id, written once here (FABRIC-END2, design §D2); every later record of the branch keeps it.
                    Grown.Record(new GrownBranch(repository, workspace, branch, line.Branch, head.Trim(), grewFrom, DateTimeOffset.UtcNow)
                    {
                        Unit = GrownBranch.NewUnit(),
                    });
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    unrecorded = error.Message;
                }
            }

            // TREEDEPS1: the dependencies the checkout installed at this tree's lockfile, copied in while the tree is still locked as
            // opening, so a cut-off open's undo takes a half copy with the tree. Copied, never linked (D51's note).
            dependencies = await TreeDependencies.CopyAsync(root, path, ct).ConfigureAwait(false);

            var (unlocked, _, unlockErr) = await WorkingTree.GitAsync(root, ["worktree", "unlock", path], ct).ConfigureAwait(false);
            if (unlocked != 0)
            {
                throw new DriverException(
                    $"could not open a session tree for `{repository}` — git would not let go of the lock it opened it under: {Failure(unlockErr)}");
            }
        }
        catch
        {
            // 🔴 `worktree add -b` makes the branch before it checks anything out, so a failure used to leave one behind on
            // every tick: sixteen on the first real workspace. And a cancel as the app closed cut an open off inside the add,
            // leaving a half-checked-out tree, locked, and its branch (OPENCUT1, `s-0031408d`). Whatever cut it off, the branch
            // and the tree are this call's own, made a moment ago under a name nothing else uses and handed to nobody, so they
            // go, and the repository is left as it was found. A kill leaves them for the look (TreeStrays).
            await TreeOpenings.UndoAsync(root, path, branch, ours).ConfigureAwait(false);
            throw;
        }

        // FABRIC-END3: a tree opened is Daoris's own ref move; its repository is watched from here, and judged at once (§B4).
        RefMoves.Mine(_context.Home, repository, workspace, root);

        // What it grew from as a fact (LANG1a): the branch it started at, never the sentence spelling it for a person.
        var opening = OpeningOf(path, branch, basedOn, grewFrom ?? line.Branch ?? "HEAD", unrecorded, dependencies);
        return new(path, branch, basedOn, opening.Note, grewFrom) { Opening = opening, Dependencies = dependencies.Told };
    }

    /// <summary>
    /// The opening's sentence, price included, with its codes (LANG1a, the language design §4 rows 25–26): its branch and the
    /// branch it started at as facts, never its path, which only the English names and the cleaner cuts where it travels. Where
    /// its checkout's dependencies were copied in (TREEDEPS1), its own code says them; each project that copied none says why.
    /// </summary>
    /// <param name="basedOn">What it grew from, spelled for a person.</param>
    /// <param name="startedAt">The branch it started at, or <c>HEAD</c>: the fact the page says.</param>
    /// <param name="unrecorded">Why where its branch started was not recorded (WSR6), in the program's words; null where it was.</param>
    /// <param name="dependencies">What was copied of the checkout's dependencies, and why not the rest; none by default.</param>
    internal static Noted OpeningOf(
        string path, string branch, string basedOn, string startedAt, string? unrecorded, DependencyCopy? dependencies = null)
    {
        dependencies ??= DependencyCopy.None;
        // 🔴 A stored shape is never changed in place: `started.tree` still says no installed dependencies, as every opening
        // recorded before TREEDEPS1 did, and a tree its checkout's were copied into says it under a code of its own.
        var opening = dependencies.Copied.Count > 0
            ? Noted.Of(
                NoteCodes.StartedTreeCopied,
                $"opened a session tree at {path} on `{branch}`, from {basedOn} — a fresh tree holds what git tracks, and here the "
                + $"dependencies your checkout installed at the same lockfile, copied in: {dependencies.Listed}. `npm ci` is not "
                + "needed there unless a lockfile changes; the rest of the repository's own setup cost, build outputs included, is "
                + "paid here, and in exchange the root's uncommitted work holds nothing.",
                ("branch", branch), ("basedOn", startedAt), ("copied", (IReadOnlyList<string>)[.. dependencies.Copied]))
            : Noted.Of(
                NoteCodes.StartedTree,
                $"opened a session tree at {path} on `{branch}`, from {basedOn} — a fresh tree holds "
                + "nothing git does not track: no installed dependencies, no build outputs. The "
                + "repository's own setup cost is paid here, and in exchange the root's uncommitted work "
                + "holds nothing.",
                ("branch", branch), ("basedOn", startedAt));
        foreach (var each in dependencies.Declined) opening = opening.Then(" ", each.Line());
        if (unrecorded is null) return opening;

        var line = Noted.Of(
            NoteCodes.StartedTreeUnrecorded,
            $"Daoris could not record where its branch started ({unrecorded}), so bringing it up to date "
            + "will cut where its work first differs from the line.");
        return opening.Then(" ", unrecorded.Length == 0 ? line : line.Also(NotePart.Said(unrecorded, NoteBy.Program)));
    }

    /// <summary>Whether <paramref name="path"/> is a tree this home opened — the only kind a landing rule reaches.</summary>
    public bool Holds(string path) => _context.Holds(path);

    /// <summary>
    /// The review's gate for this tree (REVIEWENV1c, D154 point 7; <see cref="ReviewGate.ReadAsync"/>): the chain its quest is a step
    /// of, the review rule standing for the tree's repository here, and git's ancestry from the tree's <c>HEAD</c>. Every door that
    /// lands asks it and hands it to <see cref="LandAsync"/>; a conversation, with no quest, has nothing to wait for.
    /// </summary>
    public Task<ReviewGateState> ReviewAsync(string path, string? quest, IReviewWorld world, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        var (workspace, repository) = _context.OwnerOf(full);
        return ReviewGate.ReadAsync(world, _context.Config(), full, repository, workspace, quest, ct);
    }

    /// <summary>
    /// How many commits a session tree's HEAD holds beyond its repository's line (LAND2b): zero is a done that made no commits,
    /// which lands nothing; null is git unable to say, or no line it can name, and the landing then decides.
    /// </summary>
    public async Task<int?> AheadOfLineAsync(string tree, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(tree);
        if (!Holds(full) || !Directory.Exists(full)) return null;
        var (code, commonDir, _) = await WorkingTree.GitAsync(full, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)
            .ConfigureAwait(false);
        if (code != 0) return null;
        var root = Path.GetDirectoryName(commonDir.Trim())!;
        var (workspace, repository) = _context.OwnerOf(full);
        var line = (await _context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
        var against = line is null ? null : await ComparableAsync(root, line, ct).ConfigureAwait(false);
        if (against is null) return null;
        var (countCode, count, _) = await WorkingTree.GitAsync(full, ["rev-list", "--count", $"{against}..HEAD"], ct).ConfigureAwait(false);
        return countCode == 0 && int.TryParse(count.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var ahead) ? ahead : null;
    }

    /// <summary>A tree's workspace and repository, read from the layout this home chose (<c>trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>).</summary>
    public (string Workspace, string Repository) Owner(string tree) => _context.OwnerOf(Path.GetFullPath(tree));

    /// <summary>
    /// Every session tree on this machine, read from the layout itself — the directory is the
    /// registry, the same argument the credential profiles made (D49 §4). Then each folder the look moved aside
    /// (AUTOTIDY1, AUTOTIDY2c), with when, until the purge deletes it.
    /// </summary>
    public Task<IReadOnlyList<SessionTree>> ListAsync(CancellationToken ct = default) => ListAsync(null, ct);

    /// <summary>The session trees and folders moved aside of the repositories <paramref name="only"/> holds (<see cref="ListAsync(CancellationToken)"/>).</summary>
    /// <param name="only">
    /// Whether a repository's folder is listed, by its name, in every workspace (FACTSPLIT1a): another's is never entered, so its
    /// trees cost no git. Every repository's where null.
    /// </param>
    public async Task<IReadOnlyList<SessionTree>> ListAsync(Func<string, bool>? only, CancellationToken ct = default)
    {
        var found = new List<SessionTree>();
        IEnumerable<string> Repositories(string workspace) =>
            Directory.EnumerateDirectories(workspace).Where(folder => only?.Invoke(Path.GetFileName(folder)) ?? true);

        // Both places of the trees (TREEPLACE1), each as found. The folders the look moved aside are no trees (AUTOTIDY1): git no
        // longer knows them as such.
        foreach (var root in _context.TreesRoots.Where(Directory.Exists))
        foreach (var workspace in Directory.EnumerateDirectories(root).Where(folder => Path.GetFileName(folder) != TreeSetAside.SetAsideFolder))
        foreach (var repository in Repositories(workspace))
        foreach (var tree in Directory.EnumerateDirectories(repository))
        {
            // AUTOTIDY2c: a folder with no link to a repository is no tree, and git asked in it would answer for whatever
            // repository holds the home.
            var (code, branch, _) = Path.Exists(Path.Combine(tree, ".git"))
                ? await WorkingTree.GitAsync(tree, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false)
                : (1, SessionTree.NotATree, "");
            found.Add(new(
                tree,
                Path.GetFileName(workspace),
                Path.GetFileName(repository),
                code == 0 ? branch.Trim() : branch == SessionTree.NotATree ? branch : "(unreadable)"));
        }

        // KEEPSIZE1a: the folders moved aside at every place of the trees, the old place's included.
        foreach (var aside in SetAsides.SetAsideRoots.Where(Directory.Exists))
        foreach (var workspace in SetAsideEligibility.Workspaces(aside))
        foreach (var repository in Repositories(workspace))
        foreach (var folder in Directory.EnumerateDirectories(repository))
        {
            found.Add(new(folder, Path.GetFileName(workspace), Path.GetFileName(repository), SessionTree.SetAside)
            {
                MovedAside = TreeSetAside.MovedAt(Path.GetFileName(folder)),
                // KEEP1a: the package folders the slim dropped, from the manifest beside it, which the inventory's line names.
                Slimmed = SlimManifest.Read(SlimManifest.PathFor(folder)).Manifest?.Removed ?? [],
            });
        }

        return found;
    }

    /// <inheritdoc cref="TreeStrays.TidyAsync"/>
    public Task<IReadOnlyList<StrayResult>> TidyStraysAsync(
        string root, string repository, string? workspace, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse, DateTimeOffset now,
        CancellationToken ct = default) =>
        Strays.TidyAsync(root, repository, workspace, inUse, now, ct);

    /// <inheritdoc cref="SessionLocks.LetGoUnheldAsync"/>
    public Task<IReadOnlyList<string>> LetGoUnheldLocksAsync(
        string root, string repository, string? workspace, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse, CancellationToken ct = default)
    {
        var space = RemoteTarget.Workspace(workspace);
        return SessionLocks.LetGoUnheldAsync(_context, root, space, repository, inUse, ct);
    }

    /// <inheritdoc cref="PassLeftovers.ClearCopies"/>
    internal IReadOnlyList<string> ClearPassCopies(string trees, DateTimeOffset now, Func<int, bool>? running = null) =>
        Leftovers.ClearCopies(trees, now, running ?? TreeOpenings.Running);

    /// <summary>
    /// Where a tree on <paramref name="branch"/> grows from: the branch itself where this checkout has
    /// it, its copy on <c>origin</c> where only that exists, and a refusal for a line set to a branch
    /// the checkout has nowhere — a tree grown from the wrong point is invisible until merge time.
    /// </summary>
    private static async Task<string> StartOfAsync(string root, string branch, Line line, string repository, CancellationToken ct)
    {
        // STARTGIT1: the checkout's own guess is the branch either way (its answer is the same where it stands or not), so git is
        // not asked whether it does; a line the person set is the one checked.
        if (line.Source == LineSource.Checkout) return branch;
        var (local, _, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct).ConfigureAwait(false);
        if (local == 0) return branch;

        var (remote, _, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--verify", "--quiet", $"refs/remotes/origin/{branch}"], ct).ConfigureAwait(false);
        if (remote == 0) return $"origin/{branch}";

        throw new DriverException(
            $"{CanonicalLine.Describe(line)} is `{repository}`'s line, and its checkout has no such branch, here "
            + $"or on origin. Fetch it, or set another with `daoris driver line {repository} <branch>`.");
    }

    // The callers' calls, one line each (PART10j–l): each part's doc is on its class.

    /// <inheritdoc cref="TreeLands.MergeAsync"/>
    public Task<TreeMerge> MergeAsync(string path, CancellationToken ct = default) => Lands.MergeAsync(path, ct);

    /// <inheritdoc cref="TreeLands.PlanAsync"/>
    public Task<LandingPlan> PlanAsync(string path, LandingSubject subject, CancellationToken ct = default, WorkflowProcess? process = null) =>
        Lands.PlanAsync(path, subject, ct, process);

    /// <inheritdoc cref="TreeLands.PlanRuleAsync"/>
    internal Task<LandingPlan> PlanRuleAsync(string path, LandingSubject subject, CancellationToken ct, WorkflowProcess? process) =>
        Lands.PlanRuleAsync(path, subject, ct, process);

    /// <inheritdoc cref="TreeLands.LandAsync"/>
    public Task<TreeLanding> LandAsync(
        string path, LandingSubject subject, CancellationToken ct = default, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null,
        string acceptedBy = AcceptedBy.Person, LandingGate? gate = null, bool joins = false,
        Func<bool, CancellationToken, Task<TreeLanding?>>? judged = null, Func<LandedBranch?, bool, CancellationToken, Task<string?>>? beforePush = null,
        int? uncommitted = null) =>
        Lands.LandAsync(path, subject, ct, inUse, acceptedBy, gate, joins, judged: judged, beforePush: beforePush, uncommitted: uncommitted);

    /// <inheritdoc cref="EntryLands.JoinAsync"/>
    public Task<LandingSubject> JoinEntryAsync(
        string path, string session, LandingSubject entry, CancellationToken ct = default, WorkflowProcess? process = null) =>
        Entries.JoinAsync(path, session, entry, ct, process);

    /// <summary>
    /// What one session's landing is named for, at every door that lands one session (ACCEPTALL1b, ACCEPTALL1f; D167's 2026-10-15
    /// amendment point 2): the review's <c>LANDING</c> and <c>LAND_SESSION_TREE</c>, the terminal's <c>trees land</c> and the look's
    /// landing at done. A part of an entry with others (<see cref="EntryAccepts.EntryLead"/>) is its entry's Accept for that part alone:
    /// named for the entry's first part (its chain's first quest), its own session recorded, on the entry's branch
    /// (<see cref="JoinEntryAsync"/>), which <see cref="LandingSubject.Joins"/> then lands on. Otherwise the session's own subject, the
    /// chain's first quest (WSR5), as before. A merge rule makes no branch to join; a TREEFOLLOW1 tree still lands as its own branch, and
    /// is named for its own quest and title (TREEFOLLOWNAME1).
    /// </summary>
    /// <param name="quest">The session's own quest, null for a conversation.</param>
    /// <param name="records">The service's session records, which say the session's entry and its parts.</param>
    /// <param name="find">A quest by id, as the service holds it, for the chain's walk.</param>
    /// <param name="opening">A session's opening words, which name a conversation with no quest.</param>
    /// <param name="process">The run's process the door's gate read (WORKFLOW1f), whose landing step names the entry's branch.</param>
    public async Task<LandingSubject> OwnSubjectAsync(
        string path, string session, string? quest, IReadOnlyList<SessionRecord> records, Func<string, Task<QuestView?>> find,
        Func<string, string?> opening, WorkflowProcess? process, CancellationToken ct = default)
    {
        var lead = EntryAccepts.EntryLead(records, session);
        var named = lead?.Id ?? session;
        var subject = await LandingRules.SubjectAsync(named, lead is null ? quest : lead.Quest, find, opening(named)).ConfigureAwait(false);
        // FABRIC-UNITLINK1, FABRIC-CLOSE1h13: whatever it is named for, the quest this session served and the submission its landing is
        // made on, as the door's own find read it (each door's find reads a quest once), which the landing's record keeps.
        var served = quest is null ? null : quest.TrimStart('#');
        var submission = served is null ? null : (await find(served).ConfigureAwait(false))?.Submission is { } made
            ? new QuestSubmissionRef(made.Machine, made.Sequence)
            : null;
        if (lead is null) return subject with { Served = served, Submission = submission };
        // ACCEPTFOOT2: a conversation's entry is named for its first part's work, not for the part pressed here.
        subject = await Lands.EntryForWorkAsync(subject, lead.Tree, path, ct).ConfigureAwait(false);
        var joined = await JoinEntryAsync(path, session, subject, ct, process).ConfigureAwait(false) with { Served = served, Submission = submission };
        // TREEFOLLOWNAME1: a tree on a branch of its own lands as that branch, so it is named for its own quest, not its entry's first part's.
        if (joined.EntryBranch is null || !await Entries.FollowsOwnAsync(path, ct).ConfigureAwait(false)) return joined;
        return joined.NamedFor(await LandingRules.SubjectAsync(session, quest, find, opening(session)).ConfigureAwait(false));
    }

    /// <inheritdoc cref="TreeLands.EntryForWorkAsync"/>
    public Task<LandingSubject> EntryForWorkAsync(LandingSubject entry, string? leadTree, string anyTree, CancellationToken ct = default) =>
        Lands.EntryForWorkAsync(entry, leadTree, anyTree, ct);

    /// <inheritdoc cref="EntryLands.PlanAsync"/>
    public Task<EntryPlan> PlanEntryAsync(IReadOnlyList<EntryLandingPart> parts, LandingSubject subject, CancellationToken ct = default) =>
        Entries.PlanAsync(parts, subject, ct);

    /// <inheritdoc cref="EntryLands.LandAsync"/>
    public Task<EntryLanding> LandEntryAsync(
        IReadOnlyList<EntryLandingPart> parts, LandingSubject subject, IReadOnlySet<string> leaveOut, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null) =>
        Entries.LandAsync(parts, subject, leaveOut, ct, inUse);

    /// <inheritdoc cref="TreeReturns.EnsureAsync"/>
    public Task<TreeStanding> EnsureAsync(string root, string tree, CancellationToken ct = default) => Returns.EnsureAsync(root, tree, ct);

    /// <inheritdoc cref="TreeRemovals.RemoveAsync"/>
    public Task<TreeRemoval> RemoveAsync(string path, bool force = false, CancellationToken ct = default, ServiceClient? service = null) =>
        Removals.RemoveAsync(path, force, ct, service);

    /// <inheritdoc cref="TreeTidies.RemoveBranchAsync"/>
    public Task<TreeRemoval> RemoveBranchAsync(string root, string repository, string branch, bool force = false, CancellationToken ct = default,
        ServiceClient? service = null) => Tidies.RemoveBranchAsync(root, repository, branch, force, ct, service);

    /// <inheritdoc cref="TreeSweeps.SweepPlanAsync"/>
    public Task<IReadOnlyList<SweepItem>> SweepPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default, IReadOnlySet<string>? only = null) =>
        Sweeps.SweepPlanAsync(repositories, inUse, ct, only);

    /// <summary>
    /// The clean-up (D88): every session branch the proof clears goes, with its tree, and the rest are
    /// kept and named. 🔴 Each is judged again right before it goes — a list is a fact about a moment.
    /// </summary>
    /// <param name="only">The branches the person saw listed to go, as <c>repository:branch</c>; null for every one the proof clears now.</param>
    public Task<IReadOnlyList<SweepResult>> SweepAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default) =>
        Sweeps.SweepAsync(repositories, inUse, only, ct);

    /// <inheritdoc cref="TreeSetAside.GoesByItself"/>
    public bool GoesByItself(SweepItem item) => SetAsides.GoesByItself(item);

    /// <inheritdoc cref="AutoTidies.TidyEmptyAsync"/>
    public async Task<TidyPass> TidyEmptyAsync(
        string root, string repository, string? workspace, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse,
        CancellationToken ct = default)
    {
        var pass = await AutoTidy.TidyEmptyAsync(root, repository, workspace, inUse, ct).ConfigureAwait(false);
        // FABRIC-END3: the tidy's own removals are Daoris's ref moves, judged at once by the ref watch with their echo (§B4, canary 11).
        if (pass.Results.Any(result => result.Removed) || pass.Landed.Any(result => result.Removed)) RefMoves.Mine(_context.Home, repository);
        return pass;
    }

    /// <summary>
    /// One repository's end judgement, its line read here as the look's tidy reads it (FABRIC-END3, link 1 of the design §C6): what the
    /// ref watch's hint asks. How many facts were written; nothing where this process does not hold the home's lease.
    /// </summary>
    /// <param name="root">The repository's main checkout, whose <c>.git</c> folder is its common dir.</param>
    internal async Task<int> KeepEndsAsync(string root, string repository, string? workspace, CancellationToken ct = default)
    {
        if (!HomeLease.HeldHere(_context.Home)) return 0;
        using var look = _context.OneLook();
        var space = RemoteTarget.Workspace(workspace);
        var line = (await _context.LineAsync(root, repository, space, ct).ConfigureAwait(false)).Branch;
        return await Ends.KeepAsync(root, repository, space, line, ct).ConfigureAwait(false);
    }

    /// <inheritdoc cref="TreeSetAside.SetAsideRoot"/>
    public string SetAsideRoot => SetAsides.SetAsideRoot;

    /// <inheritdoc cref="TreeSetAside.SetAsideRoots"/>
    internal IReadOnlyList<string> SetAsideRoots => SetAsides.SetAsideRoots;

    /// <inheritdoc cref="TreeSetAside.PurgeSetAside(DateTimeOffset, TimeSpan)"/>
    internal IReadOnlyList<SetAside> PurgeSetAside(DateTimeOffset now, TimeSpan after) => SetAsides.PurgeSetAside(now, after);

    /// <inheritdoc cref="FinishedRecords.RunAsync"/>
    internal Task<IReadOnlyList<FinishedStep>> RecordSetAsideAsync(
        Func<string, string, string?> rootOf, Func<FinishedPost, CancellationToken, Task<string?>>? post, DateTimeOffset now, CancellationToken ct,
        Func<string, bool>? only = null) =>
        new FinishedRecords(SetAsides).RunAsync(rootOf, post, now, ct, only);

    /// <summary>
    /// The registry's checkouts, by workspace and repository, for the record pass's git reads outside every hold (the keep-size design §3.4):
    /// a repository it does not name is unresolved, and its folder waits its days.
    /// </summary>
    internal static Func<string, string, string?> RootsOf(IReadOnlyList<RepoView> registry) =>
        (workspace, repository) => registry.FirstOrDefault(row =>
            string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(RemoteTarget.Workspace(row.Workspace), workspace, StringComparison.OrdinalIgnoreCase))?.Root;

    /// <inheritdoc cref="SetAsideKeeping.Run"/>
    internal SetAsideRun KeepSetAside(
        KeepPolicy policy, DateTimeOffset now, TimeSpan budget, CancellationToken ct, Func<string, bool>? purge = null, Func<string, bool>? slim = null) =>
        Keeping.Run(policy, now, budget, ct, purge, slim);

    /// <inheritdoc cref="SetAsideKeeping.List"/>
    internal IReadOnlyList<SetAsideListed> ListSetAside(KeepPolicy policy, DateTimeOffset now) => Keeping.List(policy, now);

    /// <inheritdoc cref="SetAsideKeeping.RemoveEarly"/>
    internal (long Bytes, string? Kept) RemoveSetAsideEarly(string folder, bool listedOwn) => Keeping.RemoveEarly(folder, listedOwn);

    /// <inheritdoc cref="TreeSetAside.LastLookAsync"/>
    internal Task<(string Code, string Sentence, string? Cause)?> LastLookAsync(
        string root, SweepItem item, CancellationToken ct, IReadOnlyList<string>? judged = null, List<string>? confirmed = null) =>
        SetAsides.LastLookAsync(root, item, ct, judged, confirmed);

    /// <inheritdoc cref="TreeSetAside.Kept"/>
    internal (string Code, string Sentence)? SetAsideKept(string folder, DateTimeOffset moved) => SetAsides.Kept(folder, moved);

    /// <inheritdoc cref="TreeSetAside.Failed"/>
    internal IReadOnlyDictionary<string, string> SetAsideFailed() => SetAsides.Failed();

    /// <inheritdoc cref="LandedCleanups.CleanPlanAsync"/>
    public Task<SweepPlan> CleanPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default) =>
        Cleanups.CleanPlanAsync(repositories, inUse, ct);

    /// <inheritdoc cref="LandedCleanups.SweepPartAsync"/>
    public Task<SweepPlan> SweepPartAsync(
        string repository, string? workspace, string root, IReadOnlySet<string> inUse, CancellationToken ct = default) =>
        Cleanups.SweepPartAsync(repository, workspace, root, inUse, ct);

    /// <inheritdoc cref="LandedCleanups.CleanAsync"/>
    public Task<SweepDone> CleanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default) =>
        Cleanups.CleanAsync(repositories, inUse, only, ct);

    /// <inheritdoc cref="TreeSync.SyncPlanAsync"/>
    public Task<SyncPlan> SyncPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        bool fetch = true, CancellationToken ct = default, SyncScope? scope = null) =>
        Sync.SyncPlanAsync(repositories, inUse, fetch, ct, scope);

    /// <inheritdoc cref="TreeSync.SyncScopeAsync"/>
    public Task<IReadOnlyList<SyncRepository>> SyncScopeAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, CancellationToken ct = default) =>
        Sync.SyncScopeAsync(repositories, ct);

    /// <inheritdoc cref="TreeSync.SyncAsync"/>
    public Task<SyncDone> SyncAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, bool fetch = false, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUseNow = null, SyncScope? scope = null) =>
        Sync.SyncAsync(repositories, inUse, only, fetch, ct, inUseNow, scope);

    /// <inheritdoc cref="ReviewRanges.ReviewBaseAsync"/>
    public Task<string> ReviewBaseAsync(string tree, string recorded, CancellationToken ct = default) =>
        Ranges.ReviewBaseAsync(tree, recorded, ct);

    /// <inheritdoc cref="ReviewRanges.ReviewRangeAsync"/>
    public Task<ReviewRange> ReviewRangeAsync(string tree, string recorded, CancellationToken ct = default) =>
        Ranges.ReviewRangeAsync(tree, recorded, ct);

    /// <inheritdoc cref="TreeUpdates.UpdateTreeAsync"/>
    public Task<TreeUpdate> UpdateTreeAsync(string tree, Func<CancellationToken, Task<bool>> running, CancellationToken ct = default) =>
        Updates.UpdateTreeAsync(tree, running, ct);

    /// <inheritdoc cref="TreeUpdates.StartInSyncAsync"/>
    public Task<TreeStartSync> StartInSyncAsync(string tree, CancellationToken ct = default) => Updates.StartInSyncAsync(tree, ct);

    /// <inheritdoc cref="TreeUpdates.GoOnInSyncAsync"/>
    public Task<TreeStartSync> GoOnInSyncAsync(
        string tree, string source, string? commit, CancellationToken ct = default, bool broughtForward = false) =>
        Updates.GoOnInSyncAsync(tree, source, commit, ct, broughtForward);

    /// <inheritdoc cref="LandedReviews.LandedReviewAsync"/>
    public Task<LandedReview> LandedReviewAsync(string? root, LandedBranch entry, bool changes = true, CancellationToken ct = default) =>
        Reviews.LandedReviewAsync(root, entry, changes, ct);

    /// <inheritdoc cref="TreeGates.GateAsync"/>
    public Task<LandingGate> GateAsync(
        string path, string? quest, IOpinionWorld world, string? session = null, CancellationToken ct = default, WorkflowProcess? process = null,
        string? tip = null) =>
        Gates.GateAsync(path, quest, world, session, ct, process, tip);

    /// <inheritdoc cref="TreeGates.ProcessAsync"/>
    public Task<WorkflowProcess> ProcessAsync(
        DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct = default) =>
        Gates.ProcessAsync(config, repository, workspace, quest, world, ct);

    /// <inheritdoc cref="TreeGates.WorkflowAsync"/>
    public Task<WorkflowGateState> WorkflowAsync(string tree, WorkflowProcess process, string? session, CancellationToken ct = default) =>
        Gates.WorkflowAsync(tree, process, session, ct);

    /// <inheritdoc cref="TreeGates.OpinionAsync"/>
    public Task<OpinionGateState> OpinionAsync(
        string tree, string repository, string workspace, string? quest, string? session, IOpinionWorld world, ReviewGateState? review,
        string occasion, CancellationToken ct = default, WorkflowProcess? process = null) =>
        Gates.OpinionAsync(tree, repository, workspace, quest, session, world, review, occasion, ct, process);

    /// <inheritdoc cref="TreeGates.ForkPointAsync"/>
    public Task<string?> ForkPointAsync(string tree, CancellationToken ct = default) => Gates.ForkPointAsync(tree, ct);

    /// <inheritdoc cref="OnlyHereProof.OnlyHereAsync"/>
    public Task<TreeOnlyHere> OnlyHereAsync(string tree, string branch, string? baseCommit, string? root, CancellationToken ct = default) =>
        OnlyHere.OnlyHereAsync(tree, branch, baseCommit, root, ct);

    /// <inheritdoc cref="OnlyHereProof.DiscardAsync"/>
    public Task<TreeRemoval> DiscardAsync(string tree, string branch, TreeOnlyHere judged, CancellationToken ct = default) =>
        OnlyHere.DiscardAsync(tree, branch, judged, ct);

    /// <inheritdoc cref="TreeWorkReads.WorkAsync"/>
    public Task<TreeWork?> WorkAsync(string tree, CancellationToken ct = default) => WorkReads.WorkAsync(tree, ct);

    /// <inheritdoc cref="TreeWorkReads.UncommittedAsync"/>
    public Task<int?> UncommittedAsync(string tree, CancellationToken ct = default) => WorkReads.UncommittedAsync(tree, ct);

    /// <summary>
    /// One <c>git status</c> of a tree a kept read proved this home's linked tree (TREELOOK1, <see cref="TreeReadPass"/>), counted;
    /// null where git cannot say. Nothing else is asked, so a caller that has not proved it asks <see cref="UncommittedAsync"/>.
    /// </summary>
    internal Task<int?> StatusCountAsync(string tree, CancellationToken ct = default) => TreeWorkReads.UncommittedOfAsync(tree, ct);

    /// <summary>The landings record a tree's walk reads (TREELOOK1's stamp).</summary>
    internal string LandingsPath => _context.Recorded.FilePath;

    /// <summary>The choices a tree's walk reads its line from (TREELOOK1's stamp).</summary>
    internal string ConfigPath => DriverConfig.ResolvePath(_context.Variable, _context.Home);

    /// <inheritdoc cref="TreeWorkReads.DiscardOfferAsync"/>
    public Task<DiscardOffer?> DiscardOfferAsync(string tree, CancellationToken ct = default) => WorkReads.DiscardOfferAsync(tree, ct);

    /// <inheritdoc cref="TreeFollows.BranchNowAsync"/>
    public Task<string?> BranchNowAsync(string tree, CancellationToken ct = default) => Follows.BranchNowAsync(tree, ct);

    /// <inheritdoc cref="TreeFollows.FindBranches"/>
    public IReadOnlyList<GrownBranch> FindBranches(string named, string? repository) => Follows.FindBranches(named, repository);

    /// <inheritdoc cref="TreeFollows.BranchOfTree"/>
    public (string Workspace, string Repository, string Branch)? BranchOfTree(string tree) => Follows.BranchOfTree(tree);

    /// <inheritdoc cref="TreeSquashes.UnlandedHereAsync"/>
    public Task<int?> UnlandedHereAsync(string tree, CancellationToken ct = default) => Squashes.UnlandedHereAsync(tree, ct);

    /// <inheritdoc cref="PullRequestAsks.AskAgainAsync"/>
    public Task<PullRequestAskedAgain> AskAgainAsync(string root, LandedBranch entry, CancellationToken ct = default) =>
        Asks.AskAgainAsync(root, entry, ct);

    /// <inheritdoc cref="PullRequestAsks.AskAtMomentAsync"/>
    internal Task<int> AskAtMomentAsync(
        string root, string repository, string? workspace, IReadOnlyCollection<LandedBranch> entries, CancellationToken ct = default) =>
        Asks.AskAtMomentAsync(root, repository, RemoteTarget.Workspace(workspace), entries, ct);

    /// <summary>A repository's line as the asks and the tidy read it (FABRIC-END6): the person's choice, else git's guess; null where none.</summary>
    internal async Task<string?> LineOfAsync(string root, string repository, string? workspace, CancellationToken ct = default) =>
        (await _context.LineAsync(root, repository, RemoteTarget.Workspace(workspace), ct).ConfigureAwait(false)).Branch;

    /// <inheritdoc cref="TreeHandOffs.HandPlanAsync"/>
    public Task<HandPlan> HandPlanAsync(string root, LandedBranch entry, string? plugin = null, CancellationToken ct = default) =>
        HandOffs.HandPlanAsync(root, entry, plugin, ct);

    /// <inheritdoc cref="TreeHandOffs.HandAsync"/>
    public Task<TreeHand> HandAsync(
        string root, LandedBranch entry, string? plugin = null, CancellationToken ct = default,
        Func<LandedBranch?, bool, CancellationToken, Task<string?>>? beforePush = null) =>
        HandOffs.HandAsync(root, entry, plugin, ct, beforePush);
}
