using System.Globalization;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// The worktrees: a repository may have more than one working tree, and the extra ones are
/// Daoris's to open and account for — <c>&lt;home&gt;/trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>,
/// each a real linked worktree of the registered root on a branch of its own.
/// </summary>
/// <remarks>
/// <para><b>Daoris owns the location; git owns the contents</b> — the same arrangement as
/// a credential profile, and for the same reason: a directory Daoris created is one it may
/// reason about, and one scattered beside somebody's checkout is not. That is also why
/// <see cref="RemoveAsync"/> refuses anything outside the trees home outright, whatever git would say.</para>
///
/// <para><b>Nothing merges itself and nothing deletes itself</b>. Opening never touches
/// the root's own state; removal refuses while the tree holds uncommitted changes or commits the
/// canonical line has not taken, naming what would be lost — and the person may say it again with
/// force, meaning it. Commits whose work a squash merge or a cherry-pick holds by content lose nothing,
/// and go unforced saying where that work is, the commits themselves kept on a recovery
/// ref the sentence names. A branch goes only while it is the commit judged, and a tree only while
/// nothing in it is uncommitted or ignored and its alone.</para>
///
/// <para><b>The name is minted here, not borrowed from the session id</b>, because the order is
/// tree → open → spawn: the ledger's open needs the tree path for the lock, so the id it would borrow
/// does not exist yet. Short and random, like the id itself; the record ties the two together.</para>
///
/// <para><b>One type, a front over classes</b>: the opening, the listing and what a tree is stay here;
/// every other part is a class over the shared <see cref="TreeContext"/>, built once below with the classes it takes in its
/// constructor, and this keeps its callers' one-line calls.</para>
/// </remarks>
/// <param name="plugins">
/// The plugin wire a branch rule's landing speaks — the home's own plugins by default; the
/// shell and the terminal hand one that says the plugin's lines where each says things.
/// </param>
public sealed class SessionTrees(string home, IWorkplacePluginWire? plugins = null) : IWorkTrees
{
    /// <summary>What the parts share: the home, the plugin wire, the seams.</summary>
    private readonly TreeContext _context = new(home, plugins ?? WorkplacePlugins.Current.Wire(home));

    /// <summary>The shared context, for Review's graph, which reads what <see cref="ReviewRanges"/> and <see cref="LandedReviews"/> read.</summary>
    internal TreeContext Context => _context;

    // The parts, each built once at first use (a field initializer cannot read `_context`), leaves first; a class takes the
    // others it reads in its constructor, and none takes this front.
    private TreeFollows? _follows;
    private ContentHolds? _content;
    private TreeSquashes? _squashes;
    private PullRequestAsks? _asks;
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
    private LandedWithdrawals? _withdrawals;
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

    /// <summary>The end judgement's one writer: where the look's tidy and <i>Accept</i>'s tidy keep what the line holds.</summary>
    internal EndKeeping Ends => _keeping ??= new(_context, Asks);

    /// <summary>Where each session tree's work is, and the branch the layout names for it.</summary>
    private TreeFollows Follows => _follows ??= new(_context);

    /// <summary>Where a branch's work is held by content, and the delete at the commit judged.</summary>
    private ContentHolds Content => _content ??= new(_context);

    /// <summary>The squash's commit and the record's word of what it squashed.</summary>
    private TreeSquashes Squashes => _squashes ??= new(_context);

    /// <summary>The occasions' asks, <i>Ask again</i> and what a kept answer proves.</summary>
    private PullRequestAsks Asks => _asks ??= new(_context);


    /// <summary>A judged branch replayed, and the grown record's tidying.</summary>
    private BranchReplays Replays => _replays ??= new(_context, Leftovers);

    internal ReplaySettles Settles => Replays.Settles;

    /// <summary>Where a session tree's review is measured from.</summary>
    private ReviewRanges Ranges => _ranges ??= new(_context);

    /// <summary>A landed session's review.</summary>
    private LandedReviews Reviews => _reviews ??= new(_context);

    /// <summary>The landing gate's parts for a tree.</summary>
    internal TreeGates Gates => _gates ??= new(_context);

    /// <summary>What the look's own tidy takes by itself, its last guards, and its set-aside.</summary>
    private TreeSetAside SetAsides => _setAside ??= new(_context);

    /// <summary>The set-aside kind's removal, the purge and the slim, which the Keeping pass runs.</summary>
    private SetAsideKeeping Keeping => _setAsideKeeping ??= new(_context, SetAsides);

    /// <summary>A session tree's removal.</summary>
    private TreeRemovals Removals => _removals ??= new(_context, Follows, Content, Squashes);

    /// <summary>The session branches' half of the clean-up, for the person's press and the look's own.</summary>
    private TreeSweeps Sweeps => _sweeps ??= new(_context, Asks, Content, Follows, Squashes, SetAsides);

    /// <summary>The look's own tidy of one repository, the landings' branches with it.</summary>
    private AutoTidies AutoTidy => _autoTidy ??= new(_context, Follows, Replays, Sweeps, SetAsides, Cleanups, Ends);

    /// <summary>The look's own tidy of the folders under a repository's trees that are no tree of its own.</summary>
    private TreeStrays Strays => _strays ??= new(_context, SetAsides, Content);

    /// <summary>What a killed opinion's or replay's pass left under a repository's trees, moved aside by the next.</summary>
    private PassLeftovers Leftovers => _leftovers ??= new(_context, SetAsides);

    /// <summary>The clean-up's list and press, and each landed branch judged.</summary>
    private LandedCleanups Cleanups => _cleanups ??= new(_context, Asks, Content, Replays, Sweeps, Ends);

    /// <summary>A landing's branch the line will never hold, withdrawn on the person's word.</summary>
    private LandedWithdrawals Withdrawals => _withdrawals ??= new(_context, Content, Cleanups);

    /// <summary>Each branch judged for the replay.</summary>
    private BranchJudging Judging => _judging ??= new(_context, Cleanups);

    /// <summary>The look, the press and the scope of bringing up to date.</summary>
    private TreeSync Sync => _sync ??= new(_context, Asks, Cleanups, Judging, Replays);

    /// <summary>The press that brings one tree up to date.</summary>
    private TreeUpdates Updates => _updates ??= new(_context, Follows, Judging, Replays, Ranges);

    /// <summary>What a tree holds that nobody has accepted.</summary>
    private TreeWorkReads WorkReads => _work ??= new(_context, Squashes, Content, Follows);

    /// <summary>Whether a tree's work is anywhere but Daoris's own branches, and its discard.</summary>
    private OnlyHereProof OnlyHere => _onlyHere ??= new(_context, Removals);

    /// <summary>A landing's tidy of the other session branches its work holds, and the person's door for one.</summary>
    private TreeTidies Tidies => _tidies ??= new(_context, Content, Squashes, Removals);

    /// <summary>A landing in sync with its line: the fetch, the replay of what the line lacks, the tree's follow.</summary>
    private TreeLandSyncs Syncs => _syncs ??= new(_context, Replays);

    /// <summary>The landing: its plan, the merge, the branch form and its advance, and the rule's tidy.</summary>
    private TreeLands Lands => _lands ??= new(_context, Follows, WorkReads, Syncs);

    /// <summary>An entry's Accept: its parts' work on one branch, in ledger order.</summary>
    private EntryLands Entries => _entries ??= new(_context, Lands, Syncs);

    /// <summary>A session's tree asked for before it goes on, and made again where its folder is gone.</summary>
    private TreeReturns Returns => _returns ??= new(_context, Follows, SetAsides);

    /// <summary>
    /// The test seam: run with the branch's name between a judgement and the delete it cleared, where a test
    /// moves the branch as a session or a person could. Production sets none.
    /// </summary>
    internal Func<string, Task>? BeforeDeleting { get => _context.BeforeDeleting; init => _context.BeforeDeleting = value; }

    /// <inheritdoc cref="TreeContext.BeforeRefDeleted"/>
    internal Func<string, Task>? BeforeRefDeleted { get => _context.BeforeRefDeleted; init => _context.BeforeRefDeleted = value; }

    /// <summary>The test seam: true for a checkout whose working trees git could not list. Production sets none.</summary>
    internal Func<string, bool>? ListFails { get => _context.ListFails; init => _context.ListFails = value; }

    /// <inheritdoc cref="TreeContext.Clock"/>
    internal Func<DateTimeOffset> Clock { get => _context.Clock; init => _context.Clock = value; }

    /// <summary>The test seam: git's guess at a checkout's line, which a test counts. Production asks git.</summary>
    internal Func<string, CancellationToken, Task<string?>> CheckoutLine { get => _context.CheckoutLine; init => _context.CheckoutLine = value; }

    /// <summary>The branches this machine's landings made — the only ones the clean-up and the hand-off act on.</summary>
    public LandedBranches Recorded => _context.Recorded;

    /// <summary>Where this machine's session branches started — what bringing one up to date cuts at.</summary>
    public SessionBranches Grown => _context.Grown;

    /// <summary>Every tree this machine's Daoris has opened lives under here, and only here.</summary>
    public string TreesRoot => _context.TreesRoot;

    /// <inheritdoc cref="TreeContext.OneLook"/>
    internal TreeLook OneLook() => _context.OneLook();

    private TreeOpening? _opening;
    private TreeListings? _listings;
    private TreeOpening Opening => _opening ??= new(_context);
    private TreeListings Listings => _listings ??= new(_context, SetAsides);

    /// <inheritdoc cref="TreeOpening.OpenAsync"/>
    public Task<TreeOpened> OpenAsync(
        string root, string repository, string workspace, CancellationToken ct = default, string? from = null, string? landed = null,
        string? partOf = null) => Opening.OpenAsync(root, repository, workspace, ct, from, landed, partOf);

    /// <inheritdoc cref="TreeListings.AheadOfLineAsync"/>
    public Task<int?> AheadOfLineAsync(string tree, CancellationToken ct = default) => Listings.AheadOfLineAsync(tree, ct);

    /// <inheritdoc cref="TreeListings.LineHoldsAsync"/>
    public Task<bool?> LineHoldsAsync(string root, string repository, string workspace, string commit, CancellationToken ct = default) =>
        Listings.LineHoldsAsync(root, repository, workspace, commit, ct);

    /// <inheritdoc cref="TreeListings.ListAsync"/>
    public Task<IReadOnlyList<SessionTree>> ListAsync(Func<string, bool>? only, CancellationToken ct = default) => Listings.ListAsync(only, ct);

    /// <summary>Whether <paramref name="path"/> is a tree this home opened — the only kind a landing rule reaches.</summary>
    public bool Holds(string path) => _context.Holds(path);

    /// <summary>A tree's workspace and repository, read from the layout this home chose (<c>trees/&lt;workspace&gt;/&lt;repository&gt;/&lt;name&gt;</c>).</summary>
    public (string Workspace, string Repository) Owner(string tree) => _context.OwnerOf(Path.GetFullPath(tree));

    /// <summary>
    /// Every session tree on this machine, read from the layout itself — the directory is the
    /// registry, the same argument the credential profiles made. Then each folder the look moved aside,
    /// with when, until the purge deletes it.
    /// </summary>
    public Task<IReadOnlyList<SessionTree>> ListAsync(CancellationToken ct = default) => ListAsync(null, ct);

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

    /// <inheritdoc cref="TreeLands.MergeAsync"/>
    public Task<TreeMerge> MergeAsync(string path, CancellationToken ct = default, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null) =>
        Lands.MergeAsync(path, ct, beside: beside);

    /// <inheritdoc cref="TreeLands.PlanAsync"/>
    public Task<LandingPlan> PlanAsync(string path, LandingSubject subject, CancellationToken ct = default, IWorkplaceProcess? process = null) =>
        Lands.PlanAsync(path, subject, ct, process);

    /// <inheritdoc cref="TreeLands.PlanRuleAsync"/>
    internal Task<LandingPlan> PlanRuleAsync(string path, LandingSubject subject, CancellationToken ct, IWorkplaceProcess? process) =>
        Lands.PlanRuleAsync(path, subject, ct, process);

    /// <inheritdoc cref="TreeLands.LandAsync"/>
    public Task<TreeLanding> LandAsync(
        string path, LandingSubject subject, CancellationToken ct = default, Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null,
        string acceptedBy = AcceptedBy.Person, IWorkplaceGate? gate = null, bool joins = false,
        Func<bool, CancellationToken, Task<TreeLanding?>>? judged = null,
        int? uncommitted = null, string? together = null, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null,
        TreeDelivery? delivery = null) =>
        Lands.LandAsync(path, subject, ct, inUse, acceptedBy, gate, joins, judged: judged, uncommitted: uncommitted,
            together: together, beside: beside, delivery: delivery);

    /// <inheritdoc cref="EntryLands.JoinAsync"/>
    public Task<LandingSubject> JoinEntryAsync(
        string path, string session, LandingSubject entry, CancellationToken ct = default, IWorkplaceProcess? process = null) =>
        Entries.JoinAsync(path, session, entry, ct, process);

    /// <summary>
    /// What one session's landing is named for, at every door that lands one session: the review's <c>LANDING</c> plan and a run's <c>land</c>, the terminal's <c>trees land --plan</c> and the look's
    /// landing at done. A part of an entry with others of its run (<see cref="RunParts.LeadOfAsync"/>) is its entry's Accept for that part alone:
    /// named for the run's first part (its chain's first quest), its own session recorded, on the entry's branch
    /// (<see cref="JoinEntryAsync"/>), which <see cref="LandingSubject.Joins"/> then lands on. Otherwise the session's own subject, the
    /// chain's first quest, as before. A merge rule makes no branch to join; a tree its agent moved onto a branch of its own still lands as that branch, and
    /// is named for its own quest and title.
    /// </summary>
    /// <param name="quest">The session's own quest, null for a conversation.</param>
    /// <param name="records">The service's session records, which say the session's entry and its parts.</param>
    /// <param name="find">A quest by id, as the service holds it, for the chain's walk.</param>
    /// <param name="opening">A session's opening words, which name a conversation with no quest.</param>
    /// <param name="process">The run's process the door's gate read, whose landing step names the entry's branch.</param>
    public async Task<LandingSubject> OwnSubjectAsync(
        string path, string session, string? quest, IReadOnlyList<SessionRecord> records, Func<string, Task<QuestView?>> find,
        Func<string, string?> opening, IWorkplaceProcess? process, CancellationToken ct = default)
    {
        var lead = await RunParts.LeadOfAsync(records, session, find).ConfigureAwait(false);
        var named = lead?.Id ?? session;
        var subject = await BranchPatterns.SubjectAsync(named, lead is null ? quest : lead.Quest, find, opening(named)).ConfigureAwait(false);
        // Whatever it is named for, the quest this session served and the submission its landing is
        // made on, as the door's own find read it (each door's find reads a quest once), which the landing's record keeps.
        var served = quest is null ? null : quest.TrimStart('#');
        var submission = served is null ? null : (await find(served).ConfigureAwait(false))?.Submission is { } made
            ? new QuestSubmissionRef(made.Machine, made.Sequence)
            : null;
        if (lead is null) return subject with { Served = served, Submission = submission };
        // A conversation's entry is named for its first part's work, not for the part pressed here.
        subject = await Lands.EntryForWorkAsync(subject, lead.Tree, path, ct, process).ConfigureAwait(false);
        var joined = await JoinEntryAsync(path, session, subject, ct, process).ConfigureAwait(false) with { Served = served, Submission = submission };
        // A tree on a branch of its own lands as that branch, so it is named for its own quest, not its entry's first part's.
        if (joined.EntryBranch is null || !await Entries.FollowsOwnAsync(path, ct).ConfigureAwait(false)) return joined;
        return joined.NamedFor(await BranchPatterns.SubjectAsync(session, quest, find, opening(session)).ConfigureAwait(false));
    }

    /// <inheritdoc cref="TreeLands.EntryForWorkAsync"/>
    public Task<LandingSubject> EntryForWorkAsync(
        LandingSubject entry, string? leadTree, string anyTree, CancellationToken ct = default, IWorkplaceProcess? process = null) =>
        Lands.EntryForWorkAsync(entry, leadTree, anyTree, ct, process);

    /// <inheritdoc cref="EntryLands.PlanAsync"/>
    public Task<EntryPlan> PlanEntryAsync(IReadOnlyList<EntryLandingPart> parts, LandingSubject subject, CancellationToken ct = default) =>
        Entries.PlanAsync(parts, subject, ct);

    /// <inheritdoc cref="TogetherAccepts.LandAsync"/>
    public Task<TreeLanding> LandTogetherAsync(
        string operation, LandingSubject subject, IReadOnlyDictionary<string, IWorkplaceGate?> memberGates, CancellationToken ct = default,
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUse = null, IWorkplaceGate? gate = null,
        Func<bool, CancellationToken, Task<TreeLanding?>>? judged = null,
        Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null, TreeDelivery? delivery = null) =>
        new TogetherAccepts(_context, Lands).LandAsync(operation, subject, memberGates, ct, inUse, gate, judged, beside, delivery);

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
    /// The clean-up: every session branch the proof clears goes, with its tree, and the rest are
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
        // The tidy's own removals are Daoris's ref moves, judged at once by the ref watch with their echo.
        if (pass.Results.Any(result => result.Removed) || pass.Landed.Any(result => result.Removed)) RefMoves.Mine(_context.Home, repository);
        return pass;
    }

    /// <summary>
    /// One repository's end judgement, its line read here as the look's tidy reads it: what the
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
        Func<string, string, string?>? rootOf, Func<FinishedPost, CancellationToken, Task<RecordRefusal?>>? post, Func<DateTimeOffset> clock,
        CancellationToken ct, Func<string, bool>? only = null, Func<bool>? spent = null) =>
        new FinishedRecords(SetAsides).RunAsync(rootOf, post, clock, ct, only, spent);

    /// <summary>
    /// The registry's checkouts, by workspace and repository, for the record pass's git reads outside every hold:
    /// a repository it does not name is unresolved, and its folder waits its days. Null where the registry did not read: then no step runs.
    /// </summary>
    internal static Func<string, string, string?>? RootsOf(IReadOnlyList<RepoView>? registry) => registry is null ? null :
        (workspace, repository) => registry.FirstOrDefault(row =>
            string.Equals(row.Repository, repository, StringComparison.OrdinalIgnoreCase)
            && string.Equals(RemoteTarget.Workspace(row.Workspace), workspace, StringComparison.OrdinalIgnoreCase))?.Root;

    /// <inheritdoc cref="SetAsideKeeping.Run"/>
    internal SetAsideRun KeepSetAside(
        KeepPolicy policy, DateTimeOffset now, TimeSpan budget, CancellationToken ct, Func<string, bool>? purge = null, Func<string, bool>? slim = null) =>
        Keeping.Run(policy, now, budget, ct, purge, slim);

    /// <inheritdoc cref="SetAsideKeeping.RunAsync"/>
    internal Task<SetAsideRun> KeepSetAsideAsync(
        KeepPolicy policy, DateTimeOffset now, TimeSpan budget, CancellationToken ct, Func<string, string, string?>? rootOf,
        Func<string, bool>? purge = null, Func<string, bool>? slim = null) =>
        Keeping.RunAsync(policy, now, budget, ct, purge, slim, rootOf);

    /// <inheritdoc cref="SetAsideKeeping.List"/>
    internal IReadOnlyList<SetAsideListed> ListSetAside(KeepPolicy policy, DateTimeOffset now) => Keeping.List(policy, now);

    /// <inheritdoc cref="SetAsideKeeping.RemoveEarly"/>
    internal (long Bytes, string? Kept) RemoveSetAsideEarly(string folder, bool listedOwn, string? listedClass = null, string? listedRef = null) =>
        Keeping.RemoveEarly(folder, listedOwn, listedClass, listedRef);

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
        CancellationToken ct = default, IReadOnlySet<string>? closedQuests = null) =>
        Cleanups.CleanPlanAsync(repositories, inUse, ct, closedQuests);

    /// <inheritdoc cref="LandedCleanups.SweepPartAsync"/>
    public Task<SweepPlan> SweepPartAsync(
        string repository, string? workspace, string root, IReadOnlySet<string> inUse, CancellationToken ct = default,
        IReadOnlySet<string>? closedQuests = null) =>
        Cleanups.SweepPartAsync(repository, workspace, root, inUse, ct, closedQuests);

    /// <inheritdoc cref="LandedCleanups.CleanAsync"/>
    public Task<SweepDone> CleanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default) =>
        Cleanups.CleanAsync(repositories, inUse, only, ct);

    /// <inheritdoc cref="LandedWithdrawals.WithdrawAsync"/>
    public Task<LandedWithdrawal> WithdrawLandedAsync(
        string root, string repository, string branch, string why, bool plan = false, CancellationToken ct = default) =>
        Withdrawals.WithdrawAsync(root, repository, branch, why, plan, ct);

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
        Func<CancellationToken, Task<IReadOnlySet<string>>>? inUseNow = null, SyncScope? scope = null,
        Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null) =>
        Sync.SyncAsync(repositories, inUse, only, fetch, ct, inUseNow, scope, beside);

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

    /// <summary>
    /// The whole landing gate for this tree as the workplace reads it (<see cref="TreeGates.GateAsync"/>). The review's
    /// <c>SessionTreeGates.GateAsync</c> answers the same gate as the record every other door reads.
    /// </summary>
    public Task<IWorkplaceGate> GateOfAsync(
        string path, string? quest, IReviewWorld world, string? session = null, CancellationToken ct = default, IWorkplaceProcess? process = null,
        string? tip = null, string? run = null) =>
        Gates.GateAsync(path, quest, world, session, ct, process, tip, run);

    /// <inheritdoc cref="TreeGates.ProcessAsync"/>
    public Task<IWorkplaceProcess> ProcessAsync(
        DriverConfig config, string repository, string? workspace, string? quest, IReviewWorld world, CancellationToken ct = default,
        string? run = null) =>
        Gates.ProcessAsync(config, repository, workspace, quest, world, ct, run);

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
    /// One <c>git status</c> of a tree a kept read proved this home's linked tree (<see cref="TreeReadPass"/>), counted;
    /// null where git cannot say. Nothing else is asked, so a caller that has not proved it asks <see cref="UncommittedAsync"/>.
    /// </summary>
    internal Task<int?> StatusCountAsync(string tree, CancellationToken ct = default) => TreeWorkReads.UncommittedOfAsync(tree, ct);

    /// <inheritdoc cref="TreeWorkReads.DeclaredAsync"/>
    internal Task<IReadOnlyList<System.Text.RegularExpressions.Regex>> DeclaredAsync(string tree, string common, CancellationToken ct = default) =>
        WorkReads.DeclaredAsync(tree, common, ct);

    /// <inheritdoc cref="TreeWorkReads.HeldAgainAsync"/>
    internal Task<(HeldWork? Work, IReadOnlyList<System.Text.RegularExpressions.Regex>? Declared)> HeldAgainAsync(
        string tree, string common, HeldWork kept, IReadOnlyList<System.Text.RegularExpressions.Regex>? declared, CancellationToken ct = default) =>
        WorkReads.HeldAgainAsync(tree, common, kept, declared, ct);

    /// <summary>The landings record a tree's walk reads.</summary>
    internal string LandingsPath => _context.Recorded.FilePath;

    /// <summary>The choices a tree's walk reads its line from.</summary>
    internal string ConfigPath => DriverConfig.ResolvePath(_context.Home);

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

    /// <summary>A repository's line as the asks and the tidy read it: the person's choice, else git's guess; null where none.</summary>
    internal async Task<string?> LineOfAsync(string root, string repository, string? workspace, CancellationToken ct = default) =>
        (await _context.LineAsync(root, repository, RemoteTarget.Workspace(workspace), ct).ConfigureAwait(false)).Branch;
}
