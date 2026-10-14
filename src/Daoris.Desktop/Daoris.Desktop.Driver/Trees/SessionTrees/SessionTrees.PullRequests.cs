namespace Daoris.Driver;

/// <summary>
/// A landed branch's pull request, asked about and judged, as <see cref="PullRequestAsks"/> (PART10l): built once here, with the
/// terminal's one-line call for <i>Ask again</i>. The tidy's own occasion (<c>TidyCarriedAsync</c>) stays in the tidy's part until
/// PART10l-ii moves it. PART10l-ii folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private PullRequestAsks? _asks;

    /// <summary>The occasions' asks, <i>Ask again</i> and what a kept answer proves (PLUGHOOK1a, PLUGHOOK1c).</summary>
    private PullRequestAsks Asks => _asks ??= new(_context);

    /// <inheritdoc cref="PullRequestAsks.AskAgainAsync"/>
    public Task<PullRequestAskedAgain> AskAgainAsync(string root, LandedBranch entry, CancellationToken ct = default) =>
        Asks.AskAgainAsync(root, entry, ct);
}
