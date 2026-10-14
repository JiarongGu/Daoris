namespace Daoris.Driver;

/// <summary>
/// A landed session's review, as <see cref="LandedReviews"/> over the context (PART10k): built once here, with its one-line public
/// call for the terminal and the bridge. The review's records are in <c>Trees/Domain/LandedReview.cs</c>. PART10l folds this part.
/// </summary>
public sealed partial class SessionTrees
{
    private LandedReviews? _reviews;

    /// <summary>A landed session's review (REVIEW2, D113).</summary>
    private LandedReviews Reviews => _reviews ??= new(_context);

    /// <inheritdoc cref="LandedReviews.LandedReviewAsync"/>
    public Task<LandedReview> LandedReviewAsync(string? root, LandedBranch entry, bool changes = true, CancellationToken ct = default) =>
        Reviews.LandedReviewAsync(root, entry, changes, ct);
}
