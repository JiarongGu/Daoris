namespace Daoris.Driver;

/// <summary>
/// What one session's own tree holds that nobody has accepted yet, as <see cref="TreeWorkReads"/> (PART10k): built once here, with
/// one-line public calls for the doors and the terminal. PART10l folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private TreeWorkReads? _work;

    /// <summary>What a tree holds that nobody has accepted (SESSUX1a, D126 §2.1).</summary>
    private TreeWorkReads WorkReads => _work ??= new(_context, Squashes, Content, Follows);

    /// <inheritdoc cref="TreeWorkReads.WorkAsync"/>
    public Task<TreeWork?> WorkAsync(string tree, CancellationToken ct = default) => WorkReads.WorkAsync(tree, ct);

    /// <inheritdoc cref="TreeWorkReads.UncommittedAsync"/>
    public Task<int?> UncommittedAsync(string tree, CancellationToken ct = default) => WorkReads.UncommittedAsync(tree, ct);

    /// <inheritdoc cref="TreeWorkReads.DiscardOfferAsync"/>
    public Task<DiscardOffer?> DiscardOfferAsync(string tree, CancellationToken ct = default) => WorkReads.DiscardOfferAsync(tree, ct);
}
