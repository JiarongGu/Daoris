namespace Daoris.Driver;

/// <summary>
/// What a session's review read (<c>SESSION_DIFF</c>): the committed files and the base they are measured
/// from, whether they were read from the session's own tree or its landed branch, the standing or landed review, the uncommitted part,
/// how the tree stands against its line, the branch its HEAD names and the Accept offer. The route makes its wire answer of this.
/// </summary>
/// <param name="Diff">The committed files: read from the tree, or the landed branch's changes; empty where only dirty work reads.</param>
/// <param name="FromBranch">Read from the landed branch in the repository's checkout, the tree no longer readable.</param>
/// <param name="Landed">The standing landing's review (read from the tree), or the landed review the files came from.</param>
/// <param name="TreeGone">Whether the session's tree is gone from this machine.</param>
/// <param name="Uncommitted">The tree's uncommitted work, where it is read.</param>
/// <param name="Behind">How the tree stands against its line, where the committed range read it.</param>
/// <param name="TreeBranch">The branch the tree's HEAD names; null where detached or not read.</param>
/// <param name="Offer">What the driver would land of the tree, where it offers anything.</param>
public sealed record SessionReview(
    TreeDiff? Diff, bool FromBranch, LandedReview? Landed, bool TreeGone, TreeDiff? Uncommitted = null,
    LineBehind? Behind = null, string? TreeBranch = null, LandOffer? Offer = null);

/// <summary>
/// Review's read of a session, today's chain: what <c>SESSION_DIFF</c> asks, moved here from
/// the route unchanged so the route and the terminal door (<c>daoris git review</c>) read through one code. Each read is
/// asked one after another, as the route asked it; every start is counted through <see cref="GitLaunch"/> in its budget, and the read
/// is said in the machine log as <c>review.read</c>.
/// </summary>
/// <remarks>
/// <para>🔴 <b>No answer changes.</b> The order, the guards and the fallbacks' precedence are the route's own as they stood; row 1a-ii
/// replaces the chain with a graph of the same reads and proves the two answer alike through the route.</para>
///
/// <para><b>Read-only</b>, as the route's remarks say: it runs git's reads in the tree the record names and in the landed
/// branch's checkout, and writes nothing but its log line, which carries no path.</para>
/// </remarks>
public static class SessionReviews
{
    /// <summary>
    /// The review of <paramref name="session"/>, whose record names <paramref name="tree"/> and <paramref name="baseCommit"/>; null where
    /// nothing is reviewable, which the route refuses in its own words. <paramref name="checkoutOf"/> finds a repository's checkout here.
    /// </summary>
    /// <remarks>A git that cannot name a read tree's branch is thrown, as <see cref="WorkingTree.BranchAsync"/> throws it.</remarks>
    public static async Task<SessionReview?> ReadAsync(
        string session, string? tree, string? baseCommit, SessionTrees trees, Func<string, CancellationToken, Task<string?>> checkoutOf,
        MachineLog? log, CancellationToken ct) =>
        (await TimedAsync(session, tree, baseCommit, trees, checkoutOf, log, ct).ConfigureAwait(false)).Review;

    /// <summary>The same, with every git start it made and the graph that made them (the terminal door's <c>--time</c>).</summary>
    internal static async Task<(SessionReview? Review, GitCount Starts, ReviewGraph Graph)> TimedAsync(
        string session, string? tree, string? baseCommit, SessionTrees trees, Func<string, CancellationToken, Task<string?>> checkoutOf,
        MachineLog? log, CancellationToken ct)
    {
        using var count = GitLaunch.Count();
        SessionReview? review;
        var graph = new ReviewGraph();
        // A review writes nothing, so each question its stages share is asked of git once (GitReadOnce).
        using (GitLaunch.Within(GitBudget.Review))
        using (GitReadOnce.Within())
        {
            // Today's reads as a graph, each started once what it needs has answered (ReviewGraphReads).
            review = await ReviewGraphReads.ReadAsync(graph, session, tree, baseCommit, trees, checkoutOf, ct).ConfigureAwait(false);
        }

        log?.Info("review.read", Line(session, review, count, graph));
        return (review, count, graph);
    }

    /// <summary>
    /// The sequential chain, kept as the oracle the graph is held equal to: each read
    /// asked one after another.
    /// </summary>
    internal static async Task<SessionReview?> ChainReadAsync(
        string session, string? tree, string? baseCommit, SessionTrees trees, Func<string, CancellationToken, Task<string?>> checkoutOf,
        CancellationToken ct)
    {
        using (GitLaunch.Within(GitBudget.Review))
        {
            return await ChainAsync(session, tree, baseCommit, trees, checkoutOf, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <c>review.read</c>'s fields: the session, the whole read's time, its depth and starts, each budget's
    /// starts, the wasted ones, the gate's wait and the creation summed, the files and the untracked files read; never a path. The depth and the
    /// wasted starts are the graph's; a read with no graph asked each start after the one before, its depth its starts.
    /// </summary>
    internal static (string Key, object? Value)[] Line(string session, SessionReview? review, GitCount count, ReviewGraph? graph = null)
    {
        var starts = count.Starts;
        return
        [
            ("session", session),
            ("answer", review is null ? "none" : review.FromBranch ? "branch" : "tree"),
            ("ms", (long)GitLaunched.Ms(count.ElapsedTicks)),
            // The graph's longest chain of dependent starts; today's chain asked each after the one before.
            ("depth", graph?.Depth ?? starts.Count),
            ("starts", starts.Count),
            ("review", count.In(GitBudget.Review)),
            ("landed", count.In(GitBudget.Landed)),
            ("offer", count.In(GitBudget.Offer)),
            ("wasted", graph?.Wasted ?? 0),
            ("waitedMs", (long)GitLaunched.Ms(starts.Sum(start => start.WaitedTicks))),
            ("createdMs", (long)GitLaunched.Ms(starts.Sum(start => start.CreatedTicks))),
            ("files", review?.Diff?.Files.Count ?? 0),
            // Uᵣ: the untracked files sent to git before the patch budget stopped them, each one start of its own budget.
            ("untracked", count.In(GitBudget.Untracked)),
        ];
    }

    /// <summary>Whether <paramref name="tree"/> is one this home opened for a session, which ran in it alone; a person's checkout is not.</summary>
    public static bool OwnTree(SessionTrees trees, string? tree)
    {
        if (string.IsNullOrWhiteSpace(tree)) return false;
        try
        {
            return trees.Holds(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    // The route's chain as it stood (TreesRoutes.DiffAsync), each read in its order; the budgets mark whose each start is.
    private static async Task<SessionReview?> ChainAsync(
        string session, string? tree, string? baseCommit, SessionTrees trees, Func<string, CancellationToken, Task<string?>> checkoutOf,
        CancellationToken ct)
    {
        // This machine's landing of the session, standing or a trace: where its work went.
        var landing = trees.Recorded.Landing(session);
        var gone = TreeFolders.TreeGone(tree);

        // What the session's OWN tree holds that no commit does, read apart; never for a conversation in
        // the person's checkout, whose uncommitted work is not knowably the session's.
        var dirty = gone || !OwnTree(trees, tree) ? null : await WorkingTree.UncommittedDiffAsync(tree!, ct).ConfigureAwait(false);

        if (!gone && !string.IsNullOrWhiteSpace(baseCommit))
        {
            // The review measures from the newest commit the work shares with the line: a tree that took
            // in newer line commits (a replay, a merge, a rebase, the line checked out) would otherwise show the line's own
            // changes as the session's work. It says how far the latest line it knows is past the tree, and fetches nothing.
            var range = await trees.ReviewRangeAsync(tree!, baseCommit, ct).ConfigureAwait(false);
            // A chain's step that merged the line reads from a merge of the line's base and its parent's tip, a tree
            // and no commit: the answer still names the commit it is measured from.
            var diff = await WorkingTree.DiffAsync(tree!, range.Reads, ct).ConfigureAwait(false) is { } read ? read with { Base = range.From } : null;
            if (diff is not null)
            {
                LandedReview? standing = null;
                if (landing is not null)
                {
                    var checkout = await checkoutOf(landing.Repository, ct).ConfigureAwait(false);
                    using (GitLaunch.Within(GitBudget.Landed))
                    {
                        standing = await trees.LandedReviewAsync(checkout, landing, changes: false, ct).ConfigureAwait(false);
                    }
                }

                // HEAD's own name, never the layout's (`SessionTrees.BranchNowAsync` falls back to it where detached). Git
                // just read the range from this HEAD, so a refusal to name it is a fault, thrown as `BranchAsync` throws it.
                var branch = await WorkingTree.BranchAsync(tree!, ct).ConfigureAwait(false);
                // The driver's own offer to land this tree (the proof by ancestry and the content hold), read where
                // the work is beyond the line and nothing landed it, so an open conversation is offered exactly what a press would land.
                LandOffer? offer = null;
                if (landing is null && range.Behind is { Ahead: > 0 } && OwnTree(trees, tree))
                {
                    using (GitLaunch.Within(GitBudget.Offer))
                    {
                        offer = await trees.WorkAsync(tree!, ct).ConfigureAwait(false) is { } work ? LandOffer.Of(tree!, work) : null;
                    }
                }

                return new(diff, FromBranch: false, standing, TreeGone: false, dirty, range.Behind, branch, offer);
            }
        }

        if (landing is not null)
        {
            var checkout = await checkoutOf(landing.Repository, ct).ConfigureAwait(false);
            LandedReview review;
            using (GitLaunch.Within(GitBudget.Landed))
            {
                review = await trees.LandedReviewAsync(checkout, landing, changes: true, ct).ConfigureAwait(false);
            }

            return new(review.Changes, FromBranch: true, review, gone);
        }

        // Nothing committed is readable (no base, or a range git cannot read), but the tree holds uncommitted work: that is the review,
        // rather than a refusal that says there is nothing to see.
        if (dirty is { Files.Count: > 0 })
        {
            return new(
                new TreeDiff([], null, ""), FromBranch: false, null, TreeGone: false, dirty,
                TreeBranch: await WorkingTree.BranchAsync(tree!, ct).ConfigureAwait(false));
        }

        return null;
    }
}
