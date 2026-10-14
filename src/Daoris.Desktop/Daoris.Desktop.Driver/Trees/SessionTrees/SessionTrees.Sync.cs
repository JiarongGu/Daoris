namespace Daoris.Driver;

/// <summary>
/// Bringing repositories up to date and the review's range, as classes over the context (PART10j): <see cref="TreeSync"/>,
/// <see cref="BranchJudging"/>, <see cref="BranchReplays"/> and <see cref="ReviewRanges"/>, each built once here, beside the
/// static <see cref="TreeRefs"/> and <see cref="LinePulls"/>. The front keeps its callers' calls, one line each, and its other
/// parts reach the classes through these properties. PART10l folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private TreeSync? _sync;
    private BranchJudging? _judging;
    private BranchReplays? _replays;
    private ReviewRanges? _ranges;

    /// <summary>The look, the press and the scope of bringing up to date (WSR6, WSR7).</summary>
    private TreeSync Sync => _sync ??= new(_context, Asks, Cleanups, Judging, Replays);

    /// <summary>Each branch judged for the replay (WSR6).</summary>
    private BranchJudging Judging => _judging ??= new(_context, Cleanups);

    /// <summary>A judged branch replayed, and the grown record's tidying (WSR6).</summary>
    private BranchReplays Replays => _replays ??= new(_context);

    /// <summary>Where a session tree's review is measured from (REVIEWBASE1).</summary>
    private ReviewRanges Ranges => _ranges ??= new(_context);

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
}
