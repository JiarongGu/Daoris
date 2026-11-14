namespace Daoris.Driver;

/// <summary>
/// A session's review where its line is pooled, holds no slot and has not landed: the line's tree is nowhere, so the review reads its
/// branch in the repository's checkout. Both reads of a review, the chain and the graph, ask here first, so the two answer alike.
/// </summary>
internal static class PooledBranchReviews
{
    /// <summary>The review of the line's branch against the record's base, or null where the line is none of that or the checkout cannot say.</summary>
    internal static async Task<SessionReview?> ReadAsync(
        SessionTrees trees, string? tree, string? baseCommit, Func<string, CancellationToken, Task<string?>> checkoutOf, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tree) || string.IsNullOrWhiteSpace(baseCommit)) return null;
        var places = new LinePlaces(trees.Context.Home);
        if (places.Of(tree).Stand != LineStand.CanBeBroughtBack) return null;
        var (_, repository) = places.OwnerOf(tree);
        if (await checkoutOf(repository, ct).ConfigureAwait(false) is not { Length: > 0 } checkout) return null;
        if (await trees.BranchNowAsync(tree, ct).ConfigureAwait(false) is not { } branch) return null;

        using (GitLaunch.Within(GitBudget.Landed))
        {
            if (await WorkingTree.TipAsync(checkout, $"refs/heads/{branch}", ct).ConfigureAwait(false) is not { } tip) return null;
            var diff = await WorkingTree.DiffBetweenAsync(checkout, baseCommit, tip, ct).ConfigureAwait(false);
            return diff is null ? null : new SessionReview(diff, FromBranch: true, Landed: null, TreeGone: true, TreeBranch: branch);
        }
    }
}
